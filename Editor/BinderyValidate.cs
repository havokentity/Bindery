// =============================================================================
// Bindery — scene-wide validation command. `Tools ▸ Bindery ▸ Validate Views in
// Scene` iterates every BinderyView in all loaded scenes (including inactive
// objects) and reports any components whose serialized object-reference fields
// have gone null — the typical symptom of a renamed, moved, or deleted child.
// Each warning is context-linked so clicking it pings the offending object.
// =============================================================================

using UnityEditor;
using UnityEngine;

namespace Bindery
{
    internal static class BinderyValidate
    {
        [MenuItem("Tools/Bindery/Validate Views in Scene", false, 100)]
        static void ValidateScene()
        {
            var views = Object.FindObjectsByType<BinderyView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (views.Length == 0)
            {
                Debug.Log("[Bindery] No Bindery views found in the scene.");
                return;
            }

            int ok = 0, broken = 0;
            foreach (var view in views)
            {
                int missing = CountMissingRefs(view);
                if (missing == 0) { ok++; continue; }

                broken++;
                string path = HierarchyPath(view.transform);
                Debug.LogWarning(
                    $"[Bindery] {view.GetType().Name} on '{path}': " +
                    $"{missing} reference(s) missing.", view);
            }

            string summary = $"[Bindery] Validated {views.Length} view(s): " +
                             $"{ok} ok, {broken} with missing references.";
            if (broken > 0)
                Debug.LogWarning(summary);
            else
                Debug.Log(summary);
        }

        [MenuItem("Tools/Bindery/Validate Views in Selection", false, 101)]
        static void ValidateSelection()
        {
            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                Debug.Log("[Bindery] Nothing selected.");
                return;
            }

            int total = 0, ok = 0, broken = 0;
            foreach (var go in selection)
            {
                if (go == null) continue;
                // Include views on the root and all descendants.
                var views = go.GetComponentsInChildren<BinderyView>(true);
                foreach (var view in views)
                {
                    total++;
                    int missing = CountMissingRefs(view);
                    if (missing == 0) { ok++; continue; }

                    broken++;
                    string path = HierarchyPath(view.transform);
                    Debug.LogWarning(
                        $"[Bindery] {view.GetType().Name} on '{path}': " +
                        $"{missing} reference(s) missing.", view);
                }
            }

            if (total == 0)
            {
                Debug.Log("[Bindery] No Bindery views found in the selection.");
                return;
            }

            string summary = $"[Bindery] Validated {total} view(s) in selection: " +
                             $"{ok} ok, {broken} with missing references.";
            if (broken > 0)
                Debug.LogWarning(summary);
            else
                Debug.Log(summary);
        }

        [MenuItem("Tools/Bindery/Validate Views in Selection", true)]
        static bool ValidateSelectionValidate() => Selection.activeGameObject != null;

        // ---- helpers --------------------------------------------------------

        // Counts serialized object-reference fields that are null, mirroring the
        // same check used by BinderyViewEditor to show the inspector warning.
        static int CountMissingRefs(BinderyView view)
        {
            int missing = 0;
            var so = new SerializedObject(view);
            var it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.propertyType == SerializedPropertyType.ObjectReference
                    && it.name != "m_Script"
                    && it.objectReferenceValue == null)
                    missing++;
            }
            return missing;
        }

        // Returns the full hierarchy path, e.g. "Canvas/Panel/OkButton".
        static string HierarchyPath(Transform t)
        {
            var parts = new System.Collections.Generic.Stack<string>();
            for (var cur = t; cur != null; cur = cur.parent)
                parts.Push(cur.name);
            return string.Join("/", parts);
        }
    }
}
