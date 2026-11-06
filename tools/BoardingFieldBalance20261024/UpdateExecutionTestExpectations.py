from pathlib import Path
import re

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'outputs/boarding-field-balance-20261024/Inputs/TestsBeforeMerge'
if not SOURCE.exists():SOURCE=ROOT/'UnityClient/Assets/_Project/Tests/EditMode'
DEST=ROOT/'outputs/boarding-field-balance-20261024/TestsOverlay'
DEST.mkdir(parents=True,exist_ok=True)
files={}
notes=[]
def edit(name,method,pairs,reason):
    if name not in files: files[name]=(SOURCE/(name+'.cs')).read_text(encoding='utf-8-sig')
    text=files[name]
    if method:
        start=text.index('public void '+method+'(')
        end=text.find('\n        [Test',start+1)
        if end<0:end=text.find('\n        private',start+1)
        if end<0:end=len(text)
    else:start,end=0,len(text)
    part=text[start:end]
    for old,new in pairs:
        if old not in part:raise ValueError(f'{name}.{method}: missing {old!r}')
    replacements=dict(pairs)
    part=re.sub('|'.join(re.escape(x) for x in sorted(replacements,key=len,reverse=True)),lambda m:replacements[m.group()],part)
    files[name]=text[:start]+part+text[end:]
    notes.append((name,method or 'fixture',reason))

edit('CoreBoardingFireArcaneThunderTests',None,[
('[TestCase("arcane_04", 42)]','[TestCase("arcane_04", 66)]'),('[TestCase("arcane_06", 25)]','[TestCase("arcane_06", 35)]'),
('SetMana(s, 1);','SetMana(s, 2);')], 'Arcane base constants rebase; fixed mana payments are 2, not 1.')
edit('CoreBoardingFireArcaneThunderTests','ThunderCombo_FromThreeEnergyAndZeroChain_DealsFortyTwoAndFinisherAddsOne',[('Is.EqualTo(42)','Is.EqualTo(78)')], '6 spark + 18 chain-one strike + 54 chain-two finisher; same three energy.')
edit('CoreBoardingFireArcaneThunderTests','ArcaneDelayedAttack_ConsumesAllManaAndLocksSixty',[('Is.EqualTo(60)','Is.EqualTo(96)')], '72 + 8*3 delayed damage.')
edit('CoreBoardingFireArcaneThunderTests','BurnPayment_UsesSelectedCardsAndBurnedAttackCannotBeRecovered',[('Is.EqualTo(5)','Is.EqualTo(4)')], 'Attack-card burning now applies four actual burn layers.')
edit('CoreBoardingFireArcaneThunderTests','BurnVariableAmount_ComesFromSelectedCards_NotResourceAmount',[('Is.EqualTo(10)','Is.EqualTo(7)')], '3 + 2*2 fuel layers; still counts selected physical cards.')
edit('CoreBoardingFireArcaneThunderTests','ResolveSelectedDelayed_RemovesQueueEntryAndNeverTriggersAgain',[('Is.EqualTo(36)','Is.EqualTo(72)'),('Is.EqualTo(1)','Is.EqualTo(2)'),('// no mana: 36 delayed','// no mana: 72 delayed'),('// gains 3 mana','// gains 6 mana')], 'Delayed base72; generated6 minus actual collection cost4 leaves2.')
edit('CoreBoardingFireArcaneThunderTests','FireHealing_RequiresPowderWithMedicine_ButNoPowderWithoutMedicine',[('Is.EqualTo(10)','Is.EqualTo(20)')], 'Paid healing doubles; unprovisioned two-health fallback unchanged.')
edit('CoreBoardingFireArcaneThunderTests','ThunderWeakness_WaitsForNextAttackAndIsConsumedOnlyOnce',[
('ThunderWeakness, Is.EqualTo(10)','ThunderWeakness, Is.EqualTo(20)'),('(expiresWithoutAttack ? 0 : 10)','(expiresWithoutAttack ? 0 : 0)'),
('(expiresWithoutAttack ? 10 : 0)','(expiresWithoutAttack ? 20 : 0)'),('(expiresWithoutAttack ? 10 : 20)','(expiresWithoutAttack ? 0 : 20)')], '20 weakness blocks the 20 attack once; expiration, zero-chain behavior unchanged.')
edit('CoreBoardingFireRevisionTests','BathingFlame_QuotesAndDealsDamageAfterAddingThreeBurn',[('Is.EqualTo(52)','Is.EqualTo(82)'),('health - 52','health - 82')], '60 base plus 2*11 burn, no status rebase.')
edit('CoreBoardingFireRevisionTests','FirePot_ReappliesOnlyAtTheNextPlayerStart',[('Is.EqualTo(6)','Is.EqualTo(4)'),('Is.EqualTo(12)','Is.EqualTo(8)')], 'Four layers now, four once at next start.')
edit('CoreBoardingFireRevisionTests','BurningThroughGuard_DoesNotTriggerRetaliation',[('PlayerBlock, Is.EqualTo(6)','PlayerBlock, Is.EqualTo(14)'),('enemy - 4','enemy - 2')], '16 guard minus2 natural burn, then two attacks each reflect1 remaining burn.')
edit('CoreBoardingInheritedFieldEffectsTests',None,[
('[TestCase("blood_01", 3)]','[TestCase("blood_01", 6)]'),('[TestCase("blood_02", 4)]','[TestCase("blood_02", 8)]'),
('[TestCase("blood_04", 3)]','[TestCase("blood_04", 6)]'),('[TestCase("blood_05", 3)]','[TestCase("blood_05", 6)]'),('[TestCase("fire_14", 2)]','[TestCase("fire_14", 4)]'),
('[TestCase("thunder_13", 70)]','[TestCase("thunder_13", 110)]'),('[TestCase("thunder_14", 56)]','[TestCase("thunder_14", 92)]'),('[TestCase("thunder_15", 176)]','[TestCase("thunder_15", 272)]')], 'Explicit authored payments and chain10 damage, inherited effects unchanged.')
edit('CoreBoardingInheritedFieldEffectsTests','BloodPoolGuardUsesExistingAutomaticTideGuardConversion',[('Is.EqualTo(14)','Is.EqualTo(28)')], '22 granted guard plus6 actual blood payment.')
edit('CoreBoardingInheritedFieldEffectsTests','ThunderPreservesChainOnOrdinaryCardsAndStartsWithoutPassiveGainOrDamage',[('PlayerBlock, Is.EqualTo(4)','PlayerBlock, Is.EqualTo(10)')], 'Ordinary shield now10, no passive chain changes.')
edit('CoreBoardingInheritedFieldEffectsTests','ThunderRepeatsBlockableOverloadForEachChainIncreaseWithoutBloodPoolOrAttackReactions',[('Is.EqualTo(994)','Is.EqualTo(988)')], 'Two spark attacks each6, environmental overload still no retaliation.')
edit('CoreBoardingInheritedFieldEffectsTests','ExpiredThunderRestoresFinisherConsumptionAndNormalResetRules',[('Is.EqualTo(930)','Is.EqualTo(890)')], 'Chain10 finisher110, then consumption/reset remains exact.')

