#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [string]$InstallRoot = (Join-Path $env:ProgramFiles 'G-Lock-Down'),
    [string]$DataRoot = (Join-Path $env:ProgramData 'G-Lock-Down')
)

$ErrorActionPreference = 'Stop'
$serviceName = 'GLockDown'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repositoryRoot 'artifacts'
$workerArtifact = Join-Path $artifactRoot 'worker'
$trayArtifact = Join-Path $artifactRoot 'tray'

Write-Host 'Publishing G-LOCK-DOWN...'
dotnet publish (Join-Path $repositoryRoot 'src\GLockDown.Worker\GLockDown.Worker.csproj') -c Release -o $workerArtifact
if ($LASTEXITCODE -ne 0) { throw 'Worker publish failed.' }
dotnet publish (Join-Path $repositoryRoot 'src\GLockDown.Tray\GLockDown.Tray.csproj') -c Release -o $trayArtifact
if ($LASTEXITCODE -ne 0) { throw 'Tray publish failed.' }

$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -ne $existingService -and $existingService.Status -ne 'Stopped') {
    Stop-Service -Name $serviceName -Force
    $existingService.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(20))
}

New-Item -ItemType Directory -Path $InstallRoot -Force | Out-Null
New-Item -ItemType Directory -Path $DataRoot -Force | Out-Null
$publicData = Join-Path $DataRoot 'Public'
New-Item -ItemType Directory -Path $publicData -Force | Out-Null
Copy-Item -Path (Join-Path $workerArtifact '*') -Destination $InstallRoot -Recurse -Force
$trayInstall = Join-Path $InstallRoot 'Tray'
New-Item -ItemType Directory -Path $trayInstall -Force | Out-Null
Copy-Item -Path (Join-Path $trayArtifact '*') -Destination $trayInstall -Recurse -Force

# Program binaries can only be changed by SYSTEM or administrators.
& icacls.exe $InstallRoot '/inheritance:r' | Out-Null
& icacls.exe $InstallRoot '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)RX' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not protect the installation directory.' }

# Usage/config files are private. Standard users can traverse the root and read
# only the separately protected Public status folder used by the tray app.
& icacls.exe $DataRoot '/inheritance:r' | Out-Null
& icacls.exe $DataRoot '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-32-545:RX' | Out-Null
& icacls.exe $publicData '/inheritance:r' | Out-Null
& icacls.exe $publicData '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-32-545:(OI)(CI)R' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not protect the data directory.' }

$workerExecutable = Join-Path $InstallRoot 'GLockDown.Worker.exe'
$serviceCommand = '"{0}" --service --data-dir "{1}"' -f $workerExecutable, $DataRoot
if ($null -eq $existingService) {
    New-Service -Name $serviceName -BinaryPathName $serviceCommand -DisplayName 'G-LOCK-DOWN Protection' -StartupType Automatic
} else {
    & sc.exe config $serviceName binPath= $serviceCommand start= auto | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not update the Windows service.' }
}

# Restart the monitor if it fails, while keeping stop/configuration rights away
# from standard authenticated users.
& sc.exe failure $serviceName reset= 86400 actions= 'restart/5000/restart/15000/restart/60000' | Out-Null
& sc.exe failureflag $serviceName 1 | Out-Null
$serviceSddl = 'D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWLOCRRC;;;AU)'
& sc.exe sdset $serviceName $serviceSddl | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not protect the Windows service.' }

$trayExecutable = Join-Path $trayInstall 'GLockDown.Tray.exe'
$runKey = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run'
New-ItemProperty -Path $runKey -Name 'GLockDownTray' -PropertyType String -Value ('"{0}"' -f $trayExecutable) -Force | Out-Null

Start-Service -Name $serviceName
Write-Host 'G-LOCK-DOWN is installed and enforcement is active.' -ForegroundColor Green
Write-Host 'The tray dashboard will start automatically at the next sign-in.'
