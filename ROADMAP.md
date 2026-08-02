# TsukiVox Unity 路线图

当前版本：**V0.76 - 拼音搜索与点歌交互优化**。

## 当前定位

TsukiVox 正从已经较成熟的 WebXR 原型迁移到 Unity 原生 Quest 应用。迁移原因不是产品方向变化，而是 Quest Browser / Web Audio 在实时人声返听和 KTV 混响上的延迟不可控，无法稳定支撑演唱体验。

当前目标是做一个面向 Quest 3 的单人原生 K 歌客户端：

- 收音使用 Quest 3 内置麦克风。
- Quest 手柄只承担视觉和交互角色，例如右手麦克风、左手荧光棒、控制指针。
- 多人房间、账号、云端歌单和跨设备同步都属于远期计划，现阶段完全不纳入设计压力。
- 局域网 Companion 或在线服务负责内容获取和播放队列，Unity Quest App 专注低延迟音频、视频播放、VR 房间和头显内交互。

WebXR 项目不是被废弃，而是作为迁移源。接下来要复用它已经验证过的内容管线、播放队列契约、交互判断和视觉体验，同时重做那些被浏览器平台限制影响的实现。

## 迁移原则

- 优先保护已经在 Quest 3 上主观满意的低延迟返听，不因为接入视频、UI 或房间表现而破坏音频链路。
- 先迁移“能唱一首歌”的核心闭环，再恢复 WebXR 版本里的完整包厢表现。
- 复用 Web 项目的内容获取和播放队列协议，兼容局域网 PC helper，并允许同一协议由在线服务承载；不在 Quest App 内实现 Bilibili / YouTube 下载器。
- 迁移体验和协议，不机械搬运 Three.js、WebXR、DOM、CSS 或 Web Audio 代码。
- Unity App 先用简单稳定的轮询 / HTTP 控制接入点歌服务，跑稳后再考虑 SSE、长连接或更复杂同步。
- 所有 Quest 3 音频相关变更都需要真机验证：麦克风权限、输入电平、返听延迟、混响舒适度、视频播放时的音频共存、削波和啸叫风险。

## WebXR 成果迁移分层

### 直接复用或保持服务边界

- PC helper 内容管线：`helper-runtime/`、`scripts/bilibili-helper.mjs`、`you-get`、`yt-dlp`、`imageio-ffmpeg`、`downloads/` 文件服务。
- 播放队列同步服务：`scripts/playlist-sync.mjs` 的队列状态、控制命令和下载调度。
- 播放队列 API 契约：`queue`、`currentIndex`、`playback`、`command`、`updatedAt`，以及 `play`、`pause`、`prev`、`next`、`replay`、`remove`。
- 输入来源规则：直链 `.mp4` / `.webm`、Bilibili BV/URL、YouTube URL/ID 的解析策略继续留在 Companion 或在线服务端。
- 产品边界和风险说明：单人本地原型、平台下载不保证成功、Cookie 和版权风险、本地下载文件不提交。

### 翻译成 Unity 实现

- 播放队列客户端：从 TypeScript `playlistClient.ts` 翻译为 C# `PlaylistClient`，通过配置的局域网 Companion 或在线服务连接队列。
- 视频大屏：从 `HTMLVideoElement + Three.js VideoTexture` 翻译为 Unity `VideoPlayer + RenderTexture`。
- 视频适配规则：保留安全显示区和按视频宽高比缩放的体验，但用 Unity mesh / material / RawImage 实现。
- 播放状态机：保留“当前歌曲未就绪、下载中、错误、ready、播放命令同步、视频结束后下一首”的行为。
- 房间体验：保留 KTV 包厢、大屏、沙发起点、茶几、墙面灯带、屏幕电平条等空间判断，用 Unity prefab 或脚本重建。
- 手柄角色：右手优先麦克风，左手荧光棒，grip 开关指针，左手 X/Y 切换荧光棒颜色。
- 演唱反馈：麦克风输入驱动灯光、电平、麦克风光环和荧光棒亮度。

### 应该重做

- 音频链路：Web Audio 只作为参数参考。Unity 版本以 Unity `Microphone` / `AudioSource` / 原生插件路线为准。
- VR 交互层：WebXR session、Three.js controller、raycaster、`VRButton` 不迁移，Unity 使用 OpenXR / Meta XR / 自有轻量交互层。
- UI：不直接迁移 DOM、CSS 或 JavaScript 运行时；在 Unity 中重做头显内可读的世界空间 UI，并把 WebXR 已验证的 Lucide 图标路径、品牌视觉和信息层级翻译为原生矢量组件。
- 浏览器 workaround：HTTPS、自签证书、自动播放手势、Quest Browser 限制等不再作为原生 App 的核心问题处理。
- 桌面预览面板：短期不在 Unity 中复刻，PC 侧继续使用 Web 项目作为点歌和下载入口。

### 暂缓迁移

- 持久歌单、批量内容管理、缓存清理 UI。
- 歌词导入和歌词偏移。
- 更多房间主题和复杂灯光模式。
- 录音回放、评分、练歌模式。
- 多人包厢和合唱。

这些功能仍然有价值，但应该排在 Unity 客户端成功接入点歌服务、稳定播放视频并保持低延迟返听之后。

## 当前状态

状态：V0.1 至 V0.7 的原生 Quest K 歌闭环已经完成；V0.75 语音输入与找歌、V0.76 拼音搜索与点歌交互也已完成第一版实现并部署到 Quest 与 Ubuntu 点歌服务。Unity 客户端现在支持文字、语音和拼音候选三种找歌入口，并继续复用同一套四行搜索结果、设备隔离队列和显式点播确认流程。V0.75 仍欠真实供应商横向评测与完整 P5 真机回归，V0.76 仍需持续回归软键盘、候选覆盖层和四档倾角下的射线命中；这些验证债务完成后，主线进入 V0.8 音频引擎打磨和安全。

已完成：

