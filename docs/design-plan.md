# Hytte Hygge — Design Plan

Status: REVIEWED (`/plan-design-review`, 2026-10-13 → 2026-10-16). Visual tokens: `DESIGN.md`.
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

## 3. User journey

### First launch (Decision 6A: play first, ask later)

1. App opens → 1.5 s logo → straight into a **Quick Ludo game vs one bot**
   ("Mormor Jane"). No Home, no system dialogs, no ads.
2. Teaching happens inside the game, one hint at a time, only when needed:
   - first turn: a soft pulse on the die + "Tap to roll"
   - first 6: "A six lets a pawn out — and you roll again"
   - first legal move choice: the movable pawns lift and glow
   - first capture (either side): "Sent home! It must wait for a six"
   Each hint shows once, ever.
3. Game ends → Result screen → **then** the UMP consent form (EU), then ATT
   (iOS), each once. No ad SDK is initialised before consent.
4. Then Home, with Ludo already the "last game".

> Check before release: confirm with current UMP/AdMob docs that deferring the
> consent form until no ad has been requested is compliant. Not legal advice.

### Storyboard

| Step | Player does | Player feels | Design supports it with |
|---|---|---|---|
| 0–5 s | Opens app | Curious, a bit wary | Warm birch background, no dialogs, the board appears |
| 5–60 s | First roll, first six | "Oh, I remember this" | Wooden clack, felt-cup dice sound, one hint at a time |
| 1–5 min | First capture, first pawn home | Playful rivalry, satisfaction | Gentle hop-home arc, chime when a pawn finishes |
| End of game 1 | Wins or loses vs Mormor Jane | Proud, or "one more" | Rank list, **Play again** primary, coins count up |
| After game 1 | Consent + ATT, then Home | Trust (asked after, not before) | Plain-language consent intro on birch card |
| Day 2–7 | Plays online / with family via room code | Connection | Room code shareable to Messages, seats fill with real names |
| Game 50 | Routine evening game | Comfort ritual | "Next unlock" progress on Result; cosmetics change the feel (boards, dice) |

## 4. Visual identity (Decision 7A: "Cabin table")

The memory we design for: a painted wooden board on the table of a summer
cabin, evening light, family around it. Every surface should look like a real
object on that table, not a screen.

### Materials

- **Table / background:** birch-cream painted wood, very faint grain. Never a
  gradient, never flat white.
- **Board:** painted wood, hand-painted track squares with slightly uneven
  edges, matte. No bevels, no gloss, no glow.
- **Pawns:** turned wooden pawns, painted, with a visible grain and a soft
  contact shadow (one shadow style, used only for objects that sit on the table).
- **Dice:** bone-white with round dark pips, rolled from a small felt cup.
- **UI chrome** (buttons, chips, sheets): flat painted-wood or paper-card
  surfaces in the palette. One corner radius (12 pt) for all chrome.

### Palette (tokens)

| Token | Hex | Use |
|---|---|---|
| `birch` | `#F3EBDD` | Background / table |
| `wood` | `#C89B6D` | Board frame, secondary surfaces |
| `ink` | `#2B2622` | Text, pips, icons |
| `lingon` | `#B23A48` | Player color 0, primary action button |
| `pine` | `#2F5D50` | Player color 1 |
| `mustard` | `#D9A441` | Player color 2 |
| `fjord` | `#3A6EA5` | Player color 3 |
| `candle` | `#F6C76B` | Highlights: legal-move glow, turn ring |

- One accent for actions: `lingon`. Never more than one primary button per screen.
- Player colors differ in lightness, not only hue. Each pawn also carries a
  **shape mark** on its top face: ● lingon, ▲ pine, ■ mustard, ◆ fjord.
  Color is never the only signal.

### Type

- **Display:** Fraunces (soft serif, OFL): game titles, result rank, room code.
- **UI / body:** Atkinson Hyperlegible (OFL): everything else. Chosen for
  legibility at small sizes and full Nordic character support (æ ø å ä ö).
- Never Inter, Roboto, Arial or the system font as the primary face.

### Sound and motion

- Sounds are real objects: wooden clack (pawn lands), felt-cup rattle (dice),
  soft thunk (sent home), small chime (pawn finishes). No synth whooshes.
- Motion is physical: pawns hop square by square (120 ms/square), a captured
  pawn arcs back to its yard (500 ms), dice tumble 600 ms.
- Win: paper **bunting** in the palette drops across the top of the screen
  (not confetti explosions).

### Banned (AI-slop list for this project)

Gradients, glossy/bevelled buttons, neon, glow effects, emoji in UI, icons in
colored circles, card grids on Home, centered-everything layouts, fake
"golden" crowns and treasure chests.

## 5. Design system

- Tokens, type scale, shape, motion and sound are in `DESIGN.md` at the repo
  root. Unity styles reference those token names.

## 6. Accessibility and layout

### Choosing a pawn (Decision 8A)

- Only **legal** pawns (from `LegalActions`) lift slightly and glow `candle`.
  Every other pawn is not tappable.
- **One legal move:** the pawn lifts, holds 400 ms so the player sees what
  happens, then moves by itself. Setting: "Auto-move when only one choice"
  (on by default).
- **Stacks:** own pawns on one square always have the same progress, so moving
  any of them gives the same result. Tapping a stack moves one of them; no
  fan-out menu is needed. A stack shows a small count badge ("×2").
- Opponent pawns sharing a safe square with yours sit offset in the square's
  corners so each stays visible.

### Rules

