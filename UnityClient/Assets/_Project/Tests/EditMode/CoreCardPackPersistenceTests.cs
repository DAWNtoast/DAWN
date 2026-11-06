using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;

namespace ShatteredSail.Core.Tests
{
    public sealed class CoreCardPackPersistenceTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject host;
        private CoreGameplayDemoController controller;
        private readonly Dictionary<string, SavedPreference> preferences = new Dictionary<string, SavedPreference>();

        [SetUp]
        public void PreserveProfileAndCreateInactiveController()
        {
            Assert.That(Application.isPlaying, Is.False);
            foreach (string key in TouchedKeys().Distinct())
            {
                preferences.Add(key, SavedPreference.Read(key));
                PlayerPrefs.DeleteKey(key);
            }
            host = new GameObject("Card Pack Persistence Test");
            host.SetActive(false);
            controller = host.AddComponent<CoreGameplayDemoController>();
            SetSession(new CoreVoyageSession());
        }

        [TearDown]
        public void RestoreEveryTouchedPreference()
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            foreach (var pair in preferences) pair.Value.Restore(pair.Key);
            preferences.Clear();
            PlayerPrefs.Save();
        }

        [Test]
        public void SaveAndReloadPreservesInventoryCopiesAndTheNextThreeDraws()
        {
            CoreVoyageSession source = controller.Session;
            int card = source.GetAvailableBattleCards(NavalBattleStage.Naval).First();
            Assert.That(source.RestoreCardPackProgress(Progress(card, 27)), Is.True);
            Invoke("SaveCardPacks");
            string json = PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey);
            Assert.That(json, Is.EqualTo(JsonUtility.ToJson(source.CaptureCardPackProgress())));

            SetSession(new CoreVoyageSession());
            Assert.That((bool)Invoke("RestoreCardPacks"), Is.True);
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureCardPackProgress()), Is.EqualTo(json));
            Assert.That(controller.Session.GetBaseBattleCardOwnedCopies(card), Is.EqualTo(29));
            Assert.That(controller.Session.GetCardPackCount("fire"), Is.EqualTo(2));
            Assert.That(controller.Session.GetCardPackCount("neutral"), Is.EqualTo(3));
            Assert.That(controller.Session.CaptureCardPackProgress().packsWithoutUsableCopy, Is.EqualTo(1));
            Assert.That(source.TryOpenCardPack("fire", out int[] expected, out string reason), Is.True, reason);
            Assert.That(controller.Session.TryOpenCardPack("fire", out int[] actual, out reason), Is.True, reason);
            CollectionAssert.AreEqual(expected, actual);
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureCardPackProgress()),
                Is.EqualTo(JsonUtility.ToJson(source.CaptureCardPackProgress())));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey), Is.EqualTo(json),
                "Reading or changing the in-memory collection does not silently write the profile.");
        }

        [Test]
        public void MissingLegacyRecordRetainsTwoCopiesAndExistingDecks()
        {
            int[] available = controller.Session.GetAvailableBattleCards(NavalBattleStage.Naval).ToArray();
            int[] deck = controller.Session.GetBattleDeck(NavalBattleStage.Naval).ToArray();
            Assert.That((bool)Invoke("RestoreCardPacks"), Is.True);
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.CardPackProgressKey), Is.False);
            CollectionAssert.AreEqual(available, controller.Session.GetAvailableBattleCards(NavalBattleStage.Naval));
            CollectionAssert.AreEqual(deck, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            Assert.That(available.All(index => controller.Session.GetBaseBattleCardOwnedCopies(index) == 2), Is.True);
            Assert.That(controller.Session.CaptureCardPackProgress().packs, Is.Empty);
            Assert.That(controller.Session.CaptureCardPackProgress().cards, Is.Empty);
            Assert.That(Read<bool>("profileSaveBlocked"), Is.False);
        }

        [Test]
        public void ExplicitEmptyArraysAreAValidEmptyInventory()
        {
            const string json = "{\"version\":1,\"packs\":[],\"cards\":[],\"randomState\":987654321}";
            PlayerPrefs.SetString(CoreGameplayDemoController.CardPackProgressKey, json);
            Assert.That((bool)Invoke("RestoreCardPacks"), Is.True);
            Assert.That(controller.Session.CaptureCardPackProgress().randomState, Is.EqualTo(987654321u));
            Assert.That(controller.Session.CaptureCardPackProgress().packs, Is.Empty);
            Assert.That(controller.Session.CaptureCardPackProgress().cards, Is.Empty);
            Assert.That(Read<bool>("profileSaveBlocked"), Is.False);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{broken-json")]
        [TestCase("null")]
        [TestCase("{}")]
        [TestCase("{\"version\":3,\"packs\":[],\"cards\":[]}")]
        [TestCase("{\"version\":1,\"cards\":[]}")]
        [TestCase("{\"version\":1,\"packs\":[]}")]
        [TestCase("{\"version\":1,\"packs\":null,\"cards\":[]}")]
        [TestCase("{\"version\":1,\"packs\":[],\"cards\":null}")]
        [TestCase("{\"version\":1,\"packs\":[{\"id\":\"fire\",\"count\":-1}],\"cards\":[]}")]
        [TestCase("{\"version\":1,\"packs\":[{\"id\":\"unknown\",\"count\":1}],\"cards\":[]}")]
        [TestCase("{\"version\":2,\"packs\":[],\"cards\":[],\"packsWithoutUsableCopy\":-1}")]
        [TestCase("{\"version\":2,\"packs\":[],\"cards\":[],\"packsWithoutUsableCopy\":2}")]
        public void InvalidRecordRemainsIntactAndBlocksReplacementSaves(string json)
        {
            Assert.That(controller.Session.GrantCardPacks("tide", 4), Is.True);
            string before = JsonUtility.ToJson(controller.Session.CaptureCardPackProgress());
            PlayerPrefs.SetString(CoreGameplayDemoController.CardPackProgressKey, json);
            Assert.That((bool)Invoke("RestoreCardPacks"), Is.False);
            Assert.That(Read<bool>("profileSaveBlocked"), Is.True);
            Assert.That(controller.enabled, Is.False);
            Assert.That(JsonUtility.ToJson(controller.Session.CaptureCardPackProgress()), Is.EqualTo(before));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey), Is.EqualTo(json));

            // A failed read must not be overwritten by a subsequent writer,
            // even if valid rewards have meanwhile reached the in-memory session.
            Assert.That(controller.Session.GrantCardPacks("neutral"), Is.True);
            try { Invoke("SaveCardPacks"); }
            catch (TargetInvocationException error)
            {
                Assert.That(error.InnerException, Is.TypeOf<ProfileSaveException>());
            }
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey), Is.EqualTo(json));
        }

        [Test]
        public void PortLoadRestoresEarnedLockedSchoolCardBeforeValidatingSavedDeck()
        {
            CoreVoyageSession source = controller.Session;
            int card = source.GetCardPackPool("fire").First(index =>
                CoreVoyageSession.CardLibrary[index].SchoolId == "fire" &&
                CoreVoyageSession.CardLibrary[index].IsAllowedInStage(NavalBattleStage.Naval));
            Assert.That(source.IsBattleCardUnlocked(CoreVoyageSession.CardLibrary[card]), Is.False);
            Assert.That(source.RestoreCardPackProgress(Progress(card, 1)), Is.True);
            List<int> deck = source.GetBattleDeck(NavalBattleStage.Naval).ToList();
            deck[0] = card;
            Assert.That(source.SetBattleDeck(NavalBattleStage.Naval, deck), Is.True);
            Invoke("SaveCardPacks");
            Invoke("SaveStageBattleDecks");
            string savedDeck = PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Naval));

            SetSession(new CoreVoyageSession());
            Assert.That(controller.Session.IsBattleCardUnlocked(CoreVoyageSession.CardLibrary[card]), Is.False);
            Assert.That((bool)Invoke("RestorePortProgression"), Is.True);
            Assert.That(controller.Session.GetBaseBattleCardOwnedCopies(card), Is.EqualTo(1));
            Assert.That(controller.Session.IsBattleCardUnlocked(CoreVoyageSession.CardLibrary[card]), Is.True);
            CollectionAssert.AreEqual(deck, controller.Session.GetBattleDeck(NavalBattleStage.Naval));
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Naval)),
                Is.EqualTo(savedDeck), "The ordinary deck migration must not discard a card earned before its school unlock.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyOwnershipIsImportedOnceFromSavedLevelAndFutureLevelsGrantNothing(bool hasVersionOne)
        {
            int fire = controller.Session.GetCardPackPool("fire").First(index => CoreVoyageSession.CardLibrary[index].SchoolId == "fire");
            int blood = controller.Session.GetCardPackPool("blood").First(index => CoreVoyageSession.CardLibrary[index].SchoolId == "blood");
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, JsonUtility.ToJson(new CoreTreasureProgress
            { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(4) }));
            if (hasVersionOne)
            {
                CoreCardPackProgress old = Progress(fire, 5);
                old.version = 1;
                PlayerPrefs.SetString(CoreGameplayDemoController.CardPackProgressKey, JsonUtility.ToJson(old));
            }
            // Current runtime experience is deliberately newer than the saved profile.
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
            { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) });
            Assert.That((bool)Invoke("RestoreCardPacks"), Is.True);
            Assert.That(controller.Session.GetBaseBattleCardOwnedCopies(fire), Is.EqualTo(hasVersionOne ? 7 : 2));
            Assert.That(controller.Session.GetBaseBattleCardOwnedCopies(blood), Is.Zero);
            Assert.That((bool)Invoke("CardPackProgressChanged"), Is.True, "The import must reach the existing protected save.");
            Assert.That((bool)Invoke("RestoreCardPacks"), Is.True);
            Assert.That(controller.Session.GetBaseBattleCardOwnedCopies(fire), Is.EqualTo(hasVersionOne ? 7 : 2));
            Invoke("SaveCardPacks");
            CoreCardPackProgress migrated = JsonUtility.FromJson<CoreCardPackProgress>(PlayerPrefs.GetString(CoreGameplayDemoController.CardPackProgressKey));
            Assert.That(migrated.version, Is.EqualTo(2));
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, JsonUtility.ToJson(new CoreTreasureProgress
            { pirateExperience = CoreVoyageSession.PirateExperienceForLevel(10) }));
            SetSession(new CoreVoyageSession());
            Assert.That((bool)Invoke("RestoreCardPacks"), Is.True);
            Assert.That(controller.Session.GetBaseBattleCardOwnedCopies(fire), Is.EqualTo(hasVersionOne ? 7 : 2));
            Assert.That(controller.Session.GetBaseBattleCardOwnedCopies(blood), Is.Zero);
            Assert.That((bool)Invoke("CardPackProgressChanged"), Is.False);
            if (hasVersionOne) Assert.That(controller.Session.GetCardPackCount("fire"), Is.EqualTo(2));
        }

        [TestCase("")]
        [TestCase("{broken")]
        [TestCase("{}")]
        [TestCase("{\"pirateExperience\":-1}")]
        public void UnreadableLegacyLevelCannotSilentlyDiscardOldSchoolCards(string json)
        {
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, json);
            Assert.That((bool)Invoke("RestoreCardPacks"), Is.False);
            Assert.That(Read<bool>("profileSaveBlocked"), Is.True);
            Assert.That(PlayerPrefs.GetString(CoreGameplayDemoController.TreasureProgressKey), Is.EqualTo(json));
            Assert.That(PlayerPrefs.HasKey(CoreGameplayDemoController.CardPackProgressKey), Is.False);
        }

        [Test]
        public void VersionTwoOwnershipDoesNotDependOnTheOldLevelRecord()
        {
            int card = controller.Session.GetCardPackPool("tide").First(index => CoreVoyageSession.CardLibrary[index].SchoolId == "tide");
            PlayerPrefs.SetString(CoreGameplayDemoController.CardPackProgressKey, JsonUtility.ToJson(Progress(card, 1)));
            PlayerPrefs.SetString(CoreGameplayDemoController.TreasureProgressKey, "{broken");
            Assert.That((bool)Invoke("RestoreCardPacks"), Is.True);
            Assert.That(controller.Session.GetBaseBattleCardOwnedCopies(card), Is.EqualTo(1));
            Assert.That(Read<bool>("profileSaveBlocked"), Is.False);
        }

        [TestCase(0, "0")]
        [TestCase(1, "1")]
        [TestCase(9, "9")]
        [TestCase(10, "9+")]
        [TestCase(29, "9+")]
        [TestCase(int.MaxValue, "9+")]
        public void OwnedCountAbbreviatesOnlyTheDisplay(int count, string expected)
        {
            MethodInfo formatter = typeof(CoreGameplayDemoController).GetMethod("WorkshopOwnedCardCount",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(formatter, Is.Not.Null);
            Assert.That((string)formatter.Invoke(null, new object[] { count }), Is.EqualTo(expected));
        }

        private static CoreCardPackProgress Progress(int cardIndex, int copies) => new CoreCardPackProgress
        {
            packs = new[]
            {
                new CoreCardPackCountSave { id = "fire", count = 2 },
                new CoreCardPackCountSave { id = "neutral", count = 3 },
            },
            cards = new[] { new CoreCardCopySave { id = CoreVoyageSession.CardLibrary[cardIndex].Id, count = copies } },
            randomState = 0xf1e2d3c4u,
            packsWithoutUsableCopy = 1,
        };

        private static IEnumerable<string> TouchedKeys()
        {
            yield return CoreGameplayDemoController.TreasureProgressKey;
            yield return CoreGameplayDemoController.CardPackProgressKey;
            yield return CoreGameplayDemoController.BattleDeckKey;
            yield return CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Preparation);
            yield return CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Naval);
            yield return CoreGameplayDemoController.StageBattleDeckKey(NavalBattleStage.Boarding);
            yield return CoreGameplayDemoController.SelectedShipKey;
            yield return CoreGameplayDemoController.ShipPortProgressKey;
            yield return CoreGameplayDemoController.CargoKey("Water");
            yield return CoreGameplayDemoController.LegacyWitchRecruitedKey;
            yield return CoreGameplayDemoController.BattleCardUpgradeKey;
            yield return CoreGameplayDemoController.SplitSupportCardVersionKey;
            yield return CoreGameplayDemoController.SpecialCardExperienceKey;
            yield return CoreGameplayDemoController.AcquiredSpecialCardsKey;
            for (int ship = 1; ship < CoreVoyageSession.Ships.Count; ship++)
                yield return CoreGameplayDemoController.ShipOwnedKey(ship);
            for (int profession = 0; profession < CrewNarrativeCatalog.ProfessionCount; profession++)
            {
                yield return CoreGameplayDemoController.CrewRecruitmentKey(profession);
                yield return CoreGameplayDemoController.ActiveCrewVariantKey(profession);
            }
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                yield return CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id);
        }

        private object Invoke(string name) => typeof(CoreGameplayDemoController)
            .GetMethod(name, PrivateInstance).Invoke(controller, null);
        private T Read<T>(string name) => (T)typeof(CoreGameplayDemoController)
            .GetField(name, PrivateInstance).GetValue(controller);
        private void SetSession(CoreVoyageSession session) => typeof(CoreGameplayDemoController)
            .GetField("session", PrivateInstance).SetValue(controller, session);

        private sealed class SavedPreference
        {
            private bool exists;
            private int kind;
            private string text;
            private int number;
            private float real;

            public static SavedPreference Read(string key)
            {
                var saved = new SavedPreference { exists = PlayerPrefs.HasKey(key) };
                if (!saved.exists) return saved;
                // Preserve the native type even if a pre-existing preference is malformed.
                if (PlayerPrefs.GetString(key, "pack-test-a") == PlayerPrefs.GetString(key, "pack-test-b"))
                {
                    saved.kind = 0;
                    saved.text = PlayerPrefs.GetString(key);
                }
                else if (PlayerPrefs.GetInt(key, 0) == PlayerPrefs.GetInt(key, 1))
                {
                    saved.kind = 1;
                    saved.number = PlayerPrefs.GetInt(key);
                }
                else
                {
                    saved.kind = 2;
                    saved.real = PlayerPrefs.GetFloat(key);
                }
                return saved;
            }

            public void Restore(string key)
            {
                if (!exists) PlayerPrefs.DeleteKey(key);
                else if (kind == 0) PlayerPrefs.SetString(key, text);
                else if (kind == 1) PlayerPrefs.SetInt(key, number);
                else PlayerPrefs.SetFloat(key, real);
            }
        }
    }
}

