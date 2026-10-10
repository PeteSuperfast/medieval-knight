using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // List of the approved Art_kit prefabs (Assets/Prefabs). It lives in Resources/Kit/KitCatalog.prefab so the player build
    // contains exactly these prefabs, their meshes and materials. Made by AssetImport (editor menu MedievalKnight/Asset Import).
    public class KitCatalog : MonoBehaviour
    {
        public GameObject[] prefabs;
    }

    // Runtime access for WorldBuilder: the prefab's meshes with their transform relative to the prefab root and their materials.
    // The scene is still built by code and merged by StaticBatcher; if the catalog is missing everything falls back to the
    // old primitive scenery.
    public static class Kit
    {
        public struct Part
        {
            public Mesh mesh;
            public Matrix4x4 local;
            public Material[] mats;      // one per submesh, de-duplicated by colour so chunks need fewer materials
            public string[] keys;        // the original material names without "MK_" (for per-zone colour overrides)
        }

        static Dictionary<string, Part[]> cache;
        static readonly Dictionary<string, Material> canon = new Dictionary<string, Material>();

        static void Load()
        {
            cache = new Dictionary<string, Part[]>();
            var go = Resources.Load<GameObject>("Kit/KitCatalog");
            var cat = go != null ? go.GetComponent<KitCatalog>() : null;
            if (cat == null || cat.prefabs == null) { Debug.LogWarning("KIT: no catalog in Resources/Kit, using primitive scenery"); return; }
            foreach (GameObject p in cat.prefabs)
            {
                if (p == null) continue;
                var parts = new List<Part>();
                Matrix4x4 inv = p.transform.worldToLocalMatrix;
                foreach (MeshFilter mf in p.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mr = mf.GetComponent<MeshRenderer>();
                    if (mr == null || mf.sharedMesh == null) continue;
                    Material[] src = mr.sharedMaterials;
                    var part = new Part { mesh = mf.sharedMesh, local = inv * mf.transform.localToWorldMatrix, mats = new Material[src.Length], keys = new string[src.Length] };
                    for (int i = 0; i < src.Length; i++)
                    {
                        string n = src[i].name;
                        part.keys[i] = n.StartsWith("MK_") ? n.Substring(3) : n;
                        part.mats[i] = Canonical(src[i]);
                    }
                    parts.Add(part);
                }
                cache[p.name] = parts.ToArray();
            }
            Debug.Log("KIT: " + cache.Count + " prefabs, " + canon.Count + " shared materials");
        }

        static Material Canonical(Material m)
        {
            Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            string key = ColorUtility.ToHtmlStringRGB(c) + (m.IsKeywordEnabled("_EMISSION") ? "E" : "");
            Material have;
            if (!canon.TryGetValue(key, out have)) { canon[key] = m; have = m; }
            return have;
        }

        public static Part[] Get(string prefabName)
        {
            if (cache == null) Load();
            Part[] p;
            return cache.TryGetValue(prefabName, out p) ? p : null;
        }
    }
}
