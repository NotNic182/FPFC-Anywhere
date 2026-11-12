using System;
using System.Linq;
using FpfcAnywhere.Core;
using UnityEngine;

namespace FpfcAnywhere
{
    /// <summary>
    /// Pins the live scores to a corner of the desktop window. IMGUI only reaches this PC's
    /// window, so the headset and the lobby never see it, though a stream capturing the window will.
    ///
    /// ModHost leaves it disabled outside a multiplayer song, so other frames pay nothing. It is
    /// separate from ModHost so a problem in one can't stop the other: the scores keep going if the
    /// free cam stops, and a broken scoreboard never takes the camera down with it.
    /// </summary>
    internal sealed class ScoreboardOverlay : MonoBehaviour
    {
        private const float LeaderStripeWidth = 0.18f;

        // Digits are all the same width, so sizing the columns from these stops the board
        // twitching as the scores climb.
        private const string WidestScore = "8 888 888";
        private const string WidestGap = "-888 888";

        private static readonly Color Normal = Color.white;
        private static readonly Color Plate = new Color(0f, 0f, 0f, OverlayLook.PlateOpacity);
        private static readonly Color Dimmed = new Color(1f, 1f, 1f, OverlayLook.GapOpacity);
        private static readonly Color Out = new Color(1f, 1f, 1f, OverlayLook.OutOpacity);

        private readonly ScoreboardFeed _feed = new ScoreboardFeed(new UnityScoreSource());
        private readonly Line[] _lines = Enumerable.Range(0, ScoreboardRows.MaxRows).Select(_ => new Line()).ToArray();
        private readonly GUIContent _sample = new GUIContent();
        private NoticeOverlay _notices = null!;
        private GUIStyle? _nameStyle;
        private GUIStyle? _numberStyle;
        private Rect _plate;
        private Rect _leaderStripe;
        private int _lineCount;
        private int _fontSize;
        private int _windowWidth;
        private int _windowHeight;
        private int _scoreColumnChars;
        private int _gapColumnChars;
        private bool _laidOut;
        private ScreenCorner _corner = ScreenCorner.TopLeft;
        private float _scale = 1f;
        private Color _leader;

        /// <summary>Takes the corner and size from the settings. They apply from the next frame drawn.</summary>
        public void Place(ScreenCorner corner, float scale)
        {
            if (corner == _corner && scale == _scale)
            {
                return;
            }

            _corner = corner;
            _scale = scale;
            _laidOut = false;
        }

        private void Awake()
        {
            useGUILayout = false;
            enabled = false;
            _notices = GetComponent<NoticeOverlay>();

            // Unity would draw this gold as orange in a linear colour project, so it's handed over corrected.
            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            _leader = new Color(
                OverlayLook.ForGui(OverlayLook.LeaderRed, linear),
                OverlayLook.ForGui(OverlayLook.LeaderGreen, linear),
                OverlayLook.ForGui(OverlayLook.LeaderBlue, linear));
        }

        private void OnEnable() => ModLog.Info("Scoreboard on");

        private void OnDisable()
        {
            _feed.Reset();
            _lineCount = 0;

            // The fonts belong to the game, so a new song looks them up again.
            _nameStyle = null;
            _numberStyle = null;
            _laidOut = false;
        }

        private void Update()
        {
            try
            {
                if (_feed.Tick(Time.unscaledDeltaTime))
                {
                    Rebuild();
                }

                var failure = _feed.TakeFailure();
                if (failure != null)
                {
                    ModLog.Error("The scoreboard stopped", failure);
                    _notices.Show(OperatorNotice.ScoreboardStopped);
                }
            }
            catch (Exception exception)
            {
                GiveUp("The scoreboard stopped", exception);
            }
        }

        private void Rebuild()
        {
            var rows = _feed.Rows;
            if (rows.Count != _lineCount)
            {
                ModLog.Info($"Scoreboard showing {rows.Count} players");
                _lineCount = rows.Count;
                _laidOut = false;
            }

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var line = _lines[index];

                if (line.Name.text != row.Name)
                {
                    line.Name.text = row.Name;
                    _laidOut = false;
                }

                line.Rank.text = row.Rank;
                line.Score.text = row.Score;
                line.Gap.text = row.Gap;

                // A number longer than its column would lose its first digits, so the column grows.
                if (row.Score.Length > _scoreColumnChars || row.Gap.Length > _gapColumnChars)
                {
                    _laidOut = false;
                }
                line.RankColor = row.IsFailed ? Out : row.IsLeader ? _leader : Normal;
                line.TextColor = row.IsFailed ? Out : Normal;
            }

            _lines[0].IsLeader = rows.Count > 0 && rows[0].IsLeader;
        }

        private void OnGUI()
        {
            if (_lineCount == 0 || Event.current.type != EventType.Repaint)
            {
                return;
            }

            try
            {
                Draw();
            }
            catch (Exception exception)
            {
                GiveUp("Could not draw the scoreboard", exception);
            }
        }

