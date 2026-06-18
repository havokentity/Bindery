// =============================================================================
// Bindery — inspector for generated views. Draws the wired references as usual, warns
// when any has gone missing (the bound object was renamed / moved / deleted), and adds
// Regenerate / Remove buttons so the whole loop is reachable from the component itself.
// Applies to every <Name>View (editorForChildClasses: true).
// =============================================================================

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Bindery
{
    [CustomEditor(typeof(BinderyView), true)]
    [CanEditMultipleObjects]
    internal sealed class BinderyViewEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            int missing = CountMissingRefs();
            if (missing > 0)
                EditorGUILayout.HelpBox(
                    missing + (missing == 1 ? " bound reference is" : " bound references are") +
                    " missing — the object may have been renamed, moved, or deleted. Regenerate to re-resolve.",
                    MessageType.Warning);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                // Defer to a delayCall: Regenerate/Remove touch the asset database and can trigger a
                // recompile, which must not happen mid-OnInspectorGUI.
                if (GUILayout.Button("Regenerate"))
                {
                    var gos = TargetObjects();
                    EditorApplication.delayCall += () => BinderyGenerator.Regenerate(gos);
                }
                if (GUILayout.Button("Remove View…"))
                {
                    var gos = TargetObjects();
                    EditorApplication.delayCall += () => BinderyGenerator.RemoveView(gos);
                }
            }
        }

        int CountMissingRefs()
        {
            int missing = 0;
            var it = serializedObject.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.propertyType == SerializedPropertyType.ObjectReference
                    && it.name != "m_Script" && it.objectReferenceValue == null)
                    missing++;
            }
            return missing;
        }

        GameObject[] TargetObjects()
        {
            var list = new List<GameObject>();
            foreach (var t in targets)
                if (t is Component c && c != null) list.Add(c.gameObject);
            return list.ToArray();
        }
    }
}
