// =============================================================================
// Bindery — post-compile wiring. Generating a view writes its .cs but the type
// doesn't exist until the next compile, so the live object can't be touched yet.
// Each queued view records, via GlobalObjectId (stable across the domain reload):
// the target object, the generated type name, and per reference the backing-field
// name + the child object + its component type. After scripts reload we resolve
// those ids, attach the generated component (or reuse an existing one), and fill
// each [SerializeField] through a SerializedObject. Entries whose type isn't
// compiled yet are kept and retried on the next reload. Idempotent.
//
// The queue lives in SessionState (survives domain reloads within an editor
// session). Scene objects and prefab instances are supported; prefab *assets*
// selected in the Project window are not (v1).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bindery
{
    internal static class BinderyWire
    {
        const string SessionKey = "Bindery.PendingWire";

        // A single reference (gid + type), OR — when isArray — an ARRAY field populated with each
        // element in order (a serialized collection like Slot0..Slot2 → one T[] field). NOTE: the
        // flag is needed because JsonUtility turns a null string[] into an empty [] on round-trip,
        // so "gids != null" can't tell a single member from an array member.
        [Serializable] class PMember { public string field; public string gid; public string type; public string[] gids; public bool isArray; }
        [Serializable] class PView { public string rootGid; public string typeName; public List<PMember> members = new List<PMember>(); }
        [Serializable] class PSet { public List<PView> views = new List<PView>(); }

        static bool _pending;

        [DidReloadScripts]
        static void OnScriptsReloaded() => RequestWire();

        /// <summary>Capture a generated view for wiring (GlobalObjectIds are read here, while
        /// the objects are live). Replaces any prior entry for the same object + type.</summary>
        public static void Enqueue(ViewModel m)
        {
            var set = Load();
            // Fully-qualified type name must use the SAME namespace the .g.cs was emitted with
            // (BinderySettings.GeneratedNamespace, carried on the model) so ResolveType finds it.
            var ns = string.IsNullOrEmpty(m.namespaceName) ? BinderyCodeGen.GeneratedNamespace : m.namespaceName;
            var pv = new PView
            {
                rootGid = GidOf(m.root.gameObject),
                typeName = ns + "." + m.className,
            };
            foreach (var mem in m.members)
            {
                if (m.collectionsAsArray && mem.IsCollected)
                {
                    if (!mem.collectionLead) continue;   // one array PMember per collection, on the lead
                    pv.members.Add(new PMember { field = "_" + mem.collectionName, type = mem.csharpType, gids = CollectionGids(m, mem), isArray = true });
                }
                else
                    pv.members.Add(new PMember { field = mem.FieldName, gid = GidOf(mem.node.gameObject), type = mem.csharpType });
            }

            set.views.RemoveAll(v => v.rootGid == pv.rootGid && v.typeName == pv.typeName);
            set.views.Add(pv);
            Save(set);
        }

        // The GlobalObjectIds of a collection's elements (the lead's group), in element-index order.
        static string[] CollectionGids(ViewModel m, ViewMember lead)
        {
            var group = new List<ViewMember>();
            foreach (var e in m.members)
                if (e.collectionName == lead.collectionName && e.parent == lead.parent && e.csharpType == lead.csharpType)
                    group.Add(e);
            group.Sort((a, b) => a.collectionIndex.CompareTo(b.collectionIndex));
            var gids = new string[group.Count];
            for (int i = 0; i < group.Count; i++) gids[i] = GidOf(group[i].node.gameObject);
            return gids;
        }

        /// <summary>Process the queue on the next tick (coalesced). Called after a Refresh and
        /// from the script-reload hook.</summary>
        public static void RequestWire()
        {
            if (_pending) return;
            _pending = true;
            EditorApplication.delayCall += () => { _pending = false; Process(); };
        }

        static void Process()
        {
            var set = Load();
            if (set.views.Count == 0) return;

            var remaining = new List<PView>();
            foreach (var v in set.views)
            {
                var type = ResolveType(v.typeName);
                if (type == null) { remaining.Add(v); continue; }     // not compiled yet — retry next reload
                bool wired;
                try { wired = Wire(v, type); }
                catch (Exception e)
                {
                    // A transient failure (e.g. a stale serialized layout mid-recompile) must not abort
                    // the whole pass and lose the queue — keep the view and retry after the next reload.
                    Debug.LogWarning($"[Bindery] wiring '{v.typeName}' failed — retrying after the next reload. ({e.Message})");
                    wired = false;
                }
                if (!wired) { remaining.Add(v); continue; }           // not ready yet — retry next reload
            }

            set.views = remaining;
            Save(set);
        }

        static bool Wire(PView v, Type type)
        {
            var rootGo = ResolveGameObject(v.rootGid);
            if (rootGo == null) return false;

            // Wire only once EVERY member type is compiled. A freshly generated sub-view type may
            // not exist yet on an early (pre-reload) wire pass — wiring then would resolve that
            // reference to null and dequeue the view half-wired. Retrying keeps the queue intact.
            foreach (var m in v.members)
                if (ResolveType(m.type) == null) return false;

            var comp = rootGo.GetComponent(type);
            if (comp == null)
            {
                comp = rootGo.AddComponent(type);
                if (comp == null) return false;
                WarnStaleViews(rootGo, type);
            }

            var so = new SerializedObject(comp);

            // If the live component's compiled type doesn't expose every member yet, it's STALE — a
            // regenerate changed its fields but the domain hasn't reloaded. Wiring the old type now
            // would set the wrong/typed-out refs and dequeue the view; retry after the reload instead.
            foreach (var m in v.members)
                if (so.FindProperty(m.field) == null) return false;

            // Size every array field first and COMMIT — a structural array resize can otherwise drop
            // value writes made to other properties on the same SerializedObject before Apply.
            bool resized = false;
            foreach (var m in v.members)
                if (m.isArray) { so.FindProperty(m.field).arraySize = m.gids.Length; resized = true; }
            if (resized) { so.ApplyModifiedPropertiesWithoutUndo(); so.Update(); }

            // Now assign values: array elements (in order) + single references.
            foreach (var m in v.members)
            {
                var prop = so.FindProperty(m.field);
                if (m.isArray)
                    for (int i = 0; i < m.gids.Length; i++)
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = ResolveReference(m.gids[i], m.type);
                else
                    prop.objectReferenceValue = ResolveReference(m.gid, m.type);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
            // Mark the object's OWN scene dirty (correct under multi-scene editing and in
            // prefab mode, where rootGo.scene is the prefab stage's preview scene) so the
            // wired references are saved. SetDirty alone doesn't flag a scene for saving.
            if (!Application.isPlaying && rootGo.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(rootGo.scene);
            return true;
        }

        // After attaching a freshly-generated view, point out any older generated view still
        // on the object (e.g. left behind by a rename or a class-suffix change) — it's no
        // longer wired and is safe to remove.
        static void WarnStaleViews(GameObject go, Type keep)
        {
            foreach (var v in go.GetComponents<BinderyView>())
            {
                if (v == null || v.GetType() == keep) continue;
                Debug.LogWarning($"[Bindery] '{go.name}' still carries an older generated view " +
                    $"'{v.GetType().Name}'. It is no longer wired — remove it if the object was " +
                    "renamed or the view-class suffix changed.", go);
            }
        }

        static UnityEngine.Object ResolveReference(string gid, string type)
        {
            var go = ResolveGameObject(gid);
            if (go == null) return null;
            var ct = ResolveType(type);
            if (ct == null) return null;                                  // type not compiled — leave unwired
            if (ct == typeof(RectTransform)) return go.GetComponent<RectTransform>();
            return go.GetComponent(ct);                                    // uGUI control/graphic OR a sub-view
        }

        // ---- GlobalObjectId helpers ---------------------------------------------------
        static string GidOf(GameObject go) => GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();

        static GameObject ResolveGameObject(string gid)
        {
            if (string.IsNullOrEmpty(gid) || !GlobalObjectId.TryParse(gid, out var g)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(g) as GameObject;
        }

        // ---- type resolution ----------------------------------------------------------
        static Type ResolveType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        // ---- session-backed queue -----------------------------------------------------
        static PSet Load()
        {
            var json = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(json)) return new PSet();
            try { return JsonUtility.FromJson<PSet>(json) ?? new PSet(); }
            catch { return new PSet(); }
        }

        static void Save(PSet set) => SessionState.SetString(SessionKey, JsonUtility.ToJson(set));
    }
}
