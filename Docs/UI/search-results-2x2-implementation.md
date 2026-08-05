# 搜索结果 2x2 布局实施文档

## 修改日期
2026-08-05

## 设计决策
根据用户选择，实施了以下方案：
- **布局**: 2行2列网格 (之前是4行1列)
- **封面比例**: 3:2 (手机端方案)
- **图标**: 使用 QuestUiIcon 手绘图标系统，不使用 SVG

## 修改的文件

### 1. QuestConsumerUiPrototype.cs
**路径**: `Assets/Scripts/AudioPrototype/QuestConsumerUiPrototype.cs`

#### 主要改动：

##### 字段声明 (行 186-191)
```csharp
// 新增字段
private readonly RawImage[] searchResultCoverImages = new RawImage[SearchResultRowCount];
private readonly TMP_Text[] searchResultDurationTexts = new TMP_Text[SearchResultRowCount];
private readonly TMP_Text[] searchResultAuthorTexts = new TMP_Text[SearchResultRowCount];

// 移除
private readonly TMP_Text[] searchResultMetaTexts = new TMP_Text[SearchResultRowCount];
```

##### BuildSongSearchPage 方法 (行 570-614)
**新的 2x2 网格布局**：
- 卡片尺寸: 
  - 宽度: `(ContentWidth - 12f) / 2f` ≈ 490px
  - 高度: `128px`
  - 列间距: `12px`
  - 行间距: `12px`

**卡片结构**：
1. **封面区域** (左侧):
   - 比例: 3:2 (192px × 128px)
   - 背景色: `#111817`
   - 圆角: `6px`
   - RawImage 组件用于显示封面图片
   
2. **时长标签** (封面右下角):
   - 尺寸: `44px × 20px`
   - 背景: `rgba(0, 0, 0, 0.82)`
   - 文字: 白色，13px，加粗
   - 圆角: `4px`

3. **内容区域** (右侧):
   - **标题**: 
     - 位置: 顶部
     - 字号: 17px
     - 最多显示 3 行，超出省略
     - 行间距: -8px (紧凑布局)
   
   - **UP主名称**: 
     - 位置: 底部左侧
     - 字号: 13px
     - 单行显示，超出省略
   
   - **加入按钮**: 
     - 位置: 底部右侧
     - 尺寸: `44px × 44px`
     - 图标: Plus (手绘)

##### RefreshSongSearch 方法 (行 1025-1044)
- 更新数据绑定以适应新的字段
- 添加封面图片异步加载
- 单独显示 UP主名称和时长（不再合并为一行）

##### LoadCoverTexture 协程 (行 2991-3018)
新增方法用于异步加载封面图片：
```csharp
private IEnumerator LoadCoverTexture(string url, RawImage target)
```
- 使用 UnityWebRequestTexture 加载图片
- 超时时间: 10秒
- 失败时静默处理（显示占位背景色）

##### SetSongSearchVoiceLayout 方法 (行 1695-1719)
更新布局计算以支持 2x2 网格的固定定位。

### 2. BuildForQuest.cs (新文件)
**路径**: `Assets/Editor/BuildForQuest.cs`

Unity 编辑器构建脚本：
- 菜单项: `Build/Build APK for Quest 3`
- 输出路径: `Builds/TsukiVox_Quest_{timestamp}.apk`
- 自动包含场景: `Assets/Scenes/AudioPrototype.unity`

## 视觉规格

### 卡片设计
```
┌─────────────────────────────────────────┐
│  ┌──────────┐  标题标题标题标题标题标   │
│  │          │  题标题标题标题标题       │
│  │  封面    │  标题标题                 │
│  │  3:2     │                           │
│  │          │  UP主名称        [+]      │
│  │     4:20 │                           │
│  └──────────┘                           │
└─────────────────────────────────────────┘
```

