namespace FpfcAnywhere.Core
{
    /// <summary>Where the free camera is and where it points, in world space.</summary>
    public readonly struct FlightPose
    {
        public readonly Vec3 Position;
        public readonly float Yaw;
        public readonly float Pitch;

        public FlightPose(Vec3 position, float yaw, float pitch)
        {
            Position = position;
            Yaw = yaw;
            Pitch = pitch;
        }

        public override string ToString() => $"{Position} yaw={Yaw:F1} pitch={Pitch:F1}";
    }

    /// <summary>One frame of operator input. Mouse values are raw per-frame deltas.</summary>
    public struct FlightInput
    {
        public bool Forward;
        public bool Back;
        public bool Left;
        public bool Right;
        public bool Up;
        public bool Down;
        public bool Fast;
        public float MouseX;
        public float MouseY;
    }

    /// <summary>Everything the settings file controls. The start height matches the game's stock FPFC.</summary>
    public sealed class FlightSettings
    {
        public float StartHeight { get; set; } = 1.7f;
        public float MoveSpeed { get; set; } = 3f;
        public float FastMultiplier { get; set; } = 3f;
        public float LookSensitivity { get; set; } = 0.03f;
        public float MaxPitch { get; set; } = 89f;
        public float MaxStepSeconds { get; set; } = 0.1f;
        public float FieldOfView { get; set; } = 90f;

        /// <summary>Roughly how long the camera takes to catch up with the controls. Zero is off.</summary>
        public float SmoothingSeconds { get; set; } = 0.5f;

        /// <summary>Whether the live scores get pinned to the window during multiplayer.</summary>
        public bool ShowScoreboard { get; set; } = true;

        /// <summary>How far the player must jump in one frame to count as a spot change, not a head movement.</summary>
        public float SpotJumpMeters { get; set; } = 0.5f;

        /// <summary>Render scale for the window while the free camera shows. 0 keeps the game's own value.</summary>
        public float RenderScale { get; set; }

        /// <summary>Anti-aliasing samples while the free camera shows: 1, 2, 4 or 8. 0 keeps the game's own value.</summary>
        public int AntiAliasing { get; set; }

        /// <summary>Frames per second ceiling while the free camera shows. 0 is no cap.</summary>
        public int FpsCap { get; set; }

        /// <summary>Whether the game camera's own draw, which nobody sees, is skipped while the free camera shows.</summary>
        public bool SkipGameDraw { get; set; } = true;
    }
}
