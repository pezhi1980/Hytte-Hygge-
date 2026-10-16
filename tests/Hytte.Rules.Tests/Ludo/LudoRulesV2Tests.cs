using System;
using System.Linq;
using Hytte.Rules.Ludo;

namespace Hytte.Rules.Tests.Ludo;

// docs/rules-ludo.md → Turn (three attempts) and Winning (PawnsToWin in state).
public class LudoRulesV2Tests
{
    private const int Yard = LudoState.Yard;
    private const int Finished = LudoState.Finished;

    private static readonly LudoRules Full = new LudoRules(new LudoOptions());
    private static readonly LudoRules Quick = new LudoRules(new LudoOptions { PawnsToWin = 2 });

    private static int[][] AllInYard(int seats) =>
        Enumerable.Range(0, seats).Select(_ => new[] { Yard, Yard, Yard, Yard }).ToArray();

    // ---- Three attempts to roll a 6 ---------------------------------------------

    [Fact]
    public void All_in_yard_failed_roll_keeps_same_seat_rolling()
    {
        var s = Full.Apply(Full.NewGame(4), 0, LudoAction.Roll, new FixedRng(3));

        Assert.Equal(0, s.CurrentSeat);
        Assert.Equal(LudoPhase.AwaitingRoll, s.Phase);
        Assert.Equal(3, s.LastRoll);
        Assert.Equal(1, s.RollAttempts);
    }

    [Fact]
    public void Three_failed_attempts_pass_the_turn_and_reset_the_counter()
    {
        var s = Full.NewGame(4);
        s = Full.Apply(s, 0, LudoAction.Roll, new FixedRng(3));
        s = Full.Apply(s, 0, LudoAction.Roll, new FixedRng(4));
        s = Full.Apply(s, 0, LudoAction.Roll, new FixedRng(5));

        Assert.Equal(1, s.CurrentSeat);
        Assert.Equal(LudoPhase.AwaitingRoll, s.Phase);
        Assert.Equal(5, s.LastRoll);
        Assert.Equal(0, s.RollAttempts);
    }

    [Fact]
    public void Six_on_second_attempt_lets_a_pawn_out_and_resets_the_counter()
    {
        var s = Full.NewGame(2);
        s = Full.Apply(s, 0, LudoAction.Roll, new FixedRng(2));
        s = Full.Apply(s, 0, LudoAction.Roll, new FixedRng(6));

        Assert.Equal(LudoPhase.AwaitingMove, s.Phase);
        Assert.Equal(4, Full.LegalActions(s, 0).Count);

        s = Full.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(0, s.Progress(0, 0));
        Assert.Equal(0, s.CurrentSeat);           // extra roll after a 6
        Assert.Equal(LudoPhase.AwaitingRoll, s.Phase);
        Assert.Equal(0, s.RollAttempts);
    }

    [Fact]
    public void Pawn_on_track_gets_a_single_attempt()
    {
        // Pawn at 54 cannot move 5; it is on the track, so no extra attempts.
        var progress = AllInYard(2);
        progress[0][0] = 54;
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingRoll, null, progress);

        s = Full.Apply(s, 0, LudoAction.Roll, new FixedRng(5));

        Assert.Equal(1, s.CurrentSeat);
        Assert.Equal(0, s.RollAttempts);
    }

    [Fact]
    public void Finished_pawns_count_like_yard_pawns_for_attempts()
    {
        var progress = AllInYard(2);
        progress[0] = new[] { Finished, Yard, Yard, Yard };
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingRoll, null, progress);

        s = Full.Apply(s, 0, LudoAction.Roll, new FixedRng(3));

        Assert.Equal(0, s.CurrentSeat);
        Assert.Equal(1, s.RollAttempts);
    }

    [Fact]
    public void Attempts_survive_a_state_round_trip()
    {
        var s = Full.Apply(Full.NewGame(2), 0, LudoAction.Roll, new FixedRng(1));
        s = Full.Apply(s, 0, LudoAction.Roll, new FixedRng(1));

        var loaded = Reload(s);

        Assert.Equal(2, loaded.RollAttempts);
        loaded = Full.Apply(loaded, 0, LudoAction.Roll, new FixedRng(1));
        Assert.Equal(1, loaded.CurrentSeat);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Create_rejects_invalid_roll_attempts(int attempts)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LudoState.Create(2, 0, LudoPhase.AwaitingRoll, null, AllInYard(2), rollAttempts: attempts));
    }

    // ---- PawnsToWin stored in the state -----------------------------------------

    [Fact]
    public void NewGame_stores_pawns_to_win_from_options()
    {
        Assert.Equal(4, Full.NewGame(2).PawnsToWin);
        Assert.Equal(2, Quick.NewGame(2).PawnsToWin);
    }

    [Fact]
    public void Create_defaults_to_full_game()
    {
        Assert.Equal(4, LudoState.Create(2, 0, LudoPhase.AwaitingRoll, null, AllInYard(2)).PawnsToWin);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Create_rejects_invalid_pawns_to_win(int pawnsToWin)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LudoState.Create(2, 0, LudoPhase.AwaitingRoll, null, AllInYard(2), pawnsToWin: pawnsToWin));
    }

    [Fact]
    public void Quick_state_wins_with_two_pawns_even_under_full_rules_object()
    {
        // The state, not the rules instance, decides the win condition.
        var progress = AllInYard(2);
        progress[0] = new[] { Finished, 55, Yard, Yard };
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 1, progress, pawnsToWin: 2);

        s = Full.Apply(s, 0, LudoAction.Move(1), new FixedRng());

        Assert.Equal(LudoPhase.Finished, s.Phase);
        Assert.Equal(0, Full.Result(s)!.Winner);
    }

    [Fact]
    public void Pawns_to_win_survives_moves()
    {
        var s = Quick.Apply(Quick.NewGame(2), 0, LudoAction.Roll, new FixedRng(6));
        s = Quick.Apply(s, 0, LudoAction.Move(0), new FixedRng());

        Assert.Equal(2, s.PawnsToWin);
    }

    private static LudoState Reload(LudoState s)
    {
        var progress = Enumerable.Range(0, s.Seats)
            .Select(seat => Enumerable.Range(0, LudoState.PawnsPerSeat).Select(p => s.Progress(seat, p)).ToArray())
            .ToArray();
        return LudoState.Create(s.Seats, s.CurrentSeat, s.Phase, s.LastRoll, progress,
            pawnsToWin: s.PawnsToWin, rollAttempts: s.RollAttempts);
    }
}
