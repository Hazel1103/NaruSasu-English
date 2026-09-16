using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 诊断「游戏说明」弹窗（DataLoader.aboutDialog）的完整结构：
/// 逐层打印对象名、激活状态、Text 内容、Button 的点击目标，
/// 结果写到 &lt;工程&gt;/Build/about-diag.txt，方便外部读取。
/// 触发方式：在 &lt;工程&gt;/Temp/ 下放一个空文件 aboutDiag.request
/// </summary>
[InitializeOnLoad]
public static class AboutDialogDiag
{
    const string RequestName = "aboutDiag.request";
    const string OutRel = "Build/about-diag.txt";

    static int tick;

    static AboutDialogDiag()
    {
        EditorApplication.update += Poll;
    }

    static string Root { get { return Directory.GetParent(Application.dataPath).FullName; } }
    static string RequestPath { get { return Path.Combine(Path.Combine(Root, "Temp"), RequestName); } }

    static void Poll()
    {
        tick++;
        if (tick % 20 != 0) return;
        if (!File.Exists(RequestPath)) return;
        try { File.Delete(RequestPath); } catch (IOException) { }
        try { Run(); }
        catch (Exception e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("异常: " + e);
            File.WriteAllText(Path.Combine(Root, OutRel), sb.ToString(), Encoding.UTF8);
            Debug.LogError("[说明弹窗诊断] " + e);
        }
    }

    [MenuItem("Tools/诊断游戏说明弹窗", false, 60)]
    public static void RunMenu() { Run(); }

    public static void Run()
    {
        var sb = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        sb.AppendLine("当前场景: " + scene.name + "  路径=" + scene.path);
        if (scene.name != "MainScene")
        {
            scene = EditorSceneManager.OpenScene("Assets/EnglishApp/Scenes/MainScene.unity");
            sb.AppendLine("已打开 MainScene");
        }

        DataLoader loader = null;
        foreach (var go in scene.GetRootGameObjects())
        {
            var dl = go.GetComponentInChildren<DataLoader>(true);
            if (dl != null) { loader = dl; break; }
        }
        sb.AppendLine("DataLoader = " + (loader == null ? "未找到" : loader.gameObject.name));
        if (loader == null)
        {
            File.WriteAllText(Path.Combine(Root, OutRel), sb.ToString(), Encoding.UTF8);
            return;
        }

        sb.AppendLine("choseDialog = " + NameOf(loader.choseDialog));
        sb.AppendLine("aboutDialog = " + NameOf(loader.aboutDialog));
        sb.AppendLine();
        if (loader.aboutDialog != null)
        {
            sb.AppendLine("===== aboutDialog 结构 =====");
            Dump(loader.aboutDialog.transform, 0, sb);
        }
        else
        {
            sb.AppendLine("aboutDialog 为空 —— 改为在场景里搜索名字含 Window 的候选：");
            foreach (var go in scene.GetRootGameObjects())
                DumpCandidates(go.transform, 0, sb);
        }

        File.WriteAllText(Path.Combine(Root, OutRel), sb.ToString(), Encoding.UTF8);
        Debug.Log("[说明弹窗诊断] 已写出 " + OutRel);
    }

    static string NameOf(GameObject go)
    {
        return go == null ? "(null)" : go.name + " @" + PathOf(go.transform);
    }

    static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }

    static void DumpCandidates(Transform t, int depth, StringBuilder sb)
    {
        if (depth > 6) return;
        string n = t.name.ToLowerInvariant();
        if (n.Contains("window") || n.Contains("dialog") || n.Contains("about"))
            sb.AppendLine("[候选] " + PathOf(t) + "  active=" + t.gameObject.activeSelf);
        for (int i = 0; i < t.childCount; i++) DumpCandidates(t.GetChild(i), depth + 1, sb);
    }

    static void Dump(Transform t, int depth, StringBuilder sb)
    {
        if (depth > 8) return;
        string indent = new string(' ', depth * 2);
        var go = t.gameObject;
        var sb2 = new StringBuilder();
        sb2.Append(indent + "- " + go.name + "  activeSelf=" + go.activeSelf + " activeInHierarchy=" + go.activeInHierarchy);
        var rect = go.GetComponent<RectTransform>();
        if (rect != null)
            sb2.Append(string.Format("  size=({0:F0},{1:F0}) anchoredPos=({2:F0},{3:F0}) scale={4:F2}",
                rect.sizeDelta.x, rect.sizeDelta.y, rect.anchoredPosition.x, rect.anchoredPosition.y, rect.localScale.x));
        var txt = go.GetComponent<Text>();
        if (txt != null)
            sb2.Append("  [Text] \"" + (txt.text ?? "").Replace("\n", "\\n") + "\"  font=" +
                       (txt.font == null ? "null" : txt.font.name) + " size=" + txt.fontSize +
                       " color=" + ColorUtility.ToHtmlStringRGB(txt.color));
        var img = go.GetComponent<Image>();
        if (img != null)
            sb2.Append("  [Image] sprite=" + (img.sprite == null ? "null" : img.sprite.name));
        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            sb2.Append("  [Button] 交互=" + btn.interactable + " 回调数=" + btn.onClick.GetPersistentEventCount());
            for (int i = 0; i < btn.onClick.GetPersistentEventCount(); i++)
            {
                var target = btn.onClick.GetPersistentTarget(i);
                sb2.Append(" | " + (target == null ? "null" : target.GetType().Name) + "." +
                           btn.onClick.GetPersistentMethodName(i) +
                           "(" + btn.onClick.GetPersistentMethodName(i) + ")");
            }
        }
        sb.AppendLine(sb2.ToString());
        for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb);
    }
}
