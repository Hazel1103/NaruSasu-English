using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 验收「关于 / 游戏说明」弹窗：进 Play → 点开说明 → 截图（顶部 + 滚到底部各一张）→ 退出 Play。
/// 输出 Build/uishots/08_about.png 与 08_about_bottom.png
/// 触发：Temp/aboutShot.request 或菜单 Tools/截图游戏说明弹窗
/// </summary>
[InitializeOnLoad]
public static class AboutShotCapture
{
    const string RequestName = "aboutShot.request";
    const string ScenePath = "Assets/EnglishApp/Scenes/MainScene.unity";
    const string KeyStep = "AboutShot.step";
    const string KeyTime = "AboutShot.time";

    static int tick;

    static AboutShotCapture() { EditorApplication.update += Poll; }

    static string Root { get { return Directory.GetParent(Application.dataPath).FullName; } }
    static string RequestPath { get { return Path.Combine(Path.Combine(Root, "Temp"), RequestName); } }
    static string OutDir { get { return Path.Combine(Path.Combine(Root, "Build"), "uishots"); } }

    static void Poll()
    {
        int step = SessionState.GetInt(KeyStep, -1);
        if (step < 0)
        {
            tick++;
            if (tick % 30 != 0) return;
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try { File.Delete(RequestPath); } catch (IOException) { }
            try { Directory.CreateDirectory(OutDir); } catch (Exception) { }
            Debug.Log("[说明截图] ===== 开始 =====");
            SessionState.SetInt(KeyStep, 0);
            SessionState.SetFloat(KeyTime, (float)EditorApplication.timeSinceStartup + 0.5f);
            return;
        }
        if (EditorApplication.timeSinceStartup < SessionState.GetFloat(KeyTime, 0f)) return;

        try { DoStep(step); }
        catch (Exception e)
        {
            Debug.LogError("[说明截图] 第 " + step + " 步异常: " + e);
            Next(step + 1, 1.0f);
        }
    }

    [MenuItem("Tools/截图游戏说明弹窗", false, 62)]
    public static void RunMenu()
    {
        try { File.WriteAllText(RequestPath, "run"); } catch (Exception e) { Debug.LogError(e.Message); }
    }

    static void DoStep(int step)
    {
        switch (step)
        {
            case 0:
                if (SceneManager.GetActiveScene().path != ScenePath)
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Log("已打开 MainScene，准备进 Play");
                Next(1, 1.0f);
                break;

            case 1:
                EditorApplication.isPlaying = true;
                Log("已进入 Play，等首页加载…");
                Next(2, 6.0f);
                break;

            case 2:
                {
                    ButtonEvents be = FindBe();
                    if (be == null) { Log("找不到 ButtonEvents！"); Next(5, 1f); break; }
                    be.OnButtonClick(2);          // 2 = 游戏说明
                    Log("已点开游戏说明弹窗");
                    Next(3, 2.5f);
                    break;
                }

            case 3:
                Shot("08_about");
                Next(4, 2.0f);
                break;

            case 4:
                ScrollTo(0.5f);
                Next(5, 2.0f);
                break;

            case 5:
                Shot("08_about_mid");
                Next(6, 2.0f);
                break;

            case 6:
                ScrollTo(0f);
                Next(7, 2.0f);
                break;

            case 7:
                Shot("08_about_bottom");
                Next(8, 2.0f);
                break;

            case 8:
                EditorApplication.isPlaying = false;
                Log("已退出 Play，流程结束");
                SessionState.SetInt(KeyStep, -1);
                break;

            default:
                Log("结束");
                SessionState.SetInt(KeyStep, -1);
                break;
        }
    }

    static ButtonEvents FindBe()
    {
        var all = Resources.FindObjectsOfTypeAll<ButtonEvents>();
        foreach (var b in all)
        {
            if (b == null) continue;
            if (!b.gameObject.scene.IsValid()) continue;   // 排除 prefab 资源
            if (b.gameObject.activeInHierarchy) return b;
        }
        foreach (var b in all)
            if (b != null && b.gameObject.scene.IsValid()) return b;
        return null;
    }

    static ScrollRect FindScroll()
    {
        DataLoader loader = null;
        var all = Resources.FindObjectsOfTypeAll<DataLoader>();
        foreach (var d in all)
            if (d != null && d.gameObject.scene.IsValid() && d.aboutDialog != null) { loader = d; break; }
        if (loader == null) return null;
        return loader.aboutDialog.GetComponentInChildren<ScrollRect>(true);
    }

    static void ScrollTo(float pos)
    {
        ScrollRect sr = FindScroll();
        if (sr == null) { Log("找不到 ScrollRect"); return; }
        sr.verticalNormalizedPosition = pos;
        Canvas.ForceUpdateCanvases();
        Log("说明弹窗滚动位置 = " + pos.ToString("F2"));
    }

    static void Shot(string name)
    {
        string path = Path.Combine(OutDir, name + ".png");
        try { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); } catch (Exception) { }
        ScreenCapture.CaptureScreenshot(path, 2);
        Log("已保存 " + path + "  (" + Screen.width + "x" + Screen.height + " x2)");
    }

    static void Next(int step, float delay)
    {
        SessionState.SetInt(KeyStep, step);
        SessionState.SetFloat(KeyTime, (float)EditorApplication.timeSinceStartup + delay);
    }

    static void Log(string s) { Debug.Log("[说明截图] " + s); }
}
