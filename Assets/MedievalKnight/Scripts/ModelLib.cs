using System;
using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // The general way to take in a model file (.fbx) from Resources: cached, a missing file is null and never an
    // exception, and everything that came along with the file (cameras, lights, colliders, scripts) is removed.
    // Used by the knight's file body (later) and available to anything else. Weapon.BuildFileModel does the same
    // for swords and is left as it is.
    public static class ModelLib
    {
        static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        static readonly HashSet<string> warned = new HashSet<string>();

        // The loaded prefab (not an instance) or null when the file is not there. Warns once per path.
        public static GameObject Model(string resPath)
        {
            if (string.IsNullOrEmpty(resPath)) return null;
            GameObject g;
            if (models.TryGetValue(resPath, out g)) return g;
            try { g = Resources.Load<GameObject>(resPath); }
            catch (Exception ex) { Warn(resPath, ex.Message); g = null; }
            models[resPath] = g;
            if (g == null) Warn(resPath, "not found in Resources");
            return g;
        }

        // The first clip of an animation file that is not a preview clip, or null.
        public static AnimationClip Clip(string resPath)
        {
            if (string.IsNullOrEmpty(resPath)) return null;
            AnimationClip c;
            if (clips.TryGetValue(resPath, out c)) return c;
            c = null;
            try
            {
                AnimationClip[] all = Resources.LoadAll<AnimationClip>(resPath);
                foreach (AnimationClip a in all)
                    if (a != null && !a.name.StartsWith("__preview")) { c = a; break; }
            }
            catch (Exception ex) { Warn(resPath, ex.Message); }
            clips[resPath] = c;
            if (c == null) Warn(resPath, "no animation clip");
            return c;
        }

        static void Warn(string path, string why)
        {
            if (warned.Add(path)) Debug.LogWarning("MODELLIB: '" + path + "': " + why);
        }

        // An instance of the model under parent with the extras removed; null when the file is missing.
        public static GameObject Spawn(string resPath, Transform parent)
        {
            GameObject src = Model(resPath);
            if (src == null) return null;
            try
            {
                GameObject inst = UnityEngine.Object.Instantiate(src, parent);
                inst.name = "File";
                Strip(inst);
                return inst;
            }
            catch (Exception ex) { Warn(resPath, "spawn failed: " + ex.Message); return null; }
        }

        // Removes cameras, lights, colliders, rigid bodies and scripts that came with a file (animators stay: they are
        // what plays the clips; the owner decides). Never touches the root itself.
        public static void Strip(GameObject inst)
        {
            foreach (Camera c in inst.GetComponentsInChildren<Camera>(true)) if (c != null && c.gameObject != inst) UnityEngine.Object.DestroyImmediate(c.gameObject);
            foreach (Light l in inst.GetComponentsInChildren<Light>(true)) if (l != null && l.gameObject != inst) UnityEngine.Object.DestroyImmediate(l.gameObject);
            foreach (Collider c in inst.GetComponentsInChildren<Collider>(true)) if (c != null) UnityEngine.Object.DestroyImmediate(c);
            foreach (Rigidbody rb in inst.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) UnityEngine.Object.DestroyImmediate(rb);
            foreach (MonoBehaviour b in inst.GetComponentsInChildren<MonoBehaviour>(true)) if (b != null) UnityEngine.Object.DestroyImmediate(b);
        }

        // Replaces every material slot with a Prims.Lit of the colour the picker gives for the slot's material name.
        // Returns the names of slots the picker did not know (it returns null for those; they get "fallback").
        public static List<string> Recolor(GameObject inst, Func<string, Color?> picker, Color fallback)
        {
            var unknown = new List<string>();
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
            {
                Material[] src = r.sharedMaterials, dst = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    string n = src[i] != null ? src[i].name : "";
                    Color? c = picker(n);
                    if (!c.HasValue) { unknown.Add(n); c = fallback; }
                    dst[i] = Prims.Lit(c.Value);
                }
                r.sharedMaterials = dst;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return unknown;
        }

        // Counts the triangles of every mesh under a root (skinned or not).
        public static int Triangles(GameObject inst)
        {
            int n = 0;
            foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) n += mf.sharedMesh.triangles.Length / 3;
            foreach (SkinnedMeshRenderer sm in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (sm.sharedMesh != null) n += sm.sharedMesh.triangles.Length / 3;
            return n;
        }
    }

    // Effect prefabs from Resources/Effects/<name>. No file = nothing happens (the game has its own effects in Fx.cs
    // and keeps using them), so a build without the folder plays exactly the same.
    public static class EffectLib
    {
        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
        public static int Played { get; private set; }          // test counter: effects that came from a file

        public static bool Play(string name, Vector3 pos, Quaternion rot, float life = 2f)
        {
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                GameObject src;
                if (!cache.TryGetValue(name, out src))
                {
                    src = Resources.Load<GameObject>("Effects/" + name);
                    cache[name] = src;
                }
                if (src == null) return false;
                GameObject go = UnityEngine.Object.Instantiate(src, pos, rot);
                UnityEngine.Object.Destroy(go, life);
                Played++;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("EFFECTLIB: '" + name + "': " + ex.Message);
                return false;
            }
        }
    }
}
