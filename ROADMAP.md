# TsukiVox Unity 路线图

## 当前定位

TsukiVox 正从已经较成熟的 WebXR 原型迁移到 Unity 原生 Quest 应用。迁移原因不是产品方向变化，而是 Quest Browser / Web Audio 在实时人声返听和 KTV 混响上的延迟不可控，无法稳定支撑演唱体验。

当前目标是做一个面向 Quest 3 的单人原生 K 歌客户端：

- 收音使用 Quest 3 内置麦克风。
- Quest 手柄只承担视觉和交互角色，例如右手麦克风、左手荧光棒、控制指针。
- 多人房间、账号、云端歌单和跨设备同步都属于远期计划，现阶段完全不纳入设计压力。
- PC 端继续负责内容获取和点歌服务，Unity Quest App 专注低延迟音频、视频播放、VR 房间和头显内交互。

WebXR 项目不是被废弃，而是作为迁移源。接下来要复用它已经验证过的内容管线、播放队列契约、交互判断和视觉体验，同时重做那些被浏览器平台限制影响的实现。

## 迁移原则

- 优先保护已经在 Quest 3 上主观满意的低延迟返听，不因为接入视频、UI 或房间表现而破坏音频链路。
- 先迁移“能唱一首歌”的核心闭环，再恢复 WebXR 版本里的完整包厢表现。
- 复用 Web 项目的 PC helper 和播放队列服务，不在 Quest App 内实现 Bilibili / YouTube 下载器。
- 迁移体验和协议，不机械搬运 Three.js、WebXR、DOM、CSS 或 Web Audio 代码。
- Unity App 先用简单稳定的轮询 / HTTP 控制接入 helper，跑稳后再考虑 SSE、长连接或更复杂同步。
- 所有 Quest 3 音频相关变更都需要真机验证：麦克风权限、输入电平、返听延迟、混响舒适度、视频播放时的音频共存、削波和啸叫风险。

## WebXR 成果迁移分层

### 直接复用或保持服务边界

- PC helper 内容管线：`helper-runtime/`、`scripts/bilibili-helper.mjs`、`you-get`、`yt-dlp`、`imageio-ffmpeg`、`downloads/` 文件服务。
- 播放队列同步服务：`scripts/playlist-sync.mjs` 的队列状态、控制命令和下载调度。
- 播放队列 API 契约：`queue`、`currentIndex`、`playback`、`command`、`updatedAt`，以及 `play`、`pause`、`prev`、`next`、`replay`、`remove`。
- 输入来源规则：直链 `.mp4` / `.webm`、Bilibili BV/URL、YouTube URL/ID 的解析策略继续留在 PC 端。
- 产品边界和风险说明：单人本地原型、平台下载不保证成功、Cookie 和版权风险、本地下载文件不提交。

### 翻译成 Unity 实现

- 播放队列客户端：从 TypeScript `playlistClient.ts` 翻译为 C# `PlaylistClient`，通过局域网连接 PC helper。
- 视频大屏：从 `HTMLVideoElement + Three.js VideoTexture` 翻译为 Unity `VideoPlayer + RenderTexture`。
- 视频适配规则：保留安全显示区和按视频宽高比缩放的体验，但用 Unity mesh / material / RawImage 实现。
- 播放状态机：保留“当前歌曲未就绪、下载中、错误、ready、播放命令同步、视频结束后下一首”的行为。
- 房间体验：保留 KTV 包厢、大屏、沙发起点、茶几、墙面灯带、屏幕电平条等空间判断，用 Unity prefab 或脚本重建。
- 手柄角色：右手优先麦克风，左手荧光棒，grip 开关指针，左手 X/Y 切换荧光棒颜色。
- 演唱反馈：麦克风输入驱动灯光、电平、麦克风光环和荧光棒亮度。

### 应该重做

- 音频链路：Web Audio 只作为参数参考。Unity 版本以 Unity `Microphone` / `AudioSource` / 原生插件路线为准。
- VR 交互层：WebXR session、Three.js controller、raycaster、`VRButton` 不迁移，Unity 使用 OpenXR / Meta XR / 自有轻量交互层。
- UI：DOM、CSS、lucide 图标体系不迁移，Unity 里重做头显内可读的世界空间 UI。
- 浏览器 workaround：HTTPS、自签证书、自动播放手势、Quest Browser 限制等不再作为原生 App 的核心问题处理。
- 桌面预览面板：短期不在 Unity 中复刻，PC 侧继续使用 Web 项目作为点歌和下载入口。

