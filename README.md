# TsukiVox Unity

TsukiVox 是面向 Meta Quest 3 的原生单人 VR K 歌客户端。项目使用 Unity `6000.5.0f1`，当前产品版本为 **V0.78 - 头显直接请求**。

应用已经收敛为 Direct-only 架构：Quest 直接访问 Bilibili 公共接口完成文字搜索、拼音候选、视频解析和媒体下载，不再连接局域网 Companion、公网点歌服务或本地开发服务。

## 当前能力

- 使用 Quest 3 内置麦克风实时返听，提供 Dry Reference、KTV Room、Strong KTV 和 Safe Small Room 四档预设。
- 在头显内通过世界空间软键盘搜索 Bilibili 视频，支持拼音候选、关键词、BV 号、完整链接和短链接。
- 匿名请求 Bilibili progressive MP4，优先 480P，并接受上游降级到 360P。
- 点播后把视频下载到 `Application.persistentDataPath/DirectMediaCache/`，显示下载百分比，完成后由 `VideoPlayer + RenderTexture` 播放。
- 使用客户端内存队列完成播放、暂停、重播、上一首、下一首、指定播放、移除和清理操作。
- 程序化生成单人 KTV 包厢、沙发、茶几、大屏、灯带、右手麦克风和左手荧光棒。
- 茶几平板提供主页、搜索点歌、人声、播放队列、设置、麦克风防碰撞和诊断界面。
- 平板支持 0/30/60/90 度四档倾角，默认 30 度，底板、屏幕和 Canvas 同步转动。
- 自动生成 Build ID，构建 APK、`adb install -r` 覆盖安装、核对 `versionName` 并从 `logcat` 验证精确构建。

## Direct 架构

```text
Quest UI
  -> BilibiliDirectClient
       -> Bilibili 搜索 / 拼音候选 / 视频详情 / playurl
       -> 匿名 progressive MP4 下载
  -> QuestPlaylistPrototype
       -> 本地内存队列与播放命令
       -> DirectMediaCache
  -> QuestVideoScreenPrototype
       -> 本地 file:// MP4
       -> VideoPlayer + RenderTexture
```

`BilibiliDirectClient` 不发送 TsukiVox 设备 ID、Bearer 凭证、Cookie 或平台账号信息。旧版本保存的服务模式、默认服务地址、设备 ID、在线完整缓存开关和已知默认 origin 的设备凭证会在 Direct-only 版本启动时清理。

Android Player 仍需要 Internet 权限访问 Bilibili HTTPS 接口，但不再允许明文 HTTP。

## 当前边界

- 只支持匿名公开的 Bilibili 内容，不支持 YouTube。
- 只接受单文件 progressive MP4；需要 DASH 音视频合并、分段合并、登录或 Cookie 的视频会显示可操作错误。
- 视频必须完整下载到头显后才能播放，不提供服务端 HLS 或 MP4 Range 流式播放。
- 播放队列只存在于当前 App 进程内；重启应用后队列清空，已完成的媒体文件仍可按稳定媒体身份复用。
- 下载成功率受 Bilibili 接口、地区、网络和平台风控变化影响。
- 当前仍是单人原型，不包含账号、多人包厢、云端歌单、评分或录音回放。

## 快速开始

1. 使用 Unity Hub `6000.5.0f1` 打开仓库根目录。
2. 用 USB-C 连接已开启开发者模式和 USB 调试的 Quest 3。
3. 在仓库根目录运行：

   ```powershell
   pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1
   ```

4. 首次启动时在 Quest 中允许麦克风权限。
5. 打开搜索点歌，输入歌名、歌手、BV 号或 Bilibili 链接。

部署脚本会生成 `build/TsukiVox-Quest.apk`，保留应用数据地覆盖安装，并写入 `build/last-deploy.json`。只有 APK 构建、ADB 安装和 Package Manager 版本核验都成功后，才算安装完成。

如果 Quest 因头显休眠或控制器不可用而拦截无人值守启动，脚本会报告“精确版本已安装、运行时启动待头显唤醒”。

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

无界面 Unity 启动检查：

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile unity-smoke.log
```

## Unity Editor 工具

- `TsukiVox > Create Audio Prototype Scene`：重新生成 `Assets/Scenes/AudioPrototype.unity`。
- `TsukiVox > Generate Consumer UI Font Asset`：重新生成 Noto Sans SC 动态多图集 TMP SDF 字体。
- `TsukiVox > Build Quest APK`：从 Editor 创建带 Build ID 的 Quest APK。

## 主要模块

| 文件 | 职责 |
| --- | --- |
| `BilibiliDirectClient.cs` | 匿名搜索、拼音候选、链接解析、progressive MP4 请求与本地缓存 |
| `DirectPlaylist.cs` | Direct 队列常量及搜索、候选、队列数据模型 |
| `QuestPlaylistPrototype.cs` | 本地队列、下载调度、搜索状态和播放控制 |
| `QuestAndroidKeyboardInput.cs` | 世界空间软键盘与 TMP 输入框交互 |
| `QuestVideoScreenPrototype.cs` | 本地视频加载、首帧、宽高比、播放同步和视频诊断 |
| `QuestAudioPrototype.cs` | 麦克风权限、实时返听、人声预设、电平和安全控制 |
| `QuestConsumerUiPrototype.cs` | 普通用户控制面板、搜索点歌、队列、设置和诊断抽屉 |
| `QuestKtvRoomPrototype.cs` | KTV 房间几何、材质、灯光反馈和空间锚点 |
| `QuestHandheldPropsPrototype.cs` | 右手麦克风、左手荧光棒及麦克风防贴脸触觉 |
| `QuestTabletTiltController.cs` | 平板四档倾角、动画、持久化和触觉反馈 |
| `QuestBuildInfo.cs` | 嵌入版本、Build ID、构建时间和 Git 状态 |

## 真机验证

每次影响 Player 的改动至少检查：

- 首次/已有授权下的麦克风启动、输入输出电平、返听可听性和四档预设。
- 世界空间软键盘的字母、符号、大小写、光标、退格、清空、完成和取消。
- 输入停止 350 ms 后最多九条拼音候选，连续输入只保留最后一次请求。
- 关键词、BV、完整链接和短链搜索，点播后的解析、下载百分比、缓存命中和错误恢复。
- 本地 ready MP4 的首帧、宽高比、播放/暂停/重播/切歌、视频结束后 `next`。
- 0/30/60/90 度平板倾角、控制器扳机命中、麦克风防碰撞和完整诊断复制。
- OpenXR、GameActivity、HTTPS 网络访问、Build ID 和覆盖安装后的设置保留。

排查时优先使用 `设置 > 诊断与支持 > 复制完整诊断信息`。工程约定、部署完成条件和测试要求见 [AGENTS.md](AGENTS.md)。
