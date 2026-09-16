using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 角色模式切换诊断（全自动）。
/// 触发：菜单 Tools/诊断模式切换，或在 Temp 下放一个 modeDiag.request 文件。
/// 流程：打开首页 -> 进 Play -> 打印面板/按钮状态 -> 真实模拟点击「佐助」按钮 -> 再打印一次 -> 退出 Play。
/// </summary>
[InitializeOnLoad]
public static class CharacterModeDiag
{
    private const string RequestFile = "modeDiag.request";
    private const string KeyStep = "CharacterModeDiag.Step";
    private const string KeyTime = "CharacterModeDiag.Time";
    private const float Tick = 0.25f;

    private static int tick;
    private static string beforeText = "";

    static CharacterModeDiag()
    {
        EditorApplication.update += Poll;
    }

    private static string RequestPath
    {
        get { return Path.Combine(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp"), RequestFile); }
    }

    private static void Log(string msg) { Debug.Log("[模式诊断] " + msg); }

    private static void Next(int step, float seconds)
    {
        SessionState.SetInt(KeyStep, step);
        SessionState.SetFloat(KeyTime, (float)EditorApplication.timeSinceStartup + seconds);
    }

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        int step = SessionState.GetInt(KeyStep, -1);

        if (step < 0)
        {
            tick++;
            if (tick % 20 != 0) return;
            if (!File.Exists(RequestPath)) return;
            try { File.Delete(RequestPath); } catch (IOException) { }
            SessionState.SetInt(KeyStep, 0);
            SessionState.SetFloat(KeyTime, (float)EditorApplication.timeSinceStartup + 0.3f);
            Log("===== 开始 =====");
            return;
        }

        if (EditorApplication.timeSinceStartup < SessionState.GetFloat(KeyTime, 0f)) return;

        try
        {
            Advance(step);
        }
        catch (System.Exception e)
        {
            Log("!! 步骤 " + step + " 异常：" + e);
            Next(step + 1, 0.5f);
        }
    }

    private static void Advance(int step)
    {
        switch (step)
        {
            case 0:
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                    Next(0, 2.0f);
                    return;
                }
                // 先把所有场景里被误存盘的运行时面板清掉：
                // 残留对象带着旧的 1080x1920 画布，会让新建的面板沿用错误缩放（只剩一半大），
                // 而且会让屏幕上同时出现两块模式选择框。
                Log("清理场景残留模式UI");
                try { SceneResidueCleaner.Clean(); }
                catch (System.Exception e) { Log("清理失败：" + e.Message); }

                Log("打开首页场景 MainScene");
                try
                {
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                        EditorSceneManager.SaveOpenScenes();
                }
                catch (System.Exception) { }
                EditorSceneManager.OpenScene("Assets/EnglishApp/Scenes/MainScene.unity");
                Next(1, 1.5f);
                break;

            case 1:
                Log("进入 Play 模式");
                EditorApplication.isPlaying = true;
                Next(2, 6.0f);
                break;

            case 2:
                Log(Collect("点击前"));
                beforeText = CurrentModeText();
                Next(3, 0.5f);
                break;

            case 3:
                Log("===== 实验1：直接调用 Instance.SelectSasuke() =====");
                if (CharacterModeManager.Instance != null)
                    CharacterModeManager.Instance.SelectSasuke();
                Log("   结果 Current = " + Mode() + " | 文字 = " + CurrentModeText());
                Next(4, 1.0f);
                break;

            case 4:
                Log("===== 实验2：模拟点击「佐助」按钮 =====");
                ClickButton("CharacterModePanel/SasukeButton");
                Log("   结果 Current = " + Mode() + " | 文字 = " + CurrentModeText());
                Next(5, 1.0f);
                break;

            case 5:
                Log("===== 实验3：模拟点击「鸣人」按钮 =====");
                ClickButton("CharacterModePanel/NarutoButton");
                Log("   结果 Current = " + Mode() + " | 文字 = " + CurrentModeText());
                Next(6, 1.0f);
                break;

            case 6:
                Log(Collect("点击后"));
                Log(string.Format("结果：文字 {0} -> {1}；Current = {2}；PlayerPrefs = {3}",
                    beforeText, CurrentModeText(), Mode(),
                    PlayerPrefs.GetInt(CharacterModeManager.PreferenceKey, -1)));
                Next(7, 0.5f);
                break;

            case 7:
                EditorApplication.isPlaying = false;
                Next(8, 2.0f);
                break;

            case 8:
                Log("===== 完成 =====");
                SessionState.SetInt(KeyStep, -1);
                break;

            default:
                SessionState.SetInt(KeyStep, -1);
                break;
        }
    }

