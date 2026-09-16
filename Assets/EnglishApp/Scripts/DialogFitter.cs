using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 对话框自适应缩放。
///
/// 背景：任务成功 / 任务失败 / 暂停 三个弹窗的窗口面板分别是 626 和 480 单位高，
/// 而战斗取景框只有 477 单位高，弹窗上下都被切掉、显示不全（用户反馈）。
/// 这里按取景框高度自动缩放窗口面板，保证任何分辨率下都完整可见。
///
/// 预制体/场景是二进制序列化改不了，所以在运行时调用。
/// </summary>
public static class DialogFitter
{
    /// <summary>窗口面板最多占取景框高度的比例。</summary>
    public const float MaxHeightRatio = 0.70f;
    /// <summary>窗口面板最多占取景框宽度的比例。</summary>
    public const float MaxWidthRatio = 0.92f;

    /// <summary>
    /// 缩放弹窗里真正的窗口面板（弹窗根节点是全屏遮罩，要缩的是它下面那块）。
    /// 重复调用是安全的：尺寸按未缩放值计算。
    /// </summary>
    public static void Fit(GameObject dialog)
    {
        if (dialog == null) return;

        Transform panel = FindPanel(dialog.transform);
        if (panel == null) return;
        RectTransform rt = panel as RectTransform;
        if (rt == null) return;

        Canvas canvas = dialog.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null) return;

        float availH = canvasRect.rect.height * MaxHeightRatio;
        float availW = canvasRect.rect.width * MaxWidthRatio;
        if (availH <= 1f || availW <= 1f) return;

        float w = rt.rect.width;
        float h = rt.rect.height;
        if (w <= 1f || h <= 1f) return;

        float scale = 1f;
        if (h > availH) scale = availH / h;
        if (w * scale > availW) scale = Mathf.Min(scale, availW / w);
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) return;

        // 居中摆放，避免缩放后跑到取景框外。
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = new Vector3(scale, scale, 1f);

        Debug.Log(string.Format(
            "[弹窗适配] {0} 窗口 {1:F0}x{2:F0} 取景框 {3:F0}x{4:F0} 可容纳 {5:F0}x{6:F0} 缩放={7:F3} 结果 {8:F0}x{9:F0}",
            dialog.name, w, h, canvasRect.rect.width, canvasRect.rect.height,
            availW, availH, scale, w * scale, h * scale));
    }

    /// <summary>找到弹窗根节点下真正的窗口面板：第一个带 Image 的直接子节点。</summary>
    private static Transform FindPanel(Transform root)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.ToLowerInvariant().StartsWith("window")
                && child.GetComponent<Image>() != null)
                return child;
        }
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.GetComponent<Image>() != null && child.GetComponent<RectTransform>() != null)
                return child;
        }
        return null;
    }
}
