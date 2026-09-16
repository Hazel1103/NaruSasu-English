using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 把模式选择框作为「常驻 UI」生成到首页场景里。
///
/// 为什么需要它：面板原本是 Play 模式下用代码创建的，编辑器里（非 Play 状态）压根不存在，
/// 只能靠诊断工具跑的时候瞥一眼，用户既看不到也没法调位置。
/// 这个工具在编辑模式下把 Canvas + 面板 + 按钮搭好并存盘，之后编辑状态和游戏里都看得到、点得动。
///
/// 触发：菜单 Tools/把模式选择框放进首页场景，或 Temp 下放一个 buildPanel.request。
/// </summary>
[InitializeOnLoad]
public static class CharacterModePanelBuilder
{
    private const string ScenePath = "Assets/EnglishApp/Scenes/MainScene.unity";
    private const string RequestFile = "buildPanel.request";
    private const string CanvasName = "CharacterModeCanvas";
    private const string RootName = "CharacterModeRuntimeUI";
    private static int tick;

    static CharacterModePanelBuilder()
    {
        EditorApplication.update += Poll;
    }

    private static string RequestPath
    {
        get { return Path.Combine(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp"), RequestFile); }
    }

    private static void Poll()
    {
        tick++;
        bool hasRequest = File.Exists(RequestPath);

        // 心跳：请求一直不执行时，靠它判断是 update 没跑，还是被状态条件挡住了。
        if (hasRequest && tick % 100 == 0)
        {
            Debug.Log(string.Format("[模式面板] 等待中 tick={0} isPlaying={1} willChange={2} isCompiling={3} isUpdating={4}",
                tick, EditorApplication.isPlaying, EditorApplication.isPlayingOrWillChangePlaymode,
                EditorApplication.isCompiling, EditorApplication.isUpdating));
        }

        if (!hasRequest) return;
        if (tick % 20 != 0) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // 上次测试留在 Play 模式里了，先退出来再改场景。
            Debug.Log("[模式面板] 当前在 Play 模式，先退出再生成。");
            SessionState.SetBool("BuildPanelPending", true);
            EditorApplication.isPlaying = false;
            return;
        }

        SessionState.SetBool("BuildPanelPending", false);
        try { File.Delete(RequestPath); } catch (IOException) { }
        Build();
    }

    [MenuItem("Tools/把模式选择框放进首页场景")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[模式面板] 请先退出 Play 模式再生成。");
            return;
        }

        // 直接改场景文件，先存盘避免 OpenScene 弹「是否保存」对话框卡住流程。
        try
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.SaveOpenScenes();
        }
        catch (System.Exception) { }

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[模式面板] 打不开场景 " + ScenePath);
            return;
        }

        // 全量重建：保证场景里的面板结构和最新代码一致（尺寸改过好几轮了）。
        int removed = 0;
        foreach (CharacterModeRuntimeUI ui in Object.FindObjectsOfType<CharacterModeRuntimeUI>(true))
        {
            Object.DestroyImmediate(ui.gameObject);
            removed++;
        }
        foreach (CharacterModeCanvas c in Object.FindObjectsOfType<CharacterModeCanvas>(true))
        {
            Object.DestroyImmediate(c.gameObject);
            removed++;
        }

        // ---- 画布：配置必须和运行时创建的一致，否则面板尺寸会跑偏 ----
        GameObject canvasObject = new GameObject(CanvasName,
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CharacterModeCanvas));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 540);   // 横屏塔防，不能用竖屏参考
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // ---- 面板本体 ----
        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(CharacterModeRuntimeUI));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvasObject.transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.localScale = Vector3.one;
        rootRect.anchoredPosition = Vector2.zero;

        CharacterModeRuntimeUI runtimeUI = root.GetComponent<CharacterModeRuntimeUI>();
        runtimeUI.BuildStructure();

        // 编辑状态下先挂上鸣人头像，不然只有一个白方块。
        Transform avatarTransform = root.transform.Find("CharacterModePanel/Avatar");
        if (avatarTransform != null)
        {
            Image avatarImage = avatarTransform.GetComponent<Image>();
            Sprite avatarSprite = Resources.Load<Sprite>("CharacterMode/naruto_avatar");
            if (avatarImage != null && avatarSprite != null)
                avatarImage.sprite = avatarSprite;
        }

        if (Object.FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = canvasObject;
        EditorGUIUtility.PingObject(canvasObject);
        Debug.Log("[模式面板] 已把模式选择框生成到 " + ScenePath + "（清理旧对象 " + removed + " 个）。"
                  + "之后编辑状态和运行时都能看到它。");
    }
}
