# Hytte Hygge — Engineering Plan Review

Date: 2026-10-12
Status: DONE_WITH_CONCERNS (one critical gap, solution defined below)

## Product scope (agreed)

- Free, online, 2–4 player Nordic nostalgia board/card games.
- Launch games: **Ludo**, **Yatzy**, **Olsen**.
- No registration: anonymous sign-in per install; optional Google/Apple link in phase 2.
- Revenue: ads (AdMob, rewarded + interstitial), non-consumable IAP only
  (ad removal, cosmetic packs, VIP subscription). Coins are earned, never sold,
  never cashed out.
- Release: global store availability, paid marketing focused on DK/SE/NO/FI first.

## Decisions

| ID | Decision | Choice |
|----|----------|--------|
| D1 | Where the rules logic lives | Shared **C# rules library** (netstandard2.1) used by Unity and a small **.NET service** on Render. Supabase = Postgres + Auth only. |
| D2 | How state reaches clients | **Stateless .NET**: client sends moves over HTTP, .NET writes to Postgres. Postgres is the source of truth. |
| D3 | Realtime fan-out | **Broadcast from .NET** to Supabase Realtime channel `match:{id}` (not `postgres_changes`). |

## Architecture

```
Unity Client ──HTTP /move──► .NET service (validate + server-side dice)
     ▲                              │
     │                              ▼
     └── Realtime Broadcast ◄── Postgres (matches.state)
            match:{id}

Bot-only game: Unity + Rules lib (fully local, no server)
```

- Dice are always rolled on the server (anti-cheat).
- Server is authoritative; the client only sends intents.
- Bot-only games run fully offline on the device with the same rules library.

## Data model

```
players(id = auth.uid, display_name, country,
        coins, xp, equipped jsonb, created_at)
coin_ledger(id, player_id, delta, reason, ref, at)
entitlements(player_id, product_id, store,
             txn_id unique, expires_at)
matches(id, game_type, mode, status, is_private,
        room_code, seats, current_seat, version,
        state jsonb, turn_deadline, created_at, started_at, finished_at)
match_players(match_id, seat, player_id null,
              is_bot, bot_level, connected, rank)
moves(match_id, seq, seat, action jsonb, at)
matchmaking_queue(player_id, game_type, mode, at)
```

- Coins are never edited directly: every change is a row in `coin_ledger`
  (auditable, prevents double rewards).
- `state` is jsonb because the three games have different state shapes.
- `moves` enables replay and debugging.

## API

```
POST /matchmake {game_type, mode}
  → {match_id, seat} | {status: "queued"}
POST /rooms {game_type, seats} → {room_code, match_id}
POST /rooms/join {room_code} → {match_id, seat}
POST /move {match_id, expected_version,
            action: {type:"roll"} | {type:"move", pawn:2}}
  → {version, state} | 409 conflict
Webhooks:
  RevenueCat → entitlements
  AdMob SSV  → coin_ledger
```

`expected_version` = optimistic locking: if two requests race, only the first
is applied (prevents double-tap double moves).

## Rules library shape

```csharp
interface IGameRules<TState, TAction> {
  TState NewGame(int seats);
  IReadOnlyList<TAction> LegalActions(TState s, int seat);
  TState Apply(TState s, int seat, TAction a, IRng rng);
  GameResult? Result(TState s);
}
```

(Implemented in `src/Hytte.Rules/Core/IGameRules.cs`. `Apply` takes the acting
seat so the rules themselves reject out-of-turn actions; randomness comes only
from the injected `IRng`, so no seed is needed in `NewGame`.)

- Bots only choose from `LegalActions`; they never re-implement rules.
- `IRng` is injected: secure seeded RNG on the server, fixed RNG in tests.
- `Apply` is pure: no DB, no Unity, no mutation of the input.

## Bottlenecks and state issues

- **Turn timeouts:** a .NET `BackgroundService` polls every second for matches
  whose `turn_deadline` has passed and auto-plays the move. Use
  `FOR UPDATE SKIP LOCKED` so two service instances never handle the same match.
- **Reconnect:** client reloads the full `state` after reconnecting, not deltas.
- **Bots in online games:** server computes the bot move immediately; the client
  adds a short animation delay.
- **Realtime limits:** Supabase free tier has a low concurrent-connection cap;
  plan for Pro at launch.
- **Render:** free instances sleep when idle; use an always-on paid instance at launch.
- **Rate limiting** on `/move`.

## Dependencies

- Use: Supabase (anonymous Auth, Postgres, Realtime), RevenueCat, AdMob + UMP,
  Unity IAP, xUnit.
- Avoid: Photon (CCU pricing is overkill for turn-based), Firebase alongside
  Supabase (two backends), any free-text chat service.

## Test map (all gaps; project is new)

```
RULES LIB (xUnit)
├── Ludo
│   ├── [GAP] exit only on 6, extra roll on 6
│   ├── [GAP] capture → pawn home; safe squares
│   ├── [GAP] exact roll to finish; no legal move → pass
│   └── [GAP] 2/3/4 seats, quick vs full mode
├── Yatzy: [GAP] all 15 categories, bonus ≥63, 3 rolls max
├── Olsen: [GAP] special cards, empty deck reshuffle
└── Bots: [GAP] 10k random games → never illegal, always ends

SERVER (integration)
├── [GAP] /move wrong seat → 403
├── [GAP] stale expected_version → 409
├── [GAP] double-tap same move → one applied
├── [GAP] deadline passed → auto-move by service
├── [GAP] all humans left → match abandoned
└── [GAP] RevenueCat webhook replay → no duplicate

USER FLOWS
├── [GAP][→E2E] queue 8s → bots fill → game ends
├── [GAP][→E2E] room code → friend joins → play
└── [GAP][→E2E] app killed mid-game → reopen → resume
```

The "10k random bot games" test is the highest-value test: it catches any
stuck state or illegal move.

## Failure modes

| Failure | Handling |
|---------|----------|
| Network drops while sending a move | Client retries; `expected_version` prevents duplicates. |
| **Timeout service crashes → match stuck forever, silent** | **CRITICAL GAP.** Fix: Render health check + client shows "opponent disconnected" after 2× turn time. |
| Two service instances run the same timeout | `FOR UPDATE SKIP LOCKED`. |

## Not in scope (v1)

Tournaments, Battle Pass, friends list, chat, "Esm-o-Famil" (Stadt Land Fluss),
Google/Apple account linking (phase 2).

## Build order

1. Ludo rules library + tests
2. Unity offline with bots
3. .NET service + database tables
4. Realtime + matchmaking
5. Yatzy and Olsen on the same infrastructure
