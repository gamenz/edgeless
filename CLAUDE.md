# Edgeless

Background Windows tray app that forces running games from **Windowed** into **Borderless Windowed**
(similar to "Borderless Gaming"). It only manipulates game windows from the outside via Win32 window APIs.

---

## 1. Anti-cheat safety rules (HARD RULES — never violate)

The #1 product requirement: **using Edgeless must never get a user's game account banned.**
Edgeless must behave exactly like a window manager (the same thing Windows Explorer, DisplayFusion,
PowerToys FancyZones do) — it touches *windows*, never *processes*, *memory* or *rendering*.

### 1.1 Forbidden — never write code that does any of these
- **Any code injection**: DLL injection, `LoadLibrary` in a foreign process, `CreateRemoteThread`,
  `NtCreateThreadEx`, `QueueUserAPC`, `SetThreadContext`, manual mapping, AppInit_DLLs, IFEO, shims.
- **`SetWindowsHookEx` of any kind** (global hooks load our DLL into other processes; low-level
  keyboard/mouse hooks look like input monitoring). Use `RegisterHotKey` for hotkeys.
- **`SetWinEventHook` with `WINEVENT_INCONTEXT`**. Only `WINEVENT_OUTOFCONTEXT` is allowed.
- **Process memory access**: `ReadProcessMemory`, `WriteProcessMemory`, `VirtualAllocEx`,
  `VirtualProtectEx`, `NtReadVirtualMemory`, etc.
- **Opening game processes with any right other than `PROCESS_QUERY_LIMITED_INFORMATION`.**
  No `PROCESS_VM_READ`, `PROCESS_ALL_ACCESS`, `PROCESS_DUP_HANDLE`, `SYNCHRONIZE`-for-wait tricks, etc.
  - In .NET: **never touch `Process.MainModule`, `Process.Modules`, `Process.Handle`,
    `Process.WaitForExit()`, `Process.Kill()`** on a game process (they open handles with extra rights).
    Get the exe name only via `QueryFullProcessImageName` with a limited-information handle.
  - No `EnumProcessModules`, `Module32First/Next`, `DuplicateHandle`, `NtQueryInformationProcess`
    on game processes.
- **Debugging APIs**: `DebugActiveProcess`, `OpenThread`, `SuspendThread`, etc.
- **Hooking/touching graphics APIs**: no DirectX/DXGI/OpenGL/Vulkan hooks, no swapchain access,
  no reading or capturing game frames (no `BitBlt`/`PrintWindow`/Desktop Duplication on the game).
- **Subclassing or changing window procedures**: never `SetWindowLongPtr(GWLP_WNDPROC / GWLP_USERDATA /
  GWLP_HINSTANCE / GWLP_ID)`. `SetWindowLongPtr` is allowed **only** with `GWL_STYLE` and `GWL_EXSTYLE`.
- **Sending input or custom messages to game windows**: no `SendInput`, `keybd_event`, `mouse_event`,
  no `SendMessage`/`PostMessage` of `WM_KEY*`, `WM_MOUSE*`, `WM_SYSCOMMAND`, `WM_ACTIVATE`, or any custom message.
- **Overlay windows** drawn on top of the game, transparent click-through windows over the game.
- **Modifying game files**, game configs, registry keys of games, or the game's install folder.
- **Kernel drivers**, services, or anything that runs outside the user's own session.
- **Bypassing protection**: if a game/anti-cheat denies access (`ERROR_ACCESS_DENIED`, protected
  process, handle open fails), **give up gracefully, log it, notify the user**. Never retry with
  higher rights, token tricks, elevation exploits, or any workaround.

### 1.2 Allowed Win32 surface toward foreign (game) windows/processes — whitelist
Only these may be called with a game's HWND / PID:

| Purpose | APIs |
|---|---|
| Find windows | `EnumWindows`, `GetWindowThreadProcessId`, `IsWindow`, `IsWindowVisible`, `IsIconic`, `IsZoomed`, `GetWindow`, `GetAncestor`, `GetClassName`, `GetWindowTextLength`/`GetWindowText`, `GetForegroundWindow` |
| Read geometry/state | `GetWindowLongPtr(GWL_STYLE / GWL_EXSTYLE)`, `GetWindowRect`, `GetClientRect`, `GetWindowPlacement`, `DwmGetWindowAttribute` (read-only, e.g. extended frame bounds) |
| Change window | `SetWindowLongPtr(GWL_STYLE / GWL_EXSTYLE)`, `SetWindowPos`, `ShowWindow`, `SetWindowPlacement` |
| Monitors | `MonitorFromWindow`, `MonitorFromRect`, `GetMonitorInfo`, `EnumDisplayMonitors`, `GetDpiForWindow` |
| Events | `SetWinEventHook` (**`WINEVENT_OUTOFCONTEXT` only**), `UnhookWinEvent` |
| Process name | `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)`, `QueryFullProcessImageName`, `CloseHandle` |

Our own windows / own process may use any API normally (tray icon, settings window, `RegisterHotKey`, etc.).

### 1.3 Process for changing the whitelist
- **Adding any new Win32/NT API that takes a foreign HWND, PID, or process/thread handle requires
  explicit user approval first.** Explain why it is needed and its anti-cheat risk.
- Every P/Invoke declaration that targets foreign windows/processes lives in **one place**
  (`src/Edgeless/Interop/`) so it can be audited at a glance.
- Code review must check every new interop call against this section.
- The machine-checked copy of these lists is `antiCheat` in `scripts/gate.rules.json` (used by the gate and by
  `InteropAuditTests`). Editing it = changing the whitelist = needs user approval.

### 1.4 Honest limits (document for users, do not hide)
- Window-level manipulation is the same as what Windows itself does, and is widely considered safe,
  but **no tool can guarantee zero ban risk**: some games' ToS forbid *any* third-party software.
- Never claim "100% safe / undetectable" in UI, README, or release notes.
- Never add "stealth" features (hiding the process, renaming the exe to evade detection, etc.).

---

## 2. Tech decisions (agreed)
- **.NET 10 (LTS)**, C#, **Native AOT** single-file `.exe`, no installer, no runtime required.
- **No WinForms/WPF**: tray icon (`Shell_NotifyIcon`) and the small settings window are raw Win32
  (keeps AOT working, exe ~ a few MB, near-zero idle CPU).
- P/Invoke via **CsWin32** source generator (`NativeMethods.txt`), AOT-safe. No reflection-based interop.
- JSON via **System.Text.Json source generation** (AOT-safe). Config: `%APPDATA%\Edgeless\config.json`.
- Logs: `%LOCALAPPDATA%\Edgeless\logs\`, size-capped rolling files.
- DPI: **Per-Monitor V2** declared in `app.manifest`; all geometry in physical pixels.
- Event-driven only (WinEvent hooks + hotkey + message loop). **No polling loops/timers while idle**
  (a one-shot timer for debounce/delay is fine).
- Autostart: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (default, no admin);
  optional "start elevated" via Task Scheduler (highest privileges) for games running as admin.
- Default mode: stretch to fill the target monitor; per-game option "keep size, center".
- Out of scope: fullscreen-exclusive-only games, scaling/upscaling, UWP/Game Pass (`ApplicationFrameHost`) windows.

## 3. Layout
Organized by responsibility, one concept per file, ≤ 12 files per folder. Names are unambiguous on purpose:
"monitor" always means a display monitor; event watching lives in `Events/`.
```
edgeless/
├─ CLAUDE.md                       # always-loaded rules (keep ≤ 200 lines)
├─ README.md
├─ .editorconfig / .gitattributes / .gitignore
├─ Directory.Build.props / Directory.Packages.props / Edgeless.slnx
├─ scripts/gate.ps1, gate.rules.json   # project gate (§5)
├─ docs/
│  ├─ specs/                       # YYYY-MM-DD-<topic>-spec.md (committed)
│  └─ plans/                       # YYYY-MM-DD-<topic>-plan.md (git-ignored, local only)
├─ src/
│  ├─ Edgeless.Core/               # pure logic, NO P/Invoke, fully unit-testable
│  │  ├─ Styles/                   # style/ex-style calculation
│  │  ├─ Geometry/                 # rects, display-monitor selection, target rect
│  │  ├─ Rules/                    # exe matching, exclusions, window selection, re-apply guard, throttle
│  │  ├─ Config/                   # model, JSON source-gen, serializer, hotkey binding, ConfigStore
│  │  └─ Logging/                  # ILog, rolling FileLogger
│  └─ Edgeless/                    # the exe (Native AOT)
│     ├─ Interop/                  # ALL foreign-window Win32 calls (§1.2) + nested CLAUDE.md
│     ├─ Windowing/                # finder, registry, BorderlessApplier (apply/restore)
│     ├─ Events/                   # WinEventWatcher, HotkeyManager
│     ├─ App/                      # EdgelessController (partial files), AppHost composition root
│     ├─ Shell/                    # message window, tray icon/menu (+ pure TrayMenuModel), notifier
│     │  └─ Settings/              # settings window: window/layout/profile editor/controls + pure SettingsDraft
│     ├─ Platform/                 # paths, command line, single instance, elevation, autostart
│     ├─ Resources/                # app.ico
│     ├─ NativeMethods.txt / NativeMethods.json / app.manifest
│     └─ Program.cs                # tiny: parse args, hand off to AppHost
└─ tests/                          # test folders mirror src folders 1:1
   ├─ Edgeless.Core.Tests/         # xUnit unit tests + Audit/InteropAuditTests
   ├─ Edgeless.IntegrationTests/   # real-window tests against TestWindow (+ Support/ helpers)
   └─ Edgeless.TestWindow/         # fake "game" window (WinForms, transparent unless --visible, never shipped)
