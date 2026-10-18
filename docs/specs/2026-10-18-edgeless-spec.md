# Edgeless — Spec

- Date: 2026-10-18
- Status: **Approved**
- Binding rules: [CLAUDE.md §1 Anti-cheat safety rules](../../CLAUDE.md)

---

## 1. Purpose

Many games offer only two choices: Fullscreen exclusive (slow alt-tab, awkward with multiple monitors) or Windowed
(borders, a title bar, not covering the screen). Edgeless is a background system-tray app that automatically turns
the Windowed window of games the user picks into a full-screen **Borderless Windowed** window, like the
"Borderless Gaming" tool, but:

- lightweight: a single `.exe`, no .NET install required, near 0% CPU when idle;
- **anti-cheat safe**: it only manipulates windows from the outside, like a window manager, and never touches the
  game's process, memory or graphics pipeline.

## 2. Goals / Non-goals / Anti-goals

**Goals**
- Automatically apply borderless when a listed game gets focus, and re-apply when the game resets its window.
- A hotkey toggles borderless for the focused window.
- Restore windows exactly to their original state when disabled, when the app exits, and on log-off.
- Correct coordinates on multiple monitors (including negative coordinates) and at every DPI scale.

**Non-goals**
- Games that only support Fullscreen exclusive.
- Scaling/upscaling (Magpie, Lossless Scaling style).
- UWP / Game Pass windows (`ApplicationFrameHost.exe`).
- Cursor clipping, managing several windows of the same game at once.

**Anti-goals** (failures even if everything else works)
- Any behavior that violates [CLAUDE.md §1](../../CLAUDE.md) — even once, even to "fix" a difficult game.
- A continuously running polling loop, or noticeably higher CPU while a game is running.
- Crashing, or leaving a game window broken (borderless with no way back) when the app exits normally.
- Advertising "100% safe / undetectable", or any "stealth" feature.

## 3. Constraints

- **Anti-cheat**: all of [CLAUDE.md §1](../../CLAUDE.md). Every Win32 call aimed at a game's HWND/PID must be in
  the §1.2 whitelist and live in `src/Edgeless/Interop/`. Path-based matching needs no new foreign API (the full path
  already comes from `QueryFullProcessImageName`); the file dialog is an own-window API.
- **Technology**: .NET 10, C#, Native AOT, single file. No WinForms/WPF; the UI is plain Win32.
  P/Invoke via CsWin32. JSON via System.Text.Json source generation.
- **Privileges**: runs without admin. Admin is only needed when a game runs as admin or when the user enables
  "start with Windows as administrator".
- **Performance**: event-driven only (WinEvent hooks, hotkey, message loop). Timers are one-shot only, for
  debounce/delay.

## 4. Detailed behavior

### 4.1 Identifying games and choosing the window

- A **profile** is bound to the **full path** of a game exe (e.g. `D:\Games\Elden Ring\Game\eldenring.exe`), because
  different games can share an exe name (`game.exe`, `launcher.exe`). Matching compares normalized full paths
  (`Path.GetFullPath`, forward slashes → backslashes, trailing whitespace trimmed), case-insensitive. There is no
  name-only matching. The exe file name is derived from the path for display only.
- The process path comes from `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` + `QueryFullProcessImageName` (Win32
  path format), cached briefly per PID (PIDs are reused). If the handle cannot be opened → skip the window, log Warn,
  try nothing else.
- Caveat (shown in the README): if the user picks a launcher exe but the game runs as a different exe, the profile
  never matches; adding the game from the tray while it is focused always records the exe that actually runs.
- **Eligible windows** of a process: top-level (`GetAncestor(GA_ROOT) == hwnd`), no owner
  (`GetWindow(GW_OWNER) == 0`), visible, not cloaked (`DWMWA_CLOAKED`), no `WS_CHILD`, no `WS_EX_TOOLWINDOW`.
  Among them, pick the one with the largest window-rect area.
- **Hard exclusion list** (also for the hotkey): Edgeless's own process; `explorer.exe` with the shell classes
  (`Progman`, `WorkerW`, `Shell_TrayWnd`, `Shell_SecondaryTrayWnd`); `ApplicationFrameHost.exe`; the desktop window.

### 4.2 Applying borderless

Apply to one `hwnd` using its profile (or the default profile when triggered by the hotkey):

1. If the window is minimized → do nothing; apply when it is restored (§4.4).
2. If this `hwnd` is not managed yet → save its **original state**: `GWL_STYLE`, `GWL_EXSTYLE`, `WINDOWPLACEMENT`
   (placement rather than rect, so a maximized window is restored correctly). Original state lives in RAM only,
   keyed by (hwnd, pid).
