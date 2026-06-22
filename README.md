# TsukiVox Unity 音频验证原型

这是 TsukiVox 从 WebXR 迁移到 Unity 原生 Quest 应用后的第一个验证工程。当前工程只聚焦一件事：确认 Quest 3 上能否实现可用的实时人声返听和 KTV 混响。

现阶段还没有迁移 WebXR 版本里的 KTV 房间、视频大屏、播放队列 UI、手柄麦克风和荧光棒交互。这些会在音频路线确认后再逐步迁移。

## 当前目标

验证 Unity 原生方案在 Quest 3 上是否能解决 WebXR / Quest Browser 中实时混响延迟过高的问题。

当前原型已经验证：

- Quest 3 麦克风权限申请。
- 通过 `UnityEngine.Microphone` 获取麦克风输入。
- 通过 `AudioSource` 做实时返听。
- 输入电平和输出电平都能在 Quest 3 真机上响应。
- 加入 `AudioListener` 后，输出链路可以正常听到。
- KTV Room / Strong KTV 预设可以明显听到混响效果。
- 使用 Unity 自动生成默认 Android 启动 Activity，避免 APK 能安装但无法打开的问题。

## 工程环境

当前使用：

```text
Unity 6000.5.0f1
```

Unity 路径：

```text
C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe
```

Quest 构建需要在 Unity Hub 中为同一个 Unity 版本安装：

- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

## 主要场景

打开场景：

```text
Assets/Scenes/AudioPrototype.unity
```

如果场景丢失，或者修改了场景生成脚本后需要刷新场景，可以在 Unity 顶部菜单运行：

```text
TsukiVox > Create Audio Prototype Scene
```

## 构建到 Quest 3

1. 用 USB-C 连接 Quest 3 和电脑。
2. 在 Quest 3 里允许 USB 调试。
3. 在 Unity 中打开 `File > Build Profiles...` 或 `File > Build Settings...`。
4. 选择 `Android`。
5. 点击 `Switch Platform`。
6. 确认 `Scenes In Build` 中包含：

```text
Assets/Scenes/AudioPrototype.unity
```

7. 在 `Run Device` 中选择 Quest 3。
8. 点击 `Build And Run`。

如果 Unity 显示 `Could not find any valid targets to launch on for Android`，说明构建成功但没有找到可启动设备。通常需要重新插拔 USB、在头显中允许 USB 调试，或在 `Run Device` 里重新选择 Quest 3。

## 真机测试流程

1. 进入 App 前先把 Quest 3 系统音量调低。
2. 第一次启动时允许麦克风权限。
3. App 会自动开始麦克风监听。
4. 正常说话或唱歌，观察：

- `Input Level` 是否响应。
- `Output Level` 是否响应。
- 是否能听到返听和混响。
- `Mic lag estimate` 是否明显偏高。
- 是否有啸叫、爆音或过载感。

当前预设：

- `Dry Reference`：干声参考。
- `KTV Room`：默认 KTV 混响测试档，当前效果比较明显。
- `Strong KTV`：更夸张的压力测试档，用来确认混响存在。
- `Safe Small Room`：较保守的小房间预设。

## 已知问题

- 当前混响参数是为了验证效果存在，尚未调到最终可唱、自然、接近 SingRoom 的状态。
- UI 仍是调试面板，不是最终 VR KTV 界面。
- 当前使用 Unity 内置 `Microphone` 音频路径，延迟是否足够专业仍需要继续测试。
- 如果后续发现 Unity 内置音频延迟仍然过高，需要改为 Android Oboe / AAudio 原生音频插件。
- 不建议添加一个极简的 `Assets/Plugins/Android/AndroidManifest.xml`。如果自定义主 Manifest 没有 Unity 的启动 Activity，APK 会安装成功但在 Quest 未知来源里点不开。

## 专业 KTV 目标

最终目标不是“能听到一点效果”，而是尽量接近真实 KTV 或 SingRoom 这类应用的演唱体验。

短期先用 Unity 内置音频链路继续验证：

- 可接受的返听延迟。
- 可控的混响强度。
- 不刺耳、不啸叫。
- 正常说话和唱歌时电平健康。

如果内置链路达不到目标，再进入 Oboe / AAudio 原生音频路线。
