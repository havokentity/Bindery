# Changelog

All notable changes to Bindery are documented here.

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