- 使用 Unity `6000.5.0f1` 创建工程。
- 完成 Quest 3 真机构建运行。
- 创建音频验证场景：`Assets/Scenes/AudioPrototype.unity`。
- Quest 麦克风权限申请可用。
- 麦克风输入电平和输出电平在 Quest 3 真机上可响应。
- Unity `Microphone` 到 `AudioSource` 的实时返听链路已经打通。
- KTV 风格测试预设可以明显听到混响。
- 加入基础安全降增益、输入/输出电平显示、DSP buffer 和麦克风延迟估算。
- 增加 Unity backend 与 Native Oboe dry backend 的 A/B 路线雏形。
- 修复自定义 Android Manifest 导致 APK 能安装但无法启动的问题，改回由 Unity 生成默认启动 Activity。
- 新增 `PlaylistClient`，可通过 HTTP 轮询 PC 端 `/api/playlist/state`。
- 新增 `QuestPlaylistPrototype`，在调试 UI 中显示 helper 连接、当前歌曲、队列、状态和 `playableUrl`。
- 支持从 Unity 向 `/api/playlist/control` 发送 `play`、`pause`、`prev`、`next` 和 `replay`。
- 支持在头显内输入 PC helper 局域网地址，并保存到 `PlayerPrefs`；避免 Quest 真机误用 `127.0.0.1` / `localhost`。
- 支持把 `/downloads/...` 相对地址解析为 PC 下载文件服务地址。
- 场景生成器已包含 V0.1 音频面板和 V0.2 PC Helper 面板。
- Android 构建预处理已固化 ARM64、GameActivity、OpenXR 基础配置、Internet 权限和本地 HTTP 访问设置。
- 新增 `QuestVideoScreenPrototype`，使用 Unity `VideoPlayer + RenderTexture` 创建 V0.3 视频大屏。
- 视频大屏可订阅 `QuestPlaylistPrototype.StateChanged`，在当前条目 ready 后解析并加载 `playableUrl`。
- 支持播放 PC helper 暴露的 MP4/WebM URL，包括 `/downloads/...` 和直接视频路径。
- 在 Android/Quest 路径上优先把远端视频缓存到 `Application.persistentDataPath/video-cache/` 后播放，降低直接 HTTP 流式播放的不确定性。
- 视频首帧和 prepare 完成后按实际宽高比适配屏幕安全区域。
- 根据 playlist `playback` 和 `command` 同步播放、暂停和重播；视频结束后可向 playlist 服务发送 `next`。
- 增加 V0.3 视频状态文本、probe/cache/prepare 诊断，以及 `Copy Debug` 按钮用于真机排查。
- 新增 `QuestAppShellPrototype`，把 V0.1/V0.2/V0.3 调试控件整理成面向头显使用的世界空间控制面板。
- 新增 V0.4 app 状态栏，显示 helper 连接、麦克风状态和运行平台。
- 新增 `Copy App Debug`，可复制平台、包名、版本、DSP buffer、playlist、音频状态、视频缓存和面板文本等诊断信息。
- `QuestUiPointer` 已升级为控制器射线 UI 指针：左右 grip 只切换对应射线显隐，左右扳机负责 UI 点击、按住和拖动，ABXY 不触发 UI 点击。
- `QuestXrBootstrap` 会确保主相机使用 HMD tracking，并记录 XR loader 状态。
- Android 构建预处理会固化应用名 `TsukiVox Quest`、GameActivity、New Input System、OpenXR loader / Quest Touch 控制器配置，并确保构建场景中存在 playlist、video screen、app shell 和 KTV 房间。
- 新增 `QuestKtvRoomPrototype`，按 WebXR 版本验证过的布局在脚本中程序化生成 V0.5 KTV 包厢：地板、四墙、软包墙板、黄铜饰条、后沙发、左右贵妃椅和茶几。
- 房间几何、灯光反馈和锚点分别挂在 Geometry / Feedback / Anchors 三个子根下，所有材质集中在一个命名调色板里，为以后可换房间主题保留结构。
- 视频大屏布局改为由房间下发：`QuestVideoScreenPrototype` 新增 `ApplyScreenLayout`，屏幕嵌入前墙黑色边框内；安全显示区与 matte 同尺寸(精确 16:9),常见 16:9 视频零黑边满屏,非 16:9 仍等比完整显示。
- 屏幕两侧新增麦克风电平条，天花板/侧墙灯带 emissive、screen glow 和 lounge glow 随麦克风输入电平响应（曲线参考 WebXR `feedback.ts`）。
- 用户默认位于沙发与茶几之间、正对大屏的世界原点；控制面板固定在茶几中央，物理平板与 Canvas 作为同一设备围绕铰链转动，坐姿下可按需要调整阅读角度。
- 墙上大屏底部状态/调试 overlay 默认隐藏；V0.65 将诊断入口收进茶几控制面板的 `设置 > 诊断与支持`，可按需展开原始详情并复制完整诊断。
- 新增 `QuestHandheldPropsPrototype`，使用 Unity 原生 mesh 为右手生成虚拟麦克风、为左手生成多色荧光棒；缺失某只手柄时隐藏对应道具。
- 左手 X/Y 循环切换荧光棒颜色，麦克风光环和荧光棒亮度随输入电平响应。
- 左右 grip 仅用于切换对应手柄射线，播放控制仅由左右扳机点击，ABXY 不参与 UI 点击。
- V0.6 已在 Quest 3 真机确认播放控制命中稳定，手持道具和反馈不会遮挡唱歌视线。
- 新增 `QuestConsumerUiPrototype`，把 V0.4 的调试控件重构为主页、人声、播放队列、设置和诊断抽屉五个面向普通用户的界面层级。
- 控制面板使用 `TextMeshProUGUI`、`TMP_InputField` 和 Noto Sans SC 动态多图集 SDF 字体，统一放大字号、安全边距、物理尺寸和世界空间 Canvas 渲染密度。
- 播放控制、预设、开关、滑杆、输入框、返回和关闭操作已通过控制器扳机交互；页面切换不再残留透明射线拦截层。
- 人声预设和角度档位使用明确的高对比选中态；Replay 图标使用与 VRSing 相同的 Lucide `RotateCcw` 几何，左上角品牌标记参考 VRSing favicon。
- 新增 `QuestTabletTiltController` 和茶几右下角独立角度开关，支持 0/30/60/90 度四档平滑动画，默认 30 度；底板、屏幕和 Canvas 共用 `Tablet Anchor` 并同步转动。
- 角度开关使用 TMP SDF 字体，选中、hover、按下和未选中状态均保持档位文字可见；档位选择可保存到新版 `PlayerPrefs` 键。
- 播放队列使用独立序号列与宽标题/状态区，避免歌曲名、序号和元信息重叠。
- 原始 Debug 不再漂浮在茶几角落或大屏上，诊断抽屉固定在控制面板内部；主页右上角导航只保留图标反馈，不显示多余 hover 文字。
- 新增头显内 Bilibili 搜索点歌页，可输入歌名、歌手或 BV 号，分页浏览结果，并把选中视频直接加入队列和立即播放。
- 新增并在 V0.76 重做 `QuestAndroidKeyboardInput`：世界空间 `TMP_InputField` 使用头显内软键盘输入字母、数字、符号和服务地址，支持大小写、光标、退格、清空、完成与取消；点击键盘时保持输入框焦点，不再依赖 Android 系统输入法。
- 点歌配置扩展为“局域网 Companion”和“在线服务”两种模式：Companion 分别使用 `5174/5175`，在线服务使用同一个完整 HTTP(S) origin 承载搜索、队列和媒体下载。
- 两种服务模式、Companion 地址和在线服务 origin 分别持久化；切换服务时会取消旧请求、重建 endpoint 并恢复轮询状态。
- 每台 Quest 首次启动会生成并持久化稳定设备 ID，所有点歌请求通过 `X-TsukiVox-Device-Id` 发送，为在线服务按设备隔离队列。
- 右手麦克风新增防贴脸触觉反馈：按麦克风网头到面部的间隙区分轻震和强震，并使用平滑、迟滞和节流避免震动抖动。
- 设置页新增“麦克风防碰撞”页面，可开关反馈、调整轻震/强震距离和震动强度、从当前姿势捕获阈值、恢复默认值；设置会持久化并进入完整诊断。
- 新增 `QuestCommandLineBuild` 和 `Tools/Deploy-Quest.ps1`，支持 Unity Editor 已打开时提交显式构建请求，或在 Editor 关闭时使用无界面 Unity 构建 APK。
- 自动部署流程使用 `adb install -r` 保留数据地覆盖安装，并通过构建收据、APK SHA-256、Android Package Manager `versionName` 和 `logcat` Build ID 核对精确版本；控制面板和诊断信息同步显示本地构建时间与 Build ID。
- 新增 `VoiceSearchRecorder`，从现有 Unity 麦克风环形缓冲旁路复制干声，完成 48 kHz 到 16 kHz 的短句采集、端点检测和内存 WAV 封装；录音期间只压低视频播放音量，不停止返听或修改增益、滤波与混响。
- `PlaylistClient`、`QuestPlaylistPrototype` 和 `QuestConsumerUiPrototype` 已接入 `/api/voice-search` 与 `/api/voice/provider`：按一下开始录音、说完自动提交、再次按下取消，显示电平、转写回执、八态文案和四个候选，失败按网络、服务、上游和配额分类。
- 腾讯云与 MiMo 识别供应商采用互斥单选而非自动故障转移；可在头显内切换，选择按稳定设备 ID 存在服务端，不会影响其他设备，云端密钥不会进入 Quest 客户端。
- 搜索页和播放队列新增清空操作，队列条目可逐项删除；启动清空使用 `clearAll`，而用户点击“清空待播”会保留当前播放条目。
- V0.76 新增 `/api/bilibili/suggest?term=...` 拼音候选：用户用头显内软键盘输入拼音并主动点击 Sparkles 候选按钮，客户端最多显示六条建议；选择建议只回填搜索框，仍需点击搜索并从结果中明确点 `+`。
- 搜索页新增可持久化的 `KTV` 关键词开关；启用后只在实际搜索请求中补充后缀，并避免重复追加。关闭语音找歌总开关后，语音入口和供应商控件会隐藏，文字/拼音搜索区域自动重排。
- 构建和部署收据统一记录带时区的本地时间。路线图功能版本已到 V0.76，但 `QuestBuildInfo.ProductVersion` 与 `ProjectSettings` 的 APK 产品版本前缀目前仍为 `0.65`，正式发布前需要统一版本标识。

