using UnityEngine;

namespace SlotMachine.Data
{
    /// <summary>
    /// Configuration for one reel symbol: identity, artwork, RNG weight and payout multiplier.
    /// Weights and multipliers are demonstration tuning values and can be edited in the Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "Symbol_", menuName = "Slot Machine/Symbol Data", order = 0)]
    public sealed class SymbolData : ScriptableObject
    {
        [SerializeField, Tooltip("Unique, stable identifier, e.g. SEVEN.")]
        private string id = string.Empty;

        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private Sprite sprite;

        [SerializeField, Min(0), Tooltip("Relative selection weight. Probability = weight / total weight.")]
        private int weight = 1;

        [SerializeField, Min(0), Tooltip("Payout = bet x multiplier when all center-row symbols match.")]
        private int payoutMultiplier = 1;

        public string Id => id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? id : displayName;
        public Sprite Sprite => sprite;
        public int Weight => weight;
        public int PayoutMultiplier => payoutMultiplier;

        /// <summary>True when the symbol can take part in weighted selection (sprite is checked separately).</summary>
        public bool IsSelectable => !string.IsNullOrWhiteSpace(id) && weight > 0 && payoutMultiplier > 0;

        /// <summary>Sets every field at once. Intended for editor tooling and tests.</summary>
        public void Configure(string symbolId, string symbolDisplayName, Sprite symbolSprite, int symbolWeight, int symbolPayoutMultiplier)
        {
            id = symbolId;
            displayName = symbolDisplayName;
            sprite = symbolSprite;
            weight = symbolWeight;
            payoutMultiplier = symbolPayoutMultiplier;
        }

        public override string ToString() => $"{Id} (w{weight}, x{payoutMultiplier})";
    }
}
