# V0.75 语音搜索实施跟踪

设计文档：[voice-search-tech-design.html](voice-search-tech-design.html)（在浏览器中打开，含端到端架构图、状态机、音频生命周期和供应商对比）

本文件是上面那份设计文档的进度跟踪版本。设计决策、图示和取舍理由以 HTML 为准；本文件只负责"做到哪一步了"。**两份文档必须同步更新**：任何范围或决策变化都要同时改 HTML 的对应章节和这里的清单。

- 版本：**V0.75 语音搜索**，V0.7 与 V0.8 之间的独立中间版本
- 状态：**P2/P3/P4 代码已完成并通过离线验证；P1 与 P5 需要真机和真实供应商**
- 基线版本：V0.7（头显内搜索点歌闭环已完成）
- 影响仓库：`TsukiVox_Unity` + `TsukiVox_Server`
- 最后更新：2026-07-27

## 版本边界

V0.75 只做一件事：**把语音变成一条可用的找歌入口**。除此之外的音频改进都不属于本版本。

具体地说，ROADMAP 中 V0.8「音频引擎打磨和安全」的全部内容都不在 V0.75 范围内——增益结构梳理、动态处理与混响参数调优、Quest 扬声器反馈保护、削波与过热提示、演唱音量校准流程，都留给 V0.8。V0.75 与 V0.8 是先后关系，不是同一版本的两个方面。

这个边界有一个实际后果：**V0.75 不允许为了语音搜索去改动返听链路的音频处理。** 语音搜索只从现有麦克风采集链路旁路复制一份干声，不调整增益、不改滤波器、不改预设。如果实现过程中发现必须动返听链路才能拿到干声，那是设计出了问题，应该回到设计文档重新讨论，而不是顺手改音频参数。

| 范围 | 归属 |
| --- | --- |
| 语音采集、上传、识别、检索、结果展示 | V0.75 |
| 服务端语音接口、供应商适配、配额与熔断 | V0.75 |
| 语音搜索相关的错误码结构化 | V0.75 |
| 增益结构、动态处理、混响参数、反馈保护、削波提示、音量校准 | V0.8 |
| 歌词、演唱辅助 | V0.9 |
| 歌单持久化与内容管理 | V0.95 |

## 目标

用户按一次麦克风按钮，说出"找周杰伦的晴天"，头显直接给出 4 个可点播的视频候选。它不是语音输入法，不把识别文字塞回搜索框；语音是一条独立的找歌入口，与 V0.7 已有的文字搜索并行存在。

范围限定为"说出歌名、歌手和版本要求"。

## 不在 V0.75 范围

功能层面：

- 哼唱识曲（听旋律找歌）
- 连续对话与多轮追问
- 语音控制播放（"下一首""暂停"）
- 识别后自动点播第一首
- MiMo 通用 `mimo-v2.5` 音频理解直接抽意图（作为实验项评估，不作为生产回退路径）

音频链路层面（属于 V0.8）：

- 任何对返听增益、动态处理、混响预设或滤波器参数的调整
- Quest 扬声器反馈保护、削波与过热输入提示
- 演唱音量校准流程

## 已确定的技术选型

| 项 | 选择 | 备注 |
| --- | --- | --- |
| 识别位置 | 云端 API，服务端调用 | 不在 Ubuntu 上跑模型，不在 Quest 上跑模型 |
| 主通道 | 腾讯云一句话识别 `16k_zh-PY` | 唯一理由是请求级临时热词 `HotwordList`，最多 128 个「词 + 权重」条目 |
| 备用通道 | `mimo-v2.5-asr` | 大陆可达、按时长计费更便宜，但无热词、无置信度 |
| 已排除 | OpenAI / Azure / Google Chirp | 服务器计划部署在中国大陆境内，访问不稳定 |
| 传输 | 一次性 HTTP 短音频，非流式 | 用户只说 3-6 秒，Realtime 不会缩短端到端时延 |
| 音频格式 | 16 kHz 单声道 16-bit WAV，≤ 6 秒 | 约 192 KB；两家上限都远未触及 |
| 密钥位置 | 仅服务端环境变量 | Quest 永不直连云厂商 |
| 故障切换 | 仅在超时 / 429 / 5xx 时切换 | 不因"识别结果看起来不对"而自动双调用 |

## 阻塞项

这三项不解决就不应开始实现。它们都不是语音识别本身的问题，但任何一条不解决，真机上都会表现为"语音搜索不好用"。

- [ ] **Bilibili 中文关键词检索被限流**
  已实测发生：`BV1Kx4y1h7vR` 精确查询成功，中文关键词返回 `BILIBILI_RATE_LIMITED`。识别再准，检索失败仍然找不到歌。必须与语音搜索并行解决。
