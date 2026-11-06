param([string]$RunTag = 'BossNumericAuditExperience2')
$ErrorActionPreference = 'Stop'
$auditRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditEvidence = Join-Path $auditRoot 'outputs/campaign-progression-retune-20261027'
$auditOutput = Join-Path $auditEvidence $RunTag
if ($RunTag -notmatch '^[A-Za-z0-9_-]+$') { throw 'RunTag must be a single simple directory name.' }
if (Test-Path -LiteralPath $auditOutput) { throw 'Use a new RunTag; existing evidence is preserved.' }
$auditBuild = Join-Path $auditOutput 'Build'
$auditUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$auditFramework = Join-Path $auditUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$auditExporter = Join-Path $PSScriptRoot 'ExportBossNumericAudit.cs'
$auditFrozenRoots = @{
    before = Join-Path $auditRoot 'outputs/campaign-boss-review-20261027/FrozenSourceMiniboss'
    after = Join-Path $auditEvidence 'FrozenSourceExperience2'
}
$auditSourcesByVersion = @{}
foreach ($auditVersion in @('before', 'after')) {
    $auditFrozen = $auditFrozenRoots[$auditVersion]
    $auditSourcesByVersion[$auditVersion] = @((Get-ChildItem -LiteralPath (Join-Path $auditFrozen 'Core/Runtime') -Filter '*.cs' | Sort-Object Name).FullName) +
        (Join-Path $auditFrozen 'Content/Runtime/CrewNarrativeCatalog.cs') + $auditExporter
}
New-Item -ItemType Directory -Path $auditBuild | Out-Null
$auditHashes = @(@($auditSourcesByVersion.before + $auditSourcesByVersion.after + $PSCommandPath | Sort-Object -Unique) | ForEach-Object {
    [pscustomobject]@{ Source = $_; SHA256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
})
$auditHashes | Export-Csv -LiteralPath (Join-Path $auditOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
try {
    foreach ($auditVersion in @('before', 'after')) {
        $auditSources = $auditSourcesByVersion[$auditVersion]
        $auditExe = Join-Path $auditBuild ($auditVersion + '.exe')
        $auditArgs = @((Join-Path $auditUnity 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-langversion:9', '-noconfig', '-nostdlib', "-out:$auditExe")
        foreach ($auditAssembly in @('mscorlib.dll', 'System.dll', 'System.Core.dll')) { $auditArgs += '-r:' + (Join-Path $auditFramework $auditAssembly) }
        $auditArgs += $auditSources
        & (Join-Path $auditUnity 'NetCoreRuntime/dotnet.exe') @auditArgs 2>&1 | Tee-Object -FilePath (Join-Path $auditOutput ($auditVersion + '-compile.log'))
        if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $auditVersion" }
        & (Join-Path $auditUnity 'MonoBleedingEdge/bin/mono.exe') $auditExe (Join-Path $auditOutput ($auditVersion + '.tsv')) 2>&1 | Tee-Object -FilePath (Join-Path $auditOutput ($auditVersion + '-export.log'))
        if ($LASTEXITCODE -ne 0) { throw "Export failed: $auditVersion" }
    }
    $auditBefore = @(Import-Csv -LiteralPath (Join-Path $auditOutput 'before.tsv') -Delimiter "`t")
    $auditAfter = @(Import-Csv -LiteralPath (Join-Path $auditOutput 'after.tsv') -Delimiter "`t")
    if ($auditBefore.Count -ne $auditAfter.Count) { throw 'Export record counts changed.' }
    $auditTargets = @{
        boss_route_shattered_sail_reef = @(144, 6, 4, 300)
        boss_route_shattered_sail_reef_tide = @(135, 6, 3, 320)
        boss_route_golden_tide_trade = @(360, 23, 8, 480)
        boss_route_golden_tide_trade_thunder = @(375, 25, 9, 500)
        boss_route_moon_tide_ruins = @(600, 33, 13, 600)
        boss_route_moon_tide_ruins_arcane = @(630, 35, 14, 620)
    }
    $auditLookup = @{}
    foreach ($auditRow in $auditAfter) {
        $auditKey = $auditRow.Boss + '/' + $auditRow.Key
        if ($auditLookup.ContainsKey($auditKey)) { throw ('Duplicate record: ' + $auditKey) }
        $auditLookup[$auditKey] = $auditRow.Value
    }
    $auditDiff = @()
    foreach ($auditRow in $auditBefore) {
        $auditKey = $auditRow.Boss + '/' + $auditRow.Key
        if (!$auditLookup.ContainsKey($auditKey)) { throw ('Missing record: ' + $auditKey) }
        $auditValue = $auditLookup[$auditKey]
        if ($auditRow.Value -ceq $auditValue) { continue }
        if (!$auditTargets.ContainsKey($auditRow.Boss)) { throw ('Non-target boss changed: ' + $auditKey) }
        $auditAllowed = $auditRow.Key -match '^Profile\.(EnemyHull|CannonDamage|Guard|BoardingHealth|BoardingHealthMultiplier)$' -or
            $auditRow.Key -eq 'Boarding.ScriptPressure' -or
            $auditRow.Key -match '^Boarding\.Turn\d+\.Card\d+\.(AttackPercent|BonusPercent)$' -or
            $auditRow.Key -match '^Naval\.Distance\d+\.Step\d+\.Plan\.(Attack|Guard)$' -or
            $auditRow.Key -match '^Naval\.Distance\d+\.Step\d+\.Move\.(Marks|PiercingDamage|ManaDamagePerPoint|ChainFiveBonus|ChainTenBonus|BloodCost|BloodDamageBonus)$' -or
            ($auditRow.Boss -eq 'boss_route_shattered_sail_reef_tide' -and $auditRow.Key -match '^Naval\.Distance\d+\.Step\d+\.Move\.TidalGuard$')
        if (!$auditAllowed) { throw ('Unexpected target mechanic change: ' + $auditKey) }
        $auditOldValue = [decimal]::Parse($auditRow.Value, [Globalization.CultureInfo]::InvariantCulture)
        $auditNewValue = [decimal]::Parse($auditValue, [Globalization.CultureInfo]::InvariantCulture)
        if ($auditRow.Boss -like 'boss_route_shattered*') {
            if ($auditNewValue -ge $auditOldValue) { throw ('First-chart value did not decrease: ' + $auditKey) }
        } elseif ($auditNewValue -le $auditOldValue) { throw ('Level 3/5 value did not increase from prior miniboss baseline: ' + $auditKey) }
        $auditDiff += [pscustomobject]@{ Boss = $auditRow.Boss; Key = $auditRow.Key; Before = $auditRow.Value; After = $auditValue }
    }
    foreach ($auditId in $auditTargets.Keys) {
        $auditFields = @('EnemyHull', 'CannonDamage', 'Guard', 'BoardingHealth')
        for ($auditIndex = 0; $auditIndex -lt $auditFields.Count; $auditIndex++) {
            if ([int]$auditLookup[($auditId + '/Profile.' + $auditFields[$auditIndex])] -ne $auditTargets[$auditId][$auditIndex]) { throw ('Unexpected target value: ' + $auditId + '/' + $auditFields[$auditIndex]) }
        }
        $auditExpectedPressure = if ($auditId -eq 'boss_route_shattered_sail_reef_tide') { [decimal]0.65 } elseif ($auditId -like 'boss_route_golden*') { [decimal]0.82 } else { [decimal]0.70 }
        if ([decimal]::Parse($auditLookup[($auditId + '/Boarding.ScriptPressure')], [Globalization.CultureInfo]::InvariantCulture) -ne $auditExpectedPressure) { throw ('Unexpected boarding pressure: ' + $auditId) }
        if (!($auditDiff | Where-Object { $_.Boss -eq $auditId -and $_.Key -like 'Boarding.*.AttackPercent' })) { throw ('Missing intended boarding attack change: ' + $auditId) }
    }
    $auditBosses = @($auditBefore.Boss | Sort-Object -Unique)
    if ($auditBosses.Count -ne 13) { throw 'Expected thirteen bosses.' }
    # Early-tide TidalGuard is assigned directly from the damage/guard-derived
    # plan at CoreNavalBossBehavior.cs:104-106. Require that formula source itself
    # to stay byte-identical, independently of allowing the derived scalar.
    $auditBehaviorBefore = Join-Path $auditFrozenRoots.before 'Core/Runtime/CoreNavalBossBehavior.cs'
    $auditBehaviorAfter = Join-Path $auditFrozenRoots.after 'Core/Runtime/CoreNavalBossBehavior.cs'
    if ((Get-FileHash -LiteralPath $auditBehaviorBefore -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $auditBehaviorAfter -Algorithm SHA256).Hash) { throw 'Naval boss behavior formulas changed.' }
    # The previous retune export is equivalent to FrozenSourceFinal: only one
    # reward comment differs, as proven by RewardReview/freeze-diff-review.json.
    $auditPrevious = @(Import-Csv -LiteralPath (Join-Path $auditEvidence 'BossNumericAudit/after.tsv') -Delimiter "`t")
    $auditLatestDiff = @()
    foreach ($auditRow in $auditPrevious) {
        $auditKey = $auditRow.Boss + '/' + $auditRow.Key
        $auditValue = $auditLookup[$auditKey]
        if ($auditRow.Value -ceq $auditValue) { continue }
        if ($auditRow.Boss -notlike 'boss_route_shattered*') { throw ('Additional change outside first-chart bosses since prior retune: ' + $auditKey) }
        $auditLatestDiff += [pscustomobject]@{ Boss = $auditRow.Boss; Key = $auditRow.Key; Before = $auditRow.Value; After = $auditValue }
    }
    foreach ($auditHash in $auditHashes) {
        if ((Get-FileHash -LiteralPath $auditHash.Source -Algorithm SHA256).Hash -ne $auditHash.SHA256) { throw ('Source drift during audit: ' + $auditHash.Source) }
    }
    $auditDiff | Export-Csv -LiteralPath (Join-Path $auditOutput 'changes.csv') -NoTypeInformation -Encoding utf8
    $auditLatestDiff | Export-Csv -LiteralPath (Join-Path $auditOutput 'changes-since-retune03.csv') -NoTypeInformation -Encoding utf8
    @(
        'PASS: compiled both complete frozen production Core snapshots; no live-source substitutions.',
        ('PASS: compared ' + $auditBefore.Count + ' scalar records across 13 non-tutorial bosses.'),
        'PASS: all 7 non-target bosses, including six legendary fleets and the treasure boss, retain identical combat profiles and action cycles.',
        'PASS: all 6 regional bosses match their exact final hull/cannon/guard/boarding-health and boarding pressure values.',
        'PASS: first-chart allowed scalar values decrease; level 3/5 allowed values increase from the prior miniboss baseline; support, costs, mechanics and sequencing are unchanged.',
        'PASS: naval cycles sampled at 100m and 400m; only allowed damage/guard and damage-derived values change.',
        'PASS: early-tide TidalGuard follows the unchanged damage/guard formula; CoreNavalBossBehavior.cs is byte-identical.',
        ('PASS: ' + $auditDiff.Count + ' changed scalar records; all belong to the 6 intended bosses.'),
        ('PASS: relative to Retune03, only the first-chart two bosses change (' + $auditLatestDiff.Count + ' scalar records); the other 11 stay identical.'),
        'PASS: all audited source hashes remained stable.'
    ) | Set-Content -LiteralPath (Join-Path $auditOutput 'summary.txt') -Encoding utf8
    Get-Content -LiteralPath (Join-Path $auditOutput 'summary.txt')
}
finally {
    $auditResolvedBuild = [IO.Path]::GetFullPath($auditBuild)
    $auditResolvedOutput = [IO.Path]::GetFullPath($auditOutput).TrimEnd('\', '/')
    if (!$auditResolvedBuild.StartsWith($auditResolvedOutput + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Build cleanup escaped the audit output.' }
    foreach ($auditAncestor in @($auditRoot, (Join-Path $auditRoot 'outputs'), $auditEvidence, $auditOutput, $auditBuild)) {
        if ((Get-Item -LiteralPath $auditAncestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw ('Cleanup blocked by directory link: ' + $auditAncestor) }
    }
    $auditBuildEntries = @(Get-ChildItem -LiteralPath $auditBuild -Force -Recurse)
    if ($auditBuildEntries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Cleanup blocked by linked entry.' }
    $auditBusy = @(Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($auditResolvedBuild, [StringComparison]::OrdinalIgnoreCase) -ge 0 })
    if ($auditBusy.Count -gt 0) { throw 'Cleanup deferred because a process still references this audit Build.' }
    $auditBytes = ($auditBuildEntries | Where-Object { !$_.PSIsContainer } | Measure-Object Length -Sum).Sum
    Remove-Item -LiteralPath $auditResolvedBuild -Recurse -Force
    if (Test-Path -LiteralPath $auditResolvedBuild) { throw 'Build cleanup failed.' }
    ('Removed only ' + $auditResolvedBuild + '; freed ' + $auditBytes + ' bytes. Kept exporter, input snapshots, TSV, hashes and logs.') | Set-Content -LiteralPath (Join-Path $auditOutput 'cleanup.txt') -Encoding utf8
}
