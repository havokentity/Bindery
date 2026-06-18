// =============================================================================
// Bindery — a project panel (Window ▸ Bindery ▸ Views) that lists every generated
// view in the open scene(s) AND in prefab assets as a NESTED TREE that mirrors view
// composition: a view generated on a child sits under the parent view that composes
// it (SettingsPanelView ▸ FooterView ▸ …). Shows whether each has missing references,
// and offers Select / Regenerate / Remove per row plus Regenerate-all and Validate.
//
// Rows are gathered on demand (open + Refresh) since scanning every prefab is not
// free. Scene rows hold the live GameObject; prefab rows hold the asset path (the
// loaded prefab is re-fetched on demand, since asset instances can be unloaded).
// Nesting is by nearest-ancestor view in the transform hierarchy — exactly the
// composition boundary Bindery uses — so the tree always matches the generated code.
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Bindery
{
    internal sealed class BinderyWindow : EditorWindow
    {
        [MenuItem("Window/Bindery/Views")]
        static void Open()
        {
            var w = GetWindow<BinderyWindow>("Bindery Views");
            w.minSize = new Vector2(420, 200);
            w.Show();
        }

        sealed class Row
        {
            public string typeName;     // generated class name
            public bool isPrefab;       // lives in a prefab asset (vs the open scene)
            public string location;     // scene-relative path, or the prefab asset path
            public string goName;       // the GameObject the view sits on (leaf name)
            public GameObject sceneGo;  // the live object for scene rows
            public int missing;         // unwired serialized references
        }

        // A view in the tree: its row + the views composed beneath it.
        sealed class Node
        {
            public Row row;
            public string id;                 // stable key for fold state
            public int depth;
            public BinderyView comp;          // only used while building (parent linkage)
            public readonly List<Node> children = new List<Node>();
        }

        readonly List<Node> _roots = new List<Node>();
        readonly HashSet<string> _collapsed = new HashSet<string>();   // ids the user folded shut
        int _total;
        bool _hasNesting;
        Vector2 _scroll;

        void OnEnable() => Rescan();
        void OnFocus() => Rescan();

        void Rescan()
        {
            _roots.Clear();
            _total = 0;
            _hasNesting = false;

            var all = new List<Node>();
            var map = new Dictionary<BinderyView, Node>();

            foreach (var v in FindObjectsByType<BinderyView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (v != null) Add(v, false, HierarchyPath(v.transform), all, map);

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (var v in root.GetComponentsInChildren<BinderyView>(true))
                    if (v != null) Add(v, true, path, all, map);
            }

            // Link each view under its nearest ancestor view (same scene/prefab) — the rest are roots.
            foreach (var n in all)
            {
                var parent = NearestAncestorView(n.comp);
                if (parent != null && map.TryGetValue(parent, out var pn)) { pn.children.Add(n); _hasNesting = true; }
                else _roots.Add(n);
            }

            _total = all.Count;
            SortTree(_roots);
            AssignDepth(_roots, 0);
            foreach (var n in all) n.comp = null;   // don't pin prefab-asset components past the scan
            Repaint();
        }

        void Add(BinderyView v, bool isPrefab, string location, List<Node> all, Dictionary<BinderyView, Node> map)
        {
            var n = new Node
            {
                comp = v,
                id = (isPrefab ? "P:" + location + ":" : "S:") + HierarchyPath(v.transform),
                row = new Row
                {
                    typeName = v.GetType().Name,
                    isPrefab = isPrefab,
                    location = location,
                    goName = v.gameObject.name,
                    sceneGo = isPrefab ? null : v.gameObject,
                    missing = CountMissing(v),
                },
            };
            all.Add(n);
            map[v] = n;
        }

        static BinderyView NearestAncestorView(BinderyView v)
        {
            for (var t = v.transform.parent; t != null; t = t.parent)
            {
                var pv = t.GetComponent<BinderyView>();
                if (pv != null) return pv;
            }
            return null;
        }

        static void SortTree(List<Node> nodes)
        {
            nodes.Sort((a, b) => string.CompareOrdinal(a.row.typeName, b.row.typeName));
            foreach (var n in nodes) SortTree(n.children);
        }

        static void AssignDepth(List<Node> nodes, int depth)
        {
            foreach (var n in nodes) { n.depth = depth; AssignDepth(n.children, depth + 1); }
        }

        // Count unwired serialized object references (including inside collection arrays); skips m_Script.
        static int CountMissing(BinderyView v)
        {
            int n = 0;
            var it = new SerializedObject(v).GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script") continue;
                if (it.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (it.objectReferenceValue == null) n++;
                }
                else if (it.isArray)
                {
                    for (int i = 0; i < it.arraySize; i++)
                    {
                        var e = it.GetArrayElementAtIndex(i);
                        if (e.propertyType == SerializedPropertyType.ObjectReference && e.objectReferenceValue == null) n++;
                    }
                }
            }
            return n;
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60))) Rescan();
                GUILayout.Label($"{_total} view(s)", EditorStyles.miniLabel);
                if (_hasNesting)
                {
                    if (GUILayout.Button("Expand", EditorStyles.toolbarButton, GUILayout.Width(56))) _collapsed.Clear();
                    if (GUILayout.Button("Collapse", EditorStyles.toolbarButton, GUILayout.Width(62))) CollapseAll();
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Regenerate All", EditorStyles.toolbarButton))
                    Defer(() => { EditorApplication.ExecuteMenuItem("Tools/Bindery/Regenerate All Views"); Rescan(); });
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton))
                    EditorApplication.ExecuteMenuItem("Tools/Bindery/Validate Views in Scene");
                // Only when the optional Visual Scripting integration is installed (detected by reflection
                // so Bindery.Editor never references Visual Scripting itself).
                if (VisualScriptingAvailable &&
                    GUILayout.Button(new GUIContent("Visual Script", "Generate a Visual Scripting playground graph for these views"), EditorStyles.toolbarButton))
                    Defer(() => EditorApplication.ExecuteMenuItem("Tools/Bindery/Generate Visual Script Playground"));
            }

            if (_total == 0)
            {
                EditorGUILayout.HelpBox("No Bindery views found in the open scene(s) or prefab assets. Generate some, then Refresh.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var n in _roots) DrawNode(n);
            EditorGUILayout.EndScrollView();
        }

        void DrawNode(Node n)
        {
            var r = n.row;
            bool hasChildren = n.children.Count > 0;
            bool expanded = !_collapsed.Contains(n.id);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (n.depth > 0) GUILayout.Space(n.depth * 16);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    if (hasChildren)
                    {
                        if (GUILayout.Button(expanded ? "▼" : "▶", EditorStyles.label, GUILayout.Width(16)))
                        {
                            if (expanded) _collapsed.Add(n.id); else _collapsed.Remove(n.id);
                        }
                    }
                    else GUILayout.Space(16);

                    var prev = GUI.color;
                    GUI.color = r.missing == 0 ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.95f, 0.8f, 0.3f);
                    GUILayout.Label("●", GUILayout.Width(14));
                    GUI.color = prev;

                    EditorGUILayout.LabelField(new GUIContent(r.typeName, r.location), EditorStyles.boldLabel, GUILayout.Width(150));
                    EditorGUILayout.LabelField(new GUIContent((r.isPrefab ? "prefab  " : "scene  ") + r.goName, r.location), EditorStyles.miniLabel);

                    GUILayout.FlexibleSpace();
                    if (r.missing > 0)
                    {
                        var c = GUI.color; GUI.color = new Color(0.95f, 0.8f, 0.3f);
                        GUILayout.Label($"{r.missing} missing", EditorStyles.miniLabel, GUILayout.Width(72));
                        GUI.color = c;
                    }

                    if (GUILayout.Button("Select", GUILayout.Width(56))) Select(r);
                    if (GUILayout.Button("Regen", GUILayout.Width(56))) Defer(() => { var go = TargetOf(r); if (go) { BinderyGenerator.Generate(new[] { go }); Rescan(); } });
                    if (GUILayout.Button("Remove", GUILayout.Width(62))) Defer(() => { var go = TargetOf(r); if (go) { BinderyGenerator.RemoveView(new[] { go }); Rescan(); } });
                }
            }

            if (hasChildren && expanded)
                foreach (var c in n.children) DrawNode(c);
        }

        void CollapseAll()
        {
            _collapsed.Clear();
            AddCollapsible(_roots);
        }

        void AddCollapsible(List<Node> nodes)
        {
            foreach (var n in nodes)
            {
                if (n.children.Count > 0) _collapsed.Add(n.id);
                AddCollapsible(n.children);
            }
        }

        GameObject TargetOf(Row r) => r.isPrefab ? AssetDatabase.LoadAssetAtPath<GameObject>(r.location) : r.sceneGo;

        void Select(Row r)
        {
            Object obj = r.isPrefab ? (Object)AssetDatabase.LoadAssetAtPath<GameObject>(r.location) : r.sceneGo;
            if (obj == null) return;
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        // Regenerate/Remove touch the asset database and can recompile — defer past OnGUI.
        static void Defer(System.Action a) => EditorApplication.delayCall += () => a();

        // True when com.unity.visualscripting is installed — checked by reflection so this assembly
        // never references Visual Scripting (the generator lives in an optional, constraint-gated asmdef).
        static bool? _vs;
        static bool VisualScriptingAvailable =>
            _vs ??= System.Type.GetType("Unity.VisualScripting.ScriptGraphAsset, Unity.VisualScripting.Flow") != null;

        static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
