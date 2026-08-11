# Repository Guidelines

## 项目结构与模块组织

这是一个基于 Unity 6000.5.0f1 的 TsukiVox 原生 Quest 单人 K 歌客户端原型，当前路线图版本为 V0.78。运行时代码放在 `Assets/Scripts/`，核心脚本位于 `Assets/Scripts/AudioPrototype/`：`QuestAudioPrototype.cs` 负责 Quest 麦克风返听与音效验证；`BilibiliDirectClient.cs` 负责匿名文字搜索、拼音候选、BV/链接解析和 progressive MP4 下载；`DirectPlaylist.cs` 定义 Direct 队列常量与数据模型；`QuestPlaylistPrototype.cs` 负责客户端内存队列、下载调度和播放控制；`QuestAndroidKeyboardInput.cs` 负责头显内世界空间软键盘与 TMP 输入框交互；`QuestVideoScreenPrototype.cs` 负责 `VideoPlayer + RenderTexture` 视频大屏、播放同步和视频诊断；`QuestKtvRoomPrototype.cs` 负责 KTV 包厢、茶几、平板底板、灯光和空间锚点；`QuestHandheldPropsPrototype.cs` 负责右手麦克风、左手荧光棒和麦克风防贴脸触觉；`QuestAppShellPrototype.cs` 负责世界空间 Canvas 和 App 诊断装配；`QuestConsumerUiPrototype.cs` 负责主页、文字/拼音点歌、人声、队列、设置、麦克风防碰撞和诊断抽屉；`QuestTabletTiltController.cs` 负责茶几平板的 0/30/60/90 度四档倾角、动画和独立档位开关；`QuestUiPointer.cs`、`QuestUiButtonFeedback.cs`、`QuestUiIcon.cs` 和 `QuestXrBootstrap.cs` 负责控制器射线、软键盘焦点保护、按钮反馈、原生矢量图标、HMD tracking 与 XR 启动；`QuestBuildInfo.cs` 和 `TsukiVoxClipboard.cs` 分别封装构建身份/本地构建时间与 Editor/Android 剪贴板复制。

编辑器工具放在 `Assets/Editor/`：`CreateAudioPrototypeScene.cs` 用于重新生成 `Assets/Scenes/AudioPrototype.unity`，`GenerateConsumerUiFontAsset.cs` 用于生成 `Assets/Resources/Fonts/NotoSansSC-SDF.asset`，`QuestAndroidBuildSettings.cs` 用于 Quest Android 构建预处理，`QuestCommandLineBuild.cs` 负责 Editor 内/无界面 APK 构建请求、嵌入 Build ID 和生成构建收据。`Tools/Deploy-Quest.ps1` 负责构建、ADB 覆盖安装、Package Manager 版本核验、启动和 `logcat` Build ID 核对。原生 Oboe 参考路径位于 `Native/TsukiVoxOboeMonitor/`，Android 插件产物位于 `Assets/Plugins/Android/libs/arm64-v8a/`。Unity 包依赖位于 `Packages/`，项目设置位于 `ProjectSettings/`。不要提交 `Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/`、`build/`，以及生成的 APK/AAB 文件。

## 构建、测试与开发命令

