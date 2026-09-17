using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// 命令行打包入口（batchmode -executeMethod 使用）。
///
/// 用法：
///   Unity.exe -batchmode -quit -nographics -projectPath <项目路径>
///     -executeMethod CommandLineBuild.BuildWindowsExe -logFile <日志路径>
///     -executeMethod CommandLineBuild.BuildAndroidApk -logFile <日志路径>
///
/// 输出：
///   Windows exe -> D:\AI\NaruSasuEnglish\NaruSasuEnglish.exe
///   Android apk -> D:\AI\NaruSasuEnglish.apk（测试签名，ARMv7+ARM64）
/// </summary>
public static class CommandLineBuild
{
    private static readonly string[] Scenes =
    {
        "Assets/EnglishApp/Scenes/MainScene.unity",
        "Assets/EnglishApp/Scenes/TD_LevelSelect.unity",
        "Assets/EnglishApp/Scenes/TD_Scene.unity"
    };

    /// <summary>Windows 64 位标准构建。</summary>
    public static void BuildWindowsExe()
    {
        string outputDirectory = @"D:\AI\NaruSasuEnglish";
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "NaruSasuEnglish.exe");

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        LogSummary(report);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Windows build failed: " + report.summary.result
                + ", errors=" + report.summary.totalErrors);

        Debug.Log("Windows build OK: " + outputPath);
    }

    /// <summary>Android 测试 APK（debug 签名，免密码）。</summary>
    public static void BuildAndroidApk()
    {
        string outputPath = @"D:\AI\NaruSasuEnglish.apk";
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        // 指向 Unity 自带 SDK 里的 NDK，避免 EditorPrefs 里残留的外部路径导致 IL2CPP 失败
        string sdkNdk = Path.Combine(EditorApplication.applicationContentsPath,
            "PlaybackEngines/AndroidPlayer/SDK/ndk/23.1.7779620");
        if (Directory.Exists(sdkNdk))
        {
            EditorPrefs.SetString("NdkRoot", sdkNdk);
            AndroidExternalToolsSettings.ndkRootPath = sdkNdk;
        }

        string sdkClang = Path.Combine(sdkNdk, "toolchains/llvm/prebuilt/windows-x86_64/bin/clang.exe");
        string bundledClang = Path.Combine(EditorApplication.applicationContentsPath,
            "PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/clang.exe");
        bool hasNdk = File.Exists(sdkClang) || File.Exists(bundledClang);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,
            hasNdk ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
        if (!hasNdk)
            Debug.LogWarning("Android NDK unavailable; falling back to Mono backend.");

        // ARM64 为主（新机型），保留 ARMv7 兼容旧机
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        // 测试包：使用 Unity debug keystore，免密码。正式发布前需替换为项目签名。
        PlayerSettings.Android.useCustomKeystore = false;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        LogSummary(report);
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Android build failed: " + report.summary.result
                + ", errors=" + report.summary.totalErrors);

        Debug.Log("Android APK OK: " + outputPath);
    }

    private static void LogSummary(BuildReport report)
    {
        Debug.Log(string.Format("[CommandLineBuild] result={0} size={1:F1}MB time={2:F0}s errors={3} warnings={4}",
            report.summary.result,
            report.summary.totalSize / (1024f * 1024f),
            report.summary.totalTime.TotalSeconds,
            report.summary.totalErrors,
            report.summary.totalWarnings));
    }
}
