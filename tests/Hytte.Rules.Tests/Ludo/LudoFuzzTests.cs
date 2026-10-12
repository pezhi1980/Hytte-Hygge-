using System.Linq;
using Hytte.Rules.Ludo;

namespace Hytte.Rules.Tests.Ludo;

// Plays many random games with random legal actions. Catches stuck states,
// illegal transitions and invariant violations that hand-written cases miss.
public class LudoFuzzTests
{
    private const int Games = 10_000;
    private const int MaxActionsPerGame = 20_000;

    [Fact]
    public void Random_games_always_finish_without_illegal_state()
    {
        for (var game = 0; game < Games; game++)
        {
            var seats = 2 + game % 3;
            var rules = new LudoRules(new LudoOptions { PawnsToWin = game % 2 == 0 ? 4 : 2 });
            var dice = new SeededRng(game);
            var choices = new SeededRng(game + 1_000_000);
            var s = rules.NewGame(seats);
            var actions = 0;

            while (s.Phase != LudoPhase.Finished)
            {
                Assert.True(actions++ < MaxActionsPerGame, $"Game {game} did not finish.");

                // Exactly one seat may act at any time.
                for (var seat = 0; seat < seats; seat++)
                {
                    var legal = rules.LegalActions(s, seat);
                    if (seat == s.CurrentSeat) Assert.NotEmpty(legal);
                    else Assert.Empty(legal);
                }

                var options = rules.LegalActions(s, s.CurrentSeat);
                var action = options[choices.Next(0, options.Count)];
                s = rules.Apply(s, s.CurrentSeat, action, dice);

                AssertInvariants(s);
            }

            var result = rules.Result(s);
            Assert.NotNull(result);
            Assert.Equal(seats, result!.Ranking.Count);
            Assert.Equal(seats, result.Ranking.Distinct().Count());
        }
    }

    private static void AssertInvariants(LudoState s)
    {
        for (var seat = 0; seat < s.Seats; seat++)
            for (var pawn = 0; pawn < LudoState.PawnsPerSeat; pawn++)
            {
                var p = s.Progress(seat, pawn);
                Assert.True(p >= LudoState.Yard && p <= LudoState.Finished, $"Bad progress {p}");
            }

        // No two different colors share an unsafe main-track square.
        var occupied = new System.Collections.Generic.Dictionary<int, int>();
        for (var seat = 0; seat < s.Seats; seat++)
            for (var pawn = 0; pawn < LudoState.PawnsPerSeat; pawn++)
            {
                var abs = s.AbsoluteSquare(seat, pawn);
                if (abs is null || LudoState.IsSafeSquare(abs.Value)) continue;
                if (occupied.TryGetValue(abs.Value, out var other))
                    Assert.Equal(other, seat);
                else
                    occupied[abs.Value] = seat;
            }
    }
}
