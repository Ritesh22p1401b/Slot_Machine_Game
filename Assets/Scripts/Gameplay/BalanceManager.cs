using System;

namespace SlotMachine.Gameplay
{
    /// <summary>
    /// Virtual credit balance. Bets are debited once at spin start; payouts credited once after
    /// evaluation. The balance can never become negative and is capped at <see cref="MaxBalance"/>.
    /// </summary>
    public sealed class BalanceManager
    {
        public const long MaxBalance = 999_999_999L;

        public BalanceManager(long startingBalance)
        {
            if (startingBalance < 0 || startingBalance > MaxBalance)
                throw new ArgumentOutOfRangeException(nameof(startingBalance), startingBalance, "Starting balance is out of range.");
            StartingBalance = startingBalance;
            Balance = startingBalance;
        }

        public long StartingBalance { get; }
        public long Balance { get; private set; }

        /// <summary>Raised with the new balance whenever it changes.</summary>
        public event Action<long> BalanceChanged;

        public bool CanAfford(int bet) => bet > 0 && bet <= Balance;

        /// <summary>Debits the bet. Returns false (balance unchanged) if the bet is invalid or unaffordable.</summary>
        public bool TryPlaceBet(int bet)
        {
            if (!CanAfford(bet))
                return false;
            Balance -= bet;
            BalanceChanged?.Invoke(Balance);
            return true;
        }

        /// <summary>Adds winnings. Returns the amount actually credited (clamped at MaxBalance).</summary>
        public long AddPayout(long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "Payout must not be negative.");
            if (amount == 0) return 0;

            long credited = Math.Min(amount, MaxBalance - Balance);
            Balance += credited;
            BalanceChanged?.Invoke(Balance);
            return credited;
        }

        /// <summary>Returns a bet that was debited but whose spin could not complete.</summary>
        public void Refund(int bet)
        {
            if (bet <= 0) throw new ArgumentOutOfRangeException(nameof(bet), bet, "Refund must be positive.");
            Balance = Math.Min(MaxBalance, Balance + bet);
            BalanceChanged?.Invoke(Balance);
        }

        public void Reset()
        {
            Balance = StartingBalance;
            BalanceChanged?.Invoke(Balance);
        }
    }
}