- [x] **Native Oboe 后端无 PCM 出口**
  已按"V0.75 限定默认 Unity 麦克风后端"实现：`IsDryCaptureAvailable` 在 Oboe 后端下返回 false，
  语音按钮置灰，服务页提示"Native 低延迟后端下无法采集语音，请先关闭它"。
  没有临时切换后端（会有麦克风重启间隙和爆音风险），也没有改原生插件。补 PCM 导出留待后续版本。
- [x] **服务端没有自身限流**
  已随 V0.75 落地：`quota.mjs` 提供每设备并发 1、每日 200 次（可配）、每分钟 12 次节流，
  以及全局日预算 2000 次熔断。计数在进程内存中，当前单进程部署够用；将来横向扩容需换成共享计数器。

## 待决策

- [x] Oboe 后端启用时的行为：已定为 V0.75 只支持默认 Unity 后端，Oboe 下语音入口置灰并给出明确提示
- [ ] MiMo 音频保留与训练条款需向小米书面确认（政策未明确音频保留时长；"不用于训练"明确覆盖的是文本内容）——**阻塞正式发布，不阻塞开发**
- [x] 每设备每日次数上限的默认值：取 200 次（`TSUKIVOX_VOICE_DAILY_DEVICE`），全局 2000 次

## 服务端环境变量

密钥只放服务端，不写入仓库，也不下发给 Quest。全部可选；未配置腾讯云与 MiMo 时语音接口返回
`SPEECH_PROVIDER_UNCONFIGURED` 503，健康检查里 `voiceSearch.available` 为 false。

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `TSUKIVOX_TENCENT_SECRET_ID` | 空 | 腾讯云密钥 ID；与 KEY 同时存在才启用主通道 |
| `TSUKIVOX_TENCENT_SECRET_KEY` | 空 | 腾讯云密钥 |
| `TSUKIVOX_TENCENT_ENGINE` | `16k_zh-PY` | 引擎类型，中英粤 |
| `TSUKIVOX_TENCENT_REGION` | 空 | 一句话识别不要求 Region |
| `TSUKIVOX_MIMO_API_KEY` | 空 | 配置后启用备用通道 |
| `TSUKIVOX_MIMO_MODEL` | `mimo-v2.5-asr` | 模型 ID 必须可配，MiMo-V2 已下线 |
| `TSUKIVOX_MIMO_API_ORIGIN` | `https://api.xiaomimimo.com` | |
| `TSUKIVOX_MIMO_LANGUAGE` | `auto` | 仅 `auto` / `zh` / `en` |
| `TSUKIVOX_SPEECH_TIMEOUT_MS` | `8000` | 单个供应商的超时 |
| `TSUKIVOX_VOICE_MAX_SECONDS` | `6` | 音频时长上限 |
| `TSUKIVOX_VOICE_MAX_BYTES` | `524288` | 请求体上限，512 KB |
| `TSUKIVOX_VOICE_MIN_MS` | `400` | 低于此值判为空录音，不计费 |
| `TSUKIVOX_VOICE_CONCURRENCY` | `1` | 每设备并发 |
| `TSUKIVOX_VOICE_PER_MINUTE` | `12` | 每设备每分钟 |
| `TSUKIVOX_VOICE_DAILY_DEVICE` | `200` | 每设备每日 |
| `TSUKIVOX_VOICE_DAILY_GLOBAL` | `2000` | 全局每日熔断 |

## 实施阶段

每一阶段都有明确的退出条件。未达退出条件不进入下一阶段。

### P1 供应商实测与检索稳定化（并行）

- [ ] 准备约 50 条 Quest 3 真机录音样本集
  - [ ] 普通话歌名 + 歌手（`周杰伦 晴天`、`五月天 倔强`）
  - [ ] 日英专名与团名（`CRYCHIC 春日影`、`MyGO!!!!!`）
  - [ ] 同音易错（`蜜制` / `秘制` 一类，验证热词权重效果）
  - [ ] 带版本要求（`晴天 KTV 伴奏`、`春日影 现场版`）
  - [ ] 中英混说与粤语各若干条
  - [ ] 口语包装（`帮我找…`、`来一首…`、`我想唱…`）
- [ ] 每条样本在四种条件下各跑一次：安静 / 有伴奏底噪 × 腾讯云带热词 / MiMo 无热词
- [ ] 记录 Top-4 命中率（主指标）、Top-1 命中率、端到端时延、单次成本、按错误码分类的失败率
- [ ] 记录热词开启/关闭的命中率差值
- [ ] 定位并修复 Bilibili 中文关键词检索限流

