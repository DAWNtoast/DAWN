param(
    [int]$Seeds = 4,
    [string]$Mode = "Baseline",
    [ValidateRange(10,60)]
    [int]$CrewReference = 20,
    [ValidateSet("None", "StarterOnly", "CrewScale", "StarterAndCrewScale", "FleetCrewScale")]
    [string]$Candidate = "None",
    [string]$Output = 'D:/Game/UnityClient/Artifacts/ShipProgression20261003/Calibration-baseline',
    [string]$UnityData = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
)
$ErrorActionPreference = 'Stop'
$progressWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ($Candidate -ne 'None' -and (Test-Path -LiteralPath (Join-Path $progressWorkspace 'UnityClient/Assets/_Project/Core/Runtime/CoreBattleCardEffects.cs'))) {
    throw 'Production now owns boarding scaling. Historical candidate transformations must only run against their archived pre-change source snapshots. Use RunProductionVerification.ps1 for current production.'
}
$progressOutput = [IO.Path]::GetFullPath($Output)
$progressSnapshot = Join-Path $progressOutput 'CoreSnapshot'
$progressBuild = Join-Path $progressOutput 'Build'
New-Item -ItemType Directory -Force -Path $progressSnapshot, $progressBuild | Out-Null
$progressSources = @(Get-ChildItem -LiteralPath (Join-Path $progressWorkspace 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs')
$progressSources | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $progressSnapshot $_.Name) }
$progressSources | ForEach-Object { Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 } | Select-Object Path,Hash | Export-Csv -LiteralPath (Join-Path $progressOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
if ($Candidate -eq 'StarterOnly' -or $Candidate -eq 'StarterAndCrewScale') {
    $progressShipFile = Join-Path $progressSnapshot 'CoreVoyageSession.cs'
    $progressText = [IO.File]::ReadAllText($progressShipFile)
    $progressOriginal = 'new CoreShipDefinition("sloop", "轻帆船 · 灰鸥", 70, 4, 8, 15f, 90f, 14)'
    if (-not $progressText.Contains($progressOriginal)) { throw 'Starter candidate no longer matches baseline stats.' }
    $progressText = $progressText.Replace($progressOriginal, 'new CoreShipDefinition("sloop", "轻帆船 · 灰鸥", 55, 2, 6, 13f, 80f, 10)')
    [IO.File]::WriteAllText($progressShipFile,$progressText)
}
if ($Candidate -eq 'FleetCrewScale') {
    $progressFleet = @(Import-Csv -LiteralPath (Join-Path $PSScriptRoot 'FleetCandidate.csv'))
    $progressShipFile = Join-Path $progressSnapshot 'CoreVoyageSession.cs'
    $progressText = [IO.File]::ReadAllText($progressShipFile)
    foreach ($progressHull in $progressFleet) {
        $progressPattern = 'new CoreShipDefinition\("' + $progressHull.id + '", "([^"]+)", [^)]*\)'
        if (-not [regex]::IsMatch($progressText,$progressPattern)) { throw "Missing candidate ship $($progressHull.id)" }
        $progressReplacement = 'new CoreShipDefinition("' + $progressHull.id + '", "$1", ' + $progressHull.hull + ', ' + $progressHull.armor + ', ' + $progressHull.firepower + ', ' + $progressHull.speed + 'f, ' + $progressHull.cargo + 'f, ' + $progressHull.crew + ')'
        $progressText = [regex]::Replace($progressText,$progressPattern,$progressReplacement)
    }
    [IO.File]::WriteAllText($progressShipFile,$progressText)
    $progressCatalogFile = Join-Path $progressSnapshot 'CoreShipMarketCatalog.cs'
    $progressCatalog = [IO.File]::ReadAllText($progressCatalogFile)
    $progressListings = [regex]::Matches($progressCatalog,'new Listing\(\d+,')
    if ($progressListings.Count -ne $progressFleet.Count) { throw 'Fleet price count mismatch' }
    for ($progressIndex=$progressListings.Count-1; $progressIndex -ge 0; $progressIndex--) {
        $progressMatch = $progressListings[$progressIndex]
        $progressCatalog = $progressCatalog.Remove($progressMatch.Index,$progressMatch.Length).Insert($progressMatch.Index,'new Listing(' + $progressFleet[$progressIndex].price + ',')
    }
    [IO.File]::WriteAllText($progressCatalogFile,$progressCatalog)
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FleetCandidate.csv') -Destination (Join-Path $progressOutput 'FleetCandidate.csv')
}
if ($Candidate -eq 'CrewScale' -or $Candidate -eq 'StarterAndCrewScale' -or $Candidate -eq 'FleetCrewScale') {
    $progressCombatFile = Join-Path $progressSnapshot 'CoreNavalBattleCombat.cs'
    $progressText = [IO.File]::ReadAllText($progressCombatFile)
    $progressText = $progressText.Replace('int damage = card.Damage;', 'int damage = card.Damage; if (Battle.Stage == NavalBattleStage.Boarding) damage = ScaleBoardingEffect(damage);')
    $progressText = $progressText.Replace('Battle.PlayerBlock = (int)Math.Min(int.MaxValue, (long)Battle.PlayerBlock + block);', 'if (Battle.Stage == NavalBattleStage.Boarding) block = ScaleBoardingEffect(block); Battle.PlayerBlock = (int)Math.Min(int.MaxValue, (long)Battle.PlayerBlock + block);')
    $progressText = $progressText.Replace('Battle.PlayerBoardingHealth + card.Repair);', 'Battle.PlayerBoardingHealth + ScaleBoardingEffect(card.Repair));')
    $progressText = $progressText.Replace('private void ApplyBattleCardStrategy(CoreBattleCard card)', 'private int ScaleBoardingEffect(int value) => (int)Math.Round(value * Math.Max(.3d, Math.Min(1.6d, SelectedOrdinaryCrew / 20d)), MidpointRounding.AwayFromZero); private void ApplyBattleCardStrategy(CoreBattleCard card)')
    $progressText = $progressText.Replace("SelectedOrdinaryCrew / 20d", "SelectedOrdinaryCrew / " + $CrewReference + "d")
    [IO.File]::WriteAllText($progressCombatFile,$progressText)
}
Get-ChildItem -LiteralPath $progressSnapshot -Filter '*.cs' | Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash | Export-Csv -LiteralPath (Join-Path $progressOutput 'compiled-source-hashes.csv') -NoTypeInformation -Encoding utf8
[IO.File]::WriteAllText((Join-Path $progressOutput 'candidate.txt'), "Candidate=$Candidate; Source product unchanged; crew factor=max(0.3,min(1.6,ordinary/$CrewReference)); integer rounding AwayFromZero.")
$progressCompiler = Join-Path $UnityData 'DotNetSdkRoslyn/csc.dll'
$progressRuntime = Join-Path $UnityData 'NetCoreRuntime/dotnet.exe'
$progressMono = Join-Path $UnityData 'MonoBleedingEdge/bin/mono.exe'
$progressFramework = Join-Path $UnityData 'MonoBleedingEdge/lib/mono/4.7.2-api'
$progressExe = Join-Path $progressBuild 'ShipProgression.exe'
$progressArguments = @($progressCompiler, '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$progressExe")
foreach ($progressAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) {
    $progressArguments += '-r:' + (Join-Path $progressFramework $progressAssembly)
}
$progressArguments += @(Get-ChildItem -LiteralPath $progressSnapshot -Filter '*.cs' | ForEach-Object FullName)
$progressProgram = if ($Mode -like 'Crew*' -or $Mode -eq 'Tutorial') { 'CrewProgressionProgram.cs' } else { 'ShipProgressionProgram.cs' }
foreach ($progressTool in @('ShipProgressionSimulator.cs',$progressProgram)) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $progressTool) -Destination (Join-Path $progressBuild $progressTool)
    $progressArguments += Join-Path $progressBuild $progressTool
}
& $progressRuntime @progressArguments
if ($LASTEXITCODE -ne 0) { throw 'Ship progression compilation failed.' }
& $progressMono $progressExe $Seeds $progressOutput $Mode $Candidate $CrewReference
if ($LASTEXITCODE -ne 0) { throw 'Ship progression simulation failed.' }
