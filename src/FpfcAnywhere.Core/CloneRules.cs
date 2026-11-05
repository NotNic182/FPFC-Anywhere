using System;
using System.Linq;

namespace FpfcAnywhere.Core
{
    /// <summary>Decides which components survive on the cloned camera.</summary>
    public static class CloneRules
    {
        // An allowlist, so anything a future update adds is removed rather than left running.
        private static readonly string[] Kept =
        {
            "UnityEngine.Transform",
            "UnityEngine.Camera",
            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData",
            "MainEffectController",
            "BloomPrePass",
        };

        public static bool Keep(string componentTypeFullName) => Kept.Contains(componentTypeFullName, StringComparer.Ordinal);
    }
}