- Touch target ≥ **48 dp** for every interactive thing, even when the pawn art
  is smaller (hit area extends invisibly).
- Body text ≥ **16 pt**; respects system text size up to 130%.
- Contrast ≥ 4.5:1 for all text (ink on birch passes).
- Color is never the only signal: pawn shape marks, turn ring + name label,
  legal moves lift + glow.
- Localization: DA, SV, NO, FI, EN at launch. Every button holds a string
  **40% longer** than English (Finnish). Test with pseudo-localization.
- Settings: sound, haptics, reduced motion (hops become slides, no bunting
  drop), auto-move.
- Safe areas respected on notched phones; the bottom action stays above the
  home indicator.
- Tablet: same portrait layout, board max width 600 pt, centered; side space
  holds player panels.

## 7. Product decisions that shape the UI

### Audience (Decision 9A)

- Store audience: **13+**. Marketing angle: nostalgia for grown-ups who played
  these games as kids. Store art and ads must not look like a kids' app.
- *Check before release (not legal advice):* Google Play Families and App Store
  age-rating rules.

### Ads (Decision 10A)

- **Interstitial:** only between games (after Result, before the next board),
  at most **1 per 3 finished games**, never in the first session, never mid-game,
  never in private rooms.
- **Rewarded:** always opt-in, one place only: "Double coins" on the Result screen.
- Ad removal (IAP) removes interstitials; rewarded stays available as an option.

### Store (Decision 11A)

- v1 sells only one-time (non-consumable) products: **Remove ads** and
  **cosmetic packs** (boards, pawns, dice).
- VIP subscription moves to v2. Coins are earned only, never sold (unchanged).

### Names (Decision 12C)

- **Public games:** generated names only: adjective + Nordic animal + number
  (e.g. "Glad Elg 42"), localized, with a "new name" reroll. No free text.
- **Private rooms:** each player can set a free-text room nickname
  ("Mor", "Farfar"), shown only to people in that room.

### Smaller defaults (decided here, low risk)

- Country flag: shown small next to the name in public games; can be hidden in
  Settings.
- Turn order: clockwise. Who starts: random, shown with a short
  "Lille Freja starts" toast. (Rules lib keeps seat 0 first; the server
  shuffles seat assignment.)
- Sound and haptics: on by default.

### Deferred

- **XP and unlocks:** what XP unlocks and the "next unlock" bar on Result is
  not designed yet. Needed before the Shop screen is built.
- **Yatzy and Olsen screens:** states and layouts are designed after their
  rules docs exist.

## Approved mockups

Claude Design canvas **"Hytte Hygge — Ludo mockups"** (approved 2026-10-16,
decision 16A).

| Screen | Artboard | Shows |
|---|---|---|
| Board, your turn | `Main.dc.html` | Rotated board (lingon bottom-left), die = 4, two legal pawns lifted with candle ring, ×2 stack on start, opponent chips incl. bot "Mormor Jane" and a bot stand-in, hint + 14 s timer in thumb zone |
| Home | `Home.dc.html` | Wordmark, coins, Shop/Settings icons, wood "Last played: Ludo" panel with Play, three game tiles |
| Result | `Result.dc.html` | Paper bunting, "You won!", rank list, +40 coins / +25 XP, opt-in rewarded "double coins", Play again, Home |

## What already exists (reuse)

- `LegalActions` drives which pawns are tappable: the UI never re-derives rules.
- `LudoState.LastRoll` stays visible after the turn passes: shows the
  opponent's roll and the "No move" moment.
- The three `LudoPhase` values map 1:1 to board states (roll / choose / finished).
- `LudoState.AbsoluteSquare` gives positions; board rotation is a view transform.

## NOT in scope (v1)

| Item | Why deferred |
|---|---|
| Landscape layout | Portrait-only (2A); tablets center the board |
| Evening / candlelight dark mode | Nice-to-have; one theme first |
| VIP subscription screens | Moved to v2 (11A) |
| Chat, emotes, friends list | Not in v1 scope (eng-review) |
| Yatzy and Olsen screens | Need their rules docs first |
| XP unlock tree | Needs its own design pass before the Shop |

## Decisions log

| # | Decision | Choice |
|---|----------|--------|
| 1A | Home hierarchy | Large Play (last game) + 3 smaller game tiles; first launch defaults to Ludo |
| 2A | Orientation | Portrait only; board rotated so own color is bottom-left; dice + action in bottom third |
| 3A | Setup flow | One screen, 3 segmented rows remembered from last game, one Start button |
| 4A | Bot fill | Always disclosed: named Nordic bot characters + small bot badge, 8s countdown, Cancel |
| 5A | Player leaves | 20 s turn timer; after 2 missed turns a badged bot stand-in plays; player can reclaim the seat |
| 6A | First launch | Straight into Quick Ludo vs 1 bot; in-game hints; UMP + ATT after game 1 |
| 7A | Visual direction | "Cabin table": painted wood, wooden pawns, earthy palette, Fraunces + Atkinson Hyperlegible |
| 8A | Pawn choice | Only legal pawns lift/glow; single legal move auto-plays after 400 ms (setting); tap on stack moves one |
| 9A | Audience | 13+; nostalgia for grown-ups |
| 10A | Interstitials | Between games only, ≤1 per 3 games, never first session / mid-game / private rooms |
| 11A | Store v1 | Remove ads + cosmetic packs (one-time). VIP subscription → v2 |
| 12C | Names | Generated names in public; free-text nickname only inside private rooms |
| 16A | Mockups | "Cabin table" mockups approved (Board, Home, Result) |
