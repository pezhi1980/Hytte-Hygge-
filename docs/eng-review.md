# Hytte Hygge — Engineering Plan Review

Date: 2026-10-12
Status: DONE_WITH_CONCERNS (one critical gap, solution defined below)

## Product scope (agreed)

- Free, online, 2–4 player Nordic nostalgia board/card games.
- Launch games: **Ludo**, **Yatzy**, **Olsen**.
- No registration: anonymous sign-in per install; optional Google/Apple link in phase 2.
- Revenue: ads (AdMob, rewarded + interstitial), non-consumable IAP only
  (ad removal, cosmetic packs). VIP subscription moved to v2 (design-plan 11A).
  Coins are earned, never sold, never cashed out.
- Audience 13+ (design-plan 9A). UI, flows and visual design: `docs/design-plan.md`.
- Release: global store availability, paid marketing focused on DK/SE/NO/FI first.

## Decisions

| ID | Decision | Choice |
|----|----------|--------|
| D1 | Where the rules logic lives | Shared **C# rules library** (netstandard2.1) used by Unity and a small **.NET service** on Render. Supabase = Postgres + Auth only. |
| D2 | How state reaches clients | **Stateless .NET**: client sends moves over HTTP, .NET writes to Postgres. Postgres is the source of truth. |
| D3 | Realtime fan-out | **Broadcast from .NET** to Supabase Realtime channel `match:{id}` (not `postgres_changes`). |
| D4 | Channel privacy | `match:{id}` is a **private** channel (`private: true`). RLS `select` policy on `realtime.messages` allows only users in `match_players` for that match; no client `insert` policy (only the server sends). "Allow public access" off. |

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
players(id = auth.uid,
        name_adj smallint, name_animal smallint, name_num smallint,
                         -- generated public name as word ids; each client renders
                         -- it in its own language (design-plan 12C). No free text.
        country, coins, xp, equipped jsonb, created_at)
coin_ledger(id, player_id, delta, reason, ref, at)
entitlements(player_id, product_id, store,
             txn_id unique, expires_at)
matches(id, game_type, mode, status, is_private,
        room_code, seats, current_seat, version,
        state jsonb, turn_deadline, created_at, started_at, finished_at)
match_players(match_id, seat, player_id null,
              is_bot, bot_level, connected, rank,
              controller,      -- human | bot_standin (design-plan 5A)
              missed_turns,    -- consecutive; 2 → bot_standin
              room_nickname null, -- private rooms only (design-plan 12C)
              bot_persona null)   -- shared persona list index (design-plan 4A)
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
POST /matches/{id}/resume
  → {version, state, seat}
  -- caller must own a seat; sets controller=human, missed_turns=0
  -- (design-plan 5A). Bumps version. Also the one reconnect path.
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
- **Bots live in the rules lib** (`Hytte.Rules.Bots`): `IBot<TState,TAction>`
  with `TAction Choose(TState s, int seat, IRng rng)`. `LudoBot` has three
  temperaments: calm (random legal), clever (prefer capture, then finish,
  then leave yard), cunning (clever + avoid squares an opponent can reach).
  The same code runs offline in Unity, for online bots and for stand-ins.
- `IRng` is injected: secure seeded RNG on the server, fixed RNG in tests.
- `Apply` is pure: no DB, no Unity, no mutation of the input.

## Bottlenecks and state issues

- **Turn timeouts:** a .NET `BackgroundService` polls every second for matches
  whose `turn_deadline` has passed and auto-plays the move. Use
  `FOR UPDATE SKIP LOCKED` so two service instances never handle the same match.
- **Reconnect:** client calls `POST /matches/{id}/resume` and reloads the full
  `state`, not deltas. The same call reclaims a seat from a bot stand-in.

## Turn advance (one code path for humans, bots and stand-ins)

Every state change (`/move`, timeout auto-move, `/resume`) runs in one DB
transaction on the match row (`SELECT … FOR UPDATE`), then broadcasts once.

```
apply action ──► version++ ──► next seat's controller?
                                   │
              ┌────────────────────┼─────────────────────┐
              ▼                    ▼                     ▼
          human              bot / bot_standin       game finished
     set turn_deadline     apply bot actions in a      ranks, rewards
     = now + 20 s          loop until a human seat     (coin_ledger)
     missed_turns kept     or finish (same txn)
              │                    │                     │
              └──────────── broadcast state once ────────┘

timeout service: deadline passed ──► auto-move for that human seat,
                                     missed_turns++ ; if 2 → controller = bot_standin
human /move on own turn ──► missed_turns = 0
```

- **Broadcast payload:** one message per transaction:
  `{from_version, version, state, steps:[{seat, action, roll}]}`. `steps` are
  the rows just written to `moves`. The client plays them through its
  animation queue (dice, 120 ms hops, captures) and then checks it ended on
  `state`; if `from_version` ≠ its version it skips animation and snaps to
  `state`.
- **Timeout poller index:** partial index on `matches(turn_deadline)
  WHERE status = 'active'` so the 1 s poll stays an index scan.
- **Seat order (design-plan "who starts"):** rules lib keeps seat 0 first. At
  match start the server assigns players to seats in a random order using the
  secure RNG. Offline games do the same on the device.
