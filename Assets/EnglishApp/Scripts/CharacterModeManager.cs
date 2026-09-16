using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 全局角色模式服务。挂载位置：任意场景中的空 GameObject（建议命名 CharacterModeManager）。
/// 若场景中没有挂载此脚本，CharacterModeBootstrap 会在启动时自动创建一个实例。
/// </summary>
public sealed class CharacterModeManager : MonoBehaviour
{
    public enum Character
    {
        Naruto = 0,
        Sasuke = 1
    }

    /// <summary>
    /// 角色主题对英语游戏界面的影响程度。
    /// 英语游戏本身有一套完整的 UI（血条/蓝条、输入框、技能按钮、关卡列表），
    /// 直接把全场景文字和图片刷成主题色会破坏可读性，所以默认只做轻度融合。
    /// </summary>
    public enum ThemeMode
    {
        /// <summary>只提供角色面板和答题台词，完全不改动游戏原有界面配色。</summary>
        None = 0,
        /// <summary>（默认）只轻微调整相机/背景色调，文字一律保持原样，保证可读。</summary>
        Subtle = 1,
        /// <summary>全量染色：把场景内所有文字和带关键字的图片刷成主题色。</summary>
        Full = 2
    }

    public const string PreferenceKey = "SpellingGame.CharacterMode";

    [Header("可选资源（未赋值时从 Resources/CharacterMode 自动加载）")]
    [Tooltip("鸣人头像 Sprite。可拖入 PNG/JPG 的 Sprite 资源。")]
    public Sprite narutoAvatar;
    [Tooltip("佐助头像 Sprite。可拖入 PNG/JPG 的 Sprite 资源。")]
    public Sprite sasukeAvatar;
    [Tooltip("鸣人答对音效。")]
    public AudioClip narutoCorrectClip;
    [Tooltip("佐助答对音效。")]
    public AudioClip sasukeCorrectClip;

    [Header("可选场景引用（用于显式控制主题）")]
    [Tooltip("场景主背景 Image；为空时使用 Camera.backgroundColor。")]
    public Image backgroundImage;
    [Tooltip("需要跟随角色变化的主色控件，例如按钮背景、进度条。")]
    public Graphic[] primaryGraphics;
    [Tooltip("需要跟随角色变化的文字控件。")]
    public Text[] themedTexts;
    [Tooltip("游戏界面中的头像 Image。")]
    public Image avatarImage;
    [Tooltip("显示当前角色名称的 Text。")]
    public Text characterNameText;
    [Tooltip("显示答题反馈台词的 Text；为空时自动创建。")]
    public Text feedbackText;
    [Tooltip("答对时播放音效的 AudioSource；为空时自动创建。")]
    public AudioSource feedbackAudioSource;

    [Header("运行时 UI")]
    [Tooltip("勾选后在没有显式 UI 引用时自动创建角色选择面板和反馈文本。")]
    public bool createRuntimeUI = true;
    [Tooltip("只有这个场景（首页）显示角色模式选择面板；关卡选择页和战斗页一律不显示。")]
    public string homeSceneName = "MainScene";
    [Tooltip("Subtle=只淡淡着色背景，游戏文字保持原样（推荐）；Full=把全场景文字/图片刷成主题色；None=完全不改动游戏界面。")]
    public ThemeMode themeMode = ThemeMode.Subtle;
    [Tooltip("Subtle 模式下背景色与主题色的混合比例，0=完全不改，1=完全用主题色。")]
    [Range(0f, 1f)]
    public float backgroundTint = 0.35f;

    public static CharacterModeManager Instance { get; private set; }
    public Character Current { get; private set; }
    public event Action<Character> CharacterChanged;

    private bool applied;
    private CharacterModeRuntimeUI runtimeUI;

    // 记录场景原始配色，用于切换角色/关闭主题时还原，避免反复叠加导致颜色漂移。
    private bool originalColorsCaptured;
    private Color originalCameraBackground = Color.black;

