// =============================================================================
// Bindery — view code generator. Pure generation (ViewModel in → C# string out):
//   • <Name>View.g.cs — `public partial class FooView : Bindery.BinderyView` with a
//     [SerializeField] reference + a typed accessor per bindable child, and a nested
//     scope class per container (view.Footer.OkButton).
//   • <Name>View.cs   — a one-time, editable behaviour stub (write-if-absent) in the
//     SAME assembly so OnBind() + your code compile against the generated partial.
//   • Bindery.Generated.asmdef — isolates generated code, referenceable from your own
//     assembly definitions.
//
// References are wired on the live object by BinderyWire after compile; the
// generated accessors are therefore plain field reads — no registry, no Find.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Bindery
{
    internal static class BinderyCodeGen
    {
        // Default generated namespace / base class. The configured values ride on the ViewModel
        // (BinderySettings.GeneratedNamespace / .BaseClass); these consts are the fallbacks used when
        // a model leaves them unset and the canonical default the asmdef ships with.
        public const string GeneratedNamespace = "Bindery.Generated";
        public const string DefaultBaseClass = "Bindery.BinderyView";

        // The configured namespace / base class for a view, defaulting when the model didn't set them.
        static string NamespaceOf(ViewModel v) => string.IsNullOrEmpty(v.namespaceName) ? GeneratedNamespace : v.namespaceName;
        static string BaseClassOf(ViewModel v) => string.IsNullOrEmpty(v.baseClass) ? DefaultBaseClass : v.baseClass;

        // ---- <Name>View.g.cs ----------------------------------------------------------
        public static string EmitViewClass(ViewModel v)
        {
            string ns = NamespaceOf(v);
            // `using UnityEngine;` is always needed (SerializeField, RectTransform, GameObject). More
            // are added as types from safe namespaces are emitted (UnityEngine.UI, TMPro, collections).
            var usings = new HashSet<string>(System.StringComparer.Ordinal) { "UnityEngine" };
            string T(string fqn) => Short(fqn, ns, usings);

            var body = new StringBuilder();
            body.AppendLine("namespace " + ns);
            body.AppendLine("{");
            body.AppendLine("    public partial class " + v.className + " : " + BaseClassOf(v));
            body.AppendLine("    {");

            // Backing fields (flat on the view; nested scopes index back into these).
            foreach (var m in v.members)
            {
                if (m.IsCollected)
                {
                    // A collection serializes as ONE T[] per group on its lead (shown as a list in the
                    // Inspector) — or, when the setting is off, an individual field per element plus a
                    // cached IReadOnlyList<T> the accessor lazy-fills.
                    if (v.collectionsAsArray)
                    {
                        if (m.collectionLead)
                            body.AppendLine("        [SerializeField] " + T(m.csharpType) + "[] " + CollectionField(m) + ";");
                    }
                    else
                    {
                        body.AppendLine("        [SerializeField] " + T(m.csharpType) + " " + m.FieldName + ";");
                        if (m.collectionLead)
                            body.AppendLine("        " + ReadOnlyListType(m.csharpType, ns, usings) + " " + CollectionField(m) + ";");
                    }
                    continue;
                }
                body.AppendLine("        [SerializeField] " + T(m.csharpType) + " " + m.FieldName + ";");
                if (m.isScope)
                    body.AppendLine("        " + m.scopeTypeName + " " + m.FieldName + "Scope;");
            }
            if (v.members.Count > 0) body.AppendLine();

            // Accessors exposed directly on the view (top-level children). A collected member's
            // individual accessor is suppressed in favour of the group's collection accessor, which
            // the lead element emits in element-index order.
            foreach (var m in v.members)
            {
                if (!m.exposeOnRoot) continue;
                if (m.IsCollected)
                {
                    if (m.collectionLead)
                        body.AppendLine(EmitCollectionAccessor(v, m, "        ", "", ns, usings));
                    continue;
                }
                if (m.isScope)
                    body.AppendLine("        public " + m.scopeTypeName + " " + m.identifier
                                + " { get { EnsureBound(); return " + m.FieldName + "Scope ??= new " + m.scopeTypeName + "(this); } }");
                else
                    body.AppendLine("        public " + T(m.csharpType) + " " + m.identifier
                                + " { get { EnsureBound(); return " + m.FieldName + "; } }");
            }

            // One nested class per container scope, exposing its direct children.
            foreach (var s in v.members)
            {
                if (!s.isScope) continue;
                body.AppendLine();
                body.AppendLine("        public sealed class " + s.scopeTypeName);
                body.AppendLine("        {");
                body.AppendLine("            readonly " + v.className + " _view;");
                body.AppendLine("            internal " + s.scopeTypeName + "(" + v.className + " view) { _view = view; }");
                body.AppendLine("            public RectTransform RectTransform { get { _view.EnsureBound(); return _view." + s.FieldName + "; } }");
                body.AppendLine("            public GameObject GameObject { get { _view.EnsureBound(); return _view." + s.FieldName + " != null ? _view." + s.FieldName + ".gameObject : null; } }");
                foreach (var c in v.members)
                {
                    if (c.parent != s.node) continue;
                    if (c.IsCollected)
                    {
                        // Collection backing field lives flat on the view; the accessor surfaces here.
                        if (c.collectionLead)
                            body.AppendLine(EmitCollectionAccessor(v, c, "            ", "_view.", ns, usings));
                        continue;
                    }
                    if (c.isScope)
                        body.AppendLine("            public " + c.scopeTypeName + " " + c.identifier
                                    + " { get { _view.EnsureBound(); return _view." + c.FieldName + "Scope ??= new " + c.scopeTypeName + "(_view); } }");
                    else
                        body.AppendLine("            public " + T(c.csharpType) + " " + c.identifier
                                    + " { get { _view.EnsureBound(); return _view." + c.FieldName + "; } }");
                }
                body.AppendLine("        }");
            }

            body.AppendLine("    }");
            body.AppendLine("}");

            // Header: comment + the `using`s actually needed (built-in/safe namespaces only — custom
            // [BinderyBind] component types stay fully qualified, so no surprise namespace collisions).
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated> Bindery accessor view — regenerated on demand. Do not edit.");
            sb.AppendLine("// Add behaviour in the editable " + v.className + ".cs (in the Views folder, NOT regenerated):");
            sb.AppendLine("//   public partial class " + v.className + " { protected override void OnBind() { } }");
            foreach (var u in OrderUsings(usings)) sb.AppendLine("using " + u + ";");
            sb.AppendLine();
            sb.Append(body);
            return sb.ToString();
        }

        // Namespaces whose bare type names are safe to use under a combined `using` set: they don't
        // collide with each other, and a project can't add types to them. Custom-component namespaces
        // stay out — adding `using`s for arbitrary user namespaces is where real ambiguity creeps in.
        static readonly HashSet<string> ShortenNamespaces = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "UnityEngine", "UnityEngine.UI", "TMPro",
        };

        // Shorten a fully-qualified type to its bare name when its namespace is known-safe (registering
        // the `using`) or is the generated file's OWN namespace (sub-views — same namespace, no using).
        // Everything else (custom [BinderyBind] components in arbitrary namespaces) stays qualified.
        static string Short(string fqn, string fileNamespace, HashSet<string> usings)
        {
            int dot = fqn.LastIndexOf('.');
            if (dot < 0) return fqn;
            string ns = fqn.Substring(0, dot);
            string name = fqn.Substring(dot + 1);
            if (ShortenNamespaces.Contains(ns)) { usings.Add(ns); return name; }
            if (ns == fileNamespace) return name;
            return fqn;
        }

        // A stable, conventional using order: System first, then the UnityEngine family, then the rest.
        static IEnumerable<string> OrderUsings(HashSet<string> usings)
        {
            string[] preferred = { "System.Collections.Generic", "UnityEngine", "UnityEngine.UI", "TMPro" };
            foreach (var u in preferred) if (usings.Contains(u)) yield return u;
            var rest = new List<string>();
            foreach (var u in usings) if (System.Array.IndexOf(preferred, u) < 0) rest.Add(u);
            rest.Sort(System.StringComparer.Ordinal);
            foreach (var u in rest) yield return u;
        }

        // ---- collection emit helpers --------------------------------------------------
        // A collected group surfaces as one cached, read-only, ordered accessor:
        //   public IReadOnlyList<Button> Slots => _Slots ??= new Button[] { _Slot0, _Slot1, _Slot2 };
        // Elements are gathered from the group's members (lead + the rest) in parsed-index order.
        // <paramref name="indent"/> sets the surrounding indent; <paramref name="fieldPrefix"/> is
        // "" on the view body or "_view." inside a scope class (the fields are flat on the view).
        static string EmitCollectionAccessor(ViewModel v, ViewMember lead, string indent, string fieldPrefix,
                                             string ns, HashSet<string> usings)
        {
            string listType = ReadOnlyListType(lead.csharpType, ns, usings);
            // Touching the accessor binds the view first (lazy + idempotent), so it works from anywhere
            // — any Awake/Start, any order, even on an inactive view that Unity hasn't Awoken.
            string bind = fieldPrefix + "EnsureBound();";

            // Array mode: the serialized T[] field IS the collection (T[] implements IReadOnlyList<T>).
            if (v.collectionsAsArray)
                return indent + "public " + listType + " " + lead.collectionName
                     + " { get { " + bind + " return " + fieldPrefix + CollectionField(lead) + "; } }";

            // Individual mode: lazily cache an array built from the per-element backing fields.
            var group = new List<ViewMember>();
            foreach (var m in v.members)
                if (m.collectionName == lead.collectionName && m.parent == lead.parent && m.csharpType == lead.csharpType)
                    group.Add(m);
            group.Sort((a, b) => a.collectionIndex.CompareTo(b.collectionIndex));

            var elems = new StringBuilder();
            for (int i = 0; i < group.Count; i++)
            {
                if (i > 0) elems.Append(", ");
                elems.Append(fieldPrefix).Append(group[i].FieldName);
            }
            return indent + "public " + listType + " " + lead.collectionName
                 + " { get { " + bind + " return " + fieldPrefix + CollectionField(lead)
                 + " ??= new " + Short(lead.csharpType, ns, usings) + "[] { " + elems + " }; } }";
        }

        // The cached array field name for a group, e.g. "_Slots" (flat on the view, like backing fields).
        static string CollectionField(ViewMember lead) => "_" + lead.collectionName;

        static string ReadOnlyListType(string elementType, string ns, HashSet<string> usings)
        {
            usings.Add("System.Collections.Generic");
            return "IReadOnlyList<" + Short(elementType, ns, usings) + ">";
        }

        // ---- <Name>View.cs (editable stub, written only if missing) -------------------
        // Optionally pre-wires each control's basic event to a named handler method (with its own
        // body) so the developer has somewhere to put behaviour. Written once, never regenerated.
        public static string EmitBehaviourStub(ViewModel v, bool scaffoldButtons, bool scaffoldControls)
        {
            // scope node → its member, so we can build access paths like "Footer.OkButton".
            var scopeByNode = new Dictionary<Transform, ViewMember>();
            foreach (var m in v.members) if (m.isScope && m.node != null) scopeByNode[m.node] = m;

            var handlers = new List<Handler>();
            foreach (var m in v.members)
            {
                if (m.isScope) continue;
                if (m.IsCollected && !m.collectionLead) continue;          // a collection is scaffolded once, on its lead
                var ev = ControlEvent(m.csharpType);
                if (ev == null) continue;                                  // graphics / sub-views: no event
                bool isButton = m.csharpType == "UnityEngine.UI.Button";
                if (isButton ? !scaffoldButtons : !scaffoldControls) continue;
                bool hasValue = !string.IsNullOrEmpty(ev.ParamDecl);
                if (m.IsCollected)
                    // a collection: wire every element in a loop to one indexed handler
                    handlers.Add(new Handler
                    {
                        isCollection = true,
                        hasValue = hasValue,
                        access = CollectionAccessPath(m, scopeByNode),
                        evt = ev.EventName,
                        name = "On" + m.collectionName + ev.Suffix,
                        param = hasValue ? "int index, " + ev.ParamDecl : "int index",
                        todo = "handle " + m.collectionName + "[index] " + ev.Action,
                    });
                else
                    handlers.Add(new Handler
                    {
                        access = AccessPath(m, scopeByNode),
                        evt = ev.EventName,
                        name = "On" + m.Key + ev.Suffix,
                        param = ev.ParamDecl,
                        todo = "handle " + m.Key + " " + ev.Action,
                    });
            }

            var sb = new StringBuilder();
            sb.AppendLine("// Your code for " + v.className + ". This file is NOT regenerated — safe to edit.");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace " + NamespaceOf(v));
            sb.AppendLine("{");
            sb.AppendLine("    public partial class " + v.className);
            sb.AppendLine("    {");
            sb.AppendLine("        // Runs once before Awake completes, after references are wired.");
            sb.AppendLine("        protected override void OnBind()");
            sb.AppendLine("        {");
            if (handlers.Count == 0)
                sb.AppendLine("            // TODO: bind events here, e.g. someButton.onClick.AddListener(() => { });");
            else
                foreach (var h in handlers)
                {
                    if (h.isCollection)
                    {
                        string lambda = h.hasValue ? "value => " + h.name + "(index, value)" : "() => " + h.name + "(index)";
                        sb.AppendLine("            for (int i = 0; i < " + h.access + ".Count; i++)");
                        sb.AppendLine("            {");
                        sb.AppendLine("                int index = i;");
                        sb.AppendLine("                " + h.access + "[i]." + h.evt + ".AddListener(" + lambda + ");");
                        sb.AppendLine("            }");
                    }
                    else
                        sb.AppendLine("            " + h.access + "." + h.evt + ".AddListener(" + h.name + ");");
                }
            sb.AppendLine("        }");

            foreach (var h in handlers)
            {
                sb.AppendLine();
                sb.AppendLine("        void " + h.name + "(" + h.param + ")");
                sb.AppendLine("        {");
                sb.AppendLine("            // TODO: " + h.todo);
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        sealed class Handler { public string access, evt, name, param, todo; public bool isCollection, hasValue; }
        sealed class ControlEvt { public string EventName, ParamDecl, Suffix, Action; }

        // The basic event Bindery scaffolds per control type — null for graphics / scopes / sub-views.
        static ControlEvt ControlEvent(string csharpType)
        {
            switch (csharpType)
            {
                case "UnityEngine.UI.Button":     return new ControlEvt { EventName = "onClick", ParamDecl = "", Suffix = "Clicked", Action = "click" };
                case "UnityEngine.UI.Toggle":     return Changed("bool");
                case "UnityEngine.UI.Slider":     return Changed("float");
                case "UnityEngine.UI.Scrollbar":  return Changed("float");
                case "UnityEngine.UI.Dropdown":   return Changed("int");
                case "TMPro.TMP_Dropdown":        return Changed("int");
                case "UnityEngine.UI.InputField": return Changed("string");
                case "TMPro.TMP_InputField":      return Changed("string");
                case "UnityEngine.UI.ScrollRect": return Changed("Vector2");
                default:                          return null;
            }
        }

        static ControlEvt Changed(string paramType) =>
            new ControlEvt { EventName = "onValueChanged", ParamDecl = paramType + " value", Suffix = "Changed", Action = "value change" };

        // "Footer.OkButton" etc. — the chain of scope accessors from the root down to the control.
        static string AccessPath(ViewMember m, Dictionary<Transform, ViewMember> scopeByNode)
        {
            if (m.exposeOnRoot) return m.identifier;
            if (m.parent != null && scopeByNode.TryGetValue(m.parent, out var scope))
                return AccessPath(scope, scopeByNode) + "." + m.identifier;
            return m.identifier; // defensive: every non-root member should resolve to a scope
        }

        // Like AccessPath, but to a collection accessor (e.g. "Slots" / "Footer.Slots") — the group
        // surfaces under its lead's effective parent named after the collection, not the element.
        static string CollectionAccessPath(ViewMember lead, Dictionary<Transform, ViewMember> scopeByNode)
        {
            if (lead.exposeOnRoot) return lead.collectionName;
            if (lead.parent != null && scopeByNode.TryGetValue(lead.parent, out var scope))
                return AccessPath(scope, scopeByNode) + "." + lead.collectionName;
            return lead.collectionName;
        }

        // ---- Bindery.Generated.asmdef -------------------------------------------------
        // The built-in references every generated view needs. Custom-component views ([BinderyBind])
        // add the assembly that defines the component on top — passed in via extraAssemblyRefs.
        static readonly string[] BaseAsmdefRefs = { "Bindery.Runtime", "UnityEngine.UI", "Unity.TextMeshPro" };

        /// <summary>Emit the Bindery.Generated asmdef JSON. The assembly NAME stays "Bindery.Generated"
        /// (its identity — referenced by user asmdefs and the wiring); <paramref name="rootNamespace"/>
        /// tracks the configured generated namespace. <paramref name="extraAssemblyRefs"/> are the
        /// assemblies that define any [BinderyBind] custom components surfaced by the generated views;
        /// they're merged after the built-ins and de-duped so a custom-component view actually compiles.
        /// CAVEAT: an extra assembly must NOT reference Bindery.Generated back, or Unity rejects the
        /// cyclic asmdef — keep [BinderyBind] components in their own leaf assembly (v1 limitation).</summary>
        public static string EmitAsmdef(IEnumerable<string> extraAssemblyRefs = null, string rootNamespace = GeneratedNamespace)
        {
            // Built-ins first, then extras in first-seen order, skipping blanks/dupes.
            var refs = new List<string>(BaseAsmdefRefs);
            var seen = new HashSet<string>(refs);
            if (extraAssemblyRefs != null)
                foreach (var r in extraAssemblyRefs)
                    if (!string.IsNullOrEmpty(r) && seen.Add(r)) refs.Add(r);

            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"name\": \"Bindery.Generated\",\n");
            sb.Append("  \"rootNamespace\": \"").Append(string.IsNullOrEmpty(rootNamespace) ? GeneratedNamespace : rootNamespace).Append("\",\n");
            sb.Append("  \"references\": [\n");
            for (int i = 0; i < refs.Count; i++)
                sb.Append("    \"").Append(refs[i]).Append(i < refs.Count - 1 ? "\",\n" : "\"\n");
            sb.Append("  ],\n");
            sb.Append("  \"autoReferenced\": true\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>Emit the typed view registry (<c>BinderyViews</c>): one cached property per
        /// generated view, each finding its instance in the loaded scene(s) on first use, plus a
        /// <c>Refresh()</c> that clears the caches. <paramref name="views"/> is (fully-qualified
        /// type name, property name) pairs.</summary>
        public static string EmitViewsRegistry(string registryNamespace, IReadOnlyList<(string typeName, string property)> views)
        {
            string ns = string.IsNullOrEmpty(registryNamespace) ? GeneratedNamespace : registryNamespace;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated> Bindery view registry — regenerated on demand. Do not edit.");
            sb.AppendLine("// Typed access to every Bindery view in the loaded scene(s): BinderyViews.SettingsPanel, …");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace " + ns);
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Typed access to every Bindery view currently in the loaded scene(s). Each");
            sb.AppendLine("    /// property finds its view on first use and caches it (cleared by <see cref=\"Refresh\"/>).</summary>");
            sb.AppendLine("    public static class BinderyViews");
            sb.AppendLine("    {");
            foreach (var (typeName, property) in views)
            {
                string field = "_" + property;
                sb.AppendLine("        static " + typeName + " " + field + ";");
                sb.AppendLine("        /// <summary>The <see cref=\"" + typeName + "\"/> in the loaded scene(s), or null if none.");
                sb.AppendLine("        /// Found (including inactive) on first use, cached, and bound before it's returned.</summary>");
                sb.AppendLine("        public static " + typeName + " " + property);
                sb.AppendLine("        {");
                sb.AppendLine("            get");
                sb.AppendLine("            {");
                sb.AppendLine("                if (!" + field + ") " + field +
                              " = Object.FindFirstObjectByType<" + typeName + ">(FindObjectsInactive.Include);");
                sb.AppendLine("                if (" + field + ") " + field + ".EnsureBound();");
                sb.AppendLine("                return " + field + ";");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            sb.AppendLine("        /// <summary>Forget every cached lookup — call after loading or unloading a scene.</summary>");
            sb.AppendLine("        public static void Refresh()");
            sb.AppendLine("        {");
            foreach (var (_, property) in views)
                sb.AppendLine("            _" + property + " = null;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }
    }
}
