# TODOs

## Design: XP and unlocks

- **What:** Decide what XP unlocks (boards, pawns, dice) and how the
  "next unlock" bar on the Result screen works.
- **Why:** Without it, game 50 has no reason to come back, and the Shop screen
  can't be built.
- **Pros:** Long-term retention; coins get a clear sink.
- **Cons:** One more small design pass.
- **Context:** Deferred in `/plan-design-review` (2026-10-16). See
  `docs/design-plan.md` → Deferred. Coins are earned only (never sold).
- **Depends on:** Nothing. Must happen **before the Shop screen is built**.
  Not needed for build step 2 (offline Ludo).

## Design: Yatzy and Olsen screens

- **What:** Design-review the board screens and state tables for Yatzy
  (score sheet) and Olsen (card hand) using `DESIGN.md`.
- **Why:** The state table in `docs/design-plan.md` covers Ludo only.
- **Pros:** All three games look like one family.
- **Cons:** A second review pass.
- **Context:** Deferred in `/plan-design-review` (2026-10-16).
- **Depends on:** `docs/rules-yatzy.md` and `docs/rules-olsen.md`
  (build step 5).
