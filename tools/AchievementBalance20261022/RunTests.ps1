param(
    [Parameter(Mandatory=$true)][ValidateSet('EditMode','PlayMode')][string]$Platform,
    [string]$RunTag = 'current',
    [string]$ProjectPath = 'D:/Game/tmp/component-rarity-halo-20261019'
)
$ErrorActionPreference = 'Stop'
$achievementWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$achievementOutput = Join-Path $achievementWorkspace ('Artifacts/AchievementBalance20261022/' + $RunTag + '/' + $Platform)
$achievementFilter = if ($Platform -eq 'EditMode') { 'Achievement;ProfileSaveJournal;OwnedShipComponentRefitEvidenceTests;CoreVoyageCheckpointTests' } else { 'Achievement;VoyageThreeEvents;VoyageHalfChart;VoyageEventArrival' }
& (Join-Path $achievementWorkspace 'tools/ReleaseBuild/RunTests.ps1') -Platform $Platform -ProjectPath $ProjectPath -Output $achievementOutput -TestFilter $achievementFilter
if ($LASTEXITCODE -ne 0) { throw ('Achievement ' + $Platform + ' tests failed.') }
