# G-LOCK-DOWN

G-LOCK-DOWN is an early Windows usage-limiter implementation based on
`work description.txt`. The current slice provides:

- a three-hour Valorant allowance;
- a four-hour shared Valorant/Roblox/Minecraft/Steam allowance;
- overlap-safe accounting (shared time advances only once);
- Bangkok-midnight resets and atomic JSON persistence;
- detection of Valorant, Roblox Player, Minecraft Bedrock, Minecraft Java using
  the official launcher runtime, the foreground Steam client, and games inside
  discovered Steam libraries;
- configurable process exclusions for non-game Steam apps such as Wallpaper Engine;
- 15, 5, and 1 minute console warnings;
- process termination and relaunch blocking while a limit is exhausted;
- timed Full Lock-Down for all tracked games and configured restricted apps;
- fail-closed behavior if the usage record is unreadable;
- a native Windows service host with automatic-restart configuration;
- a tray dashboard and desktop warning notifications;
- administrator install/uninstall scripts and protected file/service ACLs.

## Run it

Requires the .NET 10 SDK on Windows.

```powershell
dotnet build GLockDown.slnx
dotnet run --project tests/GLockDown.Tests
dotnet run --project src/GLockDown.Worker -- --dry-run
```

Dry-run mode records usage and reports intended blocks without closing apps.
Remove `--dry-run` to enforce limits. The worker creates `data/settings.json` on
first launch. Stop the development worker with Ctrl+C.

For a single safe process scan:

```powershell
dotnet run --project src/GLockDown.Worker -- --dry-run --once
```

## Install as a Windows service

### Full Lock-Down command

Block Valorant, Roblox Player, Minecraft, Steam (including the background client),
detected Steam games, and configured additional shared apps immediately for a specified
number of minutes. For example, with a development worker already running:

```powershell
dotnet run --project src/GLockDown.Worker -- --full-lock-down 60
```

For the installed service, run this in an administrator PowerShell after updating
the service to this version:

```powershell
& "$env:ProgramFiles\G-Lock-Down\GLockDown.Worker.exe" --data-dir "$env:ProgramData\G-Lock-Down" --full-lock-down 60
```

The command saves the deadline and exits; the worker must be running with the
same data directory. Enforcement begins on its next scan (normally within one
second). The dashboard displays the remaining lock-down time. Durations are
whole minutes from 1 to 525600. Repeating the command can extend an existing
lock-down but cannot shorten it. The deadline survives worker/PC restarts and
daily resets; sleep and powered-off time count toward this duration. After expiry,
normal daily limits apply without granting extra allowance. The deadline uses
the computer's UTC clock, so administrator clock changes can affect expiry.

Existing process exclusions still apply. This command does not block websites or
untracked applications. A worker in dry-run mode only reports intended blocks;
the command itself cannot be combined with `--dry-run`, `--once`, or `--service`.

Minecraft Bedrock is recognized by the `Minecraft.Windows` process name. Java
Edition is recognized when `javaw.exe` or `java.exe` runs from a `.minecraft\runtime`
or `Minecraft Launcher\runtime` directory. For a custom launcher, add its Java
executable's absolute path to `MinecraftJavaRuntimePaths` in `settings.json`.
Minecraft uses the shared allowance and is blocked when it is exhausted or Full
Lock-Down is active. Restart the worker after changing settings.

### Standalone Time Left dashboard

Double-click `artifacts/G-LOCK-DOWN-Time-Left-v5/GLockDown.Tray.exe` to open
the redesigned Windows dashboard without a terminal or a separate .NET install.
It reads the installed service's public status and refreshes every second.
The dark dashboard includes usage bars, both remaining allowances, a reset
countdown, activity/status details, and a **Keep on top** toggle. Valorant's
playable time is the smaller of its own allowance and the shared allowance.
Older services use an estimated Bangkok-midnight reset schedule. Closing the
window hides it in the system tray; **Exit tray** closes only the dashboard.

### Service installation

Installation immediately enables process-closing enforcement. First validate
detection in dry-run mode. Then open PowerShell **as Administrator** and run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\Install.ps1
```

The service runs as LocalSystem, starts automatically, and is restarted after
unexpected failures. Its binaries and private usage data are writable only by
SYSTEM and administrators. The tray dashboard starts at the next sign-in.

To remove it from an administrator PowerShell:

```powershell
.\scripts\Uninstall.ps1
```

Pass `-KeepUsageData` to preserve settings and usage history.

## Not hardened yet

The service and local files can now be protected from a standard Windows user,
but this is not yet a complete tamper-resistant parental-control release. It
does not include a browser extension, trusted online time, signed binaries or
updates, or a guided administrator recovery tool. Valorant termination with
Vanguard and the installer ACLs still require real-machine validation before
this should be relied upon. Roblox website enforcement depends on the browser
and managed-extension deployment choice.
