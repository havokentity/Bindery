# Changelog

All notable changes to Bindery are documented here.

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
