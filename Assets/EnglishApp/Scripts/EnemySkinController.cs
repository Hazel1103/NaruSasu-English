using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把小怪（Enemy）的 Animator Controller 换成火影角色（鸣人 / 佐助）的跑步动画。
///
/// 规则（按用户要求）：
///   · 玩家选「鸣人模式」 -> 随机一个【佐助】形象（Sasuke1/2/3）
///   · 玩家选「佐助模式」 -> 随机一个【鸣人】形象（Naruto1/2/3）
///   每个角色用 3 帧跑步循环（move）+ 第1帧静止帧（attack/damage/death）。
///
/// 实现要点：
///   不再用 LateUpdate 逐帧覆盖 SpriteRenderer.sprite —— 那样会和 Animator 打架。
///   这里直接给敌人的 Animator 换上对应角色的 Controller，
///   由 Animator 状态机（move/attack/damage/death + b_move 等参数）自己驱动逐帧动画。
///   敌人的移动 / 攻击 / 死亡逻辑（Enemy.cs）完全不受影响，只换外观。
/// </summary>
public sealed class EnemySkinController : MonoBehaviour
{
    /// <summary>当前小怪使用的形象。</summary>
    public enum SkinKind
    {
        Sasuke = 0,   // 鸣人模式下小怪的样子
        Naruto = 1    // 佐助模式下小怪的样子
    }

    private const int VariantCount = 3;

    /// <summary>本局是否启用了换皮（菜单里可关掉）。</summary>
    public static bool Enabled = true;

    /// <summary>马赛克小人相对原小怪的显示高度倍率。1.0 = 和原小怪一样高，默认 1.3 倍。</summary>
    [Range(0.5f, 2.5f)]
    public float heightRatio = 1.3f;

    /// <summary> Inspector 可调：是否让形象面朝左。运行中可实时勾选/取消测试方向。 </summary>
    public bool faceLeft = false;

    /// <summary>当前形象对应的角色名，供界面提示用。</summary>
    public string SkinLabel { get { return myKind == SkinKind.Sasuke ? "佐助" : "鸣人"; } }
    public int Variant { get { return myVariant + 1; } }

    /// <summary>换皮后小怪的世界高度，供 GameController 摆放单词窗口。</summary>
    public float WorldHeight { get { return targetWorldHeight > 0f ? targetWorldHeight : originalWorldHeight; } }

    private Enemy enemy;
    private SpriteRenderer[] renderers;
    private Sprite mySkin;            // 仅用于缩放基准（新形象第1帧）
    private int myVariant = -1;
    private SkinKind myKind;
    private float originalWorldHeight;
    private float targetWorldHeight;

    // ------------------------------------------------------------------ 挂载
    /// <summary>
    /// 由 Enemy.ApplyCharacterSkin 调用，给这个小怪换上对应形象的 Animator Controller。
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

        string prefix = myKind == SkinKind.Sasuke ? "Sasuke" : "Naruto";
        string ctrlName = prefix + (myVariant + 1);

        var controller = Resources.Load<RuntimeAnimatorController>("EnemyControllers/" + ctrlName);
        if (controller == null)
        {
            Debug.LogWarning("[EnemySkin] 找不到 Animator Controller：" + ctrlName +
                "（请先在 Unity 菜单运行 Tools/跑酷动画/一键配置跑步动画）");
            return;
        }

        // 先记录换皮前原小怪的高度（缩放基准），再换 Controller（换后首帧会变成新形象）。
        CollectRenderers();
        if (renderers != null && renderers.Length > 0 && renderers[0] != null)
            originalWorldHeight = renderers[0].bounds.size.y;

        var anim = enemy.GetComponent<Animator>();
        if (anim != null)
        {
            anim.runtimeAnimatorController = controller;
            enemy.ReinitAnimator();   // 重新给 damage/attack 挂动画事件并进入 move
        }

