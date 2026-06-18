// =============================================================================
// Bindery — optional Unity Visual Scripting (Bolt) integration.
//
// Generates a ScriptGraphAsset pre-populated with sample nodes for every Bindery
// view in the open scene(s): a Start event drives, per view, a FindFirstObjectByType
// that grabs the live view and a Debug.Log of it, plus a GetMember node per accessor
// wired to that view — a ready-to-poke "playground" you open in the Script Graph
// window and start dragging from.
//
// This whole assembly compiles ONLY when com.unity.visualscripting is installed:
// the asmdef sets BINDERY_VISUAL_SCRIPTING from a versionDefine and constrains the
// assembly to it, so plain Bindery never takes a hard dependency on Visual Scripting.
// =============================================================================

#if BINDERY_VISUAL_SCRIPTING
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Bindery.VisualScripting
{
    internal static class BinderyVisualScript
    {
        // Keep each view's column readable — a 40-control view shouldn't bury the graph.
        const int MaxAccessorsPerView = 8;
        const float ColumnWidth = 560f;

        [MenuItem("Tools/Bindery/Generate Visual Script Playground")]
        static void GenerateMenu()
        {
            var path = Generate();
            if (path == null) return;
            var asset = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        [MenuItem("Tools/Bindery/Generate Visual Script Playground", true)]
        static bool GenerateMenuValidate() => ViewTypesInScene().Count > 0;

        /// <summary>Build the playground graph for every view type in the open scene(s) and save it.
        /// Returns the created asset path, or null if there are no views.</summary>
        public static string Generate(string path = null)
        {
            var types = ViewTypesInScene();
            if (types.Count == 0)
            {
                Debug.LogWarning("[Bindery] No Bindery views in the open scene(s) — generate some views first, " +
                                 "then create the Visual Script playground.");
                return null;
            }

            var asset = ScriptableObject.CreateInstance<ScriptGraphAsset>();
            var graph = new FlowGraph();
            asset.graph = graph;
            Populate(graph, types);

            path ??= "Assets/" + BinderySettings.ViewsFolder + "/BinderyPlayground.asset";
            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Bindery] Visual Script playground created at {path} for {types.Count} view type(s). " +
                      "Open it with the Script Graph window and play with the nodes.");
            return path;
        }

        static void Populate(FlowGraph graph, List<Type> types)
        {
            var start = new Start { position = new Vector2(-320f, 0f) };
            graph.units.Add(start);
            start.EnsureDefined();

            ControlOutput prevExit = start.trigger;
            int col = 0;
            foreach (var t in types)
            {
                float x = col * ColumnWidth;

                // typeof(t) -> FindFirstObjectByType(Type) -> the live view (typed UnityEngine.Object).
                var typeLit = new Literal(typeof(Type), t) { position = new Vector2(x - 200f, -150f) };
                var find = new InvokeMember(new Member(typeof(UnityEngine.Object), "FindFirstObjectByType",
                    new[] { typeof(Type) })) { position = new Vector2(x, -110f) };
                var logMember = new Member(typeof(Debug), "Log", new[] { typeof(object) });
                var log = new InvokeMember(logMember) { position = new Vector2(x, 20f) };

                graph.units.Add(typeLit); graph.units.Add(find); graph.units.Add(log);
                typeLit.EnsureDefined(); find.EnsureDefined(); log.EnsureDefined();

                graph.valueConnections.Add(new ValueConnection(typeLit.output, find.inputParameters[0]));
                graph.controlConnections.Add(new ControlConnection(prevExit, find.enter));
                graph.valueConnections.Add(new ValueConnection(find.result, log.inputParameters[0]));
                graph.controlConnections.Add(new ControlConnection(find.exit, log.enter));
                prevExit = log.exit;

                // One GetMember node per accessor, target-wired to the found view, so each is live.
                var props = AccessorProps(t);
                int shown = Math.Min(props.Count, MaxAccessorsPerView);
                for (int i = 0; i < shown; i++)
                {
                    var gm = new GetMember(new Member(t, props[i].Name))
                    {
                        position = new Vector2(x + 80f, 150f + i * 70f)
                    };
                    graph.units.Add(gm);
                    gm.EnsureDefined();
                    if (gm.target != null)
                        graph.valueConnections.Add(new ValueConnection(find.result, gm.target));
                }
                if (props.Count > shown)
                    Debug.Log($"[Bindery] {t.Name}: showing {shown} of {props.Count} accessors in the playground " +
                              "(capped for readability — the rest are reachable from the view in code).");
                col++;
            }
        }

        // The generated accessors are exactly the public instance properties declared on the view type
        // itself (BinderyView's IsVisible / ParentView are inherited, so DeclaredOnly skips them).
        static List<PropertyInfo> AccessorProps(Type t) =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
             .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
             .OrderBy(p => p.MetadataToken)
             .ToList();

        static List<Type> ViewTypesInScene()
        {
            var types = new List<Type>();
            var seen = new HashSet<Type>();
            foreach (var v in UnityEngine.Object.FindObjectsByType<BinderyView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (v != null && seen.Add(v.GetType()))
                    types.Add(v.GetType());
            types.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return types;
        }

        static void EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;
            var parts = assetFolder.Split('/');
            var cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
