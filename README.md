<div align="center">

# ◆ &nbsp;B I N D E R Y

### Select a GameObject. Get a typed C# view of its uGUI children — auto-wired.

![Unity](https://img.shields.io/badge/Unity-2022.3%2B-000?style=for-the-badge&logo=unity)
![uGUI](https://img.shields.io/badge/uGUI-built--in-2196F3?style=for-the-badge)
![Editor](https://img.shields.io/badge/Editor-tool-8B5CF6?style=for-the-badge)
![license](https://img.shields.io/badge/license-MIT-blue?style=for-the-badge)

</div>

---

Bindery is a tiny Unity **Editor** tool. Right-click any GameObject or Canvas in
the Hierarchy, hit **Generate Accessor Class**, and Bindery walks that subtree and
writes you a strongly-typed `partial class` component whose properties point at the
real built-in uGUI controls underneath — `Button`, `Toggle`, `Slider`, `Dropdown`,
`InputField`, `ScrollRect`, `Image`, `RawImage`, `Text`, and `TMP_Text`. It then
**attaches that component to the object and wires every reference for you**, so the
view is ready in the Inspector with nothing left to drag.

No Figma. No reflection at runtime. No string-keyed `Find`. Every accessor is a
plain field read against a `[SerializeField]` reference resolved at edit time.

> [!TIP]
> ### ⚡ The whole loop
> ```text
> 1.  Select a GameObject / Canvas in the Hierarchy
> 2.  Right-click ▸ Bindery ▸ Generate Accessor Class   (or Tools ▸ Bindery ▸ …)
> 3.  A <Name>View component appears on the object, references filled in
> 4.  Use it from code:  view.OkButton.onClick.AddListener(...)
> ```

---

## What you get

Given this hierarchy (you select `SettingsPanel`):

```text
SettingsPanel          ← selected
├── Title              (TextMeshProUGUI)
├── VolumeSlider       (Slider)
└── Footer
    ├── OkButton       (Button)
    └── CancelButton   (Button)
```

Bindery generates `SettingsPanelView.g.cs` (accessor names mirror the GameObject names,
casing preserved):

```csharp
public partial class SettingsPanelView : Bindery.BinderyView
{
    [SerializeField] TMPro.TMP_Text _Title;
    [SerializeField] UnityEngine.UI.Slider _VolumeSlider;
    [SerializeField] UnityEngine.RectTransform _Footer;
    FooterScope _FooterScope;
    [SerializeField] UnityEngine.UI.Button _OkButton;
    [SerializeField] UnityEngine.UI.Button _CancelButton;

    public TMPro.TMP_Text Title => _Title;
    public UnityEngine.UI.Slider VolumeSlider => _VolumeSlider;
    public FooterScope Footer => _FooterScope ??= new FooterScope(this);

    public sealed class FooterScope
    {
        readonly SettingsPanelView _view;
        internal FooterScope(SettingsPanelView view) { _view = view; }
        public RectTransform RectTransform => _view._Footer;
        public GameObject GameObject => _view._Footer != null ? _view._Footer.gameObject : null;
        public UnityEngine.UI.Button OkButton => _view._OkButton;
        public UnityEngine.UI.Button CancelButton => _view._CancelButton;
    }
}
```

…attaches `SettingsPanelView` to the `SettingsPanel` GameObject, and fills
`_Title`, `_VolumeSlider`, `_Footer`, `_OkButton`, `_CancelButton` automatically.

```csharp
view.Title.text = "Settings";
view.VolumeSlider.value = 0.8f;
view.Footer.OkButton.onClick.AddListener(Apply);   // nested container → nested scope
```

## The rules it follows

- **Controls are leaves.** A `Button` / `Toggle` / `Slider` / `Dropdown` /
  `InputField` / `ScrollRect` is surfaced as itself — its internal label,
  background, handle, viewport children are never exposed.
- **Containers become scopes.** A child that holds no bindable component but
  *contains* bindable descendants turns into a nested `…Scope` class (typed
  `RectTransform`) you reach through — `view.Footer.OkButton`.
- **TextMeshPro is surfaced as `TMP_Text`** so the accessor works for any TMP text.
- **Names become C# identifiers**, casing preserved; collisions inside a view get
  `_2` / `_3` suffixes (with a console warning).

## Add your own behaviour

The generator also drops an **editable** stub (`<Name>View.cs`, written only once,
never regenerated) in the same folder so your code shares the assembly with the
generated partial:

```csharp
public partial class SettingsPanelView
{
    protected override void OnBind()   // runs once, before Awake completes
    {
        Footer.OkButton.onClick.AddListener(Apply);
    }
}
```

Regenerate any time (renamed a child, added a control) — the `.g.cs` is rewritten,
your `.cs` is left alone, and the live references are re-wired.

## Settings

**Project Settings ▸ Bindery ▸ View class suffix** — the generated class name is the
GameObject's name plus this suffix (default `View`):

| GameObject | suffix | class |
|---|---|---|
| `SettingsPanel` | `View` (default) | `SettingsPanelView` |
| `SettingsPanel` | `Blah` | `SettingsPanelBlah` |
| `SettingsPanel` | `Bindings` | `SettingsPanelBindings` |

The suffix is sanitized to identifier-legal characters, so generation never produces
an invalid class name. It's stored in `ProjectSettings/BinderySettings.asset` — commit
that file and the whole team shares one suffix (it's a project setting, not a per-user
preference).

## Install

Unity **2022.3+**, with **uGUI** and **TextMeshPro** present.

- **Package Manager ▸ Add package from git URL…**
  `https://github.com/havokentity/Bindery.git`
- or clone into your project's `Packages/` folder.

Bindery depends on `com.unity.ugui`. **TextMeshPro** comes from there automatically on
**Unity 6** (TMP was folded into `com.unity.ugui` 2.x). On **Unity 2022.3**, TMP is the
separate `com.unity.textmeshpro` package — present in new projects by default; add it via
the Package Manager if your project doesn't already have it.

Generated output lands in `Assets/Bindery/Generated/` under its own
`Bindery.Generated` assembly definition, referenceable from your own asmdefs.

## Notes & limits (v1)

- Works on **scene objects and prefab instances**. A prefab *asset* selected in the
  Project window is skipped — open it in a scene first.
- A container that is itself an `Image` (e.g. a card panel) is bound as a
  `RectTransform` scope; its own `Image` is not surfaced separately.
- `ScrollRect` is a leaf — bind its `Content` object directly if you need its items.

## License

MIT © 2026 Rajesh D'Monte
