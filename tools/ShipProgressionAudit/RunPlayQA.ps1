param([string]$Name = 'Verification-Play-v1')
$ErrorActionPreference = 'Stop'
if (Get-Process Unity -ErrorAction SilentlyContinue) { throw 'Close the current Unity test/editor before running QA.' }
$project = 'D:\Game\UnityClient'
$output = Join-Path $project ('Artifacts\ShipProgression20261003\' + $Name)
New-Item -ItemType Directory -Force -Path $output | Out-Null
$filter = 'BattleRules_;DepartureCrew_;PortShipMarket_;ShipyardEquipment_;DeckBuilderHeader_;CardForge_UpgradesBaseCardsThreeTimesAndExcludesSpecialSources;WorkshopFourChests_;ShatteredSail.Presentation.Tests.GameMasterRuntimeTests;DepartureProvisions_;PortWarehouseTrading_;Warehouse_;MarketStorage_;MarketQuantity_'
$arguments = @('-batchmode','-projectPath',$project,'-runTests','-testPlatform','PlayMode','-testFilter',$filter,'-testResults',(Join-Path $output 'Play.xml'),'-navalVisualCapturePath',(Join-Path $output 'captures'),'-logFile',(Join-Path $output 'Play.log'))
(Start-Process 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru).Id