已观察到的问题：

- 当前音效参数仍偏验证性质，不是最终舒适演唱参数。
- V0.65 已完成第一版普通用户控制面板，但后续新增页面或动态文案仍需在四档倾角和不同观看距离下回归文字清晰度、边距和射线命中。
- Oboe dry backend 可作为低延迟参考路径，但当前 KTV 混响和效果链仍主要在 Unity backend 中验证；Oboe 目前只导出电平而不导出 PCM，因此 V0.75 语音输入在该后端下会明确不可用。
- V0.3 已能加载并播放 ready 视频，但仍需要持续做 Quest 真机 + 当前所选点歌服务回归，确认不同来源和文件大小下的缓存、prepare 和首帧表现。
- Companion 地址和在线服务 origin 仍需手动输入，尚未做局域网扫描、二维码配对或更友好的连接向导；正式公网服务必须使用 HTTPS。
- 控制器射线 UI 需要继续在 Quest 真机上回归搜索页、头显内软键盘、拼音候选、服务切换、输入框、滑杆、开关、按钮、诊断抽屉、麦克风防碰撞页、四档倾角和不同手柄追踪状态。
- 头显内 Bilibili 搜索、点播和按设备隔离队列已完成客户端实现，但仍需随 Companion/在线服务协议变化持续回归搜索错误、重复请求、切换服务和断网恢复。
- V0.75 语音链路已在真机跑通，但真实供应商样本集、Top-4 命中率、伴奏底噪、配额/断网状态、连续 20 次点歌以及返听无退化等完整验收尚未结束；Bilibili 中文关键词检索限流仍会同时影响文字与语音结果。
- V0.76 拼音候选依赖 Bilibili suggest 上游；当前交互是用户主动获取候选，不是输入后自动防抖请求。选择候选也不会自动搜索或点播。
- `QuestPlaylistPrototype.ApplyServiceConfiguration` 当前调用的 `CancelCatalogRequests` 还没有包含 `CancelSuggestions`；切换 Companion/在线服务时存在旧候选请求回写新页面状态的风险，V0.76 收尾时应补齐并回归。
- 路线图版本与 APK 产品版本前缀尚未对齐：当前代码和 `ProjectSettings` 仍生成 `0.65-build.<Build ID>`，部署核验应继续以完整 Build ID 为准。
- 真实演唱体验已经进入“视频播放 + 返听 + 混响 + 头显音量”的组合评估阶段，但音频舒适度和反馈风险仍需继续打磨。

