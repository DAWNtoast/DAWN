param(
    [ValidateSet('Sync','Edit','Play','Capture')][string]$Stage = 'Sync',
    [string]$Run = 'run1',
    [string]$Filter = '',
    [ValidateSet('Code','Resources','All')][string]$Scope = 'All'
)
$ErrorActionPreference = 'Stop'
$artWorkspace = 'D:\Game'
$artProject = 'D:\Game\tmp\shipyard-detail-clean-20261011'
$artUnity = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
if ($Run -notmatch '^[a-zA-Z0-9_-]+$') { throw 'Simple run name required.' }
$artOutput = Join-Path $PSScriptRoot $Run
$artBusy = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\','/').Contains($artProject.Replace('\','/'))
})
if ($artBusy.Count) { throw ('Isolated Unity project occupied: ' + ($artBusy.ProcessId -join ',')) }
$artRetired = Join-Path $artProject 'Assets/_Project/Scenes/UIPreviewsForMe'
if ((Test-Path -LiteralPath $artRetired) -and @(Get-ChildItem -LiteralPath $artRetired -Force).Count) { throw 'Retired preview assets must remain absent.' }
if ($Stage -eq 'Sync') {
    & (Join-Path $artWorkspace 'tools/NavalTactics20261015/SyncValidation.ps1') -ProjectPath $artProject -Scope $Scope
    Copy-Item -LiteralPath (Join-Path $artWorkspace 'tools/BattleHud20261017/BattleHudCapture.cs') -Destination (Join-Path $artProject 'Assets/_Project/Editor/BattleHudCapture.cs') -Force
    $artSettingsPath = Join-Path $artProject 'ProjectSettings/ProjectSettings.asset'
    $artSettings = [IO.File]::ReadAllText($artSettingsPath)
    $artSettings = [regex]::Replace($artSettings, '(?m)^  companyName:.*$', '  companyName: CodexArtRenovation20261022')
    $artSettings = [regex]::Replace($artSettings, '(?m)^  productName:.*$', '  productName: ArtRenovationValidation20261022')
    [IO.File]::WriteAllText($artSettingsPath, $artSettings, [Text.UTF8Encoding]::new($false))
    return
}
$artSettings = [IO.File]::ReadAllText((Join-Path $artProject 'ProjectSettings/ProjectSettings.asset'))
if ($artSettings -notmatch 'companyName: CodexArtRenovation20261022') { throw 'Run Sync first for isolated player storage.' }
[IO.Directory]::CreateDirectory($artOutput) | Out-Null
$artLog = Join-Path $artOutput ($Stage.ToLowerInvariant() + '.log')
$artResult = Join-Path $artOutput ($Stage.ToLowerInvariant() + '-results.xml')
if ((Test-Path -LiteralPath $artLog) -or (Test-Path -LiteralPath $artResult)) { throw 'Existing evidence is preserved; choose a new run.' }
$artArgs = @('-batchmode','-projectPath',('"' + $artProject + '"'),'-logFile',('"' + $artLog + '"'))
if ($Stage -eq 'Capture') {
    $artArgs += @('-executeMethod','ShatteredSail.Editor.BattleHudCapture.CaptureQuickFromCommandLine',
        '-battleHudCapturePath',('"' + (Join-Path $artOutput 'Artifacts/captures') + '"'))
} else {
    if (-not $Filter) { throw 'Pass targeted test Filter.' }
    if ($Stage -eq 'Edit') { $artArgs += '-nographics' }
    $artArgs += @('-runTests','-testPlatform',($Stage + 'Mode'),'-testFilter',('"' + $Filter + '"'),
        '-testResults',('"' + $artResult + '"'))
    if ($Stage -eq 'Play') { $artArgs += @('-navalVisualCapturePath',('"' + (Join-Path $artOutput 'Artifacts/play-captures') + '"')) }
}
$artProcess = Start-Process -FilePath $artUnity -ArgumentList $artArgs -WorkingDirectory $artProject -WindowStyle Hidden -PassThru
Write-Output ("Started {0}, PID {1}, log {2}" -f $Stage,$artProcess.Id,$artLog)
