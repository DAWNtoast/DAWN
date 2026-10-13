param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateSet('Empty','Voyage')][string]$Fixture = 'Empty',
    [switch]$Visible,
    [switch]$Offscreen,
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
$startupExe = [IO.Path]::GetFullPath($Executable)
$startupOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $startupExe.StartsWith('D:\Game\Artifacts\StartupMemory20261011\', [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $startupExe -PathType Leaf)) { throw 'Use this isolated diagnostic executable.' }
if (-not $startupOutput.StartsWith('D:\Game\Artifacts\StartupMemory20261011\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Use the audit output directory.' }
if (Test-Path -LiteralPath $startupOutput) { throw 'Preserve earlier evidence; use a fresh output directory.' }
New-Item -ItemType Directory -Path $startupOutput | Out-Null
$startupReport = Join-Path $startupOutput 'memory.json'
$startupArgs = @('-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-startupMemoryOutput',$startupReport)
if ($Fixture -eq 'Voyage') { $startupArgs += '-startupMemoryVoyage' }
if ($Offscreen) { $startupArgs += '-startupMemoryOffscreen' }
if ($Visible -and $Offscreen) { throw 'Choose visible or hidden/offscreen rendering.' }
if ($Capture -and -not $Visible) { throw 'Visual capture requires a normal visible window.' }
if ($Capture) { $startupArgs += '-startupMemoryCapture' }
function Start-StartupPlayer([string[]]$Arguments, [switch]$Show) {
    $startupQuoted = $Arguments | ForEach-Object { '"' + $_.Replace('"','\"') + '"' }
    $startupWindowStyle = if ($Show) { 'Normal' } else { 'Hidden' }
    Start-Process -FilePath $startupExe -ArgumentList $startupQuoted -WindowStyle $startupWindowStyle -PassThru
}
$startupPrepare = Start-StartupPlayer ($startupArgs + @('-startupMemoryPrepare','-logFile',(Join-Path $startupOutput 'prepare.log')))
if (-not $startupPrepare.WaitForExit(60000)) { Stop-Process -Id $startupPrepare.Id; throw 'Owned preparation process timed out.' }
if ($startupPrepare.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $startupOutput 'fixture-prepared.txt'))) { throw 'Fixture preparation failed.' }
$startupTimer = [Diagnostics.Stopwatch]::StartNew()
$startupStarted = [DateTime]::UtcNow
$startupProcess = Start-StartupPlayer ($startupArgs + @('-logFile',(Join-Path $startupOutput 'player.log'))) -Show:$Visible
[pscustomobject]@{Exe=$startupExe;ProcessId=$startupProcess.Id;Fixture=$Fixture;Arguments=$startupArgs;StartedUtc=$startupStarted.ToString('o');ExeSHA256=(Get-FileHash -LiteralPath $startupExe).Hash} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $startupOutput 'launch.json') -Encoding utf8
Write-Output ('MEASURING ' + $Fixture + ' PID ' + $startupProcess.Id)
$startupSamples = [Collections.Generic.List[object]]::new()
while (-not $startupProcess.HasExited -and $startupTimer.Elapsed.TotalSeconds -lt 65) {
    $startupProcess.Refresh()
    if (-not $startupProcess.HasExited) {
        $startupSamples.Add([pscustomobject]@{ElapsedSeconds=[Math]::Round($startupTimer.Elapsed.TotalSeconds,3);Utc=[DateTime]::UtcNow.ToString('o');PrivateBytes=$startupProcess.PrivateMemorySize64;WorkingSetBytes=$startupProcess.WorkingSet64})
    }
    Start-Sleep -Milliseconds 250
}
if (-not $startupProcess.HasExited) { Stop-Process -Id $startupProcess.Id; throw 'Owned measured process timed out.' }
$startupProcess.WaitForExit()
$startupSamples | Export-Csv -LiteralPath (Join-Path $startupOutput 'process-memory.csv') -NoTypeInformation -Encoding utf8
if ($startupProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $startupReport)) { throw 'Measurement failed.' }
$startupProbe = Get-Content -LiteralPath $startupReport -Raw | ConvertFrom-Json
if ($startupProbe.status -ne 'PASS' -or -not $startupProbe.identityVerified -or $startupProbe.developmentBuild) { throw 'Probe validation failed.' }
$startupInventoryUtc = if ($startupProbe.inventoryUtc -is [DateTime]) {
    [DateTimeOffset]::new($startupProbe.inventoryUtc).ToUniversalTime()
} else {
    [DateTimeOffset]::Parse([string]$startupProbe.inventoryUtc).ToUniversalTime()
}
$startupInterval = @($startupSamples | Where-Object { [DateTimeOffset]::Parse($_.Utc).ToUniversalTime() -lt $startupInventoryUtc })
$startupStable = @($startupInterval | Where-Object { $_.ElapsedSeconds -ge 10 -and $_.ElapsedSeconds -le 22 })
if ($startupStable.Count -lt 35) { throw 'Too few stable samples.' }
function Startup-Median($Values) {
    $startupSorted = @($Values | Sort-Object)
    $startupMid = [int][Math]::Floor($startupSorted.Count / 2)
    if ($startupSorted.Count % 2) { return [double]$startupSorted[$startupMid] }
    return ([double]$startupSorted[$startupMid-1] + [double]$startupSorted[$startupMid]) / 2
}
$startupSummary = [pscustomobject]@{
    Result='PASS';Fixture=$Fixture;Samples=$startupInterval.Count;StableSamples=$startupStable.Count
    PeakPrivateMiB=[Math]::Round(($startupInterval | Measure-Object PrivateBytes -Maximum).Maximum/1MB,3)
    PeakWorkingSetMiB=[Math]::Round(($startupInterval | Measure-Object WorkingSetBytes -Maximum).Maximum/1MB,3)
    StablePrivateMiB=[Math]::Round((Startup-Median $startupStable.PrivateBytes)/1MB,3)
    StableWorkingSetMiB=[Math]::Round((Startup-Median $startupStable.WorkingSetBytes)/1MB,3)
    UnityAllocatedMiB=[Math]::Round($startupProbe.allocatedBytes/1MB,3);UnityReservedMiB=[Math]::Round($startupProbe.reservedBytes/1MB,3)
    ManagedUsedMiB=[Math]::Round($startupProbe.managedUsedBytes/1MB,3);ManagedReservedMiB=[Math]::Round($startupProbe.managedReservedBytes/1MB,3)
    Textures=$startupProbe.textures.Count;GameObjects=$startupProbe.gameObjects;AudioClips=$startupProbe.audioClips;FirstMenuReadySeconds=$startupProbe.firstMenuReadySeconds;RenderedCameraFrames=$startupProbe.renderedCameraFrames;Visible=[bool]$Visible;Offscreen=[bool]$Offscreen
    Method='250ms external OS samples; 10-22s steady interval; peak excludes post-measurement inventories and JSON. Fresh process; file/OS caches are warm, not reboot-cold. Private commit and resident working set are separate; no dedicated VRAM measurement. Offscreen=true includes one common diagnostic output RT/canvas mode, not visible-window RAM.'
}
$startupSummary | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $startupOutput 'summary.json') -Encoding utf8
$startupSummary | ConvertTo-Json
