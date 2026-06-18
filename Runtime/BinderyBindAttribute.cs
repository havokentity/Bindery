// =============================================================================
// Bindery — opt your OWN component into binding. Put [BinderyBind] on a
// MonoBehaviour and Bindery surfaces it as a typed bindable leaf (like a uGUI
// control): the generated view exposes a strongly-typed accessor to it and never
// descends into its children. This is the single extension point beyond the
// built-in uGUI + TMP + CanvasGroup types.
//
// Inherited: a subclass of a marked component is recognised too.
//
// IMPORTANT (assembly cycle): for a generated view to compile against your type,
// Bindery.Generated must reference the assembly that DEFINES it. That assembly
// must NOT, in turn, reference Bindery.Generated — that would be a cyclic asmdef
// reference Unity rejects. Keep your [BinderyBind] components in their own leaf
// assembly (one that nothing in Bindery.Generated's graph depends back on). v1
// does not resolve such cycles.
// =============================================================================

using System;

namespace Bindery
{
    /// <summary>Marks a MonoBehaviour as a Bindery-bindable component: it's surfaced as a typed
    /// leaf accessor on the generated view, exactly like a built-in uGUI control. Inherited, so a
    /// subclass of a marked component is recognised. See the file header for the asmdef-cycle caveat.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class BinderyBindAttribute : Attribute { }
}