name='CoreBoardingCurseTideBloodTests'
edit(name,'CurseMarks_AddWithoutTheOldCapAndPersistWithoutPeriodicDamage',[('Is.EqualTo(9)','Is.EqualTo(18)')],'Three six-mark applications persist unchanged.')
edit(name,'CurseConsumption_RemovesOnlyPaidLayersAndEntanglementChangesThreshold',[('Is.EqualTo(5)','Is.EqualTo(10)')],'Twelve marks minus explicit two-mark draw payment.')
edit(name,'CurseGuardAndLink_ApplyMarksEvenWhenAttackIsFullyBlocked',[('Is.EqualTo(3)','Is.EqualTo(6)'),('Is.EqualTo(4)','Is.EqualTo(8)')],'Six shield; two seed marks, two attack marks, two per link.')
edit(name,'CurseContract_SeedsAfterDamageWhileCounterPrecedesAttack',[('Is.EqualTo(2)','Is.EqualTo(4)'),('hp - 4','hp - 8')],'Contract four marks; counter two damage per actual mark.')
edit(name,'CurseWeakness_UsesCurrentMarksOnEveryAttackInItsWindow',[('hp - 1','hp'),('hp - 2','hp'),('six persistent','twelve persistent')],'6+12 weakness completely blocks test attack10 on both turns.')
edit(name,'CurseArmor_RemovesCurrentGuardAndReducesEveryGrantInItsWindow',[('Is.EqualTo(10)','Is.EqualTo(0)'),('Is.EqualTo(5)','Is.EqualTo(0)'),('current marks (3)','current marks (6)')],'Remove8+2*6; subsequent guard10 minus4+6.')
edit(name,'CurseFinale_ConsumesAllMarksAndIgnoresGuardWithoutSeedingLinks',[('hp - 68','hp - 136')],'40+8*12 piercing damage; same mark consumption and link prohibition.')
edit(name,'CurseSupports_RequireMarksWithoutConsumingThemAndAllowRepeatedEnergyConversion',[('Is.EqualTo(513)','Is.EqualTo(526)'),('Is.EqualTo(6)','Is.EqualTo(12)'),('Is.EqualTo(10)','Is.EqualTo(20)'),('Is.EqualTo(9)','Is.EqualTo(18)')],'Paid healing26; retained twelve marks; shield20.')
edit(name,'CurseUnprovisionedHeal_DoesNotSpendMarks',[('Is.EqualTo(3)','Is.EqualTo(6)')],'Six marks persist; fallback remains2.')
edit(name,'CursePainAndBloodPursuit_ReadMarksAndActualPenetration',[('hp - 9','hp - 18'),('Is.EqualTo(3)','Is.EqualTo(6)'),('EnemyBlock = 6','EnemyBlock = 12')],'12+6 pain; provision enough shield to fully absorb new12 pursuit damage.')
edit(name,None,[('[TestCase(8, 3, 0)]','[TestCase(17, 3, 0)]'),('[TestCase(3, 0, 2)]','[TestCase(3, 0, 11)]')],'Attack17 spends retained14 then tide3; attack3 retains11.')
edit(name,'MixedArcaneAndTideGuard_ConsumesOrdinaryThenRetainedThenTide',[('Is.EqualTo(20)','Is.EqualTo(44)')],'14 retained plus30 tidal guard; consumption ordering unchanged.')
edit(name,'LeaveTideConvertsCashoutGuardAndPersistsWithoutGeneratingFreePower',[('Is.EqualTo(19)','Is.EqualTo(20)'),('Is.EqualTo(17)','Is.EqualTo(16)'),('Is.EqualTo(25)','Is.EqualTo(32)')],'Absorb20, pay4, converted shield16 replenishes16 actual tide.')
edit(name,'TideCashoutGuard_CannotGenerateNewPower',[('PlayerBlock, Is.EqualTo(8)','PlayerBlock, Is.EqualTo(16)'),('TidePower, Is.EqualTo(8)','TidePower, Is.EqualTo(6)')],'Ten tide minus4; ordinary cashout shield16 produces no tide.')
edit(name,'TideSmallCounterAndHeal_UseActualStoredPower',[('s = Battle();','s = Battle(12);'),('enemy - 10','enemy - 22'),('enemy - 13','enemy - 34'),('Is.EqualTo(510)','Is.EqualTo(520)'),('TidePower, Is.EqualTo(4)','TidePower, Is.EqualTo(0)')],'Start tide10, attack12+10; pierce8 plus12; next actual block12 pays healing cost12.')
edit(name,'TideAttacks_ConsumeAllPowerAndIgnoreClientResourceAmounts',[('hp - 35','hp - 52'),('hp - 93','hp - 142')],'Actual reserve20 gives32+20 then60+30, full consumption each play.')
edit(name,'TideAmbushAndWeakness_ResolveAfterOneReducedEnemyAttack',[('s = Battle();','s = Battle(20);'),('player - 7','player - 14'),('enemy - 16','enemy - 32')],'Generate enough actual tide to pay8+4; weakness6 reduces20 to14; ambush32 once.')
edit(name,'TideMedicineBath_PaysPerPlayAndAddsToDecayingRecovery',[('s = Battle();','s = Battle(20);'),('Is.EqualTo(510)','Is.EqualTo(513)'),('Is.EqualTo(519)','Is.EqualTo(525)'),('Is.EqualTo(527)','Is.EqualTo(536)')],'Twenty stored tide permits16 payment; layers11+fallback2 decay13,12,11.')
edit(name,'TideSupportCards_ConsumePowerAndDrawWithinHandLimit',[('s = Battle();','s = Battle(20);'),('TidePower, Is.EqualTo(2)','TidePower, Is.EqualTo(4)')],'Generate20 actual tide for16 draw/energy payments; draw limits unchanged.')
edit(name,'BloodCosts_ArePaidBeforeDesperateBonusAndCannotBeShielded',[('Is.EqualTo(399)','Is.EqualTo(396)'),('enemy - 24','enemy - 48')],'Pay6 then desperation; damage32 plus16 oath, shield cannot pay life.')
edit(name,'BloodFixedLifePayment_CannotBeReducedByClientAndAllowsRepeatedEnergyCards',[('Is.EqualTo(996)','Is.EqualTo(992)'),('enemy - 34','enemy - 68'),('PlayerBlock, Is.EqualTo(11)','PlayerBlock, Is.EqualTo(22)')],'Heavy cost8 and damage68; blood guard22.')
edit(name,'BloodDrain_UsesAllActualHealthDamageOnEveryPlay',[('Is.EqualTo(501)','Is.EqualTo(510)'),('Is.EqualTo(510)','Is.EqualTo(528)'),('hp + 9','hp + 18')],'18 drain minus8 shield restores10; subsequent actual18 restores18.')
edit(name,'BloodTwoHitAttack_ChecksFirstHitPenetrationAndAppliesOathOnlyOnce',[('EnemyBlock = 20','EnemyBlock = 40'),('hp - 12','hp - 24'),('hp - 48','hp - 96'),('First 12+8 is entirely blocked: second hit remains 12.','First 24+16 is entirely blocked: second hit remains24.')],'Full shield test preserved at new40; next attack40+32; oath once per card.')
edit(name,'BloodDebt_TriggersOnEnemyAttackIncludingBlockedDamageButNotLifeCosts',[('enemy - 8','enemy - 16'),('enemy - 16','enemy - 32')],'Debt16 per actual enemy attack; exactly two-turn window.')
edit(name,'BloodDesperateHealingAndDelayedDamage_LockInAtPlay',[('enemy - 22','enemy - 44'),('Is.EqualTo(413)','Is.EqualTo(426)')],'Desperate delayed44 and paidheal26, fallback5 unchanged.')
edit(name,'BloodDesperateCards_HaveRevisedBonusesAndLastResortExhaustsEachCopy',[('enemy - 17','enemy - 34'),('PlayerBlock, Is.EqualTo(12)','PlayerBlock, Is.EqualTo(24)')],'Desperate34 attack24 shield; exhaust invariant unchanged.')

