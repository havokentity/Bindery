// =============================================================================
// Bindery — base class for generated accessor views. A generated
//   `public partial class FooView : BinderyView`
// adds a [SerializeField] reference + a typed accessor for each bindable built-in
// uGUI child of the GameObject it lives on. Those references are wired by the
// Bindery generator at edit time, so at runtime there is no reflection and no
// string-keyed Find — every accessor is a direct field read.
//
// Add your own behaviour in a SEPARATE partial file (the generator drops an
// editable `FooView.cs` stub next to the `.g.cs` for exactly this) so a
// regenerate never clobbers it:
//
//   public partial class FooView
//   {
//       protected override void OnBind()
//       {
//           OkButton.onClick.AddListener(() => Debug.Log("clicked"));
//       }
//   }
// =============================================================================

using UnityEngine;

namespace Bindery
{
    // Deliberately NOT [DisallowMultipleComponent]: that attribute also blocks DIFFERENT
    // subclasses, so renaming the object (or changing the class-name suffix) and regenerating
    // could not attach the new view next to the old one. Bindery already reuses an existing
    // same-type component instead of duplicating it, so the attribute bought nothing here.
    public abstract class BinderyView : MonoBehaviour
    {
        bool _bound;

        protected virtual void Awake() { EnsureBound(); }

        /// <summary>Runs <see cref="OnBind"/> exactly once. Awake calls it; you can call it
        /// yourself earlier (e.g. right after the view is enabled) and it stays idempotent.</summary>
        public void EnsureBound()
        {
            if (_bound) return;
            _bound = true;
            OnBind();
        }

        /// <summary>Override in a partial to hook things up once the references are wired
        /// (register listeners, cache initial state, …).</summary>
        protected virtual void OnBind() { }
    }
}
