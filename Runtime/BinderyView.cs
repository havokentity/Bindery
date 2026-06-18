// =============================================================================
// Bindery — base class for generated accessor views. A generated
//   `public partial class FooView : BinderyView`
// adds a [SerializeField] reference + a typed accessor for each bindable built-in
// uGUI child of the GameObject it lives on. Those references are wired by the
// Bindery generator at edit time, so at runtime there is no reflection and no
// string-keyed Find — each accessor is a near-direct field read that first calls
// EnsureBound() (lazy + idempotent), so touching any member from anywhere — any
// Awake/Start, any order, even on an INACTIVE view Unity hasn't Awoken — runs
// OnBind() exactly once before the reference is handed back.
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

        BinderyView _parentView;
        bool _parentResolved;

        /// <summary>The nearest ancestor Bindery view — the one that composes this view as a typed
        /// sub-view. Resolved once by walking up the transform (a typed <c>GetComponentInParent</c>,
        /// no string lookup), then cached. Null if this view stands alone.</summary>
        public BinderyView ParentView
        {
            get
            {
                if (_parentResolved) return _parentView;
                _parentResolved = true;
                var p = transform.parent;
                _parentView = p != null ? p.GetComponentInParent<BinderyView>(true) : null;
                return _parentView;
            }
        }

        /// <summary>The parent view as <typeparamref name="T"/>, or null if there is no parent view
        /// (or it isn't a <typeparamref name="T"/>). Lets you climb back up typed:
        /// <c>GetParentView&lt;SettingsPanelView&gt;()</c>.</summary>
        public T GetParentView<T>() where T : BinderyView => ParentView as T;

        /// <summary>Show or hide the whole view by toggling its GameObject's active state —
        /// <c>view.IsVisible = false</c>. Override if you'd rather hide without deactivating
        /// (e.g. drive a <see cref="CanvasGroup"/>).</summary>
        public virtual bool IsVisible
        {
            get => gameObject.activeSelf;
            set { if (gameObject.activeSelf != value) gameObject.SetActive(value); }
        }

        protected virtual void Awake() { EnsureBound(); }

        /// <summary>Runs <see cref="OnBind"/> exactly once. Awake calls it, and so does every generated
        /// accessor on first touch — so a view binds even while its GameObject is inactive (Unity skips
        /// Awake there). Idempotent, and re-entrancy-safe: the guard is set BEFORE <see cref="OnBind"/>
        /// runs, so accessors used inside OnBind don't recurse. (OnBind that needs an active object —
        /// e.g. StartCoroutine — still can't run early on an inactive view; that's a Unity limit.)</summary>
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
