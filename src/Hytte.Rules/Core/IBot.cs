namespace Hytte.Rules.Core
{
    /// <summary>
    /// A computer player. It only picks from the rules' legal actions and never
    /// re-implements rules. The same bot runs offline in Unity, for online bots,
    /// for stand-ins and for timeout auto-moves.
    /// </summary>
    public interface IBot<TState, TAction>
    {
        /// <summary>Returns one of the legal actions for <paramref name="seat"/>. Randomness only from <paramref name="rng"/>.</summary>
        TAction Choose(TState state, int seat, IRng rng);
    }
}
