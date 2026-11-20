using System;
using System.Collections.Generic;
using SlotMachine.Data;

namespace SlotMachine.Gameplay
{
    /// <summary>Result of evaluating the payline for one spin.</summary>
    public readonly struct SpinEvaluation
    {
        public SpinEvaluation(bool isWin, SymbolData winningSymbol, long payout)
        {
            IsWin = isWin;
            WinningSymbol = winningSymbol;
            Payout = payout;
        }

        public bool IsWin { get; }

        /// <summary>The matched symbol, or null on a loss.</summary>
        public SymbolData WinningSymbol { get; }

        /// <summary>Credits to add to the balance (the bet has already been deducted).</summary>
        public long Payout { get; }

        public static SpinEvaluation Loss => new SpinEvaluation(false, null, 0);
    }

    /// <summary>
    /// Pure payline evaluation.
    /// RULE: the only payline is the middle horizontal row. A spin wins when the center symbol of
    /// every reel is the same symbol; payout = bet x that symbol's multiplier. Top and bottom rows
    /// are visual only. Change this class if a different payline rule is required.
    /// </summary>
    public sealed class PayoutCalculator
    {
        public PayoutCalculator(int reelCount = 3)
        {
            if (reelCount <= 0) throw new ArgumentOutOfRangeException(nameof(reelCount));
            ReelCount = reelCount;
        }

        public int ReelCount { get; }

        /// <param name="centerRow">Center symbol of each reel, left to right.</param>
        /// <param name="bet">Credits wagered; must be positive.</param>
        /// <exception cref="ArgumentNullException">centerRow is null.</exception>
        /// <exception cref="ArgumentException">Wrong symbol count, or a null symbol in the row.</exception>
        /// <exception cref="ArgumentOutOfRangeException">bet is zero or negative.</exception>
        public SpinEvaluation Evaluate(IReadOnlyList<SymbolData> centerRow, int bet)
        {
            if (centerRow == null) throw new ArgumentNullException(nameof(centerRow));
            if (centerRow.Count != ReelCount)
                throw new ArgumentException($"Expected {ReelCount} center symbols but got {centerRow.Count}.", nameof(centerRow));
            if (bet <= 0) throw new ArgumentOutOfRangeException(nameof(bet), bet, "Bet must be positive.");

            for (int i = 0; i < centerRow.Count; i++)
            {
                if (centerRow[i] == null)
                    throw new ArgumentException($"Center symbol {i} is null.", nameof(centerRow));
            }

            SymbolData first = centerRow[0];
            for (int i = 1; i < centerRow.Count; i++)
            {
                if (!ReferenceEquals(centerRow[i], first) && centerRow[i].Id != first.Id)
                    return SpinEvaluation.Loss;
            }

            // long arithmetic: int x int cannot overflow a long.
            long payout = (long)bet * first.PayoutMultiplier;
            return new SpinEvaluation(true, first, payout);
        }
    }
}
