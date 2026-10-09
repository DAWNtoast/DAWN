param(
    [string]$Root = 'D:/Game/UnityClient/Artifacts/ShipProgression20261003',
    [string]$Output = ''
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Output)) { $Output = Join-Path $Root 'LogSummary' }
$Root = [IO.Path]::GetFullPath($Root)
$Output = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$culture = [Globalization.CultureInfo]::InvariantCulture
$segments = [Collections.Generic.List[object]]::new()
$transactions = [Collections.Generic.List[object]]::new()
$voyages = [Collections.Generic.List[object]]::new()
$branches = [Collections.Generic.List[object]]::new()
$warnings = [Collections.Generic.List[string]]::new()

function Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return $Default }
    return $property.Value
}
function Amount($Value) { return [decimal]::Parse([string]$Value, $culture) }
function Sum-Column($Rows, [string]$Column) {
    [decimal]$sum = 0
    foreach ($row in $Rows) { $sum += [decimal](Field $row $Column 0) }
    return $sum
}
function Read-SharedSnapshot([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        # Fix the byte length once, so a running game's append-only log is a bounded snapshot.
        $bytes = [byte[]]::new($stream.Length)
        $position = 0
        while ($position -lt $bytes.Length) {
            $read = $stream.Read($bytes, $position, $bytes.Length - $position)
            if ($read -eq 0) { break }
            $position += $read
        }
        return [Text.Encoding]::UTF8.GetString($bytes, 0, $position).TrimStart([char]0xFEFF)
    } finally { $stream.Dispose() }
}
function Add-Transaction($Run, $Event, $Kind, $Item, $Quantity, [decimal]$Gold) {
    $transactions.Add([pscustomobject]@{
        Run=$Run; Sequence=$Event.sequence; Cycle=$Event.cycle; Utc=$Event.utc;
        Port=$Event.port; Kind=$Kind; Item=$Item; Quantity=$Quantity; Gold=$Gold;
        ShipIndex=$Event.ship; Evidence=$Event.action
    })
}

