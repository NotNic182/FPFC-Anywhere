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
            new Setting("look_speed", 0.01f, 1f, settings => settings.LookSensitivity, (settings, value) => settings.LookSensitivity = value),
            new Setting("move_speed", 0.1f, 30f, settings => settings.MoveSpeed, (settings, value) => settings.MoveSpeed = value),
            new Setting("shift_multiplier", 1f, 10f, settings => settings.FastMultiplier, (settings, value) => settings.FastMultiplier = value),
            new Setting("fov", 20f, 120f, settings => settings.FieldOfView, (settings, value) => settings.FieldOfView = value),
            new Setting("smoothing", 0f, 1f, settings => settings.SmoothingSeconds, (settings, value) => settings.SmoothingSeconds = value),
        };

        /// <summary>The file a person finds on first launch. CRLF, so old Notepad shows it properly.</summary>
        public static string Default { get; } = string.Join("\r\n", new[]
        {
            "# FPFC Anywhere settings",
            "# Changes kick in at the next song. Delete this file to get the defaults back.",
            string.Empty,
            "# How fast the mouse turns the camera. Lower is slower. 0.01 to 1.",
            "look_speed = 0.1",
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
            "# it takes to catch up with you. 0 is off. Try 0.1 to start. 0 to 1.",
            "smoothing = 0",
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
                parts.Add(setting.Name + "=" + Format(setting.Read(settings)));
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

            if (!TryNumber(value, out var number))
            {
                setting.Write(settings, setting.Read(defaults));
                return $"\"{value}\" isn't a number, so {setting.Name} is using {Format(setting.Read(defaults))}";
            }

            var limited = Math.Min(Math.Max(number, setting.Min), setting.Max);
            setting.Write(settings, limited);

            return limited == number
                ? null
                : $"{setting.Name} has to be between {Format(setting.Min)} and {Format(setting.Max)}, so it's using {Format(limited)}";
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
            public Setting(string name, float min, float max, Func<FlightSettings, float> read, Action<FlightSettings, float> write)
            {
                Name = name;
                Min = min;
                Max = max;
                Read = read;
                Write = write;
            }

            public string Name { get; }

            public float Min { get; }

            public float Max { get; }

            public Func<FlightSettings, float> Read { get; }

            public Action<FlightSettings, float> Write { get; }
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
