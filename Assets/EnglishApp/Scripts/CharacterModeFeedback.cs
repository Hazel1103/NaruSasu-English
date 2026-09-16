using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 不改动 GameController.Attack 的监听器：通过输入框警示色和 Enemy 的 damage 动画判断答题结果。
/// 挂载位置：自动挂到当前场景的 GameController；也可手动挂载并拖入 controller。
/// </summary>
public sealed class CharacterModeFeedback : MonoBehaviour
{
    [Tooltip("可选，留空时自动查找 GameController。")]
    public GameController controller;
    [Tooltip("可选，留空时使用 CharacterModeManager.Instance。")]
    public CharacterModeManager modeManager;
    [Tooltip("两次反馈之间的最短间隔。")]
    public float feedbackCooldown = 0.25f;

    private readonly HashSet<int> damageStates = new HashSet<int>();
    private float nextFeedbackTime;
    private bool warningWasRed;
    // 输入框原本的颜色。答题警示动画结束后要还原成它，
    // 否则在 Subtle 主题下会被强行刷成主题色，反而看不清。
    private bool inputColorCaptured;
    private Color originalInputColor = Color.white;

    public void Initialize(CharacterModeManager manager, GameController gameController)
    {
        modeManager = manager;
        controller = gameController;
    }

    private void Awake()
    {
        if (modeManager == null) modeManager = CharacterModeManager.Instance;
        if (controller == null) controller = GetComponent<GameController>();
    }

    private void Update()
    {
        if (modeManager == null) modeManager = CharacterModeManager.Instance;
        if (controller == null) controller = FindObjectOfType<GameController>();
        if (modeManager == null || controller == null) return;

        if (controller.inputBox != null)
        {
            if (!inputColorCaptured && !(controller.inputBox.color.r > 0.8f && controller.inputBox.color.g < 0.35f))
            {
                originalInputColor = controller.inputBox.color;
                inputColorCaptured = true;
            }

            bool isRed = controller.inputBox.color.r > 0.8f && controller.inputBox.color.g < 0.35f;
            if (isRed && !warningWasRed)
            {
                modeManager.ShowWrongFeedback();
                nextFeedbackTime = Time.unscaledTime + feedbackCooldown;
            }
            else if (!isRed)
            {
                // 答题警示动画结束后的还原色：
                // Full 主题下用主题文字色；其余模式一律还原成场景原本的颜色，保证可读。
                bool useThemeColor = modeManager.themeMode == CharacterModeManager.ThemeMode.Full;
                controller.inputBox.color = useThemeColor ? modeManager.TextColor : originalInputColor;
            }
            warningWasRed = isRed;
        }

        Enemy[] enemies = FindObjectsOfType<Enemy>();
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null) continue;
            Animator animator = enemy.GetComponent<Animator>();
            if (animator == null) continue;
            int id = enemy.GetInstanceID();
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool damage = state.IsName("damage") || state.IsName("Base Layer.damage");
            if (damage && !damageStates.Contains(id))
            {
                damageStates.Add(id);
                if (Time.unscaledTime >= nextFeedbackTime)
                {
                    modeManager.ShowCorrectFeedback();
                    nextFeedbackTime = Time.unscaledTime + feedbackCooldown;
                }
            }
            else if (!damage)
            {
                damageStates.Remove(id);
            }
        }
    }
}
