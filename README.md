# Edgeless

A background system-tray app that turns games running in **Windowed** mode into **Borderless Windowed**
(full screen, no frame), similar to "Borderless Gaming". A single `.exe`, no .NET installation needed.

Detailed design: [docs/specs/2026-10-18-edgeless-spec.md](docs/specs/2026-10-18-edgeless-spec.md).

## Anti-cheat safety

Edgeless only manipulates **windows** from the outside, like a window manager (frame styles, position, size):

- no DLL injection, no DirectX/OpenGL/Vulkan hooks, no reading or writing game memory, no keyboard/mouse input sent
  to games;
- game processes are opened only with `PROCESS_QUERY_LIMITED_INFORMATION`, to read the exe path;
- if Windows or an anti-cheat denies access, Edgeless stops and tells you; it never tries to work around it.

The list of allowed APIs is checked automatically (`scripts/gate.ps1`, `InteropAuditTests`). Even so, **no tool can
guarantee you will never be banned**: some games forbid any third-party software in their terms of service. Read
your game's terms before using it.

## Usage

1. Run `Edgeless.exe`; an icon appears in the system tray (Windows 11 may hide it behind the **^** arrow). There is
   no main window; running `Edgeless.exe` again opens the settings window.
2. Add a game, either way:
   - Start the game in Windowed mode, focus it, then right-click the tray icon → **Add "<game>.exe" to list**
     (the most reliable way: it records the exe that is actually running).
   - **Settings…** → **Add…** → pick the game's exe.

   Games are identified by the **full path** of their exe, so two games with the same exe name in different folders
   never get mixed up. Paths through subst drives, junctions or symlinks are resolved to the real location. Note: if
   you pick a launcher but the game itself runs as a different exe, the profile will not match; add the game from the
   tray while it is running instead. If a game moves to another folder, use **Change…**.
   From then on, Edgeless applies borderless every time the game gets focus.
3. **Hotkey** `Ctrl+Alt+B` (configurable): toggles borderless on the focused window, even for games not in the list.
4. **Settings…** (or double-click the icon): per game, choose the monitor, the size mode `fill` (cover the monitor) /
   `center` (keep size, centered — good for games rendering below the monitor resolution) / `custom`, `WS_POPUP`,
   and the apply delay.
5. Game running as administrator → tray → **Restart as administrator**.
6. Exiting the app or disabling Edgeless restores every window to its original state.

| Data | Location |
|---|---|
| Configuration | `config.json` next to `Edgeless.exe` (a malformed file is backed up and replaced by defaults) |
| Logs | `logs\edgeless.log` next to `Edgeless.exe` (rolled at 1 MB, 3 files kept) |

Edgeless is portable: copy the folder and the configuration comes along. Keep the exe in a writable folder (not under
`Program Files`); otherwise the app shows an error and exits.

## Performance (measured on the published build, 2026-10-18)

| Criterion | Target | Measured |
|---|---|---|
| Exe size (Native AOT) | < 10 MB | 3.3 MB |
| Idle CPU | ~0% | 0 ms over 60 seconds |
| Idle private memory | < 30 MB | 5.6 MB (working set 17.3 MB) |

## Build

Requires the .NET 10 SDK and Visual Studio Build Tools with the "Desktop development with C++" workload (for Native
AOT).

```bash
dotnet test
```

```bash
dotnet publish src/Edgeless -c Release
```

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/gate.ps1
```

The exe is written to `src/Edgeless/bin/Release/net10.0-windows/win-x64/publish/Edgeless.exe`. Integration tests
(`tests/Edgeless.IntegrationTests`) open real (transparent) windows, so they need a signed-in desktop session.

## Manual test checklist

Automated tests cover: style/rect computation, configuration, the guard, apply/restore on real windows, re-apply when
a game resets itself, minimize, hotkey, destroyed windows, path resolution through junctions, autostart (on a test
registry key). The items below need a manual check:

- [ ] Two monitors, the secondary on the left (negative coordinates): the game covers the selected monitor.
- [ ] Different DPI per monitor (100% / 150%): correct position, the settings window renders sharply.
- [ ] Game running as administrator, Edgeless not elevated: a notification, no crash; "Restart as administrator" works.
- [ ] "Start with Windows" (Normal and As administrator), sign out and back in: the app shows only in the tray.
- [ ] Change the hotkey in settings; choose one used by another app → a warning is shown.
- [ ] Hand-edit `config.json` into invalid JSON → restart → a backup file and a notification.
- [ ] Restart Explorer → the tray icon comes back.
- [ ] Sign out while a window is borderless → the window is restored.
- [ ] A few real games.

To see the fake game window during manual tests: `Edgeless.TestWindow.exe --visible --mode reset-once`.
