using System;
using System.Collections.Generic;
using NUnit.Framework;
using SlotMachine.Data;
using SlotMachine.Gameplay;

namespace SlotMachine.Tests
{
    public sealed class RNGServiceTests
    {
        private TestSymbols symbols;

        [SetUp]
        public void SetUp() => symbols = new TestSymbols();

        [TearDown]
        public void TearDown() => symbols.Dispose();

        [Test]
        public void EmptyList_Throws() =>
            Assert.Throws<ArgumentException>(() => new RNGService(new SymbolData[0], new SequenceRandomProvider()));

        [Test]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new RNGService(null, new SequenceRandomProvider()));
            Assert.Throws<ArgumentNullException>(() => new RNGService(symbols.All, null));
        }

        [Test]
        public void NullEntries_AreExcluded()
        {
            var service = new RNGService(new[] { null, symbols.Seven, null }, new SequenceRandomProvider(0));
            Assert.AreEqual(2, service.ExcludedCount);
            Assert.AreEqual(1, service.ValidSymbols.Count);
            Assert.AreEqual(symbols.Seven, service.SelectSymbol());
        }

        [Test]
        public void InvalidWeights_AreExcludedConsistently()
        {
            SymbolData zero = symbols.Create("ZERO", 0, 5);
            SymbolData negative = symbols.Create("NEG", -3, 5);
            SymbolData noPay = symbols.Create("NOPAY", 5, 0);
            var service = new RNGService(new[] { zero, symbols.Bell, negative, noPay }, new SequenceRandomProvider());
            Assert.AreEqual(3, service.ExcludedCount);
            CollectionAssert.AreEqual(new[] { symbols.Bell }, service.ValidSymbols);
            Assert.AreEqual(20, service.TotalWeight);
        }

        [Test]
        public void OnlyInvalidEntries_Throws()
        {
            SymbolData zero = symbols.Create("ZERO", 0, 5);
            Assert.Throws<ArgumentException>(() => new RNGService(new[] { zero, null }, new SequenceRandomProvider()));
        }

        // Weights in order: SEVEN 10 [0,9], CHERRIES 30 [10,39], BELL 20 [40,59], BAR 15 [60,74]. Total 75.
        [TestCase(0, "SEVEN")]
        [TestCase(9, "SEVEN")]
        [TestCase(10, "CHERRIES")]
        [TestCase(39, "CHERRIES")]
        [TestCase(40, "BELL")]
        [TestCase(59, "BELL")]
        [TestCase(60, "BAR")]
        [TestCase(74, "BAR")]
        public void Roll_SelectsSymbolOwningThatInterval(int roll, string expectedId)
        {
            var provider = new SequenceRandomProvider(roll);
            var service = new RNGService(symbols.All, provider);
            Assert.AreEqual(expectedId, service.SelectSymbol().Id);
            Assert.AreEqual(75, provider.LastMax, "Roll range must be [0, totalWeight).");
        }

        [TestCase(-1)]
        [TestCase(75)]
        public void OutOfRangeRoll_Throws(int roll)
        {
            var service = new RNGService(symbols.All, new SequenceRandomProvider(roll));
            Assert.Throws<InvalidOperationException>(() => service.SelectSymbol());
        }

        [Test]
        public void Selection_NeverReturnsSymbolOutsideDatabase()
        {
            var service = new RNGService(symbols.All, new SeededRandomProvider(1234));
            var allowed = new HashSet<SymbolData>(symbols.All);
            for (int i = 0; i < 5000; i++)
                Assert.IsTrue(allowed.Contains(service.SelectSymbol()));
        }

        [Test]
        public void Probability_IsWeightOverTotal()
        {
            var service = new RNGService(symbols.All, new SequenceRandomProvider());
            Assert.AreEqual(10d / 75d, service.GetProbability(symbols.Seven), 1e-9);
            Assert.AreEqual(30d / 75d, service.GetProbability(symbols.Cherries), 1e-9);
        }

        [Test]
        public void SelectSymbols_FillsBuffer()
        {
            var service = new RNGService(symbols.All, new SequenceRandomProvider(0, 10, 40));
            var buffer = new SymbolData[3];
            service.SelectSymbols(buffer);
            CollectionAssert.AreEqual(new[] { symbols.Seven, symbols.Cherries, symbols.Bell }, buffer);
        }
    }
}