### 暂缓迁移

- 持久歌单、批量内容管理、缓存清理 UI。
- 歌词导入和歌词偏移。
- 更多房间主题和复杂灯光模式。
- 录音回放、评分、练歌模式。
- 多人包厢和合唱。

这些功能仍然有价值，但应该排在 Unity 客户端成功接入 PC helper、稳定播放视频并保持低延迟返听之后。

## 当前状态

状态：V0.1 Quest 3 音频验证、V0.2 PC Helper 兼容客户端、V0.3 Unity 视频大屏与播放同步、V0.4 原生 Quest App 壳和头显内 UI、V0.5 最小 VR KTV 房间都已经完成第一版。Unity 客户端已经具备“进入 KTV 包厢、读 PC 点歌队列、在房间大屏播放当前 ready 视频、麦克风返听驱动房间灯光”的可演示闭环。下一步主线是 V0.6 手柄麦克风、荧光棒和 VR 控制。

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
- `QuestUiPointer` 已升级为控制器射线 UI 指针，支持 trigger / primary button / grip 点击、hover、press、drag 和可视射线/reticle。
- `QuestXrBootstrap` 会确保主相机使用 HMD tracking，并记录 XR loader 状态。
- Android 构建预处理会固化应用名 `TsukiVox Quest`、GameActivity、New Input System、OpenXR loader / Quest Touch 控制器配置，并确保构建场景中存在 playlist、video screen、app shell 和 KTV 房间。
- 新增 `QuestKtvRoomPrototype`，按 WebXR 版本验证过的布局在脚本中程序化生成 V0.5 KTV 包厢：地板、四墙、软包墙板、黄铜饰条、后沙发、左右贵妃椅和茶几。
- 房间几何、灯光反馈和锚点分别挂在 Geometry / Feedback / Anchors 三个子根下，所有材质集中在一个命名调色板里，为以后可换房间主题保留结构。
- 视频大屏布局改为由房间下发：`QuestVideoScreenPrototype` 新增 `ApplyScreenLayout`，屏幕嵌入前墙黑色边框内并保留安全显示区与宽高比适配。
- 屏幕两侧新增麦克风电平条，天花板/侧墙灯带 emissive、screen glow 和 lounge glow 随麦克风输入电平响应（曲线参考 WebXR `feedback.ts`）。
- 用户默认位于沙发与茶几之间、正对大屏的世界原点；V0.4 控制面板移到茶几上方，倾斜成点歌平板式布局，不遮挡大屏。

已观察到的问题：

- 当前音效参数仍偏验证性质，不是最终舒适演唱参数。
- V0.4 UI 已经从散乱调试面板收敛为头显内控制壳，但仍不是最终 KTV 房间内的沉浸式界面。
- Oboe dry backend 可作为低延迟参考路径，但当前 KTV 混响和效果链仍主要在 Unity backend 中验证。
- V0.3 已能加载并播放 ready 视频，但仍需要持续做 Quest 真机 + PC helper 回归，确认不同来源和文件大小下的缓存、prepare 和首帧表现。
- helper 地址仍是手动输入 / 默认 IP 配置，尚未做局域网扫描、二维码配对或更友好的连接向导。
- 控制器射线 UI 需要继续在 Quest 真机上回归输入框、滑杆、开关、按钮和不同手柄追踪状态。
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

状态：已完成第一版。V0.4 已经把音频、PC helper、视频大屏和调试信息收敛到可在 Quest 头显内使用的世界空间控制面板，并补上控制器射线、App Debug 和构建时场景补全。

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

状态：已完成第一版，等待 Quest 3 真机回归。

已实现范围：

- `QuestKtvRoomPrototype` 程序化生成简化 KTV 包厢：房间壳、软包墙板、饰条、后沙发、左右贵妃椅、茶几和大屏边框；尺寸与配色移植自 WebXR `ktvRoom.ts` / `materials.ts` 的验证布局。
- 用户进入后默认位于沙发前、正对大屏的位置（追踪原点即沙发起点）。
- 大屏嵌入前墙，由房间统一下发位置/朝向/安全区，继续播放来自 PC helper 的当前歌曲。
- 屏幕两侧电平条、天花板/侧墙灯带、screen glow 和 lounge glow 随麦克风输入电平响应。
- V0.4 控制面板保留为茶几上的点歌平板式入口，主要体验在 VR 空间内完成。
- 房间材质集中在命名调色板中，几何/反馈/锚点分层，为后续房间主题预留结构。
- 场景生成器与 Android 构建预处理会自动补齐房间组件。