## V0.1 Quest 音频验证 Spike

目标：判断 Unity 原生路线是否足以支撑 Quest 3 上的实时演唱返听。

状态：核心通过，继续调参。

范围：

- 验证麦克风采集。
- 验证实时人声返听。
- 验证明显可听的混响 / echo 效果。
- 显示输入电平、输出电平、DSP buffer 估算和麦克风延迟估算。
- 提供 Dry、KTV Room、Strong KTV 和 Safe Small Room 预设。
- 保留音量安全提示和基础增益保护。

剩余工作：

- 把 KTV Room 从“明显有混响”调到“能唱、舒服、不过分”。
- 增加干声 / 湿声 A/B 对比按钮。
- 把预设参数迁移到 ScriptableObject 或配置资源，减少硬编码。
- 记录主观延迟标签：可唱、略别扭、明显拖拍、不可唱。
- 分别测试 Quest 内置扬声器、有线耳机、USB-C 音频设备；蓝牙音频如延迟过高，应明确不推荐。

验收标准：

- Quest 3 内置麦克风可稳定授权和启动。
- 用户能跟着唱而不被明显 slapback 延迟干扰。
- 正常说话和大声唱时电平健康，不容易立即削波。
- 中等头显音量下不应立即啸叫。

## V0.2 PC Helper 兼容客户端

目标：让 Unity App 接上 WebXR 项目的内容管线和播放队列服务。

状态：已完成第一版。Unity 不再只是独立音频原型，而是已经能读到旧项目里验证过的点歌系统状态，并向 PC playlist 服务发送基础控制。

已实现范围：

- 新增 Unity C# `PlaylistClient`，通过 HTTP 读取 PC 端 `/api/playlist/state`。
- 支持配置 helper / playlist 服务地址，例如 `http://192.168.x.x:5175`。
- 解析 `PlaylistState`、`PlaylistItem` 和 `PlaylistCommand`。
- 显示 helper 连接状态、当前歌曲、下载中、错误和 ready 状态。
- 支持从 Unity 向 `/api/playlist/control` 发送 `play`、`pause`、`prev`、`next`、`replay`。
- 第一版使用简单轮询，避免先处理 SSE 在 Unity/Quest 上的细节。
- 支持在 UI 中输入 PC IP、应用默认 PC 地址，并在重启后恢复上次 helper host。
- 支持把 ready 条目的 `playableUrl` 显示为 Quest 可访问的完整下载 URL。

验收状态：

- 代码侧已具备连接 PC helper、读取队列、显示 ready / downloading / error、解析 `playableUrl` 和发送基础控制的能力。
- 仍需要在每次改动后做 Quest 真机 + 局域网 PC helper 回归：添加 Bilibili、YouTube 或直链视频，确认 Unity App 能看到队列状态和 ready URL。
- helper 关闭、IP 错误或网络不可达时，Unity UI 应保持清楚提示并能在 helper 恢复后重新连接。

## V0.3 Unity 视频大屏与播放同步

目标：在 Unity 中复现 WebXR 版本最核心的大屏播放能力。

状态：已完成第一版。Unity 已经可以从 PC helper 队列读取当前 ready 条目，解析 `playableUrl`，在场景视频大屏上加载并播放 MP4/WebM，并和 playlist 控制状态做基础同步。

已实现范围：

- 使用 Unity `VideoPlayer + RenderTexture` 创建 KTV 大屏。
- 支持播放一个已知 URL 或 PC helper 暴露的 `/downloads/...` 文件。
- 根据视频宽高比适配大屏安全显示区。
- 从 `PlaylistClient` 获取当前 ready item 并加载 `playableUrl`。
- 根据 playlist 的 `playback` 和 `command` 同步播放、暂停、重播、上一首、下一首。
- 视频结束后向 playlist 服务发送 `next`。
- 验证视频音频与麦克风返听、混响共存。
- 支持对 HTTP/HTTPS 视频做 HEAD probe，并记录 Content-Type、Content-Length 和 Accept-Ranges。
- 在 Android/Quest 上缓存远端视频到 `Application.persistentDataPath/video-cache/`，再以 file URL 交给 `VideoPlayer` 播放。
- 提供视频状态文本和 `Copy Debug`，包含 playlist、URL 解析、缓存、VideoPlayer、RenderTexture 等诊断信息。

验收状态：

- 代码侧已具备 Quest 场景中播放 helper 提供的 MP4/WebM、显示下载中/错误/未 ready 状态、播放/暂停/重播同步和视频结束后 next 的能力。
- 已保留视频播放时同时启用麦克风返听和 KTV Room 预设的验证路径。
- 仍需要按设备回归记录不同来源视频的缓存耗时、首帧时间、播放稳定性，以及与麦克风返听/混响共存时的实际音量和反馈风险。

## V0.4 原生 Quest App 壳和头显内 UI

目标：把音频和视频验证整合成真正的 Quest App 基础壳。

状态：已完成第一版。V0.4 建立了世界空间 Canvas、控制器射线、App Debug 和构建时场景补全，是后续 V0.65 消费级控制面板的基础层；其面向用户的 UI 已由 V0.65 接管。

已实现范围：

