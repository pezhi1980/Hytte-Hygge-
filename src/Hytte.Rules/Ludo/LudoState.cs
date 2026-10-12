using System;

namespace Hytte.Rules.Ludo
{
    public enum LudoPhase
    {
        AwaitingRoll,
        AwaitingMove,
        Finished,
    }

    /// <summary>
    /// Immutable Ludo position. See docs/rules-ludo.md for the progress encoding:
    ///
    ///   -1 yard │ 0 ─────────── 50 main track │ 51 ── 55 home column │ 56 finished
    ///
    /// Absolute main-track square = (13 × color + progress) mod 52.
    /// </summary>
    public sealed class LudoState
    {
        public const int Yard = -1;
        public const int LastTrackProgress = 50;
        public const int Finished = 56;
        public const int PawnsPerSeat = 4;
        public const int TrackLength = 52;
        public const int ColorOffset = 13;
        public const int MinSeats = 2;
        public const int MaxSeats = 4;

        private static readonly int[][] SeatColors =
        {
            Array.Empty<int>(),
            Array.Empty<int>(),
            new[] { 0, 2 },
            new[] { 0, 1, 2 },
            new[] { 0, 1, 2, 3 },
        };

        private readonly int[][] _progress;

        private LudoState(int seats, int currentSeat, LudoPhase phase, int? lastRoll, int[][] progress)
        {
            Seats = seats;
            CurrentSeat = currentSeat;
            Phase = phase;
            LastRoll = lastRoll;
            _progress = progress;
        }

        public int Seats { get; }

        public int CurrentSeat { get; }

        public LudoPhase Phase { get; }

        /// <summary>The most recent roll, kept visible after the turn passes so the UI can show it.</summary>
        public int? LastRoll { get; }

        public int Progress(int seat, int pawn) => _progress[seat][pawn];

        public int ColorOf(int seat) => SeatColors[Seats][seat];

        /// <summary>Absolute main-track square, or null when the pawn is in the yard, home column or finished.</summary>
        public int? AbsoluteSquare(int seat, int pawn) => SquareOf(ColorOf(seat), _progress[seat][pawn]);

        /// <summary>Number of the seat's pawns that have reached the finish.</summary>
        public int FinishedPawns(int seat)
        {
            var count = 0;
            foreach (var p in _progress[seat])
                if (p == Finished) count++;
            return count;
        }

        /// <summary>Sum of the seat's pawn progress (yard counts as -1). Used for ranking.</summary>
        public int TotalProgress(int seat)
        {
            var sum = 0;
            foreach (var p in _progress[seat]) sum += p;
            return sum;
        }

        /// <summary>Absolute main-track square for a color and progress, or null off the main track.</summary>
        public static int? SquareOf(int color, int progress)
        {
            if (progress < 0 || progress > LastTrackProgress) return null;
            return (ColorOffset * color + progress) % TrackLength;
        }

        /// <summary>Start squares (0, 13, 26, 39) and star squares (8, 21, 34, 47).</summary>
        public static bool IsSafeSquare(int absoluteSquare) => absoluteSquare % ColorOffset == 0 || absoluteSquare % ColorOffset == 8;

        /// <summary>
        /// Builds a validated state, e.g. when loading from the database. The arrays are copied.
        /// </summary>
        public static LudoState Create(int seats, int currentSeat, LudoPhase phase, int? lastRoll, int[][] progress)
        {
            if (seats < MinSeats || seats > MaxSeats)
                throw new ArgumentOutOfRangeException(nameof(seats), seats, "Ludo needs 2–4 seats.");
            if (currentSeat < 0 || currentSeat >= seats)
                throw new ArgumentOutOfRangeException(nameof(currentSeat), currentSeat, "Current seat out of range.");
            if (lastRoll.HasValue && (lastRoll.Value < 1 || lastRoll.Value > 6))
                throw new ArgumentException("Last roll must be 1–6.", nameof(lastRoll));
            if (phase == LudoPhase.AwaitingMove && !lastRoll.HasValue)
                throw new ArgumentException("AwaitingMove requires a roll.", nameof(lastRoll));
            if (progress == null || progress.Length != seats)
                throw new ArgumentException("Progress must have one row per seat.", nameof(progress));

            var copy = new int[seats][];
            for (var seat = 0; seat < seats; seat++)
            {
                var row = progress[seat];
                if (row == null || row.Length != PawnsPerSeat)
                    throw new ArgumentException($"Seat {seat} must have {PawnsPerSeat} pawns.", nameof(progress));
                foreach (var p in row)
                    if (p < Yard || p > Finished)
                        throw new ArgumentException($"Progress {p} out of range for seat {seat}.", nameof(progress));
                copy[seat] = (int[])row.Clone();
            }

            return new LudoState(seats, currentSeat, phase, lastRoll, copy);
        }

        internal int[][] CopyProgress()
        {
            var copy = new int[Seats][];
            for (var seat = 0; seat < Seats; seat++)
                copy[seat] = (int[])_progress[seat].Clone();
            return copy;
        }

        /// <summary>Internal constructor for rules: takes ownership of an already-copied array.</summary>
        internal static LudoState FromOwned(int seats, int currentSeat, LudoPhase phase, int? lastRoll, int[][] ownedProgress) =>
            new LudoState(seats, currentSeat, phase, lastRoll, ownedProgress);
    }
}
