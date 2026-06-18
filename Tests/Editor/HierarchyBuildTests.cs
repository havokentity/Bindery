// =============================================================================
// Bindery — NUnit EditMode tests for BinderyTypeMap.Classify and
// BinderyHierarchy.Build.  Each test constructs a small in-memory GameObject
// hierarchy, exercises the relevant API, and asserts on the result.
// GameObjects are cleaned up in TearDown so the scene never accumulates debris.
// Run via Window ▸ General ▸ Test Runner (EditMode tab).
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Bindery.Tests
{
    internal sealed class HierarchyBuildTests
    {
        // All root GameObjects created per-test; destroyed in TearDown.
        readonly List<GameObject> _roots = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _roots)
                if (go != null) Object.DestroyImmediate(go);
            _roots.Clear();
        }

        // Convenience: create a root GameObject and track it for cleanup.
        GameObject MakeRoot(string name)
        {
            var go = new GameObject(name);
            _roots.Add(go);
            return go;
        }

        // Create a child attached to a parent (not tracked separately — destroyed with parent).
        static GameObject MakeChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        // -----------------------------------------------------------------------
        // BinderyTypeMap.Classify
        // -----------------------------------------------------------------------

        [Test]
        public void Classify_EmptyGameObject_ReturnsNone()
        {
            var go = MakeRoot("Empty");
            var kind = BinderyTypeMap.Classify(go, out _);
            Assert.That(kind, Is.EqualTo(BindKind.None));
        }

        [Test]
        public void Classify_NullGameObject_ReturnsNone()
        {
            var kind = BinderyTypeMap.Classify(null, out _);
            Assert.That(kind, Is.EqualTo(BindKind.None));
        }

        [Test]
        public void Classify_Button_ReturnsControl()
        {
            var go = MakeRoot("Btn");
            go.AddComponent<Button>();
            var kind = BinderyTypeMap.Classify(go, out var type);
            Assert.That(kind, Is.EqualTo(BindKind.Control));
            Assert.That(type, Is.EqualTo("UnityEngine.UI.Button"));
        }

        [Test]
        public void Classify_Toggle_ReturnsControl()
        {
            var go = MakeRoot("Toggle");
            go.AddComponent<Toggle>();
            var kind = BinderyTypeMap.Classify(go, out var type);
            Assert.That(kind, Is.EqualTo(BindKind.Control));
            Assert.That(type, Is.EqualTo("UnityEngine.UI.Toggle"));
        }

        [Test]
        public void Classify_Slider_ReturnsControl()
        {
            var go = MakeRoot("Slider");
            go.AddComponent<Slider>();
            var kind = BinderyTypeMap.Classify(go, out var type);
            Assert.That(kind, Is.EqualTo(BindKind.Control));
            Assert.That(type, Is.EqualTo("UnityEngine.UI.Slider"));
        }

        [Test]
        public void Classify_Scrollbar_ReturnsControl()
        {
            var go = MakeRoot("Scrollbar");
            go.AddComponent<Scrollbar>();
            var kind = BinderyTypeMap.Classify(go, out var type);
            Assert.That(kind, Is.EqualTo(BindKind.Control));
            Assert.That(type, Is.EqualTo("UnityEngine.UI.Scrollbar"));
        }

        [Test]
        public void Classify_ScrollRect_ReturnsControl()
        {
            var go = MakeRoot("ScrollRect");
            go.AddComponent<ScrollRect>();
            var kind = BinderyTypeMap.Classify(go, out var type);
            Assert.That(kind, Is.EqualTo(BindKind.Control));
            Assert.That(type, Is.EqualTo("UnityEngine.UI.ScrollRect"));
        }

        [Test]
        public void Classify_InputField_ReturnsControl()
        {
            var go = MakeRoot("Field");
            go.AddComponent<InputField>();
            var kind = BinderyTypeMap.Classify(go, out var type);
            Assert.That(kind, Is.EqualTo(BindKind.Control));
            Assert.That(type, Is.EqualTo("UnityEngine.UI.InputField"));
        }

        [Test]
        public void Classify_Dropdown_ReturnsControl()
        {
            var go = MakeRoot("Dropdown");
            go.AddComponent<Dropdown>();
            var kind = BinderyTypeMap.Classify(go, out var type);
            Assert.That(kind, Is.EqualTo(BindKind.Control));
            Assert.That(type, Is.EqualTo("UnityEngine.UI.Dropdown"));
        }

        [Test]
        public void Classify_Image_ReturnsGraphic()
        {
            var go = MakeRoot("Img");
            go.AddComponent<Image>();
            var kind = BinderyTypeMap.Classify(go, out var type);
            Assert.That(kind, Is.EqualTo(BindKind.Graphic));
            Assert.That(type, Is.EqualTo("UnityEngine.UI.Image"));
        }

        [Test]
        public void Classify_RawImage_ReturnsGraphic()
        {
            var go = MakeRoot("RawImg");
            go.AddComponent<RawImage>();
            var kind = BinderyTypeMap.Classify(go, out var type);
            Assert.That(kind, Is.EqualTo(BindKind.Graphic));
            Assert.That(type, Is.EqualTo("UnityEngine.UI.RawImage"));
        }

        [Test]
        public void Classify_LegacyText_ReturnsGraphic()
        {
            var go = MakeRoot("Lbl");
            go.AddComponent<Text>();
            var kind = BinderyTypeMap.Classify(go, out var type);
            Assert.That(kind, Is.EqualTo(BindKind.Graphic));
            Assert.That(type, Is.EqualTo("UnityEngine.UI.Text"));
        }

        [Test]
        public void Classify_ButtonPrefersControlOverImage()
        {
            // A Button's background Image is on the same GO; Button should win.
            var go = MakeRoot("BtnWithBg");
            go.AddComponent<Image>();
            go.AddComponent<Button>();
            var kind = BinderyTypeMap.Classify(go, out var type);
            Assert.That(kind, Is.EqualTo(BindKind.Control));
            Assert.That(type, Is.EqualTo("UnityEngine.UI.Button"));
        }

        // -----------------------------------------------------------------------
        // BinderyHierarchy.Build — single Button child
        // -----------------------------------------------------------------------

        [Test]
        public void Build_SingleButton_YieldsOneMember()
        {
            var root = MakeRoot("Panel");
            var child = MakeChild(root, "OkButton");
            child.AddComponent<Button>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.className, Is.EqualTo("PanelView"));
            Assert.That(vm.members, Has.Count.EqualTo(1));
            var m = vm.members[0];
            Assert.That(m.identifier, Is.EqualTo("OkButton"));
            Assert.That(m.csharpType, Is.EqualTo("UnityEngine.UI.Button"));
            Assert.That(m.isScope, Is.False);
            Assert.That(m.exposeOnRoot, Is.True);
        }

        // -----------------------------------------------------------------------
        // Build — two sibling buttons, distinct identifiers
        // -----------------------------------------------------------------------

        [Test]
        public void Build_TwoSiblingButtons_YieldsTwoMembers()
        {
            var root = MakeRoot("Toolbar");
            var ok = MakeChild(root, "Ok");
            ok.AddComponent<Button>();
            var cancel = MakeChild(root, "Cancel");
            cancel.AddComponent<Button>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.members, Has.Count.EqualTo(2));
            Assert.That(vm.members.Select(m => m.identifier), Is.EquivalentTo(new[] { "Ok", "Cancel" }));
        }

        // -----------------------------------------------------------------------
        // Build — two buttons with the same name → deduped
        // -----------------------------------------------------------------------

        [Test]
        public void Build_DuplicateChildNames_DeduplicatesIdentifiers()
        {
            var root = MakeRoot("Menu");
            var a = MakeChild(root, "Button");
            a.AddComponent<Button>();
            var b = MakeChild(root, "Button");
            b.AddComponent<Button>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.members, Has.Count.EqualTo(2));
            Assert.That(vm.members[0].identifier, Is.EqualTo("Button"));
            Assert.That(vm.members[1].identifier, Is.EqualTo("Button_2"));
        }

        // -----------------------------------------------------------------------
        // Build — container with children → scope + leaf members
        // -----------------------------------------------------------------------

        [Test]
        public void Build_ContainerWithButtonChild_YieldsScopeAndLeaf()
        {
            var root = MakeRoot("Screen");
            var footer = MakeChild(root, "Footer");
            // Footer has no bindable component itself, but has a bindable child → becomes a scope
            var ok = MakeChild(footer, "Ok");
            ok.AddComponent<Button>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            // Expect: Footer (scope) + Ok (leaf)
            Assert.That(vm.members, Has.Count.EqualTo(2));

            var scope = vm.members.First(m => m.isScope);
            Assert.That(scope.identifier, Is.EqualTo("Footer"));
            Assert.That(scope.csharpType, Is.EqualTo("UnityEngine.RectTransform"));
            Assert.That(scope.exposeOnRoot, Is.True);
            Assert.That(scope.scopeTypeName, Is.EqualTo("FooterScope"));

            var leaf = vm.members.First(m => !m.isScope);
            Assert.That(leaf.identifier, Is.EqualTo("Ok"));
            Assert.That(leaf.csharpType, Is.EqualTo("UnityEngine.UI.Button"));
            Assert.That(leaf.exposeOnRoot, Is.False); // parent is Footer, not root
        }

        // -----------------------------------------------------------------------
        // Build — transparent wrapper (~) flattens its children to root level
        // -----------------------------------------------------------------------

        [Test]
        public void Build_TransparentWrapper_ChildrenPromotedToRoot()
        {
            var root = MakeRoot("Dialog");
            var wrapper = MakeChild(root, "~Row");
            // ~Row is transparent; its button should appear as root-level
            var btn = MakeChild(wrapper, "CloseButton");
            btn.AddComponent<Button>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            // ~Row is invisible; only CloseButton should appear, exposeOnRoot=true
            Assert.That(vm.members, Has.Count.EqualTo(1));
            var m = vm.members[0];
            Assert.That(m.identifier, Is.EqualTo("CloseButton"));
            Assert.That(m.exposeOnRoot, Is.True);
        }

        // -----------------------------------------------------------------------
        // Build — transparent prefix is empty → no transparency
        // -----------------------------------------------------------------------

        [Test]
        public void Build_EmptyTransparentPrefix_TildeNameTreatedNormally()
        {
            var root = MakeRoot("Root");
            // When the prefix is "" the "~" wrapper is a normal container
            var container = MakeChild(root, "~Container");
            var btn = MakeChild(container, "Btn");
            btn.AddComponent<Button>();

            var vm = BinderyHierarchy.Build(root, "View", "");

            // ~Container should become a scope because it has a bindable child
            Assert.That(vm.members, Has.Count.EqualTo(2));
            var scope = vm.members.First(m => m.isScope);
            Assert.That(scope.identifier, Is.EqualTo("Container")); // '~' stripped by ToIdentifier
        }

        // -----------------------------------------------------------------------
        // Build — Image-only child (graphic, no bindable descendants) → one leaf
        // -----------------------------------------------------------------------

        [Test]
        public void Build_GraphicLeaf_IncludedAsMember()
        {
            var root = MakeRoot("Banner");
            var img = MakeChild(root, "Thumbnail");
            img.AddComponent<Image>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.members, Has.Count.EqualTo(1));
            Assert.That(vm.members[0].csharpType, Is.EqualTo("UnityEngine.UI.Image"));
            Assert.That(vm.members[0].isScope, Is.False);
        }

        // -----------------------------------------------------------------------
        // Build — empty container (no bindable descendants) → not included
        // -----------------------------------------------------------------------

        [Test]
        public void Build_EmptyContainer_NotIncluded()
        {
            var root = MakeRoot("Root");
            // A child with no bindable component and no bindable children is ignored
            MakeChild(root, "LayoutGroup");

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.members, Is.Empty);
        }

        // -----------------------------------------------------------------------
        // Build — root with no children at all → empty members list
        // -----------------------------------------------------------------------

        [Test]
        public void Build_RootWithNoChildren_EmptyMemberList()
        {
            var root = MakeRoot("EmptyPanel");

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.className, Is.EqualTo("EmptyPanelView"));
            Assert.That(vm.members, Is.Empty);
        }

        // -----------------------------------------------------------------------
        // Build — child named with a C# keyword → @-escaped identifier
        // -----------------------------------------------------------------------

        [Test]
        public void Build_ChildNamedKeyword_IdentifierAtEscaped()
        {
            var root = MakeRoot("Form");
            var kw = MakeChild(root, "class");
            kw.AddComponent<Button>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.members, Has.Count.EqualTo(1));
            Assert.That(vm.members[0].identifier, Is.EqualTo("@class"));
        }

        // -----------------------------------------------------------------------
        // Build — class name formed from root name + suffix
        // -----------------------------------------------------------------------

        [Test]
        public void Build_ClassNameUsesSuffix()
        {
            var root = MakeRoot("Settings");

            var vm = BinderyHierarchy.Build(root, "Widget", "~");

            Assert.That(vm.className, Is.EqualTo("SettingsWidget"));
        }

        // -----------------------------------------------------------------------
        // Build — reserved name collision on child → renamed
        // -----------------------------------------------------------------------

        [Test]
        public void Build_ChildNamedReserved_GetsRenaming()
        {
            // "name" is in ReservedNames; the child should be renamed "name_2"
            var root = MakeRoot("Panel");
            var child = MakeChild(root, "name");
            child.AddComponent<Button>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.members, Has.Count.EqualTo(1));
            Assert.That(vm.members[0].identifier, Is.EqualTo("name_2"));
        }

        [Test]
        public void Build_ChildNamedIsVisible_GetsRenamed()
        {
            // "IsVisible" is a BinderyView member → reserved, so the child is renamed.
            var root = MakeRoot("Panel");
            var child = MakeChild(root, "IsVisible");
            child.AddComponent<Button>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.members, Has.Count.EqualTo(1));
            Assert.That(vm.members[0].identifier, Is.EqualTo("IsVisible_2"));
        }

        // -----------------------------------------------------------------------
        // Build — repeated indexed siblings collapse into a collection
        // -----------------------------------------------------------------------

        [Test]
        public void Build_RepeatedIndexedSiblings_FormACollection()
        {
            var root = MakeRoot("Bar");
            foreach (var n in new[] { "Slot0", "Slot1", "Slot2" })
                MakeChild(root, n).AddComponent<Button>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.members, Has.Count.EqualTo(3));
            Assert.That(vm.members.All(m => m.IsCollected), Is.True);
            Assert.That(vm.members.Select(m => m.collectionName).Distinct().Single(), Is.EqualTo("Slots"));
            Assert.That(vm.members.Count(m => m.collectionLead), Is.EqualTo(1));
            // Lead is the lowest index, and indices are parsed.
            var lead = vm.members.Single(m => m.collectionLead);
            Assert.That(lead.collectionIndex, Is.EqualTo(0));
        }

        [Test]
        public void Build_LoneIndexedSibling_StaysASingleMember()
        {
            var root = MakeRoot("Bar");
            MakeChild(root, "Slot0").AddComponent<Button>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.members, Has.Count.EqualTo(1));
            Assert.That(vm.members[0].IsCollected, Is.False);
        }

        [Test]
        public void Build_MixedTypeIndexedSiblings_NotGrouped()
        {
            // Slot0 (Button) + Slot1 (Toggle) share a stem but differ in type → not a collection.
            var root = MakeRoot("Bar");
            MakeChild(root, "Slot0").AddComponent<Button>();
            MakeChild(root, "Slot1").AddComponent<Toggle>();

            var vm = BinderyHierarchy.Build(root, "View", "~");

            Assert.That(vm.members, Has.Count.EqualTo(2));
            Assert.That(vm.members.Any(m => m.IsCollected), Is.False);
        }

        // -----------------------------------------------------------------------
        // Build — a child that already has its own view composes as a sub-view
        // -----------------------------------------------------------------------

        [Test]
        public void Build_ChildWithOwnView_ComposesAsSubView()
        {
            var root = MakeRoot("Panel");
            var footer = MakeChild(root, "Footer");
            footer.AddComponent<StubBinderyView>();           // Footer is its own view
            MakeChild(footer, "Ok").AddComponent<Button>();   // ...with a child Bindery must NOT walk into

            var vm = BinderyHierarchy.Build(root, "View", "~");

            // Footer surfaces as a typed sub-view member (not a scope), and Ok is NOT walked.
            Assert.That(vm.members, Has.Count.EqualTo(1));
            var m = vm.members[0];
            Assert.That(m.identifier, Is.EqualTo("Footer"));
            Assert.That(m.isScope, Is.False);
            Assert.That(m.csharpType, Is.EqualTo(typeof(StubBinderyView).FullName));
        }
    }

    // A concrete BinderyView used to exercise sub-view composition in tests.
    internal sealed class StubBinderyView : BinderyView { }
}
