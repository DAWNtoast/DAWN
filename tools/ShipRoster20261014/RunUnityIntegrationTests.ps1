param([Parameter(Mandatory=$true)][string]$ProjectPath,
    [string]$Output = 'D:/Game/Artifacts/ShipRoster20261014/UnityIntegration',
    [string]$UnityEditor = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe')
$ErrorActionPreference = 'Stop'
$shipProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\','/')
$shipMainProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../UnityClient')).TrimEnd('\','/')
if ($shipProject -eq $shipMainProject) { throw 'Use an isolated verification project; this runner will not lock the main Unity project.' }
if (-not (Test-Path -LiteralPath (Join-Path $shipProject 'Assets/_Project/Tests/EditMode/SpecialWarshipArtCatalogTests.cs'))) {
    throw 'The isolated project must already contain the current ship-roster sources and artwork.'
}
[IO.Directory]::CreateDirectory($Output) | Out-Null
$shipFilter = 'ShatteredSail.Core.Tests.SpecialWarshipArtCatalogTests;ShatteredSail.Core.Tests.BoardingBattleArtCatalogTests;ShatteredSail.Core.Tests.CoreVoyageCheckpointTests;ShatteredSail.Core.Tests.CoreCollectionProgressionTests'
& $UnityEditor -batchmode -nographics -projectPath $shipProject -runTests -testPlatform EditMode -testFilter $shipFilter -testResults (Join-Path $Output 'results.xml') -logFile (Join-Path $Output 'editor.log')
if ($LASTEXITCODE -ne 0) { throw 'Isolated Unity integration tests failed. Inspect results.xml and editor.log.' }
