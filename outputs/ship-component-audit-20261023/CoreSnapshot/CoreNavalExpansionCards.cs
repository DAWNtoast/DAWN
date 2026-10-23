using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        /// <summary>All target/resource validation is read-only and precedes every cost or play counter.</summary>
        public bool PlayNavalCard(int handIndex, CoreNavalCardPlayOptions options)
        {
            if (GetNavalChoiceUnavailableReason(handIndex, options).Length > 0) return false;
            int cardIndex = Battle.Hand[handIndex];
            CoreBattleCard card = GetBattleCard(cardIndex);
            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id);
            options = options ?? new CoreNavalCardPlayOptions { ResourceAmount = spec.ResourceMinimum };
            CoreNavalExpansionMechanic kind = spec.Mechanic;
            bool cannon = card.Action == NavalBattleCardAction.Cannon;
            bool fireRound = cannon && Battle.PlayerFireRounds > 0;
            bool lowHull = (long)Hull * 100 <= (long)MaxHull * 40;
            bool preBurn = HasSchoolFire;
            bool enemyOnThunder = NavalExpansionShipOnField(false, CoreNavalFieldKind.Thunder);
            int chain = Battle.Schools.ThunderChain;
            int damage = card.Damage > 0 ? GetBattleCardDamage(card, NavalBattleStage.Naval) : 0;
            int piercing = NavalExpansionFirstPiercing(card, 0, true);
            NavalExpansionFirstDamageBonus(card, true); // Already included by the public damage quote.
            var shape = kind == CoreNavalExpansionMechanic.HarborExplosion || kind == CoreNavalExpansionMechanic.GreatWave
                ? NavalExpansionShape(kind, options) : new List<VoyagePoint>();
            List<CoreNavalFieldCell> collapse = kind == CoreNavalExpansionMechanic.ThunderCollapse ? NavalExpansionSelectedThunder(options) : null;
            if (collapse != null)
                foreach (CoreNavalFieldCell cell in collapse) shape.Add(new VoyagePoint(cell.X * NavalGridCellMeters, cell.Y * NavalGridCellMeters));

            Battle.Hand.RemoveAt(handIndex);
            Battle.Energy -= GetBattleCardEnergyCost(cardIndex);
            Cargo.Powder -= card.PowderCost;
            Cargo.Cannonballs -= card.CannonballCost;
            PayMana(spec.ManaCost);
            Hull -= CoreSpecialCardRules.HealthCost(card, Battle);
            if (spec.ResourceLabel == "潮势") Battle.Schools.TidePower -= options.ResourceAmount;
            if (spec.ResourceLabel == "咒印") NavalExpansionConsumeCurse(options.ResourceAmount);
            if (spec.ResourceLabel == "连锁") Battle.Schools.ThunderChain -= options.ResourceAmount;
            if (kind == CoreNavalExpansionMechanic.ThunderCollapse) Battle.Schools.ThunderChain = 0;
            Battle.RecordSuccessfulCardUse(BattleCardUseIdentity(card));
            if (Battle.CardsPlayedThisTurn < int.MaxValue) Battle.CardsPlayedThisTurn++;
            Battle.DiscardPile.Add(cardIndex);
            if (cannon)
            {
                Battle.PlayerLoadedShot = 0;
                Battle.Schools.OverflowContribution = 0;
                if (fireRound) Battle.PlayerFireRounds--;
            }

            int actual = 0, secondaryActual = 0;
            bool area = kind == CoreNavalExpansionMechanic.HarborExplosion || kind == CoreNavalExpansionMechanic.GreatWave || kind == CoreNavalExpansionMechanic.ThunderCollapse;
            if (spec.IsField)
                PlaceNavalField(NavalExpansionFieldKind(kind), true, options.AnchorX, options.AnchorY, options.Vertical, options.DirectionX, options.DirectionY);
            else switch (kind)
            {
                case CoreNavalExpansionMechanic.MeltingCannon:
                    if (NavalExpansionShipOnField(false, CoreNavalFieldKind.Fire)) Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - 6);
                    actual = NavalExpansionDirectDamage(false, damage, piercing);
                    break;
                case CoreNavalExpansionMechanic.OilMark: Battle.NavalExpansion.OilPlayerEndsRemaining = 2; break;
                case CoreNavalExpansionMechanic.FireWard:
                    AddSchoolGuard(SchoolSum(card.Block, Battle.PlayerDeckBuff)); GrantNavalFireImmunity(true); break;
                case CoreNavalExpansionMechanic.MoveFire:
                    MoveNavalFireCell(true, options.SourceX, options.SourceY, options.AnchorX, options.AnchorY); break;
                case CoreNavalExpansionMechanic.HarborExplosion:
                    var fires = new List<CoreNavalFieldCell>();
                    foreach (VoyagePoint point in shape)
                    {
                        CoreNavalFieldCell cell = GetNavalFieldAt(NavalExpansionCell(point.X), NavalExpansionCell(point.Y));
                        if (cell != null && cell.PlayerOwned && cell.Kind == CoreNavalFieldKind.Fire) fires.Add(cell);
                    }
                    damage = SchoolSum(damage, 4 * RemoveNavalFieldCells(fires));
                    actual = NavalExpansionAreaDamage(shape, damage, piercing);
                    break;
                case CoreNavalExpansionMechanic.RuneClamp:
                    Battle.NavalExpansion.RuneClamp = true;
                    Battle.NavalExpansion.RuneClampReduction = SchoolSum(Battle.NavalExpansion.RuneClampReduction, 6); break;
                case CoreNavalExpansionMechanic.StarBeam:
                    actual = NavalExpansionDirectDamage(false, damage, piercing);
                    secondaryActual = NavalExpansionDirectDamage(false, 10, 10); break;
                case CoreNavalExpansionMechanic.LoadSeal:
                    Battle.EnemyLoadedShot = Math.Max(0, Battle.EnemyLoadedShot - 6); break;
                case CoreNavalExpansionMechanic.CursePeel:
                    actual = NavalExpansionDirectDamage(false, SchoolSum(damage, options.ResourceAmount * 6), piercing);
                    NavalExpansionConsumeCurse(Battle.Schools.CurseMarks); break;
                case CoreNavalExpansionMechanic.TideCannon:
                    actual = NavalExpansionDirectDamage(false, SchoolSum(damage, options.ResourceAmount * 2), piercing); break;
                case CoreNavalExpansionMechanic.TideAnchor:
                    if (Cargo.Wood > 0)
                    {
                        Cargo.Wood--;
                        int guard = SchoolSum(card.Block, Battle.PlayerDeckBuff), before = Battle.PlayerBlock;
                        AddSchoolGuard(guard);
                        Battle.Schools.TidePaidBlock = SchoolSum(Battle.Schools.TidePaidBlock, Battle.PlayerBlock - before);
                        GrantNavalAnchor(true);
                    }
                    else AddSchoolGuard(2);
                    break;
                case CoreNavalExpansionMechanic.RecedingWave:
                    ApplyNavalMovementSlow(false);
                    PushNavalShip(false, options.DirectionX, options.DirectionY);
                    if (!NavalExpansionActive) return true;
                    break;
                case CoreNavalExpansionMechanic.TideStrip:
                    Battle.EnemyBlock = Math.Max(0, Battle.EnemyBlock - options.ResourceAmount); break;
                case CoreNavalExpansionMechanic.GreatWave:
                    actual = NavalExpansionAreaDamage(shape, SchoolSum(damage, options.ResourceAmount * 2), piercing); break;
                case CoreNavalExpansionMechanic.ArcCannon:
                    actual = NavalExpansionDirectDamage(false, damage, piercing);
                    if (Battle.EnemyHull > 0 && Hull > 0 && enemyOnThunder) secondaryActual = NavalExpansionDirectDamage(false, 6, 0);
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
                    RemoveNavalFieldCells(collapse);
                    actual = NavalExpansionAreaDamage(shape, SchoolSum(damage, 4 * chain), piercing);
                    break;
                case CoreNavalExpansionMechanic.HealingBan: Battle.NavalExpansion.EnemyHealingHalved = true; break;
                case CoreNavalExpansionMechanic.DesperateCannon:
                    actual = NavalExpansionDirectDamage(false, SchoolSum(damage, lowHull ? 12 : 0), piercing); break;
            }
            // Derived damage never enters this hook, and every active card enters only once.
            if (actual + secondaryActual > 0)
            {
                NavalExpansionOnDirectHit(card, actual + secondaryActual, enemyOnThunder);
                if (fireRound) ApplySchoolBurn(Battle.PlayerFireRoundDamage, 2);
                if (cannon) Morale = ClampMorale((long)Morale + 2);
                ApplySchoolScorch(preBurn, actual + secondaryActual);
            }
            CompleteSchoolCard(card);
            NavalExpansionCompleteAttack(card);
            DrawCards(card.Draw);
            Battle.LastAction = "打出「" + card.Name + "」" + (actual + secondaryActual > 0 ? "；敌舰实际船体伤害 " + (actual + secondaryActual) : "") + "。";
            AdvanceGuidedBossCard();
            // Every area victim is damaged before the shared simultaneous-sinking rule runs.
            if (ResolveBattleOutcome()) return true;
            if (kind == CoreNavalExpansionMechanic.GreatWave)
            {
                bool pushPlayer = NavalExpansionShapeContains(shape, true), pushEnemy = NavalExpansionShapeContains(shape, false);
                if (pushPlayer) PushNavalShip(true, options.DirectionX, options.DirectionY);
                if (NavalExpansionActive && Hull > 0 && Battle.EnemyHull > 0 && pushEnemy) PushNavalShip(false, options.DirectionX, options.DirectionY);
            }
            if (!area && NavalExpansionActive) ResolveBattleOutcome();
            return true;
        }

        private bool NavalExpansionShipOnField(bool player, CoreNavalFieldKind kind)
        {
            VoyagePoint point = player ? Battle.PlayerPositionMeters : Battle.EnemyPositionMeters;
            return GetNavalFieldAt(NavalExpansionCell(point.X), NavalExpansionCell(point.Y))?.Kind == kind;
        }

        private int NavalExpansionDirectDamage(bool player, int amount, int piercing)
        {
            amount = Math.Max(0, amount);
            int bypass = Math.Min(amount, Math.Max(0, piercing)), normal = amount - bypass;
            int block = player ? Battle.PlayerBlock : Battle.EnemyBlock;
            int absorbed = Math.Min(block, normal);
            int health = player ? Hull : Battle.EnemyHull;
            int actual = Math.Min(health, SchoolSum(bypass, normal - absorbed));
            if (player)
            {
                int ordinaryBlock = Math.Max(0, Battle.PlayerBlock - Battle.Schools.TidePaidBlock);
                int absorbedPaid = Math.Max(0, absorbed - ordinaryBlock);
                Battle.PlayerBlock -= absorbed;
                Battle.Schools.TidePaidBlock = Math.Max(0, Battle.Schools.TidePaidBlock - absorbedPaid);
                Hull -= actual;
            }
            else { Battle.EnemyBlock -= absorbed; Battle.EnemyHull -= actual; }
            return actual;
        }

        private bool NavalExpansionShapeContains(IReadOnlyList<VoyagePoint> shape, bool player)
        {
            foreach (VoyagePoint point in shape)
                if (NavalExpansionShipAt(player, NavalExpansionCell(point.X), NavalExpansionCell(point.Y))) return true;
            return false;
        }

        private int NavalExpansionAreaDamage(IReadOnlyList<VoyagePoint> shape, int damage, int piercing)
        {
            bool player = NavalExpansionShapeContains(shape, true), enemy = NavalExpansionShapeContains(shape, false);
            if (player) NavalExpansionDirectDamage(true, damage, piercing);
            return enemy ? NavalExpansionDirectDamage(false, damage, piercing) : 0;
        }
    }
}
