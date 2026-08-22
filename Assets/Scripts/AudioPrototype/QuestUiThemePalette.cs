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
        public Color DangerSurface { get; private set; }
        public Color DangerBorder { get; private set; }
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
        public Color SwitchOffTrack { get; private set; }
        public Color SwitchOffTrackHover { get; private set; }
        public Color SwitchOffBorder { get; private set; }
        public Color SwitchOffThumb { get; private set; }
        public Color SwitchDisabledTrack { get; private set; }
        public Color SwitchDisabledThumb { get; private set; }
        public Color SwitchFocusRing { get; private set; }
        public Color SwitchThumbShadow { get; private set; }

        public static QuestUiThemePalette For(RoomTheme theme)
        {
            return theme == RoomTheme.Bright ? Bright : Dark;
        }

        private static Color Rgb(uint hex, float alpha = 1f)
        {
            return new Color(
                ((hex >> 16) & 0xff) / 255f,
                ((hex >> 8) & 0xff) / 255f,
                (hex & 0xff) / 255f,
                alpha);
        }

        private static readonly QuestUiThemePalette Dark = new QuestUiThemePalette
        {
            ScreenBackground = Rgb(0x0B0D12),
            Surface = Rgb(0x11151D),
            SurfaceRaised = Rgb(0x1C2028),
            SurfaceHover = Rgb(0xEDE2CC),
            Line = Rgb(0x393B40),
            TextPrimary = Rgb(0xF2EFE8),
            TextSecondary = Rgb(0xA9A697),
            TextFaint = Rgb(0x6D6C69),
            Accent = Rgb(0xD8C49D),
            AccentStrong = Rgb(0xEDE2CC),
            AccentInk = Rgb(0x211D15),
            Warm = Rgb(0x929CAF),
            WarmSurface = Rgb(0x1A202B),
            Danger = Rgb(0xEF7075),
            DangerSurface = Rgb(0x24181D),
            DangerBorder = Rgb(0xC17478),
            BrandBackground = Rgb(0x111217),
            BrandAccent = Rgb(0xE2CFA9),
            BrandWarm = Rgb(0xB6A178),
            PendingSurface = Rgb(0x2A261E),
            EnabledSurface = Rgb(0x26231D),
            SafetySurface = Rgb(0x1B1D22),
            InactiveMeter = Rgb(0x353940),
            SliderTrack = Rgb(0x24272E),
            DebugScrim = Rgb(0x000000, 0.58f),
            DrawerSurface = Rgb(0x16191F),
            RawDetailsSurface = Rgb(0x0F1116),
            DiagnosticsActionSurface = Rgb(0x24231F),
            DockSurface = Rgb(0x111319, 0.98f),
            ButtonSurface = Rgb(0x1A1D24),
            ActiveSurface = Rgb(0xD8C49D),
            DockTextPrimary = Rgb(0xF2EFE8),
            DockTextSecondary = Rgb(0xA9A697),
            DockAccent = Rgb(0xD8C49D),
            DockAccentInk = Rgb(0x211D15),
            KeyboardPanel = Rgb(0x0D0F14, 0.99f),
            KeyboardKey = Rgb(0x1B1E25),
            KeyboardUtilityKey = Rgb(0x252831),
            KeyboardBorder = Rgb(0x4B4B47, 0.9f),
            ButtonHighlighted = Rgb(0xF0E6D2),
            ButtonPressed = Rgb(0xBCA77E),
            ButtonDisabled = Rgb(0x707071, 0.42f),
            KeyboardAccentKey = Rgb(0xD8C49D),
            KeyboardText = Rgb(0xF2EFE8),
            KeyboardAccentText = Rgb(0x211D15),
            KeyboardHighlighted = Rgb(0xF0E6D2),
            KeyboardPressed = Rgb(0xBCA77E),
            KeyboardDisabled = Rgb(0x707071, 0.45f),
            SwitchOffTrack = Rgb(0x24272E),
            SwitchOffTrackHover = Rgb(0x30333A),
            SwitchOffBorder = Rgb(0x4C4E53),
            SwitchOffThumb = Rgb(0xC6C3BA),
            SwitchDisabledTrack = Rgb(0x20232A),
            SwitchDisabledThumb = Rgb(0x696A6A),
            SwitchFocusRing = Rgb(0xD8C49D, 0.35f),
            SwitchThumbShadow = Rgb(0x000000, 0.36f),
        };

        private static readonly QuestUiThemePalette Bright = new QuestUiThemePalette
        {
            ScreenBackground = Rgb(0xF0F1F3),
            Surface = Rgb(0xFAFAF8),
            SurfaceRaised = Rgb(0xFFFFFF),
            SurfaceHover = Rgb(0xEEE7D9),
            Line = Rgb(0xCBCBC7),
            TextPrimary = Rgb(0x232427),
            TextSecondary = Rgb(0x66676C),
            TextFaint = Rgb(0x85868A),
            Accent = Rgb(0xB89E6D),
            AccentStrong = Rgb(0x796642),
            AccentInk = Rgb(0x211903),
            Warm = Rgb(0x59657B),
            WarmSurface = Rgb(0xE7EAF0),
            Danger = Rgb(0xC33B3F),
            DangerSurface = Rgb(0xF0E4E5),
            DangerBorder = Rgb(0x985156),
            BrandBackground = Rgb(0x2A2925),
            BrandAccent = Rgb(0xE3CFA8),
            BrandWarm = Rgb(0xA88F60),
            PendingSurface = Rgb(0xEFE7D6),
            EnabledSurface = Rgb(0xEEE8DC),
            SafetySurface = Rgb(0xE8E9EB),
            InactiveMeter = Rgb(0xADB0B6),
            SliderTrack = Rgb(0xC5C6C7),
            DebugScrim = Rgb(0x151515, 0.34f),
            DrawerSurface = Rgb(0xFAFAF8),
            RawDetailsSurface = Rgb(0xE6E7E8),
            DiagnosticsActionSurface = Rgb(0xEEE7D8),
            DockSurface = Rgb(0xE4E4E1, 0.98f),
            ButtonSurface = Rgb(0xF6F6F3),
            ActiveSurface = Rgb(0xB89E6D),
            DockTextPrimary = Rgb(0x232427),
            DockTextSecondary = Rgb(0x66676C),
            DockAccent = Rgb(0xB89E6D),
            DockAccentInk = Rgb(0x211903),
            KeyboardPanel = Rgb(0xF1F1EE, 0.99f),
            KeyboardKey = Rgb(0xFCFCFA),
            KeyboardUtilityKey = Rgb(0xEAEAE7),
            KeyboardBorder = Rgb(0xA8A9A8, 0.9f),
            ButtonHighlighted = Rgb(0xEEE2C9),
            ButtonPressed = Rgb(0xD2C098),
            ButtonDisabled = Rgb(0x8F9091, 0.42f),
            KeyboardAccentKey = Rgb(0xB89E6D),
            KeyboardText = Rgb(0x232427),
            KeyboardAccentText = Rgb(0x211903),
            KeyboardHighlighted = Rgb(0xF0E5CE),
            KeyboardPressed = Rgb(0xD0BE96),
            KeyboardDisabled = Rgb(0x8F9091, 0.42f),
            SwitchOffTrack = Rgb(0xDEDFE1),
            SwitchOffTrackHover = Rgb(0xD3D4D6),
            SwitchOffBorder = Rgb(0xB3B4B7),
            SwitchOffThumb = Rgb(0xFFFFFF),
            SwitchDisabledTrack = Rgb(0xE5E6E8),
            SwitchDisabledThumb = Rgb(0xBFC0C3),
            SwitchFocusRing = Rgb(0x9C814B, 0.28f),
            SwitchThumbShadow = Rgb(0x4D4E53, 0.18f),
        };
    }
}
