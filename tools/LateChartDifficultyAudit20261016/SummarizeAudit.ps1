param([string]$RunTag = 'SystemsValidation200')
$ErrorActionPreference = 'Stop'
$auditRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../Artifacts/LateChartDifficultyAudit20261016'))
$auditRows = @(Import-Csv -LiteralPath (Join-Path $auditRoot ($RunTag + '-summary.csv')))
$auditGroups = [ordered]@{
    chart3 = @('boss_route_moon_tide_ruins', 'boss_route_moon_tide_ruins_arcane')
    weathered = @('legendary_pirate_iron_oath', 'legendary_pirate_red_widow')
    gilded = @('legendary_pirate_thunder_crown', 'legendary_pirate_ember_ark')
    moonlit = @('legendary_pirate_tide_requiem', 'legendary_pirate_last_horizon')
    optional_guardian = @('treasure_boss_moon_vault')
}
$auditOutput = foreach ($auditGroup in $auditRows | Group-Object school,build,policy) {
    foreach ($auditMap in $auditGroups.Keys) {
        $auditMatches = @($auditGroup.Group | Where-Object { $_.boss -in $auditGroups[$auditMap] })
        if ($auditMatches.Count -ne $auditGroups[$auditMap].Count) { throw "Incomplete encounter set: $($auditGroup.Name) / $auditMap" }
        $auditWorst = $auditMatches | Sort-Object { [double]$_.winPercent } | Select-Object -First 1
        $auditN = [double]$auditWorst.runs
        $auditP = [double]$auditWorst.wins / $auditN
        $auditZ = 1.959963984540054
        $auditLower = ($auditP + $auditZ*$auditZ/(2*$auditN) - $auditZ*[math]::Sqrt($auditP*(1-$auditP)/$auditN + $auditZ*$auditZ/(4*$auditN*$auditN))) / (1+$auditZ*$auditZ/$auditN)
        [pscustomobject]@{
            school = $auditWorst.school; build = $auditWorst.build; policy = $auditWorst.policy
            map = $auditMap; lowerEncounterWinPercent = $auditWorst.winPercent
            lowestEncounter = $auditWorst.boss; wins = $auditWorst.wins; runs = $auditWorst.runs
            wilson95LowerPercent = [math]::Round(100*$auditLower,2)
            bothAtLeast80 = @($auditMatches | Where-Object { [double]$_.winPercent -lt 80 }).Count -eq 0
            timeouts = ($auditMatches | Measure-Object -Property timeouts -Sum).Sum
        }
    }
}
$auditOutput | Export-Csv -LiteralPath (Join-Path $auditRoot ($RunTag + '-map-minima.csv')) -NoTypeInformation -Encoding utf8
$auditOutput | Select-Object school,build,map,lowerEncounterWinPercent,bothAtLeast80 | Format-Table -AutoSize
