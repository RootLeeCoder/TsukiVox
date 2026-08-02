using UnityEngine;

namespace TsukiVox.AudioPrototype
{
    /// <summary>
    /// Shared semantic colors for the world-space consumer UI.
    /// </summary>
    public sealed class QuestUiThemePalette
    {
        public Color ScreenBackground { get; private set; }
        public Color Surface { get; private set; }
        public Color SurfaceRaised { get; private set; }
        public Color SurfaceHover { get; private set; }
        public Color Line { get; private set; }
        public Color TextPrimary { get; private set; }
        public Color TextSecondary { get; private set; }
        public Color TextFaint { get; private set; }
        public Color Accent { get; private set; }
        public Color AccentStrong { get; private set; }
        public Color AccentInk { get; private set; }
        public Color Warm { get; private set; }
        public Color WarmSurface { get; private set; }
        public Color Danger { get; private set; }
        public Color BrandBackground { get; private set; }
        public Color BrandAccent { get; private set; }
        public Color BrandWarm { get; private set; }
        public Color PendingSurface { get; private set; }
        public Color EnabledSurface { get; private set; }
        public Color SafetySurface { get; private set; }
        public Color InactiveMeter { get; private set; }
        public Color SliderTrack { get; private set; }
        public Color DebugScrim { get; private set; }
        public Color DrawerSurface { get; private set; }
        public Color RawDetailsSurface { get; private set; }
        public Color DiagnosticsActionSurface { get; private set; }
        public Color DockSurface { get; private set; }
        public Color ButtonSurface { get; private set; }
        public Color ActiveSurface { get; private set; }
        public Color DockTextPrimary { get; private set; }
        public Color DockTextSecondary { get; private set; }
        public Color DockAccent { get; private set; }
        public Color DockAccentInk { get; private set; }
        public Color KeyboardPanel { get; private set; }
        public Color KeyboardKey { get; private set; }
        public Color KeyboardUtilityKey { get; private set; }
        public Color KeyboardBorder { get; private set; }
        public Color ButtonHighlighted { get; private set; }
        public Color ButtonPressed { get; private set; }
        public Color ButtonDisabled { get; private set; }
        public Color KeyboardAccentKey { get; private set; }
        public Color KeyboardText { get; private set; }
        public Color KeyboardAccentText { get; private set; }
        public Color KeyboardHighlighted { get; private set; }
        public Color KeyboardPressed { get; private set; }
        public Color KeyboardDisabled { get; private set; }

        public static QuestUiThemePalette For(RoomTheme theme)
        {
            return theme == RoomTheme.Bright ? Bright : Dark;
        }

        private static readonly QuestUiThemePalette Dark = new QuestUiThemePalette
        {
            ScreenBackground = new Color(0.024f, 0.04f, 0.039f, 1f),
            Surface = new Color(0.048f, 0.073f, 0.071f, 1f),
            SurfaceRaised = new Color(0.068f, 0.098f, 0.094f, 1f),
            SurfaceHover = new Color(0.52f, 1f, 0.88f, 1f),
            Line = new Color(0.14f, 0.2f, 0.19f, 1f),
            TextPrimary = new Color(0.94f, 0.97f, 0.96f, 1f),
            TextSecondary = new Color(0.57f, 0.65f, 0.63f, 1f),
            TextFaint = new Color(0.37f, 0.42f, 0.41f, 1f),
            Accent = new Color(0.24f, 0.9f, 0.74f, 1f),
            AccentStrong = new Color(0.56f, 1f, 0.89f, 1f),
            AccentInk = new Color(0.012f, 0.075f, 0.059f, 1f),
            Warm = new Color(0.95f, 0.71f, 0.42f, 1f),
            WarmSurface = new Color(0.22f, 0.15f, 0.09f, 1f),
            Danger = new Color(0.94f, 0.44f, 0.44f, 1f),
            BrandBackground = new Color(0.031f, 0.035f, 0.043f, 1f),
            BrandAccent = new Color(0.196f, 0.902f, 0.765f, 1f),
            BrandWarm = new Color(1f, 0.741f, 0.447f, 1f),
            PendingSurface = new Color(0.035f, 0.16f, 0.13f, 1f),
            EnabledSurface = new Color(0.035f, 0.12f, 0.095f, 1f),
            SafetySurface = new Color(0.09f, 0.11f, 0.1f, 1f),
            InactiveMeter = new Color(0.18f, 0.26f, 0.25f, 1f),
            SliderTrack = new Color(0.12f, 0.17f, 0.16f, 1f),
            DebugScrim = new Color(0f, 0f, 0f, 0.58f),
            DrawerSurface = new Color(0.055f, 0.082f, 0.079f, 1f),
            RawDetailsSurface = new Color(0.035f, 0.052f, 0.051f, 1f),
            DiagnosticsActionSurface = new Color(0.035f, 0.11f, 0.09f, 1f),
            DockSurface = new Color(0.025f, 0.045f, 0.047f, 0.98f),
            ButtonSurface = new Color(0.07f, 0.095f, 0.1f, 1f),
            ActiveSurface = new Color(0.24f, 0.9f, 0.74f, 1f),
            DockTextPrimary = new Color(0.9f, 0.96f, 0.96f, 1f),
            DockTextSecondary = new Color(0.57f, 0.68f, 0.69f, 1f),
            DockAccent = new Color(0.25f, 0.95f, 0.72f, 1f),
            DockAccentInk = new Color(0.012f, 0.075f, 0.059f, 1f),
            KeyboardPanel = new Color(0.018f, 0.029f, 0.028f, 0.99f),
            KeyboardKey = new Color(0.09f, 0.13f, 0.125f, 1f),
            KeyboardUtilityKey = new Color(0.13f, 0.17f, 0.165f, 1f),
            KeyboardBorder = new Color(0.22f, 0.34f, 0.32f, 0.9f),
            ButtonHighlighted = new Color(0.72f, 1f, 0.9f, 1f),
            ButtonPressed = new Color(0.42f, 0.92f, 0.72f, 1f),
            ButtonDisabled = new Color(0.42f, 0.48f, 0.48f, 0.42f),
            KeyboardAccentKey = new Color(0.25f, 0.95f, 0.72f, 1f),
            KeyboardText = new Color(0.93f, 0.97f, 0.96f, 1f),
            KeyboardAccentText = new Color(0.025f, 0.12f, 0.09f, 1f),
            KeyboardHighlighted = new Color(0.8f, 1f, 0.94f, 1f),
            KeyboardPressed = new Color(0.72f, 0.92f, 0.82f, 1f),
            KeyboardDisabled = new Color(0.45f, 0.48f, 0.47f, 0.45f),
        };

