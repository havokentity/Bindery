// =============================================================================
// Bindery — a project panel (Window ▸ Bindery ▸ Views) that lists every generated
// view in the open scene(s) AND in prefab assets, shows whether each has missing
// references, and offers Select / Regenerate / Remove per row plus Regenerate-all
// and Validate. Handy for managing views across a larger project.
//
// Rows are gathered on demand (open + Refresh) since scanning every prefab is not
// free. Scene rows hold the live GameObject; prefab rows hold the asset path (the
// loaded prefab is re-fetched on demand, since asset instances can be unloaded).
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
            w.minSize = new Vector2(380, 200);
            w.Show();
        }

        sealed class Row
        {
            public string typeName;     // generated class name
            public bool isPrefab;       // lives in a prefab asset (vs the open scene)
            public string location;     // scene-relative path, or the prefab asset path
            public GameObject sceneGo;  // the live object for scene rows
            public int missing;         // unwired serialized references
        }

        readonly List<Row> _rows = new List<Row>();
        Vector2 _scroll;

        void OnEnable() => Rescan();
        void OnFocus() => Rescan();

        void Rescan()
        {
            _rows.Clear();

            foreach (var v in FindObjectsByType<BinderyView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (v != null)
                    _rows.Add(new Row { typeName = v.GetType().Name, isPrefab = false, location = HierarchyPath(v.transform), sceneGo = v.gameObject, missing = CountMissing(v) });

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                foreach (var v in root.GetComponentsInChildren<BinderyView>(true))
                    if (v != null)
                        _rows.Add(new Row { typeName = v.GetType().Name, isPrefab = true, location = path, sceneGo = null, missing = CountMissing(v) });
            }

            _rows.Sort((a, b) => string.CompareOrdinal(a.typeName, b.typeName));
            Repaint();
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
                GUILayout.Label($"{_rows.Count} view(s)", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Regenerate All", EditorStyles.toolbarButton))
                    Defer(() => { EditorApplication.ExecuteMenuItem("Tools/Bindery/Regenerate All Views"); Rescan(); });
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton))
                    EditorApplication.ExecuteMenuItem("Tools/Bindery/Validate Views in Scene");
            }

            if (_rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No Bindery views found in the open scene(s) or prefab assets. Generate some, then Refresh.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var r in _rows)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    var prev = GUI.color;
                    GUI.color = r.missing == 0 ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.95f, 0.8f, 0.3f);
                    GUILayout.Label("●", GUILayout.Width(14));
                    GUI.color = prev;

                    EditorGUILayout.LabelField(new GUIContent(r.typeName, r.location), EditorStyles.boldLabel, GUILayout.Width(150));
                    EditorGUILayout.LabelField((r.isPrefab ? "prefab  " : "scene  ") + r.location, EditorStyles.miniLabel);

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
            EditorGUILayout.EndScrollView();
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

        static string HierarchyPath(Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
