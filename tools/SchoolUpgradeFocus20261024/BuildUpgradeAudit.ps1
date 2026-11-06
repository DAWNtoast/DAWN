param(
    [Parameter(Mandatory=$true)][string]$BeforeDummy,
    [Parameter(Mandatory=$true)][string]$AfterDummy,
    [Parameter(Mandatory=$true)][string]$BeforeBoss,
    [Parameter(Mandatory=$true)][string]$AfterBoss,
    [Parameter(Mandatory=$true)][string]$Output,
    [int]$StrictDummyStart = -1,
    [int]$ValidationDummyStart = -1
)
$ErrorActionPreference = 'Stop'
if ($StrictDummyStart -ge 0 -and $ValidationDummyStart -ge 0) { throw 'Choose one dummy split classification.' }
$auditOutput = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $auditOutput -Force | Out-Null
$auditPaths = @{
    dummy=@{Before=[IO.Path]::GetFullPath($BeforeDummy);After=[IO.Path]::GetFullPath($AfterDummy)}
    boss=@{Before=[IO.Path]::GetFullPath($BeforeBoss);After=[IO.Path]::GetFullPath($AfterBoss)}
}
function AuditKey($row) { @($row.cohort,$row.loadout,$row.tier,$row.school,$row.target,$row.policy,$row.seed) -join '|' }
function AuditMean($rows,[string]$field) { [double](($rows | ForEach-Object { [double]$_.$field } | Measure-Object -Average).Average) }
function AuditLoss($rows) { [double](($rows | ForEach-Object { [double]$_.initialHP - [double]$_.endHP } | Measure-Object -Average).Average) }
$auditCounts = [System.Collections.Generic.List[object]]::new()
$auditResource = [System.Collections.Generic.List[object]]::new()
$auditTransitions = [System.Collections.Generic.List[object]]::new()
$auditComparisons = [System.Collections.Generic.List[object]]::new()
$auditCore = [System.Collections.Generic.List[object]]::new()
$auditHashes = [System.Collections.Generic.List[object]]::new()
foreach ($mode in 'dummy','boss') {
    $paths = $auditPaths[$mode]
    foreach ($side in 'Before','After') {
        foreach ($file in 'samples.csv','summary.csv','source-hashes.csv','input-hashes.csv','run-parameters.json') {
            $path = Join-Path $paths[$side] $file
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ('Incomplete run: ' + $path) }
            $auditHashes.Add([pscustomobject]@{Mode=$mode;Side=$side;File=$file;Path=$path;SHA256=(Get-FileHash -LiteralPath $path).Hash})
        }
        $rows = @(Import-Csv -LiteralPath (Join-Path $paths[$side] 'samples.csv'))
        $parameters = Get-Content -LiteralPath (Join-Path $paths[$side] 'run-parameters.json') -Raw | ConvertFrom-Json
        if ($parameters.Mode -ne $mode) { throw ('Unexpected sample mode: ' + $paths[$side]) }
        foreach ($group in ($rows | Group-Object cohort,loadout,tier,school,target,policy)) {
            $seeds = @($group.Group.seed | ForEach-Object { [int]$_ } | Sort-Object)
            if (($seeds -join ',') -cne ((0..($parameters.Seeds-1)) -join ',')) { throw ('Incomplete or duplicate seed group: ' + $group.Name) }
        }
        foreach ($group in ($rows | Group-Object cohort,school)) {
            $g = @($group.Group)
            $auditCounts.Add([pscustomobject]@{
                Mode=$mode;Side=$side;Cohort=$g[0].cohort;School=$g[0].school;N=$g.Count
                Kills=@($g | Where-Object outcome -in 'kill','sunk').Count;Deaths=@($g | Where-Object outcome -in 'death','dead').Count
                Other=@($g | Where-Object outcome -notin 'kill','sunk','death','dead').Count
                ChainAtLeast5=@($g | Where-Object {[int]$_.maxChain -ge 5}).Count
                ChainAtLeast10=@($g | Where-Object {[int]$_.maxChain -ge 10}).Count
                ChainAtLeast50=@($g | Where-Object {[int]$_.maxChain -ge 50}).Count
                ObservedMaxChain=($g | Measure-Object maxChain -Maximum).Maximum
            })
        }
    }
    $holdout = if ($mode -eq 'dummy' -and $StrictDummyStart -ge 0) {$StrictDummyStart} elseif ($mode -eq 'dummy' -and $ValidationDummyStart -ge 0) {$ValidationDummyStart} else {[int]::MaxValue}
    $pairedPath = Join-Path $auditOutput ('强化审计-paired-' + $mode + '.csv')
    & (Join-Path $PSScriptRoot 'CompareSamples.ps1') -Before $paths.Before -After $paths.After -Output $pairedPath -HoldoutStart $holdout
    foreach ($row in (Import-Csv -LiteralPath $pairedPath)) {
        $flags = @()
        if ([int]$row.AfterDeaths -gt [int]$row.BeforeDeaths) {$flags += '死亡数增加'}
        if ([int]$row.AfterKills -lt [int]$row.BeforeKills) {$flags += '击沉数减少'}
        if ([double]$row.AfterTurns - [double]$row.BeforeTurns -ge .5) {$flags += '平均延长至少0.5轮'}
        if ([double]$row.AfterHullLoss - [double]$row.BeforeHullLoss -ge 5) {$flags += '平均净损增加至少5'}
        $row | Add-Member NoteProperty Mode $mode
        $row | Add-Member NoteProperty EvidenceScope $(if ($row.Subset -eq 'holdout' -and $ValidationDummyStart -ge 0) {'木桩种子'+$ValidationDummyStart+'起：本轮选择未用、前轮用过，非严格盲测'} elseif ($row.Subset -eq 'holdout') {'未参与筛选的木桩种子'+$StrictDummyStart+'起'} else {'全部配对种子；不自动视为独立保留组'})
        if ($row.Subset -eq 'holdout' -and $ValidationDummyStart -ge 0) {$row.Subset = 'validation-reused'}
        $row | Add-Member NoteProperty TurnDelta ([double]$row.AfterTurns - [double]$row.BeforeTurns)
        $row | Add-Member NoteProperty HullLossDelta ([double]$row.AfterHullLoss - [double]$row.BeforeHullLoss)
        $row | Add-Member NoteProperty ReviewFlag ($flags -join '；')
        $auditComparisons.Add($row)
    }
    $before = @(Import-Csv -LiteralPath (Join-Path $paths.Before 'samples.csv'))
    $after = @(Import-Csv -LiteralPath (Join-Path $paths.After 'samples.csv'))
    $oldMap = @{}
    foreach ($row in $before) { $oldMap[(AuditKey $row)] = $row }
    foreach ($row in $after) {
        $old = $oldMap[(AuditKey $row)]
        if ($row.school -in 'ordinary','tide','blood') {
            foreach ($field in $row.PSObject.Properties.Name | Where-Object {$_ -ne 'trace'}) {
                if ($row.$field -cne $old.$field) { throw ('Unchanged school sample differs: '+(AuditKey $row)+'/'+$field) }
            }
        }
        if ($row.outcome -cne $old.outcome) {
            $auditTransitions.Add([pscustomobject]@{
                Mode=$mode;Cohort=$row.cohort;Tier=$row.tier;School=$row.school;Target=$row.target;Policy=$row.policy;Seed=$row.seed
                BeforeOutcome=$old.outcome;AfterOutcome=$row.outcome;BeforeTurns=$old.turns;AfterTurns=$row.turns
                BeforeEndHP=$old.endHP;AfterEndHP=$row.endHP;BeforePaidLife=$old.paidLife;AfterPaidLife=$row.paidLife
            })
        }
    }
    foreach ($group in ($after | Group-Object cohort,loadout,tier,school,target,policy)) {
        $g = @($group.Group); $first=$g[0]
        $old = @($g | ForEach-Object {$oldMap[(AuditKey $_)]})
        $ordinary = @($after | Where-Object {$_.cohort -eq $first.cohort -and $_.loadout -eq $first.loadout -and $_.target -eq $first.target -and $_.policy -eq $first.policy -and $_.school -eq 'ordinary'})
        $values = [ordered]@{Mode=$mode;Cohort=$first.cohort;Tier=$first.tier;School=$first.school;Target=$first.target;Policy=$first.policy;N=$g.Count}
        foreach ($metric in 'absorbed','paidLife','healingNetOfCost','playsIncludingPreparation','energySpentIncludingPreparation','powder','balls','wood','medicine','startMana','startManaLimit','maxMana','maxMarks','maxTide','maxChain') {
            $values['Before_'+$metric] = AuditMean $old $metric
            $values['After_'+$metric] = AuditMean $g $metric
            $values['Ordinary_'+$metric] = AuditMean $ordinary $metric
        }
        $values['BeforeNetHullLoss'] = AuditLoss $old
        $values['AfterNetHullLoss'] = AuditLoss $g
        $values['OrdinaryNetHullLoss'] = AuditLoss $ordinary
        $auditResource.Add([pscustomobject]$values)
    }
    $oldFiles = @(Get-ChildItem -LiteralPath (Join-Path $paths.Before 'CoreSnapshot') -Filter '*.cs')
    $newFiles = @(Get-ChildItem -LiteralPath (Join-Path $paths.After 'CoreSnapshot') -Filter '*.cs')
    if ((@($oldFiles.Name | Sort-Object) -join '|') -cne (@($newFiles.Name | Sort-Object) -join '|')) { throw 'Core file identities changed; audit requires explicit review.' }
    foreach ($file in $newFiles) {
        $oldHash = (Get-FileHash -LiteralPath (Join-Path $paths.Before ('CoreSnapshot/'+$file.Name))).Hash
        $newHash = (Get-FileHash -LiteralPath $file.FullName).Hash
        $sharedHash = (Get-FileHash -LiteralPath (Join-Path $auditPaths.dummy.After ('CoreSnapshot/'+$file.Name))).Hash
        if ($newHash -ne $sharedHash) { throw ('Final Core differs across runs: '+$file.Name) }
        if ($oldHash -ne $newHash) {$auditCore.Add([pscustomobject]@{Mode=$mode;File=$file.Name;BeforeSHA256=$oldHash;AfterSHA256=$newHash})}
    }
}
$auditComparisons | Export-Csv -LiteralPath (Join-Path $auditOutput '强化审计-对照.csv') -NoTypeInformation -Encoding utf8
$auditComparisons | Where-Object {$_.ReviewFlag -and $_.School -ne 'ordinary'} | Export-Csv -LiteralPath (Join-Path $auditOutput '强化审计-关注项.csv') -NoTypeInformation -Encoding utf8
$auditResource | Export-Csv -LiteralPath (Join-Path $auditOutput '强化审计-资源.csv') -NoTypeInformation -Encoding utf8
$auditCounts | Export-Csv -LiteralPath (Join-Path $auditOutput '强化审计-结局与连锁.csv') -NoTypeInformation -Encoding utf8
$auditTransitions | Export-Csv -LiteralPath (Join-Path $auditOutput '强化审计-结局迁移.csv') -NoTypeInformation -Encoding utf8
$auditCore | Export-Csv -LiteralPath (Join-Path $auditOutput '强化审计-Core变化.csv') -NoTypeInformation -Encoding utf8
$auditHashes | Export-Csv -LiteralPath (Join-Path $auditOutput '强化审计-输入哈希.csv') -NoTypeInformation -Encoding utf8
[pscustomobject]@{Paths=$auditPaths;StrictDummyStart=$StrictDummyStart;ValidationDummyStart=$ValidationDummyStart;ResourcePeaks='Observed maxima, not cumulative resource generation';NoScalarStrengthScore=$true} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $auditOutput '强化审计-输入.json') -Encoding utf8
$finalTotal = ($auditCounts | Where-Object Side -eq 'After' | Measure-Object N -Sum).Sum
('PASS {0} final samples paired; ordinary/tide/blood unchanged; decks/loadouts/strategy fixed; final Core matches across runs.' -f $finalTotal) |
    Tee-Object -FilePath (Join-Path $auditOutput '强化审计-验证.txt')
