# Hytte Hygge — Design Plan

Status: IN REVIEW (`/plan-design-review`, started 2026-10-13)
Companion to `docs/eng-review.md`. This file is the source of truth for what the
player sees. Code follows this file.

## Platform

- Mobile (iOS + Android), Unity.
- **Decision 2A: Portrait only.** On tablets the board stays centered and the side
  space holds player panels (no landscape layout in v1).

### Board screen layout (portrait)

```
┌──────────────────────────┐
│ ⏸  opponents (avatar,    │  top: opponents + turn ring
│    name, turn timer)     │
├──────────────────────────┤
│                          │
│        BOARD (square)    │  middle: board, rotated so the
│                          │  local player's color is
│  [you]                   │  always bottom-left
├──────────────────────────┤
│  you · [ DICE / ROLL ]   │  bottom third: own avatar, dice,
│                          │  main action (thumb zone)
└──────────────────────────┘
```

- The board rotates per player so **your color is always bottom-left**.
  Rules and server stay in absolute squares; rotation is view-only.

## 1. Information architecture

### Screen map

```
            ┌──────────────────────┐
            │ HOME                 │
            │ [▶ Play: last game]  │  ← primary action
            │ Ludo · Yatzy · Olsen │  ← three smaller game tiles
            └──┬────────┬──────┬───┘
       ┌───────┘        │      └────────┐
       ▼                ▼               ▼
  ┌──────────┐    ┌──────────┐    ┌──────────┐
  │ SETUP    │    │ SHOP /   │    │ SETTINGS │
  │ mode,    │    │ PROFILE  │    │          │
  │ opponents│    └──────────┘    └──────────┘
  └────┬─────┘
       ▼
  ┌──────────────┐  online only
  │ MATCHMAKING  │
  └────┬─────────┘
       ▼
  ┌──────────────┐
  │ BOARD        │
  └────┬─────────┘
       ▼
  ┌──────────────┐
  │ RESULT       │
  └──────────────┘
```

### Home

- **Decision 1A:** One large **Play** button that starts the last game played,
  plus three smaller tiles, one per game.
- First launch has no "last game": Play defaults to Ludo.

### Setup

- **Decision 3A:** One Setup screen, three segmented rows, each pre-filled with
  the player's last choice. One **Start** button at the bottom (thumb zone).

```
┌──────────────────────────┐
│ ←  Ludo                  │
│                          │
│ Mode     [ Quick | Full ]│  Quick = 2 pawns to win
│ Against  [Bots|Online|Room]
│ Players  [ 2 | 3 | 4 ]   │  hidden when Room (room sets it)
│                          │
│                          │
│ [        START         ] │
└──────────────────────────┘
```

- First launch defaults: **Quick · Bots · 2 players** (shortest path to a
  first win, no network needed).
- "Room" swaps the Start button for two buttons: **Create room** / **Join with code**.

### Matchmaking

- **Decision 4A: Bots are always disclosed, but warm.**
- Seats fill one by one with avatars. A countdown reads "Filling with friends
  in 8…" and a **Cancel** button is always visible.
- Bots are named Nordic characters (e.g. "Mormor Jane", "Onkel Bo",
  "Lille Freja") with a small bot badge on the avatar. Never a fake human
  name or photo.
- Bot level is shown as the character's temperament, not a number
  (calm / clever / cunning).

## 2. Interaction states

### Player leaves mid-game (Decision 5A)

- Turn timer: **20 s**, shown as a ring around the avatar; haptic tick in the
  last 5 s. On expiry the server auto-plays that turn (existing plan).
- After **2 missed turns in a row**, a bot takes the seat: same color, the
  avatar is greyed with a small "playing for {name}" bot badge, and bot turns
  run at bot pacing (no 20 s wait).
- If the player reconnects, they take control back immediately at their next
  turn and the badge disappears.
- *Server impact:* `match_players` needs a per-seat `controller`
  (`human` / `bot_standin`) and a `missed_turns` counter. Rules lib unchanged.

### State table

What the player **sees** in each state.

| Feature | Loading | Empty / first time | Error | Success | Partial |
|---|---|---|---|---|---|
| **App start** | Logo on birch background, max 1.5 s, then Home. Anonymous sign-in runs in background and never blocks. | No last game → Play = Ludo. Guest name auto-generated (e.g. "Glad Elg 42"), editable later. | Offline → Home still works; online options show "Offline, bots only" | — | — |
| **Matchmaking** | Seats fill one by one, countdown "Filling with friends in 8…", Cancel | — | Network lost → "Couldn't reach the table" + Try again / Play with bots | Board slides in | Some humans + bots, each bot badged (4A) |
| **Room: create** | Code appears with a 300 ms reveal | Seats show "Waiting…" + Share code button | Create failed → toast + retry | Host sees seats fill; Start enabled at ≥2 | Host may start with fewer, or fill with bots |
| **Room: join** | Button spinner | Code field: 5 large monospace boxes, no 0/O/1/I | Wrong code, room full, game already started, host left: one specific line each, field stays filled | Straight into seat | — |
| **Board: your turn** | Dice tumble starts instantly on tap; lands on server value (masks up to ~1.2 s) | — | Move rejected / 409 → board eases to server state in 200 ms, no error popup | Pawn hops square by square | — |
| **Board: no legal move** | — | — | — | Die shakes, "No move" chip 900 ms, turn passes | — |
| **Board: opponent turn** | Bot "thinking" 700–1200 ms (randomised) | — | Opponent offline → greyed avatar + ring timer; after 2 misses → bot stand-in (5A) | — | — |
| **Own connection** | "Reconnecting…" banner after 2 s | — | After 10 s → banner adds Retry; game state is never lost (server-held) | Banner fades, full state reloaded (not deltas) | — |
| **App killed mid-game** | — | — | — | Reopen → straight back into the board, not Home | — |
| **Result** | — | — | — | Rank list, **Play again** primary, coins/XP count-up | Coins pending (rewarded ad SSV) → "Reward on its way" |
| **Rewarded ad** | Button spinner | — | No fill / failed → "No video right now, try later". Never deducts anything. | Reward shown only after server confirms | — |
| **Purchase** | Store sheet (system) | — | Cancelled → silent return. Failed → one line + retry. | Item appears equipped with a short shine | Pending (family approval) → "Waiting for approval" |
| **Restore purchases** | Spinner in Settings | Nothing to restore → "No purchases found" | Error → retry | "Restored" + list | — |
| **Shop / Profile** | Skeleton rows | 0 coins → shows how to earn (play, rewarded ad), never "buy coins" | — | — | — |

## Decisions log

| # | Decision | Choice |
|---|----------|--------|
| 1A | Home hierarchy | Large Play (last game) + 3 smaller game tiles; first launch defaults to Ludo |
| 2A | Orientation | Portrait only; board rotated so own color is bottom-left; dice + action in bottom third |
| 3A | Setup flow | One screen, 3 segmented rows remembered from last game, one Start button |
| 4A | Bot fill | Always disclosed: named Nordic bot characters + small bot badge, 8s countdown, Cancel |
| 5A | Player leaves | 20 s turn timer; after 2 missed turns a badged bot stand-in plays; player can reclaim the seat |
