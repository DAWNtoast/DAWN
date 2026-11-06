using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    public enum NavalVfxRole { Cannon, Strike, Affliction, Guard, Heal, Empower, Field, Area, Dispel, Push }

    /// <summary>
    /// Authored naval cast intent. The caller supplies the final card definition and committed
    /// results; this plan neither executes rules nor infers an attack from incidental damage.
    /// </summary>
    public readonly struct NavalCardVfxPlan
    {
        public readonly NavalVfxRole Role;
        public readonly BattleSchoolAttackKind School;
        public readonly bool HasSchool;
        // A visual salvo uses one authoritative total; it does not invent per-projectile damage.
        public readonly int SalvoCount;
        public readonly bool TargetsEnemy;
        public readonly bool UsesSelectedArea;
        public readonly bool AffectsFields;
        public bool IsAttack => Role == NavalVfxRole.Cannon || Role == NavalVfxRole.Strike || Role == NavalVfxRole.Area;

        private NavalCardVfxPlan(NavalVfxRole role, BattleSchoolAttackKind school, bool hasSchool,
            int salvoCount = 1, bool usesSelectedArea = false, bool affectsFields = false)
        {
            Role = role; School = school; HasSchool = hasSchool; SalvoCount = salvoCount;
            TargetsEnemy = role == NavalVfxRole.Cannon || role == NavalVfxRole.Strike ||
                role == NavalVfxRole.Affliction || role == NavalVfxRole.Dispel || role == NavalVfxRole.Push;
            UsesSelectedArea = usesSelectedArea; AffectsFields = affectsFields;
        }

        public static NavalCardVfxPlan For(CoreBattleCard card, int chainBefore = 0)
        {
            bool hasSchool = BattleSchoolAttackPresentation.TrySchool(card?.SchoolId, out BattleSchoolAttackKind school);
            // A boarding-only self-burn or delayed card cannot become a naval projectile.
            if (card == null || !card.IsAllowedInStage(NavalBattleStage.Naval))
                return new NavalCardVfxPlan(NavalVfxRole.Empower, school, hasSchool);

            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id);
            if (spec != null)
            {
                if (spec.IsField) return new NavalCardVfxPlan(NavalVfxRole.Field, school, hasSchool,
                    usesSelectedArea: true, affectsFields: true);
                switch (spec.Mechanic)
                {
                    case CoreNavalExpansionMechanic.MeltingCannon:
                    case CoreNavalExpansionMechanic.FireMixedCannon:
                    case CoreNavalExpansionMechanic.TideCannon:
                    case CoreNavalExpansionMechanic.DesperateCannon:
                        return new NavalCardVfxPlan(NavalVfxRole.Cannon, school, hasSchool);
                    case CoreNavalExpansionMechanic.ArcCannon:
                        return new NavalCardVfxPlan(NavalVfxRole.Cannon, school, hasSchool, chainBefore >= 2 ? 2 : 1);
                    case CoreNavalExpansionMechanic.StarBeam:
                        return new NavalCardVfxPlan(NavalVfxRole.Strike, school, hasSchool, 2);
                    case CoreNavalExpansionMechanic.ArcaneFinale:
                    case CoreNavalExpansionMechanic.ManaBurst:
                    case CoreNavalExpansionMechanic.CursePeel:
                    case CoreNavalExpansionMechanic.ThunderRelay:
                    case CoreNavalExpansionMechanic.ThunderPayoff:
                        return new NavalCardVfxPlan(NavalVfxRole.Strike, school, hasSchool);
                    case CoreNavalExpansionMechanic.HarborExplosion:
                    case CoreNavalExpansionMechanic.ThunderCollapse:
                        return new NavalCardVfxPlan(NavalVfxRole.Area, school, hasSchool,
                            usesSelectedArea: true, affectsFields: true);
                    case CoreNavalExpansionMechanic.GreatWave:
                        // The target is every existing tide cell, not an enemy-ship fallback.
                        return new NavalCardVfxPlan(NavalVfxRole.Area, school, hasSchool, affectsFields: true);
                    case CoreNavalExpansionMechanic.OilMark:
                    case CoreNavalExpansionMechanic.RuneClamp:
                    case CoreNavalExpansionMechanic.LoadSeal:
                    case CoreNavalExpansionMechanic.Pulse:
                    case CoreNavalExpansionMechanic.HealingBan:
                        return new NavalCardVfxPlan(NavalVfxRole.Affliction, school, hasSchool);
                    case CoreNavalExpansionMechanic.TideStrip:
                        return new NavalCardVfxPlan(NavalVfxRole.Dispel, school, hasSchool);
                    case CoreNavalExpansionMechanic.RecedingWave:
                        return new NavalCardVfxPlan(NavalVfxRole.Push, school, hasSchool);
                    case CoreNavalExpansionMechanic.FireWard:
                    case CoreNavalExpansionMechanic.TideAnchor:
                    case CoreNavalExpansionMechanic.ArcaneCapacityGuard:
                    case CoreNavalExpansionMechanic.ArcaneChargeGuard:
                        return new NavalCardVfxPlan(NavalVfxRole.Guard, school, hasSchool);
                    case CoreNavalExpansionMechanic.MoveFire:
                        return new NavalCardVfxPlan(NavalVfxRole.Field, school, hasSchool, affectsFields: true);
                    case CoreNavalExpansionMechanic.Precharge:
                    case CoreNavalExpansionMechanic.Grounding:
                    case CoreNavalExpansionMechanic.ThunderCircuit:
                        return new NavalCardVfxPlan(NavalVfxRole.Empower, school, hasSchool);
                }
            }

            CoreSchoolMechanic mechanic = card.SchoolEffect?.Mechanic ?? CoreSchoolMechanic.None;
            bool field = CoreComponentFieldCards.IsFieldCard(card);
            // Mixed component cards retain the primary strike; field changes have their own
            // authoritative NavalFieldMotion events and are never a second invented attack.
            if (card.Damage > 0)
                return new NavalCardVfxPlan(card.Action == NavalBattleCardAction.Cannon ? NavalVfxRole.Cannon : NavalVfxRole.Strike,
                    school, hasSchool, affectsFields: field);

            switch (mechanic)
            {
                case CoreSchoolMechanic.ComponentFieldExpand:
                case CoreSchoolMechanic.ComponentFieldExtend:
                case CoreSchoolMechanic.ComponentFieldMastery:
                    return new NavalCardVfxPlan(NavalVfxRole.Field, school, hasSchool, affectsFields: true);
                case CoreSchoolMechanic.ComponentFieldShelter:
                    return new NavalCardVfxPlan(NavalVfxRole.Guard, school, hasSchool, affectsFields: true);
                case CoreSchoolMechanic.ComponentFieldPulse:
                    return new NavalCardVfxPlan(card.SchoolId == "fire" || card.SchoolId == "curse" ? NavalVfxRole.Affliction :
                        card.SchoolId == "blood" ? NavalVfxRole.Heal : NavalVfxRole.Empower,
                        school, hasSchool, affectsFields: true);
                case CoreSchoolMechanic.FireIgnite:
                case CoreSchoolMechanic.FireIgniteOrExtend:
                case CoreSchoolMechanic.FireExtend:
                case CoreSchoolMechanic.CurseMark:
                    return new NavalCardVfxPlan(NavalVfxRole.Affliction, school, hasSchool);
                case CoreSchoolMechanic.CurseDetonate:
                case CoreSchoolMechanic.CurseExploit:
                case CoreSchoolMechanic.TideRiposte:
                    return new NavalCardVfxPlan(NavalVfxRole.Strike, school, hasSchool);
                case CoreSchoolMechanic.TideCleanse:
                    return new NavalCardVfxPlan(NavalVfxRole.Dispel, school, hasSchool);
                case CoreSchoolMechanic.FireHeal:
                case CoreSchoolMechanic.ArcaneMend:
                case CoreSchoolMechanic.CurseDrain:
                case CoreSchoolMechanic.TideRenew:
                case CoreSchoolMechanic.BloodMend:
                    return new NavalCardVfxPlan(NavalVfxRole.Heal, school, hasSchool);
                case CoreSchoolMechanic.FireGuard:
                case CoreSchoolMechanic.FirePrepareGuard:
                case CoreSchoolMechanic.FireNaturalGuard:
                case CoreSchoolMechanic.FireBoardingCover:
                case CoreSchoolMechanic.ArcaneWard:
                case CoreSchoolMechanic.CurseWard:
                case CoreSchoolMechanic.TideWard:
                case CoreSchoolMechanic.ThunderWard:
                case CoreSchoolMechanic.BloodWard:
                    return new NavalCardVfxPlan(NavalVfxRole.Guard, school, hasSchool);
            }

            // Movement-labelled cards now grant action points. Only the committed tactical
            // movement path may animate hull displacement; a cast plan cannot infer movement.
            return new NavalCardVfxPlan(card.Repair > 0 ? NavalVfxRole.Heal :
                card.Block > 0 ? NavalVfxRole.Guard : NavalVfxRole.Empower, school, hasSchool);
        }
    }
}
