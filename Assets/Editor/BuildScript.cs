using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Batchmode entry points. See build.bat in the project root.
public static class BuildScript
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string OutputPath = "Builds/MedievalKnight.exe";
    const string MatFolder = "Assets/MedievalKnight/Resources/Mats";
    const string RendererPath = "Assets/Settings/Renderer3D.asset";
    const string PipelinePath = "Assets/Settings/UniversalRP.asset";
    const string UrpPackage = "Packages/com.unity.render-pipelines.universal";

    // Unity -batchmode -quit -executeMethod BuildScript.BuildWindows
    public static void BuildWindows()
    {
        Setup3D();
        if (!File.Exists(ScenePath)) CreateMainScene();
        if (!File.Exists(ScenePath))
        {
            Debug.LogError("BuildScript: missing scene " + ScenePath);
            EditorApplication.Exit(1);
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log("BuildScript: result=" + summary.result + " errors=" + summary.totalErrors
            + " size=" + summary.totalSize + " output=" + summary.outputPath);

        if (summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // The game is 3D and builds everything from code, so two things must exist as assets:
    //  - a URP "Universal Renderer" (forward, lit, shadows) assigned to the pipeline asset
    //  - three base materials in Resources/Mats (Lit, Unlit, transparent Fx), so that the URP shaders
    //    and exactly the variants the game uses end up in the player. Prims.cs instantiates them.
    [MenuItem("MedievalKnight/Setup 3D Renderer And Materials")]
    public static void Setup3D()
    {
        SetupRenderer();
        SetupMaterials();
        AssetDatabase.SaveAssets();
    }

    static void SetupRenderer()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, RendererPath);
            try { ResourceReloader.ReloadAllNullIn(data, UrpPackage); }
            catch (System.Exception e) { Debug.LogWarning("BuildScript: ResourceReloader: " + e.Message); }
            EditorUtility.SetDirty(data);
            Debug.Log("BuildScript: created " + RendererPath);
        }

        // The game uses no post-processing; without the data asset URP creates no post-process passes
        // (otherwise the player log fills with "shader stripped from the build" messages).
        if (data.postProcessData != null)
        {
            data.postProcessData = null;
            EditorUtility.SetDirty(data);
        }

        var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (urp == null)
        {
            Debug.LogError("BuildScript: missing " + PipelinePath);
            return;
        }
        var so = new SerializedObject(urp);
        SerializedProperty list = so.FindProperty("m_RendererDataList");
        if (list.arraySize < 1) list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = data;
        Set(so, "m_DefaultRendererIndex", 0);
        Set(so, "m_RendererType", 1);                    // UniversalRenderer
        Set(so, "m_MainLightRenderingMode", 1);
        Set(so, "m_MainLightShadowsSupported", 1);
        Set(so, "m_MainLightShadowmapResolution", 4096);
        Set(so, "m_ShadowDistance", 55);
        Set(so, "m_ShadowCascadeCount", 2);
        Set(so, "m_SoftShadowsSupported", 1);
        Set(so, "m_MSAA", 4);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(urp);
        Debug.Log("BuildScript: pipeline uses renderer " + AssetDatabase.GetAssetPath(data));
    }

    static void Set(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) { Debug.LogWarning("BuildScript: pipeline property not found: " + name); return; }
        switch (p.propertyType)
        {
            case SerializedPropertyType.Boolean: p.boolValue = value != 0; break;
            case SerializedPropertyType.Float: p.floatValue = value; break;
            default: p.intValue = value; break;
        }
    }

    static void SetupMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MatFolder))
        {
            Directory.CreateDirectory(MatFolder);
            AssetDatabase.Refresh();
        }

        Ensure("Lit", "Universal Render Pipeline/Lit", m =>
        {
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Metallic", 0f);
        });
        Ensure("Unlit", "Universal Render Pipeline/Unlit", m =>
        {
            m.SetColor("_BaseColor", Color.white);
        });
        Ensure("Fx", "Universal Render Pipeline/Unlit", m =>
        {
            // transparent, alpha blended, double sided, no depth write, no shadows
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.SetShaderPassEnabled("DepthOnly", false);
        });
    }

    static void Ensure(string name, string shaderName, System.Action<Material> setup)
    {
        Shader sh = Shader.Find(shaderName);
        if (sh == null)
        {
            Debug.LogError("BuildScript: shader not found: " + shaderName);
            return;
        }
        string path = MatFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(sh) { name = name };
            setup(m);
            AssetDatabase.CreateAsset(m, path);
            Debug.Log("BuildScript: created " + path);
        }
        else
        {
            if (m.shader != sh) m.shader = sh;
            setup(m);
            EditorUtility.SetDirty(m);
        }
    }

    // Recreates Assets/Scenes/Main.unity (one perspective Main Camera) and makes it the only scene in Build Settings.
    // Everything else (light, world, characters, UI) is built at runtime by MedievalKnight.Game.
    // Unity -batchmode -quit -executeMethod BuildScript.CreateMainScene
    [MenuItem("MedievalKnight/Recreate Main Scene")]
    public static void CreateMainScene()
    {
        Setup3D();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 16f, -16f);
        go.transform.rotation = Quaternion.Euler(56f, 0f, 0f);
        var cam = go.AddComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.5f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.27f, 0.2f);
        go.AddComponent<AudioListener>();

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();

        Debug.Log("BuildScript: CreateMainScene saved=" + saved);
        if (!saved && Application.isBatchMode) EditorApplication.Exit(1);
    }
}
