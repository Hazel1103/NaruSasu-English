using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 界面尺寸检测工具：把单词框（VocWin）、任务成功/失败弹窗（gameOverWin）、
/// 暂停弹窗（pauseWin）的层级结构、RectTransform 尺寸和字号打印到 Console，
/// 便于在代码里做精确调整（预制体和场景都是二进制序列化，读不出文本）。
///
/// 触发：Temp/uiDump.request，或菜单 Tools/界面尺寸检测
/// </summary>
[InitializeOnLoad]
public static class UiLayoutDump
{
    private const string RequestFileName = "uiDump.request";
    private static int tick;
    private static bool done;

    static UiLayoutDump()
    {
        EditorApplication.update += Poll;
    }

    private static string ProjectRoot
    {
        get { return Directory.GetParent(Application.dataPath).FullName; }
    }

    private static string RequestPath
    {
        get { return Path.Combine(Path.Combine(ProjectRoot, "Temp"), RequestFileName); }
    }

    private static void Poll()
    {
        // 请求文件被删掉后复位，可以反复触发（不用重编译）。
        if (done && !File.Exists(RequestPath)) done = false;
        if (done) return;

        tick++;
        if (tick % 30 != 0) return;
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        done = true;
        try { File.Delete(RequestPath); } catch (IOException) { }
        Dump();
    }

    [MenuItem("Tools/界面尺寸检测", false, 22)]
    public static void Dump()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("[界面检测] ===== 开始 =====");
        sb.AppendLine("[界面检测] 当前 Game 视图 " + Screen.width + "x" + Screen.height
                      + "  宽高比 " + ((float)Screen.width / Mathf.Max(1, Screen.height)).ToString("F2"));

        // ---------- 1) 单词框预制体 ----------
        GameObject vocPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/EnglishApp/Prefabs/UI/VocWin.prefab");
        if (vocPrefab == null)
            sb.AppendLine("[界面检测] !! 找不到 VocWin.prefab");
        else
            DumpTree(sb, "单词框 VocWin.prefab", vocPrefab.transform, 0, 4);

        // ---------- 2) 战斗场景里的弹窗 ----------
        string scenePath = "Assets/EnglishApp/Scenes/TD_Scene.unity";
        try
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        catch (Exception e)
        {
            sb.AppendLine("[界面检测] !! 打开战斗场景失败：" + e.Message);
        }

        GameController gc = UnityEngine.Object.FindObjectOfType<GameController>();
        if (gc == null)
        {
            sb.AppendLine("[界面检测] !! 战斗场景里找不到 GameController");
        }
        else
        {
            DumpTree(sb, "任务弹窗 gameOverWin", gc.gameOverWin != null ? gc.gameOverWin.transform : null, 0, 5);
            DumpTree(sb, "暂停弹窗 pauseWin", gc.pauseWin != null ? gc.pauseWin.transform : null, 0, 5);

            // 找到弹窗所在画布，打印缩放配置
            if (gc.gameOverWin != null)
            {
                Canvas canvas = gc.gameOverWin.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                    sb.AppendLine("[界面检测] 弹窗所在画布 " + canvas.name
                                  + " renderMode=" + canvas.renderMode
                                  + " 参考分辨率=" + (scaler != null ? scaler.referenceResolution.ToString() : "无")
                                  + " 匹配=" + (scaler != null ? scaler.screenMatchMode + "/" + scaler.matchWidthOrHeight : "无"));
                    RectTransform cr = canvas.GetComponent<RectTransform>();
                    if (cr != null)
                        sb.AppendLine("[界面检测] 画布 Rect = " + Rect(cr));
                }
            }
        }

        sb.AppendLine("[界面检测] ===== 结束 =====");
        Debug.Log(sb.ToString());
    }

    private static string Rect(RectTransform rt)
    {
        return string.Format("size=({0:F0},{1:F0}) anchor=({2:F2},{3:F2})-({4:F2},{5:F2}) pivot=({6:F2},{7:F2}) pos=({8:F0},{9:F0}) 世界尺寸=({10:F0},{11:F0}) scale=({12:F2},{13:F2})",
            rt.sizeDelta.x, rt.sizeDelta.y,
            rt.anchorMin.x, rt.anchorMin.y, rt.anchorMax.x, rt.anchorMax.y,
            rt.pivot.x, rt.pivot.y,
            rt.anchoredPosition.x, rt.anchoredPosition.y,
            rt.rect.width, rt.rect.height,
            rt.lossyScale.x, rt.lossyScale.y);
    }

    private static void DumpTree(StringBuilder sb, string title, Transform root, int depth, int maxDepth)
    {
        sb.AppendLine("[界面检测] --- " + title + " ---");
        if (root == null)
        {
            sb.AppendLine("[界面检测]   (为空)");
            return;
        }
        Walk(sb, root, 0, maxDepth);
    }

    private static void Walk(StringBuilder sb, Transform t, int depth, int maxDepth)
    {
        if (t == null || depth > maxDepth) return;
        string indent = new string(' ', 2 + depth * 2);
        RectTransform rt = t as RectTransform;
        Text text = t.GetComponent<Text>();
        Image image = t.GetComponent<Image>();
        string extra = "";
        if (text != null)
            extra = "  <Text 字号=" + text.fontSize + " 内容=\"" + Trim(text.text) + "\" 颜色=" + text.color
                    + " 对齐=" + text.alignment;
        else if (image != null)
            extra = "  <Image 颜色=" + image.color + (image.sprite != null ? " sprite=" + image.sprite.name : " 无图)");
        sb.AppendLine("[界面检测] " + indent + t.name
                      + (rt != null ? "  " + Rect(rt) : "  (非UI)") + extra);
        for (int i = 0; i < t.childCount; i++)
            Walk(sb, t.GetChild(i), depth + 1, maxDepth);
    }

    private static string Trim(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= 24 ? s : s.Substring(0, 24) + "…";
    }
}
