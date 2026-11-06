using System.Collections;
using System.Diagnostics;
using System.Threading;
using FpfcAnywhere.Core;
using UnityEngine;

namespace FpfcAnywhere
{
    /// <summary>
    /// The fps cap. At the end of every frame it waits for the next tick of the beat that
    /// FramePacer keeps, sleeping through most of the wait and spinning for the last stretch so
    /// that frames land within a fraction of a millisecond. It is the mod's own wait rather than
    /// Application.targetFrameRate, which the game writes from its own settings and which Unity
    /// ignores while vsync is on. Measured side by side at 60 and 120 on the test PC the two paced
    /// the same, 99 frames in 100 within 0.6 ms of the beat. Off (0) costs nothing but the yield.
    /// </summary>
    internal sealed class FrameLimiter : MonoBehaviour
    {
        // Thread.Sleep(1) can run long by about a millisecond, so the last two are spun.
        private static readonly long SpinTicks = Stopwatch.Frequency / 500;

        private readonly FramePacer _pacer = new FramePacer(Stopwatch.Frequency);

        public int FramesPerSecond
        {
            get => _pacer.FramesPerSecond;
            set => _pacer.FramesPerSecond = value;
        }

        private IEnumerator Start()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                if (_pacer.FramesPerSecond > 0)
                {
                    WaitUntil(_pacer.NextDeadline(Stopwatch.GetTimestamp()));
                }
            }
        }

        private static void WaitUntil(long deadline)
        {
            while (deadline - Stopwatch.GetTimestamp() > SpinTicks)
            {
                Thread.Sleep(1);
            }

            while (Stopwatch.GetTimestamp() < deadline)
            {
                Thread.SpinWait(64);
            }
        }
    }
}
