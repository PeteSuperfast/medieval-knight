using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Editor-only (snow-map-models step 0). Imports Assets/Models/Snow/*.fbx (approved models, never edited) as prefabs, makes
// Resources/Kit/SnowCatalog.prefab, checks the Blender -> Unity axes, builds the Showcase_Snow scene and photographs it.
//   Unity -batchmode -projectPath <proj> -executeMethod SnowImport.RunStep0        (needs a GPU: no -nographics)
//   Unity -batchmode -quit -projectPath <proj> -executeMethod SnowImport.PrefabsOnly
public static class SnowImport
{
    public const string FbxDir = "Assets/Models/Snow";
    public const string MatDir = "Assets/Materials/Snow";
    public const string PrefabDir = "Assets/Prefabs/Snow";
    public const string CatalogPath = "Assets/MedievalKnight/Resources/Kit/SnowCatalog.prefab";
    public const string LayoutPath = "Docs/snow-map-models/layout.json";
    public const string ScenePath = "Assets/Scenes/Showcase_Snow.unity";
    public const string OutDir = "Docs/snow-map-models/ingame";

    [Serializable] public class Inst { public string name, fbx; public float[] pos, rot_deg, scale; }
    [Serializable] public class Chunk { public string id, fbx; public float[] pivot_world; }
    [Serializable] public class Layout { public List<Inst> instances; public List<Chunk> terrain_chunks; }

    // palette: make_snow_map.py PAL (approved, style.md incl. N1-N3)
    static readonly Dictionary<string, string> Palette = new Dictionary<string, string>
    {
        {"grass_light","#8DB04A"}, {"grass_mid","#6C8535"}, {"pine_dark","#465820"}, {"leaf_light","#A0B85B"},
        {"wood_light","#A1773D"}, {"wood_dark","#715332"}, {"dirt","#8A5A32"}, {"sand","#D9BC7E"},
        {"stone_light","#ADABB7"}, {"stone_mid","#958A80"}, {"plaster","#D8CCB0"},
        {"roof_purple","#5E5878"}, {"roof_red","#B5523B"}, {"iron","#3A3A3A"},
        {"glow","#F5C25A"}, {"ember","#C8483A"}, {"water","#1594BB"},
        {"flower_y","#F5C25A"}, {"flower_w","#D8CCB0"}, {"flower_p","#5E5878"}, {"flower_r","#C8483A"},
        {"snow_light","#EEF3F8"}, {"snow_shadow","#C9D8E8"}, {"ice_light","#BFE6F2"}, {"ice_dark","#6FB8D6"},
        {"stone_deep","#6E6A78"}, {"ice_mid","#8FD0E4"}, {"red_deep","#9C3B32"},
    };

    static void Log(string s) { Debug.Log("SNOWIMPORT: " + s); }

    public static List<string> FbxNames()
    {
        var l = new List<string>();
        foreach (string f in Directory.GetFiles(FbxDir, "*.fbx")) l.Add(Path.GetFileNameWithoutExtension(f));
        l.Sort(StringComparer.Ordinal);
        return l;
    }

    public static Layout LoadLayout() { return JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath)); }

    // Blender (E,N,Z) -> Unity (E,Z,N)
    public static Vector3 ToUnityPos(float[] p) { return new Vector3(p[0], p[2], p[1]); }

    // Blender euler XYZ (R = Rz Ry Rx), degrees -> Unity rotation of the root (layout.json unity_axis_mapping.instance_rotation)
    public static Quaternion ToUnityRot(float[] r)
    {
        Quat qx = Quat.Axis(1, 0, 0, r[0]), qy = Quat.Axis(0, 1, 0, r[1]), qz = Quat.Axis(0, 0, 1, r[2]);
        Quat q = Quat.Mul(qz, Quat.Mul(qy, qx));
        return new Quaternion(-q.x, -q.z, -q.y, q.w);
    }

    struct Quat
    {
        public float w, x, y, z;
        public static Quat Axis(float ax, float ay, float az, float deg)
        {
            float h = deg * Mathf.Deg2Rad * 0.5f, s = Mathf.Sin(h);
            return new Quat { w = Mathf.Cos(h), x = ax * s, y = ay * s, z = az * s };
        }
        public static Quat Mul(Quat a, Quat b)
        {
            return new Quat
            {
                w = a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z,
                x = a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                y = a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
                z = a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            };
        }
    }

    public static Vector3 ToUnityScale(float[] s) { return new Vector3(s[0], s[2], s[1]); }

    public class Placed { public string proto, name; public Vector3 pos, scale; public Quaternion rot; public bool world; }

    // every piece of the map in "Blender world" Unity coordinates (E, Z, N): terrain chunks, placed instances (layout.json order),
    // then the files that already hold their own world coordinates (water, paths, drifts, lane, plaza, chains, mist, clouds)
    public static List<Placed> AllPlaced()
    {
        var lay = LoadLayout();
        var l = new List<Placed>();
        foreach (var c in lay.terrain_chunks)
            l.Add(new Placed { proto = c.fbx, name = c.id, pos = ToUnityPos(c.pivot_world), rot = Quaternion.identity, scale = Vector3.one });
        foreach (var i in lay.instances)
            l.Add(new Placed { proto = i.fbx, name = i.name, pos = ToUnityPos(i.pos), rot = ToUnityRot(i.rot_deg), scale = ToUnityScale(i.scale) });
        var used = new HashSet<string>();
        foreach (var p in l) used.Add(p.proto);
        foreach (string n in FbxNames())
        {
            if (used.Contains(n)) continue;
            if (n.StartsWith("snow_terrain_drifts") || n.StartsWith("snow_water") || n.StartsWith("snow_path") || n.StartsWith("snow_village")
                || n.StartsWith("snow_deco_chains") || n.StartsWith("snow_scene"))
                l.Add(new Placed { proto = n, name = n, pos = Vector3.zero, rot = Quaternion.identity, scale = Vector3.one, world = true });
        }
        return l;
    }

    // ------------------------------------------------------------------ entry points

    public static void RunStep0()
    {
        try
        {
            PrefabsOnly();
            var urp = AssetImport.EnsureShowcasePipeline();
            var profile = SnowPostProfile();
            var sky = AssetImport.EnsureSkyMaterial();
            BuildShowcase(profile, sky);
            StartCapture();
        }
        catch (Exception e)
        {
            Debug.LogError("SNOWIMPORT: FAILED " + e);
            EditorApplication.Exit(1);
        }
    }

    public static void PrefabsOnly()
    {
        ImportSettings();
        BuildPrefabs();
        MakeCatalog();
        AxisCheck();
    }

    [MenuItem("MedievalKnight/Snow/Import + Prefabs")]
    public static void PrefabsMenu() { PrefabsOnly(); }

    // ------------------------------------------------------------------ importer settings (only the .meta changes)

    static void ImportSettings()
    {
        AssetDatabase.Refresh();
        int changed = 0;
        foreach (string n in FbxNames())
        {
            string path = FbxDir + "/" + n + ".fbx";
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogError("SNOWIMPORT: no importer " + path); continue; }
            bool dirty = false;
            if (!imp.isReadable) { imp.isReadable = true; dirty = true; }                        // CombineMeshes at runtime
            if (imp.importAnimation) { imp.importAnimation = false; dirty = true; }
            if (imp.importCameras) { imp.importCameras = false; dirty = true; }
            if (imp.importLights) { imp.importLights = false; dirty = true; }
            if (imp.importBlendShapes) { imp.importBlendShapes = false; dirty = true; }
            if (imp.addCollider) { imp.addCollider = false; dirty = true; }
            if (imp.importNormals != ModelImporterNormals.Import) { imp.importNormals = ModelImporterNormals.Import; dirty = true; }
            if (imp.meshCompression != ModelImporterMeshCompression.Off) { imp.meshCompression = ModelImporterMeshCompression.Off; dirty = true; }
            if (imp.animationType != ModelImporterAnimationType.None) { imp.animationType = ModelImporterAnimationType.None; dirty = true; }
            if (dirty) { imp.SaveAndReimport(); changed++; }
        }
        Log("importer settings updated on " + changed + " files");
    }

    // ------------------------------------------------------------------ materials + prefabs

    static string Key(string matName)
    {
        string n = matName;
        int dot = n.LastIndexOf('.');
        if (dot > 0 && dot < n.Length - 1 && char.IsDigit(n[n.Length - 1])) n = n.Substring(0, dot);
        if (n.StartsWith("MK_")) n = n.Substring(3);
        return n;
    }

    static Material SnowMaterial(string key, Material imported)
    {
        AssetImport.EnsureDir(MatDir);
        string path = MatDir + "/MK_" + key + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) { if (m.GetFloat("_Cull") != 2f) { m.SetFloat("_Cull", 2f); EditorUtility.SetDirty(m); } return m; }
        Color c;
        string hex;
        if (Palette.TryGetValue(key, out hex)) c = AssetImport.Hex(hex);
        else
        {
            c = imported != null && imported.HasProperty("_BaseColor") ? imported.GetColor("_BaseColor") : Color.magenta;
            Debug.LogWarning("SNOWIMPORT: material '" + key + "' not in palette, using imported colour");
        }
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MK_" + key };
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", key == "water" || key.StartsWith("ice") ? 0.35f : 0.08f);
        m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        if (key == "glow" || key == "ember")
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c.linear * 1.6f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // collider kind per prototype name (prefab colliders are for the editor / future use; the game's own collision is
    // the 2D Obstacles table built from SnowBake)
    static string ColliderKind(string n)
    {
        if (n.StartsWith("snow_terrain") || n.StartsWith("snow_water") || n.StartsWith("snow_path") || n.StartsWith("snow_scene")
            || n.StartsWith("snow_village") || n.StartsWith("snow_plant") || n.Contains("deco_chains") || n.Contains("icelake")
            || n.Contains("windmillsails") || n.Contains("banner") || n.Contains("slabarc") || n.Contains("tree_log")) return "none";
        if (n.StartsWith("snow_tree_pine") || n.Contains("tree_bare") || n.Contains("tree_round") || n.Contains("tree_tall")
            || n.Contains("tree_blossom") || n.Contains("tree_stump")) return "trunk";
        return "box";
    }

    static void BuildPrefabs()
    {
        AssetDatabase.Refresh();
        AssetImport.EnsureDir(PrefabDir);
        int count = 0, tris = 0;
        foreach (string name in FbxNames())
        {
            string fbx = FbxDir + "/" + name + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (model == null) { Debug.LogError("SNOWIMPORT: missing model " + fbx); continue; }
            var root = new GameObject(name);
            // the FBX imports mirrored on X (root keeps its -90 deg X rotation): a child turned 180 deg about Y gives geometry
            // = (x, z, y) of the Blender axes, so root.position = (E, Z, N) (layout.json unity_axis_mapping.prefab_wrapper)
            var pivot = new GameObject("Pivot");
            pivot.transform.SetParent(root.transform, false);
            pivot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot.transform);
            inst.transform.localPosition = Vector3.zero;

            int t = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var src = r.sharedMaterials;
                DoubleSidedFix(r, name, src);
                var dst = new Material[src.Length];
                for (int i = 0; i < src.Length; i++) dst[i] = SnowMaterial(Key(src[i] != null ? src[i].name : "none"), src[i]);
                r.sharedMaterials = dst;
                bool big = name.StartsWith("snow_terrain_chunk") || name.StartsWith("snow_water") || name.StartsWith("snow_path")
                    || name.StartsWith("snow_scene") || name.StartsWith("snow_village");
                r.shadowCastingMode = (big && !name.Contains("waterfall")) ? ShadowCastingMode.Off : ShadowCastingMode.On;
                if (name.StartsWith("snow_scene")) r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) t += mf.sharedMesh.triangles.Length / 3;
            }
            AddCollider(root, ColliderKind(name));
            AssetImport.SetStatic(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            count++; tris += t;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Log("prefabs " + count + " tris(sum of prototypes)=" + tris + " doubledTris=" + DoubledTris);
    }

    // Some roof / cloth faces of the approved models wind inward (Blender draws both sides). The player culls back faces, so for
    // these materials the submesh gets a second copy of its triangles wound the other way with flipped normals (new Mesh asset in
    // Assets/Prefabs/Snow/Meshes; the .fbx stays untouched). Lighting stays correct, no shader change.
    static readonly HashSet<string> TwoSided = new HashSet<string> { "roof_red", "roof_purple", "red_deep" };
    public static int DoubledTris;

    static void DoubleSidedFix(MeshRenderer r, string prefabName, Material[] src)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        Mesh m = mf.sharedMesh;
        var need = new bool[m.subMeshCount];
        bool any = false;
        for (int i = 0; i < m.subMeshCount && i < src.Length; i++)
        {
            string key = Key(src[i] != null ? src[i].name : "none");
            if (prefabName.Contains("icelake") || TwoSided.Contains(key) || (key == "plaster" && (prefabName.Contains("stall") || prefabName.Contains("sails") || prefabName.Contains("banner")))) { need[i] = true; any = true; }
        }
        if (!any) return;
        var verts = new List<Vector3>(m.vertices);
        var norms = new List<Vector3>(m.normals);
        var uvs = new List<Vector2>(); m.GetUVs(0, uvs);
        bool hasUv = uvs.Count == verts.Count;
        var tris = new List<int[]>();
        for (int s = 0; s < m.subMeshCount; s++)
        {
            var idx = new List<int>(m.GetTriangles(s));
            if (need[s])
            {
                var map = new Dictionary<int, int>();
                int n = idx.Count;
                var extra = new List<int>(n);
                for (int k = 0; k < n; k += 3)
                {
                    int[] t3 = new int[3];
                    for (int c = 0; c < 3; c++)
                    {
                        int o = idx[k + c], nv;
                        if (!map.TryGetValue(o, out nv))
                        {
                            nv = verts.Count; map[o] = nv;
                            verts.Add(verts[o]); norms.Add(-norms[o]); if (hasUv) uvs.Add(uvs[o]);
                        }
                        t3[c] = nv;
                    }
                    extra.Add(t3[0]); extra.Add(t3[2]); extra.Add(t3[1]);
                }
                DoubledTris += n / 3;
                idx.AddRange(extra);
            }
            tris.Add(idx.ToArray());
        }
        var nm = new Mesh { name = m.name + "_ds", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        nm.SetVertices(verts); nm.SetNormals(norms);
        if (hasUv) nm.SetUVs(0, uvs);
        nm.subMeshCount = tris.Count;
        for (int s = 0; s < tris.Count; s++) nm.SetTriangles(tris[s], s);
        nm.RecalculateBounds();
        AssetImport.EnsureDir(PrefabDir + "/Meshes");
        string path = PrefabDir + "/Meshes/" + prefabName + "_" + r.gameObject.name + ".asset";
        AssetDatabase.CreateAsset(nm, path);
        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    static void AddCollider(GameObject root, string kind)
    {
        if (kind == "none") return;
        Bounds b = AssetImport.BoundsOf(root.GetComponentsInChildren<MeshRenderer>(true));
        if (kind == "trunk")
        {
            var c = root.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.radius = 0.5f;
            c.height = Mathf.Min(2.2f, b.size.y);
            c.center = new Vector3(b.center.x, c.height * 0.5f, b.center.z);
            return;
        }
        var bc = root.AddComponent<BoxCollider>();
        bc.center = b.center; bc.size = b.size;
    }

    public static void MakeCatalog()
    {
        var list = new List<GameObject>();
        foreach (string n in FbxNames())
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + n + ".prefab");
            if (pf != null) list.Add(pf);
        }
        AssetImport.EnsureDir("Assets/MedievalKnight/Resources/Kit");
        var go = new GameObject("SnowCatalog");
        go.AddComponent<MedievalKnight.KitCatalog>().prefabs = list.ToArray();
        PrefabUtility.SaveAsPrefabAsset(go, CatalogPath);
        UnityEngine.Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Log("catalog with " + list.Count + " prefabs");
    }

    // ------------------------------------------------------------------ axis check (assert, stop on failure)

    static Bounds PlacedBounds(string prefabName, Vector3 pos)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + prefabName + ".prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
        go.transform.position = pos;
        Bounds b = AssetImport.BoundsOf(go.GetComponentsInChildren<MeshRenderer>(true));
        UnityEngine.Object.DestroyImmediate(go);
        return b;
    }

    static void AxisCheck()
    {
        var lay = LoadLayout();
        bool ok = true;
        foreach (var c in lay.terrain_chunks)
        {
            Vector3 p = ToUnityPos(c.pivot_world);
            Bounds b = PlacedBounds(c.fbx, p);
            float dx = b.center.x - p.x, dz = b.center.z - p.z;
            bool good = Mathf.Abs(dx) < 1f && Mathf.Abs(dz) < 1f && b.size.x < 100f && b.size.z < 100f;
            if (c.id == "r2c2") good = Mathf.Abs(dx) < 200f;     // contains a map-size piece, see report
            Log("AXIS chunk " + c.id + " centre=" + b.center.ToString("F1") + " size=" + b.size.ToString("F1") + " expect=(" + p.x + "," + p.z + ") " + (good ? "OK" : "BAD"));
            if (!good && c.id != "r2c2") ok = false;
        }
        foreach (var i in lay.instances)
        {
            if (!i.fbx.StartsWith("snow_castle") && i.fbx != "snow_building_windmillbody_v01" && i.fbx != "snow_prop_fountain_v01" && i.fbx != "snow_bridge_s1_v01") continue;
            Vector3 p = ToUnityPos(i.pos);
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + i.fbx + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            go.transform.SetPositionAndRotation(p, ToUnityRot(i.rot_deg));
            go.transform.localScale = ToUnityScale(i.scale);
            Bounds b = AssetImport.BoundsOf(go.GetComponentsInChildren<MeshRenderer>(true));
            UnityEngine.Object.DestroyImmediate(go);
            Log("AXIS " + i.name + " centre=" + b.center.ToString("F1") + " size=" + b.size.ToString("F1") + " pivot=" + p.ToString("F1"));
        }
        if (!ok) { Debug.LogError("SNOWIMPORT: AXIS CHECK FAILED"); EditorApplication.Exit(2); }
    }

    // ------------------------------------------------------------------ showcase scene

    static VolumeProfile SnowPostProfile() { return AssetImport.EnsurePostProfile(); }

    static GameObject PlacePrefab(string name, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + name + ".prefab");
        if (pf == null) { Debug.LogError("SNOWIMPORT: no prefab " + name); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        return go;
    }

    struct Shot
    {
        public string file; public Vector3 target; public float yaw, pitch, dist, fov, ortho; public int w, h; public bool big;
        public Shot(string file, Vector3 target, float yaw, float pitch, float dist, float fov, int w, int h, float ortho = 0f, bool big = false)
        { this.file = file; this.target = target; this.yaw = yaw; this.pitch = pitch; this.dist = dist; this.fov = fov; this.w = w; this.h = h; this.ortho = ortho; this.big = big; }
    }

    static readonly List<Shot> Shots = new List<Shot>();

    static void BuildShowcase(VolumeProfile profile, Material sky)
    {
        Shots.Clear();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var lay = LoadLayout();
        var root = new GameObject("SnowMap").transform;

        foreach (var c in lay.terrain_chunks)
            PlacePrefab(c.fbx, ToUnityPos(c.pivot_world), Quaternion.identity, Vector3.one, root);
        int placed = 0;
        foreach (var i in lay.instances)
        {
            if (PlacePrefab(i.fbx, ToUnityPos(i.pos), ToUnityRot(i.rot_deg), ToUnityScale(i.scale), root) != null) placed++;
        }
        // files that hold the whole map in world coordinates (water, paths, snow drifts, lane, plaza, chains, mist, clouds)
        int world = 0;
        var used = new HashSet<string>();
        foreach (var c in lay.terrain_chunks) used.Add(c.fbx);
        foreach (var i in lay.instances) used.Add(i.fbx);
        foreach (string n in FbxNames())
        {
            if (used.Contains(n)) continue;
            if (n.StartsWith("snow_terrain_drifts") || n.StartsWith("snow_water") || n.StartsWith("snow_path") || n.StartsWith("snow_village")
                || n.StartsWith("snow_deco_chains") || n.StartsWith("snow_scene"))
            { PlacePrefab(n, Vector3.zero, Quaternion.identity, Vector3.one, root); world++; }
        }
        Log("showcase placed instances=" + placed + " worldFiles=" + world);

        var sun = new GameObject("Sun");
        sun.transform.rotation = Quaternion.Euler(38f, 62f, 0f);
        var l = sun.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = AssetImport.Hex("#FFEBCB");
        l.intensity = 1.45f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.85f;
        l.shadowBias = 0.05f;
        l.shadowNormalBias = 0.4f;

        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = AssetImport.Hex("#B6C4DE") * 0.8f;
        RenderSettings.ambientEquatorColor = AssetImport.Hex("#D5BFC3") * 0.7f;
        RenderSettings.ambientGroundColor = AssetImport.Hex("#8DB04A") * 0.5f;
        RenderSettings.fog = false;
        DynamicGI.UpdateEnvironment();

        var vgo = new GameObject("PostFX");
        var vol = vgo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;

        var cgo = new GameObject("ShowcaseCamera");
        cgo.tag = "MainCamera";
        var cam = cgo.AddComponent<Camera>();
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 1500f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.allowHDR = true;
        cam.allowMSAA = true;
        var acd = cgo.AddComponent<UniversalAdditionalCameraData>();
        acd.renderPostProcessing = true;
        acd.renderShadows = true;

        // Unity coordinates here are Blender world (E, Z, N): the game later moves everything by (-190, 0, -190)
        Shots.Add(new Shot("snow_overview", new Vector3(190f, 20f, 175f), 0f, 38f, 560f, 40f, 2560, 1440, 0f, true));
        Shots.Add(new Shot("snow_top", new Vector3(190f, 0f, 190f), 0f, 90f, 600f, 40f, 2048, 2048, 200f, true));
        Shots.Add(new Shot("snow_village", new Vector3(32f, 3f, 34f), 10f, 40f, 70f, 30f, 1600, 900));
        Shots.Add(new Shot("snow_bridge_s1", new Vector3(95f, 1f, 88f), 25f, 38f, 45f, 30f, 1600, 900));
        Shots.Add(new Shot("snow_pine_forest", new Vector3(170f, 18f, 170f), 15f, 40f, 90f, 30f, 1600, 900));
        Shots.Add(new Shot("snow_cliff_ascent", new Vector3(215f, 26f, 235f), 20f, 38f, 110f, 30f, 1600, 900));
        Shots.Add(new Shot("snow_icelake", new Vector3(320f, 48f, 270f), 15f, 38f, 120f, 30f, 1600, 900));
        Shots.Add(new Shot("snow_castle", new Vector3(338f, 82f, 338f), 20f, 32f, 140f, 30f, 1600, 900));

        AssetImport.EnsureDir("Assets/Scenes");
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        Log("scene saved=" + saved + " " + ScenePath);
    }

    static RenderPipelineAsset capPrev;
    static UniversalRenderPipelineAsset capUrp;
    static int capFrame, capIndex;
    static Camera capCam;

    static void StartCapture()
    {
        Directory.CreateDirectory(OutDir);
        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Showcase/UniversalRP_Showcase.asset");
        capUrp = urp;
        capPrev = QualitySettings.renderPipeline;
        capCam = GameObject.Find("ShowcaseCamera").GetComponent<Camera>();
        QualitySettings.renderPipeline = urp;
        capFrame = 0; capIndex = 0;
        EditorApplication.update += CaptureTick;
    }

    static void CaptureTick()
    {
        try
        {
            capFrame++;
            if (capFrame < 5) return;
            if (capIndex >= Shots.Count) { Finish(0); return; }
            var s = Shots[capIndex];
            capUrp.shadowDistance = s.big ? 700f : 160f;
            if (s.ortho > 0f) { capCam.orthographic = true; capCam.orthographicSize = s.ortho; }
            else { capCam.orthographic = false; capCam.fieldOfView = s.fov; }
            var rot = Quaternion.Euler(s.pitch, s.yaw, 0f);
            capCam.transform.rotation = rot;
            capCam.transform.position = s.target + rot * Vector3.back * s.dist;
            capCam.aspect = (float)s.w / s.h;
            var rt = new RenderTexture(s.w, s.h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.antiAliasing = 4;
            capCam.targetTexture = rt;
            capCam.Render();
            capCam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(s.w, s.h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, s.w, s.h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            capCam.targetTexture = null;
            File.WriteAllBytes(OutDir + "/" + s.file + ".png", tex.EncodeToPNG());
            Log("shot " + s.file + " centre=" + tex.GetPixel(s.w / 2, s.h / 2));
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            capIndex++;
        }
        catch (Exception e)
        {
            Debug.LogError("SNOWIMPORT: capture FAILED " + e);
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        EditorApplication.update -= CaptureTick;
        QualitySettings.renderPipeline = capPrev;
        Log("DONE rc=" + code);
        EditorApplication.Exit(code);
    }
}
