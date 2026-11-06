using System;

namespace FpfcAnywhere.Core
{
    /// <summary>
    /// Works out when each frame should end so that frames come at a steady rate. The beat carries
    /// over from one frame to the next, so a quick frame waits for the next tick of the beat rather
    /// than a whole period from wherever it happened to finish. A frame that overruns is never
    /// waited for: the beat restarts from it.
    /// </summary>
    public sealed class FramePacer
    {
        private readonly long _ticksPerSecond;
        private int _framesPerSecond;
        private double _deadline;
        private bool _running;

        public FramePacer(long ticksPerSecond)
        {
            _ticksPerSecond = ticksPerSecond;
        }

        /// <summary>The cap. 0 or less is no cap.</summary>
        public int FramesPerSecond
        {
            get => _framesPerSecond;
            set
            {
                _framesPerSecond = value;
                if (value <= 0)
                {
                    _running = false;
                }
            }
        }

        /// <summary>
        /// The tick the frame that just ended should wait until. Returns <paramref name="now"/>
        /// when there is nothing to wait for.
        /// </summary>
        public long NextDeadline(long now)
        {
            if (_framesPerSecond <= 0)
            {
                _running = false;
                return now;
            }

            var period = (double)_ticksPerSecond / _framesPerSecond;
            var next = _running ? _deadline + period : now + period;
            if (next < now)
            {
                next = now;
            }

            _running = true;
            _deadline = next;
            return (long)Math.Round(next);
        }
    }
}
