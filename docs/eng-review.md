# Hytte Hygge — Engineering Plan Review

Date: 2026-10-12, revised 2026-10-17 (`/plan-eng-review` + outside voice)
Status: REVIEWED. Offline-first; online architecture fixed for later.

## Product scope (agreed)

- Free Nordic nostalgia family games, 2–4 players.
- **Release 1 (offline):** **Ludo only**, vs bots, in **Danish, Swedish,
  English**. Yatzy and Olsen tiles show "Coming soon" on Home.
- **Release 2 (online):** private rooms (room code) only.
- **Later, only if retention and revenue numbers allow:** public matchmaking,
  then Yatzy and Olsen as updates, then Norwegian and Finnish.
- No registration: anonymous identity per install; optional Google/Apple link later.
- Revenue: AdMob (rewarded + interstitial, rules in design-plan 10A), one-time
  IAP only (Remove ads, cosmetic packs). VIP subscription → v2 (design-plan 11A).
  Coins are earned, never sold, never cashed out.
- Audience 13+ (design-plan 9A). UI, flows and visual design: `docs/design-plan.md`.

## Decisions

| ID | Decision | Choice |
|----|----------|--------|
| D1 | Where the rules logic lives | Shared **C# rules library** (netstandard2.1, no packages) used by Unity and the server. Bots live there too (`Hytte.Rules.Bots`). |
| D2 | Online server shape | **One stateful .NET service with SignalR** (WebSockets). Matches live in memory, every applied action is persisted to Postgres before it is broadcast. |
| D3 | Supabase role | **Auth + Postgres only.** No Supabase Realtime. |
| D4 | Purchases | **RevenueCat is the source of truth** for entitlements; the app and server query it. No local `entitlements` table. |
| D5 | Offline economy | Coins and XP are stored **on the device** in Release 1 (client trusted: coins only buy cosmetics). Migrated once into `coin_ledger` when the player first goes online. |
| D6 | Release order | Offline Ludo first, measure, then private rooms, then decide on public matchmaking. |

## Architecture

### Release 1 (offline)

```
Unity client
  ├── Hytte.Rules (Ludo + Bots)        all game logic, local RNG
  ├── Local save                        coins, XP, equipped, last game, settings
  ├── RevenueCat SDK                    Remove ads / cosmetic entitlements
  └── AdMob + UMP (+ ATT on iOS)        initialised only after consent (6A)
No server. No account. Infrastructure cost: 0.
```

### Release 2 (online, private rooms)

```
Unity ──SignalR (WSS, Supabase JWT)──► .NET service (single instance)
                                          │  per-match actor: one action at a time
                                          │  server-side dice (secure RNG)
                                          ▼
                                       Postgres (Supabase)
                                       matches.state + moves, written BEFORE
                                       the group broadcast
```

- One SignalR group per match; only seat owners are added (membership checked
  against `match_players` on join). This replaces the private Realtime channel.
- **Per-match actor:** every action for a match goes through one in-process
  queue (e.g. a `Channel<T>` per match), so actions never race inside the
  service. `version` + `expected_version` still reject stale or double-tapped
  client actions.
- **Restart safety:** state is in Postgres after every action. On start-up the
  service loads active matches lazily (on first `/resume` or SignalR join).
  Clients auto-reconnect and call `Resume`.
- **Scale-out later:** SignalR backplane over Postgres if one instance is not
  enough. Not built now.

## Data model (Release 2)

```
players(id = auth.uid,
        name_adj smallint, name_animal smallint, name_num smallint,
                         -- generated public name as word ids; each client renders
                         -- it in its own language (design-plan 12C). No free text.
        country, coins_migrated bool, xp, equipped jsonb, created_at)
coin_ledger(id, player_id, delta, reason, ref unique, at)
                         -- 'offline_migration' row once per player (D5)
matches(id, game_type, mode, status, is_private,
        room_code, seats, current_seat, version,
        state jsonb, turn_deadline, created_at, started_at, finished_at)
match_players(match_id, seat, player_id null,
              is_bot, bot_level, connected, rank,
              controller,         -- human | bot_standin (design-plan 5A)
              missed_turns,       -- consecutive; 2 → bot_standin
              room_nickname null, -- private rooms only (design-plan 12C)
              bot_persona null)   -- shared persona list index (design-plan 4A)
moves(match_id, seq, seat, action jsonb, roll, at)
```

- Coins are never edited directly: every change is a row in `coin_ledger`
  (`ref unique` prevents double rewards).
- `state` is jsonb because the games have different state shapes. Ludo state
  carries its own `PawnsToWin` and roll-attempt counter (see Rules library).
- `moves` enables replay, debugging and the animation `steps` in broadcasts.
- `matchmaking_queue` is not created until public matchmaking is approved.

## API (Release 2, SignalR hub `/hub/match`)

