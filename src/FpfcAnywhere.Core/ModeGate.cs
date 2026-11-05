using System;
using System.Collections.Generic;
using System.Linq;

namespace FpfcAnywhere.Core
{
    public enum GateDecision
    {
        Run,
        NoFpfcArgument,
        UnsupportedVersion,
    }

    /// <summary>Decides at startup whether the mod runs at all.</summary>
    public static class ModeGate
    {
        public const string SupportedVersion = "1.45.3";

        private static readonly string[] FpfcArguments = { "fpfc", "-fpfc" };

        /// <summary>
        /// The mod only runs under FPFC, because the game skips its headset-off pause only there.
        /// It also refuses unchecked versions: an update could move what gets sent to other
        /// players, and the safety checks would have to be repeated.
        /// </summary>
        public static GateDecision Evaluate(IReadOnlyList<string> arguments, string gameVersion)
        {
            if (!arguments.Any(argument => FpfcArguments.Contains(argument, StringComparer.Ordinal)))
            {
                return GateDecision.NoFpfcArgument;
            }

            var isSupported = gameVersion == SupportedVersion
                || gameVersion.StartsWith(SupportedVersion + "_", StringComparison.Ordinal);

            return isSupported ? GateDecision.Run : GateDecision.UnsupportedVersion;
        }
    }
}
