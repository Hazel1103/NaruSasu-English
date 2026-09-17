using UnityEditor;
using System;
using System.Reflection;

/// <summary>
/// 规避 Unity 2022.3.1f1c1 在 batchmode 下“初始资源刷新”卡死的问题。
/// 该版本的 Asset Import Worker 子进程在此环境无法正常工作，导致主线程永久等待。
/// 在 InitializeOnLoad 阶段（早于 AssetDatabase Initial Refresh）将 worker 数设为 0，
/// 改为主进程内联导入，即可绕开卡死。仅在 batchmode 下生效，不影响 GUI 编辑器。
/// </summary>
[InitializeOnLoad]
public static class DisableImportWorker
{
    static DisableImportWorker()
    {
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            bool batchmode = false;
            foreach (string a in args)
            {
                if (a == "-batchmode") { batchmode = true; break; }
            }
            if (batchmode)
            {
                MethodInfo setWorkerCount = typeof(AssetDatabase).GetMethod(
                    "SetDesiredWorkerCount",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (setWorkerCount != null)
                {
                    setWorkerCount.Invoke(null, new object[] { 0 });
                    UnityEngine.Debug.Log("[DisableImportWorker] batchmode: 已将资源导入 worker 数设为 0（内联导入，规避 2022.3.1 导入卡死）");
                }
                else
                {
                    UnityEngine.Debug.Log("[DisableImportWorker] 当前 Unity 版本不提供 worker 数设置 API，已跳过。");
                }
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[DisableImportWorker] 设置失败: " + e.Message);
        }
    }
}
