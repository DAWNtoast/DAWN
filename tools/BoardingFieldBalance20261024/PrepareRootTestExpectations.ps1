param([switch]$Rebuild)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskSource=Join-Path $taskRoot 'UnityClient/Assets/_Project/Tests/EditMode'
$taskOutput=Join-Path $taskRoot 'outputs/boarding-field-balance-20261024/TestsOverlay'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
function Edit-Test([string]$Name,[object[]]$Changes){
    $taskPath=Join-Path $taskOutput ($Name+'.cs')
    if((Test-Path -LiteralPath $taskPath) -and !$Rebuild){throw ('Already exists: '+$taskPath)}
    if($Changes[0] -is [string]){$Changes=,@($Changes)}
    $taskText=[IO.File]::ReadAllText((Join-Path $taskSource ($Name+'.cs')))
    foreach($taskChange in $Changes){if(!$taskText.Contains($taskChange[0])){throw ('Missing '+$Name+': '+$taskChange[0])};$taskText=$taskText.Replace($taskChange[0],$taskChange[1])}
    [IO.File]::WriteAllText($taskPath,$taskText)
}
Edit-Test 'BattleTurnCostAndInitiativeTests' @(
    @('CoreBattleBalance.EnemyHealthMultiplier, MidpointRounding.AwayFromZero','small.Battle.Profile.BoardingHealthMultiplier, MidpointRounding.AwayFromZero')
)
Edit-Test 'CoreBoardingBaselineTests' @(
    @('"basic_01", 1, 8','"basic_01", 1, 16'),@('"basic_02", 2, 20','"basic_02", 2, 40'),@('"basic_03", 3, 35','"basic_03", 3, 70'),
    @('"basic_04", 1, 4','"basic_04", 1, 10'),@('"basic_05", 2, 10','"basic_05", 2, 24'),
    @('[TestCase(2, 6, 1)]','[TestCase(2, 12, 1)]'),@('UsesMedicineForSixOtherwiseTwo','UsesMedicineForTwelveOtherwiseTwo'),
    @('[TestCase(2, 5, 1)]','[TestCase(2, 7, 1)]'),@('"basic_11", 4, 0','"basic_11", 8, 0'),@('"basic_12", 0, 2','"basic_12", 0, 4'),
    @('Is.EqualTo(993)','Is.EqualTo(981)'),@('Twelve delayed damage','Twenty-four delayed damage')
)
Edit-Test 'CoreBoardingEnemyTests' @(
    @('Assert.That(attack, Is.EqualTo(10));','Assert.That(attack, Is.EqualTo(8)); // Curse weakness now removes four per mark; thunder weakness remains five.'),
    @('Assert.That(session.Battle.EnemyBlock, Is.EqualTo(7));','Assert.That(session.Battle.EnemyBlock, Is.EqualTo(5)); // Four armor reduction leaves five after the authored guard sequence.')
)
Edit-Test 'ShipBoardingGrowthTests' @(
    @('Is.EqualTo(12), "Rank two scales the authored eight damage times 1.5 once."','Is.EqualTo(24), "Rank two scales the authored sixteen damage times 1.5 once."'),
    @('Is.EqualTo(12), "Boarding uses the upgraded value without another multiplier."','Is.EqualTo(24), "Boarding uses the upgraded value without another multiplier."'),
    @('int damage = 12; // Thrust: base 8','int damage = 24; // Thrust: base 16'),
    @('int guard = 7; // Guard: base 4','int guard = 18; // Guard: base 10'),
    @('int repair = 11; // Bandage: round(base 6','int repair = 22; // Bandage: round(base 12'),
    @('EnemyMaxBoardingHealth, Is.EqualTo(162)','EnemyMaxBoardingHealth, Is.EqualTo(378)'),
    @('PlayerMaxBoardingHealth, Is.EqualTo(76)','PlayerMaxBoardingHealth, Is.EqualTo(152)'),
    @('EnemyMaxBoardingHealth, Is.EqualTo(42)','EnemyMaxBoardingHealth, Is.EqualTo(84)')
)
Edit-Test 'BoardingMedicineTests' @(
    @('Invoke(session, "BInitialize");',('Invoke(session, "BInitialize");' + "`r`n" + '            // Isolate the payment requirement from the normal three mana granted on entry.' + "`r`n" + '            typeof(CoreSchoolBattleState).GetProperty("Mana").SetValue(session.Battle.Schools, 0);')),
    @('GetProperty("Mana").SetValue(session.Battle.Schools, 2)','GetProperty("Mana").SetValue(session.Battle.Schools, 4)'),
    @('Invoke(session, "CTBAddTideGuard", 8)','Invoke(session, "CTBAddTideGuard", 16)'),@('session.Battle.PlayerBlock -= 8','session.Battle.PlayerBlock -= 16'),@('Invoke(session, "CTBAfterEnemyAttack", 8, 0)','Invoke(session, "CTBAfterEnemyAttack", 16, 0)'),
    @('new[] { 9, 12, 15 }[card.UpgradeLevel]','new[] { 32, 41, 55 }[card.UpgradeLevel]'),@('int fallback = blood ? 3 : 2','int fallback = blood ? 4 : 2'),
    @('new[] { 6, 8, 11, 11 }','new[] { 12, 16, 22, 22 }')
)
Edit-Test 'CoreBoardingBossPacingTests' @(
    @('"boss_route_shattered_sail_reef", 2, 119','"boss_route_shattered_sail_reef", 2, 240'),@('"boss_route_shattered_sail_reef_tide", 3, 105','"boss_route_shattered_sail_reef_tide", 3, 220'),
    @('default unupgraded sloop has 60 boarding health','default unupgraded sloop has 120 boarding health'),
    @('Is.LessThanOrEqualTo(32)','Is.LessThanOrEqualTo(64)'),@('Is.LessThanOrEqualTo(45)','Is.LessThanOrEqualTo(90)'),@('leave 15 starter health','leave 30 starter health'),
    @('Is.LessThanOrEqualTo(20)','Is.LessThanOrEqualTo(40)'),@('Is.LessThanOrEqualTo(56)','Is.LessThanOrEqualTo(112)'),@('Is.LessThanOrEqualTo(30)','Is.LessThanOrEqualTo(60)'),
    @('// Fixed recommended ranks 0 / 1 / 2; profile damage cannot redefine the target.','// Recommended ranks 0 / 1 / 2 / 3; profile damage cannot redefine the target.'),
    @('id.StartsWith("boss_route_golden_tide_trade", StringComparison.Ordinal) ? 1.2m : 1.5m;',
      ('id.StartsWith("boss_route_golden_tide_trade", StringComparison.Ordinal) ? 1.2m :' + "`n" +
      '                id == "legendary_pirate_tide_requiem" || id == "legendary_pirate_last_horizon" ? 2m :' + "`n" +
      '                id.StartsWith("legendary_pirate_", StringComparison.Ordinal) ? 1.2m : 1.5m;')),
    @('action.AttackDamage - 8m * multiplier','action.AttackDamage - 16m * multiplier'),@('action.Healed - 6m * multiplier','action.Healed - 12m * multiplier')
)
Edit-Test 'CoreNavalBossCalibrationTests' @(
    @('AllFifteenProfilesPreserveBoardingAndContactParameters','AllFifteenProfilesUseBoardingCalibrationAndPreserveContactParameters'),
    @('// Frozen pre-calibration values: id, crew, HP, melee, guard, HP multiplier,','// Boarding calibration: id, crew, HP, melee, guard, HP multiplier numerator/denominator,'),
    @('9,119,10,4,2.125','9,240,20,10,240/112'),@('9,105,10,6,1.875','9,220,20,15,220/112'),
    @('16,133,15,6,1.5833333333333333333333333333','16,420,30,15,420/168'),@('16,147,15,6,1.75','16,440,30,15,440/168'),
    @('25,240,20,8,2,','25,650,40,20,650/240,'),@('25,240,19,8,2,','25,680,38,20,680/240,'),
    @('55,324,21,14,1.35','55,400,42,35,400/480'),@('52,314,22,8,1.375','52,460,44,20,460/456'),
    @('66,327,24,8,1.15','66,400,48,20,400/568'),@('60,325,23,10,1.25','60,460,46,25,460/520'),
    @('62,342,20,16,1.275','62,820,40,40,820/536'),@('58,328,22,12,1.30','58,750,44,30,750/504'),
    @('31,288,22,12,2,','31,720,44,30,720/288,'),@('tutorial_profile,2,28,5,3,1,','tutorial_profile,2,56,5,3,1,'),@('tutorial_replay,2,42,5,3,1.5,','tutorial_replay,2,84,5,3,1.5,'),
    @('decimal.Parse(c[5],CultureInfo.InvariantCulture)','Ratio(c[5])'),
    @('static CoreEncounter Encounter(string id)',('static decimal Ratio(string value) { string[] parts=value.Split(''/'' ); return decimal.Parse(parts[0],CultureInfo.InvariantCulture) / (parts.Length==1 ? 1m : decimal.Parse(parts[1],CultureInfo.InvariantCulture)); }' + "`n        static CoreEncounter Encounter(string id)"))
)
Write-Output 'Prepared seven isolated root-owned test fixtures with explicit boarding budgets.'
