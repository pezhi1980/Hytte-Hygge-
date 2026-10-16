# Hytte Hygge — Design System (v1)

Source of truth for how the game looks and feels. Decisions and reasoning live
in `docs/design-plan.md`. Approved mockups: Claude Design canvas
"Hytte Hygge — Ludo mockups" (Board, Home, Result).

## Direction: "Cabin table"

A painted wooden board on a summer-cabin table in evening light. Every surface
looks like a real object on that table, not a screen.

## Color tokens

| Token | Hex | Use |
|---|---|---|
| `birch` | `#F3EBDD` | App background (the table) |
| `paper` | `#FBF6EE` | Raised surfaces: chips, tiles, track squares |
| `paper-2` | `#EFE4D2` | Secondary rows |
| `wood` | `#C89B6D` | Board frame, hero panel |
| `wood-soft` | `#E8DCC8` | Icon buttons |
| `ink` | `#2B2622` | Text, icons, pips |
| `ink-2` | `#5C524A` | Secondary text (passes 4.5:1 on birch) |
| `lingon` | `#B23A48` | Player color 0 · primary action |
| `lingon-shade` | `#7E2532` | Primary button bottom edge |
| `pine` | `#2F5D50` | Player color 1 |
| `mustard` | `#D9A441` | Player color 2 · coins |
| `fjord` | `#3A6EA5` | Player color 3 |
| `candle` | `#F6C76B` | Legal-move ring, active-turn ring |

Rules
- One primary (`lingon`) button per screen.
- Player colors always carry a shape mark: ● lingon, ▲ pine, ■ mustard, ◆ fjord.
- No gradients (except the board's four-color center), no glow, no neon.

## Type

| Role | Face | Size / weight |
|---|---|---|
| Display (titles, results) | Fraunces | 32–48 pt, 600–700 |
| Section / game names | Fraunces | 20 pt, 600 |
| Body | Atkinson Hyperlegible | 16 pt, 400 / 700 |
| Labels, secondary | Atkinson Hyperlegible | 14 pt (never below) |
| Eyebrow labels | Atkinson Hyperlegible | 14 pt, 700, +0.04em, uppercase |

Both fonts are OFL. Never Inter, Roboto, Arial or the system font as primary.

## Shape and depth

- Corner radius: **12 pt** on all chrome (16 pt on the hero panel and dice).
- Objects that sit on the table (board, pawns, dice, primary button) get one
  hard contact shadow: `0 3px 0 rgba(43,38,34,0.25)`. Nothing else has a shadow.
- Touch targets ≥ 48 dp; icon buttons 44×44 pt visible, 48 dp hit area.

## Board

- 15 × 15 grid, portrait, board frame 6 pt `wood`, track squares `paper` with a
  1 px `ink` 16% border.
- Rotated per player: the local player's color is always bottom-left.
- Safe star squares marked `✦` in `ink` 55%.
- Legal pawns: lifted 3 pt + 3 pt `candle` outline. Stacks show a "×n" badge.

## Motion and sound

| Moment | Motion | Sound |
|---|---|---|
| Dice roll | 600 ms tumble, lands on server value | Felt-cup rattle |
| Pawn move | Hop per square, 120 ms each | Wooden clack per landing |
| Capture | 500 ms arc back to yard | Soft thunk |
| Pawn finishes | Slide to center | Small chime |
| No legal move | Die shakes, "No move" chip 900 ms | — |
| Win | Paper bunting drops across top | — |

Reduced motion: hops become slides, bunting is static.

## Banned

Gradients as backgrounds, glossy or bevelled buttons, glow, neon, emoji in UI,
icons in colored circles, card grids on Home, centered-everything layouts,
golden crowns and treasure chests.
