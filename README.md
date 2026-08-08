# TsukiVox Unity

TsukiVox 是面向 Meta Quest 3 的原生单人 VR K 歌客户端。项目从 WebXR 原型迁移到 Unity，核心原因是 Quest Browser / Web Audio 的实时人声返听与混响延迟不够稳定。

当前版本为 **V0.7 - 头显内点歌与交付闭环**。客户端已经不是单一音频 Spike，而是可以进入 KTV 包厢、在头显内搜索点歌、播放视频、实时返听人声、操作茶几控制面板，并通过自动化脚本构建和覆盖安装的完整原型。

## 当前能力

- 使用 Quest 3 内置麦克风进行实时返听，提供 Dry Reference、KTV Room、Strong KTV 和 Safe Small Room 四档预设。
- 使用 `VideoPlayer + RenderTexture` 在房间大屏播放 ready 视频；在线服务通过 HTTP Range 流式播放，Companion 保留远端文件缓存，再按实际宽高比显示。
- 程序化生成单人 KTV 包厢、沙发、茶几、大屏、灯带和麦克风电平反馈。
- 右手手柄显示为麦克风，左手显示为多色荧光棒；灯光和道具亮度响应输入电平。
- 茶几平板提供主页、搜索点歌、人声、播放队列、设置、点歌服务、麦克风防碰撞和诊断界面。
- 可在头显内搜索 Bilibili 视频、翻页并直接加入当前设备的播放队列。
- 支持“局域网 Companion”和“在线服务”两种点歌配置，并为每台 Quest 持久化稳定的设备 ID 以隔离队列。
- 麦克风靠近面部时提供渐强触觉反馈；轻震/强震距离、震动强度和校准结果可配置并持久化。
- 平板支持 0/30/60/90 度四档倾角，默认 30 度，物理底板、屏幕和 Canvas 同步转动。
- 自动生成 Build ID，构建 APK、`adb install -r` 覆盖安装、核对 `versionName`、启动应用并从 `logcat` 验证精确构建。

## 产品边界

Quest App 负责低延迟音频、视频播放、VR 房间和头显内交互，不在头显内实现 Bilibili/YouTube 下载器。内容搜索、下载和播放队列由局域网 Companion 或在线服务提供。

当前仍是单人原型，不包含账号、多人包厢、云端歌单、评分或录音回放。详细版本安排见 [ROADMAP.md](ROADMAP.md)。

## 环境要求

- Windows 10/11。
- Unity `6000.5.0f1`。
- Unity Hub 中为同一版本安装 Android Build Support、Android SDK & NDK Tools、OpenJDK。
- PowerShell 7，命令使用 `pwsh`。
- 已开启开发者模式和 USB 调试的 Meta Quest 3。
- 一个可访问的局域网 Companion 或兼容的在线点歌服务。

默认 Unity 安装路径为：

```text
C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe
```

## 快速开始

1. 使用 Unity Hub `6000.5.0f1` 打开仓库根目录。
2. 用 USB-C 连接 Quest 3，并在头显中允许 USB 调试。
3. 在仓库根目录运行：

   ```powershell
   pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1
   ```

4. 首次启动时在 Quest 中允许麦克风权限。
5. 正式构建默认连接公网服务；本地调试可在 `设置 > 点歌服务` 一键切换。
6. 回到主页打开搜索点歌，输入歌名、歌手或 BV 号，选择结果加入队列。

部署脚本会生成 `build/TsukiVox-Quest.apk`，保留应用数据地覆盖安装，并写入 `build/last-deploy.json`。只有 APK 构建、ADB 安装和 Package Manager 版本核验都成功后，才算安装完成。

如果 Quest 因头显休眠或控制器不可用而拦截无人值守启动，脚本会报告“精确版本已安装、运行时启动待头显唤醒”。唤醒头显和控制器后继续真机验证即可；未知原因未启动、崩溃或 Build ID 不匹配仍会作为失败处理。

## 点歌服务配置

### 局域网 Companion

Companion 模式使用同一台局域网 PC 的 IP 地址：

- playlist/state/control：`http://<PC IP>:5175`
- 搜索、下载文件：`http://<PC IP>:5174`

