using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 编辑器内一键运行入口。
///
/// 背景：本项目通过 Unity Hub 打开时，工程记录的 LastSceneManagerSetup 为空，
/// 编辑器会停在一个未命名的空场景（标题栏显示 Untitled）。此时英语游戏的主菜单
/// 根本没有加载，画面上只剩下运行时动态生成的角色面板，看起来就像"游戏不见了"。
///
/// 因此这里做两件事：
///   1) 编辑器空闲时若发现没有打开任何有效场景，自动切到 MainScene；
///   2) 提供菜单和请求文件两种手动触发方式。
///
/// 菜单：Build/▶ 运行游戏 (MainScene)、Build/■ 停止游戏、Build/📂 打开主菜单场景
/// 自动运行：命令行 -runGame，或 <工程>/Temp/autoplay.request
/// </summary>
[InitializeOnLoad]
public static class GameRunner
{
    private const string MainScenePath = "Assets/EnglishApp/Scenes/MainScene.unity";
    private const string AutoRunArg = "-runGame";
    private const string RequestFileName = "autoplay.request";

    private static int tick;
    private static bool started;
    private static bool sceneFixed;

    static GameRunner()
    {
        EditorApplication.update += Poll;
    }

    private static string RequestFilePath
    {
        get
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(Path.Combine(root, "Temp"), RequestFileName);
        }
    }

    private static void Poll()
    {
        if (started)
            return;

        // 每 30 帧查一次，避免每帧访问磁盘。
        tick++;
        if (tick % 30 != 0)
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // 第一优先：修正"空场景"问题，保证用户看到的是英语游戏主菜单。
        if (!sceneFixed)
        {
            if (!TryEnsureMainSceneOpen())
                return; // 场景还没准备好，下次再试
            sceneFixed = true;
        }

        bool requested = Array.IndexOf(Environment.GetCommandLineArgs(), AutoRunArg) >= 0
                         || File.Exists(RequestFilePath);
        if (!requested)
            return;

        started = true;

        try
        {
            if (File.Exists(RequestFilePath))
                File.Delete(RequestFilePath);
        }
        catch (IOException)
        {
            // 文件被占用时忽略，下次启动再删。
        }

        EditorApplication.update -= Poll;
        EditorApplication.isPlaying = true;
        Debug.Log("[GameRunner] 已进入 Play 模式：" + MainScenePath);
    }

    /// <summary>
    /// 确保编辑器当前打开的是 MainScene。
    /// 返回 false 表示现在还不适合切场景（例如仍在导入），调用方应稍后重试。
    /// </summary>
    private static bool TryEnsureMainSceneOpen()
    {
        if (EditorApplication.isPlaying)
            return true;

        if (!File.Exists(MainScenePath))
        {
            Debug.LogError("[GameRunner] 找不到主菜单场景：" + MainScenePath);
            return true; // 找不到就别反复重试刷屏
        }

        Scene active = EditorSceneManager.GetActiveScene();

        // 已打开 MainScene：什么都不用做。
        if (active.IsValid() && !string.IsNullOrEmpty(active.path)
            && string.Equals(Path.GetFullPath(active.path), Path.GetFullPath(MainScenePath), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 场景名为空 = 未保存的空场景（Untitled），直接切，无需询问保存。
        bool isUntitled = !active.IsValid() || string.IsNullOrEmpty(active.path);
        if (!isUntitled)
        {
            // 用户已经打开了别的场景并且可能有改动，交给用户决定。
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return true;
        }

        EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        Debug.Log("[GameRunner] 已自动打开主菜单场景：" + MainScenePath);
        return true;
    }

    [MenuItem("Build/📂 打开主菜单场景", false, 1)]
    public static void OpenMainSceneMenu()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[GameRunner] 请先退出 Play 模式再切换场景。");
            return;
        }
        if (!File.Exists(MainScenePath))
        {
            Debug.LogError("[GameRunner] 找不到场景：" + MainScenePath);
            return;
        }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Build/▶ 运行游戏 (MainScene)", false, 2)]
    public static void RunGame()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }

        if (!File.Exists(MainScenePath))
        {
            Debug.LogError("[GameRunner] 找不到场景：" + MainScenePath);
            return;
        }

        Scene active = EditorSceneManager.GetActiveScene();
        bool needOpen = !active.IsValid()
                        || string.IsNullOrEmpty(active.path)
                        || !string.Equals(Path.GetFullPath(active.path), Path.GetFullPath(MainScenePath), StringComparison.OrdinalIgnoreCase);

        if (needOpen && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        EditorApplication.isPlaying = true;
    }

    [MenuItem("Build/■ 停止游戏", false, 3)]
    public static void StopGame()
    {
        EditorApplication.isPlaying = false;
    }
}
