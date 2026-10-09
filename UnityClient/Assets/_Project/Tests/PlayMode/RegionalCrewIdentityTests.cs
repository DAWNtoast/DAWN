using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class RegionalCrewIdentityTests
    {
        private readonly Dictionary<string, int> intPrefs = new Dictionary<string, int>();
        private readonly Dictionary<string, string> stringPrefs = new Dictionary<string, string>();
        private readonly List<string> intKeys = new List<string>();
        private readonly List<string> stringKeys = new List<string>();

        [SetUp]
        public void PreserveRoster()
        {
            intKeys.Clear(); stringKeys.Clear(); intPrefs.Clear(); stringPrefs.Clear();
            for (int index = 0; index < 6; index += 1)
            {
                intKeys.Add(CoreGameplayDemoController.CrewRecruitmentKey(index));
                stringKeys.Add(CoreGameplayDemoController.ActiveCrewVariantKey(index));
            }
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
                intKeys.Add(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id));
            stringKeys.Add(CoreGameplayDemoController.DispatchCrewIdentityKey);
            foreach (string key in intKeys)
            {
                if (PlayerPrefs.HasKey(key)) intPrefs[key] = PlayerPrefs.GetInt(key);
                PlayerPrefs.DeleteKey(key);
            }
            foreach (string key in stringKeys)
            {
                if (PlayerPrefs.HasKey(key)) stringPrefs[key] = PlayerPrefs.GetString(key);
                PlayerPrefs.DeleteKey(key);
            }
        }

        [TearDown]
        public void RestoreRoster()
        {
            foreach (string key in intKeys) PlayerPrefs.DeleteKey(key);
            foreach (string key in stringKeys) PlayerPrefs.DeleteKey(key);
            foreach (KeyValuePair<string, int> pair in intPrefs) PlayerPrefs.SetInt(pair.Key, pair.Value);
            foreach (KeyValuePair<string, string> pair in stringPrefs) PlayerPrefs.SetString(pair.Key, pair.Value);
            PlayerPrefs.Save();
        }

        [Test]
        public void RegionalRecruitment_DoesNotRecruitAnotherPortsPerson()
        {
            CrewVariantDefinition regional = CrewNarrativeCatalog.ForPortAndProfession("断锚港", 0);
            RecruitIdentity(regional);
            Assert.That(IsRecruited(regional), Is.True);
            Assert.That(IsRecruited(CrewNarrativeCatalog.LegacyForProfession(0)), Is.False,
                "A profession-wide capability flag must not grant a second person's contract.");
            Assert.That(IsRecruited(CrewNarrativeCatalog.ForPortAndProfession("白贝港", 0)), Is.False);
        }

        [Test]
        public void LegacyRecruitment_MigratesBeforeNewRegionalContract()
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(0), 1);
            Invoke("MigrateLegacyCrewIdentities", Type.EmptyTypes);
            CrewVariantDefinition legacy = CrewNarrativeCatalog.LegacyForProfession(0);
            Assert.That(PlayerPrefs.GetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(legacy.Id)), Is.EqualTo(1));
            CrewVariantDefinition regional = CrewNarrativeCatalog.ForPortAndProfession("暮潮港", 0);
            RecruitIdentity(regional);
            Assert.That(IsRecruited(legacy), Is.True);
            Assert.That(IsRecruited(regional), Is.True);
            Assert.That(IsRecruited(CrewNarrativeCatalog.ForPortAndProfession("盐冠港", 0)), Is.False);
        }

        [Test]
        public void Dispatch_SnapshotsPersonalIdentityWhenRepresentativeChanges()
        {
            CrewVariantDefinition first = CrewNarrativeCatalog.ForPortAndProfession("铜钟港", 0);
            CrewVariantDefinition second = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 0);
            RecruitIdentity(first);
            List<int> professions = new List<int> { 0 };
            string identities = (string)Invoke("BuildDispatchCrewIdentityCsv", new[] { typeof(IReadOnlyList<int>) }, professions);
            Assert.That(identities, Is.EqualTo(first.Id));
            PlayerPrefs.SetString(CoreGameplayDemoController.DispatchCrewIdentityKey, identities);
            RecruitIdentity(second);
            string names = (string)Invoke("PersistedDispatchCrewNames", new[] { typeof(IReadOnlyList<int>) }, professions);
            Assert.That(names, Is.EqualTo(first.DisplayName));
            Assert.That((string)Invoke("BuildDispatchCrewIdentityCsv", new[] { typeof(IReadOnlyList<int>) }, professions), Is.EqualTo(second.Id));
        }

        [Test]
        public void LegacyDispatch_UsesOriginalCrewIdentityWithoutNewSnapshot()
        {
            RecruitIdentity(CrewNarrativeCatalog.ForPortAndProfession("盐冠港", 0));
            string names = (string)Invoke("PersistedDispatchCrewNames", new[] { typeof(IReadOnlyList<int>) }, new List<int> { 0 });
            Assert.That(names, Is.EqualTo(CrewNarrativeCatalog.LegacyForProfession(0).DisplayName));
        }

        [Test]
        public void RegionalLore_DistinguishesAppearanceWithoutInventingUnimplementedSkills()
        {
            foreach (int profession in Enumerable.Range(0, 6))
            {
                CrewVariantDefinition[] crew = CrewNarrativeCatalog.AllCrew.Where(c => c.ProfessionIndex == profession).ToArray();
                Assert.That(crew.Select(c => c.AppearanceTags).Distinct().Count(), Is.EqualTo(6));
                Assert.That(crew.Select(c => c.Biography).Distinct().Count(), Is.EqualTo(6));
                Assert.That(crew.All(c => c.SkillDescription == CrewNarrativeCatalog.GameplaySkillForProfession(profession)), Is.True);
                Assert.That(crew.All(c => c.Stats.Contains("不叠加")), Is.True);
            }
        }

        [Test]
        public void RegionalCrewArt_LoadsSixDistinctAtlasesWithSixCells()
        {
            PortArtCatalog art = new PortArtCatalog();
            HashSet<Texture2D> atlases = new HashSet<Texture2D>();
            foreach (string port in CrewNarrativeCatalog.Ports)
            {
                Texture2D atlas = art.RegionalCrewAtlas(port);
                Assert.That(atlas, Is.Not.Null, port + " requires its own six-person parchment portrait atlas.");
                Assert.That(atlas.name, Does.EndWith("_portraits_v2"), "Rejected seated v1 artwork must never be displayed.");
                Assert.That((atlas.width / 3f) / (atlas.height / 2f), Is.EqualTo(1f).Within(0.02f),
                    "The 3x2 portrait atlas must contain square cells without stretching.");
                Assert.That(atlases.Add(atlas), Is.True, port + " must not reuse another port's atlas.");
                HashSet<Rect> cells = new HashSet<Rect>();
                foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.ForPort(port))
                {
                    Assert.That(art.CrewPortrait(crew), Is.SameAs(atlas));
                    Rect cell = art.CrewPortraitUv(crew);
                    Assert.That(cells.Add(cell), Is.True);
                    Assert.That(cell.width, Is.EqualTo(1f / 3f).Within(0.0001f));
                    Assert.That(cell.height, Is.EqualTo(0.5f));
                    Rect face = art.CrewPortraitUv(crew, true);
                    Assert.That(cell.Contains(face.center), Is.True);
                }
            }
        }

        [Test]
        public void RegionalTavernHotspots_AreIndividuallyCalibratedAndNonOverlapping()
        {
            HashSet<Rect> navigatorBounds = new HashSet<Rect>();
            foreach (string port in CrewNarrativeCatalog.Ports)
            {
                navigatorBounds.Add(TavernCrewLayoutCatalog.NormalizedBounds(port, 0));
                for (int first = 0; first < 6; first += 1)
                {
                    Rect bounds = TavernCrewLayoutCatalog.NormalizedBounds(port, first);
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(0f));
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(1f));
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(1f));
                    for (int second = first + 1; second < 6; second += 1)
                        Assert.That(bounds.Overlaps(TavernCrewLayoutCatalog.NormalizedBounds(port, second)), Is.False,
                            port + " characters " + first + " and " + second + " must not steal each other's click.");
                }
            }
            Assert.That(navigatorBounds.Count, Is.EqualTo(6), "Real redraws require separately calibrated seats.");
        }

        private static void RecruitIdentity(CrewVariantDefinition crew)
        {
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewVariantRecruitmentKey(crew.Id), 1);
            PlayerPrefs.SetInt(CoreGameplayDemoController.CrewRecruitmentKey(crew.ProfessionIndex), 1);
            PlayerPrefs.SetString(CoreGameplayDemoController.ActiveCrewVariantKey(crew.ProfessionIndex), crew.Id);
        }

        private static bool IsRecruited(CrewVariantDefinition crew)
        {
            return (bool)Invoke("IsCrewRecruited", new[] { typeof(CrewVariantDefinition) }, crew);
        }

        private static object Invoke(string name, Type[] parameterTypes, params object[] args)
        {
            MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static, null, parameterTypes, null);
            Assert.That(method, Is.Not.Null, name);
            return method.Invoke(null, args);
        }
    }
}