**退出条件**：主通道确定（若 MiMo 无热词已追平腾讯云带热词，则主备顺序反转）；关键词检索连续 100 次成功率 > 95%

### P2 服务端 `/api/voice-search`

- [x] 独立的音频读取路径（`readAudioBody`，裸 `audio/wav`，不经过 `jsonBodyLimitBytes = 128 * 1024`）
- [x] 时长、大小、格式校验（`voice-search.mjs` 的 `parseWav`，只接受 16 kHz 单声道 16-bit）
- [x] `SpeechRecognizer` 适配层（`speech.mjs`：腾讯云 TC3 签名 + MiMo，模型名与地址全部走环境变量）
- [x] 动态热词表组装（`playlist.mjs` 的 `collectHotwords`：最近 20 次点播，歌名权重 10、歌手 8）
- [x] 查询规整：剥离"帮我找/来一首"等口语，抽取歌手、歌名、版本词
- [x] 主备故障切换（仅超时 / 429 / 5xx；内存中保留同一段音频用于转发）
- [x] 复用 `bilibili.mjs` 检索并取前 4 条
- [x] 设备配额：每设备并发 1、每日次数上限、每分钟节流（`quota.mjs`）
- [x] 全局日预算熔断，达阈值返回 `VOICE_QUOTA_EXCEEDED`
- [x] 音频不落盘：拿到文本即释放 Buffer，transcript 不写入队列历史或设备状态文件

**退出条件**：无 Quest 也能用 curl + 本地 WAV 端到端返回 4 条结果；配额与熔断可被测试主动触发
**已达成**（2026-07-27）：`npm test` 15/15 通过；`node scripts/voice-search-smoke.mjs` 四项检查全通过，
含健康检查、4 条候选、空音频拒绝且不计费、配额触发。冒烟脚本默认使用本地假供应商，不消耗额度。
待热词来源扩展：收藏与全局热门歌手尚未实现（当前只有设备点播历史），因为 V0.7 还没有收藏功能。

### P3 Quest 采集与上传

> 边界守卫：本阶段只**旁路读取**音频，不修改音频处理。禁止在 P3 中调整增益、滤波器、混响预设或 `OnAudioFilterRead` 的处理逻辑——那些属于 V0.8。唯一允许的音量改动是录音期间压低**视频播放**音量，它不属于返听链路。

- [x] 从现有 48 kHz 环形缓冲复制**干声**（`QuestAudioPrototype.TryReadDrySamples`，只读旁路）
- [x] 独立 7 秒累积缓冲（现有 2 秒循环 clip 留不住一整句）
- [x] 降采样到 16 kHz 单声道（整窗平均，兼作简易抗锯齿）
- [x] 端点检测：静音 800 ms 提交，硬上限 6 秒，前置静音 > 2.5 秒判定"没听到"
- [x] WAV 封装（`VoiceSearchRecorder.EncodeWav`）
- [x] 录音期间把视频音量压到约 15%（不暂停），结束后恢复（`QuestVideoScreenPrototype.SetPlaybackVolume`）
- [x] 不停止麦克风、不重启 `monitorSource`，避免爆音
- [x] `PlaylistClient` 扩展：`POST /api/voice-search`，超时 25 秒（识别 8 秒 + 检索 12 秒 + 余量）
- [x] 错误模型补 `code` / `retryable` 字段（新增 `PlaylistRequestError`，可区分传输失败与 HTTP 错误）
- [x] 上传后立即清零音频缓冲，不留副本、不写 `persistentDataPath`、不进日志

**退出条件**：Editor 内生成的 WAV 能被服务端正确识别
**已达成**（2026-07-27）：用真实 `VoiceSearchRecorder.cs` 源码在 Editor 外驱动合成麦克风音频，
8 项录音器检查全通过（WAV 头、样本钳位、48→16 kHz 重采样率、尾部静音自动提交、前置静音判定、
6 秒硬上限、无干声时拒绝启动、清零后无残留）；其产出的 WAV 上传到服务端返回 200 与 4 条候选，
音频字节完全一致。过程中发现并修复一个真实缺陷：`CapturedMilliseconds` 原先报告未裁剪的长度，
与实际上传的字节数不符（2220 ms vs 1650 ms），会让客户端上报错误时长。

### P4 UI 与状态机

