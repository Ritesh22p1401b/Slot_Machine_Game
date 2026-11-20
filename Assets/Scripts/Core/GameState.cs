namespace SlotMachine.Core
{
    /// <summary>
    /// Finite states of the slot machine.
    /// Ready -> Spinning -> Evaluating -> (Winning | Losing) -> Ready.
    /// A spin rejected for insufficient credits never leaves Ready.
    /// </summary>
    public enum GameState
    {
        Ready,
        Spinning,
        Evaluating,
        Winning,
        Losing
    }
}
