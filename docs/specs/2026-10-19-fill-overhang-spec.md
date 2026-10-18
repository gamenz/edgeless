# Fill overhang — Spec

- Date: 2026-10-19
- Status: **Approved**
- Extends: [Edgeless spec](2026-10-18-edgeless-spec.md) §4.2 (target rect) and §4.4 (re-apply)
- Binding rules: [CLAUDE.md §1 Anti-cheat safety rules](../../CLAUDE.md)

---

## 1. Problem

In `fill` mode the target rect is exactly `rcMonitor`. Two real games show that a target equal to a size the game
itself can choose causes bugs, because the game then never sees a real resize:

1. **Elden Ring — resolution change reverted.** The game runs at 1280x720 windowed; Edgeless stretches the window
   to 3840x2160. The user picks 3840x2160 (the monitor resolution) in the game. The game asks for a 3840x2160 window,
   but the window already has that size, so nothing changes: no `WM_SIZE`, no WinEvent (a full trace of every event
   of the process showed none). After a while the game reverts to 1280x720. The same happens with any resolution
   equal to the monitor's (e.g. 2560x1440 on a desktop set to 1440p). Tested by the user: a `custom` profile of
   3840x2161 (1 px beyond the monitor) fixes it.
2. **Expedition 33 — black bars after fullscreen → windowed 4K.** The game makes a framed window whose outer rect is
   clamped to exactly 3840x2160 (client ~3818x2104) and sizes its swap chain to that client area. Removing the frame
   at the same outer size only grows the client area; the game does not resize its swap chain → black bars at the
   right and bottom. This is currently worked around by a "two-step" resize (1 px shorter, then the target) whenever
   the old outer size equals the target.

Edgeless cannot detect the moment a game *wants* a resolution (no event exists, and polling is forbidden), so the fix
must make the collision impossible instead of reacting to it.

## 2. Goals / Non-goals

**Goals**
- In `fill` mode, never leave a managed window at a size the game itself can request, unless the game put it there.
- Keep pixel-perfect output when the game renders at the monitor's native resolution.
- Replace the two-step resize with this single mechanism.
- Work for every game automatically, with no per-game setting.

**Non-goals**
- Games that adopt the window size as their render resolution (Expedition 33 renders at 4K once stretched to 4K).
  That is game logic; it cannot be changed from outside the game.
- `center` and `custom` modes: unchanged.
- Reacting to monitor layout changes while a window is managed (unchanged from today).

## 3. Design

### 3.1 Fill target with a 1 px overhang

`fill` target = `rcMonitor` grown by **1 physical pixel on one free edge**. The extra row/column lies outside the
monitor and is never visible; the window still covers the whole monitor.

**Choosing the edge** (pure logic in `Edgeless.Core/Geometry`, from the selected monitor and all monitors from
`EnumDisplayMonitors`):
- Candidate edges in order: **bottom, right, top, left**.
- For each edge, the 1 px strip just outside the monitor along that edge (e.g. bottom: `(L, B, R, B+1)`).
- An edge is **free** when the strip intersects no other monitor's `rcMonitor`. Pick the first free edge.
- No free edge (monitors on all four sides) → no overhang: target = `rcMonitor` (today's behavior, logged once as
  Info per apply).

Example, one 3840x2160 monitor at (0,0): target = `(0,0)-(3840,2161)`. A second monitor directly below → right edge:
`(0,0)-(3841,2160)`.

### 3.2 Native fit is accepted as is

Before applying, if the window is **already borderless** (same check as today, honoring `addPopupStyle`) **and its
window rect equals `rcMonitor` exactly**, Edgeless treats it as done (`AlreadyBorderless`, no API call), even though
it differs from the overhang target.

Why: when the game renders at the native resolution and sets its own borderless monitor-sized window (Elden Ring in
windowed mode at 3840x2160 does exactly this), stretching it by 1 px would make the compositor resample 2160 rows into
2161 and blur the whole image. Below native resolution the image is scaled anyway, so 1 px more is invisible.