- **Bot disclosure (4A):** `match_players` gets `bot_persona smallint`
  (index into the shared persona list: name + temperament). `is_bot` and
  `controller = bot_standin` are both sent to clients so the badge can render.
- **Room nickname (12C):** 1–16 characters after trimming; control and
  bidi-override characters stripped; set only on private-room seats; never
  copied to `players`.
- **Client-only, no server work:** interstitial frequency (10A) is a local
  counter plus the ad-removal entitlement; consent timing (6A) means the ad SDK
  is not initialised before the UMP form. Anonymous sign-in at launch stays.
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

## Test map

Server integration tests run against **real Postgres in Testcontainers**
(same major version and the same Supabase migrations as production), locally
and in CI. Realtime RLS (D4) is tested against a Supabase branch in a separate,
slower job. No in-memory fake DB: row locks and `SKIP LOCKED` are the point.

```
RULES LIB (xUnit)
├── Ludo                     [DONE] LudoRulesTests + 10k fuzz (LudoFuzzTests)
├── Bots (Hytte.Rules.Bots)
│   ├── [GAP] every choice ∈ LegalActions — 10k games per temperament (fuzz)
│   ├── [GAP] clever: capture > finish > leave yard (fixed positions)
│   └── [GAP] cunning: moves out of an opponent's reach when it can
├── Yatzy: [GAP] all 15 categories, bonus ≥63, 3 rolls max
└── Olsen: [GAP] special cards, empty deck reshuffle

SERVER (integration, Testcontainers Postgres)
├── /move
│   ├── [GAP] wrong seat → 403 ; not a member → 403
│   ├── [GAP] stale expected_version → 409
│   ├── [GAP] double-tap same move → exactly one applied
│   └── [GAP] own-turn human move → missed_turns = 0
├── Turn advance
│   ├── [GAP] next seat human → turn_deadline = now + 20 s
│   ├── [GAP] next seat bot / bot_standin → loop until human seat or finish
│   ├── [GAP] game ends inside bot loop → ranks + one coin_ledger row per player
│   └── [GAP] whole loop = one transaction, one broadcast
├── Timeout service
│   ├── [GAP] deadline passed → auto-move, missed_turns++
│   ├── [GAP] 2nd miss → controller = bot_standin, bot plays at once
│   └── [GAP] two service instances → each match handled once (SKIP LOCKED)
├── /matches/{id}/resume
│   ├── [GAP] not a member → 403
│   ├── [GAP] bot_standin → human, missed_turns = 0, version++
│   ├── [GAP] finished match → final state, no change
│   └── [GAP] two concurrent resumes → one version bump, same final state
├── Seat shuffle: [GAP] each player gets exactly one seat; ~uniform over 10k
├── Names: [GAP] name id outside word list → 400 ; nickname bidi/control chars stripped
├── [GAP] all humans left → match abandoned
└── [GAP] RevenueCat webhook replay → no duplicate entitlement

REALTIME (Supabase branch job)
├── [GAP][→E2E] non-member cannot join match:{id}
└── [GAP][→E2E] member cannot send to match:{id} (server only)

USER FLOWS
├── [GAP][→E2E] queue 8 s → named, badged bots fill → game ends
├── [GAP][→E2E] room code → friend joins with nickname → only room sees it
├── [GAP][→E2E] miss 2 turns → stand-in plays → reopen app → resume → play
├── [GAP][→E2E] app killed mid-game → reopen → resume
└── [GAP][→E2E] first launch → bot game → no ad SDK before consent
```

The "10k random bot games" tests are the highest-value tests: they catch any
stuck state or illegal move, and now also any bot that picks an illegal action.

## Failure modes

| Failure | Handling |
|---------|----------|
| Network drops while sending a move | Client retries; `expected_version` prevents duplicates. |
| **Timeout service crashes → match stuck forever, silent** | **CRITICAL GAP.** Fix: Render health check + client shows "opponent disconnected" after 2× turn time. |
| Two service instances run the same timeout | `FOR UPDATE SKIP LOCKED`. |
| Player reconnects while the stand-in bot loop runs | Both take the match row lock; `/resume` waits, then flips `controller` for the next turn. Test: concurrent resume. |
| Bot loop never reaches a human seat (all humans stand-ins or gone) | Loop ends at game finish (rules guarantee termination, fuzz-tested); if every human seat is a stand-in for 10 min → match abandoned. |
| Nickname with bidi override renders a fake name | Strip control + bidi characters server-side; test. |
| Realtime private channel misconfigured (public access left on) | Integration job asserts a non-member join is rejected; fails CI. |
| Bot picks an illegal action | `Apply` throws `IllegalActionException`; bot fuzz test prevents it; server logs + falls back to first legal action. |

## Not in scope (v1)

Tournaments, Battle Pass, friends list, chat, "Esm-o-Famil" (Stadt Land Fluss),
Google/Apple account linking (phase 2).

## Build order

1. Ludo rules library + tests
2. Unity offline with bots
3. .NET service + database tables
4. Realtime + matchmaking
5. Yatzy and Olsen on the same infrastructure
