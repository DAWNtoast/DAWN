param(
    [string]$IsolatedProject = 'D:/Game/tmp/departure-chart-inline-20261014',
    [string]$Output = 'D:/Game/Artifacts/RegionalDepartureDocks20261016/Validation',
    [switch]$StageOnly
)
$ErrorActionPreference = 'Stop'
$dockRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dockSource = Join-Path $dockRoot 'UnityClient'
$dockTarget = [IO.Path]::GetFullPath($IsolatedProject).TrimEnd('\', '/')
if (-not $dockTarget.StartsWith((Join-Path $dockRoot 'tmp') + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Validation requires a cached disposable project under D:/Game/tmp.'
}
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Another Unity editor is running; leave it untouched.' }
if (-not (Test-Path -LiteralPath (Join-Path $dockTarget 'Library'))) { throw 'Expected an existing cached validation project.' }
function Sync-DockDirectory([string]$Relative) {
    & robocopy (Join-Path $dockSource $Relative) (Join-Path $dockTarget $Relative) /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Failed to stage $Relative" }
}
# Scenes are deliberately excluded; this runner never saves or regenerates previews.
foreach ($dockRelative in @('Packages', 'ProjectSettings', 'Assets/_Project/Core',
    'Assets/_Project/Content', 'Assets/_Project/Persistence', 'Assets/_Project/Presentation/Runtime',
    'Assets/_Project/Editor', 'Assets/_Project/Tests', 'Assets/_Project/Resources')) {
    Sync-DockDirectory $dockRelative
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RegionalDepartureDockCapture.cs') -Destination (Join-Path $dockTarget 'Assets/_Project/Editor/RegionalDepartureDockCapture.cs')
if ($StageOnly) { Write-Output 'Current sources and resources staged in the disposable project.'; exit 0 }
[IO.Directory]::CreateDirectory($Output) | Out-Null
$dockFilter = 'ShatteredSail.Presentation.Tests.DepartureDockSceneLayoutTests;ShatteredSail.Presentation.Tests.PortFacilityArtCatalogLazyTests;ShatteredSail.Presentation.Tests.VoyageDepartureShipLayoutTests;ShatteredSail.Presentation.Tests.DepartureTreasureSeedSelectionTests'
$dockResults = Join-Path $Output 'editmode-results.xml'
$dockLog = Join-Path $Output 'editor.log'
$dockUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe'
$dockArguments = @('-batchmode', '-nographics', '-projectPath', $dockTarget,
    '-runTests', '-testPlatform', 'EditMode', '-testFilter', $dockFilter,
    '-testResults', $dockResults, '-logFile', $dockLog)
$dockProcess = Start-Process -FilePath $dockUnity -ArgumentList $dockArguments -WindowStyle Hidden -PassThru
$dockProcess.WaitForExit()
if ($dockProcess.ExitCode -ne 0) { throw "Unity validation failed: $($dockProcess.ExitCode). Inspect $dockLog" }
if (-not (Test-Path -LiteralPath $dockResults)) { throw 'Unity did not produce a test report.' }
[xml]$dockReport = Get-Content -LiteralPath $dockResults -Raw
if ($dockReport.'test-run'.result -ne 'Passed') { throw 'Unity reported failed tests; inspect editmode-results.xml.' }
Write-Output "Regional departure validation passed: $($dockReport.'test-run'.total) tests. $dockResults"
