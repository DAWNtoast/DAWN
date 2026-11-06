using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreComponentFieldCardsTests
    {
        private static readonly string[] Schools = { "fire", "arcane", "curse", "tide", "thunder", "blood" };

        [Test]
        public void ExactlySixtyEpicAndLegendarySourcesUseTheirOwnSchoolFieldAtEveryRank()
        {
            CoreBattleCard[] cards = CoreSpecialCardCatalog.All.Select(definition => definition.Card)
                .Where(CoreComponentFieldCards.IsFieldCard).ToArray();
            Assert.That(cards, Has.Length.EqualTo(60));
            var session = new CoreVoyageSession();
            foreach (string school in Schools)
            {
                Assert.That(cards.Count(card => card.SchoolId == school), Is.EqualTo(10), school);
                foreach (CoreBattleCard basis in cards.Where(card => card.SchoolId == school))
                    for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(basis.Id); rank++)
                    {
                        CoreBattleCard card = session.GetBattleCardAtLevel(Index(basis.Id), rank);
                        Assert.That(CoreComponentFieldCards.IsFieldCard(card), Is.True, card.Id);
                        Assert.That(card.AllowedStages, Is.EqualTo(NavalBattleStages.Naval), card.Id);
                        Assert.That(card.Description, Does.Contain(CoreComponentFieldCards.FieldName(CoreComponentFieldCards.Kind(card)))
                            .And.Not.Contain("每回合首次成功").And.Not.Contain("{"), card.Id + " +" + rank);
                        Assert.That(CoreSpecialCardRules.IsDoctrine(card), Is.False, card.Id);
                        Assert.That(CoreSpecialCardRules.HealthCost(card), Is.Zero, card.Id);
                        Assert.That(card.SchoolEffect.Threshold + card.SchoolEffect.WoodCost, Is.Zero, card.Id);
                    }
            }
        }

        [TestCase("fire")]
        [TestCase("arcane")]
        [TestCase("curse")]
        [TestCase("tide")]
        [TestCase("thunder")]
        [TestCase("blood")]
        public void EverySourceAndAuthoredUpgradeExecutesItsMatchingFieldBenefit(string school)
        {
            foreach (CoreBattleCard basis in Cards(school))
                for (int rank = 0; rank <= new CoreVoyageSession().GetCardMaximumUpgradeLevel(basis.Id); rank++)
                {
                    CoreVoyageSession session = Start(basis.Id, rank);
                    CoreBattleCard card = session.GetBattleCard(Index(basis.Id));
                    Assert.That(card.UpgradeLevel, Is.EqualTo(rank), card.Id);
                    CoreNavalFieldKind kind = CoreComponentFieldCards.Kind(card);
                    CoreSchoolMechanic mechanic = card.SchoolEffect.Mechanic;
                    bool enemyPulse = school == "fire" || school == "curse" || school == "thunder";
                    bool underEnemy = mechanic == CoreSchoolMechanic.ComponentFieldStrike ||
                        mechanic == CoreSchoolMechanic.ComponentFieldPulse && enemyPulse;
                    bool underPlayer = mechanic == CoreSchoolMechanic.ComponentFieldShelter ||
                        mechanic == CoreSchoolMechanic.ComponentFieldPulse && !enemyPulse;
                    Place(session, kind, true, underEnemy ? 2 : underPlayer ? 0 : 1, underEnemy || underPlayer ? 0 : 1);
                    int expiry = session.Battle.NavalFields.Cells[0].ExpiresAtOwnerTurn;
                    int originalCells = session.Battle.NavalFields.Cells.Count;
                    int enemy = session.Battle.EnemyHull, hull = session.Hull;
                    int expectedDamage = card.Damage + (mechanic == CoreSchoolMechanic.ComponentFieldStrike ? card.SchoolEffect.Power : 0);
                    // Component damage is still the authored base plus its own-field
                    // bonus. The target's shared terrain then changes the full hit.
                    int expectedHitDamage = underEnemy && school == "arcane"
                        ? (int)Math.Ceiling(expectedDamage * 1.25m)
                        : underEnemy && school == "curse" ? (int)Math.Ceiling(expectedDamage * .75m) : expectedDamage;
                    int expectedGuard = card.Block + (mechanic == CoreSchoolMechanic.ComponentFieldShelter ? card.SchoolEffect.Power : 0);
                    Assert.That(session.GetBattleCardEffects(card, NavalBattleStage.Naval).Damage, Is.EqualTo(expectedDamage), card.Id);
                    Assert.That(session.GetBattleCardDamage(card, NavalBattleStage.Naval), Is.EqualTo(expectedHitDamage), card.Id);
                    Assert.That(session.GetBattleCardEffects(card, NavalBattleStage.Naval).Block, Is.EqualTo(expectedGuard), card.Id);
                    if (school == "blood")
                    {
                        int expectedRepair = mechanic == CoreSchoolMechanic.ComponentFieldShelter ? card.SchoolEffect.Secondary :
                            mechanic == CoreSchoolMechanic.ComponentFieldPulse ? card.SchoolEffect.Power : 0;
                        CoreBattleCardEffects quote = session.GetBattleCardEffects(card, NavalBattleStage.Naval);
                        Assert.That(quote.FullRepair, Is.EqualTo(expectedRepair), card.Id + " field repair quote");
                        Assert.That(quote.WoodCost, Is.Zero, card.Id + " field repair never spends planks");
                    }
                    Play(session, card.Id);
                    string context = card.Id + " +" + rank;
                    Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemy - expectedHitDamage), context);
                    Assert.That(session.Battle.PlayerBlock, Is.EqualTo(expectedGuard), context);
                    Assert.That(session.Cargo.Wood, Is.Zero, context);
                    Assert.That(session.Battle.SpecialCards.IsActive(school), Is.False, context);
                    if (mechanic == CoreSchoolMechanic.ComponentFieldExpand || mechanic == CoreSchoolMechanic.ComponentFieldMastery)
                        Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(originalCells + (school == "fire" ? 12 : school == "tide" ? 16 : school == "thunder" ? 28 : 8)), context);
                    if (mechanic == CoreSchoolMechanic.ComponentFieldExtend)
                        Assert.That(session.Battle.NavalFields.Cells.All(cell => cell.ExpiresAtOwnerTurn == expiry +
                            (mechanic == CoreSchoolMechanic.ComponentFieldMastery ? card.SchoolEffect.Secondary : card.SchoolEffect.Power)), Is.True, context);
                    if (mechanic == CoreSchoolMechanic.ComponentFieldPulse)
                    {
                        int value = school == "fire" ? session.Battle.EnemyBurnTurns : school == "arcane" ? session.Battle.Schools.Mana :
                            school == "curse" ? session.Battle.Schools.CurseMarks : school == "tide" ? session.Battle.Schools.TidePower :
                            school == "thunder" ? session.Battle.Schools.ThunderChain : session.Hull - hull;
                        Assert.That(value, Is.EqualTo(card.SchoolEffect.Power), context);
                    }
                    if (mechanic == CoreSchoolMechanic.ComponentFieldShelter && (school == "arcane" || school == "blood"))
                        Assert.That(school == "arcane" ? session.Battle.Schools.Mana : session.Hull - hull,
                            Is.EqualTo(card.SchoolEffect.Secondary), context);
                }
        }

        [TestCase("fire")]
        [TestCase("arcane")]
        [TestCase("curse")]
        [TestCase("tide")]
        [TestCase("thunder")]
        [TestCase("blood")]
        public void MissingEnemyOwnedAndDifferentSchoolFieldsDoNotGrantComponentBenefitsButKeepPublicTerrainEffects(string school)
        {
            foreach (CoreBattleCard card in Cards(school))
                foreach (int fieldMode in new[] { 0, 1, 2 })
                {
                    CoreVoyageSession session = Start(card.Id);
                    CoreNavalFieldKind kind = CoreComponentFieldCards.Kind(card);
                    if (fieldMode == 2) kind = kind == CoreNavalFieldKind.Fire ? CoreNavalFieldKind.Blood : CoreNavalFieldKind.Fire;
                    if (fieldMode != 0)
                    {
                        Place(session, kind, fieldMode == 2, 0, 0);
                        Place(session, kind, fieldMode == 2, 2, 0);
                    }
                    CoreNavalFieldCell[] before = session.Battle.NavalFields.Cells.Select(cell => cell.Copy()).ToArray();
                    int hull = session.Hull, enemy = session.Battle.EnemyHull;
                    // Both ships occupy the selected kind. Ownership does not disable
                    // fire attack power, arcane vulnerability or curse protection.
                    decimal terrainMultiplier = fieldMode == 0 ? 1m :
                        kind == CoreNavalFieldKind.Fire || kind == CoreNavalFieldKind.Arcane ? 1.25m :
                        kind == CoreNavalFieldKind.Curse ? .75m : 1m;
                    int expectedHitDamage = (int)Math.Ceiling(card.Damage * terrainMultiplier);
                    Assert.That(session.GetBattleCardEffects(card, NavalBattleStage.Naval).Damage,
                        Is.EqualTo(card.Damage), card.Id + " must not gain its component-only damage bonus");
                    Assert.That(session.GetBattleCardDamage(card, NavalBattleStage.Naval),
                        Is.EqualTo(expectedHitDamage), card.Id + " public field damage");
                    Play(session, card.Id);
                    Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(before.Length), card.Id);
                    foreach (CoreNavalFieldCell cell in before)
                        Assert.That(session.GetNavalFieldAt(cell.X, cell.Y).ExpiresAtOwnerTurn, Is.EqualTo(cell.ExpiresAtOwnerTurn), card.Id);
                    Assert.That(session.Battle.EnemyHull, Is.EqualTo(enemy - expectedHitDamage), card.Id);
                    Assert.That(session.Battle.PlayerBlock, Is.EqualTo(card.Block), card.Id);
                    Assert.That(session.Hull, Is.EqualTo(hull), card.Id);
                    Assert.That(new[] { session.Battle.EnemyBurnTurns, session.Battle.Schools.Mana, session.Battle.Schools.CurseMarks,
                        session.Battle.Schools.TidePower, session.Battle.Schools.ThunderChain },
                        Is.EqualTo(new[] { 0, 0, 0, 0, school == "thunder" && card.Damage > 0 ? 1 : 0 }), card.Id);
                }
        }

        [Test]
        public void WaterExpansionUsesEntireOriginalBoundaryAndPreservesDirection()
        {
            CoreBattleCard card = Card("tide", "e01");
            CoreVoyageSession session = Start(card.Id);
            Place(session, CoreNavalFieldKind.Tide, true, 1, 1);
            CoreNavalFieldCell[] original = session.Battle.NavalFields.Cells.Select(cell => cell.Copy()).ToArray();
            int instance = original[0].InstanceId, expiry = original[0].ExpiresAtOwnerTurn;
            Play(session, card.Id);
            CoreNavalFieldCell[] added = session.Battle.NavalFields.Cells.Where(cell => !original.Any(old => old.X == cell.X && old.Y == cell.Y)).ToArray();
            Assert.That(added, Has.Length.EqualTo(16));
            Assert.That(added.Any(cell => cell.X == 2 && cell.Y == 0), Is.True, "A field can grow under a ship without triggering it immediately.");
            foreach (CoreNavalFieldCell cell in added)
            {
                Assert.That(original.Any(old => Math.Abs(old.X - cell.X) + Math.Abs(old.Y - cell.Y) == 1), Is.True);
                Assert.That(cell.InstanceId, Is.EqualTo(instance));
                Assert.That(cell.ExpiresAtOwnerTurn, Is.EqualTo(expiry));
                Assert.That(new[] { cell.DirectionX, cell.DirectionY }, Is.EqualTo(new[] { 1, 0 }));
            }
            Assert.That(session.Battle.NavalFields.TriggerLedger, Is.Empty, "Growing a field is not an arrival event.");
        }

        [Test]
        public void FireExpansionNeverOverwritesEnemyFieldsAndIncludesOutOfRangeSources()
        {
            CoreBattleCard card = Card("fire", "l05");
            CoreVoyageSession session = Start(card.Id);
            Place(session, CoreNavalFieldKind.Fire, true, 1, 1);
            Place(session, CoreNavalFieldKind.Blood, false, 0, 1);
            CoreNavalFieldCell[] occupied = session.Battle.NavalFields.Cells.Where(cell => !cell.PlayerOwned).Select(cell => cell.Copy()).ToArray();
            Pose(session, true, 500, 0);
            Place(session, CoreNavalFieldKind.Fire, true, 6, 1);
            int remoteInstance = session.GetNavalFieldAt(6, 1).InstanceId;
            int remoteExpiry = session.GetNavalFieldAt(6, 1).ExpiresAtOwnerTurn;
            Pose(session, true, 0, 0);
            Play(session, card.Id);
            foreach (CoreNavalFieldCell cell in occupied)
                Assert.That(session.GetNavalFieldAt(cell.X, cell.Y).InstanceId, Is.EqualTo(cell.InstanceId));
            Assert.That(session.Battle.NavalFields.Cells.Count(cell => cell.InstanceId == remoteInstance), Is.EqualTo(21));
            Assert.That(session.Battle.NavalFields.Cells.Where(cell => cell.InstanceId == remoteInstance).All(cell => cell.ExpiresAtOwnerTurn == remoteExpiry), Is.True);
        }

        [Test]
        public void ExtensionUpdatesTheCompleteInstanceAndSurvivesCheckpointRestore()
        {
            CoreBattleCard card = Card("fire", "l02");
            CoreVoyageSession session = Start(card.Id);
            Pose(session, true, 200, 0);
            Place(session, CoreNavalFieldKind.Fire, true, 4, 0);
            int instance = session.GetNavalFieldAt(4, 0).InstanceId;
            int newExpiry = session.GetNavalFieldAt(4, 0).ExpiresAtOwnerTurn + card.SchoolEffect.Power;
            Pose(session, true, 0, 0);
            Play(session, card.Id);
            Assert.That(session.Battle.NavalFields.Cells.Where(cell => cell.InstanceId == instance)
                .All(cell => cell.ExpiresAtOwnerTurn == newExpiry), Is.True, "Cells at five grids retain their instance's new expiry.");
            CoreNavalFieldState snapshot = session.CaptureNavalFields();
            session.ClearNavalFields();
            Assert.That(session.RestoreNavalFields(snapshot), Is.True);
            int remaining = session.GetNavalFieldRemainingTurns(session.GetNavalFieldAt(4, 0));
            for (int turn = 1; turn < remaining; turn++)
            {
                session.NavalFieldsBeginTurn(true);
                session.NavalFieldsEndTurn(true);
                session.NavalFieldsBeginTurn(false);
                session.NavalFieldsEndTurn(false);
                Assert.That(session.Battle.NavalFields.Cells.Count, Is.EqualTo(9));
            }
            session.NavalFieldsBeginTurn(false);
            session.NavalFieldsEndTurn(false);
            Assert.That(session.Battle.NavalFields.Cells, Is.Empty);
        }

        [TestCase("fire")]
        [TestCase("curse")]
        [TestCase("thunder")]
        public void ShelterImmunityMatchesMovementPreviewRestoresAndExpiresNextPlayerTurn(string school)
        {
            CoreBattleCard card = Card(school, "e04");
            CoreVoyageSession session = Start(card.Id);
            CoreNavalFieldKind kind = CoreComponentFieldCards.Kind(card);
            Place(session, kind, false, 1, 0);
            Pose(session, false, 500, 0);
            Play(session, card.Id);
            session.Battle.PlayerBlock = 0;
            if (school == "thunder") SetSchool(session, "ThunderChain", 45);
            int hull = session.Hull;
            CoreNavalFieldState snapshot = session.CaptureNavalFields();
            session.ClearNavalFields();
            Assert.That(session.RestoreNavalFields(snapshot), Is.True);
            Set(session.Battle, "NavalActionPoints", 3);
            CollectionAssert.Contains(session.GetNavalReachableCells(), new VoyagePoint(200, 0),
                "A shelter-protected route must remain selectable beyond its field.");
            CollectionAssert.Contains(session.GetNavalReachableAttackOrigins(), new VoyagePoint(200, 0));
            Assert.That(session.Battle.NavalFields.TriggerLedger, Is.Empty, "Reach queries cannot trigger terrain.");
            Assert.That(session.TryAppendNavalMovementSegment(new VoyagePoint(200, 0), out string reason), Is.True, reason);
            CoreNavalMovementFieldPreview preview = session.NavalMovementFieldPreview;
            Assert.That(preview.Damage, Is.Zero);
            Assert.That(preview.StopsAtThunder, Is.False);
            Assert.That(preview.FinalPositionMeters.X, Is.EqualTo(200f));
            Assert.That(session.CommitNavalMovement(out reason), Is.True, reason);
            Assert.That(session.Battle.PlayerPositionMeters.X, Is.EqualTo(preview.FinalPositionMeters.X));
            Assert.That(session.Hull, Is.EqualTo(hull));
            if (school == "thunder") Assert.That(session.Battle.Schools.ThunderChain, Is.EqualTo(55), "Shelter only prevents overload damage; landing chain still accumulates.");
            Assert.That(session.NavalFieldCurseBonus(true, 3), Is.Zero);
            if (school == "fire")
            {
                session.NavalFieldsEndTurn(true);
                Assert.That(session.Battle.PlayerBurnStacks, Is.Zero);
            }
            if (school == "curse")
            {
                session.NavalFieldsEndTurn(true);
                Assert.That(session.Battle.PlayerCurseMarks, Is.Zero);
            }
            session.NavalFieldsBeginTurn(true);
            Assert.That(session.Battle.NavalFields.Player.FieldImmunityMask, Is.Zero);
            Assert.That(session.Battle.NavalFields.Player.FieldImmunityExpires, Is.Zero);
            if (school == "curse")
            {
                session.NavalFieldsEndTurn(true);
                Assert.That(session.Battle.PlayerCurseMarks, Is.EqualTo(4));
            }
            else
            {
                Pose(session, true, 0, 0);
                Set(session.Battle, "NavalAwaitingHeadingSelection", false);
                Set(session.Battle, "NavalActionPoints", 3);
                Assert.That(session.TryAppendNavalMovementSegment(new VoyagePoint(200, 0), out reason), Is.True, reason);
                CoreNavalMovementFieldPreview unprotected = session.NavalMovementFieldPreview;
                if (school == "fire")
                {
                    Assert.That(unprotected.Damage, Is.Zero);
                    Pose(session, true, 100, 0);
                    session.NavalFieldsEndTurn(true);
                    Assert.That(session.Battle.PlayerBurnStacks, Is.GreaterThan(0));
                }
                if (school == "thunder")
                {
                    Assert.That(unprotected.StopsAtThunder, Is.False);
                    Assert.That(unprotected.Damage, Is.EqualTo(1000));
                    Assert.That(unprotected.Sinks, Is.True);
                    CollectionAssert.Contains(session.GetNavalReachableCells(), new VoyagePoint(200, 0),
                        "Thunder does not prohibit selecting a route.");
                    CollectionAssert.DoesNotContain(session.GetNavalReachableAttackOrigins(), new VoyagePoint(200, 0),
                        "The next landing overloads after immunity expires, so there is no living attack origin beyond it.");
                }
            }
        }

        [Test]
        public void FieldShelterOnlyProtectsItsOwnKind()
        {
            CoreBattleCard card = Card("fire", "e04");
            CoreVoyageSession session = Start(card.Id);
            Pose(session, false, 500, 0);
            Place(session, CoreNavalFieldKind.Thunder, false, 1, 0);
            Play(session, card.Id);
            session.Battle.PlayerBlock = 0;
            SetSchool(session, "ThunderChain", 45);
            Set(session.Battle, "NavalActionPoints", 3);
            Assert.That(session.TryAppendNavalMovementSegment(new VoyagePoint(200, 0), out _), Is.True);
            Assert.That(session.NavalMovementFieldPreview.StopsAtThunder, Is.False);
            Assert.That(session.NavalMovementFieldPreview.Sinks, Is.True);
            Assert.That(session.NavalMovementFieldPreview.Damage, Is.EqualTo(1000));
        }

        private static IEnumerable<CoreBattleCard> Cards(string school) => CoreSpecialCardCatalog.All.Select(definition => definition.Card)
            .Where(card => card.SchoolId == school && CoreComponentFieldCards.IsFieldCard(card));
        private static CoreBattleCard Card(string school, string key) => Cards(school).Single(card => card.ArtId == "card_school_" + school + "_" + key);
        private static int Index(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count).Single(index => CoreVoyageSession.CardLibrary[index].Id == id);
        private static CoreVoyageSession Start(string id, int rank = 0)
        {
            var session = new CoreVoyageSession();
            session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That(session.RestoreCardPackProgress(null, session.PirateLevel), Is.True, "Fixture starts with a migrated full collection.");
            session.ConfigureSpecialCardSources(null, CoreSpecialCardCatalog.All.Where(definition => definition.SourceKind == CoreSpecialCardSourceKind.Component)
                .Select(definition => definition.SourceId).ToArray());
            session.ConfigureCardUpgrades(new Dictionary<string, int> { { id, rank } });
            session.SetSeed("COMPONENT-FIELD-CARDS");
            Assert.That(session.StartTrainingBattle(CoreTrainingTargets.All[0]), Is.True);
            Assert.That(session.EndBattleTurn(), Is.True);
            session.Battle.Energy = 100;
            session.Battle.EnemyHull = session.Battle.EnemyMaxHull = 10000;
            session.Battle.PlayerBlock = session.Battle.EnemyBlock = 0;
            session.Battle.PlayerDeckBuff = 0;
            typeof(CoreVoyageSession).GetProperty("Hull").SetValue(session, 20);
            SetSchool(session, "ManaLimit", 100); SetSchool(session, "TideLimit", 100); SetSchool(session, "ThunderLimit", 100);
            SetSchool(session, "Mana", 0);
            session.Cargo.Powder = session.Cargo.Cannonballs = 100;
            session.Cargo.Wood = 0;
            Pose(session, true, 0, 0); Pose(session, false, 200, 0);
            return session;
        }
        private static void Play(CoreVoyageSession session, string id)
        {
            session.Battle.Hand.Clear(); session.Battle.Hand.Add(Index(id));
            Assert.That(session.PlayCard(0), Is.True, id + ": " + session.CardUnavailableReason(0));
        }
        private static void Place(CoreVoyageSession session, CoreNavalFieldKind kind, bool owned, int x, int y)
        {
            VoyagePoint original = owned ? session.Battle.PlayerPositionMeters : session.Battle.EnemyPositionMeters;
            if (kind == CoreNavalFieldKind.Fire) Pose(session, owned, (x + 1) * 100, (y + 1) * 100);
            Assert.That(session.PlaceNavalField(kind, owned, x, y, false, kind == CoreNavalFieldKind.Tide ? 1 : 0, 0), Is.True,
                session.CanPlaceNavalField(kind, owned, x, y, false, kind == CoreNavalFieldKind.Tide ? 1 : 0, 0));
            if (kind == CoreNavalFieldKind.Fire) Pose(session, owned, original.X, original.Y);
        }
        private static void SetSchool(CoreVoyageSession session, string name, int value) =>
            typeof(CoreSchoolBattleState).GetProperty(name).SetValue(session.Battle.Schools, value);
        private static void Set(object target, string name, object value) => target.GetType()
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        private static void Pose(CoreVoyageSession session, bool player, float x, float y) => typeof(CoreBattleState)
            .GetMethod("SetTacticalPose", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session.Battle,
                new object[] { player, new VoyagePoint(x, y), player ? 0f : 180f });
    }
}
