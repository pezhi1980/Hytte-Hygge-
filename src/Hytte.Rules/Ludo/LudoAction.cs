using System;

namespace Hytte.Rules.Ludo
{
    /// <summary>
    /// Values start at 1 so that <c>default(LudoAction)</c> (e.g. from a failed
    /// deserialization) has no valid kind and is never mistaken for a roll.
    /// </summary>
    public enum LudoActionKind
    {
        Roll = 1,
        Move = 2,
    }

    /// <summary>A player intent: roll the die, or move one pawn by the last roll.</summary>
    public readonly struct LudoAction : IEquatable<LudoAction>
    {
        private LudoAction(LudoActionKind kind, int pawn)
        {
            Kind = kind;
            Pawn = pawn;
        }

        public LudoActionKind Kind { get; }

        /// <summary>Pawn index 0–3 for <see cref="LudoActionKind.Move"/>; -1 for Roll.</summary>
        public int Pawn { get; }

        public static LudoAction Roll => new LudoAction(LudoActionKind.Roll, -1);

        public static LudoAction Move(int pawn) => new LudoAction(LudoActionKind.Move, pawn);

        public bool Equals(LudoAction other) => Kind == other.Kind && Pawn == other.Pawn;

        public override bool Equals(object? obj) => obj is LudoAction other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 397) ^ Pawn;

        public override string ToString()
        {
            switch (Kind)
            {
                case LudoActionKind.Roll: return "Roll";
                case LudoActionKind.Move: return $"Move({Pawn})";
                default: return $"Invalid({(int)Kind},{Pawn})";
            }
        }

        public static bool operator ==(LudoAction left, LudoAction right) => left.Equals(right);

        public static bool operator !=(LudoAction left, LudoAction right) => !left.Equals(right);
    }
}
