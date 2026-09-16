using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Unity 菜单构建入口。菜单：Build/Build SpellingGame APK (Android)。
/// </summary>
public static class CharacterModeBuild
{
    [MenuItem("Build/Build SpellingGame APK (Android)")]
    public static void BuildAndroidApk()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string outputDirectory = Path.Combine(root, "Build");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "SpellingGame.apk");

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        string bundledNdk = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/clang.exe");
        string sdkNdk = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/SDK/ndk/23.1.7779620");
        if (Directory.Exists(sdkNdk))
        {
            // Unity stores external tool paths in EditorPrefs; this also works
            // when the bundled NDK folder is present but incomplete.
            EditorPrefs.SetString("NdkRoot", sdkNdk);
            AndroidExternalToolsSettings.ndkRootPath = sdkNdk;
            bundledNdk = Path.Combine(sdkNdk, "toolchains/llvm/prebuilt/windows-x86_64/bin/clang.exe");
        }
        bool hasNdk = File.Exists(bundledNdk);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, hasNdk ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
        if (!hasNdk)
            Debug.LogWarning("Android NDK is unavailable; using Mono for this test APK. Install Android NDK to build with IL2CPP.");
        // Keep ARM64 for Find X8 while also enabling ARMv7 for Unity's Mono
        // fallback (some Unity versions reject a Mono-only ARM64 selection).
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        // Use Unity's debug keystore for a password-free test APK. Replace this
        // with a project release keystore before publishing to an app store.
        PlayerSettings.Android.useCustomKeystore = false;

        string[] scenes =
        {
            "Assets/EnglishApp/Scenes/MainScene.unity",
            "Assets/EnglishApp/Scenes/TD_LevelSelect.unity",
            "Assets/EnglishApp/Scenes/TD_Scene.unity"
        };

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Android build failed: " + report.summary.result);

        Debug.Log("SpellingGame APK created: " + outputPath);
    }
}
