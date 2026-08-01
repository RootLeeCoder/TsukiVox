using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    [DisallowMultipleComponent]
    public sealed class QuestConsumerUiPrototype : MonoBehaviour
    {
        private const string RootName = "Consumer UI";
        private const string DefaultSongSearchInput = "BV1Kx4y1h7vR";
        private const string AppendKtvSearchPrefsKey = "TsukiVox.AppendKtvToSearch";
        private const int QueueRowCount = 5;
        private const int SearchResultRowCount = 4;
        private const int SuggestRowCount = 6;
        private const int VoiceLevelBarCount = 18;
        private const float RefreshIntervalSeconds = 0.1f;
        private const float ContentWidth = 992f;
        private const float MicClearanceStep = 0.0025f;

        private static readonly Color ScreenBackground = new Color(0.024f, 0.04f, 0.039f, 1f);
        private static readonly Color Surface = new Color(0.048f, 0.073f, 0.071f, 1f);
        private static readonly Color SurfaceRaised = new Color(0.068f, 0.098f, 0.094f, 1f);
        private static readonly Color SurfaceHover = new Color(0.52f, 1f, 0.88f, 1f);
        private static readonly Color Line = new Color(0.14f, 0.2f, 0.19f, 1f);
        private static readonly Color TextPrimary = new Color(0.94f, 0.97f, 0.96f, 1f);
        private static readonly Color TextSecondary = new Color(0.57f, 0.65f, 0.63f, 1f);
        private static readonly Color TextFaint = new Color(0.37f, 0.42f, 0.41f, 1f);
        private static readonly Color Accent = new Color(0.24f, 0.9f, 0.74f, 1f);
        private static readonly Color AccentStrong = new Color(0.56f, 1f, 0.89f, 1f);
        private static readonly Color AccentInk = new Color(0.012f, 0.075f, 0.059f, 1f);
        private static readonly Color Warm = new Color(0.95f, 0.71f, 0.42f, 1f);
        private static readonly Color WarmSurface = new Color(0.22f, 0.15f, 0.09f, 1f);
        private static readonly Color Danger = new Color(0.94f, 0.44f, 0.44f, 1f);
        private static readonly Color BrandBackground = new Color(0.031f, 0.035f, 0.043f, 1f);
        private static readonly Color BrandAccent = new Color(0.196f, 0.902f, 0.765f, 1f);
        private static readonly Color BrandWarm = new Color(1f, 0.741f, 0.447f, 1f);

        private enum UiPage
        {
            Home,
            SongSearch,
            Voice,
            Queue,
            Settings,
            Service,
            MicProtection,
        }

        private RectTransform panel;
        private RectTransform consumerRoot;
        private RectTransform homePage;
        private RectTransform songSearchPage;
        private RectTransform voicePage;
        private RectTransform queuePage;
        private RectTransform settingsPage;
        private RectTransform servicePage;
        private RectTransform micProtectionPage;
        private RectTransform debugScrim;
        private RectTransform debugDrawer;
        private RectTransform rawDetailsRoot;

        private CanvasGroup homeGroup;
        private CanvasGroup songSearchGroup;
        private CanvasGroup voiceGroup;
        private CanvasGroup queueGroup;
        private CanvasGroup settingsGroup;
        private CanvasGroup serviceGroup;
        private CanvasGroup micProtectionGroup;
        private CanvasGroup debugScrimGroup;
        private CanvasGroup debugDrawerGroup;

        private QuestAudioPrototype audioPrototype;
        private QuestPlaylistPrototype playlistPrototype;
        private QuestVideoScreenPrototype videoScreenPrototype;
        private QuestHandheldPropsPrototype handheldPropsPrototype;
        private QuestAppShellPrototype appShellPrototype;
        private QuestPlaylistPrototype subscribedPlaylist;
        private static TMP_FontAsset sharedUiFont;
        private TMP_FontAsset uiFont;

        private TMP_Text connectionText;
        private QuestUiSurface connectionDot;
        private RectTransform queueBadge;
        private TMP_Text queueBadgeText;
        private TMP_Text songMetaText;
        private TMP_Text songTitleText;
        private TMP_Text songDetailText;
        private TMP_Text transportHoverText;
        private TMP_Text voiceSummaryText;
        private TMP_Text voiceModeSummaryText;
        private QuestUiSurface voiceLiveDot;
        private QuestUiSurface[] waveBars;

        private Button queuePageButton;
        private Button songSearchPageButton;
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
        private readonly TMP_Text[] queueTitleTexts = new TMP_Text[QueueRowCount];
        private readonly TMP_Text[] queueMetaTexts = new TMP_Text[QueueRowCount];
        private readonly Button[] queueRemoveButtons = new Button[QueueRowCount];
        private readonly QuestUiIcon[] queueRemoveIcons = new QuestUiIcon[QueueRowCount];
        private TMP_Text queueFooterText;
        private Button clearQueueButton;
        private readonly string[] queueRowItemIds = new string[QueueRowCount];

        private Button songSearchBackButton;
        private TMP_InputField songSearchInput;
        private TMP_Text appendKtvSearchLabel;
        private Toggle appendKtvSearchToggle;
        private Button suggestSearchButton;
        private Button songSearchButton;
        private Button voiceSearchButton;
        private QuestUiIcon voiceSearchIcon;
        private TMP_Text voiceSearchStatusText;
        private TMP_Text voiceHeardText;
        private RectTransform voiceLevelRoot;
        private readonly QuestUiSurface[] voiceLevelBars = new QuestUiSurface[VoiceLevelBarCount];
        private VoiceSearchUiState lastVoiceState = VoiceSearchUiState.Idle;
        private RectTransform suggestContainer;
        private readonly RectTransform[] suggestRows = new RectTransform[SuggestRowCount];
        private readonly Button[] suggestButtons = new Button[SuggestRowCount];
        private readonly TMP_Text[] suggestTexts = new TMP_Text[SuggestRowCount];
        private readonly RectTransform[] searchResultRows = new RectTransform[SearchResultRowCount];
        private readonly QuestUiSurface[] searchResultSurfaces = new QuestUiSurface[SearchResultRowCount];
        private readonly TMP_Text[] searchResultTitleTexts = new TMP_Text[SearchResultRowCount];
        private readonly TMP_Text[] searchResultMetaTexts = new TMP_Text[SearchResultRowCount];
        private readonly Button[] searchResultAddButtons = new Button[SearchResultRowCount];
        private Button searchPreviousPageButton;
        private Button searchNextPageButton;
        private Button clearSearchButton;
        private TMP_Text songSearchStatusText;
        private int songSearchPageNumber = 1;
        private bool appendKtvToSearch;

        private Button settingsBackButton;
        private Button openServiceSettingsButton;
        private Button openMicProtectionButton;
        private QuestUiSurface openMicProtectionSurface;
        private QuestUiIcon openMicProtectionIcon;
        private TMP_Text settingsConnectionText;
        private TMP_InputField helperHostInput;
        private Button applyHostButton;
        private Button defaultHostButton;
        private Button serviceBackButton;
        private Button companionModeButton;
        private Button onlineModeButton;
        private TMP_Text serviceConnectionText;
        private Toggle voiceSearchEnabledToggle;
        private TMP_Text voiceSearchHintText;
        private TMP_Text voiceProviderLabelText;
        private Button tencentProviderButton;
        private Button mimoProviderButton;
        private Toggle monitorOutputToggle;
        private Toggle safetyToggle;
        private Toggle nativeToggle;
        private Button openDiagnosticsButton;
        private TMP_Text settingsBuildText;

        private Button micProtectionBackButton;
        private Toggle micProtectionToggle;
        private Slider micWarningDistanceSlider;
        private Slider micCriticalDistanceSlider;
        private Slider micHapticStrengthSlider;
        private TMP_Text micLiveDistanceText;
        private TMP_Text micLiveStateText;
        private TMP_Text micWarningDistanceValueText;
        private TMP_Text micCriticalDistanceValueText;
        private TMP_Text micHapticStrengthValueText;
        private TMP_Text micCalibrationStatusText;
        private Button captureWarningDistanceButton;
        private Button captureCriticalDistanceButton;
        private Button resetMicProtectionButton;

        private Button closeDiagnosticsButton;
        private Button debugScrimButton;
        private Button rawDetailsButton;
        private TMP_Text rawDetailsButtonText;
        private TMP_Text diagnosticsHealthText;
        private TMP_Text diagnosticsServiceText;
        private TMP_Text diagnosticsVideoText;
        private TMP_Text rawDiagnosticsText;
        private Button copyDiagnosticsButton;

        private UiPage currentPage;
        private bool isConfigured;
        private bool rawDetailsVisible;
        private float nextRefreshAt;
        private Coroutine pageTransition;
        private Coroutine drawerTransition;
        private Coroutine micCalibrationCoroutine;

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
            handheldPropsPrototype = FindAnyObjectByType<QuestHandheldPropsPrototype>();
            appShellPrototype = appShell;
            uiFont = ResolveUiFont();
            appendKtvToSearch = PlayerPrefs.GetInt(AppendKtvSearchPrefsKey, 0) != 0;
            videoScreenPrototype?.SetStatusOverlayVisible(false);

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
            CancelMicFaceCalibration();
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
            songSearchPage = EnsurePage(consumerRoot, "Song Search Page", out songSearchGroup);
            voicePage = EnsurePage(consumerRoot, "Voice Page", out voiceGroup);
            queuePage = EnsurePage(consumerRoot, "Queue Page", out queueGroup);
            settingsPage = EnsurePage(consumerRoot, "Settings Page", out settingsGroup);
            servicePage = EnsurePage(consumerRoot, "Service Page", out serviceGroup);
            micProtectionPage = EnsurePage(consumerRoot, "Mic Protection Page", out micProtectionGroup);

            BuildHomePage();
            BuildSongSearchPage();
            BuildVoicePage();
            BuildQueuePage();
            BuildSettingsPage();
            BuildServicePage();
            BuildMicProtectionPage();
            BuildDebugDrawer();
            HideLegacyUi();
            consumerRoot.SetAsLastSibling();
        }

        private void BuildHomePage()
        {
            CreateDivider(homePage, "Header Divider", new Vector2(0f, 190f), new Vector2(ContentWidth, 1f));
            CreateDivider(homePage, "Transport Top Divider", new Vector2(0f, 42f), new Vector2(ContentWidth, 1f));
            CreateDivider(homePage, "Footer Divider", new Vector2(0f, -180f), new Vector2(ContentWidth, 1f));

            var brandMark = EnsureRect(homePage, "Brand Mark", new Vector2(-476f, 232f), new Vector2(42f, 42f));
            var brandSurface = EnsureSurface(brandMark, BrandBackground, 10f, false);
            var brandRing = EnsureIcon(brandMark, "Icon", QuestUiIconKind.BrandRing, Vector2.zero, new Vector2(34f, 34f), BrandAccent);
            brandRing.StrokeWidth = 2f;
            var moonDot = EnsureRect(brandMark, "Moon Dot", new Vector2(4f, 4f), new Vector2(10f, 10f));
            EnsureSurface(moonDot, TextPrimary, 5f, false);
            var brandSmile = EnsureIcon(brandMark, "Smile", QuestUiIconKind.BrandSmile, Vector2.zero, new Vector2(34f, 34f), BrandWarm);
            brandSmile.StrokeWidth = 2.8f;
            brandSurface.raycastTarget = false;
            CreateText(homePage, "Brand", "TsukiVox", 23, FontStyle.Bold, new Vector2(-356f, 240f), new Vector2(180f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(homePage, "Brand CN", "月读声域", 16, FontStyle.Normal, new Vector2(-356f, 216f), new Vector2(180f, 24f), TextAnchor.MiddleLeft, TextSecondary);

            connectionDot = EnsureSurface(EnsureRect(homePage, "Connection Dot", new Vector2(84f, 232f), new Vector2(12f, 12f)), Accent, 6f, false);
            connectionText = CreateText(homePage, "Connection", "点歌服务已连接", 17, FontStyle.Normal, new Vector2(196f, 232f), new Vector2(190f, 36f), TextAnchor.MiddleLeft, TextPrimary);
            SetChildActive(homePage, "Header Hover Label", false);

            songSearchPageButton = CreateIconButton(homePage, "Open Song Search", QuestUiIconKind.Search, new Vector2(336f, 232f), new Vector2(64f, 64f), Accent, AccentInk, out _);
            queuePageButton = CreateIconButton(homePage, "Open Queue", QuestUiIconKind.Queue, new Vector2(408f, 232f), new Vector2(64f, 64f), Surface, TextPrimary, out _);
            settingsPageButton = CreateIconButton(homePage, "Open Settings", QuestUiIconKind.Settings, new Vector2(480f, 232f), new Vector2(64f, 64f), Surface, TextPrimary, out _);
            ConfigureHover(songSearchPageButton, null, string.Empty);
            ConfigureHover(queuePageButton, null, string.Empty);
            ConfigureHover(settingsPageButton, null, string.Empty);

            queueBadge = EnsureRect(queuePageButton.transform, "Badge", new Vector2(25f, 25f), new Vector2(24f, 24f));
            EnsureSurface(queueBadge, Accent, 12f, false);
            queueBadgeText = CreateText(queueBadge, "Label", "0", 14, FontStyle.Bold, Vector2.zero, queueBadge.sizeDelta, TextAnchor.MiddleCenter, AccentInk);

            songMetaText = CreateText(homePage, "Song Meta", "播放队列为空", 18, FontStyle.Bold, new Vector2(-270f, 150f), new Vector2(450f, 32f), TextAnchor.MiddleLeft, Accent);
            songTitleText = CreateText(homePage, "Song Title", "等待点歌", 44, FontStyle.Bold, new Vector2(-190f, 103f), new Vector2(610f, 66f), TextAnchor.MiddleLeft, TextPrimary);
            songTitleText.enableAutoSizing = true;
            songTitleText.fontSizeMin = 28f;
            songTitleText.fontSizeMax = 44f;
            songDetailText = CreateText(homePage, "Song Detail", "从 PC 添加歌曲后即可开始", 17, FontStyle.Normal, new Vector2(-250f, 62f), new Vector2(490f, 30f), TextAnchor.MiddleLeft, TextSecondary);

            waveBars = new QuestUiSurface[8];
            var waveHeights = new[] { 20f, 38f, 58f, 30f, 50f, 24f, 40f, 16f };
            for (var index = 0; index < waveBars.Length; index += 1)
            {
                var barRect = EnsureRect(homePage, $"Wave Bar {index}", new Vector2(326f + index * 20f, 108f), new Vector2(6f, waveHeights[index]));
                waveBars[index] = EnsureSurface(barRect, Accent, 3f, false);
            }

            transportHoverText = CreateText(homePage, "Transport Hover Label", string.Empty, 16, FontStyle.Bold, new Vector2(0f, -132f), new Vector2(320f, 30f), TextAnchor.MiddleCenter, AccentStrong);
            replayButton = CreateIconButton(homePage, "Replay", QuestUiIconKind.Replay, new Vector2(-220f, -45f), new Vector2(90f, 90f), Surface, TextPrimary, out _);
            previousButton = CreateIconButton(homePage, "Previous", QuestUiIconKind.Previous, new Vector2(-110f, -45f), new Vector2(90f, 90f), Surface, TextPrimary, out _);
            playPauseButton = CreateIconButton(homePage, "Play Pause", QuestUiIconKind.Play, new Vector2(0f, -45f), new Vector2(104f, 104f), Accent, AccentInk, out playPauseIcon);
            nextButton = CreateIconButton(homePage, "Next", QuestUiIconKind.Next, new Vector2(110f, -45f), new Vector2(90f, 90f), Surface, TextPrimary, out _);
            microphoneButton = CreateIconButton(homePage, "Microphone", QuestUiIconKind.Microphone, new Vector2(220f, -45f), new Vector2(90f, 90f), WarmSurface, Warm, out microphoneIcon);
            microphoneSurface = microphoneButton.targetGraphic as QuestUiSurface;
            ConfigureHover(replayButton, transportHoverText, "重播");
            ConfigureHover(previousButton, transportHoverText, "上一首");
            ConfigureHover(playPauseButton, transportHoverText, "播放");
            ConfigureHover(nextButton, transportHoverText, "下一首");
            ConfigureHover(microphoneButton, transportHoverText, "麦克风");

            voicePageButton = CreateSurfaceButton(homePage, "Voice Summary", new Vector2(0f, -225f), new Vector2(ContentWidth, 72f), Color.clear, Color.clear);
            voiceLiveDot = EnsureSurface(EnsureRect(voicePageButton.transform, "Live Dot", new Vector2(-472f, 0f), new Vector2(12f, 12f)), Warm, 6f, false);
            voiceSummaryText = CreateText(voicePageButton.transform, "Status", "麦克风已开启", 18, FontStyle.Bold, new Vector2(-340f, 0f), new Vector2(220f, 40f), TextAnchor.MiddleLeft, TextPrimary);
            voiceModeSummaryText = CreateText(voicePageButton.transform, "Mode", "人声 · KTV", 17, FontStyle.Normal, new Vector2(-110f, 0f), new Vector2(190f, 40f), TextAnchor.MiddleLeft, TextSecondary);
            CreateText(voicePageButton.transform, "Command", "调整人声", 17, FontStyle.Normal, new Vector2(360f, 0f), new Vector2(150f, 40f), TextAnchor.MiddleRight, TextSecondary);
            EnsureIcon(voicePageButton.transform, "Chevron", QuestUiIconKind.ChevronRight, new Vector2(476f, 0f), new Vector2(22f, 22f), TextSecondary);
        }

        private void BuildSongSearchPage()
        {
            BuildSubpageHeader(songSearchPage, "搜索点歌", out songSearchBackButton);

            // 语音找歌是与文字搜索并列的入口，放在输入框上方一行。
            voiceSearchButton = CreateIconButton(
                songSearchPage,
                "Voice Search",
                QuestUiIconKind.Microphone,
                new Vector2(-446f, 178f),
                new Vector2(86f, 54f),
                Accent,
                AccentInk,
                out voiceSearchIcon);
            voiceSearchStatusText = CreateText(
                songSearchPage,
                "Voice Status",
                "按一下说出歌名",
                17,
                FontStyle.Bold,
                new Vector2(60f, 190f),
                new Vector2(820f, 26f),
                TextAnchor.MiddleLeft,
                TextPrimary);
            voiceHeardText = CreateText(
                songSearchPage,
                "Voice Heard",
                string.Empty,
                15,
                FontStyle.Normal,
                new Vector2(60f, 166f),
                new Vector2(820f, 24f),
                TextAnchor.MiddleLeft,
                TextSecondary);

            // 录音电平条：明确的"正在录音"视觉状态，不允许静默采集。
            voiceLevelRoot = EnsureRect(
                songSearchPage,
                "Voice Level",
                new Vector2(60f, 146f),
                new Vector2(VoiceLevelBarCount * 14f, 14f));
            for (var index = 0; index < VoiceLevelBarCount; index += 1)
            {
                var barRect = EnsureRect(
                    voiceLevelRoot,
                    $"Bar {index}",
                    new Vector2(-voiceLevelRoot.sizeDelta.x * 0.5f + 7f + index * 14f, 0f),
                    new Vector2(6f, 14f));
                voiceLevelBars[index] = EnsureSurface(barRect, Accent, 3f, false);
            }

            songSearchInput = CreateInputField(
                songSearchPage,
                "Song Search Input",
                "输入歌名、歌手或 BV 号",
                new Vector2(-166f, 112f),
                new Vector2(640f, 54f));
            songSearchInput.characterLimit = 80;
            songSearchInput.SetTextWithoutNotify(DefaultSongSearchInput);
            appendKtvSearchLabel = CreateText(
                songSearchPage,
                "Append KTV Label",
                "KTV",
                16,
                FontStyle.Bold,
                new Vector2(190f, 112f),
                new Vector2(56f, 36f),
                TextAnchor.MiddleCenter,
                TextSecondary);
            appendKtvSearchToggle = CreateSwitch(songSearchPage, "Append KTV Switch", new Vector2(260f, 112f));
            suggestSearchButton = CreateIconButton(
                songSearchPage,
                "Search Suggestions",
                QuestUiIconKind.Sparkles,
                new Vector2(354f, 112f),
                new Vector2(70f, 54f),
                SurfaceRaised,
                AccentStrong,
                out _);
            songSearchButton = CreateIconButton(
                songSearchPage,
                "Search Songs",
                QuestUiIconKind.Search,
                new Vector2(446f, 112f),
                new Vector2(86f, 54f),
                Accent,
                AccentInk,
                out _);

            // 建议列表容器：在输入框下方，搜索结果上方
            suggestContainer = EnsureRect(
                songSearchPage,
                "Suggest Container",
                new Vector2(0f, -32f),
                new Vector2(ContentWidth, 36f * SuggestRowCount));
            suggestContainer.gameObject.SetActive(false);

            for (var index = 0; index < SuggestRowCount; index += 1)
            {
                var row = EnsureRect(
                    suggestContainer,
                    $"Suggest Row {index}",
                    new Vector2(0f, 90f - index * 36f),
                    new Vector2(ContentWidth, 34f));
                suggestRows[index] = row;
                suggestButtons[index] = CreateSurfaceButton(
                    row,
                    "Button",
                    Vector2.zero,
                    new Vector2(ContentWidth, 34f),
                    Surface,
                    Line);
                suggestTexts[index] = CreateText(
                    suggestButtons[index].transform,
                    "Text",
                    string.Empty,
                    16,
                    FontStyle.Normal,
                    new Vector2(-20f, 0f),
                    new Vector2(940f, 28f),
                    TextAnchor.MiddleLeft,
                    TextPrimary);
                ConfigureHover(suggestButtons[index], null, string.Empty);
            }

            const float firstY = 48f;
            for (var index = 0; index < SearchResultRowCount; index += 1)
            {
                var row = EnsureRect(
                    songSearchPage,
                    $"Search Result {index}",
                    new Vector2(0f, firstY - index * 66f),
                    new Vector2(ContentWidth, 60f));
                searchResultRows[index] = row;
                searchResultSurfaces[index] = EnsureSurface(row, Surface, 6f, false);
                searchResultTitleTexts[index] = CreateText(
                    row,
                    "Title",
                    "歌曲",
                    18,
                    FontStyle.Bold,
                    new Vector2(-62f, 11f),
                    new Vector2(810f, 28f),
                    TextAnchor.MiddleLeft,
                    TextPrimary);
                searchResultTitleTexts[index].enableAutoSizing = true;
                searchResultTitleTexts[index].fontSizeMin = 15f;
                searchResultTitleTexts[index].fontSizeMax = 18f;
                searchResultMetaTexts[index] = CreateText(
                    row,
                    "Meta",
                    "Bilibili",
                    15,
                    FontStyle.Normal,
                    new Vector2(-62f, -15f),
                    new Vector2(810f, 24f),
                    TextAnchor.MiddleLeft,
                    TextSecondary);
                searchResultAddButtons[index] = CreateIconButton(
                    row,
                    "Add",
                    QuestUiIconKind.Plus,
                    new Vector2(460f, 0f),
                    new Vector2(52f, 52f),
                    SurfaceRaised,
                    AccentStrong,
                    out _);
            }

            searchPreviousPageButton = CreateTextButton(songSearchPage, "Previous Search Page", "上一页", new Vector2(-424f, -226f), new Vector2(128f, 46f), Surface, TextPrimary);
            songSearchStatusText = CreateText(songSearchPage, "Search Status", "输入关键词或按麦克风说出歌名", 16, FontStyle.Normal, new Vector2(0f, -226f), new Vector2(520f, 34f), TextAnchor.MiddleCenter, TextSecondary);
            searchNextPageButton = CreateTextButton(songSearchPage, "Next Search Page", "下一页", new Vector2(424f, -226f), new Vector2(128f, 46f), Surface, TextPrimary);
            clearSearchButton = CreateTextButton(songSearchPage, "Clear Search", "清空", new Vector2(-286f, -226f), new Vector2(104f, 46f), Surface, TextSecondary);
        }

        private void BuildVoicePage()
        {
            BuildSubpageHeader(voicePage, "人声", out voiceBackButton);

            CreateText(voicePage, "Microphone Title", "麦克风", 21, FontStyle.Bold, new Vector2(-396f, 135f), new Vector2(200f, 36f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(voicePage, "Microphone Hint", "开启后可听到实时返听", 16, FontStyle.Normal, new Vector2(-326f, 105f), new Vector2(340f, 26f), TextAnchor.MiddleLeft, TextSecondary);
            inputMeterSlider = CreateSlider(voicePage, "Input Meter", new Vector2(120f, 124f), new Vector2(510f, 46f), false);
            voiceMicrophoneToggle = CreateSwitch(voicePage, "Microphone Switch", new Vector2(460f, 124f));

            CreateDivider(voicePage, "Microphone Divider", new Vector2(0f, 70f), new Vector2(ContentWidth, 1f));
            CreateText(voicePage, "Volume Title", "返听音量", 21, FontStyle.Bold, new Vector2(-396f, 28f), new Vector2(200f, 36f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(voicePage, "Volume Hint", "建议先低后高", 16, FontStyle.Normal, new Vector2(-356f, -2f), new Vector2(280f, 26f), TextAnchor.MiddleLeft, TextSecondary);
            monitorVolumeSlider = CreateSlider(voicePage, "Monitor Volume", new Vector2(115f, 18f), new Vector2(510f, 50f), true);
            EnsureIcon(voicePage, "Volume Icon", QuestUiIconKind.Volume, new Vector2(460f, 18f), new Vector2(30f, 30f), TextSecondary);

            CreateDivider(voicePage, "Volume Divider", new Vector2(0f, -42f), new Vector2(ContentWidth, 1f));
            CreateText(voicePage, "Preset Title", "人声效果", 21, FontStyle.Bold, new Vector2(-396f, -78f), new Vector2(200f, 36f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(voicePage, "Preset Hint", "四档预设直接选择", 16, FontStyle.Normal, new Vector2(-326f, -105f), new Vector2(340f, 26f), TextAnchor.MiddleLeft, TextSecondary);

            var labels = new[] { "原声", "KTV", "强效", "柔和" };
            for (var index = 0; index < presetButtons.Length; index += 1)
            {
                presetButtons[index] = CreateTextButton(voicePage, $"Preset {index}", labels[index], new Vector2(-339f + index * 226f, -154f), new Vector2(210f, 60f), Surface, TextPrimary);
                presetButtons[index].transition = Selectable.Transition.None;
                presetSurfaces[index] = presetButtons[index].targetGraphic as QuestUiSurface;
            }

            var safetyNote = EnsureRect(voicePage, "Safety Note", new Vector2(0f, -225f), new Vector2(ContentWidth, 48f));
            EnsureSurface(safetyNote, new Color(0.09f, 0.11f, 0.1f, 1f), 6f, false);
            CreateText(safetyNote, "Label", "安全保护会在返听过响时自动降低音量", 16, FontStyle.Normal, Vector2.zero, new Vector2(930f, 32f), TextAnchor.MiddleCenter, TextSecondary);
        }

        private void BuildQueuePage()
        {
            BuildSubpageHeader(queuePage, "播放队列", out queueBackButton);
            var firstY = 145f;
            for (var index = 0; index < QueueRowCount; index += 1)
            {
                var row = EnsureRect(queuePage, $"Queue Row {index}", new Vector2(0f, firstY - index * 72f), new Vector2(ContentWidth, 64f));
                queueRows[index] = row;
                queueRowSurfaces[index] = EnsureSurface(row, Surface, 6f, false);
                queueIndicators[index] = EnsureSurface(EnsureRect(row, "Indicator", new Vector2(-480f, 0f), new Vector2(5f, 42f)), Accent, 2f, false);
                // 标题和信息行给右侧删除按钮留出空间。
                queueTitleTexts[index] = CreateText(row, "Title", "歌曲", 18, FontStyle.Bold, new Vector2(5f, 12f), new Vector2(780f, 30f), TextAnchor.MiddleLeft, TextPrimary);
                queueMetaTexts[index] = CreateText(row, "Meta", "等待", 16, FontStyle.Normal, new Vector2(5f, -15f), new Vector2(780f, 24f), TextAnchor.MiddleLeft, TextSecondary);
                CreateText(row, "Number", (index + 1).ToString("00"), 16, FontStyle.Bold, new Vector2(-442f, 0f), new Vector2(58f, 32f), TextAnchor.MiddleCenter, TextSecondary);
                queueRemoveButtons[index] = CreateIconButton(
                    row,
                    "Remove",
                    QuestUiIconKind.Close,
                    new Vector2(462f, 0f),
                    new Vector2(44f, 44f),
                    SurfaceRaised,
                    TextSecondary,
                    out queueRemoveIcons[index]);
                ConfigureHover(queueRemoveButtons[index], null, string.Empty);
            }

            queueFooterText = CreateText(queuePage, "Queue Footer", "队列为空", 16, FontStyle.Normal, new Vector2(-70f, -225f), new Vector2(560f, 32f), TextAnchor.MiddleCenter, TextSecondary);
            clearQueueButton = CreateTextButton(queuePage, "Clear Queue", "清空待播", new Vector2(400f, -225f), new Vector2(176f, 48f), Surface, TextPrimary);
            ConfigureHover(clearQueueButton, null, string.Empty);
        }

        private void BuildSettingsPage()
        {
            BuildSubpageHeader(settingsPage, "设置", out settingsBackButton);
            SetChildActive(settingsPage, "Service Section", false);
            SetChildActive(settingsPage, "Service Status", false);
            SetChildActive(settingsPage, "Host Label", false);
            SetChildActive(settingsPage, "Helper Host", false);
            SetChildActive(settingsPage, "Apply Host", false);
            SetChildActive(settingsPage, "Default Host", false);
            openMicProtectionButton = CreateSurfaceButton(settingsPage, "Open Mic Protection", new Vector2(396f, 232f), new Vector2(200f, 52f), Surface, Line);
            openMicProtectionSurface = openMicProtectionButton.targetGraphic as QuestUiSurface;
            openMicProtectionIcon = EnsureIcon(openMicProtectionButton.transform, "Icon", QuestUiIconKind.Microphone, new Vector2(-70f, 0f), new Vector2(24f, 24f), Accent);
            CreateText(openMicProtectionButton.transform, "Label", "防碰撞", 17, FontStyle.Bold, new Vector2(22f, 0f), new Vector2(118f, 34f), TextAnchor.MiddleCenter, TextPrimary);

            openServiceSettingsButton = CreateSurfaceButton(settingsPage, "Open Service Settings", new Vector2(0f, 132f), new Vector2(ContentWidth, 82f), Surface, Line);
            CreateText(openServiceSettingsButton.transform, "Title", "点歌服务", 19, FontStyle.Bold, new Vector2(-350f, 13f), new Vector2(260f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            settingsConnectionText = CreateText(openServiceSettingsButton.transform, "Status", "正在连接", 16, FontStyle.Bold, new Vector2(250f, 13f), new Vector2(350f, 30f), TextAnchor.MiddleRight, Accent);
            CreateText(openServiceSettingsButton.transform, "Hint", "局域网 Companion", 15, FontStyle.Normal, new Vector2(-270f, -18f), new Vector2(420f, 26f), TextAnchor.MiddleLeft, TextSecondary);
            EnsureIcon(openServiceSettingsButton.transform, "Chevron", QuestUiIconKind.ChevronRight, new Vector2(458f, 0f), new Vector2(24f, 24f), TextSecondary);

            CreateDivider(settingsPage, "Service Divider", new Vector2(0f, 82f), new Vector2(ContentWidth, 1f));
            CreateText(settingsPage, "Audio Section", "音频高级设置", 16, FontStyle.Bold, new Vector2(-356f, 55f), new Vector2(280f, 30f), TextAnchor.MiddleLeft, TextSecondary);
            CreateSettingToggle(settingsPage, "Monitor Output", "返听输出", "关闭后仍保留麦克风输入", 12f, out monitorOutputToggle);
            CreateSettingToggle(settingsPage, "Safety Limiter", "安全保护", "建议始终保持开启", -56f, out safetyToggle);
            CreateSettingToggle(settingsPage, "Native Backend", "Native 低延迟", "原声路径，不包含 KTV 效果", -124f, out nativeToggle);

            CreateDivider(settingsPage, "Audio Divider", new Vector2(0f, -184f), new Vector2(ContentWidth, 1f));
            openDiagnosticsButton = CreateSurfaceButton(settingsPage, "Open Diagnostics", new Vector2(0f, -228f), new Vector2(ContentWidth, 72f), Surface, Line);
            CreateText(openDiagnosticsButton.transform, "Title", "诊断与支持", 19, FontStyle.Bold, new Vector2(-340f, 11f), new Vector2(300f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            settingsBuildText = CreateText(openDiagnosticsButton.transform, "Hint", QuestBuildInfo.SettingsSummary, 15, FontStyle.Normal, new Vector2(-20f, -17f), new Vector2(840f, 26f), TextAnchor.MiddleLeft, TextSecondary);
            EnsureIcon(openDiagnosticsButton.transform, "Chevron", QuestUiIconKind.ChevronRight, new Vector2(458f, 0f), new Vector2(24f, 24f), TextSecondary);
        }

        private void BuildServicePage()
        {
            BuildSubpageHeader(servicePage, "点歌服务", out serviceBackButton);
            companionModeButton = CreateTextButton(servicePage, "Companion Mode", "局域网 Companion", new Vector2(-250f, 132f), new Vector2(480f, 60f), Surface, TextPrimary);
            onlineModeButton = CreateTextButton(servicePage, "Online Mode", "在线服务", new Vector2(250f, 132f), new Vector2(480f, 60f), Surface, TextPrimary);

            CreateText(servicePage, "Address Label", "服务地址", 17, FontStyle.Bold, new Vector2(-402f, 75f), new Vector2(180f, 32f), TextAnchor.MiddleLeft, TextSecondary);
            serviceConnectionText = CreateText(servicePage, "Connection Status", "正在连接", 17, FontStyle.Bold, new Vector2(330f, 75f), new Vector2(320f, 32f), TextAnchor.MiddleRight, Accent);
            helperHostInput = CreateInputField(servicePage, "Service Address", QuestPlaylistPrototype.DefaultHelperHostAddress, new Vector2(-170f, 18f), new Vector2(620f, 56f));
            applyHostButton = CreateTextButton(servicePage, "Apply Service Address", "应用", new Vector2(230f, 18f), new Vector2(150f, 56f), Accent, AccentInk);
            defaultHostButton = CreateTextButton(servicePage, "Default Service Address", "恢复默认地址", new Vector2(406f, 18f), new Vector2(180f, 56f), Surface, TextPrimary);

            SetChildActive(servicePage, "Mode Hint", false);
            SetChildActive(servicePage, "Privacy Note", false);
            SetChildActive(servicePage, "Voice Divider", false);
            CreateDivider(servicePage, "Address Divider", new Vector2(0f, -32f), new Vector2(ContentWidth, 1f));

            CreateText(servicePage, "Voice Title", "语音找歌", 19, FontStyle.Bold, new Vector2(-356f, -74f), new Vector2(280f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            voiceSearchEnabledToggle = CreateSwitch(servicePage, "Voice Search Switch", new Vector2(460f, -74f));

            // 识别供应商二选一，与上方服务模式相同的互斥按钮样式。
            voiceProviderLabelText = CreateText(servicePage, "Provider Label", "识别供应商", 16, FontStyle.Normal, new Vector2(-380f, -132f), new Vector2(230f, 28f), TextAnchor.MiddleLeft, TextSecondary);
            tencentProviderButton = CreateTextButton(servicePage, "Tencent Provider", "腾讯云", new Vector2(-60f, -132f), new Vector2(210f, 50f), Surface, TextPrimary);
            mimoProviderButton = CreateTextButton(servicePage, "MiMo Provider", "小米 MiMo", new Vector2(170f, -132f), new Vector2(210f, 50f), Surface, TextPrimary);
            voiceSearchHintText = CreateText(
                servicePage,
                "Voice Hint",
                "关闭后不再采集或上传语音",
                15,
                FontStyle.Normal,
                new Vector2(0f, -180f),
                new Vector2(ContentWidth, 26f),
                TextAnchor.MiddleLeft,
                TextSecondary);
        }

        private void BuildMicProtectionPage()
        {
            BuildSubpageHeader(micProtectionPage, "麦克风防碰撞", out micProtectionBackButton);

            CreateText(micProtectionPage, "Enable Title", "防贴脸震动", 21, FontStyle.Bold, new Vector2(-366f, 150f), new Vector2(260f, 36f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(micProtectionPage, "Enable Hint", "靠近嘴部时提供渐强触觉反馈", 16, FontStyle.Normal, new Vector2(-270f, 120f), new Vector2(450f, 26f), TextAnchor.MiddleLeft, TextSecondary);
            micProtectionToggle = CreateSwitch(micProtectionPage, "Protection Switch", new Vector2(460f, 138f));

            CreateText(micProtectionPage, "Live Label", "当前间隙", 16, FontStyle.Bold, new Vector2(-414f, 76f), new Vector2(150f, 30f), TextAnchor.MiddleLeft, TextSecondary);
            micLiveDistanceText = CreateText(micProtectionPage, "Live Distance", "-- cm", 27, FontStyle.Bold, new Vector2(-244f, 76f), new Vector2(180f, 42f), TextAnchor.MiddleLeft, TextPrimary);
            micLiveStateText = CreateText(micProtectionPage, "Live State", "等待右手麦克风", 17, FontStyle.Bold, new Vector2(292f, 76f), new Vector2(380f, 34f), TextAnchor.MiddleRight, TextSecondary);
            CreateDivider(micProtectionPage, "Live Divider", new Vector2(0f, 52f), new Vector2(ContentWidth, 1f));

            CreateText(micProtectionPage, "Warning Title", "轻震起点", 18, FontStyle.Bold, new Vector2(-402f, 22f), new Vector2(170f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(micProtectionPage, "Warning Hint", "网头表面间隙", 14, FontStyle.Normal, new Vector2(-365f, -4f), new Vector2(240f, 24f), TextAnchor.MiddleLeft, TextSecondary);
            micWarningDistanceSlider = CreateSlider(micProtectionPage, "Warning Distance", new Vector2(70f, 14f), new Vector2(430f, 48f), true);
            ConfigureWholeNumberSlider(micWarningDistanceSlider, 2f, 32f);
            micWarningDistanceValueText = CreateText(micProtectionPage, "Warning Value", "6.3 cm", 18, FontStyle.Bold, new Vector2(334f, 14f), new Vector2(92f, 34f), TextAnchor.MiddleRight, AccentStrong);
            captureWarningDistanceButton = CreateTextButton(micProtectionPage, "Capture Warning", "捕获", new Vector2(454f, 14f), new Vector2(104f, 46f), Surface, TextPrimary);
            CreateDivider(micProtectionPage, "Warning Divider", new Vector2(0f, -28f), new Vector2(ContentWidth, 1f));

            CreateText(micProtectionPage, "Critical Title", "强震起点", 18, FontStyle.Bold, new Vector2(-402f, -56f), new Vector2(170f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(micProtectionPage, "Critical Hint", "必须小于轻震起点", 14, FontStyle.Normal, new Vector2(-345f, -82f), new Vector2(280f, 24f), TextAnchor.MiddleLeft, TextSecondary);
            micCriticalDistanceSlider = CreateSlider(micProtectionPage, "Critical Distance", new Vector2(70f, -64f), new Vector2(430f, 48f), true);
            ConfigureWholeNumberSlider(micCriticalDistanceSlider, 0f, 16f);
            micCriticalDistanceValueText = CreateText(micProtectionPage, "Critical Value", "1.8 cm", 18, FontStyle.Bold, new Vector2(334f, -64f), new Vector2(92f, 34f), TextAnchor.MiddleRight, Warm);
            captureCriticalDistanceButton = CreateTextButton(micProtectionPage, "Capture Critical", "捕获", new Vector2(454f, -64f), new Vector2(104f, 46f), Surface, TextPrimary);
            CreateDivider(micProtectionPage, "Critical Divider", new Vector2(0f, -106f), new Vector2(ContentWidth, 1f));

            CreateText(micProtectionPage, "Strength Title", "震动强度", 18, FontStyle.Bold, new Vector2(-402f, -134f), new Vector2(170f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(micProtectionPage, "Strength Hint", "同时缩放轻震与强震", 14, FontStyle.Normal, new Vector2(-335f, -160f), new Vector2(300f, 24f), TextAnchor.MiddleLeft, TextSecondary);
            micHapticStrengthSlider = CreateSlider(micProtectionPage, "Haptic Strength", new Vector2(70f, -142f), new Vector2(430f, 48f), true);
            ConfigureWholeNumberSlider(micHapticStrengthSlider, 2f, 10f);
            micHapticStrengthValueText = CreateText(micProtectionPage, "Strength Value", "100%", 18, FontStyle.Bold, new Vector2(356f, -142f), new Vector2(116f, 34f), TextAnchor.MiddleRight, AccentStrong);
            CreateDivider(micProtectionPage, "Strength Divider", new Vector2(0f, -184f), new Vector2(ContentWidth, 1f));

            micCalibrationStatusText = CreateText(micProtectionPage, "Calibration Status", string.Empty, 16, FontStyle.Bold, new Vector2(-184f, -226f), new Vector2(610f, 40f), TextAnchor.MiddleLeft, AccentStrong);
            resetMicProtectionButton = CreateTextButton(micProtectionPage, "Reset Protection", "恢复默认", new Vector2(404f, -226f), new Vector2(188f, 52f), Surface, TextPrimary);
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
            CreateText(debugDrawer, "Title", "诊断与支持", 26, FontStyle.Bold, new Vector2(-130f, 248f), new Vector2(310f, 46f), TextAnchor.MiddleLeft, TextPrimary);
            closeDiagnosticsButton = CreateIconButton(debugDrawer, "Close", QuestUiIconKind.Close, new Vector2(250f, 250f), new Vector2(52f, 52f), Surface, TextPrimary, out _);

            diagnosticsHealthText = CreateText(debugDrawer, "Health", "应用正常 · 音频正常 · 视频正常", 18, FontStyle.Bold, new Vector2(0f, 174f), new Vector2(548f, 52f), TextAnchor.MiddleLeft, TextPrimary);
            CreateDivider(debugDrawer, "Health Divider", new Vector2(0f, 138f), new Vector2(548f, 1f));
            diagnosticsServiceText = CreateText(debugDrawer, "Service", "点歌服务：未连接", 16, FontStyle.Normal, new Vector2(0f, 104f), new Vector2(548f, 44f), TextAnchor.MiddleLeft, TextSecondary);
            CreateDivider(debugDrawer, "Service Divider", new Vector2(0f, 70f), new Vector2(548f, 1f));

            SetChildActive(debugDrawer, "Video Debug Label", false);
            SetChildActive(debugDrawer, "Video Debug Switch", false);
            diagnosticsVideoText = CreateText(debugDrawer, "Video Status", "视频状态：待机", 16, FontStyle.Normal, new Vector2(0f, 34f), new Vector2(548f, 62f), TextAnchor.MiddleLeft, TextSecondary);
            CreateDivider(debugDrawer, "Video Divider", new Vector2(0f, -3f), new Vector2(548f, 1f));

            rawDetailsButton = CreateSurfaceButton(debugDrawer, "Raw Details Toggle", new Vector2(0f, -47f), new Vector2(548f, 56f), Color.clear, Color.clear);
            rawDetailsButtonText = CreateText(rawDetailsButton.transform, "Title", "展开原始详情", 17, FontStyle.Bold, new Vector2(-112f, 0f), new Vector2(310f, 36f), TextAnchor.MiddleLeft, TextPrimary);
            EnsureIcon(rawDetailsButton.transform, "Chevron", QuestUiIconKind.ChevronRight, new Vector2(250f, 0f), new Vector2(20f, 20f), TextSecondary);

            rawDetailsRoot = EnsureRect(debugDrawer, "Raw Details", new Vector2(0f, -126f), new Vector2(548f, 104f));
            EnsureSurface(rawDetailsRoot, new Color(0.035f, 0.052f, 0.051f, 1f), 6f, false);
            rawDiagnosticsText = CreateText(rawDetailsRoot, "Text", string.Empty, 13, FontStyle.Normal, Vector2.zero, new Vector2(510f, 86f), TextAnchor.UpperLeft, TextSecondary);
            rawDiagnosticsText.textWrappingMode = TextWrappingModes.Normal;
            rawDiagnosticsText.overflowMode = TextOverflowModes.Truncate;

            copyDiagnosticsButton = CreateTextButton(debugDrawer, "Copy Complete Diagnostics", "复制完整诊断信息", new Vector2(0f, -226f), new Vector2(548f, 52f), new Color(0.035f, 0.11f, 0.09f, 1f), AccentStrong);
            EnsureIcon(copyDiagnosticsButton.transform, "Icon", QuestUiIconKind.Copy, new Vector2(-160f, 0f), new Vector2(22f, 22f), AccentStrong);

            debugScrim.SetAsLastSibling();
            debugDrawer.SetAsLastSibling();
        }

        private void WireUi()
        {
            WireButton(songSearchPageButton, OpenSongSearchPage);
            WireButton(queuePageButton, () => ShowPage(UiPage.Queue));
            WireButton(settingsPageButton, () => ShowPage(UiPage.Settings));
            WireButton(voicePageButton, () => ShowPage(UiPage.Voice));
            WireButton(voiceBackButton, () => ShowPage(UiPage.Home));
            WireButton(songSearchBackButton, () => ShowPage(UiPage.Home));
            WireButton(queueBackButton, () => ShowPage(UiPage.Home));
            WireButton(settingsBackButton, () => ShowPage(UiPage.Home));
            WireButton(openServiceSettingsButton, () => ShowPage(UiPage.Service));
            WireButton(serviceBackButton, () => ShowPage(UiPage.Settings));
            WireButton(openMicProtectionButton, () => ShowPage(UiPage.MicProtection));
            WireButton(micProtectionBackButton, () => ShowPage(UiPage.Settings));

            WireButton(replayButton, () => playlistPrototype?.SendReplay());
            WireButton(previousButton, () => playlistPrototype?.SendPrevious());
            WireButton(playPauseButton, () => playlistPrototype?.SendPlayPause());
            WireButton(nextButton, () => playlistPrototype?.SendNext());
            WireButton(microphoneButton, () => audioPrototype?.ToggleMonitoring());

            WireButton(songSearchButton, () => SearchSongs(1));
            WireButton(suggestSearchButton, SearchSuggestions);
            WireButton(voiceSearchButton, ToggleVoiceSearch);
            appendKtvSearchToggle.onValueChanged.RemoveAllListeners();
            appendKtvSearchToggle.SetIsOnWithoutNotify(appendKtvToSearch);
            appendKtvSearchToggle.onValueChanged.AddListener(HandleAppendKtvSearchToggle);
            RefreshAppendKtvSearchToggle();
            songSearchInput.onSubmit.RemoveAllListeners();
            songSearchInput.onSubmit.AddListener(_ => SearchSongs(1));
            songSearchInput.onSelect.RemoveAllListeners();
            songSearchInput.onSelect.AddListener(_ => BeginEditingSearch());
            songSearchInput.onValueChanged.RemoveAllListeners();
            WireButton(searchPreviousPageButton, () => SearchSongs(Mathf.Max(1, songSearchPageNumber - 1)));
            WireButton(searchNextPageButton, () => SearchSongs(songSearchPageNumber + 1));
            WireButton(clearSearchButton, ClearSearchResults);
            WireButton(clearQueueButton, ClearQueue);
            for (var index = 0; index < QueueRowCount; index += 1)
            {
                var rowIndex = index;
                WireButton(queueRemoveButtons[index], () => RemoveQueueRow(rowIndex));
            }
            for (var index = 0; index < searchResultAddButtons.Length; index += 1)
            {
                var resultIndex = index;
                WireButton(searchResultAddButtons[index], () => AddSearchResult(resultIndex));
            }
            for (var index = 0; index < SuggestRowCount; index += 1)
            {
                var suggestIndex = index;
                WireButton(suggestButtons[index], () => SelectSuggestion(suggestIndex));
            }

            voiceMicrophoneToggle.onValueChanged.RemoveAllListeners();
            voiceMicrophoneToggle.onValueChanged.AddListener(HandleMicrophoneToggle);
            monitorVolumeSlider.onValueChanged.RemoveAllListeners();
            monitorVolumeSlider.onValueChanged.AddListener(value => audioPrototype?.SetMonitorVolume(value));
            for (var index = 0; index < presetButtons.Length; index += 1)
            {
                var presetIndex = index;
                WireButton(presetButtons[index], () =>
                {
                    audioPrototype?.SelectPreset(presetIndex);
                    RefreshVoice();
                });
            }

            WireButton(companionModeButton, SelectCompanionService);
            WireButton(onlineModeButton, SelectOnlineService);
            WireButton(tencentProviderButton, () => SelectVoiceProvider("tencent"));
            WireButton(mimoProviderButton, () => SelectVoiceProvider("mimo"));
            WireButton(applyHostButton, ApplyServiceAddress);
            WireButton(defaultHostButton, ApplyDefaultServiceAddress);
            monitorOutputToggle.onValueChanged.RemoveAllListeners();
            monitorOutputToggle.onValueChanged.AddListener(value => audioPrototype?.SetMonitorOutput(value));
            safetyToggle.onValueChanged.RemoveAllListeners();
            safetyToggle.onValueChanged.AddListener(value => audioPrototype?.SetSafetyLimiterEnabled(value));
            nativeToggle.onValueChanged.RemoveAllListeners();
            nativeToggle.onValueChanged.AddListener(value => audioPrototype?.SetPreferNativeBackend(value));

            micProtectionToggle.onValueChanged.RemoveAllListeners();
            micProtectionToggle.onValueChanged.AddListener(value => handheldPropsPrototype?.SetMicFaceHapticsEnabled(value));
            if (voiceSearchEnabledToggle != null)
            {
                voiceSearchEnabledToggle.onValueChanged.RemoveAllListeners();
                voiceSearchEnabledToggle.onValueChanged.AddListener(value =>
                {
                    playlistPrototype?.SetVoiceSearchEnabled(value);
                    RefreshSettings();
                });
            }
            micWarningDistanceSlider.onValueChanged.RemoveAllListeners();
            micWarningDistanceSlider.onValueChanged.AddListener(value => handheldPropsPrototype?.SetMicFaceWarningClearance(value * MicClearanceStep));
            micCriticalDistanceSlider.onValueChanged.RemoveAllListeners();
            micCriticalDistanceSlider.onValueChanged.AddListener(value => handheldPropsPrototype?.SetMicFaceCriticalClearance(value * MicClearanceStep));
            micHapticStrengthSlider.onValueChanged.RemoveAllListeners();
            micHapticStrengthSlider.onValueChanged.AddListener(value => handheldPropsPrototype?.SetMicFaceHapticStrength(value * 0.1f));
            WireButton(captureWarningDistanceButton, () => StartMicFaceCalibration(captureWarning: true));
            WireButton(captureCriticalDistanceButton, () => StartMicFaceCalibration(captureWarning: false));
            WireButton(resetMicProtectionButton, ResetMicProtectionPreferences);

            WireButton(openDiagnosticsButton, () => SetDebugDrawerVisible(true));
            WireButton(closeDiagnosticsButton, () => SetDebugDrawerVisible(false));
            WireButton(debugScrimButton, () => SetDebugDrawerVisible(false));
            WireButton(rawDetailsButton, ToggleRawDetails);
            WireButton(copyDiagnosticsButton, () => appShellPrototype?.CopyCompleteDebugInfoToClipboard());
        }

        private void RefreshAll()
        {
            RefreshHome();
            RefreshSongSearch();
            RefreshSuggestions();
            RefreshVoice();
            RefreshQueue();
            RefreshSettings();
            RefreshMicProtection();
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
                songDetailText.text = connected ? "打开搜索点歌选择视频" : "请在设置中检查点歌服务";
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

        private void RefreshSongSearch()
        {
            if (songSearchInput == null || playlistPrototype == null)
            {
                return;
            }

            RefreshVoiceSearch();

            var response = playlistPrototype.SearchResults;
            var items = response?.items ?? Array.Empty<BilibiliCatalogItem>();
            for (var index = 0; index < SearchResultRowCount; index += 1)
            {
                var visible = index < items.Length && items[index] != null && items[index].IsValid;
                searchResultRows[index].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                var item = items[index];
                var isPending = playlistPrototype.IsAddingItem && playlistPrototype.PendingAddItem == item;
                searchResultSurfaces[index].color = isPending
                    ? new Color(0.035f, 0.16f, 0.13f, 1f)
                    : Surface;
                searchResultTitleTexts[index].text = SafeText(item.title, item.bvid);
                searchResultMetaTexts[index].text = $"{SafeText(item.author, "未知 UP 主")} · {FormatDuration(item)} · {item.bvid}";
                searchResultAddButtons[index].interactable = playlistPrototype.CanAddItem && !isPending;
            }

            songSearchButton.interactable = playlistPrototype.IsConnected &&
                                            !playlistPrototype.IsSearching &&
                                            !string.IsNullOrWhiteSpace(songSearchInput.text);
            searchPreviousPageButton.interactable = !playlistPrototype.IsSearching && songSearchPageNumber > 1;
            searchNextPageButton.interactable = !playlistPrototype.IsSearching && response != null && response.hasMore;
            if (clearSearchButton != null)
            {
                clearSearchButton.interactable = !playlistPrototype.IsSearching &&
                                                 !playlistPrototype.IsVoiceBusy &&
                                                 playlistPrototype.HasSearchResults;
            }

            if (!playlistPrototype.IsConnected)
            {
                songSearchStatusText.text = $"{playlistPrototype.ServiceDisplayName}未连接，请检查服务设置";
                songSearchStatusText.color = Warm;
            }
            else if (playlistPrototype.IsSearching)
            {
                songSearchStatusText.text = "正在搜索 Bilibili";
                songSearchStatusText.color = TextSecondary;
            }
            else if (!string.IsNullOrWhiteSpace(playlistPrototype.LastSearchError))
            {
                songSearchStatusText.text = SingleLine(playlistPrototype.LastSearchError);
                songSearchStatusText.color = Danger;
            }
            else if (playlistPrototype.IsAddingItem)
            {
                songSearchStatusText.text = $"正在点播 · {SafeText(playlistPrototype.PendingAddItem?.title, "视频")}";
                songSearchStatusText.color = TextSecondary;
            }
            else if (!string.IsNullOrWhiteSpace(playlistPrototype.LastAddItemError))
            {
                songSearchStatusText.text = SingleLine(playlistPrototype.LastAddItemError);
                songSearchStatusText.color = Danger;
            }
            else if (playlistPrototype.LastAddedItem != null && playlistPrototype.LastAddedItem.IsValid)
            {
                songSearchStatusText.text = $"已点播 · {SafeText(playlistPrototype.LastAddedItem.title, playlistPrototype.LastAddedItem.bvid)}";
                songSearchStatusText.color = Accent;
            }
            else if (response != null)
            {
                songSearchStatusText.text = items.Length == 0
                    ? "没有找到相关视频"
                    : $"第 {Mathf.Max(1, response.page)} 页 · 共 {Mathf.Max(items.Length, response.total)} 个结果";
                songSearchStatusText.color = TextSecondary;
            }
            else
            {
                songSearchStatusText.text = "输入关键词后搜索";
                songSearchStatusText.color = TextSecondary;
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
                var outline = presetButtons[index].GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = isSelected ? AccentStrong : Line;
                    outline.effectDistance = isSelected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
                }

                var label = presetButtons[index].GetComponentInChildren<TMP_Text>(true);
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
                    queueRowItemIds[rowIndex] = null;
                    continue;
                }

                var item = state.queue[itemIndex];
                var isCurrent = itemIndex == state.currentIndex;
                queueRowItemIds[rowIndex] = item.id;
                if (queueRemoveButtons[rowIndex] != null)
                {
                    queueRemoveButtons[rowIndex].interactable =
                        playlistPrototype != null && playlistPrototype.CanSendControl;
                }
                queueRowSurfaces[rowIndex].color = isCurrent
                    ? new Color(0.035f, 0.16f, 0.13f, 1f)
                    : Surface;
                queueIndicators[rowIndex].gameObject.SetActive(isCurrent);
                queueTitleTexts[rowIndex].text = SafeText(item.title, "未命名歌曲");
                queueMetaTexts[rowIndex].text = $"{FormatSource(item.sourceType)} · {FormatItemStatus(item.status)}";
                var numberText = queueRows[rowIndex].Find("Number")?.GetComponent<TMP_Text>();
                if (numberText != null)
                {
                    numberText.text = (itemIndex + 1).ToString("00");
                }
            }

            queueFooterText.text = count == 0
                ? "播放队列为空"
                : count > QueueRowCount ? $"共 {count} 首 · 当前显示附近歌曲" : $"共 {count} 首";

            if (clearQueueButton != null)
            {
                // 清空会保留正在播放的那首，所以只有一首歌时点它没有意义。
                clearQueueButton.interactable = playlistPrototype != null &&
                                                playlistPrototype.CanSendControl &&
                                                count > 1;
            }
        }

        private void RefreshSettings()
        {
            var connected = playlistPrototype != null && playlistPrototype.IsConnected;
            var serviceName = playlistPrototype?.ServiceDisplayName ?? "未配置";
            settingsConnectionText.text = connected ? "已连接" : "未连接";
            settingsConnectionText.color = connected ? Accent : Warm;
            if (serviceConnectionText != null)
            {
                serviceConnectionText.text = connected ? $"已连接 · {serviceName}" : $"未连接 · {serviceName}";
                serviceConnectionText.color = connected ? Accent : Warm;
            }

            var settingsHint = openServiceSettingsButton?.transform.Find("Hint")?.GetComponent<TMP_Text>();
            if (settingsHint != null)
            {
                settingsHint.text = SingleLine($"{serviceName} · {playlistPrototype?.ServiceAddress ?? "未配置"}");
            }

            settingsBuildText.text = QuestBuildInfo.SettingsSummary;
            if (helperHostInput != null && !helperHostInput.isFocused && playlistPrototype != null)
            {
                helperHostInput.SetTextWithoutNotify(playlistPrototype.ServiceAddress);
            }

            if (helperHostInput?.placeholder is TMP_Text placeholder)
            {
                placeholder.text = playlistPrototype != null && playlistPrototype.IsOnlineService
                    ? QuestPlaylistPrototype.DefaultOnlineServiceOrigin
                    : QuestPlaylistPrototype.DefaultHelperHostAddress;
            }

            SetServiceModeButtonVisual(companionModeButton, playlistPrototype == null || !playlistPrototype.IsOnlineService);
            SetServiceModeButtonVisual(onlineModeButton, playlistPrototype != null && playlistPrototype.IsOnlineService);

            if (voiceSearchEnabledToggle != null && playlistPrototype != null)
            {
                voiceSearchEnabledToggle.SetIsOnWithoutNotify(playlistPrototype.IsVoiceSearchEnabled);
                RefreshSwitchVisual(voiceSearchEnabledToggle, Accent);
                SetVoiceFeatureVisible(playlistPrototype.IsVoiceSearchEnabled);
                RefreshVoiceProviderButtons();
                if (voiceSearchHintText != null)
                {
                    voiceSearchHintText.text = !playlistPrototype.IsVoiceSearchEnabled
                        ? "已关闭：入口已隐藏，不再采集或上传语音"
                        : audioPrototype != null && audioPrototype.PrefersNativeOboeBackend
                            ? "Native 低延迟后端下无法采集语音，请先关闭它"
                            : DescribeVoiceProviderHint();
                }
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

        private void RefreshMicProtection()
        {
            if (handheldPropsPrototype == null)
            {
                handheldPropsPrototype = FindAnyObjectByType<QuestHandheldPropsPrototype>();
            }

            var available = handheldPropsPrototype != null;
            openMicProtectionButton.interactable = available;
            openMicProtectionSurface.color = available && handheldPropsPrototype.MicFaceHapticsEnabled
                ? new Color(0.035f, 0.12f, 0.095f, 1f)
                : Surface;
            openMicProtectionIcon.color = available && handheldPropsPrototype.MicFaceHapticsEnabled ? Accent : TextSecondary;

            if (!available)
            {
                micLiveDistanceText.text = "-- cm";
                micLiveStateText.text = "麦克风组件缺失";
                micLiveStateText.color = Danger;
                SetMicProtectionControlsInteractable(false);
                return;
            }

            micProtectionToggle.SetIsOnWithoutNotify(handheldPropsPrototype.MicFaceHapticsEnabled);
            RefreshSwitchVisual(micProtectionToggle, Accent);
            micWarningDistanceSlider.SetValueWithoutNotify(handheldPropsPrototype.MicFaceWarningClearance / MicClearanceStep);
            micCriticalDistanceSlider.SetValueWithoutNotify(handheldPropsPrototype.MicFaceCriticalClearance / MicClearanceStep);
            micHapticStrengthSlider.SetValueWithoutNotify(handheldPropsPrototype.MicFaceHapticStrength / 0.1f);
            micWarningDistanceValueText.text = FormatClearance(handheldPropsPrototype.MicFaceWarningClearance);
            micCriticalDistanceValueText.text = FormatClearance(handheldPropsPrototype.MicFaceCriticalClearance);
            micHapticStrengthValueText.text = $"{handheldPropsPrototype.MicFaceHapticStrength:P0}";

            var clearance = handheldPropsPrototype.MicrophoneFaceSurfaceClearance;
            if (float.IsPositiveInfinity(clearance))
            {
                micLiveDistanceText.text = "-- cm";
                micLiveStateText.text = "等待右手麦克风";
                micLiveStateText.color = TextSecondary;
            }
            else
            {
                micLiveDistanceText.text = FormatClearance(clearance);
                if (!handheldPropsPrototype.MicFaceHapticsEnabled)
                {
                    micLiveStateText.text = "震动已关闭";
                    micLiveStateText.color = TextSecondary;
                }
                else if (handheldPropsPrototype.IsMicrophoneFaceCritical)
                {
                    micLiveStateText.text = "强震区域";
                    micLiveStateText.color = Danger;
                }
                else if (handheldPropsPrototype.IsMicrophoneFaceWarningActive)
                {
                    micLiveStateText.text = "轻震区域";
                    micLiveStateText.color = Warm;
                }
                else
                {
                    micLiveStateText.text = "安全距离";
                    micLiveStateText.color = Accent;
                }
            }

            SetMicProtectionControlsInteractable(micCalibrationCoroutine == null);
        }

        private void SetMicProtectionControlsInteractable(bool interactable)
        {
            micProtectionToggle.interactable = interactable;
            micWarningDistanceSlider.interactable = interactable;
            micCriticalDistanceSlider.interactable = interactable;
            micHapticStrengthSlider.interactable = interactable;
            captureWarningDistanceButton.interactable = interactable;
            captureCriticalDistanceButton.interactable = interactable;
            resetMicProtectionButton.interactable = interactable;
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
                ? $"点歌服务：已连接 · {playlistPrototype.ServiceDisplayName}"
                : $"点歌服务：未连接 · {playlistPrototype?.ServiceDisplayName ?? "未配置"}";

            var videoSummary = videoScreenPrototype == null
                ? "视频组件缺失"
                : videoScreenPrototype.IsPreparing ? "视频正在准备"
                : videoScreenPrototype.IsPlaying ? "视频正在播放"
                : "视频待机";
            diagnosticsVideoText.text = $"视频状态：{videoSummary}\n{SingleLine(videoScreenPrototype?.StatusSummary)}";

            rawDiagnosticsText.text =
                $"构建信息  {QuestBuildInfo.RawSummary}\n" +
                $"音频后端  {audioPrototype?.ActiveBackendName ?? "missing"}\n" +
                $"人声预设  {audioPrototype?.CurrentPresetName ?? "missing"}\n" +
                $"输入/输出  {(audioPrototype?.InputLevel ?? 0f):P0} / {(audioPrototype?.OutputLevel ?? 0f):P0}\n" +
                $"防碰撞  {(handheldPropsPrototype == null ? "missing" : $"{handheldPropsPrototype.MicFaceWarningClearance * 100f:0.0}/{handheldPropsPrototype.MicFaceCriticalClearance * 100f:0.0}cm {handheldPropsPrototype.MicFaceHapticStrength:P0}")}\n" +
                $"播放服务  {playlistPrototype?.PlaylistOrigin ?? "missing"}\n" +
                $"语音找歌  {DescribeVoiceDiagnostics()}\n" +
                $"视频状态  {SingleLine(videoScreenPrototype?.StatusSummary)}";
        }

        /// <summary>
        /// Voice search line for the diagnostics drawer. Includes the recognition
        /// provider and last error code so on-device triage can tell apart capture,
        /// network, upstream throttling and quota problems.
        /// </summary>
        private string DescribeVoiceDiagnostics()
        {
            if (playlistPrototype == null)
            {
                return "missing";
            }
            if (!playlistPrototype.IsVoiceSearchEnabled)
            {
                return "已关闭";
            }

            var parts = playlistPrototype.VoiceState.ToString();
            if (!playlistPrototype.CanStartVoiceSearch && !playlistPrototype.IsVoiceBusy)
            {
                parts += " 不可用";
            }
            // 选中的供应商始终显示；VoiceProvider 是上一次实际识别用的那个。
            var selection = playlistPrototype.VoiceProviderSelection;
            if (!string.IsNullOrEmpty(selection))
            {
                parts += $" · 选中 {selection}";
            }
            if (!string.IsNullOrEmpty(playlistPrototype.VoiceProvider))
            {
                parts += $" · 上次 {playlistPrototype.VoiceProvider}";
            }
            if (!string.IsNullOrEmpty(playlistPrototype.VoiceErrorCode))
            {
                parts += $" · {playlistPrototype.VoiceErrorCode}";
            }
            return parts;
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

        private void ApplyServiceAddress()
        {
            playlistPrototype?.ApplyServiceAddress(helperHostInput != null ? helperHostInput.text : string.Empty);
            RefreshSettings();
        }

        private void OpenSongSearchPage()
        {
            // 不自动搜索：启动后搜索列表保持为空，预设 BV 号只留在输入框里，
            // 由用户主动点搜索或用语音找歌。
            ShowPage(UiPage.SongSearch);
        }

        /// <summary>
        /// Clears the search page: results, status and the voice transcript receipt.
        /// The input field keeps its text so the same query can be re-run.
        /// </summary>
        private void SelectVoiceProvider(string provider)
        {
            playlistPrototype?.SelectVoiceProvider(provider);
            RefreshSettings();
        }

        /// <summary>
        /// Highlights the active provider and disables ones the server has no
        /// credentials for, so an unusable channel cannot be selected.
        /// </summary>
        private void RefreshVoiceProviderButtons()
        {
            if (playlistPrototype == null)
            {
                return;
            }

            var selection = playlistPrototype.VoiceProviderSelection;
            var options = playlistPrototype.VoiceProviderOptions;
            var busy = playlistPrototype.IsSwitchingVoiceProvider || playlistPrototype.IsVoiceBusy;
            var enabled = playlistPrototype.IsVoiceSearchEnabled;

            SetVoiceProviderButton(tencentProviderButton, "tencent", selection, options, busy, enabled);
            SetVoiceProviderButton(mimoProviderButton, "mimo", selection, options, busy, enabled);
        }

        private static void SetVoiceProviderButton(
            Button button,
            string provider,
            string selection,
            string[] options,
            bool busy,
            bool voiceEnabled)
        {
            if (button == null)
            {
                return;
            }

            var isSelected = string.Equals(selection, provider, StringComparison.OrdinalIgnoreCase);
            var isConfigured = false;
            if (options != null)
            {
                for (var index = 0; index < options.Length; index += 1)
                {
                    if (string.Equals(options[index], provider, StringComparison.OrdinalIgnoreCase))
                    {
                        isConfigured = true;
                        break;
                    }
                }
            }

            SetServiceModeButtonVisual(button, isSelected);
            button.interactable = voiceEnabled && isConfigured && !isSelected && !busy;

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null && !isSelected)
            {
                // 未配置密钥的供应商压暗，明确表示不可选而不是点了没反应。
                label.color = isConfigured ? TextPrimary : TextFaint;
            }
        }

        private string DescribeVoiceProviderHint()
        {
            if (playlistPrototype == null)
            {
                return "按一下搜索页的麦克风按钮说出歌名";
            }
            if (playlistPrototype.IsSwitchingVoiceProvider)
            {
                return "正在切换识别供应商…";
            }

            var options = playlistPrototype.VoiceProviderOptions;
            if (options == null || options.Length == 0)
            {
                return "服务端未配置语音识别密钥";
            }

            var current = QuestPlaylistPrototype.DescribeVoiceProvider(playlistPrototype.VoiceProviderSelection);
            if (options.Length == 1)
            {
                return $"当前使用 {current}；服务端只配置了这一个供应商";
            }

            // 明确说明这是本机设置，避免误以为会改到别的头显。
            return playlistPrototype.IsVoiceProviderDeviceSelected
                ? $"当前使用 {current}（本机设置）；两者互不回退，切换后立即生效"
                : $"当前使用 {current}（服务端默认）；切换后仅影响本机";
        }

        /// <summary>
        /// Clears the search page: results, status and the voice transcript receipt.
        /// The input field keeps its text so the same query can be re-run.
        /// </summary>
        private void ClearSearchResults()
        {
            songSearchPageNumber = 1;
            playlistPrototype?.ClearSearchResults();
            RefreshSongSearch();
        }

        private void ClearQueue()
        {
            playlistPrototype?.ClearQueue();
            RefreshQueue();
        }

        /// <summary>
        /// Removes the song shown on one queue row. Row index is resolved to the
        /// item id captured during the last refresh, because the visible window
        /// scrolls with the current song.
        /// </summary>
        private void RemoveQueueRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= QueueRowCount)
            {
                return;
            }

            var itemId = queueRowItemIds[rowIndex];
            if (string.IsNullOrEmpty(itemId))
            {
                return;
            }

            playlistPrototype?.RemoveQueueItem(itemId);
            RefreshQueue();
        }

        private void ToggleVoiceSearch()
        {
            if (playlistPrototype == null)
            {
                return;
            }

            var wasIdle = !playlistPrototype.IsVoiceBusy;
            playlistPrototype.ToggleVoiceSearch();

            // 开始与提交各给一次轻触觉反馈。
            if (wasIdle && playlistPrototype.IsVoiceBusy)
            {
                handheldPropsPrototype?.PulseVoiceFeedback();
            }
            RefreshSongSearch();
        }

        /// <summary>
        /// Renders the eight voice search states. Every state has visible copy, and
        /// failures stay distinguishable so on-device triage is possible.
        /// </summary>
        private void RefreshVoiceSearch()
        {
            if (voiceSearchButton == null || playlistPrototype == null)
            {
                return;
            }

            var visible = playlistPrototype.IsVoiceSearchEnabled;
            SetVoiceFeatureVisible(visible);
            if (!visible)
            {
                return;
            }

            var state = playlistPrototype.VoiceState;
            var listening = state == VoiceSearchUiState.Listening;
            var busy = playlistPrototype.IsVoiceBusy;

            voiceSearchButton.interactable = busy || playlistPrototype.CanStartVoiceSearch;
            if (voiceSearchIcon != null)
            {
                voiceSearchIcon.color = listening ? Danger : AccentInk;
            }

            switch (state)
            {
                case VoiceSearchUiState.Listening:
                    voiceSearchStatusText.text = "正在听… 再按一次取消";
                    voiceSearchStatusText.color = Accent;
                    break;
                case VoiceSearchUiState.Uploading:
                    voiceSearchStatusText.text = "识别中…";
                    voiceSearchStatusText.color = TextSecondary;
                    break;
                case VoiceSearchUiState.Searching:
                    voiceSearchStatusText.text = "正在找歌…";
                    voiceSearchStatusText.color = TextSecondary;
                    break;
                case VoiceSearchUiState.Results:
                    voiceSearchStatusText.text = "语音找歌完成";
                    voiceSearchStatusText.color = Accent;
                    break;
                case VoiceSearchUiState.Empty:
                    voiceSearchStatusText.text = "没找到视频，可改用下方文字搜索";
                    voiceSearchStatusText.color = Warm;
                    break;
                case VoiceSearchUiState.NoSpeech:
                    voiceSearchStatusText.text = SafeText(playlistPrototype.VoiceErrorMessage, "没听到声音，再试一次");
                    voiceSearchStatusText.color = Warm;
                    break;
                case VoiceSearchUiState.Failed:
                    voiceSearchStatusText.text = SingleLine(
                        SafeText(playlistPrototype.VoiceErrorMessage, "语音找歌失败"));
                    voiceSearchStatusText.color = Danger;
                    break;
                default:
                    voiceSearchStatusText.text = playlistPrototype.CanStartVoiceSearch
                        ? "按一下说出歌名"
                        : "语音找歌暂不可用";
                    voiceSearchStatusText.color = playlistPrototype.CanStartVoiceSearch
                        ? TextPrimary
                        : TextSecondary;
                    break;
            }

            // "听到：" 只是可读回执，不是可编辑输入框。
            var transcript = playlistPrototype.VoiceTranscript;
            voiceHeardText.text = string.IsNullOrWhiteSpace(transcript)
                ? string.Empty
                : $"听到：{SingleLine(transcript)}";

            var level = listening ? Mathf.Clamp01(playlistPrototype.VoiceLevel) : 0f;
            for (var index = 0; index < VoiceLevelBarCount; index += 1)
            {
                if (voiceLevelBars[index] == null)
                {
                    continue;
                }

                var bar = (RectTransform)voiceLevelBars[index].transform;
                if (!listening)
                {
                    bar.sizeDelta = new Vector2(6f, 3f);
                    voiceLevelBars[index].color = new Color(0.18f, 0.26f, 0.25f, 1f);
                    continue;
                }

                // 中间的条更高，形成常见的电平包络形状。
                var distance = Mathf.Abs(index - (VoiceLevelBarCount - 1) * 0.5f) / ((VoiceLevelBarCount - 1) * 0.5f);
                var scale = Mathf.Lerp(1f, 0.35f, distance);
                var height = Mathf.Max(3f, level * 14f * scale);
                bar.sizeDelta = new Vector2(6f, height);
                voiceLevelBars[index].color = Accent;
            }
        }

        private void SetVoiceFeatureVisible(bool visible)
        {
            voiceSearchButton?.gameObject.SetActive(visible);
            voiceSearchStatusText?.gameObject.SetActive(visible);
            voiceHeardText?.gameObject.SetActive(visible);
            voiceLevelRoot?.gameObject.SetActive(visible);
            SetSongSearchVoiceLayout(visible);

            voiceProviderLabelText?.gameObject.SetActive(visible);
            tencentProviderButton?.gameObject.SetActive(visible);
            mimoProviderButton?.gameObject.SetActive(visible);
        }

        private void SetSongSearchVoiceLayout(bool voiceVisible)
        {
            var searchRowY = voiceVisible ? 112f : 150f;
            SetAnchoredY(songSearchInput?.transform as RectTransform, searchRowY);
            SetAnchoredY(appendKtvSearchLabel?.rectTransform, searchRowY);
            SetAnchoredY(appendKtvSearchToggle?.transform as RectTransform, searchRowY);
            SetAnchoredY(suggestSearchButton?.transform as RectTransform, searchRowY);
            SetAnchoredY(songSearchButton?.transform as RectTransform, searchRowY);
            SetAnchoredY(suggestContainer, voiceVisible ? -32f : 0f);

            var firstResultY = voiceVisible ? 48f : 78f;
            for (var index = 0; index < searchResultRows.Length; index += 1)
            {
                SetAnchoredY(searchResultRows[index], firstResultY - index * 66f);
            }

            var footerY = voiceVisible ? -226f : -200f;
            SetAnchoredY(searchPreviousPageButton?.transform as RectTransform, footerY);
            SetAnchoredY(clearSearchButton?.transform as RectTransform, footerY);
            SetAnchoredY(songSearchStatusText?.rectTransform, footerY);
            SetAnchoredY(searchNextPageButton?.transform as RectTransform, footerY);
        }

        private static void SetAnchoredY(RectTransform rect, float y)
        {
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
            }
        }

        private void SearchSongs(int page)
        {
            if (playlistPrototype == null || songSearchInput == null)
            {
                return;
            }

            var query = songSearchInput.text?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                songSearchStatusText.text = "请输入歌名、歌手或 BV 号";
                songSearchStatusText.color = Danger;
                return;
            }

            songSearchPageNumber = Mathf.Max(1, page);
            playlistPrototype.SearchBilibili(BuildSongSearchQuery(query), songSearchPageNumber, SearchResultRowCount);

            // 搜索时隐藏建议
            HideSuggestions();
        }

        private string BuildSongSearchQuery(string query)
        {
            if (!appendKtvToSearch ||
                string.Equals(query, "KTV", StringComparison.OrdinalIgnoreCase) ||
                query.EndsWith(" KTV", StringComparison.OrdinalIgnoreCase))
            {
                return query;
            }

            return $"{query} KTV";
        }

        private void HandleAppendKtvSearchToggle(bool enabled)
        {
            appendKtvToSearch = enabled;
            PlayerPrefs.SetInt(AppendKtvSearchPrefsKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshAppendKtvSearchToggle();
        }

        private void RefreshAppendKtvSearchToggle()
        {
            if (appendKtvSearchToggle == null)
            {
                return;
            }

            appendKtvSearchToggle.SetIsOnWithoutNotify(appendKtvToSearch);
            RefreshSwitchVisual(appendKtvSearchToggle, Accent);
            if (appendKtvSearchLabel != null)
            {
                appendKtvSearchLabel.color = appendKtvToSearch ? TextPrimary : TextSecondary;
            }
        }

        private void SearchSuggestions()
        {
            if (playlistPrototype == null || songSearchInput == null)
            {
                return;
            }

            var keyboard = songSearchInput.GetComponent<QuestAndroidKeyboardInput>();
            keyboard?.HideKeyboard();

            var query = songSearchInput.text?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                HideSuggestions();
                songSearchStatusText.text = "请输入内容后再获取搜索建议";
                songSearchStatusText.color = Danger;
                return;
            }

            if (suggestContainer != null)
            {
                suggestContainer.gameObject.SetActive(false);
            }

            songSearchStatusText.text = "正在获取搜索建议…";
            songSearchStatusText.color = TextSecondary;
            playlistPrototype.FetchBilibiliSuggestions(query);
        }

        private void BeginEditingSearch()
        {
            if (playlistPrototype == null ||
                (!playlistPrototype.IsFetchingSuggestions && playlistPrototype.SuggestResults == null))
            {
                return;
            }

            HideSuggestions();
        }

        private void SelectSuggestion(int index)
        {
            var suggestions = playlistPrototype?.SuggestResults?.result?.tag;
            if (suggestions == null || index < 0 || index >= suggestions.Length)
            {
                return;
            }

            var selected = suggestions[index];
            if (string.IsNullOrWhiteSpace(selected.value))
            {
                return;
            }

            // 填入搜索框
            if (songSearchInput != null)
            {
                songSearchInput.text = selected.value;
            }

            // 隐藏建议
            HideSuggestions();
        }

        private void HideSuggestions()
        {
            if (suggestContainer != null)
            {
                suggestContainer.gameObject.SetActive(false);
            }
            playlistPrototype?.ClearSuggestions();
        }

        private void RefreshSuggestions()
        {
            if (suggestContainer == null || playlistPrototype == null)
            {
                return;
            }

            if (suggestSearchButton != null)
            {
                suggestSearchButton.interactable = playlistPrototype.IsConnected &&
                                                   !playlistPrototype.IsFetchingSuggestions &&
                                                   !string.IsNullOrWhiteSpace(songSearchInput?.text);
            }

            var keyboard = songSearchInput != null
                ? songSearchInput.GetComponent<QuestAndroidKeyboardInput>()
                : null;
            if (keyboard != null && keyboard.IsKeyboardOpen)
            {
                suggestContainer.gameObject.SetActive(false);
                return;
            }

            if (playlistPrototype.IsFetchingSuggestions)
            {
                suggestContainer.gameObject.SetActive(false);
                songSearchStatusText.text = "正在获取搜索建议…";
                songSearchStatusText.color = TextSecondary;
                return;
            }

            var suggestions = playlistPrototype.SuggestResults?.result?.tag;
            var hasSuggestions = suggestions != null && suggestions.Length > 0;

            suggestContainer.gameObject.SetActive(hasSuggestions);

            if (!hasSuggestions)
            {
                if (playlistPrototype.SuggestResults != null)
                {
                    songSearchStatusText.text = "没有找到搜索建议";
                    songSearchStatusText.color = TextSecondary;
                }
                return;
            }

            suggestContainer.SetAsLastSibling();
            songSearchStatusText.text = $"找到 {Mathf.Min(SuggestRowCount, suggestions.Length)} 条搜索建议";
            songSearchStatusText.color = Accent;

            for (var index = 0; index < SuggestRowCount; index += 1)
            {
                var visible = index < suggestions.Length;
                if (suggestRows[index] != null)
                {
                    suggestRows[index].gameObject.SetActive(visible);
                }

                if (visible && suggestTexts[index] != null)
                {
                    suggestTexts[index].text = suggestions[index].value;
                }
            }
        }

        private void AddSearchResult(int index)
        {
            var items = playlistPrototype?.SearchResults?.items;
            if (items == null || index < 0 || index >= items.Length || items[index] == null)
            {
                return;
            }

            playlistPrototype.AddItem(items[index], true);
        }

        private void ApplyDefaultServiceAddress()
        {
            if (helperHostInput != null)
            {
                helperHostInput.SetTextWithoutNotify(playlistPrototype != null && playlistPrototype.IsOnlineService
                    ? QuestPlaylistPrototype.DefaultOnlineServiceOrigin
                    : QuestPlaylistPrototype.DefaultHelperHostAddress);
            }

            playlistPrototype?.ApplyDefaultServiceAddress();
            RefreshSettings();
        }

        private void SelectCompanionService()
        {
            playlistPrototype?.UseCompanionService();
            helperHostInput?.SetTextWithoutNotify(playlistPrototype?.ServiceAddress ?? QuestPlaylistPrototype.DefaultHelperHostAddress);
            RefreshSettings();
        }

        private void SelectOnlineService()
        {
            playlistPrototype?.UseOnlineService();
            helperHostInput?.SetTextWithoutNotify(playlistPrototype?.ServiceAddress ?? QuestPlaylistPrototype.DefaultOnlineServiceOrigin);
            RefreshSettings();
        }

        private void StartMicFaceCalibration(bool captureWarning)
        {
            if (handheldPropsPrototype == null || !Application.isPlaying)
            {
                return;
            }

            CancelMicFaceCalibration();
            micCalibrationCoroutine = StartCoroutine(CaptureMicFaceDistance(captureWarning));
        }

        private IEnumerator CaptureMicFaceDistance(bool captureWarning)
        {
            handheldPropsPrototype.SetMicFaceHapticsSuppressed(true);
            var label = captureWarning ? "轻震起点" : "强震起点";
            for (var remaining = 3; remaining > 0; remaining -= 1)
            {
                micCalibrationStatusText.text = $"{label} · {remaining}";
                yield return new WaitForSecondsRealtime(1f);
            }

            micCalibrationStatusText.text = $"{label} · 采样中";
            var samples = new List<float>(64);
            const float sampleDuration = 0.7f;
            var elapsed = 0f;
            while (elapsed < sampleDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var clearance = handheldPropsPrototype.MicrophoneFaceSurfaceClearance;
                if (!float.IsPositiveInfinity(clearance) && !float.IsNaN(clearance))
                {
                    samples.Add(clearance);
                }

                yield return null;
            }

            if (samples.Count == 0)
            {
                micCalibrationStatusText.text = "未检测到右手麦克风";
            }
            else
            {
                samples.Sort();
                var capturedClearance = Mathf.Round(samples[samples.Count / 2] / MicClearanceStep) * MicClearanceStep;
                if (captureWarning)
                {
                    handheldPropsPrototype.SetMicFaceWarningClearance(capturedClearance);
                }
                else
                {
                    handheldPropsPrototype.SetMicFaceCriticalClearance(capturedClearance);
                }

                var savedClearance = captureWarning
                    ? handheldPropsPrototype.MicFaceWarningClearance
                    : handheldPropsPrototype.MicFaceCriticalClearance;
                micCalibrationStatusText.text = $"{label}已保存 · {FormatClearance(savedClearance)}";
            }

            handheldPropsPrototype.SetMicFaceHapticsSuppressed(false);
            RefreshMicProtection();
            yield return new WaitForSecondsRealtime(1.2f);
            micCalibrationStatusText.text = string.Empty;
            micCalibrationCoroutine = null;
            RefreshMicProtection();
        }

        private void CancelMicFaceCalibration()
        {
            if (micCalibrationCoroutine != null)
            {
                StopCoroutine(micCalibrationCoroutine);
                micCalibrationCoroutine = null;
            }

            handheldPropsPrototype?.SetMicFaceHapticsSuppressed(false);
            if (micCalibrationStatusText != null)
            {
                micCalibrationStatusText.text = string.Empty;
            }
        }

        private void ResetMicProtectionPreferences()
        {
            CancelMicFaceCalibration();
            handheldPropsPrototype?.ResetMicFaceHapticPreferences();
            micCalibrationStatusText.text = "已恢复默认参数";
            RefreshMicProtection();
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
            subscribedPlaylist.SearchStateChanged += HandleSearchStateChanged;
            subscribedPlaylist.AddItemStateChanged += HandleSearchStateChanged;
            subscribedPlaylist.SuggestStateChanged += HandleSuggestStateChanged;
            subscribedPlaylist.VoiceSearchStateChanged += HandleVoiceSearchStateChanged;
        }

        private void UnsubscribePlaylist()
        {
            if (subscribedPlaylist == null)
            {
                return;
            }

            subscribedPlaylist.StateChanged -= HandlePlaylistStateChanged;
            subscribedPlaylist.SearchStateChanged -= HandleSearchStateChanged;
            subscribedPlaylist.AddItemStateChanged -= HandleSearchStateChanged;
            subscribedPlaylist.SuggestStateChanged -= HandleSuggestStateChanged;
            subscribedPlaylist.VoiceSearchStateChanged -= HandleVoiceSearchStateChanged;
            subscribedPlaylist = null;
        }

        private void HandlePlaylistStateChanged(QuestPlaylistPrototype sender, PlaylistState state)
        {
            RefreshAll();
        }

        private void HandleSearchStateChanged(QuestPlaylistPrototype sender)
        {
            RefreshSongSearch();
        }

        private void HandleSuggestStateChanged(QuestPlaylistPrototype sender)
        {
            RefreshSuggestions();
        }

        private void HandleVoiceSearchStateChanged(QuestPlaylistPrototype sender)
        {
            var state = sender.VoiceState;
            // 提交时给一次轻震，让用户知道"说完了、已经在处理"。
            if (state != lastVoiceState && state == VoiceSearchUiState.Uploading)
            {
                handheldPropsPrototype?.PulseVoiceFeedback();
            }
            lastVoiceState = state;
            RefreshSongSearch();
        }

        private void ShowPage(UiPage page)
        {
            if (currentPage == UiPage.MicProtection && page != UiPage.MicProtection)
            {
                CancelMicFaceCalibration();
            }

            if (pageTransition != null)
            {
                StopCoroutine(pageTransition);
                pageTransition = null;
                ShowPageImmediate(currentPage);
            }

            if (page == currentPage)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                ShowPageImmediate(page);
                return;
            }

            var previousPage = currentPage;
            currentPage = page;
            pageTransition = StartCoroutine(AnimatePage(previousPage, page));
        }

        private IEnumerator AnimatePage(UiPage previous, UiPage next)
        {
            var previousGroup = GetPageGroup(previous);
            var nextGroup = GetPageGroup(next);
            var previousRect = previousGroup.GetComponent<RectTransform>();
            var nextRect = nextGroup.GetComponent<RectTransform>();
            previousGroup.interactable = false;
            previousGroup.blocksRaycasts = false;
            nextGroup.gameObject.SetActive(true);
            nextGroup.alpha = 0f;
            nextGroup.interactable = true;
            nextGroup.blocksRaycasts = true;
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
            previousGroup.interactable = false;
            previousGroup.blocksRaycasts = false;
            previousRect.anchoredPosition = Vector2.zero;
            nextGroup.alpha = 1f;
            nextGroup.interactable = true;
            nextGroup.blocksRaycasts = true;
            nextRect.anchoredPosition = Vector2.zero;
            pageTransition = null;
        }

        private void ShowPageImmediate(UiPage page)
        {
            currentPage = page;
            SetPageGroupImmediate(homeGroup, page == UiPage.Home);
            SetPageGroupImmediate(songSearchGroup, page == UiPage.SongSearch);
            SetPageGroupImmediate(voiceGroup, page == UiPage.Voice);
            SetPageGroupImmediate(queueGroup, page == UiPage.Queue);
            SetPageGroupImmediate(settingsGroup, page == UiPage.Settings);
            SetPageGroupImmediate(serviceGroup, page == UiPage.Service);
            SetPageGroupImmediate(micProtectionGroup, page == UiPage.MicProtection);
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
                UiPage.SongSearch => songSearchGroup,
                UiPage.Queue => queueGroup,
                UiPage.Settings => settingsGroup,
                UiPage.Service => serviceGroup,
                UiPage.MicProtection => micProtectionGroup,
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
            CreateDivider(page, "Header Divider", new Vector2(0f, 190f), new Vector2(ContentWidth, 1f));
            backButton = CreateIconButton(page, "Back", QuestUiIconKind.Back, new Vector2(-468f, 232f), new Vector2(56f, 56f), Surface, TextPrimary, out _);
            CreateText(page, "Page Title", title, 28, FontStyle.Bold, new Vector2(-300f, 232f), new Vector2(250f, 48f), TextAnchor.MiddleLeft, TextPrimary);
        }

        private static void SetChildActive(Transform parent, string childName, bool active)
        {
            var child = parent.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }

        private void CreateSettingToggle(RectTransform parent, string name, string title, string hint, float y, out Toggle toggle)
        {
            CreateText(parent, $"{name} Title", title, 18, FontStyle.Bold, new Vector2(-336f, y + 8f), new Vector2(320f, 34f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(parent, $"{name} Hint", hint, 16, FontStyle.Normal, new Vector2(-256f, y - 20f), new Vector2(480f, 26f), TextAnchor.MiddleLeft, TextSecondary);
            toggle = CreateSwitch(parent, $"{name} Switch", new Vector2(460f, y - 4f));
            CreateDivider(parent, $"{name} Divider", new Vector2(0f, y - 39f), new Vector2(ContentWidth, 1f));
        }

        private static void SetServiceModeButtonVisual(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            if (button.targetGraphic is QuestUiSurface surface)
            {
                surface.color = selected ? Accent : Surface;
            }
            button.colors = CreateButtonColors(selected ? Accent : Surface);

            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = selected ? AccentStrong : Line;
                outline.effectDistance = selected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = selected ? AccentInk : TextPrimary;
            }
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
            CreateText(button.transform, "Label", label, 18, FontStyle.Bold, Vector2.zero, size - new Vector2(20f, 12f), TextAnchor.MiddleCenter, textColor);
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

        private static void ConfigureWholeNumberSlider(Slider slider, float minimum, float maximum)
        {
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = true;
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

        private TMP_InputField CreateInputField(Transform parent, string name, string placeholderValue, Vector2 position, Vector2 size)
        {
            RetireLegacyInputField(parent, name);
            var root = EnsureRect(parent, name, position, size);
            var surface = EnsureSurface(root, Surface, 6f, true);

            // 确保 surface 可以接收 raycast
            surface.raycastTarget = true;

            var text = CreateText(root, "Text", string.Empty, 18, FontStyle.Normal, Vector2.zero, size - new Vector2(32f, 8f), TextAnchor.MiddleLeft, TextPrimary);
            var placeholder = CreateText(root, "Placeholder", placeholderValue, 18, FontStyle.Normal, Vector2.zero, size - new Vector2(32f, 8f), TextAnchor.MiddleLeft, TextSecondary);

            // 确保文本不会拦截 raycast
            text.raycastTarget = false;
            if (placeholder is TMP_Text placeholderText)
            {
                placeholderText.raycastTarget = false;
            }

            var input = GetOrAddComponent<TMP_InputField>(root.gameObject);
            input.targetGraphic = surface;
            input.textViewport = root;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.characterLimit = 80;
            input.caretWidth = 3;
            input.navigation = new Navigation { mode = Navigation.Mode.None };
            QuestAndroidKeyboardInput.Configure(input);
            return input;
        }

        private void CreateDivider(Transform parent, string name, Vector2 position, Vector2 size)
        {
            EnsureSurface(EnsureRect(parent, name, position, size), Line, 0f, false);
        }

        private TextMeshProUGUI CreateText(
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
            RetireLegacyText(parent, name);
            var rect = EnsureRect(parent, name, position, size);
            var text = GetOrAddComponent<TextMeshProUGUI>(rect.gameObject);
            text.font = uiFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style == FontStyle.Bold ? FontStyles.Bold : FontStyles.Normal;
            text.color = color;
            text.alignment = ToTmpAlignment(alignment);
            text.richText = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.extraPadding = true;
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
                var childObject = new GameObject(name, typeof(RectTransform));
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
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
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

        private static void ConfigureHover(Button button, TMP_Text label, string value)
        {
            if (button == null)
            {
                return;
            }

            var feedback = GetOrAddComponent<QuestUiButtonFeedback>(button.gameObject);
            feedback.Configure(label, value);
        }

        private static TMP_FontAsset ResolveUiFont()
        {
            if (sharedUiFont != null)
            {
                return sharedUiFont;
            }

            sharedUiFont = Resources.Load<TMP_FontAsset>("Fonts/NotoSansSC-SDF");
            if (sharedUiFont != null)
            {
                sharedUiFont.isMultiAtlasTexturesEnabled = true;
                return sharedUiFont;
            }

            var sourceFont = Resources.Load<Font>("Fonts/NotoSansSC-VF");
            if (sourceFont == null)
            {
                Debug.LogWarning("[TsukiVox UI] Noto Sans SC source font is missing; TMP will use its default font.");
                return TMP_Settings.defaultFontAsset;
            }

            sharedUiFont = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                96,
                10,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true);
            if (sharedUiFont != null)
            {
                sharedUiFont.name = "TsukiVox Noto Sans SC SDF Runtime";
                sharedUiFont.isMultiAtlasTexturesEnabled = true;
            }

            return sharedUiFont != null ? sharedUiFont : TMP_Settings.defaultFontAsset;
        }

        private static TextAlignmentOptions ToTmpAlignment(TextAnchor alignment)
        {
            return alignment switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
                TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Midline,
            };
        }

        private static void RetireLegacyText(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing == null || existing.GetComponent<Text>() == null)
            {
                return;
            }

            RetireLegacyNode(existing, name);
        }

        private static void RetireLegacyInputField(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing == null || existing.GetComponent<InputField>() == null)
            {
                return;
            }

            RetireLegacyNode(existing, name);
        }

        private static void RetireLegacyNode(Transform legacyNode, string originalName)
        {
            if (Application.isPlaying)
            {
                legacyNode.name = $"{originalName} Legacy";
                legacyNode.gameObject.SetActive(false);
                return;
            }

            DestroyImmediate(legacyNode.gameObject);
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

        private static string FormatClearance(float clearance)
        {
            return $"{Mathf.Max(0f, clearance) * 100f:0.0} cm";
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

        private static string FormatDuration(BilibiliCatalogItem item)
        {
            if (!string.IsNullOrWhiteSpace(item?.durationText))
            {
                return item.durationText.Trim();
            }

            var totalSeconds = Mathf.Max(0, item?.durationSeconds ?? 0);
            return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
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
