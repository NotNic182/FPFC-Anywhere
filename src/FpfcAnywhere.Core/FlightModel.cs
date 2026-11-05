using System;

namespace FpfcAnywhere.Core
{
    /// <summary>Pure flight maths for the free camera.</summary>
    public static class FlightModel
    {
        private const float DegreesToRadians = (float)(Math.PI / 180.0);

        /// <summary>
        /// Picks the pose a song starts from. Without a tracked headset the game's camera sits at
        /// floor level, so the height is lifted; a source that is already higher is kept.
        /// </summary>
        public static FlightPose StartFrom(Vec3 sourcePosition, float sourceYaw, FlightSettings settings)
        {
            var height = Math.Max(sourcePosition.Y, settings.StartHeight);
            return new FlightPose(new Vec3(sourcePosition.X, height, sourcePosition.Z), sourceYaw, 0f);
        }

        /// <summary>
        /// Picks where to stand to see the player's menu. The game's camera marks the player's
        /// spot, but with no headset it sits at the spot's floor, so it is raised to head height.
        /// </summary>
        public static FlightPose MenuView(Vec3 cameraPosition, float cameraLocalHeight, float yaw, FlightSettings settings)
        {
            var lift = Math.Max(0f, settings.StartHeight - cameraLocalHeight);
            return new FlightPose(cameraPosition + new Vec3(0f, lift, 0f), yaw, 0f);
        }

        /// <summary>
        /// Carries the camera to a new spectator spot, keeping its position and view relative to
        /// the spot. The menu travels with the spot, so its buttons stay under the pointer.
        /// </summary>
        public static FlightPose FollowSpot(FlightPose pose, Vec3 oldSpot, float oldYaw, Vec3 newSpot, float newYaw)
        {
            var turn = newYaw - oldYaw;
            var radians = turn * DegreesToRadians;
            var sin = (float)Math.Sin(radians);
            var cos = (float)Math.Cos(radians);

            var offset = pose.Position - oldSpot;
            var turned = new Vec3((offset.X * cos) + (offset.Z * sin), offset.Y, (offset.Z * cos) - (offset.X * sin));

            return new FlightPose(newSpot + turned, pose.Yaw + turn, pose.Pitch);
        }

        /// <summary>Advances the camera one frame. Mouse deltas are per frame, so they ignore time.</summary>
        public static FlightPose Step(FlightPose pose, in FlightInput input, float deltaSeconds, FlightSettings settings)
        {
            var yaw = pose.Yaw + (input.MouseX * settings.LookSensitivity);
            var pitch = Clamp(pose.Pitch - (input.MouseY * settings.LookSensitivity), -settings.MaxPitch, settings.MaxPitch);

            var direction = FlatDirection(yaw, input);
            var speed = input.Fast ? settings.MoveSpeed * settings.FastMultiplier : settings.MoveSpeed;
            var seconds = Clamp(deltaSeconds, 0f, settings.MaxStepSeconds);

            return new FlightPose(pose.Position + (direction * (speed * seconds)), yaw, pitch);
        }

        // Forward and strafe stay on the horizontal plane so looking down never drives into the floor.
        private static Vec3 FlatDirection(float yaw, in FlightInput input)
        {
            var radians = yaw * DegreesToRadians;
            var sin = (float)Math.Sin(radians);
            var cos = (float)Math.Cos(radians);

            var forward = Axis(input.Forward, input.Back);
            var strafe = Axis(input.Right, input.Left);
            var vertical = Axis(input.Up, input.Down);

            var direction = new Vec3((sin * forward) + (cos * strafe), vertical, (cos * forward) - (sin * strafe));
            var length = direction.Length;
            return length > 0f ? direction * (1f / length) : Vec3.Zero;
        }

        private static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

        private static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);
    }
}
