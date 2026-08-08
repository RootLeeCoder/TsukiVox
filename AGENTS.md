# Repository Guidelines

## 项目结构与模块组织

这是一个基于 Unity 6000.5.0f1 的 TsukiVox 原生 Quest 单人 K 歌客户端原型，当前路线图版本为 V0.76。运行时代码放在 `Assets/Scripts/`，核心脚本位于 `Assets/Scripts/AudioPrototype/`：`QuestAudioPrototype.cs` 负责 Quest 麦克风返听、干声环形缓冲与音效验证；`VoiceSearchRecorder.cs` 负责 V0.75 的旁路干声采集、端点检测、48 kHz 到 16 kHz 降采样和内存 WAV 封装；`PlaylistClient.cs` 和 `QuestPlaylistPrototype.cs` 负责局域网 Companion/在线服务的文字搜索、拼音候选、语音搜索、识别供应商、队列、控制、设备 ID 和媒体 URL 解析；`QuestAndroidKeyboardInput.cs` 负责 V0.76 头显内世界空间软键盘与 TMP 输入框交互，不再调用 Android 系统输入法；`QuestVideoScreenPrototype.cs` 负责 `VideoPlayer + RenderTexture` 视频大屏、播放同步、录音期间视频降音量和视频诊断；`QuestKtvRoomPrototype.cs` 负责 KTV 包厢、茶几、平板底板、灯光和空间锚点；`QuestHandheldPropsPrototype.cs` 负责右手麦克风、左手荧光棒、麦克风防贴脸触觉和语音找歌轻触觉；`QuestAppShellPrototype.cs` 负责世界空间 Canvas 和 App 诊断装配；`QuestConsumerUiPrototype.cs` 负责主页、文字/语音/拼音点歌、人声、队列、设置、点歌服务、供应商选择、麦克风防碰撞和诊断抽屉；`QuestTabletTiltController.cs` 负责茶几平板的 0/30/60/90 度四档倾角、动画和独立档位开关；`QuestUiPointer.cs`、`QuestUiButtonFeedback.cs`、`QuestUiIcon.cs` 和 `QuestXrBootstrap.cs` 负责控制器射线、软键盘焦点保护、按钮反馈、原生矢量图标、HMD tracking 与 XR 启动；`QuestBuildInfo.cs` 和 `TsukiVoxClipboard.cs` 分别封装构建身份/本地构建时间与 Editor/Android 剪贴板复制。

编辑器工具放在 `Assets/Editor/`：`CreateAudioPrototypeScene.cs` 用于重新生成 `Assets/Scenes/AudioPrototype.unity`，`GenerateConsumerUiFontAsset.cs` 用于生成 `Assets/Resources/Fonts/NotoSansSC-SDF.asset`，`QuestAndroidBuildSettings.cs` 用于 Quest Android 构建预处理，`QuestCommandLineBuild.cs` 负责 Editor 内/无界面 APK 构建请求、嵌入 Build ID 和生成构建收据。`Tools/Deploy-Quest.ps1` 负责构建、ADB 覆盖安装、Package Manager 版本核验、启动和 `logcat` Build ID 核对。原生 Oboe 参考路径位于 `Native/TsukiVoxOboeMonitor/`，Android 插件产物位于 `Assets/Plugins/Android/libs/arm64-v8a/`。Unity 包依赖位于 `Packages/`，项目设置位于 `ProjectSettings/`。不要提交 `Library/`、`Temp/`、`Obj/`、`Logs/`、`UserSettings/`、`build/`，以及生成的 APK/AAB 文件。

## 构建、测试与开发命令

