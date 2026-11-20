using System.Collections.Generic;
using UnityEngine;

namespace SlotMachine.Data
{
    /// <summary>Credits, bet options and reel timing. Edit in the Inspector to tune the game.</summary>
    [CreateAssetMenu(fileName = "SlotMachineSettings", menuName = "Slot Machine/Settings", order = 2)]
    public sealed class SlotMachineSettings : ScriptableObject
    {
        [Header("Credits")]
        [SerializeField, Min(0)] private long startingBalance = 1000;
        [SerializeField] private int[] betOptions = { 10, 50, 100 };
        [SerializeField, Min(0)] private int defaultBetIndex;

        [Header("Reel timing (seconds)")]
        [SerializeField, Min(0.3f), Tooltip("Spin time of the first reel.")]
        private float firstReelDuration = 1.6f;
        [SerializeField, Min(0f), Tooltip("Extra time each subsequent reel spins before stopping.")]
        private float reelStopStagger = 0.38f;
        [SerializeField, Min(0f), Tooltip("Delay between reel start times.")]
        private float reelStartStagger = 0.08f;
        [SerializeField, Min(1f), Tooltip("Cruise speed in symbol cells per second.")]
        private float spinSpeed = 16f;
        [SerializeField, Min(0.05f)] private float accelerationTime = 0.2f;
        [SerializeField, Min(0.1f), Tooltip("Length of the ease-out stop.")]
        private float decelerationTime = 0.55f;
        [SerializeField, Range(0f, 1.5f), Tooltip("0 = pure cubic ease-out, higher = small overshoot bounce.")]
        private float stopBounce = 0.6f;
        [SerializeField, Min(0f), Tooltip("How long the win/lose state is held before returning to Ready.")]
        private float resultHoldTime = 0.6f;

        public long StartingBalance => startingBalance;
        public IReadOnlyList<int> BetOptions => betOptions;
        public int DefaultBetIndex => defaultBetIndex;
        public float FirstReelDuration => firstReelDuration;
        public float ReelStopStagger => reelStopStagger;
        public float ReelStartStagger => reelStartStagger;
        public float SpinSpeed => spinSpeed;
        public float AccelerationTime => accelerationTime;
        public float DecelerationTime => decelerationTime;
        public float StopBounce => stopBounce;
        public float ResultHoldTime => resultHoldTime;

        public bool Validate(List<string> errors)
        {
            int startCount = errors.Count;
            if (startingBalance < 0)
                errors.Add($"{name}: starting balance must not be negative.");
            if (betOptions == null || betOptions.Length == 0)
            {
                errors.Add($"{name}: at least one bet option is required.");
            }
            else
            {
                foreach (int bet in betOptions)
                {
                    if (bet <= 0)
                        errors.Add($"{name}: bet option {bet} must be positive.");
                }

                if (defaultBetIndex < 0 || defaultBetIndex >= betOptions.Length)
                    errors.Add($"{name}: default bet index {defaultBetIndex} is out of range.");
            }

            if (decelerationTime + accelerationTime > firstReelDuration)
                errors.Add($"{name}: acceleration + deceleration time must not exceed the first reel duration.");

            return errors.Count == startCount;
        }
    }
}
