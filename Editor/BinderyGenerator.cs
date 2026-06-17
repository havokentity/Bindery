// =============================================================================
// Bindery — the entry point. `GameObject ▸ Bindery ▸ Generate Accessor Class`
// (Hierarchy right-click) and `Tools ▸ Bindery ▸ …` both run Generate over the
// current selection: build a ViewModel per selected root, write the generated
// files into Assets/Bindery/Generated/, queue the live object for wiring, then
// Refresh so the new view component compiles. BinderyWire attaches it and fills
// the [SerializeField] references once the type exists.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bindery
{
    internal static class BinderyGenerator
    {
        const string GenRoot = "Assets/Bindery/Generated";

        [MenuItem("GameObject/Bindery/Generate Accessor Class", false, 30)]
        static void GenerateFromHierarchy() => Generate(Selection.gameObjects);

        [MenuItem("GameObject/Bindery/Generate Accessor Class", true)]
        static bool ValidateFromHierarchy() => Selection.activeGameObject != null;

        [MenuItem("Tools/Bindery/Generate Accessor Class for Selection", false)]
        static void GenerateFromTools() => Generate(Selection.gameObjects);

        [MenuItem("Tools/Bindery/Generate Accessor Class for Selection", true)]
        static bool ValidateFromTools() => Selection.activeGameObject != null;

        public static void Generate(GameObject[] roots)
        {
            if (roots == null || roots.Length == 0) return;

            Directory.CreateDirectory(GenRoot);
            bool wroteAsmdef = WriteIfChanged(GenRoot + "/Bindery.Generated.asmdef", BinderyCodeGen.EmitAsmdef());

            string suffix = BinderySettings.ClassSuffix;
            bool queued = false;
            var seen = new HashSet<int>();
            foreach (var go in roots)
            {
                if (go == null || !seen.Add(go.GetInstanceID())) continue;
                if (!go.scene.IsValid())
                {
                    Debug.LogWarning($"[Bindery] '{go.name}' is a project asset, not a scene object — open it in a scene first. Skipped.");
                    continue;
                }

                var model = BinderyHierarchy.Build(go, suffix);
                if (model.members.Count == 0)
                {
                    Debug.LogWarning($"[Bindery] '{go.name}' has no bindable built-in uGUI children — nothing to generate.");
                    continue;
                }

                string genPath = GenRoot + "/" + model.className + ".g.cs";
                WriteIfChanged(genPath, BinderyCodeGen.EmitViewClass(model));
                WriteIfAbsent(GenRoot + "/" + model.className + ".cs", BinderyCodeGen.EmitBehaviourStub(model));

                BinderyWire.Enqueue(model);
                queued = true;
                Debug.Log($"[Bindery] Generated {model.className} ({model.members.Count} members) for '{go.name}'.", go);
            }

            if (!queued && !wroteAsmdef) return;
            AssetDatabase.Refresh();
            // Wire now too: when the generated code is unchanged no compile follows the
            // Refresh, so [DidReloadScripts] never fires — but the type already exists.
            BinderyWire.RequestWire();
        }

        static bool WriteIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path) == content) return false;
            File.WriteAllText(path, content);
            return true;
        }

        static void WriteIfAbsent(string path, string content)
        {
            if (!File.Exists(path)) File.WriteAllText(path, content);
        }
    }
}
