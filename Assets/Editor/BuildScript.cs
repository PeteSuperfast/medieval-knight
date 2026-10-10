using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batchmode entry points. See build.bat in the project root.
public static class BuildScript
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string OutputPath = "Builds/MedievalKnight.exe";
    const string SpriteFolder = "Assets/MedievalKnight/Resources/Sprites";

    // Unity -batchmode -quit -executeMethod BuildScript.BuildWindows
    public static void BuildWindows()
    {
        FixTextures();
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

    // The game loads its art with Resources.Load<Texture2D> and slices sprites in code (see Art.cs),
    // so every PNG must be an uncompressed, readable, point-filtered, non-resized texture.
    [MenuItem("MedievalKnight/Fix Texture Import Settings")]
    public static void FixTextures()
    {
        if (!AssetDatabase.IsValidFolder(SpriteFolder)) return;
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;

            bool ok = imp.textureType == TextureImporterType.Default
                && imp.npotScale == TextureImporterNPOTScale.None
                && imp.isReadable
                && !imp.mipmapEnabled
                && imp.filterMode == FilterMode.Point
                && imp.textureCompression == TextureImporterCompression.Uncompressed
                && imp.wrapMode == TextureWrapMode.Clamp
                && imp.alphaIsTransparency;
            if (ok) continue;

            imp.textureType = TextureImporterType.Default;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.isReadable = true;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaIsTransparency = true;
            imp.sRGBTexture = true;
            imp.SaveAndReimport();
            changed++;
        }
        Debug.Log("BuildScript: FixTextures changed=" + changed);
    }

    // Recreates Assets/Scenes/Main.unity (one perspective Main Camera) and makes it the only scene in Build Settings.
    // Everything else is built at runtime by MedievalKnight.Game.
    // Unity -batchmode -quit -executeMethod BuildScript.CreateMainScene
    [MenuItem("MedievalKnight/Recreate Main Scene")]
    public static void CreateMainScene()
    {
        FixTextures();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(0f, 3.6f, -14f);
        var cam = go.AddComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 39.3f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 300f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.36f, 0.5f, 0.78f);
        go.AddComponent<AudioListener>();

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();

        Debug.Log("BuildScript: CreateMainScene saved=" + saved);
        if (!saved && Application.isBatchMode) EditorApplication.Exit(1);
    }
}
