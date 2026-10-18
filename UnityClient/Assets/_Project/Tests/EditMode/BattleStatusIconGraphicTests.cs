using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class BattleStatusIconGraphicTests
    {
        private readonly List<Object> temporary = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = temporary.Count - 1; i >= 0; i--)
                if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
            temporary.Clear();
        }

        [Test]
        public void EveryCombatSymbol_HasDistinctBoundedGeometryAtSmallHudSize()
        {
            HashSet<string> fingerprints = new HashSet<string>();
            foreach (CoreBattleStatusKind kind in Enum.GetValues(typeof(CoreBattleStatusKind)))
            {
                BattleStatusIconGraphic graphic = Create();
                graphic.Configure(kind, "test_status", false);
                Mesh mesh = Populate(graphic);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0), kind.ToString());
                Assert.That(mesh.vertexCount, Is.LessThan(2048), kind.ToString());
                Assert.That(mesh.triangles.Length, Is.GreaterThan(0), kind.ToString());
                Rect rect = graphic.rectTransform.rect;
                int fingerprint = 17;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x), Is.False, kind.ToString());
                    Assert.That(float.IsNaN(vertex.y) || float.IsInfinity(vertex.y), Is.False, kind.ToString());
                    Assert.That(vertex.x, Is.InRange(rect.xMin, rect.xMax), kind.ToString());
                    Assert.That(vertex.y, Is.InRange(rect.yMin, rect.yMax), kind.ToString());
                    unchecked
                    {
                        fingerprint = fingerprint * 31 + Mathf.RoundToInt(vertex.x * 1000f);
                        fingerprint = fingerprint * 31 + Mathf.RoundToInt(vertex.y * 1000f);
                    }
                }
                foreach (Color32 tint in mesh.colors32)
                {
                    unchecked { fingerprint = fingerprint * 31 + tint.r * 256 * 256 + tint.g * 256 + tint.b; }
                }
                Assert.That(fingerprints.Add(fingerprint.ToString()), Is.True, "Symbols must differ: " + kind);
                Assert.That(graphic.raycastTarget, Is.False, kind.ToString());
            }
        }

        [Test]
        public void Reconfigure_UpdatesIdentityAndDebuffOutlineWithoutBlockingTooltipReceiver()
        {
            BattleStatusIconGraphic graphic = Create();
            graphic.Configure(CoreBattleStatusKind.Guard, "guard", false);
            Color32[] buffColors = Populate(graphic).colors32;
            graphic.Configure(CoreBattleStatusKind.Guard, "curse_armor", true);
            Color32[] debuffColors = Populate(graphic).colors32;
            Assert.That(graphic.Kind, Is.EqualTo(CoreBattleStatusKind.Guard));
            Assert.That(graphic.StatusId, Is.EqualTo("curse_armor"));
            Assert.That(graphic.IsDebuff, Is.True);
            Assert.That(graphic.raycastTarget, Is.False);
            CollectionAssert.AreNotEqual(buffColors, debuffColors);
            graphic.Configure(CoreBattleStatusKind.Draw, null, false);
            Assert.That(graphic.StatusId, Is.EqualTo(string.Empty));
            Assert.That(graphic.Kind, Is.EqualTo(CoreBattleStatusKind.Draw));
        }

        [TestCase(CoreBattleStatusKind.Guard, "guard|deck_buff|prepare_fire_guard|natural_fire_guard|boarding_cover")]
        [TestCase(CoreBattleStatusKind.Fire, "burn|fire_rounds|boarding_burn|scorch|overflow|oil|self_burn|enemy_burn|fire_pact|fire_pots|free_powder")]
        [TestCase(CoreBattleStatusKind.Retaliation, "fire_retaliation|curse_counter|tide_ambush")]
        [TestCase(CoreBattleStatusKind.Curse, "curse_guard|curse_contract|curse_link|curse_tick_bonus|voyage_curse")]
        [TestCase(CoreBattleStatusKind.Thunder, "thunder_ready|precharge|grounding|thunder_residual")]
        [TestCase(CoreBattleStatusKind.Blood, "blood_attack|blood_debt|blood_oath|desperate")]
        [TestCase(CoreBattleStatusKind.Seal, "rune_clamp|load_seal|curse_armor")]
        [TestCase(CoreBattleStatusKind.Weakness, "pulse_reduction|thunder_weakness|curse_weakness|tide_weakness|crew_shortage")]
        [TestCase(CoreBattleStatusKind.Arcane, "arcane_piercing|retained_guard|arcane_echo")]
        [TestCase(CoreBattleStatusKind.Tide, "tide_guard|retained_guard")]
        [TestCase(CoreBattleStatusKind.Healing, "healing|healing_halved")]
        public void RelatedEffects_HaveMeaningfulDistinctVariantsInsideTheIcon(CoreBattleStatusKind kind, string ids)
        {
            HashSet<int> fingerprints = new HashSet<int>();
            BattleStatusIconGraphic graphic = Create();
            foreach (string id in ids.Split('|'))
            {
                graphic.Configure(kind, id, false);
                Mesh mesh = Populate(graphic);
                Assert.That(mesh.vertexCount, Is.InRange(1, 2048), id);
                Rect rect = graphic.rectTransform.rect;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Assert.That(vertex.x, Is.InRange(rect.xMin, rect.xMax), id);
                    Assert.That(vertex.y, Is.InRange(rect.yMin, rect.yMax), id);
                }
                Assert.That(fingerprints.Add(Fingerprint(mesh)), Is.True, "Distinct effect must have a distinct symbol: " + id);
            }
        }

        [Test]
        public void EmptyRect_ProducesNoInvalidMesh()
        {
            BattleStatusIconGraphic graphic = Create();
            graphic.Configure(CoreBattleStatusKind.Fire, "burn", true);
            graphic.rectTransform.sizeDelta = Vector2.zero;
            Assert.That(Populate(graphic).vertexCount, Is.Zero);
        }

        private BattleStatusIconGraphic Create()
        {
            GameObject host = new GameObject("Battle Status Symbol", typeof(RectTransform), typeof(BattleStatusIconGraphic));
            temporary.Add(host);
            BattleStatusIconGraphic graphic = host.GetComponent<BattleStatusIconGraphic>();
            graphic.rectTransform.sizeDelta = new Vector2(36f, 36f);
            return graphic;
        }

        private static int Fingerprint(Mesh mesh)
        {
            unchecked
            {
                int hash = 17;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    hash = hash * 31 + Mathf.RoundToInt(vertex.x * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(vertex.y * 1000f);
                }
                foreach (Color32 tint in mesh.colors32) hash = hash * 31 + tint.r * 256 * 256 + tint.g * 256 + tint.b;
                return hash;
            }
        }

        private Mesh Populate(BattleStatusIconGraphic graphic)
        {
            Mesh mesh = new Mesh();
            temporary.Add(mesh);
            using (VertexHelper helper = new VertexHelper())
            {
                typeof(BattleStatusIconGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            return mesh;
        }
    }
}
