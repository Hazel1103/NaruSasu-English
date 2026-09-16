using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把小怪（Enemy）的贴图换成"马赛克"风格的鸣人 / 佐助形象。
///
/// 规则（按用户要求）：
///   · 玩家选「鸣人模式」 -> 进攻的小怪显示【佐助】形象
///   · 玩家选「佐助模式」 -> 进攻的小怪显示【鸣人】形象
///   每种形象各有 3 个姿势变体，每个小怪出生时随机挑一个。
///
/// 实现要点：
///   小怪原本是逐帧动画（Animator 每帧写 SpriteRenderer.sprite），
///   因此不能在 Start 里直接改 sprite —— 会被下一帧动画覆盖。
///   这里在 LateUpdate 里"盖"上去（执行顺序排在动画之后），保证始终是我们的形象。
///   动画本身照常播放，只是它写进来的图被我们替换掉，移动/攻击/死亡逻辑完全不受影响。
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class EnemySkinController : MonoBehaviour
{
    /// <summary>当前小怪使用的形象。</summary>
    public enum SkinKind
    {
        Sasuke = 0,   // 鸣人模式下小怪的样子
        Naruto = 1    // 佐助模式下小怪的样子
    }

    private const string ResourceFolder = "CharacterMode/enemy/";
    private const int VariantCount = 3;

    // 缓存：避免每个小怪每次出生都重新 Load 一遍贴图
    private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, Sprite[]> variantCache = new Dictionary<string, Sprite[]>();

    private Enemy enemy;
    private SpriteRenderer[] renderers;
    private Sprite mySkin;
    private int myVariant = -1;
    private SkinKind myKind;
    private bool ready;

    // 原始小怪的高度（换皮前），用来把新贴图缩放到和原小怪差不多大，
    // 否则马赛克图的像素尺寸不同会让小怪突然变大或变小。
    private float originalWorldHeight;
    private float targetWorldHeight;

    /// <summary>换皮后小怪的世界高度，供 GameController 摆放单词窗口。</summary>
    public float WorldHeight { get { return targetWorldHeight > 0f ? targetWorldHeight : originalWorldHeight; } }

    /// <summary>本局是否启用了换皮（菜单里可关掉）。</summary>
    public static bool Enabled = true;

    /// <summary>
    /// 马赛克小人相对原小怪的显示高度倍率。1.0 = 和原小怪一样高。
    /// 图纸造型是完整全身像，同样高度下视觉体量比原来的大头造型小，
    /// 所以默认放大到 1.2 倍，手机上才看得清跑步动作。
    /// </summary>
    [Range(0.5f, 1.5f)]
    public float heightRatio = 1.2f;

    /// <summary>当前形象对应的角色名，供界面提示用。</summary>
    public string SkinLabel { get { return myKind == SkinKind.Sasuke ? "佐助" : "鸣人"; } }
    public int Variant { get { return myVariant + 1; } }

    // ------------------------------------------------------------------ 挂载
    /// <summary>
    /// 由 Enemy.InitEnemyData 调用，给这个小怪换上皮。
    /// </summary>
    public void Apply(Enemy owner)
    {
        if (!Enabled) return;
        enemy = owner;

        // 依赖 CharacterModeManager 的当前模式决定画谁：
        // 鸣人模式 -> 佐助形象；佐助模式 -> 鸣人形象（故意反过来）
        CharacterModeManager.Character current = CharacterModeManager.Character.Naruto;
        if (CharacterModeManager.Instance != null)
            current = CharacterModeManager.Instance.Current;

        myKind = current == CharacterModeManager.Character.Naruto ? SkinKind.Sasuke : SkinKind.Naruto;
        myVariant = PickVariant();

        Sprite skin = GetVariant(myKind, myVariant);
        if (skin == null)
        {
            // 资源缺失时保持原样，不要把小怪变成隐形。
            Debug.LogWarning("[EnemySkin] 找不到小怪皮肤贴图，保持原形象：" + myKind + " #" + (myVariant + 1));
            return;
        }

        mySkin = skin;
        CollectRenderers();

        // 记录换皮前的原始显示尺寸，作为缩放基准。
        if (originalWorldHeight <= 0f && renderers != null && renderers.Length > 0 && renderers[0] != null)
        {
            originalWorldHeight = renderers[0].bounds.size.y;
            targetWorldHeight = originalWorldHeight;
        }

        ApplyScaleToMatch();

        // 诊断日志：确认小怪确实拿到了马赛克皮肤
        Debug.Log(string.Format(
            "[EnemySkin] 对象={0} 皮肤={1} 变体={2}/{3} 贴图={4}x{5}ppu={6} 原高度={7:F2} -> 目标高度={8:F2} 缩放={9:F3} 渲染器数={10}",
            owner != null ? owner.gameObject.name : "?",
            SkinLabel, myVariant + 1, VariantCount,
            mySkin.rect.width, mySkin.rect.height, mySkin.pixelsPerUnit,
            originalWorldHeight, targetWorldHeight,
            renderers != null && renderers.Length > 0 ? renderers[0].transform.localScale.y : 0f,
            renderers != null ? renderers.Length : 0));

        ready = true;
        // 立刻先刷一次，避免出生瞬间闪一下原图
        ApplySkinNow();
    }

    /// <summary>诊断：报告小怪在屏幕上的实际位置和可见性，便于确认它是否真的进入了画面。</summary>
    private void ReportOnScreen(string when)
    {
        if (renderers == null || renderers.Length == 0) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 world = renderers[0].bounds.center;
        Vector3 vp = cam.WorldToViewportPoint(world);
        bool onScreen = vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f && vp.z > 0f;
        Debug.Log(string.Format(
            "[EnemySkin] {0} 皮肤={1} 屏幕位置=({2:F2},{3:F2}) 可见={4} 渲染器启用={5} 激活={6} 大小={7:F2}x{8:F2}",
            when, SkinLabel, vp.x, vp.y, onScreen,
            renderers[0].enabled, renderers[0].gameObject.activeInHierarchy,
            renderers[0].bounds.size.x, renderers[0].bounds.size.y));
    }

    /// <summary>
    /// 把马赛克贴图缩放到和原小怪接近的显示大小，
    /// 保证换肤后小怪在战场上的占位、血条和单词窗口位置基本不变。
    /// </summary>
    private void ApplyScaleToMatch()
    {
        if (mySkin == null || renderers == null || originalWorldHeight <= 0f) return;

        // 贴图本身在世界单位下的高度（pixelsPerUnit 取自 Sprite 导入设置，通常为 100）。
        float skinWorldHeight = mySkin.rect.height / mySkin.pixelsPerUnit;
        if (skinWorldHeight <= 0.01f) return;

        // 和原小怪保持同样的显示高度：马赛克小人画的是紧凑全身像，
        // 缩到 0.82 会显得太小、在手机尺寸下几乎看不清形象。
        float wantHeight = originalWorldHeight * heightRatio;
        float s = wantHeight / skinWorldHeight;
        if (float.IsNaN(s) || float.IsInfinity(s) || s <= 0f) return;

        Vector3 scale = renderers[0].transform.localScale;
        // 只缩放承载小怪本体的那个节点，避免影响 Battle 场景里其它层级。
        renderers[0].transform.localScale = new Vector3(scale.x, scale.y * s, scale.z);
        targetWorldHeight = wantHeight;
    }

    private void CollectRenderers()
    {
        if (renderers != null && renderers.Length > 0) return;

        List<SpriteRenderer> list = new List<SpriteRenderer>();
        foreach (SpriteRenderer sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null) continue;
            // 排除血条、单词窗口等 UI 相关对象，只替换小怪本体。
            if (IsUiRelated(sr.transform)) continue;
            list.Add(sr);
        }
        renderers = list.ToArray();
    }

    private static bool IsUiRelated(Transform t)
    {
        Transform cur = t;
        while (cur != null)
        {
            string n = cur.gameObject.name.ToLowerInvariant();
            if (n.Contains("vocwin") || n.Contains("canvas") || n.Contains("slider")
                || n.Contains("hpbar") || n.Contains("hpslider") || n.Contains("ui"))
                return true;
            cur = cur.parent;
        }
        return false;
    }

    // ------------------------------------------------------------------ 每帧覆盖
    private float nextReport;
    private static bool traceEnabled = System.IO.File.Exists(
        System.IO.Path.Combine(
            System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath), "Temp"),
            "enemySkinTrace.request"));

    private void LateUpdate()
    {
        if (!ready || mySkin == null) return;

        // 诊断开关：默认关闭。需要排查"小怪是不是没进画面"时，
        // 在 Temp 下放一个 enemySkinTrace.request 文件即可开启逐秒位置上报。
        if (traceEnabled && Time.unscaledTime >= nextReport)
        {
            nextReport = Time.unscaledTime + 1.0f;
            ReportOnScreen("位置上报");
        }

        // 角色模式在游戏中途切换时，把小怪形象一起切过去，保证和玩家选的模式一致。
        CharacterModeManager.Character current = CharacterModeManager.Character.Naruto;
        if (CharacterModeManager.Instance != null)
            current = CharacterModeManager.Instance.Current;
        SkinKind wantKind = current == CharacterModeManager.Character.Naruto ? SkinKind.Sasuke : SkinKind.Naruto;
        if (wantKind != myKind)
        {
            myKind = wantKind;
            Sprite skin = GetVariant(myKind, myVariant >= 0 ? myVariant : 0);
            if (skin != null) mySkin = skin;
        }

        ApplySkinNow();
    }

    private void ApplySkinNow()
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;
            // 只在真的不一样时赋值，避免每帧触发无谓的重建。
            if (sr.sprite != mySkin) sr.sprite = mySkin;
        }
    }

    // ------------------------------------------------------------------ 资源
    private static Sprite GetVariant(SkinKind kind, int variant)
    {
        string prefix = kind == SkinKind.Sasuke ? "sasuke" : "naruto";
        string key = prefix + "_" + variant;
        Sprite cached;
        if (spriteCache.TryGetValue(key, out cached)) return cached;

        string path = ResourceFolder + "enemy_" + prefix + "_" + (variant + 1);
        Sprite sprite = LoadSprite(path);
        spriteCache[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// 随机挑一个变体。
    /// 不直接用 UnityEngine.Random.Range：它在每次进入 Play 模式时会被重置成相同种子，
    /// 导致"每局第一只小怪永远是同一个变体"，看起来就像随机没生效。
    /// 这里改用独立的 System.Random，用实例 ID + 启动 tick + 已发放次数混合做种子。
    /// </summary>
    private static int PickVariant()
    {
        return variantRandom.Next(VariantCount);
    }

    private static readonly System.Random variantRandom =
        new System.Random(unchecked((int)(System.DateTime.Now.Ticks ^ (System.Diagnostics.Process.GetCurrentProcess().Id * 2654435761L))));

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = Resources.Load<Sprite>(path);
        if (sprite != null) return sprite;

        // 图片若没被导入成 Sprite 类型，退化为用贴图现场建一个。
        Texture2D texture = Resources.Load<Texture2D>(path);
        if (texture == null) return null;

        Debug.LogWarning("[EnemySkin] " + path + " 不是 Sprite 类型，已按贴图临时创建 Sprite。");
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>把某个形象的全部变体裁到一张图上，用于设置界面的预览。</summary>
    public static Sprite[] GetAllVariants(SkinKind kind)
    {
        string prefix = kind == SkinKind.Sasuke ? "sasuke" : "naruto";
        Sprite[] cached;
        if (variantCache.TryGetValue(prefix, out cached)) return cached;

        Sprite[] result = new Sprite[VariantCount];
        for (int i = 0; i < VariantCount; i++)
            result[i] = GetVariant(kind, i);
        variantCache[prefix] = result;
        return result;
    }
}
