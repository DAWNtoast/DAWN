param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Sync', 'Edit', 'Play', 'Capture')][string]$Stage,
    [string]$ProjectPath = 'D:/Game/tmp/shipyard-detail-clean-20261011',
    [string]$Output = 'D:/Game/Artifacts/BattleHud20261017/Validation',
    [string]$TestFilter = '',
    [ValidateSet('Code', 'Resources', 'All')][string]$SyncScope = 'All',
    [string]$CaptureMethod = 'ShatteredSail.Editor.BattleHudCapture.CaptureFromCommandLine',
    [string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
)

$ErrorActionPreference = 'Stop'
$battleHudWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')).TrimEnd('\', '/')
$battleHudProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
$battleHudTmp = Join-Path $battleHudWorkspace 'tmp'
$battleHudOutput = [IO.Path]::GetFullPath($Output).TrimEnd('\', '/')
$battleHudArtifacts = Join-Path $battleHudWorkspace 'Artifacts'
if (-not $battleHudProject.StartsWith($battleHudTmp + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation requires an isolated project below this workspace/tmp.'
}
if (-not $battleHudOutput.StartsWith($battleHudArtifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation outputs must be below this workspace/Artifacts.'
}
if ($battleHudProject -match '(^|[/\\])UIPreviewsForMe([/\\]|$)' -or $battleHudOutput -match '(^|[/\\])UIPreviewsForMe([/\\]|$)') {
    throw 'Retired preview locations are forbidden.'
}
if (-not (Test-Path -LiteralPath (Join-Path $battleHudProject 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'An existing isolated Unity project is required.'
}
$battleHudRetired = Join-Path $battleHudProject 'Assets/_Project/Scenes/UIPreviewsForMe'
if ((Test-Path -LiteralPath $battleHudRetired) -and @(Get-ChildItem -LiteralPath $battleHudRetired -Force).Count) {
    throw 'This validation never imports or restores retired previews; choose a clean isolated project.'
}
$battleHudRunning = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').Contains($battleHudProject.Replace('\', '/'))
})
if ($battleHudRunning.Count) { throw 'The isolated project is occupied by Unity.' }

$battleHudSettingsPath = Join-Path $battleHudProject 'ProjectSettings/ProjectSettings.asset'
if ($Stage -eq 'Sync') {
    & (Join-Path $battleHudWorkspace 'tools/NavalTactics20261015/SyncValidation.ps1') -ProjectPath $battleHudProject -Scope $SyncScope
    $battleHudSettings = [IO.File]::ReadAllText($battleHudSettingsPath)
    $battleHudSettings = [regex]::Replace($battleHudSettings, '(?m)^  companyName:.*$', '  companyName: CodexBattleHudValidation20261017')
    $battleHudSettings = [regex]::Replace($battleHudSettings, '(?m)^  productName:.*$', '  productName: BattleHudValidation20261017')
    [IO.File]::WriteAllText($battleHudSettingsPath, $battleHudSettings, [Text.UTF8Encoding]::new($false))
    $battleHudCaptureSource = Join-Path $PSScriptRoot 'BattleHudCapture.cs'
    if (Test-Path -LiteralPath $battleHudCaptureSource) {
        Copy-Item -LiteralPath $battleHudCaptureSource -Destination (Join-Path $battleHudProject 'Assets/_Project/Editor/BattleHudCapture.cs') -Force
    }
    Write-Output "Battle HUD sources synchronized into $battleHudProject with a dedicated player storage identity."
    return
}

$battleHudSettings = [IO.File]::ReadAllText($battleHudSettingsPath)
if ($battleHudSettings -notmatch '(?m)^  companyName: CodexBattleHudValidation20261017\r?$' -or
    $battleHudSettings -notmatch '(?m)^  productName: BattleHudValidation20261017\r?$') {
    throw 'The dedicated Battle HUD player storage identity is missing; run Sync first.'
}
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw 'Unity editor is unavailable.' }
[IO.Directory]::CreateDirectory($battleHudOutput) | Out-Null
$battleHudStageName = $Stage.ToLowerInvariant()
$battleHudLog = Join-Path $battleHudOutput ($battleHudStageName + '.log')
$battleHudResults = Join-Path $battleHudOutput ($battleHudStageName + '-results.xml')
if ((Test-Path -LiteralPath $battleHudLog) -or (Test-Path -LiteralPath $battleHudResults)) {
    throw 'Existing verification evidence is preserved. Choose a new output directory.'
}
$battleHudArgs = @('-batchmode', '-projectPath', ('"' + $battleHudProject + '"'), '-logFile', ('"' + $battleHudLog + '"'))
if ($Stage -eq 'Capture') {
    $battleHudArgs += @('-executeMethod', $CaptureMethod, '-battleHudCapturePath', ('"' + (Join-Path $battleHudOutput 'captures') + '"'))
} else {
    if (-not $TestFilter) { throw 'Pass a targeted TestFilter for Edit or Play.' }
    if ($Stage -eq 'Edit') { $battleHudArgs += '-nographics' }
    $battleHudArgs += @('-runTests', '-testPlatform', ($Stage + 'Mode'), '-testFilter', ('"' + $TestFilter + '"'),
        '-testResults', ('"' + $battleHudResults + '"'))
    if ($Stage -eq 'Play') {
        $battleHudArgs += @('-navalVisualCapturePath', ('"' + (Join-Path $battleHudOutput 'play-captures') + '"'))
    }
}
$battleHudProcess = Start-Process -FilePath $UnityEditor -ArgumentList $battleHudArgs -WorkingDirectory $battleHudProject -WindowStyle Hidden -PassThru
Write-Output ("Started Battle HUD {0}: PID {1}; log {2}" -f $Stage, $battleHudProcess.Id, $battleHudLog)
