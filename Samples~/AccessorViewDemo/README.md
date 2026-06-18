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
   view.Title.text = "Settings";                       // touching an accessor binds the view
   view.VolumeSlider.value = 0.8f;
   view.Footer.OkButton.onClick.AddListener(Apply);
   ```

## Things to try next

- **Open the panel** — `Window ▸ Bindery ▸ Views` lists every generated view as a
  tree, with per-view Select / Regenerate / Remove and a checkbox to expose a view
  in the `BinderyViews` registry. `SettingsPanel` is a plain child of the canvas (no
  `Canvas` component of its own), so its view starts **out** of the registry — tick
  its checkbox and `BinderyViews.SettingsPanel` becomes available.
- **Compose a sub-view** — select `Footer` and generate a view on *it* too. The
  parent `SettingsPanelView` recomposes so `view.Footer` becomes a `FooterView`
  (a typed sub-view) instead of a scope.
- **Transparent wrapper** — rename `Footer` to `~Footer` before generating and
  Bindery promotes `OkButton` / `CancelButton` straight onto `view.OkButton` /
  `view.CancelButton` (no `Footer` scope).
- **Rename swap** — rename `SettingsPanel` and regenerate: the old view is removed
  and your editable stub is migrated to the new name, leaving one clean view.

## Notes

- The builder is safe to run multiple times — it creates a new Canvas each time, so
  you always get a clean hierarchy to experiment with.
- `Footer` has no bindable component, so Bindery turns it into a `FooterScope`; its
  children (`OkButton`, `CancelButton`) are reached through it: `view.Footer.OkButton`.