    public Color PrimaryColor { get { return Current == Character.Naruto ? new Color32(239, 112, 25, 255) : new Color32(18, 35, 58, 255); } }
    /// <summary>
    /// 按钮高亮色。佐助的主色太深（接近黑），直接拿来当按钮底色会和深色面板糊成一团，
    /// 所以另给一个亮蓝，保证「哪个按钮被选中」一眼能看出来。
    /// </summary>
    public Color AccentColor { get { return Current == Character.Naruto ? new Color32(255, 142, 42, 255) : new Color32(74, 136, 208, 255); } }
    /// <summary>
    /// 模式选择面板的底色。要够深才能衬出高亮按钮，但不能深到看不见 ——
    /// 佐助用过深的藏青时，面板几乎和背景融为一体，看着就像"面板消失了"。
    /// </summary>
    public Color PanelColor { get { return Current == Character.Naruto ? new Color32(92, 46, 14, 235) : new Color32(30, 46, 68, 235); } }
    public Color BackgroundColor { get { return Current == Character.Naruto ? new Color32(255, 237, 215, 255) : new Color32(184, 198, 213, 255); } }
    public Color TextColor { get { return Current == Character.Naruto ? Color.black : Color.white; } }
    public string CorrectLine { get { return Current == Character.Naruto ? "做得不错！继续努力！可不能半途而废！" : "……进步了。"; } }
    public string WrongLine { get { return Current == Character.Naruto ? "没关系！再来一次，多加练习一定能记住！" : "基础不够扎实，重新记。"; } }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Current = (Character)Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, (int)Character.Naruto), 0, 1);
        LoadDefaultResources();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        ApplyToScene();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        applied = false;
        ApplyToScene();
    }

    private void LoadDefaultResources()
    {
        if (narutoAvatar == null) narutoAvatar = LoadSprite("CharacterMode/naruto_avatar");
        if (sasukeAvatar == null) sasukeAvatar = LoadSprite("CharacterMode/sasuke_avatar");
        if (narutoCorrectClip == null) narutoCorrectClip = Resources.Load<AudioClip>("CharacterMode/naruto_correct");
        if (sasukeCorrectClip == null) sasukeCorrectClip = Resources.Load<AudioClip>("CharacterMode/sasuke_correct");
    }

    /// <summary>
    /// 加载头像 Sprite。若图片没有生成 Sprite 子资源（导入类型不是 Sprite），
    /// 则退化为直接用贴图创建一个 Sprite，避免界面上只出现一个白色方块。
    /// </summary>
    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null)
            return sprite;

        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null)
        {
            Debug.LogWarning("[CharacterMode] 头像资源不存在：" + path);
            return null;
        }

        Debug.LogWarning("[CharacterMode] " + path + " 没有 Sprite 子资源，已按贴图临时创建 Sprite。"
                         + "建议把该图片的 Texture Type 设为 Sprite (2D and UI)。");
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    public void SelectNaruto() { Select(Character.Naruto); }
    public void SelectSasuke() { Select(Character.Sasuke); }

    public void Select(Character character)
    {
        Current = character;
        PlayerPrefs.SetInt(PreferenceKey, (int)character);
        PlayerPrefs.Save();
        applied = false;
        ApplyToScene();
        if (CharacterChanged != null) CharacterChanged(Current);
    }

    public void ShowCorrectFeedback() { ShowFeedback(CorrectLine, true); }
    public void ShowWrongFeedback() { ShowFeedback(WrongLine, false); }

    public void ShowFeedback(string line, bool correct)
    {
        ApplyToScene();
        if (feedbackText == null) return;
        feedbackText.text = line;
        feedbackText.color = TextColor;
        feedbackText.gameObject.SetActive(true);
        CancelInvoke("HideFeedback");
        Invoke("HideFeedback", 2.2f);
        if (correct && feedbackAudioSource != null)
        {
            AudioClip clip = Current == Character.Naruto ? narutoCorrectClip : sasukeCorrectClip;
            if (clip != null) feedbackAudioSource.PlayOneShot(clip);
        }
    }

    private void HideFeedback()
    {
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    /// <summary>当前场景是否是首页（只有首页显示角色模式选择面板）。</summary>
    public bool IsHomeScene
    {
        get { return SceneManager.GetActiveScene().name == homeSceneName; }
    }

    public void ApplyToScene()
    {
        if (applied && runtimeUI != null) return;
        LoadDefaultResources();
        if (createRuntimeUI)
        {
            // 面板在所有场景都创建：底部的答题反馈文字在战斗中要用到。
            // 但"模式选择框"只在首页显示（用户要求），其它页面隐藏。
            if (runtimeUI == null)
            {
                runtimeUI = FindObjectOfType<CharacterModeRuntimeUI>();
                if (runtimeUI == null) runtimeUI = CharacterModeRuntimeUI.Create(this);
            }
            if (runtimeUI != null)
            {
                runtimeUI.Bind(this);
                runtimeUI.SetPanelVisible(IsHomeScene);
            }
        }

        if (avatarImage != null) avatarImage.sprite = Current == Character.Naruto ? narutoAvatar : sasukeAvatar;
        if (characterNameText != null)
        {
            characterNameText.text = Current == Character.Naruto ? "鸣人模式" : "佐助模式";
            // 运行时面板的名字画在深色面板上，必须保持白色才看得清；
            // 只有场景自带的文字控件才跟随主题色。
            if (characterNameText.name != "CharacterName")
                characterNameText.color = TextColor;
        }
        if (feedbackText != null) feedbackText.color = TextColor;
        if (backgroundImage != null) backgroundImage.color = BackgroundColor;
        if (primaryGraphics != null)
        {
            foreach (Graphic graphic in primaryGraphics)
                if (graphic != null) graphic.color = PrimaryColor;
        }
        if (themedTexts != null)
        {
            foreach (Text text in themedTexts)
                if (text != null) text.color = TextColor;
        }
        ApplySceneTheme();

        CharacterModeFeedback feedback = FindObjectOfType<CharacterModeFeedback>();
        if (feedback == null)
        {
            GameController controller = FindObjectOfType<GameController>();
            if (controller != null)
            {
                feedback = controller.gameObject.AddComponent<CharacterModeFeedback>();
                feedback.Initialize(this, controller);
            }
        }
        applied = true;
    }

    /// <summary>
    /// 按 themeMode 把角色主题融合到当前场景。
    /// 关键点：英语游戏原有的文字配色一律不碰，否则血条数值、单词、按钮文字会变糊。
    /// </summary>
    private void ApplySceneTheme()
    {
        Camera mainCamera = Camera.main;

        if (themeMode == ThemeMode.None)
        {
            RestoreCameraBackground(mainCamera);
            return;
        }

        // 首次上色前先记下原始背景色，之后所有混合都基于它，不会一次比一次更深。
        if (mainCamera != null && !originalColorsCaptured)
        {
            originalCameraBackground = mainCamera.backgroundColor;
            originalColorsCaptured = true;
        }

        if (themeMode == ThemeMode.Subtle)
        {
            // 只把相机背景朝主题色方向轻轻推一点，游戏 UI 完全保持原样。
            if (mainCamera != null)
                mainCamera.backgroundColor = Color.Lerp(originalCameraBackground, BackgroundColor, backgroundTint);
            return;
        }

        // Full：完整主题化（可能改变游戏原有观感，仅在明确需要时使用）
        if (mainCamera != null)
            mainCamera.backgroundColor = BackgroundColor;

        Text[] sceneTexts = FindObjectsOfType<Text>();
        foreach (Text text in sceneTexts)
        {
            if (text == null || (runtimeUI != null && text.transform.IsChildOf(runtimeUI.transform))) continue;
            // 输入框由 CharacterModeFeedback 负责改色，这里跳过，避免答题警示色被覆盖。
            GameController controller = FindObjectOfType<GameController>();
            if (controller != null && controller.inputBox == text) continue;
            text.color = TextColor;
        }

        Image[] sceneImages = FindObjectsOfType<Image>();
        foreach (Image image in sceneImages)
        {
            if (image == null || (runtimeUI != null && image.transform.IsChildOf(runtimeUI.transform))) continue;
            string objectName = image.gameObject.name.ToLowerInvariant();
            if (image.sprite == null && objectName.Contains("background"))
                image.color = BackgroundColor;
            else if (image.sprite == null && (objectName.Contains("button") || objectName.Contains("panel") || objectName.Contains("header") || objectName.Contains("slider")))
                image.color = PrimaryColor;
        }
    }

    private void RestoreCameraBackground(Camera mainCamera)
    {
        if (mainCamera != null && originalColorsCaptured)
            mainCamera.backgroundColor = originalCameraBackground;
    }

    internal void RegisterRuntimeReferences(Image avatar, Text name, Text feedback, AudioSource source)
    {
        avatarImage = avatar;
        characterNameText = name;
        feedbackText = feedback;
        feedbackAudioSource = source;
    }
}

