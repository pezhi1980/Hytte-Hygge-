using System;
using System.Linq;
using Hytte.Rules.Core;
using Hytte.Rules.Ludo;

namespace Hytte.Rules.Tests.Ludo;

// Every test maps to a section of docs/rules-ludo.md.
public class LudoRulesTests
{
    private const int Yard = LudoState.Yard;
    private const int Finished = LudoState.Finished;

    private static readonly LudoRules Rules = new LudoRules(new LudoOptions());

    private static int[][] AllInYard(int seats) =>
        Enumerable.Range(0, seats).Select(_ => new[] { Yard, Yard, Yard, Yard }).ToArray();

    // ---- Setup -------------------------------------------------------------

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void NewGame_puts_all_pawns_in_yard_and_seat0_rolls(int seats)
    {
        var s = Rules.NewGame(seats);

        Assert.Equal(seats, s.Seats);
        Assert.Equal(0, s.CurrentSeat);
        Assert.Equal(LudoPhase.AwaitingRoll, s.Phase);
        Assert.Null(s.LastRoll);
        for (var seat = 0; seat < seats; seat++)
            for (var pawn = 0; pawn < LudoState.PawnsPerSeat; pawn++)
                Assert.Equal(Yard, s.Progress(seat, pawn));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void NewGame_rejects_invalid_seat_count(int seats)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rules.NewGame(seats));
    }

    [Fact]
    public void Two_seats_use_opposite_colors()
    {
        var s = Rules.NewGame(2);

        Assert.Equal(0, s.ColorOf(0));
        Assert.Equal(2, s.ColorOf(1));
    }

    [Fact]
    public void Four_seats_use_all_colors_in_order()
    {
        var s = Rules.NewGame(4);

        Assert.Equal(new[] { 0, 1, 2, 3 }, Enumerable.Range(0, 4).Select(s.ColorOf).ToArray());
    }

    // ---- Turn: rolling -----------------------------------------------------

    [Fact]
    public void Only_current_seat_may_roll()
    {
        var s = Rules.NewGame(4);

        Assert.Equal(new[] { LudoAction.Roll }, Rules.LegalActions(s, 0).ToArray());
        Assert.Empty(Rules.LegalActions(s, 1));
    }

    [Fact]
    public void Roll_without_legal_move_passes_turn_and_keeps_roll_visible()
    {
        var s = Rules.Apply(Rules.NewGame(4), 0, LudoAction.Roll, new FixedRng(3));

        Assert.Equal(1, s.CurrentSeat);
        Assert.Equal(LudoPhase.AwaitingRoll, s.Phase);
        Assert.Equal(3, s.LastRoll);
    }

    [Fact]
    public void Six_with_all_pawns_finished_except_blocked_still_passes()
    {
        // Seat 0: three finished, one at 55 -> a 6 overshoots, no legal move.
        var progress = AllInYard(2);
        progress[0] = new[] { Finished, Finished, Finished, 55 };
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingRoll, null, progress);

        var next = Rules.Apply(s, 0, LudoAction.Roll, new FixedRng(6));

        Assert.Equal(1, next.CurrentSeat);
        Assert.Equal(LudoPhase.AwaitingRoll, next.Phase);
    }

    [Fact]
    public void Roll_of_six_at_start_offers_every_yard_pawn()
    {
        var s = Rules.Apply(Rules.NewGame(4), 0, LudoAction.Roll, new FixedRng(6));

        Assert.Equal(LudoPhase.AwaitingMove, s.Phase);
        Assert.Equal(0, s.CurrentSeat);
        Assert.Equal(
            new[] { LudoAction.Move(0), LudoAction.Move(1), LudoAction.Move(2), LudoAction.Move(3) },
            Rules.LegalActions(s, 0).ToArray());
    }

    // ---- Moving --------------------------------------------------------------

    [Fact]
    public void Leaving_yard_on_six_goes_to_start_and_grants_extra_roll()
    {
        var s = Rules.Apply(Rules.NewGame(4), 0, LudoAction.Roll, new FixedRng(6));

        s = Rules.Apply(s, 0, LudoAction.Move(2), new FixedRng());

        Assert.Equal(0, s.Progress(0, 2));
        Assert.Equal(0, s.CurrentSeat);
        Assert.Equal(LudoPhase.AwaitingRoll, s.Phase);
    }

    [Fact]
    public void Non_six_move_passes_turn()
    {
        var progress = AllInYard(4);
        progress[0][0] = 10;
        var s = LudoState.Create(4, 0, LudoPhase.AwaitingRoll, null, progress);

        s = Rules.Apply(s, 0, LudoAction.Roll, new FixedRng(4));
        Assert.Equal(new[] { LudoAction.Move(0) }, Rules.LegalActions(s, 0).ToArray());

        s = Rules.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(14, s.Progress(0, 0));
        Assert.Equal(1, s.CurrentSeat);
        Assert.Equal(LudoPhase.AwaitingRoll, s.Phase);
    }

    [Fact]
    public void Pawn_moves_from_main_track_into_home_column()
    {
        var progress = AllInYard(2);
        progress[0][0] = 49;
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 4, progress);

        s = Rules.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(53, s.Progress(0, 0));
    }

    [Fact]
    public void Overshooting_finish_is_not_legal()
    {
        var progress = AllInYard(2);
        progress[0][0] = 54;
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 3, progress);

        Assert.DoesNotContain(LudoAction.Move(0), Rules.LegalActions(s, 0));
    }

    [Fact]
    public void Exact_roll_finishes_pawn()
    {
        var progress = AllInYard(2);
        progress[0][0] = 54;
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 2, progress);

        s = Rules.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(Finished, s.Progress(0, 0));
    }

    [Fact]
    public void Finished_pawn_cannot_move()
    {
        var progress = AllInYard(2);
        progress[0] = new[] { Finished, 20, Yard, Yard };
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 1, progress);

        Assert.Equal(new[] { LudoAction.Move(1) }, Rules.LegalActions(s, 0).ToArray());
    }

    [Fact]
    public void Own_pawns_may_stack()
    {
        var progress = AllInYard(2);
        progress[0] = new[] { 10, 12, Yard, Yard };
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 2, progress);

        s = Rules.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(12, s.Progress(0, 0));
        Assert.Equal(12, s.Progress(0, 1));
    }

    // ---- Capture -------------------------------------------------------------

    [Fact]
    public void Landing_on_opponent_on_normal_square_sends_it_home()
    {
        // 4 seats. Seat 0 (color 0) at progress 4 = absolute 4.
        // Seat 1 (color 1) at progress 44 = absolute (13+44)%52 = 5.
        var progress = AllInYard(4);
        progress[0][0] = 4;
        progress[1][0] = 44;
        var s = LudoState.Create(4, 0, LudoPhase.AwaitingMove, 1, progress);

        s = Rules.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(5, s.Progress(0, 0));
        Assert.Equal(Yard, s.Progress(1, 0));
    }

    [Fact]
    public void Capture_sends_every_opponent_pawn_on_the_square_home()
    {
        var progress = AllInYard(4);
        progress[0][0] = 4;
        progress[1] = new[] { 44, 44, Yard, Yard };
        var s = LudoState.Create(4, 0, LudoPhase.AwaitingMove, 1, progress);

        s = Rules.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(Yard, s.Progress(1, 0));
        Assert.Equal(Yard, s.Progress(1, 1));
    }

    [Fact]
    public void No_capture_on_star_square()
    {
        // Seat 1 at progress 47 = absolute (13+47)%52 = 8, a star square.
        var progress = AllInYard(4);
        progress[0][0] = 4;
        progress[1][0] = 47;
        var s = LudoState.Create(4, 0, LudoPhase.AwaitingMove, 4, progress);

        s = Rules.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(8, s.Progress(0, 0));
        Assert.Equal(47, s.Progress(1, 0));
    }

    [Fact]
    public void No_capture_on_start_square()
    {
        // Seat 1 at progress 39 = absolute 0, seat 0's start square.
        var progress = AllInYard(4);
        progress[1][0] = 39;
        var s = LudoState.Create(4, 0, LudoPhase.AwaitingMove, 6, progress);

        s = Rules.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(0, s.Progress(0, 0));
        Assert.Equal(39, s.Progress(1, 0));
    }

    [Fact]
    public void Home_column_never_captures()
    {
        // Seat 0 into home column 52; seat 1 has a pawn whose progress is also 52.
        var progress = AllInYard(4);
        progress[0][0] = 50;
        progress[1][0] = 52;
        var s = LudoState.Create(4, 0, LudoPhase.AwaitingMove, 2, progress);

        s = Rules.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(52, s.Progress(0, 0));
        Assert.Equal(52, s.Progress(1, 0));
    }

    [Fact]
    public void Two_seat_capture_uses_opposite_color_offset()
    {
        // 2 seats: seat 1 is color 2 (offset 26). Progress 10 -> absolute 36.
        // Seat 0 at 33 rolls 3 -> absolute 36 (not safe).
        var progress = AllInYard(2);
        progress[0][0] = 33;
        progress[1][0] = 10;
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 3, progress);

        s = Rules.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(Yard, s.Progress(1, 0));
    }

    // ---- Winning ---------------------------------------------------------------

    [Fact]
    public void Finishing_last_pawn_wins_and_ends_game()
    {
        var progress = AllInYard(3);
        progress[0] = new[] { Finished, Finished, Finished, 55 };
        progress[1] = new[] { 10, Yard, Yard, Yard };
        progress[2] = new[] { 30, 5, Yard, Yard };
        var s = LudoState.Create(3, 0, LudoPhase.AwaitingMove, 1, progress);

        s = Rules.Apply(s, 0, LudoAction.Move(3), new FixedRng());

        Assert.Equal(LudoPhase.Finished, s.Phase);
        var result = Rules.Result(s);
        Assert.NotNull(result);
        Assert.Equal(new[] { 0, 2, 1 }, result!.Ranking.ToArray());
        for (var seat = 0; seat < 3; seat++)
            Assert.Empty(Rules.LegalActions(s, seat));
    }

    [Fact]
    public void Winning_with_a_six_does_not_grant_extra_roll()
    {
        var progress = AllInYard(2);
        progress[0] = new[] { Finished, Finished, Finished, 50 };
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 6, progress);

        s = Rules.Apply(s, 0, LudoAction.Move(3), new FixedRng());

        Assert.Equal(LudoPhase.Finished, s.Phase);
        Assert.Equal(0, Rules.Result(s)!.Winner);
    }

    [Fact]
    public void Quick_mode_wins_with_two_pawns()
    {
        var quick = new LudoRules(new LudoOptions { PawnsToWin = 2 });
        var progress = AllInYard(2);
        progress[0] = new[] { Finished, 55, Yard, Yard };
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 1, progress);

        s = quick.Apply(s, 0, LudoAction.Move(1), new FixedRng());

        Assert.Equal(LudoPhase.Finished, s.Phase);
        Assert.Equal(0, quick.Result(s)!.Winner);
    }

    [Fact]
    public void No_result_while_game_is_running()
    {
        Assert.Null(Rules.Result(Rules.NewGame(2)));
    }

    // ---- Illegal actions ---------------------------------------------------------

    [Fact]
    public void Wrong_seat_cannot_act()
    {
        Assert.Throws<IllegalActionException>(() =>
            Rules.Apply(Rules.NewGame(2), 1, LudoAction.Roll, new FixedRng(6)));
    }

    [Fact]
    public void Cannot_move_before_rolling()
    {
        var progress = AllInYard(2);
        progress[0][0] = 10;
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingRoll, null, progress);

        Assert.Throws<IllegalActionException>(() =>
            Rules.Apply(s, 0, LudoAction.Move(0), new FixedRng()));
    }

    [Fact]
    public void Cannot_roll_twice()
    {
        var s = Rules.Apply(Rules.NewGame(2), 0, LudoAction.Roll, new FixedRng(6));

        Assert.Throws<IllegalActionException>(() =>
            Rules.Apply(s, 0, LudoAction.Roll, new FixedRng(6)));
    }

    [Fact]
    public void Cannot_move_yard_pawn_without_six()
    {
        var progress = AllInYard(2);
        progress[0][0] = 10;
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 5, progress);

        Assert.Throws<IllegalActionException>(() =>
            Rules.Apply(s, 0, LudoAction.Move(1), new FixedRng()));
    }

    [Fact]
    public void Cannot_act_after_game_finished()
    {
        var progress = AllInYard(2);
        progress[0] = new[] { Finished, Finished, Finished, Finished };
        var s = LudoState.Create(2, 0, LudoPhase.Finished, 1, progress);

        Assert.Throws<IllegalActionException>(() =>
            Rules.Apply(s, 1, LudoAction.Roll, new FixedRng(1)));
    }

    [Fact]
    public void Create_rejects_invalid_progress()
    {
        var progress = AllInYard(2);
        progress[0][0] = 57;

        Assert.Throws<ArgumentException>(() =>
            LudoState.Create(2, 0, LudoPhase.AwaitingRoll, null, progress));
    }

    // ---- Purity ------------------------------------------------------------------

    [Fact]
    public void Apply_does_not_mutate_input_state()
    {
        var progress = AllInYard(4);
        progress[0][0] = 4;
        progress[1][0] = 44;
        var before = LudoState.Create(4, 0, LudoPhase.AwaitingMove, 1, progress);

        Rules.Apply(before, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(4, before.Progress(0, 0));
        Assert.Equal(44, before.Progress(1, 0));
        Assert.Equal(LudoPhase.AwaitingMove, before.Phase);
    }

    [Fact]
    public void Create_copies_caller_arrays()
    {
        var progress = AllInYard(2);
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingRoll, null, progress);

        progress[0][0] = 30;

        Assert.Equal(Yard, s.Progress(0, 0));
    }
}
