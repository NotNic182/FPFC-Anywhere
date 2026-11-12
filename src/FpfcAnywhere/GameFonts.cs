using System.Linq;
using FpfcAnywhere.Core;
using UnityEngine;

namespace FpfcAnywhere
{
    /// <summary>
    /// The game's Roboto, shared by both overlays. The fonts belong to the game, which can load them
    /// late and drop them again, so a missing one means Unity's built-in font for now, never no text.
    /// </summary>
    internal static class GameFonts
    {
        private static readonly FontSearch Search = new FontSearch(OverlayType.RetrySeconds);
        private static Font? _text;
        private static Font? _figures;

        /// <summary>Notices and player names. Null until the game has loaded it.</summary>
        public static Font? Text
        {
            get
            {
                Refresh();
                return _text;
            }
        }

        /// <summary>Ranks, scores and gaps. Null until the game has loaded it.</summary>
        public static Font? Figures
        {
            get
            {
                Refresh();
                return _figures;
            }
        }

        // Unity's own comparison, so a font the game has destroyed counts as missing.
        private static void Refresh()
        {
            if (!Search.ShouldLook(Time.unscaledTime, _text != null && _figures != null))
            {
                return;
            }

            var fonts = Resources.FindObjectsOfTypeAll<Font>();
            _text = fonts.FirstOrDefault(font => font != null && font.name == OverlayType.TextFont);
            _figures = fonts.FirstOrDefault(font => font != null && font.name == OverlayType.FiguresFont);
        }
    }
}
