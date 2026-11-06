param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$RunTag,
    [ValidatePattern('^[A-Za-z0-9_-]+$')][string]$BaselineTag='baseline-small-v2'
)
$ErrorActionPreference='Stop'
$taskWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskRoot=Join-Path $taskWorkspace 'outputs/boarding-field-balance-20261024'
$taskOutput=Join-Path $taskRoot $RunTag
$taskRows=@(Import-Csv -LiteralPath (Join-Path $taskOutput 'runs.csv'))
function Average($rows,$column){if(!$rows.Count){return $null};[math]::Round(($rows|ForEach-Object {[double]$_.$column}|Measure-Object -Average).Average,3)}
function SampleKey($r){@($r.boss,$r.tier,$r.rank,$r.school,$r.policy,$r.field,$r.owner,$r.duration,$r.seed)-join '|'}
$taskGroups=foreach($taskGroup in ($taskRows|Group-Object boss,tier,field,duration,owner)){
    $taskR=$taskGroup.Group[0]
    $taskCombat=@($taskGroup.Group|Where-Object {$_.school -notin @('field-only','single-burn','single-pot')})
    $taskWins=@($taskCombat|Where-Object outcome -eq 'win')
    if(!$taskCombat.Count){continue}
    [pscustomobject]@{Boss=$taskR.boss;Tier=$taskR.tier;Field=$taskR.field;Duration=$taskR.duration;Owner=$taskR.owner;Samples=$taskCombat.Count;Wins=$taskWins.Count;Deaths=@($taskCombat|Where-Object outcome -eq 'loss').Count;Timeouts=@($taskCombat|Where-Object outcome -eq 'timeout').Count;TurnsAll=(Average $taskCombat 'turns');TurnsWins=(Average $taskWins 'turns');Wins3to5=@($taskWins|Where-Object {[int]$_.turns -ge 3 -and [int]$_.turns -le 5}).Count;Wins5to8=@($taskWins|Where-Object {[int]$_.turns -ge 5 -and [int]$_.turns -le 8}).Count;MeanFinalHpPercent=[math]::Round(($taskCombat|ForEach-Object {100.0*[int]$_.playerFinalHp/[int]$_.playerMaxHp}|Measure-Object -Average).Average,3);MeanBurnHpLoss=(Average $taskCombat 'enemyBurnActualHpLoss');MeanCardWindowHpLoss=(Average $taskCombat 'cardWindowEnemyHpLoss')}
}
$taskGroups | Export-Csv -LiteralPath (Join-Path $taskOutput 'audit-overview.csv') -NoTypeInformation -Encoding utf8
if($RunTag -ne $BaselineTag){
    $taskBaseline=@{};foreach($taskR in (Import-Csv -LiteralPath (Join-Path (Join-Path $taskRoot $BaselineTag) 'runs.csv'))){$taskKey=SampleKey $taskR;if($taskBaseline.ContainsKey($taskKey)){throw "Duplicate baseline sample $taskKey"};$taskBaseline[$taskKey]=$taskR}
    $taskPaired=foreach($taskR in $taskRows){
        $taskKey=SampleKey $taskR;if(!$taskBaseline.ContainsKey($taskKey)){continue};$taskB=$taskBaseline[$taskKey]
        [pscustomobject]@{Boss=$taskR.boss;Tier=$taskR.tier;Rank=$taskR.rank;School=$taskR.school;Policy=$taskR.policy;Field=$taskR.field;Duration=$taskR.duration;Owner=$taskR.owner;Seed=$taskR.seed;BeforeOutcome=$taskB.outcome;AfterOutcome=$taskR.outcome;BeforeTurns=$taskB.turns;AfterTurns=$taskR.turns;BeforePlayerMaxHp=$taskB.playerMaxHp;AfterPlayerMaxHp=$taskR.playerMaxHp;BeforeEnemyMaxHp=$taskB.enemyMaxHp;AfterEnemyMaxHp=$taskR.enemyMaxHp;BeforeFinalHpPercent=[math]::Round(100.0*[int]$taskB.playerFinalHp/[int]$taskB.playerMaxHp,3);AfterFinalHpPercent=[math]::Round(100.0*[int]$taskR.playerFinalHp/[int]$taskR.playerMaxHp,3);BeforeBurnHpLoss=$taskB.enemyBurnActualHpLoss;AfterBurnHpLoss=$taskR.enemyBurnActualHpLoss;BeforePlays=$taskB.plays;AfterPlays=$taskR.plays}
    }
    @($taskPaired)|Export-Csv -LiteralPath (Join-Path $taskOutput 'audit-paired.csv') -NoTypeInformation -Encoding utf8
    Write-Output ('Paired '+@($taskPaired).Count+'/'+$taskRows.Count+' samples against '+$BaselineTag)
}
Write-Output ('Summarized '+$taskRows.Count+' samples in '+$taskOutput)
