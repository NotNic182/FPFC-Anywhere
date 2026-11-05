using System;

namespace FpfcAnywhere.Core
{
    /// <summary>Unity-free vector, so flight logic can run in plain unit tests.</summary>
    public readonly struct Vec3
    {
        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);

        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float Length => (float)Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }
}
