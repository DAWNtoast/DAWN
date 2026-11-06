$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskRuns=Join-Path $taskRoot 'outputs/boarding-field-balance-20261024'
$taskOutput=Join-Path $taskRuns 'final-analysis'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
function Key($r){@($r.boss,$r.tier,$r.rank,$r.school,$r.policy,$r.field,$r.owner,$r.duration,$r.seed)-join '|'}
function Avg($rows,$property){if(!$rows.Count){return $null};[math]::Round(($rows|ForEach-Object {[double]$_.$property}|Measure-Object -Average).Average,3)}
$taskNative=@{'boss_route_shattered_sail_reef'='fire';'boss_route_shattered_sail_reef_tide'='none';'legendary_pirate_iron_oath'='curse';'legendary_pirate_red_widow'='blood';'boss_route_golden_tide_trade'='blood';'boss_route_golden_tide_trade_thunder'='thunder';'legendary_pirate_thunder_crown'='thunder';'legendary_pirate_ember_ark'='fire';'boss_route_moon_tide_ruins'='curse';'boss_route_moon_tide_ruins_arcane'='arcane';'legendary_pirate_tide_requiem'='none';'legendary_pirate_last_horizon'='arcane';'treasure_boss_moon_vault'='none'}
function Summary($rows,$properties){
    foreach($taskG in ($rows|Group-Object -Property $properties)){
        $taskR=$taskG.Group[0];$taskWins=@($taskG.Group|Where-Object outcome -eq 'win');$taskIsBoss=$taskNative.ContainsKey($taskR.boss)
        $taskMinimum=if($taskIsBoss){5}else{3};$taskMaximum=if($taskIsBoss){8}else{5}
        [pscustomobject]@{Boss=$taskR.boss;Tier=$taskR.tier;EnemyKind=if($taskIsBoss){'boss'}else{'ordinary'};School=if($properties -contains 'school'){$taskR.school}else{'all-seven'};Field=$taskR.field;Owner=$taskR.owner;Duration=$taskR.duration;Samples=$taskG.Count;Wins=$taskWins.Count;Deaths=@($taskG.Group|Where-Object outcome -eq 'loss').Count;Timeouts=@($taskG.Group|Where-Object outcome -eq 'timeout').Count;SuccessfulMeanTurns=(Avg $taskWins 'turns');AllMeanTurns=(Avg $taskG.Group 'turns');SuccessfulMinTurns=if($taskWins.Count){($taskWins|Measure-Object turns -Minimum).Minimum}else{$null};SuccessfulMaxTurns=if($taskWins.Count){($taskWins|Measure-Object turns -Maximum).Maximum}else{$null};Target=$taskMinimum.ToString()+'-'+$taskMaximum;TargetWins=@($taskWins|Where-Object {[int]$_.turns -ge $taskMinimum -and [int]$_.turns -le $taskMaximum}).Count;PlayerMaxHp=$taskR.playerMaxHp;EnemyMaxHp=$taskR.enemyMaxHp;MeanFinalHpPercent=[math]::Round(($taskG.Group|ForEach-Object {100.0*[int]$_.playerFinalHp/[int]$_.playerMaxHp}|Measure-Object -Average).Average,3);MeanActualBurnHpLoss=(Avg $taskG.Group 'enemyBurnActualHpLoss');MeanMaximumChain=(Avg $taskG.Group 'maxChain');MeanMaximumMana=(Avg $taskG.Group 'maxMana');MeanMaximumTide=(Avg $taskG.Group 'maxTide')}
    }
}
$taskAfter=@(Import-Csv (Join-Path $taskRuns 'final-all/runs.csv'))
if($taskAfter.Count -ne 8640){throw 'Expected final 8640 samples'}
$taskBaseline=@{};$taskBaselineTags=@('baseline-final','baseline-supplemental','baseline-extra-fire','baseline-extra-defense')
foreach($taskTag in $taskBaselineTags){
    $taskDirectory=Join-Path $taskRuns $taskTag
    foreach($taskFile in @('BoardingProbe.cs','NavalBalanceProbe.cs','decks.csv','calibration-loadouts.csv')){if((Get-FileHash (Join-Path $taskDirectory $taskFile)).Hash -ne (Get-FileHash (Join-Path $taskRuns ('final-all/'+$taskFile))).Hash){throw ('Changed paired input '+$taskTag+'/'+$taskFile)}}
    foreach($taskR in (Import-Csv (Join-Path $taskDirectory 'runs.csv'))){$taskKey=Key $taskR;if($taskBaseline.ContainsKey($taskKey)){throw ('Duplicate baseline sample '+$taskKey)};$taskR|Add-Member -NotePropertyName RunTag -NotePropertyValue $taskTag;$taskBaseline[$taskKey]=$taskR}
}
$taskSeen=@{};$taskBefore=[Collections.Generic.List[object]]::new()
$taskPairs=foreach($taskR in $taskAfter){
    $taskKey=Key $taskR;if(!$taskBaseline.ContainsKey($taskKey) -or $taskSeen.ContainsKey($taskKey)){throw ('Missing/duplicate final sample '+$taskKey)};$taskSeen[$taskKey]=$true;$taskB=$taskBaseline[$taskKey];$taskBefore.Add($taskB)
    [pscustomobject]@{Boss=$taskR.boss;Tier=$taskR.tier;Rank=$taskR.rank;School=$taskR.school;Policy=$taskR.policy;Field=$taskR.field;Owner=$taskR.owner;Duration=$taskR.duration;Seed=$taskR.seed;BaselineRun=$taskB.RunTag;BeforeOutcome=$taskB.outcome;AfterOutcome=$taskR.outcome;BeforeTurns=$taskB.turns;AfterTurns=$taskR.turns;BeforePlayerMaxHp=$taskB.playerMaxHp;AfterPlayerMaxHp=$taskR.playerMaxHp;BeforeEnemyMaxHp=$taskB.enemyMaxHp;AfterEnemyMaxHp=$taskR.enemyMaxHp;BeforeFinalHpPercent=[math]::Round(100.0*[int]$taskB.playerFinalHp/[int]$taskB.playerMaxHp,3);AfterFinalHpPercent=[math]::Round(100.0*[int]$taskR.playerFinalHp/[int]$taskR.playerMaxHp,3);BeforeBurnHpLoss=$taskB.enemyBurnActualHpLoss;AfterBurnHpLoss=$taskR.enemyBurnActualHpLoss}
}
@($taskPairs)|Export-Csv (Join-Path $taskOutput 'strict-paired-8640.csv') -NoTypeInformation -Encoding utf8
$taskCombat=@($taskAfter|Where-Object school -ne 'field-only')
$taskBossNone=@($taskCombat|Where-Object {$_.field -eq 'none' -and $taskNative.ContainsKey($_.boss)})
if($taskBossNone.Count -ne 728 -or @($taskBossNone|Group-Object boss).Count -ne 13){throw 'Missing complete all13 no-field coverage'}
Summary $taskBossNone @('boss','tier')|Sort-Object Tier,Boss|Export-Csv (Join-Path $taskOutput 'all13-bosses-no-field.csv') -NoTypeInformation -Encoding utf8
Summary @($taskCombat|Where-Object {$taskNative.ContainsKey($_.boss) -and $_.field -eq $taskNative[$_.boss] -and ($_.field -eq 'none' -or $_.duration -eq '5')}) @('boss','tier','field','duration')|Sort-Object Tier,Boss|Export-Csv (Join-Path $taskOutput 'all13-bosses-native-field.csv') -NoTypeInformation -Encoding utf8
Summary @($taskCombat|Where-Object {!$taskNative.ContainsKey($_.boss)}) @('boss','tier','field','duration')|Sort-Object Tier,Boss,Field,Duration|Export-Csv (Join-Path $taskOutput 'ordinary-by-real-route-and-player-tier.csv') -NoTypeInformation -Encoding utf8
Summary $taskCombat @('boss','tier','school','field','duration')|Sort-Object Tier,Boss,School,Field,Duration|Export-Csv (Join-Path $taskOutput 'schools-fields.csv') -NoTypeInformation -Encoding utf8
Summary @($taskCombat|Where-Object {$_.school -eq 'thunder' -and $_.field -in @('none','thunder')}) @('boss','tier','school','field','duration')|Sort-Object Tier,Boss,Field|Export-Csv (Join-Path $taskOutput 'thunder-with-without-grid.csv') -NoTypeInformation -Encoding utf8
Summary @($taskAfter|Where-Object school -eq 'field-only') @('boss','tier','school','field','duration')|Sort-Object Tier,Boss,Field,Duration|Export-Csv (Join-Path $taskOutput 'passive-field-only-separate.csv') -NoTypeInformation -Encoding utf8
$taskOwner=@(Import-Csv (Join-Path $taskRuns 'final-owner-combinations/runs.csv'))
if($taskOwner.Count -ne 1800){throw 'Expected 1800 ownership combination samples'}
Summary @($taskOwner|Where-Object school -ne 'field-only') @('boss','tier','field','owner','duration')|Sort-Object Tier,Boss,Field,Owner|Export-Csv (Join-Path $taskOutput 'owner-combinations-combat.csv') -NoTypeInformation -Encoding utf8
Summary @($taskOwner|Where-Object school -eq 'field-only') @('boss','tier','school','field','owner','duration')|Sort-Object Tier,Boss,Field,Owner|Export-Csv (Join-Path $taskOutput 'owner-combinations-passive-separate.csv') -NoTypeInformation -Encoding utf8
$taskSingle=@(Import-Csv (Join-Path $taskRuns 'final-single-card/runs.csv'))
if($taskSingle.Count -ne 160){throw 'Expected 160 single-card samples'}
Summary $taskSingle @('boss','tier','school','field','duration')|Sort-Object Tier,Boss,School,Field|Export-Csv (Join-Path $taskOutput 'single-burn-pot-separate.csv') -NoTypeInformation -Encoding utf8
$taskSingleBefore=@{};$taskSingleSeen=@{}
foreach($taskFile in @('BoardingProbe.cs','NavalBalanceProbe.cs','decks.csv','calibration-loadouts.csv')){if((Get-FileHash (Join-Path $taskRuns ('baseline-single-final/'+$taskFile))).Hash -ne (Get-FileHash (Join-Path $taskRuns ('final-single-card/'+$taskFile))).Hash){throw ('Changed single-card paired input '+$taskFile)}}
foreach($taskR in (Import-Csv (Join-Path $taskRuns 'baseline-single-final/runs.csv'))){$taskKey=Key $taskR;if($taskSingleBefore.ContainsKey($taskKey)){throw 'Duplicate single-card baseline key'};$taskSingleBefore[$taskKey]=$taskR}
$taskSinglePairs=foreach($taskR in $taskSingle){$taskKey=Key $taskR;if(!$taskSingleBefore.ContainsKey($taskKey) -or $taskSingleSeen.ContainsKey($taskKey)){throw ('Missing/duplicate single-card key '+$taskKey)};$taskSingleSeen[$taskKey]=$true;$taskB=$taskSingleBefore[$taskKey];[pscustomobject]@{Key=$taskKey;BeforeOutcome=$taskB.outcome;AfterOutcome=$taskR.outcome;BeforeTurns=$taskB.turns;AfterTurns=$taskR.turns;BeforeBurnHpLoss=$taskB.enemyBurnActualHpLoss;AfterBurnHpLoss=$taskR.enemyBurnActualHpLoss}}
if($taskSingleBefore.Count -ne $taskSingle.Count){throw 'Unexpected unpaired single-card baseline rows'}
$taskSinglePairs|Export-Csv (Join-Path $taskOutput 'single-burn-pot-strict-paired-160.csv') -NoTypeInformation -Encoding utf8
$taskCoverage=foreach($taskVersion in @('before','after')){
    $taskVersionRows=if($taskVersion -eq 'before'){@($taskBefore)}else{$taskAfter}
    foreach($taskScope in @('boss-no-field','boss-all-fields','ordinary-no-field','ordinary-all-fields')){
        $taskWantBoss=$taskScope.StartsWith('boss');$taskNoField=$taskScope.EndsWith('no-field')
        $taskSelected=@($taskVersionRows|Where-Object {$_.school -ne 'field-only' -and $taskNative.ContainsKey($_.boss) -eq $taskWantBoss -and (!$taskNoField -or $_.field -eq 'none')})
        $taskLow=if($taskWantBoss){5}else{3};$taskHigh=if($taskWantBoss){8}else{5};$taskWins=@($taskSelected|Where-Object outcome -eq 'win')
        [pscustomobject]@{Version=$taskVersion;Scope=$taskScope;Samples=$taskSelected.Count;Wins=$taskWins.Count;Deaths=@($taskSelected|Where-Object outcome -eq 'loss').Count;Timeouts=@($taskSelected|Where-Object outcome -eq 'timeout').Count;Target=$taskLow.ToString()+'-'+$taskHigh;TargetWins=@($taskWins|Where-Object {[int]$_.turns -ge $taskLow -and [int]$_.turns -le $taskHigh}).Count;SuccessfulMeanTurns=(Avg $taskWins 'turns')}
    }
}
$taskCoverage|Export-Csv (Join-Path $taskOutput 'target-coverage-before-after.csv') -NoTypeInformation -Encoding utf8
$taskCoreChecks=0
foreach($taskRun in @('final-all','final-owner-combinations','final-single-card')){foreach($taskH in (Import-Csv (Join-Path $taskRuns ($taskRun+'/source-hashes.csv')))){if((Get-FileHash $taskH.Snapshot).Hash -ne $taskH.SHA256 -or (Get-FileHash (Join-Path $taskRuns ('experiments/combined-c/'+[IO.Path]::GetFileName($taskH.Source)))).Hash -ne $taskH.SHA256){throw 'Final snapshot changed or differs from combined-c'};$taskCoreChecks++}}
[pscustomobject]@{FinalPairedSamples=$taskAfter.Count;BaselineRuns=$taskBaselineTags;All13BossesNoFieldSamples=$taskBossNone.Count;CombatSamples=$taskCombat.Count;PassiveSamples=$taskAfter.Count-$taskCombat.Count;OwnerCombinationSamples=$taskOwner.Count;SingleCardSamples=$taskSingle.Count;SingleCardWins=@($taskSingle|Where-Object outcome -eq 'win').Count;FrozenCoreHashChecks=$taskCoreChecks;Source='combined-c';PopulationClaim=$false;ScopeNotes=@('Three real ordinary encounters at five player loadouts; not five invented enemy tiers.','Tide enemy native-field entries use none because directional current is not inherited.','Passive and single-card strategies remain separate from combat target coverage.','Controlled adjacent-cell field injection does not include naval placement/access costs.','Mixed ownership means first field player-owned, second field enemy-owned.')}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $taskOutput 'validation-summary.json') -Encoding utf8
$taskCoverage|Format-Table
Write-Output ('PASS complete final analysis: '+$taskOutput)
