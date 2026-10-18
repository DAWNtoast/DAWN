param(
    [ValidateSet('Capture', 'EditMode', 'PlayMode')][string]$Mode = 'Capture',
    [string]$OutputRoot = ('D:/Game/Artifacts/HeaderRedesign20261014/' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$project = 'D:/Game/UnityClient'
$unity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
$conflicts = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Replace('\', '/').IndexOf($project, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
if ($conflicts.Count -gt 0) { throw 'A Unity process already owns this project; run this project capture/tests one at a time.' }
$output = [IO.Path]::GetFullPath($OutputRoot)
$allowed = [IO.Path]::GetFullPath('D:/Game/Artifacts/HeaderRedesign20261014') + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be below D:/Game/Artifacts/HeaderRedesign20261014.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$source = Join-Path $PSScriptRoot 'HeaderRedesignCapture20261014.cs'
$installed = Join-Path $project 'Assets/_Project/Editor/HeaderRedesignCapture20261014.cs'
if (Test-Path -LiteralPath $installed) {
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $installed).Hash) { throw 'Installed review source differs. Preserve it and investigate before replacing.' }
} else { Copy-Item -LiteralPath $source -Destination $installed }
$log = Join-Path $output ($Mode + '-unity.log')
if (Test-Path -LiteralPath $log) { throw 'Log already exists; use a new output root.' }
$arguments = @('-batchmode', '-projectPath', ('"' + $project + '"'), '-logFile', ('"' + $log + '"'))
if ($Mode -eq 'Capture') {
    $captures = Join-Path $output 'captures'
    $arguments += @('-quit', '-executeMethod', 'ShatteredSail.Editor.HeaderRedesignCapture20261014.CaptureFromCommandLine', '-headerRedesignCapturePath', ('"' + $captures + '"'))
} else {
    if ([string]::IsNullOrWhiteSpace($TestFilter)) {
        $TestFilter = if ($Mode -eq 'EditMode') { 'PortFacilityHeaderTests;NavigationDashboardTests;BattleHudLayoutTests;GameTypographyTests;CompactHeaderLayoutTests;VoyageSettingsModalTests' } else { 'VoyageHeader_ReusesTimelineAndControlsAndReleasesInactiveArt' }
    }
    $results = Join-Path $output ($Mode + '-results.xml')
    $arguments += @('-runTests', '-testPlatform', $Mode, '-testFilter', ('"' + $TestFilter + '"'), '-testResults', ('"' + $results + '"'))
}
$process = Start-Process -FilePath $unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$record = [ordered]@{ mode = $Mode; pid = $process.Id; output = $output; log = $log; installedEditorTool = $installed; sourceHash = (Get-FileHash -LiteralPath $source).Hash; startedAtUtc = [DateTime]::UtcNow.ToString('O'); arguments = $arguments }
$record | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $output ($Mode + '-process.json')) -Encoding utf8
$record | ConvertTo-Json -Depth 3