name='CoreThunderBoardingRevisionTests'
edit(name,None,[('[TestCase("thunder_13", 70)]','[TestCase("thunder_13", 110)]'),('[TestCase("thunder_14", 56)]','[TestCase("thunder_14", 92)]'),('[TestCase(9, 82)]','[TestCase(9, 128)]'),('[TestCase(10, 176)]','[TestCase(10, 272)]'),('[TestCase(19, 284)]','[TestCase(19, 416)]'),('[TestCase(20, 592)]','[TestCase(20, 864)]'),('[TestCase(21, 616)]','[TestCase(21, 896)]'),('[TestCase(2, 10)]','[TestCase(2, 20)]')],'Finisher slopes7/6/8; 10/20 thresholds still multiply twice. Weakness20.')
edit(name,'MultipleHitsUsePrePlayChainAndAwardOnlyOneUncappedChain',[('Is.EqualTo(42)','Is.EqualTo(66)')],'Two hits of6+3*9; one chain awarded per card.')
edit(name,'ParalysisPersistsAcrossIdleTurnsAndReducesNextAttackWithoutHalfDamageCap',[('ThunderWeakness, Is.EqualTo(10)','ThunderWeakness, Is.EqualTo(20)')],'New20 weakness persists; preview cannot spend it.')
edit(name,'ResidualHitsImmediatelyForDelayedAttackAndConsumesOneChargePerCard',[('Is.EqualTo(4)','Is.EqualTo(8)'),('Is.EqualTo(14)','Is.EqualTo(28)'),('Is.EqualTo(18)','Is.EqualTo(34)'),('Is.EqualTo(32)','Is.EqualTo(62)'),('Two five-damage authored hits plus one four-damage residual hit.','Two nine-damage authored hits plus one eight-damage residual hit, after the first residual.')],'Residual8 and delayed28; chain1 twohits9 each, one residual per card.')
name='CoreBloodBoardingRevisionTests'
edit(name,None,[('[TestCase("blood_11", 401, 7)]','[TestCase("blood_11", 401, 14)]'),('[TestCase("blood_11", 400, 17)]','[TestCase("blood_11", 400, 34)]'),('[TestCase("blood_13", 401, 12)]','[TestCase("blood_13", 401, 24)]'),('[TestCase("blood_13", 400, 22)]','[TestCase("blood_13", 400, 44)]'),('[TestCase(401, 5)]','[TestCase(401, 10)]'),('[TestCase(400, 12)]','[TestCase(400, 24)]'),('[TestCase("blood_09", 500, 1, 6)]','[TestCase("blood_09", 500, 1, 12)]'),('[TestCase("blood_09", 400, 1, 13)]','[TestCase("blood_09", 400, 1, 26)]'),('[TestCase("card_special_crew_broken_anchor_doctor_hector", 500, 1, 7)]','[TestCase("card_special_crew_broken_anchor_doctor_hector", 500, 1, 14)]'),('[TestCase("card_special_crew_broken_anchor_doctor_hector", 400, 1, 16)]','[TestCase("card_special_crew_broken_anchor_doctor_hector", 400, 1, 32)]'),('[TestCase(0, 1000, 9)]','[TestCase(0, 1000, 18)]'),('[TestCase(8, 1000, 1)]','[TestCase(8, 1000, 10)]')],'Direct damage/guard/paidheal doubled; 40% boundary and unprovisioned fallback unchanged. Drain uses actual18 minus shield.')
edit(name,'LifePaymentsBypassGuardAndEachEnergyCopyCanBeUsed',[('Is.EqualTo(994)','Is.EqualTo(988)')],'Two explicit6 life payments bypass100guard.')
edit(name,'OathRequiresDesperationWhenPlayedAndRemainsAfterHealing',[('Is.EqualTo(16)','Is.EqualTo(32)'),('Is.EqualTo(32)','Is.EqualTo(64)'),('Is.EqualTo(48)','Is.EqualTo(96)'),('Is.EqualTo(56)','Is.EqualTo(112)')],'16 base plus16 oath per card, then16 after expiry.')
edit(name,'OathBonusIsLockedIntoDelayedAttackAndAppliedOncePerPlayedCard',[('Is.EqualTo(30)','Is.EqualTo(60)'),('Is.EqualTo(36)','Is.EqualTo(72)'),('Is.EqualTo(66)','Is.EqualTo(132)')],'Delayed44+16; twin strike24+32+16 oath once.')
edit(name,'LifeDrainDoesNotIncludeHealthRemovedByImmediateCurseExecution',[('Is.EqualTo(509)','Is.EqualTo(518)')],'Only18 actual attack life loss heals; execution remainder excluded.')
edit(name,'EveryDrainCopyHealsAndHealingBanBlocksDrainAndRecoveryUntilOwnEndsExpire',[('Is.EqualTo(518)','Is.EqualTo(536)'),('Is.EqualTo(521)','Is.EqualTo(539)')],'Two18 drains; manually queued5 recovery still decays and eventually heals3.')
edit(name,'DebtRetaliatesForEveryAttackIncludingFullyBlockedHitsAndPreviewMatches',[('new[] { 992, 984, 976 }','new[] { 984, 968, 952 }'),('Is.EqualTo(976)','Is.EqualTo(952)'),('Is.EqualTo(952)','Is.EqualTo(904)')],'16 retaliation each of three blocked attacks for exactly two enemy turns.')

