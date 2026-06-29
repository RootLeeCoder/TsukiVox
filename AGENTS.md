# Repository Guidelines

## 项目结构与模块组织

这是一个基于 Unity 6000.5.0f1 的 TsukiVox 原生 Quest 音频/播放队列/视频播放原型项目。运行时代码放在 `Assets/Scripts/`，当前核心脚本位于 `Assets/Scripts/AudioPrototype/`：`QuestAudioPrototype.cs` 负责 Quest 麦克风返听与音效验证，`PlaylistClient.cs` 和 `QuestPlaylistPrototype.cs` 负责 V0.2 PC helper / playlist API 接入，`QuestVideoScreenPrototype.cs` 负责 V0.3 `VideoPlayer + RenderTexture` 视频大屏、播放同步和视频调试信息，`QuestAppShellPrototype.cs` 负责 V0.4 头显内控制壳、面板整理和 App 诊断，`QuestUiPointer.cs` 和 `QuestXrBootstrap.cs` 负责控制器射线 UI 与 HMD tracking / XR 启动基础，`TsukiVoxClipboard.cs` 封装 Editor/Android 剪贴板复制。编辑器工具放在 `Assets/Editor/`，例如 `CreateAudioPrototypeScene.cs` 用于重新生成 `Assets/Scenes/AudioPrototype.unity`，`QuestAndroidBuildSettings.cs` 用于 Quest Android 构建预处理。原生 Oboe 参考路径位于 `Native/TsukiVoxOboeMonitor/`，Android 插件产物位于 `Assets/Plugins/Android/libs/arm64-v8a/`。Unity 包依赖位于 `Packages/`，项目设置位于 `ProjectSettings/`。不要提交 `Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/`、`build/`，以及生成的 APK/AAB 文件。

## 构建、测试与开发命令

- 使用 Unity Hub 以 `6000.5.0f1` 打开项目。
- 在 Unity 菜单中运行 `TsukiVox > Create Audio Prototype Scene`，可重新生成原型场景。
- 如需把当前默认 PC helper 地址写入场景，可运行 `TsukiVox > Apply Current Helper Host To Scene`。
- V0.2-V0.4 原型默认连接局域网 PC：playlist 服务 `http://<PC IP>:5175`，下载文件服务 `http://<PC IP>:5174`。Quest 真机不能用 `127.0.0.1` 或 `localhost` 访问 PC helper，应在头显内 UI 的 `PC IP` 输入框中配置局域网地址。
- Quest 3 构建流程：打开 `File > Build Profiles...` 或 `File > Build Settings...`，选择 `Android`，确认包含 `Assets/Scenes/AudioPrototype.unity`，然后执行 `Build And Run`。
- 可用以下命令做无界面启动检查：
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile unity-smoke.log
  ```

## 编码风格与命名约定

使用 C#，保持四空格缩进，花括号单独成行，风格与现有脚本一致。运行时代码使用 `TsukiVox.AudioPrototype` 命名空间，编辑器代码使用 `TsukiVox.AudioPrototype.Editor`。除非确实需要继承，`MonoBehaviour` 类优先声明为 `sealed`。类型、方法、枚举值和 Unity 菜单名使用 PascalCase；字段和局部变量使用 camelCase。私有序列化字段保持 `[SerializeField] private`，并用清晰的 `[Header]` 分组。

## 测试指南

当前仓库尚未提交自动化测试。可独立验证的逻辑应使用 Unity Test Runner，并放在 `Assets/Tests/EditMode/` 或 `Assets/Tests/PlayMode/`，测试文件名以 `Tests.cs` 结尾。音频相关改动必须在 Quest 3 真机上验证：麦克风权限、输入/输出电平、返听可听性、预设切换，以及是否存在明显削波、啸叫或反馈。播放队列相关改动需要同时验证 PC helper 可达性、`/api/playlist/state` 轮询、`/api/playlist/control` 控制命令、`playableUrl`/`/downloads/...` 解析，以及断网或 helper 关闭后的 UI 恢复提示。视频相关改动需要验证 ready 条目的 MP4/WebM 加载、远端缓存、首帧显示、宽高比适配、播放/暂停/重播/切歌同步、视频结束后 `next`，以及视频音频与麦克风返听/混响共存；排查时优先使用面板中的 `Copy Debug` 信息。App 壳和头显内 UI 改动需要在 Quest 3 中验证世界空间面板可读性、控制器射线命中、按钮/输入框/滑杆/开关可操作性、`Copy App Debug` 可用性，以及重新构建/重新安装后 OpenXR、GameActivity、麦克风权限和 helper 地址应用流程稳定。

## 提交与 Pull Request 规范

当前 Git 历史只有 `init`，因此暂不强制复杂格式。提交信息应简短、使用祈使句，例如 `Add Quest audio safety limiter`。Pull Request 应说明用户可见行为变化，列出已完成的 Quest/设备验证；如果改动 UI，请附截图或短视频；如果改动了生成场景、包依赖或 Android 配置，也需要明确说明。

## 安全与配置提示

避免添加过于简化的自定义 `Assets/Plugins/Android/AndroidManifest.xml`；不完整的 manifest 可能导致 APK 可以安装但无法启动。麦克风权限逻辑应保持清晰，并在设备上测试首次启动时的权限弹窗。V0.2-V0.4 为了局域网 helper 和视频文件服务使用明文 HTTP、Internet 权限与应用内视频缓存，这是本地原型约束；不要把公网服务、密钥、Cookie、个人下载内容、缓存视频或真机诊断剪贴板内容写入 Unity 项目。
