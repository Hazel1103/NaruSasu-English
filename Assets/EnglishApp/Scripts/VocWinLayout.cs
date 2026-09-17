using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单词框（VocWin）排版修正。
///
/// 背景：小怪头顶的单词框原本只有 97 单位高、字号 14，
/// 在取景框里被压成扁扁的一条，英文和中文都看不清（用户反馈）。
/// 这里把框整体拉高拉宽，并同步放大字号，让英文和中文各占一行看清楚。
///
/// 预制体是二进制序列化改不了，所以在运行时由 GameController 生成小怪时调用。
/// </summary>
public static class VocWinLayout
{
    /// <summary>单词框高度倍率（原 97 → 约 388）。</summary>
    public const float HeightScale = 4.0f;
    /// <summary>单词框宽度倍率（原 241 → 约 603），长单词不再挤成一团。</summary>
    public const float WidthScale = 2.5f;
    /// <summary>整个单词框的整体缩放（解决在镜头里被压得过小的问题）。</summary>
    public const float BoxScale = 2.5f;
    /// <summary>英文字号倍率（原 14）。</summary>
    public const float EnFontScale = 6.0f;
    /// <summary>中文字号倍率（原 14）。</summary>
    public const float ZhFontScale = 5.0f;
    /// <summary>文本两侧留白。</summary>
    public const float TextPadding = 40f;

    private sealed class Applied : MonoBehaviour { }

    public static void Apply(GameObject vocWin)
    {
        if (vocWin == null) return;
        // 同一个单词框只处理一次，避免重复调用把尺寸越放越大。
        if (vocWin.GetComponent<Applied>() != null) return;

        Transform box = vocWin.transform.Find("Voc");
        if (box == null && vocWin.transform.childCount > 0)
            box = vocWin.transform.GetChild(0);
        if (box == null) return;

        RectTransform boxRect = box as RectTransform;
        Vector2 oldSize = Vector2.zero;
        if (boxRect != null)
        {
            oldSize = boxRect.sizeDelta;
            // pivot 在底边中点，加高只会向上长，不会压到小怪身上。
            boxRect.sizeDelta = new Vector2(oldSize.x * WidthScale, oldSize.y * HeightScale);
            // 额外整体放大，避免在镜头里被压成看不见的小点。
            boxRect.localScale = new Vector3(BoxScale, BoxScale, 1f);
        }

        Text en = FindText(box, "en");
        Text zh = FindText(box, "zh");
        float maxTextWidth = 0f;
        if (en != null)
        {
            en.fontSize = Mathf.Max(en.fontSize, Mathf.RoundToInt(en.fontSize * EnFontScale));
            en.alignment = TextAnchor.MiddleCenter;
            en.horizontalOverflow = HorizontalWrapMode.Overflow;
            en.verticalOverflow = VerticalWrapMode.Overflow;
            StripNewlines(en);
            maxTextWidth = Mathf.Max(maxTextWidth, en.preferredWidth);
        }
        if (zh != null)
        {
            zh.fontSize = Mathf.Max(zh.fontSize, Mathf.RoundToInt(zh.fontSize * ZhFontScale));
            zh.alignment = TextAnchor.MiddleCenter;
            zh.horizontalOverflow = HorizontalWrapMode.Overflow;
            zh.verticalOverflow = VerticalWrapMode.Overflow;
            StripNewlines(zh);
            maxTextWidth = Mathf.Max(maxTextWidth, zh.preferredWidth);
        }

        // 根据实际文字宽度再撑开一点框，避免溢出或被强制换行。
        if (boxRect != null && maxTextWidth > 0f)
        {
            float wantWidth = maxTextWidth * WidthScale + TextPadding;
            if (boxRect.sizeDelta.x < wantWidth)
                boxRect.sizeDelta = new Vector2(wantWidth, boxRect.sizeDelta.y);
        }

        // 删掉/隐藏中文释义底下的红线、下划线、分隔线等装饰。
        RemoveDecorativeLines(box);

        vocWin.AddComponent<Applied>();
        Debug.Log(string.Format(
            "[单词框] 尺寸 {0}x{1} -> {2}x{3} scale={4} 英文字号={5} 中文字号={6} 最大文本宽={7:F0}",
            oldSize.x, oldSize.y,
            boxRect != null ? boxRect.sizeDelta.x : 0f,
            boxRect != null ? boxRect.sizeDelta.y : 0f,
            BoxScale,
            en != null ? en.fontSize : 0, zh != null ? zh.fontSize : 0,
            maxTextWidth));
    }

    private static Text FindText(Transform parent, string childName)
    {
        Transform t = parent.Find(childName);
        if (t != null)
        {
            Text direct = t.GetComponent<Text>();
            if (direct != null) return direct;
        }
        // 名字不完全一致时，退化为按顺序取：0=英文 1=中文
        Text[] all = parent.GetComponentsInChildren<Text>(true);
        if (childName == "en" && all.Length > 0) return all[0];
        if (childName == "zh" && all.Length > 1) return all[1];
        return null;
    }

    /// <summary>去掉文本里的显式换行符，让单词和释义都不折行。</summary>
    private static void StripNewlines(Text text)
    {
        if (text == null) return;
        text.text = text.text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
    }

    /// <summary>隐藏/删除红线、下划线、分隔线等装饰性 UI。</summary>
    private static void RemoveDecorativeLines(Transform box)
    {
        if (box == null) return;
        string[] lineKeywords = new[] { "line", "split", "divider", "underline", "redline", "红线", "分隔", "下划线", "横线", "线" };
        foreach (Transform child in box.GetComponentsInChildren<Transform>(true))
        {
            if (child == box) continue;
            string lower = child.gameObject.name.ToLowerInvariant();
            bool matched = false;
            foreach (string kw in lineKeywords)
            {
                if (lower.Contains(kw))
                {
                    matched = true;
                    break;
                }
            }

            // 即使名字不匹配，也按外观判断：红色/橙色的细长 Image 就当作装饰线隐藏。
            if (!matched)
            {
                var img = child.GetComponent<Image>();
                var rect = child as RectTransform;
                if (img != null && rect != null)
                {
                    Color c = img.color;
                    bool reddish = c.r > 0.55f && c.g < 0.45f && c.b < 0.45f;
                    bool thinHorizontal = rect.sizeDelta.y < 15f && rect.sizeDelta.x > rect.sizeDelta.y * 3f;
                    matched = reddish && thinHorizontal;
                }
            }

            if (matched)
            {
                child.gameObject.SetActive(false);
                Debug.Log("[单词框] 已隐藏装饰线：" + child.gameObject.name);
            }
        }
    }
}