头显内只输入 PC IP，例如 `192.168.1.20`。Quest 不能使用 `127.0.0.1` 或 `localhost` 访问电脑上的服务；同时确认 Windows 防火墙允许对应端口。

### 在线服务

正式构建默认使用：

```text
https://api.tsukivox.com
```

点歌服务页同时保留“本地开发”入口，可快速切换到 `http://192.168.50.41:8080`。
同一 origin 必须同时提供设备登记、搜索、队列控制和媒体下载。公网服务使用 HTTPS，
客户端只保存每个 origin 独立的可撤销设备凭证，不持有云服务密钥或 Cookie。

服务模式、Companion 地址、在线服务地址和各 origin 的设备凭证分别保存在应用私有的
`PlayerPrefs`。每台 Quest 还会生成稳定设备 ID，并通过 `X-TsukiVox-Device-Id` 与 Bearer
凭证发送，使服务端可以隔离队列并单独撤销设备访问。

## 服务 API 契约

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| `GET` | `/api/playlist/state` | 轮询当前队列、索引和播放状态 |
| `POST` | `/api/playlist/control` | `play`（可选队列项 `id`）、`pause`、`prev`、`next`、`replay`、`remove`、`clear`、`clearPlayed`、`clearExceptCurrent`、`clearAll` |
| `GET` | `/api/bilibili/search` | 按 `query`、`page`、`pageSize` 搜索视频 |
| `POST` | `/api/playlist/items` | 把搜索结果加入队列，可请求立即播放 |
| `GET` | `/downloads/...` | 获取 ready 条目的媒体文件 |

客户端会解析完整 `playableUrl`、`/downloads/...` 相对路径和直接 MP4/WebM 路径。队列响应中的 `updatedAt` 用于拒绝晚到的旧状态。

## Quest 交互

- 右手：虚拟麦克风。
- 左手：荧光棒；X/Y 循环切换颜色。
- 左右 grip：只开关对应手柄的 UI 射线。
- 左右扳机：点击、按住和拖动世界空间 UI。
- ABXY：不触发 UI 点击。
- 茶几右下角：切换 0/30/60/90 度平板倾角。
- `设置 > 麦克风防碰撞`：调整防贴脸触觉、捕获轻震/强震距离或恢复默认值。

搜索框和服务地址使用 Quest Android 系统键盘。选择输入框后可输入中文；提交或取消后应回到控制器射线交互。

## 构建命令

完整构建、安装和启动：

```powershell
pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1
```

只构建 APK：

```powershell
pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1 -BuildOnly
```

只安装已有 APK：

```powershell
pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1 -InstallOnly
```

构建 Release APK：

```powershell
pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1 -Release
```

项目已经在 Unity Editor 中打开时，脚本会向当前 Editor 提交一次显式构建请求；Editor 关闭时会自动使用无界面 Unity。无需为了自动部署手动关闭或打开 Editor。

