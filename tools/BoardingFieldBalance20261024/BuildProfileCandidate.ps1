param([string]$Tag='profile-a')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskInputs=Join-Path $taskRoot 'outputs/boarding-field-balance-20261024/Inputs/CoreSnapshot'
$taskOutput=Join-Path $taskRoot ('outputs/boarding-field-balance-20261024/experiments/'+$Tag)
if(Test-Path -LiteralPath $taskOutput){throw 'Choose a fresh candidate tag'}
New-Item -ItemType Directory -Path $taskOutput | Out-Null
function Replace-Task([string]$Text,[string]$Before,[string]$After){
    if(($Text.Split([string[]]@($Before),[StringSplitOptions]::None)).Count -ne 2){throw ('Expected unique source: '+$Before)}
    return $Text.Replace($Before,$After)
}
$taskChanges=@{
    'CoreBattleBalance.cs'=@(
        @('CaptainBoardingHealth = 20','CaptainBoardingHealth = 40'),
        @('BoardingHealthPerSailor = 4','BoardingHealthPerSailor = 8'),
        @('108, 10, 10, 6, 0, 9','108, 10, 20, 6, 0, 9'),
        @('boardingHealthMultiplier: 2.125m, boardingGuard: 4','boardingHealthMultiplier: 240m / 112m, boardingGuard: 10'),
        @('440, 48, 15, 14, 0, 16','440, 48, 30, 14, 0, 16'),
        @('boardingHealthMultiplier: 19m / 12m, boardingGuard: 6','boardingHealthMultiplier: 420m / 168m, boardingGuard: 15'),
        @('920, 76, 20, 28, 0, 25','920, 76, 40, 28, 0, 25'),
        @('boardingHealthMultiplier: LateBossBoardingHealthMultiplier, boardingGuard: 8','boardingHealthMultiplier: 650m / 240m, boardingGuard: 20'),
        @('880, 80, 22, 36, 20, 31','880, 80, 44, 36, 20, 31'),
        @('boardingHealthMultiplier: LateBossBoardingHealthMultiplier, boardingGuard: 12','boardingHealthMultiplier: 720m / 288m, boardingGuard: 30'),
        @('9 + tier * 3, 6 + tier * 3','9 + tier * 3, 12 + tier * 6'),
        @('boardingGuard: 4 + tier * 2','boardingHealthMultiplier: 1.75m + tier * .375m, boardingGuard: 10 + tier * 5'),
        @('// 119 HP for the starter deck, 133 HP for a partially upgraded crew deck.','// Boarding-only budgets account for shared inherited fields and upgraded crew cards.')
    )
    'CoreRouteBossCatalog.cs'=@(
        @('88, 9, 10, 4, 0, 9','88, 9, 20, 4, 0, 9'),
        @('boardingHealthMultiplier: 1.875m, boardingGuard: 6','boardingHealthMultiplier: 220m / 112m, boardingGuard: 15'),
        @('400, 50, 15, 16, 0, 16','400, 50, 30, 16, 0, 16'),
        @('boardingHealthMultiplier: 1.75m, boardingGuard: 6','boardingHealthMultiplier: 440m / 168m, boardingGuard: 15'),
        @('880, 78, 19, 30, 0, 25','880, 78, 38, 30, 0, 25'),
        @('boardingHealthMultiplier: CoreBattleBalance.LateBossBoardingHealthMultiplier, boardingGuard: 8','boardingHealthMultiplier: 680m / 240m, boardingGuard: 20')
    )
    'CoreLegendaryWarships.cs'=@(
        @('240, 21, 21, 18, 12, 55','240, 21, 42, 18, 12, 55'),
        @('boardingHealthMultiplier: 1.35m, boardingGuard: 14','boardingHealthMultiplier: 400m / 480m, boardingGuard: 35'),
        @('420, 52, 22, 20, 8, 52','420, 52, 44, 20, 8, 52'),
        @('boardingHealthMultiplier: 1.375m, boardingGuard: 8','boardingHealthMultiplier: 460m / 456m, boardingGuard: 20'),
        @('240, 22, 24, 12, 8, 66','240, 22, 48, 12, 8, 66'),
        @('boardingHealthMultiplier: 1.15m, boardingGuard: 8','boardingHealthMultiplier: 400m / 568m, boardingGuard: 20'),
        @('440, 48, 23, 18, 8, 60','440, 48, 46, 18, 8, 60'),
        @('boardingHealthMultiplier: 1.25m, boardingGuard: 10','boardingHealthMultiplier: 460m / 520m, boardingGuard: 25'),
        @('1000, 90, 20, 24, 16, 62','1000, 90, 40, 24, 16, 62'),
        @('boardingHealthMultiplier: 1.275m, boardingGuard: 16','boardingHealthMultiplier: 820m / 536m, boardingGuard: 40'),
        @('1240, 96, 22, 30, 8, 58','1240, 96, 44, 30, 8, 58'),
        @('boardingHealthMultiplier: 1.30m, boardingGuard: 12','boardingHealthMultiplier: 880m / 504m, boardingGuard: 30')
    )
}
foreach($taskFile in $taskChanges.Keys){
    $taskText=[IO.File]::ReadAllText((Join-Path $taskInputs $taskFile))
    foreach($taskPair in $taskChanges[$taskFile]){$taskText=Replace-Task $taskText $taskPair[0] $taskPair[1]}
    [IO.File]::WriteAllText((Join-Path $taskOutput $taskFile),$taskText)
}
$taskFile='CoreBoardingBossCatalog.cs'
$taskText=[IO.File]::ReadAllText((Join-Path $taskInputs $taskFile))
$taskText=Replace-Task $taskText 'int rank = legendary != null || route.RouteId == CoreBattleBalance.ThirdRouteId ? 2 :' 'int rank = legendary != null ? (id == "legendary_pirate_tide_requiem" || id == "legendary_pirate_last_horizon" ? 3 : 1) : route.RouteId == CoreBattleBalance.ThirdRouteId ? 2 :'
$taskText=Replace-Task $taskText 'decimal attackScale = reference / Math.Max(1, profile.MeleeDamage);' 'decimal statusScale = reference / Math.Max(1, profile.MeleeDamage); decimal attackScale = reference * 2m / Math.Max(1, profile.MeleeDamage);'
$taskText=Replace-Task $taskText 'decimal guardScale = reference / Math.Max(1, profile.BoardingGuard);' 'decimal guardScale = reference * 2.5m / Math.Max(1, profile.BoardingGuard);'
$taskText=Replace-Task $taskText 'CoreBattleBalance.ScaleBattleValue(card.BurnPercent, attackScale)' 'CoreBattleBalance.ScaleBattleValue(card.BurnPercent, statusScale)'
[IO.File]::WriteAllText((Join-Path $taskOutput $taskFile),$taskText)
Write-Output ('Created isolated boarding profile candidate: '+$taskOutput)
