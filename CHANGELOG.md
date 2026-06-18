# Changelog

All notable changes to Bindery are documented here.

## [0.14.0] — 2026-06-18

- **Lazy binding — works on inactive views.** Every generated accessor (and each `BinderyViews`
  registry getter) now calls `EnsureBound()` before returning, so a view runs its `OnBind()` the
  instant any member is touched — from any `Awake`/`Start`, in any script order, and **even while
  the view's GameObject is inactive**, where Unity never calls `Awake`. Reaching
  `someInactiveView.OkButton` resolves the (always-deserialized) reference *and* binds the view.
  `EnsureBound` is idempotent and re-entrancy-safe (the guard is set before `OnBind` runs), so
  `Awake` and the accessors all funnel through one binding. (`OnBind` work that *requires* an active
  object — e.g. `StartCoroutine` — still can't run early on an inactive view; that's a Unity limit.)

## [0.13.0] — 2026-06-18

- **`BinderyViews` registry.** A single generated static class exposes every view by a typed,
  cached property — `BinderyViews.SettingsPanel`, `BinderyViews.Hud` — each finding its view in the
  loaded scene(s) on first use (re-found if destroyed) with a `BinderyViews.Refresh()` to clear the
  caches after a scene change. The property name is the class name minus the suffix
  (`SettingsPanelView` → `SettingsPanel`), disambiguated on collision. It **regenerates itself** as
  views are added/removed (kept consistent *before* each recompile, so a removed view never leaves
  the registry referencing a deleted type) and lives in the generated assembly + namespace. On by
  default; **Project Settings ▸ Bindery ▸ View registry** turns it off (deleted on the next reload).

## [0.12.0] — 2026-06-18

- **Visual Scripting playground (optional).** With `com.unity.visualscripting` installed,
  `Tools ▸ Bindery ▸ Generate Visual Script Playground` (and a **Visual Script** button in the
  Bindery Views window) builds a Bolt `ScriptGraphAsset` pre-populated with sample nodes for every
  view in the open scene(s): a **Start** event, a **`FindFirstObjectByType`** + **`Debug.Log`** per
  view, and a **`Get Member`** node per accessor already wired to the live view — a ready-to-poke
  starting point you open in the Script Graph window.
- **Stays dependency-free.** The generator lives in its own assembly gated by an
  `com.unity.visualscripting` version define, so Bindery takes **no** hard dependency on Visual
  Scripting — the menu and button only appear when the package is present, and the package compiles
  identically without it.

## [0.11.0] — 2026-06-18

- **Bindery Views window.** `Window ▸ Bindery ▸ Views` opens one panel listing every generated view
  in the project — across the open scene(s) **and** prefab assets — with a status dot (green / amber)
  and missing-reference count per view, plus **Select / Regen / Remove** per row and **Regenerate
  All / Validate / Refresh** on the toolbar. Handy for managing views scattered across a larger
  project.
- **Remove works on prefab-asset views.** Removing a view that lives in a prefab asset now detaches
  the component through the prefab's loaded contents and re-saves the asset (instead of leaving a
  missing-script behind), so the window's **Remove** — and the menu / inspector remove — all clean
  up prefab views correctly.

## [0.10.0] — 2026-06-18

- **Generate on prefab assets.** Select a prefab in the Project window and **Generate Accessor
  Class** — no need to open it in a scene first. The view component is attached to the prefab and
  its references are wired by relative path (via `LoadPrefabContents` / `SaveAsPrefabAsset`), since
  the deferred post-compile wiring can't hold prefab contents across a recompile.

## [0.9.0] — 2026-06-18

- **Collections serialize as a single array.** A detected collection (`Slot0, Slot1, …`) now
  serializes as ONE `[SerializeField] T[]` field — shown as a proper **list in the Inspector** —
  instead of an individual field per element. On by default; **Project Settings ▸ Bindery ▸
  Collections** has a checkbox to restore the per-element fields. The array is wired element-by-element.
- **Wiring resilience.** A transient wiring failure (e.g. a stale serialized layout mid-recompile)
  is now caught and retried after the next reload instead of aborting the whole wire pass.

## [0.8.0] — 2026-06-18

- **Regenerate All Views.** `Tools ▸ Bindery ▸ Regenerate All Views` re-runs generation on every
  view in the open scene(s) — handy after changing a setting (suffix / namespace / base class) or
  editing hierarchies, without hunting each view down.
