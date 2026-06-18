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
        public const string DefaultViewsFolder = "Bindery/Views";

        [SerializeField] string classSuffix = DefaultSuffix;
        [SerializeField] string transparentPrefix = DefaultTransparentPrefix;
        [SerializeField] string viewsFolder = DefaultViewsFolder;
        [SerializeField] bool scaffoldButtonHandlers = true;
        [SerializeField] bool scaffoldControlHandlers = true;

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

        /// <summary>Project-relative folder (under Assets/) for the editable, hand-written view stubs
        /// — kept apart from the regenerated <c>.g.cs</c>. Must live under the generated assembly's
        /// root ("Bindery/"); a value outside it falls back to the default.</summary>
        public static string ViewsFolder
        {
            get
            {
                var f = (Instance.viewsFolder ?? "").Trim().Trim('/');
                return string.IsNullOrEmpty(f) ? DefaultViewsFolder : f;
            }
        }

        /// <summary>When generating a new view stub, pre-wire each Button's onClick to a named handler
        /// method (with its own body). On by default.</summary>
        public static bool ScaffoldButtonHandlers => Instance.scaffoldButtonHandlers;

        /// <summary>When generating a new view stub, pre-wire each non-button control's basic event
        /// (Toggle/Slider/Dropdown/InputField/… onValueChanged) to a named handler. On by default.</summary>
        public static bool ScaffoldControlHandlers => Instance.scaffoldControlHandlers;

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

                    EditorGUI.BeginChangeCheck();
                    string nextViews = EditorGUILayout.DelayedTextField(
                        new GUIContent("Editable views folder",
                            "Project-relative folder (under Assets/) for the hand-edited view stubs, " +
                            "kept apart from the generated .g.cs. Must be under \"Bindery/\" so it shares " +
                            "the generated assembly. Default \"Bindery/Views\"."),
                        s.viewsFolder);
                    if (EditorGUI.EndChangeCheck())
                    {
                        s.viewsFolder = nextViews;
                        Save();
                    }

                    if (GUILayout.Button("Reset to default (\"Bindery/Views\")", GUILayout.Width(260)))
                    {
                        s.viewsFolder = DefaultViewsFolder;
                        Save();
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("New view stubs", EditorStyles.boldLabel);

                    EditorGUI.BeginChangeCheck();
                    bool nextBtn = EditorGUILayout.ToggleLeft(
                        new GUIContent("Scaffold button click handlers",
                            "When a view stub is first generated, pre-wire each Button's onClick to a " +
                            "named handler method (with its own empty body) in OnBind()."),
                        s.scaffoldButtonHandlers);
                    bool nextCtrl = EditorGUILayout.ToggleLeft(
                        new GUIContent("Scaffold control event handlers",
                            "Same, for other controls' basic events — Toggle/Slider/Scrollbar/Dropdown/" +
                            "InputField onValueChanged, ScrollRect onValueChanged."),
                        s.scaffoldControlHandlers);
                    if (EditorGUI.EndChangeCheck())
                    {
                        s.scaffoldButtonHandlers = nextBtn;
                        s.scaffoldControlHandlers = nextCtrl;
                        Save();
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.HelpBox(
                        "Stored in ProjectSettings/BinderySettings.asset — commit it to share these " +
                        "settings across the team.", MessageType.None);

                    EditorGUI.indentLevel--;
                },
                keywords = new HashSet<string>(new[] { "Bindery", "suffix", "class", "view", "accessor", "codegen" }),
            };
        }
    }
}
