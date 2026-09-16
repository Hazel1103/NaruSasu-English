using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 清理被误存进场景文件的运行时 UI 对象。
///
/// 背景：模式选择面板是 Play 模式下用代码创建的（CharacterModeCanvas / CharacterModeRuntimeUI）。
/// 一旦在编辑期保存了场景，这套对象就会被写进 .unity 文件。之后每次进 Play 都会再建一套，
/// 于是屏幕上出现两个模式选择框。
/// 这个工具在 Edit 模式下打开场景、删掉残留、存盘。
/// </summary>
public static class SceneResidueCleaner
{
    private const string Menu = "Tools/清理场景残留模式UI";

    /// <summary>
    /// 收集场景里真正属于「残留」的对象。
    /// 注意：模式选择框现在是有意放进场景的常驻 UI（Tools/把模式选择框放进首页场景），
    /// 它的引用会被序列化恢复，所以 IsIntact() 为真 —— 这种要留着。
    /// 只有引用全丢的空壳才是历史遗留的垃圾。
    /// </summary>
    private static List<GameObject> CollectResidue(UnityEngine.SceneManagement.Scene scene)
    {
        List<GameObject> hits = new List<GameObject>();

        foreach (CharacterModeRuntimeUI ui in Object.FindObjectsOfType<CharacterModeRuntimeUI>(true))
        {
            if (ui != null && !ui.IsIntact())
                hits.Add(ui.gameObject);
        }

        // 画布：只有当它下面没有任何完整面板时，才算残留。
        foreach (CharacterModeCanvas c in Object.FindObjectsOfType<CharacterModeCanvas>(true))
        {
            if (c == null) continue;
            bool hasIntactPanel = false;
            foreach (CharacterModeRuntimeUI ui in c.GetComponentsInChildren<CharacterModeRuntimeUI>(true))
            {
                if (ui != null && ui.IsIntact()) { hasIntactPanel = true; break; }
            }
            if (!hasIntactPanel) hits.Add(c.gameObject);
        }

        return hits;
    }

    [MenuItem(Menu)]
    public static void Clean()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[清理] 请先退出 Play 模式再清理。");
            return;
        }

        List<string> scenes = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/")) scenes.Add(path);
        }

        int total = 0;
        foreach (string path in scenes)
        {
            int n = CleanScene(path);
            if (n > 0)
            {
                total += n;
                Debug.Log("[清理] " + path + " -> 删除 " + n + " 个残留对象并保存");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log(total == 0
            ? "[清理] 所有场景都是干净的，没有残留。"
            : "[清理] 共删除 " + total + " 个残留对象。");
    }

    private static int CleanScene(string path)
    {
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        if (!scene.IsValid()) return 0;

        List<GameObject> hits = CollectResidue(scene);
        if (hits.Count == 0) return 0;

        // 只删最外层：祖先也在列表里的跳过，避免重复删 / 删到已被销毁的对象。
        int removed = 0;
        foreach (GameObject go in hits)
        {
            if (go == null) continue;
            bool hasAncestorInList = false;
            Transform p = go.transform.parent;
            while (p != null)
            {
                if (hits.Contains(p.gameObject)) { hasAncestorInList = true; break; }
                p = p.parent;
            }
            if (hasAncestorInList) continue;

            Object.DestroyImmediate(go);
            removed++;
        }

        if (removed > 0)
            EditorSceneManager.SaveScene(scene);
        return removed;
    }

    /// <summary>
    /// 供其它 Editor 流程调用：在保存场景之前先扫一遍残留，避免把运行时对象写进场景文件。
    /// </summary>
    public static int CleanActiveScene()
    {
        if (EditorApplication.isPlaying) return 0;
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return 0;

        List<GameObject> hits = CollectResidue(scene);
        int removed = 0;
        foreach (GameObject go in hits)
        {
            if (go == null) continue;
            bool skip = false;
            Transform p = go.transform.parent;
            while (p != null)
            {
                if (hits.Contains(p.gameObject)) { skip = true; break; }
                p = p.parent;
            }
            if (skip) continue;
            Object.DestroyImmediate(go);
            removed++;
        }
        return removed;
    }
}
