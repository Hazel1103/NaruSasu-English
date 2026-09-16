using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 小怪换皮演示：一键进入塔防战斗场景并自动开始游戏，方便直接看到新形象的小怪。
///
/// 背景：塔防战斗要从"主菜单 -> 选词库 -> 关卡选择 -> 战斗"一路点进去，
/// 验证一次换皮要操作很多步。这里提供一个直达入口用于验收。
///
/// 菜单：Tools/🎬 小怪换皮演示（直达塔防战斗）
/// </summary>
[InitializeOnLoad]
public static class EnemySkinDemo
{
    private const string BattleScene = "Assets/EnglishApp/Scenes/TD_Scene.unity";
    private const string RequestFileName = "enemySkinDemo.request";
    /// <summary>
    /// 可选：强行指定要演示的模式。
    /// 内容写 naruto（小怪=佐助）或 sasuke（小怪=鸣人），用于逐个验收两种搭配。
    /// </summary>
    private const string ModeFileName = "enemySkinMode.request";

    private static int tick;
    private static bool triggered;

    static EnemySkinDemo()
    {
        EditorApplication.update += Poll;
    }

    private static string RequestPath
    {
        get
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(Path.Combine(root, "Temp"), RequestFileName);
        }
    }

    private static void Poll()
    {
        // 请求文件被外部删掉后复位，允许反复触发演示。
        // 否则每次都得改脚本逼 Unity 重新编译（只有域名重载才清得掉静态状态）。
        if (triggered && !File.Exists(RequestPath)) triggered = false;
        if (triggered) return;
        tick++;
        if (tick % 30 != 0) return;
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        triggered = true;
        EnterBattle(true);
        try { File.Delete(RequestPath); } catch (IOException) { }
    }

    [MenuItem("Tools/🎬 小怪换皮演示（直达塔防战斗）", false, 21)]
    public static void EnterBattle()
    {
        EnterBattle(false);
    }

    /// <summary>由请求文件触发时带抓拍标记。</summary>
    public static void EnterBattleWithCapture()
    {
        EnterBattle(true);
    }

    private static void EnterBattle(bool capture)
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[换皮演示] 请先退出 Play 模式。");
            return;
        }

        if (!File.Exists(BattleScene))
        {
            Debug.LogError("[换皮演示] 找不到战斗场景：" + BattleScene);
            return;
        }

        // 战斗场景读取 persistentDataPath 里的词库 xml 与 PlayerPrefs 关卡信息，
        // 这些在之前的运行中已经准备好（词库 English_1，关卡 1-1）。
        try
        {
            // 若指定了演示模式，先把角色模式写进 PlayerPrefs，
            // 这样进入战斗后小怪就会按对应搭配换皮。
            string modePath = Path.Combine(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Temp"), ModeFileName);
            if (File.Exists(modePath))
            {
                string mode = File.ReadAllText(modePath).Trim().ToLowerInvariant();
                if (mode == "sasuke")
                {
                    PlayerPrefs.SetInt(CharacterModeManager.PreferenceKey, (int)CharacterModeManager.Character.Sasuke);
                    Debug.Log("[换皮演示] 强制【佐助模式】→ 小怪应显示鸣人形象");
                }
                else if (mode == "naruto")
                {
                    PlayerPrefs.SetInt(CharacterModeManager.PreferenceKey, (int)CharacterModeManager.Character.Naruto);
                    Debug.Log("[换皮演示] 强制【鸣人模式】→ 小怪应显示佐助形象");
                }
                PlayerPrefs.Save();
            }

            string missionVoc = PlayerPrefs.GetString(Conf.missionVocFileKey);
            if (string.IsNullOrEmpty(missionVoc))
            {
                PlayerPrefs.SetString(Conf.missionVocFileKey, "English_1");
                Debug.Log("[换皮演示] 未设置任务词库，已默认使用 English_1");
            }
            if (string.IsNullOrEmpty(PlayerPrefs.GetString(Conf.levelNum)))
                PlayerPrefs.SetString(Conf.levelNum, "1-1");
            if (string.IsNullOrEmpty(PlayerPrefs.GetString(Conf.levelVocIndex)))
                PlayerPrefs.SetString(Conf.levelVocIndex, "0-9");
            if (!PlayerPrefs.HasKey(Conf.levelType))
                PlayerPrefs.SetInt(Conf.levelType, 1);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[换皮演示] 初始化 PlayerPrefs 时出错（可忽略）：" + e.Message);
        }

        EditorSceneManager.OpenScene(BattleScene, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
        Debug.Log("[换皮演示] 已进入塔防战斗场景，开始观察小怪形象。");
    }
}
