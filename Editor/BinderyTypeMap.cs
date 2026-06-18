// =============================================================================
// Bindery — maps the built-in uGUI component on a GameObject to the C# accessor
// type Bindery should surface for it, and classifies each node as a control (an
// interactive leaf), a graphic (a display element), or nothing bindable.
//
//   • A CONTROL is a leaf: we never surface its internal label / background /
//     handle children — a Button is just a Button.
//   • A GRAPHIC is a leaf too, UNLESS it nests bindable children (then the reader
//     promotes it to a container scope).
//
// Unity's built-in uGUI + TextMeshPro types are recognised with no configuration,
// plus a CanvasGroup (surfaced as a display/behaviour leaf). Beyond those, any
// MonoBehaviour you mark with [Bindery.BinderyBind] is surfaced as a typed control
// leaf — the one extension point for your own components.
// =============================================================================

using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bindery
{
    internal enum BindKind { None, Control, Graphic }

    internal static class BinderyTypeMap
    {
        /// <summary>Classify a node by the most specific built-in uGUI component it carries.
        /// Controls are checked before graphics (a Button also has an Image — we want Button),
        /// and TMP variants before their legacy counterparts.</summary>
        public static BindKind Classify(GameObject go, out string csharpType)
        {
            csharpType = "UnityEngine.RectTransform";
            if (go == null) return BindKind.None;

            // ---- controls (interactive → treated as leaves) -------------------------
            if (Has<TMP_Dropdown>(go, "TMPro.TMP_Dropdown", ref csharpType)) return BindKind.Control;
            if (Has<Dropdown>(go, "UnityEngine.UI.Dropdown", ref csharpType)) return BindKind.Control;
            if (Has<TMP_InputField>(go, "TMPro.TMP_InputField", ref csharpType)) return BindKind.Control;
            if (Has<InputField>(go, "UnityEngine.UI.InputField", ref csharpType)) return BindKind.Control;
            if (Has<Button>(go, "UnityEngine.UI.Button", ref csharpType)) return BindKind.Control;
            if (Has<Toggle>(go, "UnityEngine.UI.Toggle", ref csharpType)) return BindKind.Control;
            if (Has<Slider>(go, "UnityEngine.UI.Slider", ref csharpType)) return BindKind.Control;
            if (Has<Scrollbar>(go, "UnityEngine.UI.Scrollbar", ref csharpType)) return BindKind.Control;
            if (Has<ScrollRect>(go, "UnityEngine.UI.ScrollRect", ref csharpType)) return BindKind.Control;

            // ---- graphics (display → leaf unless they nest bindable children) -------
            // TextMeshProUGUI is surfaced through its abstract base TMP_Text so the
            // accessor works for any TMP text component.
            if (go.GetComponent<TextMeshProUGUI>() != null) { csharpType = "TMPro.TMP_Text"; return BindKind.Graphic; }
            if (Has<Text>(go, "UnityEngine.UI.Text", ref csharpType)) return BindKind.Graphic;
            if (Has<RawImage>(go, "UnityEngine.UI.RawImage", ref csharpType)) return BindKind.Graphic;
            if (Has<Image>(go, "UnityEngine.UI.Image", ref csharpType)) return BindKind.Graphic;

            // A CanvasGroup is a display/behaviour element — treat it as a graphic leaf so you can
            // drive alpha/interactable through a typed accessor. Like an Image-container, a
            // CanvasGroup on a node that ALSO nests bindable children is absorbed into the scope
            // the reader promotes for that node (the CanvasGroup isn't separately surfaced) — an
            // accepted v1 limitation.
            if (Has<CanvasGroup>(go, "UnityEngine.CanvasGroup", ref csharpType)) return BindKind.Graphic;

            // ---- custom components ([Bindery.BinderyBind]) → typed control leaves ---
            // Your own MonoBehaviour, marked with [BinderyBind], is surfaced like a control:
            // a typed leaf we never descend into. Scan components deterministically and take
            // the first marked one (inherited attribute counts).
            foreach (var comp in go.GetComponents<MonoBehaviour>())
            {
                if (comp == null) continue;                                // missing script
                var t = comp.GetType();
                if (t.GetCustomAttribute<BinderyBindAttribute>(inherit: true) == null) continue;
                csharpType = t.FullName;
                return BindKind.Control;
            }

            return BindKind.None;
        }

        static bool Has<T>(GameObject go, string csharpName, ref string csharpType) where T : Component
        {
            if (go.GetComponent<T>() == null) return false;
            csharpType = csharpName;
            return true;
        }
    }
}
