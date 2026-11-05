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

    /// <summary>Tunable flight values. The start height matches the game's stock FPFC.</summary>
    public sealed class FlightSettings
    {
        public float StartHeight { get; set; } = 1.7f;
        public float MoveSpeed { get; set; } = 3f;
        public float FastMultiplier { get; set; } = 3f;
        public float LookSensitivity { get; set; } = 0.1f;
        public float MaxPitch { get; set; } = 89f;
        public float MaxStepSeconds { get; set; } = 0.1f;
        public float FieldOfView { get; set; } = 90f;

        /// <summary>Roughly how long the camera takes to catch up with the controls. Zero is off.</summary>
        public float SmoothingSeconds { get; set; }

        /// <summary>How far the player must jump in one frame to count as a spot change, not a head movement.</summary>
        public float SpotJumpMeters { get; set; } = 0.5f;
    }
}