3. If the window is maximized → `ShowWindow(SW_RESTORE)` first.
4. Compute the new style (pure logic in `Edgeless.Core`):
   - Remove `WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX`.
   - If the profile enables `addPopupStyle` → add `WS_POPUP`. **Off by default** (like Borderless Gaming: removing the
     frame is enough for most games; some games react badly to `WS_POPUP`).
   - Ex-style: remove `WS_EX_DLGMODALFRAME | WS_EX_CLIENTEDGE | WS_EX_STATICEDGE | WS_EX_WINDOWEDGE`; keep
     `WS_EX_APPWINDOW` so the game stays on the taskbar and in alt-tab.
5. Compute the **target rect** (pure logic):
   - Monitor: `monitor = "current"` → `MonitorFromWindow(MONITOR_DEFAULTTONEAREST)`; or a device name
     (e.g. `\\.\DISPLAY2`) → look it up via `EnumDisplayMonitors`; not found → use the current monitor and log Warn.
   - Use `rcMonitor` (not `rcWork` — the taskbar area must be covered too).
   - `sizeMode`:
     - `fill` (default): target = `rcMonitor`.
     - `center`: keep the current client-area size, centered in `rcMonitor` (for games rendering below the monitor
       resolution, avoiding a blurry stretch).
     - `custom`: `x, y, width, height` relative to the top-left corner of the selected monitor.
   - All coordinates are physical pixels; negative coordinates are supported.
6. `SetWindowLongPtr(GWL_STYLE)`, `SetWindowLongPtr(GWL_EXSTYLE)`, then `SetWindowPos(hwnd, NULL, rect,
   SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOZORDER | SWP_NOOWNERZORDER)`.
7. Check every step for errors. `SetWindowLongPtr` returning 0 with `GetLastError() == ERROR_ACCESS_DENIED` → UIPI
   handling (§4.9); stop, do not retry.

Steps 4–6 are **idempotent**: before applying, compare the current style/ex-style/rect with the desired values; if
they already match, call no API at all. This is the main guard against loops caused by the app's own `SetWindowPos`.

### 4.3 Restoring

- Only restore when `IsWindow(hwnd)` and the PID still matches the saved one (the hwnd may have been reused).
- Write back the original `GWL_STYLE`, `GWL_EXSTYLE` → original `SetWindowPlacement` → `SetWindowPos(...
  SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER)` to redraw the frame.
- After restoring, drop the window from the managed list.
- If the process has exited, just drop the state.

### 4.4 Background automation (event-driven)

