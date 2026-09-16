# 鸣人 / 佐助双角色模式接入说明

本实现面向原项目 Unity 2018.3.0f2，未改动 `GameController`、`Enemy`、`GameSceneEvents` 的答题、计分、词库和对错判断代码。新增功能全部位于 `Assets/EnglishApp/Scripts/CharacterModeManager.cs` 和 `CharacterModeFeedback.cs`。

## 已包含内容

- `CharacterModeManager`：全局角色状态、`PlayerPrefs` 持久化、主题色、头像、台词和音效。
- `CharacterModeRuntimeUI`：当场景没有手工 UI 时，自动在 Canvas 右上角创建鸣人/佐助选择按钮、头像和反馈文本。
- `CharacterModeFeedback`：自动挂到游戏场景的 `GameController`，监听原有输入框警示色和敌人 `damage` 动画来显示角色台词，不改变原答题流程。
- `Assets/Resources/CharacterMode/naruto_avatar.jpg`：鸣人头像示例（已设置为 Sprite）。
- `Assets/Resources/CharacterMode/sasuke_avatar.jpg`：佐助头像示例（已设置为 Sprite）。
- `naruto_correct.wav` / `sasuke_correct.wav`：内置的轻快和低沉答对音效。

## UI 搭建方式 A：自动 UI（推荐先验证）

1. 将本目录中的脚本和 `Resources/CharacterMode` 复制到工程对应目录。
2. 重新打开 Unity，等待资源导入完成。
3. 不需要修改原有场景。`CharacterModeBootstrap` 会在启动时创建持久化管理器；运行时会复用当前场景 Canvas，并创建角色面板。
4. 在任意场景运行，点击“鸣人”或“佐助”。选择会立即应用并写入 `PlayerPrefs`，下次启动自动沿用。

## UI 搭建方式 B：手工 UGUI（正式项目可用）

1. 在首个场景创建空物体 `CharacterModeManager`，挂载 `CharacterModeManager`。若使用手工 UI，可取消 `Create Runtime UI`。
2. 在 Canvas 下创建 `Panel`、`Image(Avatar)`、`Text(CharacterName)`、`Text(CharacterFeedback)` 和两个 `Button`。
3. 将两个头像拖到管理器的 `Naruto Avatar`、`Sasuke Avatar`；将两个 WAV 拖到 `Naruto Correct Clip`、`Sasuke Correct Clip`。
4. 把头像 Image、角色名 Text、反馈 Text、答对音效 AudioSource 分别拖到对应字段。两个按钮的 `OnClick` 分别绑定 `CharacterModeManager.SelectNaruto` 和 `SelectSasuke`。
5. 背景 Image 拖到 `Background Image`；需要切换主色的按钮/进度条拖入 `Primary Graphics`；需要切换文字色的 Text 拖入 `Themed Texts`。
6. Avatar 的 `Image Type` 建议使用 `Simple`，`Preserve Aspect` 打开。反馈 Text 建议宽度 900、高度 84、水平居中，并开启换行。

## 资源导入设置

- 头像：Inspector 中 `Texture Type = Sprite (2D and UI)`、`Sprite Mode = Single`、`Mesh Type = Full Rect`、`Filter Mode = Bilinear`、`Compression = Normal Quality`。若替换为透明 PNG，打开 `Alpha Is Transparency`。
- 音效：`Load Type = Decompress On Load`、`Compression Format = Vorbis`、`Preload Audio Data = On`、`3D Sound = Off`。
- 示例 JPG 已裁剪为脸部头像；若需要替换角色，只需保留同名文件并重新导入，脚本无需修改。

## 主题和台词

- 鸣人：主色 `#EF7019`，背景 `#FFEDD7`，黑色文字；答对“做得不错！继续努力！可不能半途而废！”，答错“没关系！再来一次，多加练习一定能记住！”。
- 佐助：主色 `#12233A`，背景 `#B8C6D5`，白色文字；答对“……进步了。”，答错“基础不够扎实，重新记。”。

## Android / OPPO Find X8 构建

原工程的 `ProjectSettings/ProjectVersion.txt` 为 Unity 2018.3.0f2。打开 Unity Hub 安装同版本 Editor，并勾选 Android Build Support、SDK/NDK 和 OpenJDK，然后执行：

1. `File > Build Settings`，选择 Android，点击 `Switch Platform`。
2. `Player Settings > Other Settings`：`Scripting Backend = IL2CPP`、`Target Architectures = ARM64`、`API Compatibility Level = .NET 4.x`、关闭不需要的 Auto Graphics API 并保留 Vulkan 或 OpenGLES3。
3. `Player Settings > Resolution and Presentation`：方向选择 Portrait（或按项目需要选择 Auto Rotation），勾选 `Render Outside Safe Area`，Canvas 使用 `Scale With Screen Size`，参考分辨率建议 `1080 x 1920`。
4. 在 Build Settings 中加入 `MainScene`、`TD_LevelSelect`、`TD_Scene`，点击 `Build` 输出 `Build/SpellingGame.apk`；也可以直接使用菜单 `Build > Build SpellingGame APK (Android)`，脚本会自动切换 Android、设置 IL2CPP/ARM64 并加入这三个场景。

本工作区没有安装 Unity Editor，因而无法在当前环境实际生成二进制 APK；工程源码、资源和 Android 参数已准备好，可在安装 Unity 2018 Android 模块的机器上直接构建。
