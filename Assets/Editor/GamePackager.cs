using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 一键打包：Windows x64 (.exe) + Android (.apk)
///
/// 触发方式
///   1. 菜单 Build/打包 Windows (x64) / 打包 Android APK / 打包 全部
///   2. 请求文件  &lt;工程&gt;/Temp/package.request  内容为 win | apk | all
///
/// 流程状态全部放在 SessionState，因为 SwitchActiveBuildTarget 可能触发域重载，
/// 静态字段会被清空。
///
/// 日志：&lt;工程&gt;/Build/package-log.txt
/// </summary>
public static class GamePackager
{
    // ---------------------------------------------------------------- 常量
    const string SS_PENDING  = "GamePackager.Pending";
    const string SS_PHASE    = "GamePackager.Phase";
    const string SS_TRIES    = "GamePackager.SwitchTries";
    const string SS_STARTED  = "GamePackager.BuildStarted";

    static readonly string[] Scenes =
    {
        "Assets/EnglishApp/Scenes/MainScene.unity",
        "Assets/EnglishApp/Scenes/TD_LevelSelect.unity",
        "Assets/EnglishApp/Scenes/TD_Scene.unity",
    };

    static string Root    { get { return Directory.GetParent(Application.dataPath).FullName; } }
    static string LogPath { get { return Path.Combine(Root, "Build/package-log.txt"); } }
    static string ReqPath { get { return Path.Combine(Root, "Temp/package.request"); } }

    // ---------------------------------------------------------------- 生命周期
    [InitializeOnLoadMethod]
    static void Init()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    [MenuItem("Build/打包 Windows (x64)")]
    public static void MenuBuildWin() { StartJob("win"); }

    [MenuItem("Build/打包 Android APK")]
    public static void MenuBuildApk() { StartJob("apk"); }

    [MenuItem("Build/打包 Android APK (IL2CPP ARM64)")]
    public static void MenuBuildApk64() { StartJob("apk64"); }

    [MenuItem("Build/打包 全部 (Win + APK)")]
    public static void MenuBuildAll() { StartJob("all"); }

