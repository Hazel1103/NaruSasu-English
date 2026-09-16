using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 修改首页「关于 / 游戏说明」弹窗里的作者信息文案与超链接。
///
/// 弹窗结构（Canvas/Window(About)/Window/Scroll View/Viewport/Content）：
///   UserDefine  运行时填「自定义词库存放目录」
///   Author      "作者:奈何"                     [Button] ButtonEvents.OpenUrl
///   Feedback    "意见反馈"
///   MyBolg      "Blog: blog.csdn.net/final5788" [Button] ButtonEvents.OpenUrl
///   MyGithub    "Github: github.com/sunsvip"    [Button] ButtonEvents.OpenUrl
///   MyEmail     "Email: 772065979@qq.com"
///
/// 触发：Temp/aboutEdit.request 或菜单 Tools/修改游戏说明文案
/// 结果写到 Build/about-edit.txt
/// </summary>
[InitializeOnLoad]
public static class AboutDialogEditor
{
    const string RequestName = "aboutEdit.request";
    const string OutRel = "Build/about-edit.txt";
    const string ScenePath = "Assets/EnglishApp/Scenes/MainScene.unity";

    static int tick;

    static AboutDialogEditor()
    {
        EditorApplication.update += Poll;
    }

    static string Root { get { return Directory.GetParent(Application.dataPath).FullName; } }
    static string RequestPath { get { return Path.Combine(Path.Combine(Root, "Temp"), RequestName); } }

    /// <summary>对象名 -> 新文案</summary>
    static readonly Dictionary<string, string> NewText = new Dictionary<string, string>
    {
        { "Author",   "作者：胡安" },
        { "MyBolg",   "原作者：奈何" },
        { "MyGithub", "Github: github.com/Hazel1103" },
        { "MyEmail",  "Email：13571993443@139.com" },
    };

    /// <summary>对象名 -> 超链接（点一下用系统浏览器打开）</summary>
    static readonly Dictionary<string, string> NewUrl = new Dictionary<string, string>
    {
        { "Author",   "https://github.com/Hazel1103" },
        { "MyBolg",   "https://blog.csdn.net/final5788" },
        { "MyGithub", "https://github.com/Hazel1103" },
    };

    static void Poll()
    {
        tick++;
        if (tick % 20 != 0) return;
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try { File.Delete(RequestPath); } catch (IOException) { }
        try { Run(); }
        catch (Exception e)
        {
            Write("异常: " + e);
            Debug.LogError("[说明文案] " + e);
        }
    }

    [MenuItem("Tools/修改游戏说明文案", false, 61)]
    public static void RunMenu() { Run(); }

    public static void Run()
    {
        var sb = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            sb.AppendLine("已打开 MainScene");
        }
        sb.AppendLine("场景: " + scene.name);

        DataLoader loader = null;
        foreach (var go in scene.GetRootGameObjects())
        {
            var dl = go.GetComponentInChildren<DataLoader>(true);
            if (dl != null) { loader = dl; break; }
        }
        if (loader == null || loader.aboutDialog == null)
        {
            sb.AppendLine("找不到 aboutDialog，中止");
            Write(sb.ToString());
            return;
        }

        Transform dialog = loader.aboutDialog.transform;
        sb.AppendLine("aboutDialog = " + dialog.name);
        sb.AppendLine();

        foreach (var kv in NewText)
        {
            Transform t = FindDeep(dialog, kv.Key);
            if (t == null) { sb.AppendLine("[缺失] " + kv.Key); continue; }
            Text txt = t.GetComponent<Text>();
            if (txt == null) { sb.AppendLine("[无 Text] " + kv.Key); continue; }

            string before = txt.text;
            if (before == kv.Value)
            {
                sb.AppendLine("[已是目标值] " + kv.Key + " = \"" + kv.Value + "\"");
            }
            else
            {
                Undo.RecordObject(txt, "修改游戏说明文案");
                txt.text = kv.Value;
                EditorUtility.SetDirty(txt);
                sb.AppendLine("[改为] " + kv.Key + " : \"" + before + "\"  ->  \"" + kv.Value + "\"");
            }

            // 超链接
            string url;
            if (NewUrl.TryGetValue(kv.Key, out url))
            {
                Button btn = t.GetComponent<Button>();
                if (btn == null)
                {
                    sb.AppendLine("    (该行没有 Button，跳过链接)");
                }
                else
                {
                    string oldMode, oldUrl;
                    ReadCall(btn, out oldMode, out oldUrl);
                    SetCall(btn, url);
                    string m2, u2;
                    ReadCall(btn, out m2, out u2);
                    sb.AppendLine("    链接: 原=" + oldMode + " \"" + oldUrl + "\"  ->  新=" + m2 + " \"" + u2 + "\"");
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine();
        sb.AppendLine("场景保存 = " + saved);
        Write(sb.ToString());
        Debug.Log("[说明文案] 已保存场景，详情见 " + OutRel);
    }

    static void ReadCall(Button btn, out string mode, out string url)
    {
        mode = "?";
        url = "";
        var so = new SerializedObject(btn);
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        if (calls == null || calls.arraySize == 0) return;
        var el = calls.GetArrayElementAtIndex(0);
        var m = el.FindPropertyRelative("m_Mode");
        var args = el.FindPropertyRelative("m_Arguments");
        mode = (m == null ? "?" : m.enumValueIndex.ToString());
        var s = args == null ? null : args.FindPropertyRelative("m_StringArgument");
        url = s == null ? "" : s.stringValue;
    }

    static void SetCall(Button btn, string url)
    {
        var so = new SerializedObject(btn);
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        if (calls == null || calls.arraySize == 0) return;
        var el = calls.GetArrayElementAtIndex(0);
        var m = el.FindPropertyRelative("m_Mode");
        if (m != null) m.enumValueIndex = 5;      // PersistentListenerMode.String
        var args = el.FindPropertyRelative("m_Arguments");
        if (args != null)
        {
            var s = args.FindPropertyRelative("m_StringArgument");
            if (s != null) s.stringValue = url;
            var tn = args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName");
            if (tn != null) tn.stringValue = "System.String";
        }
        so.ApplyModifiedProperties();
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    static void Write(string s)
    {
        File.WriteAllText(Path.Combine(Root, OutRel), s, Encoding.UTF8);
    }
}