name='CoreBoardingTransactionTests'
edit(name,'BurnSelectionsOnBothSidesOfPlayedSlot_ExhaustExactlyThosePhysicalCards',[('Is.EqualTo(10)','Is.EqualTo(7)')],'Two selected cards produce3+2*2 layers, exact physical exhaustion retained.')
edit(name,None,[('Mana(s, 1);','Mana(s, 2);')],'Reading costs two mana; fully fund existing transaction/RNG test.')
edit(name,'RetainedGuardBatches_ExpireIndependentlyWithoutRefreshingOlderShield',[('Is.EqualTo(5)','Is.EqualTo(14)'),('Is.EqualTo(10)','Is.EqualTo(28)')],'Two14 guard batches expire independently.')
name='CoreBoardingManuscriptRulesTests'
edit(name,'Curse_EntanglementReducesThresholdWithoutDamageOrExpiry',[('Is.EqualTo(9)','Is.EqualTo(18)')],'Three6 marks; threshold and no periodic damage unchanged.')
edit(name,'Curse_RetiredTickBonusCannotCausePeriodicDamage',[('Is.EqualTo(3)','Is.EqualTo(6)')],'Single6 mark seed; retired periodic damage still absent.')
edit(name,'ContinuousRecovery_MedicineAddsToOneDecayingPool',[('Is.EqualTo(6)','Is.EqualTo(8)'),('Is.EqualTo(513)','Is.EqualTo(517)'),('Is.EqualTo(4)','Is.EqualTo(6)')],'Existing1 plus paid7 layers, ticks8 and7, decay one each.')
edit(name,'UpgradedTreatment_DescriptionAndAppliedLayersAgree',[('CoreCardUpgradeDesign.ScaleEffect(card, 5, rank)','new[] { 7, 8, 10, 12 }[rank]')],'Explicit sustained recovery curve prevents squared lifetime inflation.')
edit(name,'TideBath_CanStackWithoutBlockingAndPaysEachPlay',[('ChargeTide(s, 12)','ChargeTide(s, 20)'),('ContinuousRecovery, Is.EqualTo(8)','ContinuousRecovery, Is.EqualTo(11)'),('ContinuousRecovery, Is.EqualTo(10)','ContinuousRecovery, Is.EqualTo(13)')],'Actual20 tide pays16; paid11 recovery then fallback2, remaining4.')
edit(name,'ManuscriptBaseDamageAndUncappedSelfBurnBonusApply',[('hp - 8','hp - 16'),('hp - 15','hp - 30'),('Is.EqualTo(52)','Is.EqualTo(82)'),('hp - 52','hp - 82')],'Curse16 seeds2, pain12+2; bath60+2*11.')
edit(name,'FirePot_AppliesOnceAtTheFollowingPlayerStart',[('Is.EqualTo(12)','Is.EqualTo(8)')],'Two batches4 burn, only one deferred batch.')
edit(name,'Weakness_StacksPersistsAndExpiresPerApplication',[('"BEnemyAttack", 10','"BEnemyAttack", 20'),('Is.EqualTo(4)','Is.EqualTo(8)'),('Is.EqualTo(10)','Is.EqualTo(20)')],'Use20 attack to observe one12 weakness vs two24 weakness vs expiry, preserving stacking coverage.')
edit(name,'ArmorReduction_PersistsAcrossRepeatedGuardGrants',[('"CTBEnemyBlockGain", 10','"CTBEnemyBlockGain", 20'),('Is.EqualTo(5)','Is.EqualTo(10)')],'Use20 guard grant to observe one10 reduction and two20 reductions.')
edit(name,'Counter_TriggersBeforeEveryAttackAndCanPreventLethalHit',[('hp - 24','hp - 48')],'Two reactions at2*12 marks; lethal-prevention branch unchanged.')
edit(name,'ArcaneRetain_KeepsAllGrantedGuardAndEchoHasNoOldDamageCap',[('Is.EqualTo(5)','Is.EqualTo(14)'),('Is.EqualTo(21)','Is.EqualTo(33)')],'14 retained shield; half of48+6*3 is33.')
edit(name,'EchoUpgrade_ScalesTheFinalHalfDamageWithoutASeparateCap',[('ScaleEffect(echo, 21,','ScaleEffect(echo, 33,')],'Upgrade applies once to half of66 authored damage.')
edit(name,'AllManaAttacks_UseTheEntireOverLimitPoolInPaymentQuoteAndDamage',[('? 9 *','? 11 *'),('? 66 : 92','? 90 : 128')],'Seven overlimit mana adds7 attacks or6/8 per mana, unchanged marginal resources.')
edit(name,'DelayedEcho_WaitsForPrimarySettlementThenStartsItsOwnDelayEvenWhenFullyGuarded',[('// Two remaining mana lock the main delayed attack at 52.','// Four remaining mana lock the main delayed attack at104.'),('ScaleEffect(echoCard, 26,','ScaleEffect(echoCard, 52,'),('Is.EqualTo(52)','Is.EqualTo(104)'),('EnemyBlock = 100','EnemyBlock = 200'),('Is.EqualTo(48)','Is.EqualTo(96)')],'Generate6/pay2 leaves4; delayed72+8*4=104, fully blocked by200; echo52 independent delay.')
edit(name,'RemovedEnergyCardOnceLimits_DoNotBlockSecondPlay',[('ChargeTide(s, 8)','ChargeTide(s, 16)')],'Fund each16 tide payment without changing repeat-use assertions.')
edit(name,'DelayedChoice_CanResolveTheSelectedLaterEffectAndRejectsStaleChoice',[('AvailableMana, Is.EqualTo(3)','AvailableMana, Is.EqualTo(6)'),('hp - 60','hp - 120'),('Amount, Is.EqualTo(12)','Amount, Is.EqualTo(24)')],'Six mana generates delayed120, selected later effect resolves; basic24 remains queued.')
name='CoreTideBoardingRevisionTests'
edit(name,'DeepTideGrantsFifteenGuardAndCashoutPaysTwoPower',[('Is.EqualTo(15)','Is.EqualTo(30)'),('MinResource, Is.EqualTo(2)','MinResource, Is.EqualTo(4)'),('MaxResource, Is.EqualTo(2)','MaxResource, Is.EqualTo(4)'),('TidePower, Is.EqualTo(4)','TidePower, Is.EqualTo(2)'),('PlayerBlock, Is.EqualTo(17)','PlayerBlock, Is.EqualTo(40)'),('TideGuard, Is.EqualTo(9)','TideGuard, Is.EqualTo(24)')],'Thirty tidal shield absorbs6; pay4 tide for16 ordinary shield.')
edit(name,'LeaveTideConvertsCurrentAndFutureGuardWithoutErasingRetention',[('Is.EqualTo(19)','Is.EqualTo(48)'),('Is.EqualTo(23)','Is.EqualTo(58)'),('Is.EqualTo(5)','Is.EqualTo(14)'),('BlockDamage(s, 5)','BlockDamage(s, 14)'),('Is.EqualTo(12)','Is.EqualTo(21)'),('Is.EqualTo(4)','Is.EqualTo(10)')],'24+14retained+10shield=48; next10, actual7 blocked; turn preserves14, actual14 absorbed yields21 total.')
edit(name,'OrdinaryGuardIsSpentFirstAndAttacksChargeOnlyOnce',[('PlayerBlock -= 7','PlayerBlock -= 13'),('"BAfterEnemyAttack", 7','"BAfterEnemyAttack", 13'),('Is.EqualTo(12)','Is.EqualTo(27)')],'Ten ordinary guard then3 tidal absorption yields exactly3 tide once.')
edit(name,'BurningChargesTideGuardWithoutTriggeringAttackOnlyRetaliation',[('TideGuard, Is.EqualTo(10)','TideGuard, Is.EqualTo(25)')],'Thirty tidal guard absorbs actual5 burn; same5 resource and no attack-only reaction.')
edit(name,'BoardingImportsLargeNavalReserveWithoutClampingAndQuotesSpendItSafely',[('30L +','60L +')],'Base60 plus actual1.5 per reserve; saturation safe and full consumption unchanged.')

