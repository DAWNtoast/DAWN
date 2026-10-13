param([string]$Version = 'v1', [ValidateRange(0, 3)][int]$StartAt = 0)
$ErrorActionPreference = 'Stop'
# Retired by user request; exit before filesystem or Unity actions.
throw 'UIPreviewsForMe was retired on 2026-10-13; saved-preview generation, refresh and copy-back are disabled.'
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Unity must be closed before syncing scene references.' }
$project = 'D:\Game\UnityClient'
$editor = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$jobs = @(
    @{ Method = 'SyncAndCaptureBoardingCrewFromCommandLine'; Folder = "Artifacts\ShipProgression20261003\BoardingReferences-$Version" },
    @{ Method = 'SyncAndCaptureRegionalShipyardsFromCommandLine'; Folder = "Artifacts\ShipProgression20261003\ShipyardReferences-$Version" },
    @{ Method = 'SyncAndCaptureRareWarehouseFromCommandLine'; Folder = "Artifacts\RareWarehouse20261003\References-$Version" }
)
foreach ($job in ($jobs | Select-Object -Skip $StartAt)) {
    $output = Join-Path $project $job.Folder
    if (Test-Path $output) { throw "Reference output already exists: $output" }
    $launchArgs = @('-batchmode', '-quit', '-projectPath', $project, '-executeMethod',
        ('ShatteredSail.Editor.GameplayPreviewSceneBuilder.' + $job.Method),
        '-navalVisualCapturePath', $output, '-logFile', ($output + '.log'))
    $process = Start-Process $editor -ArgumentList $launchArgs -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "$($job.Method) failed, exit $($process.ExitCode). See $output.log" }
    Write-Output "Completed $($job.Method): $output"
}
$verification = Join-Path $project "Artifacts\ShipProgression20261003\Verification-Final-$Version"
if (Test-Path $verification) { throw "Verification output already exists: $verification" }
New-Item -ItemType Directory -Path $verification | Out-Null
$launchArgs = @('-batchmode', '-projectPath', $project, '-runTests', '-testPlatform', 'EditMode',
    '-testResults', (Join-Path $verification 'Edit.xml'), '-logFile', (Join-Path $verification 'Edit.log'))
$process = Start-Process $editor -ArgumentList $launchArgs -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Final EditMode failed, exit $($process.ExitCode)." }
[xml]$results = Get-Content -LiteralPath (Join-Path $verification 'Edit.xml') -Raw
if ($results.'test-run'.result -ne 'Passed') { throw 'Final EditMode did not pass.' }
Write-Output "Final EditMode: $($results.'test-run'.passed)/$($results.'test-run'.total) passed."
