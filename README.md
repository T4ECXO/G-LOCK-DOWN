# G-LOCK-DOWN

G-LOCK-DOWN is an early Windows usage-limiter implementation based on
`work description.txt`. The current slice provides:

- a three-hour Valorant allowance;
- a four-hour shared Valorant/Roblox/Steam allowance;
- overlap-safe accounting (shared time advances only once);
- Bangkok-midnight resets and atomic JSON persistence;
- detection of Valorant, Roblox Player, the foreground Steam client, and games
  inside discovered Steam libraries;
- configurable process exclusions for non-game Steam apps such as Wallpaper Engine;
- 15, 5, and 1 minute console warnings;
- process termination and relaunch blocking while a limit is exhausted;
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
