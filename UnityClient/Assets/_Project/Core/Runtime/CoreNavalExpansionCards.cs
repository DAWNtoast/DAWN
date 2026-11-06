using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        /// <summary>All target/resource validation is read-only and precedes every cost or play counter.</summary>
        public bool PlayNavalCard(int handIndex, CoreNavalCardPlayOptions options)
        {
            if (Phase == CoreDemoPhase.Battle && ResolveBattleOutcome()) return false;
            if (GetNavalChoiceUnavailableReason(handIndex, options).Length > 0) return false;
            int cardIndex = Battle.Hand[handIndex];
            CoreBattleCard card = GetBattleCard(cardIndex);
            bool retainedThunderAtPlay = CoreThunderRules.IsInThunderField(Battle, true);
            try
            {
            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id);
            options = options ?? new CoreNavalCardPlayOptions { ResourceAmount = spec.ResourceMinimum };
            int resourceAmount = NavalExpansionResourceAmount(spec, options);
            CoreNavalExpansionMechanic kind = spec.Mechanic;
            bool cannon = card.Action == NavalBattleCardAction.Cannon;
            bool bloodBenefit = HasBloodPaidBenefit(card);
            bool preBurn = HasSchoolFire;
            bool enemyOnThunder = NavalExpansionShipOnField(false, CoreNavalFieldKind.Thunder);
            int chain = Battle.Schools.ThunderChain;
            int damage = card.Damage > 0 ? GetBattleCardUnmodifiedFieldDamage(card, NavalBattleStage.Naval) : 0;
            bool fireAttacker = NavalTradeoffField(true, CoreNavalFieldKind.Fire);
            int piercing = NavalExpansionFirstPiercing(card, 0, true);
            NavalExpansionFirstDamageBonus(card, true); // Already included by the public damage quote.
            var shape = kind == CoreNavalExpansionMechanic.HarborExplosion || kind == CoreNavalExpansionMechanic.ThunderCollapse
                ? NavalExpansionShape(kind, options) : new List<VoyagePoint>();
            if (kind == CoreNavalExpansionMechanic.GreatWave)
                foreach (CoreNavalFieldCell cell in Battle.NavalFields.Cells)
                    if (cell.Kind == CoreNavalFieldKind.Tide) shape.Add(cell.PositionMeters);
            List<CoreNavalFieldCell> collapse = kind == CoreNavalExpansionMechanic.ThunderCollapse ? NavalExpansionSelectedThunder(options) : null;

            Battle.Hand.RemoveAt(handIndex);
            Battle.Energy -= GetBattleCardEnergyCost(cardIndex);
            Cargo.Powder -= card.PowderCost;
            Cargo.Cannonballs -= card.CannonballCost;
            PayMana(spec.ManaCost);
            PayBattleHealthCost(true, GetBattleCardHealthCost(card));
            if (spec.ResourceLabel == "法力") PayMana(resourceAmount);
            if (spec.ResourceLabel == "潮势") Battle.Schools.TidePower -= resourceAmount;
            if (spec.ResourceLabel == "咒印") NavalExpansionConsumeCurse(resourceAmount);
            // Naval finishers read the pre-play chain, then clear it after all attack hooks.
            Battle.RecordSuccessfulCardUse(BattleCardUseIdentity(card));
            if (Battle.CardsPlayedThisTurn < int.MaxValue) Battle.CardsPlayedThisTurn++;
            Battle.DiscardPile.Add(cardIndex);
            if (CheckCardCurseExecution()) return FinishCardCurseExecution(card, cardIndex, true);
            if (cannon)
            {
                Battle.PlayerLoadedShot = 0;
                Battle.Schools.OverflowContribution = 0;
            }

            int actual = 0, secondaryActual = 0, enemyBlockBefore = Battle.EnemyBlock;
            bool area = kind == CoreNavalExpansionMechanic.HarborExplosion || kind == CoreNavalExpansionMechanic.GreatWave || kind == CoreNavalExpansionMechanic.ThunderCollapse;
            if (spec.IsField)
                PlaceNavalField(NavalExpansionFieldKind(kind), true, options.AnchorX, options.AnchorY, options.Vertical, options.DirectionX, options.DirectionY);
            else switch (kind)
            {
                case CoreNavalExpansionMechanic.FireMixedCannon:
                    actual = NavalActiveCardDamage(false, damage, piercing);
                    ApplyNavalFireArcanePower(card);
                    break;
                case CoreNavalExpansionMechanic.ArcaneCapacityGuard:
                case CoreNavalExpansionMechanic.ArcaneChargeGuard:
                    ApplyNavalFireArcanePower(card);
                    break;
                case CoreNavalExpansionMechanic.ArcaneFinale:
                case CoreNavalExpansionMechanic.ThunderRelay:
                case CoreNavalExpansionMechanic.ThunderPayoff:
                    actual = NavalActiveCardDamage(false, damage, piercing);
                    break;
                case CoreNavalExpansionMechanic.ThunderCircuit:
                    ResolveNavalThunderCircuit(card);
                    break;
                case CoreNavalExpansionMechanic.MeltingCannon:
                    if (NavalExpansionShipOnField(false, CoreNavalFieldKind.Fire)) Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - 6);
                    actual = NavalActiveCardDamage(false, damage, piercing);
                    break;
                case CoreNavalExpansionMechanic.OilMark: Battle.NavalExpansion.OilPlayerEndsRemaining = 1; break;
                case CoreNavalExpansionMechanic.FireWard:
                    AddSchoolGuard(SchoolSum(card.Block, Battle.PlayerDeckBuff)); GrantFireBurnPrevention(true, false); break;
                case CoreNavalExpansionMechanic.MoveFire:
                    ExpandAllPlayerFireFields(); break;
                case CoreNavalExpansionMechanic.HarborExplosion:
                    var fires = new List<CoreNavalFieldCell>();
                    foreach (VoyagePoint point in shape)
                    {
                        CoreNavalFieldCell cell = GetNavalFieldAt(NavalExpansionCell(point.X), NavalExpansionCell(point.Y));
                        if (cell != null && cell.PlayerOwned && cell.Kind == CoreNavalFieldKind.Fire) fires.Add(cell);
                    }
                    damage = SchoolSum(damage, 4 * RemoveNavalFieldCells(fires));
                    actual = NavalExpansionAreaDamage(shape, damage, piercing, fireAttacker);
                    break;
                case CoreNavalExpansionMechanic.RuneClamp:
                    Battle.NavalExpansion.RuneClamp = true;
                    Battle.NavalExpansion.RuneClampReduction = SchoolSum(Battle.NavalExpansion.RuneClampReduction, 6); break;
                case CoreNavalExpansionMechanic.StarBeam:
                    actual = NavalActiveCardDamage(false, 10, 10);
                    if (Battle.EnemyHull > 0 && Hull > 0) secondaryActual = NavalActiveCardDamage(false, damage, piercing);
                    break;
                case CoreNavalExpansionMechanic.ManaBurst:
                    // The read-only quote above captures all mana before its payment.
                    actual = NavalActiveCardDamage(false, damage, piercing);
                    break;
                case CoreNavalExpansionMechanic.LoadSeal:
                    Battle.NavalExpansion.LoadSeal = true;
                    Battle.NavalExpansion.LoadSealReduction = SchoolSum(Battle.NavalExpansion.LoadSealReduction, 6); break;
                case CoreNavalExpansionMechanic.CursePeel:
                    actual = NavalActiveCardDamage(false, SchoolSum(damage, SchoolProduct(resourceAmount, 6)), piercing);
                    NavalExpansionConsumeCurse(Battle.Schools.CurseMarks); break;
                case CoreNavalExpansionMechanic.TideCannon:
                    actual = NavalActiveCardDamage(false, SchoolSum(damage, SchoolProduct(resourceAmount,
                        CoreNavalExpansionBalance.TideStrikeDamageCoefficient(card.UpgradeLevel))), piercing); break;
                case CoreNavalExpansionMechanic.TideAnchor:
                    if (Cargo.Wood > 0)
                    {
                        Cargo.Wood--;
                        int guard = SchoolSum(card.Block, Battle.PlayerDeckBuff);
                        AddSchoolGuard(guard);
                        Battle.Schools.TidePaidBlock = Battle.PlayerBlock;
                        GrantNavalTideImmunity(true);
                    }
                    else AddSchoolGuard(2);
                    break;
                case CoreNavalExpansionMechanic.RecedingWave:
                    ApplyNavalMovementCap(false, 1);
                    float stern = Battle.EnemyHeadingDegrees * NavalDegreesToRadians;
                    PushNavalShip(false, -(int)Math.Round(Math.Cos(stern)), -(int)Math.Round(Math.Sin(stern)));
                    if (!NavalExpansionActive) return true;
                    break;
                case CoreNavalExpansionMechanic.TideStrip:
                    Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - resourceAmount); break;
                case CoreNavalExpansionMechanic.GreatWave:
                    actual = NavalExpansionAreaDamage(shape, SchoolSum(damage, resourceAmount), 0, fireAttacker); break;
                case CoreNavalExpansionMechanic.ArcCannon:
                    actual = NavalActiveCardDamage(false, damage, piercing);
                    if (Battle.EnemyHull > 0 && Hull > 0 && chain >= 2) secondaryActual = NavalActiveCardDamage(false, 12, 0);
                    break;
                case CoreNavalExpansionMechanic.Precharge:
                    Battle.NavalExpansion.Precharge = true;
                    Battle.NavalExpansion.PrechargeBonus = SchoolSum(Battle.NavalExpansion.PrechargeBonus, 2); break;
                case CoreNavalExpansionMechanic.Pulse:
                    Battle.NavalExpansion.PulseReduction = SchoolSum(Battle.NavalExpansion.PulseReduction, chain >= 2 ? 8 : 4); break;
                case CoreNavalExpansionMechanic.Grounding:
                    if (!Battle.NavalExpansion.GroundingUsed) Battle.NavalExpansion.GroundingReady = true;
                    break;
                case CoreNavalExpansionMechanic.ThunderCollapse:
                    int removed = RemoveNavalFieldCells(collapse);
                    actual = NavalExpansionAreaDamage(shape,
                        CoreNavalExpansionBalance.ThunderCollapseDamage(damage, card.UpgradeLevel, chain, removed), piercing, fireAttacker);
                    break;
                case CoreNavalExpansionMechanic.HealingBan: ApplyBattleHealingBan(false, 2); break;
                case CoreNavalExpansionMechanic.DesperateCannon:
                    actual = NavalActiveCardDamage(false, SchoolSum(damage, bloodBenefit ?
                        CoreNavalExpansionBalance.DesperateCannonBonus(card.UpgradeLevel) : 0), piercing); break;
            }
            if (CheckCardCurseExecution()) return FinishCardCurseExecution(card, cardIndex, true, SchoolSum(actual, secondaryActual));
            // Derived damage never enters this hook, and every active card enters only once.
            int scorchDamage = 0;
            NavalExpansionOnDirectHit(card, SchoolSum(SchoolSum(actual, secondaryActual), Math.Max(0, enemyBlockBefore - Battle.EnemyBlock)), enemyOnThunder);
            if (actual + secondaryActual > 0)
            {
                if (cannon) Morale = ClampMorale((long)Morale + 2);
                scorchDamage = ApplySchoolScorch(preBurn, SchoolSum(actual, secondaryActual));
            }
            if (CheckCardCurseExecution()) return FinishCardCurseExecution(card, cardIndex, true, SchoolSum(SchoolSum(actual, secondaryActual), scorchDamage));
            ApplyAttachedFireAfterAttack(card, !area || NavalExpansionShapeContains(shape, false));
            CompleteSchoolCard(card);
            if (Phase != CoreDemoPhase.Battle) return true;
            if (CheckCardCurseExecution()) return FinishCardCurseExecution(card, cardIndex, true, SchoolSum(SchoolSum(actual, secondaryActual), scorchDamage));
            NavalExpansionCompleteAttack(card);
            if (Phase != CoreDemoPhase.Battle) return true;
            DrawCards(card.Draw);
            Battle.LastAction = "打出「" + card.Name + "」" + (actual + secondaryActual > 0 ? "；敌舰实际船体伤害 " + (actual + secondaryActual) : "") + "。";
            AdvanceGuidedBossCard();
            // Every area victim is damaged before the shared simultaneous-sinking rule runs.
            if (ResolveBattleOutcome()) return true;
            if (!area && NavalExpansionActive) ResolveBattleOutcome();
            return true;
            }
            finally { CompleteNavalThunderFinisher(card, retainedThunderAtPlay); }
        }

        private bool NavalExpansionShipOnField(bool player, CoreNavalFieldKind kind)
        {
            VoyagePoint point = player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            return GetNavalFieldAt(NavalExpansionCell(point.X), NavalExpansionCell(point.Y))?.Kind == kind;
        }

        private int NavalExpansionDirectDamage(bool player, int amount, int piercing) =>
            NavalExpansionApplyDirectDamage(player, amount, piercing, true);

        private int NavalExpansionApplyDirectDamage(bool player, int amount, int piercing, bool gainTide)
        {
            amount = Math.Max(0, amount);
            int bypass = Math.Min(amount, Math.Max(0, piercing)), normal = amount - bypass;
            int block = player ? Battle.PlayerBlock : Battle.EnemyBlock;
            int absorbed = Math.Min(block, normal);
            int health = player ? Hull : Battle.EnemyHull;
            int actual = Math.Min(health, SchoolSum(bypass, normal - absorbed));
            if (player)
            {
                Battle.PlayerBlock -= absorbed;
                RecordPaidTideGuardCore(absorbed, gainTide);
                Hull -= actual;
            }
            else { Battle.EnemyBlock -= absorbed; RecordEnemyTidalAbsorption(absorbed); Battle.EnemyHull -= actual; }
            CheckNavalCurseExecution();
            return actual;
        }

        private bool NavalExpansionShapeContains(IReadOnlyList<VoyagePoint> shape, bool player)
        {
            foreach (VoyagePoint point in shape)
                if (NavalExpansionShipAt(player, NavalExpansionCell(point.X), NavalExpansionCell(point.Y))) return true;
            return false;
        }

        private int NavalExpansionAreaDamage(IReadOnlyList<VoyagePoint> shape, int damage, int piercing, bool fireAttacker)
        {
            bool player = NavalExpansionShapeContains(shape, true), enemy = NavalExpansionShapeContains(shape, false);
            if (player) NavalActiveCardDamage(true, damage, piercing, fireAttacker);
            return enemy ? NavalActiveCardDamage(false, damage, piercing, fireAttacker) : 0;
        }
    }
}
