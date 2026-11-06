param(
    [ValidatePattern('^BossFieldBehavior20261026(?:[A-Za-z0-9_-]*)$')]
    [string]$RunTag = 'BossFieldBehavior20261026'
)
$ErrorActionPreference = 'Stop'
$bossFieldRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$bossFieldArtifacts = Join-Path $bossFieldRoot 'Artifacts/BossCalibration20261024'
$bossFieldOutput = Join-Path $bossFieldArtifacts $RunTag
$bossFieldBuild = [IO.Path]::GetFullPath((Join-Path $bossFieldOutput 'Build'))
$bossFieldFixtures = @(
    'CoreNavalBossFieldBehavior20261026Tests',
    'CoreNavalBossBehaviorTests', 'CoreNavalBossCalibrationTests', 'CoreNavalBossRush20261026Tests',
    'CoreNavalDualField20261026Tests', 'CoreNavalFieldsTests', 'CoreNavalExpansionCardTests',
    'CoreTideFieldRevision20261024Tests', 'CoreThunderFieldRevision20261024Tests',
    'CoreBloodFieldRevision20261024Tests', 'CoreFireRules20261024Tests',
    'CoreArcaneNavalRevision20261024Tests', 'CoreCurseNavalRevision20261024Tests',
    'CoreBattleIntentPresentationTests', 'CoreBattleStatusPresentationTests',
    'CoreNavalMovementRamTests', 'CoreNavalMovementReachTests', 'CoreNavalSixSchoolIntegrationTests',
    'CoreBoardingFieldInheritanceTests', 'CoreBoardingFieldEnemyTests', 'CoreBoardingInheritedFieldEffectsTests',
    'CoreBoardingDualField20261026Tests', 'CoreGuidedBossScriptTests',
    'CoreTrainingBattleTests', 'CoreTrainingBattleContinuityTests'
)

function Assert-BossFieldBuildPath {
    $bossFieldExpected = [IO.Path]::GetFullPath((Join-Path $bossFieldRoot ('Artifacts/BossCalibration20261024/' + $RunTag + '/Build')))
    if ($bossFieldBuild -ne $bossFieldExpected) { throw 'Unexpected task build directory.' }
    foreach ($bossFieldDirectory in @($bossFieldRoot, (Join-Path $bossFieldRoot 'Artifacts'),
        $bossFieldArtifacts, $bossFieldOutput, $bossFieldBuild)) {
        if ((Test-Path -LiteralPath $bossFieldDirectory) -and
            ((Get-Item -LiteralPath $bossFieldDirectory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw ('Refusing task build through a directory link: ' + $bossFieldDirectory)
        }
    }
}

Assert-BossFieldBuildPath
try {
    & (Join-Path $bossFieldRoot 'tools/BossCalibration20261024/RunCoreTests.ps1') -RunTag $RunTag -Fixtures $bossFieldFixtures
} finally {
    # The synchronous compiler and test runner have returned. Retain source hashes,
    # fixtures, test results and diagnostic logs; only these two binaries are ours.
    Assert-BossFieldBuildPath
    $bossFieldBusy = @(Get-CimInstance Win32_Process | Where-Object {
        $_.CommandLine -and
        ($_.CommandLine.IndexOf($bossFieldBuild, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
         $_.CommandLine.IndexOf($bossFieldBuild.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase) -ge 0)
    })
    if ($bossFieldBusy.Count -gt 0) { throw 'A process still references the task build; cleanup deferred.' }
    $bossFieldFreed = 0L
    $bossFieldRemoved = [Collections.Generic.List[string]]::new()
    foreach ($bossFieldName in @('NavalBalanceTests.exe', 'nunit.framework.dll')) {
        $bossFieldFile = [IO.Path]::GetFullPath((Join-Path $bossFieldBuild $bossFieldName))
        if ([IO.Path]::GetDirectoryName($bossFieldFile) -ne $bossFieldBuild) { throw 'Unexpected cleanup target.' }
        if (Test-Path -LiteralPath $bossFieldFile) {
            $bossFieldItem = Get-Item -LiteralPath $bossFieldFile -Force
            if ($bossFieldItem.PSIsContainer -or ($bossFieldItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                throw ('Refusing unexpected compiler artifact: ' + $bossFieldFile)
            }
            $bossFieldFreed += $bossFieldItem.Length
            Remove-Item -LiteralPath $bossFieldFile -Force
            if (Test-Path -LiteralPath $bossFieldFile) { throw ('Cleanup failed: ' + $bossFieldFile) }
            $bossFieldRemoved.Add($bossFieldName)
        }
    }
    if ((Test-Path -LiteralPath $bossFieldBuild) -and @(Get-ChildItem -LiteralPath $bossFieldBuild -Force).Count -eq 0) {
        Remove-Item -LiteralPath $bossFieldBuild
    }
    if (Test-Path -LiteralPath $bossFieldOutput) {
        [pscustomobject]@{
            FreedBytes = $bossFieldFreed
            RemovedFiles = @($bossFieldRemoved)
            BuildRemoved = -not (Test-Path -LiteralPath $bossFieldBuild)
            Preserved = 'Test logs, results.xml, failures.txt, fixtures and source hashes'
            MainProjectCleanup = $false
            SharedDependencyRetained = 'UnityClient/Library/PackageCache NUnit; Unity installation compiler and framework references'
        } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $bossFieldOutput 'cleanup.json') -Encoding utf8
    }
    Write-Output ('Task test-cache cleanup released ' + $bossFieldFreed + ' bytes; UnityClient was not cleaned.')
}
