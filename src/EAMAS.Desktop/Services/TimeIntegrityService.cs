using System.Diagnostics;

namespace EAMAS.Desktop.Services
{
    /// <summary>
    /// Detects system clock manipulation by comparing wall-clock time (DateTime.UtcNow)
    /// against a monotonic hardware clock (System.Diagnostics.Stopwatch).
    ///
    /// Stopwatch uses QueryPerformanceCounter / the CPU's TSC — it is immune to
    /// manual date/time changes, NTP adjustments, and timezone switches.
    ///
    /// Usage:
    ///   – Call <see cref="GetTrustedUtcNow"/> instead of DateTime.UtcNow when recording timestamps.
    ///   – Call <see cref="GetTrueElapsedSince"/> to get the real wall-time since a given point.
    ///   – Call <see cref="DetectClockJump"/> periodically to check for manipulation.
    /// </summary>
    public class TimeIntegrityService
    {
        // Anchors: we record "what the clock said" and "what the stopwatch said" at startup.
        // Any divergence between the two is a clock manipulation.
        private readonly DateTime _anchorUtc;
        private readonly Stopwatch _stopwatch;

        // Tolerance: small clock drifts happen naturally (NTP sync, daylight saving, etc).
        // We only flag jumps larger than this threshold.
        private static readonly TimeSpan JumpThreshold = TimeSpan.FromSeconds(30);

        // Maximum plausible duration for a single activity session (poll interval × safety factor).
        // If a session claims to be longer than this, it was inflated by a clock change.
        private static readonly TimeSpan MaxSessionDuration = TimeSpan.FromMinutes(15);

        // Track last known good state for jump detection
        private DateTime _lastWallClock;
        private TimeSpan _lastStopwatchElapsed;
        private readonly object _lock = new();

        public TimeIntegrityService()
        {
            _anchorUtc = DateTime.UtcNow;
            _stopwatch = Stopwatch.StartNew();
            _lastWallClock = _anchorUtc;
            _lastStopwatchElapsed = TimeSpan.Zero;
        }

        /// <summary>
        /// Returns a trusted UTC timestamp derived from the monotonic stopwatch.
        /// This value cannot be inflated by changing the system clock.
        /// </summary>
        public DateTime GetTrustedUtcNow()
        {
            return _anchorUtc + _stopwatch.Elapsed;
        }

        /// <summary>
        /// Returns the true elapsed time since <paramref name="since"/>,
        /// measured by the monotonic stopwatch rather than by comparing wall-clock times.
        /// </summary>
        public TimeSpan GetTrueElapsedSince(TimeSpan stopwatchSnapshotAtStart)
        {
            return _stopwatch.Elapsed - stopwatchSnapshotAtStart;
        }

        /// <summary>
        /// Returns the current stopwatch snapshot. Store this when a session starts,
        /// then pass it to <see cref="GetTrueElapsedSince"/> when the session ends.
        /// </summary>
        public TimeSpan GetMonotonicSnapshot()
        {
            return _stopwatch.Elapsed;
        }

        /// <summary>
        /// Detects if the system clock has jumped since the last check.
        /// Returns the jump magnitude (positive = forward, negative = backward)
        /// and null if no significant jump was detected.
        /// </summary>
        public TimeSpan? DetectClockJump()
        {
            lock (_lock)
            {
                var currentWall = DateTime.UtcNow;
                var currentElapsed = _stopwatch.Elapsed;

                // How much wall-clock time passed since last check?
                var wallDelta = currentWall - _lastWallClock;
                // How much real (monotonic) time passed since last check?
                var realDelta = currentElapsed - _lastStopwatchElapsed;

                // The difference between wall-clock delta and real delta is the "jump"
                var drift = wallDelta - realDelta;

                _lastWallClock = currentWall;
                _lastStopwatchElapsed = currentElapsed;

                if (drift.Duration() > JumpThreshold)
                    return drift;

                return null;
            }
        }

        /// <summary>
        /// Clamps a session duration to prevent inflation from clock manipulation.
        /// Uses the monotonic stopwatch to verify the claimed duration.
        ///
        /// If the wall-clock duration exceeds the real elapsed time (+ tolerance),
        /// the duration is capped to the real elapsed time.
        /// </summary>
        /// <param name="wallClockStart">Session start from DateTime.UtcNow at that time.</param>
        /// <param name="wallClockEnd">Session end from DateTime.UtcNow now.</param>
        /// <param name="monotonicStart">Stopwatch snapshot taken at session start.</param>
        /// <returns>
        /// A validated (start, end) pair where the duration is clamped to real elapsed time.
        /// Also returns whether the duration was adjusted.
        /// </returns>
        public (DateTime Start, DateTime End, bool WasAdjusted) ValidateSessionDuration(
            DateTime wallClockStart, DateTime wallClockEnd, TimeSpan monotonicStart)
        {
            var wallDuration = wallClockEnd - wallClockStart;
            var realDuration = GetTrueElapsedSince(monotonicStart);

            // Allow small tolerance (the poll interval can introduce up to ~5 seconds of natural drift)
            var tolerance = TimeSpan.FromSeconds(10);
            var maxAllowed = realDuration + tolerance;

            // Also enforce the absolute maximum session cap
            if (maxAllowed > MaxSessionDuration)
                maxAllowed = MaxSessionDuration;

            if (wallDuration <= maxAllowed)
            {
                // Duration looks legitimate — no adjustment needed.
                // Still cap to MaxSessionDuration as a safety net.
                if (wallDuration > MaxSessionDuration)
                {
                    var cappedEnd = wallClockStart + MaxSessionDuration;
                    return (wallClockStart, cappedEnd, true);
                }
                return (wallClockStart, wallClockEnd, false);
            }

            // Wall-clock duration exceeds real duration — clock was moved forward.
            // Use the trusted time as the end instead.
            var trustedEnd = wallClockStart + realDuration;
            return (wallClockStart, trustedEnd, true);
        }

        /// <summary>
        /// Simple check: is the claimed duration suspicious compared to
        /// what the poll interval should produce?
        /// </summary>
        public static bool IsDurationSuspicious(TimeSpan duration, int pollIntervalSeconds)
        {
            // A session recorded between two polls should never exceed
            // 3× the poll interval (generous safety factor for system load/sleep).
            var maxReasonable = TimeSpan.FromSeconds(pollIntervalSeconds * 3);
            return duration > maxReasonable && duration > MaxSessionDuration;
        }
    }
}