        // This runs every frame while a match is on, so everything that can be worked out once
        // already has been. The text only changes a few times a second.
        private void Draw()
        {
            var fontSize = ScoreboardLayout.FontSize(Screen.height, _scale);
            if (!_laidOut || fontSize != _fontSize || Screen.width != _windowWidth || Screen.height != _windowHeight)
            {
                LayOut(fontSize);
            }

            var previous = GUI.color;
            GUI.color = Plate;
            GUI.DrawTexture(_plate, Texture2D.whiteTexture);

            if (_lines[0].IsLeader)
            {
                GUI.color = _leader;
                GUI.DrawTexture(_leaderStripe, Texture2D.whiteTexture);
            }

            for (var index = 0; index < _lineCount; index++)
            {
                var line = _lines[index];

                GUI.color = line.RankColor;
                GUI.Label(line.RankArea, line.Rank, _numberStyle!);

                GUI.color = line.TextColor;
                GUI.Label(line.NameArea, line.Name, _nameStyle!);
                GUI.Label(line.ScoreArea, line.Score, _numberStyle!);

                if (line.Gap.text.Length > 0)
                {
                    GUI.color = Dimmed;
                    GUI.Label(line.GapArea, line.Gap, _numberStyle!);
                }
            }

            GUI.color = previous;
        }

        private void LayOut(int fontSize)
        {
            if (_nameStyle == null || _numberStyle == null || fontSize != _fontSize)
            {
                BuildStyles(fontSize);
            }

            _fontSize = fontSize;
            _windowWidth = Screen.width;
            _windowHeight = Screen.height;
            _laidOut = true;

            var padding = NoticeLayout.Padding(fontSize);
            var columnGap = ScoreboardLayout.ColumnGap(fontSize);
            var rowHeight = ScoreboardLayout.RowHeight(fontSize);

            var rankWidth = WidthOf(_lines[_lineCount - 1].Rank.text, _numberStyle!);
            var scoreSample = ScoreboardLayout.Sample(WidestScore, _lines.Take(_lineCount).Select(line => line.Score.text));
            var gapSample = ScoreboardLayout.Sample(WidestGap, _lines.Take(_lineCount).Select(line => line.Gap.text));
            _scoreColumnChars = scoreSample.Length;
            _gapColumnChars = gapSample.Length;
            var scoreWidth = WidthOf(scoreSample, _numberStyle!);
            var gapWidth = WidthOf(gapSample, _numberStyle!);
            var widestName = _lines.Take(_lineCount).Max(line => _nameStyle!.CalcSize(line.Name).x);

            // The margin comes from the window, not the board, so a bigger board doesn't creep inwards.
            var margin = NoticeLayout.Margin(ScoreboardLayout.FontSize(_windowHeight));
            var otherColumns = (padding * 2f) + rankWidth + scoreWidth + gapWidth + (columnGap * 3f);
            var nameWidth = ScoreboardLayout.NameWidth(widestName, _windowWidth, fontSize, otherColumns, margin);
            var plateWidth = otherColumns + nameWidth;
            var plateHeight = (rowHeight * _lineCount) + (padding * 2f);
            var origin = ScoreboardLayout.PlateOrigin(_corner, _windowWidth, _windowHeight, plateWidth, plateHeight, margin);
            _plate = new Rect(origin.Left, origin.Top, plateWidth, plateHeight);
            _leaderStripe = new Rect(origin.Left, origin.Top + padding, fontSize * LeaderStripeWidth, rowHeight);

            var rankLeft = origin.Left + padding;
            var nameLeft = rankLeft + rankWidth + columnGap;
            var scoreLeft = nameLeft + nameWidth + columnGap;
            var gapLeft = scoreLeft + scoreWidth + columnGap;

            for (var index = 0; index < _lineCount; index++)
            {
                var top = origin.Top + padding + (rowHeight * index);
                var line = _lines[index];
                line.RankArea = new Rect(rankLeft, top, rankWidth, rowHeight);
                line.NameArea = new Rect(nameLeft, top, nameWidth, rowHeight);
                line.ScoreArea = new Rect(scoreLeft, top, scoreWidth, rowHeight);
                line.GapArea = new Rect(gapLeft, top, gapWidth, rowHeight);
            }
        }

        private float WidthOf(string text, GUIStyle style)
        {
            _sample.text = text;
            return style.CalcSize(_sample).x;
        }

        private void BuildStyles(int fontSize)
        {
            _nameStyle = StyleFor(GameFonts.Text, fontSize, TextAnchor.MiddleLeft);
            _numberStyle = StyleFor(GameFonts.Figures, fontSize, TextAnchor.MiddleRight);
        }

        // Rich text stays off because the names come from other players, and a name like
        // <size=500> must not be able to take over a broadcast.
        private static GUIStyle StyleFor(Font? font, int fontSize, TextAnchor alignment)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = fontSize,
                alignment = alignment,
                clipping = TextClipping.Clip,
                richText = false,
                wordWrap = false,
                margin = new RectOffset(),
                padding = new RectOffset(),
            };
            style.normal.textColor = Color.white;
            return style;
        }

        // This would otherwise repeat every frame and fill the log during a match.
        private void GiveUp(string message, Exception exception)
        {
            enabled = false;
            ModLog.Error(message, exception);
            _notices.Show(OperatorNotice.ScoreboardStopped);
        }

        private sealed class Line
        {
            public readonly GUIContent Rank = new GUIContent();
            public readonly GUIContent Name = new GUIContent();
            public readonly GUIContent Score = new GUIContent();
            public readonly GUIContent Gap = new GUIContent();
            public Rect RankArea;
            public Rect NameArea;
            public Rect ScoreArea;
            public Rect GapArea;
            public Color RankColor;
            public Color TextColor;
            public bool IsLeader;
        }
    }
}