name='CoreCurseBoardingRevisionTests'
edit(name,'CurseSupportThresholdsDoNotConsumeMarks',[('AddMarks(s, 2)','AddMarks(s, 4)'),('PlayerBlock, Is.EqualTo(10)','PlayerBlock, Is.EqualTo(20)'),('Is.EqualTo(913)','Is.EqualTo(926)'),('CurseMarks, Is.EqualTo(2)','CurseMarks, Is.EqualTo(4)'),('CurseMarks, Is.EqualTo(1)','CurseMarks, Is.EqualTo(2)')],'Four marks fund threshold-only supports, paid26heal, only draw consumes2.')
edit(name,'FinalePaysMarksBeforePiercingDamage',[('EnemyBoardingHealth = 80','EnemyBoardingHealth = 100')],'Use100 target so40+8*6=88 leaves12, preserving assertion marks paid before lethal threshold check.')
edit(name,'ContractMarksStopTheNextEnemyCardImmediately',[('EnemyBoardingHealth = 4','EnemyBoardingHealth = 8')],'Four marks per contract reaction: first equals50%, second exceeds; third attack still stopped.')
edit(name,'DamageExecutionReportCountsOnlyTheHitAndStopsFollowupDraw',[('伤害 4；咒印消灭敌方','伤害 8；咒印消灭敌方')],'Basic draw attack deals actual8 before execution, followup draw still stopped.')
edit(name,'DelayedAttackRecordsActualDamageAndZeroFinalHealthSeparately',[('HealthLost, Is.EqualTo(12)','HealthLost, Is.EqualTo(24)')],'Delayed24 actual loss then curse removes remaining6, distinct accounting.')
edit(name,'LinkedCurseExecutionStopsThunderChainAfterTheAttackSurvives',[('EnemyBoardingHealth = 7','EnemyBoardingHealth = 11'),('CurseMarks, Is.EqualTo(3)','CurseMarks, Is.EqualTo(4)')],'Enemy11 minus spark6 leaves5; initial2marks survive, link adds2 to4 and executes before chain gain.')
name='CoreThunderBloodWinConditionTests'
edit(name,None,[('[TestCase(false, 0, 996)]','[TestCase(false, 0, 992)]'),('[TestCase(false, 9, 987)]','[TestCase(false, 9, 983)]'),('[TestCase(true, 0, 987)]','[TestCase(true, 0, 974)]')],'Total26 active life cost followed by actual18/9/0 drain.')
edit(name,'BoardingThunderReachesTwoChainAndFundsFinisherWithinThreeStartingEnergy',[('Is.EqualTo(37)','Is.EqualTo(68)')],'6 spark+8 drawattack+54 chain2finisher, three-energy economy unchanged.')
edit(name,'BoardingBloodPaysForResourceAndDamageThenRecoversOnlyActualUnblockedLife',[('Is.EqualTo(997)','Is.EqualTo(994)'),('Is.EqualTo(987)','Is.EqualTo(974)'),('59 - drainBlock','118 - drainBlock')],'Costs6+6+6+8; attacks32+68+18 minus actual guard.')
edit(name,'BoardingThunderCanSpendItsRemainingBudgetForSeventyTwoDamage',[('Is.EqualTo(72)','Is.EqualTo(128)')],'6+8+two12+22+68 finisher; actual legal deck budget unchanged.')
edit(name,'BoardingBloodAddsLifeFundedResourcesToTheSameOrdinaryEnergySupport',[('Is.EqualTo(87)','Is.EqualTo(174)'),('Is.EqualTo(996)','Is.EqualTo(992)')],'68+32+18+24+32=174; 26 life costs minus18 real healing leaves992.')
edit(name,'OrdinaryBoardingCanProduceFiftyFiveDamageWithoutPayingLife',[('Is.EqualTo(55)','Is.EqualTo(110)')],'70+40 ordinary attacks from same deck/energy sequence.')
name='CoreTideAudit20261024Tests'
edit(name,None,[
('[TestCase("card_boarding_tide_11", 5, 1, 2, 5, 1)]','[TestCase("card_boarding_tide_11", 11, 1, 2, 11, 1)]'),('[TestCase("card_boarding_tide_11", 6, 1, 10, 0, 0)]','[TestCase("card_boarding_tide_11", 12, 1, 20, 0, 0)]'),('[TestCase("card_boarding_tide_11", 6, 0, 2, 6, 0)]','[TestCase("card_boarding_tide_11", 12, 0, 2, 12, 0)]'),
('[TestCase("card_boarding_tide_12", 7, 1, 2, 7, 1)]','[TestCase("card_boarding_tide_12", 15, 1, 2, 15, 1)]'),('[TestCase("card_boarding_tide_12", 8, 1, 8, 0, 0)]','[TestCase("card_boarding_tide_12", 16, 1, 11, 0, 0)]'),('[TestCase("card_boarding_tide_12", 8, 0, 2, 8, 0)]','[TestCase("card_boarding_tide_12", 16, 0, 2, 16, 0)]'),
('[TestCase("card_special_crew_white_shell_doctor_mirei", 7, 1, 2, 7, 1)]','[TestCase("card_special_crew_white_shell_doctor_mirei", 15, 1, 2, 15, 1)]'),('[TestCase("card_special_crew_white_shell_doctor_mirei", 8, 1, 8, 0, 0)]','[TestCase("card_special_crew_white_shell_doctor_mirei", 16, 1, 11, 0, 0)]')],'Test exactly one below and at actual12/16 tide payment thresholds, paidheal20/sustain11, missing supplies still fallback2 without spending medicine.')
name='CoreBoardingEnemySchoolTests'
edit(name,'TideGuardActuallyAbsorbsBuildsPowerAndRetainsOnlyOneEnemyWindow',[('Play(session, "basic_01")','Play(session, "basic_11")')],'Use eight-damage draw attack to preserve partial twelve-guard/retention fixture; authored basic strike is now16.')
edit(name,'TidePowerCountsAllTidalBlockedDamageAndPersistsIfUnused',[('enemy - 15','enemy - 50')],'Seventy attack minus20 tidalguard leaves50actual life loss; generated resource still20.')
edit(name,'PreviewDecaysPlayerSelfBurnBeforeCalculatingEveryFireRetaliation',[('enemy - 4','enemy - 2'),('enemy - 8','enemy - 4'),('enemy - 12','enemy - 6')],'Selfburn3 decays2 then three attack counters each2; preview exactly matches commit.')
name='CoreSchoolRebalanceRulesTests'
edit(name,'CurseWardAlias_RequiresTwoMarksWithoutSpendingThem_ForTenGuardThatExpires',[('Is.EqualTo(9)','Is.EqualTo(18)'),('PlayerBlock, Is.EqualTo(10)','PlayerBlock, Is.EqualTo(20)')],'Three six-mark casts; shield20 and expiry semantics retained.')
edit(name,'OnlyTideGuardGeneratesMomentum_WithoutAnyWoodPayment',[('unpaidTide ? 4 : 0','unpaidTide ? 10 : 0')],'Fully absorbed ten tidalguard yields10, ordinaryguard yieldszero.')
edit(name,'TideAbsorbsAboveTheOldLimitAndUnusedReservePersistsAtTheNextEndTurn',[('TidePaidBlock, Is.EqualTo(20)','TidePaidBlock, Is.EqualTo(50)'),('TidePower, Is.EqualTo(20)','TidePower, Is.EqualTo(44)')],'Five casts generate50guard; configured enemy attack44 absorbs44, only actual absorbed damage yields tide and persists.')
edit(name,'FireAliasesRequirePowder_UnlessAFuelSacrificeGrantsAFreeUse',[('EnemyBurnDamage, Is.EqualTo(4)','EnemyBurnDamage, Is.EqualTo(3)')],'Fire02 alias applies3 authoredburn withfreepowder; freeuse count unchanged.')
edit(name,'ThunderGrowthRemainsLinearBeyondFifty_WithoutGrantingGuardOrFieldOverloadInBoarding',[('Is.EqualTo(8 + i)','Is.EqualTo(16 + 2 * i)'),('before - 8 - i','before - 16 - 2 * i')],'Each chain point adds2 exactly; no cap, shield, or overload outside grid.')
edit(name,'BloodPaymentIgnoresGuardAndLeavesOne_WhileCostsDoNotUpgrade',[('PlayerBoardingHealth = 4','PlayerBoardingHealth = 7')],'Six fixed life payment leavesone at eachupgrade; guard999 cannot pay.')

