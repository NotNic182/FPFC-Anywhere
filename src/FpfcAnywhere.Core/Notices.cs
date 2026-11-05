using System;

namespace FpfcAnywhere.Core
{
    /// <summary>
    /// Things the operator has to be told on screen. A key that does nothing or a camera that quietly
    /// gives up looks like a hang in the middle of a broadcast, and the log is no help at that moment.
    /// </summary>
    public enum OperatorNotice
    {
        None,
        NoMenuHere,
        FreeCamIsOff,
        NoGameCamera,
        FreeCamStopped,
        NeedsFpfc,
        WrongGameVersion,
        SettingsProblem,
    }

    public static class NoticeText
    {
        public static string For(OperatorNotice notice)
        {
            switch (notice)
            {
                case OperatorNotice.NoMenuHere:
                    return "No menu here. It only opens while spectating.";
                case OperatorNotice.FreeCamIsOff:
                    return "Free cam is off. Press G to turn it on.";
                case OperatorNotice.NoGameCamera:
                    return "Free cam hasn't started yet. Still waiting on the game.";
                case OperatorNotice.FreeCamStopped:
                    return "Free cam stopped working. Restart the game to get it back.";
                case OperatorNotice.NeedsFpfc:
                    return "FPFC Anywhere is off. Launch with FPFC on.";
                case OperatorNotice.WrongGameVersion:
                    return "FPFC Anywhere is off. It only runs on Beat Saber " + ModeGate.SupportedVersion + ".";
                case OperatorNotice.SettingsProblem:
                    return "A setting in " + SettingsFile.FileName + " wasn't used. The log says which.";
                default:
                    return string.Empty;
            }
        }

        public static OperatorNotice ForGate(GateDecision decision)
        {
            switch (decision)
            {
                case GateDecision.NoFpfcArgument:
                    return OperatorNotice.NeedsFpfc;
                case GateDecision.UnsupportedVersion:
                    return OperatorNotice.WrongGameVersion;
                default:
                    return OperatorNotice.None;
            }
        }
    }

    /// <summary>Decides how long a notice stays up. Only the latest one shows, so they never pile up.</summary>
    public sealed class NoticeTimer
    {
        public const float ShortSeconds = 3f;
        public const float LongSeconds = 8f;
        public const float FadeSeconds = 0.4f;

        // The startup notices land just before the game's first load, which arrives as one huge
        // frame. Capping the step means that frame cannot use up the notice before anyone sees it.
        private const float MaxStepSeconds = 0.1f;

        private float _remaining;

        public OperatorNotice Current { get; private set; }

        public float Opacity => Current == OperatorNotice.None ? 0f : Math.Min(1f, _remaining / FadeSeconds);

        public void Show(OperatorNotice notice)
        {
            if (notice == OperatorNotice.None)
            {
                return;
            }

            Current = notice;
            _remaining = IsWrongKeypress(notice) ? ShortSeconds : LongSeconds;
        }

        public void Tick(float deltaSeconds)
        {
            if (Current == OperatorNotice.None)
            {
                return;
            }

            _remaining -= Math.Min(Math.Max(deltaSeconds, 0f), MaxStepSeconds);
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                Current = OperatorNotice.None;
            }
        }

        private static bool IsWrongKeypress(OperatorNotice notice) =>
            notice == OperatorNotice.NoMenuHere || notice == OperatorNotice.FreeCamIsOff;
    }

    /// <summary>Sizes the notice from the window, so it reads the same on a stream at any resolution.</summary>
    public static class NoticeLayout
    {
        public const int SmallestFont = 16;
        public const int LargestFont = 64;

        private const float WindowHeightsPerFont = 42f;

        public static int FontSize(int screenHeight)
        {
            var size = (int)Math.Round(screenHeight / WindowHeightsPerFont);
            return Math.Min(Math.Max(size, SmallestFont), LargestFont);
        }

        /// <summary>Roughly the 4% broadcast safe area at 1080p, so stream overlays do not cover it.</summary>
        public static float Margin(int fontSize) => fontSize * 1.75f;

        public static float Padding(int fontSize) => fontSize * 0.6f;

        /// <summary>Text wider than this wraps. A tiny window still gets a usable width.</summary>
        public static float MaxTextWidth(int screenWidth, int fontSize)
        {
            var width = screenWidth - (Margin(fontSize) * 2f) - (Padding(fontSize) * 2f);
            return Math.Max(width, fontSize);
        }
    }
}
