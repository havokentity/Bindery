// =============================================================================
// Bindery — assembly-level attributes for Bindery.Editor.
// Grants the editor test assembly access to internal types so tests can reach
// IdentifierUtil, BinderySettings, BinderyTypeMap, and BinderyHierarchy directly,
// and the optional Visual Scripting integration assembly access to BinderySettings.
// =============================================================================

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Bindery.Editor.Tests")]
[assembly: InternalsVisibleTo("Bindery.VisualScripting.Editor")]
