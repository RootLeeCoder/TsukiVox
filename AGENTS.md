# Repository Guidelines

## 项目结构与模块组织

这是一个基于 Unity 6000.5.0f1 的 TsukiVox 原生 Quest 音频原型项目。运行时代码放在 `Assets/Scripts/`，当前核心脚本是 `Assets/Scripts/AudioPrototype/QuestAudioPrototype.cs`。编辑器工具放在 `Assets/Editor/`，例如 `CreateAudioPrototypeScene.cs` 用于重新生成 `Assets/Scenes/AudioPrototype.unity`。Unity 包依赖位于 `Packages/`，项目设置位于 `ProjectSettings/`。不要提交 `Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/`、`build/`，以及生成的 APK/AAB 文件。

## 构建、测试与开发命令

- 使用 Unity Hub 以 `6000.5.0f1` 打开项目。
- 在 Unity 菜单中运行 `TsukiVox > Create Audio Prototype Scene`，可重新生成原型场景。
- Quest 3 构建流程：打开 `File > Build Profiles...` 或 `File > Build Settings...`，选择 `Android`，确认包含 `Assets/Scenes/AudioPrototype.unity`，然后执行 `Build And Run`。
- 可用以下命令做无界面启动检查：
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile unity-smoke.log
  ```

## 编码风格与命名约定

使用 C#，保持四空格缩进，花括号单独成行，风格与现有脚本一致。运行时代码使用 `TsukiVox.AudioPrototype` 命名空间，编辑器代码使用 `TsukiVox.AudioPrototype.Editor`。除非确实需要继承，`MonoBehaviour` 类优先声明为 `sealed`。类型、方法、枚举值和 Unity 菜单名使用 PascalCase；字段和局部变量使用 camelCase。私有序列化字段保持 `[SerializeField] private`，并用清晰的 `[Header]` 分组。

## 测试指南

当前仓库尚未提交自动化测试。可独立验证的逻辑应使用 Unity Test Runner，并放在 `Assets/Tests/EditMode/` 或 `Assets/Tests/PlayMode/`，测试文件名以 `Tests.cs` 结尾。音频相关改动必须在 Quest 3 真机上验证：麦克风权限、输入/输出电平、返听可听性、预设切换，以及是否存在明显削波、啸叫或反馈。

## 提交与 Pull Request 规范

当前 Git 历史只有 `init`，因此暂不强制复杂格式。提交信息应简短、使用祈使句，例如 `Add Quest audio safety limiter`。Pull Request 应说明用户可见行为变化，列出已完成的 Quest/设备验证；如果改动 UI，请附截图或短视频；如果改动了生成场景、包依赖或 Android 配置，也需要明确说明。

## 安全与配置提示

避免添加过于简化的自定义 `Assets/Plugins/Android/AndroidManifest.xml`；不完整的 manifest 可能导致 APK 可以安装但无法启动。麦克风权限逻辑应保持清晰，并在设备上测试首次启动时的权限弹窗。