- [x] 语音入口放在搜索页顶部，与文字输入框并列；结果区沿用现有 4 行布局
- [x] 电平条与"正在听…"视觉状态（18 根电平条，不允许静默采集）
- [x] "听到：xxx" 只读回执（`TMP_Text`，不是可编辑输入框）
- [x] 一次点击开始、说完自动停、录音中再点一次取消（不用长按）
- [x] 开始与提交各一次轻触觉反馈（`QuestHandheldPropsPrototype.PulseVoiceFeedback`）
- [x] 8 个状态的文案：`Idle` / `Listening` / `Uploading` / `Searching` / `Results` / `NoSpeech` / `Empty` / `Failed`
- [x] `Failed` 按原因分类：网络 / 服务 / 上游限流 / 配额（`DescribeVoiceFailure`）
- [x] `NoSpeech` 不发起云端调用，也不显示成"识别失败"（端上判定，直接结束）
- [x] 文字搜索输入框始终在同一页面，随时可切换
- [x] `设置 > 点歌服务` 增加语音找歌总开关，关闭后完全不采集、不上传，并持久化到 `PlayerPrefs`
- [x] 结果不自动点播第一首，仍由用户点 `+` 确认（沿用现有 `AddSearchResult`）
- [x] 诊断抽屉显示语音状态、供应商与最后错误码，便于真机排查

**退出条件**：8 个状态在头显里都能被主动触发并显示正确文案
**代码已完成，退出条件待真机验证**：状态机与文案已实现并通过编译，但"在头显里都能被主动触发"
必须戴上 Quest 逐个走一遍，属于 P5 范围。

### P5 真机回归

- [ ] 背景音乐播放中录音
- [ ] 返听不断音、无爆音
- [ ] 中文、中英混合歌名
- [ ] 断网恢复
- [ ] 配额耗尽提示
- [ ] 供应商故障切换
- [ ] 麦克风权限：V0.7 返听已申请过，确认不出现新的权限弹窗
- [ ] **返听音质与延迟相比 V0.7 无可感知变化**（验证语音搜索确实是旁路，没有动到音频链路）
- [ ] 人声预设切换、混响可听性、输入/输出电平表现与 V0.7 一致

以下两项由 `AGENTS.md` 强制要求，任何触及 UI 或麦克风的改动都必须回归：

- [ ] 四档倾角（0/30/60/90 度）下录音按钮可命中（`AGENTS.md` V0.7 控制面板约定）
- [ ] 不改变既有输入职责：右手麦克风、左手荧光棒、左手 X/Y 换色、左右 grip 切换各自射线、左右扳机点击 UI（`AGENTS.md` V0.7 点歌与设备体验约定）

**退出条件**：一次完整 KTV 场景下连续 20 次语音点歌无卡死、无爆音、无误点播；且返听体验相比 V0.7 无退化

> P3/P4 改动了 `Assets/Scripts/`，按 `AGENTS.md` 的"Quest 自动部署完成条件"，交付前必须执行一次
> `pwsh -NoLogo -NoProfile -File .\Tools\Deploy-Quest.ps1`，并核对 Package Manager 读回的 `versionName`
> 与 `build/last-deploy.json` 中的 Build ID 一致。P1/P2 只动服务端和文档，不要求真机部署。

## V0.75 验收标准

全部满足才算 V0.75 完成，可以写入 ROADMAP 并开始 V0.8：

- [ ] 用户能在头显内只用语音完成一次点歌：按下、说一句、看到 4 个候选、点 `+` 播放
- [ ] 语音入口与 V0.7 文字搜索并存，两者互不影响，可随时切换
- [ ] 8 个状态在真机上都能正确显示，失败原因可区分（网络 / 服务 / 上游限流 / 配额）
- [ ] 供应商故障切换在真机可验证，Quest 侧不感知供应商，也不持有任何云端密钥
- [ ] 服务端设备配额与全局熔断生效，可被主动触发
- [ ] 音频不落盘，transcript 不写入队列历史或设备状态文件
- [ ] 返听音质、延迟、预设行为相比 V0.7 无退化
- [ ] `Deploy-Quest.ps1` 精确版本核验通过，设置页 Build ID 与 `build/last-deploy.json` 一致
- [ ] ROADMAP 增加 V0.75 章节，`当前状态` 与 `当前优先级` 相应更新

## 接口契约摘要

完整契约（含全部错误码表）见 HTML 第 07 节。

```
POST /api/voice-search
Content-Type: audio/wav
X-TsukiVox-Device-Id: <V0.7 已有的稳定设备 ID>
X-TsukiVox-Audio-Ms: 4200

<binary WAV, 16 kHz mono 16-bit, ≤ 6 s, ≤ 512 KB>
```