- 配置 OpenXR / Meta XR 基础运行环境。
- 加入 XR camera rig。
- 加入控制器射线或近距离 UI 交互。
- 把当前调试 Canvas 替换为头显内可读的世界空间控制面板。
- 控制面板保留：helper 连接、当前歌曲、播放控制、麦克风状态、预设切换、监听音量、干湿比、安全状态。
- 固化 Quest 3 构建设置：ARM64、包名、应用名、Android min SDK、麦克风权限。
- 使用 `QuestAppShellPrototype` 组织 `Prototype Canvas`：分区显示 Connection、Now Playing、Mic 和 Debug。
- 使用 `QuestUiPointer` 从 Quest 控制器发射世界空间 UI 射线，并驱动 Button、InputField、Slider 和 Toggle。
- 使用 `QuestXrBootstrap` 确保 Main Camera 绑定 HMD tracking，并在启动时报告 XR loader 状态。
- 使用 `TsukiVoxClipboard` 统一 `Copy Debug` / `Copy App Debug` 在 Editor 和 Android 上的剪贴板复制。
- 在构建预处理里确保 build scene 自动补齐 playlist、video screen 和 app shell，避免旧场景漏掉 V0.4 组件。

验收状态：

- 代码侧已具备 Quest 头显内控制 helper 连接、播放、麦克风、音频预设和调试复制的基础壳。
- Android 构建设置已覆盖应用名、包名、ARM64、GameActivity、New Input System、OpenXR loader、Quest Touch 控制器和 Internet / HTTP 访问。
- 仍需要每次 UI/输入改动后在 Quest 3 真机上确认：App 从未知来源启动、面板可读可点、指针命中稳定、重新安装后麦克风权限和音频启动流程稳定。

## V0.5 最小 VR KTV 房间

目标：重建 WebXR 版本里最核心的 KTV 包厢体验，但先控制视觉复杂度。

状态：已完成第一版。房间观感、茶几位置、大屏布局和控制面板空间关系已经在 Quest 3 真机持续回归；后续重点关注完整演唱时的性能和音视频共存。

已实现范围：

- `QuestKtvRoomPrototype` 程序化生成简化 KTV 包厢：房间壳、软包墙板、饰条、后沙发、左右贵妃椅、茶几和大屏边框；尺寸与配色移植自 WebXR `ktvRoom.ts` / `materials.ts` 的验证布局。
- 用户进入后默认位于沙发前、正对大屏的位置（追踪原点即沙发起点）。
- 大屏嵌入前墙，由房间统一下发位置/朝向/安全区；matte 为 3.8×2.1375 m(16:9),安全区与 matte 同尺寸,16:9 视频满屏无黑边。
- 屏幕两侧电平条、天花板/侧墙灯带、screen glow 和 lounge glow 随麦克风输入电平响应。
- 世界空间控制面板保留为茶几上的点歌平板式入口（茶几 z=1.1），V0.65 已将固定 68° 布局升级为 0/30/60/90 度四档可调平板，默认 30 度。
- 墙上大屏调试 overlay 默认隐藏；V0.65 的诊断与原始信息固定在控制面板抽屉中，不作为常驻空间元素显示。
- 房间材质集中在命名调色板中，几何/反馈/锚点分层，为后续房间主题预留结构。
- 场景生成器与 Android 构建预处理会自动补齐房间组件。

早期观感微调（2026-07-06/07）：

- 茶几前移、控制面板跟随并加大后仰角,避免坐姿下遮挡大屏底边。
- 大屏黑边从 WebXR 0.8 安全缩放改为零边距 16:9 matte;调试信息从常显改为默认隐藏。

持续回归标准：

- 用户戴上 Quest 3 后进入一个可唱歌的小房间。
- 视频在大屏上播放(16:9 满屏、非 16:9 完整不裁切),音频返听和混响可用。
- 大屏调试信息默认不可见；V0.65 控制面板中的诊断抽屉和复制完整诊断入口可用。
- 房间灯光能随演唱电平变化。
- 性能在 Quest 3 上稳定，无明显掉帧或视频卡顿。

## V0.6 手柄麦克风、荧光棒和 VR 控制

目标：迁移 WebXR 原型中已经验证过的手柄角色体验。

状态：已完成第一版并通过 Quest 3 真机验收。

范围：

- 右手手柄显示为虚拟麦克风。
- 左手手柄显示为荧光棒。
- 麦克风和荧光棒模型使用 Unity 原生 mesh / prefab 重建。
- 荧光棒保留多色切换，左手 X/Y 或等价按键切换颜色。
- 左右 grip 只负责开关对应手柄射线；左右扳机负责点击 VR 内播放控制，ABXY 不参与 UI 点击。
- 麦克风光环、荧光棒亮度和房间灯光响应输入电平。

验收标准：

- 手柄角色分配符合预期：右手麦克风，左手荧光棒。
- 没有右手或左手时有合理 fallback。
- VR 内播放控制命中稳定。
- 荧光棒和麦克风反馈不会遮挡唱歌视线。

验收状态：

- Quest 3 真机已确认右手麦克风、左手荧光棒角色分配符合预期；缺失手柄时会隐藏对应道具。
- Quest 3 真机已确认 VR 内播放控制命中稳定。
- Quest 3 真机已确认荧光棒和麦克风反馈不会遮挡唱歌视线。
- 输入职责已分离：grip 仅切换射线，扳机仅负责 UI 点击，左手 X/Y 仅负责荧光棒换色。

## V0.65 面向普通用户的茶几控制面板

目标：把 V0.4 留下的工程调试壳升级为普通用户可以直接理解、看清和操作的沉浸式茶几平板，同时保留但隐藏完整诊断能力。

状态：已完成。控制面板经过多轮 Quest 3 截图与扳机交互回归，主页、子页面、四档倾角、选中态、中文清晰度和诊断入口已经形成稳定的第一版用户体验。

已实现范围：

