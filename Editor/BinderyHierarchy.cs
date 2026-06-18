// =============================================================================
// Bindery — reads a selected GameObject's subtree into a ViewModel: one member
// per bindable built-in uGUI child, with case-preserved/deduped C# identifiers,
// mapped accessor types, and nested scopes for containers.
//
// Traversal rule: descend the tree, but STOP at controls (a control is a leaf).
// A node is a member when it carries a bindable component; a node that carries no
// bindable component but has bindable descendants becomes a container *scope*
// (typed RectTransform) that we recurse into. Each member records an "effective
// parent" — the root or the nearest enclosing scope it attaches to.
//
// A child whose name starts with the transparent prefix (e.g. "~") is a wrapper: it
// surfaces nothing itself and its children are promoted to its level (their effective
// parent skips it). Layout-only containers stay out of the generated API this way.
//
// A child that already carries its OWN generated view (attached, or present in the
// generation batch) is a composition boundary: it's surfaced as a member typed as that
// view and we DON'T descend into it — the sub-view owns its subtree.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Bindery
{
    /// <summary>One bindable child surfaced as a serialized, strongly-typed accessor.</summary>
    internal struct ViewMember
    {
        public string identifier;     // case-preserved C# identifier, e.g. "okButton" (may carry @)
        public string csharpType;     // fully-qualified, e.g. "UnityEngine.UI.Button" (RectTransform for scopes)
        public string path;           // transform path relative to the view root (e.g. "Footer/OK") — for comments
        public Transform node;        // live transform (edit-time only)
        public Transform parent;      // node.parent — equals the view root for top-level members
        public bool isScope;          // container with bindable descendants → emits a nested class
        public string scopeTypeName;  // nested class name when isScope (e.g. "FooterScope")
        public bool exposeOnRoot;     // parent == root → accessor lives directly on the view

        /// <summary>Backing-field stem; drops any @ escape: `@class` → key "class", field "_class".</summary>
        public string Key => (identifier ?? "").TrimStart('@');
        public string FieldName => "_" + Key;
    }

    /// <summary>One generated view: the component that sits on the selected GameObject.</summary>
    internal struct ViewModel
    {
        public string className;      // "<RootName>View"
        public Transform root;        // the selected GameObject's transform
        public List<ViewMember> members;
    }

    internal static class BinderyHierarchy
    {
        // Names a generated accessor must NOT take: members it would shadow or clash with on the
        // base class (Object/Component/Behaviour/MonoBehaviour + BinderyView), the Unity message
        // methods a user is likely to add to the stub, and the base private fields a "_<name>"
        // backing field would collide with. A child whose name resolves to one of these is renamed.
        static readonly HashSet<string> ReservedNames = new HashSet<string>(System.StringComparer.Ordinal)
        {
            // UnityEngine.Object / Component / Behaviour / MonoBehaviour
            "name", "hideFlags", "gameObject", "transform", "tag", "enabled", "isActiveAndEnabled",
            "useGUILayout", "runInEditMode",
            "GetComponent", "GetComponents", "GetComponentInChildren", "GetComponentInParent",
            "GetComponentsInChildren", "GetComponentsInParent", "TryGetComponent",
            "CompareTag", "SendMessage", "SendMessageUpwards", "BroadcastMessage",
            "StartCoroutine", "StopCoroutine", "StopAllCoroutines",
            "Invoke", "InvokeRepeating", "CancelInvoke", "IsInvoking", "print",
            // Unity message methods a developer commonly adds alongside OnBind
            "Awake", "Start", "Update", "FixedUpdate", "LateUpdate",
            "OnEnable", "OnDisable", "OnDestroy", "Reset", "OnValidate", "OnGUI",
            // BinderyView's own surface
            "OnBind", "EnsureBound", "IsVisible", "ParentView", "GetParentView",
            // BinderyView private fields, which a "_<name>" backing field would collide with
            "bound", "parentView", "parentResolved",
        };

        /// <summary><paramref name="classSuffix"/> is appended to the (identifier-safe) GameObject
        /// name to form the generated class name — e.g. "View" → SettingsPanelView. A child whose
        /// name starts with <paramref name="transparentPrefix"/> (e.g. "~") is treated as a
        /// transparent wrapper: it surfaces nothing itself and its children are promoted to its
        /// level. See <see cref="BinderySettings.ClassSuffix"/> / <see cref="BinderySettings.TransparentPrefix"/>.</summary>
        public static ViewModel Build(GameObject root, string classSuffix, string transparentPrefix,
                                      IDictionary<Transform, string> batchSubViews = null)
        {
            var rootT = root.transform;
            var collected = new List<ViewMember>();
            Collect(rootT, rootT, rootT, transparentPrefix, batchSubViews, collected);

            // Global dedupe across the whole view → unique backing-field names.
            var rawIds = new List<string>(collected.Count);
            foreach (var m in collected)
                rawIds.Add(IdentifierUtil.ToIdentifier(m.node.name));

            string className = ClassName(root, classSuffix);
            var ids = IdentifierUtil.Dedupe(rawIds, ReservedNames, (orig, renamed) =>
                Debug.LogWarning($"[Bindery] {className}: accessor name '{orig}' is reserved or already used → renamed '{renamed}'."));

            for (int i = 0; i < collected.Count; i++)
            {
                var m = collected[i];
                m.identifier = ids[i];
                collected[i] = m;
            }
            AssignScopeTypeNames(collected);

            return new ViewModel { className = className, root = rootT, members = collected };
        }

        /// <summary>The generated class name for a root: identifier-safe GameObject name + suffix.
        /// Shared with the generator so its batch map of view types matches what Build emits.</summary>
        public static string ClassName(GameObject root, string classSuffix)
            => IdentifierUtil.ToIdentifier(root.name).TrimStart('@')
             + BinderySettings.Sanitize(classSuffix, BinderySettings.DefaultSuffix);

        // <paramref name="effParent"/> is the level a collected member attaches to: the root, or
        // the nearest enclosing real scope. It diverges from node.parent only across a transparent
        // wrapper, whose children are promoted to ITS level (so their effParent skips it).
        static void Collect(Transform node, Transform effParent, Transform root, string prefix,
                            IDictionary<Transform, string> batch, List<ViewMember> outList)
        {
            foreach (Transform child in node)
            {
                var go = child.gameObject;

                if (IsTransparent(child, prefix))
                {
                    // Transparent wrapper: surface nothing for it, promote its children to this
                    // level. A transparent control is excluded outright — we don't descend, so its
                    // internal label/handle can never leak.
                    if (BinderyTypeMap.Classify(go, out _) != BindKind.Control)
                        Collect(child, effParent, root, prefix, batch, outList);
                    continue;
                }

                // Already its own Bindery view (attached now, or being generated in this batch)?
                // Compose it as a typed boundary and STOP — that view owns its own subtree.
                var subType = SubViewTypeOf(child, batch);
                if (subType != null)
                {
                    outList.Add(Leaf(child, effParent, root, subType));
                    continue;
                }

                var kind = BinderyTypeMap.Classify(go, out var type);

                if (kind == BindKind.Control)
                {
                    outList.Add(Leaf(child, effParent, root, type));
                    continue; // control = leaf; never surface its internals
                }

                bool hasBindableDesc = HasBindableDescendant(child, prefix, batch);
                if (!hasBindableDesc)
                {
                    if (kind == BindKind.Graphic) outList.Add(Leaf(child, effParent, root, type));
                    continue; // nothing bindable in or below this node
                }

                // Container with bindable descendants → a scope (typed RectTransform), recurse.
                outList.Add(Scope(child, effParent, root));
                Collect(child, child, root, prefix, batch, outList);
            }
        }

        // Does this subtree contain anything bindable? A control or a composed sub-view counts
        // (and neither is descended into). A transparent wrapper never counts itself, but its
        // children still do (they get promoted up) — unless it's a transparent control (excluded).
        static bool HasBindableDescendant(Transform node, string prefix, IDictionary<Transform, string> batch)
        {
            foreach (Transform child in node)
            {
                if (IsTransparent(child, prefix))
                {
                    if (BinderyTypeMap.Classify(child.gameObject, out _) != BindKind.Control
                        && HasBindableDescendant(child, prefix, batch)) return true;
                    continue;
                }

                if (SubViewTypeOf(child, batch) != null) return true; // a composed sub-view counts

                var kind = BinderyTypeMap.Classify(child.gameObject, out _);
                if (kind == BindKind.Control || kind == BindKind.Graphic) return true;
                if (HasBindableDescendant(child, prefix, batch)) return true;
            }
            return false;
        }

        static bool IsTransparent(Transform t, string prefix)
            => !string.IsNullOrEmpty(prefix) && t.name.StartsWith(prefix, System.StringComparison.Ordinal);

        // The fully-qualified type name of the Bindery view that owns this node — from the current
        // generation batch (component not attached until post-compile) or an already-attached view.
        // Null when the node has no view, so the walker treats it normally.
        static string SubViewTypeOf(Transform child, IDictionary<Transform, string> batch)
        {
            if (batch != null && batch.TryGetValue(child, out var t)) return t;
            var v = child.GetComponent<BinderyView>();
            return v != null ? v.GetType().FullName : null;
        }

        static ViewMember Leaf(Transform node, Transform effParent, Transform root, string type) => new ViewMember
        {
            csharpType = type,
            node = node,
            parent = effParent,
            path = RelativePath(root, node),
            isScope = false,
            exposeOnRoot = effParent == root,
        };

        static ViewMember Scope(Transform node, Transform effParent, Transform root) => new ViewMember
        {
            csharpType = "UnityEngine.RectTransform",
            node = node,
            parent = effParent,
            path = RelativePath(root, node),
            isScope = true,
            exposeOnRoot = effParent == root,
        };

        static void AssignScopeTypeNames(List<ViewMember> members)
        {
            var taken = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var m in members) taken.Add(m.Key);

            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (!m.isScope) continue;

                string baseName = IdentifierUtil.ToIdentifier(m.Key + "Scope").TrimStart('@');
                string typeName = baseName;
                int suffix = 2;
                while (!taken.Add(typeName))
                    typeName = baseName + suffix++;

                m.scopeTypeName = typeName;
                members[i] = m;
            }
        }

        static string RelativePath(Transform root, Transform node)
        {
            var parts = new List<string>();
            var t = node;
            while (t != null && t != root) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
