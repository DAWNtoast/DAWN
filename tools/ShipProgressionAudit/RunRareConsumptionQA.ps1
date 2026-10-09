param([string]$Version = 'v1', [ValidateSet('Play','References','FinalEdit')][string]$Stage = 'Play',
    [string]$Filter = 'RareWarehouse_;RareWarehouseSale_;PortInvestment_;RareResourceEvents_;BoardingMedicine_;BattleRules_;BattleResponsive_;WarehouseCargo_;MarketQuantity_;TreasureCore_')
$ErrorActionPreference = 'Stop'
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Close Unity before starting isolated QA.' }
$project = 'D:\Game\UnityClient'
$editor = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe'
$root = Join-Path $project 'Artifacts\RareConsumption20261003'
function Run-Unity([string[]]$LaunchArgs) {
    $proc = Start-Process $editor -ArgumentList $LaunchArgs -WindowStyle Hidden -PassThru
    $proc.WaitForExit()
    if ($proc.ExitCode -ne 0) { throw "Unity failed ($($proc.ExitCode)); see the stage log." }
}
if ($Stage -eq 'References') {
    foreach ($job in @(
        @{ Name='Warehouse'; Method='SyncAndCaptureRareWarehouseFromCommandLine' },
        @{ Name='Investment'; Method='SyncAndCaptureRareInvestmentFromCommandLine' },
        @{ Name='Boarding'; Method='SyncAndCaptureBoardingCrewFromCommandLine' }
    )) {
        $folder = Join-Path $root ($job.Name + '-References-' + $Version)
        if (Test-Path $folder) { throw "Output already exists: $folder" }
        Run-Unity @('-batchmode','-quit','-projectPath',$project,'-executeMethod',
            ('ShatteredSail.Editor.GameplayPreviewSceneBuilder.' + $job.Method),
            '-navalVisualCapturePath',$folder,'-logFile',($folder + '.log'))
        Write-Output "Reference sync completed: $($job.Name)"
    }
} else {
    $platform = if ($Stage -eq 'Play') { 'PlayMode' } else { 'EditMode' }
    $folder = Join-Path $root ($Stage + '-' + $Version)
    if (Test-Path $folder) { throw "Output already exists: $folder" }
    New-Item -ItemType Directory -Path $folder | Out-Null
    $launch = @('-batchmode','-projectPath',$project,'-runTests','-testPlatform',$platform,
        '-testResults',(Join-Path $folder 'Results.xml'),'-logFile',(Join-Path $folder 'Unity.log'))
    if ($Stage -eq 'Play') {
        $launch += @('-testFilter',
            $Filter,
            '-navalVisualCapturePath',(Join-Path $folder 'captures'))
    }
    Run-Unity $launch
    [xml]$result = Get-Content -LiteralPath (Join-Path $folder 'Results.xml') -Raw
    if ($result.'test-run'.result -ne 'Passed') { throw 'QA failed; inspect Results.xml.' }
    Write-Output "$platform $($result.'test-run'.passed)/$($result.'test-run'.total) passed."
}