- 移除茶几杯子等干扰物，将放大的控制面板和平板底板放在茶几中央，保持大屏仍是房间主视觉。
- 新增主页、人声、播放队列、设置四个主页面；常用播放和麦克风操作留在主页，设置与诊断进入次级页面。
- 使用 Noto Sans SC 动态多图集 TMP SDF 字体替换旧版 UI Text，提升中文在 Quest 世界空间 Canvas 中的边缘清晰度。
- 统一扩大字号、按钮、内容安全边距和面板物理尺寸，修复文字贴边、队列信息重叠、底部状态间距和小字号虚化。
- 修复页面动画期间 `CanvasGroup` 射线状态，确保扳机可以点击返回、播放、人声预设、滑杆、开关、PC IP 和诊断按钮。
- 使用高对比强调色表达播放、人声预设和倾角档位的选中状态；按钮 hover/按下动画不清空或遮蔽标签。
- 新增茶几右下角四档角度开关：0° 平放、30° 低角、60° 阅读、90° 直立；默认 30°，档位切换使用平滑缓动和完成触觉反馈。
- 平板底板、屏幕与 Canvas 挂在同一个 `Tablet Anchor` 下；运行时会修复错误父子关系和局部旋转，避免屏幕固定偏移或只转动其中一层。
- 播放控制图标采用 Unity 原生矢量绘制，Replay 对齐 VRSing 的 Lucide `RotateCcw`；品牌标记采用 VRSing favicon 的黑色圆角底、青色圆环、白色月点和暖色弧线语言。
- Debug 固定为控制面板内的 `设置 > 诊断与支持` 抽屉，默认隐藏；可查看健康摘要、服务和视频状态，展开原始详情并复制完整诊断信息。
- 精简主页右上角导航 hover，只保留图标的视觉反馈；缩小子页面返回底板和诊断关闭底板，并与分隔线保持稳定间距。

验收状态：

- Quest 3 已确认主页和各子页面可通过控制器扳机进入、操作和返回。
- Quest 3 已确认中文主面板清晰度、预设高亮、Replay 图标、文字间距、队列布局和诊断抽屉达到当前版本要求。
- Quest 3 已确认底板和屏幕能够同步转动；代码侧已进一步消除指针初始化造成的固定角度偏差，并保证角度档位标签不会在 hover 后消失。
- Android 目标 Unity 编译和脚本差异检查已通过；后续涉及 Canvas 层级、字体、射线或页面布局的改动必须继续做四档倾角真机回归。

## V0.7 头显内点歌与交付闭环

目标：让用户不再依赖 PC 页面完成日常点歌，并让局域网原型可以平滑切换到在线服务；同时补齐麦克风近脸保护和可核验的 Quest 自动交付流程。

状态：已完成第一版。V0.65 之后完成的头显内搜索点歌、双服务配置、设备队列隔离、麦克风防贴脸触觉和自动部署共同组成 V0.7，不再把这些改动视为 V0.65 的零散补丁。

已实现范围：

- 在主页新增搜索点歌入口，在头显内完成 Bilibili 关键词/BV 号搜索、分页、结果状态展示、加入队列和立即播放。
- `PlaylistClient` 接入 `/api/bilibili/search` 和点播接口，并处理超时、服务错误、无结果、重复请求及较旧队列状态回写。
- V0.7 最初使用 `QuestAndroidKeyboardInput` 桥接 Quest Android 输入，V0.76 已把它重做为头显内世界空间软键盘，继续服务搜索词和服务地址输入。
- 设置页新增独立点歌服务页面，可在局域网 Companion 与在线服务之间切换，并分别保存服务模式和地址。
- Companion 保持 playlist `5175`、download/search `5174` 的双 origin；在线服务使用一个完整 HTTP(S) origin 同时提供搜索、队列控制和媒体下载。
- 生成并持久化稳定的设备 ID，通过 `X-TsukiVox-Device-Id` 附加到请求，让服务端可以隔离不同 Quest 设备的队列。
- 右手麦克风根据网头表面与面部的实际间隙提供渐强触觉反馈；轻震/强震阈值、震动强度、开关和校准结果均可在设置页调整并持久化。
- 设置和完整诊断显示麦克风当前间隙、阈值、触觉状态、服务模式、连接状态和构建信息，便于真机排查。
- 自动部署脚本统一完成 APK 构建、`adb install -r`、Package Manager `versionName` 核验、应用启动和 `logcat` Build ID 核验，并输出构建/部署收据。
- Unity Editor 已打开时由脚本提交一次显式构建请求；Editor 关闭时使用无界面 Unity。另提供 `-BuildOnly` 和 `-InstallOnly`，无需开发者手动切换流程。

验收状态：

- 客户端已具备从搜索到点播、队列轮询、播放控制和媒体 URL 解析的头显内闭环；Companion 和在线服务共用同一套用户界面。
- 服务模式、各自地址和设备 ID 可跨重启恢复，切换服务后会重置旧状态并连接新的 endpoint。
- 麦克风防贴脸反馈具备默认参数、用户校准、强度控制、关闭选项和完整诊断，不影响原有 X/Y 换色及 grip/扳机职责。
- 自动部署把构建失败、ADB 设备异常、安装失败和版本不匹配作为硬失败；Quest 控制器检查阻止无人值守启动时，会单独报告“精确版本已安装、运行时启动待头显唤醒”。
- 后续点歌或输入改动仍必须在 Quest 3 上回归头显内软键盘、拼音候选、搜索分页、点播错误、服务切换、不同设备队列隔离和断网恢复。

## V0.75 语音输入与找歌

目标：让用户在不改动既有返听处理链的前提下，说出歌名、歌手或版本要求，并在头显内得到可明确确认的点播候选。

状态：第一版已实现并部署，Quest 真机已跑通语音链路；真实供应商横向评测与 P5 完整真机回归尚未结束。详细设计与剩余检查见 `Docs/VoiceSearch/voice-search-implementation.md`。

已实现范围：