name='CoreSchoolBattleMechanicsTests'
edit(name,'FireAlias_UsesFourBurnAtLowCrew_AndNewLayersJoinTheRemainingTotal',[('Is.EqualTo(4)','Is.EqualTo(3)'),('Is.EqualTo(3)','Is.EqualTo(2)'),('Is.EqualTo(7)','Is.EqualTo(5)'),('hp - 7','hp - 5'),('Is.EqualTo(6)','Is.EqualTo(4)')],'Threeburn eachcast; firstdecays2, secondstacks5 then4, no crew scaling.')
edit(name,'BoardingFireGuard_ReactsOnceToAttackAbsorption_AfterBurnConsumesGuard',[('PlayerBlock, Is.EqualTo(8)','PlayerBlock, Is.EqualTo(16)'),('LastEnemyGuardAbsorbed, Is.EqualTo(6)','LastEnemyGuardAbsorbed, Is.EqualTo(10)'),('before - 2','before - 1')],'16 shield minus2naturalburn absorbs10enemyattack; counter1remainingburn, thenzero nextturn.')
edit(name,'BoardingFireExhaust_BurnsAnotherAttackForFiveLayers_WithoutLegacyCharges',[('before - 8','before - 16'),('EnemyBurnDamage, Is.EqualTo(5)','EnemyBurnDamage, Is.EqualTo(4)')],'Sixteen direct attack and4burn from sacrificed attack, no legacy charges.')
edit(name,'BoardingAndNavalMarksPersistAndNavalMarkDamageResolvesImmediately',[('initial - 8','initial - 16'),('CurseMarks, Is.EqualTo(1)','CurseMarks, Is.EqualTo(2)')],'Boarding curse16 seeds2persistentmarks; naval section unchanged.')
edit(name,'TideAliases_RequireActualAbsorptionButNoWood_AndSpendAllMomentum',[('PlayerBlock, Is.EqualTo(4)','PlayerBlock, Is.EqualTo(10)'),('TidePower, Is.EqualTo(4)','TidePower, Is.EqualTo(10)'),('before - 10','before - 22'),('before - 16','before - 34'),('base six damage','base twelve damage')],'Absorb10 actualshield, then12+10damage and12atzerotide, fullconsumption.')
edit(name,'BloodCost_RejectsLethalPaymentBeforeAnyMutation',[('int cost = 3','int cost = 6')],'Lethal cost boundary now6, failed play remains atomic.')
edit(name,'BloodMendAlias_QuotesFixedMedicineHealing_WithoutDoubleCrewScaling',[('ActualRepair, Is.EqualTo(6)','ActualRepair, Is.EqualTo(12)'),('Is.EqualTo(986)','Is.EqualTo(992)'),('.Repair, Is.EqualTo(8)','.Repair, Is.EqualTo(15)')],'Paidheal12 withrank1 rounding15, no crew factor.')
edit(name,'BoardingBurnTicksButCurseMustBeSpent_WithoutCrewScaling',[('enemy - 8 - 8 - 28 - 4','enemy - 16 - 16 - 56 - 3'),('enemy - 8 - 8 - 28','enemy - 16 - 16 - 56'),('enemy - 8','enemy - 16'),('EnemyBurnDamage, Is.EqualTo(4)','EnemyBurnDamage, Is.EqualTo(3)')],'Fire16+burn3, curse16seeds2 thenfinisher40+16 consumesmarks; sameburntick3.')
edit(name,'BoardingAliasBonusesAndCostsStayFixedAcrossCrewCounts',[('enemy - 6 - 11 - 16 - 10','enemy - 12 - 22 - 32 - 22'),('enemy - 6 - 11 - 16','enemy - 12 - 22 - 32'),('enemy - 6 - 11','enemy - 12 - 22'),('enemy - 6','enemy - 12'),('Mana, Is.EqualTo(2)','Mana, Is.EqualTo(3)'),('own - 3','own - 6')],'Arcane12gains2; chain3 strike22; blood32cost6; tide10actualabsorb yields22attack, independentcrew.')