        private static readonly QuestUiThemePalette Bright = new QuestUiThemePalette
        {
            ScreenBackground = new Color(0.91f, 0.895f, 0.86f, 1f),
            Surface = new Color(0.965f, 0.95f, 0.91f, 1f),
            SurfaceRaised = new Color(0.995f, 0.985f, 0.955f, 1f),
            SurfaceHover = new Color(0.78f, 0.96f, 0.89f, 1f),
            Line = new Color(0.72f, 0.69f, 0.63f, 1f),
            TextPrimary = new Color(0.09f, 0.12f, 0.115f, 1f),
            TextSecondary = new Color(0.28f, 0.33f, 0.315f, 1f),
            TextFaint = new Color(0.47f, 0.49f, 0.46f, 1f),
            Accent = new Color(0.24f, 0.9f, 0.74f, 1f),
            AccentStrong = new Color(0.08f, 0.64f, 0.5f, 1f),
            AccentInk = new Color(0.012f, 0.075f, 0.059f, 1f),
            Warm = new Color(0.78f, 0.48f, 0.16f, 1f),
            WarmSurface = new Color(1f, 0.91f, 0.76f, 1f),
            Danger = new Color(0.76f, 0.2f, 0.2f, 1f),
            BrandBackground = new Color(0.14f, 0.15f, 0.15f, 1f),
            BrandAccent = new Color(0.08f, 0.68f, 0.53f, 1f),
            BrandWarm = new Color(0.86f, 0.53f, 0.2f, 1f),
            PendingSurface = new Color(0.8f, 0.95f, 0.88f, 1f),
            EnabledSurface = new Color(0.82f, 0.94f, 0.88f, 1f),
            SafetySurface = new Color(0.91f, 0.92f, 0.84f, 1f),
            InactiveMeter = new Color(0.67f, 0.72f, 0.68f, 1f),
            SliderTrack = new Color(0.71f, 0.7f, 0.66f, 1f),
            DebugScrim = new Color(0.08f, 0.07f, 0.055f, 0.34f),
            DrawerSurface = new Color(0.975f, 0.96f, 0.925f, 1f),
            RawDetailsSurface = new Color(0.89f, 0.875f, 0.835f, 1f),
            DiagnosticsActionSurface = new Color(0.81f, 0.94f, 0.88f, 1f),
            DockSurface = new Color(0.89f, 0.87f, 0.82f, 0.98f),
            ButtonSurface = new Color(0.96f, 0.945f, 0.91f, 1f),
            ActiveSurface = new Color(0.24f, 0.9f, 0.74f, 1f),
            DockTextPrimary = new Color(0.09f, 0.12f, 0.115f, 1f),
            DockTextSecondary = new Color(0.32f, 0.37f, 0.35f, 1f),
            DockAccent = new Color(0.08f, 0.68f, 0.53f, 1f),
            DockAccentInk = new Color(0.012f, 0.075f, 0.059f, 1f),
            KeyboardPanel = new Color(0.94f, 0.925f, 0.89f, 0.99f),
            KeyboardKey = new Color(0.985f, 0.975f, 0.945f, 1f),
            KeyboardUtilityKey = new Color(0.9f, 0.885f, 0.845f, 1f),
            KeyboardBorder = new Color(0.62f, 0.66f, 0.62f, 0.9f),
            ButtonHighlighted = new Color(0.74f, 0.96f, 0.87f, 1f),
            ButtonPressed = new Color(0.52f, 0.88f, 0.73f, 1f),
            ButtonDisabled = new Color(0.56f, 0.57f, 0.54f, 0.42f),
            KeyboardAccentKey = new Color(0.24f, 0.9f, 0.74f, 1f),
            KeyboardText = new Color(0.09f, 0.12f, 0.115f, 1f),
            KeyboardAccentText = new Color(0.012f, 0.075f, 0.059f, 1f),
            KeyboardHighlighted = new Color(0.74f, 0.96f, 0.87f, 1f),
            KeyboardPressed = new Color(0.52f, 0.88f, 0.73f, 1f),
            KeyboardDisabled = new Color(0.56f, 0.57f, 0.54f, 0.42f),
        };
    }
}