param(
    [string]$Output = 'D:/Game/Artifacts/VoyageEventChoiceAudit20261022/Verified'
)
$ErrorActionPreference = 'Stop'
# Compile current production Core sources and complete NUnit fixtures. The
# shared runner records source hashes and rejects edits during compilation.
& (Join-Path $PSScriptRoot '../EventLibrary20261015/RunCoreValidation.ps1') -Output $Output `
    -AdditionalFixtures @('CoreVoyageEventChoiceEffectTests.cs', 'CoreVoyageChoiceLogTests.cs',
        'CoreEventChoiceCatalogAuditTests.cs', 'CoreTreasureChoiceAuditTests.cs',
        'CoreBattleRewardDescriptionTests.cs', 'CoreBattleRewardConversionTests.cs', 'BattleRewardOverflowTests.cs')
