using System;
using Hytte.Rules.Core;
using Hytte.Rules.Ludo;

namespace Hytte.Rules.Bots
{
    /// <summary>How a Ludo bot plays. Shown to players as a character trait, never a number.</summary>
    public enum LudoTemperament
    {
        /// <summary>Random legal move. Also used for timeout auto-moves.</summary>
        Calm = 1,

        /// <summary>Capture &gt; finish &gt; leave yard &gt; enter home column &gt; furthest advance.</summary>
        Clever = 2,

        /// <summary>Clever, plus avoids landing where an opponent can capture next turn.</summary>
        Cunning = 3,
    }

    /// <summary>
    /// Ludo bot. Scores each legal move and plays the best one (ties: lowest pawn index).
    ///
    ///   legal actions ──► 1 action? ──► play it
    ///                      │
    ///                      ├─ Calm ─────► rng picks one
    ///                      └─ Clever / Cunning ─► score every move, play the max
    /// </summary>
    public sealed class LudoBot : IBot<LudoState, LudoAction>
    {
        private const int CaptureScore = 1000;
        private const int FinishScore = 500;
        private const int LeaveYardScore = 300;
        private const int HomeColumnScore = 200;
        private const int LandInDangerPenalty = 600;
        private const int EscapeDangerScore = 150;

        // Legal moves do not depend on the win condition, so one shared instance is enough.
        private static readonly LudoRules Rules = new LudoRules(new LudoOptions());

        public LudoBot(LudoTemperament temperament)
        {
            if (!Enum.IsDefined(typeof(LudoTemperament), temperament))
                throw new ArgumentOutOfRangeException(nameof(temperament), temperament, "Unknown temperament.");
            Temperament = temperament;
        }

        public LudoTemperament Temperament { get; }

        public LudoAction Choose(LudoState state, int seat, IRng rng)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var legal = Rules.LegalActions(state, seat);
            if (legal.Count == 0)
                throw new InvalidOperationException($"Seat {seat} has no legal action.");
            if (legal.Count == 1) return legal[0];
            if (Temperament == LudoTemperament.Calm) return legal[rng.Next(0, legal.Count)];

            var best = legal[0];
            var bestScore = int.MinValue;
            foreach (var action in legal)
            {
                var score = Score(state, seat, action.Pawn);
                if (score > bestScore)
                {
                    best = action;
                    bestScore = score;
                }
            }

            return best;
        }

        private int Score(LudoState state, int seat, int pawn)
        {
            var roll = state.LastRoll!.Value;
            var from = state.Progress(seat, pawn);
            var to = from == LudoState.Yard ? 0 : from + roll;
            var color = state.ColorOf(seat);
            var target = LudoState.SquareOf(color, to);
            var targetExposed = target.HasValue && !LudoState.IsSafeSquare(target.Value);

            var score = to; // tie-break: advance the furthest
            if (targetExposed && OpponentOn(state, seat, target!.Value)) score += CaptureScore;
            if (to == LudoState.Finished) score += FinishScore;
            else if (from == LudoState.Yard) score += LeaveYardScore;
            else if (to > LudoState.LastTrackProgress && from <= LudoState.LastTrackProgress) score += HomeColumnScore;

            if (Temperament == LudoTemperament.Cunning)
            {
                var landsInDanger = targetExposed && Threatened(state, seat, target!.Value);
                if (landsInDanger) score -= LandInDangerPenalty;

                var source = LudoState.SquareOf(color, from);
                var wasInDanger = source.HasValue && !LudoState.IsSafeSquare(source.Value) && Threatened(state, seat, source.Value);
                if (wasInDanger && !landsInDanger) score += EscapeDangerScore;
            }

            return score;
        }

        private static bool OpponentOn(LudoState state, int seat, int square)
        {
            for (var other = 0; other < state.Seats; other++)
            {
                if (other == seat) continue;
                for (var pawn = 0; pawn < LudoState.PawnsPerSeat; pawn++)
                    if (state.AbsoluteSquare(other, pawn) == square) return true;
            }
            return false;
        }

        /// <summary>True when an opponent pawn on the main track could land on <paramref name="square"/> with one roll.</summary>
        private static bool Threatened(LudoState state, int seat, int square)
        {
            for (var other = 0; other < state.Seats; other++)
            {
                if (other == seat) continue;
                var color = state.ColorOf(other);
                var targetProgress = ((square - LudoState.ColorOffset * color) % LudoState.TrackLength + LudoState.TrackLength) % LudoState.TrackLength;
                if (targetProgress > LudoState.LastTrackProgress) continue;

                for (var pawn = 0; pawn < LudoState.PawnsPerSeat; pawn++)
                {
                    var p = state.Progress(other, pawn);
                    if (p < 0 || p > LudoState.LastTrackProgress) continue;
                    var distance = targetProgress - p;
                    if (distance >= 1 && distance <= 6) return true;
                }
            }
            return false;
        }
    }
}
