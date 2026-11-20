using System.Collections.Generic;
using UnityEngine;

namespace SlotMachine.Data
{
    /// <summary>All symbols that may appear on the reels. Every reel uses the same weights.</summary>
    [CreateAssetMenu(fileName = "SymbolDatabase", menuName = "Slot Machine/Symbol Database", order = 1)]
    public sealed class SymbolDatabase : ScriptableObject
    {
        [SerializeField] private List<SymbolData> symbols = new List<SymbolData>();

        public IReadOnlyList<SymbolData> Symbols => symbols;

        /// <summary>Replaces the symbol list. Intended for editor tooling and tests.</summary>
        public void SetSymbols(IEnumerable<SymbolData> newSymbols)
        {
            symbols = new List<SymbolData>(newSymbols);
        }

        /// <summary>
        /// Full runtime validation: non-null entries, unique non-empty IDs, sprites assigned,
        /// positive weights and multipliers. Returns true when no errors were found.
        /// </summary>
        public bool Validate(List<string> errors)
        {
            int startCount = errors.Count;
            if (symbols == null || symbols.Count == 0)
            {
                errors.Add($"{name}: the symbol database is empty.");
                return false;
            }

            var seenIds = new HashSet<string>();
            for (int i = 0; i < symbols.Count; i++)
            {
                SymbolData symbol = symbols[i];
                if (symbol == null)
                {
                    errors.Add($"{name}: symbol entry {i} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(symbol.Id))
                    errors.Add($"{name}: symbol '{symbol.name}' has an empty ID.");
                else if (!seenIds.Add(symbol.Id))
                    errors.Add($"{name}: duplicate symbol ID '{symbol.Id}'.");

                if (symbol.Sprite == null)
                    errors.Add($"{name}: symbol '{symbol.Id}' has no sprite assigned.");
                if (symbol.Weight <= 0)
                    errors.Add($"{name}: symbol '{symbol.Id}' must have a positive weight.");
                if (symbol.PayoutMultiplier <= 0)
                    errors.Add($"{name}: symbol '{symbol.Id}' must have a positive payout multiplier.");
            }

            return errors.Count == startCount;
        }
    }
}