/// <summary>标记角色模式专用的独立画布，用于重复调用时复用。</summary>
public sealed class CharacterModeCanvas : MonoBehaviour
{
}

/// <summary>
/// 自动创建全平台 UGUI 角色选择面板。也可删除后改用手工搭建的 CharacterModeUI 预制体。
/// </summary>
public sealed class CharacterModeRuntimeUI : MonoBehaviour
{
    /// <summary>
    /// 全局唯一实例。切场景时 runtimeUI 字段可能因时序问题查不到，
    /// 导致重复创建出第二块面板，所以这里用静态引用兜底。
    /// </summary>
    private static CharacterModeRuntimeUI current;

    /// <summary>全局唯一的角色面板画布，避免切场景时重复创建。</summary>
    private static CharacterModeCanvas canvasMarker;

    // 这些引用要序列化：面板可以是场景里的常驻对象（由 Tools/把模式选择框放进首页场景 生成），
    // 不序列化的话重新打开场景后引用全丢，面板就成了点不动的空壳。
    private CharacterModeManager manager;
    [SerializeField] private Image avatar;
    [SerializeField] private Text nameText;
    [SerializeField] private Text feedback;
    [SerializeField] private Image panel;
    [SerializeField] private Button narutoButton;
    [SerializeField] private Button sasukeButton;
    [SerializeField] private AudioSource audioSource;
    /// <summary>上次绑定回调的管理器实例；换了实例才需要重绑。</summary>
    private CharacterModeManager boundManager;

