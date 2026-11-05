using System;
using System.Linq;
using FpfcAnywhere.Core;
using UnityEngine;

namespace FpfcAnywhere
{
    /// <summary>
    /// Pins the live scores to the top left of the desktop window. IMGUI only reaches this PC's
    /// window, so the headset and the lobby never see it, though a stream capturing the window will.
    ///
    /// ModHost leaves it disabled outside a multiplayer song, so other frames pay nothing. It is
    /// separate from ModHost so a problem in one can't stop the other: the scores keep going if the
    /// free cam stops, and a broken scoreboard never takes the camera down with it.
    /// </summary>
    internal sealed class ScoreboardOverlay : MonoBehaviour
    {
        // Same as the notices, which keeps white text above 7:1 even over a pure white scene.
        private const float PlateAlpha = 0.75f;

        private const float LeaderStripeWidth = 0.18f;

        // Digits are all the same width, so sizing the columns from these stops the board
        // twitching as the scores climb.
        private const string WidestScore = "8 888 888";
        private const string WidestGap = "-888 888";

        private static readonly Color Plate = new Color(0f, 0f, 0f, PlateAlpha);
        private static readonly Color Leader = new Color(1f, 0.82f, 0.3f);
        private static readonly Color Normal = Color.white;
        private static readonly Color Dimmed = new Color(1f, 1f, 1f, 0.7f);
        private static readonly Color Out = new Color(1f, 1f, 1f, 0.5f);

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
        private bool _laidOut;

        private void Awake()
        {
            useGUILayout = false;
            enabled = false;
            _notices = GetComponent<NoticeOverlay>();
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
                line.RankColor = row.IsFailed ? Out : row.IsLeader ? Leader : Normal;
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
            var fontSize = ScoreboardLayout.FontSize(Screen.height);
            if (!_laidOut || fontSize != _fontSize || Screen.width != _windowWidth)
            {
                LayOut(fontSize);
            }

            var previous = GUI.color;
            GUI.color = Plate;
            GUI.DrawTexture(_plate, Texture2D.whiteTexture);

            if (_lines[0].IsLeader)
            {
                GUI.color = Leader;
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
            _laidOut = true;

            var padding = NoticeLayout.Padding(fontSize);
            var columnGap = ScoreboardLayout.ColumnGap(fontSize);
            var rowHeight = ScoreboardLayout.RowHeight(fontSize);

            var rankWidth = WidthOf(_lines[_lineCount - 1].Rank.text, _numberStyle!);
            var scoreWidth = WidthOf(WidestScore, _numberStyle!);
            var gapWidth = WidthOf(WidestGap, _numberStyle!);
            var widestName = _lines.Take(_lineCount).Max(line => _nameStyle!.CalcSize(line.Name).x);

            var otherColumns = (padding * 2f) + rankWidth + scoreWidth + gapWidth + (columnGap * 3f);
            var nameWidth = ScoreboardLayout.NameWidth(widestName, _windowWidth, fontSize, otherColumns);

            var margin = NoticeLayout.Margin(fontSize);
            _plate = new Rect(margin, margin, otherColumns + nameWidth, (rowHeight * _lineCount) + (padding * 2f));
            _leaderStripe = new Rect(margin, margin + padding, fontSize * LeaderStripeWidth, rowHeight);

            var nameLeft = margin + padding + rankWidth + columnGap;
            var scoreLeft = nameLeft + nameWidth + columnGap;
            var gapLeft = scoreLeft + scoreWidth + columnGap;

            for (var index = 0; index < _lineCount; index++)
            {
                var top = margin + padding + (rowHeight * index);
                var line = _lines[index];
                line.RankArea = new Rect(margin + padding, top, rankWidth, rowHeight);
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
            var fonts = Resources.FindObjectsOfTypeAll<Font>();
            _nameStyle = StyleFor(fonts.FirstOrDefault(font => font.name == "Roboto-Regular"), fontSize, TextAnchor.MiddleLeft);
            _numberStyle = StyleFor(fonts.FirstOrDefault(font => font.name == "Roboto-Bold"), fontSize, TextAnchor.MiddleRight);
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