```
CreateRoom(game_type, seats, mode)      → {room_code, match_id, seat}
JoinRoom(room_code, nickname?)          → {match_id, seat}
Resume(match_id)                        → {version, state, seat}
    -- caller must own a seat; sets controller=human, missed_turns=0
    -- (design-plan 5A). The one reconnect path.
Act(match_id, expected_version, action) → ok | conflict(version, state)
    action: {type:"roll"} | {type:"move", pawn:2}

server → group:  Update {from_version, version, state,
                         steps:[{seat, action, roll}]}
HTTP:  GET /health   -- fails if the deadline loop has not ticked in 5 s
Webhooks: AdMob SSV → coin_ledger (rewarded "double coins")
```

`expected_version` = optimistic locking: a stale or repeated action is
rejected with the current state (prevents double-tap double moves).

## Rules library shape

```csharp
interface IGameRules<TState, TAction> {
  TState NewGame(int seats);
  IReadOnlyList<TAction> LegalActions(TState s, int seat);
  TState Apply(TState s, int seat, TAction a, IRng rng);
  GameResult? Result(TState s);
}
interface IBot<TState, TAction> {
  TAction Choose(TState s, int seat, IRng rng);   // always ∈ LegalActions
}
```

- `Apply` is pure: no DB, no Unity, no mutation of the input.
- `IRng` is injected: secure RNG on the server, local RNG offline, fixed RNG in tests.
- **Bots** (`Hytte.Rules.Bots`): `LudoBot` with three temperaments: calm
  (random legal), clever (prefer capture, then finish, then leave yard),
  cunning (clever + avoid squares an opponent can reach). Same code offline,
  for online bots, stand-ins and timeout auto-moves.
- **Pending rules changes** (spec updated in `docs/rules-ludo.md`, code next):
  - `PawnsToWin` moves from `LudoRules` into `LudoState`, so a stored state
    fully determines its own result.
  - **Three attempts to roll a 6** while the seat has no pawn on the track.

## Turn flow (online)

```
action applied ──► persist (matches + moves) ──► next seat's controller?
                                                   │
              ┌────────────────────────────────────┼──────────────────────┐
              ▼                                    ▼                      ▼
            human                         bot / bot_standin          game finished
   deadline = now + 20 s              apply bot actions until a      ranks, coin_ledger
   (per action: roll OR move)         human seat or finish
              │                                    │                      │
              └──────────────── one Update to the group ──────────────────┘
```

- **Turn timer (per action):** 20 s for each roll or move; every action
  restarts it. On expiry the server plays that action with the **calm** bot.
  A turn counts as missed if any of its actions was auto-played; 2 missed turns
  in a row → `controller = bot_standin` (design-plan 5A). An own-turn human
  action resets `missed_turns`.
- **Deadlines:** an in-memory timer per active match, plus **lazy processing**:
  every `Act`, `Resume` or `JoinRoom` first applies any overdue deadline for
  that match. `/health` fails if the timer loop stalls (fixes the old silent
  critical gap).
- **Broadcast payload:** `steps` are the rows just written to `moves`; the
  client animates them (dice, 120 ms hops, captures), then checks it ended on
  `state`. If `from_version` ≠ its version it snaps to `state`.
- **Seat order:** players are assigned to seats in random order (secure RNG
  online, local RNG offline); the rules lib keeps seat 0 first.
- **Room nickname (12C):** 1–16 characters after trimming; control and
  bidi-override characters stripped; never copied to `players`.
- **Client-only:** interstitial frequency (10A) and consent timing (6A).
- **Render:** always-on paid instance for Release 2 (free instances sleep).
- **Rate limiting** on `Act` per connection.

## Dependencies

- Use: Unity, Supabase (anonymous Auth, Postgres), ASP.NET Core SignalR,
  RevenueCat, AdMob + UMP, Unity IAP via RevenueCat, xUnit, Testcontainers.
- Avoid: Supabase Realtime (D3), Photon (CCU pricing), Firebase alongside
  Supabase, any free-text chat service.

## Test map

Rules and bots: xUnit. Server: integration tests against **real Postgres in
Testcontainers** (same major version, same migrations) locally and in CI; the
SignalR hub is hosted in-process with `WebApplicationFactory`.