for fixture,old,new in [
('CoreBoardingFireArcaneThunderTests','ThunderCombo_FromThreeEnergyAndZeroChain_DealsFortyTwoAndFinisherAddsOne','ThunderCombo_FromThreeEnergyAndZeroChain_DealsSeventyEightAndFinisherAddsOne'),
('CoreBoardingFireArcaneThunderTests','ArcaneDelayedAttack_ConsumesAllManaAndLocksSixty','ArcaneDelayedAttack_ConsumesAllManaAndLocksNinetySix'),
('CoreTideBoardingRevisionTests','DeepTideGrantsFifteenGuardAndCashoutPaysTwoPower','DeepTideGrantsThirtyGuardAndCashoutPaysFourPower'),
('CoreSchoolBattleMechanicsTests','FireAlias_UsesFourBurnAtLowCrew_AndNewLayersJoinTheRemainingTotal','FireAlias_UsesThreeBurnAtLowCrew_AndNewLayersJoinTheRemainingTotal'),
('CoreSchoolBattleMechanicsTests','BoardingFireExhaust_BurnsAnotherAttackForFiveLayers_WithoutLegacyCharges','BoardingFireExhaust_BurnsAnotherAttackForFourLayers_WithoutLegacyCharges'),
('CoreSchoolRebalanceRulesTests','CurseWardAlias_RequiresTwoMarksWithoutSpendingThem_ForTenGuardThatExpires','CurseWardAlias_RequiresFourMarksWithoutSpendingThem_ForTwentyGuardThatExpires'),
('CoreThunderBloodWinConditionTests','BoardingThunderCanSpendItsRemainingBudgetForSeventyTwoDamage','BoardingThunderCanSpendItsRemainingBudgetForOneHundredTwentyEightDamage'),
('CoreThunderBloodWinConditionTests','OrdinaryBoardingCanProduceFiftyFiveDamageWithoutPayingLife','OrdinaryBoardingCanProduceOneHundredTenDamageWithoutPayingLife')]:
    edit(fixture,None,[('public void '+old+'(','public void '+new+'(')],'Rename numerical test title to match independently verified expectation.')

for name,text in files.items():(DEST/(name+'.cs')).write_text(text,encoding='utf-8')
import csv
with (DEST/'execution-expectation-rationale.csv').open('w',newline='',encoding='utf-8') as f:
    w=csv.writer(f);w.writerow(['fixture','method','reason']);w.writerows(notes)
print(f'Wrote {len(files)} reviewed execution fixtures / {len(notes)} rationale groups')
