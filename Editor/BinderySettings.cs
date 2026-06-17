// =============================================================================
// Bindery — PROJECT settings. The generated class name is the GameObject's name
// plus a configurable suffix (default "View"): "SettingsPanel" + "View" →
// SettingsPanelView, or set it to "Blah" → SettingsPanelBlah. Edit it under
//   Project Settings ▸ Bindery ▸ View class suffix
//
// Stored as a ScriptableObject serialized to ProjectSettings/BinderySettings.asset
// — committed to source control and shared by everyone on the project (NOT per-user
// EditorPrefs). It lives in ProjectSettings/ rather than Assets/ because Bindery is
// installed as an immutable package and so can't hold settings inside its own folder.
// The suffix is sanitized to identifier-legal characters on read, so generation
// never produces an invalid class name.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Bindery
{
    internal sealed class BinderySettings : ScriptableObject
    {
        const string AssetPath = "ProjectSettings/BinderySettings.asset";
        public const string DefaultSuffix = "View";
        public const string DefaultTransparentPrefix = "~";

        [SerializeField] string classSuffix = DefaultSuffix;
        [SerializeField] string transparentPrefix = DefaultTransparentPrefix;

        static BinderySettings _instance;

        /// <summary>The single project-wide settings object, loaded from (or defaulted for)
        /// ProjectSettings/BinderySettings.asset.</summary>
        internal static BinderySettings Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var loaded = InternalEditorUtility.LoadSerializedFileAndForget(AssetPath);
                _instance = loaded != null && loaded.Length > 0 ? loaded[0] as BinderySettings : null;
                if (_instance == null)
                {
                    _instance = CreateInstance<BinderySettings>();
                    _instance.classSuffix = DefaultSuffix;
                }
                _instance.hideFlags = HideFlags.HideAndDontSave;
                return _instance;
            }
        }

        static void Save() =>
            InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { Instance }, AssetPath, true);

        /// <summary>Suffix appended to the GameObject name to form the generated class name,
        /// sanitized to identifier-legal characters (falls back to "View" if empty/garbage).</summary>
        public static string ClassSuffix => Sanitize(Instance.classSuffix, DefaultSuffix);

        /// <summary>Name prefix that marks a GameObject as a transparent wrapper — it surfaces
        /// nothing and its children are promoted to its level. Used verbatim (not sanitized); an
        /// empty value turns the feature off (no node is ever treated as transparent).</summary>
        public static string TransparentPrefix => Instance.transparentPrefix ?? "";

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
            return new SettingsProvider("Project/Bindery", SettingsScope.Project)
            {
                label = "Bindery",
                guiHandler = _ =>
                {
                    var s = Instance;
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Code generation", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    string next = EditorGUILayout.DelayedTextField(
                        new GUIContent("View class suffix",
                            "Appended to the GameObject name to form the generated class name. " +
                            "\"View\" → SettingsPanelView,  \"Blah\" → SettingsPanelBlah."),
                        s.classSuffix);
                    if (EditorGUI.EndChangeCheck())
                    {
                        s.classSuffix = next;
                        Save();
                    }

                    EditorGUILayout.LabelField(" ", "Preview:  SettingsPanel" + Sanitize(next, DefaultSuffix));

                    if (GUILayout.Button("Reset to default (\"View\")", GUILayout.Width(220)))
                    {
                        s.classSuffix = DefaultSuffix;
                        Save();
                    }

                    EditorGUILayout.Space();

                    EditorGUI.BeginChangeCheck();
                    string nextPrefix = EditorGUILayout.DelayedTextField(
                        new GUIContent("Transparent prefix",
                            "A child whose name starts with this is treated as a transparent wrapper: " +
                            "it generates nothing and its children are promoted to its level " +
                            "(e.g. \"~Row\" → its buttons appear directly on the parent). Empty turns it off."),
                        s.transparentPrefix);
                    if (EditorGUI.EndChangeCheck())
                    {
                        s.transparentPrefix = nextPrefix;
                        Save();
                    }

                    if (GUILayout.Button("Reset to default (\"~\")", GUILayout.Width(220)))
                    {
                        s.transparentPrefix = DefaultTransparentPrefix;
                        Save();
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox(
                        "Stored in ProjectSettings/BinderySettings.asset — commit it to share the " +
                        "suffix across the team.", MessageType.None);

                    EditorGUI.indentLevel--;
                },
                keywords = new HashSet<string>(new[] { "Bindery", "suffix", "class", "view", "accessor", "codegen" }),
            };
        }
    }
}