- **Collection handler scaffolding.** A new view stub now scaffolds collections too: an indexed
  loop wires every element to one handler — `for (…) Slots[i].onClick.AddListener(() => OnSlotsClicked(index));`
  with `void OnSlotsClicked(int index)` (and `OnMutesChanged(int index, bool value)` for value
  events). Previously collection elements were left for you to wire by hand.

## [0.7.0] — 2026-06-18

- **Recursive remove.** When you Remove on an object that has Bindery views nested below it, the
  confirmation now offers **Remove all** (the selected view *and* every nested one) / **Selected
  only** / **Cancel**. Remove also works on a view-less parent that only has nested views. Each
  removed view still deletes its `.g.cs` + stub, and any surviving ancestor view is regenerated.

## [0.6.1] — 2026-06-18

- **Fix: cyclic assembly dependency from composed views.** A view that composes a sub-view
  (a member typed `Bindery.Generated.FooterView`) made the asmdef-reference collector add the
  generated assembly to its *own* references — a self-cycle Unity reports as "cyclic dependencies
  … between Assembly-CSharp-Editor, Bindery.Generated", breaking the whole project's compile. The
  generated assembly (and the predefined `Assembly-CSharp` / `Assembly-CSharp-Editor`) are now
  skipped; a `[BinderyBind]` component sitting in `Assembly-CSharp` warns to move to its own asmdef.

## [0.6.0] — 2026-06-18

- **Custom component binding.** Mark any of your own MonoBehaviours with `[Bindery.BinderyBind]`
  and Bindery surfaces it as a strongly-typed control leaf, just like a built-in. The generated
  asmdef automatically references the assembly that defines the component so the view compiles.
  (That assembly must not reference `Bindery.Generated` back — keep `[BinderyBind]` components in
  their own leaf assembly, a v1 limitation.)
- **`CanvasGroup` is bindable** — surfaced as a typed leaf so you can drive alpha / interactable /
  blocksRaycasts through the view.
- **Collection accessors.** 2+ sibling children of the same bindable kind whose names share a stem
  and a trailing index — `Slot0, Slot1, Slot2`, `Slot 0/Slot 1`, `Item (1)/Item (2)` — surface as
  ONE ordered, read-only accessor (`view.Slots`, an `IReadOnlyList<Button>`) instead of N. Each
  element keeps its own wired field; the collection is a cached array in index order. Works at the
  root and in scopes; a lone `Slot0` stays a single accessor; mixed types aren't grouped.
- **Configurable generated namespace + base class.** Project Settings ▸ Bindery now sets the
  **generated namespace** (default `Bindery.Generated`) and the **view base class** (default
  `Bindery.BinderyView`), applied across the `.g.cs`, the stub, the asmdef `rootNamespace`, and
  wiring. The base must still derive from `Bindery.BinderyView`.
- **Validate Views command.** `Tools ▸ Bindery ▸ Validate Views in Scene` (and `… in Selection`)
  scans every `BinderyView` — including inactive — and logs a clickable warning for any whose
  references have gone null.
- **Sample + tests + CI.** An importable *Accessor View Demo* sample, an EditMode test suite, and
  a lightweight package-validation GitHub Actions workflow now ship with the package.

## [0.5.0] — 2026-06-17

- **Composable views.** Generate a view on a subobject and its ancestors compose it as a
  strongly-typed sub-view instead of re-walking its subtree: `settingsPanel.Footer` returns the
  child `FooterView`, so `settingsPanel.Footer.OkButton` resolves through the real view. Any
  descendant that already carries a `BinderyView` is auto-detected as a boundary and not re-walked.
- **Auto-recompose ancestors.** Generating a sub-view walks up and regenerates the ancestor
  views in the same pass so they pick up the new boundary with no extra step (deepest-first, so
  the sub-view is wired before the parent that holds it).
- **Parent navigation.** `BinderyView.ParentView` and `GetParentView<T>()` climb back up to the
  composing view — resolved once via a typed `GetComponentInParent`, then cached (no string lookup).
- **Editable stubs live in their own folder.** The hand-edited `<Name>View.cs` now lands in a
  configurable **`Bindery/Views`** folder (Project Settings ▸ Bindery), apart from the regenerated
  `.g.cs` in `Bindery/Generated`. The asmdef moved up to `Bindery/` so both still share one
  assembly; existing stubs are left where they are.
