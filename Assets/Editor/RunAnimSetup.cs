using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 一键配置 6 个角色（佐助1-3、鸣人1-3）的敌人 Animator Controller：
///   · move  = 3 帧跑步循环（@12FPS，Loop Time 开）
///   · attack / damage / death = 第1帧静止帧（无攻击/受击/死亡素材时的替代，击倒后原地不动）
/// 状态机用 bool 参数 b_move / b_attack / b_damage / b_death，匹配 Enemy.cs 的逻辑。
/// 输出到 Assets/Resources/EnemyControllers/，供 EnemySkinController 运行时按模式切换；
/// 帧精灵同时复制到 Assets/Resources/EnemyRunFrames/ 供缩放基准使用。
/// 菜单：Tools > 跑酷动画 > 一键配置跑步动画
/// </summary>
public static class RunAnimSetup
{
    private const string FramesRoot = "Assets/Sprites/RunFrames";
    private const string RuntimeFrames = "Assets/Resources/EnemyRunFrames";
    private const string OutRoot = "Assets/Resources/EnemyControllers";
    private const float Fps = 12f;
    /// <summary>受击/攻击/死亡静止帧持续多久（秒）。用户期望：答对后小怪停顿 1 秒再消失。</summary>
    private const float StillDuration = 1f;

