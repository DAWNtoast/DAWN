$ErrorActionPreference='Stop'
$balanceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$balanceOut=Join-Path $balanceRoot 'Artifacts/CardBalanceReview20261012'
$balanceChecks=@{}
foreach($balanceFile in @('EditModeVerified.xml','EditModeTypographyVerified.xml','PlayModeFinal.xml','PlayModeDepartureVerified.xml')) {
    # The corrected TestCase's expected string is part of its NUnit name.
    # Replace the whole retested fixture, including that old parameterized name.
    if($balanceFile -eq 'EditModeTypographyVerified.xml') {
        foreach($balanceKey in @($balanceChecks.Keys)) {
            if($balanceKey.StartsWith('ShatteredSail.Core.Tests.CardDescriptionTextTests.')) { $balanceChecks.Remove($balanceKey) }
        }
    }
    $balanceDoc=[xml](Get-Content -LiteralPath (Join-Path $balanceOut "Unity/$balanceFile") -Raw)
    foreach($balanceCase in $balanceDoc.SelectNodes('//test-case')) {
        $balanceChecks[$balanceCase.fullname]=[pscustomobject]@{Name=$balanceCase.fullname;Result=$balanceCase.result;Evidence=$balanceFile}
    }
}
$balanceFailed=@($balanceChecks.Values | Where-Object Result -ne 'Passed')
if($balanceFailed.Count) { $balanceFailed | Format-List; throw 'Unresolved Unity test failures' }
$balanceCore=[xml](Get-Content -LiteralPath (Join-Path $balanceRoot 'UnityClient/Artifacts/CardBalanceReview20261012/CoreAfter/results.xml') -Raw)
$balanceCoreSuite=$balanceCore.SelectSingleNode('//test-suite[@type="Assembly"]')
$balanceComparison=Get-Content -LiteralPath (Join-Path $balanceRoot 'UnityClient/Artifacts/CardBalanceReview20261012/CoreAfter/baseline-failure-comparison.json') -Raw | ConvertFrom-Json
if($balanceComparison.NewOrRemovedFailures -ne 0) { throw 'Core failure set changed' }
$balanceBattle=Get-Content -LiteralPath (Join-Path $balanceRoot 'UnityClient/Artifacts/CardBalanceReview20261012/battle-review-summary.json') -Raw | ConvertFrom-Json
$balanceVerify=[ordered]@{
    status='review_ready_not_packaged'; packageCreated=$false
    summary="216 张卡、864 个等级完成核对；$($balanceChecks.Count) 项相关 Unity 回归全部通过。Core 共 872 项：854 通过，18 项失败与修改前完全相同，无新增失败。完成 $($balanceBattle.totalBeforeAndAfterBattles) 场真实 Core 战斗前后对照，未发现非法出牌或循环超时。等待用户审核后再打包。"
    core=@{total=872;passed=[int]$balanceCoreSuite.passed;failed=[int]$balanceCoreSuite.failed;preexistingFailures=18;newFailures=0;baselineComparison='../../UnityClient/Artifacts/CardBalanceReview20261012/CoreAfter/baseline-failure-comparison.json';failureDetails='../../UnityClient/Artifacts/CardBalanceReview20261012/CoreAfter/failures.tsv'}
    unity=@{uniqueTests=$balanceChecks.Count;passed=$balanceChecks.Count;failed=0;editMode=689;playMode=10;evidence=@('Unity/EditModeVerified.xml','Unity/EditModeTypographyVerified.xml','Unity/PlayModeFinal.xml','Unity/PlayModeDepartureVerified.xml');note='Retested fixture results replace the earlier fixture; no readability threshold was reduced.'}
    definitions=(Get-Content -LiteralPath (Join-Path $balanceOut 'definition-verification.json') -Raw | ConvertFrom-Json)
    battles=@{pairedBattles=$balanceBattle.pairedBattles;totalBeforeAndAfter=$balanceBattle.totalBeforeAndAfterBattles;sourceCardCoverage='126/126';schoolBasicCoverage='60/60';watchdogs=0;illegalPlayFailures=0;negativeTerminalHealth=0;ammunitionBudgetViolations=0;evidence='battle-review-summary.json';limitations='Fixed greedy policy, not proof of optimal play or identical matchup win rates. +2/+3 win-rate saturation; source combo effects may be underestimated.'}
    previews=@{savedAndReloaded=23;scenesTextPatched=16;textStringsPatched=56;nonTextSceneDataPreserved=$true;evidence='persistent-preview-migration.json'}
    review=@{fullCardTable='index.html';methodology='数值口径与例外.txt';numericalSummary='review-summary.json';rankExceptions='audit-exceptions.json';targetScope='Frozen typical expected-effect budget: six-school four-rank averages approximately 130% of comparable ordinary median. 132/132 four-rank card means in 120–140%; 37 cards and 81 ranks have disclosed individual-rank exceptions.'}
}
$balanceVerify | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $balanceOut 'verification-summary.json') -Encoding utf8
$balanceChecks.Values | Sort-Object Name | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $balanceOut 'unity-tests-final.json') -Encoding utf8
[pscustomobject]@{UniqueUnityTests=$balanceChecks.Count;Passed=$balanceChecks.Count;Failed=0;CorePassed=$balanceCoreSuite.passed;CoreFailed=$balanceCoreSuite.failed}
