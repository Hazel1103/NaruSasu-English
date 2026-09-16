using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 通过"请求文件"驱动编辑器做一次完整的小怪皮肤验收：
///   1) 若正在 Play 模式，先退出；
///   2) 刷新资源数据库（让新建的 png/meta 被导入成 Sprite）；
///   3) 执行角色模式自检，把结果写进日志。
///
/// 触发方式：在 <工程>/Temp/ 下放一个空文件 enemySkinCheck.request
/// 也可以直接用菜单：Tools/小怪皮肤验收
/// </summary>
[InitializeOnLoad]
public static class EnemySkinCheckRunner
{
    private const string RequestFileName = "enemySkinCheck.request";
    private const int WaitFramesAfterImport = 90;

    private static int tick;
    private static int phase;         // 0=等条件 1=等导入完成 2=跑自检 3=结束
    private static int waitCounter;
    private static bool finished;

    static EnemySkinCheckRunner()
    {
        EditorApplication.update += Poll;
    }

    private static string RequestPath
    {
        get
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(Path.Combine(root, "Temp"), RequestFileName);
        }
    }

    private static void Poll()
    {
        // 自检跑完、请求文件也已被删除之后复位到初始状态。
        // 这样外部脚本可以反复触发验收 —— 否则每次都得改个脚本逼 Unity 重新编译
        // （只有域名重载才会把静态状态清掉），非常慢。
        if (finished && !File.Exists(RequestPath))
        {
            finished = false;
            phase = 0;
            waitCounter = 0;
        }
        if (finished) return;

        tick++;
        if (tick % 20 != 0) return;

        if (!File.Exists(RequestPath)) return;

        switch (phase)
        {
            case 0:
                // 先退出 Play 模式，否则没法重新导入资源
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                    Debug.Log("[皮肤验收] 正在退出 Play 模式…");
                    return;
                }
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    return;

                Debug.Log("[皮肤验收] 开始刷新资源数据库（导入小怪贴图）…");
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                phase = 1;
                waitCounter = 0;
                break;

            case 1:
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    waitCounter = 0;
                    return;
                }
                waitCounter++;
                if (waitCounter < 3) return;   // 等几轮确认真的稳定了

                Debug.Log("[皮肤验收] 资源导入完成，开始自检。");
                phase = 2;
                break;

            case 2:
                try
                {
                    CharacterModeSelfTest.Run();
                }
                catch (Exception e)
                {
                    Debug.LogError("[皮肤验收] 自检异常：" + e);
                }

                try { File.Delete(RequestPath); }
                catch (IOException) { }

                Debug.Log("[皮肤验收] ===== 完成 =====");
                Debug.Log("[皮肤验收] 请按 Build/▶ 运行游戏 (MainScene) 查看小怪新形象。");
                phase = 3;
                finished = true;   // 等请求文件被外部删掉后自动复位，可再次触发
                break;
        }
    }

    [MenuItem("Tools/小怪皮肤验收", false, 20)]
    public static void RunMenu()
    {
        try
        {
            File.WriteAllText(RequestPath, "run");
            Debug.Log("[皮肤验收] 已写入请求文件，稍后自动执行：" + RequestPath);
        }
        catch (Exception e)
        {
            Debug.LogError("[皮肤验收] 无法写入请求文件：" + e.Message);
        }
    }
}
