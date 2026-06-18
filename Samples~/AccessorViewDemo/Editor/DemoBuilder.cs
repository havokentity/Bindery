// =============================================================================
// Bindery — Accessor View Demo builder.
// Programmatically creates a sample Canvas hierarchy that matches the
// SettingsPanel example from the README, selects it, then lets the user
// run Tools ▸ Bindery ▸ Generate Accessor Class for Selection to see the
// full code-generation loop in one click.
//
// This script lives in Editor/ so it is stripped from runtime builds.
// =============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Bindery.Samples
{
    internal static class DemoBuilder
    {
        [MenuItem("Tools/Bindery/Samples/Build Accessor View Demo")]
        static void Build()
        {
            // ----------------------------------------------------------------
            // Canvas root
            // ----------------------------------------------------------------
            var canvasGo = new GameObject("BinderyDemo_Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // ----------------------------------------------------------------
            // EventSystem — only one per scene; skip if one already exists.
            // ----------------------------------------------------------------
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
            }

            // ----------------------------------------------------------------
            // SettingsPanel — the object to point Bindery at.
            // ----------------------------------------------------------------
            var panel = MakeRect("SettingsPanel", canvasGo.transform);
            StretchFill(panel.GetComponent<RectTransform>());

            // Title — TextMeshProUGUI
            var titleGo = MakeRect("Title", panel.transform);
            titleGo.AddComponent<TextMeshProUGUI>().text = "Settings";
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.85f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;

            // VolumeSlider — Slider
            var sliderGo = MakeRect("VolumeSlider", panel.transform);
            var slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.75f;
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.1f, 0.6f);
            sliderRt.anchorMax = new Vector2(0.9f, 0.75f);
            sliderRt.offsetMin = sliderRt.offsetMax = Vector2.zero;

            // Footer — plain RectTransform container (no bindable component).
            // Bindery will turn this into a FooterScope because it holds bindable
            // descendants but carries no component of its own.
            var footer = MakeRect("Footer", panel.transform);
            var footerRt = footer.GetComponent<RectTransform>();
            footerRt.anchorMin = new Vector2(0f, 0f);
            footerRt.anchorMax = new Vector2(1f, 0.25f);
            footerRt.offsetMin = footerRt.offsetMax = Vector2.zero;

            // OkButton / CancelButton — Buttons inside Footer
            var okGo = MakeRect("OkButton", footer.transform);
            okGo.AddComponent<Button>();
            var okRt = okGo.GetComponent<RectTransform>();
            okRt.anchorMin = new Vector2(0.55f, 0.2f);
            okRt.anchorMax = new Vector2(0.85f, 0.8f);
            okRt.offsetMin = okRt.offsetMax = Vector2.zero;

            // Optional visual label for OkButton
            var okLabel = MakeRect("Label", okGo.transform);
            okLabel.AddComponent<TextMeshProUGUI>().text = "OK";
            StretchFill(okLabel.GetComponent<RectTransform>());

            var cancelGo = MakeRect("CancelButton", footer.transform);
            cancelGo.AddComponent<Button>();
            var cancelRt = cancelGo.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(0.15f, 0.2f);
            cancelRt.anchorMax = new Vector2(0.45f, 0.8f);
            cancelRt.offsetMin = cancelRt.offsetMax = Vector2.zero;

            var cancelLabel = MakeRect("Label", cancelGo.transform);
            cancelLabel.AddComponent<TextMeshProUGUI>().text = "Cancel";
            StretchFill(cancelLabel.GetComponent<RectTransform>());

            // ----------------------------------------------------------------
            // Select SettingsPanel so the user can run Bindery immediately.
            // ----------------------------------------------------------------
            Selection.activeGameObject = panel;

            Debug.Log("[Bindery] Demo hierarchy built. SettingsPanel is selected — " +
                      "run Tools ▸ Bindery ▸ Generate Accessor Class for Selection to continue.");
        }

        // Validate: always enabled (we want to allow running in any open scene).
        [MenuItem("Tools/Bindery/Samples/Build Accessor View Demo", true)]
        static bool Validate() => true;

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        /// Creates a child GameObject with just a RectTransform and parents it.
        static GameObject MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// Stretches a RectTransform to fill its parent (anchors 0,0 → 1,1, offsets 0).
        static void StretchFill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
#endif
