$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutput=Join-Path $taskRoot 'outputs/boarding-field-balance-20261024'
$taskBefore=Get-Content -LiteralPath (Join-Path $taskOutput 'Inputs/Catalog/card-list-data.json') -Raw|ConvertFrom-Json
$taskAfter=Get-Content -LiteralPath (Join-Path $taskOutput 'VerifiedCatalog/card-list-data.json') -Raw|ConvertFrom-Json
$taskUpBefore=Get-Content -LiteralPath (Join-Path $taskOutput 'Inputs/Catalog/card-upgrade-data.json') -Raw|ConvertFrom-Json
$taskUpAfter=Get-Content -LiteralPath (Join-Path $taskOutput 'VerifiedCatalog/card-upgrade-data.json') -Raw|ConvertFrom-Json
function Cell($value){([string]$value).Replace('|','\|').Replace("`r",'').Replace("`n",'<br>')}
$taskLines=[Collections.Generic.List[string]]::new()
$taskLines.Add('# 接舷卡牌调整明细')
$taskLines.Add('')
$taskLines.Add('2026-10-24。由调整前快照与最终生产导出逐项比较。只列接舷阶段；完整基础、强化卡面见同目录 VerifiedCatalog。场地规则和 23 个关键词定义不变，本轮没有新增卡牌或关键词。')
$taskLines.Add('')
$taskChanges=[Collections.Generic.List[object]]::new()
foreach($taskCard in $taskAfter.Cards){
    $taskCurrent=$taskCard.StageVariants|Where-Object {$_.Stages -contains '接舷'}
    if(!$taskCurrent){continue}
    $taskPrevious=($taskBefore.Cards|Where-Object Id -eq $taskCard.Id).StageVariants|Where-Object {$_.Stages -contains '接舷'}
    if(!$taskPrevious){throw ('Unexpected new card '+$taskCard.Id)}
    if($taskPrevious.Energy -ceq $taskCurrent.Energy -and $taskPrevious.OtherCost -ceq $taskCurrent.OtherCost -and $taskPrevious.Effect -ceq $taskCurrent.Effect){continue}
    $taskChanges.Add([pscustomobject]@{Id=$taskCard.Id;Number=$taskCard.Number;Name=$taskCard.Name;School=$taskCard.School;Source=$taskCard.Source;BeforeEnergy=$taskPrevious.Energy;AfterEnergy=$taskCurrent.Energy;BeforeCost=$taskPrevious.OtherCost;AfterCost=$taskCurrent.OtherCost;BeforeEffect=$taskPrevious.Effect;AfterEffect=$taskCurrent.Effect})
}
$taskLines.Add('共 '+$taskChanges.Count+' 个已公开卡牌身份的接舷基础卡面发生变化（同机制的来源牌分别计数）。准备与海战的 236 个阶段定义及全部强化等级逐项保持一致。')
$taskChanges|Export-Csv -LiteralPath (Join-Path $taskOutput 'boarding-card-changes.csv') -NoTypeInformation -Encoding utf8
foreach($taskSchool in @('普通','火焰','秘术','诅咒','潮汐','雷霆','血契')){
    $taskRows=@($taskChanges|Where-Object School -eq $taskSchool)
    $taskLines.Add('');$taskLines.Add('## '+$taskSchool);$taskLines.Add('')
    $taskLines.Add('| 编号／名称 | 能量／其他消耗：旧 → 新 | 旧效果 | 新效果 |')
    $taskLines.Add('|---|---|---|---|')
    foreach($taskRow in $taskRows){$taskLines.Add('| '+$taskRow.Number+' '+$taskRow.Name+' | '+(Cell ($taskRow.BeforeEnergy+'／'+$taskRow.BeforeCost+' → '+$taskRow.AfterEnergy+'／'+$taskRow.AfterCost))+' | '+(Cell $taskRow.BeforeEffect)+' | '+(Cell $taskRow.AfterEffect)+' |')}
}
$taskLines.Add('');$taskLines.Add('## 强化上限与资源曲线');$taskLines.Add('')
$taskLines.Add('| 名称 | 强化上限：旧 → 新 | 各级新卡面（+0 起） |');$taskLines.Add('|---|---|---|')
foreach($taskCard in $taskUpAfter.Cards){
    $taskStage=$taskCard.Stages|Where-Object Stage -eq '接舷'
    if(!$taskStage){continue}
    $taskOld=($taskUpBefore.Cards|Where-Object Id -eq $taskCard.Id).Stages|Where-Object Stage -eq '接舷'
    if($taskStage.MaximumLevel -eq $taskOld.MaximumLevel -and $taskCard.Id -notin @('card_boarding_fire_03','card_boarding_fire_04','card_boarding_arcane_01','card_boarding_arcane_02','card_boarding_curse_01','card_boarding_curse_03','card_boarding_basic_07','card_boarding_tide_12')){continue}
    $taskFaces=@($taskStage.Levels|ForEach-Object {'+'+$_.Level+'：'+(Cell $_.Effect)})-join '<br>'
    $taskLines.Add('| '+$taskCard.Name+' | +'+$taskOld.MaximumLevel+' → +'+$taskStage.MaximumLevel+' | '+$taskFaces+' |')
}
$taskLines.Add('');$taskLines.Add('持续伤害和持续恢复使用独立层数曲线，避免把总收益当成线性数值处理；实际挡伤生成的潮势、实际扣血吸血、场地层数、连锁门槛和咒印即死比例没有附加翻倍。')
[IO.File]::WriteAllLines((Join-Path $taskOutput '接舷卡牌调整明细.md'),$taskLines,[Text.UTF8Encoding]::new($false))
Write-Output ('Exported '+$taskChanges.Count+' changed boarding card faces.')