        // 用新形象第1帧做缩放基准，放大到原小怪高度 * heightRatio。
        Sprite first = Resources.Load<Sprite>("EnemyRunFrames/" + ctrlName + "/" + ctrlName + "_Run_0");
        if (first != null && originalWorldHeight > 0f)
        {
            mySkin = first;
            ApplyScaleToMatch();
        }

        bool appliedFlip = renderers != null && renderers.Length > 0 ? renderers[0].flipX : false;
        Vector3 appliedScale = renderers != null && renderers.Length > 0 ? renderers[0].transform.localScale : Vector3.one;
        Debug.Log(string.Format(
            "[EnemySkin] 对象={0} 形象={1} 变体={2}/{3} Controller={4} 原高度={5:F2} -> 目标高度={6:F2} 渲染器数={7} flipX={8} scale={9:F2},{10:F2} faceLeft={11}",
            owner != null ? owner.gameObject.name : "?",
            SkinLabel, myVariant + 1, VariantCount, ctrlName,
            originalWorldHeight, targetWorldHeight,
            renderers != null ? renderers.Length : 0,
            appliedFlip, appliedScale.x, appliedScale.y, faceLeft));
    }

    /// <summary>
    /// 把新形象缩放到和原小怪接近的显示大小（放大 heightRatio 倍，手机上更清晰）。
    /// 注意：必须等比例缩放 X/Y，否则会把角色压扁；同时用 flipX 统一让角色朝左。
    /// </summary>
    private void ApplyScaleToMatch()
    {
        if (mySkin == null || renderers == null || renderers.Length == 0 || originalWorldHeight <= 0f) return;

        float skinWorldHeight = mySkin.rect.height / mySkin.pixelsPerUnit;
        if (skinWorldHeight <= 0.01f) return;

        float wantHeight = originalWorldHeight * heightRatio;
        float s = wantHeight / skinWorldHeight;
        if (float.IsNaN(s) || float.IsInfinity(s) || s <= 0f) return;

        // 等比例缩放，避免只拉 Y 导致角色被压扁；z 保持原值。
        // X 轴保持正值，用 SpriteRenderer.flipX 做朝向翻转。
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr == null) continue;
            Vector3 scale = sr.transform.localScale;
            // 保持 scale.x 为正，朝向完全由 flipX 控制
            sr.transform.localScale = new Vector3(Mathf.Abs(s), Mathf.Abs(s), scale.z);
            sr.flipX = faceLeft;
        }

        targetWorldHeight = wantHeight;
    }

    /// <summary>
    /// 保险：每帧强制按 faceLeft 设置 flipX。
    /// 这样即使 Animator Write Defaults 把 flipX 重置，也能立即纠正。
    /// </summary>
    void LateUpdate()
    {
        if (renderers == null || renderers.Length == 0) return;
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr == null) continue;
            sr.flipX = faceLeft;
            // scale.x 被其他脚本改负会导致双重翻转，这里强制保持正值。
            Vector3 scale = sr.transform.localScale;
            if (scale.x < 0f)
                sr.transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
        }
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

    // ------------------------------------------------------------------ 资源
    /// <summary>
    /// 随机挑一个变体。用独立 System.Random，避免 Play 模式每次重置种子导致第一只永远同变体。
    /// </summary>
    private static int PickVariant()
    {
        return variantRandom.Next(VariantCount);
    }

    private static readonly System.Random variantRandom =
        new System.Random(unchecked((int)(System.DateTime.Now.Ticks ^ (System.Diagnostics.Process.GetCurrentProcess().Id * 2654435761L))));

    /// <summary>把某形象的全部变体裁到一张图上，用于设置界面的预览。</summary>
    public static Sprite[] GetAllVariants(SkinKind kind)
    {
        string prefix = kind == SkinKind.Sasuke ? "Sasuke" : "Naruto";
        Sprite[] result = new Sprite[VariantCount];
        for (int i = 0; i < VariantCount; i++)
            result[i] = Resources.Load<Sprite>("EnemyRunFrames/" + prefix + (i + 1) + "/" + prefix + (i + 1) + "_Run_0");
        return result;
    }
}
