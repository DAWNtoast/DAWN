param(
    [string]$CatalogDirectory = '',
    [string]$FixedCatalogDirectory = '',
    [switch]$SkipExport,
    [switch]$SyncFixed
)
$ErrorActionPreference = 'Stop'
$taskWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskRound = Join-Path $taskWorkspace 'outputs/boss-calibration-20261024'
$taskInputs = Join-Path $taskRound 'Inputs/Catalog'
if (!$CatalogDirectory) { $CatalogDirectory = Join-Path $taskRound 'VerifiedCatalog' }
if (!$FixedCatalogDirectory) { $FixedCatalogDirectory = Join-Path $taskWorkspace 'outputs/card-list-by-school-20261023' }
$CatalogDirectory = [IO.Path]::GetFullPath($CatalogDirectory)
$FixedCatalogDirectory = [IO.Path]::GetFullPath($FixedCatalogDirectory)
if ($CatalogDirectory.TrimEnd('\','/') -eq $FixedCatalogDirectory.TrimEnd('\','/')) { throw 'Export to a separate verified directory before synchronizing.' }
$taskNames = @('全部卡牌清单.html','全部卡牌清单.md','card-list-data.json','全部卡牌强化清单.html','全部卡牌强化清单.md','card-upgrade-data.json')
$taskMetadata = @('source-hashes.csv','export.log','validation.txt','upgrade-source-hashes.csv','upgrade-export.log','upgrade-validation.txt','upgrade-inputs.json','audit-validation.txt','catalog-integrity.json','catalog-unchanged-hashes.csv')
$taskGuardPath = Join-Path $taskRound 'Inputs/fixed-catalog-guard.csv'
$taskLastSyncPath = Join-Path $taskRound 'fixed-catalog-hashes.csv'
function Get-TaskHash([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
    return ''
}
function Get-TaskCanonical($Value) { return ConvertTo-Json -InputObject $Value -Depth 100 -Compress }
function Get-TaskTextHash([string]$Value) {
    $taskSha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($taskSha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value))).Replace('-','') }
    finally { $taskSha.Dispose() }
}
function Assert-Task([bool]$Condition,[string]$Message) { if (!$Condition) { throw $Message } }

# Capture once. The six manuscript inputs are never overwritten; metadata is
# captured separately because this round's original input folder contains only
# the six catalog products. Later syncs accept only this guard or a prior own copy.
if (!(Test-Path -LiteralPath $taskGuardPath)) {
    $taskBackup = Join-Path $taskRound 'Inputs/CatalogMetadata'
    New-Item -ItemType Directory -Force -Path $taskBackup | Out-Null
    $taskGuard = foreach ($taskName in ($taskNames + $taskMetadata)) {
        $taskFixedPath = Join-Path $FixedCatalogDirectory $taskName
        $taskCurrent = Get-TaskHash $taskFixedPath
        if ($taskNames -contains $taskName) {
            $taskCaptured = Get-TaskHash (Join-Path $taskInputs $taskName)
            Assert-Task ($taskCaptured -ne '' -and $taskCurrent -eq $taskCaptured) ('Fixed catalog differs from captured input; review before replacing: '+$taskName)
        } elseif ($taskCurrent -ne '') {
            Copy-Item -LiteralPath $taskFixedPath -Destination (Join-Path $taskBackup $taskName) -Force
        }
        [pscustomobject]@{ File=$taskName; ExpectedSHA256=$taskCurrent; FixedPath=$taskFixedPath }
    }
    $taskGuard | Export-Csv -LiteralPath $taskGuardPath -NoTypeInformation -Encoding utf8
}
$taskGuardMap = @{}
foreach ($taskRow in Import-Csv -LiteralPath $taskGuardPath) { $taskGuardMap[$taskRow.File] = $taskRow.ExpectedSHA256 }
$taskPreviousMap = @{}
if (Test-Path -LiteralPath $taskLastSyncPath) {
    foreach ($taskRow in Import-Csv -LiteralPath $taskLastSyncPath) { $taskPreviousMap[$taskRow.File] = $taskRow.SHA256 }
}
foreach ($taskName in ($taskNames + $taskMetadata)) {
    Assert-Task ($taskGuardMap.ContainsKey($taskName)) ('Missing synchronization guard: '+$taskName)
    $taskCurrent = Get-TaskHash (Join-Path $FixedCatalogDirectory $taskName)
    Assert-Task ($taskCurrent -eq $taskGuardMap[$taskName] -or ($taskPreviousMap.ContainsKey($taskName) -and $taskCurrent -eq $taskPreviousMap[$taskName])) ('External fixed-catalog edit detected; preserved: '+$taskName)
}

