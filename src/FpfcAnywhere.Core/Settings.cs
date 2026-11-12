using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FpfcAnywhere.Core
{
    /// <summary>
    /// The settings people edit by hand. Nothing typed in that file can stop the mod: a line that
    /// makes no sense is reported, and the camera carries on with a sensible value.
    /// </summary>
    public static class SettingsText
    {
        private static readonly Setting[] Settings =
        {
            Setting.Between("look_speed", 0.01f, 1f, settings => settings.LookSensitivity, (settings, value) => settings.LookSensitivity = value),
            Setting.Between("move_speed", 0.1f, 30f, settings => settings.MoveSpeed, (settings, value) => settings.MoveSpeed = value),
            Setting.Between("shift_multiplier", 1f, 10f, settings => settings.FastMultiplier, (settings, value) => settings.FastMultiplier = value),
            Setting.Between("fov", 20f, 120f, settings => settings.FieldOfView, (settings, value) => settings.FieldOfView = value),
            Setting.Between("smoothing", 0f, 1f, settings => settings.SmoothingSeconds, (settings, value) => settings.SmoothingSeconds = value),
            Setting.Between("scoreboard", 0f, 1f, settings => settings.ShowScoreboard ? 1f : 0f, (settings, value) => settings.ShowScoreboard = value > 0f),
            Setting.Word("scoreboard_corner", new[] { "top_left", "top_right", "bottom_left", "bottom_right" }, settings => (float)settings.ScoreboardCorner, (settings, value) => settings.ScoreboardCorner = (ScreenCorner)(int)value),
            Setting.Between("scoreboard_scale", 0.5f, 2f, settings => settings.ScoreboardScale, (settings, value) => settings.ScoreboardScale = value),
            Setting.OffOrBetween("render_scale", 0.5f, 2f, settings => settings.RenderScale, (settings, value) => settings.RenderScale = value),
            Setting.OneOf("anti_aliasing", new[] { 0f, 1f, 2f, 4f, 8f }, settings => settings.AntiAliasing, (settings, value) => settings.AntiAliasing = (int)value),
            Setting.OffOrBetween("fps_cap", 20f, 1000f, settings => settings.FpsCap, (settings, value) => settings.FpsCap = (int)Math.Round(value)),
            Setting.Between("skip_game_draw", 0f, 1f, settings => settings.SkipGameDraw ? 1f : 0f, (settings, value) => settings.SkipGameDraw = value > 0f),
        };

        /// <summary>The file a person finds on first launch. CRLF, so old Notepad shows it properly.</summary>
        public static string Default { get; } = string.Join("\r\n", new[]
        {
            "# FPFC Anywhere settings",
            "# Changes kick in at the next song. Delete this file to get the defaults back.",
            string.Empty,
            "# How fast the mouse turns the camera. Lower is slower. 0.01 to 1.",
            "look_speed = 0.03",
            string.Empty,
            "# How fast you fly, in meters per second. 0.1 to 30.",
            "move_speed = 3",
            string.Empty,
            "# How much faster you go while holding Shift. 1 to 10.",
            "shift_multiplier = 3",
            string.Empty,
            "# Field of view, top to bottom. Lower is more zoomed in. 20 to 120.",
            "fov = 90",
            string.Empty,
            "# Makes the camera glide instead of starting and stopping dead. Roughly how many seconds",
            "# it takes to catch up with you. Higher is floatier. 0 is off. 0 to 1.",
            "smoothing = 0.5",
            string.Empty,
            "# Pins the live scores to a corner during multiplayer. 1 is on, 0 is off.",
            "scoreboard = 1",
            string.Empty,
            "# Which corner the scoreboard sits in: top_left, top_right, bottom_left or bottom_right.",
            "# Notices move to the bottom right while it's in the bottom left.",
            "scoreboard_corner = top_left",
            string.Empty,
            "# How big the scoreboard is. 1 is normal. 0.5 to 2.",
            "scoreboard_scale = 1",
            string.Empty,
            "# Draws the game at this fraction of the window size while the free cam is on. 1 is the",
            "# window's own size, and a big fps win if your VR resolution scale is higher. 0 keeps the",
            "# game's setting. 0, or 0.5 to 2.",
            "render_scale = 0",
            string.Empty,
            "# Anti-aliasing while the free cam is on. 1 is off, 2, 4 and 8 are smoother and slower.",
            "# 0 keeps the game's setting.",
            "anti_aliasing = 0",
            string.Empty,
            "# Caps the fps while the free cam is on. Keeps the GPU cooler when the stream only needs",
            "# 60 anyway. 0 is no cap. 0, or 20 to 1000.",
            "fps_cap = 0",
            string.Empty,
            "# Skips drawing the game's own view while the free cam is on, since nobody sees it. 1 is on,",
            "# 0 draws it anyway like older versions did.",
            "skip_game_draw = 1",
            string.Empty,
        });

        /// <summary>
        /// Puts the file's values on the settings and returns one line per mistake. Anything the
        /// file leaves out goes back to its default, so removing a line behaves as you would expect.
        /// </summary>
        public static IReadOnlyList<string> Apply(string text, FlightSettings settings)
        {
            var defaults = new FlightSettings();
            foreach (var setting in Settings)
            {
                setting.Write(settings, setting.Read(defaults));
            }

            var problems = new List<string>();
            var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            for (var index = 0; index < lines.Length; index++)
            {
                var problem = ApplyLine(WithoutNote(lines[index]), settings, defaults);
                if (problem != null)
                {
                    problems.Add($"line {index + 1}: {problem}");
                }
            }

            return problems;
        }

        public static string Describe(FlightSettings settings)
        {
            var parts = new List<string>();
            foreach (var setting in Settings)
            {
                parts.Add(setting.Name + "=" + setting.Show(setting.Read(settings)));
            }

            return string.Join(" ", parts);
        }

        private static string? ApplyLine(string line, FlightSettings settings, FlightSettings defaults)
        {
            if (line.Length == 0)
            {
                return null;
            }

            var equals = line.IndexOf('=');
            if (equals < 0)
            {
                return "this line has no =";
            }

            var name = line.Substring(0, equals).Trim();
            var value = line.Substring(equals + 1).Trim();
            var setting = Array.Find(Settings, item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (setting == null)
            {
                return $"there's no setting called \"{name}\"";
            }

            // Spaces and dashes count as underscores, since "top right" is what most people type.
            if (setting.Words != null)
            {
                var wanted = value.Replace(' ', '_').Replace('-', '_');
                var index = Array.FindIndex(setting.Words, word => string.Equals(word, wanted, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    setting.Write(settings, setting.Read(defaults));
                    return $"{setting.Name} has to be {setting.Allowed}, so it's using {setting.Show(setting.Read(defaults))}";
                }

                setting.Write(settings, index);
                return null;
            }

            if (!TryNumber(value, out var number))
            {
                setting.Write(settings, setting.Read(defaults));
                return $"\"{value}\" isn't a number, so {setting.Name} is using {Format(setting.Read(defaults))}";
            }

            var limited = setting.Fix(number);
            setting.Write(settings, limited);

            return limited == number
                ? null
                : $"{setting.Name} has to be {setting.Allowed}, so it's using {Format(limited)}";
        }

        private static string WithoutNote(string line)
        {
            var note = line.IndexOfAny(new[] { '#', ';' });
            return (note < 0 ? line : line.Substring(0, note)).Trim();
        }

        // A comma counts as a decimal point because plenty of people write numbers that way, and
        // the PC's language is ignored so the same file means the same thing everywhere.
        private static bool TryNumber(string text, out float number)
        {
            return float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                && !float.IsNaN(number)
                && !float.IsInfinity(number);
        }

        private static string Format(float value) => value.ToString(CultureInfo.InvariantCulture);

        private sealed class Setting
        {
            private Setting(string name, string allowed, Func<float, float> fix, Func<FlightSettings, float> read, Action<FlightSettings, float> write, string[]? words = null)
            {
                Name = name;
                Allowed = allowed;
                Fix = fix;
                Read = read;
                Write = write;
                Words = words;
            }

            public string Name { get; }

            /// <summary>The allowed values in words, for the problem line.</summary>
            public string Allowed { get; }

            /// <summary>Turns any number into the value that gets used.</summary>
            public Func<float, float> Fix { get; }

            public Func<FlightSettings, float> Read { get; }

            public Action<FlightSettings, float> Write { get; }

            /// <summary>For a setting written as a word, the words in order. Each one's place is its value.</summary>
            public string[]? Words { get; }

            /// <summary>The value as a person would write it in the file.</summary>
            public string Show(float value) => Words != null ? Words[Math.Min(Math.Max((int)value, 0), Words.Length - 1)] : Format(value);

            public static Setting Between(string name, float min, float max, Func<FlightSettings, float> read, Action<FlightSettings, float> write)
            {
                return new Setting(name, $"between {Format(min)} and {Format(max)}", value => Math.Min(Math.Max(value, min), max), read, write);
            }

            /// <summary>Zero or anything at or below it switches the setting off, otherwise the range applies.</summary>
            public static Setting OffOrBetween(string name, float min, float max, Func<FlightSettings, float> read, Action<FlightSettings, float> write)
            {
                return new Setting(name, $"0 or between {Format(min)} and {Format(max)}", value => value <= 0f ? 0f : Math.Min(Math.Max(value, min), max), read, write);
            }

            /// <summary>One of a few words, like a corner.</summary>
            public static Setting Word(string name, string[] words, Func<FlightSettings, float> read, Action<FlightSettings, float> write)
            {
                return new Setting(name, Listing(words), value => value, read, write, words);
            }

            /// <summary>Only the listed values, in rising order. Anything else uses the nearest one, the higher on a tie.</summary>
            public static Setting OneOf(string name, float[] values, Func<FlightSettings, float> read, Action<FlightSettings, float> write)
            {
                var allowed = Listing(Array.ConvertAll(values, Format));

                return new Setting(name, allowed, value =>
                {
                    var nearest = values[0];
                    foreach (var candidate in values)
                    {
                        if (Math.Abs(candidate - value) <= Math.Abs(nearest - value))
                        {
                            nearest = candidate;
                        }
                    }

                    return nearest;
                }, read, write);
            }

            private static string Listing(string[] items)
            {
                var words = string.Join(", ", items);
                var last = words.LastIndexOf(", ", StringComparison.Ordinal);
                return words.Substring(0, last) + " or " + words.Substring(last + 2);
            }
        }
    }

    /// <summary>
    /// Keeps the settings in step with the file on disk. A settings problem must never cost a
    /// match, so nothing here throws: on any failure the camera keeps the values it already has.
    /// </summary>
    public sealed class SettingsFile
    {
        public const string FileName = "FpfcAnywhere.ini";

        private readonly string _path;
        private DateTime _lastRead = DateTime.MinValue;
        private string? _lastFailure;

        public SettingsFile(string path)
        {
            _path = path;
        }

        public IReadOnlyList<string> Problems { get; private set; } = Array.Empty<string>();

        /// <summary>Returns true when there is something new to report, so a file that stays the same stays quiet.</summary>
        public bool Refresh(FlightSettings settings)
        {
            try
            {
                if (!File.Exists(_path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_path));
                    File.WriteAllText(_path, SettingsText.Default);
                }

                var written = File.GetLastWriteTimeUtc(_path);
                if (written == _lastRead)
                {
                    return false;
                }

                Problems = SettingsText.Apply(File.ReadAllText(_path), settings);
                _lastRead = written;
                _lastFailure = null;
                return true;
            }
            catch (Exception exception)
            {
                if (exception.Message == _lastFailure)
                {
                    return false;
                }

                _lastFailure = exception.Message;
                Problems = new[] { exception.Message };
                return true;
            }
        }
    }
}
