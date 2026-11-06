namespace FpfcAnywhere.Core
{
    /// <summary>
    /// While the free camera is showing, the game's own camera still draws the whole scene into a
    /// view nobody sees. These rules say when that draw can be skipped. The skip itself is a draw
    /// type flag on the game camera, the one thing the mod writes on it, and the Unity layer puts
    /// it back the moment these rules say no.
    /// </summary>
    public sealed class HiddenDraw
    {
        /// <summary>
        /// EnvironmentShaderWarmup draws its quads through the game camera once, just after a song
        /// loads. The draw runs for this long after the clone appears so that frame is not lost.
        /// </summary>
        public const float HoldSeconds = 1f;

        private readonly FlightSettings _settings;

        public HiddenDraw(FlightSettings settings)
        {
            _settings = settings;
        }

        /// <summary>True when the game's draw can be skipped this frame.</summary>
        public bool CanSkip(bool freeCamShowing, float secondsSinceClone, bool warmupVisible, bool headsetRunning)
        {
            return _settings.SkipGameDraw
                && freeCamShowing
                && secondsSinceClone >= HoldSeconds
                && !warmupVisible
                && !headsetRunning;
        }
    }
}
