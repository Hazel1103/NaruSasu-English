using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 自动修复 Build Settings 里的场景列表。
///
/// 背景：本工程的 EditorBuildSettings 里还残留着已废弃的
///   Assets/GameRes/Scenes/MainScene.unity
///   Assets/GameRes/Scenes/TD_Scene.unity
/// 这两个路径早就随目录调整被删掉了，但它们排在列表的最前面（索引 0 和 1）。
/// 而主菜单的"开始游戏"按钮调用的是 DataLoader.LoadVocLibAndStartGame(1)，
/// 也就是按索引加载场景 —— 它拿到的是那个已经不存在的旧 TD_Scene，于是点了没反应。
///
/// 这里把列表整理成唯一正确的顺序：
///   0 = MainScene（主菜单）
///   1 = TD_LevelSelect（关卡选择）  <- 开始游戏按钮跳这里
///   2 = TD_Scene（塔防答题）
///
/// 只在列表与期望不一致时才写入，避免每次编译都触发资源导入。
/// </summary>
[InitializeOnLoad]
public static class BuildSettingsFixer
{
    private static readonly string[] ExpectedScenes =
    {
        "Assets/EnglishApp/Scenes/MainScene.unity",
        "Assets/EnglishApp/Scenes/TD_LevelSelect.unity",
        "Assets/EnglishApp/Scenes/TD_Scene.unity"
    };

    private const string MenuPath = "Build/🔧 修复场景列表 (Build Settings)";

    static BuildSettingsFixer()
    {
        EditorApplication.delayCall += TryFix;
    }

    [MenuItem(MenuPath, false, 20)]
    public static void FixNow()
    {
        Apply(true);
    }

    private static void TryFix()
    {
        Apply(false);
    }

    private static void Apply(bool force)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;

        var current = EditorBuildSettings.scenes;
        if (!force && IsAlreadyCorrect(current))
            return;

        var list = new List<EditorBuildSettingsScene>();
        foreach (string path in ExpectedScenes)
        {
            if (!File.Exists(path))
            {
                Debug.LogError("[BuildSettingsFixer] 场景缺失，已跳过：" + path);
                continue;
            }
            list.Add(new EditorBuildSettingsScene(path, true));
        }

        if (list.Count == 0)
            return;

        EditorBuildSettings.scenes = list.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildSettingsFixer] 已修复 Build Settings 场景列表："
                  + string.Join(" -> ", Array.ConvertAll(list.ToArray(), s => Path.GetFileNameWithoutExtension(s.path))));
    }

    private static bool IsAlreadyCorrect(EditorBuildSettingsScene[] current)
    {
        if (current == null || current.Length != ExpectedScenes.Length)
            return false;

        for (int i = 0; i < ExpectedScenes.Length; i++)
        {
            if (current[i] == null)
                return false;
            if (!string.Equals(current[i].path, ExpectedScenes[i], StringComparison.OrdinalIgnoreCase))
                return false;
            if (!current[i].enabled)
                return false;
        }
        return true;
    }
}
