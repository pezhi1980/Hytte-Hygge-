# Hytte Hygge

Free online 2–4 player Nordic family games: Ludo, Yatzy, Olsen.

## Layout

```
src/Hytte.Rules/          Pure game rules (netstandard2.1, C# 8, no packages).
                          Shared by the Unity client and the .NET server.
tests/Hytte.Rules.Tests/  xUnit tests for the rules.
docs/eng-review.md        Architecture and decisions.
docs/rules-ludo.md        Exact Ludo rule variant (source of truth).
```

## Test

```
dotnet test
```

Requires the .NET 8 SDK. The Ludo suite includes a fuzz test that plays
10,000 random games (about 15 seconds).

## Using the rules in Unity

Build `src/Hytte.Rules` and copy `Hytte.Rules.dll` into `Assets/Plugins`,
or reference the source folder through an assembly definition.
