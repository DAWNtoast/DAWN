param([string]$RunTag = 'RuntimeAudit')
$ErrorActionPreference = 'Stop'
$auditRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$auditEvidence = Join-Path $auditRoot 'outputs/chart-boss-difficulty-20261027'
$auditOutput = Join-Path $auditEvidence $RunTag
if ($RunTag -notmatch '^[A-Za-z0-9_-]+$') { throw 'RunTag must be a single simple directory name.' }
if (Test-Path -LiteralPath $auditOutput) { throw 'Use a new RunTag; existing evidence is preserved.' }
$auditBuild = Join-Path $auditOutput 'Build'
$auditUnity = 'D:/Unity/Hub/Editor/6000.3.23f1/Editor/Data'
$auditFramework = Join-Path $auditUnity 'MonoBleedingEdge/lib/mono/4.7.2-api'
$auditExporter = Join-Path $PSScriptRoot 'ExportBossAudit.cs'
$auditRuntime = @(Get-ChildItem -LiteralPath (Join-Path $auditRoot 'UnityClient/Assets/_Project/Core/Runtime') -Filter '*.cs' | Sort-Object Name)
$auditNarrative = Get-Item -LiteralPath (Join-Path $auditRoot 'UnityClient/Assets/_Project/Content/Runtime/CrewNarrativeCatalog.cs')
$auditChangedNames = @('CoreBattleBalance.cs', 'CoreBoardingBossCatalog.cs', 'CoreRouteBossCatalog.cs')
New-Item -ItemType Directory -Path $auditBuild | Out-Null
$auditHashes = @($auditRuntime + $auditNarrative + (Get-Item -LiteralPath $auditExporter) + (Get-Item -LiteralPath $PSCommandPath) + @($auditChangedNames | ForEach-Object { Get-Item -LiteralPath (Join-Path $auditEvidence ('Before/' + $_)) }) | ForEach-Object {
    [pscustomobject]@{ Source = $_.FullName; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$auditHashes | Export-Csv -LiteralPath (Join-Path $auditOutput 'source-hashes.csv') -NoTypeInformation -Encoding utf8
try {
    foreach ($auditVersion in @('before', 'after')) {
        $auditSources = @($auditRuntime | ForEach-Object {
            if ($auditVersion -eq 'before' -and $_.Name -in $auditChangedNames) { Join-Path $auditEvidence ('Before/' + $_.Name) }
            else { $_.FullName }
        }) + $auditNarrative.FullName + $auditExporter
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
        boss_route_golden_tide_trade = @(576, 38, 11, 696)
        boss_route_golden_tide_trade_thunder = @(576, 40, 13, 760)
        boss_route_moon_tide_ruins = @(1248, 61, 22, 696)
        boss_route_moon_tide_ruins_arcane = @(1320, 59, 24, 624)
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
            $auditRow.Key -match '^Boarding\.Turn\d+\.Card\d+\.(AttackPercent|BonusPercent)$' -or
            $auditRow.Key -match '^Naval\.Distance\d+\.Step\d+\.Plan\.(Attack|Guard)$' -or
            $auditRow.Key -match '^Naval\.Distance\d+\.Step\d+\.Move\.(Marks|PiercingDamage|ManaDamagePerPoint|ChainFiveBonus|ChainTenBonus|BloodCost|BloodDamageBonus)$'
        if (!$auditAllowed) { throw ('Unexpected target mechanic change: ' + $auditKey) }
        if ([decimal]::Parse($auditValue, [Globalization.CultureInfo]::InvariantCulture) -ge [decimal]::Parse($auditRow.Value, [Globalization.CultureInfo]::InvariantCulture)) { throw ('Target value did not decrease: ' + $auditKey) }
        $auditDiff += [pscustomobject]@{ Boss = $auditRow.Boss; Key = $auditRow.Key; Before = $auditRow.Value; After = $auditValue }
    }
    foreach ($auditId in $auditTargets.Keys) {
        $auditFields = @('EnemyHull', 'CannonDamage', 'Guard', 'BoardingHealth')
        for ($auditIndex = 0; $auditIndex -lt $auditFields.Count; $auditIndex++) {
            if ([int]$auditLookup[($auditId + '/Profile.' + $auditFields[$auditIndex])] -ne $auditTargets[$auditId][$auditIndex]) { throw ('Unexpected target value: ' + $auditId + '/' + $auditFields[$auditIndex]) }
        }
        $auditOldHp = [int]($auditBefore | Where-Object { $_.Boss -eq $auditId -and $_.Key -eq 'Profile.BoardingHealth' }).Value
        if ([decimal]$auditTargets[$auditId][3] -ne [decimal]$auditOldHp * [decimal]0.8) { throw ('Boarding health is not exactly 20 percent lower: ' + $auditId) }
        if (!($auditDiff | Where-Object { $_.Boss -eq $auditId -and $_.Key -like 'Boarding.*.AttackPercent' })) { throw ('No reduced boarding attack: ' + $auditId) }
    }
    $auditBosses = @($auditBefore.Boss | Sort-Object -Unique)
    if ($auditBosses.Count -ne 13) { throw 'Expected thirteen bosses.' }
    foreach ($auditHash in $auditHashes) {
        if ((Get-FileHash -LiteralPath $auditHash.Source -Algorithm SHA256).Hash -ne $auditHash.SHA256) { throw ('Source drift during audit: ' + $auditHash.Source) }
    }
    $auditDiff | Export-Csv -LiteralPath (Join-Path $auditOutput 'changes.csv') -NoTypeInformation -Encoding utf8
    @(
        'PASS: compiled production Core before and after the three scoped runtime changes.',
        ('PASS: compared ' + $auditBefore.Count + ' scalar records across 13 non-tutorial bosses.'),
        'PASS: all 9 non-target bosses have identical profiles, complete naval action cycles, and complete boarding decks.',
        'PASS: all 4 target bosses have the intended hull/cannon/guard/boarding-health values; boarding health is exactly 20% lower.',
        'PASS: target boarding attack percentages decrease; support, resource costs, mechanics, identities, and action sequencing are unchanged.',
        'PASS: naval cycles sampled at 100m and 400m; only allowed damage/guard and damage-derived values decrease.',
        ('PASS: ' + $auditDiff.Count + ' changed scalar records; all belong to the 4 intended bosses.'),
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
