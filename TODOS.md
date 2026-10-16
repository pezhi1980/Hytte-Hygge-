# TODOs

Build-step numbers refer to `docs/eng-review.md` → Build order.

## Design: XP and unlocks

- **What:** Decide what XP unlocks (boards, pawns, dice) and how the
  "next unlock" bar on the Result screen works.
- **Why:** Without it, game 50 has no reason to come back, and the Shop screen
  can't be built.
- **Pros:** Long-term retention; coins get a clear sink.
- **Cons:** One more small design pass.
- **Context:** Deferred in `/plan-design-review` (2026-10-16). Coins and XP are
  local on the device in Release 1 (eng-review D5) and never sold.
- **Depends on:** Nothing. Must happen **before the Shop screen** in build
  step 4 (Release 1 has cosmetics).

## Asset list for Release 1

- **What:** A list of every art and sound asset Release 1 needs, each with its
  source (made / bought / AI-generated) and licence.
- **Why:** The "cabin table" look (DESIGN.md) depends on painted wood, turned
  pawns, dice and foley sounds; none of this is estimated yet.
- **Pros:** Real scope for Release 1; no surprise blockers at the end.
- **Cons:** Half a day of listing and sourcing research.
- **Context:** Outside-voice finding in `/plan-eng-review` (2026-10-17). Cap
  v1 at 1 board, 1 pawn set, 1 dice set, 1 cosmetic pack.
- **Depends on:** `DESIGN.md`. Build step 3, before Unity work (step 4).

## Revenue model (back of envelope)

- **What:** eCPM × DAU × ad frequency (design-plan 10A) + Remove-ads
  conversion vs. monthly costs (stores, Render, Supabase Pro for Release 2).
- **Why:** Ad frequency is deliberately low; we need to know what DAU makes
  Release 2's running costs worth it.
- **Pros:** A clear go/no-go number for the online release.
- **Cons:** Estimates only until Release 1 data exists.
- **Context:** Outside-voice finding in `/plan-eng-review` (2026-10-17).
  Re-run with real numbers after Release 1 (build step 6).
- **Depends on:** Nothing. Build step 3.

## Design: Yatzy and Olsen screens

- **What:** Design-review the board screens and state tables for Yatzy
  (score sheet) and Olsen (card hand) using `DESIGN.md`.
- **Why:** The state table in `docs/design-plan.md` covers Ludo only.
- **Pros:** All three games look like one family.
- **Cons:** A second review pass.
- **Context:** Deferred in `/plan-design-review` (2026-10-16). Both games now
  ship as later updates (eng-review D6).
- **Depends on:** `docs/rules-yatzy.md` and `docs/rules-olsen.md` (build step 8).
