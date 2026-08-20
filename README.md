# ScreenSwitch

Moves both monitors between your work computer and your personal one with a single keypress — no
reaching behind the desk, no cycling through the monitor's OSD buttons.

It lives as a small icon in the system tray, next to the clock, and listens for a global
`Ctrl+Alt+S`.

## How it works

Modern monitors support **DDC/CI**, a control protocol that rides on the video cable itself and
accepts exactly the same commands the physical OSD buttons issue. The relevant one is VCP feature
`0x60`, "Input Select"; writing to it changes the active input.

The practical consequence: **the computer you are sitting at is the one that pushes the monitors
over to the other machine.** That is why the app is installed on both — each configured to send the
displays to the *other* one:

| Machine | Connected via | Target configured on it | Pressing the hotkey there |
|---|---|---|---|
| Work PC | HDMI | `DisplayPort1` | Monitors jump to the personal laptop |
| Personal laptop | DisplayPort | `HDMI1` (or `HDMI2`) | Monitors jump to the work PC |

## Installation

### 1. Get the executable

The quick way: on GitHub, open the **Actions** tab, pick the most recent `build` run and download
the `ScreenSwitch-win-x64` artifact. Inside is a single `ScreenSwitch.exe` — self-contained, no
.NET install required, no administrator rights required. Put it somewhere permanent, e.g.
`C:\Tools\ScreenSwitch\`.

> Windows will almost certainly show a **SmartScreen** warning the first time ("Windows protected
> your PC"). That is a reputation prompt, not a virus detection: the file is unsigned and brand
> new. Click **More info → Run anyway**, or right-click the downloaded ZIP → Properties → tick
> **Unblock** before extracting, which avoids the prompt entirely.

Alternatively, build it yourself:

```powershell
dotnet publish src/ScreenSwitch/ScreenSwitch.csproj -c Release -r win-x64 -o publish
```

### 2. First run — on **each** of the two machines

1. Make sure DDC/CI is enabled on both monitors: OSD menu → **System Setup** → **DDC/CI** → **On**.
   (Usually on by default.)
2. Run `ScreenSwitch.exe`. A blue icon with arrows appears in the tray.
   (The tray interface itself is in Hebrew; menu items are given below in English with the
   Hebrew label in brackets.)
3. Right-click the icon → **Monitor details…** (פרטי מסכים…) to see which monitors were detected, which input each
   one is on, and which inputs they support. This is also how you find out whether the work PC is on
   `HDMI1` or `HDMI2`.
4. Right-click → **Switch to** (עבור אל) → pick the input belonging to the **other** computer. The first
   choice is saved automatically as the standing target, and from then on `Ctrl+Alt+S` works.
5. You will then be asked whether to **start with Windows**. Say yes and the app comes back on its
   own after a reboot — no need to launch the .exe again.

Repeat on the second machine, this time targeting the first machine's input.

### Auto-start

Answering yes to the first-run prompt is all it takes. To change your mind later, right-click the
tray icon and toggle **Start with Windows** (הפעל עם Windows), or from a script:

```powershell
ScreenSwitch.exe --autostart on
ScreenSwitch.exe --autostart off
ScreenSwitch.exe --autostart        # report the current state
```

This writes a single value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. It is
per-user, so it never needs administrator rights.

## Daily use

- **`Ctrl+Alt+S`** — send both monitors to the other computer.
- **Double-click the tray icon** — the same thing.
- **Right-click** — the full menu: a one-off switch to a different input, changing the standing
  target, re-detecting monitors.

## Gaming: not switching mid-match

A global hotkey is a global hotkey, and a stray `Ctrl+Alt+S` during a teamfight would be a bad time
to lose both displays. So while a game is in the foreground, **the first press is swallowed** and a
tray balloon says so. Press again within 1.5 seconds and the switch goes through — that second press
is how you say you meant it.

A game counts as "in the foreground" when either:

- Windows reports an exclusive full-screen Direct3D app, or the foreground window covers an entire
  monitor (this catches borderless windowed, which is how most games actually run); or
- the foreground process is named in `blockedProcesses`, which is what covers a game played in a
  regular window. `League of Legends` is listed by default.

The guard only applies to the hotkey. The tray menu, a double-click on the icon and the command line
all switch unconditionally — reaching any of them means you already left the game. Alt-tabbing out
or minimising likewise drops the guard, so the hotkey behaves normally the moment you are back on
the desktop.

To turn the whole thing off, set `"blockWhileGaming": false`.

One thing worth knowing: while ScreenSwitch is running, Windows delivers `Ctrl+Alt+S` to it and to
nothing else — the combination is invisible to every other application, games included. If you ever
want that chord inside a game, change `hotkey` to something a game will never use, such as
`Ctrl+Alt+F12` or `Ctrl+Shift+Pause`.

## Command line

Useful for diagnostics, and for wiring the switch to a Stream Deck or anything else that can run a
file:

```powershell
ScreenSwitch.exe --list          # what is connected, current input, supported inputs
ScreenSwitch.exe --switch        # switch to the target from the config file
ScreenSwitch.exe --to HDMI2      # switch to a specific input
ScreenSwitch.exe --autostart on  # start with Windows
```

## Configuration

Stored at `%APPDATA%\ScreenSwitch\config.json`, openable straight from the tray menu. Every field is
optional:

```jsonc
{
  "targetInput": "DisplayPort1",   // where the monitors go from this machine
  "hotkey": "Ctrl+Alt+S",          // any Ctrl/Alt/Shift/Win combination + letter, digit or F1-F24
  "delayBetweenMonitorsMs": 150,   // pause between one monitor and the next
  "showNotifications": true,       // tray balloons
  "retryCount": 1,                 // retries for a monitor that did not respond
  "blockWhileGaming": true,        // swallow the first hotkey press while a game is in front
  "blockedProcesses": ["League of Legends"],  // always treated as a game, even windowed
  "overrideWindowMs": 1500,        // how long a second press counts as "I meant it"; 0 disables it
  "monitorTargets": {              // a different target for one specific monitor
    "\\\\?\\DISPLAY#ACI27E7#5&...": "HDMI2"
  }
}
```

`targetInput` accepts names (`HDMI1`, `HDMI 2`, `DisplayPort1`), aliases (`DP`, `HDMI`) or a raw
value (`0x11`). `monitorTargets` is only needed when the two monitors are wired to different ports —
say one on HDMI 1 and the other on HDMI 2. Names in `blockedProcesses` are matched
case-insensitively, with or without the `.exe`.

## Troubleshooting

**"The monitor did not respond to the DDC/CI command"** — nearly always DDC/CI switched off in the
OSD. Check **System Setup → DDC/CI → On** on each monitor separately.

**One monitor switches, the other does not** — run `--list` and check that both are detected. If the
second one does not appear at all, try another cable or port; if it appears but fails, raise
`retryCount` to 2 and `delayBetweenMonitorsMs` to 300.

**It switches, but to the wrong port** — the two monitors are on different ports. Use
`monitorTargets`.

**The hotkey does nothing** — either another application already owns the combination (a balloon
says so at startup; change `hotkey` in the config and restart), or a game is in the foreground and
the guard swallowed the press (press again within 1.5 seconds, or check the balloon).

**Connected through a dock or USB-C** — DDC usually passes through docks, but not always. If the
monitors are not detected through the dock, connect DisplayPort straight to the laptop.

**The monitors switched and now I cannot switch back** — press `Ctrl+Alt+S` on the other computer.
If something has gone badly wrong, the monitor's own buttons always still work.

## Project layout

| Path | Role |
|---|---|
| `src/ScreenSwitch.Core/` | Platform-neutral logic: capabilities parsing, config, hotkeys, the gaming guard |
| `src/ScreenSwitch/` | The tray app: WinForms, P/Invoke to `dxva2.dll`, command line |
| `tests/ScreenSwitch.Tests/` | Unit tests over `ScreenSwitch.Core` |
| `tools/make_icon.py` | Regenerates `Resources/app.ico` |
