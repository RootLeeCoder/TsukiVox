using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    [DisallowMultipleComponent]
    public sealed class QuestConsumerUiPrototype : MonoBehaviour
    {
        private const string RootName = "Consumer UI";
        private const int QueueRowCount = 5;
        private const float RefreshIntervalSeconds = 0.1f;

        private static readonly Color ScreenBackground = new Color(0.024f, 0.04f, 0.039f, 1f);
        private static readonly Color Surface = new Color(0.048f, 0.073f, 0.071f, 1f);
        private static readonly Color SurfaceRaised = new Color(0.068f, 0.098f, 0.094f, 1f);
        private static readonly Color SurfaceHover = new Color(0.52f, 1f, 0.88f, 1f);
        private static readonly Color Line = new Color(0.14f, 0.2f, 0.19f, 1f);
        private static readonly Color TextPrimary = new Color(0.94f, 0.97f, 0.96f, 1f);
        private static readonly Color TextSecondary = new Color(0.57f, 0.65f, 0.63f, 1f);
        private static readonly Color Accent = new Color(0.24f, 0.9f, 0.74f, 1f);
        private static readonly Color AccentStrong = new Color(0.56f, 1f, 0.89f, 1f);
        private static readonly Color AccentInk = new Color(0.012f, 0.075f, 0.059f, 1f);
        private static readonly Color Warm = new Color(0.95f, 0.71f, 0.42f, 1f);
        private static readonly Color WarmSurface = new Color(0.22f, 0.15f, 0.09f, 1f);
        private static readonly Color Danger = new Color(0.94f, 0.44f, 0.44f, 1f);

        private enum UiPage
        {
            Home,
            Voice,
            Queue,
            Settings,
        }

        private RectTransform panel;
        private RectTransform consumerRoot;
        private RectTransform homePage;
        private RectTransform voicePage;
        private RectTransform queuePage;
        private RectTransform settingsPage;
        private RectTransform debugScrim;
        private RectTransform debugDrawer;
        private RectTransform rawDetailsRoot;

        private CanvasGroup homeGroup;
        private CanvasGroup voiceGroup;
        private CanvasGroup queueGroup;
        private CanvasGroup settingsGroup;
        private CanvasGroup debugScrimGroup;
        private CanvasGroup debugDrawerGroup;

        private QuestAudioPrototype audioPrototype;
        private QuestPlaylistPrototype playlistPrototype;
        private QuestVideoScreenPrototype videoScreenPrototype;
        private QuestAppShellPrototype appShellPrototype;
        private QuestPlaylistPrototype subscribedPlaylist;
        private Font uiFont;

        private Text connectionText;
        private QuestUiSurface connectionDot;
        private RectTransform queueBadge;
        private Text queueBadgeText;
        private Text songMetaText;
        private Text songTitleText;
        private Text songDetailText;
        private Text transportHoverText;
        private Text headerHoverText;
        private Text voiceSummaryText;
        private Text voiceModeSummaryText;
        private QuestUiSurface voiceLiveDot;
        private QuestUiSurface[] waveBars;

        private Button queuePageButton;
        private Button settingsPageButton;
        private Button replayButton;
        private Button previousButton;
        private Button playPauseButton;
        private Button nextButton;
        private Button microphoneButton;
        private Button voicePageButton;
        private QuestUiIcon playPauseIcon;
        private QuestUiIcon microphoneIcon;
        private QuestUiSurface microphoneSurface;

        private Button voiceBackButton;
        private Toggle voiceMicrophoneToggle;
        private Slider inputMeterSlider;
        private Slider monitorVolumeSlider;
        private readonly Button[] presetButtons = new Button[4];
        private readonly QuestUiSurface[] presetSurfaces = new QuestUiSurface[4];

        private Button queueBackButton;
        private readonly RectTransform[] queueRows = new RectTransform[QueueRowCount];
        private readonly QuestUiSurface[] queueRowSurfaces = new QuestUiSurface[QueueRowCount];
        private readonly QuestUiSurface[] queueIndicators = new QuestUiSurface[QueueRowCount];
        private readonly Text[] queueTitleTexts = new Text[QueueRowCount];
        private readonly Text[] queueMetaTexts = new Text[QueueRowCount];
        private Text queueFooterText;

        private Button settingsBackButton;
        private Text settingsConnectionText;
        private InputField helperHostInput;
        private Button applyHostButton;
        private Button defaultHostButton;
        private Toggle monitorOutputToggle;
        private Toggle safetyToggle;
        private Toggle nativeToggle;
        private Button openDiagnosticsButton;

        private Button closeDiagnosticsButton;
        private Button debugScrimButton;
        private Toggle videoDebugToggle;
        private Button rawDetailsButton;
        private Text rawDetailsButtonText;
        private Text diagnosticsHealthText;
        private Text diagnosticsServiceText;
        private Text rawDiagnosticsText;
        private Button copyDiagnosticsButton;

        private UiPage currentPage;
        private bool isConfigured;
        private bool rawDetailsVisible;
        private float nextRefreshAt;
        private Coroutine pageTransition;
        private Coroutine drawerTransition;

        public void Configure(
            RectTransform targetPanel,
            QuestAudioPrototype audio,
            QuestPlaylistPrototype playlist,
            QuestVideoScreenPrototype video,
            QuestAppShellPrototype appShell)
        {
            panel = targetPanel;
            audioPrototype = audio;
            playlistPrototype = playlist;
            videoScreenPrototype = video;
            appShellPrototype = appShell;
            uiFont = Resources.Load<Font>("Fonts/NotoSansSC-VF") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (panel == null)
            {
                return;
            }

            EnsureUiHierarchy();
            WireUi();
            SubscribePlaylist();
            ShowPageImmediate(UiPage.Home);
            SetDebugDrawerImmediate(false);
            RefreshAll();
            isConfigured = true;
        }

        private void OnEnable()
        {
            SubscribePlaylist();
        }

        private void OnDisable()
        {
            UnsubscribePlaylist();
        }

        private void Update()
        {
            if (!isConfigured || Time.unscaledTime < nextRefreshAt)
            {
                return;
            }

            nextRefreshAt = Time.unscaledTime + RefreshIntervalSeconds;
            RefreshAll();
        }

        private void EnsureUiHierarchy()
        {
            panel.sizeDelta = QuestAppShellPrototype.ControlPanelSize;
            var panelSurface = panel.GetComponent<QuestUiSurface>();
            if (panelSurface == null)
            {
                var legacyImage = panel.GetComponent<Image>();
                if (legacyImage != null)
                {
                    legacyImage.color = ScreenBackground;
                }
            }
            else
            {
                panelSurface.color = ScreenBackground;
            }

            consumerRoot = EnsureRect(panel, RootName, Vector2.zero, QuestAppShellPrototype.ControlPanelSize);
            homePage = EnsurePage(consumerRoot, "Home Page", out homeGroup);
            voicePage = EnsurePage(consumerRoot, "Voice Page", out voiceGroup);
            queuePage = EnsurePage(consumerRoot, "Queue Page", out queueGroup);
            settingsPage = EnsurePage(consumerRoot, "Settings Page", out settingsGroup);

            BuildHomePage();
            BuildVoicePage();
            BuildQueuePage();
            BuildSettingsPage();
            BuildDebugDrawer();
            HideLegacyUi();
            consumerRoot.SetAsLastSibling();
        }

        private void BuildHomePage()
        {
            CreateDivider(homePage, "Header Divider", new Vector2(0f, 216f), new Vector2(1070f, 1f));
            CreateDivider(homePage, "Transport Top Divider", new Vector2(0f, 50f), new Vector2(1070f, 1f));
            CreateDivider(homePage, "Footer Divider", new Vector2(0f, -186f), new Vector2(1070f, 1f));

            var brandMark = EnsureRect(homePage, "Brand Mark", new Vector2(-510f, 248f), new Vector2(38f, 38f));
            var brandSurface = EnsureSurface(brandMark, new Color(0.03f, 0.11f, 0.09f, 1f), 19f, false);
            var moonIcon = EnsureIcon(brandMark, "Icon", QuestUiIconKind.Moon, Vector2.zero, new Vector2(24f, 24f), AccentStrong);
            moonIcon.StrokeWidth = 2f;
            brandSurface.raycastTarget = false;
            CreateText(homePage, "Brand", "TsukiVox", 20, FontStyle.Bold, new Vector2(-454f, 256f), new Vector2(190f, 28f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(homePage, "Brand CN", "月读声域", 12, FontStyle.Normal, new Vector2(-454f, 235f), new Vector2(190f, 22f), TextAnchor.MiddleLeft, TextSecondary);

            connectionDot = EnsureSurface(EnsureRect(homePage, "Connection Dot", new Vector2(238f, 248f), new Vector2(10f, 10f)), Accent, 5f, false);
            connectionText = CreateText(homePage, "Connection", "点歌服务已连接", 14, FontStyle.Normal, new Vector2(335f, 248f), new Vector2(176f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            headerHoverText = CreateText(homePage, "Header Hover Label", string.Empty, 12, FontStyle.Bold, new Vector2(382f, 207f), new Vector2(230f, 22f), TextAnchor.MiddleCenter, AccentStrong);

            queuePageButton = CreateIconButton(homePage, "Open Queue", QuestUiIconKind.Queue, new Vector2(474f, 248f), new Vector2(48f, 48f), Surface, TextPrimary, out _);
            settingsPageButton = CreateIconButton(homePage, "Open Settings", QuestUiIconKind.Settings, new Vector2(532f, 248f), new Vector2(48f, 48f), Surface, TextPrimary, out _);
            ConfigureHover(queuePageButton, headerHoverText, "播放队列");
            ConfigureHover(settingsPageButton, headerHoverText, "设置");

            queueBadge = EnsureRect(queuePageButton.transform, "Badge", new Vector2(20f, 20f), new Vector2(22f, 22f));
            EnsureSurface(queueBadge, Accent, 11f, false);
            queueBadgeText = CreateText(queueBadge, "Label", "0", 11, FontStyle.Bold, Vector2.zero, queueBadge.sizeDelta, TextAnchor.MiddleCenter, AccentInk);

            songMetaText = CreateText(homePage, "Song Meta", "播放队列为空", 14, FontStyle.Bold, new Vector2(-330f, 166f), new Vector2(430f, 28f), TextAnchor.MiddleLeft, Accent);
            songTitleText = CreateText(homePage, "Song Title", "等待点歌", 40, FontStyle.Bold, new Vector2(-248f, 116f), new Vector2(590f, 64f), TextAnchor.MiddleLeft, TextPrimary);
            songTitleText.resizeTextForBestFit = true;
            songTitleText.resizeTextMinSize = 24;
            songTitleText.resizeTextMaxSize = 40;
            songDetailText = CreateText(homePage, "Song Detail", "从 PC 添加歌曲后即可开始", 15, FontStyle.Normal, new Vector2(-300f, 71f), new Vector2(490f, 28f), TextAnchor.MiddleLeft, TextSecondary);

            waveBars = new QuestUiSurface[8];
            var waveHeights = new[] { 20f, 38f, 58f, 30f, 50f, 24f, 40f, 16f };
            for (var index = 0; index < waveBars.Length; index += 1)
            {
                var barRect = EnsureRect(homePage, $"Wave Bar {index}", new Vector2(332f + index * 20f, 119f), new Vector2(6f, waveHeights[index]));
                waveBars[index] = EnsureSurface(barRect, Accent, 3f, false);
            }

            transportHoverText = CreateText(homePage, "Transport Hover Label", string.Empty, 13, FontStyle.Bold, new Vector2(0f, -126f), new Vector2(300f, 28f), TextAnchor.MiddleCenter, AccentStrong);
            replayButton = CreateIconButton(homePage, "Replay", QuestUiIconKind.Replay, new Vector2(-204f, -38f), new Vector2(84f, 84f), Surface, TextPrimary, out _);
            previousButton = CreateIconButton(homePage, "Previous", QuestUiIconKind.Previous, new Vector2(-102f, -38f), new Vector2(84f, 84f), Surface, TextPrimary, out _);
            playPauseButton = CreateIconButton(homePage, "Play Pause", QuestUiIconKind.Play, new Vector2(0f, -38f), new Vector2(96f, 96f), Accent, AccentInk, out playPauseIcon);
            nextButton = CreateIconButton(homePage, "Next", QuestUiIconKind.Next, new Vector2(102f, -38f), new Vector2(84f, 84f), Surface, TextPrimary, out _);
            microphoneButton = CreateIconButton(homePage, "Microphone", QuestUiIconKind.Microphone, new Vector2(204f, -38f), new Vector2(84f, 84f), WarmSurface, Warm, out microphoneIcon);
            microphoneSurface = microphoneButton.targetGraphic as QuestUiSurface;
            ConfigureHover(replayButton, transportHoverText, "重播");
            ConfigureHover(previousButton, transportHoverText, "上一首");
            ConfigureHover(playPauseButton, transportHoverText, "播放");
            ConfigureHover(nextButton, transportHoverText, "下一首");
            ConfigureHover(microphoneButton, transportHoverText, "麦克风");

            voicePageButton = CreateSurfaceButton(homePage, "Voice Summary", new Vector2(0f, -232f), new Vector2(1070f, 74f), Color.clear, Color.clear);
            voiceLiveDot = EnsureSurface(EnsureRect(voicePageButton.transform, "Live Dot", new Vector2(-500f, 0f), new Vector2(10f, 10f)), Warm, 5f, false);
            voiceSummaryText = CreateText(voicePageButton.transform, "Status", "麦克风已开启", 15, FontStyle.Bold, new Vector2(-407f, 0f), new Vector2(170f, 36f), TextAnchor.MiddleLeft, TextPrimary);
            voiceModeSummaryText = CreateText(voicePageButton.transform, "Mode", "人声 · KTV", 14, FontStyle.Normal, new Vector2(-235f, 0f), new Vector2(170f, 36f), TextAnchor.MiddleLeft, TextSecondary);
            CreateText(voicePageButton.transform, "Command", "调整人声", 14, FontStyle.Normal, new Vector2(432f, 0f), new Vector2(130f, 36f), TextAnchor.MiddleRight, TextSecondary);
            EnsureIcon(voicePageButton.transform, "Chevron", QuestUiIconKind.ChevronRight, new Vector2(512f, 0f), new Vector2(20f, 20f), TextSecondary);
        }

        private void BuildVoicePage()
        {
            BuildSubpageHeader(voicePage, "人声", out voiceBackButton);

            CreateText(voicePage, "Microphone Title", "麦克风", 17, FontStyle.Bold, new Vector2(-470f, 150f), new Vector2(180f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(voicePage, "Microphone Hint", "开启后可听到实时返听", 12, FontStyle.Normal, new Vector2(-395f, 121f), new Vector2(330f, 24f), TextAnchor.MiddleLeft, TextSecondary);
            inputMeterSlider = CreateSlider(voicePage, "Input Meter", new Vector2(125f, 139f), new Vector2(510f, 44f), false);
            voiceMicrophoneToggle = CreateSwitch(voicePage, "Microphone Switch", new Vector2(493f, 139f));

            CreateDivider(voicePage, "Microphone Divider", new Vector2(0f, 82f), new Vector2(1030f, 1f));
            CreateText(voicePage, "Volume Title", "返听音量", 17, FontStyle.Bold, new Vector2(-470f, 38f), new Vector2(180f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(voicePage, "Volume Hint", "建议先低后高", 12, FontStyle.Normal, new Vector2(-420f, 9f), new Vector2(280f, 24f), TextAnchor.MiddleLeft, TextSecondary);
            monitorVolumeSlider = CreateSlider(voicePage, "Monitor Volume", new Vector2(120f, 28f), new Vector2(520f, 48f), true);
            EnsureIcon(voicePage, "Volume Icon", QuestUiIconKind.Volume, new Vector2(492f, 28f), new Vector2(28f, 28f), TextSecondary);

            CreateDivider(voicePage, "Volume Divider", new Vector2(0f, -31f), new Vector2(1030f, 1f));
            CreateText(voicePage, "Preset Title", "人声效果", 17, FontStyle.Bold, new Vector2(-470f, -80f), new Vector2(180f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(voicePage, "Preset Hint", "四档预设直接选择", 12, FontStyle.Normal, new Vector2(-395f, -109f), new Vector2(330f, 24f), TextAnchor.MiddleLeft, TextSecondary);

            var labels = new[] { "原声", "KTV", "强效", "柔和" };
            for (var index = 0; index < presetButtons.Length; index += 1)
            {
                presetButtons[index] = CreateTextButton(voicePage, $"Preset {index}", labels[index], new Vector2(-168f + index * 178f, -96f), new Vector2(162f, 52f), Surface, TextPrimary);
                presetSurfaces[index] = presetButtons[index].targetGraphic as QuestUiSurface;
            }

            var safetyNote = EnsureRect(voicePage, "Safety Note", new Vector2(0f, -196f), new Vector2(1020f, 58f));
            EnsureSurface(safetyNote, new Color(0.09f, 0.11f, 0.1f, 1f), 6f, false);
            CreateText(safetyNote, "Label", "安全保护会在返听过响时自动降低音量", 13, FontStyle.Normal, Vector2.zero, new Vector2(930f, 34f), TextAnchor.MiddleCenter, TextSecondary);
        }

        private void BuildQueuePage()
        {
            BuildSubpageHeader(queuePage, "播放队列", out queueBackButton);
            var firstY = 164f;
            for (var index = 0; index < QueueRowCount; index += 1)
            {
                var row = EnsureRect(queuePage, $"Queue Row {index}", new Vector2(0f, firstY - index * 76f), new Vector2(1030f, 66f));
                queueRows[index] = row;
                queueRowSurfaces[index] = EnsureSurface(row, Surface, 6f, false);
                queueIndicators[index] = EnsureSurface(EnsureRect(row, "Indicator", new Vector2(-501f, 0f), new Vector2(5f, 42f)), Accent, 2f, false);
                queueTitleTexts[index] = CreateText(row, "Title", "歌曲", 16, FontStyle.Bold, new Vector2(-310f, 11f), new Vector2(360f, 28f), TextAnchor.MiddleLeft, TextPrimary);
                queueMetaTexts[index] = CreateText(row, "Meta", "等待", 12, FontStyle.Normal, new Vector2(-310f, -14f), new Vector2(360f, 22f), TextAnchor.MiddleLeft, TextSecondary);
                CreateText(row, "Number", (index + 1).ToString("00"), 13, FontStyle.Bold, new Vector2(-455f, 0f), new Vector2(52f, 30f), TextAnchor.MiddleCenter, TextSecondary);
            }

            queueFooterText = CreateText(queuePage, "Queue Footer", "队列为空", 13, FontStyle.Normal, new Vector2(0f, -238f), new Vector2(700f, 30f), TextAnchor.MiddleCenter, TextSecondary);
        }

        private void BuildSettingsPage()
        {
            BuildSubpageHeader(settingsPage, "设置", out settingsBackButton);

            CreateText(settingsPage, "Service Section", "点歌服务", 13, FontStyle.Bold, new Vector2(-468f, 177f), new Vector2(200f, 28f), TextAnchor.MiddleLeft, TextSecondary);
            settingsConnectionText = CreateText(settingsPage, "Service Status", "正在连接", 14, FontStyle.Bold, new Vector2(360f, 177f), new Vector2(300f, 28f), TextAnchor.MiddleRight, Accent);
            CreateText(settingsPage, "Host Label", "PC IP", 14, FontStyle.Bold, new Vector2(-472f, 126f), new Vector2(100f, 42f), TextAnchor.MiddleLeft, TextPrimary);
            helperHostInput = CreateInputField(settingsPage, "Helper Host", QuestPlaylistPrototype.DefaultHelperHostAddress, new Vector2(-120f, 126f), new Vector2(500f, 48f));
            applyHostButton = CreateTextButton(settingsPage, "Apply Host", "应用", new Vector2(222f, 126f), new Vector2(124f, 48f), Accent, AccentInk);
            defaultHostButton = CreateTextButton(settingsPage, "Default Host", "使用默认", new Vector2(410f, 126f), new Vector2(160f, 48f), Surface, TextPrimary);

            CreateDivider(settingsPage, "Service Divider", new Vector2(0f, 76f), new Vector2(1030f, 1f));
            CreateText(settingsPage, "Audio Section", "音频高级设置", 13, FontStyle.Bold, new Vector2(-430f, 45f), new Vector2(280f, 28f), TextAnchor.MiddleLeft, TextSecondary);
            CreateSettingToggle(settingsPage, "Monitor Output", "返听输出", "关闭后仍保留麦克风输入", 0f, out monitorOutputToggle);
            CreateSettingToggle(settingsPage, "Safety Limiter", "安全保护", "建议始终保持开启", -72f, out safetyToggle);
            CreateSettingToggle(settingsPage, "Native Backend", "Native 低延迟", "原声路径，不包含 KTV 效果", -144f, out nativeToggle);

            CreateDivider(settingsPage, "Audio Divider", new Vector2(0f, -190f), new Vector2(1030f, 1f));
            openDiagnosticsButton = CreateSurfaceButton(settingsPage, "Open Diagnostics", new Vector2(0f, -231f), new Vector2(1030f, 62f), Surface, Line);
            CreateText(openDiagnosticsButton.transform, "Title", "诊断与支持", 16, FontStyle.Bold, new Vector2(-390f, 10f), new Vector2(280f, 28f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(openDiagnosticsButton.transform, "Hint", "视频调试、原始状态与复制诊断信息", 12, FontStyle.Normal, new Vector2(-310f, -15f), new Vector2(440f, 22f), TextAnchor.MiddleLeft, TextSecondary);
            EnsureIcon(openDiagnosticsButton.transform, "Chevron", QuestUiIconKind.ChevronRight, new Vector2(480f, 0f), new Vector2(22f, 22f), TextSecondary);
        }

        private void BuildDebugDrawer()
        {
            debugScrim = EnsureRect(consumerRoot, "Debug Scrim", Vector2.zero, QuestAppShellPrototype.ControlPanelSize);
            var scrimSurface = EnsureSurface(debugScrim, new Color(0f, 0f, 0f, 0.58f), 0f, true);
            debugScrimButton = GetOrAddComponent<Button>(debugScrim.gameObject);
            debugScrimButton.targetGraphic = scrimSurface;
            debugScrimButton.transition = Selectable.Transition.None;
            debugScrimGroup = GetOrAddComponent<CanvasGroup>(debugScrim.gameObject);

            debugDrawer = EnsureRect(consumerRoot, "Debug Drawer", new Vector2(250f, 0f), new Vector2(620f, 560f));
            EnsureSurface(debugDrawer, new Color(0.055f, 0.082f, 0.079f, 1f), 0f, true);
            debugDrawerGroup = GetOrAddComponent<CanvasGroup>(debugDrawer.gameObject);
            CreateDivider(debugDrawer, "Header Divider", new Vector2(0f, 216f), new Vector2(572f, 1f));
            CreateText(debugDrawer, "Title", "诊断与支持", 21, FontStyle.Bold, new Vector2(-170f, 248f), new Vector2(310f, 44f), TextAnchor.MiddleLeft, TextPrimary);
            closeDiagnosticsButton = CreateIconButton(debugDrawer, "Close", QuestUiIconKind.Close, new Vector2(266f, 248f), new Vector2(44f, 44f), Surface, TextPrimary, out _);

            diagnosticsHealthText = CreateText(debugDrawer, "Health", "应用正常 · 音频正常 · 视频正常", 14, FontStyle.Bold, new Vector2(0f, 174f), new Vector2(548f, 52f), TextAnchor.MiddleLeft, TextPrimary);
            CreateDivider(debugDrawer, "Health Divider", new Vector2(0f, 138f), new Vector2(548f, 1f));
            diagnosticsServiceText = CreateText(debugDrawer, "Service", "点歌服务：未连接", 13, FontStyle.Normal, new Vector2(0f, 104f), new Vector2(548f, 44f), TextAnchor.MiddleLeft, TextSecondary);
            CreateDivider(debugDrawer, "Service Divider", new Vector2(0f, 70f), new Vector2(548f, 1f));

            CreateText(debugDrawer, "Video Debug Label", "显示视频调试信息", 14, FontStyle.Bold, new Vector2(-128f, 35f), new Vector2(300f, 42f), TextAnchor.MiddleLeft, TextPrimary);
            videoDebugToggle = CreateSwitch(debugDrawer, "Video Debug Switch", new Vector2(244f, 35f));
            CreateDivider(debugDrawer, "Video Divider", new Vector2(0f, 1f), new Vector2(548f, 1f));

            rawDetailsButton = CreateSurfaceButton(debugDrawer, "Raw Details Toggle", new Vector2(0f, -39f), new Vector2(548f, 60f), Color.clear, Color.clear);
            rawDetailsButtonText = CreateText(rawDetailsButton.transform, "Title", "展开原始详情", 14, FontStyle.Bold, new Vector2(-112f, 0f), new Vector2(310f, 34f), TextAnchor.MiddleLeft, TextPrimary);
            EnsureIcon(rawDetailsButton.transform, "Chevron", QuestUiIconKind.ChevronRight, new Vector2(250f, 0f), new Vector2(20f, 20f), TextSecondary);

            rawDetailsRoot = EnsureRect(debugDrawer, "Raw Details", new Vector2(0f, -119f), new Vector2(548f, 112f));
            EnsureSurface(rawDetailsRoot, new Color(0.035f, 0.052f, 0.051f, 1f), 6f, false);
            rawDiagnosticsText = CreateText(rawDetailsRoot, "Text", string.Empty, 11, FontStyle.Normal, Vector2.zero, new Vector2(510f, 92f), TextAnchor.UpperLeft, TextSecondary);
            rawDiagnosticsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            rawDiagnosticsText.verticalOverflow = VerticalWrapMode.Truncate;

            copyDiagnosticsButton = CreateTextButton(debugDrawer, "Copy Complete Diagnostics", "复制完整诊断信息", new Vector2(0f, -226f), new Vector2(548f, 52f), new Color(0.035f, 0.11f, 0.09f, 1f), AccentStrong);
            EnsureIcon(copyDiagnosticsButton.transform, "Icon", QuestUiIconKind.Copy, new Vector2(-160f, 0f), new Vector2(22f, 22f), AccentStrong);

            debugScrim.SetAsLastSibling();
            debugDrawer.SetAsLastSibling();
        }

        private void WireUi()
        {
            WireButton(queuePageButton, () => ShowPage(UiPage.Queue));
            WireButton(settingsPageButton, () => ShowPage(UiPage.Settings));
            WireButton(voicePageButton, () => ShowPage(UiPage.Voice));
            WireButton(voiceBackButton, () => ShowPage(UiPage.Home));
            WireButton(queueBackButton, () => ShowPage(UiPage.Home));
            WireButton(settingsBackButton, () => ShowPage(UiPage.Home));

            WireButton(replayButton, () => playlistPrototype?.SendReplay());
            WireButton(previousButton, () => playlistPrototype?.SendPrevious());
            WireButton(playPauseButton, () => playlistPrototype?.SendPlayPause());
            WireButton(nextButton, () => playlistPrototype?.SendNext());
            WireButton(microphoneButton, () => audioPrototype?.ToggleMonitoring());

            voiceMicrophoneToggle.onValueChanged.RemoveAllListeners();
            voiceMicrophoneToggle.onValueChanged.AddListener(HandleMicrophoneToggle);
            monitorVolumeSlider.onValueChanged.RemoveAllListeners();
            monitorVolumeSlider.onValueChanged.AddListener(value => audioPrototype?.SetMonitorVolume(value));
            for (var index = 0; index < presetButtons.Length; index += 1)
            {
                var presetIndex = index;
                WireButton(presetButtons[index], () => audioPrototype?.SelectPreset(presetIndex));
            }

            WireButton(applyHostButton, ApplyHelperHost);
            WireButton(defaultHostButton, ApplyDefaultHelperHost);
            monitorOutputToggle.onValueChanged.RemoveAllListeners();
            monitorOutputToggle.onValueChanged.AddListener(value => audioPrototype?.SetMonitorOutput(value));
            safetyToggle.onValueChanged.RemoveAllListeners();
            safetyToggle.onValueChanged.AddListener(value => audioPrototype?.SetSafetyLimiterEnabled(value));
            nativeToggle.onValueChanged.RemoveAllListeners();
            nativeToggle.onValueChanged.AddListener(value => audioPrototype?.SetPreferNativeBackend(value));

            WireButton(openDiagnosticsButton, () => SetDebugDrawerVisible(true));
            WireButton(closeDiagnosticsButton, () => SetDebugDrawerVisible(false));
            WireButton(debugScrimButton, () => SetDebugDrawerVisible(false));
            videoDebugToggle.onValueChanged.RemoveAllListeners();
            videoDebugToggle.onValueChanged.AddListener(value => videoScreenPrototype?.SetStatusOverlayVisible(value));
            WireButton(rawDetailsButton, ToggleRawDetails);
            WireButton(copyDiagnosticsButton, () => appShellPrototype?.CopyCompleteDebugInfoToClipboard());
        }

        private void RefreshAll()
        {
            RefreshHome();
            RefreshVoice();
            RefreshQueue();
            RefreshSettings();
            RefreshDiagnostics();
        }

        private void RefreshHome()
        {
            var connected = playlistPrototype != null && playlistPrototype.IsConnected;
            connectionText.text = connected ? "点歌服务已连接" : "点歌服务离线";
            connectionText.color = connected ? TextPrimary : Warm;
            connectionDot.color = connected ? Accent : Warm;

            var state = playlistPrototype?.CurrentState;
            var count = state?.QueueCount ?? 0;
            queueBadge.gameObject.SetActive(count > 0);
            queueBadgeText.text = Mathf.Min(count, 99).ToString();

            var item = state?.CurrentItem;
            if (item == null)
            {
                songMetaText.text = connected ? "播放队列为空" : "点歌服务未连接";
                songTitleText.text = "等待点歌";
                songDetailText.text = connected ? "从 PC 添加歌曲后即可开始" : "请在设置中检查 PC IP";
            }
            else
            {
                var index = Mathf.Clamp(state.currentIndex + 1, 1, Mathf.Max(count, 1));
                songMetaText.text = item.status == PlaylistClient.StatusDownloading
                    ? "正在准备歌曲"
                    : $"正在播放 · 第 {index} / {count} 首";
                songTitleText.text = SafeText(item.title, "未命名歌曲");
                songDetailText.text = $"{FormatSource(item.sourceType)} · {FormatPlayback(item, state.playback)}";
            }

            var isPlaying = string.Equals(state?.playback, "playing", StringComparison.OrdinalIgnoreCase);
            playPauseIcon.SetIcon(isPlaying ? QuestUiIconKind.Pause : QuestUiIconKind.Play);
            ConfigureHover(playPauseButton, transportHoverText, isPlaying ? "暂停" : "播放");

            var canSend = playlistPrototype != null && playlistPrototype.CanSendControl;
            var hasReadyItem = item != null && item.IsReady;
            replayButton.interactable = canSend && hasReadyItem;
            previousButton.interactable = canSend && count > 0;
            playPauseButton.interactable = canSend && hasReadyItem;
            nextButton.interactable = canSend && count > 0;

            var micLive = audioPrototype != null && audioPrototype.IsMonitoring;
            microphoneButton.interactable = audioPrototype != null && !audioPrototype.IsWaitingForPermission;
            microphoneSurface.color = micLive ? WarmSurface : Surface;
            microphoneIcon.color = micLive ? Warm : TextSecondary;
            voiceLiveDot.color = micLive ? Warm : TextSecondary;
            voiceSummaryText.text = audioPrototype != null && audioPrototype.IsWaitingForPermission
                ? "等待麦克风权限"
                : micLive ? "麦克风已开启" : "麦克风已关闭";
            voiceModeSummaryText.text = $"人声 · {FormatPreset(audioPrototype?.CurrentPresetIndex ?? 0)}";

            var inputLevel = audioPrototype?.InputLevel ?? 0f;
            for (var index = 0; index < waveBars.Length; index += 1)
            {
                var baseHeight = 12f + ((index * 17) % 5) * 7f;
                var height = Mathf.Lerp(8f, baseHeight + 36f, Mathf.Clamp01(inputLevel * (0.7f + index * 0.05f)));
                waveBars[index].rectTransform.sizeDelta = new Vector2(6f, height);
                waveBars[index].color = micLive ? Accent : new Color(0.18f, 0.26f, 0.25f, 1f);
            }
        }

        private void RefreshVoice()
        {
            if (audioPrototype == null)
            {
                return;
            }

            voiceMicrophoneToggle.SetIsOnWithoutNotify(audioPrototype.IsMonitoring);
            RefreshSwitchVisual(voiceMicrophoneToggle, Warm);
            inputMeterSlider.SetValueWithoutNotify(audioPrototype.InputLevel);
            monitorVolumeSlider.maxValue = audioPrototype.MonitorVolumeMaximum;
            monitorVolumeSlider.SetValueWithoutNotify(audioPrototype.MonitorVolume);

            for (var index = 0; index < presetButtons.Length; index += 1)
            {
                var isSelected = audioPrototype.CurrentPresetIndex == index;
                presetSurfaces[index].color = isSelected ? Accent : Surface;
                var label = presetButtons[index].GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.color = isSelected ? AccentInk : TextPrimary;
                }
            }
        }

        private void RefreshQueue()
        {
            var state = playlistPrototype?.CurrentState;
            var count = state?.QueueCount ?? 0;
            var startIndex = count <= QueueRowCount
                ? 0
                : Mathf.Clamp(state.currentIndex, 0, count - QueueRowCount);

            for (var rowIndex = 0; rowIndex < QueueRowCount; rowIndex += 1)
            {
                var itemIndex = startIndex + rowIndex;
                var visible = state?.queue != null && itemIndex >= 0 && itemIndex < count;
                queueRows[rowIndex].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var item = state.queue[itemIndex];
                var isCurrent = itemIndex == state.currentIndex;
                queueRowSurfaces[rowIndex].color = isCurrent
                    ? new Color(0.035f, 0.16f, 0.13f, 1f)
                    : Surface;
                queueIndicators[rowIndex].gameObject.SetActive(isCurrent);
                queueTitleTexts[rowIndex].text = SafeText(item.title, "未命名歌曲");
                queueMetaTexts[rowIndex].text = $"{FormatSource(item.sourceType)} · {FormatItemStatus(item.status)}";
                var numberText = queueRows[rowIndex].Find("Number")?.GetComponent<Text>();
                if (numberText != null)
                {
                    numberText.text = (itemIndex + 1).ToString("00");
                }
            }

            queueFooterText.text = count == 0
                ? "播放队列为空"
                : count > QueueRowCount ? $"共 {count} 首 · 当前显示附近歌曲" : $"共 {count} 首";
        }

        private void RefreshSettings()
        {
            var connected = playlistPrototype != null && playlistPrototype.IsConnected;
            settingsConnectionText.text = connected ? "已连接" : "未连接";
            settingsConnectionText.color = connected ? Accent : Warm;
            if (helperHostInput != null && !helperHostInput.isFocused && playlistPrototype != null)
            {
                helperHostInput.SetTextWithoutNotify(playlistPrototype.HelperHost);
            }

            if (audioPrototype == null)
            {
                return;
            }

            monitorOutputToggle.SetIsOnWithoutNotify(audioPrototype.IsMonitorOutputEnabled);
            safetyToggle.SetIsOnWithoutNotify(audioPrototype.IsSafetyLimiterEnabled);
            nativeToggle.SetIsOnWithoutNotify(audioPrototype.PrefersNativeOboeBackend);
            RefreshSwitchVisual(monitorOutputToggle, Accent);
            RefreshSwitchVisual(safetyToggle, Accent);
            RefreshSwitchVisual(nativeToggle, Accent);
        }

        private void RefreshDiagnostics()
        {
            var audioStatus = audioPrototype == null
                ? "音频缺失"
                : audioPrototype.IsMonitoring ? "音频正常" : "音频待机";
            var videoStatus = videoScreenPrototype == null
                ? "视频缺失"
                : videoScreenPrototype.IsPreparing ? "视频准备中" : "视频正常";
            diagnosticsHealthText.text = $"应用正常 · {audioStatus} · {videoStatus}";
            diagnosticsHealthText.color = audioPrototype == null || videoScreenPrototype == null ? Warm : TextPrimary;

            diagnosticsServiceText.text = playlistPrototype != null && playlistPrototype.IsConnected
                ? $"点歌服务：已连接 · {playlistPrototype.HelperHost}"
                : $"点歌服务：未连接 · {playlistPrototype?.HelperHost ?? "未配置"}";

            if (videoScreenPrototype != null)
            {
                videoDebugToggle.SetIsOnWithoutNotify(videoScreenPrototype.IsStatusOverlayVisible);
                RefreshSwitchVisual(videoDebugToggle, Accent);
            }

            rawDiagnosticsText.text =
                $"音频后端  {audioPrototype?.ActiveBackendName ?? "missing"}\n" +
                $"人声预设  {audioPrototype?.CurrentPresetName ?? "missing"}\n" +
                $"输入/输出  {(audioPrototype?.InputLevel ?? 0f):P0} / {(audioPrototype?.OutputLevel ?? 0f):P0}\n" +
                $"播放服务  {playlistPrototype?.PlaylistOrigin ?? "missing"}\n" +
                $"视频状态  {SingleLine(videoScreenPrototype?.StatusSummary)}";
        }

        private void HandleMicrophoneToggle(bool enabled)
        {
            if (audioPrototype == null || enabled == audioPrototype.IsMonitoring)
            {
                return;
            }

            if (enabled)
            {
                audioPrototype.StartMonitoring();
            }
            else
            {
                audioPrototype.StopMonitoring();
            }
        }

        private void ApplyHelperHost()
        {
            playlistPrototype?.ApplyHelperHost(helperHostInput != null ? helperHostInput.text : string.Empty);
        }

        private void ApplyDefaultHelperHost()
        {
            if (helperHostInput != null)
            {
                helperHostInput.SetTextWithoutNotify(QuestPlaylistPrototype.DefaultHelperHostAddress);
            }

            playlistPrototype?.ApplyDefaultHelperHost();
        }

        private void ToggleRawDetails()
        {
            rawDetailsVisible = !rawDetailsVisible;
            rawDetailsRoot.gameObject.SetActive(rawDetailsVisible);
            rawDetailsButtonText.text = rawDetailsVisible ? "收起原始详情" : "展开原始详情";
        }

        private void SubscribePlaylist()
        {
            if (playlistPrototype == null || subscribedPlaylist == playlistPrototype)
            {
                return;
            }

            UnsubscribePlaylist();
            subscribedPlaylist = playlistPrototype;
            subscribedPlaylist.StateChanged += HandlePlaylistStateChanged;
        }

        private void UnsubscribePlaylist()
        {
            if (subscribedPlaylist == null)
            {
                return;
            }

            subscribedPlaylist.StateChanged -= HandlePlaylistStateChanged;
            subscribedPlaylist = null;
        }

        private void HandlePlaylistStateChanged(QuestPlaylistPrototype sender, PlaylistState state)
        {
            RefreshAll();
        }

        private void ShowPage(UiPage page)
        {
            if (page == currentPage)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                ShowPageImmediate(page);
                return;
            }

            if (pageTransition != null)
            {
                StopCoroutine(pageTransition);
            }

            pageTransition = StartCoroutine(AnimatePage(currentPage, page));
        }

        private IEnumerator AnimatePage(UiPage previous, UiPage next)
        {
            var previousGroup = GetPageGroup(previous);
            var nextGroup = GetPageGroup(next);
            var previousRect = previousGroup.GetComponent<RectTransform>();
            var nextRect = nextGroup.GetComponent<RectTransform>();
            nextGroup.gameObject.SetActive(true);
            nextGroup.alpha = 0f;
            nextRect.anchoredPosition = new Vector2(22f, 0f);

            const float duration = 0.14f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                nextGroup.alpha = eased;
                previousGroup.alpha = 1f - eased;
                nextRect.anchoredPosition = Vector2.Lerp(new Vector2(22f, 0f), Vector2.zero, eased);
                previousRect.anchoredPosition = Vector2.Lerp(Vector2.zero, new Vector2(-12f, 0f), eased);
                yield return null;
            }

            previousGroup.gameObject.SetActive(false);
            previousGroup.alpha = 1f;
            previousRect.anchoredPosition = Vector2.zero;
            nextGroup.alpha = 1f;
            nextRect.anchoredPosition = Vector2.zero;
            currentPage = next;
            pageTransition = null;
        }

        private void ShowPageImmediate(UiPage page)
        {
            currentPage = page;
            SetPageGroupImmediate(homeGroup, page == UiPage.Home);
            SetPageGroupImmediate(voiceGroup, page == UiPage.Voice);
            SetPageGroupImmediate(queueGroup, page == UiPage.Queue);
            SetPageGroupImmediate(settingsGroup, page == UiPage.Settings);
        }

        private void SetDebugDrawerVisible(bool visible)
        {
            if (!Application.isPlaying)
            {
                SetDebugDrawerImmediate(visible);
                return;
            }

            if (drawerTransition != null)
            {
                StopCoroutine(drawerTransition);
            }

            drawerTransition = StartCoroutine(AnimateDebugDrawer(visible));
        }

        private IEnumerator AnimateDebugDrawer(bool visible)
        {
            if (visible)
            {
                debugScrim.gameObject.SetActive(true);
                debugDrawer.gameObject.SetActive(true);
            }

            var startAlpha = debugDrawerGroup.alpha;
            var endAlpha = visible ? 1f : 0f;
            var startX = debugDrawer.anchoredPosition.x;
            var endX = visible ? 250f : 310f;
            const float duration = 0.16f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / duration), 3f);
                debugDrawerGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                debugScrimGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
                debugDrawer.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, t), 0f);
                yield return null;
            }

            SetDebugDrawerImmediate(visible);
            drawerTransition = null;
        }

        private void SetDebugDrawerImmediate(bool visible)
        {
            debugScrim.gameObject.SetActive(visible);
            debugDrawer.gameObject.SetActive(visible);
            debugScrimGroup.alpha = visible ? 1f : 0f;
            debugDrawerGroup.alpha = visible ? 1f : 0f;
            debugDrawer.anchoredPosition = new Vector2(visible ? 250f : 310f, 0f);
            rawDetailsVisible = false;
            rawDetailsRoot.gameObject.SetActive(false);
            rawDetailsButtonText.text = "展开原始详情";
        }

        private CanvasGroup GetPageGroup(UiPage page)
        {
            return page switch
            {
                UiPage.Voice => voiceGroup,
                UiPage.Queue => queueGroup,
                UiPage.Settings => settingsGroup,
                _ => homeGroup,
            };
        }

        private static void SetPageGroupImmediate(CanvasGroup group, bool visible)
        {
            group.gameObject.SetActive(visible);
            group.alpha = 1f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            group.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        }

        private void HideLegacyUi()
        {
            for (var index = 0; index < panel.childCount; index += 1)
            {
                var child = panel.GetChild(index);
                child.gameObject.SetActive(child == consumerRoot);
            }
        }

        private RectTransform EnsurePage(Transform parent, string name, out CanvasGroup canvasGroup)
        {
            var page = EnsureRect(parent, name, Vector2.zero, QuestAppShellPrototype.ControlPanelSize);
            canvasGroup = GetOrAddComponent<CanvasGroup>(page.gameObject);
            return page;
        }

        private void BuildSubpageHeader(RectTransform page, string title, out Button backButton)
        {
            CreateDivider(page, "Header Divider", new Vector2(0f, 216f), new Vector2(1070f, 1f));
            backButton = CreateIconButton(page, "Back", QuestUiIconKind.Back, new Vector2(-516f, 248f), new Vector2(44f, 44f), Surface, TextPrimary, out _);
            CreateText(page, "Page Title", title, 22, FontStyle.Bold, new Vector2(-430f, 248f), new Vector2(280f, 44f), TextAnchor.MiddleLeft, TextPrimary);
        }

        private void CreateSettingToggle(RectTransform parent, string name, string title, string hint, float y, out Toggle toggle)
        {
            CreateText(parent, $"{name} Title", title, 15, FontStyle.Bold, new Vector2(-390f, y), new Vector2(300f, 28f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(parent, $"{name} Hint", hint, 11, FontStyle.Normal, new Vector2(-330f, y - 25f), new Vector2(420f, 22f), TextAnchor.MiddleLeft, TextSecondary);
            toggle = CreateSwitch(parent, $"{name} Switch", new Vector2(485f, y - 5f));
            CreateDivider(parent, $"{name} Divider", new Vector2(0f, y - 39f), new Vector2(1030f, 1f));
        }

        private Button CreateIconButton(
            Transform parent,
            string name,
            QuestUiIconKind iconKind,
            Vector2 position,
            Vector2 size,
            Color background,
            Color iconColor,
            out QuestUiIcon icon)
        {
            var button = CreateSurfaceButton(parent, name, position, size, background, Line);
            icon = EnsureIcon(button.transform, "Icon", iconKind, Vector2.zero, size * 0.46f, iconColor);
            return button;
        }

        private Button CreateTextButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color background, Color textColor)
        {
            var button = CreateSurfaceButton(parent, name, position, size, background, Line);
            CreateText(button.transform, "Label", label, 14, FontStyle.Bold, Vector2.zero, size - new Vector2(18f, 10f), TextAnchor.MiddleCenter, textColor);
            return button;
        }

        private Button CreateSurfaceButton(Transform parent, string name, Vector2 position, Vector2 size, Color background, Color border)
        {
            var rect = EnsureRect(parent, name, position, size);
            var surface = EnsureSurface(rect, background, Mathf.Min(8f, size.y * 0.15f), true);
            var outline = GetOrAddComponent<Outline>(rect.gameObject);
            outline.effectColor = border;
            outline.effectDistance = border.a > 0f ? new Vector2(1f, -1f) : Vector2.zero;
            outline.useGraphicAlpha = false;
            var button = GetOrAddComponent<Button>(rect.gameObject);
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateButtonColors(background);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private Slider CreateSlider(Transform parent, string name, Vector2 position, Vector2 size, bool showHandle)
        {
            var root = EnsureRect(parent, name, position, size);
            var backgroundRect = EnsureRect(root, "Track", Vector2.zero, new Vector2(size.x, 10f));
            var background = EnsureSurface(backgroundRect, new Color(0.12f, 0.17f, 0.16f, 1f), 5f, true);
            var fillArea = EnsureRect(root, "Fill Area", Vector2.zero, new Vector2(size.x, 10f));
            var fillRect = EnsureRect(fillArea, "Fill", Vector2.zero, fillArea.sizeDelta);
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(1f, 0.5f);
            fillRect.sizeDelta = new Vector2(0f, 10f);
            var fill = EnsureSurface(fillRect, Accent, 5f, false);
            RectTransform handleRect = null;
            QuestUiSurface handle = null;
            if (showHandle)
            {
                var handleArea = EnsureRect(root, "Handle Slide Area", Vector2.zero, new Vector2(size.x - 20f, size.y));
                handleRect = EnsureRect(handleArea, "Handle", Vector2.zero, new Vector2(24f, 24f));
                handle = EnsureSurface(handleRect, AccentStrong, 12f, true);
            }

            var slider = GetOrAddComponent<Slider>(root.gameObject);
            slider.targetGraphic = showHandle ? handle : background;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.interactable = showHandle;
            return slider;
        }

        private Toggle CreateSwitch(Transform parent, string name, Vector2 position)
        {
            var root = EnsureRect(parent, name, position, new Vector2(60f, 32f));
            var surface = EnsureSurface(root, SurfaceRaised, 16f, true);
            var knob = EnsureRect(root, "Knob", new Vector2(-13f, 0f), new Vector2(24f, 24f));
            EnsureSurface(knob, TextSecondary, 12f, false);
            var toggle = GetOrAddComponent<Toggle>(root.gameObject);
            toggle.targetGraphic = surface;
            toggle.graphic = null;
            toggle.transition = Selectable.Transition.ColorTint;
            toggle.colors = CreateButtonColors(SurfaceRaised);
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            return toggle;
        }

        private InputField CreateInputField(Transform parent, string name, string placeholderValue, Vector2 position, Vector2 size)
        {
            var root = EnsureRect(parent, name, position, size);
            var surface = EnsureSurface(root, Surface, 6f, true);
            var text = CreateText(root, "Text", string.Empty, 15, FontStyle.Normal, Vector2.zero, size - new Vector2(28f, 6f), TextAnchor.MiddleLeft, TextPrimary);
            var placeholder = CreateText(root, "Placeholder", placeholderValue, 15, FontStyle.Normal, Vector2.zero, size - new Vector2(28f, 6f), TextAnchor.MiddleLeft, TextSecondary);
            var input = GetOrAddComponent<InputField>(root.gameObject);
            input.targetGraphic = surface;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = InputField.ContentType.Standard;
            input.characterLimit = 80;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            return input;
        }

        private void CreateDivider(Transform parent, string name, Vector2 position, Vector2 size)
        {
            EnsureSurface(EnsureRect(parent, name, position, size), Line, 0f, false);
        }

        private Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            Vector2 position,
            Vector2 size,
            TextAnchor alignment,
            Color color)
        {
            var rect = EnsureRect(parent, name, position, size);
            var text = GetOrAddComponent<Text>(rect.gameObject);
            text.font = uiFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private QuestUiIcon EnsureIcon(Transform parent, string name, QuestUiIconKind kind, Vector2 position, Vector2 size, Color color)
        {
            var rect = EnsureRect(parent, name, position, size);
            var icon = GetOrAddComponent<QuestUiIcon>(rect.gameObject);
            icon.SetIcon(kind);
            icon.color = color;
            icon.raycastTarget = false;
            return icon;
        }

        private static RectTransform EnsureRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var childObject = new GameObject(name);
                childObject.transform.SetParent(parent, false);
                child = childObject.transform;
            }

            var rect = child.GetComponent<RectTransform>() ?? child.gameObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static QuestUiSurface EnsureSurface(RectTransform rect, Color color, float radius, bool raycastTarget)
        {
            var surface = GetOrAddComponent<QuestUiSurface>(rect.gameObject);
            surface.color = color;
            surface.SetCornerRadius(radius);
            surface.raycastTarget = raycastTarget;
            return surface;
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            return target.GetComponent<T>() ?? target.AddComponent<T>();
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private static void ConfigureHover(Button button, Text label, string value)
        {
            if (button == null)
            {
                return;
            }

            var feedback = GetOrAddComponent<QuestUiButtonFeedback>(button.gameObject);
            feedback.Configure(label, value);
        }

        private static ColorBlock CreateButtonColors(Color normal)
        {
            return new ColorBlock
            {
                normalColor = normal,
                highlightedColor = SurfaceHover,
                pressedColor = Accent,
                selectedColor = AccentStrong,
                disabledColor = new Color(normal.r, normal.g, normal.b, 0.34f),
                colorMultiplier = 1f,
                fadeDuration = 0.06f,
            };
        }

        private static void RefreshSwitchVisual(Toggle toggle, Color activeColor)
        {
            if (toggle == null)
            {
                return;
            }

            var surface = toggle.targetGraphic as QuestUiSurface;
            var knob = toggle.transform.Find("Knob") as RectTransform;
            if (surface != null)
            {
                surface.color = toggle.isOn ? new Color(activeColor.r * 0.24f, activeColor.g * 0.24f, activeColor.b * 0.24f, 1f) : SurfaceRaised;
            }

            if (knob != null)
            {
                knob.anchoredPosition = new Vector2(toggle.isOn ? 13f : -13f, 0f);
                var knobSurface = knob.GetComponent<QuestUiSurface>();
                if (knobSurface != null)
                {
                    knobSurface.color = toggle.isOn ? activeColor : TextSecondary;
                }
            }
        }

        private static string FormatPreset(int index)
        {
            return index switch
            {
                0 => "原声",
                1 => "KTV",
                2 => "强效",
                3 => "柔和",
                _ => "KTV",
            };
        }

        private static string FormatSource(string sourceType)
        {
            return sourceType switch
            {
                "bilibili" => "Bilibili",
                "youtube" => "YouTube",
                "direct" => "本地视频",
                _ => "视频",
            };
        }

        private static string FormatPlayback(PlaylistItem item, string playback)
        {
            if (item.status == PlaylistClient.StatusDownloading)
            {
                return "正在准备";
            }

            if (item.status == PlaylistClient.StatusError)
            {
                return "准备失败";
            }

            return string.Equals(playback, "playing", StringComparison.OrdinalIgnoreCase) ? "播放中" : "已暂停";
        }

        private static string FormatItemStatus(string status)
        {
            return status switch
            {
                PlaylistClient.StatusReady => "已就绪",
                PlaylistClient.StatusDownloading => "正在准备",
                PlaylistClient.StatusError => "准备失败",
                _ => "等待中",
            };
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string SingleLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "waiting";
            }

            var line = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return line.Length <= 54 ? line : $"{line[..51]}...";
        }
    }
}