```
RULES LIB (xUnit)
├── Ludo                    [DONE] LudoRulesTests + 10k fuzz
│   ├── [GAP] three attempts to roll a 6 when no pawn is on the track
│   │         (fail 3× → pass; 6 on try 2 → leave yard; attempts reset per turn)
│   └── [GAP] PawnsToWin read from state (quick vs full after round-trip)
├── Bots
│   ├── [GAP] every choice ∈ LegalActions — 2k games per temperament
│   ├── [GAP] clever: capture > finish > leave yard (fixed positions)
│   └── [GAP] cunning: moves out of an opponent's reach when it can
├── Yatzy / Olsen: [GAP] when their rules docs exist
UNITY (Release 1, EditMode/PlayMode)
├── [GAP] local coins/XP persist across app restarts
├── [GAP] interstitial counter: never first session, ≤1 per 3 games, none with Remove ads
└── [GAP][→E2E] first launch → bot game → no ad SDK before consent
SERVER (Release 2, Testcontainers)
├── Act
│   ├── [GAP] wrong seat / not a member → rejected
│   ├── [GAP] stale expected_version → conflict + current state
│   ├── [GAP] double-tap same action → exactly one applied
│   └── [GAP] own-turn human action → missed_turns = 0
├── Turn flow
│   ├── [GAP] action persisted before broadcast (kill after persist → state survives)
│   ├── [GAP] bot / stand-in loop until human seat or finish; one Update
│   ├── [GAP] game ends inside bot loop → ranks + one coin_ledger row per player
│   └── [GAP] per-match actor: 50 concurrent Acts → versions strictly sequential
├── Deadlines
│   ├── [GAP] expiry → calm-bot auto-action, timer restarts per action
│   ├── [GAP] 2 missed turns → bot_standin
│   ├── [GAP] overdue deadline applied lazily on Act/Resume after restart
│   └── [GAP] /health fails when the loop has not ticked for 5 s
├── Resume: [GAP] non-member rejected; stand-in → human; finished → final state
├── Rooms: [GAP] wrong code / full / started / host left; nickname sanitised
├── Names: [GAP] word id outside list → rejected
├── Seat shuffle: [GAP] one seat each; ~uniform over 10k
└── Offline migration: [GAP] coins migrated once (second call no-op)
USER FLOWS
├── [GAP][→E2E] room code → friend joins with nickname → play → finish
├── [GAP][→E2E] miss 2 turns → stand-in plays → reopen → Resume → play
└── [GAP][→E2E] service restart mid-game → clients reconnect → same state
```

The 10k random-game tests (rules and bots) are the highest-value tests: they
catch stuck states, illegal moves and bots that pick illegal actions.

## Failure modes

| Failure | Handling |
|---------|----------|
| Network drops while sending an action | Client retries; `expected_version` prevents duplicates. |
| Deadline loop dies silently | Lazy deadline processing on every call + `/health` tick check → Render restarts the service. |
| Service restarts mid-game | State persisted before broadcast; clients auto-reconnect and `Resume`. |
| Player reconnects while the stand-in loop runs | Actor queue orders `Resume` after the loop; `controller` flips for the next turn. |
| Every human seat is a stand-in | Game still terminates (fuzz-tested); after 10 min with no human the match is abandoned. |
| Nickname with bidi override | Stripped server-side; tested. |
| Bot picks an illegal action | Bot fuzz test prevents it; at runtime log and fall back to the first legal action. |
| Reinstall loses purchases | RevenueCat restore (D4). Coins/XP are per device until account linking (stated in-app). |

## Not in scope (now)

- Public matchmaking and `matchmaking_queue` (after Release 2 numbers).
- Yatzy and Olsen (updates after Release 1).
- Norwegian and Finnish.
- Account linking (Google/Apple), cross-device coins.
- Tournaments, Battle Pass, friends list, chat, "Esm-o-Famil".
- SignalR scale-out backplane.
- Rules lib packaged as a Unity DLL in CI (Unity references the source folder via asmdef for now).

## Build order

1. Ludo rules library + tests **[done]**
2. Rules changes: three-attempts rule, `PawnsToWin` in state; `Hytte.Rules.Bots` + fuzz
3. Asset list (board, pawns, dice, sounds: source for each) and a simple
   revenue model (eCPM × DAU vs. costs) — see `TODOS.md`
4. Unity offline Ludo (DA/SV/EN), local coins/XP, AdMob + UMP/ATT, Remove ads
5. **Store compliance:** consent flows, Google Play Data safety, App Store
   privacy labels, age rating 13+, privacy policy, data-deletion contact
6. **Release 1** → measure day-1 / day-7 retention and ad revenue
7. .NET SignalR service + Postgres tables, private rooms → **Release 2**
8. Decide on public matchmaking, Yatzy, Olsen from the numbers

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| CEO Review | `/plan-ceo-review` | Scope & strategy | 0 | — | — |
| Codex Review | `/codex review` | Independent 2nd opinion | 0 | — | Codex not installed; Claude outside voice used |
| Eng Review | `/plan-eng-review` | Architecture & tests (required) | 1 | CLEAR (PLAN) | 6 issues + 12 outside-voice findings, 0 critical gaps open |
| Design Review | `/plan-design-review` | UI/UX gaps | 1 | CLEAR (FULL) | score: 1/10 → 8/10, 13 decisions |
| DX Review | `/plan-devex-review` | Developer experience gaps | 0 | — | — |

- **CROSS-MODEL:** outside voice reshaped scope (offline-first, Ludo-only,
  SignalR instead of Supabase Realtime); all 12 findings accepted by the user.
- **UNRESOLVED:** 0
- **VERDICT:** ENG + DESIGN CLEARED — ready to implement build step 2.
