# NaruSasu English（鸣佐英语）· 塔防背单词（Naruto &amp; Sasuke Spelling TD）

> 一个「塔防 + 英语单词拼写」的小游戏。在经典单词拼写玩法之上，接入了**鸣人 / 佐助双角色模式**：
> 角色主题配色、答题台词、答对音效、头像，以及**进攻小怪的专属形象**。
>
> A small "tower-defense × English spelling" game, with dual Naruto / Sasuke character modes.

- 引擎：**Unity 2022.3.1f1c1**
- 玩法场景：主菜单 → 选择词库 → 选择关卡 → 塔防战斗（拼写正确即可攻击来袭小怪）
- 支持平台：**Windows (x64)** / **Android**

---

## 一、玩法简介

1. 敌人（小怪）从右侧沿路径向左进攻，头顶显示需要拼写的**英文单词**。
2. 在输入框中把单词拼对，即可攻击/消灭小怪；拼错会提示并扣减机会。
3. 消灭全部波次即通关，血条归零则失败。
4. 词库首次启动时联网自动下载，也可把自己的词库文件放进「自定义词库存放目录」（见游戏内「游戏说明」）。

## 二、双角色模式

在**主菜单左上角**的角色面板里切换「鸣人 / 佐助」，选择会立即生效并持久化（下次启动沿用）。

| | 鸣人模式 | 佐助模式 |
|---|---|---|
| 主题主色 | 橙色 `#EF7019` | 深蓝黑 `#12233A` |
| 背景氛围 | 暖色 `#FFEDD7` | 冷色 `#B8C6D5` |
| 头像 | 鸣人 | 佐助 |
| 答对台词 | 做得不错！继续努力！可不能半途而废！ | ……进步了。 |
| 答错台词 | 没关系！再来一次，多加练习一定能记住！ | 基础不够扎实，重新记。 |
| 答对音效 | 轻快版 | 低沉版 |
| **进攻小怪形象** | **佐助** | **鸣人** |

> 小怪形象按「对手」逻辑配置：进入鸣人模式后，来袭的敌人显示为佐助形象，反之亦然。
> 每种形象有 **3 个跑动相位**，随机出现，并做了**左右镜像**处理以朝向塔的方向。

## 三、相对原版的改造内容

本项目基于原作者的单词拼写游戏，主要新增/改造：

- **双角色模式系统**：全局角色状态 + `PlayerPrefs` 持久化、主题配色、台词、音效、头像。
  - 答题反馈以「角色台词」形式呈现，**不修改**原有的答题、计分、词库与对错判断逻辑。
  - 首页的模式选择面板已做成**场景常驻 UI**，编辑器里即可看到并调整位置。
- **小怪换皮**：使用 `EnemySkinController`（`LateUpdate` + `DefaultExecutionOrder(10000)`）在动画之后覆盖 `SpriteRenderer.sprite`，
  保证 Animator 序列帧不会把皮肤盖回去；按世界高度等比缩放，血条与单词窗口位置保持稳定。
  - 贴图由原始图纸直接转换：**白底透明化（四边 flood-fill）→ 面积平均缩小（alpha 加权，不做颜色量化，完全保留原图配色）→ 3 个跑动相位 → 左右镜像**。
- **单词框放大**：`VocWinLayout` 把单词窗口从 `241×97` 提升到 `301×194`，英文/中文字号 `34 / 28`。
- **弹窗自适应**：`DialogFitter` 让 Game Over / 暂停等弹窗按取景框「70% 高 / 92% 宽」等比缩放居中，适配不同分辨率。
- **横屏适配**：模式面板画布参考分辨率修正为 `960×540`（横屏），修复了此前面板被压扁、按钮点不中的问题。
- **游戏说明页**：更新作者、来源与联系方式。

## 四、运行与构建

### 直接玩（推荐）

| 平台 | 获取方式 |
|---|---|
| Windows | 下载 Release 中的 `NaruSasu English.exe`，双击运行（保持 `NaruSasu English_Data` 同目录） |
| Android | 安装 Release 中的 `NaruSasu English_ARM64.apk`（需允许「安装未知来源应用」） |

### 从源码构建

```text
1. 用 Unity Hub 安装 Unity 2022.3.1f1c1（含 Windows Build Support；
   如需 APK 再勾选 Android Build Support + SDK/NDK + OpenJDK）
2. Hub 里 Add → 选择本工程目录，等待首次导入完成
3.    菜单 Build/打包 Windows (x64)              → Build/Windows/NaruSasu English.exe
   菜单 Build/打包 Android APK (IL2CPP ARM64) → Build/Android/NaruSasu English_ARM64.apk
   菜单 Build/打包 全部 (Win + APK)
```

> 也支持**请求文件**方式驱动（无需在编辑器里点菜单，便于自动化）：
> 在 `<工程>/Temp/` 下新建 `package.request`，内容写 `win` / `apk` / `all`。
> 构建过程日志见 `<工程>/Build/package-log.txt`。

> ⚠️ **Android 构建注意**：Unity 的 Android 工具链对**非 ASCII 工程路径**敏感。
> 若工程放在含中文的目录下且 Gradle 报错，请把工程复制到纯英文路径（如 `E:\NaruSasuEnglish`）后再构建 APK。

## 五、目录结构

```
Assets/
  Editor/                 编辑器工具（打包、自检、截图、场景诊断等）
    GamePackager.cs          ★ 一键打包 Windows / Android
    CharacterModeSelfTest.cs   角色模式自检
    UiShowcaseCapture.cs       界面验收截图
  EnglishApp/
    Scripts/                游戏逻辑
      CharacterModeManager.cs  ★ 双角色模式（状态/主题/台词/音效/UI）
      CharacterModeFeedback.cs 答题台词反馈
      EnemySkinController.cs   ★ 小怪换皮
      VocWinLayout.cs          单词框尺寸
      DialogFitter.cs          弹窗自适应
    Scenes/                 MainScene / TD_LevelSelect / TD_Scene
  GameRes/                 美术、字体、音效等资源
  Resources/CharacterMode/ 角色头像、字体与 6 张小怪贴图
ProjectSettings/           工程设置
Packages/                  包依赖
Docs/screenshots/          README 用截图
```

## 六、致谢与来源

- **原作者**：奈何 —— 原始「英语单词拼写游戏」，<https://blog.csdn.net/final5788/article/details/61615498>
- 本仓库为在原作基础上的二次开发版本（同人性质，鸣人 / 佐助形象版权归原作者所有，仅供学习交流，**请勿用于商业用途**）。
- 鸣人 / 佐助为《火影忍者》角色，相关形象版权归集英社 / 岸本齐史所有。

## 七、联系方式

- 作者：胡安
- GitHub：<https://github.com/Hazel1103>
- Email：13571993443@139.com