    /// <summary>面板结构是否完整（引用都在）。场景里的常驻面板会被序列化恢复成完整实例。</summary>
    public bool IsIntact()
    {
        return panel != null && avatar != null && narutoButton != null && sasukeButton != null;
    }

    public static CharacterModeRuntimeUI Create(CharacterModeManager owner)
    {
        // 已有实例就直接复用，杜绝"两个角色面板"。
        if (current != null && current.avatar != null)
        {
            current.Bind(owner);
            return current;
        }

        // 场景里可能已经摆好了一块常驻面板（由 Tools 菜单生成并保存）——优先复用它。
        // 同时也清掉"空壳"：早期被误存盘的面板引用全为 null，留着会多出一块看不见的板子。
        CharacterModeRuntimeUI scenePanel = null;
        foreach (CharacterModeRuntimeUI found in FindObjectsOfType<CharacterModeRuntimeUI>())
        {
            if (found == null) continue;
            if (found.IsIntact()) { scenePanel = found; continue; }
            if (Application.isPlaying) Destroy(found.gameObject);
            else DestroyImmediate(found.gameObject);
        }
        if (scenePanel != null)
        {
            current = scenePanel;
            current.Bind(owner);
            return current;
        }
        // 始终使用一块独立的覆盖画布：场景自带画布的参考分辨率可能与 1080x1920
        // 不同（例如 MainScene 是 800x600），复用会导致面板尺寸和位置偏离屏幕。
        CharacterModeCanvas marker = canvasMarker != null ? canvasMarker : FindObjectOfType<CharacterModeCanvas>();
        Canvas canvas = marker != null ? marker.GetComponent<Canvas>() : null;
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("CharacterModeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CharacterModeCanvas));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // 游戏是横屏塔防。参考分辨率必须按横屏给，否则竖屏 1080x1920 的参考会把
            // 面板整体压到 0.45 倍 —— 两个模式按钮实际只剩 53x22 像素，根本点不中。
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasMarker = canvasObject.GetComponent<CharacterModeCanvas>();
        }
        else
        {
            canvasMarker = marker;
            // 复用旧画布时必须强制纠正参考分辨率：场景文件里若残留过旧画布（1080x1920），
            // 沿用它会把整块面板压到 0.45 倍，按钮只剩 47x22 像素。
            CanvasScaler existing = canvas.GetComponent<CanvasScaler>();
            if (existing != null)
                existing.referenceResolution = new Vector2(960, 540);
        }
        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        GameObject root = new GameObject("CharacterModeRuntimeUI", typeof(RectTransform), typeof(CharacterModeRuntimeUI));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.SetParent(canvas.transform, false);
        // 必须撑满画布：代码创建的 RectTransform 默认尺寸为 0，
        // 不拉满的话子元素按锚点定位（例如右上角）会落到画布外或错位。
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.localScale = Vector3.one;
        CharacterModeRuntimeUI ui = root.GetComponent<CharacterModeRuntimeUI>();
        ui.Build(owner);
        current = ui;
        return ui;
    }

    private void OnDestroy()
    {
        if (current == this)
            current = null;
    }

