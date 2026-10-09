param([ValidateSet('Edit','Play','References')][string]$Stage = 'Edit', [string]$Version = 'v1', [string]$EditFilter = '',
    [string]$Filter = 'MonthlyCalendar_;Dispatch_;DepartureProvisions_;MarketStorage_;MarketQuantity_;PortWarehouseTrading_;RareWarehouseSale_;TradeMarket_;PortInvestment_;GameMasterRuntimeTests;MainMenuSceneTests;RegionalComponentShopStock_;ComponentShopShowroomPaginationTests;GuidedTutorial_')
$ErrorActionPreference = 'Stop'
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Close Unity before isolated QA.' }
$project = 'D:\Game\UnityClient'
$output = Join-Path $project ('Artifacts\MonthlyCalendar20261003\' + $Stage + '-' + $Version)
if (Test-Path -LiteralPath $output) { throw "Output already exists: $output" }
New-Item -ItemType Directory -Path $output | Out-Null
$launch = @('-batchmode','-projectPath',$project,'-logFile',(Join-Path $output 'Unity.log'))
if ($Stage -eq 'References') {
    $launch += @('-quit','-executeMethod','ShatteredSail.Editor.GameplayPreviewSceneBuilder.SyncAndCaptureMonthlyCalendarFromCommandLine',
        '-navalVisualCapturePath',(Join-Path $output 'captures'))
} else {
    $platform = if ($Stage -eq 'Play') { 'PlayMode' } else { 'EditMode' }
    $launch += @('-runTests','-testPlatform',$platform,'-testResults',(Join-Path $output 'Results.xml'))
    if ($Stage -eq 'Play') { $launch += @('-testFilter',$Filter,'-navalVisualCapturePath',(Join-Path $output 'captures')) }
    elseif ($EditFilter) { $launch += @('-testFilter',$EditFilter) }
}
$process = Start-Process 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -ArgumentList $launch -WindowStyle Hidden -PassThru
$process.WaitForExit()
Write-Output "Unity exit: $($process.ExitCode)"
if (Test-Path -LiteralPath (Join-Path $output 'Results.xml')) {
    [xml]$result = Get-Content -LiteralPath (Join-Path $output 'Results.xml') -Raw
    $result.'test-run' | Select-Object result,total,passed,failed,skipped,duration
}
if ($process.ExitCode -ne 0) { exit $process.ExitCode }