Resulting flow for the Elden Ring case:
1. Game at 1280x720 → Edgeless stretches to 3840x2161.
2. User picks 3840x2160 in the game → the game's own `SetWindowPos` is now a real resize (2161 → 2160) → the game
   receives `WM_SIZE` and keeps the setting.
3. `EVENT_OBJECT_LOCATIONCHANGE` → re-apply → borderless and exactly `rcMonitor` → accepted, nothing changes.

A framed window at exactly `rcMonitor` (the Expedition 33 case) is **not** accepted (it is not borderless), so it gets
the overhang target, whose size differs from the old outer size → a real resize → the game resizes its swap chain.

### 3.3 Two-step resize removed

`BorderlessApplier` sets the target with one `SetWindowPos`, as before the two-step fix. The two-step code and its
"two-step" log suffix are removed.

### 3.4 Logging

The `Applied:` line names the overhang edge, e.g. `... -> (0,0 3840x2161) overhang=bottom ...` (`overhang=none` when
no edge is free). The existing `Already borderless:` line is kept.

### 3.5 Anti-cheat

No new API. `EnumDisplayMonitors` and `SetWindowPos` are already in the CLAUDE.md §1.2 whitelist and already used.
A window 1 px larger than the monitor is an ordinary window position.

## 4. Risks

- **Bleed onto an adjacent monitor**: avoided by the free-edge rule; with monitors on all sides there is no overhang.
- **Games that adopt the window size** (Expedition 33): they may render at 3840x2161 or clamp themselves back to
  3840x2160. Either way the result is stable: 2161 equals the target, and a borderless exact fit is accepted (§3.2).
- **Hidden row**: one row (or column) of the scaled image is off-screen. At the scale factors involved (≥ 1.5x for
  1440p on 4K) this is under one source pixel.
- **Compositor features** (independent flip, VRR) may prefer a window exactly equal to the monitor. The overhang only
  applies when the game renders below native resolution, where the output is already scaled.
- **Guard** (§4.4 of the main spec): the native-fit acceptance never triggers an apply, so it cannot add re-applies.

## 5. Changes

- `Edgeless.Core/Geometry`: fill-target computation with free-edge selection (all monitors in, target + edge out).
- `TargetRectCalculator.ComputeTarget`: `fill` uses the overhang target; `center`/`custom` unchanged.
- `BorderlessApplier.Apply`: native-fit acceptance; drop two-step; log the edge.
- Main spec §4.2 step 5 (`fill` target) and the idempotency note: updated to point to this spec.

## 6. Testing

**Unit (`Edgeless.Core.Tests`)**
- Single monitor → bottom overhang.
- Monitor below → right; monitors below and right → top; monitors on all four sides → no overhang.
- Negative coordinates (monitor left of / above the primary).
- A monitor only diagonally adjacent does not block an edge.

**Integration (`Edgeless.IntegrationTests`, TestWindow)**
- Framed window → `fill` → rect = monitor + 1 px at the chosen edge, borderless.
- Borderless window at exactly `rcMonitor` → `AlreadyBorderless`, rect unchanged.
- Framed window at exactly `rcMonitor` (Expedition 33 case) → outer size changes (`track-size` counter increases).
- Stretched window, then the window itself sets exactly `rcMonitor` (Elden Ring case) → the window saw a real
  resize, and the next apply returns `AlreadyBorderless`.
- Existing `fill` assertions (`rect == rcMonitor`) updated to the overhang target.

## 7. Acceptance criteria

- Elden Ring: from any lower windowed resolution, picking the monitor resolution keeps it (no revert), with a plain
  `fill` profile.
- Elden Ring at native resolution: window stays exactly `rcMonitor` (no blur).
- Expedition 33: fullscreen → windowed 4K shows no black bars, without the two-step code.
- Wuchang: unchanged behavior (1440p setting kept, stretched to fill).
- Gate passes; all tests pass.

## 8. Decisions for review

- Edge order bottom → right → top → left (bottom first: the hidden row is the bottom of the image, usually HUD-free
  margin, and a monitor below is rarer than one to the side).
- No setting to turn the overhang off. If a game turns out to dislike it, `custom` with `rcMonitor` values is the
  per-game escape hatch.
- Overhang is always exactly 1 px.
