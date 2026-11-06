$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskRuns=Join-Path $taskRoot 'outputs/boarding-field-balance-20261024'
$taskOutput=Join-Path $taskRuns 'candidate-b-analysis'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
function Key($r){@($r.boss,$r.tier,$r.rank,$r.school,$r.policy,$r.field,$r.owner,$r.duration,$r.seed)-join '|'}
function Avg($rows,$property){if(!$rows.Count){return $null};[math]::Round(($rows|ForEach-Object {[double]$_.$property}|Measure-Object -Average).Average,3)}
$taskNative=@{'boss_route_shattered_sail_reef'='fire';'boss_route_shattered_sail_reef_tide'='tide';'legendary_pirate_iron_oath'='curse';'legendary_pirate_red_widow'='blood';'boss_route_golden_tide_trade'='blood';'boss_route_golden_tide_trade_thunder'='thunder';'legendary_pirate_thunder_crown'='thunder';'legendary_pirate_ember_ark'='fire';'boss_route_moon_tide_ruins'='curse';'boss_route_moon_tide_ruins_arcane'='arcane';'legendary_pirate_tide_requiem'='tide';'legendary_pirate_last_horizon'='arcane';'treasure_boss_moon_vault'='none'}
$taskPairCounts=[Collections.Generic.List[object]]::new();$taskAfter=[Collections.Generic.List[object]]::new();$taskBefore=[Collections.Generic.List[object]]::new()
foreach($taskPair in @(@('baseline-final','candidate-b-final'),@('baseline-supplemental','candidate-b-supplemental'),@('baseline-single-final','candidate-b-single-final'))){
    $taskOldDir=Join-Path $taskRuns $taskPair[0];$taskNewDir=Join-Path $taskRuns $taskPair[1]
    foreach($taskFile in @('BoardingProbe.cs','NavalBalanceProbe.cs','decks.csv','calibration-loadouts.csv')){if((Get-FileHash (Join-Path $taskOldDir $taskFile)).Hash -ne (Get-FileHash (Join-Path $taskNewDir $taskFile)).Hash){throw ('Changed paired input: '+$taskFile)}}
    $taskMap=@{};$taskSeen=@{};$taskOld=@(Import-Csv (Join-Path $taskOldDir 'runs.csv'));$taskNew=@(Import-Csv (Join-Path $taskNewDir 'runs.csv'))
    foreach($taskR in $taskOld){$taskKey=Key $taskR;if($taskMap.ContainsKey($taskKey)){throw 'Duplicate old sample'};$taskMap[$taskKey]=$taskR;$taskR|Add-Member -NotePropertyName RunTag -NotePropertyValue $taskPair[0];$taskBefore.Add($taskR)}
    foreach($taskR in $taskNew){$taskKey=Key $taskR;if(!$taskMap.ContainsKey($taskKey) -or $taskSeen.ContainsKey($taskKey)){throw 'Missing/duplicate new sample'};$taskSeen[$taskKey]=$true;$taskR|Add-Member -NotePropertyName RunTag -NotePropertyValue $taskPair[1];$taskAfter.Add($taskR)}
    if($taskOld.Count -ne $taskNew.Count){throw 'Different paired sample count'}
    $taskPairCounts.Add([pscustomobject]@{Before=$taskPair[0];After=$taskPair[1];StrictlyPaired=$taskNew.Count;ProbeAndDecksIdentical=$true})
}
$taskCombat=@($taskAfter|Where-Object {$_.school -notin @('field-only','single-burn','single-pot')})
$taskBossNone=@($taskCombat|Where-Object {$_.field -eq 'none' -and $taskNative.ContainsKey($_.boss)})
if($taskBossNone.Count -ne 728){throw 'Expected 13 bosses x 7 schools x 2 policies x 4 seeds'}
function Summarize-Groups($rows,$properties){
    foreach($taskG in ($rows|Group-Object -Property $properties)){
        $taskR=$taskG.Group[0];$taskWins=@($taskG.Group|Where-Object outcome -eq 'win')
        [pscustomobject]@{Boss=$taskR.boss;Tier=$taskR.tier;School=if($properties -contains 'school'){$taskR.school}else{'all-seven'};Field=$taskR.field;Duration=$taskR.duration;Samples=$taskG.Count;Wins=$taskWins.Count;Deaths=@($taskG.Group|Where-Object outcome -eq 'loss').Count;Timeouts=@($taskG.Group|Where-Object outcome -eq 'timeout').Count;SuccessfulMeanTurns=(Avg $taskWins 'turns');SuccessfulMinTurns=if($taskWins.Count){($taskWins|Measure-Object turns -Minimum).Minimum}else{$null};SuccessfulMaxTurns=if($taskWins.Count){($taskWins|Measure-Object turns -Maximum).Maximum}else{$null};Wins5to8=@($taskWins|Where-Object {[int]$_.turns -ge 5 -and [int]$_.turns -le 8}).Count;PlayerMaxHp=$taskR.playerMaxHp;EnemyMaxHp=$taskR.enemyMaxHp;MeanFinalHpPercent=[math]::Round(($taskG.Group|ForEach-Object {100.0*[int]$_.playerFinalHp/[int]$_.playerMaxHp}|Measure-Object -Average).Average,3);MeanActualBurnHpLoss=(Avg $taskG.Group 'enemyBurnActualHpLoss');MeanMaximumChain=(Avg $taskG.Group 'maxChain')}
    }
}
Summarize-Groups $taskBossNone @('boss','tier')|Sort-Object Tier,Boss|Export-Csv (Join-Path $taskOutput 'all-bosses-none.csv') -NoTypeInformation -Encoding utf8
Summarize-Groups @($taskCombat|Where-Object {$taskNative.ContainsKey($_.boss) -and $_.field -eq $taskNative[$_.boss]}) @('boss','tier','field','duration')|Sort-Object Tier,Boss|Export-Csv (Join-Path $taskOutput 'all-bosses-native-field.csv') -NoTypeInformation -Encoding utf8
Summarize-Groups $taskCombat @('boss','tier','school','field','duration')|Sort-Object Tier,Boss,School,Field,Duration|Export-Csv (Join-Path $taskOutput 'school-fields.csv') -NoTypeInformation -Encoding utf8
Summarize-Groups @($taskCombat|Where-Object {$_.school -eq 'thunder' -and $_.field -in @('none','thunder')}) @('boss','tier','school','field','duration')|Sort-Object Tier,Boss,Field|Export-Csv (Join-Path $taskOutput 'thunder-with-without-field.csv') -NoTypeInformation -Encoding utf8
Summarize-Groups @($taskAfter|Where-Object school -eq 'field-only') @('boss','tier','school','field','duration')|Sort-Object Tier,Boss,Field,Duration|Export-Csv (Join-Path $taskOutput 'field-only.csv') -NoTypeInformation -Encoding utf8
$taskSingle=foreach($taskVersion in @('before','after')){
    $taskSource=if($taskVersion -eq 'before'){$taskBefore}else{$taskAfter}
    foreach($taskRow in (Summarize-Groups @($taskSource|Where-Object {$_.school -in @('single-burn','single-pot')}) @('boss','tier','school','field','duration'))){$taskRow|Add-Member -NotePropertyName Version -NotePropertyValue $taskVersion;$taskRow}
}
$taskSingle|Export-Csv (Join-Path $taskOutput 'single-card-before-after.csv') -NoTypeInformation -Encoding utf8
[pscustomobject]@{Pairs=@($taskPairCounts);TotalPaired=($taskPairCounts|Measure-Object StrictlyPaired -Sum).Sum;All13BossesNoFieldSamples=$taskBossNone.Count;Sources='Frozen combined-b: profile-b (last horizon HP750), card-a; old frozen Inputs/CoreSnapshot';PopulationClaim=$false}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $taskOutput 'paired-integrity.json') -Encoding utf8
Write-Output ('PASS: '+($taskPairCounts|Measure-Object StrictlyPaired -Sum).Sum+' strict pairs; all13 bosses 728 no-field samples; '+$taskOutput)
