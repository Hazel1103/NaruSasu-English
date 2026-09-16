using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色模式自检：验证字体、头像资源和角色面板能否正常创建。
/// 菜单：Tools/角色模式自检；命令行：
/// Unity.exe -batchmode -quit -projectPath <工程> -executeMethod CharacterModeSelfTest.Run
/// </summary>
public static class CharacterModeSelfTest
{
    [MenuItem("Tools/角色模式自检")]
    public static void Run()
    {
        Debug.Log("[自检] ===== 开始 =====");

        Font font = Resources.Load<Font>("CharacterMode/font");
        Debug.Log("[自检] 中文字体 CharacterMode/font => " + (font != null ? font.name : "NULL"));

        Debug.Log("[自检] 鸣人头像 => " + (Resources.Load<Sprite>("CharacterMode/naruto_avatar") != null ? "OK" : "NULL"));
        Debug.Log("[自检] 佐助头像 => " + (Resources.Load<Sprite>("CharacterMode/sasuke_avatar") != null ? "OK" : "NULL"));
        Debug.Log("[自检] 鸣人音效 => " + (Resources.Load<AudioClip>("CharacterMode/naruto_correct") != null ? "OK" : "NULL"));
        Debug.Log("[自检] 佐助音效 => " + (Resources.Load<AudioClip>("CharacterMode/sasuke_correct") != null ? "OK" : "NULL"));

        // 小怪马赛克皮肤：鸣人模式用佐助形象，佐助模式用鸣人形象，各 3 个变体
        CheckEnemySkins();

        CharacterModeManager manager = null;
        try
        {
            GameObject host = new GameObject("CharacterModeSelfTestManager");
            manager = host.AddComponent<CharacterModeManager>();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[自检] 创建管理器异常（编辑模式下 DontDestroyOnLoad 警告可忽略）：" + e.Message);
        }

        if (manager == null)
        {
            Debug.LogError("[自检] 无法创建 CharacterModeManager，终止。");
            Debug.Log("[自检] ===== 结束（失败）=====");
            return;
        }

        try
        {
            manager.ApplyToScene();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[自检] ApplyToScene 抛异常：" + e.GetType().Name + " - " + e.Message);
            Debug.Log("[自检] ===== 结束（失败）=====");
            return;
        }

        CharacterModeCanvas marker = Object.FindObjectOfType<CharacterModeCanvas>();
        if (marker != null)
        {
            Canvas c = marker.GetComponent<Canvas>();
            CanvasScaler scaler = marker.GetComponent<CanvasScaler>();
            Debug.Log("[自检] 独立画布 => 已创建, sortingOrder=" + (c != null ? c.sortingOrder.ToString() : "?")
                      + ", 参考分辨率=" + (scaler != null ? scaler.referenceResolution.ToString() : "?"));
        }
        else
        {
            Debug.LogError("[自检] 独立画布 => 未创建");
        }

        CharacterModeRuntimeUI ui = Object.FindObjectOfType<CharacterModeRuntimeUI>();
        Debug.Log("[自检] 角色面板 => " + (ui != null ? "已创建" : "未创建"));

        if (ui != null)
        {
            Text[] texts = ui.GetComponentsInChildren<Text>(true);
            Debug.Log("[自检] 文本控件数量 = " + texts.Length);
            foreach (Text t in texts)
            {
                Debug.Log("[自检]   Text '" + t.name + "' | 字体=" + (t.font != null ? t.font.name : "NULL")
                          + " | 内容='" + t.text + "' | 激活=" + t.gameObject.activeSelf);
            }

            Image[] images = ui.GetComponentsInChildren<Image>(true);
            Debug.Log("[自检] 图片控件数量 = " + images.Length);
            foreach (Image im in images)
            {
                Debug.Log("[自检]   Image '" + im.name + "' | sprite=" + (im.sprite != null ? im.sprite.name : "NULL"));
            }

            Button[] buttons = ui.GetComponentsInChildren<Button>(true);
            Debug.Log("[自检] 按钮数量 = " + buttons.Length);
        }

        Debug.Log("[自检] ===== 结束 =====");
    }

    /// <summary>
    /// 检查 6 张小怪马赛克贴图是否就位，并打印每个变体的尺寸与像素尺寸，
    /// 便于确认 Unity 已按 Sprite 类型正确导入（而不是当成普通贴图）。
    /// </summary>
    private static void CheckEnemySkins()
    {
        Debug.Log("[自检] --- 小怪马赛克皮肤 ---");
        string[] kinds = { "sasuke", "naruto" };
        string[] labels = { "佐助形象（鸣人模式下的小怪）", "鸣人形象（佐助模式下的小怪）" };
        int okCount = 0;

        for (int k = 0; k < kinds.Length; k++)
        {
            string total = "";
            for (int i = 1; i <= 3; i++)
            {
                string path = "CharacterMode/enemy/enemy_" + kinds[k] + "_" + i;
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite == null)
                {
                    Debug.LogError("[自检] " + labels[k] + " 变体" + i + " => 缺失 (" + path + ")");
                    continue;
                }
                okCount++;
                total += string.Format(" #{0}({1}x{2}px, {3:F0}x{4:F0}u)", i,
                    sprite.rect.width, sprite.rect.height,
                    sprite.rect.width / sprite.pixelsPerUnit,
                    sprite.rect.height / sprite.pixelsPerUnit);
            }
            Debug.Log("[自检] " + labels[k] + " =>" + total);
        }

        Debug.Log("[自检] 小怪皮肤就位数量 = " + okCount + " / 6"
                  + (okCount == 6 ? "  ✅ 全部正常" : "  ⚠️ 有缺失，小怪会回退成原形象"));
    }
}
