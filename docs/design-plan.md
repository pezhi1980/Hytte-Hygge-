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

## Decisions log

| # | Decision | Choice |
|---|----------|--------|
| 1A | Home hierarchy | Large Play (last game) + 3 smaller game tiles; first launch defaults to Ludo |
| 2A | Orientation | Portrait only; board rotated so own color is bottom-left; dice + action in bottom third |
| 3A | Setup flow | One screen, 3 segmented rows remembered from last game, one Start button |
