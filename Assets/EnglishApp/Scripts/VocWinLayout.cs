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
    /// <summary>单词框高度倍率（原 97 → 约 194）。</summary>
    public const float HeightScale = 2.0f;
    /// <summary>单词框宽度倍率（原 241 → 约 301），长单词不再挤成一团。</summary>
    public const float WidthScale = 1.25f;
    /// <summary>英文字号倍率（原 14）。</summary>
    public const float EnFontScale = 2.4f;
    /// <summary>中文字号倍率（原 14）。</summary>
    public const float ZhFontScale = 2.0f;

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
        }

        Text en = FindText(box, "en");
        Text zh = FindText(box, "zh");
        if (en != null)
        {
            en.fontSize = Mathf.Max(en.fontSize, Mathf.RoundToInt(en.fontSize * EnFontScale));
            en.alignment = TextAnchor.MiddleCenter;
            en.horizontalOverflow = HorizontalWrapMode.Wrap;
            en.verticalOverflow = VerticalWrapMode.Overflow;
        }
        if (zh != null)
        {
            zh.fontSize = Mathf.Max(zh.fontSize, Mathf.RoundToInt(zh.fontSize * ZhFontScale));
            zh.alignment = TextAnchor.UpperCenter;
            zh.horizontalOverflow = HorizontalWrapMode.Wrap;
            zh.verticalOverflow = VerticalWrapMode.Overflow;
        }

        vocWin.AddComponent<Applied>();
        Debug.Log(string.Format(
            "[单词框] 尺寸 {0}x{1} -> {2}x{3}  英文字号={4} 中文字号={5}",
            oldSize.x, oldSize.y,
            boxRect != null ? boxRect.sizeDelta.x : 0f,
            boxRect != null ? boxRect.sizeDelta.y : 0f,
            en != null ? en.fontSize : 0, zh != null ? zh.fontSize : 0));
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
}
