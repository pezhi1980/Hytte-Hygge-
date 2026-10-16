# Ludo — rule variant implemented by `Hytte.Rules`

This is the single source of truth for Ludo. The C# library and its tests
follow this file exactly. Change this file first, then the tests, then the code.

## Board and pawns

- 2–4 seats. Each seat has 4 pawns of one color.
- Seat → color: 2 seats use colors 0 and 2 (opposite corners);
  3 seats use 0, 1, 2; 4 seats use 0, 1, 2, 3.
- The main track has 52 squares. Color `c` enters the track at absolute
  square `13 × c`.
- Each pawn's position is stored as **progress** relative to its own color:

| Progress | Meaning |
|----------|---------|
| -1 | In the yard |
| 0–50 | On the main track (0 = own start square) |
| 51–55 | In the own home column (no other color can enter) |
| 56 | Finished |

- Absolute square on the main track = `(13 × color + progress) mod 52`.

## Safe squares

Absolute squares `0, 8, 13, 21, 26, 34, 39, 47` (the four start squares and
the four star squares). Pawns on a safe square cannot be captured.

## Turn

1. The current seat **rolls** one die (1–6). The die is rolled by the server
   (or the local RNG for offline bot games), never by the client.
2. If the roll gives the seat **no legal move**:
   - **Three attempts:** if every pawn of the seat is in the yard or finished
     (none at progress 0–55), the same seat rolls again, up to **3 rolls in
     total** for that turn. After the 3rd roll without a legal move the turn
     passes. A legal roll on attempt 1–3 is played normally.
   - Otherwise the turn passes automatically to the next seat.
   - No extra roll, even on a 6, when the 6 had no legal move.
   - The attempt counter is part of the state and resets whenever the turn
     passes or a move is made.
3. Otherwise the seat **moves** exactly one pawn by the roll.
4. After a move: a roll of **6 gives another roll** to the same seat;
   any other roll passes the turn to the next seat.

## Legal moves for a roll `r`

- A pawn in the yard may leave **only on a 6**, to progress 0.
- A finished pawn cannot move.
- A pawn at progress `p` may move to `p + r` only if `p + r ≤ 56`
  (**exact roll needed to finish**).
- Own pawns may share a square (stacking). There are no blocks.

## Capture

- When a pawn lands on a main-track square (progress 0–50) that is **not safe**,
  every opponent pawn on that absolute square goes back to its yard.
- Home-column squares (51–55) are never shared with other colors.

## Winning

- A seat wins when `PawnsToWin` of its pawns are finished. `PawnsToWin` is
  stored **in the game state** (set by `NewGame`), not only in the rules object.
  - Full mode: `PawnsToWin = 4`.
  - Quick mode: `PawnsToWin = 2`.
- The game ends as soon as one seat wins.
- Ranking: winner first, then the other seats by total progress
  (yard counts as -1), highest first; ties keep seat order.

## Not in this version

Three-sixes penalty, extra roll on capture, blocks of two pawns.
