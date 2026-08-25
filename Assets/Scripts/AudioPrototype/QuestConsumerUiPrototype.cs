using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    [DisallowMultipleComponent]
    public sealed class QuestConsumerUiPrototype : MonoBehaviour
    {
        private const string RootName = "Consumer UI";
        private const string AppendKtvSearchPrefsKey = "TsukiVox.AppendKtvToSearch";
        private const int QueueRowCount = 6;
        private const int QueueCoverSlotCount = QueueRowCount;
        private const int SearchResultRowCount = 4;
        private const int SuggestRowCount = 9;
        private const int SuggestColumnCount = 3;
        private const float RefreshIntervalSeconds = 0.1f;
        private const float SuggestDebounceSeconds = 0.35f;
        private const float SuggestColumnGap = 12f;
        private const float SuggestRowGap = 6f;
        private const float SuggestRowHeight = 34f;
        private const float ContentWidth = 992f;
        private const int SearchResultColumnCount = 2;
        private const float SearchResultColumnGap = 12f;
        private const float SearchResultRowGap = 12f;
        private const float SearchResultCardHeight = 128f;
        private const float SearchResultCoverWidth = 192f;
        private const float SearchResultCoverRetrySeconds = 30f;
        private const int SearchResultSkeletonElementCount = 7;
        private const float MicWarningClearanceStep = 0.0025f;
        private const float MicCriticalClearanceStep = 0.001f;
        private const float MicMouthOffsetStep = 0.005f;
        private const float QueueDrawerWidth = 650f;
        private const float QueueDrawerContentWidth = 590f;
        private const float QueueRowWidth = 580f;
        private const float EnqueueConfirmationSeconds = 6.5f;
        private const float ExitConfirmationSeconds = 4f;
        private const float DestructiveActionConfirmationSeconds = 4f;
        private const float MaintenanceResultSeconds = 5f;

        private Color ScreenBackground => palette.ScreenBackground;
        private Color Surface => palette.Surface;
        private Color SurfaceRaised => palette.SurfaceRaised;
        private Color SurfaceHover => palette.SurfaceHover;
        private Color Line => palette.Line;
        private Color TextPrimary => palette.TextPrimary;
        private Color TextSecondary => palette.TextSecondary;
        private Color TextFaint => palette.TextFaint;
        private Color Accent => palette.Accent;
        private Color AccentStrong => palette.AccentStrong;
        private Color AccentInk => palette.AccentInk;
        private Color Warm => palette.Warm;
        private Color WarmSurface => palette.WarmSurface;
        private Color Danger => palette.Danger;
        private Color DangerSurface => palette.DangerSurface;
        private Color DangerBorder => palette.DangerBorder;
        private Color BrandBackground => palette.BrandBackground;
        private Color BrandAccent => palette.BrandAccent;
        private Color BrandWarm => palette.BrandWarm;

        private enum ThemeColorRole
        {
            Clear,
            ScreenBackground,
            Surface,
            SurfaceRaised,
            Line,
            TextPrimary,
            TextSecondary,
            TextFaint,
            Accent,
            AccentStrong,
            AccentInk,
            Warm,
            WarmSurface,
            Danger,
            DangerSurface,
            DangerBorder,
            BrandBackground,
            BrandAccent,
            BrandWarm,
            PendingSurface,
            EnabledSurface,
            SafetySurface,
            InactiveMeter,
            SliderTrack,
            DebugScrim,
            DrawerSurface,
            RawDetailsSurface,
            DiagnosticsActionSurface,
        }
        private enum UiPage
        {
            Home,
            SongSearch,
            Voice,
            Settings,
            RoomAmbience,
            StageLighting,
            MicProtection,
            MicMouthPoint,
        }

        private RectTransform panel;
        private RectTransform consumerRoot;
        private RectTransform homePage;
        private RectTransform songSearchPage;
        private RectTransform voicePage;
        private RectTransform settingsPage;
        private RectTransform roomAmbiencePage;
        private RectTransform stageLightingPage;
        private RectTransform micProtectionPage;
        private RectTransform micMouthPointPage;
        private RectTransform queueScrim;
        private RectTransform queueDrawer;
        private RectTransform queueListViewport;
        private RectTransform queueScrollbarRoot;
        private RectTransform enqueueConfirmation;
        private RectTransform enqueueFlyer;
        private RectTransform debugScrim;
        private RectTransform debugDrawer;
        private RectTransform rawDetailsRoot;

        private CanvasGroup homeGroup;
        private CanvasGroup songSearchGroup;
        private CanvasGroup voiceGroup;
        private CanvasGroup settingsGroup;
        private CanvasGroup roomAmbienceGroup;
        private CanvasGroup stageLightingGroup;
        private CanvasGroup micProtectionGroup;
        private CanvasGroup micMouthPointGroup;
        private CanvasGroup queueScrimGroup;
        private CanvasGroup queueDrawerGroup;
        private CanvasGroup enqueueConfirmationGroup;
        private CanvasGroup enqueueFlyerGroup;
        private CanvasGroup debugScrimGroup;
        private CanvasGroup debugDrawerGroup;

        private QuestAudioPrototype audioPrototype;
        private QuestPlaylistPrototype playlistPrototype;
        private QuestVideoScreenPrototype videoScreenPrototype;
        private QuestHandheldPropsPrototype handheldPropsPrototype;
        private QuestAppShellPrototype appShellPrototype;
        private QuestKtvRoomPrototype roomPrototype;
        private QuestStageLightingPrototype stageLightingPrototype;
        private QuestKtvRoomPrototype subscribedRoom;
        private QuestUiThemePalette palette = QuestUiThemePalette.For(RoomTheme.Dark);
        private readonly Dictionary<UnityEngine.Object, ThemeColorRole> themeBindings = new Dictionary<UnityEngine.Object, ThemeColorRole>();
        private QuestPlaylistPrototype subscribedPlaylist;
        private static TMP_FontAsset sharedUiFont;
        private TMP_FontAsset uiFont;

        private RectTransform queueBadge;
        private TMP_Text queueBadgeText;
        private TMP_Text songMetaText;
        private TMP_Text songTitleText;
        private TMP_Text songDetailText;
        private TMP_Text transportHoverText;
        private Slider homeVideoVolumeSlider;
        private Slider homeVoiceVolumeSlider;
        private TMP_Text homeVideoVolumeValueText;
        private TMP_Text homeVoiceVolumeValueText;
        private QuestUiIcon homeVoiceVolumeIcon;
        private QuestUiSurface[] waveBars;

        private Button queueDrawerButton;
        private QuestUiSurface queueDrawerButtonSurface;
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
        private Button spatialSpeakersModeButton;
        private Button lowLatencyModeButton;
        private QuestUiSurface spatialSpeakersModeSurface;
        private QuestUiSurface lowLatencyModeSurface;
        private Slider inputMeterSlider;
        private Slider ambienceSlider;
        private Slider echoSlider;
        private Slider dynamicsSlider;
        private Toggle distanceMonitoringToggle;
        private TMP_Text distanceMonitoringStatusText;
        private TMP_Text voicePresetStatusText;
        private TMP_Text ambienceValueText;
        private TMP_Text echoValueText;
        private TMP_Text dynamicsValueText;
        private TMP_Text voiceMixerFooterText;
        private readonly Button[] presetButtons = new Button[4];
        private readonly QuestUiSurface[] presetSurfaces = new QuestUiSurface[4];

        private Button closeQueueDrawerButton;
        private Button queueScrimButton;
        private TMP_Text queueDrawerCountText;
        private readonly RectTransform[] queueRows = new RectTransform[QueueRowCount];
        private readonly QuestUiSurface[] queueRowSurfaces = new QuestUiSurface[QueueRowCount];
        private readonly CanvasGroup[] queueRowGroups = new CanvasGroup[QueueRowCount];
        private readonly RawImage[] queueCoverImages = new RawImage[QueueCoverSlotCount];
        private readonly string[] queueCoverUrls = new string[QueueCoverSlotCount];
        private readonly Texture2D[] queueCoverTextures = new Texture2D[QueueCoverSlotCount];
        private readonly Coroutine[] queueCoverRequests = new Coroutine[QueueCoverSlotCount];
        private readonly float[] queueCoverRetryAfter = new float[QueueCoverSlotCount];
        private readonly TMP_Text[] queueTitleTexts = new TMP_Text[QueueRowCount];
        private readonly TMP_Text[] queueMetaTexts = new TMP_Text[QueueRowCount];
        private readonly TMP_Text[] queueNumberTexts = new TMP_Text[QueueRowCount];
        private readonly TMP_Text[] queueStateTexts = new TMP_Text[QueueRowCount];
        private readonly Slider[] queueProgressSliders = new Slider[QueueRowCount];
        private readonly TMP_Text[] queueProgressTexts = new TMP_Text[QueueRowCount];
        private readonly Button[] queuePlayButtons = new Button[QueueRowCount];
        private readonly QuestUiIcon[] queuePlayIcons = new QuestUiIcon[QueueRowCount];
        private readonly Button[] queueRemoveButtons = new Button[QueueRowCount];
        private readonly QuestUiIcon[] queueRemoveIcons = new QuestUiIcon[QueueRowCount];
        private QuestQueueSwipeHandler queueSwipeHandler;
        private Scrollbar queueScrollbar;
        private TMP_Text queueEmptyText;
        private TMP_Text queueFooterText;
        private Button clearPlayedButton;
        private Button clearAllQueueButton;
        private readonly string[] queueRowItemIds = new string[QueueRowCount];
        private int queueWindowStartIndex;
        private int lastQueueCurrentIndex = int.MinValue;

        private Button songSearchBackButton;
        private TMP_InputField songSearchInput;
        private TMP_Text appendKtvSearchLabel;
        private Toggle appendKtvSearchToggle;
        private Button songSearchButton;
        private RectTransform suggestContainer;
        private readonly RectTransform[] suggestRows = new RectTransform[SuggestRowCount];
        private readonly Button[] suggestButtons = new Button[SuggestRowCount];
        private readonly TMP_Text[] suggestTexts = new TMP_Text[SuggestRowCount];
        private readonly RectTransform[] searchResultRows = new RectTransform[SearchResultRowCount];
        private readonly QuestUiSurface[] searchResultSurfaces = new QuestUiSurface[SearchResultRowCount];
        private readonly RawImage[] searchResultCoverImages = new RawImage[SearchResultRowCount];
        private readonly TMP_Text[] searchResultTitleTexts = new TMP_Text[SearchResultRowCount];
        private readonly TMP_Text[] searchResultAuthorTexts = new TMP_Text[SearchResultRowCount];
        private readonly TMP_Text[] searchResultDurationTexts = new TMP_Text[SearchResultRowCount];
        private readonly RectTransform[] searchResultDurationBadges = new RectTransform[SearchResultRowCount];
        private readonly RectTransform[] searchResultUploaderBadges = new RectTransform[SearchResultRowCount];
        private readonly Button[] searchResultAddButtons = new Button[SearchResultRowCount];
        private readonly QuestUiIcon[] searchResultAddIcons = new QuestUiIcon[SearchResultRowCount];
        private readonly TMP_Text[] searchResultAddTexts = new TMP_Text[SearchResultRowCount];
        private readonly RectTransform[] searchResultSkeletonRoots = new RectTransform[SearchResultRowCount];
        private readonly QuestUiSurface[,] searchResultSkeletonSurfaces =
            new QuestUiSurface[SearchResultRowCount, SearchResultSkeletonElementCount];
        private readonly string[] searchResultCoverUrls = new string[SearchResultRowCount];
        private readonly Texture2D[] searchResultCoverTextures = new Texture2D[SearchResultRowCount];
        private readonly Coroutine[] searchResultCoverRequests = new Coroutine[SearchResultRowCount];
        private readonly float[] searchResultCoverRetryAfter = new float[SearchResultRowCount];
        private Button searchPreviousPageButton;
        private Button searchNextPageButton;
        private Button clearSearchButton;
        private TMP_Text songSearchStatusText;
        private int songSearchPageNumber = 1;
        private bool appendKtvToSearch;
        private bool searchResultSkeletonVisible;

        private Button settingsBackButton;
        private Button openRoomAmbienceButton;
        private QuestUiSurface openRoomAmbienceSurface;
        private QuestUiIcon openRoomAmbienceIcon;
        private Button openStageLightingButton;
        private QuestUiSurface openStageLightingSurface;
        private QuestUiIcon openStageLightingIcon;
        private Button playBuiltInDefaultButton;
        private QuestUiIcon playBuiltInDefaultIcon;
        private TMP_Text playBuiltInDefaultText;
        private Button stopBuiltInDefaultButton;
        private QuestUiIcon stopBuiltInDefaultIcon;
        private Button openMicProtectionButton;
        private QuestUiSurface openMicProtectionSurface;
        private QuestUiIcon openMicProtectionIcon;
        private QuestUiSurface voicePageSurface;
        private QuestUiIcon voicePageIcon;
        private TMP_Text ceilingStarsLabel;
        private TMP_Text ceilingAuroraLabel;
        private Toggle ceilingStarsToggle;
        private Toggle ceilingAuroraToggle;
        private Button openDiagnosticsButton;
        private TMP_Text settingsBuildText;
        private Button exitApplicationButton;
        private QuestUiIcon exitApplicationIcon;
        private TMP_Text exitApplicationText;

        private Button roomAmbienceBackButton;

        private Button stageLightingBackButton;
        private Toggle stageLightingToggle;
        private TMP_Text stageLightingStatusText;
        private TMP_Text stageLightingPresetStatusText;
        private TMP_Text stageLightingColorNameText;
        private Slider stageLightingIntensitySlider;
        private Slider stageLightingSpeedSlider;
        private Slider stageLightingWidthSlider;
        private Slider stageLightingRangeSlider;
        private TMP_Text stageLightingIntensityValueText;
        private TMP_Text stageLightingSpeedValueText;
        private TMP_Text stageLightingWidthValueText;
        private TMP_Text stageLightingRangeValueText;
        private Toggle stageLightingMotionToggle;
        private Toggle stageLightingPulseToggle;
        private Toggle stageLightingBeamsToggle;
        private readonly Button[] stageLightingPresetButtons = new Button[4];
        private readonly QuestUiSurface[] stageLightingPresetSurfaces = new QuestUiSurface[4];
        private readonly TMP_Text[] stageLightingPresetLabels = new TMP_Text[4];
        private readonly Button[] stageLightingColorButtons = new Button[5];
        private readonly QuestUiSurface[] stageLightingColorSurfaces = new QuestUiSurface[5];
        private readonly Outline[] stageLightingColorOutlines = new Outline[5];

        private Button micProtectionBackButton;
        private Button openMicMouthPointButton;
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

        private Button micMouthPointBackButton;
        private Slider micMouthHorizontalSlider;
        private Slider micMouthDownSlider;
        private Slider micMouthForwardSlider;
        private TMP_Text micMouthHorizontalValueText;
        private TMP_Text micMouthDownValueText;
        private TMP_Text micMouthForwardValueText;
        private TMP_Text micMouthMarkerStatusText;
        private TMP_Text micMouthCoordinateText;
        private Button resetMicMouthPointButton;

        private Button closeDiagnosticsButton;
        private Button debugScrimButton;
        private Button rawDetailsButton;
        private TMP_Text rawDetailsButtonText;
        private TMP_Text diagnosticsHealthText;
        private TMP_Text diagnosticsRequestModeText;
        private TMP_Text diagnosticsVideoText;
        private TMP_Text rawDiagnosticsText;
        private Button clearMediaCacheButton;
        private TMP_Text clearMediaCacheButtonText;
        private QuestUiIcon clearMediaCacheIcon;
        private Button restoreDefaultSettingsButton;
        private TMP_Text restoreDefaultSettingsButtonText;
        private QuestUiIcon restoreDefaultSettingsIcon;
        private Button copyDiagnosticsButton;

        private TMP_Text enqueueConfirmationTitleText;
        private TMP_Text enqueueConfirmationDetailText;
        private Button enqueueConfirmationButton;
        private RawImage enqueueFlyerImage;
        private QuestUiIcon enqueueFlyerFallbackIcon;

        private UiPage currentPage;
        private UiPage stageLightingReturnPage = UiPage.Home;
        private bool isConfigured;
        private bool rawDetailsVisible;
        private bool queueDrawerVisible;
        private float nextRefreshAt;
        private float exitConfirmationExpiresAt;
        private float clearMediaCacheConfirmationExpiresAt;
        private float restoreDefaultSettingsConfirmationExpiresAt;
        private float maintenanceResultExpiresAt;
        private string maintenanceResultMessage = string.Empty;
        private string lastMaintenanceDetails = string.Empty;
        private bool maintenanceResultFailed;
        private Coroutine pageTransition;
        private Coroutine debugDrawerTransition;
        private Coroutine queueDrawerTransition;
        private Coroutine enqueueFeedbackCoroutine;
        private Coroutine enqueueConfirmationCoroutine;
        private Coroutine micCalibrationCoroutine;
        private Coroutine suggestDebounceRoutine;
        private BilibiliCatalogItem pendingEnqueueFeedbackItem;
        private int pendingEnqueueFeedbackIndex = -1;
        private readonly HashSet<string> confirmedSearchItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> pendingQueueRemovalItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void Configure(
            RectTransform targetPanel,
            QuestAudioPrototype audio,
            QuestPlaylistPrototype playlist,
            QuestVideoScreenPrototype video,
            QuestAppShellPrototype appShell,
            QuestKtvRoomPrototype room,
            QuestStageLightingPrototype stageLighting)
        {
            var requiresBuild = consumerRoot == null || targetPanel != panel || consumerRoot.parent != targetPanel;
            if (roomPrototype != room)
            {
                UnsubscribeRoom();
                roomPrototype = room;
            }

            panel = targetPanel;
            audioPrototype = audio;
            playlistPrototype = playlist;
            videoScreenPrototype = video;
            handheldPropsPrototype = FindAnyObjectByType<QuestHandheldPropsPrototype>();
            appShellPrototype = appShell;
            stageLightingPrototype = stageLighting;
            palette = QuestUiThemePalette.For(roomPrototype != null ? roomPrototype.CurrentTheme : RoomTheme.Dark);
            uiFont = ResolveUiFont();
            appendKtvToSearch = PlayerPrefs.GetInt(AppendKtvSearchPrefsKey, 0) != 0;
            videoScreenPrototype?.SetStatusOverlayVisible(false);

            if (panel == null)
            {
                return;
            }

            if (requiresBuild)
            {
                ReleaseSearchResultCovers();
                ReleaseQueueCovers();
                themeBindings.Clear();
                EnsureUiHierarchy();
                ShowPageImmediate(UiPage.Home);
                SetQueueDrawerImmediate(false);
                SetEnqueueConfirmationImmediate(false);
                SetDebugDrawerImmediate(false);
            }
            else
            {
                ApplyThemeToExistingUi();
            }

            WireUi();
            SubscribePlaylist();
            SubscribeRoom();
            ReconcileConfirmedSearchItems(playlistPrototype?.CurrentState);
            RefreshAll();
            isConfigured = true;
        }

        private void OnEnable()
        {
            SubscribePlaylist();
            SubscribeRoom();
            TryCompleteEnqueueFeedback(playlistPrototype);
            if (isConfigured)
            {
                SetMicFaceMouthMarkerForPage(currentPage);
            }
        }

        private void OnDisable()
        {
            if (queueDrawerTransition != null)
            {
                StopCoroutine(queueDrawerTransition);
                queueDrawerTransition = null;
            }
            if (enqueueFeedbackCoroutine != null)
            {
                StopCoroutine(enqueueFeedbackCoroutine);
                enqueueFeedbackCoroutine = null;
            }
            HideEnqueueConfirmation();
            if (enqueueFlyer != null)
            {
                enqueueFlyer.gameObject.SetActive(false);
            }
            if (queueDrawer != null)
            {
                SetQueueDrawerImmediate(false);
            }
            CancelMicFaceCalibration();
            handheldPropsPrototype?.SetMicFaceMouthMarkerVisible(false);
            CancelSuggestionDebounce();
            CancelSearchResultCoverRequests();
            CancelQueueCoverRequests();
            exitConfirmationExpiresAt = 0f;
            clearMediaCacheConfirmationExpiresAt = 0f;
            restoreDefaultSettingsConfirmationExpiresAt = 0f;
            UnsubscribePlaylist();
            UnsubscribeRoom();
        }

        private void OnDestroy()
        {
            ReleaseSearchResultCovers();
            ReleaseQueueCovers();
        }

        private void Update()
        {
            if (!isConfigured)
            {
                return;
            }

            UpdateSearchResultSkeletonAnimation();
            if (Time.unscaledTime < nextRefreshAt)
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
            settingsPage = EnsurePage(consumerRoot, "Settings Page", out settingsGroup);
            roomAmbiencePage = EnsurePage(consumerRoot, "Room Ambience Page", out roomAmbienceGroup);
            stageLightingPage = EnsurePage(consumerRoot, "Stage Lighting Page", out stageLightingGroup);
            micProtectionPage = EnsurePage(consumerRoot, "Mic Protection Page", out micProtectionGroup);
            micMouthPointPage = EnsurePage(consumerRoot, "Mic Mouth Point Page", out micMouthPointGroup);
            SetChildActive(consumerRoot, "Queue Page", false);

            BuildHomePage();
            BuildSongSearchPage();
            BuildVoicePage();
            BuildSettingsPage();
            BuildRoomAmbiencePage();
            BuildStageLightingPage();
            BuildMicProtectionPage();
            BuildMicMouthPointPage();
            BuildQueueDrawer();
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
            CreateText(homePage, "Brand", "TsukiVox", 23, FontStyle.Bold, new Vector2(-392f, 232f), new Vector2(120f, 36f), TextAnchor.MiddleCenter, TextPrimary);
            SetChildActive(homePage, "Brand CN", false);
            SetChildActive(homePage, "Connection Dot", false);
            SetChildActive(homePage, "Connection", false);
            SetChildActive(homePage, "Header Hover Label", false);

            songSearchPageButton = CreateLabeledIconButton(homePage, "Open Song Search", "搜索点歌", QuestUiIconKind.Search, new Vector2(160f, 232f), new Vector2(128f, 52f), Accent, AccentInk, AccentInk, out _);
            settingsPageButton = CreateLabeledIconButton(homePage, "Open Settings", "设置", QuestUiIconKind.Settings, new Vector2(432f, 232f), new Vector2(128f, 52f), Surface, TextPrimary, TextPrimary, out _);
            ConfigureHover(songSearchPageButton, null, string.Empty);
            ConfigureHover(settingsPageButton, null, string.Empty);

            songMetaText = CreateText(homePage, "Song Meta", "播放队列为空", 18, FontStyle.Bold, new Vector2(-270f, 150f), new Vector2(450f, 32f), TextAnchor.MiddleLeft, Accent);
            songTitleText = CreateText(homePage, "Song Title", "等待点歌", 44, FontStyle.Bold, new Vector2(-190f, 103f), new Vector2(610f, 66f), TextAnchor.MiddleLeft, TextPrimary);
            songTitleText.enableAutoSizing = true;
            songTitleText.fontSizeMin = 28f;
            songTitleText.fontSizeMax = 44f;
            songDetailText = CreateText(homePage, "Song Detail", string.Empty, 17, FontStyle.Normal, new Vector2(-250f, 62f), new Vector2(490f, 30f), TextAnchor.MiddleLeft, TextSecondary);

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

            SetChildActive(homePage, "Voice Summary", false);
            EnsureIcon(homePage, "Video Volume Icon", QuestUiIconKind.Volume, new Vector2(-474f, -225f), new Vector2(28f, 28f), Accent);
            CreateText(homePage, "Video Volume Label", "视频", 16, FontStyle.Bold, new Vector2(-424f, -225f), new Vector2(68f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            homeVideoVolumeSlider = CreateSlider(homePage, "Home Video Volume", new Vector2(-260f, -225f), new Vector2(260f, 46f), true, Accent, AccentStrong);
            homeVideoVolumeValueText = CreateText(homePage, "Video Volume Value", "75%", 16, FontStyle.Bold, new Vector2(-88f, -225f), new Vector2(64f, 32f), TextAnchor.MiddleRight, AccentStrong);

            CreateDivider(homePage, "Mixer Channel Divider", new Vector2(0f, -225f), new Vector2(1f, 42f));
            homeVoiceVolumeIcon = EnsureIcon(homePage, "Voice Volume Icon", QuestUiIconKind.Microphone, new Vector2(70f, -225f), new Vector2(28f, 28f), Accent);
            CreateText(homePage, "Voice Volume Label", "人声", 16, FontStyle.Bold, new Vector2(120f, -225f), new Vector2(68f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            homeVoiceVolumeSlider = CreateSlider(homePage, "Home Voice Volume", new Vector2(284f, -225f), new Vector2(260f, 46f), true, Accent, AccentStrong);
            homeVoiceVolumeValueText = CreateText(homePage, "Voice Volume Value", "70%", 16, FontStyle.Bold, new Vector2(456f, -225f), new Vector2(64f, 32f), TextAnchor.MiddleRight, AccentStrong);
            SetChildActive(homePage, "Open Voice Settings", false);
        }

        private void BuildSongSearchPage()
        {
            BuildSubpageHeader(songSearchPage, "搜索点歌", out songSearchBackButton);

            songSearchInput = CreateInputField(
                songSearchPage,
                "Song Search Input",
                "请输入歌曲名，推荐使用全拼",
                new Vector2(-126f, 150f),
                new Vector2(720f, 54f));
            songSearchInput.characterLimit = 80;
            songSearchInput.SetTextWithoutNotify(string.Empty);
            appendKtvSearchLabel = CreateText(
                songSearchPage,
                "Append KTV Label",
                "KTV",
                16,
                FontStyle.Bold,
                new Vector2(266f, 150f),
                new Vector2(56f, 36f),
                TextAnchor.MiddleCenter,
                TextSecondary);
            appendKtvSearchToggle = CreateSwitch(songSearchPage, "Append KTV Switch", new Vector2(326f, 150f));
            SetChildActive(songSearchPage, "Search Suggestions", false);
            songSearchButton = CreateIconButton(
                songSearchPage,
                "Search Songs",
                QuestUiIconKind.Search,
                new Vector2(446f, 150f),
                new Vector2(86f, 54f),
                Accent,
                AccentInk,
                out _);

            // 输入时用紧凑候选网格临时替代结果区，避免与世界空间键盘重叠。
            var suggestRowCount = Mathf.CeilToInt(SuggestRowCount / (float)SuggestColumnCount);
            var suggestContainerHeight = suggestRowCount * SuggestRowHeight +
                                         Mathf.Max(0, suggestRowCount - 1) * SuggestRowGap;
            suggestContainer = EnsureRect(
                songSearchPage,
                "Suggest Container",
                new Vector2(0f, 61f),
                new Vector2(ContentWidth, suggestContainerHeight));
            suggestContainer.gameObject.SetActive(false);

            var suggestColumnWidth = (ContentWidth -
                                      Mathf.Max(0, SuggestColumnCount - 1) * SuggestColumnGap) /
                                     SuggestColumnCount;
            for (var index = 0; index < SuggestRowCount; index += 1)
            {
                var rowIndex = index / SuggestColumnCount;
                var columnIndex = index % SuggestColumnCount;
                var rowX = -ContentWidth * 0.5f + suggestColumnWidth * 0.5f +
                           columnIndex * (suggestColumnWidth + SuggestColumnGap);
                var rowY = (suggestRowCount - 1) * (SuggestRowHeight + SuggestRowGap) * 0.5f -
                           rowIndex * (SuggestRowHeight + SuggestRowGap);
                var row = EnsureRect(
                    suggestContainer,
                    $"Suggest Row {index}",
                    new Vector2(rowX, rowY),
                    new Vector2(suggestColumnWidth, SuggestRowHeight));
                suggestRows[index] = row;
                suggestButtons[index] = CreateSurfaceButton(
                    row,
                    "Button",
                    Vector2.zero,
                    new Vector2(suggestColumnWidth, SuggestRowHeight),
                    Surface,
                    Line);
                suggestTexts[index] = CreateText(
                    suggestButtons[index].transform,
                    "Text",
                    string.Empty,
                    17,
                    FontStyle.Normal,
                    Vector2.zero,
                    new Vector2(suggestColumnWidth - 28f, 30f),
                    TextAnchor.MiddleLeft,
                    TextPrimary);
                suggestTexts[index].enableAutoSizing = true;
                suggestTexts[index].fontSizeMin = 14f;
                suggestTexts[index].fontSizeMax = 17f;
                suggestTexts[index].textWrappingMode = TextWrappingModes.NoWrap;
                suggestTexts[index].overflowMode = TextOverflowModes.Ellipsis;
                ConfigureHover(suggestButtons[index], null, string.Empty);
            }

            var cardWidth = (ContentWidth - SearchResultColumnGap) / SearchResultColumnCount;
            const float firstRowY = 44f;
            for (var index = 0; index < SearchResultRowCount; index += 1)
            {
                var columnIndex = index % SearchResultColumnCount;
                var rowIndex = index / SearchResultColumnCount;
                var cardX = -ContentWidth * 0.5f + cardWidth * 0.5f +
                            columnIndex * (cardWidth + SearchResultColumnGap);
                var cardY = firstRowY - rowIndex * (SearchResultCardHeight + SearchResultRowGap);
                var row = EnsureRect(
                    songSearchPage,
                    $"Search Result {index}",
                    new Vector2(cardX, cardY),
                    new Vector2(cardWidth, SearchResultCardHeight));
                searchResultRows[index] = row;
                searchResultSurfaces[index] = EnsureSurface(row, Surface, 6f, false);
                var rowMask = GetOrAddComponent<Mask>(row.gameObject);
                rowMask.showMaskGraphic = true;

                var coverX = -cardWidth * 0.5f + SearchResultCoverWidth * 0.5f;
                var cover = EnsureRect(
                    row,
                    "Cover",
                    new Vector2(coverX, 0f),
                    new Vector2(SearchResultCoverWidth, SearchResultCardHeight));
                EnsureSurface(cover, SurfaceRaised, 0f, false);
                var coverImageRect = EnsureRect(
                    cover,
                    "Image",
                    Vector2.zero,
                    new Vector2(SearchResultCoverWidth, SearchResultCardHeight));
                var coverImage = GetOrAddComponent<RawImage>(coverImageRect.gameObject);
                coverImage.texture = null;
                coverImage.color = Color.white;
                coverImage.raycastTarget = false;
                coverImage.enabled = false;
                searchResultCoverImages[index] = coverImage;

                var durationRoot = EnsureRect(
                    cover,
                    "Duration",
                    new Vector2(64f, -48f),
                    new Vector2(52f, 22f));
                searchResultDurationBadges[index] = durationRoot;
                var durationSurface = GetOrAddComponent<QuestUiSurface>(durationRoot.gameObject);
                durationSurface.color = new Color(0f, 0f, 0f, 0.82f);
                durationSurface.SetCornerRadius(4f);
                durationSurface.raycastTarget = false;
                searchResultDurationTexts[index] = CreateText(
                    durationRoot,
                    "Text",
                    "0:00",
                    13,
                    FontStyle.Bold,
                    Vector2.zero,
                    new Vector2(48f, 20f),
                    TextAnchor.MiddleCenter,
                    TextPrimary);
                themeBindings.Remove(searchResultDurationTexts[index]);
                searchResultDurationTexts[index].color = Color.white;

                var contentLeft = -cardWidth * 0.5f + SearchResultCoverWidth;
                var contentWidth = cardWidth - SearchResultCoverWidth;
                searchResultTitleTexts[index] = CreateText(
                    row,
                    "Title",
                    "歌曲",
                    17,
                    FontStyle.Bold,
                    new Vector2(contentLeft + contentWidth * 0.5f, 25f),
                    new Vector2(contentWidth - 24f, 68f),
                    TextAnchor.UpperLeft,
                    TextPrimary);
                searchResultTitleTexts[index].overflowMode = TextOverflowModes.Ellipsis;
                searchResultTitleTexts[index].maxVisibleLines = 3;

                var badgeX = contentLeft + 24f;
                var badgeRoot = EnsureRect(
                    row,
                    "Uploader Badge",
                    new Vector2(badgeX, -43f),
                    new Vector2(24f, 16f));
                searchResultUploaderBadges[index] = badgeRoot;
                EnsureSurface(badgeRoot, TextSecondary, 4f, false);
                var badgeInner = EnsureRect(badgeRoot, "Inner", Vector2.zero, new Vector2(22f, 14f));
                EnsureSurface(badgeInner, Surface, 3f, false);
                CreateText(
                    badgeInner,
                    "Text",
                    "UP",
                    9,
                    FontStyle.Bold,
                    Vector2.zero,
                    new Vector2(20f, 12f),
                    TextAnchor.MiddleCenter,
                    TextSecondary);
                var authorLeft = badgeX + 18f;
                var addButtonLeft = cardWidth * 0.5f - 98f;
                var authorWidth = Mathf.Max(40f, addButtonLeft - authorLeft - 8f);
                searchResultAuthorTexts[index] = CreateText(
                    row,
                    "Author",
                    "未知 UP 主",
                    13,
                    FontStyle.Normal,
                    new Vector2(authorLeft + authorWidth * 0.5f, -43f),
                    new Vector2(authorWidth, 22f),
                    TextAnchor.MiddleLeft,
                    TextSecondary);
                searchResultAuthorTexts[index].textWrappingMode = TextWrappingModes.NoWrap;
                searchResultAuthorTexts[index].overflowMode = TextOverflowModes.Ellipsis;
                searchResultAddButtons[index] = CreateSurfaceButton(
                    row,
                    "Add",
                    new Vector2(cardWidth * 0.5f - 54f, -36f),
                    new Vector2(88f, 44f),
                    SurfaceRaised,
                    Line);
                searchResultAddIcons[index] = EnsureIcon(
                    searchResultAddButtons[index].transform,
                    "Icon",
                    QuestUiIconKind.Plus,
                    new Vector2(-24f, 0f),
                    new Vector2(20f, 20f),
                    AccentStrong);
                searchResultAddTexts[index] = CreateText(
                    searchResultAddButtons[index].transform,
                    "Label",
                    "点歌",
                    15,
                    FontStyle.Bold,
                    new Vector2(14f, 0f),
                    new Vector2(48f, 30f),
                    TextAnchor.MiddleCenter,
                    AccentStrong);
                ConfigureHover(searchResultAddButtons[index], null, string.Empty);

                BuildSearchResultSkeleton(row, index, cardWidth, coverX, contentLeft, contentWidth, badgeX);
                SetChildActive(row, "Meta", false);
            }

            searchPreviousPageButton = CreateTextButton(songSearchPage, "Previous Search Page", "上一页", new Vector2(-424f, -200f), new Vector2(128f, 46f), Surface, TextPrimary);
            songSearchStatusText = CreateText(songSearchPage, "Search Status", "请输入关键词搜索歌曲", 16, FontStyle.Normal, new Vector2(0f, -200f), new Vector2(520f, 34f), TextAnchor.MiddleCenter, TextSecondary);
            searchNextPageButton = CreateTextButton(songSearchPage, "Next Search Page", "下一页", new Vector2(424f, -200f), new Vector2(128f, 46f), Surface, TextPrimary);
            clearSearchButton = CreateTextButton(songSearchPage, "Clear Search", "清空", new Vector2(-286f, -200f), new Vector2(104f, 46f), Surface, TextSecondary);
        }

        private void BuildSearchResultSkeleton(
            RectTransform row,
            int resultIndex,
            float cardWidth,
            float coverX,
            float contentLeft,
            float contentWidth,
            float badgeX)
        {
            var root = EnsureRect(row, "Skeleton", Vector2.zero, row.sizeDelta);
            searchResultSkeletonRoots[resultIndex] = root;

            var titleLeft = contentLeft + 12f;
            var titleLine1Width = Mathf.Min(236f, contentWidth - 24f);
            var titleLine2Width = Mathf.Min(198f, contentWidth - 24f);
            var titleLine3Width = Mathf.Min(142f, contentWidth - 24f);
            CreateSearchResultSkeletonElement(
                root,
                resultIndex,
                0,
                "Cover",
                new Vector2(coverX, 0f),
                new Vector2(SearchResultCoverWidth, SearchResultCardHeight),
                0f);
            CreateSearchResultSkeletonElement(
                root,
                resultIndex,
                1,
                "Title 1",
                new Vector2(titleLeft + titleLine1Width * 0.5f, 43f),
                new Vector2(titleLine1Width, 13f),
                5f);
            CreateSearchResultSkeletonElement(
                root,
                resultIndex,
                2,
                "Title 2",
                new Vector2(titleLeft + titleLine2Width * 0.5f, 21f),
                new Vector2(titleLine2Width, 13f),
                5f);
            CreateSearchResultSkeletonElement(
                root,
                resultIndex,
                3,
                "Title 3",
                new Vector2(titleLeft + titleLine3Width * 0.5f, -1f),
                new Vector2(titleLine3Width, 13f),
                5f);
            CreateSearchResultSkeletonElement(
                root,
                resultIndex,
                4,
                "Uploader Badge",
                new Vector2(badgeX, -43f),
                new Vector2(24f, 16f),
                4f);
            CreateSearchResultSkeletonElement(
                root,
                resultIndex,
                5,
                "Author",
                new Vector2(badgeX + 76f, -43f),
                new Vector2(108f, 12f),
                5f);
            CreateSearchResultSkeletonElement(
                root,
                resultIndex,
                6,
                "Add",
                new Vector2(cardWidth * 0.5f - 54f, -36f),
                new Vector2(88f, 44f),
                6f);

            root.gameObject.SetActive(false);
        }

        private void CreateSearchResultSkeletonElement(
            Transform parent,
            int resultIndex,
            int elementIndex,
            string name,
            Vector2 position,
            Vector2 size,
            float cornerRadius)
        {
            var rect = EnsureRect(parent, name, position, size);
            searchResultSkeletonSurfaces[resultIndex, elementIndex] =
                EnsureSurface(rect, SurfaceRaised, cornerRadius, false);
        }

        private void SetSearchResultSkeletonVisible(int index, bool visible)
        {
            var skeletonRoot = searchResultSkeletonRoots[index];
            if (skeletonRoot != null)
            {
                skeletonRoot.gameObject.SetActive(visible);
                if (visible)
                {
                    skeletonRoot.SetAsLastSibling();
                }
            }

            searchResultTitleTexts[index]?.gameObject.SetActive(!visible);
            searchResultAuthorTexts[index]?.gameObject.SetActive(!visible);
            searchResultDurationBadges[index]?.gameObject.SetActive(!visible);
            searchResultUploaderBadges[index]?.gameObject.SetActive(!visible);
            searchResultAddButtons[index]?.gameObject.SetActive(!visible);
        }

        private void UpdateSearchResultSkeletonAnimation()
        {
            if (!searchResultSkeletonVisible)
            {
                return;
            }

            var animationTime = Time.unscaledTime * 3.4f;
            for (var resultIndex = 0; resultIndex < SearchResultRowCount; resultIndex += 1)
            {
                for (var elementIndex = 0;
                     elementIndex < SearchResultSkeletonElementCount;
                     elementIndex += 1)
                {
                    var surface = searchResultSkeletonSurfaces[resultIndex, elementIndex];
                    if (surface == null)
                    {
                        continue;
                    }

                    var phase = animationTime - resultIndex * 0.62f - elementIndex * 0.18f;
                    var wave = 0.5f + 0.5f * Mathf.Sin(phase);
                    surface.color = Color.Lerp(SurfaceRaised, Line, 0.2f + wave * 0.62f);
                }
            }
        }

        private void BuildVoicePage()
        {
            BuildSubpageHeader(voicePage, "人声", out voiceBackButton);
            RetireLegacyVoiceMixerUi();

            CreateText(voicePage, "Microphone Title", "实时返听", 21, FontStyle.Bold, new Vector2(-396f, 135f), new Vector2(200f, 36f), TextAnchor.MiddleLeft, TextPrimary);
            distanceMonitoringStatusText = CreateText(voicePage, "Distance Status", "等待右手麦克风", 15, FontStyle.Bold, new Vector2(-286f, 105f), new Vector2(420f, 26f), TextAnchor.MiddleLeft, TextSecondary);
            inputMeterSlider = CreateSlider(voicePage, "Input Meter", new Vector2(108f, 124f), new Vector2(430f, 46f), false);
            voiceMicrophoneToggle = CreateSwitch(voicePage, "Microphone Switch", new Vector2(460f, 124f));

            CreateDivider(voicePage, "Microphone Divider", new Vector2(0f, 88f), new Vector2(ContentWidth, 1f));
            CreateText(voicePage, "Monitor Mode Title", "返听方式", 16, FontStyle.Bold, new Vector2(-416f, 61f), new Vector2(130f, 30f), TextAnchor.MiddleLeft, TextPrimary);
            spatialSpeakersModeButton = CreateTextButton(
                voicePage,
                "Spatial Speakers Mode",
                "空间音箱",
                new Vector2(-138f, 61f),
                new Vector2(300f, 44f),
                Surface,
                TextPrimary);
            lowLatencyModeButton = CreateTextButton(
                voicePage,
                "Low Latency Mode",
                "低延迟",
                new Vector2(184f, 61f),
                new Vector2(300f, 44f),
                Surface,
                TextPrimary);
            spatialSpeakersModeButton.transition = Selectable.Transition.None;
            lowLatencyModeButton.transition = Selectable.Transition.None;
            spatialSpeakersModeSurface = spatialSpeakersModeButton.targetGraphic as QuestUiSurface;
            lowLatencyModeSurface = lowLatencyModeButton.targetGraphic as QuestUiSurface;

            CreateText(voicePage, "Preset Title", "人声预设", 18, FontStyle.Bold, new Vector2(-414f, 15f), new Vector2(160f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            voicePresetStatusText = CreateText(voicePage, "Preset Status", "KTV · 预设值", 14, FontStyle.Normal, new Vector2(-244f, 15f), new Vector2(220f, 28f), TextAnchor.MiddleLeft, TextSecondary);
            CreateText(voicePage, "Distance Link Label", "距离跟随", 16, FontStyle.Bold, new Vector2(350f, 15f), new Vector2(130f, 30f), TextAnchor.MiddleRight, TextPrimary);
            distanceMonitoringToggle = CreateSwitch(voicePage, "Distance Monitoring Switch", new Vector2(460f, 15f));

            var labels = new[] { "原声", "KTV", "强效", "柔和" };
            for (var index = 0; index < presetButtons.Length; index += 1)
            {
                presetButtons[index] = CreateTextButton(voicePage, $"Preset {index}", labels[index], new Vector2(-369f + index * 246f, -27f), new Vector2(232f, 46f), Surface, TextPrimary);
                presetButtons[index].transition = Selectable.Transition.None;
                presetSurfaces[index] = presetButtons[index].targetGraphic as QuestUiSurface;
            }

            CreateDivider(voicePage, "Preset Divider", new Vector2(0f, -55f), new Vector2(ContentWidth, 1f));

            CreateText(voicePage, "Ambience Title", "空间感", 17, FontStyle.Bold, new Vector2(-408f, -92f), new Vector2(170f, 30f), TextAnchor.MiddleLeft, TextPrimary);
            ambienceSlider = CreateSlider(voicePage, "Ambience", new Vector2(86f, -92f), new Vector2(520f, 42f), true);
            ambienceValueText = CreateText(voicePage, "Ambience Value", "55%", 16, FontStyle.Bold, new Vector2(430f, -92f), new Vector2(94f, 30f), TextAnchor.MiddleRight, AccentStrong);

            CreateText(voicePage, "Echo Title", "回声", 17, FontStyle.Bold, new Vector2(-408f, -146f), new Vector2(170f, 30f), TextAnchor.MiddleLeft, TextPrimary);
            echoSlider = CreateSlider(voicePage, "Echo", new Vector2(86f, -146f), new Vector2(520f, 42f), true);
            echoValueText = CreateText(voicePage, "Echo Value", "30%", 16, FontStyle.Bold, new Vector2(430f, -146f), new Vector2(94f, 30f), TextAnchor.MiddleRight, AccentStrong);

            CreateText(voicePage, "Dynamics Title", "人声稳定", 17, FontStyle.Bold, new Vector2(-408f, -200f), new Vector2(170f, 30f), TextAnchor.MiddleLeft, TextPrimary);
            dynamicsSlider = CreateSlider(voicePage, "Dynamics", new Vector2(86f, -200f), new Vector2(520f, 42f), true);
            dynamicsValueText = CreateText(voicePage, "Dynamics Value", "65%", 16, FontStyle.Bold, new Vector2(430f, -200f), new Vector2(94f, 30f), TextAnchor.MiddleRight, AccentStrong);

            voiceMixerFooterText = CreateText(voicePage, "Mixer Footer", "距离跟随只调整返听增益", 14, FontStyle.Normal, new Vector2(0f, -245f), new Vector2(ContentWidth, 28f), TextAnchor.MiddleCenter, TextSecondary);
        }

        private void RetireLegacyVoiceMixerUi()
        {
            SetChildActive(voicePage, "Microphone Hint", false);
            SetChildActive(voicePage, "Volume Hint", false);
            SetChildActive(voicePage, "Volume Icon", false);
            SetChildActive(voicePage, "Volume Divider", false);
            SetChildActive(voicePage, "Volume Title", false);
            SetChildActive(voicePage, "Monitor Volume", false);
            SetChildActive(voicePage, "Monitor Volume Value", false);
            SetChildActive(voicePage, "Preset Hint", false);
            SetChildActive(voicePage, "Safety Note", false);
        }

        private void RetireSettingsAudioAdvancedUi()
        {
            SetChildActive(settingsPage, "Audio Section", false);
            var settingNames = new[] { "Monitor Output", "Safety Limiter", "Native Backend" };
            var elementNames = new[] { "Title", "Hint", "Switch", "Divider" };
            foreach (var settingName in settingNames)
            {
                foreach (var elementName in elementNames)
                {
                    SetChildActive(settingsPage, $"{settingName} {elementName}", false);
                }
            }
        }

        private void BuildQueueDrawer()
        {
            queueDrawerButton = CreateLabeledIconButton(
                consumerRoot,
                "Open Queue Drawer",
                "播放队列",
                QuestUiIconKind.Queue,
                new Vector2(296f, 232f),
                new Vector2(128f, 52f),
                Surface,
                TextPrimary,
                TextPrimary,
                out _);
            queueDrawerButtonSurface = queueDrawerButton.targetGraphic as QuestUiSurface;
            ConfigureHover(queueDrawerButton, null, string.Empty);

            queueBadge = EnsureRect(queueDrawerButton.transform, "Badge", new Vector2(52f, 20f), new Vector2(20f, 20f));
            EnsureSurface(queueBadge, Accent, 12f, false);
            queueBadgeText = CreateText(queueBadge, "Label", "0", 14, FontStyle.Bold, Vector2.zero, queueBadge.sizeDelta, TextAnchor.MiddleCenter, AccentInk);

            enqueueConfirmation = EnsureRect(
                consumerRoot,
                "Enqueue Confirmation",
                new Vector2(118f, 232f),
                new Vector2(500f, 52f));
            EnsureSurface(enqueueConfirmation, palette.PendingSurface, 7f, false);
            var confirmationOutline = GetOrAddComponent<Outline>(enqueueConfirmation.gameObject);
            confirmationOutline.effectColor = Accent;
            confirmationOutline.effectDistance = new Vector2(1f, -1f);
            confirmationOutline.useGraphicAlpha = false;
            BindTheme(confirmationOutline, ThemeColorRole.Accent);
            enqueueConfirmationGroup = GetOrAddComponent<CanvasGroup>(enqueueConfirmation.gameObject);
            EnsureSurface(
                EnsureRect(enqueueConfirmation, "Check Background", new Vector2(-224f, 0f), new Vector2(30f, 30f)),
                Accent,
                15f,
                false);
            EnsureIcon(
                enqueueConfirmation,
                "Check",
                QuestUiIconKind.Check,
                new Vector2(-224f, 0f),
                new Vector2(18f, 18f),
                AccentInk);
            enqueueConfirmationTitleText = CreateText(
                enqueueConfirmation,
                "Title",
                "已加入播放队列",
                15,
                FontStyle.Bold,
                new Vector2(-55f, 9f),
                new Vector2(292f, 24f),
                TextAnchor.MiddleLeft,
                TextPrimary);
            enqueueConfirmationTitleText.enableAutoSizing = true;
            enqueueConfirmationTitleText.fontSizeMin = 12f;
            enqueueConfirmationTitleText.fontSizeMax = 15f;
            enqueueConfirmationTitleText.textWrappingMode = TextWrappingModes.NoWrap;
            enqueueConfirmationTitleText.overflowMode = TextOverflowModes.Ellipsis;
            enqueueConfirmationDetailText = CreateText(
                enqueueConfirmation,
                "Detail",
                "已加入播放队列",
                12,
                FontStyle.Normal,
                new Vector2(-55f, -11f),
                new Vector2(292f, 20f),
                TextAnchor.MiddleLeft,
                TextSecondary);
            enqueueConfirmationButton = CreateTextButton(
                enqueueConfirmation,
                "View Queue",
                "查看队列",
                new Vector2(192f, 0f),
                new Vector2(104f, 40f),
                Accent,
                AccentInk);
            enqueueConfirmationButton.GetComponentInChildren<TMP_Text>(true).fontSize = 14f;

            enqueueFlyer = EnsureRect(consumerRoot, "Enqueue Flyer", Vector2.zero, new Vector2(150f, 72f));
            EnsureSurface(enqueueFlyer, palette.PendingSurface, 6f, false);
            enqueueFlyerGroup = GetOrAddComponent<CanvasGroup>(enqueueFlyer.gameObject);
            enqueueFlyerGroup.alpha = 0f;
            enqueueFlyerGroup.interactable = false;
            enqueueFlyerGroup.blocksRaycasts = false;
            var flyerImageRect = EnsureRect(enqueueFlyer, "Image", Vector2.zero, enqueueFlyer.sizeDelta);
            enqueueFlyerImage = GetOrAddComponent<RawImage>(flyerImageRect.gameObject);
            enqueueFlyerImage.raycastTarget = false;
            enqueueFlyerImage.enabled = false;
            enqueueFlyerFallbackIcon = EnsureIcon(
                enqueueFlyer,
                "Fallback",
                QuestUiIconKind.Queue,
                Vector2.zero,
                new Vector2(34f, 34f),
                AccentStrong);
            enqueueFlyer.gameObject.SetActive(false);

            queueScrim = EnsureRect(consumerRoot, "Queue Scrim", Vector2.zero, QuestAppShellPrototype.ControlPanelSize);
            var scrimSurface = EnsureSurface(queueScrim, palette.DebugScrim, 0f, true);
            queueScrimButton = GetOrAddComponent<Button>(queueScrim.gameObject);
            queueScrimButton.targetGraphic = scrimSurface;
            queueScrimButton.transition = Selectable.Transition.None;
            queueScrimGroup = GetOrAddComponent<CanvasGroup>(queueScrim.gameObject);

            var visibleDrawerX = (QuestAppShellPrototype.ControlPanelSize.x - QueueDrawerWidth) * 0.5f;
            queueDrawer = EnsureRect(
                consumerRoot,
                "Queue Drawer",
                new Vector2(visibleDrawerX, 0f),
                new Vector2(QueueDrawerWidth, QuestAppShellPrototype.ControlPanelSize.y));
            EnsureSurface(queueDrawer, palette.DrawerSurface, 0f, true);
            GetOrAddComponent<RectMask2D>(queueDrawer.gameObject).padding = Vector4.zero;
            queueDrawerGroup = GetOrAddComponent<CanvasGroup>(queueDrawer.gameObject);
            SetChildActive(queueDrawer, "Current Item", false);

            CreateDivider(queueDrawer, "Header Divider", new Vector2(0f, 216f), new Vector2(QueueDrawerContentWidth, 1f));
            CreateText(queueDrawer, "Title", "播放队列", 26, FontStyle.Bold, new Vector2(-170f, 248f), new Vector2(250f, 46f), TextAnchor.MiddleLeft, TextPrimary);
            queueDrawerCountText = CreateText(queueDrawer, "Count", "0 首", 15, FontStyle.Normal, new Vector2(-6f, 248f), new Vector2(80f, 36f), TextAnchor.MiddleLeft, TextSecondary);
            closeQueueDrawerButton = CreateIconButton(
                queueDrawer,
                "Close",
                QuestUiIconKind.Close,
                new Vector2(285f, 248f),
                new Vector2(46f, 46f),
                Surface,
                TextPrimary,
                out _);

            queueListViewport = EnsureRect(
                queueDrawer,
                "Queue List Viewport",
                new Vector2(-5f, 10f),
                new Vector2(QueueRowWidth, 408f));
            EnsureSurface(queueListViewport, Color.clear, 0f, true);
            GetOrAddComponent<RectMask2D>(queueListViewport.gameObject).padding = Vector4.zero;
            queueSwipeHandler = GetOrAddComponent<QuestQueueSwipeHandler>(queueListViewport.gameObject);
            queueSwipeHandler.Configure(ScrollQueueByRows);

            const float firstY = 170f;
            for (var index = 0; index < QueueRowCount; index += 1)
            {
                var rowName = $"Queue Row {index}";
                var existingRow = queueDrawer.Find(rowName);
                if (existingRow != null && existingRow.parent != queueListViewport)
                {
                    existingRow.SetParent(queueListViewport, false);
                }

                var row = EnsureRect(
                    queueListViewport,
                    rowName,
                    new Vector2(0f, firstY - index * 64f),
                    new Vector2(QueueRowWidth, 62f));
                queueRows[index] = row;
                queueRowSurfaces[index] = EnsureSurface(row, Color.clear, 6f, false);
                queueRowGroups[index] = GetOrAddComponent<CanvasGroup>(row.gameObject);
                CreateDivider(row, "Divider", new Vector2(0f, -31f), new Vector2(QueueRowWidth, 1f));
                queueNumberTexts[index] = CreateText(row, "Number", (index + 1).ToString(), 13, FontStyle.Normal, new Vector2(-278f, 0f), new Vector2(20f, 30f), TextAnchor.MiddleCenter, TextFaint);
                CreateQueueCover(row, "Cover", index, new Vector2(-244f, 0f), new Vector2(46f, 44f));
                queueTitleTexts[index] = CreateText(row, "Title", "歌曲", 15, FontStyle.Bold, new Vector2(-62f, 12f), new Vector2(310f, 25f), TextAnchor.MiddleLeft, TextPrimary);
                queueTitleTexts[index].textWrappingMode = TextWrappingModes.NoWrap;
                queueTitleTexts[index].overflowMode = TextOverflowModes.Ellipsis;
                queueMetaTexts[index] = CreateText(row, "Meta", "等待", 12, FontStyle.Normal, new Vector2(-80f, -9f), new Vector2(240f, 19f), TextAnchor.MiddleLeft, TextSecondary);
                queueMetaTexts[index].textWrappingMode = TextWrappingModes.NoWrap;
                queueMetaTexts[index].overflowMode = TextOverflowModes.Ellipsis;
                queueProgressSliders[index] = CreateSlider(
                    row,
                    "Preparation Progress",
                    new Vector2(-82f, -23f),
                    new Vector2(270f, 10f),
                    false,
                    trackThickness: 4f);
                queueProgressTexts[index] = CreateText(
                    row,
                    "Preparation Percent",
                    "0%",
                    11,
                    FontStyle.Bold,
                    new Vector2(87f, -22f),
                    new Vector2(54f, 18f),
                    TextAnchor.MiddleRight,
                    AccentStrong);
                queueStateTexts[index] = CreateText(row, "State", "播放中", 12, FontStyle.Bold, new Vector2(170f, 0f), new Vector2(70f, 28f), TextAnchor.MiddleRight, TextSecondary);
                queueStateTexts[index].gameObject.SetActive(false);
                queuePlayButtons[index] = CreateIconButton(
                    row,
                    "Play",
                    QuestUiIconKind.Play,
                    new Vector2(223f, 0f),
                    new Vector2(34f, 34f),
                    SurfaceRaised,
                    AccentStrong,
                    out queuePlayIcons[index]);
                ConfigureHover(queuePlayButtons[index], null, string.Empty);
                queueRemoveButtons[index] = CreateIconButton(
                    row,
                    "Remove",
                    QuestUiIconKind.Trash,
                    new Vector2(264f, 0f),
                    new Vector2(34f, 34f),
                    SurfaceRaised,
                    Danger,
                    out queueRemoveIcons[index]);
                ConfigureHover(queueRemoveButtons[index], null, string.Empty);
            }

            queueScrollbarRoot = EnsureRect(queueDrawer, "Queue Scrollbar", new Vector2(306f, 2f), new Vector2(14f, 382f));
            EnsureSurface(queueScrollbarRoot, Color.clear, 7f, true);
            EnsureSurface(
                EnsureRect(queueScrollbarRoot, "Track", Vector2.zero, new Vector2(8f, 374f)),
                Line,
                4f,
                false);
            var slidingArea = EnsureRect(queueScrollbarRoot, "Sliding Area", Vector2.zero, new Vector2(8f, 360f));
            var handle = EnsureRect(slidingArea, "Handle", Vector2.zero, new Vector2(8f, 90f));
            var handleSurface = EnsureSurface(handle, TextFaint, 4f, false);
            queueScrollbar = GetOrAddComponent<Scrollbar>(queueScrollbarRoot.gameObject);
            queueScrollbar.targetGraphic = handleSurface;
            queueScrollbar.handleRect = handle;
            queueScrollbar.direction = Scrollbar.Direction.BottomToTop;
            queueScrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            queueScrollbar.numberOfSteps = 0;
            queueScrollbar.SetValueWithoutNotify(1f);
            queueScrollbar.size = 1f;
            queueScrollbarRoot.gameObject.SetActive(false);

            queueEmptyText = CreateText(
                queueDrawer,
                "Empty Queue",
                "待播队列为空",
                15,
                FontStyle.Normal,
                new Vector2(0f, -3f),
                new Vector2(550f, 40f),
                TextAnchor.MiddleCenter,
                TextSecondary);
            CreateDivider(queueDrawer, "Footer Divider", new Vector2(0f, -194f), new Vector2(QueueDrawerContentWidth, 1f));
            queueFooterText = CreateText(queueDrawer, "Queue Footer", "还没有点播歌曲", 13, FontStyle.Normal, new Vector2(-170f, -232f), new Vector2(300f, 32f), TextAnchor.MiddleLeft, TextSecondary);
            SetChildActive(queueDrawer, "Clear Queue", false);
            clearPlayedButton = CreateTextButton(queueDrawer, "Clear Played", "清空已播", new Vector2(140f, -232f), new Vector2(96f, 38f), Surface, TextPrimary);
            clearPlayedButton.GetComponentInChildren<TMP_Text>(true).fontSize = 13f;
            ConfigureHover(clearPlayedButton, null, string.Empty);
            clearAllQueueButton = CreateTextButton(queueDrawer, "Clear Except Current", "清空全部", new Vector2(244f, -232f), new Vector2(96f, 38f), Surface, TextPrimary);
            clearAllQueueButton.GetComponentInChildren<TMP_Text>(true).fontSize = 13f;
            ConfigureHover(clearAllQueueButton, null, string.Empty);

            queueScrim.SetAsLastSibling();
            queueDrawer.SetAsLastSibling();
        }

        private RawImage CreateQueueCover(
            Transform parent,
            string name,
            int slot,
            Vector2 position,
            Vector2 size)
        {
            var root = EnsureRect(parent, name, position, size);
            EnsureSurface(root, SurfaceRaised, 5f, false);
            EnsureIcon(root, "Fallback", QuestUiIconKind.Queue, Vector2.zero, size * 0.42f, TextFaint);
            var imageRect = EnsureRect(root, "Image", Vector2.zero, size);
            var image = GetOrAddComponent<RawImage>(imageRect.gameObject);
            image.raycastTarget = false;
            image.enabled = false;
            queueCoverImages[slot] = image;
            return image;
        }

        private void BuildSettingsPage()
        {
            SetChildActive(settingsPage, "Request Mode Label", false);
            SetChildActive(settingsPage, "Request Mode Value", false);
            SetChildActive(settingsPage, "Request Mode Divider", false);
            SetChildActive(settingsPage, "Room Ambience Label", false);
            SetChildActive(settingsPage, "Ceiling Stars Label", false);
            SetChildActive(settingsPage, "Ceiling Stars Switch", false);
            SetChildActive(settingsPage, "Ceiling Aurora Label", false);
            SetChildActive(settingsPage, "Ceiling Aurora Switch", false);

            BuildSubpageHeader(settingsPage, "设置", out settingsBackButton);
            playBuiltInDefaultButton = CreateSurfaceButton(settingsPage, "Play Built-in Default", new Vector2(386f, 232f), new Vector2(196f, 52f), Surface, Line);
            playBuiltInDefaultIcon = EnsureIcon(playBuiltInDefaultButton.transform, "Icon", QuestUiIconKind.Play, new Vector2(-72f, 0f), new Vector2(24f, 24f), Accent);
            playBuiltInDefaultText = CreateText(playBuiltInDefaultButton.transform, "Label", "播放内置视频", 16, FontStyle.Bold, new Vector2(18f, 0f), new Vector2(124f, 34f), TextAnchor.MiddleCenter, TextPrimary);
            stopBuiltInDefaultButton = CreateIconButton(settingsPage, "Stop Built-in Default", QuestUiIconKind.Stop, new Vector2(250f, 232f), new Vector2(56f, 52f), Surface, TextSecondary, out stopBuiltInDefaultIcon);

            const float settingsRowHeight = 76f;
            const float settingsRowX = 0f;
            const float roomAmbienceRowY = 136f;
            const float stageLightingRowY = 60f;
            const float voiceRowY = -16f;
            const float micProtectionRowY = -92f;
            openRoomAmbienceButton = CreateSettingsNavigationRow(
                settingsPage,
                "Open Room Ambience",
                "房间氛围",
                QuestUiIconKind.Moon,
                new Vector2(settingsRowX, roomAmbienceRowY),
                settingsRowHeight,
                false,
                out openRoomAmbienceSurface,
                out openRoomAmbienceIcon);
            openStageLightingButton = CreateSettingsNavigationRow(
                settingsPage,
                "Open Stage Lighting",
                "舞台灯光",
                QuestUiIconKind.Spotlight,
                new Vector2(settingsRowX, stageLightingRowY),
                settingsRowHeight,
                true,
                out openStageLightingSurface,
                out openStageLightingIcon);
            voicePageButton = CreateSettingsNavigationRow(
                settingsPage,
                "Open Voice Settings",
                "人声设置",
                QuestUiIconKind.SlidersHorizontal,
                new Vector2(settingsRowX, voiceRowY),
                settingsRowHeight,
                true,
                out voicePageSurface,
                out voicePageIcon);
            openMicProtectionButton = CreateSettingsNavigationRow(
                settingsPage,
                "Open Mic Protection",
                "防碰撞",
                QuestUiIconKind.Microphone,
                new Vector2(settingsRowX, micProtectionRowY),
                settingsRowHeight,
                true,
                out openMicProtectionSurface,
                out openMicProtectionIcon);

            CreateDivider(settingsPage, "Settings Rows Divider", new Vector2(0f, -130f), new Vector2(ContentWidth, 1f));
            RetireSettingsAudioAdvancedUi();
            SetChildActive(settingsPage, "Audio Divider", false);
            openDiagnosticsButton = CreateSurfaceButton(settingsPage, "Open Diagnostics", new Vector2(-116f, -228f), new Vector2(760f, 72f), Surface, Line);
            CreateText(openDiagnosticsButton.transform, "Title", "诊断与支持", 19, FontStyle.Bold, new Vector2(-230f, 11f), new Vector2(260f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            settingsBuildText = CreateText(openDiagnosticsButton.transform, "Hint", QuestBuildInfo.SettingsSummary, 15, FontStyle.Normal, new Vector2(-65f, -17f), new Vector2(520f, 26f), TextAnchor.MiddleLeft, TextSecondary);
            EnsureIcon(openDiagnosticsButton.transform, "Chevron", QuestUiIconKind.ChevronRight, new Vector2(340f, 0f), new Vector2(24f, 24f), TextSecondary);

            exitApplicationButton = CreateSurfaceButton(settingsPage, "Exit Application", new Vector2(388f, -228f), new Vector2(216f, 72f), DangerSurface, DangerBorder);
            exitApplicationButton.transition = Selectable.Transition.None;
            exitApplicationIcon = EnsureIcon(exitApplicationButton.transform, "Icon", QuestUiIconKind.Power, new Vector2(-72f, 0f), new Vector2(24f, 24f), DangerBorder);
            exitApplicationText = CreateText(exitApplicationButton.transform, "Label", "退出应用", 17, FontStyle.Bold, new Vector2(30f, 0f), new Vector2(132f, 34f), TextAnchor.MiddleCenter, DangerBorder);
        }

        private Button CreateSettingsNavigationRow(
            RectTransform parent,
            string name,
            string label,
            QuestUiIconKind iconKind,
            Vector2 position,
            float rowHeight,
            bool showTopDivider,
            out QuestUiSurface surface,
            out QuestUiIcon icon)
        {
            var button = CreateSurfaceButton(parent, name, position, new Vector2(ContentWidth, rowHeight), Color.clear, Color.clear);
            button.gameObject.SetActive(true);
            surface = button.targetGraphic as QuestUiSurface;
            surface.color = Color.clear;
            button.transition = Selectable.Transition.None;
            var outline = button.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }

            SetChildActive(button.transform, "Icon", false);
            SetChildActive(button.transform, "Label", false);
            icon = EnsureIcon(button.transform, "Icon", iconKind, new Vector2(-452f, 0f), new Vector2(28f, 28f), TextPrimary);
            icon.gameObject.SetActive(false);
            var chevron = EnsureIcon(button.transform, "Chevron", QuestUiIconKind.ChevronRight, new Vector2(460f, 0f), new Vector2(24f, 24f), TextSecondary);
            chevron.gameObject.SetActive(true);
            const float textLeft = -486f;
            const float textRight = 420f;
            var textWidth = textRight - textLeft;
            var textX = (textLeft + textRight) * 0.5f;
            var title = CreateText(button.transform, "Title", label, 19, FontStyle.Bold, new Vector2(textX, 15f), new Vector2(textWidth, 28f), TextAnchor.MiddleLeft, TextPrimary);
            title.characterSpacing = 1f;
            var descriptions = label switch
            {
                "房间氛围" => "星空、极光与包厢主题",
                "舞台灯光" => "灯组、颜色与动态预设",
                "人声设置" => "返听、预设与空间效果",
                "防碰撞" => "靠近嘴部时提供触觉反馈",
                _ => string.Empty,
            };
            var description = CreateText(button.transform, "Description", descriptions, 14, FontStyle.Normal, new Vector2(textX, -15f), new Vector2(textWidth, 22f), TextAnchor.MiddleLeft, TextSecondary);
            description.characterSpacing = 0.5f;
            CreateDivider(button.transform, "Divider", new Vector2(0f, rowHeight * 0.5f), new Vector2(ContentWidth, 1f));
            SetChildActive(button.transform, "Divider", showTopDivider);

            var feedback = button.GetComponent<QuestUiButtonFeedback>();
            if (feedback != null)
            {
                feedback.enabled = false;
            }
            return button;
        }

        private void BuildRoomAmbiencePage()
        {
            BuildSubpageHeader(roomAmbiencePage, "房间氛围", out roomAmbienceBackButton);

            CreateText(roomAmbiencePage, "Room Ambience Label", "房间氛围", 18, FontStyle.Bold, new Vector2(-394f, 132f), new Vector2(200f, 34f), TextAnchor.MiddleLeft, TextPrimary);
            ceilingStarsLabel = CreateText(roomAmbiencePage, "Ceiling Stars Label", "星空", 17, FontStyle.Bold, new Vector2(92f, 132f), new Vector2(80f, 34f), TextAnchor.MiddleCenter, TextSecondary);
            ceilingStarsToggle = CreateSwitch(roomAmbiencePage, "Ceiling Stars Switch", new Vector2(188f, 132f));
            ceilingAuroraLabel = CreateText(roomAmbiencePage, "Ceiling Aurora Label", "极光", 17, FontStyle.Bold, new Vector2(314f, 132f), new Vector2(80f, 34f), TextAnchor.MiddleCenter, TextSecondary);
            ceilingAuroraToggle = CreateSwitch(roomAmbiencePage, "Ceiling Aurora Switch", new Vector2(422f, 132f));
            CreateDivider(roomAmbiencePage, "Ambience Divider", new Vector2(0f, 82f), new Vector2(ContentWidth, 1f));
        }

        private void BuildStageLightingPage()
        {
            BuildSubpageHeader(stageLightingPage, "舞台灯光", out stageLightingBackButton);

            EnsureIcon(stageLightingPage, "Status Icon", QuestUiIconKind.Spotlight, new Vector2(-456f, 151f), new Vector2(30f, 30f), AccentStrong);
            CreateText(stageLightingPage, "Enable Title", "Livehouse 灯组", 21, FontStyle.Bold, new Vector2(-319f, 151f), new Vector2(230f, 36f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(stageLightingPage, "Enable Hint", "6 台灯具 · 4 盏动态射灯", 15, FontStyle.Normal, new Vector2(-269f, 122f), new Vector2(330f, 26f), TextAnchor.MiddleLeft, TextSecondary);
            stageLightingStatusText = CreateText(stageLightingPage, "Status", "开启", 17, FontStyle.Bold, new Vector2(330f, 140f), new Vector2(140f, 34f), TextAnchor.MiddleRight, AccentStrong);
            stageLightingToggle = CreateSwitch(stageLightingPage, "Stage Lighting Switch", new Vector2(460f, 140f));
            CreateDivider(stageLightingPage, "Enable Divider", new Vector2(0f, 96f), new Vector2(ContentWidth, 1f));

            CreateText(stageLightingPage, "Preset Label", "场景", 17, FontStyle.Bold, new Vector2(-438f, 59f), new Vector2(100f, 30f), TextAnchor.MiddleLeft, TextSecondary);
            var presetNames = new[] { "氛围", "LIVE", "极光", "高潮" };
            for (var index = 0; index < stageLightingPresetButtons.Length; index += 1)
            {
                var x = -250f + index * 154f;
                var button = CreateSurfaceButton(stageLightingPage, $"Lighting Preset {index}", new Vector2(x, 59f), new Vector2(142f, 42f), Surface, Line);
                stageLightingPresetButtons[index] = button;
                stageLightingPresetSurfaces[index] = button.targetGraphic as QuestUiSurface;
                stageLightingPresetLabels[index] = CreateText(button.transform, "Label", presetNames[index], 16, FontStyle.Bold, Vector2.zero, new Vector2(118f, 30f), TextAnchor.MiddleCenter, TextPrimary);
            }
            stageLightingPresetStatusText = CreateText(stageLightingPage, "Preset Status", "LIVE", 15, FontStyle.Bold, new Vector2(426f, 59f), new Vector2(120f, 30f), TextAnchor.MiddleRight, AccentStrong);

            CreateText(stageLightingPage, "Color Label", "颜色", 17, FontStyle.Bold, new Vector2(-438f, 7f), new Vector2(100f, 30f), TextAnchor.MiddleLeft, TextSecondary);
            var colorNames = new[] { "海洋", "霓虹", "日落", "冰白", "光谱" };
            for (var index = 0; index < stageLightingColorButtons.Length; index += 1)
            {
                var look = (StageLightingColorLook)index;
                var swatchColor = QuestStageLightingPrototype.GetColorLookSwatch(look);
                var button = CreateSurfaceButton(stageLightingPage, $"Lighting Color {index}", new Vector2(-250f + index * 74f, 7f), new Vector2(54f, 38f), swatchColor, Line);
                var surface = button.targetGraphic as QuestUiSurface;
                stageLightingColorButtons[index] = button;
                stageLightingColorSurfaces[index] = surface;
                stageLightingColorOutlines[index] = button.GetComponent<Outline>();
                themeBindings.Remove(surface);
                if (look == StageLightingColorLook.Spectrum)
                {
                    EnsureIcon(button.transform, "Spectrum", QuestUiIconKind.Sparkles, Vector2.zero, new Vector2(22f, 22f), AccentInk);
                }
            }
            stageLightingColorNameText = CreateText(stageLightingPage, "Color Name", "霓虹", 16, FontStyle.Bold, new Vector2(348f, 7f), new Vector2(220f, 30f), TextAnchor.MiddleRight, TextPrimary);
            for (var index = 0; index < stageLightingColorButtons.Length; index += 1)
            {
                ConfigureHover(stageLightingColorButtons[index], stageLightingColorNameText, colorNames[index]);
            }
            CreateDivider(stageLightingPage, "Color Divider", new Vector2(0f, -30f), new Vector2(ContentWidth, 1f));

            const float parameterColumnSpacing = 500f;
            const float parameterLabelX = -432f;
            const float parameterSliderX = -252f;
            const float parameterValueX = -80f;
            var parameterSliderSize = new Vector2(250f, 42f);
            var parameterValueSize = new Vector2(72f, 30f);

            CreateText(stageLightingPage, "Intensity Label", "强度", 16, FontStyle.Bold, new Vector2(parameterLabelX, -70f), new Vector2(88f, 30f), TextAnchor.MiddleLeft, TextPrimary);
            stageLightingIntensitySlider = CreateSlider(stageLightingPage, "Lighting Intensity", new Vector2(parameterSliderX, -70f), parameterSliderSize, true, Accent, AccentStrong);
            stageLightingIntensityValueText = CreateText(stageLightingPage, "Intensity Value", "72%", 16, FontStyle.Bold, new Vector2(parameterValueX, -70f), parameterValueSize, TextAnchor.MiddleRight, AccentStrong);

            CreateText(stageLightingPage, "Speed Label", "速度", 16, FontStyle.Bold, new Vector2(parameterLabelX + parameterColumnSpacing, -70f), new Vector2(88f, 30f), TextAnchor.MiddleLeft, TextPrimary);
            stageLightingSpeedSlider = CreateSlider(stageLightingPage, "Lighting Speed", new Vector2(parameterSliderX + parameterColumnSpacing, -70f), parameterSliderSize, true, Accent, AccentStrong);
            stageLightingSpeedValueText = CreateText(stageLightingPage, "Speed Value", "52%", 16, FontStyle.Bold, new Vector2(parameterValueX + parameterColumnSpacing, -70f), parameterValueSize, TextAnchor.MiddleRight, AccentStrong);

            CreateText(stageLightingPage, "Width Label", "光束", 16, FontStyle.Bold, new Vector2(parameterLabelX, -132f), new Vector2(88f, 30f), TextAnchor.MiddleLeft, TextPrimary);
            stageLightingWidthSlider = CreateSlider(stageLightingPage, "Lighting Width", new Vector2(parameterSliderX, -132f), parameterSliderSize, true, Accent, AccentStrong);
            stageLightingWidthValueText = CreateText(stageLightingPage, "Width Value", "34°", 16, FontStyle.Bold, new Vector2(parameterValueX, -132f), parameterValueSize, TextAnchor.MiddleRight, AccentStrong);

            CreateText(stageLightingPage, "Range Label", "幅度", 16, FontStyle.Bold, new Vector2(parameterLabelX + parameterColumnSpacing, -132f), new Vector2(88f, 30f), TextAnchor.MiddleLeft, TextPrimary);
            stageLightingRangeSlider = CreateSlider(stageLightingPage, "Lighting Range", new Vector2(parameterSliderX + parameterColumnSpacing, -132f), parameterSliderSize, true, Accent, AccentStrong);
            stageLightingRangeValueText = CreateText(stageLightingPage, "Range Value", "62%", 16, FontStyle.Bold, new Vector2(parameterValueX + parameterColumnSpacing, -132f), parameterValueSize, TextAnchor.MiddleRight, AccentStrong);

            CreateDivider(stageLightingPage, "Control Divider", new Vector2(0f, -178f), new Vector2(ContentWidth, 1f));
            const float toggleColumnSpacing = 336f;
            const float toggleLabelX = -390f;
            const float toggleSwitchX = -252f;
            var toggleLabelSize = new Vector2(132f, 32f);
            CreateText(stageLightingPage, "Motion Label", "自动扫动", 16, FontStyle.Bold, new Vector2(toggleLabelX, -226f), toggleLabelSize, TextAnchor.MiddleLeft, TextPrimary);
            stageLightingMotionToggle = CreateSwitch(stageLightingPage, "Lighting Motion Switch", new Vector2(toggleSwitchX, -226f));
            CreateText(stageLightingPage, "Pulse Label", "节拍脉冲", 16, FontStyle.Bold, new Vector2(toggleLabelX + toggleColumnSpacing, -226f), toggleLabelSize, TextAnchor.MiddleLeft, TextPrimary);
            stageLightingPulseToggle = CreateSwitch(stageLightingPage, "Lighting Pulse Switch", new Vector2(toggleSwitchX + toggleColumnSpacing, -226f));
            CreateText(stageLightingPage, "Beams Label", "烟雾光束", 16, FontStyle.Bold, new Vector2(toggleLabelX + toggleColumnSpacing * 2f, -226f), toggleLabelSize, TextAnchor.MiddleLeft, TextPrimary);
            stageLightingBeamsToggle = CreateSwitch(stageLightingPage, "Lighting Beams Switch", new Vector2(toggleSwitchX + toggleColumnSpacing * 2f, -226f));
        }

        private void BuildMicProtectionPage()
        {
            BuildSubpageHeader(micProtectionPage, "麦克风防碰撞", out micProtectionBackButton);
            openMicMouthPointButton = CreateSurfaceButton(micProtectionPage, "Open Mouth Point", new Vector2(406f, 232f), new Vector2(188f, 52f), Surface, Line);
            EnsureIcon(openMicMouthPointButton.transform, "Icon", QuestUiIconKind.Crosshair, new Vector2(-62f, 0f), new Vector2(24f, 24f), Accent);
            CreateText(openMicMouthPointButton.transform, "Label", "定位点", 17, FontStyle.Bold, new Vector2(22f, 0f), new Vector2(112f, 34f), TextAnchor.MiddleCenter, TextPrimary);

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
            micWarningDistanceValueText = CreateText(micProtectionPage, "Warning Value", "3.0 cm", 18, FontStyle.Bold, new Vector2(334f, 14f), new Vector2(92f, 34f), TextAnchor.MiddleRight, AccentStrong);
            captureWarningDistanceButton = CreateTextButton(micProtectionPage, "Capture Warning", "捕获", new Vector2(454f, 14f), new Vector2(104f, 46f), Surface, TextPrimary);
            CreateDivider(micProtectionPage, "Warning Divider", new Vector2(0f, -28f), new Vector2(ContentWidth, 1f));

            CreateText(micProtectionPage, "Critical Title", "强震起点", 18, FontStyle.Bold, new Vector2(-402f, -56f), new Vector2(170f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(micProtectionPage, "Critical Hint", "必须小于轻震起点", 14, FontStyle.Normal, new Vector2(-345f, -82f), new Vector2(280f, 24f), TextAnchor.MiddleLeft, TextSecondary);
            micCriticalDistanceSlider = CreateSlider(micProtectionPage, "Critical Distance", new Vector2(70f, -64f), new Vector2(430f, 48f), true);
            ConfigureWholeNumberSlider(micCriticalDistanceSlider, 0f, 40f);
            micCriticalDistanceValueText = CreateText(micProtectionPage, "Critical Value", "0.8 cm", 18, FontStyle.Bold, new Vector2(334f, -64f), new Vector2(92f, 34f), TextAnchor.MiddleRight, Warm);
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

        private void BuildMicMouthPointPage()
        {
            BuildSubpageHeader(micMouthPointPage, "嘴部定位点", out micMouthPointBackButton);

            EnsureIcon(micMouthPointPage, "Marker Icon", QuestUiIconKind.Crosshair, new Vector2(-458f, 142f), new Vector2(36f, 36f), AccentStrong);
            CreateText(micMouthPointPage, "Marker Title", "空间定位标记", 21, FontStyle.Bold, new Vector2(-300f, 150f), new Vector2(260f, 36f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(micMouthPointPage, "Marker Hint", "基准：头显双眼中心", 16, FontStyle.Normal, new Vector2(-270f, 120f), new Vector2(450f, 26f), TextAnchor.MiddleLeft, TextSecondary);
            micMouthMarkerStatusText = CreateText(micMouthPointPage, "Marker Status", "空间标记已显示", 16, FontStyle.Bold, new Vector2(330f, 138f), new Vector2(310f, 32f), TextAnchor.MiddleRight, AccentStrong);
            CreateDivider(micMouthPointPage, "Marker Divider", new Vector2(0f, 94f), new Vector2(ContentWidth, 1f));

            CreateText(micMouthPointPage, "Horizontal Title", "左右位置", 18, FontStyle.Bold, new Vector2(-398f, 58f), new Vector2(180f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(micMouthPointPage, "Horizontal Hint", "相对头显中心线", 14, FontStyle.Normal, new Vector2(-360f, 32f), new Vector2(260f, 24f), TextAnchor.MiddleLeft, TextSecondary);
            micMouthHorizontalSlider = CreateSlider(micMouthPointPage, "Horizontal Offset", new Vector2(60f, 50f), new Vector2(480f, 48f), true);
            ConfigureWholeNumberSlider(
                micMouthHorizontalSlider,
                QuestHandheldPropsPrototype.MinimumMouthOffsetX / MicMouthOffsetStep,
                QuestHandheldPropsPrototype.MaximumMouthOffsetX / MicMouthOffsetStep);
            micMouthHorizontalValueText = CreateText(micMouthPointPage, "Horizontal Value", "居中", 18, FontStyle.Bold, new Vector2(420f, 50f), new Vector2(132f, 34f), TextAnchor.MiddleRight, AccentStrong);
            CreateDivider(micMouthPointPage, "Horizontal Divider", new Vector2(0f, 12f), new Vector2(ContentWidth, 1f));

            CreateText(micMouthPointPage, "Down Title", "向下位置", 18, FontStyle.Bold, new Vector2(-398f, -24f), new Vector2(180f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(micMouthPointPage, "Down Hint", "从双眼中心向下", 14, FontStyle.Normal, new Vector2(-360f, -50f), new Vector2(260f, 24f), TextAnchor.MiddleLeft, TextSecondary);
            micMouthDownSlider = CreateSlider(micMouthPointPage, "Down Offset", new Vector2(60f, -32f), new Vector2(480f, 48f), true);
            ConfigureWholeNumberSlider(
                micMouthDownSlider,
                -QuestHandheldPropsPrototype.MaximumMouthOffsetY / MicMouthOffsetStep,
                -QuestHandheldPropsPrototype.MinimumMouthOffsetY / MicMouthOffsetStep);
            micMouthDownValueText = CreateText(micMouthPointPage, "Down Value", "11.0 cm", 18, FontStyle.Bold, new Vector2(420f, -32f), new Vector2(132f, 34f), TextAnchor.MiddleRight, AccentStrong);
            CreateDivider(micMouthPointPage, "Down Divider", new Vector2(0f, -70f), new Vector2(ContentWidth, 1f));

            CreateText(micMouthPointPage, "Forward Title", "向前位置", 18, FontStyle.Bold, new Vector2(-398f, -106f), new Vector2(180f, 32f), TextAnchor.MiddleLeft, TextPrimary);
            CreateText(micMouthPointPage, "Forward Hint", "从双眼平面向前", 14, FontStyle.Normal, new Vector2(-360f, -132f), new Vector2(260f, 24f), TextAnchor.MiddleLeft, TextSecondary);
            micMouthForwardSlider = CreateSlider(micMouthPointPage, "Forward Offset", new Vector2(60f, -114f), new Vector2(480f, 48f), true);
            ConfigureWholeNumberSlider(
                micMouthForwardSlider,
                QuestHandheldPropsPrototype.MinimumMouthOffsetZ / MicMouthOffsetStep,
                QuestHandheldPropsPrototype.MaximumMouthOffsetZ / MicMouthOffsetStep);
            micMouthForwardValueText = CreateText(micMouthPointPage, "Forward Value", "2.0 cm", 18, FontStyle.Bold, new Vector2(420f, -114f), new Vector2(132f, 34f), TextAnchor.MiddleRight, AccentStrong);
            CreateDivider(micMouthPointPage, "Forward Divider", new Vector2(0f, -158f), new Vector2(ContentWidth, 1f));

            micMouthCoordinateText = CreateText(micMouthPointPage, "Coordinates", "HMD 局部坐标  X 0.0 · Y -11.0 · Z +2.0 cm", 16, FontStyle.Normal, new Vector2(-190f, -218f), new Vector2(600f, 40f), TextAnchor.MiddleLeft, TextSecondary);
            resetMicMouthPointButton = CreateTextButton(micMouthPointPage, "Reset Mouth Point", "恢复默认位置", new Vector2(390f, -218f), new Vector2(216f, 52f), Surface, TextPrimary);
        }

        private void BuildDebugDrawer()
        {
            debugScrim = EnsureRect(consumerRoot, "Debug Scrim", Vector2.zero, QuestAppShellPrototype.ControlPanelSize);
            var scrimSurface = EnsureSurface(debugScrim, palette.DebugScrim, 0f, true);
            debugScrimButton = GetOrAddComponent<Button>(debugScrim.gameObject);
            debugScrimButton.targetGraphic = scrimSurface;
            debugScrimButton.transition = Selectable.Transition.None;
            debugScrimGroup = GetOrAddComponent<CanvasGroup>(debugScrim.gameObject);

            debugDrawer = EnsureRect(consumerRoot, "Debug Drawer", new Vector2(250f, 0f), new Vector2(620f, 560f));
            EnsureSurface(debugDrawer, palette.DrawerSurface, 0f, true);
            debugDrawerGroup = GetOrAddComponent<CanvasGroup>(debugDrawer.gameObject);
            CreateDivider(debugDrawer, "Header Divider", new Vector2(0f, 216f), new Vector2(572f, 1f));
            CreateText(debugDrawer, "Title", "诊断与支持", 26, FontStyle.Bold, new Vector2(-130f, 248f), new Vector2(310f, 46f), TextAnchor.MiddleLeft, TextPrimary);
            closeDiagnosticsButton = CreateIconButton(debugDrawer, "Close", QuestUiIconKind.Close, new Vector2(250f, 250f), new Vector2(52f, 52f), Surface, TextPrimary, out _);

            diagnosticsHealthText = CreateText(debugDrawer, "Health", "应用正常 · 音频正常 · 视频正常", 18, FontStyle.Bold, new Vector2(0f, 174f), new Vector2(548f, 52f), TextAnchor.MiddleLeft, TextPrimary);
            CreateDivider(debugDrawer, "Health Divider", new Vector2(0f, 138f), new Vector2(548f, 1f));
            diagnosticsRequestModeText = CreateText(debugDrawer, "Request Mode", "点歌方式：直接请求", 16, FontStyle.Normal, new Vector2(0f, 104f), new Vector2(548f, 44f), TextAnchor.MiddleLeft, TextSecondary);
            CreateDivider(debugDrawer, "Request Mode Divider", new Vector2(0f, 70f), new Vector2(548f, 1f));

            SetChildActive(debugDrawer, "Video Debug Label", false);
            SetChildActive(debugDrawer, "Video Debug Switch", false);
            diagnosticsVideoText = CreateText(debugDrawer, "Video Status", "视频状态：待机", 16, FontStyle.Normal, new Vector2(0f, 34f), new Vector2(548f, 62f), TextAnchor.MiddleLeft, TextSecondary);
            CreateDivider(debugDrawer, "Video Divider", new Vector2(0f, -3f), new Vector2(548f, 1f));

            rawDetailsButton = CreateSurfaceButton(debugDrawer, "Raw Details Toggle", new Vector2(0f, -36f), new Vector2(548f, 44f), Color.clear, Color.clear);
            rawDetailsButtonText = CreateText(rawDetailsButton.transform, "Title", "展开原始详情", 17, FontStyle.Bold, new Vector2(-112f, 0f), new Vector2(310f, 36f), TextAnchor.MiddleLeft, TextPrimary);
            EnsureIcon(rawDetailsButton.transform, "Chevron", QuestUiIconKind.ChevronRight, new Vector2(250f, 0f), new Vector2(20f, 20f), TextSecondary);

            rawDetailsRoot = EnsureRect(debugDrawer, "Raw Details", new Vector2(0f, -113f), new Vector2(548f, 100f));
            EnsureSurface(rawDetailsRoot, palette.RawDetailsSurface, 6f, false);
            rawDiagnosticsText = CreateText(rawDetailsRoot, "Text", string.Empty, 12, FontStyle.Normal, Vector2.zero, new Vector2(510f, 82f), TextAnchor.UpperLeft, TextSecondary);
            rawDiagnosticsText.textWrappingMode = TextWrappingModes.Normal;
            rawDiagnosticsText.overflowMode = TextOverflowModes.Truncate;

            clearMediaCacheButton = CreateSurfaceButton(debugDrawer, "Clear Media Cache", new Vector2(-141f, -190f), new Vector2(266f, 44f), Surface, Line);
            clearMediaCacheIcon = EnsureIcon(clearMediaCacheButton.transform, "Icon", QuestUiIconKind.Trash, new Vector2(-103f, 0f), new Vector2(20f, 20f), Danger);
            clearMediaCacheButtonText = CreateText(clearMediaCacheButton.transform, "Label", "清除媒体缓存", 15, FontStyle.Bold, new Vector2(16f, 0f), new Vector2(210f, 32f), TextAnchor.MiddleCenter, Danger);

            restoreDefaultSettingsButton = CreateSurfaceButton(debugDrawer, "Restore Default Settings", new Vector2(141f, -190f), new Vector2(266f, 44f), Surface, Line);
            restoreDefaultSettingsIcon = EnsureIcon(restoreDefaultSettingsButton.transform, "Icon", QuestUiIconKind.Replay, new Vector2(-103f, 0f), new Vector2(20f, 20f), Warm);
            restoreDefaultSettingsButtonText = CreateText(restoreDefaultSettingsButton.transform, "Label", "恢复全部默认设置", 15, FontStyle.Bold, new Vector2(16f, 0f), new Vector2(210f, 32f), TextAnchor.MiddleCenter, TextPrimary);

            copyDiagnosticsButton = CreateTextButton(debugDrawer, "Copy Complete Diagnostics", "复制完整诊断信息", new Vector2(0f, -244f), new Vector2(548f, 40f), palette.DiagnosticsActionSurface, AccentStrong);
            copyDiagnosticsButton.GetComponentInChildren<TMP_Text>(true).fontSize = 16f;
            EnsureIcon(copyDiagnosticsButton.transform, "Icon", QuestUiIconKind.Copy, new Vector2(-160f, 0f), new Vector2(20f, 20f), AccentStrong);

            debugScrim.SetAsLastSibling();
            debugDrawer.SetAsLastSibling();
        }

        private void WireUi()
        {
            WireButton(songSearchPageButton, OpenSongSearchPage);
            WireButton(queueDrawerButton, ToggleQueueDrawer);
            WireButton(closeQueueDrawerButton, () => SetQueueDrawerVisible(false));
            WireButton(queueScrimButton, () => SetQueueDrawerVisible(false));
            WireButton(enqueueConfirmationButton, () => SetQueueDrawerVisible(true));
            WireButton(settingsPageButton, () => ShowPage(UiPage.Settings));
            WireButton(voicePageButton, () => ShowPage(UiPage.Voice));
            WireButton(voiceBackButton, () => ShowPage(UiPage.Settings));
            WireButton(songSearchBackButton, () => ShowPage(UiPage.Home));
            WireButton(settingsBackButton, () => ShowPage(UiPage.Home));
            WireButton(roomAmbienceBackButton, () => ShowPage(UiPage.Settings));
            WireButton(openRoomAmbienceButton, () => ShowPage(UiPage.RoomAmbience));
            WireButton(openStageLightingButton, () => OpenStageLightingPage(UiPage.Settings));
            WireButton(stageLightingBackButton, () => ShowPage(stageLightingReturnPage));
            WireButton(playBuiltInDefaultButton, () => videoScreenPrototype?.ToggleBuiltInDefaultPlayback());
            WireButton(stopBuiltInDefaultButton, () => videoScreenPrototype?.StopBuiltInDefault());
            WireButton(openMicProtectionButton, () => ShowPage(UiPage.MicProtection));
            WireButton(micProtectionBackButton, () => ShowPage(UiPage.Settings));
            WireButton(openMicMouthPointButton, () => ShowPage(UiPage.MicMouthPoint));
            WireButton(micMouthPointBackButton, () => ShowPage(UiPage.MicProtection));

            WireButton(replayButton, () => playlistPrototype?.SendReplay());
            WireButton(previousButton, () => playlistPrototype?.SendPrevious());
            WireButton(playPauseButton, () => playlistPrototype?.SendPlayPause());
            WireButton(nextButton, () => playlistPrototype?.SendNext());
            WireButton(microphoneButton, () => audioPrototype?.ToggleMonitoring());
            WireButton(spatialSpeakersModeButton, () =>
            {
                audioPrototype?.SetMonitorMode(MonitorMode.UnitySpatialSpeakers);
                RefreshVoice();
            });
            WireButton(lowLatencyModeButton, () =>
            {
                audioPrototype?.SetMonitorMode(MonitorMode.OboeLowLatency);
                RefreshVoice();
            });

            homeVideoVolumeSlider.onValueChanged.RemoveAllListeners();
            homeVideoVolumeSlider.onValueChanged.AddListener(value =>
            {
                videoScreenPrototype?.SetVideoVolume(value);
                RefreshHome();
            });
            homeVoiceVolumeSlider.onValueChanged.RemoveAllListeners();
            homeVoiceVolumeSlider.onValueChanged.AddListener(value =>
            {
                audioPrototype?.SetMonitorVolume(value);
                RefreshHome();
            });

            WireButton(songSearchButton, () => SearchSongs(1));
            appendKtvSearchToggle.onValueChanged.RemoveAllListeners();
            appendKtvSearchToggle.SetIsOnWithoutNotify(appendKtvToSearch);
            appendKtvSearchToggle.onValueChanged.AddListener(HandleAppendKtvSearchToggle);
            RefreshAppendKtvSearchToggle();
            songSearchInput.onSubmit.RemoveAllListeners();
            songSearchInput.onSubmit.AddListener(HandleSongSearchSubmitted);
            songSearchInput.onSelect.RemoveAllListeners();
            songSearchInput.onValueChanged.RemoveAllListeners();
            songSearchInput.onValueChanged.AddListener(HandleSongSearchInputChanged);
            WireButton(searchPreviousPageButton, () => SearchSongs(Mathf.Max(1, songSearchPageNumber - 1)));
            WireButton(searchNextPageButton, () => SearchSongs(songSearchPageNumber + 1));
            WireButton(clearSearchButton, ClearSearchResults);
            WireButton(clearPlayedButton, ClearPlayedQueue);
            WireButton(clearAllQueueButton, ClearQueueExceptCurrent);
            for (var index = 0; index < QueueRowCount; index += 1)
            {
                var rowIndex = index;
                WireButton(queuePlayButtons[index], () => PlayQueueRow(rowIndex));
                WireButton(queueRemoveButtons[index], () => RemoveQueueRow(rowIndex));
            }
            queueScrollbar.onValueChanged.RemoveAllListeners();
            queueScrollbar.onValueChanged.AddListener(HandleQueueScrollbarChanged);
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
            ambienceSlider.onValueChanged.RemoveAllListeners();
            ambienceSlider.onValueChanged.AddListener(value =>
            {
                audioPrototype?.SetAmbienceAmount(value);
                RefreshVoice();
            });
            echoSlider.onValueChanged.RemoveAllListeners();
            echoSlider.onValueChanged.AddListener(value =>
            {
                audioPrototype?.SetEchoAmount(value);
                RefreshVoice();
            });
            dynamicsSlider.onValueChanged.RemoveAllListeners();
            dynamicsSlider.onValueChanged.AddListener(value =>
            {
                audioPrototype?.SetDynamicsAmount(value);
                RefreshVoice();
            });
            distanceMonitoringToggle.onValueChanged.RemoveAllListeners();
            distanceMonitoringToggle.onValueChanged.AddListener(value =>
            {
                audioPrototype?.SetDistanceMonitoringEnabled(value);
                RefreshVoice();
            });
            for (var index = 0; index < presetButtons.Length; index += 1)
            {
                var presetIndex = index;
                WireButton(presetButtons[index], () =>
                {
                    audioPrototype?.SelectPreset(presetIndex);
                    RefreshVoice();
                });
            }

            ceilingStarsToggle.onValueChanged.RemoveAllListeners();
            ceilingStarsToggle.onValueChanged.AddListener(value =>
            {
                roomPrototype?.SetStarsEnabled(value);
                RefreshRoomAmbience();
                RefreshSettings();
            });
            ceilingAuroraToggle.onValueChanged.RemoveAllListeners();
            ceilingAuroraToggle.onValueChanged.AddListener(value =>
            {
                roomPrototype?.SetAuroraEnabled(value);
                RefreshRoomAmbience();
                RefreshSettings();
            });

            stageLightingToggle.onValueChanged.RemoveAllListeners();
            stageLightingToggle.onValueChanged.AddListener(value =>
            {
                stageLightingPrototype?.SetLightingEnabled(value);
                RefreshStageLighting();
            });
            for (var index = 0; index < stageLightingPresetButtons.Length; index += 1)
            {
                var preset = (StageLightingPreset)index;
                WireButton(stageLightingPresetButtons[index], () =>
                {
                    stageLightingPrototype?.ApplyPreset(preset);
                    RefreshStageLighting();
                });
            }
            for (var index = 0; index < stageLightingColorButtons.Length; index += 1)
            {
                var look = (StageLightingColorLook)index;
                WireButton(stageLightingColorButtons[index], () =>
                {
                    stageLightingPrototype?.SetColorLook(look);
                    RefreshStageLighting();
                });
            }
            stageLightingIntensitySlider.onValueChanged.RemoveAllListeners();
            stageLightingIntensitySlider.onValueChanged.AddListener(value => stageLightingPrototype?.SetIntensity(value));
            stageLightingSpeedSlider.onValueChanged.RemoveAllListeners();
            stageLightingSpeedSlider.onValueChanged.AddListener(value => stageLightingPrototype?.SetMovementSpeed(value));
            stageLightingWidthSlider.onValueChanged.RemoveAllListeners();
            stageLightingWidthSlider.onValueChanged.AddListener(value => stageLightingPrototype?.SetBeamWidth(value));
            stageLightingRangeSlider.onValueChanged.RemoveAllListeners();
            stageLightingRangeSlider.onValueChanged.AddListener(value => stageLightingPrototype?.SetMotionRange(value));
            stageLightingMotionToggle.onValueChanged.RemoveAllListeners();
            stageLightingMotionToggle.onValueChanged.AddListener(value => stageLightingPrototype?.SetAutomaticMotion(value));
            stageLightingPulseToggle.onValueChanged.RemoveAllListeners();
            stageLightingPulseToggle.onValueChanged.AddListener(value => stageLightingPrototype?.SetBeatPulse(value));
            stageLightingBeamsToggle.onValueChanged.RemoveAllListeners();
            stageLightingBeamsToggle.onValueChanged.AddListener(value => stageLightingPrototype?.SetBeamsVisible(value));

            micProtectionToggle.onValueChanged.RemoveAllListeners();
            micProtectionToggle.onValueChanged.AddListener(value => handheldPropsPrototype?.SetMicFaceHapticsEnabled(value));
            micWarningDistanceSlider.onValueChanged.RemoveAllListeners();
            micWarningDistanceSlider.onValueChanged.AddListener(value => handheldPropsPrototype?.SetMicFaceWarningClearance(value * MicWarningClearanceStep));
            micCriticalDistanceSlider.onValueChanged.RemoveAllListeners();
            micCriticalDistanceSlider.onValueChanged.AddListener(value => handheldPropsPrototype?.SetMicFaceCriticalClearance(value * MicCriticalClearanceStep));
            micHapticStrengthSlider.onValueChanged.RemoveAllListeners();
            micHapticStrengthSlider.onValueChanged.AddListener(value => handheldPropsPrototype?.SetMicFaceHapticStrength(value * 0.1f));
            WireButton(captureWarningDistanceButton, () => StartMicFaceCalibration(captureWarning: true));
            WireButton(captureCriticalDistanceButton, () => StartMicFaceCalibration(captureWarning: false));
            WireButton(resetMicProtectionButton, ResetMicProtectionPreferences);
            micMouthHorizontalSlider.onValueChanged.RemoveAllListeners();
            micMouthHorizontalSlider.onValueChanged.AddListener(SetMicMouthHorizontalOffset);
            micMouthDownSlider.onValueChanged.RemoveAllListeners();
            micMouthDownSlider.onValueChanged.AddListener(SetMicMouthDownOffset);
            micMouthForwardSlider.onValueChanged.RemoveAllListeners();
            micMouthForwardSlider.onValueChanged.AddListener(SetMicMouthForwardOffset);
            WireButton(resetMicMouthPointButton, ResetMicMouthPoint);

            WireButton(openDiagnosticsButton, () =>
            {
                SetQueueDrawerVisible(false);
                SetDebugDrawerVisible(true);
            });
            WireButton(exitApplicationButton, HandleExitApplication);
            WireButton(closeDiagnosticsButton, () => SetDebugDrawerVisible(false));
            WireButton(debugScrimButton, () => SetDebugDrawerVisible(false));
            WireButton(rawDetailsButton, ToggleRawDetails);
            WireButton(clearMediaCacheButton, HandleClearMediaCache);
            WireButton(restoreDefaultSettingsButton, HandleRestoreDefaultSettings);
            WireButton(copyDiagnosticsButton, () => appShellPrototype?.CopyCompleteDebugInfoToClipboard());

            var switchVisuals = consumerRoot.GetComponentsInChildren<QuestUiSwitchVisual>(true);
            for (var index = 0; index < switchVisuals.Length; index += 1)
            {
                switchVisuals[index].RebindToggleListener();
            }
        }

        private void RefreshAll()
        {
            RefreshHome();
            RefreshSongSearch();
            RefreshSuggestions();
            RefreshVoice();
            RefreshQueue();
            RefreshSettings();
            RefreshRoomAmbience();
            RefreshStageLighting();
            RefreshMicProtection();
            RefreshMicMouthPoint();
            RefreshDiagnostics();
        }

        private void RefreshHome()
        {
            var hasVideoAudio = videoScreenPrototype != null;
            homeVideoVolumeSlider.interactable = hasVideoAudio;
            homeVideoVolumeSlider.SetValueWithoutNotify(videoScreenPrototype?.VideoVolume ?? 0f);
            homeVideoVolumeValueText.text = hasVideoAudio
                ? $"{Mathf.RoundToInt(videoScreenPrototype.VideoVolume * 100f)}%"
                : "--";
            homeVideoVolumeValueText.color = hasVideoAudio ? AccentStrong : TextSecondary;

            var hasVoiceAudio = audioPrototype != null;
            homeVoiceVolumeSlider.interactable = hasVoiceAudio;
            homeVoiceVolumeSlider.SetValueWithoutNotify(audioPrototype?.MonitorVolume ?? 0f);
            homeVoiceVolumeValueText.text = hasVoiceAudio
                ? $"{Mathf.RoundToInt(audioPrototype.MonitorVolume * 100f)}%"
                : "--";
            voicePageButton.interactable = hasVoiceAudio;

            var connected = playlistPrototype != null && playlistPrototype.IsConnected;
            var state = playlistPrototype?.CurrentState;
            var count = state?.QueueCount ?? 0;
            queueBadge.gameObject.SetActive(count > 0);
            queueBadgeText.text = Mathf.Min(count, 99).ToString();

            var item = state?.CurrentItem;
            if (item == null)
            {
                songMetaText.text = connected ? "播放队列为空" : "点歌组件不可用";
                songTitleText.text = "等待点歌";
                songDetailText.text = connected ? string.Empty : "请重新启动应用";
            }
            else
            {
                var index = Mathf.Clamp(state.currentIndex + 1, 1, Mathf.Max(count, 1));
                var isCaching = videoScreenPrototype != null &&
                                videoScreenPrototype.IsCachingVideo &&
                                string.Equals(videoScreenPrototype.ActiveItemId, item.id, StringComparison.Ordinal);
                songMetaText.text = item.status == DirectPlaylist.StatusDownloading
                    ? $"正在准备歌曲 · {FormatProgress(item.progress)}"
                    : isCaching
                        ? $"正在缓存歌曲 · {FormatProgress(videoScreenPrototype.CacheProgress)}"
                    : $"正在播放 · 第 {index} / {count} 首";
                songTitleText.text = SafeText(item.title, "未命名歌曲");
                songDetailText.text = isCaching
                    ? $"{FormatSource(item.sourceType)} · 缓存到头显"
                    : $"{FormatSource(item.sourceType)} · {FormatPlayback(item, state.playback)}";
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
            homeVoiceVolumeIcon.color = audioPrototype != null && audioPrototype.IsSafetyReducingGain
                ? Danger
                : hasVoiceAudio ? Accent : TextSecondary;
            homeVoiceVolumeValueText.color = hasVoiceAudio ? AccentStrong : TextSecondary;

            var inputLevel = audioPrototype?.InputLevel ?? 0f;
            for (var index = 0; index < waveBars.Length; index += 1)
            {
                var baseHeight = 12f + ((index * 17) % 5) * 7f;
                var height = Mathf.Lerp(8f, baseHeight + 36f, Mathf.Clamp01(inputLevel * (0.7f + index * 0.05f)));
                waveBars[index].rectTransform.sizeDelta = new Vector2(6f, height);
                waveBars[index].color = micLive ? Accent : palette.InactiveMeter;
            }
        }

        private void RefreshSongSearch()
        {
            if (songSearchInput == null || playlistPrototype == null)
            {
                return;
            }

            var response = playlistPrototype.SearchResults;
            var items = response?.items ?? Array.Empty<BilibiliCatalogItem>();
            searchResultSkeletonVisible = playlistPrototype.IsSearching;
            for (var index = 0; index < SearchResultRowCount; index += 1)
            {
                if (searchResultSkeletonVisible)
                {
                    searchResultRows[index].gameObject.SetActive(true);
                    searchResultSurfaces[index].color = Surface;
                    SetSearchResultSkeletonVisible(index, true);
                    continue;
                }

                SetSearchResultSkeletonVisible(index, false);
                var visible = index < items.Length && items[index] != null && items[index].IsValid;
                searchResultRows[index].gameObject.SetActive(visible);
                if (!visible)
                {
                    SetSearchResultCover(index, string.Empty);
                    continue;
                }

                var item = items[index];
                var isPending = playlistPrototype.IsAddingItem && playlistPrototype.PendingAddItem == item;
                var catalogItemId = NormalizeCatalogItemId(item);
                var isRemoving = pendingQueueRemovalItemIds.Contains(catalogItemId);
                var isConfirmed = confirmedSearchItemIds.Contains(catalogItemId) && !isRemoving;
                searchResultSurfaces[index].color = isPending
                    ? palette.PendingSurface
                    : Surface;
                searchResultTitleTexts[index].text = SafeText(item.title, item.bvid);
                searchResultAuthorTexts[index].text = SafeText(item.author, "未知 UP 主");
                searchResultDurationTexts[index].text = FormatDuration(item);
                SetSearchResultCover(index, item.coverUrl);
                RefreshSearchResultAddButton(index, isPending, isConfirmed, isRemoving);
            }

            songSearchButton.interactable = playlistPrototype.IsConnected &&
                                            !playlistPrototype.IsSearching &&
                                            !string.IsNullOrWhiteSpace(songSearchInput.text);
            searchPreviousPageButton.interactable = !playlistPrototype.IsSearching && songSearchPageNumber > 1;
            searchNextPageButton.interactable = !playlistPrototype.IsSearching && response != null && response.hasMore;
            if (clearSearchButton != null)
            {
                clearSearchButton.interactable = !playlistPrototype.IsSearching &&
                                                 playlistPrototype.HasSearchResults;
            }

            if (!playlistPrototype.IsConnected)
            {
                songSearchStatusText.text = "直接请求尚未就绪，请稍后重试";
                songSearchStatusText.color = Warm;
            }
            else if (playlistPrototype.IsSearching)
            {
                songSearchStatusText.text = "正在检索中...";
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
            RefreshSwitchVisual(voiceMicrophoneToggle);
            inputMeterSlider.SetValueWithoutNotify(audioPrototype.InputLevel);
            ambienceSlider.SetValueWithoutNotify(audioPrototype.AmbienceAmount);
            echoSlider.SetValueWithoutNotify(audioPrototype.EchoAmount);
            dynamicsSlider.SetValueWithoutNotify(audioPrototype.DynamicsAmount);
            distanceMonitoringToggle.SetIsOnWithoutNotify(audioPrototype.IsDistanceMonitoringEnabled);
            RefreshSwitchVisual(distanceMonitoringToggle);

            ambienceValueText.text = $"{Mathf.RoundToInt(audioPrototype.AmbienceAmount * 100f)}%";
            echoValueText.text = $"{Mathf.RoundToInt(audioPrototype.EchoAmount * 100f)}%";
            dynamicsValueText.text = $"{Mathf.RoundToInt(audioPrototype.DynamicsAmount * 100f)}%";
            var spatialSelected = audioPrototype.SelectedMonitorMode == MonitorMode.UnitySpatialSpeakers;
            if (spatialSpeakersModeSurface != null)
            {
                spatialSpeakersModeSurface.color = spatialSelected ? Accent : Surface;
            }
            if (lowLatencyModeSurface != null)
            {
                lowLatencyModeSurface.color = spatialSelected ? Surface : Accent;
            }
            if (spatialSpeakersModeButton != null)
            {
                spatialSpeakersModeButton.interactable = true;
                var spatialLabel = spatialSpeakersModeButton.GetComponentInChildren<TMP_Text>(true);
                if (spatialLabel != null)
                {
                    spatialLabel.color = spatialSelected ? AccentInk : TextPrimary;
                }
            }
            if (lowLatencyModeButton != null)
            {
                lowLatencyModeButton.interactable = audioPrototype.IsNativeBackendSelectable || !spatialSelected;
                var lowLatencyLabel = lowLatencyModeButton.GetComponentInChildren<TMP_Text>(true);
                if (lowLatencyLabel != null)
                {
                    lowLatencyLabel.color = spatialSelected ? TextPrimary : AccentInk;
                }
            }
            voicePresetStatusText.text = $"{FormatPreset(audioPrototype.CurrentPresetIndex)} · " +
                                         (audioPrototype.HasCustomEffectSettings ? "已微调" : "预设值");

            if (!audioPrototype.IsMonitoring)
            {
                distanceMonitoringStatusText.text = "麦克风已关闭 · 距离跟随待机";
                distanceMonitoringStatusText.color = TextSecondary;
            }
            else if (!audioPrototype.IsDistanceMonitoringEnabled)
            {
                distanceMonitoringStatusText.text = "固定返听 · 不随距离变化";
                distanceMonitoringStatusText.color = TextSecondary;
            }
            else if (!audioPrototype.IsMicrophoneDistanceTracked)
            {
                distanceMonitoringStatusText.text = "等待右手麦克风 · 返听已静音";
                distanceMonitoringStatusText.color = Warm;
            }
            else
            {
                distanceMonitoringStatusText.text =
                    $"距离 {audioPrototype.MicrophoneSurfaceClearance * 100f:0.0} cm · 返听 {audioPrototype.DistanceMonitorGain:P0}";
                distanceMonitoringStatusText.color = AccentStrong;
            }

            if (voiceMixerFooterText != null)
            {
                if (audioPrototype.IsNativeFallbackActive)
                {
                    voiceMixerFooterText.text = $"低延迟不可用，已回退空间音箱 · {ShortenStatusReason(audioPrototype.NativeFallbackReason)}";
                    voiceMixerFooterText.color = Warm;
                }
                else if (audioPrototype.IsSafetyReducingGain)
                {
                    voiceMixerFooterText.text = "安全保护正在降低返听增益";
                    voiceMixerFooterText.color = Warm;
                }
                else if (audioPrototype.ActiveMonitorMode == MonitorMode.OboeLowLatency)
                {
                    voiceMixerFooterText.text = "低延迟无墙面定位 · 轻量空间感、回声和人声稳定可用";
                    voiceMixerFooterText.color = TextSecondary;
                }
                else
                {
                    voiceMixerFooterText.text = "人声已定位至墙面音箱 · 距离跟随只调整增益";
                    voiceMixerFooterText.color = TextSecondary;
                }
            }

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

        private static string ShortenStatusReason(string reason)
        {
            const int maximumCharacters = 30;
            if (string.IsNullOrWhiteSpace(reason))
            {
                return "原生音频异常";
            }

            var singleLine = reason.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return singleLine.Length <= maximumCharacters
                ? singleLine
                : singleLine.Substring(0, maximumCharacters - 1) + "\u2026";
        }

        private void RefreshQueue()
        {
            var state = playlistPrototype?.CurrentState;
            var count = state?.QueueCount ?? 0;
            var currentIndex = state != null && state.currentIndex >= 0 && state.currentIndex < count
                ? state.currentIndex
                : -1;
            if (currentIndex != lastQueueCurrentIndex)
            {
                CenterQueueWindowOnCurrent(count, currentIndex);
                lastQueueCurrentIndex = currentIndex;
            }

            var maximumStartIndex = Mathf.Max(0, count - QueueRowCount);
            queueWindowStartIndex = Mathf.Clamp(queueWindowStartIndex, 0, maximumStartIndex);
            queueDrawerCountText.text = $"{count} 首";
            for (var rowIndex = 0; rowIndex < QueueRowCount; rowIndex += 1)
            {
                var itemIndex = queueWindowStartIndex + rowIndex;
                var visible = state?.queue != null && itemIndex >= 0 && itemIndex < count;
                queueRows[rowIndex].gameObject.SetActive(visible);
                if (!visible)
                {
                    queueRowItemIds[rowIndex] = null;
                    SetQueueCover(rowIndex, string.Empty);
                    continue;
                }

                var item = state.queue[itemIndex];
                queueRowItemIds[rowIndex] = item.id;
                var isPast = currentIndex >= 0 && itemIndex < currentIndex;
                var isCurrent = itemIndex == currentIndex;
                var canControl = playlistPrototype != null &&
                                 playlistPrototype.CanSendControl &&
                                 !string.IsNullOrWhiteSpace(queueRowItemIds[rowIndex]);
                var canPlay = canControl &&
                              !string.Equals(item.status, DirectPlaylist.StatusError, StringComparison.OrdinalIgnoreCase) &&
                              item.IsReady;

                queueRowGroups[rowIndex].alpha = isPast ? 0.48f : 1f;
                queueRowSurfaces[rowIndex].color = isCurrent ? palette.PendingSurface : Color.clear;
                queueTitleTexts[rowIndex].text = SafeText(item.title, "未命名歌曲");
                queueMetaTexts[rowIndex].text = FormatQueueItemMeta(item);
                queueNumberTexts[rowIndex].text = (itemIndex + 1).ToString();
                queueNumberTexts[rowIndex].color = isCurrent ? Accent : TextFaint;
                var isPlaying = isCurrent &&
                                item.IsReady &&
                                !(videoScreenPrototype != null && videoScreenPrototype.IsCachingVideo) &&
                                string.Equals(state.playback, "playing", StringComparison.OrdinalIgnoreCase);
                var isCaching = isCurrent &&
                                videoScreenPrototype != null &&
                                videoScreenPrototype.IsCachingVideo &&
                                string.Equals(videoScreenPrototype.ActiveItemId, item.id, StringComparison.Ordinal);
                queueStateTexts[rowIndex].gameObject.SetActive(isPlaying || isCaching);
                queueStateTexts[rowIndex].text = isCaching ? "缓存中" : "播放中";
                queueStateTexts[rowIndex].color = isCaching ? Warm : Accent;
                var isPreparingOnServer = string.Equals(
                    item.status,
                    DirectPlaylist.StatusDownloading,
                    StringComparison.OrdinalIgnoreCase);
                var showProgress = isPreparingOnServer || isCaching;
                var progress = Mathf.Clamp01(isCaching ? videoScreenPrototype.CacheProgress : item.progress);
                queueProgressSliders[rowIndex].gameObject.SetActive(showProgress);
                queueProgressSliders[rowIndex].SetValueWithoutNotify(progress);
                queueProgressTexts[rowIndex].gameObject.SetActive(showProgress);
                queueProgressTexts[rowIndex].text = FormatProgress(progress);
                queuePlayButtons[rowIndex].interactable = canPlay;
                queuePlayIcons[rowIndex].color = canPlay ? AccentStrong : TextFaint;
                queueRemoveButtons[rowIndex].interactable = canControl;
                queueRemoveIcons[rowIndex].color = canControl ? Danger : TextFaint;
                SetQueueCover(rowIndex, item.coverUrl);
            }

            queueEmptyText.gameObject.SetActive(count == 0);
            queueEmptyText.text = "播放队列为空";
            queueFooterText.text = count == 0
                ? "还没有点播歌曲"
                : currentIndex >= 0
                    ? $"当前第 {currentIndex + 1} / {count} 首"
                    : $"共 {count} 首";

            var canScroll = maximumStartIndex > 0;
            queueScrollbarRoot.gameObject.SetActive(canScroll);
            if (canScroll)
            {
                queueScrollbar.size = Mathf.Clamp01(QueueRowCount / (float)count);
                queueScrollbar.numberOfSteps = maximumStartIndex + 1;
                queueScrollbar.interactable = true;
                queueScrollbar.SetValueWithoutNotify(1f - queueWindowStartIndex / (float)maximumStartIndex);
            }

            var canSendControl = playlistPrototype != null && playlistPrototype.CanSendControl;
            if (clearPlayedButton != null)
            {
                clearPlayedButton.interactable = canSendControl && currentIndex > 0;
            }

            if (clearAllQueueButton != null)
            {
                var removableCount = currentIndex >= 0 ? count - 1 : count;
                clearAllQueueButton.interactable = canSendControl && removableCount > 0;
            }
        }

        private void RefreshSettings()
        {
            settingsBuildText.text = QuestBuildInfo.SettingsSummary;
            if (roomPrototype == null)
            {
                roomPrototype = FindAnyObjectByType<QuestKtvRoomPrototype>();
                SubscribeRoom();
            }

            var roomAvailable = roomPrototype != null;
            openRoomAmbienceButton.interactable = roomAvailable;
            openRoomAmbienceSurface.color = Color.clear;
            openRoomAmbienceIcon.color = TextPrimary;

            if (stageLightingPrototype == null)
            {
                stageLightingPrototype = FindAnyObjectByType<QuestStageLightingPrototype>();
            }
            if (openStageLightingButton != null)
            {
                openStageLightingButton.interactable = stageLightingPrototype != null;
                openStageLightingSurface.color = Color.clear;
                openStageLightingIcon.color = TextPrimary;
            }
            var hasVoiceAudio = audioPrototype != null;
            voicePageButton.interactable = hasVoiceAudio;
            voicePageSurface.color = Color.clear;
            voicePageIcon.color = TextPrimary;

            if (handheldPropsPrototype == null)
            {
                handheldPropsPrototype = FindAnyObjectByType<QuestHandheldPropsPrototype>();
            }
            var micProtectionAvailable = handheldPropsPrototype != null;
            openMicProtectionButton.interactable = micProtectionAvailable;
            openMicProtectionSurface.color = Color.clear;
            openMicProtectionIcon.color = TextPrimary;

            if (playBuiltInDefaultButton != null)
            {
                var isBuiltInDefaultActive = videoScreenPrototype != null && videoScreenPrototype.IsPlayingBuiltInDefault;
                var isBuiltInDefaultPreparing = videoScreenPrototype != null && videoScreenPrototype.IsBuiltInDefaultPreparing;
                var isBuiltInDefaultPaused = videoScreenPrototype != null && videoScreenPrototype.IsBuiltInDefaultPaused;
                playBuiltInDefaultButton.interactable = videoScreenPrototype != null && !isBuiltInDefaultPreparing;
                playBuiltInDefaultText.text = isBuiltInDefaultPreparing
                    ? "正在准备视频"
                    : isBuiltInDefaultPaused
                        ? "继续内置视频"
                        : isBuiltInDefaultActive
                            ? "暂停内置视频"
                            : "播放内置视频";
                playBuiltInDefaultIcon.SetIcon(isBuiltInDefaultActive && !isBuiltInDefaultPaused && !isBuiltInDefaultPreparing
                    ? QuestUiIconKind.Pause
                    : QuestUiIconKind.Play);
                playBuiltInDefaultIcon.color = isBuiltInDefaultActive ? AccentStrong : Accent;
                if (playBuiltInDefaultButton.targetGraphic is QuestUiSurface builtInDefaultSurface)
                {
                    builtInDefaultSurface.color = isBuiltInDefaultActive ? palette.EnabledSurface : Surface;
                }

                stopBuiltInDefaultButton.interactable = isBuiltInDefaultActive;
                stopBuiltInDefaultIcon.color = isBuiltInDefaultActive ? Warm : TextFaint;
                if (stopBuiltInDefaultButton.targetGraphic is QuestUiSurface stopBuiltInDefaultSurface)
                {
                    stopBuiltInDefaultSurface.color = isBuiltInDefaultActive ? WarmSurface : Surface;
                }
            }

            var exitConfirmationActive = exitConfirmationExpiresAt > Time.unscaledTime;
            exitApplicationText.text = exitConfirmationActive ? "再次点击退出" : "退出应用";
            exitApplicationText.color = DangerBorder;
            exitApplicationIcon.color = DangerBorder;
            if (exitApplicationButton.targetGraphic is QuestUiSurface exitSurface)
            {
                exitSurface.color = DangerSurface;
            }

        }

        private void RefreshRoomAmbience()
        {
            if (roomPrototype == null)
            {
                roomPrototype = FindAnyObjectByType<QuestKtvRoomPrototype>();
                SubscribeRoom();
            }

            var available = roomPrototype != null;
            var starsAvailable = available && roomPrototype.StarsAvailable;
            ceilingStarsToggle.interactable = starsAvailable;
            ceilingAuroraToggle.interactable = available;
            ceilingStarsToggle.SetIsOnWithoutNotify(available && roomPrototype.StarsEnabled);
            ceilingAuroraToggle.SetIsOnWithoutNotify(available && roomPrototype.AuroraEnabled);
            RefreshSwitchVisual(ceilingStarsToggle);
            RefreshSwitchVisual(ceilingAuroraToggle);
            ceilingStarsLabel.color = !starsAvailable
                ? TextFaint
                : roomPrototype.StarsEnabled ? TextPrimary : TextSecondary;
            ceilingAuroraLabel.text = available && roomPrototype.CurrentTheme == RoomTheme.Bright ? "火烧云" : "极光";
            ceilingAuroraLabel.color = available && roomPrototype.AuroraEnabled ? TextPrimary : TextSecondary;
        }

        private void RefreshStageLighting()
        {
            if (stageLightingPrototype == null)
            {
                stageLightingPrototype = FindAnyObjectByType<QuestStageLightingPrototype>();
            }

            var available = stageLightingPrototype != null;
            var enabled = available && stageLightingPrototype.LightingEnabled;
            stageLightingToggle.interactable = available;
            stageLightingToggle.SetIsOnWithoutNotify(enabled);
            RefreshSwitchVisual(stageLightingToggle);
            stageLightingStatusText.text = !available ? "灯组不可用" : enabled ? "开启" : "已关闭";
            stageLightingStatusText.color = enabled ? AccentStrong : TextSecondary;

            var preset = available ? stageLightingPrototype.CurrentPreset : StageLightingPreset.Custom;
            var presetNames = new[] { "氛围", "LIVE", "极光", "高潮" };
            for (var index = 0; index < stageLightingPresetButtons.Length; index += 1)
            {
                var selected = preset == (StageLightingPreset)index;
                stageLightingPresetButtons[index].interactable = available;
                stageLightingPresetSurfaces[index].color = selected ? Accent : Surface;
                stageLightingPresetLabels[index].color = selected ? AccentInk : enabled ? TextPrimary : TextSecondary;
                stageLightingPresetButtons[index].colors = CreateButtonColors(stageLightingPresetSurfaces[index].color);
                var outline = stageLightingPresetButtons[index].GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = selected ? AccentStrong : Line;
                    outline.effectDistance = selected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
                }
            }
            stageLightingPresetStatusText.text = preset == StageLightingPreset.Custom
                ? "自定义"
                : presetNames[Mathf.Clamp((int)preset, 0, presetNames.Length - 1)];
            stageLightingPresetStatusText.color = available ? AccentStrong : TextFaint;

            var selectedLook = available ? stageLightingPrototype.ColorLook : StageLightingColorLook.Neon;
            var colorNames = new[] { "海洋", "霓虹", "日落", "冰白", "光谱" };
            for (var index = 0; index < stageLightingColorButtons.Length; index += 1)
            {
                var swatchColor = QuestStageLightingPrototype.GetColorLookSwatch((StageLightingColorLook)index);
                stageLightingColorButtons[index].interactable = enabled;
                stageLightingColorSurfaces[index].color = swatchColor;
                stageLightingColorButtons[index].colors = CreateButtonColors(swatchColor);
                var selected = selectedLook == (StageLightingColorLook)index;
                stageLightingColorOutlines[index].effectColor = selected ? AccentStrong : Line;
                stageLightingColorOutlines[index].effectDistance = selected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }
            stageLightingColorNameText.text = colorNames[Mathf.Clamp((int)selectedLook, 0, colorNames.Length - 1)];
            stageLightingColorNameText.color = enabled ? TextPrimary : TextFaint;

            var lightingIntensity = available ? stageLightingPrototype.Intensity : 0f;
            var lightingSpeed = available ? stageLightingPrototype.MovementSpeed : 0f;
            var lightingWidth = available ? stageLightingPrototype.BeamWidth : 0f;
            var lightingRange = available ? stageLightingPrototype.MotionRange : 0f;
            stageLightingIntensitySlider.interactable = enabled;
            stageLightingSpeedSlider.interactable = enabled;
            stageLightingWidthSlider.interactable = enabled;
            stageLightingRangeSlider.interactable = enabled;
            stageLightingIntensitySlider.SetValueWithoutNotify(lightingIntensity);
            stageLightingSpeedSlider.SetValueWithoutNotify(lightingSpeed);
            stageLightingWidthSlider.SetValueWithoutNotify(lightingWidth);
            stageLightingRangeSlider.SetValueWithoutNotify(lightingRange);
            stageLightingIntensityValueText.text = $"{Mathf.RoundToInt(lightingIntensity * 100f)}%";
            stageLightingSpeedValueText.text = $"{Mathf.RoundToInt(lightingSpeed * 100f)}%";
            stageLightingWidthValueText.text = $"{Mathf.RoundToInt(Mathf.Lerp(20f, 46f, lightingWidth))}°";
            stageLightingRangeValueText.text = $"{Mathf.RoundToInt(lightingRange * 100f)}%";
            stageLightingIntensityValueText.color = enabled ? AccentStrong : TextFaint;
            stageLightingSpeedValueText.color = enabled ? Warm : TextFaint;
            stageLightingWidthValueText.color = enabled ? AccentStrong : TextFaint;
            stageLightingRangeValueText.color = enabled ? Warm : TextFaint;

            stageLightingMotionToggle.interactable = enabled;
            stageLightingPulseToggle.interactable = enabled;
            stageLightingBeamsToggle.interactable = enabled;
            stageLightingMotionToggle.SetIsOnWithoutNotify(available && stageLightingPrototype.AutomaticMotion);
            stageLightingPulseToggle.SetIsOnWithoutNotify(available && stageLightingPrototype.BeatPulse);
            stageLightingBeamsToggle.SetIsOnWithoutNotify(available && stageLightingPrototype.BeamsVisible);
            RefreshSwitchVisual(stageLightingMotionToggle);
            RefreshSwitchVisual(stageLightingPulseToggle);
            RefreshSwitchVisual(stageLightingBeamsToggle);
        }

        private void RefreshMicProtection()
        {
            if (handheldPropsPrototype == null)
            {
                handheldPropsPrototype = FindAnyObjectByType<QuestHandheldPropsPrototype>();
            }

            var available = handheldPropsPrototype != null;
            openMicProtectionButton.interactable = available;
            openMicProtectionSurface.color = Color.clear;
            openMicProtectionIcon.color = TextPrimary;

            if (!available)
            {
                micLiveDistanceText.text = "-- cm";
                micLiveStateText.text = "麦克风组件缺失";
                micLiveStateText.color = Danger;
                SetMicProtectionControlsInteractable(false);
                return;
            }

            micProtectionToggle.SetIsOnWithoutNotify(handheldPropsPrototype.MicFaceHapticsEnabled);
            RefreshSwitchVisual(micProtectionToggle);
            micWarningDistanceSlider.SetValueWithoutNotify(handheldPropsPrototype.MicFaceWarningClearance / MicWarningClearanceStep);
            micCriticalDistanceSlider.SetValueWithoutNotify(handheldPropsPrototype.MicFaceCriticalClearance / MicCriticalClearanceStep);
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
            openMicMouthPointButton.interactable = interactable;
            micWarningDistanceSlider.interactable = interactable;
            micCriticalDistanceSlider.interactable = interactable;
            micHapticStrengthSlider.interactable = interactable;
            captureWarningDistanceButton.interactable = interactable;
            captureCriticalDistanceButton.interactable = interactable;
            resetMicProtectionButton.interactable = interactable;
        }

        private void RefreshMicMouthPoint()
        {
            if (handheldPropsPrototype == null)
            {
                handheldPropsPrototype = FindAnyObjectByType<QuestHandheldPropsPrototype>();
            }

            if (handheldPropsPrototype == null)
            {
                micMouthHorizontalValueText.text = "--";
                micMouthDownValueText.text = "--";
                micMouthForwardValueText.text = "--";
                micMouthMarkerStatusText.text = "麦克风组件缺失";
                micMouthMarkerStatusText.color = Danger;
                micMouthCoordinateText.text = "HMD 局部坐标不可用";
                SetMicMouthPointControlsInteractable(false);
                return;
            }

            handheldPropsPrototype.SetMicFaceMouthMarkerVisible(currentPage == UiPage.MicMouthPoint);
            var offset = handheldPropsPrototype.MicFaceMouthLocalOffset;
            micMouthHorizontalSlider.SetValueWithoutNotify(offset.x / MicMouthOffsetStep);
            micMouthDownSlider.SetValueWithoutNotify(-offset.y / MicMouthOffsetStep);
            micMouthForwardSlider.SetValueWithoutNotify(offset.z / MicMouthOffsetStep);
            micMouthHorizontalValueText.text = Mathf.Abs(offset.x) < 0.0001f
                ? "居中"
                : offset.x < 0f ? $"左 {Mathf.Abs(offset.x) * 100f:0.0} cm" : $"右 {offset.x * 100f:0.0} cm";
            micMouthDownValueText.text = $"{-offset.y * 100f:0.0} cm";
            micMouthForwardValueText.text = $"{offset.z * 100f:0.0} cm";
            micMouthMarkerStatusText.text = handheldPropsPrototype.IsMicFaceMouthMarkerVisible
                ? "空间标记已显示"
                : "正在定位头显";
            micMouthMarkerStatusText.color = handheldPropsPrototype.IsMicFaceMouthMarkerVisible ? AccentStrong : TextSecondary;
            micMouthCoordinateText.text =
                $"HMD 局部坐标  X {offset.x * 100f:+0.0;-0.0;0.0} · " +
                $"Y {offset.y * 100f:+0.0;-0.0;0.0} · Z {offset.z * 100f:+0.0;-0.0;0.0} cm";
            SetMicMouthPointControlsInteractable(true);
        }

        private void SetMicMouthPointControlsInteractable(bool interactable)
        {
            micMouthHorizontalSlider.interactable = interactable;
            micMouthDownSlider.interactable = interactable;
            micMouthForwardSlider.interactable = interactable;
            resetMicMouthPointButton.interactable = interactable;
        }

        private void SetMicMouthHorizontalOffset(float stepValue)
        {
            if (handheldPropsPrototype == null)
            {
                return;
            }

            var offset = handheldPropsPrototype.MicFaceMouthLocalOffset;
            offset.x = stepValue * MicMouthOffsetStep;
            handheldPropsPrototype.SetMicFaceMouthLocalOffset(offset);
            RefreshMicMouthPoint();
        }

        private void SetMicMouthDownOffset(float stepValue)
        {
            if (handheldPropsPrototype == null)
            {
                return;
            }

            var offset = handheldPropsPrototype.MicFaceMouthLocalOffset;
            offset.y = -stepValue * MicMouthOffsetStep;
            handheldPropsPrototype.SetMicFaceMouthLocalOffset(offset);
            RefreshMicMouthPoint();
        }

        private void SetMicMouthForwardOffset(float stepValue)
        {
            if (handheldPropsPrototype == null)
            {
                return;
            }

            var offset = handheldPropsPrototype.MicFaceMouthLocalOffset;
            offset.z = stepValue * MicMouthOffsetStep;
            handheldPropsPrototype.SetMicFaceMouthLocalOffset(offset);
            RefreshMicMouthPoint();
        }

        private void ResetMicMouthPoint()
        {
            handheldPropsPrototype?.ResetMicFaceMouthLocalOffset();
            RefreshMicMouthPoint();
        }

        private void RefreshDiagnostics()
        {
            var audioStatus = audioPrototype == null
                ? "音频缺失"
                : audioPrototype.IsMonitoring ? "音频正常" : "音频待机";
            var videoStatus = videoScreenPrototype == null
                ? "视频缺失"
                : videoScreenPrototype.IsCachingVideo ? $"视频缓存中 {FormatProgress(videoScreenPrototype.CacheProgress)}"
                : videoScreenPrototype.IsPreparing ? "视频准备中" : "视频正常";
            diagnosticsHealthText.text = $"应用正常 · {audioStatus} · {videoStatus}";
            diagnosticsHealthText.color = audioPrototype == null || videoScreenPrototype == null ? Warm : TextPrimary;

            diagnosticsRequestModeText.text = playlistPrototype != null && playlistPrototype.IsConnected
                ? "点歌方式：已就绪 · 直接请求"
                : "点歌方式：组件不可用";

            var videoSummary = videoScreenPrototype == null
                ? "视频组件缺失"
                : videoScreenPrototype.IsCachingVideo ? $"视频正在缓存 {FormatProgress(videoScreenPrototype.CacheProgress)}"
                : videoScreenPrototype.IsPreparing ? "视频正在准备"
                : videoScreenPrototype.IsPlaying ? "视频正在播放"
                : "视频待机";
            var clearConfirmationActive = clearMediaCacheConfirmationExpiresAt > Time.unscaledTime;
            var restoreConfirmationActive = restoreDefaultSettingsConfirmationExpiresAt > Time.unscaledTime;
            var maintenanceResultActive = maintenanceResultExpiresAt > Time.unscaledTime &&
                                          !string.IsNullOrEmpty(maintenanceResultMessage);
            if (clearConfirmationActive)
            {
                diagnosticsVideoText.text = "清除媒体缓存：请再次点击确认\n将停止播放、取消下载并清空当前队列";
                diagnosticsVideoText.color = Danger;
            }
            else if (restoreConfirmationActive)
            {
                diagnosticsVideoText.text = "将恢复月夜主题、平板 60°、星空/极光与极光舞台预设\n媒体缓存不会被删除";
                diagnosticsVideoText.color = Warm;
            }
            else if (maintenanceResultActive)
            {
                diagnosticsVideoText.text = maintenanceResultMessage;
                diagnosticsVideoText.color = maintenanceResultFailed ? Danger : AccentStrong;
            }
            else
            {
                diagnosticsVideoText.text = $"视频状态：{videoSummary}\n{SingleLine(videoScreenPrototype?.StatusSummary)}";
                diagnosticsVideoText.color = TextSecondary;
            }

            RefreshMaintenanceButtons(clearConfirmationActive, restoreConfirmationActive);

            rawDiagnosticsText.text =
                $"构建信息  {QuestBuildInfo.RawSummary}\n" +
                $"音频后端  {audioPrototype?.ActiveBackendName ?? "missing"}\n" +
                $"人声定位  {(audioPrototype == null ? "missing" : audioPrototype.IsSpatialVoiceEnabled ? "墙面音箱" : "off")}\n" +
                $"人声预设  {audioPrototype?.CurrentPresetName ?? "missing"}\n" +
                $"输入/输出  {(audioPrototype?.InputLevel ?? 0f):P0} / {(audioPrototype?.OutputLevel ?? 0f):P0}\n" +
                $"主页音量  视频 {(videoScreenPrototype?.VideoVolume ?? 0f):P0} · 人声 {(audioPrototype?.MonitorVolume ?? 0f):P0} · 前级 {(audioPrototype?.MonitorPreGainDecibels ?? -80f):+0.0;-0.0;0.0} dB\n" +
                $"人声参数  空间 {(audioPrototype?.AmbienceAmount ?? 0f):P0} · 回声 {(audioPrototype?.EchoAmount ?? 0f):P0} · 稳定 {(audioPrototype?.DynamicsAmount ?? 0f):P0}\n" +
                $"距离返听  {(audioPrototype == null ? "missing" : $"{(audioPrototype.IsDistanceMonitoringEnabled ? "on" : "off")} · {(audioPrototype.IsMicrophoneDistanceTracked ? $"{audioPrototype.MicrophoneSurfaceClearance * 100f:0.0}cm" : "untracked")} · {audioPrototype.DistanceMonitorGain:P0}")}\n" +
                $"防碰撞  {(handheldPropsPrototype == null ? "missing" : $"{handheldPropsPrototype.MicFaceWarningClearance * 100f:0.0}/{handheldPropsPrototype.MicFaceCriticalClearance * 100f:0.0}cm {handheldPropsPrototype.MicFaceHapticStrength:P0}")}\n" +
                $"嘴部定位  {(handheldPropsPrototype == null ? "missing" : handheldPropsPrototype.MicFaceMouthLocalOffset.ToString("F3"))}\n" +
                $"内容接口  {BilibiliDirectClient.ApiOrigin}\n" +
                $"视频状态  {SingleLine(videoScreenPrototype?.StatusSummary)}" +
                (string.IsNullOrEmpty(lastMaintenanceDetails)
                    ? string.Empty
                    : $"\n存储操作  {SingleLine(lastMaintenanceDetails)}");
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

        private void HandleExitApplication()
        {
            if (exitConfirmationExpiresAt <= 0f || Time.unscaledTime > exitConfirmationExpiresAt)
            {
                exitConfirmationExpiresAt = Time.unscaledTime + ExitConfirmationSeconds;
                RefreshSettings();
                return;
            }

            exitApplicationButton.interactable = false;
            PlayerPrefs.Save();
            Debug.Log("[TsukiVox UI] Exiting application after user confirmation.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OpenSongSearchPage()
        {
            // 启动时输入和结果都保持为空，由用户输入或选择拼音候选。
            ShowPage(UiPage.SongSearch);
        }

        private void OpenStageLightingPage(UiPage returnPage)
        {
            stageLightingReturnPage = returnPage == UiPage.Settings ? UiPage.Settings : UiPage.Home;
            ShowPage(UiPage.StageLighting);
        }

        private void ClearSearchResults()
        {
            songSearchPageNumber = 1;
            playlistPrototype?.ClearSearchResults();
            RefreshSongSearch();
        }

        private void ClearPlayedQueue()
        {
            if (playlistPrototype == null || !playlistPrototype.CanSendControl)
            {
                return;
            }

            var state = playlistPrototype.CurrentState;
            var queue = state?.queue;
            if (queue == null || state.currentIndex <= 0 || state.currentIndex >= queue.Length)
            {
                return;
            }

            MarkQueueItemsPendingRemoval(queue, 0, state.currentIndex, -1);
            playlistPrototype.ClearPlayedQueue();
            RefreshQueue();
            RefreshSongSearch();
        }

        private void ClearQueueExceptCurrent()
        {
            if (playlistPrototype == null || !playlistPrototype.CanSendControl)
            {
                return;
            }

            var state = playlistPrototype.CurrentState;
            var queue = state?.queue;
            if (queue == null || queue.Length == 0)
            {
                return;
            }

            var currentIndex = state.currentIndex >= 0 && state.currentIndex < queue.Length
                ? state.currentIndex
                : -1;
            if (queue.Length == 1 && currentIndex == 0)
            {
                return;
            }

            MarkQueueItemsPendingRemoval(queue, 0, queue.Length, currentIndex);
            playlistPrototype.ClearQueueExceptCurrent();
            RefreshQueue();
            RefreshSongSearch();
        }

        private void MarkQueueItemsPendingRemoval(
            PlaylistItem[] queue,
            int startIndex,
            int endIndex,
            int preservedIndex)
        {
            for (var index = Mathf.Max(0, startIndex); index < Mathf.Min(endIndex, queue.Length); index += 1)
            {
                if (index == preservedIndex)
                {
                    continue;
                }

                var catalogItemId = NormalizeCatalogItemId(queue[index]?.sourceInput);
                if (string.IsNullOrEmpty(catalogItemId))
                {
                    continue;
                }

                confirmedSearchItemIds.Remove(catalogItemId);
                pendingQueueRemovalItemIds.Add(catalogItemId);
            }
        }

        private void PlayQueueRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= QueueRowCount)
            {
                return;
            }

            var itemId = queueRowItemIds[rowIndex];
            if (string.IsNullOrWhiteSpace(itemId) ||
                playlistPrototype == null ||
                !playlistPrototype.CanSendControl)
            {
                return;
            }

            playlistPrototype.PlayQueueItem(itemId);
            RefreshQueue();
        }

        private void HandleQueueScrollbarChanged(float value)
        {
            var count = playlistPrototype?.CurrentState?.QueueCount ?? 0;
            var maximumStartIndex = Mathf.Max(0, count - QueueRowCount);
            if (maximumStartIndex == 0)
            {
                queueWindowStartIndex = 0;
                return;
            }

            var nextStartIndex = Mathf.Clamp(
                Mathf.RoundToInt((1f - Mathf.Clamp01(value)) * maximumStartIndex),
                0,
                maximumStartIndex);
            if (nextStartIndex == queueWindowStartIndex)
            {
                return;
            }

            queueWindowStartIndex = nextStartIndex;
            RefreshQueue();
        }

        private void ScrollQueueByRows(int rowDelta)
        {
            if (rowDelta == 0)
            {
                return;
            }

            var count = playlistPrototype?.CurrentState?.QueueCount ?? 0;
            var maximumStartIndex = Mathf.Max(0, count - QueueRowCount);
            var nextStartIndex = Mathf.Clamp(queueWindowStartIndex + rowDelta, 0, maximumStartIndex);
            if (nextStartIndex == queueWindowStartIndex)
            {
                return;
            }

            queueWindowStartIndex = nextStartIndex;
            RefreshQueue();
        }

        private void CenterQueueWindowOnCurrent(int count, int currentIndex)
        {
            var maximumStartIndex = Mathf.Max(0, count - QueueRowCount);
            queueWindowStartIndex = currentIndex < 0
                ? 0
                : Mathf.Clamp(currentIndex - QueueRowCount / 2, 0, maximumStartIndex);
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

            RemoveQueueItem(itemId);
        }

        private void RemoveQueueItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) ||
                playlistPrototype == null ||
                !playlistPrototype.CanSendControl)
            {
                return;
            }

            var queue = playlistPrototype?.CurrentState?.queue;
            if (queue != null)
            {
                for (var index = 0; index < queue.Length; index += 1)
                {
                    if (queue[index] != null && string.Equals(queue[index].id, itemId, StringComparison.Ordinal))
                    {
                        var catalogItemId = NormalizeCatalogItemId(queue[index].sourceInput);
                        if (!string.IsNullOrEmpty(catalogItemId))
                        {
                            confirmedSearchItemIds.Remove(catalogItemId);
                            pendingQueueRemovalItemIds.Add(catalogItemId);
                        }
                        break;
                    }
                }
            }

            playlistPrototype.RemoveQueueItem(itemId);
            RefreshQueue();
            RefreshSongSearch();
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
                HideSuggestions();
                songSearchStatusText.text = "请输入歌曲名";
                songSearchStatusText.color = Danger;
                return;
            }

            HideSuggestions();
            songSearchPageNumber = Mathf.Max(1, page);
            playlistPrototype.SearchBilibili(BuildSongSearchQuery(query), songSearchPageNumber, SearchResultRowCount);
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
            RefreshSwitchVisual(appendKtvSearchToggle);
            if (appendKtvSearchLabel != null)
            {
                appendKtvSearchLabel.color = appendKtvToSearch ? TextPrimary : TextSecondary;
            }
        }

        private void HandleSongSearchInputChanged(string value)
        {
            HideSuggestions();
            var query = value?.Trim();
            if (!string.IsNullOrWhiteSpace(query))
            {
                suggestDebounceRoutine = StartCoroutine(FetchSuggestionsAfterDebounce(query));
            }
        }

        private void HandleSongSearchSubmitted(string value)
        {
            HideSuggestions();
            var query = value?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                songSearchStatusText.text = "请输入歌曲名";
                songSearchStatusText.color = Danger;
                return;
            }

            FetchSuggestions(query);
        }

        private IEnumerator FetchSuggestionsAfterDebounce(string query)
        {
            yield return new WaitForSecondsRealtime(SuggestDebounceSeconds);
            suggestDebounceRoutine = null;

            if (!string.Equals(songSearchInput?.text?.Trim(), query, StringComparison.Ordinal))
            {
                yield break;
            }

            FetchSuggestions(query);
        }

        private void FetchSuggestions(string query)
        {
            if (currentPage != UiPage.SongSearch ||
                playlistPrototype == null ||
                !playlistPrototype.IsConnected ||
                string.IsNullOrWhiteSpace(query))
            {
                RefreshSongSearch();
                return;
            }

            Debug.Log($"[TsukiVox Suggest] Requesting suggestions for input length {query.Length}.");
            playlistPrototype.FetchBilibiliSuggestions(query);
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

            if (songSearchInput != null)
            {
                songSearchInput.SetTextWithoutNotify(selected.value);
                songSearchInput.GetComponent<QuestAndroidKeyboardInput>()?.HideKeyboard();
            }

            SearchSongs(1);
        }

        private void HideSuggestions()
        {
            CancelSuggestionDebounce();
            if (suggestContainer != null)
            {
                suggestContainer.gameObject.SetActive(false);
            }

            if (playlistPrototype != null &&
                (playlistPrototype.IsFetchingSuggestions || playlistPrototype.SuggestResults != null))
            {
                playlistPrototype.ClearSuggestions();
            }
        }

        private void CancelSuggestionDebounce()
        {
            if (suggestDebounceRoutine == null)
            {
                return;
            }

            StopCoroutine(suggestDebounceRoutine);
            suggestDebounceRoutine = null;
        }

        private void RefreshSuggestions()
        {
            if (suggestContainer == null || playlistPrototype == null)
            {
                return;
            }

            var keyboard = songSearchInput != null
                ? songSearchInput.GetComponent<QuestAndroidKeyboardInput>()
                : null;
            var keyboardOpen = keyboard != null && keyboard.IsKeyboardOpen;

            if (playlistPrototype.IsFetchingSuggestions)
            {
                suggestContainer.gameObject.SetActive(false);
                SuppressSearchResults(true);
                if (!playlistPrototype.IsSearching && !playlistPrototype.IsAddingItem)
                {
                    songSearchStatusText.text = "正在获取搜索建议…";
                    songSearchStatusText.color = TextSecondary;
                }
                return;
            }

            var suggestions = playlistPrototype.SuggestResults?.result?.tag;
            var hasSuggestions = playlistPrototype.IsConnected && suggestions != null && suggestions.Length > 0;

            suggestContainer.gameObject.SetActive(hasSuggestions);
            SuppressSearchResults(keyboardOpen || hasSuggestions);

            if (!hasSuggestions)
            {
                if (!string.IsNullOrWhiteSpace(playlistPrototype.LastSuggestError) &&
                    !playlistPrototype.IsSearching && !playlistPrototype.IsAddingItem)
                {
                    songSearchStatusText.text = SingleLine(playlistPrototype.LastSuggestError);
                    songSearchStatusText.color = Danger;
                }
                else if (playlistPrototype.IsConnected && playlistPrototype.SuggestResults != null &&
                    !playlistPrototype.IsSearching && !playlistPrototype.IsAddingItem)
                {
                    songSearchStatusText.text = "没有找到搜索建议";
                    songSearchStatusText.color = TextSecondary;
                }
                return;
            }

            suggestContainer.SetAsLastSibling();
            if (!playlistPrototype.IsSearching && !playlistPrototype.IsAddingItem)
            {
                songSearchStatusText.text = $"找到 {Mathf.Min(SuggestRowCount, suggestions.Length)} 条搜索建议";
                songSearchStatusText.color = Accent;
            }

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

            var item = items[index];
            var catalogItemId = NormalizeCatalogItemId(item);
            if (pendingQueueRemovalItemIds.Contains(catalogItemId))
            {
                return;
            }

            if (confirmedSearchItemIds.Contains(catalogItemId))
            {
                if (TryFindActiveQueueItem(item, out var queueItem))
                {
                    RemoveQueueItem(queueItem.id);
                }
                return;
            }

            pendingEnqueueFeedbackItem = item;
            pendingEnqueueFeedbackIndex = index;
            var state = playlistPrototype.CurrentState;
            var shouldPlayImmediately = state?.CurrentItem == null ||
                                        !string.Equals(
                                            state.playback,
                                            "playing",
                                            StringComparison.OrdinalIgnoreCase);
            playlistPrototype.AddItem(item, shouldPlayImmediately);
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
                var clearanceStep = captureWarning ? MicWarningClearanceStep : MicCriticalClearanceStep;
                var capturedClearance = Mathf.Round(samples[samples.Count / 2] / clearanceStep) * clearanceStep;
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
            RefreshMicMouthPoint();
        }

        private void HandleClearMediaCache()
        {
            var now = Time.unscaledTime;
            if (clearMediaCacheConfirmationExpiresAt <= now)
            {
                clearMediaCacheConfirmationExpiresAt = now + DestructiveActionConfirmationSeconds;
                restoreDefaultSettingsConfirmationExpiresAt = 0f;
                maintenanceResultExpiresAt = 0f;
                RefreshDiagnostics();
                return;
            }

            clearMediaCacheConfirmationExpiresAt = 0f;
            if (playlistPrototype == null)
            {
                SetMaintenanceResult(
                    "无法清除媒体缓存\n点歌组件当前不可用",
                    true,
                    "media cache clear failed: playlist component missing");
                RefreshDiagnostics();
                return;
            }

            var result = playlistPrototype.ClearMediaCache();
            confirmedSearchItemIds.Clear();
            pendingQueueRemovalItemIds.Clear();
            if (result.Succeeded)
            {
                var message = result.DeletedFileCount == 0
                    ? "媒体缓存已经为空\n当前播放和队列已停止"
                    : $"媒体缓存已清除 · {result.DeletedFileCount} 个文件\n已释放 {FormatBytes(result.DeletedBytes)}，当前队列已清空";
                SetMaintenanceResult(
                    message,
                    false,
                    $"cleared {result.DeletedFileCount} files, {result.DeletedBytes} bytes");
            }
            else
            {
                SetMaintenanceResult(
                    $"媒体缓存未完全清除\n已删除 {result.DeletedFileCount} 个文件，请复制诊断信息",
                    true,
                    result.Error);
            }

            RefreshAll();
        }

        private void HandleRestoreDefaultSettings()
        {
            var now = Time.unscaledTime;
            if (restoreDefaultSettingsConfirmationExpiresAt <= now)
            {
                restoreDefaultSettingsConfirmationExpiresAt = now + DestructiveActionConfirmationSeconds;
                clearMediaCacheConfirmationExpiresAt = 0f;
                maintenanceResultExpiresAt = 0f;
                RefreshDiagnostics();
                return;
            }

            restoreDefaultSettingsConfirmationExpiresAt = 0f;
            CancelMicFaceCalibration();
            PlayerPrefs.DeleteAll();
            appendKtvToSearch = false;
            audioPrototype?.RestoreDefaultSettings();
            videoScreenPrototype?.RestoreDefaultVolume();

            if (handheldPropsPrototype == null)
            {
                handheldPropsPrototype = FindAnyObjectByType<QuestHandheldPropsPrototype>();
            }
            handheldPropsPrototype?.ResetMicFaceHapticPreferences();

            var tiltController = FindAnyObjectByType<QuestTabletTiltController>();
            tiltController?.RestoreDefaultSetting();
            var themeController = FindAnyObjectByType<QuestRoomThemeController>();
            themeController?.RestoreDefaultSetting();
            if (roomPrototype == null)
            {
                roomPrototype = FindAnyObjectByType<QuestKtvRoomPrototype>();
            }
            roomPrototype?.RestoreCelestialDefaults();
            if (stageLightingPrototype == null)
            {
                stageLightingPrototype = FindAnyObjectByType<QuestStageLightingPrototype>();
            }
            stageLightingPrototype?.RestoreDefaultSettings();
            PlayerPrefs.Save();

            exitConfirmationExpiresAt = 0f;
            RefreshAppendKtvSearchToggle();
            SetMaintenanceResult(
                "全部设置已恢复默认\n月夜主题 · 平板 60° · 星空/极光开启 · 舞台预设“极光”",
                false,
                "all settings restored to defaults; media cache retained");
            RefreshAll();
        }

        private void SetMaintenanceResult(string message, bool failed, string details)
        {
            maintenanceResultMessage = message ?? string.Empty;
            maintenanceResultFailed = failed;
            maintenanceResultExpiresAt = Time.unscaledTime + MaintenanceResultSeconds;
            lastMaintenanceDetails = details ?? string.Empty;
        }

        private void RefreshMaintenanceButtons(bool clearConfirmationActive, bool restoreConfirmationActive)
        {
            if (clearMediaCacheButton != null)
            {
                clearMediaCacheButton.interactable = playlistPrototype != null;
                clearMediaCacheButtonText.text = clearConfirmationActive ? "再次点击确认" : "清除媒体缓存";
                clearMediaCacheButtonText.color = Danger;
                clearMediaCacheIcon.color = Danger;
                if (clearMediaCacheButton.targetGraphic is QuestUiSurface clearSurface)
                {
                    clearSurface.color = clearConfirmationActive ? WarmSurface : Surface;
                    clearMediaCacheButton.colors = CreateButtonColors(clearSurface.color);
                }
            }

            if (restoreDefaultSettingsButton != null)
            {
                restoreDefaultSettingsButtonText.text = restoreConfirmationActive ? "再次点击确认" : "恢复全部默认设置";
                restoreDefaultSettingsButtonText.color = restoreConfirmationActive ? Danger : TextPrimary;
                restoreDefaultSettingsIcon.color = restoreConfirmationActive ? Danger : Warm;
                if (restoreDefaultSettingsButton.targetGraphic is QuestUiSurface restoreSurface)
                {
                    restoreSurface.color = restoreConfirmationActive ? WarmSurface : Surface;
                    restoreDefaultSettingsButton.colors = CreateButtonColors(restoreSurface.color);
                }
            }
        }

        private void ToggleRawDetails()
        {
            rawDetailsVisible = !rawDetailsVisible;
            rawDetailsRoot.gameObject.SetActive(rawDetailsVisible);
            rawDetailsButtonText.text = rawDetailsVisible ? "收起原始详情" : "展开原始详情";
        }

        private void SubscribeRoom()
        {
            if (!isActiveAndEnabled || roomPrototype == null || subscribedRoom == roomPrototype)
            {
                return;
            }

            UnsubscribeRoom();
            subscribedRoom = roomPrototype;
            subscribedRoom.ThemeChanged += HandleRoomThemeChanged;
        }

        private void UnsubscribeRoom()
        {
            if (subscribedRoom == null)
            {
                return;
            }

            subscribedRoom.ThemeChanged -= HandleRoomThemeChanged;
            subscribedRoom = null;
        }

        private void HandleRoomThemeChanged(RoomTheme theme)
        {
            palette = QuestUiThemePalette.For(theme);
            ApplyThemeToExistingUi();
            RefreshAll();
        }

        private void ApplyThemeToExistingUi()
        {
            if (panel == null || consumerRoot == null)
            {
                return;
            }

            var panelSurface = panel.GetComponent<QuestUiSurface>();
            if (panelSurface != null)
            {
                panelSurface.color = ScreenBackground;
            }
            else
            {
                var panelImage = panel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = ScreenBackground;
                }
            }

            foreach (var binding in themeBindings)
            {
                if (binding.Key == null)
                {
                    continue;
                }

                var color = GetThemeColor(binding.Value);
                if (binding.Key is Graphic graphic)
                {
                    graphic.color = color;
                }
                else if (binding.Key is Outline outline)
                {
                    outline.effectColor = color;
                }
            }

            var buttons = consumerRoot.GetComponentsInChildren<Button>(true);
            for (var index = 0; index < buttons.Length; index += 1)
            {
                var button = buttons[index];
                if (button != null && button.transition == Selectable.Transition.ColorTint && button.targetGraphic != null)
                {
                    button.colors = CreateButtonColors(button.targetGraphic.color);
                }
            }

            var toggles = consumerRoot.GetComponentsInChildren<Toggle>(true);
            for (var index = 0; index < toggles.Length; index += 1)
            {
                var toggle = toggles[index];
                if (toggle == null)
                {
                    continue;
                }

                if (toggle.TryGetComponent<QuestUiSwitchVisual>(out var switchVisual))
                {
                    switchVisual.SetPalette(palette, true);
                }
                else if (toggle.targetGraphic != null)
                {
                    toggle.colors = CreateButtonColors(toggle.targetGraphic.color);
                }
            }
        }

        private void SuppressSearchResults(bool suppressed)
        {
            if (!suppressed)
            {
                return;
            }

            for (var index = 0; index < searchResultRows.Length; index += 1)
            {
                searchResultRows[index]?.gameObject.SetActive(false);
            }
        }

        private void BindTheme(UnityEngine.Object target, ThemeColorRole role)
        {
            if (target != null)
            {
                themeBindings[target] = role;
            }
        }

        private ThemeColorRole ResolveThemeColorRole(Color color)
        {
            if (color.a <= 0.001f)
            {
                return ThemeColorRole.Clear;
            }

            var roles = (ThemeColorRole[])Enum.GetValues(typeof(ThemeColorRole));
            for (var index = 1; index < roles.Length; index += 1)
            {
                if (Approximately(color, GetThemeColor(roles[index])))
                {
                    return roles[index];
                }
            }

            return ThemeColorRole.Clear;
        }

        private Color GetThemeColor(ThemeColorRole role)
        {
            return role switch
            {
                ThemeColorRole.ScreenBackground => palette.ScreenBackground,
                ThemeColorRole.Surface => palette.Surface,
                ThemeColorRole.SurfaceRaised => palette.SurfaceRaised,
                ThemeColorRole.Line => palette.Line,
                ThemeColorRole.TextPrimary => palette.TextPrimary,
                ThemeColorRole.TextSecondary => palette.TextSecondary,
                ThemeColorRole.TextFaint => palette.TextFaint,
                ThemeColorRole.Accent => palette.Accent,
                ThemeColorRole.AccentStrong => palette.AccentStrong,
                ThemeColorRole.AccentInk => palette.AccentInk,
                ThemeColorRole.Warm => palette.Warm,
                ThemeColorRole.WarmSurface => palette.WarmSurface,
                ThemeColorRole.Danger => palette.Danger,
                ThemeColorRole.DangerSurface => palette.DangerSurface,
                ThemeColorRole.DangerBorder => palette.DangerBorder,
                ThemeColorRole.BrandBackground => palette.BrandBackground,
                ThemeColorRole.BrandAccent => palette.BrandAccent,
                ThemeColorRole.BrandWarm => palette.BrandWarm,
                ThemeColorRole.PendingSurface => palette.PendingSurface,
                ThemeColorRole.EnabledSurface => palette.EnabledSurface,
                ThemeColorRole.SafetySurface => palette.SafetySurface,
                ThemeColorRole.InactiveMeter => palette.InactiveMeter,
                ThemeColorRole.SliderTrack => palette.SliderTrack,
                ThemeColorRole.DebugScrim => palette.DebugScrim,
                ThemeColorRole.DrawerSurface => palette.DrawerSurface,
                ThemeColorRole.RawDetailsSurface => palette.RawDetailsSurface,
                ThemeColorRole.DiagnosticsActionSurface => palette.DiagnosticsActionSurface,
                _ => Color.clear,
            };
        }

        private static bool Approximately(Color left, Color right)
        {
            const float epsilon = 0.001f;
            return Mathf.Abs(left.r - right.r) <= epsilon &&
                   Mathf.Abs(left.g - right.g) <= epsilon &&
                   Mathf.Abs(left.b - right.b) <= epsilon &&
                   Mathf.Abs(left.a - right.a) <= epsilon;
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
            subscribedPlaylist = null;
        }

        private void HandlePlaylistStateChanged(QuestPlaylistPrototype sender, PlaylistState state)
        {
            ReconcileConfirmedSearchItems(state);
            TryCompleteEnqueueFeedback(sender);
            RefreshAll();
        }

        private void HandleSearchStateChanged(QuestPlaylistPrototype sender)
        {
            TryCompleteEnqueueFeedback(sender);
            RefreshSongSearch();
        }

        private void TryCompleteEnqueueFeedback(QuestPlaylistPrototype sender)
        {
            if (pendingEnqueueFeedbackItem == null || sender == null || sender.IsAddingItem)
            {
                return;
            }

            var item = pendingEnqueueFeedbackItem;
            var resultIndex = pendingEnqueueFeedbackIndex;
            pendingEnqueueFeedbackItem = null;
            pendingEnqueueFeedbackIndex = -1;

            var itemId = NormalizeCatalogItemId(item);
            var lastAddedId = NormalizeCatalogItemId(sender.LastAddedItem);
            var succeeded = !string.IsNullOrEmpty(itemId) &&
                            string.Equals(itemId, lastAddedId, StringComparison.OrdinalIgnoreCase) &&
                            string.IsNullOrWhiteSpace(sender.LastAddItemError);
            if (!succeeded)
            {
                return;
            }

            confirmedSearchItemIds.Add(itemId);
            StartEnqueueFeedback(resultIndex, item);
        }

        private void ReconcileConfirmedSearchItems(PlaylistState state)
        {
            if (state?.queue == null)
            {
                return;
            }

            var queuedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeStartIndex = state.currentIndex >= 0 ? state.currentIndex : 0;
            for (var index = activeStartIndex; index < state.queue.Length; index += 1)
            {
                var itemId = NormalizeCatalogItemId(state.queue[index]?.sourceInput);
                if (!string.IsNullOrEmpty(itemId))
                {
                    queuedIds.Add(itemId);
                }
            }

            if (state.queue.Length > 0 && queuedIds.Count == 0)
            {
                pendingQueueRemovalItemIds.Clear();
                return;
            }

            confirmedSearchItemIds.Clear();
            confirmedSearchItemIds.UnionWith(queuedIds);
            pendingQueueRemovalItemIds.Clear();
        }

        private void HandleSuggestStateChanged(QuestPlaylistPrototype sender)
        {
            RefreshSuggestions();
        }

        private void ShowPage(UiPage page)
        {
            if (currentPage == UiPage.MicProtection && page != UiPage.MicProtection)
            {
                CancelMicFaceCalibration();
            }
            if (currentPage == UiPage.SongSearch && page != UiPage.SongSearch)
            {
                songSearchInput?.GetComponent<QuestAndroidKeyboardInput>()?.HideKeyboard();
                HideSuggestions();
            }
            if (currentPage == UiPage.Settings && page != UiPage.Settings)
            {
                exitConfirmationExpiresAt = 0f;
            }

            SetMicFaceMouthMarkerForPage(page);

            SetQueueButtonVisibleForPage(page);

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
            SetPageGroupImmediate(settingsGroup, page == UiPage.Settings);
            SetPageGroupImmediate(roomAmbienceGroup, page == UiPage.RoomAmbience);
            SetPageGroupImmediate(stageLightingGroup, page == UiPage.StageLighting);
            SetPageGroupImmediate(micProtectionGroup, page == UiPage.MicProtection);
            SetPageGroupImmediate(micMouthPointGroup, page == UiPage.MicMouthPoint);
            SetMicFaceMouthMarkerForPage(page);
            SetQueueButtonVisibleForPage(page);
        }

        private void SetMicFaceMouthMarkerForPage(UiPage page)
        {
            if (handheldPropsPrototype == null)
            {
                handheldPropsPrototype = FindAnyObjectByType<QuestHandheldPropsPrototype>();
            }

            handheldPropsPrototype?.SetMicFaceMouthMarkerVisible(page == UiPage.MicMouthPoint);
        }

        private void SetQueueButtonVisibleForPage(UiPage page)
        {
            if (queueDrawerButton == null)
            {
                return;
            }

            var visible = page == UiPage.Home || page == UiPage.SongSearch;
            var buttonRect = queueDrawerButton.GetComponent<RectTransform>();
            var buttonX = page == UiPage.SongSearch
                ? ContentWidth * 0.5f - buttonRect.sizeDelta.x * 0.5f
                : 296f;
            buttonRect.anchoredPosition = new Vector2(buttonX, buttonRect.anchoredPosition.y);
            if (!visible && queueDrawerVisible)
            {
                SetQueueDrawerImmediate(false);
            }

            queueDrawerButton.gameObject.SetActive(visible);
        }

        private void ToggleQueueDrawer()
        {
            SetQueueDrawerVisible(!queueDrawerVisible);
        }

        private void SetQueueDrawerVisible(bool visible)
        {
            HideEnqueueConfirmation();
            if (visible)
            {
                var state = playlistPrototype?.CurrentState;
                var count = state?.QueueCount ?? 0;
                var currentIndex = state != null && state.currentIndex >= 0 && state.currentIndex < count
                    ? state.currentIndex
                    : -1;
                CenterQueueWindowOnCurrent(count, currentIndex);
                lastQueueCurrentIndex = currentIndex;
                RefreshQueue();
            }
            if (visible && debugDrawer != null && debugDrawer.gameObject.activeSelf)
            {
                SetDebugDrawerImmediate(false);
            }
            if (queueDrawerVisible == visible && queueDrawerTransition == null)
            {
                return;
            }

            if (!Application.isPlaying)
            {
                SetQueueDrawerImmediate(visible);
                return;
            }

            if (queueDrawerTransition != null)
            {
                StopCoroutine(queueDrawerTransition);
            }

            queueDrawerTransition = StartCoroutine(AnimateQueueDrawer(visible));
        }

        private IEnumerator AnimateQueueDrawer(bool visible)
        {
            if (visible)
            {
                queueScrim.gameObject.SetActive(true);
                queueDrawer.gameObject.SetActive(true);
                queueScrim.SetAsLastSibling();
                queueDrawer.SetAsLastSibling();
            }

            var visibleX = (QuestAppShellPrototype.ControlPanelSize.x - QueueDrawerWidth) * 0.5f;
            var hiddenX = QuestAppShellPrototype.ControlPanelSize.x * 0.5f + QueueDrawerWidth * 0.5f;
            var startAlpha = queueScrimGroup.alpha;
            var endAlpha = visible ? 1f : 0f;
            var startX = queueDrawer.anchoredPosition.x;
            var endX = visible ? visibleX : hiddenX;
            const float duration = 0.24f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - normalized, 3f);
                queueScrimGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                queueDrawerGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                queueDrawer.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, eased), 0f);
                yield return null;
            }

            SetQueueDrawerImmediate(visible);
            queueDrawerTransition = null;
        }

        private void SetQueueDrawerImmediate(bool visible)
        {
            queueDrawerVisible = visible;
            queueScrim.gameObject.SetActive(visible);
            queueDrawer.gameObject.SetActive(visible);
            queueScrimGroup.alpha = visible ? 1f : 0f;
            queueScrimGroup.interactable = visible;
            queueScrimGroup.blocksRaycasts = visible;
            queueDrawerGroup.alpha = visible ? 1f : 0f;
            queueDrawerGroup.interactable = visible;
            queueDrawerGroup.blocksRaycasts = visible;
            var visibleX = (QuestAppShellPrototype.ControlPanelSize.x - QueueDrawerWidth) * 0.5f;
            var hiddenX = QuestAppShellPrototype.ControlPanelSize.x * 0.5f + QueueDrawerWidth * 0.5f;
            queueDrawer.anchoredPosition = new Vector2(visible ? visibleX : hiddenX, 0f);
            if (queueDrawerButtonSurface != null)
            {
                queueDrawerButtonSurface.color = visible ? SurfaceHover : Surface;
            }
        }

        private void StartEnqueueFeedback(int resultIndex, BilibiliCatalogItem item)
        {
            if (enqueueFeedbackCoroutine != null)
            {
                StopCoroutine(enqueueFeedbackCoroutine);
            }
            HideEnqueueConfirmation();
            enqueueFlyer.gameObject.SetActive(false);
            enqueueFlyerImage.texture = null;
            queueBadge.localScale = Vector3.one;

            if (!Application.isPlaying)
            {
                ShowEnqueueConfirmation(item);
                return;
            }

            enqueueFeedbackCoroutine = StartCoroutine(AnimateEnqueueFeedback(resultIndex, item));
        }

        private IEnumerator AnimateEnqueueFeedback(int resultIndex, BilibiliCatalogItem item)
        {
            var canFly = !queueDrawerVisible &&
                         currentPage == UiPage.SongSearch &&
                         resultIndex >= 0 &&
                         resultIndex < SearchResultRowCount &&
                         searchResultRows[resultIndex] != null &&
                         searchResultRows[resultIndex].gameObject.activeInHierarchy &&
                         IsSearchResultIndex(resultIndex, item);
            if (canFly)
            {
                var sourceRect = searchResultCoverImages[resultIndex].rectTransform;
                var sourceBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(consumerRoot, sourceRect);
                var targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    consumerRoot,
                    queueDrawerButton.GetComponent<RectTransform>());
                var start = new Vector2(sourceBounds.center.x, sourceBounds.center.y);
                var target = new Vector2(targetBounds.center.x, targetBounds.center.y);
                var control = Vector2.Lerp(start, target, 0.52f) + Vector2.up * 96f;
                var sourceTexture = searchResultCoverImages[resultIndex].texture;

                enqueueFlyer.gameObject.SetActive(true);
                enqueueFlyer.SetAsLastSibling();
                enqueueFlyerGroup.alpha = 0f;
                enqueueFlyerGroup.interactable = false;
                enqueueFlyerGroup.blocksRaycasts = false;
                enqueueFlyer.anchoredPosition = start;
                enqueueFlyer.localScale = Vector3.one * 0.92f;
                enqueueFlyerImage.texture = sourceTexture;
                enqueueFlyerImage.uvRect = searchResultCoverImages[resultIndex].uvRect;
                enqueueFlyerImage.enabled = sourceTexture != null;
                enqueueFlyerFallbackIcon.gameObject.SetActive(sourceTexture == null);

                const float duration = 0.72f;
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var normalized = Mathf.Clamp01(elapsed / duration);
                    var eased = 1f - Mathf.Pow(1f - normalized, 3f);
                    var oneMinus = 1f - eased;
                    enqueueFlyer.anchoredPosition = oneMinus * oneMinus * start +
                                                     2f * oneMinus * eased * control +
                                                     eased * eased * target;
                    enqueueFlyer.localScale = Vector3.one * Mathf.Lerp(0.92f, 0.24f, eased);
                    enqueueFlyerGroup.alpha = normalized < 0.15f
                        ? normalized / 0.15f
                        : Mathf.Clamp01(1f - (normalized - 0.72f) / 0.28f);
                    yield return null;
                }

                enqueueFlyerImage.texture = null;
                enqueueFlyer.gameObject.SetActive(false);
            }

            if (!queueDrawerVisible)
            {
                ShowEnqueueConfirmation(item);
            }
            yield return AnimateQueueBadgePulse();
            enqueueFeedbackCoroutine = null;
        }

        private IEnumerator AnimateQueueBadgePulse()
        {
            queueBadge.gameObject.SetActive(true);
            const float duration = 0.42f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var pulse = Mathf.Sin(normalized * Mathf.PI);
                queueBadge.localScale = Vector3.one * (1f + pulse * 0.38f);
                if (queueDrawerButtonSurface != null)
                {
                    queueDrawerButtonSurface.color = Color.Lerp(Surface, Accent, pulse * 0.7f);
                }
                yield return null;
            }

            queueBadge.localScale = Vector3.one;
            if (queueDrawerButtonSurface != null)
            {
                queueDrawerButtonSurface.color = queueDrawerVisible ? SurfaceHover : Surface;
            }
        }

        private void ShowEnqueueConfirmation(BilibiliCatalogItem item)
        {
            enqueueConfirmationTitleText.text = $"《{SafeText(item?.title, item?.bvid)}》已加入";
            enqueueConfirmationDetailText.text = BuildEnqueueConfirmationDetail(item);
            if (enqueueConfirmationCoroutine != null)
            {
                StopCoroutine(enqueueConfirmationCoroutine);
            }

            if (!Application.isPlaying)
            {
                SetEnqueueConfirmationImmediate(true);
                return;
            }

            enqueueConfirmationCoroutine = StartCoroutine(AnimateEnqueueConfirmation());
        }

        private IEnumerator AnimateEnqueueConfirmation()
        {
            enqueueConfirmation.gameObject.SetActive(true);
            enqueueConfirmation.SetAsLastSibling();
            enqueueConfirmationGroup.interactable = true;
            enqueueConfirmationGroup.blocksRaycasts = true;
            enqueueConfirmationGroup.alpha = 0f;
            var target = new Vector2(118f, 232f);
            enqueueConfirmation.anchoredPosition = target + Vector2.up * 8f;
            const float transitionDuration = 0.16f;
            var elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / transitionDuration), 3f);
                enqueueConfirmationGroup.alpha = eased;
                enqueueConfirmation.anchoredPosition = Vector2.Lerp(target + Vector2.up * 8f, target, eased);
                yield return null;
            }

            enqueueConfirmationGroup.alpha = 1f;
            enqueueConfirmation.anchoredPosition = target;
            var visibleElapsed = 0f;
            while (visibleElapsed < EnqueueConfirmationSeconds)
            {
                visibleElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                enqueueConfirmationGroup.alpha = 1f - Mathf.Clamp01(elapsed / transitionDuration);
                yield return null;
            }

            SetEnqueueConfirmationImmediate(false);
            enqueueConfirmationCoroutine = null;
        }

        private void HideEnqueueConfirmation()
        {
            if (enqueueConfirmationCoroutine != null)
            {
                StopCoroutine(enqueueConfirmationCoroutine);
                enqueueConfirmationCoroutine = null;
            }
            if (enqueueConfirmation != null)
            {
                SetEnqueueConfirmationImmediate(false);
            }
        }

        private void SetEnqueueConfirmationImmediate(bool visible)
        {
            enqueueConfirmation.gameObject.SetActive(visible);
            enqueueConfirmation.anchoredPosition = new Vector2(118f, 232f);
            enqueueConfirmationGroup.alpha = visible ? 1f : 0f;
            enqueueConfirmationGroup.interactable = visible;
            enqueueConfirmationGroup.blocksRaycasts = visible;
        }

        private void SetDebugDrawerVisible(bool visible)
        {
            if (visible && queueDrawerVisible)
            {
                SetQueueDrawerImmediate(false);
            }
            if (!Application.isPlaying)
            {
                SetDebugDrawerImmediate(visible);
                return;
            }

            if (debugDrawerTransition != null)
            {
                StopCoroutine(debugDrawerTransition);
            }

            debugDrawerTransition = StartCoroutine(AnimateDebugDrawer(visible));
        }

        private IEnumerator AnimateDebugDrawer(bool visible)
        {
            if (visible)
            {
                debugScrim.gameObject.SetActive(true);
                debugDrawer.gameObject.SetActive(true);
                debugScrim.SetAsLastSibling();
                debugDrawer.SetAsLastSibling();
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
            debugDrawerTransition = null;
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
            if (!visible)
            {
                clearMediaCacheConfirmationExpiresAt = 0f;
                restoreDefaultSettingsConfirmationExpiresAt = 0f;
            }
        }

        private CanvasGroup GetPageGroup(UiPage page)
        {
            return page switch
            {
                UiPage.Voice => voiceGroup,
                UiPage.SongSearch => songSearchGroup,
                UiPage.Settings => settingsGroup,
                UiPage.RoomAmbience => roomAmbienceGroup,
                UiPage.StageLighting => stageLightingGroup,
                UiPage.MicProtection => micProtectionGroup,
                UiPage.MicMouthPoint => micMouthPointGroup,
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

        private Button CreateLabeledIconButton(
            Transform parent,
            string name,
            string label,
            QuestUiIconKind iconKind,
            Vector2 position,
            Vector2 size,
            Color background,
            Color iconColor,
            Color textColor,
            out QuestUiIcon icon)
        {
            var button = CreateSurfaceButton(parent, name, position, size, background, Line);
            var iconSize = Mathf.Min(24f, size.y * 0.46f);
            var iconX = -size.x * 0.5f + 14f + iconSize * 0.5f;
            icon = EnsureIcon(button.transform, "Icon", iconKind, new Vector2(iconX, 0f), new Vector2(iconSize, iconSize), iconColor);

            var labelLeft = iconX + iconSize * 0.5f + 8f;
            var labelRight = size.x * 0.5f - 12f;
            var labelWidth = Mathf.Max(1f, labelRight - labelLeft);
            CreateText(
                button.transform,
                "Label",
                label,
                15,
                FontStyle.Bold,
                new Vector2((labelLeft + labelRight) * 0.5f, 0f),
                new Vector2(labelWidth, size.y - 12f),
                TextAnchor.MiddleCenter,
                textColor);
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
            BindTheme(outline, ResolveThemeColorRole(border));
            var button = GetOrAddComponent<Button>(rect.gameObject);
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateButtonColors(background);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private Slider CreateSlider(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            bool showHandle,
            Color? fillColor = null,
            Color? handleColor = null,
            float trackThickness = 10f)
        {
            var resolvedFillColor = fillColor ?? Accent;
            var resolvedHandleColor = handleColor ?? AccentStrong;
            trackThickness = Mathf.Clamp(trackThickness, 1f, size.y);
            var trackSize = new Vector2(size.x, trackThickness);
            var trackRadius = trackThickness * 0.5f;
            var root = EnsureRect(parent, name, position, size);
            var backgroundRect = EnsureRect(root, "Track", Vector2.zero, trackSize);
            var background = EnsureSurface(backgroundRect, palette.SliderTrack, trackRadius, true);
            var fillArea = EnsureRect(root, "Fill Area", Vector2.zero, trackSize);
            var fillRect = EnsureRect(fillArea, "Fill", Vector2.zero, fillArea.sizeDelta);
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(1f, 0.5f);
            fillRect.sizeDelta = new Vector2(0f, trackThickness);
            var fill = EnsureSurface(fillRect, resolvedFillColor, trackRadius, false);
            RectTransform handleRect = null;
            QuestUiSurface handle = null;
            if (showHandle)
            {
                var handleArea = EnsureRect(root, "Handle Slide Area", Vector2.zero, new Vector2(size.x - 20f, size.y));
                handleRect = EnsureRect(handleArea, "Handle", Vector2.zero, new Vector2(24f, 24f));
                handle = EnsureSurface(handleRect, resolvedHandleColor, 12f, true);
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
            var root = EnsureRect(parent, name, position, new Vector2(72f, 48f));
            var legacyKnob = root.Find("Knob");
            if (legacyKnob != null)
            {
                RetireLegacyNode(legacyKnob, "Knob");
            }

            var hitArea = EnsureSurface(root, Color.clear, 8f, true);
            var visualRoot = EnsureRect(root, "Visual", Vector2.zero, new Vector2(60f, 32f));
            var focusRingRect = EnsureRect(visualRoot, "Focus Ring", Vector2.zero, new Vector2(68f, 40f));
            var focusRingSurface = EnsureSurface(focusRingRect, Color.clear, 20f, false);
            var borderRect = EnsureRect(visualRoot, "Border", Vector2.zero, new Vector2(62f, 34f));
            var borderSurface = EnsureSurface(borderRect, palette.SwitchOffBorder, 17f, false);
            var trackRect = EnsureRect(visualRoot, "Track", Vector2.zero, new Vector2(60f, 32f));
            var trackSurface = EnsureSurface(trackRect, palette.SwitchOffTrack, 16f, false);
            var knob = EnsureRect(visualRoot, "Knob", new Vector2(-14f, 0f), new Vector2(24f, 24f));
            var knobSurface = EnsureSurface(knob, palette.SwitchOffThumb, 12f, false);
            var knobShadow = GetOrAddComponent<Shadow>(knob.gameObject);
            knobShadow.effectColor = palette.SwitchThumbShadow;
            knobShadow.effectDistance = new Vector2(0f, -2f);
            knobShadow.useGraphicAlpha = true;

            var toggle = GetOrAddComponent<Toggle>(root.gameObject);
            toggle.targetGraphic = hitArea;
            toggle.graphic = null;
            toggle.transition = Selectable.Transition.None;
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };

            var switchVisual = GetOrAddComponent<QuestUiSwitchVisual>(root.gameObject);
            switchVisual.Configure(
                toggle,
                visualRoot,
                knob,
                focusRingSurface,
                borderSurface,
                trackSurface,
                knobSurface,
                knobShadow,
                palette);
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
            QuestAndroidKeyboardInput.Configure(input, roomPrototype);
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
            BindTheme(text, ResolveThemeColorRole(color));
            return text;
        }

        private QuestUiIcon EnsureIcon(Transform parent, string name, QuestUiIconKind kind, Vector2 position, Vector2 size, Color color)
        {
            var rect = EnsureRect(parent, name, position, size);
            var icon = GetOrAddComponent<QuestUiIcon>(rect.gameObject);
            icon.SetIcon(kind);
            icon.color = color;
            icon.raycastTarget = false;
            BindTheme(icon, ResolveThemeColorRole(color));
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

        private QuestUiSurface EnsureSurface(RectTransform rect, Color color, float radius, bool raycastTarget)
        {
            var surface = GetOrAddComponent<QuestUiSurface>(rect.gameObject);
            surface.color = color;
            surface.SetCornerRadius(radius);
            surface.raycastTarget = raycastTarget;
            BindTheme(surface, ResolveThemeColorRole(color));
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

        private ColorBlock CreateButtonColors(Color normal)
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

        private void RefreshSwitchVisual(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            if (toggle.TryGetComponent<QuestUiSwitchVisual>(out var switchVisual))
            {
                switchVisual.SetPalette(palette);
                switchVisual.RefreshState();
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
            if (item.status == DirectPlaylist.StatusDownloading)
            {
                return "正在准备";
            }

            if (item.status == DirectPlaylist.StatusError)
            {
                return "准备失败";
            }

            return string.Equals(playback, "playing", StringComparison.OrdinalIgnoreCase) ? "播放中" : "已暂停";
        }

        private static string FormatItemStatus(string status)
        {
            return status switch
            {
                DirectPlaylist.StatusReady => "已就绪",
                DirectPlaylist.StatusDownloading => "正在准备",
                DirectPlaylist.StatusError => "准备失败",
                _ => "等待中",
            };
        }

        private static string FormatQueueItemMeta(PlaylistItem item)
        {
            if (item == null)
            {
                return "等待中";
            }

            var author = string.IsNullOrWhiteSpace(item.author)
                ? FormatSource(item.sourceType)
                : item.author.Trim();
            var detail = item.status == DirectPlaylist.StatusDownloading
                ? "正在准备"
                : string.IsNullOrWhiteSpace(item.durationText)
                ? FormatItemStatus(item.status)
                : item.durationText.Trim();
            return $"{author} · {detail}";
        }

        private static string FormatProgress(float progress)
        {
            return $"{Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%";
        }

        private static string FormatBytes(long bytes)
        {
            var value = Math.Max(0L, bytes);
            if (value < 1024L)
            {
                return $"{value} B";
            }

            var scaled = (double)value;
            var units = new[] { "B", "KB", "MB", "GB", "TB" };
            var unitIndex = 0;
            while (scaled >= 1024d && unitIndex < units.Length - 1)
            {
                scaled /= 1024d;
                unitIndex += 1;
            }

            return $"{scaled:0.#} {units[unitIndex]}";
        }

        private static string NormalizeCatalogItemId(BilibiliCatalogItem item)
        {
            return NormalizeCatalogItemId(item?.bvid);
        }

        private static string NormalizeCatalogItemId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            var marker = trimmed.IndexOf("BV", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
            {
                return string.Empty;
            }

            var end = marker + 2;
            while (end < trimmed.Length && char.IsLetterOrDigit(trimmed[end]))
            {
                end += 1;
            }
            return trimmed.Substring(marker, end - marker);
        }

        private bool TryFindActiveQueueItem(BilibiliCatalogItem catalogItem, out PlaylistItem queueItem)
        {
            queueItem = null;
            var state = playlistPrototype?.CurrentState;
            var queue = state?.queue;
            var catalogItemId = NormalizeCatalogItemId(catalogItem);
            if (queue == null || string.IsNullOrEmpty(catalogItemId))
            {
                return false;
            }

            var activeStartIndex = state.currentIndex >= 0 ? state.currentIndex : 0;
            for (var index = queue.Length - 1; index >= activeStartIndex; index -= 1)
            {
                var candidate = queue[index];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.id))
                {
                    continue;
                }

                if (string.Equals(
                        NormalizeCatalogItemId(candidate.sourceInput),
                        catalogItemId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    queueItem = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool IsSearchResultIndex(int index, BilibiliCatalogItem expectedItem)
        {
            var items = playlistPrototype?.SearchResults?.items;
            return items != null &&
                   index >= 0 &&
                   index < items.Length &&
                   string.Equals(
                       NormalizeCatalogItemId(items[index]),
                       NormalizeCatalogItemId(expectedItem),
                       StringComparison.OrdinalIgnoreCase);
        }

        private string BuildEnqueueConfirmationDetail(BilibiliCatalogItem item)
        {
            var state = playlistPrototype?.CurrentState;
            var queue = state?.queue;
            var itemId = NormalizeCatalogItemId(item);
            if (queue == null || queue.Length == 0 || string.IsNullOrEmpty(itemId))
            {
                return "已加入播放队列";
            }

            for (var index = queue.Length - 1; index >= 0; index -= 1)
            {
                if (!string.Equals(
                        NormalizeCatalogItemId(queue[index]?.sourceInput),
                        itemId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (index == state.currentIndex)
                {
                    return "正在准备播放";
                }

                if (state.currentIndex >= 0 && index > state.currentIndex)
                {
                    return $"将在 {index - state.currentIndex} 首后播放";
                }
                break;
            }

            return $"队列现有 {queue.Length} 首";
        }

        private void SetQueueCover(int slot, string url)
        {
            if (slot < 0 || slot >= QueueCoverSlotCount)
            {
                return;
            }

            var resolvedUrl = string.IsNullOrWhiteSpace(url)
                ? string.Empty
                : playlistPrototype?.ResolveCatalogAssetUrl(url) ?? url.Trim();
            var normalizedUrl = string.IsNullOrWhiteSpace(resolvedUrl) ? string.Empty : resolvedUrl.Trim();
            var isSameUrl = string.Equals(queueCoverUrls[slot], normalizedUrl, StringComparison.Ordinal);
            if (isSameUrl &&
                (string.IsNullOrEmpty(normalizedUrl) ||
                 queueCoverTextures[slot] != null ||
                 queueCoverRequests[slot] != null ||
                 Time.unscaledTime < queueCoverRetryAfter[slot]))
            {
                return;
            }

            if (!isSameUrl)
            {
                CancelQueueCoverRequest(slot);
                ReleaseQueueCoverTexture(slot);
                queueCoverUrls[slot] = normalizedUrl;
                queueCoverRetryAfter[slot] = 0f;
            }

            var image = queueCoverImages[slot];
            if (image != null)
            {
                image.texture = null;
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                image.enabled = false;
            }

            if (string.IsNullOrEmpty(normalizedUrl) || !isActiveAndEnabled)
            {
                return;
            }

            queueCoverRequests[slot] = StartCoroutine(LoadQueueCover(slot, normalizedUrl));
        }

        private IEnumerator LoadQueueCover(int slot, string url)
        {
            Texture2D loadedTexture = null;
            using (var request = UnityWebRequestTexture.GetTexture(url, true))
            {
                request.timeout = 10;
                if (Uri.TryCreate(url, UriKind.Absolute, out var coverUri) &&
                    coverUri.Host.EndsWith("hdslb.com", StringComparison.OrdinalIgnoreCase))
                {
                    request.SetRequestHeader("Referer", "https://www.bilibili.com/");
                }

                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    loadedTexture = DownloadHandlerTexture.GetContent(request);
                }
            }

            if (!string.Equals(queueCoverUrls[slot], url, StringComparison.Ordinal))
            {
                DestroySearchResultCoverTexture(loadedTexture);
                yield break;
            }

            queueCoverRequests[slot] = null;
            if (loadedTexture == null)
            {
                queueCoverRetryAfter[slot] = Time.unscaledTime + SearchResultCoverRetrySeconds;
                yield break;
            }

            loadedTexture.wrapMode = TextureWrapMode.Clamp;
            loadedTexture.filterMode = FilterMode.Bilinear;
            ReleaseQueueCoverTexture(slot);
            queueCoverTextures[slot] = loadedTexture;

            var image = queueCoverImages[slot];
            if (image != null)
            {
                image.texture = loadedTexture;
                image.uvRect = CalculateCoverUvRect(loadedTexture);
                image.enabled = true;
            }
        }

        private void CancelQueueCoverRequests()
        {
            for (var slot = 0; slot < QueueCoverSlotCount; slot += 1)
            {
                CancelQueueCoverRequest(slot);
            }
        }

        private void CancelQueueCoverRequest(int slot)
        {
            var request = queueCoverRequests[slot];
            if (request == null)
            {
                return;
            }

            StopCoroutine(request);
            queueCoverRequests[slot] = null;
        }

        private void ReleaseQueueCovers()
        {
            CancelQueueCoverRequests();
            for (var slot = 0; slot < QueueCoverSlotCount; slot += 1)
            {
                var image = queueCoverImages[slot];
                if (image != null)
                {
                    image.texture = null;
                    image.enabled = false;
                }

                ReleaseQueueCoverTexture(slot);
                queueCoverUrls[slot] = string.Empty;
                queueCoverRetryAfter[slot] = 0f;
            }
        }

        private void ReleaseQueueCoverTexture(int slot)
        {
            var texture = queueCoverTextures[slot];
            queueCoverTextures[slot] = null;
            DestroySearchResultCoverTexture(texture);
        }

        private void SetSearchResultCover(int index, string url)
        {
            if (index < 0 || index >= SearchResultRowCount)
            {
                return;
            }

            var normalizedUrl = string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim();
            var isSameUrl = string.Equals(
                searchResultCoverUrls[index],
                normalizedUrl,
                StringComparison.Ordinal);
            if (isSameUrl &&
                (string.IsNullOrEmpty(normalizedUrl) ||
                 searchResultCoverTextures[index] != null ||
                 searchResultCoverRequests[index] != null ||
                 Time.unscaledTime < searchResultCoverRetryAfter[index]))
            {
                return;
            }

            if (!isSameUrl)
            {
                CancelSearchResultCoverRequest(index);
                ReleaseSearchResultCoverTexture(index);
                searchResultCoverUrls[index] = normalizedUrl;
                searchResultCoverRetryAfter[index] = 0f;
            }

            var image = searchResultCoverImages[index];
            if (image != null)
            {
                image.texture = null;
                image.uvRect = new Rect(0f, 0f, 1f, 1f);
                image.enabled = false;
            }

            if (string.IsNullOrEmpty(normalizedUrl) || !isActiveAndEnabled)
            {
                return;
            }

            searchResultCoverRequests[index] = StartCoroutine(LoadSearchResultCover(index, normalizedUrl));
        }

        private IEnumerator LoadSearchResultCover(int index, string url)
        {
            Texture2D loadedTexture = null;
            using (var request = UnityWebRequestTexture.GetTexture(url, true))
            {
                request.timeout = 10;
                if (Uri.TryCreate(url, UriKind.Absolute, out var coverUri) &&
                    coverUri.Host.EndsWith("hdslb.com", StringComparison.OrdinalIgnoreCase))
                {
                    request.SetRequestHeader("Referer", "https://www.bilibili.com/");
                }

                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    loadedTexture = DownloadHandlerTexture.GetContent(request);
                }
            }

            if (!string.Equals(searchResultCoverUrls[index], url, StringComparison.Ordinal))
            {
                DestroySearchResultCoverTexture(loadedTexture);
                yield break;
            }

            searchResultCoverRequests[index] = null;
            if (loadedTexture == null)
            {
                searchResultCoverRetryAfter[index] = Time.unscaledTime + SearchResultCoverRetrySeconds;
                yield break;
            }

            loadedTexture.wrapMode = TextureWrapMode.Clamp;
            loadedTexture.filterMode = FilterMode.Bilinear;
            ReleaseSearchResultCoverTexture(index);
            searchResultCoverTextures[index] = loadedTexture;

            var image = searchResultCoverImages[index];
            if (image != null)
            {
                image.texture = loadedTexture;
                image.uvRect = CalculateCoverUvRect(loadedTexture);
                image.enabled = true;
            }
        }

        private void RefreshSearchResultAddButton(int index, bool isPending, bool isConfirmed, bool isRemoving)
        {
            var button = searchResultAddButtons[index];
            var icon = searchResultAddIcons[index];
            var label = searchResultAddTexts[index];
            if (button == null || icon == null || label == null)
            {
                return;
            }

            var surface = button.targetGraphic as QuestUiSurface;
            if (isRemoving)
            {
                button.interactable = false;
                button.transition = Selectable.Transition.None;
                if (surface != null)
                {
                    surface.color = palette.PendingSurface;
                }
                icon.SetIcon(QuestUiIconKind.Trash);
                icon.color = TextSecondary;
                label.text = "移除中";
                label.color = TextSecondary;
                return;
            }

            if (isConfirmed)
            {
                button.interactable = playlistPrototype.CanSendControl;
                button.transition = Selectable.Transition.ColorTint;
                button.colors = CreateButtonColors(Accent);
                if (surface != null)
                {
                    surface.color = Accent;
                }
                icon.SetIcon(QuestUiIconKind.Check);
                icon.color = AccentInk;
                label.text = "已点";
                label.color = AccentInk;
                return;
            }

            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateButtonColors(isPending ? palette.PendingSurface : SurfaceRaised);
            button.interactable = playlistPrototype.CanAddItem && !isPending;
            if (surface != null)
            {
                surface.color = isPending ? palette.PendingSurface : SurfaceRaised;
            }
            icon.SetIcon(isPending ? QuestUiIconKind.Queue : QuestUiIconKind.Plus);
            icon.color = isPending ? Accent : AccentStrong;
            label.text = isPending ? "加入中" : "点歌";
            label.color = isPending ? Accent : AccentStrong;
        }

        private static Rect CalculateCoverUvRect(Texture texture)
        {
            const float targetAspect = 3f / 2f;
            if (texture == null || texture.height <= 0)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            var sourceAspect = texture.width / (float)texture.height;
            if (sourceAspect > targetAspect)
            {
                var normalizedWidth = targetAspect / sourceAspect;
                return new Rect((1f - normalizedWidth) * 0.5f, 0f, normalizedWidth, 1f);
            }

            if (sourceAspect < targetAspect)
            {
                var normalizedHeight = sourceAspect / targetAspect;
                return new Rect(0f, (1f - normalizedHeight) * 0.5f, 1f, normalizedHeight);
            }

            return new Rect(0f, 0f, 1f, 1f);
        }

        private void CancelSearchResultCoverRequests()
        {
            for (var index = 0; index < searchResultCoverRequests.Length; index += 1)
            {
                CancelSearchResultCoverRequest(index);
            }
        }

        private void CancelSearchResultCoverRequest(int index)
        {
            var request = searchResultCoverRequests[index];
            if (request == null)
            {
                return;
            }

            StopCoroutine(request);
            searchResultCoverRequests[index] = null;
        }

        private void ReleaseSearchResultCovers()
        {
            CancelSearchResultCoverRequests();
            for (var index = 0; index < SearchResultRowCount; index += 1)
            {
                var image = searchResultCoverImages[index];
                if (image != null)
                {
                    image.texture = null;
                    image.enabled = false;
                }

                ReleaseSearchResultCoverTexture(index);
                searchResultCoverUrls[index] = string.Empty;
                searchResultCoverRetryAfter[index] = 0f;
            }
        }

        private void ReleaseSearchResultCoverTexture(int index)
        {
            var texture = searchResultCoverTextures[index];
            searchResultCoverTextures[index] = null;
            DestroySearchResultCoverTexture(texture);
        }

        private static void DestroySearchResultCoverTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }

        private static string FormatDuration(BilibiliCatalogItem item)
        {
            if (!string.IsNullOrWhiteSpace(item?.durationText))
            {
                return item.durationText.Trim();
            }

            var totalSeconds = Mathf.Max(0, item?.durationSeconds ?? 0);
            var hours = totalSeconds / 3600;
            var minutes = totalSeconds % 3600 / 60;
            var seconds = totalSeconds % 60;
            return hours > 0
                ? $"{hours}:{minutes:00}:{seconds:00}"
                : $"{minutes}:{seconds:00}";
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