- `VoiceSearchRecorder` 只读复制 `QuestAudioPrototype` 的 Unity 麦克风干声环形缓冲，使用独立 7 秒缓冲、48 kHz 到 16 kHz 单声道降采样、800 ms 尾部静音端点、2.5 秒前置静音判定和 6 秒硬上限，并在内存中封装 16-bit WAV。
- 录音期间把视频播放音量临时压到约 15%，但不中断麦克风、返听或当前人声预设；封装上传 payload 后立即清零录音器采集缓冲，payload 只在请求期间留在内存，不写入 `persistentDataPath` 或日志。
- `PlaylistClient` 通过 `POST /api/voice-search` 上传 WAV 和实际音频时长，继续携带稳定的 `X-TsukiVox-Device-Id`，并使用结构化 `code` / `retryable` 错误模型区分网络、服务、上游限流与配额。
- 服务端完成 WAV 校验、短句识别、口语查询规整、当前设备队列最近 20 项热词、Bilibili 检索、每设备并发/分钟/每日配额和全局日预算熔断；音频不落盘，转写不写入队列历史或设备状态。
- 搜索页提供一次点击开始、自动停句、再次点击取消、18 根电平条、“听到”只读回执、开始/提交触觉反馈和八态文案；成功后复用现有四行结果，仍由用户点 `+`，不会自动点播。
- `设置 > 点歌服务` 提供语音总开关以及腾讯云/MiMo 供应商选择。每次识别只调用所选供应商，不因请求失败自动回退；每台设备的选择保存在服务端设备状态中，未选择或已选供应商后来失去配置时使用服务端默认值。
- 语音总开关关闭后入口、状态、电平和供应商控件全部隐藏，不采集也不上传；搜索页会给文字与拼音入口重新排版。
- Native Oboe dry backend 目前没有 PCM 出口，启用时语音入口会不可用并显示原因，不会为了录音临时重启或切换音频后端。

待完成验收：

- 当前 `TsukiVox_Server` 的 21 项自动化测试全部通过，已覆盖语音格式、配额、互斥供应商、按设备偏好、无自动故障转移、热词和签名；下面的真实供应商与 Quest 场景验收仍需人工完成。
- 用约 50 条普通话、中英混合、日英专名、粤语、版本词和口语包装真机样本，对腾讯云与 MiMo 记录 Top-4/Top-1 命中率、时延、成本和失败率。
- 在伴奏播放中验证返听不断音、无爆音、无可感知延迟或音质退化，并覆盖断网、无语音、配额耗尽、供应商不可用和麦克风权限状态。
- 在 0/30/60/90 度下验证录音按钮和候选结果可命中，连续完成 20 次语音点歌且不误点播。
- 解决或规避 Bilibili 中文关键词搜索限流；识别成功但目录检索失败仍不能算语音找歌成功。

## V0.76 拼音搜索与点歌交互优化

目标：在 Quest 不依赖系统中文输入法的情况下，用拼音快速得到中文搜索词，同时让搜索、语音设置和键盘射线交互更稳定、可预测。

状态：第一版已实现并部署。`Docs/pinyin-suggest-deployment.md` 记录了最初部署，但其中“输入 2 个字符后 300 ms 自动建议”已经过时；当前代码以显式候选按钮为准。

已实现范围：

- 点歌服务新增 `GET /api/bilibili/suggest?term=...`，代理 Bilibili suggest 上游并保留限流、超时和上游不可用错误；Unity 网络层、业务层和 `SuggestStateChanged` 事件已完整接入。
- 搜索页使用 Sparkles 图标的独立候选按钮。用户输入任意非空拼音后主动请求建议，键盘关闭后最多显示六行；重新聚焦编辑或执行正式搜索时会取消并清空旧建议。
- 点击候选只把中文建议回填到 `TMP_InputField` 并关闭候选层，不自动发起搜索；正式搜索仍使用 Search 按钮，点播仍使用结果行的 `+`。
- `QuestAndroidKeyboardInput` 从 Android 原生输入桥接重做为世界空间软键盘，支持字母、大小写、数字/符号、空格、光标、退格、清空、完成和取消。`QuestUiPointer` 在点击软键盘时不改走输入框选中目标，从而保持焦点和连续输入。
- 搜索页新增持久化 `KTV` 开关。开启时搜索请求自动补 ` KTV`，已经以 `KTV` 结尾的查询不会重复追加；候选请求始终使用输入框原文。
- 搜索列表启动时保持为空，默认 BV 号只保留在输入框；用户必须主动请求候选或搜索。语音关闭时相关布局会隐藏并为文字/拼音区域让出空间。
- 点歌服务页重新整理地址、恢复默认、语音总开关和供应商控件布局；构建时间与部署收据统一改用带时区的本地时间。

验收状态：

- 服务端部署记录已验证健康检查及 `chunriy` 到“春日影”等候选响应，Unity APK 已完成构建、覆盖安装和 Package Manager 版本核验。
- 当前服务端 21 项自动化测试通过，但测试目录尚未覆盖 `/api/bilibili/suggest`；V0.76 目前依赖部署时的实网接口检查，后续应补离线可重复的建议接口测试。
- 仍需持续真机回归软键盘的完成/取消/焦点恢复、候选按钮和六行覆盖层的射线命中、候选后搜索、`KTV` 后缀开关持久化，以及四档倾角和语音开关两种布局。
- 当前 APK 产品版本前缀仍为 `0.65`；在 `QuestBuildInfo.ProductVersion` 与 `ProjectSettings` 正式升级前，V0.76 表示功能路线图版本，精确安装版本仍以 Build ID 为准。

## V0.8 音频引擎打磨和安全

目标：把可用的音频链路打磨到更接近真实 KTV / SingRoom 的演唱体验。

范围：

- 建立清晰的增益结构：输入增益、监听增益、干声增益、湿声增益、limiter ceiling。
- 优化动态处理：noise gate、compressor、makeup gain、soft limiter。
- 优化混响：early reflections、plate / hall 风格、短 KTV 房间预设、强效果预设。
- 增加 Quest 扬声器反馈保护。
- 增加 clipping、过热输入和安全降增益提示。
- 增加校准流程：正常说话、大声唱、自动建议输入/输出音量。
- 增加每次测试的屏幕摘要或日志。

验收标准：

