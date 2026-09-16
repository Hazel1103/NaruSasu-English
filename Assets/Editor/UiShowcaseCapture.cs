using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 界面验收截图：依次跑「首页 → 关卡选择 → 战斗 → 暂停弹窗 → 失败弹窗 → 成功弹窗」，
/// 每一步抓一张图存到 &lt;工程&gt;/Build/uishots。
///
/// 触发：Temp/uiShowcase.request，或菜单 Tools/界面验收截图
/// </summary>
[InitializeOnLoad]
public static class UiShowcaseCapture
{
    private const string RequestFileName = "uiShowcase.request";
    private const string HomeScene = "Assets/EnglishApp/Scenes/MainScene.unity";
    private const string LevelScene = "Assets/EnglishApp/Scenes/TD_LevelSelect.unity";
    private const string BattleScene = "Assets/EnglishApp/Scenes/TD_Scene.unity";

    private static int tick;

    // 流程状态必须放 SessionState：进入/退出 Play 模式会触发域重载，
    // 普通静态字段会被清零，流程走到第一步就断了。
    private const string KeyStep = "UiShowcaseCapture.step";
    private const string KeyTime = "UiShowcaseCapture.time";

    static UiShowcaseCapture()
    {
        EditorApplication.update += Poll;
    }

    private static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }
    private static string RequestPath { get { return Path.Combine(Path.Combine(ProjectRoot, "Temp"), RequestFileName); } }
    private static string OutDir { get { return Path.Combine(Path.Combine(ProjectRoot, "Build"), "uishots"); } }

    private static void Poll()
    {
        int step = SessionState.GetInt(KeyStep, -1);

        // 重新下请求 = 从头再来一次（否则上次卡住的步骤会被接着跑，直接乱套）
        bool reRequest = File.Exists(RequestPath);
        if (reRequest && !EditorApplication.isPlaying && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
        {
            step = -1;
            SessionState.SetInt(KeyStep, -1);
        }

        if (step < 0)
        {
            tick++;
            if (tick % 30 != 0) return;
            if (!reRequest && !File.Exists(RequestPath)) return;
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists(RequestPath)) return;
            try { File.Delete(RequestPath); } catch (IOException) { }
            try { Directory.CreateDirectory(OutDir); } catch (Exception) { }
            Debug.Log("[界面截图] ===== 开始 =====");
            SessionState.SetInt(KeyStep, 0);
            SessionState.SetFloat(KeyTime, (float)EditorApplication.timeSinceStartup + 0.5f);
            return;
        }
        Advance(step);
    }

    private static void Advance(int step)
    {
        if (EditorApplication.timeSinceStartup < SessionState.GetFloat(KeyTime, 0f)) return;

        try
        {
            DoStep(step);
        }
        catch (Exception e)
        {
            // 任何一步抛异常都会让 step 停住 -> 每帧重复执行 -> 死循环刷日志。
            // 这里兜底：报错后强制跳到下一步。
            Log("步骤 " + step + " 异常：" + e.Message);
            Next(step + 1, 2.0);
        }
    }

    private static void DoStep(int step)
    {
        switch (step)
        {
            case 0:
                EditorSceneManager.OpenScene(HomeScene, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
                Log("已进入首页，等待界面加载…");
                Next(1, 6.0);
                break;
            case 1:
                Shot("01_home");
                EditorApplication.isPlaying = false;
                Next(2, 3.0);
                break;
            case 2:
                EditorSceneManager.OpenScene(LevelScene, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
                Log("已进入关卡选择页…");
                Next(3, 6.0);
                break;
            case 3:
                Shot("02_levelselect");
                EditorApplication.isPlaying = false;
                Next(4, 3.0);
                break;
            case 4:
                PrepareBattlePrefs();
                EditorSceneManager.OpenScene(BattleScene, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
                Log("已进入战斗场景，等小怪和单词框出现…");
                Next(5, 8.0);
                break;
            case 5:
                Shot("03_battle");
                // 关键：CaptureScreenshot 是「延迟到帧尾」写入的。
                // 如果同一帧里既截图又触发弹窗，03_battle 会被「弹窗出现后」的画面覆盖
                // （实测 03 与 04 的 md5 完全相同）。所以触发必须挪到下一帧。
                Next(50, 2.0);
                break;
            case 50:   // 切到另一个角色模式，验证另一种小怪形象
                {
                    CharacterModeManager m = CharacterModeManager.Instance;
                    if (m != null)
                    {
                        if (m.Current == CharacterModeManager.Character.Naruto) m.SelectSasuke();
                        else m.SelectNaruto();
                        Log("已切换模式 -> " + m.Current);
                    }
                }
                Next(51, 3.0);
                break;
            case 51:
                Shot("07_othermode");
                Next(52, 1.0);
                break;
            case 52:
                {
                    GameSceneEvents ev = UnityEngine.Object.FindObjectOfType<GameSceneEvents>();
                    if (ev != null) ev.OnButtonClick(1);
                }
                Log("已触发暂停弹窗");
                Next(6, 5.0);
                break;
            case 6:
                Shot("04_pause");
                Next(60, 1.0);
                break;
            case 60:   // 关掉暂停，恢复时间流速
                {
                    GameSceneEvents ev2 = UnityEngine.Object.FindObjectOfType<GameSceneEvents>();
                    if (ev2 != null) ev2.OnButtonClick(4);   // 关闭暂停
                    Time.timeScale = 1f;
                }
                Next(7, 2.0);
                break;
            case 7:
                {
                    GameController gc = UnityEngine.Object.FindObjectOfType<GameController>();
                    if (gc != null) gc.Damage(99999);      // 直接把塔打爆 -> 失败弹窗
                }
                Log("已触发失败弹窗");
                Next(8, 5.0);
                break;
            case 8:
                LogState("失败");
                Shot("05_fail");
                Next(70, 1.0);
                break;
            case 70:   // 复用同一个弹窗显示"游戏胜利"（走反射调用，不改动玩法代码）
                {
                    GameController gc = UnityEngine.Object.FindObjectOfType<GameController>();
                    if (gc != null)
                    {
                        // 直接跳进战斗场景时 levelNum 没被赋值，胜利分支会按 (0,0) 去取关卡 -> 空引用。
                        // 这里用反射补成 1-1，纯粹是为了截图，不动玩法代码。
                        try
                        {
                            PlayerPrefs.SetString(Conf.levelNum, "1-1");
                            PlayerPrefs.Save();
                            System.Reflection.FieldInfo fi = typeof(GameController).GetField(
                                "levelNum", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (fi != null) fi.SetValue(gc, new Vector2(1f, 1f));
                        }
                        catch (Exception e) { Log("补关卡号失败（可忽略）：" + e.Message); }

                        try
                        {
                            System.Reflection.MethodInfo mi = typeof(GameController).GetMethod(
                                "ShowGameOverWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (mi != null) mi.Invoke(gc, new object[] { true });
                            else Log("找不到 ShowGameOverWindow");
                        }
                        catch (Exception e)
                        {
                            Log("触发胜利弹窗异常：" + (e.InnerException != null ? e.InnerException.Message : e.Message));
                        }
                    }
                    Log("已触发成功弹窗");
                }
                Next(9, 5.0);   // 无论成功与否都往下走，避免卡死循环
                break;
            case 9:
                LogState("成功");
                Shot("06_win");
                Next(90, 1.0);
                break;
            case 90:
                EditorApplication.isPlaying = false;
                Next(10, 3.0);
                break;
            case 10:
                Debug.Log("[界面截图] ===== 完成，输出目录 " + OutDir + " =====");
                SessionState.SetInt(KeyStep, -1);
                break;
            default:
                SessionState.SetInt(KeyStep, -1);
                break;
        }
    }

    private static void Next(int step, double seconds)
    {
        SessionState.SetInt(KeyStep, step);
        SessionState.SetFloat(KeyTime, (float)EditorApplication.timeSinceStartup + (float)seconds);
    }

    private static void Log(string msg)
    {
        Debug.Log("[界面截图] " + msg);
    }

    private static void Shot(string name)
    {
        string path = Path.Combine(OutDir, name + ".png");
        try
        {
            // 强制重绘所有视图，避免编辑器窗口失焦时画面不刷新
            try { UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); } catch (Exception) { }

            // 用「延迟到帧尾」的官方截屏。同步版 CaptureScreenshotAsTexture 在编辑器失焦时
            // 会抓到过期画面（实测关卡页和暂停页两张图字节完全相同），所以不能用。
            // 代价：同一次 editor update 里不能再改 UI，否则会被写进同一帧 —— 触发弹窗和
            // 截图必须拆成两个 step。
            ScreenCapture.CaptureScreenshot(path, 2);
            Debug.Log("[界面截图] 已保存 " + path + "  (" + Screen.width + "x" + Screen.height + " x2)");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[界面截图] 保存失败：" + e.Message);
        }
    }

    /// <summary>截屏前把弹窗状态打出来，确认真的显示的是我们想要的那个。</summary>
    private static void LogState(string label)
    {
        GameController gc = UnityEngine.Object.FindObjectOfType<GameController>();
        if (gc == null || gc.gameOverWin == null) { Log(label + "：找不到 gameOverWin"); return; }
        GameObject w = gc.gameOverWin;
        string title = "";
        Transform header = w.transform.Find("Window/Header");
        if (header != null)
        {
            Text t = header.GetComponentInChildren<Text>();
            if (t != null) title = t.text;
        }
        RectTransform r = w.transform.Find("Window") as RectTransform;
        Log(label + "：gameOverWin.activeSelf=" + w.activeSelf
            + " 标题='" + title + "'"
            + (r != null ? (" Window尺寸=" + r.rect.width.ToString("0") + "x" + r.rect.height.ToString("0") + " scale=" + r.localScale.x.ToString("0.000")) : ""));
    }

    /// <summary>战斗场景依赖 PlayerPrefs 里的关卡信息，先补齐（与换皮演示一致）。</summary>
    private static void PrepareBattlePrefs()
    {
        try
        {
            if (string.IsNullOrEmpty(PlayerPrefs.GetString(Conf.missionVocFileKey)))
                PlayerPrefs.SetString(Conf.missionVocFileKey, "English_1");
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
            Debug.LogWarning("[界面截图] 初始化 PlayerPrefs 出错（可忽略）：" + e.Message);
        }
    }

    [MenuItem("Tools/界面验收截图", false, 23)]
    public static void RunMenu()
    {
        try
        {
            File.WriteAllText(RequestPath, "run");
            Debug.Log("[界面截图] 已写入请求文件，稍后自动执行：" + RequestPath);
        }
        catch (Exception e)
        {
            Debug.LogError("[界面截图] 无法写入请求文件：" + e.Message);
        }
    }
}
