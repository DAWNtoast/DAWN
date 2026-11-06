$ErrorActionPreference = 'Stop'
$auditRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../outputs/naval-balance-20261024'))
$auditBefore = @(Import-Csv (Join-Path $auditRoot 'card-curves.csv'))
$auditAfter = @(Import-Csv (Join-Path $auditRoot 'card-curves-after.csv'))
$auditNumbers = [ordered]@{
    card_school_fire_b04 = 2009
    card_school_arcane_b03 = 3004
    card_school_arcane_b04 = 3006
    card_school_arcane_n03 = 3007
    card_school_arcane_n04 = 3025
    card_school_tide_n02 = 5008
    card_school_thunder_n06 = 6011
    card_school_blood_b06 = 7006
}
$auditRows = foreach ($auditId in $auditNumbers.Keys) {
    $auditOldRanks = @($auditBefore | Where-Object Id -eq $auditId)
    $auditNewRanks = @($auditAfter | Where-Object Id -eq $auditId)
    $auditMaximum = ($auditNewRanks | Measure-Object Rank -Maximum).Maximum
    if ($auditMaximum -ne ($auditOldRanks | Measure-Object Rank -Maximum).Maximum) { throw "Upgrade cap changed: $auditId" }
    for ($auditRank = 0; $auditRank -le 3; $auditRank++) {
        $auditEffectiveRank = [Math]::Min($auditRank, $auditMaximum)
        $auditOld = $auditOldRanks | Where-Object {[int]$_.Rank -eq $auditEffectiveRank}
        $auditNew = $auditNewRanks | Where-Object {[int]$_.Rank -eq $auditEffectiveRank}
        foreach ($auditCost in @('Energy','Powder','Cannonballs','Draw','Stages','Action')) {
            if ($auditOld.$auditCost -ne $auditNew.$auditCost) { throw "Unexpected change: $auditId / $auditCost" }
        }
        $auditOldX = [regex]::Match($auditOld.Description,'[+＋](\d+)X')
        $auditNewX = [regex]::Match($auditNew.Description,'[+＋](\d+)X')
        $auditResource = switch ($auditId) {
            card_school_arcane_b03 {'法力1'}
            card_school_arcane_b04 {'法力2'}
            card_school_arcane_n03 {'法力2'}
            card_school_arcane_n04 {'全部法力X'}
            card_school_tide_n02 {'全部潮势X'}
            card_school_thunder_n06 {'全部连锁X（雷栅内不扣除）；所选己方雷栅'}
            default {''}
        }
        [pscustomobject][ordered]@{
            Number=$auditNumbers[$auditId]; Id=$auditId; Name=$auditNew.Name
            RequestedRank=$auditRank; EffectiveRank=$auditEffectiveRank; MaximumRank=$auditMaximum
            IsClamped=($auditRank -gt $auditMaximum)
            BeforeBase=[int]$auditOld.Damage; AfterBase=[int]$auditNew.Damage
            BeforeXCoefficient=$(if($auditOldX.Success){[int]$auditOldX.Groups[1].Value}else{0})
            AfterXCoefficient=$(if($auditNewX.Success){[int]$auditNewX.Groups[1].Value}else{0})
            FixedPiercing=$(if($auditId -eq 'card_school_arcane_n03'){10}else{0})
            ConditionalBonus=$(if($auditId -in @('card_school_fire_b04','card_school_blood_b06')){[int]$auditNew.Power}else{0})
            RemovedFieldCellDamage=$(if($auditId -eq 'card_school_thunder_n06'){4}else{0})
            BaseScalesWithFirepower=($auditNew.Action -eq 'Cannon')
            Energy=[int]$auditNew.Energy; Powder=[int]$auditNew.Powder; Cannonballs=[int]$auditNew.Cannonballs
            ResourceCost=$auditResource; Draw=[int]$auditNew.Draw
            BeforeText=$auditOld.Description; AfterText=$auditNew.Description
        }
    }
}
$auditRows | Export-Csv (Join-Path $auditRoot 'card-changes.csv') -NoTypeInformation -Encoding utf8
Write-Output "Exported $($auditRows.Count) requested-rank rows for $($auditNumbers.Count) cards; unavailable +3 ranks explicitly clamp."
