# Accessor View Demo

A minimal end-to-end walkthrough of Bindery's code-generation loop.

## What this sample contains

`Editor/DemoBuilder.cs` — an Editor-only script with the menu item
**Tools ▸ Bindery ▸ Samples ▸ Build Accessor View Demo**.

Running it drops a ready-made Canvas hierarchy into the active scene:

```
SettingsPanel             ← Canvas root → Panel container
├── Title                 (TextMeshProUGUI)
├── VolumeSlider          (Slider)
└── Footer                (container — no component, just a RectTransform)
    ├── OkButton          (Button)
    └── CancelButton      (Button)
```

`SettingsPanel` is automatically selected in the Hierarchy when the builder
finishes, so you can run Bindery immediately without clicking around.

## Walkthrough

1. **Import the sample** via *Package Manager ▸ Bindery ▸ Samples ▸ Accessor
   View Demo ▸ Import*.

2. **Build the hierarchy** — with any scene open, run
   **Tools ▸ Bindery ▸ Samples ▸ Build Accessor View Demo**.
   The demo Canvas appears and `SettingsPanel` is selected.

3. **Generate the view** — run
   **Tools ▸ Bindery ▸ Generate Accessor Class for Selection**
   (or right-click `SettingsPanel` in the Hierarchy and choose
   **Bindery ▸ Generate Accessor Class**).

4. **Inspect the output** — after recompile, `SettingsPanel` will have a
   `SettingsPanelView` component attached with every reference already wired.
   Open `Assets/Bindery/Generated/SettingsPanelView.g.cs` to see the generated
   code and `Assets/Bindery/Views/SettingsPanelView.cs` for the editable stub.

5. **Consume from code** — in your own MonoBehaviour, grab the view and use it:

   ```csharp
   var view = GetComponent<SettingsPanelView>();
   view.EnsureBound();
   view.Title.text = "Settings";
   view.VolumeSlider.value = 0.8f;
   view.Footer.OkButton.onClick.AddListener(Apply);
   ```

## Notes

- The builder is safe to run multiple times — it creates a new Canvas each
  time, so you always get a clean hierarchy to experiment with.
- `Footer` has no bindable component, so Bindery turns it into a `FooterScope`.
  Its children (`OkButton`, `CancelButton`) are reached through that scope:
  `view.Footer.OkButton`.
- To try the `~` transparent-wrapper feature, rename `Footer` to `~Footer` in
  the Hierarchy before generating — Bindery will promote `OkButton` and
  `CancelButton` directly to `view.OkButton` / `view.CancelButton`.
