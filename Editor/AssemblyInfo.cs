// =============================================================================
// Bindery — assembly-level attributes for Bindery.Editor.
// Grants the editor test assembly access to internal types so tests can reach
// IdentifierUtil, BinderySettings, BinderyTypeMap, and BinderyHierarchy directly.
// =============================================================================

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Bindery.Editor.Tests")]
