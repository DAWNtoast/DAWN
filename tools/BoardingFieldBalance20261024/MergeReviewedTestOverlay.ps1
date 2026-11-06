param()
$ErrorActionPreference = 'Stop'
$boardingWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$boardingOverlay = Join-Path $boardingWorkspace 'outputs/boarding-field-balance-20261024/TestsOverlay'
$boardingTarget = Join-Path $boardingWorkspace 'UnityClient/Assets/_Project/Tests/EditMode'
$boardingBackup = Join-Path $boardingWorkspace 'outputs/boarding-field-balance-20261024/Inputs/TestsBeforeMerge'
$boardingBaseline = Import-Csv (Join-Path $boardingWorkspace 'Artifacts/BoardingFieldBalance20261024/CandidateAOriginalExpectations/source-hashes.csv')
$boardingGroups = @{
    Execution = @('CoreBoardingManuscriptRulesTests','CoreBoardingCurseTideBloodTests','CoreBloodBoardingRevisionTests','CoreSchoolBattleMechanicsTests','CoreBoardingInheritedFieldEffectsTests','CoreBoardingFireArcaneThunderTests','CoreThunderBoardingRevisionTests','CoreThunderBloodWinConditionTests','CoreCurseBoardingRevisionTests','CoreSchoolRebalanceRulesTests','CoreTideBoardingRevisionTests','CoreBoardingTransactionTests','CoreTideAudit20261024Tests','CoreBoardingFireRevisionTests','CoreBoardingEnemySchoolTests')
    Profiles = @('BattleTurnCostAndInitiativeTests','CoreBoardingBaselineTests','CoreBoardingBossPacingTests','CoreNavalBossCalibrationTests','ShipBoardingGrowthTests','BoardingMedicineTests','CoreBoardingEnemyTests')
    Catalog = @('BattleCardUpgradeTests','CoreCardAuthoredUpgradeTests','CoreCardUpgradeLimitTests','CoreBoardingCardCatalogTests','CoreBattleCardRuleTextBoardingTests','CoreBattleCardIconCostTextTests','CoreBattleCardDescriptionConsistencyTests','CoreBattleCardFaceDescriptionTests','CoreBattleCardWordingTests','CoreArcaneCatalogRevisionTests','CoreBloodCatalogRevisionTests','CoreCurseCatalogRevisionTests','CoreThunderCatalogRevisionTests','CoreTideCatalogRevisionTests','CoreCardAuthoredDamageTests','CoreBattleStatusPresentationTests','CoreArcaneManaRevisionTests')
}
$boardingAllowed = @($boardingGroups.Values | ForEach-Object { $_ })
$boardingFiles = @(Get-ChildItem -LiteralPath $boardingOverlay -Filter '*.cs')
if ($boardingFiles.Count -ne 39) { throw 'Expected exactly 39 reviewed fixtures.' }
$boardingManifest = foreach($boardingFile in $boardingFiles) {
    if ($boardingFile.BaseName -notin $boardingAllowed) { throw ('Unreviewed fixture: ' + $boardingFile.Name) }
    $boardingDestination = Join-Path $boardingTarget $boardingFile.Name
    $boardingOriginal = @($boardingBaseline | Where-Object { [IO.Path]::GetFullPath($_.Source) -eq $boardingDestination })
    if($boardingOriginal.Count -ne 1) { throw ('Missing baseline hash: ' + $boardingFile.Name) }
    $boardingCurrentHash = (Get-FileHash -LiteralPath $boardingDestination).Hash
    $boardingNewHash = (Get-FileHash -LiteralPath $boardingFile.FullName).Hash
    $boardingOldHash = $boardingOriginal[0].SHA256
    $boardingBackupFile = Join-Path $boardingBackup $boardingFile.Name
    if($boardingCurrentHash -ne $boardingOldHash -and $boardingCurrentHash -ne $boardingNewHash) {
        throw ('Production fixture changed since baseline: ' + $boardingFile.Name)
    }
    if(Test-Path -LiteralPath $boardingBackupFile) {
        if((Get-FileHash -LiteralPath $boardingBackupFile).Hash -ne $boardingOldHash) { throw ('Backup differs: ' + $boardingFile.Name) }
    } elseif($boardingCurrentHash -ne $boardingOldHash) { throw ('Updated fixture lacks original backup: ' + $boardingFile.Name) }
    [pscustomobject]@{ Fixture=$boardingFile.Name; Owner=($boardingGroups.Keys | Where-Object { $boardingFile.BaseName -in $boardingGroups[$_] }); BeforeSHA256=$boardingOldHash; AfterSHA256=$boardingNewHash; Source=$boardingFile.FullName; Destination=$boardingDestination; Backup=$boardingBackupFile }
}
# Validate every path/hash before writing any production fixture.
New-Item -ItemType Directory -Path $boardingBackup -Force | Out-Null
foreach($boardingEntry in $boardingManifest) {
    if(-not (Test-Path -LiteralPath $boardingEntry.Backup)) { Copy-Item -LiteralPath $boardingEntry.Destination -Destination $boardingEntry.Backup }
}
foreach($boardingEntry in $boardingManifest) {
    Copy-Item -LiteralPath $boardingEntry.Source -Destination $boardingEntry.Destination -Force
    if((Get-FileHash -LiteralPath $boardingEntry.Destination).Hash -ne $boardingEntry.AfterSHA256) { throw ('Copy mismatch: ' + $boardingEntry.Fixture) }
}
$boardingManifest | Export-Csv -LiteralPath (Join-Path $boardingOverlay 'merged-tests-manifest.csv') -NoTypeInformation -Encoding utf8
Write-Output 'Backed up and merged 39 reviewed fixtures. Existing metadata preserved.'
