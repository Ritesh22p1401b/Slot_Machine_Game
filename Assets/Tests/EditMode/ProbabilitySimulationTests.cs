using System;
using System.Collections.Generic;
using NUnit.Framework;
using SlotMachine.Data;
using SlotMachine.Gameplay;

namespace SlotMachine.Tests
{
    /// <summary>
    /// Statistical sanity check (not run during gameplay). Observed frequencies should be close to
    /// the configured weights; tolerance is ~5 standard deviations so the test is not flaky.
    /// </summary>
    public sealed class ProbabilitySimulationTests
    {
        [Test]
        public void ObservedFrequencies_MatchConfiguredWeights()
        {
            using (var symbols = new TestSymbols())
            {
                const int draws = 200_000;
                var service = new RNGService(symbols.All, new SeededRandomProvider(20261120));
                var counts = new Dictionary<SymbolData, int>();
                foreach (SymbolData symbol in symbols.All)
                    counts[symbol] = 0;

                for (int i = 0; i < draws; i++)
                    counts[service.SelectSymbol()]++;

                foreach (SymbolData symbol in symbols.All)
                {
                    double expected = service.GetProbability(symbol);
                    double observed = (double)counts[symbol] / draws;
                    double sigma = Math.Sqrt(expected * (1 - expected) / draws);
                    Assert.AreEqual(expected, observed, 5 * sigma, $"{symbol.Id}: expected {expected:P2}, observed {observed:P2}");
                }
            }
        }

        [Test]
        public void UnityRandomProvider_StaysWithinRange()
        {
            using (var symbols = new TestSymbols())
            {
                var service = new RNGService(symbols.All, new UnityRandomProvider());
                var counts = new Dictionary<string, int>();
                for (int i = 0; i < 20_000; i++)
                {
                    string id = service.SelectSymbol().Id;
                    counts[id] = counts.TryGetValue(id, out int c) ? c + 1 : 1;
                }

                Assert.AreEqual(4, counts.Count, "Every configured symbol should appear in 20,000 draws.");
            }
        }
    }
}
