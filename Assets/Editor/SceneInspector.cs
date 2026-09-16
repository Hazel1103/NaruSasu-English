using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 诊断工具：把场景的完整 UI 层级结构导出成文本，
/// 用于排查"游戏界面看不见"这类问题。
/// 菜单：Build/🔍 导出场景结构
/// </summary>
public static class SceneInspector
{
    private const string OutDir = "Build";

    [MenuItem("Build/🔍 导出场景结构", false, 10)]
    public static void InspectCurrentScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("[SceneInspector] 没有打开的场景");
            return;
        }
        Export(scene.path, false);
    }

    /// <summary>
    /// 批处理入口：导出全部三个场景的结构。
    /// </summary>
    public static void Run()
    {
        string[] scenes =
        {
            "Assets/EnglishApp/Scenes/MainScene.unity",
            "Assets/EnglishApp/Scenes/TD_LevelSelect.unity",
            "Assets/EnglishApp/Scenes/TD_Scene.unity"
        };

        var report = new StringBuilder();
        report.AppendLine("===== 场景结构诊断报告 =====");
        report.AppendLine("导出时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine();

        foreach (string path in scenes)
        {
            if (!File.Exists(path))
            {
                report.AppendLine("## 场景缺失: " + path);
                report.AppendLine();
                continue;
            }
            try
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                report.AppendLine(DumpScene(scene.name, path));
            }
            catch (Exception e)
            {
                report.AppendLine("## 导出失败 " + path + " : " + e.Message);
            }
            report.AppendLine();
        }

        string root = Directory.GetParent(Application.dataPath).FullName;
        string outPath = Path.Combine(Path.Combine(root, OutDir), "scene-report.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllText(outPath, report.ToString(), Encoding.UTF8);
        Debug.Log("[SceneInspector] 报告已写出: " + outPath);
        Debug.Log("[自检] ===== 结束 =====");
    }

    [MenuItem("Build/🔍 导出当前场景结构", false, 11)]
    public static void ExportCurrent()
    {
        var scene = EditorSceneManager.GetActiveScene();
        Export(scene.path, true);
    }

    private static void Export(string scenePath, bool openReport)
    {
        if (string.IsNullOrEmpty(scenePath) || !File.Exists(scenePath))
        {
            Debug.LogWarning("[SceneInspector] 场景路径无效: " + scenePath);
            return;
        }
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        string text = DumpScene(scene.name, scenePath);

        string root = Directory.GetParent(Application.dataPath).FullName;
        string outPath = Path.Combine(Path.Combine(root, OutDir), "scene-report.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllText(outPath, text, Encoding.UTF8);
        Debug.Log("[SceneInspector] 报告已写出: " + outPath);
        if (openReport) EditorUtility.OpenWithDefaultApp(outPath);
    }

    private static string DumpScene(string name, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("########################################");
        sb.AppendLine("## 场景: " + name);
        sb.AppendLine("## 路径: " + path);
        sb.AppendLine("########################################");

        // 相机
        sb.AppendLine();
        sb.AppendLine("--- 相机 ---");
        foreach (var cam in UnityEngine.Object.FindObjectsOfType<Camera>(true))
        {
            sb.AppendLine(string.Format("  Camera '{0}' | depth={1} | clearFlags={2} | bg={3} | ortho={4}",
                GetPath(cam.transform), cam.depth, cam.clearFlags,
                ColorToHex(cam.backgroundColor), cam.orthographic));
        }

        // Canvas 列表（含排序、分辨率、激活状态）
        sb.AppendLine();
        sb.AppendLine("--- Canvas（决定谁盖住谁）---");
        var canvases = new List<Canvas>(UnityEngine.Object.FindObjectsOfType<Canvas>(true));
        canvases.Sort((a, b) => b.sortingOrder.CompareTo(a.sortingOrder));
        foreach (var c in canvases)
        {
            var scaler = c.GetComponent<CanvasScaler>();
            var rect = c.GetComponent<RectTransform>();
            string res = scaler != null
                ? string.Format("{0}x{1} (mode={2})", scaler.referenceResolution.x,
                    scaler.referenceResolution.y, scaler.uiScaleMode)
                : "无 CanvasScaler";
            sb.AppendLine(string.Format("  [{0}] sortingOrder={1} | renderMode={2} | 参考分辨率={3} | active={4} | size={5}",
                GetPath(c.transform), c.sortingOrder, c.renderMode, res,
                IsActive(c.gameObject), rect != null ? rect.sizeDelta.ToString() : "-"));
        }

        // EventSystem
        sb.AppendLine();
        var es = UnityEngine.Object.FindObjectOfType<EventSystem>();
        sb.AppendLine("--- EventSystem: " + (es != null ? GetPath(es.transform) + " (存在)" : "缺失！") + " ---");

        // 关键游戏脚本
        sb.AppendLine();
        sb.AppendLine("--- 游戏逻辑脚本 ---");
        AppendComponent<GameController>(sb, "GameController");
        AppendComponent<LevelManager>(sb, "LevelManager");
        AppendComponent<DataLoader>(sb, "DataLoader");
        AppendComponent<CharacterModeManager>(sb, "CharacterModeManager");
        AppendComponent<CharacterModeFeedback>(sb, "CharacterModeFeedback");

        // 完整层级（UI 部分重点标注）
        sb.AppendLine();
        sb.AppendLine("--- 完整层级（标记 active / 组件）---");
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            DumpTransform(root.transform, 0, sb);
        }

        return sb.ToString();
    }

    private static void AppendComponent<T>(StringBuilder sb, string label) where T : Component
    {
        var found = UnityEngine.Object.FindObjectsOfType<T>(true);
        if (found == null || found.Length == 0)
        {
            sb.AppendLine(string.Format("  {0}: 未挂载", label));
            return;
        }
        foreach (var f in found)
        {
            sb.AppendLine(string.Format("  {0}: {1} (active={2})", label, GetPath(f.transform), IsActive(f.gameObject)));
        }
    }

    private static void DumpTransform(Transform t, int depth, StringBuilder sb)
    {
        var comps = new List<string>();
        foreach (var c in t.GetComponents<Component>())
        {
            if (c == null) { comps.Add("<Missing Script>"); continue; }
            string typeName = c.GetType().Name;
            if (typeName == "Transform" || typeName == "RectTransform") continue;
            comps.Add(typeName);
        }

        string flags = "";
        if (!t.gameObject.activeSelf) flags += "[对象关闭]";
        if (!IsActive(t.gameObject)) flags += "[父级关闭]";

        string extra = "";
        var img = t.GetComponent<Image>();
        if (img != null)
            extra += string.Format(" color={0} sprite={1}", ColorToHex(img.color),
                img.sprite != null ? img.sprite.name : "null");
        var txt = t.GetComponent<Text>();
        if (txt != null)
            extra += string.Format(" text=\"{0}\" color={1} font={2} size={3}",
                txt.text, ColorToHex(txt.color),
                txt.font != null ? txt.font.name : "null", txt.fontSize);
        var rect = t.GetComponent<RectTransform>();
        if (rect != null)
            extra += string.Format(" anchoredPos={0} sizeDelta={1}", rect.anchoredPosition, rect.sizeDelta);

        sb.AppendLine(string.Format("{0}{1}{2} {3}{4}",
            new string(' ', depth * 2),
            t.gameObject.activeSelf ? "●" : "○",
            t.name,
            comps.Count > 0 ? "{" + string.Join(",", comps.ToArray()) + "}" : "",
            flags + extra));

        for (int i = 0; i < t.childCount; i++)
            DumpTransform(t.GetChild(i), depth + 1, sb);
    }

    private static bool IsActive(GameObject go)
    {
        Transform t = go.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf) return false;
            t = t.parent;
        }
        return true;
    }

    private static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }

    private static string ColorToHex(Color c)
    {
        Color32 v = c;
        return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", v.r, v.g, v.b, v.a);
    }
}
