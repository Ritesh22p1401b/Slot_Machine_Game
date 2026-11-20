using System;
using NUnit.Framework;
using SlotMachine.Gameplay;

namespace SlotMachine.Tests
{
    public sealed class BetSelectorTests
    {
        [Test]
        public void DefaultBet_IsSelected()
        {
            var bets = new BetSelector(new[] { 10, 50, 100 }, 0);
            Assert.AreEqual(10, bets.SelectedBet);
        }

        [Test]
        public void Select_ChangesBet_AndRejectsOutOfRange()
        {
            var bets = new BetSelector(new[] { 10, 50, 100 }, 0);
            Assert.IsTrue(bets.Select(2));
            Assert.AreEqual(100, bets.SelectedBet);
            Assert.IsFalse(bets.Select(3));
            Assert.IsFalse(bets.Select(-1));
            Assert.AreEqual(100, bets.SelectedBet);
            bets.Reset();
            Assert.AreEqual(10, bets.SelectedBet);
        }

        [Test]
        public void InvalidOptions_Throw()
        {
            Assert.Throws<ArgumentException>(() => new BetSelector(new int[0], 0));
            Assert.Throws<ArgumentException>(() => new BetSelector(new[] { 10, 0 }, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BetSelector(new[] { 10 }, 1));
        }
    }

    public sealed class ReelMotionProfileTests
    {
        [TestCase(1.6f, 0f)]
        [TestCase(1.6f, 1f)]
        [TestCase(2.36f, 0.6f)]
        [TestCase(0.4f, 1.5f)]
        public void EndsExactlyOnWholeCell(float duration, float bounce)
        {
            var motion = new ReelMotionProfile(duration, 16f, 0.2f, 0.55f, bounce);
            Assert.AreEqual(motion.TotalCells, motion.Evaluate(motion.Duration));
            Assert.AreEqual(motion.TotalCells, motion.Evaluate(motion.Duration + 5f));
            Assert.AreEqual(0f, motion.Evaluate(0f));
            Assert.GreaterOrEqual(motion.TotalCells, 6);
        }

        [Test]
        public void IsContinuous_AndOvershootStaysBelowOneCell()
        {
            var motion = new ReelMotionProfile(2f, 16f, 0.2f, 0.55f, 1.5f);
            float previous = 0f;
            float maxStep = 0f;
            float peak = 0f;
            for (int i = 1; i <= 2000; i++)
            {
                float p = motion.Evaluate(motion.Duration * i / 2000f);
                maxStep = Math.Max(maxStep, Math.Abs(p - previous));
                peak = Math.Max(peak, p);
                previous = p;
            }

            // A 1 ms sample at ~16 cells/s should never jump by more than a small fraction of a cell.
            Assert.Less(maxStep, 0.1f);
            Assert.Less(peak - motion.TotalCells, 1f);
        }

        [Test]
        public void CubicStop_NeverOvershoots()
        {
            var motion = new ReelMotionProfile(1.6f, 16f, 0.2f, 0.55f, 0f);
            for (int i = 0; i <= 1000; i++)
                Assert.LessOrEqual(motion.Evaluate(motion.Duration * i / 1000f), motion.TotalCells + 1e-4f);
        }
    }
}