if (!$SkipExport) {
    & (Join-Path $taskWorkspace 'tools/CardListBySchool20261023/ExportCardList.ps1') -Output $CatalogDirectory
}
foreach ($taskManifest in @('source-hashes.csv','upgrade-source-hashes.csv')) {
    foreach ($taskRow in Import-Csv -LiteralPath (Join-Path $CatalogDirectory $taskManifest)) {
        Assert-Task ((Get-TaskHash $taskRow.Source) -eq $taskRow.SHA256) ('Export is stale against current source: '+$taskRow.Source)
    }
}
$taskAuditItems = [Collections.Generic.List[object]]::new()
$taskJsonHashes = [Collections.Generic.List[object]]::new()
$taskDataMap = @{}
foreach ($taskJsonName in @('card-list-data.json','card-upgrade-data.json')) {
    $taskBeforePath = Join-Path $taskInputs $taskJsonName
    $taskAfterPath = Join-Path $CatalogDirectory $taskJsonName
    $taskBefore = Get-Content -LiteralPath $taskBeforePath -Raw -Encoding utf8 | ConvertFrom-Json
    $taskAfter = Get-Content -LiteralPath $taskAfterPath -Raw -Encoding utf8 | ConvertFrom-Json
    $taskDataMap[$taskJsonName] = $taskAfter
    Assert-Task ($taskBefore.Cards.Count -eq $taskAfter.Cards.Count) ('Card count changed: '+$taskJsonName)
    for ($taskIndex=0; $taskIndex -lt $taskBefore.Cards.Count; $taskIndex++) {
        $taskOldCard = $taskBefore.Cards[$taskIndex]
        $taskNewCard = $taskAfter.Cards[$taskIndex]
        $taskOldJson = Get-TaskCanonical $taskOldCard
        $taskNewJson = Get-TaskCanonical $taskNewCard
        $taskAuditItems.Add([pscustomobject]@{
            Kind=$(if($taskJsonName -eq 'card-list-data.json'){'base-card'}else{'upgrade-card'})
            Key=$taskOldCard.Id; Unchanged=($taskOldJson -ceq $taskNewJson)
            BeforeSHA256=(Get-TaskTextHash $taskOldJson); AfterSHA256=(Get-TaskTextHash $taskNewJson)
        })
        if ($taskJsonName -eq 'card-upgrade-data.json') {
            foreach ($taskOldStage in $taskOldCard.Stages) {
                $taskNewStage = @($taskNewCard.Stages | Where-Object Stage -ceq $taskOldStage.Stage)
                Assert-Task ($taskNewStage.Count -eq 1) ('Stage changed: '+$taskOldCard.Id+'/'+$taskOldStage.Stage)
                foreach ($taskOldLevel in $taskOldStage.Levels) {
                    $taskNewLevel = @($taskNewStage[0].Levels | Where-Object Level -eq $taskOldLevel.Level)
                    Assert-Task ($taskNewLevel.Count -eq 1) ('Upgrade level changed: '+$taskOldCard.Id+'/'+$taskOldStage.Stage+'/'+$taskOldLevel.Level)
                    $taskOldLevelJson = Get-TaskCanonical $taskOldLevel
                    $taskNewLevelJson = Get-TaskCanonical $taskNewLevel[0]
                    $taskAuditItems.Add([pscustomobject]@{
                        Kind='upgrade-level'; Key=($taskOldCard.Id+'|'+$taskOldStage.Stage+'|'+$taskOldLevel.Level)
                        Unchanged=($taskOldLevelJson -ceq $taskNewLevelJson)
                        BeforeSHA256=(Get-TaskTextHash $taskOldLevelJson); AfterSHA256=(Get-TaskTextHash $taskNewLevelJson)
                    })
                }
            }
        }
    }
    $taskCanonicalEqual = (Get-TaskCanonical $taskBefore) -ceq (Get-TaskCanonical $taskAfter)
    $taskBeforeHash = Get-TaskHash $taskBeforePath
    $taskAfterHash = Get-TaskHash $taskAfterPath
    $taskJsonHashes.Add([pscustomobject]@{ File=$taskJsonName; BeforeSHA256=$taskBeforeHash; AfterSHA256=$taskAfterHash; ByteIdentical=($taskBeforeHash -eq $taskAfterHash); AllFieldsEqual=$taskCanonicalEqual })
}
$taskAuditItems | Export-Csv -LiteralPath (Join-Path $CatalogDirectory 'catalog-unchanged-items.csv') -NoTypeInformation -Encoding utf8
$taskJsonHashes | Export-Csv -LiteralPath (Join-Path $CatalogDirectory 'catalog-unchanged-hashes.csv') -NoTypeInformation -Encoding utf8
Assert-Task (@($taskAuditItems | Where-Object {!$_.Unchanged}).Count -eq 0) 'Card effects, cost or upgrades changed; see catalog-unchanged-items.csv. Nothing synchronized.'
Assert-Task (@($taskJsonHashes | Where-Object {!$_.AllFieldsEqual}).Count -eq 0) 'Complete base/upgrade JSON differs from input, including metadata, glossary or rules. Nothing synchronized.'
$taskBase = $taskDataMap['card-list-data.json']
$taskUpgrade = $taskDataMap['card-upgrade-data.json']
Assert-Task ($taskBase.Cards.Count -eq 324 -and $taskUpgrade.StageCount -eq 358 -and $taskUpgrade.LevelCount -eq 1058) 'Unexpected reference dimensions.'
$taskBaseHtml = [IO.File]::ReadAllText((Join-Path $CatalogDirectory '全部卡牌清单.html'))
$taskBaseMd = [IO.File]::ReadAllText((Join-Path $CatalogDirectory '全部卡牌清单.md'))
$taskUpgradeHtml = [IO.File]::ReadAllText((Join-Path $CatalogDirectory '全部卡牌强化清单.html'))
Assert-Task ([regex]::Matches($taskBaseHtml,'data-card-id="').Count -eq 324 -and [regex]::Matches($taskBaseMd,'(?m)^### ').Count -eq 324) 'Base rendered count differs.'
Assert-Task ([regex]::Matches($taskUpgradeHtml,'<article id="card-').Count -eq 324 -and [regex]::Matches($taskUpgradeHtml,'<table>').Count -eq 358) 'Upgrade rendered count differs.'
Assert-Task ([regex]::Matches($taskBaseMd,'<!-- card-upgrade-navigation -->').Count -eq 1 -and $taskBaseHtml.Contains('全部卡牌强化清单.html')) 'Upgrade navigation is missing or duplicated.'