**Global hook (one, for the app's lifetime):** `SetWinEventHook(EVENT_SYSTEM_FOREGROUND,
EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS)`.
- On every foreground change: remember the "last foreground window not owned by Edgeless/the shell" (used by
  "Add to list"); resolve the process path → if it matches an enabled profile → schedule an apply after `applyDelayMs`
  (default 500 ms, one-shot timer). Many games reposition their window right after startup.

**Startup scan:** one `EnumWindows` pass to apply to listed games that are already running.

**Per-process hooks** (created when the first window of that PID becomes managed, removed when none of its windows
is managed any more), all `WINEVENT_OUTOFCONTEXT` with `idProcess = pid`:
- `EVENT_OBJECT_LOCATIONCHANGE`: the game resized/moved itself (resolution change, alt-tab).
- `EVENT_SYSTEM_MINIMIZEEND`: the window was restored from minimized.
- `EVENT_OBJECT_DESTROY`: the window was destroyed → clean up its state.
- Only handle events with `idObject == OBJID_WINDOW`, `idChild == CHILDID_SELF` and a managed `hwnd`.

**Re-apply:**
- An event for a managed window → debounce 250 ms (one-shot, restarted by new events) → run §4.2 again (idempotency
  check first).
- **Guard against "fighting" the game**: if the same window really has to be re-applied (not skipped as already
  matching) more than **5 times in 10 seconds** → stop managing it (restore the original state), log Warn and notify
  "Game X keeps resetting its window; Edgeless stopped managing it". Manage it again when the user presses the hotkey
  or the game restarts.
- If the game creates a new window (some games destroy and recreate it on resolution change), the new window is
  picked up by the foreground event.

No repeating timers, no polling. No WMI or process-start watching.

### 4.5 Hotkey

- `RegisterHotKey` on the app's hidden window; default `Ctrl+Alt+B`, changeable in settings.
- Pressing the hotkey on the foreground window (unless excluded):
  - managed → restore it (§4.3) and mark it "disabled by the user" for this hwnd, so automation does not re-apply it
    until the game restarts or the hotkey is pressed again;
  - not managed → apply using the profile for that process path if one exists, otherwise the default profile
    (`current` + `fill`). The hotkey does **not** add the game to the list.
- Registration failure (taken by another app) → notify; settings show a warning next to the hotkey field.
- When globally disabled, the hotkey does nothing.

### 4.6 Global enable/disable

- Disable: restore every managed window, remove per-process hooks. The foreground hook stays but only remembers the
  last window; nothing is applied.
- Enable: scan again as on startup.
- The enabled state is saved in the config.

### 4.7 Tray icon and menu

Tray icon via `Shell_NotifyIcon`. Tooltip: `Edgeless — On (n windows)` / `Edgeless — Off`. Double-click opens
settings. Right-click menu (all UI copy is English):

- ☑ **Enable Edgeless**
- **Add "<exe>" to list** — add the last foreground window's process (§4.4) by its full path; the label shows the file
  name. Disabled when there is none or that path is already listed. Adds it with the default profile and applies
  immediately.
- **Managed windows ▸** — submenu of managed windows (exe + title); clicking one restores that window.
- **Settings…**
- ☑ **Start with Windows**
- **Restart as administrator** — shown only when the app is not elevated.
- **Open log folder**
- **Exit**

### 4.8 Settings window (plain Win32)

One simple, fixed-size, DPI-scaled window:

- Profile list (listbox, one line per profile: `<file name> — <folder>`; deep folders show the root and the last two
  levels, e.g. `D:\…\Elden Ring\Game`; the list scrolls horizontally) + **Add…** / **Remove** buttons. **Add…** opens
  the standard Open File dialog (`GetOpenFileNameW`, filter `*.exe`, must exist); choosing a path that is already
  listed just selects that profile; cancelling changes nothing.
- Details of the selected profile: full path (read-only edit) + **Change…** button (same dialog, replaces the path),
  ☑ Enabled; monitor (combobox: "Current monitor" + each monitor as `\\.\DISPLAYn — 2560×1440`); size mode
  (`fill` / `center` / `custom` + four x, y, w, h fields enabled only for custom); ☑ Add `WS_POPUP`; apply delay (ms).
- General: hotkey (common control `msctls_hotkey32`), start with Windows (Off / Normal / As administrator).
- Buttons **Save**, **Cancel**, **Open config.json**.
- Paths chosen in the dialog and paths in a hand-edited config are resolved to the real file location (subst drives,
  junctions, symlinks) with `CreateFileW` (attributes only) + `GetFinalPathNameByHandleW`, the same way process paths
  are, so aliases still match.
- Save → write the config and apply immediately (re-register the hotkey, re-apply changed profiles to managed
  windows).

### 4.9 Administrator privileges (UIPI)

- The app knows whether it runs elevated (reads its own process token).
- `ERROR_ACCESS_DENIED` when changing a game window → notify **once per game process**: "Game X runs as
  administrator. Run Edgeless as administrator (tray → Restart as administrator)." No crash, no retry.
- "Restart as administrator": relaunch the same exe with the `runas` verb and `--elevated-restart`; the old instance
  restores every window and exits once the new instance has taken the single-instance mutex.

### 4.10 Start with Windows

- **Normal**: value `Edgeless` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` =
  `"<exe path>" --autostart`.
- **As administrator**: Task Scheduler task `Edgeless`, at-logon trigger for the current user, "Run with highest
  privileges", created with `schtasks.exe`. Creating the task needs admin → if the app is not elevated, show UAC for
  this action only.
- Choosing one mode removes the other.
- On every start: if the exe has moved, update the path in the autostart entry.
- `--autostart` → show nothing but the tray icon.

### 4.11 Configuration

`config.json` in the same folder as `Edgeless.exe` (portable: everything lives next to the exe). If that folder is
not writable (e.g. under `Program Files`), show an error dialog asking the user to move the exe, and exit.

```json
{
  "version": 1,
  "enabled": true,
  "hotkey": { "modifiers": ["Ctrl", "Alt"], "key": "B" },
  "autostart": "normal",
  "profiles": [
    {
      "path": "D:\\Games\\Elden Ring\\Game\\eldenring.exe",
      "enabled": true,
      "monitor": "current",
      "sizeMode": "fill",
      "custom": { "x": 0, "y": 0, "width": 1920, "height": 1080 },
      "addPopupStyle": false,
      "applyDelayMs": 500
    }
  ]
}
```

- `autostart`: `off` | `normal` | `elevated`. `monitor`: `current` or a device name.
  `sizeMode`: `fill` | `center` | `custom`.
- `path`: full path of the game exe; profiles with a blank or relative path are dropped on load, duplicates (same
  normalized path) keep the first. There is no `exe` field (no released version used it, so no migration).
