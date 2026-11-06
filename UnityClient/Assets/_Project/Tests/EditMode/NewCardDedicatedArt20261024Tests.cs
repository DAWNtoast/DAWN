using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class NewCardDedicatedArt20261024Tests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] CardIds = {
            "card_school_arcane_n04", "card_school_arcane_n05", "card_school_arcane_n06", "card_school_arcane_n07",
            "card_school_fire_n07", "card_school_fire_n08",
            "card_school_thunder_n07", "card_school_thunder_n08", "card_school_thunder_n09"
        };

        [TestCaseSource(nameof(CardIds))]
        public void NewPaintingOverridesSharedMechanicalArtAtEveryLegalUpgrade(string cardId)
        {
            CardSchoolArtCatalog.ReleaseLoadedTextures();
            var session = new CoreVoyageSession();
            int index = FindCard(cardId);
            CoreBattleCard original = CoreVoyageSession.CardLibrary[index];
            string expectedPath = "CardArt/Western/" + cardId;
            Texture2D expected = Resources.Load<Texture2D>(expectedPath);
            Assert.That(expected, Is.Not.Null, "The new illustration must ship as its own resource.");
            Assert.That(original.ArtId, Is.Not.EqualTo(cardId), "Retain the shared historical rule identity.");
            Assert.That(expected.width, Is.EqualTo(1024));
            Assert.That(expected.height, Is.EqualTo(1024));
            Assert.That(expected.mipmapCount, Is.EqualTo(1));
            for (int rank = 0; rank <= session.GetCardMaximumUpgradeLevel(index); rank++)
            {
                CoreBattleCard card = session.GetBattleCardAtLevel(index, rank);
                Assert.That(card.UpgradeLevel, Is.EqualTo(rank));
                Assert.That(card.ArtId, Is.EqualTo(original.ArtId), cardId + " / rank " + rank);
                Assert.That(CardSchoolArtCatalog.WesternIllustrationPath(card), Is.EqualTo(expectedPath));
                Assert.That(CardSchoolArtCatalog.DedicatedIllustrationPath(card), Is.EqualTo(expectedPath));
                Assert.That(CardSchoolArtCatalog.Illustration(card, null), Is.SameAs(expected));
                Assert.That(CardSchoolArtCatalog.IllustrationUv(card, null), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                Assert.That(CardSchoolArtCatalog.Frame(card), Is.Not.Null, cardId + " retains its resource-matching frame.");
            }
        }

        [Test]
        public void NineNewPaintingsAreDistinctAndUseTheExistingCompleteCardImportBudget()
        {
            var textures = new HashSet<Texture2D>();
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            var guids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string cardId in CardIds)
            {
                string path = "Assets/_Project/Resources/CardArt/Western/" + cardId + ".png";
                Assert.That(textures.Add(Resources.Load<Texture2D>("CardArt/Western/" + cardId)), Is.True, cardId);
                Assert.That(File.Exists(path), Is.True, path);
                using (SHA256 hash = SHA256.Create())
                    Assert.That(hashes.Add(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)))), Is.True, cardId + " must not repeat another painting.");
                Assert.That(guids.Add(AssetDatabase.AssetPathToGUID(path)), Is.True, cardId + " has independent metadata.");
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
                Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D));
                Assert.That(importer.sRGBTexture, Is.True);
                Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
                Assert.That(importer.alphaIsTransparency, Is.False);
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.isReadable, Is.False);
                Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
                Assert.That(importer.compressionQuality, Is.EqualTo(100));
                TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
                Assert.That(standalone.overridden, Is.True);
                Assert.That(standalone.maxTextureSize, Is.EqualTo(1024));
                Assert.That(standalone.format, Is.EqualTo(TextureImporterFormat.BC7));
                Assert.That(standalone.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
                Assert.That(standalone.compressionQuality, Is.EqualTo(100));
            }
            Assert.That(textures, Has.Count.EqualTo(9));
        }

        [TestCase("Collection", 0)] [TestCase("Collection", 1)] [TestCase("Collection", 2)] [TestCase("Collection", 3)]
        [TestCase("Upgrade", 0)] [TestCase("Upgrade", 1)] [TestCase("Upgrade", 2)] [TestCase("Upgrade", 3)]
        [TestCase("Battle", 0)] [TestCase("Battle", 1)] [TestCase("Battle", 2)] [TestCase("Battle", 3)]
        public void RealCollectionUpgradeAndNavalFactoriesDisplayTheCompleteNewPainting(string surface, int rank)
        {
            Assert.That(Application.isPlaying, Is.False, "Disposable edit-mode previews must never load or save player progress.");
            var host = new GameObject("New Card Art Preview Test");
            try
            {
                var controller = host.AddComponent<CoreGameplayDemoController>();
                controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.Battle, CoreGameplayPreviewPort.GrayGull, 0);
                var session = new CoreVoyageSession();
                session.ConfigureCardUpgrades(CardIds.ToDictionary(id => id, id => rank));
                session.SetSeed("NEW-CARD-DEDICATED-ART-20261024");
                session.StartVoyage();
                typeof(CoreVoyageSession).GetMethod("StartBattle", PrivateInstance).Invoke(session,
                    new object[] { new CoreEncounter("new_card_art_test", "插画检查敌舰", "海战", "测试", true, false, 1, 70), false });
                session.Battle.Stage = NavalBattleStage.Naval;
                typeof(CoreGameplayDemoController).GetField("session", PrivateInstance).SetValue(controller, session);
                typeof(CoreGameplayDemoController).GetField("selectedBattleDeckStage", PrivateInstance).SetValue(controller, NavalBattleStage.Naval);
                foreach (string cardId in CardIds)
                {
                    var holder = new GameObject("New Art Card Holder", typeof(RectTransform)).GetComponent<RectTransform>();
                    holder.SetParent(host.transform, false);
                    try
                    {
                        int index = FindCard(cardId);
                        CoreBattleCard card = session.GetBattleCard(index);
                        Texture2D expected = Resources.Load<Texture2D>("CardArt/Western/" + cardId);
                        Assert.That(card.UpgradeLevel, Is.EqualTo(Math.Min(rank, session.GetCardMaximumUpgradeLevel(index))));
                        RawImage art;
                        if (surface == "Battle")
                        {
                            session.Battle.Hand.Clear();
                            session.Battle.Hand.Add(index);
                            Invoke(controller, "CreateRetainedBattleHandCard", holder, session.Battle, 0);
                            art = holder.GetComponentsInChildren<RawImage>(true).Single(image => image.name == "Card Art 0");
                        }
                        else if (surface == "Upgrade")
                        {
                            Invoke(controller, "CreateUpgradeComparison", holder, "NewArt", "强化插画", card, Vector2.zero, Color.gray, 1f);
                            art = holder.GetComponentsInChildren<RawImage>(true).Single(image => image.name == "Upgrade Comparison Art NewArt");
                        }
                        else
                        {
                            Invoke(controller, "CreateCollectionCardFace", holder, card, "New Art Collection", "New Art", "New Name", 1f, null, null);
                            art = holder.GetComponentsInChildren<RawImage>(true).Single(image => image.name == "New Art");
                        }
                        string context = surface + " / " + cardId + " / rank " + rank;
                        Assert.That(art.texture, Is.SameAs(expected), context);
                        Assert.That(art.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), context);
                        Assert.That(art.rectTransform.sizeDelta.x / art.rectTransform.sizeDelta.y, Is.EqualTo(1f).Within(.0001f), context);
                        Assert.That(art.rectTransform.anchoredPosition, Is.EqualTo(CardFaceLayout.IllustrationLayout(art).center), context);
                    }
                    finally { Object.DestroyImmediate(holder.gameObject); }
                }
            }
            finally { Object.DestroyImmediate(host); CardSchoolArtCatalog.ReleaseLoadedTextures(); }
        }

        private static int FindCard(string id) => Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
            .Single(index => CoreVoyageSession.CardLibrary[index].Id == id);

        private static object Invoke(CoreGameplayDemoController controller, string method, params object[] args)
        {
            MethodInfo info = typeof(CoreGameplayDemoController).GetMethod(method, PrivateInstance);
            Assert.That(info, Is.Not.Null, method);
            try { return info.Invoke(controller, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