$runDirectories = @(Get-ChildItem -LiteralPath $Root -Directory | Where-Object {
    $_.Name -match '^(Baseline-NoSwap|Production-NoSwap|Production-OneSwap|Production-TwoSwap)(-|$)'
})
foreach ($directory in $runDirectories) {
    $logs = @(Get-ChildItem -LiteralPath $directory.FullName -Filter 'strict-actions.jsonl' -File -Recurse)
    if ($logs.Count -eq 0) { continue }
    if ($logs.Count -gt 1) { $warnings.Add("Multiple logs in $($directory.Name); each is reported separately.") }
    foreach ($log in $logs) {
        $run = $directory.Name
        if ($logs.Count -gt 1) { $run += '/' + $log.Directory.Name }
        $events = [Collections.Generic.List[object]]::new()
        $lines = (Read-SharedSnapshot $log.FullName) -split "`n"
        for ($i=0; $i -lt $lines.Length; $i++) {
            if ([string]::IsNullOrWhiteSpace($lines[$i])) { continue }
            try { $events.Add(($lines[$i] | ConvertFrom-Json)) }
            catch { $warnings.Add("$run line $($i+1) not complete/valid JSON; ignored in this snapshot.") }
        }
        if ($events.Count -eq 0) { continue }
        $first=$events[0]; $last=$events[$events.Count-1]
        $branch = [regex]::Match($directory.Name, '^(Baseline-NoSwap|Production-NoSwap|Production-OneSwap|Production-TwoSwap)').Value
        $pendingClick=$null; $previous=$null; $transitionOrigin=''; $transitionButton=''
        $battleEncounter=''; $lastReturnButton=''; $reserveReturn=$false
        [decimal]$portSpent=0; [decimal]$travelPrincipal=0
        $gm=0; $departures=0; $ended=$false; $stop=''; $finalMarker=''; $operatorStopped=$false
        $resumeDeclared=[string]$first.action -match '^Resume exact earned settled checkpoint;'
        foreach ($event in $events) {
            $action=[string]$event.action
            if ($action -match '^PORT CYCLE ' -or $action -eq 'After click Start Voyage') {
                $transitionOrigin=''; $transitionButton=''; $lastReturnButton=''; $reserveReturn=$false; $battleEncounter=''
            }
            if ($event.phase -eq 'Battle' -and $event.encounter) { $battleEncounter=$event.encounter }
            if ($null -ne $previous -and $previous.phase -ne 'Settlement' -and $event.phase -eq 'Settlement') {
                $transitionOrigin=$previous.phase
                $transitionButton=if($action.StartsWith('After click ')){$action.Substring(12)}else{$action}
            }
            if ($action -match '^Return on public supply/hull reserve:|^Cannot fund displayed route;') { $reserveReturn=$true }
            if ($action -match '^Before click (.+)$') { $pendingClick=$event }
            if ($action -match '^After click (.+)$') {
                $button=$Matches[1]
                if ($button -eq 'Game Master Refresh Market') { $gm++ }
                if ($button -eq 'Start Voyage' -and $event.phase -eq 'Sailing') { $departures++ }
                if ($button -match '^(Safe Return|Retreat Battle|Emergency Encounter Return|Emergency Battle Return)$' -and $event.phase -eq 'Settlement') {
                    $lastReturnButton=$button
                }
                if ($null -ne $pendingClick -and $pendingClick.action -eq ('Before click '+$button)) {
                    [decimal]$spent=(Amount $pendingClick.gold)-(Amount $event.gold)
                    if ($spent -gt 0 -and $pendingClick.phase -eq 'Port') {
                        if ($button -eq 'Start Voyage') { $travelPrincipal += $spent }
                        elseif ($event.phase -eq 'Port') {
                            $portSpent += $spent
                            if ($button -eq 'Confirm Shipyard Vessel') { Add-Transaction $run $event 'ShipPurchase' ([string]$event.ship) 1 $spent }
                            elseif ($button -eq 'Confirm Battle Card Upgrade') { Add-Transaction $run $event 'CardUpgrade' '' 1 $spent }
                            elseif ($button -match '^Recruit Tavern Crew ') { Add-Transaction $run $event 'CrewRecruitment' $button 1 $spent }
                            elseif ($button -notmatch '^Market Buy ') { Add-Transaction $run $event 'OtherPortSpending' $button 1 $spent }
                        }
                    }
                    $pendingClick=$null
                }
            }
            if ($action -match '^(Trade|Provision) purchase (\w+) x(\d+); paid=([0-9.]+)') {
                Add-Transaction $run $event ($Matches[1]+'Purchase') $Matches[2] ([int]$Matches[3]) (Amount $Matches[4])
            }
            if ($action -match '^Real market sale (\w+) x(\d+); received=([0-9.]+)') {
                Add-Transaction $run $event 'MarketSaleMixedSources' $Matches[1] ([int]$Matches[2]) (Amount $Matches[3])
            }
            if ($action -match '^SETTLEMENT outcome=(\w+);') {
                $outcome=$Matches[1]; $classification=$outcome
                if ($outcome -eq 'Failure') {
                    if ($lastReturnButton -match '^Emergency') { $classification='EmergencyTowNonCombatDefeat' }
                    elseif ($lastReturnButton -eq 'Safe Return') { $classification='SupplyTowAfterReturn' }
                    elseif ($transitionOrigin -eq 'Battle' -and $transitionButton -notmatch 'Retreat|Emergency') { $classification='ConfirmedCombatDefeat' }
                    else { $classification='FailureCauseUnconfirmed' }
                } elseif ($lastReturnButton -eq 'Retreat Battle') { $classification='VoluntaryBattleRetreat' }
                elseif ($outcome -eq 'SafeReturn' -and $reserveReturn) { $classification='ReserveSafeReturn' }
                $voyages.Add([pscustomobject]@{
                    Run=$run; Cycle=$event.cycle; Sequence=$event.sequence; Utc=$event.utc;
                    Outcome=$outcome; Classification=$classification; ReturnButton=$lastReturnButton;
                    TransitionOrigin=$transitionOrigin; TransitionButton=$transitionButton;
                    BattleEncounter=if($transitionOrigin -eq 'Battle'){$battleEncounter}else{''};
                    DistanceNm=$event.distance; EffectiveVoyage=([double]$event.distance -ge 1);
                    Hull=$event.hull; Water=$event.water; Food=$event.food; Evidence=$action
                })
            }
            if ($action -eq 'RUN ENDED') { $ended=$true }
            if ($action -match '^DRIVER STOP:.*Operator stopped the test driver at port;') { $operatorStopped=$true }
            if ($action -match '^DRIVER STOP:|^AUDIT STOP:') { $stop=($action -split '\r?\n\s*at ')[0].Trim() }
            if ($action -match '^FINAL objectives=') { $finalMarker=$action }
            $previous=$event
        }
        $legendStart=@(Field $first 'legendWins' @()) | Sort-Object -Unique
        $legendEnd=@(Field $last 'legendWins' @()) | Sort-Object -Unique
        $rankStart=@(Field $first 'completedRanks' @()) | Sort-Object -Unique
        $rankEnd=@(Field $last 'completedRanks' @()) | Sort-Object -Unique
        $objectives=@($legendEnd).Count -ge 5 -and @('weathered','gilded','moonlit' | Where-Object {$_ -notin $rankEnd}).Count -eq 0
        # RUN ENDED is emitted by the fixture's finally block even for an operator pause.
        # Only the latest segment determines branch completion; earlier pauses remain evidence.
        $stopKind=if($operatorStopped){'OperatorPause'}elseif(-not $ended){'RunningOrUnverified'}elseif($objectives){'ObjectivesCompleted'}elseif($stop -match '^AUDIT STOP:'){'AuditStop'}elseif($stop -match '^DRIVER STOP:'){'DriverStop'}else{'EndedWithoutObjectives'}
        $status=if($operatorStopped){'主动暂停：保留港口检查点待续玩'}elseif(-not $ended){'采集中或待核实（未见结束记录）'}elseif($objectives){'已结束：目标记录齐全'}else{'已结束：目标未齐'}
        if (-not $stop -and $ended) { $stop=if($finalMarker){$finalMarker}else{'结束记录存在，未记录停止原因'} }
        $reviewed=''
        if ($directory.Name -eq 'Baseline-NoSwap-Finale') {
            $reviewed='已核实旧基线末段：秘藏核心奖励放不下，驱动重复选择继续探索后在Encounter状态中止；不是获得第三类秘藏。'
        }
        $fixtureResult=''
        $xmlFile=Join-Path $directory.FullName 'Play.xml'
        if(Test-Path -LiteralPath $xmlFile){
            try {
                [xml]$xml=Read-SharedSnapshot $xmlFile
                $case=$xml.SelectSingleNode('//test-case[contains(@name,"StrictEconomy_AllLegendsAndThreeTreasureRanks")]')
                if($null -ne $case){$fixtureResult=$case.GetAttribute('result')}
            } catch {$warnings.Add("$run Play.xml incomplete; its fixture status is omitted.")}
        }
        $tx=@($transactions | Where-Object {$_.Run -eq $run})
        $vs=@($voyages | Where-Object {$_.Run -eq $run})
        $purchases=@($tx | Where-Object {$_.Kind -eq 'ShipPurchase'})
        $reportedPurchaseDelta=[int](Field $last 'shipPurchases' 0)-[int](Field $first 'shipPurchases' 0)
        if($reportedPurchaseDelta -ne $purchases.Count){$warnings.Add("$run ship purchase counter delta $reportedPurchaseDelta differs from confirmed paid UI purchases $($purchases.Count); inspect transactions before inferring missing spend.")}
        $segments.Add([pscustomobject]@{
            Branch=$branch; Run=$run; StartUtc=$first.utc; LastUtc=$last.utc;
            StartCycle=$first.cycle; EndCycle=$last.cycle; LastPhase=$last.phase; Status=$status;
            SegmentEnded=$ended; StopKind=$stopKind; OperatorPaused=$operatorStopped; ResumedFromCheckpoint=$resumeDeclared;
            ObjectivesRecorded=$objectives; LegendCount=@($legendEnd).Count; Legends=($legendEnd -join ';');
            NewlyRecordedLegends=(@($legendEnd | Where-Object {$_ -notin $legendStart}) -join ';');
            Ranks=($rankEnd -join ';'); RankCount=@($rankEnd).Count; NewlyRecordedRanks=(@($rankEnd | Where-Object {$_ -notin $rankStart}) -join ';');
            StartGold=(Amount $first.gold); EndGold=(Amount $last.gold); NetAccountChange=((Amount $last.gold)-(Amount $first.gold));
            ShipPurchases=$purchases.Count; ShipPurchaseCounterDelta=$reportedPurchaseDelta; PurchasedShipIndices=(($purchases | ForEach-Object Item)-join ';');
            ShipPurchaseGold=(Sum-Column $purchases 'Gold'); PortSpendingGold=$portSpent;
            TradePurchaseGold=(Sum-Column @($tx | Where-Object {$_.Kind -eq 'TradePurchase'}) 'Gold');
            ProvisionPurchaseGold=(Sum-Column @($tx | Where-Object {$_.Kind -eq 'ProvisionPurchase'}) 'Gold');
            MarketSaleGoldMixedSources=(Sum-Column @($tx | Where-Object {$_.Kind -eq 'MarketSaleMixedSources'}) 'Gold');
            CardUpgradeGold=(Sum-Column @($tx | Where-Object {$_.Kind -eq 'CardUpgrade'}) 'Gold');
            CrewRecruitmentGold=(Sum-Column @($tx | Where-Object {$_.Kind -eq 'CrewRecruitment'}) 'Gold');
            VoyagePrincipalOut=$travelPrincipal; GmMarketRefreshes=$gm; Departures=$departures;
            Settlements=$vs.Count; EffectiveSettledVoyages=@($vs | Where-Object EffectiveVoyage).Count;
            ConfirmedCombatDefeats=@($vs | Where-Object {$_.Classification -eq 'ConfirmedCombatDefeat'}).Count;
            EmergencyTowReturns=@($vs | Where-Object {$_.Classification -eq 'EmergencyTowNonCombatDefeat'}).Count;
            SupplyTowReturns=@($vs | Where-Object {$_.Classification -eq 'SupplyTowAfterReturn'}).Count;
            ReserveSafeReturns=@($vs | Where-Object {$_.Classification -eq 'ReserveSafeReturn'}).Count;
            UnclassifiedFailures=@($vs | Where-Object {$_.Classification -eq 'FailureCauseUnconfirmed'}).Count;
            StopReason=$stop; ReviewedContext=$reviewed; FixtureResult=$fixtureResult; LogPath=$log.FullName
        })
    }
}

