using System;
using FpfcAnywhere.Core;
using UnityEngine;

namespace FpfcAnywhere
{
    /// <summary>
    /// Entry point. Unity calls <see cref="Init"/> itself because the installer lists it in
    /// RuntimeInitializeOnLoads.json, so no patcher or proxy DLL is involved.
    /// </summary>
    public static class Bootstrap
    {
        public static void Init()
        {
            // Nothing thrown from here may reach the game's startup.
            try
            {
                ModLog.Start();

                var arguments = Environment.GetCommandLineArgs();
                var decision = ModeGate.Evaluate(arguments, Application.version);
                ModLog.Info($"Game {Application.version}, Unity {Application.unityVersion}, decision: {decision}");

                if (decision != GateDecision.Run)
                {
                    return;
                }

                var host = new GameObject("FpfcAnywhere");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<ModHost>();
            }
            catch (Exception exception)
            {
                ModLog.Error("Startup failed, the mod is off for this run", exception);
            }
        }
    }
}
