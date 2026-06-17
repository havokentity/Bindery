// =============================================================================
// Bindery — post-compile wiring. Generating a view writes its .cs but the type
// doesn't exist until the next compile, so the live object can't be touched yet.
// Each queued view records, via GlobalObjectId (stable across the domain reload):
// the target object, the generated type name, and per reference the backing-field
// name + the child object + its component type. After scripts reload we resolve
// those ids, attach the generated component (or reuse an existing one), and fill
// each [SerializeField] through a SerializedObject. Entries whose type isn't
// compiled yet are kept and retried on the next reload. Idempotent.
//
// The queue lives in SessionState (survives domain reloads within an editor
// session). Scene objects and prefab instances are supported; prefab *assets*
// selected in the Project window are not (v1).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bindery
{
    internal static class BinderyWire
    {
        const string SessionKey = "Bindery.PendingWire";

        [Serializable] class PMember { public string field; public string gid; public string type; }
        [Serializable] class PView { public string rootGid; public string typeName; public List<PMember> members = new List<PMember>(); }
        [Serializable] class PSet { public List<PView> views = new List<PView>(); }

        static bool _pending;

        [DidReloadScripts]
        static void OnScriptsReloaded() => RequestWire();

        /// <summary>Capture a generated view for wiring (GlobalObjectIds are read here, while
        /// the objects are live). Replaces any prior entry for the same object + type.</summary>
        public static void Enqueue(ViewModel m)
        {
            var set = Load();
            var pv = new PView
            {
                rootGid = GidOf(m.root.gameObject),
                typeName = BinderyCodeGen.GeneratedNamespace + "." + m.className,
            };
            foreach (var mem in m.members)
                pv.members.Add(new PMember { field = mem.FieldName, gid = GidOf(mem.node.gameObject), type = mem.csharpType });

            set.views.RemoveAll(v => v.rootGid == pv.rootGid && v.typeName == pv.typeName);
            set.views.Add(pv);
            Save(set);
        }

        /// <summary>Process the queue on the next tick (coalesced). Called after a Refresh and
        /// from the script-reload hook.</summary>
        public static void RequestWire()
        {
            if (_pending) return;
            _pending = true;
            EditorApplication.delayCall += () => { _pending = false; Process(); };
        }

        static void Process()
        {
            var set = Load();
            if (set.views.Count == 0) return;

            var remaining = new List<PView>();
            bool dirty = false;
            foreach (var v in set.views)
            {
                var type = ResolveType(v.typeName);
                if (type == null) { remaining.Add(v); continue; }     // not compiled yet — retry next reload
                if (!Wire(v, type)) { remaining.Add(v); continue; }   // target not resolvable yet
                dirty = true;
            }

            set.views = remaining;
            Save(set);
            if (dirty && !Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        static bool Wire(PView v, Type type)
        {
            var rootGo = ResolveGameObject(v.rootGid);
            if (rootGo == null) return false;

            var comp = rootGo.GetComponent(type);
            if (comp == null) comp = rootGo.AddComponent(type);
            if (comp == null) return false;

            var so = new SerializedObject(comp);
            foreach (var m in v.members)
            {
                var prop = so.FindProperty(m.field);
                if (prop == null) continue;
                prop.objectReferenceValue = ResolveReference(m);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(comp);
            return true;
        }

        static UnityEngine.Object ResolveReference(PMember m)
        {
            var go = ResolveGameObject(m.gid);
            if (go == null) return null;
            var ct = ResolveType(m.type);
            if (ct == null || ct == typeof(RectTransform)) return go.GetComponent<RectTransform>();
            return go.GetComponent(ct);
        }

        // ---- GlobalObjectId helpers ---------------------------------------------------
        static string GidOf(GameObject go) => GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();

        static GameObject ResolveGameObject(string gid)
        {
            if (string.IsNullOrEmpty(gid) || !GlobalObjectId.TryParse(gid, out var g)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(g) as GameObject;
        }

        // ---- type resolution ----------------------------------------------------------
        static Type ResolveType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        // ---- session-backed queue -----------------------------------------------------
        static PSet Load()
        {
            var json = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(json)) return new PSet();
            try { return JsonUtility.FromJson<PSet>(json) ?? new PSet(); }
            catch { return new PSet(); }
        }

        static void Save(PSet set) => SessionState.SetString(SessionKey, JsonUtility.ToJson(set));
    }
}
