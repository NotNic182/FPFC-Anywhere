using System;
using FpfcAnywhere.Core;
using UnityEngine;

namespace FpfcAnywhere
{
    /// <summary>
    /// Puts a short line of text in the corner of the desktop window. IMGUI only reaches this PC's
    /// window, so the headset and the lobby never see it, though a stream capturing the window will.
    ///
    /// It stays disabled unless a notice is up, so normal frames pay nothing. It is separate from
    /// ModHost because the worst notice has to appear after the fail-safe has disabled ModHost.
    /// </summary>
    internal sealed class NoticeOverlay : MonoBehaviour
    {
        private readonly NoticeTimer _timer = new NoticeTimer();
        private readonly GUIContent _content = new GUIContent();
        private GUIStyle? _style;
        private ScreenCorner _boardCorner = ScreenCorner.TopLeft;

        /// <summary>Where the scoreboard is, so a notice never lands on top of it.</summary>
        public void AvoidBoard(ScreenCorner board) => _boardCorner = board;

        public void Show(OperatorNotice notice)
        {
            if (notice == OperatorNotice.None)
            {
                return;
            }

            _timer.Show(notice);
            _content.text = NoticeText.For(notice);
            enabled = true;
        }

        private void Awake()
        {
            useGUILayout = false;
            enabled = false;
        }

        private void Update()
        {
            _timer.Tick(Time.unscaledDeltaTime);
            if (_timer.Current == OperatorNotice.None)
            {
                enabled = false;
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            try
            {
                Draw();
            }
            catch (Exception exception)
            {
                // This would otherwise repeat every frame and fill the log during a match.
                enabled = false;
                ModLog.Error("Could not draw a notice", exception);
            }
        }

        private void Draw()
        {
            var fontSize = NoticeLayout.FontSize(Screen.height);
            var style = StyleFor(fontSize);
            var margin = NoticeLayout.Margin(fontSize);
            var padding = NoticeLayout.Padding(fontSize);

            // Wrapping is only switched on when the text truly does not fit. Left on, a line measured
            // to the exact width can still wrap its last word onto a row the box has no room for.
            var wanted = style.CalcSize(_content);
            var maxWidth = NoticeLayout.MaxTextWidth(Screen.width, fontSize);
            var wraps = wanted.x > maxWidth;
            style.wordWrap = wraps;
            var textWidth = wraps ? maxWidth : wanted.x;
            var textHeight = wraps ? style.CalcHeight(_content, textWidth) : wanted.y;

            var plateWidth = textWidth + (padding * 2f);
            var plate = new Rect(
                NoticeLayout.PlateLeft(NoticeLayout.Corner(_boardCorner), Screen.width, plateWidth, margin),
                Screen.height - margin - textHeight - (padding * 2f),
                plateWidth,
                textHeight + (padding * 2f));
            var text = new Rect(plate.x + padding, plate.y + padding, textWidth, textHeight);

            var opacity = _timer.Opacity;
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, OverlayLook.PlateOpacity * opacity);
            GUI.DrawTexture(plate, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, opacity);
            GUI.Label(text, _content, style);
            GUI.color = previous;
        }

        // The same face as the scoreboard's names. A notice at startup can come before the game has
        // loaded it, so the style is rebuilt once the font turns up.
        private GUIStyle StyleFor(int fontSize)
        {
            var font = GameFonts.Text;
            if (_style == null || _style.fontSize != fontSize || _style.font != font)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = fontSize,
                    clipping = TextClipping.Overflow,
                    alignment = TextAnchor.UpperLeft,
                    margin = new RectOffset(),
                    padding = new RectOffset(),
                };
                _style.normal.textColor = Color.white;
            }

            return _style;
        }
    }
}