```

### Build and test
- Toolchain: .NET 10 SDK (pinned in `global.json`, tests use Microsoft.Testing.Platform) + VS Build Tools C++
  workload (Native AOT linker). If publish says `vswhere.exe` is not recognized, add
  `C:\Program Files (x86)\Microsoft Visual Studio\Installer` to `PATH`.
- `dotnet test` — all tests; integration tests open real (transparent) windows, so they need a desktop session.
- `dotnet publish src/Edgeless -c Release` — Native AOT, win-x64, x64 only (CsWin32 needs it for `*WindowLongPtr`).
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/gate.ps1` — the gate (§5).

## 4. Conventions
- Identifiers and code comments in English; replies to the user in Vietnamese.
- LF line endings only.
- Complex work: spec in `docs/specs/` → user feedback → plan in `docs/plans/` → user approval → code.
- **Plans are always written with the `superpowers:writing-plans` skill** (invoke it, do not hand-roll a plan).
- `docs/plans/` is git-ignored. **Never link to a plan** from any committed file (code comments, README,
  CLAUDE.md, specs, commit messages, PR bodies). Links may only point to specs in `docs/specs/`.
  A plan may link to its spec, not the other way around.

## 5. File size rules and the gate
Small, single-purpose files keep every file readable in one pass by an AI model and reviewable at a glance.

| Files | Soft limit (warn) | Hard limit (fail) | Other |
|---|---|---|---|
| `src/**/*.cs` | 250 lines | 350 lines | lines ≤ 140 chars |
| `tests/**/*.cs` | 350 | 500 | lines ≤ 140 chars |
| root `CLAUDE.md` | 160 | 200 | |
| nested `**/CLAUDE.md` | 40 | 60 | |
| `docs/specs/*.md` | 400 | 500 | |
| `docs/plans/*.md` | 550 | 650 | |
| other `*.md` | 300 | 400 | |
| `scripts/**` | 300 | 400 | lines ≤ 140 chars |
| project files (`csproj/props/json/manifest/slnx`) | 120 | 200 | |
| folders under `src/`, `tests/` | | 12 files | |

All text files: UTF-8 **without BOM**, **LF only**. Generated code (`obj/`, `bin/`) is excluded.

- Run the gate: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/gate.ps1` (exit 0 = pass).
- It also checks: no links to plans outside `docs/plans/`, and the anti-cheat audit (§1).
- **Run it before saying a task is done and before every commit.** A failure blocks the commit.
- Over the soft limit: plan a split. Over the hard limit: split by responsibility (C# `partial` files named
  `Type.Concern.cs` are fine), never by cramming lines.
- A per-file exception goes in `exceptions` of `scripts/gate.rules.json` with a `reason`, and only with user approval.