- **`IsVisible` on `BinderyView`.** `view.IsVisible = false` shows/hides the view by toggling its
  GameObject active state (virtual — override to drive a `CanvasGroup` instead).
- **Remove a view.** `GameObject ▸ Bindery ▸ Remove Accessor Class` (and the Tools menu) detaches
  the view component and deletes its class files (`.g.cs` + editable stub) after an "are you sure"
  confirmation. Any ancestor view that composed it is regenerated first, so nothing is left
  referencing a deleted type.
- **Inspector buttons.** Generated views now have a custom inspector with **Regenerate** and
  **Remove View** buttons, plus a warning when a wired reference has gone missing (object renamed,
  moved, or deleted) — the whole loop is reachable from the component.
- **Reserved-name guard.** A child whose name resolves to a base-class member (`transform`,
  `gameObject`, `name`, `enabled`, `IsVisible`, `ParentView`, `Awake`, …) or a common Unity message
  is now renamed (`_2`) instead of silently shadowing it or breaking compilation.
- **Scaffolded event handlers.** A freshly generated view stub now pre-wires each control's basic
  event to a **named handler method** (with its own body and a `// TODO` placeholder) —
  `OkButton.onClick.AddListener(OnOkButtonClicked)` plus `void OnOkButtonClicked() { // TODO… }`,
  and `onValueChanged` for Toggle/Slider/Scrollbar/Dropdown/InputField/ScrollRect. Two checkboxes
  in Project Settings ▸ Bindery (*button* / *other control* handlers), both on by default.

## [0.4.0] — 2026-06-17

- **Transparent wrappers.** A GameObject whose name starts with the transparent prefix
  (default `~`, configurable in `Project Settings ▸ Bindery`) generates nothing itself and
  its children are promoted to its level — so a layout-only `~ButtonRow` yields
  `view.OkButton` instead of `view.ButtonRow.OkButton`. The marker is stripped from names,
  composes recursively, and (on a leaf/control) doubles as an "exclude this" marker.

## [0.3.0] — 2026-06-17

- **Unity 6 compatibility.** Dropped the explicit `com.unity.textmeshpro` dependency
  (it's deprecated in Unity 6, where TextMeshPro ships inside `com.unity.ugui` 2.x).
  Bindery now depends only on `com.unity.ugui`, which carries TMP on Unity 6 and is the
  separate package on 2022.3.
- **View class suffix is now a project setting**, not a per-user preference. It moved
  from `Preferences ▸ Bindery` (EditorPrefs) to `Project Settings ▸ Bindery`, stored in
  `ProjectSettings/BinderySettings.asset` — commit it to share one suffix across the team.
- Generating from the Hierarchy menu with **multiple GameObjects selected** now runs once
  for the whole selection instead of once per selected object (Unity invokes `GameObject/`
  menu commands per-object).
- Selecting a **uGUI control as the generation root** (a `Button`, `Slider`, …) is now
  refused with a warning instead of surfacing the control's internal label/handle as members
  — a control is a leaf.
- Wiring now marks the wired object's **own scene** dirty (correct under multi-scene editing
  and in prefab mode) rather than the active scene.

## [0.2.0] — 2026-06-17

- Configurable **view class suffix** (`Preferences ▸ Bindery`), default `View`.
  e.g. set it to `Blah` and `SettingsPanel` generates `SettingsPanelBlah`. The suffix
  is sanitized to identifier-legal characters.

## [0.1.0] — 2026-06-17

Initial release.

- `GameObject ▸ Bindery ▸ Generate Accessor Class` (Hierarchy right-click) and
  `Tools ▸ Bindery ▸ Generate Accessor Class for Selection`.
- Walks the selected GameObject / Canvas subtree and emits a strongly-typed
  `partial class <Name>View : BinderyView` with a `[SerializeField]` reference and
  a typed accessor per bindable built-in uGUI child.
- Auto-wires the serialized references on the live object after compile via
  `GlobalObjectId`, so the view is ready in the Inspector with no manual dragging.
- Nested containers become nested scope classes (`view.Footer.OkButton`).
- One-time editable behaviour stub (`<Name>.cs`) generated alongside the
  `.g.cs` so `OnBind()` and your own code live in the same assembly.
