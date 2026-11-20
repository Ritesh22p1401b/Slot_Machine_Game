using System;
using System.Collections.Generic;

namespace SlotMachine.Gameplay
{
    /// <summary>Holds the list of allowed bets and the currently selected one.</summary>
    public sealed class BetSelector
    {
        private readonly int[] options;
        private readonly int defaultIndex;

        public BetSelector(IReadOnlyList<int> betOptions, int defaultIndex)
        {
            if (betOptions == null || betOptions.Count == 0)
                throw new ArgumentException("At least one bet option is required.", nameof(betOptions));

            options = new int[betOptions.Count];
            for (int i = 0; i < options.Length; i++)
            {
                if (betOptions[i] <= 0)
                    throw new ArgumentException($"Bet option {betOptions[i]} must be positive.", nameof(betOptions));
                options[i] = betOptions[i];
            }

            if (defaultIndex < 0 || defaultIndex >= options.Length)
                throw new ArgumentOutOfRangeException(nameof(defaultIndex));

            this.defaultIndex = defaultIndex;
            SelectedIndex = defaultIndex;
        }

        public IReadOnlyList<int> Options => options;
        public int SelectedIndex { get; private set; }
        public int SelectedBet => options[SelectedIndex];

        /// <summary>Selects a bet option. Returns false when the index is out of range.</summary>
        public bool Select(int index)
        {
            if (index < 0 || index >= options.Length)
                return false;
            SelectedIndex = index;
            return true;
        }

        public void Reset() => SelectedIndex = defaultIndex;
    }
}