成功响应返回 `transcript`、`normalizedQuery`、`intent`、`provider`、`audioMs`，以及与 `/api/bilibili/search` **完全同构**的 `items` 数组——这样 Unity 侧可直接复用现有结果渲染与点播路径，不需要第二套结果模型。

主要新增错误码：`AUDIO_TOO_SHORT` 400、`AUDIO_TOO_LARGE` 413、`AUDIO_FORMAT_UNSUPPORTED` 415、`SPEECH_NO_RESULT` 422、`VOICE_QUOTA_EXCEEDED` 429、`SPEECH_PROVIDER_FAILED` 502、`SPEECH_TIMEOUT` 504。

## 代码锚点

改动会落在这些位置：

- `Assets/Scripts/AudioPrototype/QuestAudioPrototype.cs` — 48 kHz / 2 秒循环缓冲，干声取样点
- `Assets/Scripts/AudioPrototype/NativeOboeDryMonitor.cs` — 只导出电平，需要 PCM 时才改
- `Assets/Scripts/AudioPrototype/PlaylistClient.cs` — 新增语音接口、错误码结构化
- `Assets/Scripts/AudioPrototype/QuestConsumerUiPrototype.cs` — 搜索页复用（`SearchResultRowCount = 4`）
- 新增 `VoiceSearchRecorder`（重采样、端点检测、WAV 封装）✅
- `TsukiVox_Server/src/api.mjs` — 新路由、配额、`jsonBodyLimitBytes` 旁路 ✅
- `TsukiVox_Server/src/bilibili.mjs` — 检索复用（未修改）
- 新增 `TsukiVox_Server/src/speech.mjs`（腾讯云 + MiMo 适配层与 TC3 签名）✅
- 新增 `TsukiVox_Server/src/quota.mjs`（设备配额与全局熔断）✅
- 新增 `TsukiVox_Server/src/voice-search.mjs`（WAV 校验、查询规整、编排）✅
- 新增 `TsukiVox_Server/test/voice-search.test.mjs`（15 项测试）✅
- 新增 `TsukiVox_Server/scripts/voice-search-smoke.mjs`（无 Quest 冒烟检查）✅
- `QuestVideoScreenPrototype` — 新增 `SetPlaybackVolume`，用于录音期间压低视频音量 ✅
- `QuestHandheldPropsPrototype` — 新增 `PulseVoiceFeedback` 轻触觉 ✅

## 官方文档出处

- [腾讯云一句话识别 API](https://cloud.tencent.com/document/product/1093/35646) — `SentenceRecognition`，含 `HotwordList` 临时热词
- [腾讯云语音识别计费概述（在线版）](https://cloud.tencent.com/document/product/1093/35686) — 一句话识别每月 5000 次免费额度
- [MiMo-V2.5-ASR 接口文档](https://mimo.mi.com/docs/zh-CN/api/audio/Speech-Recognition)
- [MiMo 语音识别使用指南](https://mimo.mi.com/docs/zh-CN/quick-start/usage-guide/audio/Speech-Recognition) — 方言、噪声、带伴奏歌词转写、10 MB Base64 上限
- [MiMo 速率限制](https://mimo.mi.com/docs/zh-CN/api/guidance/rate-limit) — `mimo-v2.5-asr`：100 RPM / 10K TPM
- [MiMo 按量计费](https://mimo.mi.com/docs/zh-CN/price/pay-as-you-go) — ASR ¥0.5/小时
- [MiMo 音频理解](https://mimo.mi.com/docs/zh-CN/quick-start/usage-guide/multimodal-understanding/audio-understanding) — 通用 `mimo-v2.5`，实验项

云厂商的价格、模型名与限制随时间变化，接入前请重新核对当期官方文档。MiMo-V2 已于 2026-06-30 下线，模型 ID 必须走环境变量而不是写死。

## 变更记录

| 日期 | 变更 |
| --- | --- |
| 2026-07-27 | 创建设计文档与本跟踪文件；选型定为腾讯云主用、MiMo 备用；识别改为云端 API（不在服务器跑模型） |
| 2026-07-27 | 定为 V0.75 独立中间版本；明确 V0.8「音频引擎打磨和安全」不在本版本范围；增加版本边界、V0.75 验收标准，以及"不改动返听链路"的守卫与回归项 |
| 2026-07-27 | 实现 P2（服务端接口、双供应商适配、配额熔断）、P3（Quest 干声采集与上传）、P4（UI 与状态机）。服务端 15/15 测试通过，录音器 8/8 检查通过，Editor 产出 WAV 已完成服务端往返验证，Unity 编译 0 错误。P1 与 P5 需要真机和真实供应商 |