    [MenuItem("Build/打印当前 PlayerSettings")]
    public static void MenuDumpSettings()
    {
        Log("===== PlayerSettings =====");
        Log("activeBuildTarget = " + EditorUserBuildSettings.activeBuildTarget);
        Log("companyName       = " + PlayerSettings.companyName);
        Log("productName       = " + PlayerSettings.productName);
        Log("bundleVersion     = " + PlayerSettings.bundleVersion);
        Log("Android id        = " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
        Log("Standalone id     = " + PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Standalone));
        Log("视角/分辨率        = " + PlayerSettings.defaultScreenWidth + "x" + PlayerSettings.defaultScreenHeight
            + "  fullscreen=" + PlayerSettings.fullScreenMode);
        Log("脚本后端(Android)  = " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android));
        Log("脚本后端(Win)      = " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone));
        Log("Android ABI       = " + PlayerSettings.Android.targetArchitectures);
        Log("持久化目录         = " + Application.persistentDataPath);
        Log("===== end =====");
    }

    // ---------------------------------------------------------------- 主循环
    static void Tick()
    {
        // 1) 请求文件
        if (File.Exists(ReqPath))
        {
            string req = null;
            try { req = File.ReadAllText(ReqPath).Trim(); } catch (Exception e) { Log("读 request 失败: " + e.Message); }
            try { File.Delete(ReqPath); } catch { }
            if (string.IsNullOrEmpty(req)) req = "all";
            StartJob(req.ToLowerInvariant());
        }

        string pending = SessionState.GetString(SS_PENDING, "");
        if (string.IsNullOrEmpty(pending)) return;

        int phase = SessionState.GetInt(SS_PHASE, 0);

        switch (phase)
        {
            // ---------------- 0. 退出 Play 模式
            case 0:
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Log("[0] 检测到 Play 模式，先退出…");
                    EditorApplication.isPlaying = false;
                    return;
                }
                SessionState.SetInt(SS_PHASE, 1);
                return;

            // ---------------- 1. 等编译结束
            case 1:
                if (EditorApplication.isCompiling)
                {
                    // 不打日志避免刷屏
                    return;
                }
                SessionState.SetInt(SS_PHASE, 2);
                SessionState.SetInt(SS_TRIES, 0);
                return;

            // ---------------- 2. 切到目标平台（可能触发域重载，本阶段幂等）
            case 2:
            {
                if (EditorApplication.isCompiling) return;

                BuildTargetGroup wantGroup;
                BuildTarget wantTarget;
                TargetOf(pending, out wantGroup, out wantTarget);

                if (EditorUserBuildSettings.activeBuildTarget == wantTarget)
                {
                    Log("[2] 当前平台已是 " + wantTarget);
                    SessionState.SetInt(SS_PHASE, 3);
                    return;
                }

                int tries = SessionState.GetInt(SS_TRIES, 0);
                if (tries >= 3)
                {
                    Log("[2] !! 切换平台连续失败 3 次，放弃。当前=" + EditorUserBuildSettings.activeBuildTarget);
                    Finish("失败：无法切换到 " + wantTarget);
                    return;
                }
                SessionState.SetInt(SS_TRIES, tries + 1);

                Log("[2] 切换构建平台 -> " + wantTarget + "（第 " + (tries + 1) + " 次）");
                bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(wantGroup, wantTarget);
                Log("[2] SwitchActiveBuildTarget 返回 " + ok
                    + "，现在 active=" + EditorUserBuildSettings.activeBuildTarget);
                return;
            }

            // ---------------- 3. 配置 PlayerSettings + 真正构建
            case 3:
            {
                if (EditorApplication.isCompiling) return;

                // 域重载保护：若本轮已开始过构建，说明是被重载打断的残留状态
                if (SessionState.GetString(SS_STARTED, "") == "1")
                {
                    Log("[3] !! 上一次构建被域重载打断，标记为未完成");
                    SessionState.SetString(SS_STARTED, "2");
                    return;
                }
                if (SessionState.GetString(SS_STARTED, "") == "2")
                {
                    SessionState.SetString(SS_STARTED, "");
                    SessionState.SetInt(SS_PHASE, 4);
                    return;
                }

                SessionState.SetString(SS_STARTED, "1");
                bool ok = false;
                try
                {
                    ok = RunBuild(pending);
                }
                catch (Exception e)
                {
                    Log("[3] !! 构建抛异常: " + e);
                    ok = false;
                }
                SessionState.SetString(SS_STARTED, "2");
                SessionState.SetString("GamePackager.LastResult", ok ? "OK" : "FAIL");
                return;
            }

            // ---------------- 4. 收尾 / 多任务串联
            case 4:
            {
                string res = SessionState.GetString("GamePackager.LastResult", "?");
                Log("[4] " + pending + " 结果 = " + res);

                if (pending == "all")
                {
                    if (res == "OK" && SessionState.GetString("GamePackager.AllStage", "") == "win")
                    {
                        SessionState.SetString("GamePackager.AllStage", "apk");
                        SessionState.SetString(SS_PENDING, "apk");
                        SessionState.SetInt(SS_PHASE, 0);
                        SessionState.SetString(SS_STARTED, "");
                        Log("[4] all 模式：继续构建 APK");
                        return;
                    }
                }

                Finish(res == "OK" ? "成功" : "失败");
                return;
            }
        }
    }

    // ---------------------------------------------------------------- 任务控制
    static void StartJob(string cmd)
    {
        try { File.WriteAllText(LogPath, ""); } catch { }
        Log("############ 打包任务开始 : " + cmd + " ############");
        Log("时间 = " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Log("工程 = " + Root);
        SessionState.SetString(SS_PENDING, cmd);
        SessionState.SetInt(SS_PHASE, 0);
        SessionState.SetInt(SS_TRIES, 0);
        SessionState.SetString(SS_STARTED, "");
        SessionState.SetString("GamePackager.LastResult", "");
        SessionState.SetString("GamePackager.AllStage", cmd == "all" ? "win" : cmd);
    }

    static void Finish(string msg)
    {
        Log("############ 打包任务结束 : " + msg + " ############");
        Log("@PACKAGE-DONE@ " + msg);
        SessionState.SetString(SS_PENDING, "");
        SessionState.SetInt(SS_PHASE, 0);
        SessionState.SetInt(SS_TRIES, 0);
        SessionState.SetString(SS_STARTED, "");
    }

    static bool IsAndroid(string cmd) { return cmd != null && cmd.StartsWith("apk"); }

    static void TargetOf(string cmd, out BuildTargetGroup group, out BuildTarget target)
    {
        if (IsAndroid(cmd)) { group = BuildTargetGroup.Android;    target = BuildTarget.Android; }
        else                { group = BuildTargetGroup.Standalone; target = BuildTarget.StandaloneWindows64; }
    }

    // ---------------------------------------------------------------- 构建
    static bool RunBuild(string cmd)
    {
        if (IsAndroid(cmd)) return BuildAndroid(cmd);
        return BuildWindows();
    }

    // ---------------- Windows x64 ----------------
    static bool BuildWindows()
    {
        Log("----- Windows x64 构建开始 -----");

        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.allowFullscreenSwitch = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.productName = "NaruSasu English";
        PlayerSettings.companyName = "Hazel1103";
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        try { PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Low); }
        catch (Exception e) { Log("  stripping 设置跳过: " + e.Message); }

        string outDir = Path.Combine(Root, "Build/Windows");
        Directory.CreateDirectory(outDir);
        string exe = Path.Combine(outDir, "NaruSasu English.exe");

        Log("  输出 = " + exe);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = exe,
            targetGroup = BuildTargetGroup.Standalone,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });

        BuildSummary s = report.summary;
        Log("  result=" + s.result + "  errors=" + s.totalErrors + "  warnings=" + s.totalWarnings);
        Log("  size=" + (s.totalSize / 1048576.0).ToString("0.0") + " MB   耗时=" + s.totalTime);
        if (s.result != BuildResult.Succeeded)
        {
            foreach (var step in report.steps)
                foreach (var msg in step.messages)
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Log("  [" + step.name + "] " + msg.content);
            Log("----- Windows 构建失败 -----");
            return false;
        }

        File.WriteAllText(Path.Combine(outDir, "使用说明.txt"),
            "NaruSasu English（塔防背单词 / Naruto & Sasuke Spelling TD）\r\n"
            + "======================\r\n\r\n"
            + "1. 双击 NaruSasu English.exe 直接开始游戏。\r\n"
            + "2. 请保持 NaruSasu English_Data 文件夹与 exe 在同一个目录，不要单独移动 exe。\r\n"
            + "3. 首次运行若弹出 Windows 防火墙 / SmartScreen 提示，选择“仍要运行”即可。\r\n"
            + "4. 游戏需要联网下载词库（首次启动会自动下载）。\r\n"
            + "5. 所有分辨率与画质设置可在启动后按需调整。\r\n",
            new UTF8Encoding(true));

        Log("----- Windows 构建成功 -----");
        return true;
    }

    // ---------------- Android ----------------
    /// <summary>
    /// cmd == "apk"   → Mono 后端 + ARMv7（构建快，适合老设备 / 快速验证）
    /// cmd == "apk64" → IL2CPP 后端 + ARM64|ARMv7（兼容所有现代手机，作为正式发布包）
    /// </summary>
    static bool BuildAndroid(string cmd)
    {
        bool il2cpp = (cmd == "apk64");
        Log("----- Android APK 构建开始 (" + (il2cpp ? "IL2CPP/ARM64+ARMv7" : "Mono/ARMv7") + ") -----");

        try
        {
            PlayerSettings.Android.useCustomKeystore = false;   // 使用 Unity 自带 debug keystore
            PlayerSettings.Android.useAPKExpansionFiles = false;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = Mathf.Max(1, PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.productName = "NaruSasu English";
            PlayerSettings.companyName = "Hazel1103";
            try { PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.Hazel1103.NaruSasuEnglish"); }
            catch (Exception e) { Log("  applicationIdentifier 设置跳过: " + e.Message); }
            if (il2cpp)
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
                PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, Il2CppCompilerConfiguration.Release);
            }
            else
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.Mono2x);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;
            }
        }
        catch (Exception e) { Log("  Android 设置部分失败: " + e.Message); }

        try
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }
        catch (Exception e) { Log("  屏幕方向设置失败: " + e.Message); }

        // NDK / SDK 路径
        try
        {
            string sdkNdk = Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines/AndroidPlayer/SDK/ndk/23.1.7779620");
            string bundled = Path.Combine(EditorApplication.applicationContentsPath,
                "PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/windows-x86_64/bin/clang.exe");
            if (Directory.Exists(sdkNdk))
            {
                EditorPrefs.SetString("NdkRoot", sdkNdk);
                AndroidExternalToolsSettings.ndkRootPath = sdkNdk;
                bundled = Path.Combine(sdkNdk, "toolchains/llvm/prebuilt/windows-x86_64/bin/clang.exe");
            }
            Log("  NDK 可用 = " + File.Exists(bundled));
        }
        catch (Exception e) { Log("  NDK 设置失败: " + e.Message); }

        EditorUserBuildSettings.buildAppBundle = false;

        // Unity 的 Android 工具链不接受非 ASCII 工程路径，提前给出明确提示
        bool ascii = true;
        foreach (char c in Root) { if (c > 127) { ascii = false; break; } }
        if (!ascii)
        {
            Log("  !! 工程路径含非 ASCII 字符，Android 构建必然失败：");
            Log("     " + Root);
            Log("     请把工程复制到纯英文路径（例如 E:\\SpellingGameBuild）后再构建 APK。");
        }

        string outDir = Path.Combine(Root, "Build/Android");
        Directory.CreateDirectory(outDir);
        string apk = Path.Combine(outDir, il2cpp ? "NaruSasu English_ARM64.apk" : "NaruSasu English.apk");

        Log("  输出 = " + apk);
        Log("  ABI = " + PlayerSettings.Android.targetArchitectures
            + "  后端 = " + PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android)
            + "  minSdk = " + PlayerSettings.Android.minSdkVersion);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = apk,
            targetGroup = BuildTargetGroup.Android,
            target = BuildTarget.Android,
            options = BuildOptions.None,
        });

        BuildSummary s = report.summary;
        Log("  result=" + s.result + "  errors=" + s.totalErrors + "  warnings=" + s.totalWarnings);
        Log("  size=" + (s.totalSize / 1048576.0).ToString("0.0") + " MB   耗时=" + s.totalTime);
        if (s.result != BuildResult.Succeeded)
        {
            foreach (var step in report.steps)
                foreach (var msg in step.messages)
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Log("  [" + step.name + "] " + msg.content);
            Log("----- Android 构建失败 -----");
            return false;
        }

        Log("----- Android 构建成功 -----");
        return true;
    }

    // ---------------------------------------------------------------- 命令行入口
    // 用法（纯英文工程路径下）：
    //   Unity.exe -batchmode -quit -projectPath E:\SpellingGameBuild ^
    //             -executeMethod GamePackager.CliBuildApk64 -logFile <日志路径>
    public static void CliBuildWin()   { CliRun("win"); }
    public static void CliBuildApk()   { CliRun("apk"); }
    public static void CliBuildApk64() { CliRun("apk64"); }

    static void CliRun(string cmd)
    {
        Log("############ CLI 构建开始 : " + cmd + " ############");
        Log("时间 = " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Log("工程 = " + Root);
        int code = 3;
        try
        {
            BuildTargetGroup g;
            BuildTarget t;
            TargetOf(cmd, out g, out t);
            if (EditorUserBuildSettings.activeBuildTarget != t)
            {
                bool sw = EditorUserBuildSettings.SwitchActiveBuildTarget(g, t);
                Log("CLI 切换平台 -> " + t + " 返回 " + sw
                    + "，active=" + EditorUserBuildSettings.activeBuildTarget);
            }
            code = RunBuild(cmd) ? 0 : 1;
        }
        catch (Exception e)
        {
            Log("CLI 异常: " + e);
            code = 2;
        }
        SessionState.SetString(SS_PENDING, "");
        Log("@PACKAGE-DONE@ cli " + cmd + " code=" + code);
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    // ---------------------------------------------------------------- 日志
    static void Log(string line)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.AppendAllText(LogPath, DateTime.Now.ToString("[HH:mm:ss] ") + line + "\r\n",
                new UTF8Encoding(true));
        }
        catch { }
        Debug.Log("[GamePackager] " + line);
    }
}