- 使用 Unity Hub 以 `6000.5.0f1` 打开项目。
- 在 Unity 菜单中运行 `TsukiVox > Create Audio Prototype Scene`，可重新生成原型场景。
- 如果修改了中文 UI 字符范围、TMP 设置或源字体，运行 `TsukiVox > Generate Consumer UI Font Asset` 重新生成动态多图集 SDF 字体；不要把 V0.76 消费级 UI 改回旧版 `UnityEngine.UI.Text`。
- 如需把当前默认 Companion 地址写入场景，可运行 `TsukiVox > Apply Current Helper Host To Scene`；此命令不修改在线服务 origin。
- Companion 模式连接局域网 PC：playlist 服务 `http://<PC IP>:5175`，下载/搜索服务 `http://<PC IP>:5174`。Quest 真机不能用 `127.0.0.1` 或 `localhost` 访问 PC，应在头显内 `设置 > 点歌服务` 输入局域网地址。
- 点歌服务提供“局域网 Companion”“公网服务”“本地开发”三个入口。Companion 继续使用 PC IP 与 `5174/5175`；公网默认 origin 为 `https://api.tsukivox.com`，本地开发入口为 `http://192.168.50.41:8080`。在线协议同时承载设备登记、文字搜索、`/api/bilibili/suggest` 拼音候选、`/api/voice-search` 语音找歌、`/api/voice/provider` 供应商选择、队列和签名媒体下载。
- Quest 3 日常构建部署使用 `pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1`。脚本会生成 `build/TsukiVox-Quest.apk`，通过 `adb install -r` 保留应用数据地覆盖安装，启动应用，并核对 `logcat` 中的 Build ID。
- 只构建 APK 使用 `pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1 -BuildOnly`；只安装已有 APK 使用 `pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1 -InstallOnly`。同一项目已在 Unity Editor 中打开时，脚本会向当前编辑器提交一次显式构建请求；编辑器关闭时则自动使用无界面 Unity，不需要手动切换模式。
- 可用以下命令做无界面启动检查：
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile unity-smoke.log
  ```

## 编码风格与命名约定

使用 C#，保持四空格缩进，花括号单独成行，风格与现有脚本一致。运行时代码使用 `TsukiVox.AudioPrototype` 命名空间，编辑器代码使用 `TsukiVox.AudioPrototype.Editor`。除非确实需要继承，`MonoBehaviour` 类优先声明为 `sealed`。类型、方法、枚举值和 Unity 菜单名使用 PascalCase；字段和局部变量使用 camelCase。私有序列化字段保持 `[SerializeField] private`，并用清晰的 `[Header]` 分组。

## V0.65-V0.76 产品与控制面板约定

- V0.65 茶几控制面板继续作为 V0.76 的视觉与交互基线。它是面向普通用户的主操作入口，不再是调试 Canvas；主页只保留当前歌曲、播放控制、麦克风状态和清晰导航，原始调试信息必须收进 `设置 > 诊断与支持` 抽屉。
- 控制面板和物理底板必须共用 `Tablet Anchor`，四档倾角为 0/30/60/90 度，默认 30 度。调整角度时底板、屏幕和 Canvas 必须同步平滑转动，不能恢复固定世界旋转或产生额外角度偏差。
- 控制器扳机是 UI 点击入口。页面切换时必须同步维护 `CanvasGroup.interactable` 与 `blocksRaycasts`；返回、关闭、播放、搜索、点播、翻页、预设、滑杆、开关和服务地址输入框都要保持可命中。
- 消费级 UI 使用 `TextMeshProUGUI`、`TMP_InputField` 和 `NotoSansSC-SDF`。中文字号、边距、选中态和 hover 状态必须在 Quest 3 中可读，不能让动态内容覆盖相邻元素或贴住面板边缘。
- 图标使用 `QuestUiIcon` 的原生矢量路径。播放控制沿用 Lucide 视觉语言，品牌标记参考 VRSing favicon，但不要把 Web DOM/SVG 运行时直接搬进 Unity。
- 茶几右下角的角度开关独立于转动平板，任何选中、hover、按下和未选中状态都必须保留完整档位文字。

## V0.7-V0.76 点歌与设备体验约定

- 头显内搜索点歌是 V0.7 之后的核心入口。Quest 客户端只调用服务端文字搜索、拼音候选、语音识别、队列和下载协议，不在 App 内加入 Bilibili/YouTube 下载器、云端语音密钥、Cookie 或平台凭据。
- `PlaylistClient` 的在线状态、控制、文字搜索、拼音候选、语音搜索、供应商读写和添加请求都必须携带持久化的 `X-TsukiVox-Device-Id` 与该 origin 独立的 Bearer 凭证。不要把设备 ID 改成每次启动重新生成，也不要把凭证、在线队列或语音供应商偏好跨设备或跨 origin 共享。
- Companion 和在线服务必须保留各自独立的持久化地址。Companion 使用 PC IP 加 `5174/5175`，在线服务使用单一完整 HTTP(S) origin；切换模式必须取消旧文字/拼音/语音/点播请求、清空陈旧状态并重启轮询及供应商状态读取。
- 在线服务（包括公网和 Ubuntu 本地开发入口）必须直接使用服务端的 HTTP Range 媒体流，不得在 Android 上强制完整缓存后才播放；Companion 继续保留完整缓存兼容路径。
- 搜索和服务地址继续使用 `TMP_InputField + QuestAndroidKeyboardInput`。V0.76 的 `QuestAndroidKeyboardInput` 是世界空间软键盘，不是 Android 系统输入法；修改时必须保留字母/符号切换、大小写、光标、退格、清空、完成/取消、原文恢复、输入框焦点和控制器射线连续命中。
- 麦克风防贴脸触觉必须使用网头表面间隙而不是手柄原点距离，并保留轻震/强震阈值、迟滞、节流、强度缩放、校准期间抑制震动和 `PlayerPrefs` 持久化。
- 麦克风触觉不能改变既有输入职责：右手麦克风、左手荧光棒、左手 X/Y 换色、左右 grip 切换各自射线、左右扳机点击 UI。

## V0.75 语音输入与找歌约定

- 语音找歌只能旁路读取 `QuestAudioPrototype` 的现有干声缓冲。不得为了语音搜索调整返听增益、滤波器、混响预设或 `OnAudioFilterRead` 处理，也不得停止/重启麦克风；这些音频引擎改动属于 V0.8。
- `VoiceSearchRecorder` 保持 7 秒独立缓冲、16 kHz 单声道 16-bit WAV、800 ms 尾部静音自动提交、2.5 秒前置静音判定和 6 秒硬上限。无语音必须在端上结束，不调用云端；封装 payload 后清零录音器采集缓冲，payload 只在请求期间保留，不落盘、不记录音频字节。
- 录音期间只允许通过 `QuestVideoScreenPrototype.SetPlaybackVolume` 暂时压低视频音量，结束、取消和失败路径都必须恢复；返听与人声预设必须连续工作。
- 语音入口与文字/拼音搜索共用四行结果，但状态机必须保留 `Idle`、`Listening`、`Uploading`、`Searching`、`Results`、`NoSpeech`、`Empty`、`Failed` 八态、电平条和“听到”只读回执。结果不能自动点播，仍需用户点 `+`。
- 腾讯云和 MiMo 是互斥供应商，每次识别不因请求失败自动故障转移。`GET/POST /api/voice/provider` 的选择按设备保存在服务端；设备未选择或已选供应商后来失去配置时才解析到服务端默认值。Quest 只显示可配置项，不持有任何供应商密钥。语音总开关关闭后入口与供应商控件必须隐藏，并完全停止采集和上传。
- Native Oboe 后端当前不提供 PCM 出口，语音按钮必须置为不可用并说明原因；不得在用户点击语音时偷偷切回 Unity backend。

## V0.76 拼音搜索与软键盘约定

- 拼音建议通过 `GET /api/bilibili/suggest?term=...` 获取。用户编辑任意非空内容并停顿 350 ms 后自动请求；连续输入必须重置防抖，不设置最少字符数，也不再保留独立的 Sparkles 候选按钮。
- 客户端最多显示九条候选，并使用不遮挡世界空间键盘的 3x3 紧凑布局。文本内容变化、执行正式搜索、切换服务或离开流程时必须取消/清空旧防抖、请求和候选；软键盘为连续输入重新聚焦输入框时不得取消防抖。候选显示或键盘打开时搜索结果行必须暂时隐藏，不能互相覆盖或截获射线。
- 软键盘“完成”只关闭键盘并立即请求当前输入的候选，不得触发正式搜索或取消尚未显示的自动候选。点击候选必须回填搜索框、关闭候选层并自动执行正式搜索；点播仍需用户从四行结果中点 `+`。启动时搜索框与搜索结果保持为空，placeholder 使用“请输入歌曲名，推荐使用全拼”。
- `KTV` 开关使用 `TsukiVox.AppendKtvToSearch` 持久化，只在正式搜索请求中追加后缀；候选请求使用输入原文，查询已经等于或以 ` KTV` 结尾时不得重复追加。
- `QuestUiPointer` 点击软键盘子对象时必须保持 TMP 输入框的选中目标。软键盘的文本与图标不得拦截父按钮 raycast，完成/取消后输入框仍须可被扳机再次命中。

## 测试指南

当前 Unity 仓库尚未提交自动化测试。可独立验证的逻辑应使用 Unity Test Runner，并放在 `Assets/Tests/EditMode/` 或 `Assets/Tests/PlayMode/`，测试文件名以 `Tests.cs` 结尾。音频相关改动必须在 Quest 3 真机上验证：麦克风权限、输入/输出电平、返听可听性、预设切换，以及是否存在明显削波、啸叫或反馈。播放队列相关改动需要同时验证当前所选服务可达性、`/api/playlist/state` 轮询、`/api/playlist/control` 控制命令、`/api/bilibili/search` 文字搜索、`/api/bilibili/suggest` 拼音候选、`/api/voice-search` 语音找歌、`/api/voice/provider` 供应商读写、`/api/playlist/items` 点播、稳定的 `X-TsukiVox-Device-Id`、`playableUrl`/`/downloads/...` 解析，以及断网或服务关闭后的 UI 恢复提示。服务配置改动还需回归 Companion/在线服务切换、各自地址持久化、endpoint 重建、请求取消和不同设备的队列/供应商隔离。视频相关改动需要验证 ready 条目的 MP4/WebM 加载、远端缓存、首帧显示、宽高比适配、播放/暂停/重播/切歌同步、视频结束后 `next`，以及视频音频与麦克风返听/混响共存；排查时优先使用 `设置 > 诊断与支持 > 复制完整诊断信息`。

V0.75 语音改动必须额外验证：Unity backend 下背景音乐播放中录音、视频降音量与恢复、返听不断音且无爆音、中文/中英混合/粤语、无语音不上传、断网与配额提示、供应商按设备切换、八态文案、连续 20 次点歌，以及返听音质/延迟/预设相比 V0.7 无退化。Native Oboe 下应明确不可用，而不是静默失败或自动切换后端。

V0.76 拼音与输入改动必须额外验证：世界空间软键盘的字母/符号、大小写、光标、退格、清空、“完成”立即请求候选、“取消”原文恢复；输入任意非空内容并停顿 350 ms 后自动请求最多九条候选，连续输入只保留最后一次请求；候选回填并自动正式搜索，结果仍需点 `+` 点播；`KTV` 后缀开关持久化且不重复追加；语音开关关闭/开启后的两套布局；候选、键盘和结果层不互相遮挡或截获射线。

V0.76 控制面板和设备体验改动必须在 Quest 3 中额外回归：初次启动默认 30 度；0/30/60/90 度下底板与 Canvas 共面；角度开关文字始终可见；主页、搜索点歌、人声、播放队列、设置、点歌服务、麦克风防碰撞和诊断抽屉无文字重叠；预设和档位选中态足够醒目；右上角图标 hover 不出现多余文字；返回/关闭按钮与分隔线有间距；扳机可操作按钮、输入框、软键盘、候选、滑杆和开关；麦克风靠近面部时轻震/强震、校准、关闭和持久化符合设置；原始诊断不会漂到茶几或大屏其他位置。重新构建/安装后还需确认 OpenXR、GameActivity、麦克风权限、两种点歌服务、Build ID 和 `复制完整诊断信息` 稳定。

## Quest 自动部署完成条件

- 修改 `Assets/Scripts/`、`Assets/Scenes/`、`Assets/Resources/`、`Assets/Plugins/Android/`、`Packages/` 或影响 Player 的 `ProjectSettings/` 后，在任务交付前必须执行 `pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1`。一次完整任务在相关编辑和静态检查完成后部署一次，不要在每次保存文件后部署。
- 纯文档、注释、测试代码或不影响 Player 的 Editor 工具改动不要求真机部署；用户明确要求跳过部署或当前任务只讨论方案时也不执行。
- 默认只允许 `adb install -r` 覆盖安装，不得默认卸载应用、清除数据或跳过首次权限行为测试所需的用户确认。
- 脚本必须确认 APK 构建成功、ADB 安装成功，并从 Android Package Manager 读回包含本次 Build ID 的 `versionName`，才能报告自动安装完成。设置页显示的 Build ID 必须与 `build/last-deploy.json` 一致。
- 路线图功能版本已到 V0.76，但当前 `QuestBuildInfo.ProductVersion` 和 `ProjectSettings` 的 APK 产品版本前缀仍为 `0.65`。在专门的版本升级改动完成前，不要仅凭 `0.65-build.<Build ID>` 前缀判断功能版本，也不要只改其中一处；精确安装核验继续以完整 Build ID 为准。
- 安装后应自动启动应用，并优先从 `logcat` 核对同一 Build ID。如果 Quest 因头显休眠或控制器不可用而显示系统 `LaunchCheckControllerRequiredDialogActivity`，可以报告“精确版本已安装、运行时启动待头显唤醒”，不能报告已完成运行时验证。未知原因未启动、应用崩溃或 Build ID 不匹配仍视为部署失败。
- Quest 未连接、`unauthorized`/`offline`、打开的 Unity 未接受构建请求、构建失败、安装失败或版本核对失败时，必须明确报告未完成的阶段和原因。即使设备不可用，条件允许时也应使用 `-BuildOnly` 完成 APK 构建检查。

## 提交与 Pull Request 规范

提交信息应简短并明确描述用户可见结果，例如 `Complete V0.76 pinyin song search`。Pull Request 应说明用户可见行为变化，列出已完成的 Quest/设备验证；如果改动 UI，请附截图或短视频；如果改动了生成场景、字体资产、包依赖或 Android 配置，也需要明确说明。

## 安全与配置提示

避免添加过于简化的自定义 `Assets/Plugins/Android/AndroidManifest.xml`；不完整的 manifest 可能导致 APK 可以安装但无法启动。麦克风权限逻辑应保持清晰，并在设备上测试首次启动时的权限弹窗。局域网 Companion 和 Ubuntu 测试服务可以在原型阶段使用明文 HTTP、Internet 权限与应用内视频缓存；正式公网在线服务必须使用 HTTPS。不要把公网服务密钥、Cookie、个人下载内容、稳定设备 ID、缓存视频或真机诊断剪贴板内容写入 Unity 项目或日志样例。
