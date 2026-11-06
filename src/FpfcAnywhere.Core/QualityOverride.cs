using System;
using System.Collections.Generic;
using System.Globalization;

namespace FpfcAnywhere.Core
{
    /// <summary>The three values the game owns that decide how much work each frame of the window is.</summary>
    public interface IQualityKnobs
    {
        /// <summary>False while a headset is running, or when the game's render pipeline cannot be found.</summary>
        bool CanOverride { get; }

        float RenderScale { get; set; }

        /// <summary>MSAA samples: 1 is off, then 2, 4 or 8.</summary>
        int AntiAliasing { get; set; }

        /// <summary>Frames per second ceiling. 0 is no cap.</summary>
        int FpsCap { get; set; }
    }

    /// <summary>
    /// Draws the window at the operator's quality settings while the free camera is showing, and
    /// hands the game its own values back the moment it is not. The game re-applies its graphics
    /// settings on scene changes, which can land either side of the mod's writes, so every frame
    /// checks the values are still the mod's, and whatever the game wrote in the meantime is what
    /// it gets back.
    /// </summary>
    public sealed class QualityOverride
    {
        private readonly IQualityKnobs _knobs;
        private readonly Knob[] _all;
        private bool _showing;

        public QualityOverride(IQualityKnobs knobs, FlightSettings settings)
        {
            _knobs = knobs;
            _all = new[]
            {
                new Knob("render_scale", () => settings.RenderScale, () => knobs.RenderScale, value => knobs.RenderScale = value),
                new Knob("anti_aliasing", () => settings.AntiAliasing, () => knobs.AntiAliasing, value => knobs.AntiAliasing = (int)value),
                new Knob("fps_cap", () => settings.FpsCap, () => knobs.FpsCap, value => knobs.FpsCap = (int)value),
            };
        }

        /// <summary>True while at least one value is the mod's rather than the game's.</summary>
        public bool Applied
        {
            get
            {
                foreach (var knob in _all)
                {
                    if (knob.Active)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void Update(bool freeCamShowing)
        {
            if (!freeCamShowing || !_knobs.CanOverride)
            {
                Restore();
                return;
            }

            if (!_showing)
            {
                _showing = true;
                foreach (var knob in _all)
                {
                    knob.Apply();
                }

                return;
            }

            foreach (var knob in _all)
            {
                knob.Keep();
            }
        }

        /// <summary>Hands every value back now. Safe to call at any time, including from the fail-safe.</summary>
        public void Restore()
        {
            _showing = false;
            foreach (var knob in _all)
            {
                knob.Restore();
            }
        }

        /// <summary>What is overridden right now and what the game had, for the log. Empty when nothing is.</summary>
        public string Describe()
        {
            var parts = new List<string>();
            foreach (var knob in _all)
            {
                if (knob.Active)
                {
                    parts.Add(knob.Describe());
                }
            }

            return string.Join(" ", parts);
        }

        private sealed class Knob
        {
            private const float Tolerance = 0.001f;

            private readonly string _name;
            private readonly Func<float> _wanted;
            private readonly Func<float> _read;
            private readonly Action<float> _write;
            private float _original;
            private float _applied;

            public Knob(string name, Func<float> wanted, Func<float> read, Action<float> write)
            {
                _name = name;
                _wanted = wanted;
                _read = read;
                _write = write;
            }

            public bool Active { get; private set; }

            /// <summary>Starts overriding, unless the setting is off or the game already has that value.</summary>
            public void Apply()
            {
                var wanted = _wanted();
                if (wanted <= 0f)
                {
                    return;
                }

                var current = _read();
                if (Same(current, wanted))
                {
                    return;
                }

                _original = current;
                _write(wanted);
                _applied = _read();
                Active = true;
            }

            /// <summary>A value the game wrote in the meantime becomes the one to hand back, and the mod's goes on again.</summary>
            public void Keep()
            {
                if (!Active)
                {
                    return;
                }

                var current = _read();
                if (Same(current, _applied))
                {
                    return;
                }

                _original = current;
                _write(_applied);
                _applied = _read();
            }

            /// <summary>Hands the game's value back, unless the game has already written something else.</summary>
            public void Restore()
            {
                if (!Active)
                {
                    return;
                }

                Active = false;
                if (Same(_read(), _applied))
                {
                    _write(_original);
                }
            }

            public string Describe() => $"{_name}={Format(_applied)} (game {Format(_original)})";

            private static bool Same(float left, float right) => Math.Abs(left - right) < Tolerance;

            private static string Format(float value) => value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
