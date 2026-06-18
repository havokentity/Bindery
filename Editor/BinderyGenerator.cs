// =============================================================================
// Bindery — the entry point. `GameObject ▸ Bindery ▸ Generate Accessor Class`
// (Hierarchy right-click) and `Tools ▸ Bindery ▸ …` both run Generate over the
// current selection: build a ViewModel per selected root, write the generated
// files into Assets/Bindery/Generated/, queue the live object for wiring, then
// Refresh so the new view component compiles. BinderyWire attaches it and fills
// the [SerializeField] references once the type exists.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bindery
{
    internal static class BinderyGenerator
    {
        // The asmdef sits at the umbrella root so BOTH the generated .g.cs (Generated/) and the
        // hand-edited stubs (the configurable Views/ folder) land in the one Bindery.Generated
        // assembly — required because a view is a partial class split across those two files.
        const string OutputRoot = "Assets/Bindery";
        const string GeneratedDir = OutputRoot + "/Generated";
        const string AsmdefPath = OutputRoot + "/Bindery.Generated.asmdef";
        const string LegacyAsmdefPath = GeneratedDir + "/Bindery.Generated.asmdef";

        // The shared typed-view registry (BinderyViews.SettingsPanel, …). It lives in — and references
        // view types from — the one generated assembly, so listing only that assembly's views keeps it
        // self-contained and always compilable.
        const string GeneratedAssemblyName = "Bindery.Generated";
        const string RegistryPath = GeneratedDir + "/BinderyViews.g.cs";

        [MenuItem("GameObject/Bindery/Generate Accessor Class", false, 30)]
        static void GenerateFromHierarchy(MenuCommand command)
        {
            // Unity invokes a GameObject/ menu command ONCE PER SELECTED OBJECT (each passed
            // as command.context). Generate already processes the whole selection in one pass,
            // so collapse those N invocations to a single run on the first selected object.
            var sel = Selection.gameObjects;
            if (command.context is GameObject ctx && sel.Length > 1 && ctx != sel[0]) return;
            Generate(sel);
        }

        [MenuItem("GameObject/Bindery/Generate Accessor Class", true)]
        static bool ValidateFromHierarchy() => Selection.activeGameObject != null;

        [MenuItem("Tools/Bindery/Generate Accessor Class for Selection", false)]
        static void GenerateFromTools() => Generate(Selection.gameObjects);

        [MenuItem("Tools/Bindery/Generate Accessor Class for Selection", true)]
        static bool ValidateFromTools() => Selection.activeGameObject != null;

        [MenuItem("Tools/Bindery/Regenerate All Views", false, 20)]
        static void RegenerateAll()
        {
            // Every view in the loaded scene(s) — refresh after a settings change (suffix / namespace /
            // base class) or hierarchy edits. Generate handles the batch (composition, ancestors, wiring).
            var views = UnityEngine.Object.FindObjectsByType<BinderyView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var gos = new List<GameObject>();
            var seen = new HashSet<int>();
            foreach (var v in views)
                if (v != null && seen.Add(v.gameObject.GetInstanceID())) gos.Add(v.gameObject);

            if (gos.Count == 0)
            {
                EditorUtility.DisplayDialog("Bindery", "No Bindery views found in the open scene(s).", "OK");
                return;
            }
            Debug.Log($"[Bindery] Regenerating {gos.Count} view(s) in the open scene(s)…");
            Generate(gos.ToArray());
        }

        [MenuItem("GameObject/Bindery/Remove Accessor Class", false, 31)]
        static void RemoveFromHierarchy(MenuCommand command)
        {
            var sel = Selection.gameObjects;
            if (command.context is GameObject ctx && sel.Length > 1 && ctx != sel[0]) return;
            RemoveView(sel);
        }

        [MenuItem("GameObject/Bindery/Remove Accessor Class", true)]
        static bool ValidateRemoveFromHierarchy() => SelectionHasView();

        [MenuItem("Tools/Bindery/Remove Accessor Class for Selection", false)]
        static void RemoveFromTools() => RemoveView(Selection.gameObjects);

        [MenuItem("Tools/Bindery/Remove Accessor Class for Selection", true)]
        static bool ValidateRemoveFromTools() => SelectionHasView();

        static bool SelectionHasView()
        {
            // Enabled when a view sits anywhere in the selection's subtree — so Remove works on a
            // view-less parent that has nested views (recursive remove offers to cascade).
            foreach (var go in Selection.gameObjects)
                if (go != null && go.GetComponentInChildren<BinderyView>(true) != null) return true;
            return false;
        }

        public static void Generate(GameObject[] roots)
        {
            if (roots == null || roots.Length == 0) return;

            Directory.CreateDirectory(GeneratedDir);
            string viewsDir = ResolveViewsDir();
            Directory.CreateDirectory(viewsDir);

            string namespaceName = BinderySettings.GeneratedNamespace;
            string baseClass = BinderySettings.BaseClass;

            // The asmdef is written AFTER the view models are built (below), so we know which extra
            // assemblies any [BinderyBind] custom components need referenced. Drop the legacy copy now.
            bool wroteAsmdef = RemoveLegacyAsmdef();   // the asmdef used to live in Generated/

            // Assemblies that DEFINE custom-bound ([BinderyBind]) component types used across the
            // generated views — the generated asmdef must reference these or those views won't compile.
            var extraAsmRefs = new HashSet<string>(System.StringComparer.Ordinal);

            string suffix = BinderySettings.ClassSuffix;
            string transparentPrefix = BinderySettings.TransparentPrefix;

            // Worklist = the selection PLUS any ancestor views, so generating a view on a subobject
            // also recomposes the parents that should now hold it as a typed sub-view.
            var worklist = new List<GameObject>();
            var seen = new HashSet<int>();
            var ancestors = new HashSet<int>();
            foreach (var go in roots)
            {
                if (!AddRoot(go, worklist, seen)) continue;
                foreach (var anc in AncestorViews(go))
                    if (AddRoot(anc, worklist, seen)) ancestors.Add(anc.GetInstanceID());
            }

            bool queued = false;
            if (worklist.Count > 0)
            {
                // Deepest first, so a composed sub-view is always WIRED before the parent holding it.
                worklist.Sort((a, b) => Depth(b.transform).CompareTo(Depth(a.transform)));

                // Map every view in this batch to its full type name, so an ancestor's walk can
                // compose a sub-view whose component isn't attached yet (it attaches post-compile).
                var batch = new Dictionary<Transform, string>();
                foreach (var go in worklist)
                    batch[go.transform] = namespaceName + "." + BinderyHierarchy.ClassName(go, suffix);

                foreach (var go in worklist)
                {
                    var model = BinderyHierarchy.Build(go, suffix, transparentPrefix, batch);
                    // Stamp the configured namespace + base class so codegen and BinderyWire emit/resolve
                    // the same fully-qualified type (Build doesn't know these — they're a generator concern).
                    model.namespaceName = namespaceName;
                    model.baseClass = baseClass;
                    model.collectionsAsArray = BinderySettings.SerializeCollectionsAsArray;
                    if (!go.scene.IsValid() && PrefabUtility.IsPartOfPrefabAsset(go))
                    {
                        model.isPrefabAsset = true;
                        model.prefabPath = AssetDatabase.GetAssetPath(go);
                    }
                    if (model.members.Count == 0)
                    {
                        Debug.LogWarning($"[Bindery] '{go.name}' has no bindable children — nothing to generate.");
                        continue;
                    }

                    WriteIfChanged(GeneratedDir + "/" + model.className + ".g.cs", BinderyCodeGen.EmitViewClass(model));
                    WriteStubIfAbsent(model, viewsDir);

                    CollectCustomAssemblies(model, extraAsmRefs);
                    BinderyWire.Enqueue(model);
                    queued = true;
                    string note = ancestors.Contains(go.GetInstanceID()) ? " (recomposed ancestor)" : "";
                    Debug.Log($"[Bindery] Generated {model.className} ({model.members.Count} members) for '{go.name}'{note}.", go);
                }
            }

            // Now that every model is built we know which custom-component assemblies to reference.
            // With none, this emits the same JSON (plus the configured rootNamespace), so behaviour is unchanged.
            wroteAsmdef |= WriteIfChanged(AsmdefPath, BinderyCodeGen.EmitAsmdef(extraAsmRefs, namespaceName));

            if (!queued && !wroteAsmdef) return;
            AssetDatabase.Refresh();
            // Wire now too: when the generated code is unchanged no compile follows the
            // Refresh, so [DidReloadScripts] never fires — but the types already exist.
            BinderyWire.RequestWire();
        }

        // ---- the typed view registry (BinderyViews) ----------------------------------------------

        [DidReloadScripts]
        static void OnScriptsReloaded()
        {
            // Rebuild BinderyViews to match the generated view types now compiled. Deferred a tick so
            // it runs after the reload settles; writing it triggers at most one further reload, which
            // then finds the content unchanged (the registry isn't itself a view) — so it converges.
            EditorApplication.delayCall += () => RegenerateRegistry();
        }

        /// <summary>Regenerate (or, when disabled/empty, delete) the shared <c>BinderyViews</c> registry
        /// so it always matches the current set of generated views. <paramref name="excludeClassNames"/>
        /// drops types about to be deleted — TypeCache still lists them until the next compile, so a
        /// removal must exclude them here or the registry would reference a vanished type and fail to build.</summary>
        internal static void RegenerateRegistry(ICollection<string> excludeClassNames = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!BinderySettings.GenerateViewsRegistry) { DeleteRegistry(); return; }

            var views = CollectRegistryViews(excludeClassNames);
            if (views.Count == 0) { DeleteRegistry(); return; }

            string code = BinderyCodeGen.EmitViewsRegistry(BinderySettings.GeneratedNamespace, views);
            if (WriteIfChanged(RegistryPath, code))
                AssetDatabase.ImportAsset(RegistryPath, ImportAssetOptions.ForceUpdate);
        }

        static void DeleteRegistry()
        {
            if (File.Exists(RegistryPath)) { DeleteAssetIfExists(RegistryPath); AssetDatabase.Refresh(); }
        }

        // Every concrete view type in the one generated assembly, paired with a property name (the class
        // name minus the configured suffix, disambiguated on collision). Restricting to that assembly
        // keeps the registry self-contained: it can always reference what it lists.
        static List<(string typeName, string property)> CollectRegistryViews(ICollection<string> excludeClassNames)
        {
            string baseName = BinderySettings.BaseClass;
            var types = TypeCache.GetTypesDerivedFrom<BinderyView>()
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition
                            && t.Assembly.GetName().Name == GeneratedAssemblyName
                            && t.FullName != baseName
                            && (excludeClassNames == null || !excludeClassNames.Contains(t.Name)))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ThenBy(t => t.FullName, StringComparer.Ordinal)
                .ToList();

            string suffix = BinderySettings.ClassSuffix;
            var props = new string[types.Count];
            for (int i = 0; i < types.Count; i++) props[i] = Stem(types[i].Name, suffix);
            Disambiguate(types, props);

            var list = new List<(string, string)>(types.Count);
            for (int i = 0; i < types.Count; i++)
                list.Add(("global::" + types[i].FullName, props[i]));
            return list;
        }

        // Strip the configured suffix to get the friendly property name ("PanelView" → "Panel").
        static string Stem(string className, string suffix) =>
            !string.IsNullOrEmpty(suffix) && className.Length > suffix.Length &&
            className.EndsWith(suffix, StringComparison.Ordinal)
                ? className.Substring(0, className.Length - suffix.Length)
                : className;

        // On a duplicate stem fall back to the full class name; if that still collides (same simple name
        // in two namespaces) fall back to the dotted full name with separators replaced.
        static void Disambiguate(List<Type> types, string[] props)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var p in props) counts[p] = counts.TryGetValue(p, out var c) ? c + 1 : 1;
                bool clean = true;
                for (int i = 0; i < props.Length; i++)
                {
                    if (counts[props[i]] <= 1) continue;
                    clean = false;
                    props[i] = pass == 0 ? types[i].Name : types[i].FullName.Replace('.', '_').Replace('+', '_');
                }
                if (clean) break;
            }
        }

        // Removes the generated view component from the selected object(s) and DELETES the class
        // files (.g.cs + editable stub). Confirms first (skipped in batch mode). Any ancestor view
        // that composed a removed view is regenerated so it no longer references a deleted type.
        public static void RemoveView(GameObject[] selection)
        {
            if (selection == null) return;

            // Direct: selected objects that carry a view. Nested: views strictly BELOW the selection.
            var direct = new List<(GameObject go, string cls)>();
            var directIds = new HashSet<int>();
            foreach (var go in selection)
            {
                if (go == null || !directIds.Add(go.GetInstanceID())) continue;
                var comp = go.GetComponent<BinderyView>();
                if (comp != null) direct.Add((go, comp.GetType().Name));
            }

            var nested = new List<(GameObject go, string cls)>();
            var nestedSeen = new HashSet<int>(directIds);   // skip the direct ones (and dedupe across selection)
            foreach (var go in selection)
            {
                if (go == null) continue;
                foreach (var v in go.GetComponentsInChildren<BinderyView>(true))
                    if (v != null && nestedSeen.Add(v.gameObject.GetInstanceID()))
                        nested.Add((v.gameObject, v.GetType().Name));
            }

            if (direct.Count == 0 && nested.Count == 0)
            {
                EditorUtility.DisplayDialog("Bindery", "No Bindery view on the selection or below it.", "OK");
                return;
            }

            // Pick the set to remove. When views exist nested below, offer to cascade.
            List<(GameObject go, string cls)> toRemove;
            if (nested.Count == 0)
            {
                if (!Application.isBatchMode &&
                    !EditorUtility.DisplayDialog("Remove Bindery view?", RemoveMessage(direct), "Remove", "Cancel"))
                    return;
                toRemove = direct;
            }
            else if (direct.Count == 0)   // selection has no view itself, only nested ones below
            {
                if (!Application.isBatchMode &&
                    !EditorUtility.DisplayDialog("Remove Bindery views?", RemoveMessage(nested), "Remove", "Cancel"))
                    return;
                toRemove = nested;
            }
            else
            {
                var all = new List<(GameObject go, string cls)>(direct);
                all.AddRange(nested);
                int choice = Application.isBatchMode ? 0 : EditorUtility.DisplayDialogComplex(
                    "Remove Bindery views?",
                    RemoveMessage(all) + "\n\nThere " + (nested.Count == 1 ? "is 1 view" : "are " + nested.Count + " views") +
                        " nested below the selection. Remove everything, or only the selected view(s)?",
                    "Remove all (" + all.Count + ")", "Cancel", "Selected only (" + direct.Count + ")");
                if (choice == 1) return;                   // Cancel
                toRemove = choice == 2 ? direct : all;     // 2 = Selected only, 0 = Remove all
            }

            RemoveTargets(toRemove);
        }

        // The confirmation body listing the views about to be removed.
        static string RemoveMessage(List<(GameObject go, string cls)> targets)
        {
            var msg = new StringBuilder();
            msg.Append("Remove ").Append(targets.Count).Append(targets.Count == 1 ? " Bindery view" : " Bindery views")
               .Append(" and DELETE the generated class file(s)?\n");
            foreach (var t in targets) msg.Append("\n   • ").Append(t.cls).Append("   (on '").Append(t.go.name).Append("')");
            msg.Append("\n\nThis deletes the .g.cs AND the editable view stub (including any code you ")
               .Append("added there). The deleted files cannot be recovered.");
            return msg.ToString();
        }

        // Detach each view component and delete its class files; regenerate any ancestor view that
        // ISN'T itself being removed (so it stops referencing a deleted type). Shared by every path.
        static void RemoveTargets(List<(GameObject go, string cls)> targets)
        {
            var targetIds = new HashSet<int>();
            foreach (var t in targets) targetIds.Add(t.go.GetInstanceID());

            // Ancestor views above the removed set must be regenerated (collected BEFORE removing).
            var regen = new List<GameObject>();
            var regenSeen = new HashSet<int>();
            foreach (var t in targets)
                foreach (var anc in AncestorViews(t.go))
                    if (!targetIds.Contains(anc.GetInstanceID()) && regenSeen.Add(anc.GetInstanceID()))
                        regen.Add(anc);

            // 1) Detach the components (no Undo — the class files go too, so a half-undo would only
            //    resurrect a missing-script component). Prefab-asset views are detached via prefab contents.
            foreach (var t in targets)
            {
                if (!t.go.scene.IsValid() && PrefabUtility.IsPartOfPrefabAsset(t.go))
                    RemoveViewFromPrefab(AssetDatabase.GetAssetPath(t.go), t.cls);
                else
                {
                    var comp = t.go.GetComponent<BinderyView>();
                    if (comp != null) UnityEngine.Object.DestroyImmediate(comp);
                    if (!Application.isPlaying && t.go.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(t.go.scene);
                }
                Debug.Log($"[Bindery] Removed view {t.cls} from '{t.go.name}'.", t.go);
            }

            // 2) Recompose surviving ancestors first — now their .g.cs no longer references the removed
            //    types, so 3) can delete those files without leaving a dangling reference.
            if (regen.Count > 0) Generate(regen.ToArray());

            // 3) Delete the removed views' class files.
            string viewsDir = ResolveViewsDir();
            foreach (var t in targets)
            {
                DeleteAssetIfExists(GeneratedDir + "/" + t.cls + ".g.cs");
                DeleteAssetIfExists(viewsDir + "/" + t.cls + ".cs");
                DeleteAssetIfExists(GeneratedDir + "/" + t.cls + ".cs"); // legacy stub location
            }

            // 4) Drop the removed types from the registry BEFORE the recompile — TypeCache still lists
            //    them, so without the exclude the registry would reference a just-deleted type and break
            //    the build (which would also stop the post-reload regen from ever running).
            RegenerateRegistry(new HashSet<string>(targets.Select(t => t.cls), StringComparer.Ordinal));

            AssetDatabase.Refresh();
        }

        static void DeleteAssetIfExists(string path)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        }

        // Detach a generated view from a prefab ASSET: edit a loaded copy of its contents, destroy the
        // matching view component(s), and save back.
        static void RemoveViewFromPrefab(string prefabPath, string className)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var bv in root.GetComponentsInChildren<BinderyView>(true))
                    if (bv != null && bv.GetType().Name == className)
                        UnityEngine.Object.DestroyImmediate(bv, true);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // Adds a generation root to the worklist (deduped), applying the scene + control-root guards.
        static bool AddRoot(GameObject go, List<GameObject> worklist, HashSet<int> seen)
        {
            if (go == null || !seen.Add(go.GetInstanceID())) return false;
            if (!go.scene.IsValid() && !PrefabUtility.IsPartOfPrefabAsset(go))
            {
                Debug.LogWarning($"[Bindery] '{go.name}' is a project asset that isn't a prefab — open it in a scene first. Skipped.");
                return false;
            }
            if (BinderyTypeMap.Classify(go, out _) == BindKind.Control)
            {
                Debug.LogWarning($"[Bindery] '{go.name}' is itself a uGUI control — a control is a leaf with nothing to bind inside it. Select a container or panel instead. Skipped.");
                return false;
            }
            worklist.Add(go);
            return true;
        }

        // Ancestors that already carry a generated view — those compose this node and must be
        // regenerated so they pick it up as a typed sub-view.
        static System.Collections.Generic.IEnumerable<GameObject> AncestorViews(GameObject go)
        {
            for (var t = go.transform.parent; t != null; t = t.parent)
                if (t.GetComponent<BinderyView>() != null)
                    yield return t.gameObject;
        }

        static int Depth(Transform t)
        {
            int d = 0;
            for (var p = t.parent; p != null; p = p.parent) d++;
            return d;
        }

        // The editable-stub folder from settings, forced under the umbrella so the asmdef covers it
        // (a view's stub + .g.cs are one partial class → must share an assembly).
        static string ResolveViewsDir()
        {
            string dir = "Assets/" + BinderySettings.ViewsFolder;
            if (dir != OutputRoot && !dir.StartsWith(OutputRoot + "/", System.StringComparison.Ordinal))
            {
                Debug.LogWarning($"[Bindery] Editable views folder '{BinderySettings.ViewsFolder}' must be under " +
                    $"'Bindery/' so it shares the generated assembly — falling back to '{BinderySettings.DefaultViewsFolder}'.");
                dir = "Assets/" + BinderySettings.DefaultViewsFolder;
            }
            return dir;
        }

        // The asmdef moved from Generated/ to the umbrella root; drop the old one so the project
        // doesn't end up with two assemblies named "Bindery.Generated".
        static bool RemoveLegacyAsmdef()
        {
            if (!File.Exists(LegacyAsmdefPath)) return false;
            File.Delete(LegacyAsmdefPath);
            if (File.Exists(LegacyAsmdefPath + ".meta")) File.Delete(LegacyAsmdefPath + ".meta");
            return true;
        }

        // Write the one-time editable stub into the Views folder — unless a stub for this class
        // already exists there OR in the legacy Generated/ location (so we never duplicate the
        // partial and clobber the user's code).
        static void WriteStubIfAbsent(ViewModel model, string viewsDir)
        {
            string newPath = viewsDir + "/" + model.className + ".cs";
            string legacyPath = GeneratedDir + "/" + model.className + ".cs";
            if (File.Exists(newPath) || File.Exists(legacyPath)) return;
            File.WriteAllText(newPath, BinderyCodeGen.EmitBehaviourStub(
                model, BinderySettings.ScaffoldButtonHandlers, BinderySettings.ScaffoldControlHandlers));
        }

        // Assembly names already on the generated asmdef (or auto-referenced by Unity), so a member
        // typed from one of these needs no extra reference. Custom [BinderyBind] components live
        // OUTSIDE this set, and only they push a name into extraAsmRefs.
        static readonly HashSet<string> AlreadyReferencedAsms = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "Bindery.Runtime", "UnityEngine.UI", "Unity.TextMeshPro",
            // Unity's auto-referenced predefined modules — RectTransform / CanvasGroup / etc. resolve here.
            "UnityEngine", "UnityEngine.CoreModule", "mscorlib",
            // The generated assembly itself: composed SUB-VIEW members (Bindery.Generated.FooterView)
            // resolve here — never self-reference, that's a cycle.
            "Bindery.Generated",
        };

        // Predefined assemblies an asmdef CANNOT reference — they auto-reference Bindery.Generated, so
        // adding them back makes a cycle. A [BinderyBind] component in one of these must move to its
        // own asmdef; we skip it (with a warning) instead of breaking the whole project's compile.
        static readonly HashSet<string> PredefinedAsms = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "Assembly-CSharp", "Assembly-CSharp-Editor",
            "Assembly-CSharp-firstpass", "Assembly-CSharp-Editor-firstpass",
        };

        // Adds the defining assembly of every custom-bound ([BinderyBind]) member type in this model
        // to <paramref name="acc"/>. A member's csharpType is resolved to a Type; built-in / uGUI / TMP
        // / CanvasGroup types resolve to already-referenced assemblies and are skipped, so only custom
        // components contribute. Unresolved types (not compiled yet) are skipped — picked up on regen.
        static void CollectCustomAssemblies(ViewModel model, HashSet<string> acc)
        {
            foreach (var m in model.members)
            {
                if (m.isScope) continue;                                   // scopes are RectTransform
                var t = ResolveType(m.csharpType);
                if (t == null) continue;
                string asm = t.Assembly.GetName().Name;
                if (AlreadyReferencedAsms.Contains(asm)) continue;
                if (PredefinedAsms.Contains(asm))
                {
                    Debug.LogWarning($"[Bindery] '{m.csharpType}' lives in '{asm}', which the generated " +
                        "assembly can't reference. Move that [BinderyBind] component into its own assembly " +
                        "definition so the view compiles.", model.root);
                    continue;
                }
                acc.Add(asm);
            }
        }

        // Resolve a fully-qualified type name across the loaded assemblies (mirrors BinderyWire's
        // resolver, kept local so the generator doesn't depend on its internals).
        static System.Type ResolveType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;
            var t = System.Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        static bool WriteIfChanged(string path, string content)
        {
            if (File.Exists(path) && File.ReadAllText(path) == content) return false;
            File.WriteAllText(path, content);
            return true;
        }
    }
}
