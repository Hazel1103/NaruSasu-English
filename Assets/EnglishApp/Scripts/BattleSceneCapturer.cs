using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 战斗画面抓拍器：运行时按固定间隔把 Game 视图存成 PNG，
/// 用于验证小怪换皮效果（编辑器 Game 面板太小时，外部截图会裁掉小怪）。
///
/// 只做"抓拍"，不参与任何游戏逻辑。
/// </summary>
public sealed class BattleSceneCapturer : MonoBehaviour
{
    /// <summary>抓拍间隔（秒）。</summary>
    public float interval = 2.5f;
    /// <summary>最多抓几张。</summary>
    public int maxShots = 12;
    /// <summary>超采样倍数：4 = 分辨率放大 4 倍，小怪细节才看得清。</summary>
    public int superSize = 4;
    /// <summary>输出目录；为空时用 <工程>/Build/captures。</summary>
    public string outputDir;

    private int shotIndex;
    private static BattleSceneCapturer instance;

    /// <summary>由编辑器脚本或调试代码调用，开始抓拍。</summary>
    public static BattleSceneCapturer Begin(int shots, float every, string dir, int superSize = 4)
    {
        if (instance != null) return instance;
        GameObject host = new GameObject("BattleSceneCapturer");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<BattleSceneCapturer>();
        instance.maxShots = shots;
        instance.interval = every;
        instance.outputDir = dir;
        instance.superSize = superSize;
        instance.StartCoroutine(instance.CaptureLoop());
        Debug.Log("[抓拍] 开始，间隔 " + every + "s，共 " + shots + " 张，输出到 " + instance.ResolveDir());
        return instance;
    }

    private string ResolveDir()
    {
        if (!string.IsNullOrEmpty(outputDir)) return outputDir;
        string root = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(Path.Combine(root, "Build"), "captures");
    }

    /// <summary>
    /// 自动看护：常驻一个轻量检查器，一旦检测到进入战斗场景（且有 capture.request）
    /// 就开始抓拍。比依赖 sceneLoaded 事件更可靠 —— 编辑器直接打开场景时
    /// 事件注册可能晚于场景加载。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (guard != null) return;
        GameObject host = new GameObject("BattleSceneCaptureGuard");
        DontDestroyOnLoad(host);
        guard = host.AddComponent<CaptureGuard>();
    }

    private static CaptureGuard guard;

    /// <summary>后台看护组件，负责发现战斗场景并启动抓拍。</summary>
    private sealed class CaptureGuard : MonoBehaviour
    {
        private float nextCheck;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;

            if (instance != null) return;

            UnityEngine.SceneManagement.Scene scene =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "TD_Scene") return;

            string root = Directory.GetParent(Application.dataPath).FullName;
            string requestPath = Path.Combine(Path.Combine(root, "Temp"), "capture.request");
            if (!File.Exists(requestPath)) return;

            // 请求文件支持 "子目录名" 或 "子目录名,张数,间隔秒"，
            // 这样调参数不用重新编译脚本（Unity 每次改脚本都要重编译，很慢）。
            string tag = "";
            int shots = 30;
            float every = 1.0f;
            try
            {
                string raw = File.ReadAllText(requestPath).Trim();
                string[] parts = raw.Split(',');
                tag = parts[0].Trim();
                if (parts.Length > 1) int.TryParse(parts[1].Trim(), out shots);
                if (parts.Length > 2) float.TryParse(parts[2].Trim(), out every);
            }
            catch (Exception) { }
            if (tag == "1" || tag == "go") tag = "";
            if (shots < 1) shots = 30;
            if (every < 0.2f) every = 1.0f;

            string dir = null;
            if (!string.IsNullOrEmpty(tag))
            {
                string baseDir = Path.Combine(Path.Combine(root, "Build"), "captures");
                dir = Path.Combine(baseDir, tag);
            }

            // 小怪走到塔前会停下攻击，画面就静止了；所以抓密一点，趁它们还在移动时多拍几张。
            // 一波只有一两只，想凑齐 3 个随机变体得多拍一会儿。
            Begin(shots, every, dir);
        }
    }

    private IEnumerator CaptureLoop()
    {
        string dir = ResolveDir();
        try { Directory.CreateDirectory(dir); } catch (Exception) { }

        // 等第一波小怪生成出来。
        // 必须用 Realtime：游戏暂停/结算时 Time.timeScale 会归零，
        // 用 WaitForSeconds 的话协程会永远卡住，一张都拍不到。
        Debug.Log("[抓拍] timeScale=" + Time.timeScale);
        yield return new WaitForSecondsRealtime(1.5f);

        while (shotIndex < maxShots)
        {
            // 等这一帧渲染结束再抓，否则可能拿到黑图
            yield return new WaitForEndOfFrame();

            string file = Path.Combine(dir, string.Format("battle_{0:D2}.png", shotIndex));
            try
            {
                // superSize 超采样：小怪在 1:1 下只有约 30px，放大 4 倍才看得清
                ScreenCapture.CaptureScreenshot(file, superSize);
                Debug.Log("[抓拍] 已保存 " + file + "  (" + Screen.width + "x" + Screen.height
                          + " superSize=" + superSize + " 输出约 "
                          + (Screen.width * superSize) + "x" + (Screen.height * superSize) + ")");
                ReportEnemies(shotIndex);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[抓拍] 保存失败：" + e.Message);
            }

            shotIndex++;
            yield return new WaitForSecondsRealtime(interval);
        }

        // 抓完就把请求文件删掉，否则看护组件会立刻再开一轮，把前面的好帧覆盖掉。
        try
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string req = Path.Combine(Path.Combine(root, "Temp"), "capture.request");
            if (File.Exists(req)) File.Delete(req);
        }
        catch (Exception) { }

        Debug.Log("[抓拍] 完成，共 " + shotIndex + " 张。");
        instance = null;
        Destroy(gameObject);
    }

    /// <summary>
    /// 上报当前所有小怪在「超采样后大图」里的像素坐标，
    /// 便于外部脚本精确裁剪出小怪区域做展示。
    /// </summary>
    private void ReportEnemies(int shot)
    {
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        if (enemies == null || enemies.Length == 0)
        {
            Debug.Log("[抓拍] 第" + shot + "张：当前没有存活小怪");
            return;
        }
        Camera cam = Camera.main;
        for (int i = 0; i < enemies.Length && i < 6; i++)
        {
            Enemy e = enemies[i];
            if (e == null) continue;
            Vector3 vp = cam != null
                ? cam.WorldToViewportPoint(e.transform.position)
                : Vector3.zero;
            // 视口坐标(左下原点) -> 大图像素坐标(左上原点)
            int px = Mathf.RoundToInt(vp.x * Screen.width * superSize);
            int py = Mathf.RoundToInt((1f - vp.y) * Screen.height * superSize);
            var skin = e.GetComponent<EnemySkinController>();
            string who = skin != null ? (skin.SkinLabel + " #" + skin.Variant) : "原始";
            Debug.Log(string.Format(
                "[抓拍] 第{0}张 小怪{1} {2} 皮肤={3} 大图坐标=({4},{5}) 在画面内={6}",
                shot, i, e.name, who, px, py,
                vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f));
        }
    }
}
