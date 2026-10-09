param(
    [string]$Project = 'D:/Game/tmp/第七港口v2-build-project',
    [string]$Output = 'D:/Game/Artifacts/SequentialOptimization20261008/01-PlayerPerformance',
    [string]$Unity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe',
    [ValidateRange(640,7680)][int]$Width = 1920,
    [ValidateRange(480,4320)][int]$Height = 1080,
    [switch]$UiVariants,
    [switch]$BuildOnly
)
$ErrorActionPreference = 'Stop'
function ConvertTo-AuditWindowsCommandLine([string[]]$Arguments) {
    # Start-Process joins ArgumentList without preserving argument boundaries.
    # Quote each native Windows argument, including trailing backslashes and quotes.
    $quoted = foreach ($argument in $Arguments) {
        if ($null -eq $argument) { $argument = '' }
        $escaped = [regex]::Replace($argument, '(\\*)"', '$1$1\"')
        $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
        '"' + $escaped + '"'
    }
    return $quoted -join ' '
}
$auditWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditProject = [IO.Path]::GetFullPath($Project)
$auditOutput = [IO.Path]::GetFullPath($Output)
if (-not $auditProject.StartsWith((Join-Path $auditWorkspace 'tmp') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Use an isolated project under the workspace tmp directory.'
}
if (-not $auditOutput.StartsWith($auditWorkspace + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Output must be inside the workspace.'
}
New-Item -ItemType Directory -Path $auditOutput -Force | Out-Null
$auditRuntime = Join-Path $auditProject 'Assets/PlayerPerformanceAudit'
$auditEditor = Join-Path $auditProject 'Assets/PlayerPerformanceAudit/Editor'
New-Item -ItemType Directory -Path $auditRuntime,$auditEditor -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PlayerPerformanceProbe.cs') -Destination $auditRuntime
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PlayerPerformanceAuditBuilder.cs') -Destination $auditEditor
@'
{
  "name": "ShatteredSail.PlayerPerformanceAudit",
  "references": ["ShatteredSail.Presentation", "ShatteredSail.Core", "ShatteredSail.Content", "Unity.UGUI"],
  "autoReferenced": true
}
'@ | Set-Content -LiteralPath (Join-Path $auditRuntime 'ShatteredSail.PlayerPerformanceAudit.asmdef') -Encoding utf8
@'
{
  "name": "ShatteredSail.PlayerPerformanceAudit.Editor",
  "references": [],
  "includePlatforms": ["Editor"],
  "autoReferenced": true
}
'@ | Set-Content -LiteralPath (Join-Path $auditEditor 'ShatteredSail.PlayerPerformanceAudit.Editor.asmdef') -Encoding utf8
$auditExecutable = Join-Path $auditOutput 'build/PerformanceAudit.exe'
$auditBuildLog = Join-Path $auditOutput 'build.log'
if (Test-Path -LiteralPath $auditExecutable) { throw 'Choose a new output directory; existing builds are preserved.' }
$auditBuildArguments = @('-batchmode','-quit','-projectPath',$auditProject,'-executeMethod','ShatteredSail.Editor.PlayerPerformanceAuditBuilder.BuildFromCommandLine','-playerAuditBuildOutput',$auditExecutable,'-logFile',$auditBuildLog)
$auditBuildProcess = Start-Process -FilePath $Unity -ArgumentList (ConvertTo-AuditWindowsCommandLine $auditBuildArguments) -WindowStyle Hidden -PassThru
Write-Output ('UNITY_BUILD_PID=' + $auditBuildProcess.Id)
$auditBuildProcess.WaitForExit()
if ($auditBuildProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $auditExecutable)) { throw ('Build failed; inspect ' + $auditBuildLog) }
Write-Output ('PLAYER_BUILT=' + $auditExecutable)
if ($BuildOnly) { return }
$auditReport = Join-Path $auditOutput 'player-performance.json'
$auditPlayerLog = Join-Path $auditOutput 'player.log'
if (Test-Path -LiteralPath $auditReport) { throw 'Refusing to replace measurement evidence.' }
$auditPlayerArguments = @('-screen-fullscreen','0','-screen-width',$Width.ToString(),'-screen-height',$Height.ToString(),
    '-playerAuditWidth',$Width.ToString(),'-playerAuditHeight',$Height.ToString(),'-playerPerformanceOutput',$auditReport,'-logFile',$auditPlayerLog)
if ($UiVariants) { $auditPlayerArguments += '-playerAuditUiVariants' }
$auditPlayerProcess = Start-Process -FilePath $auditExecutable -ArgumentList (ConvertTo-AuditWindowsCommandLine $auditPlayerArguments) -WindowStyle Hidden -PassThru
Write-Output ('PLAYER_PID=' + $auditPlayerProcess.Id)
if (-not $auditPlayerProcess.WaitForExit(240000)) {
    Stop-Process -Id $auditPlayerProcess.Id
    throw 'This audit-owned Player exceeded its 240-second bound; see player.log.'
}
if ($auditPlayerProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $auditReport)) { throw ('Player measurement failed; inspect ' + $auditPlayerLog) }
Write-Output ('PLAYER_REPORT=' + $auditReport)
