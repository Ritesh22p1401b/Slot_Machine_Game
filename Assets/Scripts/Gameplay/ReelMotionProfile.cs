using System;

namespace SlotMachine.Gameplay
{
    /// <summary>
    /// Position-over-time curve for one reel, measured in symbol cells.
    /// Phases: quadratic ease-in -> constant cruise -> ease-out (cubic, optionally with a small
    /// "back" overshoot). The cruise speed is adjusted slightly so that the reel travels an exact
    /// whole number of cells, which guarantees that the stop position is perfectly cell-aligned.
    /// Velocity is continuous across all phase boundaries.
    /// </summary>
    public sealed class ReelMotionProfile
    {
        private readonly float accelTime;
        private readonly float cruiseTime;
        private readonly float decelTime;
        private readonly float bounce;
        private readonly float speed;
        private readonly float accelDistance;
        private readonly float cruiseEndDistance;
        private readonly float decelDistance;

        /// <param name="duration">Total time from start to rest.</param>
        /// <param name="targetSpeed">Desired cruise speed in cells per second.</param>
        /// <param name="accelerationTime">Ease-in time.</param>
        /// <param name="decelerationTime">Ease-out time.</param>
        /// <param name="stopBounce">0 = cubic ease-out; larger values overshoot slightly before settling.</param>
        /// <param name="minimumCells">Lower bound on travelled cells.</param>
        public ReelMotionProfile(float duration, float targetSpeed, float accelerationTime, float decelerationTime, float stopBounce, int minimumCells = 6)
        {
            if (duration <= 0f) throw new ArgumentOutOfRangeException(nameof(duration));
            if (targetSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(targetSpeed));
            if (stopBounce < 0f) throw new ArgumentOutOfRangeException(nameof(stopBounce));

            // Scale the ease phases down if they would not fit inside the duration.
            float easeTotal = accelerationTime + decelerationTime;
            float scale = easeTotal > duration ? duration / easeTotal : 1f;
            accelTime = Math.Max(0.0001f, accelerationTime * scale);
            decelTime = Math.Max(0.0001f, decelerationTime * scale);
            cruiseTime = Math.Max(0f, duration - accelTime - decelTime);
            bounce = stopBounce;

            // distance = v * (a/2 + cruise + c/(b+3)); solve v for an integer distance.
            float distancePerSpeed = accelTime * 0.5f + cruiseTime + decelTime / (bounce + 3f);
            TotalCells = Math.Max(minimumCells, (int)Math.Round(targetSpeed * distancePerSpeed));
            speed = TotalCells / distancePerSpeed;

            accelDistance = speed * accelTime * 0.5f;
            cruiseEndDistance = accelDistance + speed * cruiseTime;
            decelDistance = speed * decelTime / (bounce + 3f);
            Duration = accelTime + cruiseTime + decelTime;
        }

        /// <summary>Whole number of cells travelled from start to rest.</summary>
        public int TotalCells { get; }

        public float Duration { get; }

        public float CruiseSpeed => speed;

        /// <summary>Position in cells at time t (clamped to [0, Duration]). Evaluate(Duration) == TotalCells.</summary>
        public float Evaluate(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= Duration) return TotalCells;

            if (t < accelTime)
                return 0.5f * speed * t * t / accelTime;

            float cruiseEnd = accelTime + cruiseTime;
            if (t < cruiseEnd)
                return accelDistance + speed * (t - accelTime);

            float s = (t - cruiseEnd) / decelTime;
            return cruiseEndDistance + decelDistance * EaseOutBack(s, bounce);
        }

        /// <summary>1 + (b+1)(s-1)^3 + b(s-1)^2. With b = 0 this is the cubic ease-out 1-(1-s)^3.</summary>
        private static float EaseOutBack(float s, float b)
        {
            float u = s - 1f;
            return 1f + (b + 1f) * u * u * u + b * u * u;
        }
    }
}
