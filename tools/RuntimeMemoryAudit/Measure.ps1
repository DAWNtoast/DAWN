param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateRange(60,1800)][int]$TimeoutSeconds = 660
)
$ErrorActionPreference = 'Stop'
$auditRoot = [IO.Path]::GetFullPath('D:\Game\Artifacts\RuntimeMemoryOptimization20261014') + '\'
$auditExe = [IO.Path]::GetFullPath($Executable)
$auditOutput = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $auditExe.StartsWith($auditRoot, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $auditExe -PathType Leaf)) { throw 'Use this round diagnostic executable.' }
if (-not $auditOutput.StartsWith($auditRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use this round artifact directory.' }
if (Test-Path -LiteralPath $auditOutput) { throw 'Preserve previous evidence; measurement directory must be new.' }
if (@(Get-Process -Name Unity,MemoryAudit -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Coordinate an exclusive Unity/diagnostic Player measurement slot.' }
New-Item -ItemType Directory -Path $auditOutput | Out-Null
function Quote-AuditArguments([string[]]$Arguments) {
    (($Arguments | ForEach-Object {
        $value = [regex]::Replace($_, '(\\*)"', '$1$1\"')
        '"' + [regex]::Replace($value, '(\\+)$', '$1$1') + '"'
    }) -join ' ')
}
$auditArguments = @('-screen-fullscreen','0','-screen-width','1920','-screen-height','1080','-runtimeMemoryPlayerOutput',(Join-Path $auditOutput 'memory.json'),'-logFile',(Join-Path $auditOutput 'player.log'))
$auditTimer = [Diagnostics.Stopwatch]::StartNew()
$auditLaunchUtc = [DateTime]::UtcNow
$auditProcess = Start-Process -FilePath $auditExe -ArgumentList (Quote-AuditArguments $auditArguments) -WindowStyle Hidden -PassThru
$managed = Join-Path ([IO.Path]::GetDirectoryName($auditExe)) ([IO.Path]::GetFileNameWithoutExtension($auditExe) + '_Data\Managed')
$assemblyHashes = @(Get-ChildItem -LiteralPath $managed -Filter 'ShatteredSail.*.dll' -File | ForEach-Object { [pscustomobject]@{Name=$_.Name;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
[pscustomobject]@{Exe=$auditExe;ProcessId=$auditProcess.Id;Arguments=$auditArguments;StartedUtc=$auditLaunchUtc.ToString('o');ExeSHA256=(Get-FileHash -LiteralPath $auditExe).Hash;Assemblies=$assemblyHashes;Window='Hidden; controlled offscreen production Canvas render';SampleIntervalMs=250} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $auditOutput 'launch.json') -Encoding utf8
Write-Output ('MEMORY_PLAYER_PID=' + $auditProcess.Id)
$auditSamples = [Collections.Generic.List[object]]::new()
while (-not $auditProcess.HasExited -and $auditTimer.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
    $auditProcess.Refresh()
    if (-not $auditProcess.HasExited) {
        $auditSamples.Add([pscustomobject]@{ElapsedSeconds=[Math]::Round($auditTimer.Elapsed.TotalSeconds,4);Utc=[DateTime]::UtcNow.ToString('o');PrivateBytes=$auditProcess.PrivateMemorySize64;WorkingSetBytes=$auditProcess.WorkingSet64})
    }
    Start-Sleep -Milliseconds 250
}
$auditTimedOut = -not $auditProcess.HasExited
if ($auditTimedOut) { Stop-Process -Id $auditProcess.Id }
$auditProcess.WaitForExit()
$auditSamples | Export-Csv -LiteralPath (Join-Path $auditOutput 'process-memory.csv') -NoTypeInformation -Encoding utf8
[pscustomobject]@{ExitCode=$auditProcess.ExitCode;TimedOut=$auditTimedOut;Samples=$auditSamples.Count;ElapsedSeconds=$auditTimer.Elapsed.TotalSeconds;PeakPrivateMiB=($auditSamples|Measure-Object PrivateBytes -Maximum).Maximum/1MB;PeakWorkingSetMiB=($auditSamples|Measure-Object WorkingSetBytes -Maximum).Maximum/1MB} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $auditOutput 'process-summary.json') -Encoding utf8
if ($auditTimedOut -or $auditProcess.ExitCode -ne 0) { throw 'Owned diagnostic Player failed; see preserved logs and partial report.' }
& (Join-Path $PSScriptRoot 'Analyze.ps1') -OutputDirectory $auditOutput
