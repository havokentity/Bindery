<div align="center">

# ◆ &nbsp;B I N D E R Y

### Select a GameObject. Get a typed C# view of its uGUI children — auto-wired.

![Unity](https://img.shields.io/badge/Unity-2022.3%2B-000?style=for-the-badge&logo=unity)
![uGUI](https://img.shields.io/badge/uGUI-built--in-2196F3?style=for-the-badge)
![Editor](https://img.shields.io/badge/Editor-tool-8B5CF6?style=for-the-badge)
![license](https://img.shields.io/badge/license-MIT-blue?style=for-the-badge)

![CI](https://github.com/havokentity/Bindery/actions/workflows/validate.yml/badge.svg)

</div>

---

Bindery is a tiny Unity **Editor** tool. Right-click any GameObject or Canvas in
the Hierarchy, hit **Generate Accessor Class**, and Bindery walks that subtree and
writes you a strongly-typed `partial class` component whose properties point at the
real built-in uGUI controls underneath — `Button`, `Toggle`, `Slider`, `Dropdown`,
`InputField`, `ScrollRect`, `Image`, `RawImage`, `Text`, `TMP_Text`, `CanvasGroup` — and
**your own components** marked `[BinderyBind]`. It then **attaches that component to the
object and wires every reference for you**, so the view is ready in the Inspector with
nothing left to drag.

No reflection at runtime. No string-keyed `Find`. Every accessor is a plain field
read against a `[SerializeField]` reference resolved at edit time.

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
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Bindery.Generated
{
    public partial class SettingsPanelView : Bindery.BinderyView
    {
        [SerializeField] TMP_Text _Title;
        [SerializeField] Slider _VolumeSlider;
        [SerializeField] RectTransform _Footer;
        FooterScope _FooterScope;
        [SerializeField] Button _OkButton;
        [SerializeField] Button _CancelButton;

        public TMP_Text Title => _Title;
        public Slider VolumeSlider => _VolumeSlider;
        public FooterScope Footer => _FooterScope ??= new FooterScope(this);

        public sealed class FooterScope
        {
            readonly SettingsPanelView _view;
            internal FooterScope(SettingsPanelView view) { _view = view; }
            public RectTransform RectTransform => _view._Footer;
            public Button OkButton => _view._OkButton;
            public Button CancelButton => _view._CancelButton;
        }
    }
}
```

Built-in uGUI / TMP types are emitted bare (`Button`, not `UnityEngine.UI.Button`) under the file's
`using`s; your own `[BinderyBind]` components stay fully qualified. Each accessor also calls
`EnsureBound()` before returning (elided above for brevity — see [Binding is lazy](#binding-is-lazy--inactive-views-included)).

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
- **Sub-views compose.** If a descendant already has its *own* generated view, the
  parent surfaces it as that typed view (not a scope) and stops there —
  `view.Footer` is a `FooterView`. See *Composing views*.
- **`~`-prefixed nodes are transparent.** Prefix a GameObject's name with `~` and it
  generates *nothing* — its children are promoted to its level. A layout-only
  `~ButtonRow` gives you `view.OkButton`, not `view.ButtonRow.OkButton`. (On a leaf it
  just means "skip this one.") The prefix is configurable — see *Settings*.
- **TextMeshPro is surfaced as `TMP_Text`** so the accessor works for any TMP text.
- **Your own components bind too.** Mark a MonoBehaviour with `[Bindery.BinderyBind]` and it's
  surfaced as a typed leaf — `CanvasGroup` is recognized out of the box. See *Custom components*.
- **Repeated siblings collapse into a collection.** `Slot0, Slot1, Slot2` (same type, shared stem +
  index) become one ordered `view.Slots` (`IReadOnlyList<Button>`) instead of three accessors,
  serialized as a single array (a list in the Inspector). See *Collections* in *Settings*.
- **Names become C# identifiers**, casing preserved; collisions — with each other *or* with a
  base-class member (`transform`, `IsVisible`, `Awake`, …) — get `_2` / `_3` suffixes (with a
  console warning), so generation never shadows a base member or fails to compile.

## Add your own behaviour

The generator also drops an **editable** stub (`<Name>View.cs`, written only once,
never regenerated) in the **`Bindery/Views`** folder — apart from the regenerated
`.g.cs`, but in the same assembly so it's the same partial class. By default it comes
**pre-wired**: each control's basic event is hooked to a named handler method with its own
body, ready for you to fill in:

```csharp
public partial class SettingsPanelView
{
    protected override void OnBind()   // runs once, on first access (or Awake) — see below
    {
        Footer.OkButton.onClick.AddListener(OnOkButtonClicked);
        VolumeSlider.onValueChanged.AddListener(OnVolumeSliderChanged);
        // IsVisible = false;          // hide the whole view; flip back with IsVisible = true
    }

    void OnOkButtonClicked()
    {
        // TODO: handle OkButton click
    }

    void OnVolumeSliderChanged(float value)
    {
        // TODO: handle VolumeSlider value change
    }
}
```

Buttons get `onClick`; Toggle/Slider/Scrollbar/Dropdown/InputField/ScrollRect get
`onValueChanged`. **Collections** scaffold an indexed loop into a single handler —
`for (…) Slots[i].onClick.AddListener(() => OnSlotsClicked(index));` with `void OnSlotsClicked(int index)`.
Turn either group off in *Settings*. (Since the stub is written once, the scaffolding reflects the
controls present at first generation.)

Regenerate any time (renamed a child, added a control) — the `.g.cs` is rewritten,
your `.cs` is left alone, and the live references are re-wired.

### Binding is lazy — inactive views included

`OnBind()` runs **once, on first touch** — every generated accessor calls `EnsureBound()` before
handing back its reference. So the moment any code reaches a view's member — from any `Awake` /
`Start`, in any script order, through `BinderyViews`, a sub-view, or a held reference — the view
binds itself. Crucially this works **even while the view's GameObject is inactive**, where Unity
never calls `Awake`: `someInactiveView.OkButton.onClick.AddListener(…)` both resolves the (always
deserialized) reference *and* runs the view's own `OnBind`. It's idempotent — `Awake` and the
accessors all funnel through the same one-time `EnsureBound`. (The one thing that can't run early on
an inactive object is `OnBind` work that *needs* it active, e.g. `StartCoroutine` — a Unity limit.)

#### Why a listener you add to an inactive view's control still fires when it's shown

A surprisingly handy consequence: from an **active** object you can reach a control on an
**inactive** Bindery view, add a listener, and it fires the moment the view is shown — no extra
wiring. Three plain Unity facts make that work (no reflection, no tricks):

1. **The reference is always there.** Bindery wires each control into a `[SerializeField]` field at
   edit time, and Unity deserializes serialized fields when the scene loads **whether or not the
   GameObject is active** — inactive objects get their data, they just don't run lifecycle callbacks.
   So `_OkButton` points at a real `Button` even while the panel is hidden.
2. **`BinderyViews` finds inactive views on purpose** — the lookup is
   `FindFirstObjectByType<…>(FindObjectsInactive.Include)`, so the view resolves even while hidden.
3. **The listener lives on the *Button*, and survives deactivation.** `onClick.AddListener(…)`
   stores a runtime callback on the Button's `UnityEvent`. Deactivating and re-activating a
   GameObject does **not** clear runtime listeners — so when the panel is shown and clicked, the
   callback fires. (It simply *can't* fire while hidden — you can't click inactive UI.)

Two gotchas worth knowing when you lean on this:

- **Duplicate listeners.** If the code that calls `AddListener` runs again — the object is
  reinstantiated, an additive scene reloads, etc. — you'll register the handler a *second* time and
  it fires twice. Wire it once, or `RemoveListener(...)` first.
- **Stale registry cache on scene reload.** `BinderyViews` caches each view it finds; after loading
  a different scene that cache can hold a destroyed reference. Call **`BinderyViews.Refresh()`** to
  clear it (each property re-finds on next access). A destroyed/`null` cached entry is re-found
  automatically — `Refresh()` is for proactively dropping the whole set across a scene change.

## Composing views

Generate a view on a child that already lives inside another view, and the parent
**composes** it instead of re-walking its subtree. Say `SettingsPanel` has a
`SettingsPanelView` and you also generate a view on its `Footer`:

```text
SettingsPanel   (SettingsPanelView)
└── Footer       (FooterView)   ← generate here too
    ├── OkButton
    └── CancelButton
```

The parent stops at that boundary and exposes the child as its typed view:

```csharp
public FooterView Footer => _Footer;                 // a FooterView, not a FooterScope

settingsPanel.Footer.OkButton.onClick.AddListener(Apply);          // down, through the real view
footer.GetParentView<SettingsPanelView>().VolumeSlider.value = 1f;  // up, back to the parent
```

- **Auto-detected** — any descendant carrying a `BinderyView` is treated as a boundary; the
  parent never re-walks below it.
- **Ancestors auto-recompose** — generating the child also regenerates the ancestor view(s) in
  the same step (deepest-first, so refs wire in order), so the composition appears immediately.
- **Up is `ParentView`** — `view.ParentView` / `view.GetParentView<T>()` resolve the composing
  view via a cached `GetComponentInParent` — no runtime string lookup.
- A `~`-transparent node still wins — it's ignored even if it carries a view.

## One registry for every view

Bindery keeps a single generated **`BinderyViews`** class with one typed, cached property per view —
so you can reach any view from anywhere without a `FindObjectOfType` of your own:

```csharp
using Bindery.Generated;

BinderyViews.SettingsPanel.Footer.OkButton.onClick.AddListener(Save);
var hp = BinderyViews.Hud.PlayerHealth;     // each property is typed as its view
```

Each property finds its view in the loaded scene(s) on first use and caches it (a destroyed/reloaded
view is re-found automatically); `BinderyViews.Refresh()` clears the caches after a scene change. The
property name is the view's class name minus the suffix (`SettingsPanelView` → `SettingsPanel`),
disambiguated if two would collide. The registry **regenerates itself** as you add and remove views —
no upkeep — and lives in the same generated assembly + namespace as the views. Turn it off under
**Project Settings ▸ Bindery ▸ View registry** (it's deleted on the next reload).

## Removing a view

Select the object and hit **Bindery ▸ Remove Accessor Class** (Hierarchy right-click or the
`Tools` menu) — or use the **Remove View** button on the view's inspector. After an *are-you-sure*
confirmation, Bindery detaches the view component and deletes its class files — both the `.g.cs`
and your editable `<Name>View.cs` stub (so back up any `OnBind` code first; the deletion can't be
undone). If an ancestor view was composing the one you remove, it's regenerated automatically so it
stops referencing the deleted type (the subtree falls back to a normal scope).

If there are views **nested below** what you're removing, the confirmation offers **Remove all**
(the selected view plus every nested one) / **Selected only** / **Cancel** — so you can clear a
whole panel's worth of views in one go. Remove also works on a view-less parent that just has
nested views, and on a view that lives in a **prefab asset** — the component is detached from the
prefab's contents and the asset is re-saved, leaving no missing-script behind.

A generated view's **inspector** also carries **Regenerate** and **Remove View** buttons and warns
when a wired reference has gone missing — so the whole loop is reachable without the menus.
**`Tools ▸ Bindery ▸ Validate Views in Scene`** scans every view (including inactive) and logs a
clickable warning for any with missing references. **`Tools ▸ Bindery ▸ Regenerate All Views`**
re-runs generation on every view in the open scene(s) — handy after changing a setting.

## The Bindery Views window

**`Window ▸ Bindery ▸ Views`** opens one panel listing every generated view across the project —
those in the open scene(s) **and** those baked into prefab assets — as a **nested tree that mirrors
composition**: a view generated on a child sits *under* the parent view that composes it, so the
panel reads the same way the code does:

```text
▼ SettingsPanelView   scene  SettingsPanel
    ▼ FooterView       scene  Footer          ← composed sub-view, nested under its parent
  PanelView           scene  Panel            ← a separate root
```

Nesting is by nearest-ancestor view (exactly Bindery's composition boundary), so the tree always
matches what the generator produced. Each row shows the view's class name, the GameObject it sits on,
a status dot (green when fully wired, amber when one or more references have gone missing) with the
missing-reference count, and three buttons:

- **Select** — pings and selects the object (or the prefab asset) in the project.
- **Regen** — regenerates that single view in place.
- **Remove** — detaches the view and deletes its class files. Works on **prefab-asset** views too:
  the component is removed from the prefab via its loaded contents and the asset is saved, so no
  missing-script is left behind.

Across the top is a **Bindery Tools** frame — the whole `Tools ▸ Bindery` toolset, one click away:
**Generate ▸ Selection** and **Remove ▸ Selection** (act on the current Hierarchy/Project selection),
**Regenerate All**, **Validate Scene**, and **Visual Script** (when Visual Scripting is installed).
It's shown even with no views yet, so you can generate the first one straight from the window. The
thin strip above it holds **Refresh**, the view count, **Expand** / **Collapse** (when there's
nesting), and **Settings** (jumps to *Project Settings ▸ Bindery*). The list refreshes on open, on
focus, and on demand — handy on larger projects where views are scattered across scenes and prefabs.

## Visual Scripting playground

If your project has **Unity Visual Scripting** installed (`com.unity.visualscripting`), Bindery can
hand you a ready-to-poke Bolt graph. **`Tools ▸ Bindery ▸ Generate Visual Script Playground`** (or
the **Visual Script** button in the Bindery Views window) builds a `ScriptGraphAsset` in your views
folder with, for every view type in the open scene(s):

- a **Start** event that fans out through each view,
- a **`FindFirstObjectByType`** node that grabs the live view at runtime (and a **`Debug.Log`** of
  it so the graph does something the moment you press Play), and
- a **`Get Member`** node per accessor, already wired to that view — so `Save`, `Footer`, `Slots` …
  are sitting on the canvas, typed and connected, ready to drag into your own logic.

It's a starting point, not a finished machine — open it in the **Script Graph** window and play.
This integration is **entirely optional**: the generator lives in its own assembly that only
compiles when Visual Scripting is present, so plain Bindery never depends on it (and the menu /
button simply don't appear without it).

## Custom components

Built-in uGUI isn't the whole story. Mark any of your own MonoBehaviours with `[BinderyBind]` and
Bindery surfaces it as a strongly-typed leaf:

```csharp
[Bindery.BinderyBind]
public class HealthBar : MonoBehaviour { /* … */ }

// →  view.PlayerHealth   is a typed HealthBar
```

Bindery automatically adds the assembly that defines the component to the generated
`Bindery.Generated.asmdef` so the view compiles. **Caveat:** that assembly must *not* reference
`Bindery.Generated` back (Unity rejects the cyclic asmdef) — keep `[BinderyBind]` components in
their own leaf assembly. `CanvasGroup` is recognized without any attribute.

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

**Project Settings ▸ Bindery ▸ Transparent prefix** — a name prefix (default `~`) that
marks a GameObject as a transparent wrapper. The node generates nothing and its children
are promoted to its level, so a layout-only container stays out of the API:

```text
SettingsPanel                 SettingsPanel
└── ~ButtonRow        ⇒       ├── (ButtonRow: no field, no scope)
    ├── OkButton              ├── OkButton      → view.OkButton
    └── CancelButton          └── CancelButton  → view.CancelButton
```

It composes recursively (nested `~` wrappers collapse), the marker is never part of an
identifier, and the wired references still point at the real child objects. Set the prefix
empty to turn the feature off. Stored in the same project settings asset.

**Project Settings ▸ Bindery ▸ Editable views folder** — where the hand-edited `<Name>View.cs`
stubs go (default `Bindery/Views`), kept apart from the regenerated `.g.cs` in `Bindery/Generated`.
It must stay under `Bindery/` so it shares the generated assembly (a view is one partial class
across the two files); a value outside that falls back to the default.

**Project Settings ▸ Bindery ▸ New view stubs** — two checkboxes (both on) that control the
handler scaffolding in a freshly generated stub: *Scaffold button click handlers* and *Scaffold
control event handlers*. Turn them off if you'd rather start from an empty `OnBind()`.

**Project Settings ▸ Bindery ▸ Generated output** — the **generated namespace** (default
`Bindery.Generated`) and the **view base class** (default `Bindery.BinderyView`) that views derive
from. Both are sanitized to legal dotted C# names and applied across the `.g.cs`, the stub, the
asmdef `rootNamespace`, and wiring. The base class must still derive from `Bindery.BinderyView`.

**Project Settings ▸ Bindery ▸ Collections** — *Serialize collections as a single array* (on by
default). A collection serializes as one `T[]` field, shown as a **list in the Inspector**, instead
of an individual `[SerializeField]` per element. Turn it off to get the per-element fields back.

## Install

Unity **2022.3+**, with **uGUI** and **TextMeshPro** present.

- **Package Manager ▸ Add package from git URL…**
  `https://github.com/havokentity/Bindery.git`
- or clone into your project's `Packages/` folder.

Bindery depends on `com.unity.ugui`. **TextMeshPro** comes from there automatically on
**Unity 6** (TMP was folded into `com.unity.ugui` 2.x). On **Unity 2022.3**, TMP is the
separate `com.unity.textmeshpro` package — present in new projects by default; add it via
the Package Manager if your project doesn't already have it.

Generated output lands under `Assets/Bindery/` — the regenerated `.g.cs` in `Generated/`,
your editable stubs in `Views/` — both under one `Bindery.Generated` assembly definition
(at `Assets/Bindery/`), referenceable from your own asmdefs.

## Sample

Import **Accessor View Demo** from *Package Manager ▸ Bindery ▸ Samples*, then run
**Tools ▸ Bindery ▸ Samples ▸ Build Accessor View Demo** to drop a ready-made `SettingsPanel`
hierarchy into the active scene — one menu click from a live generated view.

## Notes & limits (v1)

- Works on **scene objects, prefab instances, and prefab *assets***. Select a prefab in the
  Project window and generate — the view is attached to the prefab and its references are wired
  by relative path (no need to open it in a scene first).
- A container that is itself an `Image` (e.g. a card panel) is bound as a
  `RectTransform` scope; its own `Image` is not surfaced separately.
- `ScrollRect` is a leaf — bind its `Content` object directly if you need its items.

## License

MIT © 2026 Rajesh D'Monte
