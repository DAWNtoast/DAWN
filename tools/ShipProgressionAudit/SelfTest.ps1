param([string]$Output='D:/Game/UnityClient/Artifacts/ShipProgression20261003/LogSummarySelfTest')
$ErrorActionPreference='Stop'
$inputRoot=Join-Path $Output 'Input'
$capture=Join-Path $inputRoot 'Production-OneSwap/captures'
New-Item -ItemType Directory -Path $capture -Force | Out-Null
$rows=[Collections.Generic.List[object]]::new()
$legends=@('legendary_pirate_iron_oath','legendary_pirate_red_widow','legendary_pirate_thunder_crown','legendary_pirate_ember_ark','legendary_pirate_tide_requiem')
function Event([string]$Action,[string]$Phase,[decimal]$Gold,[int]$Cycle=0,[int]$Ship=7,[int]$Purchases=1){
    $rows.Add([pscustomobject]@{sequence=$rows.Count;utc='2026-10-03T00:00:00Z';cycle=$Cycle;action=$Action;phase=$Phase;port='铜钟港';gold=$Gold;ship=$Ship;shipPurchases=$Purchases;legendWins=$legends;completedRanks=@('weathered','gilded','moonlit');distance=820;hull=240;playerCrew=20;water=100;food=100;encounter='legendary_pirate_ember_ark'})
}
Event 'Fresh tutorial completed' 'Port' 6000 0 0 0
Event 'Before click Confirm Shipyard Vessel' 'Port' 6000 0 0 0
Event 'After click Confirm Shipyard Vessel' 'Port' 1200
Event 'Before click Market Buy wood' 'Port' 1200
Event 'After click Market Buy wood' 'Port' 1100
Event 'Trade purchase wood x20; paid=100; port=铜钟港' 'Port' 1100
Event 'Before click Market Sell wood' 'Port' 1100
Event 'After click Market Sell wood' 'Port' 1225
Event 'Real market sale wood x20; received=125; retained=10' 'Port' 1225
Event 'Before click Game Master Refresh Market' 'Port' 1225
Event 'After click Game Master Refresh Market' 'Port' 1225
Event 'Before click Start Voyage' 'Port' 1225
Event 'After click Start Voyage' 'Sailing' 1165
Event 'Before click End Battle Turn' 'Battle' 1165
Event 'After click End Battle Turn' 'Settlement' 1165
Event 'SETTLEMENT outcome=Failure; fragments=0;' 'Settlement' 1165
Event 'PORT CYCLE 1' 'Port' 1225 1
Event 'Before click Emergency Encounter Return' 'Encounter' 1225 1
Event 'After click Emergency Encounter Return' 'Settlement' 1225 1
Event 'SETTLEMENT outcome=Failure; fragments=0;' 'Settlement' 1225 1
$json=($rows | ForEach-Object {$_ | ConvertTo-Json -Compress}) -join "`n"
[IO.File]::WriteAllText((Join-Path $capture 'strict-actions.jsonl'),$json+"`n"+'{"sequence":',[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path (Split-Path $capture -Parent) 'Play.xml'),'<test-run><test-case name="StrictEconomy_AllLegendsAndThreeTreasureRanks_RealUI" result="Passed" /></test-run>')
$reportRoot=Join-Path $Output 'Report'
& (Join-Path $PSScriptRoot 'Summarize.ps1') -Root $inputRoot -Output $reportRoot | Out-Null
$segment=Import-Csv -LiteralPath (Join-Path $reportRoot 'segments.csv') | Select-Object -First 1
function Check([bool]$Pass,[string]$Description){if(-not $Pass){throw $Description};Write-Output ('PASS '+$Description)}
Check ($segment.Status -like '采集中*') 'RUN ENDED absent remains undecided even with Passed and all objectives recorded'
Check ($segment.ShipPurchases -eq 1 -and [decimal]$segment.ShipPurchaseGold -eq 4800) 'Only the paid ship purchase counts, with its actual gold delta'
Check ([decimal]$segment.PortSpendingGold -eq 4900 -and [decimal]$segment.VoyagePrincipalOut -eq 60) 'Departure principal is excluded from port spending'
Check ([decimal]$segment.TradePurchaseGold -eq 100 -and [decimal]$segment.MarketSaleGoldMixedSources -eq 125) 'Explicit purchase and mixed-source sale totals are each counted once'
Check ($segment.ConfirmedCombatDefeats -eq 1 -and $segment.EmergencyTowReturns -eq 1) 'Combat death and healthy emergency return are separated'
Check ($segment.GmMarketRefreshes -eq 1) 'Refresh intent and completed button do not double-count'
Check ((Get-Content -LiteralPath (Join-Path $reportRoot 'warnings.txt') -Raw) -match 'not complete/valid JSON') 'Live incomplete trailing line is warned and ignored'

