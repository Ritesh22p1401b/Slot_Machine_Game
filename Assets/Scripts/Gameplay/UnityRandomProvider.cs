namespace SlotMachine.Gameplay
{
    /// <summary>
    /// Production random provider backed by UnityEngine.Random.
    /// Adequate for an offline game with virtual credits; it is NOT cryptographically secure
    /// and must not be used for real-money gambling.
    /// </summary>
    public sealed class UnityRandomProvider : IRandomProvider
    {
        public int Range(int minInclusive, int maxExclusive)
        {
            return UnityEngine.Random.Range(minInclusive, maxExclusive);
        }
    }
}
