#Requires -RunAsAdministrator
[CmdletBinding()]
param(
    [switch]$KeepUsageData,
    [string]$InstallRoot = (Join-Path $env:ProgramFiles 'G-Lock-Down'),
    [string]$DataRoot = (Join-Path $env:ProgramData 'G-Lock-Down')
)

$ErrorActionPreference = 'Stop'
$serviceName = 'GLockDown'
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -ne $service) {
    if ($service.Status -ne 'Stopped') {
        Stop-Service -Name $serviceName -Force
        $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(20))
    }
    & sc.exe delete $serviceName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not remove the Windows service.' }
}

$runKey = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run'
Remove-ItemProperty -Path $runKey -Name 'GLockDownTray' -ErrorAction SilentlyContinue
Get-Process -Name 'GLockDown.Tray' -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path -LiteralPath $InstallRoot) {
    Remove-Item -LiteralPath $InstallRoot -Recurse -Force
}
if (!$KeepUsageData -and (Test-Path -LiteralPath $DataRoot)) {
    Remove-Item -LiteralPath $DataRoot -Recurse -Force
}

Write-Host 'G-LOCK-DOWN was removed.' -ForegroundColor Green
if ($KeepUsageData) {
    Write-Host "Usage data was kept at $DataRoot"
}