无界面 Unity 启动检查：

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile unity-smoke.log
```

## Unity Editor 工具

- `TsukiVox > Create Audio Prototype Scene`：重新生成 `Assets/Scenes/AudioPrototype.unity`。
- `TsukiVox > Generate Consumer UI Font Asset`：重新生成 Noto Sans SC 动态多图集 TMP SDF 字体。
- `TsukiVox > Apply Current Helper Host To Scene`：把当前默认 Companion 地址写入场景。
- `TsukiVox > Build Quest APK`：从 Editor 创建带 Build ID 的 Quest APK。

修改中文字符范围、TMP 设置或源字体后必须重新生成字体资产。消费级 UI 使用 `TextMeshProUGUI` 和 `TMP_InputField`，不要退回旧版 `UnityEngine.UI.Text`。

## 主要模块

| 文件 | 职责 |
| --- | --- |
| `QuestAudioPrototype.cs` | 麦克风权限、实时返听、人声预设、电平和安全控制 |
| `PlaylistClient.cs` | HTTP 搜索、队列状态、播放控制、点播请求和 URL 解析 |
| `QuestPlaylistPrototype.cs` | 服务模式、持久化设备 ID、轮询及点歌状态机 |
| `QuestAndroidKeyboardInput.cs` | Quest 系统键盘与 TMP 输入框桥接 |
| `QuestVideoScreenPrototype.cs` | 在线流式播放、Companion 缓存、视频探测、prepare、宽高比和播放同步 |
| `QuestKtvRoomPrototype.cs` | KTV 房间几何、材质、灯光反馈和空间锚点 |
| `QuestHandheldPropsPrototype.cs` | 右手麦克风、左手荧光棒及麦克风防贴脸触觉 |
| `QuestConsumerUiPrototype.cs` | 普通用户控制面板、搜索点歌、服务设置和诊断抽屉 |
| `QuestTabletTiltController.cs` | 平板四档倾角、动画、持久化和触觉反馈 |
| `QuestUiPointer.cs` | Quest 控制器射线、扳机点击与拖动 |
| `QuestAppShellPrototype.cs` | 世界空间 App 壳和完整诊断汇总 |
| `QuestBuildInfo.cs` | 读取嵌入的版本、Build ID、构建时间和 Git 状态 |

主要场景为 `Assets/Scenes/AudioPrototype.unity`。Android 构建预处理会固化 ARM64、GameActivity、New Input System、OpenXR loader、Quest Touch 控制器、Internet 权限和本地 HTTP 访问设置，并确保构建场景包含核心组件。

## 真机验证

每次涉及 Player 的改动至少检查：

- 首次/已有授权下的麦克风启动、输入输出电平和返听可听性。
- 四档人声预设、监听音量、安全限制，以及是否出现削波、啸叫或明显拖拍。
- Companion/在线服务切换、地址持久化、不同设备队列隔离和断网恢复。
- 中文系统键盘、搜索分页、点播成功/失败状态和队列更新。
- ready 视频的在线 Range 流式播放、Companion 缓存、首帧、宽高比、播放/暂停/重播/切歌和结束后 `next`。
- 0/30/60/90 度下平板底板、屏幕和 Canvas 共面，所有页面文字无重叠且按钮可命中。
- 麦克风防碰撞的轻震、强震、强度、校准、关闭和持久化。
- OpenXR、GameActivity、Build ID 和完整诊断复制。

## 诊断与故障排查

优先打开 `设置 > 诊断与支持`：

- 健康摘要显示麦克风、点歌服务和视频状态。
- 展开原始详情查看 endpoint、队列、缓存、VideoPlayer、XR、平板倾角、麦克风间隙和 Build ID。
- 使用 `复制完整诊断信息` 获取一次完整快照。

常见问题：

- 服务未连接：确认没有在 Quest 中填写 `localhost`，检查服务进程、PC IP、端口和防火墙。
- 视频长时间未显示：检查当前条目是否为 `ready`、`playableUrl` 是否可从 Quest 访问，以及诊断中的 probe/cache/prepare 状态。
- 安装成功但未自动进入 App：唤醒头显和控制器；若部署收据为 `blocked_by_quest_controller_check`，精确 APK 已安装，但仍需人工完成运行时验证。
- 搜索框没有系统键盘：重新用扳机选择输入框，并确认页面没有透明 `CanvasGroup` 拦截射线。
- 返听刺耳或啸叫：先降低 Quest 系统音量和监听音量，切换 Safe Small Room，不要在未确认安全前使用 Strong KTV。

## 当前限制与下一步

- 音频参数仍偏验证性质，V0.8 将集中梳理增益结构、动态处理、混响和反馈保护。
- Unity backend 仍是主要 KTV 效果链；Native Oboe dry backend 目前作为低延迟 A/B 参考路线。
- 头显内搜索当前以 Bilibili 为主；下载成功率受来源、网络、Cookie 和平台变化影响。
- 服务地址仍需手动输入，尚未实现局域网发现或二维码配对。
- 当前仓库尚未提交自动化测试，音频、XR 输入和视频链路必须依赖 Quest 3 真机回归。
- 蓝牙音频通常带来明显延迟，不作为推荐演唱输出路径。

工程约定、部署完成条件和测试要求见 [AGENTS.md](AGENTS.md)。
