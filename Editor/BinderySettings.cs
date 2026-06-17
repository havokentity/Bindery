// =============================================================================
// Bindery — user settings. The generated class name is the GameObject's name plus
// a configurable suffix (default "View"): "SettingsPanel" + "View" → SettingsPanelView,
// or set it to "Blah" → SettingsPanelBlah. Edit it under
//   Preferences ▸ Bindery ▸ View class suffix
// Stored in EditorPrefs (per machine/user). The suffix is sanitized to identifier-
// legal characters on read, so generation never produces an invalid class name.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Bindery
{
    internal static class BinderySettings
    {
        const string SuffixKey = "Bindery.ClassSuffix";
        public const string DefaultSuffix = "View";

        /// <summary>Suffix appended to the GameObject name to form the generated class name,
        /// sanitized to identifier-legal characters (falls back to "View" if empty/garbage).</summary>
        public static string ClassSuffix
        {
            get => Sanitize(EditorPrefs.GetString(SuffixKey, DefaultSuffix), DefaultSuffix);
            set => EditorPrefs.SetString(SuffixKey, value ?? "");
        }

        // Keep only characters legal inside a C# identifier; the suffix is appended to an
        // already-valid name, so a leading digit here is fine.
        public static string Sanitize(string raw, string fallback)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')
                    sb.Append(c);
            return sb.Length == 0 ? fallback : sb.ToString();
        }

        [SettingsProvider]
        static SettingsProvider Create()
        {
            return new SettingsProvider("Preferences/Bindery", SettingsScope.User)
            {
                label = "Bindery",
                guiHandler = _ =>
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Code generation", EditorStyles.boldLabel);

                    string current = EditorPrefs.GetString(SuffixKey, DefaultSuffix);
                    EditorGUI.BeginChangeCheck();
                    string next = EditorGUILayout.TextField(
                        new GUIContent("View class suffix",
                            "Appended to the GameObject name to form the generated class name. " +
                            "\"View\" → SettingsPanelView,  \"Blah\" → SettingsPanelBlah."),
                        current);
                    if (EditorGUI.EndChangeCheck())
                        EditorPrefs.SetString(SuffixKey, next);

                    EditorGUILayout.LabelField(" ", "Preview:  SettingsPanel" + Sanitize(next, DefaultSuffix));

                    if (GUILayout.Button("Reset to default (\"View\")", GUILayout.Width(220)))
                        EditorPrefs.SetString(SuffixKey, DefaultSuffix);

                    EditorGUI.indentLevel--;
                },
                keywords = new HashSet<string>(new[] { "Bindery", "suffix", "class", "view", "accessor", "codegen" }),
            };
        }
    }
}