- 使用 Unity Hub 以 `6000.5.0f1` 打开项目。
- 在 Unity 菜单中运行 `TsukiVox > Create Audio Prototype Scene`，可重新生成原型场景。
- 如果修改了中文 UI 字符范围、TMP 设置或源字体，运行 `TsukiVox > Generate Consumer UI Font Asset` 重新生成动态多图集 SDF 字体；不要把 V0.76 消费级 UI 改回旧版 `UnityEngine.UI.Text`。
- V0.78 只保留“直接请求”：Quest 直接访问 Bilibili HTTPS 公共接口，不再配置局域网 Companion、公网服务、本地开发服务或自定义服务地址。
- 点播媒体下载到 `Application.persistentDataPath/DirectMediaCache/` 后播放；Direct 只支持匿名公开的单文件 progressive MP4，不支持 Cookie、YouTube 或 DASH 音视频合并。
- Quest 3 日常构建部署使用 `pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1`。脚本会生成 `build/TsukiVox-Quest.apk`，通过 `adb install -r` 保留应用数据地覆盖安装，启动应用，并核对 `logcat` 中的 Build ID。
- 只构建 APK 使用 `pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1 -BuildOnly`；只安装已有 APK 使用 `pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1 -InstallOnly`。同一项目已在 Unity Editor 中打开时，脚本会向当前编辑器提交一次显式构建请求；编辑器关闭时则自动使用无界面 Unity，不需要手动切换模式。
- 可用以下命令做无界面启动检查：
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile unity-smoke.log
  ```

## 编码风格与命名约定

使用 C#，保持四空格缩进，花括号单独成行，风格与现有脚本一致。运行时代码使用 `TsukiVox.AudioPrototype` 命名空间，编辑器代码使用 `TsukiVox.AudioPrototype.Editor`。除非确实需要继承，`MonoBehaviour` 类优先声明为 `sealed`。类型、方法、枚举值和 Unity 菜单名使用 PascalCase；字段和局部变量使用 camelCase。私有序列化字段保持 `[SerializeField] private`，并用清晰的 `[Header]` 分组。

## V0.65-V0.78 产品与控制面板约定

- V0.65 茶几控制面板继续作为 V0.78 的视觉与交互基线。它是面向普通用户的主操作入口，不再是调试 Canvas；主页只保留当前歌曲、播放控制、麦克风状态和清晰导航，原始调试信息必须收进 `设置 > 诊断与支持` 抽屉。
- 控制面板和物理底板必须共用 `Tablet Anchor`，四档倾角为 0/30/60/90 度，默认 30 度。调整角度时底板、屏幕和 Canvas 必须同步平滑转动，不能恢复固定世界旋转或产生额外角度偏差。
- 控制器扳机是 UI 点击入口。页面切换时必须同步维护 `CanvasGroup.interactable` 与 `blocksRaycasts`；返回、关闭、播放、搜索、点播、翻页、预设、滑杆和开关都要保持可命中。
- 消费级 UI 使用 `TextMeshProUGUI`、`TMP_InputField` 和 `NotoSansSC-SDF`。中文字号、边距、选中态和 hover 状态必须在 Quest 3 中可读，不能让动态内容覆盖相邻元素或贴住面板边缘。
- 图标使用 `QuestUiIcon` 的原生矢量路径。播放控制沿用 Lucide 视觉语言，品牌标记参考 VRSing favicon，但不要把 Web DOM/SVG 运行时直接搬进 Unity。
- 茶几右下角的角度开关独立于转动平板，任何选中、hover、按下和未选中状态都必须保留完整档位文字。

## V0.7-V0.78 点歌与设备体验约定

- 头显内搜索点歌是 V0.7 之后的核心入口。V0.78 使用 `BilibiliDirectClient` 匿名访问 Bilibili 公共接口，不发送 TsukiVox 设备 ID、Bearer、Cookie 或平台账号信息。
- Direct 请求 480P progressive MP4，并允许上游降级到 360P；返回多个分段、需要分轨合并、登录或 Cookie 时必须明确失败，不能静默选择无声视频。
- 本地队列只存在于当前 App 进程，媒体文件按稳定身份复用；取消下载、移除正在下载的条目或退出应用时必须终止请求并清理临时文件。
- 搜索使用 `TMP_InputField + QuestAndroidKeyboardInput`。世界空间软键盘必须保留字母/符号切换、大小写、光标、退格、清空、完成/取消、原文恢复、输入框焦点和控制器射线连续命中。
- 麦克风防贴脸触觉必须使用网头表面间隙而不是手柄原点距离，并保留轻震/强震阈值、迟滞、节流、强度缩放、校准期间抑制震动和 `PlayerPrefs` 持久化。
- 麦克风触觉不能改变既有输入职责：右手麦克风、左手荧光棒、左手 X/Y 换色、左右 grip 切换各自射线、左右扳机点击 UI。

## V0.76 拼音搜索与软键盘约定

- 拼音建议通过 `GET /api/bilibili/suggest?term=...` 获取。用户编辑任意非空内容并停顿 350 ms 后自动请求；连续输入必须重置防抖，不设置最少字符数，也不再保留独立的 Sparkles 候选按钮。
- 客户端最多显示九条候选，并使用不遮挡世界空间键盘的 3x3 紧凑布局。文本内容变化、执行正式搜索或离开流程时必须取消/清空旧防抖、请求和候选；软键盘为连续输入重新聚焦输入框时不得取消防抖。候选显示或键盘打开时搜索结果行必须暂时隐藏，不能互相覆盖或截获射线。
- 软键盘“完成”只关闭键盘并立即请求当前输入的候选，不得触发正式搜索或取消尚未显示的自动候选。点击候选必须回填搜索框、关闭候选层并自动执行正式搜索；点播仍需用户从四行结果中点 `+`。启动时搜索框与搜索结果保持为空，placeholder 使用“请输入歌曲名，推荐使用全拼”。
- `KTV` 开关使用 `TsukiVox.AppendKtvToSearch` 持久化，只在正式搜索请求中追加后缀；候选请求使用输入原文，查询已经等于或以 ` KTV` 结尾时不得重复追加。
- `QuestUiPointer` 点击软键盘子对象时必须保持 TMP 输入框的选中目标。软键盘的文本与图标不得拦截父按钮 raycast，完成/取消后输入框仍须可被扳机再次命中。

## 测试指南

当前 Unity 仓库尚未提交自动化测试。可独立验证的逻辑应使用 Unity Test Runner，并放在 `Assets/Tests/EditMode/` 或 `Assets/Tests/PlayMode/`，测试文件名以 `Tests.cs` 结尾。音频相关改动必须在 Quest 3 真机上验证：麦克风权限、输入/输出电平、返听可听性、预设切换，以及是否存在明显削波、啸叫或反馈。Direct 点歌相关改动需要验证 Bilibili 关键词/BV/完整链接/短链搜索、拼音候选、匿名播放地址解析、480P/360P 结果、下载百分比、稳定缓存命中、取消与失败恢复、本地队列控制，以及不支持的分轨/分段内容能够明确失败。视频相关改动需要验证本地 ready MP4 加载、首帧显示、宽高比适配、播放/暂停/重播/切歌同步、视频结束后 `next`，以及视频音频与麦克风返听/混响共存；排查时优先使用 `设置 > 诊断与支持 > 复制完整诊断信息`。

V0.76 拼音与输入改动必须额外验证：世界空间软键盘的字母/符号、大小写、光标、退格、清空、“完成”立即请求候选、“取消”原文恢复；输入任意非空内容并停顿 350 ms 后自动请求最多九条候选，连续输入只保留最后一次请求；候选回填并自动正式搜索，结果仍需点 `+` 点播；`KTV` 后缀开关持久化且不重复追加；候选、键盘和结果层不互相遮挡或截获射线。

V0.78 控制面板和设备体验改动必须在 Quest 3 中额外回归：初次启动默认 30 度；0/30/60/90 度下底板与 Canvas 共面；角度开关文字始终可见；主页、搜索点歌、人声、播放队列、设置、麦克风防碰撞和诊断抽屉无文字重叠；设置页不得重新出现服务选择、地址输入或在线缓存开关；预设和档位选中态足够醒目；右上角图标 hover 不出现多余文字；返回/关闭按钮与分隔线有间距；扳机可操作按钮、输入框、软键盘、候选、滑杆和开关；麦克风靠近面部时轻震/强震、校准、关闭和持久化符合设置。重新构建/安装后还需确认 OpenXR、GameActivity、麦克风权限、HTTPS Direct 请求、Build ID 和 `复制完整诊断信息` 稳定。

## Quest 自动部署完成条件

- 修改 `Assets/Scripts/`、`Assets/Scenes/`、`Assets/Resources/`、`Assets/Plugins/Android/`、`Packages/` 或影响 Player 的 `ProjectSettings/` 后，在任务交付前必须执行 `pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1`。一次完整任务在相关编辑和静态检查完成后部署一次，不要在每次保存文件后部署。
- 纯文档、注释、测试代码或不影响 Player 的 Editor 工具改动不要求真机部署；用户明确要求跳过部署或当前任务只讨论方案时也不执行。
- 默认只允许 `adb install -r` 覆盖安装，不得默认卸载应用、清除数据或跳过首次权限行为测试所需的用户确认。
- 脚本必须确认 APK 构建成功、ADB 安装成功，并从 Android Package Manager 读回包含本次 Build ID 的 `versionName`，才能报告自动安装完成。设置页显示的 Build ID 必须与 `build/last-deploy.json` 一致。
- 当前 `QuestBuildInfo.ProductVersion` 和 `ProjectSettings` 的 APK 产品版本前缀均为 `0.78`；修改版本时必须同步更新两处，精确安装核验继续以完整 Build ID 为准。
- 安装后应自动启动应用，并优先从 `logcat` 核对同一 Build ID。如果 Quest 因头显休眠或控制器不可用而显示系统 `LaunchCheckControllerRequiredDialogActivity`，可以报告“精确版本已安装、运行时启动待头显唤醒”，不能报告已完成运行时验证。未知原因未启动、应用崩溃或 Build ID 不匹配仍视为部署失败。
- Quest 未连接、`unauthorized`/`offline`、打开的 Unity 未接受构建请求、构建失败、安装失败或版本核对失败时，必须明确报告未完成的阶段和原因。即使设备不可用，条件允许时也应使用 `-BuildOnly` 完成 APK 构建检查。

## 提交与 Pull Request 规范

提交信息应简短并明确描述用户可见结果，例如 `Make Direct Request the only song source`。Pull Request 应说明用户可见行为变化，列出已完成的 Quest/设备验证；如果改动 UI，请附截图或短视频；如果改动了生成场景、字体资产、包依赖或 Android 配置，也需要明确说明。

## 安全与配置提示

避免添加过于简化的自定义 `Assets/Plugins/Android/AndroidManifest.xml`；不完整的 manifest 可能导致 APK 可以安装但无法启动。麦克风权限逻辑应保持清晰，并在设备上测试首次启动时的权限弹窗。Direct 只访问 Bilibili HTTPS 公共接口，Android Player 不允许明文 HTTP。不要把 Cookie、平台凭据、个人下载内容、缓存视频或真机诊断剪贴板内容写入 Unity 项目或日志样例。