# Separate deterministic cases avoid a previous self-test's Continue folder changing
# the operator-only assertion, and never touch the real Unity logs/checkpoints.
Event 'DRIVER STOP: System.InvalidOperationException: Operator stopped the test driver at port; retain actual checkpoint, restore user profile via fixture.' 'Port' 1225 1
Event 'RUN ENDED' 'Port' 1225 1
$pausedJson=($rows | ForEach-Object {$_ | ConvertTo-Json -Compress}) -join "`n"
function Write-Scenario([string]$Name,[string]$FirstJson,[string]$ContinueJson=''){
    $scenario=Join-Path $Output $Name
    $firstCapture=Join-Path $scenario 'Input/Production-OneSwap/captures'
    New-Item -ItemType Directory -Path $firstCapture -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $firstCapture 'strict-actions.jsonl'),$FirstJson,[Text.UTF8Encoding]::new($false))
    if($ContinueJson){
        $nextCapture=Join-Path $scenario 'Input/Production-OneSwap-Continue/captures'
        New-Item -ItemType Directory -Path $nextCapture -Force | Out-Null
        [IO.File]::WriteAllText((Join-Path $nextCapture 'strict-actions.jsonl'),$ContinueJson,[Text.UTF8Encoding]::new($false))
    }
    & (Join-Path $PSScriptRoot 'Summarize.ps1') -Root (Join-Path $scenario 'Input') -Output (Join-Path $scenario 'Report') | Out-Null
    return (Import-Csv -LiteralPath (Join-Path $scenario 'Report/branches.csv') | Where-Object Branch -eq 'Production-OneSwap')
}
$paused=Write-Scenario 'Case-OperatorPause' $pausedJson
Check ($paused.Status -like '主动暂停*' -and $paused.LatestStopKind -eq 'OperatorPause' -and $paused.OperatorPauses -eq 1) 'Operator stop remains a pause despite RUN ENDED and recorded objectives'

$rows.Clear()
Event 'Resume exact earned settled checkpoint; no edited values' 'Port' 1225 1
Event 'Before click Market Buy Food' 'Port' 1225 1
Event 'After click Market Buy Food' 'Port' 1205 1
Event 'Provision purchase Food x100; paid=20; port=铜钟港' 'Port' 1205 1
Event 'Before click Start Voyage' 'Port' 1205 1
Event 'After click Start Voyage' 'Sailing' 1145 1
Event 'Return on public supply/hull reserve: water=9 food=9' 'Sailing' 1145 1
Event 'Before click Safe Return' 'Sailing' 1145 1
Event 'After click Safe Return' 'Settlement' 1145 1
Event 'SETTLEMENT outcome=SafeReturn; fragments=0;' 'Settlement' 1145 1
Event 'PORT CYCLE 2' 'Port' 1205 2
foreach($event in $rows){$event.utc='2026-10-03T00:01:00Z'}
$activeJson=($rows | ForEach-Object {$_ | ConvertTo-Json -Compress}) -join "`n"
$active=Write-Scenario 'Case-ResumedActive' $pausedJson $activeJson
Check ($active.Segments -eq 2 -and $active.OperatorPauses -eq 1 -and $active.ResumedSegments -eq 1 -and $active.Status -like '采集中*') 'Continue joins original branch and active continuation overrides earlier pause status'
Check ($active.StartCycle -eq 0 -and $active.EndCycle -eq 2 -and $active.ShipPurchases -eq 1 -and [decimal]$active.ShipPurchaseGold -eq 4800 -and [decimal]$active.ProvisionPurchaseGold -eq 20 -and [decimal]$active.PortSpendingGold -eq 4920) 'Continuation accumulates real spending once without counting checkpoint starting resources'
Check ($active.EffectiveSettledVoyages -eq 3 -and $active.ConfirmedCombatDefeats -eq 1 -and $active.EmergencyTowReturns -eq 1 -and $active.ReserveSafeReturns -eq 1) 'Resumed voyage preserves actual outcomes and does not turn safe return into battle defeat'

Event 'FINAL objectives=True' 'Port' 1205 2
Event 'RUN ENDED' 'Port' 1205 2
foreach($event in $rows){$event.utc='2026-10-03T00:01:00Z'}
$endedJson=($rows | ForEach-Object {$_ | ConvertTo-Json -Compress}) -join "`n"
$completed=Write-Scenario 'Case-ResumedEnded' $pausedJson $endedJson
Check ($completed.Status -eq '已结束：目标记录齐全' -and $completed.LatestStopKind -eq 'ObjectivesCompleted' -and $completed.Segments -eq 2 -and $completed.OperatorPauses -eq 1) 'A completed continuation closes the combined branch without erasing its earlier pause'
Write-Output 'Summary tool self-test: 12 passed.'
