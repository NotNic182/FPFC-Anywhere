using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FpfcAnywhere.Core
{
    /// <summary>A corner of the game window. The order matches the words in the settings file.</summary>
    public enum ScreenCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    /// <summary>One player, as the game ranks them.</summary>
    public readonly struct PlayerScore : IEquatable<PlayerScore>
    {
        public PlayerScore(string? name, int score, bool failed)
        {
            Name = name;
            Score = score;
            Failed = failed;
        }

        public string? Name { get; }

        public int Score { get; }

        public bool Failed { get; }

        public bool Equals(PlayerScore other) => Score == other.Score && Failed == other.Failed && Name == other.Name;

        public override bool Equals(object? obj) => obj is PlayerScore other && Equals(other);

        public override int GetHashCode() => (Name?.GetHashCode() ?? 0) ^ Score;
    }

    /// <summary>Where the scores come from. Reading must not change anything in the game.</summary>
    public interface IScoreSource
    {
        /// <summary>Fills the list in the game's own order. False while there is no score list to read.</summary>
        bool TryRead(List<PlayerScore> players);
    }

    /// <summary>One line of the board, ready to draw.</summary>
    public sealed class ScoreboardRow
    {
        public ScoreboardRow(string rank, string name, string score, string gap, bool isLeader, bool isFailed)
        {
            Rank = rank;
            Name = name;
            Score = score;
            Gap = gap;
            IsLeader = isLeader;
            IsFailed = isFailed;
        }

        public string Rank { get; }

        public string Name { get; }

        public string Score { get; }

        public string Gap { get; }

        public bool IsLeader { get; }

        public bool IsFailed { get; }
    }

    /// <summary>
    /// Turns the game's ranking into text. It never sorts: the game already ranks everyone at the
    /// same moment in the song and puts failed players last, so the board can't disagree with it.
    /// </summary>
    public static class ScoreboardRows
    {
        /// <summary>A normal lobby holds five, so this only ever cuts a lobby on a modded server.</summary>
        public const int MaxRows = 10;

        public const int MaxNameLength = 22;

        // One ellipsis character: tighter than three periods, so a cut name keeps two more letters.
        private const string CutMark = "\u2026";
        private const string FailedText = "F";
        private const string NoName = "?";

        // Real languages put up to about three marks on one letter. Glitch text stacks dozens, which
        // would draw over the rows above and below.
        private const int MostMarksPerLetter = 3;

        private static readonly NumberFormatInfo GameNumbers = new NumberFormatInfo { NumberGroupSeparator = " " };

        public static IReadOnlyList<ScoreboardRow> From(IReadOnlyList<PlayerScore> players)
        {
            var rows = new List<ScoreboardRow>(Math.Min(players.Count, MaxRows));
            var hasLeader = players.Count > 0 && !players[0].Failed && players[0].Score > 0;

            for (var index = 0; index < players.Count && index < MaxRows; index++)
            {
                var player = players[index];
                var behind = hasLeader && !player.Failed ? players[0].Score - player.Score : 0;

                rows.Add(new ScoreboardRow(
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    CleanName(player.Name),
                    player.Failed ? FailedText : Number(player.Score),
                    behind > 0 ? "-" + Number(behind) : string.Empty,
                    hasLeader && index == 0,
                    player.Failed));
            }

            return rows;
        }

        // The same format as the game's own score displays.
        private static string Number(int value) => value.ToString("#,0", GameNumbers);

        // Names arrive from other players. What the game's font can't draw would show as a box on
        // stream, and invisible characters could make a name look empty, so both are left out. Any
        // kind of space or line break becomes one plain space.
        private static string CleanName(string? name)
        {
            if (name == null)
            {
                return NoName;
            }

            var oneLine = new StringBuilder(name.Length);
            var marks = 0;
            var spaceWaiting = false;
            for (var index = 0; index < name.Length; index++)
            {
                var length = 1;
                if (char.IsHighSurrogate(name[index]) && index + 1 < name.Length && char.IsLowSurrogate(name[index + 1]))
                {
                    length = 2;
                }
                else if (char.IsSurrogate(name[index]))
                {
                    continue;
                }

                var codePoint = char.ConvertToUtf32(name, index);
                var category = CharUnicodeInfo.GetUnicodeCategory(name, index);
                var piece = name.Substring(index, length);
                index += length - 1;

                if (char.IsControl(piece, 0) || char.IsWhiteSpace(piece, 0))
                {
                    spaceWaiting = oneLine.Length > 0;
                    continue;
                }

                if (IsEmojiPart(codePoint) || category == UnicodeCategory.Format)
                {
                    continue;
                }

                if (category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.EnclosingMark)
                {
                    // A mark needs a letter right before it to sit on.
                    if (oneLine.Length > 0 && !spaceWaiting && marks < MostMarksPerLetter)
                    {
                        oneLine.Append(piece);
                        marks++;
                    }

                    continue;
                }

                if (spaceWaiting)
                {
                    oneLine.Append(' ');
                    spaceWaiting = false;
                }

                oneLine.Append(piece);
                marks = 0;
            }

            var clean = oneLine.ToString();
            if (clean.Length == 0)
            {
                return NoName;
            }

            if (clean.Length <= MaxNameLength)
            {
                return clean;
            }

            var keep = MaxNameLength - CutMark.Length;
            if (char.IsHighSurrogate(clean[keep - 1]))
            {
                keep--;
            }

            return clean.Substring(0, keep) + CutMark;
        }

        // Pictographs the game's font has no shapes for, and the selectors and keycaps that build emoji.
        private static bool IsEmojiPart(int codePoint) =>
            (codePoint >= 0x1F000 && codePoint <= 0x1FAFF)
            || (codePoint >= 0xFE00 && codePoint <= 0xFE0F)
            || codePoint == 0x20E3;
    }

    /// <summary>
    /// Keeps the rows up to date without reading every frame. The game's scores are smoothed a little
    /// as they arrive, so ten reads a second looks live and costs next to nothing at any frame rate.
    /// </summary>
    public sealed class ScoreboardFeed
    {
        private const float ReadSeconds = 0.1f;

        // Looking for the list is the slow part, so it only happens this often.
        private const float SearchSeconds = 1f;

        private readonly IScoreSource _source;
        private readonly List<PlayerScore> _latest = new List<PlayerScore>();
        private readonly List<PlayerScore> _shown = new List<PlayerScore>();
        private float _untilNextRead;
        private bool _stopped;
        private Exception? _failure;

        public ScoreboardFeed(IScoreSource source)
        {
            _source = source;
        }

        public IReadOnlyList<ScoreboardRow> Rows { get; private set; } = Array.Empty<ScoreboardRow>();

        /// <summary>Returns true when the rows changed, so nothing gets rebuilt for no reason.</summary>
        public bool Tick(float deltaSeconds)
        {
            if (_stopped)
            {
                return false;
            }

            _untilNextRead -= deltaSeconds;
            if (_untilNextRead > 0f)
            {
                return false;
            }

            bool found;
            try
            {
                found = _source.TryRead(_latest);
            }
            catch (Exception exception)
            {
                // A scoreboard problem must never cost a match, and this would otherwise repeat
                // ten times a second. The next song gets a clean try.
                _stopped = true;
                _failure = exception;
                found = false;
            }

            _untilNextRead = found ? ReadSeconds : SearchSeconds;
            if (!found)
            {
                _latest.Clear();
            }

            if (_latest.SequenceEqual(_shown))
            {
                return false;
            }

            _shown.Clear();
            _shown.AddRange(_latest);
            Rows = ScoreboardRows.From(_shown);
            return true;
        }

        /// <summary>Hands over a failure once, so it gets logged once.</summary>
        public Exception? TakeFailure()
        {
            var failure = _failure;
            _failure = null;
            return failure;
        }

        public void Reset()
        {
            _stopped = false;
            _failure = null;
            _untilNextRead = 0f;
            _latest.Clear();
            _shown.Clear();
            Rows = Array.Empty<ScoreboardRow>();
        }
    }

    /// <summary>Sizes the board from the window, so it reads the same on a stream at any resolution.</summary>
    public static class ScoreboardLayout
    {
        public const int SmallestFont = 14;
        public const int LargestFont = 56;

        private const float WindowHeightsPerFont = 48f;

        public static int FontSize(int screenHeight) => FontSize(screenHeight, 1f);

        /// <summary>The size setting can shrink the board, but never below what still reads on a stream.</summary>
        public static int FontSize(int screenHeight, float scale)
        {
            var size = (int)Math.Round(screenHeight / WindowHeightsPerFont * scale);
            return Math.Min(Math.Max(size, SmallestFont), LargestFont);
        }

        /// <summary>
        /// The text a column is sized from. Digits are all one width, so the standard sample keeps the
        /// board still as scores climb, and anything longer takes over so no digit gets clipped off.
        /// </summary>
        public static string Sample(string standard, IEnumerable<string> texts)
        {
            var widest = standard;
            foreach (var text in texts)
            {
                if (text != null && text.Length > widest.Length)
                {
                    widest = text;
                }
            }

            return widest;
        }

        /// <summary>Where the board's top left goes so it sits in its corner, inside the broadcast margin.</summary>
        public static (float Left, float Top) PlateOrigin(ScreenCorner corner, int screenWidth, int screenHeight, float plateWidth, float plateHeight, float margin)
        {
            var right = corner == ScreenCorner.TopRight || corner == ScreenCorner.BottomRight;
            var bottom = corner == ScreenCorner.BottomLeft || corner == ScreenCorner.BottomRight;
            var left = right ? Math.Max(screenWidth - margin - plateWidth, 0f) : margin;
            var top = bottom ? Math.Max(screenHeight - margin - plateHeight, 0f) : margin;
            return (left, top);
        }

        public static float RowHeight(int fontSize) => fontSize * 1.5f;

        public static float ColumnGap(int fontSize) => fontSize * 0.8f;

        /// <summary>The name column gives way before the board runs off the window.</summary>
        public static float NameWidth(float wanted, int screenWidth, int fontSize, float otherColumns) =>
            NameWidth(wanted, screenWidth, fontSize, otherColumns, NoticeLayout.Margin(fontSize));

        /// <summary>The same, with the margin the board really sits at, which comes from the window.</summary>
        public static float NameWidth(float wanted, int screenWidth, int fontSize, float otherColumns, float margin)
        {
            var room = screenWidth - (margin * 2f) - otherColumns;
            return Math.Max(Math.Min(wanted, room), fontSize);
        }
    }

    /// <summary>
    /// The game's live score list, by name, because the mod does not link to the game's code. Only
    /// ever read. It holds the same scores the game already draws for spectators, and everything in
    /// it arrived from the other players, so reading it sends nothing and touches nothing.
    /// </summary>
    public static class GameScoreNames
    {
        public const string Assembly = "Main";
        public const string Provider = "MultiplayerScoreProvider";
        public const string RankedPlayers = "rankedPlayers";
        public const string Player = "RankedPlayer";
        public const string UserName = "userName";
        public const string Score = "score";
        public const string IsFailed = "isFailed";
    }
}
