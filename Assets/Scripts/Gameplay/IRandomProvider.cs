namespace SlotMachine.Gameplay
{
    /// <summary>Source of random integers. Injected so tests can supply deterministic values.</summary>
    public interface IRandomProvider
    {
        /// <summary>Returns an integer in [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);
    }
}
