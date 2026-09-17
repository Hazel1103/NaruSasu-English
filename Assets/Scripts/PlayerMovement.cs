using UnityEngine;

/// <summary>
/// 像素角色左右移动 + 跑步动画控制。
/// 挂在角色物体上（需带 SpriteRenderer、Animator、Rigidbody2D）。
/// Animator 使用 RunAnimSetup 生成的 Controller（Float: Speed，Idle ↔ Run）。
/// 素材朝向为朝左：向左时 localScale.x = -1（保持原样），向右时翻转为 1。
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("移动速度")]
    public float moveSpeed = 5f;

    private Animator animator;
    private Rigidbody2D rb;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");

    void Start()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        float input = Input.GetAxisRaw("Horizontal");

        // 动画参数：|速度| > 0.1 切到 Run，否则回 Idle
        animator.SetFloat(SpeedParam, Mathf.Abs(input));

        // 朝向：素材默认朝左
        if (input > 0)
            transform.localScale = new Vector3(1f, 1f, 1f);   // 朝右（翻转）
        else if (input < 0)
            transform.localScale = new Vector3(-1f, 1f, 1f);  // 朝左（原始）

        rb.velocity = new Vector2(input * moveSpeed, rb.velocity.y);
    }
}
