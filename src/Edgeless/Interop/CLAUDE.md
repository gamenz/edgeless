# Interop — anti-cheat boundary

Every Win32 call that takes a **foreign** HWND, PID or process handle lives in this folder, and nowhere else.

Before adding or changing anything here:
1. Re-read root `CLAUDE.md` §1 (forbidden list + whitelist). Those rules win over any convenience.
2. The API must already be in `antiCheat` of `scripts/gate.rules.json`. If it is not, **stop and ask the user**
   (explain why it is needed and its anti-cheat risk). Never add it yourself.
3. `OpenProcess` only in `ForeignProcess.cs`, only with `PROCESS_QUERY_LIMITED_INFORMATION`.
4. `SetWindowLongPtr` only with `GWL_STYLE` / `GWL_EXSTYLE`. `SetWinEventHook` only with `WINEVENT_OUTOFCONTEXT`.
5. On `ERROR_ACCESS_DENIED` or any failure: return the error, never retry with other rights or workarounds.
6. Run the gate (`scripts/gate.ps1`) and `InteropAuditTests` after every change.