foreach($branch in @('Baseline-NoSwap','Production-NoSwap','Production-OneSwap','Production-TwoSwap')){
    $parts=@($segments | Where-Object {$_.Branch -eq $branch} | Sort-Object StartUtc)
    if($parts.Count -eq 0){
        $branches.Add([pscustomobject]@{Branch=$branch;Status='尚无实玩日志';Segments=0;OperatorPauses=0;ResumedSegments=0;LatestStopKind='NoLogs';StartCycle=$null;EndCycle=$null;Legends='';LegendCount=0;Ranks='';RankCount=0;ObjectivesRecorded=$false;ShipPurchases=0;ShipPurchaseGold=0;PortSpendingGold=0;TradePurchaseGold=0;ProvisionPurchaseGold=0;MarketSaleGoldMixedSources=0;CardUpgradeGold=0;CrewRecruitmentGold=0;GmMarketRefreshes=0;EffectiveSettledVoyages=0;ConfirmedCombatDefeats=0;EmergencyTowReturns=0;SupplyTowReturns=0;ReserveSafeReturns=0;UnclassifiedFailures=0;StopReason=''})
        continue
    }
    $a=$parts[0];$z=$parts[$parts.Count-1]
    $row=[ordered]@{Branch=$branch;Status=$z.Status;Segments=$parts.Count;OperatorPauses=@($parts | Where-Object OperatorPaused).Count;ResumedSegments=@($parts | Where-Object ResumedFromCheckpoint).Count;LatestStopKind=$z.StopKind;StartCycle=$a.StartCycle;EndCycle=$z.EndCycle;Legends=$z.Legends;LegendCount=$z.LegendCount;Ranks=$z.Ranks;RankCount=$z.RankCount;ObjectivesRecorded=$z.ObjectivesRecorded}
    foreach($column in @('ShipPurchases','ShipPurchaseGold','PortSpendingGold','TradePurchaseGold','ProvisionPurchaseGold','MarketSaleGoldMixedSources','CardUpgradeGold','CrewRecruitmentGold','GmMarketRefreshes','EffectiveSettledVoyages','ConfirmedCombatDefeats','EmergencyTowReturns','SupplyTowReturns','ReserveSafeReturns','UnclassifiedFailures')){$row[$column]=Sum-Column $parts $column}
    $row['StopReason']=$z.StopReason
    $branches.Add([pscustomobject]$row)
}
$segments | Sort-Object Branch,StartUtc | Export-Csv -LiteralPath (Join-Path $Output 'segments.csv') -NoTypeInformation -Encoding UTF8
$branches | Export-Csv -LiteralPath (Join-Path $Output 'branches.csv') -NoTypeInformation -Encoding UTF8
$transactions | Export-Csv -LiteralPath (Join-Path $Output 'transactions.csv') -NoTypeInformation -Encoding UTF8
$voyages | Export-Csv -LiteralPath (Join-Path $Output 'voyage-outcomes.csv') -NoTypeInformation -Encoding UTF8
[IO.File]::WriteAllLines((Join-Path $Output 'warnings.txt'), [string[]]$warnings, [Text.UTF8Encoding]::new($false))
$report=[Collections.Generic.List[string]]::new()
$report.Add('船型成长实玩日志汇总；生成UTC：'+[DateTime]::UtcNow.ToString('O'))
$report.Add('只读日志快照。cycle不是成功航次数；有效结算航次要求距离>=1海里。日志从教学结束后开始，均不含前3段教学。')
$report.Add('目标只认legendWins及completedRanks；Passed只表示测试方法完成，绝不等于五传奇和三类秘藏全通。尚无RUN ENDED的分支不判完成或失败。')
$report.Add('Operator stopped at port 是保留实际港口检查点后的主动暂停；即使 finally 写入 RUN ENDED，也不判整条路线结束。Continue/其他后缀自动合并；分支状态取最新一段，交易与结算次数按各段累加。')
$report.Add('贸易采购按明示paid计；市场出售包含战利品/其他存货，不可直接相减称纯贸易利润。港口总支出来自真实点击前后余额差，已排除出航带出的60本金。')
$report.Add('战斗失败必须由Battle阶段转入Failure结算，且不是主动撤退或应急返航。Emergency返回单列，不能仅凭Failure或旧AUDIT STOP文案称为战败。')
if(@($segments | Where-Object {$_.Run -eq 'Baseline-NoSwap-Finale'}).Count -gt 0){
    $report.Add('旧基线核实结论：五传奇、weathered和moonlit秘藏；Finale因核心奖励货舱不足及重复选择中止，未全通。')
}
foreach($row in $branches){
    $report.Add('')
    $report.Add($row.Branch+' | '+$row.Status+' | cycle '+$row.StartCycle+' -> '+$row.EndCycle)
    $report.Add('分段 '+$row.Segments+'；主动暂停 '+$row.OperatorPauses+'；声明从原检查点续玩 '+$row.ResumedSegments+'；最新停止类型 '+$row.LatestStopKind)
    $report.Add('传奇 '+$row.LegendCount+'/5：'+$row.Legends+'；秘藏 '+$row.RankCount+'/3：'+$row.Ranks)
    $report.Add('买船 '+$row.ShipPurchases+' 艘 / '+$row.ShipPurchaseGold+' 金币；港口总支出 '+$row.PortSpendingGold+'；贸易采购 '+$row.TradePurchaseGold+'；补给采购 '+$row.ProvisionPurchaseGold+'；市场出售（混合来源） '+$row.MarketSaleGoldMixedSources)
    $report.Add('GM市场刷新 '+$row.GmMarketRefreshes+'；有效结算航次 '+$row.EffectiveSettledVoyages+'；确认战败 '+$row.ConfirmedCombatDefeats+'；非战败应急拖带 '+$row.EmergencyTowReturns+'；返航补给拖带 '+$row.SupplyTowReturns+'；补给/船体预警安全返航 '+$row.ReserveSafeReturns+'；原因待核实失败 '+$row.UnclassifiedFailures)
    if($row.StopReason){$report.Add('停止原因：'+$row.StopReason)}
}
$report.Add('');$report.Add('分段明细：segments.csv；逐笔采购/出售/买船/强化：transactions.csv；每次结算证据及分类：voyage-outcomes.csv。')
$report | Set-Content -LiteralPath (Join-Path $Output 'summary.txt') -Encoding UTF8
$branches | Format-Table Branch,Status,StartCycle,EndCycle,ConfirmedCombatDefeats,EmergencyTowReturns -AutoSize
Write-Output ('Output: '+$Output)