    /// <summary>
    /// Unity 2022 起内置 Arial.ttf 已被移除，Resources.GetBuiltinResource 会抛异常。
    /// 优先使用工程自带的中文字体，避免面板创建中断。
    /// </summary>
    private static Font ResolveFont()
    {
        Font projectFont = Resources.Load<Font>("CharacterMode/font");
        if (projectFont != null)
            return projectFont;

        foreach (string name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
        {
            try
            {
                Font builtin = Resources.GetBuiltinResource<Font>(name);
                if (builtin != null)
                    return builtin;
            }
            catch (Exception)
            {
                // 该版本没有这个内置字体，继续尝试下一个。
            }
        }
        return null;
    }

    private void Build(CharacterModeManager owner)
    {
        BuildStructure();
        Bind(owner);
        owner.RegisterRuntimeReferences(avatar, nameText, feedback, audioSource);
    }

    /// <summary>
    /// 只搭 UI 结构，不依赖运行时管理器。这样编辑模式下也能调用，
    /// 把面板作为普通对象生成到场景里（用户要求：编辑状态和游戏里都要看得见、能点）。
    /// 运行时 Bind() 会按真实模式刷新外观和点击回调。
    /// </summary>
    public void BuildStructure()
    {
        Font font = ResolveFont();

        // 全部尺寸都按横屏参考分辨率 960x540 设计。
        // 面板 276x132 —— 在 882x477 的 Game 视图里实际约 248x119，
        // 两个按钮约 94x43 像素，手指/鼠标都点得中（之前只有 53x22，极易点偏到隔壁按钮）。
        panel = CreateImage("CharacterModePanel", transform, new Vector2(276, 132), new Vector2(0, 1), new Vector2(150, -80), new Color32(0, 0, 0, 170));
        avatar = CreateImage("Avatar", panel.transform, new Vector2(64, 64), new Vector2(0, 1), new Vector2(48, -46), Color.white);

        // 角色名放在头像右侧。原来用画布中心的相对偏移会超出面板宽度被裁掉。
        nameText = CreateText("CharacterName", panel.transform, font, new Vector2(176, 34), Vector2.zero, 22, TextAnchor.MiddleLeft);
        RectTransform nameRect = nameText.rectTransform;
        nameRect.anchorMin = nameRect.anchorMax = new Vector2(0, 1);
        nameRect.anchoredPosition = new Vector2(176, -46);

        // 两个按钮左右对称，各留 30 边距、中间隔 8。
        CreateButton("NarutoButton", panel.transform, font, "鸣人", new Vector2(104, 48), new Vector2(82, 26), out narutoButton);
        CreateButton("SasukeButton", panel.transform, font, "佐助", new Vector2(104, 48), new Vector2(194, 26), out sasukeButton);

        // 反馈文字贴屏幕底部居中。
        feedback = CreateText("CharacterFeedback", transform, font, new Vector2(860, 84), Vector2.zero, 28, TextAnchor.MiddleCenter);
        RectTransform feedbackRect = feedback.rectTransform;
        feedbackRect.anchorMin = feedbackRect.anchorMax = new Vector2(0.5f, 0f);
        feedbackRect.pivot = new Vector2(0.5f, 0f);
        feedbackRect.anchoredPosition = new Vector2(0, 96);
        feedback.gameObject.SetActive(false);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        ApplyDefaultLook();
    }

    /// <summary>
    /// 编辑模式下没有管理器可绑定，先把面板摆成默认的「鸣人模式」样子，
    /// 这样在编辑器里也能看清面板长什么样。进游戏后 Bind() 会按真实模式刷新。
    /// </summary>
    private void ApplyDefaultLook()
    {
        if (nameText != null)
        {
            nameText.text = "鸣人模式";
            nameText.color = Color.white;
        }
        if (avatar != null) avatar.preserveAspect = true;
        if (panel != null) panel.color = new Color32(92, 46, 14, 235);
        SetButtonColor(narutoButton, new Color32(255, 142, 42, 255));
        SetButtonColor(sasukeButton, new Color32(74, 74, 82, 255));
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image != null) image.color = color;
    }