$taskSyncRows = foreach ($taskName in $taskNames) {
    $taskBeforeHash = Get-TaskHash (Join-Path $taskInputs $taskName)
    $taskVerifiedHash = Get-TaskHash (Join-Path $CatalogDirectory $taskName)
    $taskFixedHash = Get-TaskHash (Join-Path $FixedCatalogDirectory $taskName)
    [pscustomobject]@{ File=$taskName; InputSHA256=$taskBeforeHash; VerifiedSHA256=$taskVerifiedHash; FixedSHA256=$taskFixedHash; IdenticalToInput=($taskBeforeHash -eq $taskVerifiedHash); Synchronized=($taskVerifiedHash -eq $taskFixedHash) }
}
$taskSyncRows | Export-Csv -LiteralPath (Join-Path $CatalogDirectory 'catalog-sync-hashes.csv') -NoTypeInformation -Encoding utf8
$taskSummary = [ordered]@{
    Utc=[DateTime]::UtcNow.ToString('o'); InputDirectory=$taskInputs; VerifiedDirectory=$CatalogDirectory
    BaseCards=$taskBase.Cards.Count; UpgradeCards=$taskUpgrade.Cards.Count; StageCount=$taskUpgrade.StageCount; LevelCount=$taskUpgrade.LevelCount
    ComparedItems=$taskAuditItems.Count; ChangedItems=0; AllBaseAndUpgradeJsonFieldsEqual=$true
    BothJsonByteIdentical=(@($taskJsonHashes | Where-Object {!$_.ByteIdentical}).Count -eq 0)
    SixCatalogFilesIdenticalToInput=(@($taskSyncRows | Where-Object {!$_.IdenticalToInput}).Count -eq 0)
    SyncRequested=[bool]$SyncFixed; SourceHashesCurrent=$true
}
$taskSummary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $CatalogDirectory 'catalog-integrity.json') -Encoding utf8
[IO.File]::WriteAllLines((Join-Path $CatalogDirectory 'audit-validation.txt'),@(
    'PASS 324 base cards / 324 upgrade identities / 358 stages / 1058 legal upgrade rows.',
    'PASS every base and upgrade JSON field equals the captured input; no card or keyword change.',
    ('PASS '+$taskAuditItems.Count+' individual identity/level comparisons; 0 changed.'),
    'PASS current source hashes for both exporters and rendered card/navigation counts.',
    'Sync uses captured hashes and prior own-sync hashes; external edits are never overwritten.'
),[Text.UTF8Encoding]::new($false))