- Missing file → create defaults. Missing fields → use defaults.
- Malformed JSON → rename to `config.invalid-<yyyyMMdd-HHmmss>.json`, run with defaults, notify the user.
- Safe writes: write a temp file, then rename (never a half-written file).

### 4.12 App lifecycle

- **Single instance**: named mutex `Local\Edgeless.SingleInstance`. A second instance signals the running one to open
  settings (registered window message to the app's hidden window), then exits.
- **Exit / global disable / log-off / shutdown**: restore every managed window. Log-off/shutdown are handled via
  `WM_QUERYENDSESSION` / `WM_ENDSESSION` on the hidden window.
- **Explorer restart**: handle `TaskbarCreated` to re-create the tray icon.
- A crash cannot restore: accepted, since the game regains its frame when restarted. An unhandled-exception handler
  tries to restore and log before exiting.

### 4.13 DPI and multiple monitors

- `app.manifest`: `PerMonitorV2` (with `PerMonitor` as fallback), plus common controls v6 (for `msctls_hotkey32` and
  modern visuals).
- All coordinates are physical pixels; no DPI conversion for game windows.
- A game that receives `WM_DPICHANGED` (when moved to a monitor with a different DPI) and resizes itself is handled
  naturally by re-apply + guard (§4.4).
- The app's settings window and menus scale with the DPI of the monitor they are on.

### 4.14 Logging and notifications

- `logs\edgeless.log` next to `Edgeless.exe`, rolled at 1 MB, 3 files kept. Each line: timestamp, level
  (Info/Warn/Error), message. Flushed to disk on every line.
- Logged: start/exit, apply/restore (exe, pid, hwnd, old → new rect), API failures with error codes, guard trips,
  config errors, hotkey errors.
- Notifications use the `Shell_NotifyIcon` balloon (shown as a toast on Windows 11). The same message is shown at most
  once every 5 minutes.

## 5. Testing

- **`Edgeless.Core.Tests`** (xUnit): style/ex-style computation, target rect (fill/center/custom, negative
  coordinates, missing monitor), path normalization/matching, exclusion list, guard/debounce (fake clock), config read/write
  (missing fields, malformed JSON).
- **`Edgeless.TestWindow`**: a "fake game" Win32 window with modes: normal; resets its style after N ms; keeps
  resizing (to exercise the guard); destroys and recreates its window; runs as administrator.
- **Manual checklist** (recorded in the README when done): two monitors with the secondary on the left (negative
  coordinates); 100% / 150% DPI on different monitors; a game running as admin; exiting the app while windows are
  managed; log-off; a few real games.

## 6. Acceptance criteria

1. The build is a Native AOT single-file exe that runs on Windows 10 22H2+ / 11 without installing .NET; < 10 MB.
2. Idle: CPU ~0%, RAM (private bytes) < 30 MB.
3. A listed game gets focus → covers exactly the right monitor, no frame, taskbar hidden while the game is in the
   foreground.
4. A game resetting its window → re-applied within ~1 second; a game "fighting" → the guard stops and notifies.
5. The hotkey toggles correctly; restore returns the window to its original position, size and style (including an
   originally maximized window).
6. Exit / global disable / log-off → every window is restored.
7. Game running as admin, app not elevated → notification shown, no crash.
8. Code review: every interop call aimed at foreign windows/processes lives in `Interop/` and is in the CLAUDE.md
   §1.2 whitelist.

## 7. Decisions made by Claude — for review

- .NET 10 + Native AOT + plain Win32 UI (instead of .NET 8 + WinForms).
- `WS_POPUP` **off** by default, can be enabled per profile.
- Defaults: `fill`, `applyDelayMs = 500`, debounce 250 ms, guard 5 times / 10 seconds.
- Save `WINDOWPLACEMENT` instead of only the window rect.
- The hotkey works on any non-excluded window, does not add it to the list, and obeys the global switch.
- Monitors are stored by device name instead of index.
- Autostart has 3 modes; the elevated mode uses Task Scheduler.
- Portable layout (changed on 2026-10-18 at the user's request): `config.json` and `logs\` live next to the exe; a
  non-writable exe folder is a startup error, with no fallback to `%APPDATA%`.
- Profiles match by full exe path, not exe name (changed on 2026-10-18 at the user's request: names can collide).
  Games are added with an Open File dialog (`GetOpenFileNameW`, own window only — allowlisted with the user's approval)
  or from the tray while focused.
- No UWP/Game Pass support, no managing several windows of one process at once.

## 8. Left to the builder

- Class structure inside each folder (folder layout is in CLAUDE.md §3).
- Pixel layout of the settings window, the icon.
- How the message loop / hidden window is organized, as long as everything runs on a single UI thread (out-of-context
  WinEvent callbacks are delivered to the thread that registered the hook, so that thread needs a message loop).