验收标准（需真机回归确认）：

- 用户戴上 Quest 3 后进入一个可唱歌的小房间。
- 视频在大屏上播放，音频返听和混响可用。
- 房间灯光能随演唱电平变化。
- 性能在 Quest 3 上稳定，无明显掉帧或视频卡顿。

## V0.6 手柄麦克风、荧光棒和 VR 控制

目标：迁移 WebXR 原型中已经验证过的手柄角色体验。

范围：

- 右手手柄显示为虚拟麦克风。
- 左手手柄显示为荧光棒。
- 麦克风和荧光棒模型使用 Unity 原生 mesh / prefab 重建。
- 荧光棒保留多色切换，左手 X/Y 或等价按键切换颜色。
- grip 开关控制指针，用于点击 VR 内播放控制。
- 麦克风光环、荧光棒亮度和房间灯光响应输入电平。

验收标准：

- 手柄角色分配符合预期：右手麦克风，左手荧光棒。
- 没有右手或左手时有合理 fallback。
- VR 内播放控制命中稳定。
- 荧光棒和麦克风反馈不会遮挡唱歌视线。

## V0.7 音频引擎打磨和安全

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

## V0.8 歌词和演唱辅助

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

## V0.9 歌单持久化与内容管理

目标：让 TsukiVox 可以作为可重复使用的个人 KTV 工具。

范围：

- 继续优先在 PC helper 侧实现持久歌单，而不是把内容库塞进 Quest App。
- 增加本地持久歌单文件，例如 `playlist.json` 或 `data/playlists/*.json`。
- 保存歌曲标题、来源类型、原始链接、BVID/YouTube ID、本地下载文件、下载时间和可选封面。
- 支持启动时恢复上次歌单。
- 支持清理失效下载文件和孤立下载文件。
- 支持重新下载、替换源、删除歌曲和批量导入。

验收标准：

- 重启 PC helper 后歌单不丢失。
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
- PC helper 点歌、下载和播放队列控制。
- KTV 房间和视频大屏。
- 实时人声返听和调好的 KTV 混响。
- 控制器麦克风 / 荧光棒交互。
- 基础歌词显示。
- 安全增益和反馈保护。
- 清晰的安装、构建、连接和排障文档。

## 当前优先级

V0.5 第一版已经落地，当前最重要的切片是真机回归加 V0.6：

1. 在 Quest 3 真机回归 V0.5：房间可读性、默认站位、控制面板可点、大屏播放、灯光电平响应、帧率稳定，以及视频播放 + 返听 + 混响共存。
2. 保持现有 Quest 3 低延迟返听链路可用。
3. 保持 V0.2 的 PC helper / playlist API 接入可回归。
4. 保持 V0.3 的 `VideoPlayer + RenderTexture` 视频大屏、远端缓存和播放同步可回归。
5. 开始 V0.6：右手手柄麦克风、左手荧光棒、grip 指针开关和输入电平驱动的手持道具反馈。

完成真机回归后，TsukiVox Unity 才真正从“头显内可操作的技术原型”进入“有 KTV 空间感的 Quest K 歌客户端”阶段。

## 待决策问题

- 第一版正式可用版本是否默认依赖 PC helper 常驻。
- Unity 客户端连接 helper 的地址配置方式：手动输入、局域网扫描，还是二维码/配置文件。
- helper API 是否需要为 Unity 增加更明确的 health endpoint。
- 视频文件分辨率默认策略是否继续沿用 WebXR 版本的 Bilibili 480P、YouTube H.264 720P 上限，以及是否需要限制单个缓存文件大小。
- V0.5 房间已选择脚本生成的极简几何；何时引入 prefab / ProBuilder / 美术资产升级房间观感仍待定。
- 蓝牙音频是否因为延迟过高而明确标记为不支持。
- Oboe / AAudio 是保留为备用分支，还是在 Unity 路线跑稳后继续作为高级音频引擎投入。
