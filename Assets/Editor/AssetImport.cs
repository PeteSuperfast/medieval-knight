using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Editor-only. Imports the approved Art_kit models (Assets/Models/*.fbx) as prefabs and builds the Showcase scene.
// Nothing here is referenced by Assets/Scenes/Main.unity or Resources, so the game player build and gameplay do not change.
//   Unity -batchmode -projectPath <proj> -executeMethod AssetImport.RunAll     (needs a GPU: no -nographics)
// Steps: 1 materials + prefabs, 2 showcase lighting/post-processing assets, 3 Showcase scene, 4 render the shots.
public static class AssetImport
{
    const string MatDir = "Assets/Materials/Kit";
    const string PrefabDir = "Assets/Prefabs";
    const string ShowDir = "Assets/Showcase";
    const string ScenePath = "Assets/Scenes/Showcase.unity";
    const string OutDir = "Docs/asset-import/showcase";
    const string UrpPackage = "Packages/com.unity.render-pipelines.universal";
    const float FrontYaw = 180f;

    // palette from Docs/style.md / Art_kit/approved.md (material name in the FBX is "MK_<key>")
    static readonly Dictionary<string, string> Palette = new Dictionary<string, string>
    {
        {"rock_top","#ADABB7"}, {"rock_side","#958A80"}, {"opening_dark","#3A3A3A"}, {"plaster_cream","#D8CCB0"},
        {"wood_dark","#715332"}, {"wood_light","#A1773D"}, {"roof_purple","#5E5878"}, {"window_glow","#F5C25A"},
        {"barrel_iron","#3A3A3A"}, {"barrel_wood","#A1773D"}, {"barrel_groove","#715332"},
        {"fence_wood","#A1773D"}, {"fence_side","#715332"}, {"fence_peg","#3A3A3A"},
        {"mushroom_red","#C8483A"}, {"grass_mid","#6C8535"}, {"grass_light","#8DB04A"},
        {"leaf_light","#A0B85B"}, {"leaf_under","#6C8535"},
        {"leaf_dark","#465820"}, {"leaf_mid","#6C8535"}, {"trunk","#715332"}, {"pine_dark","#465820"}, {"pine_mid","#6C8535"},
    };

    class Def
    {
        public string cat, fbx, collider;
        public Def(string cat, string fbx, string collider) { this.cat = cat; this.fbx = fbx; this.collider = collider; }
        public string Name { get { return Path.GetFileNameWithoutExtension(fbx); } }
        public string PrefabPath { get { return PrefabDir + "/" + cat + "/" + Name + ".prefab"; } }
    }

    static readonly Def[] Defs =
    {
        new Def("Trees", "Assets/Models/Tree_Pine.fbx", "trunk"),
        new Def("Trees", "Assets/Models/Tree_Round_Slender.fbx", "trunk"),
        new Def("Trees", "Assets/Models/Tree_Round_Wide.fbx", "trunk"),
        new Def("Rocks", "Assets/Models/Rocks/Rock_Small_A.fbx", "rock"),
        new Def("Rocks", "Assets/Models/Rocks/Rock_Small_B.fbx", "rock"),
        new Def("Rocks", "Assets/Models/Rocks/Rock_Small_C.fbx", "rock"),
        new Def("Rocks", "Assets/Models/Rocks/Rock_Medium_A.fbx", "rock"),
        new Def("Rocks", "Assets/Models/Rocks/Rock_Medium_B.fbx", "rock"),
        new Def("Rocks", "Assets/Models/Rocks/Rock_Medium_C.fbx", "rock"),
        new Def("Rocks", "Assets/Models/Rocks/Rock_Large_A.fbx", "rock"),
        new Def("Rocks", "Assets/Models/Rocks/Rock_Large_B.fbx", "rock"),
        new Def("Rocks", "Assets/Models/Rocks/Rock_Large_C.fbx", "rock"),
        new Def("Props", "Assets/Models/Props/Barrel_Upright.fbx", "barrel"),
        new Def("Props", "Assets/Models/Props/Barrel_Lying.fbx", "box"),
        new Def("Props", "Assets/Models/Props/Fence_Straight_2m.fbx", "box"),
        new Def("Props", "Assets/Models/Props/Fence_CornerPost.fbx", "box"),
        new Def("Props", "Assets/Models/Props/Tree_Stump.fbx", "box"),
        new Def("Props", "Assets/Models/Props/Mushroom_Red_Cluster.fbx", "none"),
        new Def("Props", "Assets/Models/Props/Grass_Tuft.fbx", "none"),
        new Def("Props", "Assets/Models/Props/Stepping_Stone.fbx", "none"),
        new Def("Buildings", "Assets/Models/Buildings/Cottage.fbx", "cottage"),
    };

    static void Log(string s) { Debug.Log("ASSETIMPORT: " + s); }

    // ------------------------------------------------------------------ entry points

    public static void RunAll()
    {
        try
        {
            BuildPrefabs();
            var urp = EnsureShowcasePipeline();
            var profile = EnsurePostProfile();
            var sky = EnsureSkyMaterial();
            BuildShowcaseScene(profile, sky);
            StartCapture(urp);      // the pipeline switch only takes effect on a later editor frame
            return;
        }
        catch (Exception e)
        {
            Debug.LogError("ASSETIMPORT: FAILED " + e);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("MedievalKnight/Asset Import/Build Prefabs")]
    public static void BuildPrefabsMenu() { BuildPrefabs(); }

    // ------------------------------------------------------------------ materials + prefabs

    static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static string Key(string matName)
    {
        string n = matName;
        int dot = n.LastIndexOf('.');
        if (dot > 0 && dot < n.Length - 1 && char.IsDigit(n[n.Length - 1])) n = n.Substring(0, dot);
        if (n.StartsWith("MK_")) n = n.Substring(3);
        return n;
    }

    static Material GetKitMaterial(string key, Material imported)
    {
        EnsureDir(MatDir);
        string path = MatDir + "/MK_" + key + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;

        Color c;
        string hex;
        if (Palette.TryGetValue(key, out hex)) c = Hex(hex);
        else
        {
            c = imported != null && imported.HasProperty("_BaseColor") ? imported.GetColor("_BaseColor")
              : imported != null && imported.HasProperty("_Color") ? imported.GetColor("_Color") : Color.magenta;
            Debug.LogWarning("ASSETIMPORT: material '" + key + "' not in palette, using imported colour " + ColorUtility.ToHtmlStringRGB(c));
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(shader) { name = "MK_" + key };
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", key.Contains("iron") || key == "opening_dark" ? 0.2f : 0.1f);
        m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        if (key == "window_glow")
        {
            // emission does not come in the FBX: #F5C25A, strength 1.6 (linear multiplier on the colour)
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c.linear * 1.6f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void SetStatic(GameObject go)
    {
        var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic
                  | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
    }

    static Bounds BoundsOf(IEnumerable<Renderer> rs)
    {
        bool first = true;
        Bounds b = new Bounds();
        foreach (var r in rs)
        {
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static void BuildPrefabs()
    {
        AssetDatabase.Refresh();
        EnsureDir(PrefabDir);
        var seen = new HashSet<string>();
        foreach (var d in Defs)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(d.fbx);
            if (model == null) { Debug.LogError("ASSETIMPORT: missing model " + d.fbx); continue; }
            EnsureDir(PrefabDir + "/" + d.cat);

            var root = new GameObject(d.Name);
            // Directional assets (cottage, fences): the FBX imports with its Blender front (-Y, door / rails side) facing Unity +Z.
            // A pivot turns it 180 deg so the front faces -Z (toward the game camera). The FBX root keeps its own rotation
            // (Cottage: X -90); set FrontYaw to 0 to get the raw import orientation back.
            Transform holder = root.transform;
            if (d.Name == "Cottage" || d.Name.StartsWith("Fence_"))
            {
                var pivot = new GameObject("Pivot");
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localRotation = Quaternion.Euler(0f, FrontYaw, 0f);
                holder = pivot.transform;
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, holder);
            inst.transform.localPosition = Vector3.zero;

            int tris = 0;
            var names = new List<string>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var src = r.sharedMaterials;
                var dst = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    string key = Key(src[i] != null ? src[i].name : "none");
                    if (seen.Add(key)) names.Add(key);
                    dst[i] = GetKitMaterial(key, src[i]);
                }
                r.sharedMaterials = dst;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
            }

            AddColliders(root, d);
            SetStatic(root);
            PrefabUtility.SaveAsPrefabAsset(root, d.PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            Log("prefab " + d.PrefabPath + " tris=" + tris + " collider=" + d.collider + (names.Count > 0 ? " newMats=" + string.Join(",", names) : ""));
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void AddColliders(GameObject root, Def d)
    {
        var rends = root.GetComponentsInChildren<MeshRenderer>(true);
        Bounds b = BoundsOf(rends);
        switch (d.collider)
        {
            case "none": break;
            case "box":
            {
                var c = root.AddComponent<BoxCollider>();
                c.center = b.center; c.size = b.size;
                break;
            }
            case "barrel":
            {
                var c = root.AddComponent<CapsuleCollider>();
                c.direction = 1;
                c.center = b.center;
                c.height = b.size.y;
                c.radius = Mathf.Min(b.size.x, b.size.z) * 0.5f * 0.95f;
                break;
            }
            case "rock":
            {
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
                break;
            }
            case "trunk":
            {
                // trunk radius from the vertices near the ground; the capsule covers the trunk only (not the crown)
                float cx = 0, cz = 0; int n = 0;
                var pts = new List<Vector3>();
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    foreach (var v in mf.sharedMesh.vertices)
                    {
                        var w = mf.transform.TransformPoint(v);
                        if (w.y < 0.45f) { pts.Add(w); cx += w.x; cz += w.z; n++; }
                    }
                float radius = 0.25f;
                if (n > 0)
                {
                    cx /= n; cz /= n;
                    float m = 0f;
                    foreach (var p in pts) m = Mathf.Max(m, new Vector2(p.x - cx, p.z - cz).magnitude);
                    radius = Mathf.Clamp(m, 0.15f, 0.5f);
                }
                float h = Mathf.Min(2.2f, b.size.y);
                var c = root.AddComponent<CapsuleCollider>();
                c.direction = 1;
                c.radius = radius;
                c.height = h;
                c.center = new Vector3(cx, h * 0.5f, cz);
                break;
            }
            case "cottage":
            {
                var body = new List<Renderer>();
                var chim = new List<Renderer>();
                foreach (var r in rends)
                {
                    if (r.name.Contains("Plinth") || r.name.Contains("Walls")) body.Add(r);
                    else if (r.name.Contains("Chimney")) chim.Add(r);
                }
                if (body.Count > 0)
                {
                    var bb = BoundsOf(body);
                    var c = root.AddComponent<BoxCollider>();
                    c.center = bb.center; c.size = bb.size;
                }
                if (chim.Count > 0)
                {
                    var cb = BoundsOf(chim);
                    var c = root.AddComponent<BoxCollider>();
                    c.center = cb.center; c.size = cb.size;
                }
                break;
            }
        }
    }

    // ------------------------------------------------------------------ showcase lighting assets

    static void SetProp(SerializedObject so, string name, float v)
    {
        var p = so.FindProperty(name);
        if (p == null) { Debug.LogWarning("ASSETIMPORT: property not found " + name); return; }
        switch (p.propertyType)
        {
            case SerializedPropertyType.Boolean: p.boolValue = v != 0; break;
            case SerializedPropertyType.Float: p.floatValue = v; break;
            default: p.intValue = (int)v; break;
        }
    }

    // A separate pipeline asset + renderer (post-processing + SSAO) used only by the Showcase capture.
    // The game's UniversalRP.asset / Renderer3D.asset are not touched.
    static UniversalRenderPipelineAsset EnsureShowcasePipeline()
    {
        EnsureDir(ShowDir);
        string rp = ShowDir + "/Renderer_Showcase.asset", pp = ShowDir + "/UniversalRP_Showcase.asset";
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rp);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, rp);
            try { ResourceReloader.ReloadAllNullIn(data, UrpPackage); }
            catch (Exception e) { Debug.LogWarning("ASSETIMPORT: ResourceReloader: " + e.Message); }

            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "ScreenSpaceAmbientOcclusion";
            AssetDatabase.AddObjectToAsset(ssao, data);
            var sso = new SerializedObject(ssao);
            var s = sso.FindProperty("m_Settings");
            if (s != null)
            {
                var pI = s.FindPropertyRelative("Intensity"); if (pI != null) pI.floatValue = 0.9f;
                var pR = s.FindPropertyRelative("Radius"); if (pR != null) pR.floatValue = 0.35f;
                sso.ApplyModifiedPropertiesWithoutUndo();
            }
            else Debug.LogWarning("ASSETIMPORT: SSAO m_Settings not found, default values");
            AssetDatabase.SaveAssets();

            var so = new SerializedObject(data);
            var feats = so.FindProperty("m_RendererFeatures");
            feats.arraySize++;
            feats.GetArrayElementAtIndex(feats.arraySize - 1).objectReferenceValue = ssao;
            var map = so.FindProperty("m_RendererFeatureMap");
            string guid; long id;
            if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out guid, out id))
            {
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
        Log("showcase renderer postProcessData=" + (data.postProcessData != null) + " features=" + data.rendererFeatures.Count);

        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pp);
        if (urp == null)
        {
            urp = UniversalRenderPipelineAsset.Create(data);
            AssetDatabase.CreateAsset(urp, pp);
            var so = new SerializedObject(urp);
            SetProp(so, "m_MainLightRenderingMode", 1);
            SetProp(so, "m_MainLightShadowsSupported", 1);
            SetProp(so, "m_MainLightShadowmapResolution", 4096);
            SetProp(so, "m_ShadowDistance", 80);
            SetProp(so, "m_ShadowCascadeCount", 2);
            SetProp(so, "m_SoftShadowsSupported", 1);
            SetProp(so, "m_MSAA", 4);
            SetProp(so, "m_SupportsHDR", 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssets();
        }
        return urp;
    }

    static VolumeProfile EnsurePostProfile()
    {
        EnsureDir(ShowDir);
        string path = ShowDir + "/Showcase_PostFX.asset";
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (existing != null) return existing;

        var p = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(p, path);

        var bloom = p.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);       // only the emissive windows bloom
        bloom.intensity.Override(0.35f);
        bloom.scatter.Override(0.6f);
        bloom.tint.Override(new Color(1f, 0.93f, 0.8f));

        var ca = p.Add<ColorAdjustments>(true);   // slightly warm grading
        ca.postExposure.Override(0f);
        ca.saturation.Override(6f);
        ca.contrast.Override(5f);
        ca.colorFilter.Override(new Color(1f, 0.96f, 0.9f));

        foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
        EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();
        return p;
    }

    static Material EnsureSkyMaterial()
    {
        EnsureDir(ShowDir);
        string shaderPath = ShowDir + "/MK_SkyGradient.shader";
        if (!File.Exists(shaderPath))
        {
            File.WriteAllText(shaderPath,
@"Shader ""MK/SkyGradient""
{
    Properties { _Top (""Top"", Color) = (0.71, 0.77, 0.87, 1) _Bottom (""Horizon"", Color) = (0.84, 0.75, 0.76, 1) }
    SubShader
    {
        Tags { ""Queue""=""Background"" ""RenderType""=""Background"" ""PreviewType""=""Skybox"" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""
            fixed4 _Top; fixed4 _Bottom;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }
            fixed4 frag (v2f i) : SV_Target { float t = saturate(normalize(i.dir).y * 1.6); return lerp(_Bottom, _Top, t); }
            ENDCG
        }
    }
}
");
            AssetDatabase.ImportAsset(shaderPath);
        }
        string matPath = ShowDir + "/MK_SkyGradient.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null)
        {
            m = new Material(AssetDatabase.LoadAssetAtPath<Shader>(shaderPath));
            m.SetColor("_Top", Hex("#B6C4DE"));
            m.SetColor("_Bottom", Hex("#D5BFC3"));
            AssetDatabase.CreateAsset(m, matPath);
            AssetDatabase.SaveAssets();
        }
        return m;
    }

    // ------------------------------------------------------------------ showcase scene

    static GameObject Place(string cat, string name, Vector3 pos, float yaw = 0f, Transform parent = null)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/" + cat + "/" + name + ".prefab");
        if (pf == null) { Debug.LogError("ASSETIMPORT: missing prefab " + cat + "/" + name); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        return go;
    }

    struct Shot
    {
        public string file; public Vector3 target; public float yaw, pitch, dist, fov; public int w, h;
        public Shot(string file, Vector3 target, float yaw, float pitch, float dist, float fov, int w = 1280, int h = 960)
        { this.file = file; this.target = target; this.yaw = yaw; this.pitch = pitch; this.dist = dist; this.fov = fov; this.w = w; this.h = h; }
    }

    static readonly List<Shot> Shots = new List<Shot>();

    static void BuildShowcaseScene(VolumeProfile profile, Material sky)
    {
        Shots.Clear();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(60f, 1f, 60f);
        ground.transform.position = new Vector3(100f, 0f, 40f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = GetKitMaterial("grass_light", null);
        UnityEngine.Object.DestroyImmediate(ground.GetComponent<MeshCollider>());

        // warm side light, soft long shadows
        var sun = new GameObject("Sun");
        sun.transform.rotation = Quaternion.Euler(38f, 62f, 0f);
        var l = sun.AddComponent<Light>();
        l.type = LightType.Directional;
        l.color = Hex("#FFEBCB");
        l.intensity = 1.45f;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.85f;
        l.shadowBias = 0.05f;
        l.shadowNormalBias = 0.4f;

        // sky gradient (#B6C4DE -> #D5BFC3) and gradient ambient
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Hex("#B6C4DE") * 0.8f;
        RenderSettings.ambientEquatorColor = Hex("#D5BFC3") * 0.7f;
        RenderSettings.ambientGroundColor = Hex("#8DB04A") * 0.5f;
        RenderSettings.fog = false;
        DynamicGI.UpdateEnvironment();

        // post-processing volume
        var vgo = new GameObject("PostFX");
        var vol = vgo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;

        // camera (shots move it)
        var cgo = new GameObject("ShowcaseCamera");
        cgo.tag = "MainCamera";
        var cam = cgo.AddComponent<Camera>();
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 400f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.allowHDR = true;
        cam.allowMSAA = true;
        var acd = cgo.AddComponent<UniversalAdditionalCameraData>();
        acd.renderPostProcessing = true;
        acd.renderShadows = true;
        cgo.transform.position = new Vector3(0f, 6f, -12f);
        cgo.transform.rotation = Quaternion.Euler(30f, 0f, 0f);

        // ---- groups, 40 m apart on X; one shot per Art_kit preview ----
        var root = new GameObject("Showcase").transform;

        float x = 0f;   // Tree_Pine
        Place("Trees", "Tree_Pine", new Vector3(x, 0, 0), 0, root);
        Shots.Add(new Shot("Tree_Pine", new Vector3(x, 2f, 0), 20f, 32f, 14f, 30f));

        x = 40f;        // Round trees
        Place("Trees", "Tree_Round_Slender", new Vector3(x - 4f, 0, 0), 0, root);
        Place("Trees", "Tree_Round_Wide", new Vector3(x + 5f, 0, 0), 0, root);
        Shots.Add(new Shot("Round_trees", new Vector3(x + 0.5f, 2f, 0), 12f, 36f, 26f, 30f));

        x = 80f;        // Rocks 3x3
        string[] kinds = { "Small", "Medium", "Large" };
        float[] rz = { 8f, 3f, -3.5f };
        float[] rx = { 3f, 4f, 5.5f };
        for (int k = 0; k < 3; k++)
            for (int v = 0; v < 3; v++)
                Place("Rocks", "Rock_" + kinds[k] + "_" + "ABC"[v], new Vector3(x + (v - 1) * rx[k], 0, rz[k]), 0, root);
        Shots.Add(new Shot("Rocks_variants", new Vector3(x, 1f, 2f), 8f, 38f, 26f, 30f));

        x = 120f;       // Barrels
        Place("Props", "Barrel_Upright", new Vector3(x - 0.9f, 0, 0), 0, root);
        Place("Props", "Barrel_Lying", new Vector3(x + 0.9f, 0, 0), 0, root);
        Shots.Add(new Shot("Barrels", new Vector3(x, 0.4f, 0), 15f, 30f, 6.5f, 30f, 1280, 720));

        x = 160f;       // Fence
        Place("Props", "Fence_Straight_2m", new Vector3(x - 1.5f, 0, 0), 0, root);
        Place("Props", "Fence_CornerPost", new Vector3(x + 2.2f, 0, 0), 0, root);
        Shots.Add(new Shot("Fence", new Vector3(x + 0.4f, 0.5f, 0), 15f, 32f, 9f, 30f));

        x = 200f;       // Small props
        Place("Props", "Mushroom_Red_Cluster", new Vector3(x - 1.0f, 0, 0), 0, root);
        Place("Props", "Grass_Tuft", new Vector3(x - 0.3f, 0, 0), 0, root);
        Place("Props", "Stepping_Stone", new Vector3(x + 0.5f, 0, 0), 0, root);
        Place("Props", "Tree_Stump", new Vector3(x + 1.4f, 0, 0), 0, root);
        Shots.Add(new Shot("Small_props", new Vector3(x + 0.2f, 0.15f, 0), 10f, 35f, 4.4f, 30f, 1280, 640));

        x = 240f;       // Cottage + dressing (barrels, stones, mushrooms, grass as in the Blender preview)
        Place("Buildings", "Cottage", new Vector3(x, 0, 0), 0, root);
        Place("Props", "Barrel_Upright", new Vector3(x - 1.9f, 0, -2.7f), 0, root);
        Place("Props", "Barrel_Lying", new Vector3(x + 1.0f, 0, -3.4f), 80f, root);
        Place("Props", "Stepping_Stone", new Vector3(x - 0.7f, 0, -3.6f), 20f, root);
        Place("Props", "Stepping_Stone", new Vector3(x - 1.4f, 0, -4.4f), 70f, root);
        Place("Props", "Stepping_Stone", new Vector3(x - 2.4f, 0, -5.0f), 5f, root);
        Place("Props", "Mushroom_Red_Cluster", new Vector3(x + 1.9f, 0, -3.3f), 0, root);
        Place("Props", "Grass_Tuft", new Vector3(x - 2.5f, 0, -2.0f), 0, root);
        Place("Props", "Grass_Tuft", new Vector3(x - 0.6f, 0, -3.0f), 0, root);
        Place("Props", "Grass_Tuft", new Vector3(x + 2.9f, 0, -2.4f), 0, root);
        Shots.Add(new Shot("Cottage_front", new Vector3(x, 2.7f, -0.3f), 28f, 36f, 26f, 30f, 1024, 768));
        Shots.Add(new Shot("Cottage_back", new Vector3(x, 2.7f, 0f), 205f, 36f, 26f, 30f, 1024, 768));

        // ---- overview: a small village corner with everything together ----
        float vx = 0f, vz = 60f;
        Place("Buildings", "Cottage", new Vector3(vx, 0, vz), 0, root);
        Place("Props", "Barrel_Upright", new Vector3(vx - 1.9f, 0, vz - 2.7f), 0, root);
        Place("Props", "Barrel_Lying", new Vector3(vx + 1.0f, 0, vz - 3.4f), 80f, root);
        Place("Props", "Stepping_Stone", new Vector3(vx - 0.7f, 0, vz - 3.6f), 20f, root);
        Place("Props", "Stepping_Stone", new Vector3(vx - 1.4f, 0, vz - 4.4f), 70f, root);
        Place("Props", "Stepping_Stone", new Vector3(vx - 2.4f, 0, vz - 5.0f), 5f, root);
        for (int i = 0; i < 4; i++) Place("Props", "Fence_Straight_2m", new Vector3(vx + 4f + i * 2f, 0, vz - 6f), 0, root);
        Place("Props", "Fence_CornerPost", new Vector3(vx + 12.5f, 0, vz - 6f), 0, root);
        Place("Trees", "Tree_Round_Wide", new Vector3(vx - 11f, 0, vz + 3f), 0, root);
        Place("Trees", "Tree_Round_Slender", new Vector3(vx - 7f, 0, vz + 9f), 0, root);
        Place("Trees", "Tree_Pine", new Vector3(vx + 10f, 0, vz + 5f), 0, root);
        Place("Trees", "Tree_Pine", new Vector3(vx + 14f, 0, vz + 1f), 0, root);
        Place("Trees", "Tree_Pine", new Vector3(vx + 8f, 0, vz + 11f), 0, root);
        Place("Rocks", "Rock_Large_B", new Vector3(vx + 16f, 0, vz - 4f), 30f, root);
        Place("Rocks", "Rock_Medium_A", new Vector3(vx - 8f, 0, vz - 6f), 0, root);
        Place("Rocks", "Rock_Medium_C", new Vector3(vx - 10f, 0, vz - 8f), 100f, root);
        Place("Rocks", "Rock_Small_A", new Vector3(vx + 5f, 0, vz - 3f), 0, root);
        Place("Rocks", "Rock_Small_C", new Vector3(vx - 5f, 0, vz - 4f), 40f, root);
        Place("Props", "Tree_Stump", new Vector3(vx + 6f, 0, vz + 2f), 0, root);
        Place("Props", "Mushroom_Red_Cluster", new Vector3(vx + 1.9f, 0, vz - 3.3f), 0, root);
        Place("Props", "Mushroom_Red_Cluster", new Vector3(vx - 9f, 0, vz + 1f), 0, root);
        for (int i = 0; i < 8; i++)
            Place("Props", "Grass_Tuft", new Vector3(vx - 6f + i * 2.9f, 0, vz - 2f + (i % 3) * 3f), i * 47f, root);
        Shots.Add(new Shot("Overview", new Vector3(vx + 2f, 2f, vz), 25f, 38f, 40f, 32f, 1280, 720));

        EnsureDir("Assets/Scenes");
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        Log("scene saved=" + saved + " " + ScenePath);
    }

    static UniversalRenderPipelineAsset capUrp;
    static RenderPipelineAsset capPrev;
    static int capFrame, capIndex;
    static Camera capCam;

    static void StartCapture(UniversalRenderPipelineAsset urp)
    {
        Directory.CreateDirectory(OutDir);
        // NewScene() unloads unreferenced assets, so the reference passed in may be dead: load it again
        urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ShowDir + "/UniversalRP_Showcase.asset");
        capUrp = urp;
        capPrev = QualitySettings.renderPipeline;
        capCam = GameObject.Find("ShowcaseCamera").GetComponent<Camera>();
        Log("capture start: prev=" + (capPrev != null ? capPrev.name : "null") + " urp=" + (urp != null ? urp.name : "null")
            + " graphicsDefault=" + (GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.name : "null"));
        QualitySettings.renderPipeline = urp;
        Log("after set: qrp=" + (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline.name : "null")
            + " current=" + (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "null"));
        capFrame = 0; capIndex = 0;
        EditorApplication.update += CaptureTick;
    }

    static void CaptureTick()
    {
        try
        {
            capFrame++;
            if (capFrame < 5) return;                    // let the new pipeline come up
            if (capFrame == 5)
                Log("pipeline current=" + (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "null")
                    + " quality=" + QualitySettings.GetQualityLevel() + " qrp=" + (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline.name : "null")
                    + " cam.pos=" + capCam.transform.position + " device=" + SystemInfo.graphicsDeviceType);
            if (capIndex >= Shots.Count)
            {
                Finish(0);
                return;
            }
            var s = Shots[capIndex];
            var rot = Quaternion.Euler(s.pitch, s.yaw, 0f);
            capCam.transform.rotation = rot;
            capCam.transform.position = s.target + rot * Vector3.back * s.dist;
            capCam.fieldOfView = s.fov;
            capCam.aspect = (float)s.w / s.h;

            var rt = new RenderTexture(s.w, s.h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            capCam.targetTexture = rt;
            capCam.Render();
            capCam.Render();
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(s.w, s.h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, s.w, s.h), 0, 0);
            tex.Apply();
            Log("pixel center=" + tex.GetPixel(s.w / 2, s.h / 2) + " corner=" + tex.GetPixel(2, 2));
            RenderTexture.active = prevActive;
            capCam.targetTexture = null;
            string path = OutDir + "/" + s.file + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            Log("shot " + path);
            capIndex++;
        }
        catch (Exception e)
        {
            Debug.LogError("ASSETIMPORT: capture FAILED " + e);
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        EditorApplication.update -= CaptureTick;
        QualitySettings.renderPipeline = capPrev;     // the game's pipeline is back as it was
        Log("DONE rc=" + code);
        EditorApplication.Exit(code);
    }
}
