using System;
using System.Collections.Generic;

namespace Hytte.Rules.Core
{
    /// <summary>
    /// Source of randomness. The server injects a secure RNG, tests inject a fixed one.
    /// </summary>
    public interface IRng
    {
        /// <summary>Returns an integer in [minInclusive, maxExclusive).</summary>
        int Next(int minInclusive, int maxExclusive);
    }

    /// <summary>
    /// Pure rules of one game. Implementations never mutate the input state,
    /// never touch I/O, and are shared by the Unity client and the server.
    /// </summary>
    public interface IGameRules<TState, TAction>
    {
        TState NewGame(int seats);

        /// <summary>Actions the given seat may take now. Empty when it is not that seat's turn.</summary>
        IReadOnlyList<TAction> LegalActions(TState state, int seat);

        /// <summary>Returns the next state. Throws <see cref="IllegalActionException"/> for an illegal action.</summary>
        TState Apply(TState state, int seat, TAction action, IRng rng);

        /// <summary>Null while the game is running.</summary>
        GameResult? Result(TState state);
    }

    public sealed class GameResult
    {
        public GameResult(IReadOnlyList<int> ranking)
        {
            if (ranking == null || ranking.Count == 0)
                throw new ArgumentException("Ranking must contain at least one seat.", nameof(ranking));
            Ranking = ranking;
        }

        /// <summary>Seats in finishing order; index 0 is the winner.</summary>
        public IReadOnlyList<int> Ranking { get; }

        public int Winner => Ranking[0];
    }

    public sealed class IllegalActionException : Exception
    {
        public IllegalActionException(string message) : base(message) { }
    }
}
