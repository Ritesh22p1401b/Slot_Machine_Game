using System;
using System.Collections.Generic;
using SlotMachine.Data;

namespace SlotMachine.Gameplay
{
    /// <summary>
    /// Weighted random symbol selection.
    /// Null entries and entries with an empty ID, non-positive weight or non-positive multiplier
    /// are excluded (consistently, at construction time). Probability of a symbol is
    /// weight / sum(valid weights).
    /// </summary>
    public sealed class RNGService
    {
        private readonly IRandomProvider random;
        private readonly List<SymbolData> validSymbols = new List<SymbolData>();
        private readonly int totalWeight;

        /// <exception cref="ArgumentNullException">symbols or random is null.</exception>
        /// <exception cref="ArgumentException">No valid symbols, or the total weight exceeds int.MaxValue.</exception>
        public RNGService(IReadOnlyList<SymbolData> symbols, IRandomProvider random)
        {
            if (symbols == null) throw new ArgumentNullException(nameof(symbols));
            this.random = random ?? throw new ArgumentNullException(nameof(random));

            long sum = 0;
            foreach (SymbolData symbol in symbols)
            {
                if (symbol == null || !symbol.IsSelectable)
                {
                    ExcludedCount++;
                    continue;
                }

                validSymbols.Add(symbol);
                sum += symbol.Weight;
            }

            if (validSymbols.Count == 0)
                throw new ArgumentException("No valid symbols available for weighted selection.", nameof(symbols));
            if (sum > int.MaxValue)
                throw new ArgumentException("Total symbol weight exceeds the supported range.", nameof(symbols));

            totalWeight = (int)sum;
        }

        public IReadOnlyList<SymbolData> ValidSymbols => validSymbols;

        /// <summary>Number of supplied entries that were rejected as invalid.</summary>
        public int ExcludedCount { get; }

        public int TotalWeight => totalWeight;

        /// <summary>Configured probability of a symbol (0 when it is not a valid entry).</summary>
        public double GetProbability(SymbolData symbol)
        {
            return validSymbols.Contains(symbol) ? (double)symbol.Weight / totalWeight : 0d;
        }

        /// <summary>Draws one symbol according to the configured weights.</summary>
        public SymbolData SelectSymbol()
        {
            int roll = random.Range(0, totalWeight);
            if (roll < 0 || roll >= totalWeight)
                throw new InvalidOperationException($"Random provider returned {roll}, outside [0, {totalWeight}).");

            for (int i = 0; i < validSymbols.Count; i++)
            {
                int weight = validSymbols[i].Weight;
                if (roll < weight)
                    return validSymbols[i];
                roll -= weight;
            }

            // Unreachable while weights are positive and roll < totalWeight.
            throw new InvalidOperationException("Weighted selection fell outside every symbol interval.");
        }

        /// <summary>Fills <paramref name="buffer"/> with independent weighted draws.</summary>
        public void SelectSymbols(SymbolData[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = SelectSymbol();
        }
    }
}
