using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FpfcAnywhere.Core
{
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

        private const string CutMark = "...";
        private const string FailedText = "F";
        private const string NoName = "?";

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

        private static string CleanName(string? name)
        {
            if (name == null)
            {
                return NoName;
            }

            var oneLine = new StringBuilder(name.Length);
            foreach (var character in name)
            {
                oneLine.Append(char.IsControl(character) ? ' ' : character);
            }

            var clean = oneLine.ToString().Trim();
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

        public static int FontSize(int screenHeight)
        {
            var size = (int)Math.Round(screenHeight / WindowHeightsPerFont);
            return Math.Min(Math.Max(size, SmallestFont), LargestFont);
        }

        public static float RowHeight(int fontSize) => fontSize * 1.5f;

        public static float ColumnGap(int fontSize) => fontSize * 0.8f;

        /// <summary>The name column gives way before the board runs off the window.</summary>
        public static float NameWidth(float wanted, int screenWidth, int fontSize, float otherColumns)
        {
            var room = screenWidth - (NoticeLayout.Margin(fontSize) * 2f) - otherColumns;
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