    [MenuItem("Tools/诊断模式切换")]
    public static void RunMenu()
    {
        try
        {
            File.WriteAllText(RequestPath, "run");
            Log("已提交诊断请求，稍等几秒看 Console");
        }
        catch (System.Exception e)
        {
            Log("写请求失败：" + e.Message);
        }
    }

    [MenuItem("Tools/清理场景残留模式UI")]
    public static void Cleanup()
    {
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
        Debug.Log("[模式诊断] 已清理运行时残留对象：" + removed + "（记得保存场景）");
    }

    private static string Mode()
    {
        return CharacterModeManager.Instance == null ? "(无管理器)" : CharacterModeManager.Instance.Current.ToString();
    }

    // ------------------------------------------------------------------ 模拟点击
    private static void ClickButton(string path)
    {
        CharacterModeRuntimeUI[] uis = Object.FindObjectsOfType<CharacterModeRuntimeUI>(true);
        if (uis.Length == 0)
        {
            Log("!! 场景里没有 CharacterModeRuntimeUI，按钮根本不存在");
            return;
        }
        foreach (CharacterModeRuntimeUI ui in uis)
        {
            Transform t = ui.transform.Find(path);
            if (t == null)
            {
                Log("!! 找不到 " + path + "（面板结构异常）");
                continue;
            }
            Button btn = t.GetComponent<Button>();
            if (btn == null) { Log("!! " + path + " 上没有 Button 组件"); continue; }
            Log("   对 " + t.name + " 派发 pointerClick（该按钮监听数见下）");
            DescribeListeners(btn);
            ExecuteEvents.Execute(btn.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }
    }

    /// <summary>反射读出按钮上绑定的运行时监听（方法名 + 目标对象），用于确认有没有绑反。</summary>
    private static void DescribeListeners(Button btn)
    {
        try
        {
            System.Type baseType = btn.onClick.GetType();
            while (baseType != null && baseType.Name != "UnityEventBase")
                baseType = baseType.BaseType;
            if (baseType == null) { Log("      (无法定位 UnityEventBase)"); return; }

            var f = baseType.GetField("m_RuntimeCalls",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f == null) { Log("      (没有 m_RuntimeCalls 字段)"); return; }
            object callList = f.GetValue(btn.onClick);
            if (callList == null) { Log("      (运行时监听列表为空)"); return; }

            var lf = callList.GetType().GetField("m_RuntimeCalls",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (lf == null) { Log("      (无法读取监听列表)"); return; }
            var calls = lf.GetValue(callList) as System.Collections.IEnumerable;
            if (calls == null) { Log("      (监听列表为空)"); return; }

            int i = 0;
            foreach (object call in calls)
            {
                i++;
                string target = "?", method = "?";
                var tf = call.GetType().GetField("m_Target",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (tf != null)
                {
                    object tgt = tf.GetValue(call);
                    target = tgt == null ? "null" : (tgt is Object ? ((Object)tgt).name : tgt.ToString());
                }
                var mprop = call.GetType().GetProperty("MethodName",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (mprop != null) method = (mprop.GetValue(call) ?? "?").ToString();
                Log("      监听#" + i + " 目标=" + target + " 方法=" + method);
            }
            if (i == 0) Log("      !! 该按钮一个运行时监听都没有！");
        }
        catch (System.Exception e)
        {
            Log("      (读监听失败：" + e.Message + ")");
        }
    }

    private static string CurrentModeText()
    {
        foreach (Text t in Object.FindObjectsOfType<Text>(true))
            if (t.name == "CharacterName") return t.text;
        return "(无)";
    }

    // ------------------------------------------------------------------ 信息收集
    private static string Collect(string label)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("---------- " + label + " ----------");
        sb.AppendLine("场景 = " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

        CharacterModeManager mgr = CharacterModeManager.Instance;
        if (mgr == null)
            sb.AppendLine("!! CharacterModeManager.Instance == null");
        else
            sb.AppendLine(string.Format("管理器={0} 当前={1} homeSceneName='{2}' IsHomeScene={3} createRuntimeUI={4}",
                mgr.name, mgr.Current, mgr.homeSceneName, mgr.IsHomeScene, mgr.createRuntimeUI));

        EventSystem[] systems = Object.FindObjectsOfType<EventSystem>();
        sb.AppendLine("EventSystem 数量=" + systems.Length + (EventSystem.current == null ? " （current 为空！）" : ""));

        CharacterModeRuntimeUI[] uis = Object.FindObjectsOfType<CharacterModeRuntimeUI>(true);
        sb.AppendLine("CharacterModeRuntimeUI 数量=" + uis.Length);
        foreach (CharacterModeRuntimeUI ui in uis)
        {
            Canvas canvas = ui.GetComponentInParent<Canvas>();
            sb.AppendLine(string.Format("  UI:{0} 激活={1} Canvas={2} sortingOrder={3}",
                ui.name, ui.gameObject.activeInHierarchy,
                canvas == null ? "无" : canvas.name,
                canvas == null ? "-" : canvas.sortingOrder.ToString()));

            RectTransform panel = ui.transform.Find("CharacterModePanel") as RectTransform;
            if (panel == null) { sb.AppendLine("     !! 无 CharacterModePanel"); continue; }
            sb.AppendLine(string.Format("     面板 激活={0} 屏幕矩形={1} 颜色={2}",
                panel.gameObject.activeInHierarchy, Fmt(ScreenRect(panel)),
                panel.GetComponent<Image>() == null ? "-" : panel.GetComponent<Image>().color.ToString()));

            DumpButton(sb, ui.transform, "CharacterModePanel/NarutoButton");
            DumpButton(sb, ui.transform, "CharacterModePanel/SasukeButton");

            // 射线：佐助按钮中心命中了谁
            Transform btn = ui.transform.Find("CharacterModePanel/SasukeButton");
            if (btn != null)
            {
                Rect r = ScreenRect(btn as RectTransform);
                sb.AppendLine("     射线 @" + (int)r.center.x + "," + (int)r.center.y + " （屏幕 " + Screen.width + "x" + Screen.height + "）");
                foreach (string hit in RaycastAt(new Vector2(r.center.x, r.center.y)))
                    sb.AppendLine("        -> " + hit);
            }
        }
        return sb.ToString();
    }

    private static void DumpButton(StringBuilder sb, Transform uiRoot, string path)
    {
        Transform t = uiRoot.Find(path);
        if (t == null) { sb.AppendLine("     !! 找不到 " + path); return; }
        Button btn = t.GetComponent<Button>();
        Image img = t.GetComponent<Image>();
        sb.AppendLine(string.Format("     按钮 {0}: 激活={1} interactable={2} 矩形={3} 颜色={4} raycast={5} targetGraphic={6}",
            t.name, t.gameObject.activeInHierarchy,
            btn == null ? "无Button" : btn.interactable.ToString(),
            Fmt(ScreenRect(t as RectTransform)),
            img == null ? "无" : img.color.ToString(),
            img == null ? "?" : img.raycastTarget.ToString(),
            btn == null || btn.targetGraphic == null ? "null" : btn.targetGraphic.name));
    }

    private static string Fmt(Rect r)
    {
        return string.Format("x[{0:F0},{1:F0}] y[{2:F0},{3:F0}] {4:F0}x{5:F0}",
            r.xMin, r.xMax, r.yMin, r.yMax, r.width, r.height);
    }

    /// <summary>把 RectTransform 换算成屏幕像素矩形（左下原点）。</summary>
    private static Rect ScreenRect(RectTransform rect)
    {
        if (rect == null) return default(Rect);
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = corners[i];
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Camera cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
                if (cam != null) p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            }
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }

    /// <summary>在指定屏幕点做 UGUI 射线，返回命中的对象链（最上层在前）。</summary>
    private static List<string> RaycastAt(Vector2 screenPoint)
    {
        List<string> result = new List<string>();
        EventSystem es = EventSystem.current;
        if (es == null) { result.Add("!! EventSystem.current == null"); return result; }
        PointerEventData data = new PointerEventData(es) { position = screenPoint };
        List<RaycastResult> hits = new List<RaycastResult>();
        es.RaycastAll(data, hits);
        if (hits.Count == 0) { result.Add("(什么都没命中 —— 点不到！)"); return result; }
        foreach (RaycastResult h in hits)
        {
            string chain = h.gameObject.name;
            Transform p = h.gameObject.transform.parent;
            int depth = 0;
            while (p != null && depth < 4) { chain = p.name + "/" + chain; p = p.parent; depth++; }
            result.Add(chain + " [order=" + h.sortingOrder + " depth=" + h.depth + "]");
        }
        return result;
    }
}