if ($SyncFixed) {
    # Complete all conflict checks and source freshness checks before the first copy.
    foreach ($taskName in ($taskNames + $taskMetadata)) {
        $taskCurrent = Get-TaskHash (Join-Path $FixedCatalogDirectory $taskName)
        $taskVerified = Get-TaskHash (Join-Path $CatalogDirectory $taskName)
        Assert-Task ($taskVerified -ne '') ('Missing verified artifact: '+$taskName)
        Assert-Task ($taskCurrent -eq $taskGuardMap[$taskName] -or $taskCurrent -eq $taskVerified -or ($taskPreviousMap.ContainsKey($taskName) -and $taskCurrent -eq $taskPreviousMap[$taskName])) ('External edit occurred during export; preserved: '+$taskName)
    }
    foreach ($taskManifest in @('source-hashes.csv','upgrade-source-hashes.csv')) {
        foreach ($taskRow in Import-Csv -LiteralPath (Join-Path $CatalogDirectory $taskManifest)) {
            Assert-Task ((Get-TaskHash $taskRow.Source) -eq $taskRow.SHA256) ('Source changed before sync: '+$taskRow.Source)
        }
    }
    $taskLastSync = foreach ($taskName in ($taskNames + $taskMetadata)) {
        $taskSourcePath = Join-Path $CatalogDirectory $taskName
        $taskDestination = Join-Path $FixedCatalogDirectory $taskName
        Copy-Item -LiteralPath $taskSourcePath -Destination $taskDestination -Force
        $taskHash = Get-TaskHash $taskDestination
        Assert-Task ($taskHash -eq (Get-TaskHash $taskSourcePath)) ('Copy mismatch: '+$taskName)
        [pscustomobject]@{ File=$taskName; SHA256=$taskHash; FixedPath=$taskDestination }
    }
    $taskLastSync | Export-Csv -LiteralPath $taskLastSyncPath -NoTypeInformation -Encoding utf8
    foreach ($taskRow in $taskSyncRows) { $taskRow.FixedSHA256=Get-TaskHash (Join-Path $FixedCatalogDirectory $taskRow.File); $taskRow.Synchronized=$taskRow.VerifiedSHA256 -eq $taskRow.FixedSHA256 }
    $taskSyncRows | Export-Csv -LiteralPath (Join-Path $CatalogDirectory 'catalog-sync-hashes.csv') -NoTypeInformation -Encoding utf8
}
$taskSynced = @($taskSyncRows | Where-Object {!$_.Synchronized}).Count -eq 0
$taskReport = @(
    '# Boss 校准后的卡牌不变检查',
    '',
    '本次重新从当前源码导出基础及强化清单，与本轮冻结输入逐项比较。324 张基础牌、324 个强化身份、358 个阶段及 1058 行合法强化效果全部一致；1706 个身份／等级比较项，改动为 0。',
    '',
    '完整基础及强化 JSON（含字段说明、规则、费用、卡牌编号、强化上限与强化价格）全部字段一致。原始输入文件保留。',
    '',
    ('固定六个清单的内容同步状态：'+$(if($taskSynced){'一致。'}else{'尚未同步。'})),
    ('本次是否刷新来源元数据：'+$(if($SyncFixed){'已刷新。'}else{'仅校验，最终定稿后运行 -SyncFixed。'})),
    '',
    '[逐项检查](catalog-unchanged-items.csv) · [两个 JSON 哈希](catalog-unchanged-hashes.csv) · [六个清单哈希](catalog-sync-hashes.csv) · [结构化结论](catalog-integrity.json)',
    '',
    '同步前核对最初输入／捕获元数据以及此前本工具写入的哈希；若检测到外部编辑，立即停止并保留原文件。'
)
[IO.File]::WriteAllLines((Join-Path $CatalogDirectory '卡牌不变检查.md'),$taskReport,[Text.UTF8Encoding]::new($false))
Write-Output ('PASS unchanged cards: '+$taskAuditItems.Count+' comparisons, 0 changes; fixed content synchronized='+$taskSynced+'; metadata refreshed='+[bool]$SyncFixed)
Write-Output (Join-Path $CatalogDirectory '卡牌不变检查.md')
