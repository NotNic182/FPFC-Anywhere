using System;

namespace FpfcAnywhere.Core
{
    /// <summary>
    /// The type every overlay uses: the game's own Roboto, so a notice and the scoreboard read as one
    /// thing and sit with the game's menus. One family, two weights. Regular carries words, Bold
    /// carries the figures a viewer compares at a glance.
    /// </summary>
    public static class OverlayType
    {
        /// <summary>Notices and player names.</summary>
        public const string TextFont = "Roboto-Regular";

        /// <summary>Ranks, scores and how far behind first place someone is.</summary>
        public const string FiguresFont = "Roboto-Bold";

        /// <summary>How long to wait before looking for fonts the game hasn't loaded yet.</summary>
        public const float RetrySeconds = 1f;
    }

    /// <summary>
    /// The look both overlays share: a dark plate under white text, dimmer text for what matters
    /// less, and gold for the leader. Opacities are the alpha a colour is drawn with.
    /// </summary>
    public static class OverlayLook
    {
        /// <summary>How dark the plate is. Measured in game, it keeps the rest of what's behind it.</summary>
        public const float PlateOpacity = 0.75f;

        /// <summary>How far behind first place someone is.</summary>
        public const float GapOpacity = 0.7f;

        /// <summary>A player who failed. Dimmer than the gap, still readable over a white scene.</summary>
        public const float OutOpacity = 0.6f;

        /// <summary>The leader's gold, as it should look on screen.</summary>
        public const float LeaderRed = 1f;
        public const float LeaderGreen = 0.82f;
        public const float LeaderBlue = 0.3f;

        /// <summary>
        /// White text at this opacity on the plate, with pure white behind both, which is the worst a
        /// scene can do. The plate and text mix in screen values, as measured from captures.
        /// </summary>
        public static double WorstContrast(float textOpacity)
        {
            var behind = 1.0 - PlateOpacity;
            var text = textOpacity + ((1.0 - textOpacity) * behind);
            return (Luminance(text) + 0.05) / (Luminance(behind) + 0.05);
        }

        /// <summary>
        /// The value to give Unity's GUI so the screen shows the colour wanted. In a linear colour
        /// project Unity turns GUI colours into linear values and the window never turns them back.
        /// </summary>
        public static float ForGui(float display, bool linearProject)
        {
            if (!linearProject)
            {
                return display;
            }

            return display <= 0.0031308f ? display * 12.92f : (float)((1.055 * Math.Pow(display, 1.0 / 2.4)) - 0.055);
        }

        /// <summary>A screen value as light, the standard sRGB curve.</summary>
        public static float ToLinear(float display) =>
            display <= 0.04045f ? display / 12.92f : (float)Math.Pow((display + 0.055) / 1.055, 2.4);

        private static double Luminance(double grey) => ToLinear((float)grey);
    }

    /// <summary>
    /// Decides when to look for the game's fonts. Looking means going through every loaded font, too
    /// slow for every frame, and the startup notices arrive before the game has loaded its fonts at
    /// all. Those use Unity's built-in font until the search finds the real ones.
    /// </summary>
    public sealed class FontSearch
    {
        private readonly float _retrySeconds;
        private float? _lastLook;

        public FontSearch(float retrySeconds)
        {
            _retrySeconds = Math.Max(retrySeconds, 0f);
        }

        public bool ShouldLook(float now, bool haveFonts)
        {
            if (haveFonts)
            {
                return false;
            }

            if (_lastLook is float last && now - last < _retrySeconds && now >= last)
            {
                return false;
            }

            _lastLook = now;
            return true;
        }
    }
}
