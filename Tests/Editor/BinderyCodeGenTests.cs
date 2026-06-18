// =============================================================================
// Bindery — NUnit EditMode tests for BinderyCodeGen: the emitted .g.cs string for
// short type names + usings, lazy-binding accessors, collection accessors, the base
// class, custom (non-built-in) types staying fully qualified, and the BinderyViews
// registry shape. Builds a small model (or constructs one directly) and asserts on
// the generated code. Run via Window ▸ General ▸ Test Runner (EditMode tab).
// =============================================================================

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Bindery.Tests
{
    internal sealed class BinderyCodeGenTests
    {
        readonly List<GameObject> _roots = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _roots) if (go != null) Object.DestroyImmediate(go);
            _roots.Clear();
        }

        GameObject MakeRoot(string name) { var go = new GameObject(name); _roots.Add(go); return go; }
        static GameObject MakeChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        // Build a model the way the generator does (namespace / base / collections-as-array stamped).
        ViewModel Model(GameObject root, bool arrays = true)
        {
            var vm = BinderyHierarchy.Build(root, "View", "~");
            vm.namespaceName = "Bindery.Generated";
            vm.baseClass = "Bindery.BinderyView";
            vm.collectionsAsArray = arrays;
            return vm;
        }

        [Test]
        public void Emit_BuiltInControl_UsesShortNameAndUsing()
        {
            var root = MakeRoot("Panel");
            MakeChild(root, "Go").AddComponent<Button>();

            var code = BinderyCodeGen.EmitViewClass(Model(root));

            Assert.That(code, Does.Contain("using UnityEngine.UI;"));
            Assert.That(code, Does.Contain("public Button Go"));
            Assert.That(code, Does.Not.Contain("UnityEngine.UI.Button Go"));   // not fully qualified
        }

        [Test]
        public void Emit_Accessor_CallsEnsureBound()
        {
            var root = MakeRoot("Panel");
            MakeChild(root, "Go").AddComponent<Button>();

            var code = BinderyCodeGen.EmitViewClass(Model(root));

            Assert.That(code, Does.Contain("EnsureBound(); return _Go;"));
        }

        [Test]
        public void Emit_BaseClass_StaysFullyQualified()
        {
            var root = MakeRoot("Panel");
            MakeChild(root, "Go").AddComponent<Button>();

            var code = BinderyCodeGen.EmitViewClass(Model(root));

            Assert.That(code, Does.Contain("public partial class PanelView : Bindery.BinderyView"));
        }

        [Test]
        public void Emit_Collection_AsReadOnlyListArray()
        {
            var root = MakeRoot("Bar");
            MakeChild(root, "Slot0").AddComponent<Button>();
            MakeChild(root, "Slot1").AddComponent<Button>();

            var code = BinderyCodeGen.EmitViewClass(Model(root, arrays: true));

            Assert.That(code, Does.Contain("using System.Collections.Generic;"));
            Assert.That(code, Does.Contain("[SerializeField] Button[] _Slots;"));
            Assert.That(code, Does.Contain("IReadOnlyList<Button> Slots"));
        }

        [Test]
        public void Emit_CustomComponentType_StaysFullyQualified()
        {
            // A member from a non-built-in namespace (a [BinderyBind] component) must NOT be shortened.
            var vm = new ViewModel
            {
                className = "PanelView",
                namespaceName = "Bindery.Generated",
                baseClass = "Bindery.BinderyView",
                collectionsAsArray = true,
                members = new List<ViewMember>
                {
                    new ViewMember { identifier = "Widget", csharpType = "My.Custom.Widget", path = "Widget", exposeOnRoot = true },
                },
            };

            var code = BinderyCodeGen.EmitViewClass(vm);

            Assert.That(code, Does.Contain("My.Custom.Widget Widget"));         // kept qualified
            Assert.That(code, Does.Not.Contain("using My.Custom;"));            // no using added for it
        }

        [Test]
        public void EmitRegistry_HasFindAndRefresh()
        {
            var views = new List<(string typeName, string property)>
            {
                ("global::Bindery.Generated.HudView", "Hud"),
            };

            var code = BinderyCodeGen.EmitViewsRegistry("Bindery.Generated", views);

            Assert.That(code, Does.Contain("public static class BinderyViews"));
            Assert.That(code, Does.Contain("public static global::Bindery.Generated.HudView Hud"));
            Assert.That(code, Does.Contain("FindFirstObjectByType<global::Bindery.Generated.HudView>(FindObjectsInactive.Include)"));
            Assert.That(code, Does.Contain("EnsureBound();"));
            Assert.That(code, Does.Contain("public static void Refresh()"));
        }
    }
}