### 颜色规格
- 卡片背景: `Surface` (主题色)
- 封面背景: `#111817`
- 时长背景: `rgba(0, 0, 0, 0.82)`
- 时长文字: `#FFFFFF`
- 标题: `TextPrimary` (主题色)
- UP主: `TextSecondary` (主题色)
- 按钮激活: `AccentStrong` (主题色)

### 间距规格
- 卡片之间横向间距: `12px`
- 卡片之间纵向间距: `12px`
- 卡片内边距: 无 (封面贴边)
- 内容区域边距: `6px`

## 兼容性

### 主题系统
所有颜色都通过 `QuestUiThemePalette` 系统绑定，支持：
- 暗色主题 (默认)
- 亮色主题
- 自动主题切换

### 图标系统
使用 `QuestUiIcon` 组件，支持：
- Plus (加入队列)
- Check (已加入)
- 手绘风格，非 SVG
- 自动适配主题颜色

### 封面加载
- 支持 Bilibili CDN 图片
- 支持本地服务器图片
- 自动处理跨域和超时
- 失败时显示占位背景

## 构建流程

### 自动构建
1. 修改完成后，Unity 后台构建已启动
2. 构建输出目录: `Builds/`
3. 构建日志: `Builds/build.log`

### 手动构建
```bash
# 使用 Unity 菜单
Build > Build APK for Quest 3

# 或使用命令行
"/c/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" \
  -quit -batchmode \
  -projectPath "c:/Users/Reegon/Documents/TsukiVox_Unity" \
  -executeMethod BuildForQuest.BuildAPK \
  -logFile "Builds/build.log"
```

### 安装到 Quest 3
```bash
# 通过 ADB 安装
adb install -r Builds/TsukiVox_Quest_*.apk

# 或通过 Quest 开发者中心上传
```

## 测试要点

### UI 测试
- [ ] 2x2 网格正确显示
- [ ] 封面图片正确加载
- [ ] 3:2 比例正确保持
- [ ] 时长标签位置正确
- [ ] 标题最多3行截断
- [ ] UP主名称单行截断
- [ ] 加入按钮交互正常

### 主题测试
- [ ] 暗色主题显示正常
- [ ] 亮色主题显示正常
- [ ] 主题切换平滑

### 网络测试
- [ ] Bilibili 封面加载正常
- [ ] 本地服务器封面加载正常
- [ ] 网络超时处理正常
- [ ] 加载失败显示占位背景

### Quest 3 测试
- [ ] 分辨率适配正常
- [ ] 触控交互流畅
- [ ] 射线选择准确
- [ ] 性能流畅 (60fps)

## 性能优化

### 图片加载
- 异步加载，不阻塞主线程
- 10秒超时避免长时间等待
- 加载失败静默处理，不影响其他卡片

### 布局计算
- 固定布局，避免动态计算
- 最小化 UI 重建
- 使用 RectTransform 缓存

### 内存管理
- RawImage 纹理在卡片销毁时自动释放
- 协程在目标对象销毁时自动停止

## 已知限制

1. **封面缓存**: 当前未实现封面缓存，每次显示都会重新下载
2. **加载动画**: 封面加载时无加载指示器
3. **分页**: 每页固定显示4个结果 (2x2)
4. **横屏模式**: 当前仅优化竖屏布局

## 后续优化建议

1. **封面缓存系统**: 实现本地纹理缓存，减少重复下载
2. **加载动画**: 添加封面加载时的 shimmer 动画
3. **虚拟滚动**: 支持更多结果的连续滚动
4. **手势支持**: 添加卡片滑动和长按交互
5. **性能监控**: 添加帧率和内存监控面板

## 参考文档

- 原型设计: `Docs/UI/search-results-2x2-options.html`
- 封面比例研究: HTML 中的 `ratio-phone` 方案
- 图标系统: `Assets/Scripts/AudioPrototype/QuestUiIcon.cs`
- 主题系统: `Assets/Scripts/AudioPrototype/QuestUiThemePalette.cs`