    [MenuItem("Tools/跑酷动画/一键配置跑步动画")]
    public static void SetupAll()
    {
        ConfigureTextureImports(FramesRoot);
        CopyFramesToResources();
        AssetDatabase.Refresh();
        ConfigureTextureImports(RuntimeFrames);

        EnsureDir(OutRoot);
        foreach (var charDir in Directory.GetDirectories(FramesRoot))
        {
            string charName = Path.GetFileName(charDir);
            EnsureDir($"{OutRoot}/{charName}");
            BuildCharacter(charName);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[RunAnimSetup] 完成。敌人 Animator Controller 已输出到 {OutRoot}，让 EnemySkinController 运行时按玩家模式切换即可。");
    }

    // ---------- 1. 导入设置 ----------
    private static void ConfigureTextureImports(params string[] roots)
    {
        foreach (var root in roots)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (importer.spritePixelsPerUnit != 100f) { importer.spritePixelsPerUnit = 100f; dirty = true; }
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
                if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; dirty = true; }

                if (dirty) importer.SaveAndReimport();
            }
        }
    }

    // ---------- 2. 复制帧到 Resources（运行时加载用） ----------
    private static void CopyFramesToResources()
    {
        EnsureDir(RuntimeFrames);
        foreach (var charDir in Directory.GetDirectories(FramesRoot))
        {
            string charName = Path.GetFileName(charDir);
            string destDir = $"{RuntimeFrames}/{charName}";
            EnsureDir(destDir);
            foreach (var f in Directory.GetFiles(charDir, "*.png"))
            {
                string dest = $"{destDir}/{Path.GetFileName(f)}";
                if (!File.Exists(dest) || File.GetLastWriteTime(dest) < File.GetLastWriteTime(f))
                    File.Copy(f, dest, true);
            }
        }
    }

    // ---------- 3. 生成每个角色的动画片段 + Controller ----------
    private static void BuildCharacter(string charName)
    {
        var sprites = LoadOrderedFrames(charName);
        if (sprites.Count == 0 || sprites.Any(s => s == null))
        {
            Debug.LogError($"[RunAnimSetup] 角色 {charName} 未加载到帧精灵，请确认 {FramesRoot}/{charName} 下存在 *_0/_1/_2.png 且 Texture Type 已设为 Sprite。");
            return;
        }

        var move = new AnimationClip { name = "move", frameRate = Fps };
        SetSpriteCurve(move, sprites, Fps, loop: true);
        SaveClip(move, charName, "move");

        Sprite still = sprites[0];
        var attack = MakeStill("attack", still, 0.5f);
        SaveClip(attack, charName, "attack");
        var damage = MakeStill("damage", still, 0.2f);
        SaveClip(damage, charName, "damage");
        var death = MakeStill("death", still, 1f);
        SaveClip(death, charName, "death");

        BuildController(charName, move, attack, damage, death);
    }

    private static void SetSpriteCurve(AnimationClip clip, List<Sprite> sprites, float fps, bool loop)
    {
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    private static AnimationClip MakeStill(string name, Sprite still, float duration = -1f)
    {
        if (duration < 0f) duration = StillDuration;
        var clip = new AnimationClip { name = name, frameRate = Fps };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[2];
        keys[0] = new ObjectReferenceKeyframe { time = 0f, value = still };
        keys[1] = new ObjectReferenceKeyframe { time = duration, value = still };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static void SaveClip(AnimationClip clip, string charName, string stateName)
    {
        string path = $"{OutRoot}/{charName}/{charName}_{stateName}.anim";

        // 直接在片段资源里挂结束事件，运行时不需要再动态添加。
        switch (stateName)
        {
            case "damage":
                AddEndEvent(clip, "DamageAnimEndCallBack");
                break;
            case "attack":
                AddEndEvent(clip, "AttackAnimEndCallBack");
                break;
        }

        AssetDatabase.CreateAsset(clip, path);
        // CreateAsset 会把 clip.name 设为文件名，这里还原成状态名，供 Enemy.cs 的事件匹配
        clip.name = stateName;
        EditorUtility.SetDirty(clip);
    }

    private static void AddEndEvent(AnimationClip clip, string functionName)
    {
        var evt = new AnimationEvent
        {
            time = clip.length,
            functionName = functionName
        };
        // 用 AnimationUtility.SetAnimationEvents 更可靠，避免 AddEvent 后事件未持久化。
        AnimationUtility.SetAnimationEvents(clip, new[] { evt });
    }

    private static void BuildController(string charName, AnimationClip move, AnimationClip attack, AnimationClip damage, AnimationClip death)
    {
        string path = $"{OutRoot}/{charName}.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            AssetDatabase.DeleteAsset(path);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("b_move", AnimatorControllerParameterType.Bool);
        controller.AddParameter("b_attack", AnimatorControllerParameterType.Bool);
        controller.AddParameter("b_damage", AnimatorControllerParameterType.Bool);
        controller.AddParameter("b_death", AnimatorControllerParameterType.Bool);

        var sm = controller.layers[0].stateMachine;
        var sMove = sm.AddState("move"); sMove.motion = move; sm.defaultState = sMove;
        var sAttack = sm.AddState("attack"); sAttack.motion = attack;
        var sDamage = sm.AddState("damage"); sDamage.motion = damage;
        var sDeath = sm.AddState("death"); sDeath.motion = death;

        // 关闭 Write Defaults：避免 Animator 把未动画的属性（如 SpriteRenderer.flipX）
        // 每帧重置回 prefab 默认值，导致换皮后的朝向/缩放被覆盖。
        sMove.writeDefaultValues = false;
        sAttack.writeDefaultValues = false;
        sDamage.writeDefaultValues = false;
        sDeath.writeDefaultValues = false;

        Add(sMove, sAttack, "b_attack");
        Add(sMove, sDamage, "b_damage");
        Add(sAttack, sDamage, "b_damage");
        Add(sAttack, sMove, "b_attack", false);
        Add(sDamage, sMove, "b_damage", false);
        Add(sMove, sDeath, "b_death");
        Add(sAttack, sDeath, "b_death");
        Add(sDamage, sDeath, "b_death");

        AssetDatabase.SaveAssets();
        Debug.Log($"[RunAnimSetup] 生成 {path}");
    }

    private static void Add(AnimatorState from, AnimatorState to, string param, bool ifTrue = true)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.AddCondition(ifTrue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, param);
    }

    // ---------- 工具 ----------
    private static List<Sprite> LoadOrderedFrames(string charName)
    {
        string dir = $"{FramesRoot}/{charName}";
        return Directory.GetFiles(dir, "*.png")
            .Select(Path.GetFileName)
            .OrderBy(n => GetFrameIndex(n))
            .Select(n => AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{n}"))
            .ToList();
    }

    private static int GetFrameIndex(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        int underscore = name.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(name.Substring(underscore + 1), out int idx) ? idx : 0;
    }

    private static void EnsureDir(string path)
    {
        path = path.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
