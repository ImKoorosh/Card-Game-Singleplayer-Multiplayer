#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// One-click build helpers: fixes the settings that cause pink/purple screens and bad orientation in builds,
/// then builds a Windows .exe or an Android .apk into the project's Builds folder.
/// </summary>
public static class CardBattleBuild
{
    const string ScenePath = "Assets/CardBattle/Scenes/Battle.unity";
    const string MenuPath = "Assets/CardBattle/Scenes/Menu.unity";

    [MenuItem("Tools/Card Battle/Fix Build Settings (pink screen, orientation, Android)")]
    public static void FixBuildSettings()
    {
        AddAlwaysIncludedShaders();

        PlayerSettings.productName = "Card Battle";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.defaultScreenWidth = 540;
        PlayerSettings.defaultScreenHeight = 960;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true; // online play: an unfocused window must keep sending network heartbeats

        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

        EnsureSceneInBuildList();
        AssetDatabase.SaveAssets();
        Debug.Log("Card Battle: build settings fixed (shaders included, portrait, 540x960 window, Android IL2CPP/ARM64/API 24).");
    }

    [MenuItem("Tools/Card Battle/Build Windows (.exe)")]
    public static void BuildWindows() { Build(BuildTarget.StandaloneWindows64, "Builds/Windows/CardBattle.exe"); }

    [MenuItem("Tools/Card Battle/Build Android (.apk)")]
    public static void BuildAndroid()
    {
        EditorUserBuildSettings.buildAppBundle = false; // .apk you can install directly; turn on for Google Play (.aab)
        Build(BuildTarget.Android, "Builds/Android/CardBattle.apk");
    }

    static void Build(BuildTarget target, string path)
    {
        FixBuildSettings();
        string[] scenes = GetScenes();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("Card Battle", "No scene found. Run Tools > Card Battle > Build Prototype Scene first.", "OK");
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = scenes;
        options.locationPathName = path;
        options.target = target;
        options.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("Card Battle: build finished -> " + path);
            EditorUtility.RevealInFinder(path);
        }
        else
        {
            Debug.LogError("Card Battle: build " + report.summary.result + ". Read the red errors above this line in the Console.");
        }
    }

    // Shaders that are only used by code-created materials can be stripped from builds, which shows up as magenta/pink.
    static void AddAlwaysIncludedShaders()
    {
        UnityEngine.Object gs = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
        if (gs == null) return;
        SerializedObject so = new SerializedObject(gs);
        SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
        if (list == null) return;
        foreach (string name in new[] { "Standard", "Sprites/Default", "UI/Default", "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit" })
        {
            Shader shader = Shader.Find(name);
            if (shader == null) continue;
            bool present = false;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
            if (present) continue;
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureSceneInBuildList()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (File.Exists(MenuPath) && !scenes.Exists(s => s.path == MenuPath)) scenes.Insert(0, new EditorBuildSettingsScene(MenuPath, true));
        if (File.Exists(ScenePath) && !scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static string[] GetScenes()
    {
        List<string> result = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            if (s.enabled) result.Add(s.path);
        return result.ToArray();
    }
}
#endif
