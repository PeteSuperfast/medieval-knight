using System.Collections.Generic;
using UnityEngine;

namespace MedievalKnight
{
    // Builds the snow map's scene from Resources/Snow (SnowLayout): the terrain / water / path files as their own renderers (so every
    // 95 m chunk is culled by itself) and the ~1,140 placed pieces merged by StaticBatcher. Everything is fixed data; nothing is random.
    // Game.SetBiome calls this the first time the snow map is wanted (behind the black screen); the scene is then only switched on / off.
    public static class SnowBuilder
    {
        public static string LastStats = "";

        static bool NoShadow(string proto)
        {
            return proto.StartsWith("snow_terrain_chunk") || proto.StartsWith("snow_terrain_drifts") || proto.StartsWith("snow_water")
                || proto.StartsWith("snow_path") || proto.StartsWith("snow_scene") || proto.StartsWith("snow_village") || proto.StartsWith("snow_plant");
        }

        public static void Build(Transform parent)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SnowLayout.Load();
            SnowLayoutDto lay = SnowLayout.Layout;
            var b = new StaticBatcher();
            int n = lay.protos != null ? lay.inst.Length / 11 : 0;
            int direct = 0, batched = 0, spinners = 0, missing = 0;
            var tris = 0;
            for (int i = 0; i < n; i++)
            {
                int o = i * 11;
                string proto = lay.protos[(int)lay.inst[o]];
                Kit.Part[] parts = Kit.Get(proto);
                if (parts == null || parts.Length == 0) { missing++; continue; }
                Vector3 pos = new Vector3(lay.inst[o + 1], lay.inst[o + 2], lay.inst[o + 3]);
                var rot = new Quaternion(lay.inst[o + 4], lay.inst[o + 5], lay.inst[o + 6], lay.inst[o + 7]);
                Vector3 scale = new Vector3(lay.inst[o + 8], lay.inst[o + 9], lay.inst[o + 10]);
                Matrix4x4 root = Matrix4x4.TRS(pos, rot, scale);
                bool shadow = !NoShadow(proto);
                bool direct1 = lay.world != null && i < lay.world.Length && lay.world[i];
                bool sails = proto == "snow_building_windmillsails_v01";
                if (sails)
                {
                    // the sails turn about the Blender Y axis = the prefab's Z axis (hub = the piece's pivot); looks only, never synced
                    var hub = Prims.Node(parent, "WindmillSails", pos);
                    hub.rotation = rot;
                    hub.localScale = scale;
                    hub.gameObject.AddComponent<Spinner>().axis = Vector3.forward;
                    hub.gameObject.GetComponent<Spinner>().degPerSec = 22f;
                    foreach (Kit.Part part in parts) AddRenderer(hub, part, Matrix4x4.identity, true);
                    spinners++;
                    continue;
                }
                foreach (Kit.Part part in parts)
                {
                    tris += part.mesh.triangles.Length / 3;
                    if (direct1)
                    {
                        AddRenderer(parent, part, root, shadow);
                        direct++;
                    }
                    else
                    {
                        int sub = part.mesh.subMeshCount;
                        for (int s = 0; s < sub; s++)
                        {
                            int j = Mathf.Min(s, part.mats.Length - 1);
                            b.Add(part.mesh, root * part.local, part.mats[j], shadow, s);
                            batched++;
                        }
                    }
                }
            }
            int chunks = b.Build(parent);
            MakeObstacles();
            LastStats = "SNOWWORLD built in " + sw.ElapsedMilliseconds + " ms: " + n + " pieces, " + batched + " batched parts into " + chunks + " chunk meshes, "
                + direct + " direct renderers, " + spinners + " spinner(s), " + tris + " tris, " + missing + " prefabs missing";
            Debug.Log(LastStats);
        }

        // The collision of the snow map: the circles / boxes of the placed pieces plus the boxes of the baked "no walking" table
        // (water, cliffs, mountains, castle walls). Cleared and filled again each time: the same file gives the same set.
        public static void MakeObstacles()
        {
            SnowLayout.Load();
            Biome was = MapLayout.Current;
            Obstacles.Use(Biome.Snow);
            Obstacles.Clear();
            float[] o = SnowLayout.Layout.obs;
            for (int i = 0; i + 6 < o.Length; i += 7)
            {
                int mask = (int)o[i + 6];
                if (o[i] < 0.5f) Obstacles.AddCircle(new Vector2(o[i + 1], o[i + 2]), o[i + 3], mask);
                else Obstacles.AddBox(new Vector2(o[i + 1], o[i + 2]), new Vector2(o[i + 3], o[i + 4]), o[i + 5], mask);
            }
            Obstacles.Use(was);
        }

        static void AddRenderer(Transform parent, Kit.Part part, Matrix4x4 root, bool shadow)
        {
            Matrix4x4 m = root * part.local;
            var go = new GameObject(part.mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(m.GetColumn(3), m.rotation);
            go.transform.localScale = m.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = part.mats;
            mr.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }
    }
}
