using System;
using NUnit.Framework;
using SlotMachine.Data;
using SlotMachine.Gameplay;

namespace SlotMachine.Tests
{
    public sealed class PayoutCalculatorTests
    {
        private TestSymbols symbols;
        private PayoutCalculator calculator;

        [SetUp]
        public void SetUp()
        {
            symbols = new TestSymbols();
            calculator = new PayoutCalculator();
        }

        [TearDown]
        public void TearDown() => symbols.Dispose();

        [TestCase(10, 200)]
        [TestCase(50, 1000)]
        [TestCase(100, 2000)]
        public void ThreeSevens_PaysBetTimesTwenty(int bet, long expected)
        {
            SpinEvaluation result = calculator.Evaluate(Row(symbols.Seven, symbols.Seven, symbols.Seven), bet);
            Assert.IsTrue(result.IsWin);
            Assert.AreEqual(symbols.Seven, result.WinningSymbol);
            Assert.AreEqual(expected, result.Payout);
        }

        [Test]
        public void ThreeCherries_PaysBetTimesTwo() =>
            Assert.AreEqual(20, calculator.Evaluate(Row(symbols.Cherries, symbols.Cherries, symbols.Cherries), 10).Payout);

        [Test]
        public void ThreeBells_PaysBetTimesFive() =>
            Assert.AreEqual(50, calculator.Evaluate(Row(symbols.Bell, symbols.Bell, symbols.Bell), 10).Payout);

        [Test]
        public void ThreeBars_PaysBetTimesTen() =>
            Assert.AreEqual(100, calculator.Evaluate(Row(symbols.Bar, symbols.Bar, symbols.Bar), 10).Payout);

        [Test]
        public void AnyMismatch_PaysZero()
        {
            SymbolData s = symbols.Seven, b = symbols.Bell;
            foreach (SymbolData[] row in new[] { Row(s, s, b), Row(b, s, s), Row(s, b, s), Row(symbols.Bar, symbols.Bell, symbols.Cherries) })
            {
                SpinEvaluation result = calculator.Evaluate(row, 10);
                Assert.IsFalse(result.IsWin);
                Assert.AreEqual(0, result.Payout);
                Assert.IsNull(result.WinningSymbol);
            }
        }

        [Test]
        public void NullRow_IsRejected() =>
            Assert.Throws<ArgumentNullException>(() => calculator.Evaluate(null, 10));

        [Test]
        public void WrongResultCount_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => calculator.Evaluate(Row(symbols.Seven, symbols.Seven), 10));
            Assert.Throws<ArgumentException>(() => calculator.Evaluate(Row(symbols.Seven, symbols.Seven, symbols.Seven, symbols.Seven), 10));
        }

        [TestCase(0)]
        [TestCase(-10)]
        public void NonPositiveBet_IsRejected(int bet) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Evaluate(Row(symbols.Seven, symbols.Seven, symbols.Seven), bet));

        [Test]
        public void NullSymbolInRow_IsRejected() =>
            Assert.Throws<ArgumentException>(() => calculator.Evaluate(Row(symbols.Seven, null, symbols.Seven), 10));

        [Test]
        public void LargeBet_DoesNotOverflow()
        {
            SymbolData big = symbols.Create("BIG", 1, int.MaxValue);
            SpinEvaluation result = calculator.Evaluate(Row(big, big, big), int.MaxValue);
            Assert.AreEqual((long)int.MaxValue * int.MaxValue, result.Payout);
        }

        private static SymbolData[] Row(params SymbolData[] row) => row;
    }
}
