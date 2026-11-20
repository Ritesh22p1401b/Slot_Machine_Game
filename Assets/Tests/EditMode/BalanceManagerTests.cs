using System;
using NUnit.Framework;
using SlotMachine.Gameplay;

namespace SlotMachine.Tests
{
    public sealed class BalanceManagerTests
    {
        [Test]
        public void StartingBalance_Initializes()
        {
            var balance = new BalanceManager(1000);
            Assert.AreEqual(1000, balance.Balance);
            Assert.AreEqual(1000, balance.StartingBalance);
        }

        [TestCase(-1)]
        [TestCase(BalanceManager.MaxBalance + 1)]
        public void InvalidStartingBalance_Throws(long start) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new BalanceManager(start));

        [Test]
        public void ValidBet_DeductsExactlyOnce()
        {
            var balance = new BalanceManager(1000);
            int changes = 0;
            balance.BalanceChanged += _ => changes++;
            Assert.IsTrue(balance.TryPlaceBet(10));
            Assert.AreEqual(990, balance.Balance);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void InsufficientBalance_RejectsWithoutChange()
        {
            var balance = new BalanceManager(40);
            Assert.IsFalse(balance.TryPlaceBet(50));
            Assert.AreEqual(40, balance.Balance);
        }

        [Test]
        public void ExactBalanceBet_IsAllowed_AndReachesZero()
        {
            var balance = new BalanceManager(50);
            Assert.IsTrue(balance.TryPlaceBet(50));
            Assert.AreEqual(0, balance.Balance);
            Assert.IsFalse(balance.TryPlaceBet(10));
            Assert.AreEqual(0, balance.Balance);
        }

        [TestCase(0)]
        [TestCase(-10)]
        public void NonPositiveBet_IsRejected(int bet)
        {
            var balance = new BalanceManager(1000);
            Assert.IsFalse(balance.TryPlaceBet(bet));
            Assert.AreEqual(1000, balance.Balance);
        }

        [Test]
        public void Payout_AddsCredits()
        {
            var balance = new BalanceManager(1000);
            balance.TryPlaceBet(10);
            Assert.AreEqual(200, balance.AddPayout(200));
            Assert.AreEqual(1190, balance.Balance);
        }

        [Test]
        public void NegativePayout_Throws() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new BalanceManager(10).AddPayout(-1));

        [Test]
        public void Payout_IsClampedAtMaximum()
        {
            var balance = new BalanceManager(BalanceManager.MaxBalance - 5);
            Assert.AreEqual(5, balance.AddPayout(1000));
            Assert.AreEqual(BalanceManager.MaxBalance, balance.Balance);
        }

        [Test]
        public void Reset_RestoresStartingCredits()
        {
            var balance = new BalanceManager(1000);
            balance.TryPlaceBet(100);
            balance.AddPayout(500);
            balance.Reset();
            Assert.AreEqual(1000, balance.Balance);
        }

        [Test]
        public void Refund_ReturnsBet()
        {
            var balance = new BalanceManager(100);
            balance.TryPlaceBet(50);
            balance.Refund(50);
            Assert.AreEqual(100, balance.Balance);
        }

        [Test]
        public void Balance_NeverBecomesNegative_UnderRepeatedBets()
        {
            var balance = new BalanceManager(95);
            for (int i = 0; i < 20; i++)
            {
                balance.TryPlaceBet(10);
                Assert.GreaterOrEqual(balance.Balance, 0);
            }

            Assert.AreEqual(5, balance.Balance);
        }
    }
}