    /// <summary>
    /// 重新绑定两个模式按钮的点击回调。
    /// 每次 Bind 都重绑：回调里取的是「当前的」CharacterModeManager.Instance，
    /// 这样即使面板被复用到另一个管理器实例上（切场景、域重载），也不会点到失效的对象。
    /// </summary>
    private void BindButtons()
    {
        // 已经绑到同一个管理器就不再动它：避免在一次 onClick 派发过程中反复清空调听列表。
        if (boundManager == CharacterModeManager.Instance && narutoButton != null && sasukeButton != null)
            return;
        boundManager = CharacterModeManager.Instance;

        if (narutoButton != null)
        {
            narutoButton.onClick.RemoveAllListeners();
            narutoButton.onClick.AddListener(delegate ()
            {
                CharacterModeManager m = CharacterModeManager.Instance;
                if (m != null) m.Select(CharacterModeManager.Character.Naruto);
            });
        }
        if (sasukeButton != null)
        {
            sasukeButton.onClick.RemoveAllListeners();
            sasukeButton.onClick.AddListener(delegate ()
            {
                CharacterModeManager m = CharacterModeManager.Instance;
                if (m != null) m.Select(CharacterModeManager.Character.Sasuke);
            });
        }
    }

    public void Bind(CharacterModeManager owner)
    {
        manager = owner;
        if (avatar == null) return;
        BindButtons();
        avatar.sprite = owner.Current == CharacterModeManager.Character.Naruto ? owner.narutoAvatar : owner.sasukeAvatar;
        avatar.preserveAspect = true;
        nameText.text = owner.Current == CharacterModeManager.Character.Naruto ? "鸣人模式" : "佐助模式";
        nameText.color = Color.white;
        feedback.color = owner.TextColor;

        // 面板用压暗的主题色，按钮高亮用强调色，未选中用中性灰 ——
        // 保证「佐助」被选中时也能一眼看出来（之前面板和按钮同色，看着像没反应）。
        panel.color = owner.PanelColor;
        Color idle = new Color32(74, 74, 82, 255);
        Image narutoImage = narutoButton == null ? null : narutoButton.GetComponent<Image>();
        Image sasukeImage = sasukeButton == null ? null : sasukeButton.GetComponent<Image>();
        if (narutoImage != null)
            narutoImage.color = owner.Current == CharacterModeManager.Character.Naruto ? owner.AccentColor : idle;
        if (sasukeImage != null)
            sasukeImage.color = owner.Current == CharacterModeManager.Character.Sasuke ? owner.AccentColor : idle;
    }

    /// <summary>
    /// 显示/隐藏模式选择框。关卡选择页、战斗页不显示（用户要求），
    /// 底部的答题反馈文字不属于选择框，保持显示。
    /// </summary>
    public void SetPanelVisible(bool visible)
    {
        if (panel != null) panel.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 兜底：面板可见性只在 ApplyToScene 里设置一次是不够的 ——
    /// 场景切换、域重载、面板被别的流程误关都可能让状态对不上。
    /// 这里每帧按「当前是不是首页」纠偏，代价只是一次布尔比较。
    /// </summary>
    private void Update()
    {
        if (manager == null || panel == null) return;
        bool want = manager.IsHomeScene;
        if (panel.gameObject.activeSelf != want)
            panel.gameObject.SetActive(want);
    }

    /// <summary>兼容旧调用：隐藏模式选择框。</summary>
    public void HidePanel()
    {
        SetPanelVisible(false);
    }

    private static Image CreateImage(string objectName, Transform parent, Vector2 size, Vector2 anchor, Vector2 position, Color color)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        obj.GetComponent<Image>().color = color;
        return obj.GetComponent<Image>();
    }

    private static Text CreateText(string objectName, Transform parent, Font font, Vector2 size, Vector2 position, int fontSize, TextAnchor alignment)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        Text text = obj.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void CreateButton(string objectName, Transform parent, Font font, string label, Vector2 size, Vector2 position, out Button button)
    {
        Image image = CreateImage(objectName, parent, size, new Vector2(0, 0), position, new Color32(90, 90, 90, 255));
        button = image.gameObject.AddComponent<Button>();
        // 代码 AddComponent 出来的 Button 不会自动填 targetGraphic，手动指定保证按下态正常。
        button.targetGraphic = image;
        Text text = CreateText("Label", image.transform, font, size, Vector2.zero, 22, TextAnchor.MiddleCenter);
        text.text = label;
        text.color = Color.white;
        // 文字不参与射线，点击一律落到按钮本体上，避免多命中一层。
        text.raycastTarget = false;
    }
}

/// <summary>启动时创建全局管理器，不需要修改原有场景和答题脚本。</summary>
public static class CharacterModeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateManager()
    {
        if (CharacterModeManager.Instance != null) return;
        GameObject obj = new GameObject("CharacterModeManager");
        obj.AddComponent<CharacterModeManager>();
    }
}
