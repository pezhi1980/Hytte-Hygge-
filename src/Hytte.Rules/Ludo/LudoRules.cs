using System;
using System.Collections.Generic;
using System.Linq;
using Hytte.Rules.Core;

namespace Hytte.Rules.Ludo
{
    public sealed class LudoOptions
    {
        /// <summary>4 = full game, 2 = quick game.</summary>
        public int PawnsToWin { get; set; } = LudoState.PawnsPerSeat;
    }

    /// <summary>
    /// Ludo rules as specified in docs/rules-ludo.md.
    ///
    ///   AwaitingRoll ──Roll──► AwaitingMove ──Move──► AwaitingRoll (same seat on 6, else next)
    ///     ▲      │                                          │
    ///     │      └─ no legal move: same seat again while    └──► Finished (state.PawnsToWin reached)
    ///     │         no pawn is in play and attempts &lt; 3,
    ///     └──────── otherwise next seat (attempts reset)
    /// </summary>
    public sealed class LudoRules : IGameRules<LudoState, LudoAction>
    {
        private static readonly IReadOnlyList<LudoAction> NoActions = Array.Empty<LudoAction>();
        private static readonly IReadOnlyList<LudoAction> RollOnly = new[] { LudoAction.Roll };

        /// <summary>Win condition for games started by <see cref="NewGame"/>; stored in the state from then on.</summary>
        private readonly int _newGamePawnsToWin;

        public LudoRules(LudoOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.PawnsToWin < 1 || options.PawnsToWin > LudoState.PawnsPerSeat)
                throw new ArgumentOutOfRangeException(nameof(options), options.PawnsToWin, "PawnsToWin must be 1–4.");
            _newGamePawnsToWin = options.PawnsToWin;
        }

        public LudoState NewGame(int seats)
        {
            var progress = new int[seats < 0 ? 0 : seats][];
            for (var seat = 0; seat < progress.Length; seat++)
                progress[seat] = new[] { LudoState.Yard, LudoState.Yard, LudoState.Yard, LudoState.Yard };
            return LudoState.Create(seats, 0, LudoPhase.AwaitingRoll, null, progress, _newGamePawnsToWin);
        }

        public IReadOnlyList<LudoAction> LegalActions(LudoState state, int seat)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Phase == LudoPhase.Finished || seat != state.CurrentSeat) return NoActions;
            if (state.Phase == LudoPhase.AwaitingRoll) return RollOnly;
            return LegalMoves(state, seat, state.LastRoll!.Value);
        }

        public LudoState Apply(LudoState state, int seat, LudoAction action, IRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (!LegalActions(state, seat).Contains(action))
                throw new IllegalActionException($"{action} is not legal for seat {seat} in phase {state.Phase}.");

            return action.Kind == LudoActionKind.Roll
                ? ApplyRoll(state, seat, rng)
                : ApplyMove(state, seat, action.Pawn);
        }

        public GameResult? Result(LudoState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Phase != LudoPhase.Finished) return null;

            var seats = Enumerable.Range(0, state.Seats).ToList();
            var winners = seats.Where(s => state.FinishedPawns(s) >= state.PawnsToWin).ToList();
            if (winners.Count != 1)
                throw new InvalidOperationException(
                    $"Finished state must have exactly one seat with {state.PawnsToWin} finished pawns, found {winners.Count}.");

            var winner = winners[0];
            var ranking = new List<int> { winner };
            ranking.AddRange(seats
                .Where(s => s != winner)
                .OrderByDescending(state.TotalProgress)); // stable: ties keep seat order
            return new GameResult(ranking);
        }

        private LudoState ApplyRoll(LudoState state, int seat, IRng rng)
        {
            var roll = rng.Next(1, 7);
            if (LegalMoves(state, seat, roll).Count > 0)
                return state.With(seat, LudoPhase.AwaitingMove, roll, state.CopyProgress(), state.RollAttempts);

            // No legal move. Three attempts while no pawn is in play, else the turn passes.
            var attemptsUsed = state.RollAttempts + 1;
            if (state.HasNoPawnInPlay(seat) && attemptsUsed < LudoState.MaxRollAttempts)
                return state.With(seat, LudoPhase.AwaitingRoll, roll, state.CopyProgress(), attemptsUsed);

            return state.With(NextSeat(state), LudoPhase.AwaitingRoll, roll, state.CopyProgress(), 0);
        }

        private LudoState ApplyMove(LudoState state, int seat, int pawn)
        {
            var roll = state.LastRoll!.Value;
            var progress = state.CopyProgress();
            var from = progress[seat][pawn];
            var to = from == LudoState.Yard ? 0 : from + roll;
            progress[seat][pawn] = to;

            CaptureAt(state, seat, to, progress);

            var moved = state.With(seat, LudoPhase.AwaitingRoll, roll, progress, 0);
            if (moved.FinishedPawns(seat) >= state.PawnsToWin)
                return state.With(seat, LudoPhase.Finished, roll, progress, 0);

            return roll == 6
                ? moved
                : state.With(NextSeat(state), LudoPhase.AwaitingRoll, roll, progress, 0);
        }

        /// <summary>Sends every opponent pawn on the mover's (unsafe) main-track square back to its yard.</summary>
        private static void CaptureAt(LudoState state, int mover, int moverProgress, int[][] progress)
        {
            var square = LudoState.SquareOf(state.ColorOf(mover), moverProgress);
            if (square == null || LudoState.IsSafeSquare(square.Value)) return;

            for (var other = 0; other < state.Seats; other++)
            {
                if (other == mover) continue;
                var color = state.ColorOf(other);
                for (var pawn = 0; pawn < LudoState.PawnsPerSeat; pawn++)
                    if (LudoState.SquareOf(color, progress[other][pawn]) == square)
                        progress[other][pawn] = LudoState.Yard;
            }
        }

        private static IReadOnlyList<LudoAction> LegalMoves(LudoState state, int seat, int roll)
        {
            var moves = new List<LudoAction>(LudoState.PawnsPerSeat);
            for (var pawn = 0; pawn < LudoState.PawnsPerSeat; pawn++)
                if (CanMove(state.Progress(seat, pawn), roll))
                    moves.Add(LudoAction.Move(pawn));
            return moves;
        }

        private static bool CanMove(int progress, int roll)
        {
            if (progress == LudoState.Finished) return false;
            if (progress == LudoState.Yard) return roll == 6;
            return progress + roll <= LudoState.Finished;
        }

        private static int NextSeat(LudoState state) => (state.CurrentSeat + 1) % state.Seats;
    }
}