- KTV Room 预设明显但不过分。
- Strong KTV 预设可用于压力测试。
- 正常演唱时不频繁触发安全降增益。
- 用户能完成一首歌，不被明显延迟、爆音、啸叫或刺耳混响打断。

## V0.9 歌词和演唱辅助

目标：让体验更接近真正 KTV。

范围：

- 支持导入或关联 `.lrc` 歌词。
- 在大屏下方或前方显示当前行和下一行歌词。
- 支持歌词时间偏移调整。
- 播放队列条目可关联歌词文件。
- 歌词位置不能遮挡视频主体。

验收标准：

- 歌词同步足够支撑演唱。
- 用户可以在头显中调整歌词偏移。
- 没有歌词时不影响正常视频播放。

## V0.95 歌单持久化与内容管理

目标：让 TsukiVox 可以作为可重复使用的个人 KTV 工具。

范围：

- 继续优先在点歌服务端实现持久歌单，而不是把内容库塞进 Quest App。
- Companion 增加本地持久歌单文件，例如 `playlist.json` 或 `data/playlists/*.json`；在线服务按稳定设备 ID 保存队列。
- 保存歌曲标题、来源类型、原始链接、BVID/YouTube ID、本地下载文件、下载时间和可选封面。
- 支持启动时恢复上次歌单。
- 支持清理失效下载文件和孤立下载文件。
- 支持重新下载、替换源、删除歌曲和批量导入。

验收标准：

- 重启当前所选点歌服务后歌单不丢失。
- Quest App 连接后能看到恢复后的歌单和当前歌曲。
- 下载文件堆积有基本清理方式。

## 音频升级分支：Oboe / AAudio

目标：如果 Unity 内置音频在完整 K 歌场景中不够稳定或延迟回退，则切换关键音频路径。

触发条件：

- 接入视频、XR UI 和房间后，Unity `Microphone` 返听重新变得明显拖拍。
- Unity 音频链路无法在 Quest 3 上稳定避免过高 buffer 或不可接受延迟。
- 需要更接近专业声卡 / SingRoom 的低延迟 callback 控制。

范围：

- 构建 Android 原生插件，使用 Oboe / AAudio。
- 使用 48 kHz 低延迟 callback 处理。
- 在原生层实现输入增益、压缩、混响 send、limiter、输出增益。
- 暴露 C# 控制接口，用于 Unity UI 调预设和显示电平。
- Unity 继续负责渲染、UI、房间和交互，只替换关键音频路径。

验收标准：

- 往返延迟显著低于 Unity `Microphone` 路线。
- 主观演唱体验更接近 SingRoom / KTV。
- App 暂停、恢复、权限变化后音频仍稳定。

## V1.0 个人 Quest KTV MVP

目标：交付一个稳定的个人 VR KTV App。

预期能力：

- Quest 3 原生 App。
- 局域网 Companion 或在线服务点歌、下载和播放队列控制。
- KTV 房间和视频大屏。
- 实时人声返听和调好的 KTV 混响。
- 控制器麦克风 / 荧光棒交互。
- 基础歌词显示。
- 安全增益和反馈保护。
- 清晰的安装、构建、连接和排障文档。

## 当前优先级

V0.75 与 V0.76 已把语音和拼音输入接到现有头显内点歌闭环。下一步先收掉两个中间版本的验收债务，再进入 V0.8：

1. 完成 V0.75 P1/P5：真实供应商样本评测、伴奏底噪、错误/配额状态、连续 20 次点歌和返听无退化回归，并继续处理 Bilibili 中文关键词限流。
2. 完成 V0.76 真机回归：世界空间软键盘、显式拼音候选、候选回填后搜索、`KTV` 开关、语音开关两种布局以及 0/30/60/90 度射线命中。
3. 在正式发布前把 `QuestBuildInfo.ProductVersion`、`ProjectSettings` 和路线图版本统一；在此之前继续用完整 Build ID 核验 APK，不把 `0.65` 前缀误报为 V0.76 版本号。
4. 开始 V0.8：梳理增益结构、动态处理、混响参数和反馈保护，优先改善完整演唱体验；不得让 V0.75 的旁路采集改变现有低延迟返听。
5. 把 V0.7 的双服务配置、稳定设备 ID、麦克风防贴脸触觉与精确版本部署，以及 V0.75/V0.76 的语音/拼音输入行为作为新的功能基线。
6. 继续保护 V0.3 视频大屏、V0.5/V0.6 房间与手持交互、V0.65 消费级控制面板，后续功能不得暴露常驻 Debug、降低中文清晰度、破坏四档倾角或造成按钮不可点击。

TsukiVox Unity 已经进入“可以在头显内用文字、语音或拼音候选找歌，并可重复交付精确构建”的阶段。

## 待决策问题

- 第一版正式可用版本默认选择局域网 Companion 还是在线服务，以及在线服务不可用时是否自动回退。
- Unity 客户端连接 Companion 的配置方式：继续手动输入，还是增加局域网扫描、二维码或配置文件。
- Companion/在线服务 API 是否需要增加统一、明确的 health endpoint 和协议版本字段。
- V0.75 正式版本默认使用腾讯云还是 MiMo；需要以真实 Quest 样本的 Top-4 命中率、时延和成本决定，而不是恢复不可归因的自动回退。
- Native Oboe 后端何时增加只读 PCM 出口，使语音输入不再局限于 Unity 麦克风后端。
- 拼音候选是否继续保持显式按钮触发，还是在上游限流和请求取消策略验证充分后恢复输入防抖自动请求。
- 视频文件分辨率默认策略是否继续沿用 WebXR 版本的 Bilibili 480P、YouTube H.264 720P 上限，以及是否需要限制单个缓存文件大小。
- V0.5 房间已选择脚本生成的极简几何；何时引入 prefab / ProBuilder / 美术资产升级房间观感仍待定。
- 蓝牙音频是否因为延迟过高而明确标记为不支持。
- Oboe / AAudio 是保留为备用分支，还是在 Unity 路线跑稳后继续作为高级音频引擎投入。
