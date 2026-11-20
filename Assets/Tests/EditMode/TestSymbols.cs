using System.Collections.Generic;
using SlotMachine.Data;
using SlotMachine.Gameplay;
using UnityEngine;

namespace SlotMachine.Tests
{
    /// <summary>Creates in-memory symbols matching the demonstration configuration.</summary>
    internal sealed class TestSymbols : System.IDisposable
    {
        private readonly List<Object> created = new List<Object>();

        public TestSymbols()
        {
            Seven = Create("SEVEN", 10, 20);
            Cherries = Create("CHERRIES", 30, 2);
            Bell = Create("BELL", 20, 5);
            Bar = Create("BAR", 15, 10);
            All = new[] { Seven, Cherries, Bell, Bar };
        }

        public SymbolData Seven { get; }
        public SymbolData Cherries { get; }
        public SymbolData Bell { get; }
        public SymbolData Bar { get; }
        public SymbolData[] All { get; }

        public SymbolData Create(string id, int weight, int multiplier)
        {
            var symbol = ScriptableObject.CreateInstance<SymbolData>();
            symbol.name = id;
            symbol.Configure(id, id, null, weight, multiplier);
            created.Add(symbol);
            return symbol;
        }

        public void Dispose()
        {
            foreach (Object obj in created)
                Object.DestroyImmediate(obj);
        }
    }

    /// <summary>Returns a scripted sequence of values, failing loudly if a value is out of range.</summary>
    internal sealed class SequenceRandomProvider : IRandomProvider
    {
        private readonly Queue<int> values;

        public SequenceRandomProvider(params int[] values)
        {
            this.values = new Queue<int>(values);
        }

        public int LastMax { get; private set; }

        public int Range(int minInclusive, int maxExclusive)
        {
            LastMax = maxExclusive;
            return values.Count > 0 ? values.Dequeue() : minInclusive;
        }
    }

    /// <summary>Seeded System.Random for repeatable statistical tests.</summary>
    internal sealed class SeededRandomProvider : IRandomProvider
    {
        private readonly System.Random random;

        public SeededRandomProvider(int seed)
        {
            random = new System.Random(seed);
        }

        public int Range(int minInclusive, int maxExclusive) => random.Next(minInclusive, maxExclusive);
    }
}
