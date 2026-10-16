using System.Linq;
using Hytte.Rules.Bots;
using Hytte.Rules.Ludo;

namespace Hytte.Rules.Tests.Ludo;

// docs/eng-review.md → Rules library shape (bots).
public class LudoBotTests
{
    private const int Yard = LudoState.Yard;
    private const int Finished = LudoState.Finished;

    private static readonly LudoRules Rules = new LudoRules(new LudoOptions());

    private static int[][] AllInYard(int seats) =>
        Enumerable.Range(0, seats).Select(_ => new[] { Yard, Yard, Yard, Yard }).ToArray();

    [Theory]
    [InlineData(LudoTemperament.Calm)]
    [InlineData(LudoTemperament.Clever)]
    [InlineData(LudoTemperament.Cunning)]
    public void Roll_phase_always_rolls(LudoTemperament temperament)
    {
        var bot = new LudoBot(temperament);

        Assert.Equal(LudoAction.Roll, bot.Choose(Rules.NewGame(2), 0, new FixedRng()));
    }

    [Theory]
    [InlineData(LudoTemperament.Calm)]
    [InlineData(LudoTemperament.Clever)]
    [InlineData(LudoTemperament.Cunning)]
    public void Single_legal_move_is_chosen_without_using_rng(LudoTemperament temperament)
    {
        var progress = AllInYard(2);
        progress[0][2] = 10;
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 3, progress);

        Assert.Equal(LudoAction.Move(2), new LudoBot(temperament).Choose(s, 0, new FixedRng()));
    }

    [Fact]
    public void Calm_picks_a_random_legal_move()
    {
        var progress = AllInYard(2);
        progress[0] = new[] { 10, 20, Yard, Yard };
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 3, progress);

        Assert.Equal(LudoAction.Move(1), new LudoBot(LudoTemperament.Calm).Choose(s, 0, new FixedRng(1)));
        Assert.Equal(LudoAction.Move(0), new LudoBot(LudoTemperament.Calm).Choose(s, 0, new FixedRng(0)));
    }

    [Fact]
    public void Clever_prefers_capture_over_finishing()
    {
        // Roll 1. Pawn 0: 4 → 5 captures seat 1 (color 1, progress 44 = absolute 5).
        // Pawn 1: 55 → 56 finishes.
        var progress = AllInYard(4);
        progress[0] = new[] { 4, 55, Yard, Yard };
        progress[1][0] = 44;
        var s = LudoState.Create(4, 0, LudoPhase.AwaitingMove, 1, progress);

        Assert.Equal(LudoAction.Move(0), new LudoBot(LudoTemperament.Clever).Choose(s, 0, new FixedRng()));
    }

    [Fact]
    public void Clever_prefers_finishing_over_leaving_the_yard()
    {
        var progress = AllInYard(2);
        progress[0] = new[] { 50, Yard, Yard, Yard };
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 6, progress);

        Assert.Equal(LudoAction.Move(0), new LudoBot(LudoTemperament.Clever).Choose(s, 0, new FixedRng()));
    }

    [Fact]
    public void Clever_prefers_leaving_the_yard_over_a_plain_move()
    {
        var progress = AllInYard(2);
        progress[0] = new[] { 10, Yard, Yard, Yard };
        var s = LudoState.Create(2, 0, LudoPhase.AwaitingMove, 6, progress);

        Assert.Equal(LudoAction.Move(1), new LudoBot(LudoTemperament.Clever).Choose(s, 0, new FixedRng()));
    }

    [Fact]
    public void Cunning_avoids_landing_within_reach_of_an_opponent()
    {
        // 4 seats, roll 2.
        // Pawn 0: 30 → 32 (absolute 32). Seat 1 (color 1) sits at progress 16 =
        //         absolute 29, three squares behind: it can capture with a 3.
        // Pawn 1: 20 → 22 (absolute 22). No opponent within six squares behind.
        var progress = AllInYard(4);
        progress[0] = new[] { 30, 20, Yard, Yard };
        progress[1][0] = 16;
        var s = LudoState.Create(4, 0, LudoPhase.AwaitingMove, 2, progress);

        Assert.Equal(LudoAction.Move(0), new LudoBot(LudoTemperament.Clever).Choose(s, 0, new FixedRng()));
        Assert.Equal(LudoAction.Move(1), new LudoBot(LudoTemperament.Cunning).Choose(s, 0, new FixedRng()));
    }

    [Theory]
    [InlineData(LudoTemperament.Calm)]
    [InlineData(LudoTemperament.Clever)]
    [InlineData(LudoTemperament.Cunning)]
    public void Bots_only_choose_legal_actions_and_games_finish(LudoTemperament temperament)
    {
        const int games = 2_000;
        const int maxActions = 20_000;
        var bot = new LudoBot(temperament);

        for (var game = 0; game < games; game++)
        {
            var seats = 2 + game % 3;
            var rules = new LudoRules(new LudoOptions { PawnsToWin = game % 2 == 0 ? 4 : 2 });
            var dice = new SeededRng(game);
            var choices = new SeededRng(game + 1_000_000);
            var s = rules.NewGame(seats);
            var actions = 0;

            while (s.Phase != LudoPhase.Finished)
            {
                Assert.True(actions++ < maxActions, $"{temperament} game {game} did not finish.");
                var action = bot.Choose(s, s.CurrentSeat, choices);
                Assert.Contains(action, rules.LegalActions(s, s.CurrentSeat));
                s = rules.Apply(s, s.CurrentSeat, action, dice);
            }

            Assert.NotNull(rules.Result(s));
        }
    }
}
