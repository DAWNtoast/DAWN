using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace ShatteredSail.Core.Tests
{
    public sealed class BoardingSpatialSchoolGraphicTests
    {
        readonly List<Object> temporary=new List<Object>();
        static readonly Vector2 Source=new Vector2(-480,205),Target=new Vector2(480,205);
        const float Floor=65f,Sky=438f;
        [TearDown] public void Cleanup(){for(int i=temporary.Count-1;i>=0;i--)if(temporary[i]!=null)Object.DestroyImmediate(temporary[i]);temporary.Clear();}

        [Test]
        public void FireHeatsTargetDeckThenEruptsUpwardFromTheSameFloor()
        {
            var effect=Create(BattleSchoolAttackKind.Fire);
            Vector3[] heat=Visible(effect,.12f);
            Assert.That(heat.Length,Is.GreaterThan(0));
            Assert.That(heat.Min(v=>v.x),Is.GreaterThan(Target.x-180));
            Assert.That(heat.Max(v=>v.x),Is.LessThan(Target.x+180));
            Assert.That(heat.Max(v=>v.y),Is.LessThan(Floor+30),"Fire must begin under boots, not fly horizontally from the caster.");
            effect.SetProgress(.60f);
            var flames=effect.GetComponentsInChildren<RawImage>();
            Assert.That(flames.Length,Is.EqualTo(3),"The authored eruption art must be imported and available.");
            foreach(var flame in flames)
            {
                Assert.That(flame.texture,Is.Not.Null);Assert.That(flame.raycastTarget,Is.False);
                Assert.That(flame.color.a,Is.GreaterThan(.2f));
                Assert.That(flame.rectTransform.pivot.y,Is.Zero);
                Assert.That(flame.rectTransform.anchoredPosition.y,Is.InRange(Floor-20,Floor+5));
                Assert.That(flame.rectTransform.rect.height,Is.GreaterThan(150f));
            }
        }

        [Test]
        public void ThunderFormsOverTargetThenDescendsFromSkyToDeck()
        {
            var effect=Create(BattleSchoolAttackKind.Thunder);
            Vector3[] charge=Visible(effect,.12f),falling=Visible(effect,.30f),contact=Visible(effect,effect.ContactAt);
            Assert.That(charge.Min(v=>v.y),Is.GreaterThan(Target.y+120),"Charge belongs above the target, not on the horizontal caster-to-target line.");
            Assert.That(charge.Average(v=>v.x),Is.InRange(Target.x-90,Target.x+90));
            Assert.That(falling.Min(v=>v.y),Is.LessThan(charge.Min(v=>v.y)-60));
            Assert.That(contact.Min(v=>v.y),Is.LessThan(Floor+10));
            Assert.That(contact.Max(v=>v.y),Is.GreaterThan(Sky-35));
        }

        [TestCase(false)] [TestCase(true)]
        public void TideTravelsAlongDeckAndReversesWithAttackingSide(bool reverse)
        {
            var effect=Create(BattleSchoolAttackKind.Tide,reverse);
            var wave=effect.GetComponentsInChildren<RawImage>().Single();
            Assert.That(wave.texture,Is.Not.Null);Assert.That(wave.raycastTarget,Is.False);
            effect.SetProgress(.08f);float first=wave.rectTransform.anchoredPosition.x;
            effect.SetProgress(.30f);float second=wave.rectTransform.anchoredPosition.x;
            Assert.That((second-first)*(reverse?-1f:1f),Is.GreaterThan(500f));
            Assert.That(wave.rectTransform.pivot.y,Is.Zero);
            Assert.That(wave.rectTransform.anchoredPosition.y,Is.InRange(Floor-35,Floor+5));
            Assert.That(wave.uvRect.width*(reverse?-1f:1f),Is.GreaterThan(0));
            Vector3[] sheet=Visible(effect,.30f);
            Assert.That(sheet.Min(v=>v.y),Is.GreaterThan(Floor-12));
            Assert.That(sheet.Count(v=>v.y<Floor+28),Is.GreaterThan(sheet.Length/2),"The water body must hug the deck below its crest.");
        }

        [Test]
        public void CurseClimbsAndWrapsTargetInsteadOfUsingAProjectile()
        {
            var effect=Create(BattleSchoolAttackKind.Curse);
            Vector3[] early=Visible(effect,.08f),grown=Visible(effect,.45f);
            Assert.That(early.Min(v=>v.x),Is.GreaterThan(Target.x-170));
            Assert.That(grown.Min(v=>v.x),Is.GreaterThan(Target.x-170));
            Assert.That(grown.Max(v=>v.x),Is.LessThan(Target.x+170));
            Assert.That(grown.Max(v=>v.y),Is.GreaterThan(early.Max(v=>v.y)+100));
            Assert.That(grown.Min(v=>v.y),Is.LessThan(Floor+5));
        }

        [TestCase(false)] [TestCase(true)]
        public void BloodReturnsFromVictimToCasterAfterContact(bool reverse)
        {
            var effect=Create(BattleSchoolAttackKind.Blood,reverse);
            float first=VisibleMeanX(effect,.60f),later=VisibleMeanX(effect,.85f);
            Assert.That((first-later)*(reverse?-1f:1f),Is.GreaterThan(250f),"The later stream must return toward the caster, rather than keep attacking outward.");
        }

        [Test]
        public void ArcaneConvergesAtCasterThenFracturesAroundTarget()
        {
            var effect=Create(BattleSchoolAttackKind.Arcane);
            Assert.That(VisibleMeanX(effect,.10f),Is.LessThan(-300f));
            Assert.That(VisibleMeanX(effect,.50f),Is.GreaterThan(300f));
        }

        [Test]
        public void SpatialEffectsAreBoundedDeterministicAndRetireAllSpriteLayers()
        {
            foreach(BattleSchoolAttackKind school in Enum.GetValues(typeof(BattleSchoolAttackKind)))
            {
                var effect=Create(school);
                UnityEngine.Random.State random=UnityEngine.Random.state;
                foreach(float p in new[]{0f,.12f,.30f,.45f,.65f,.85f})
                {
                    var mesh=Populate(effect,p);
                    Assert.That(mesh.vertexCount,Is.LessThanOrEqualTo(3000));
                    foreach(Vector3 v in mesh.vertices)
                    {
                        Assert.That(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y),Is.False);
                        Assert.That(v.x,Is.InRange(-870f,870f));Assert.That(v.y,Is.InRange(-450f,450f));
                    }
                    CollectionAssert.AreEqual(mesh.vertices,Populate(effect,p).vertices);
                }
                Assert.That(UnityEngine.Random.state,Is.EqualTo(random));
                effect.SetProgress(1f);
                Assert.That(Populate(effect,1f).vertexCount,Is.Zero);
                Assert.That(effect.GetComponentsInChildren<RawImage>(true).All(g=>g.color.a==0f),Is.True);
                Assert.That(effect.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),Is.True);
            }
        }

        BattleSchoolAttackGraphic Create(BattleSchoolAttackKind school,bool reverse=false)
        {
            var go=new GameObject("spatial school test",typeof(RectTransform),typeof(BattleSchoolAttackGraphic));temporary.Add(go);
            var effect=go.GetComponent<BattleSchoolAttackGraphic>();effect.rectTransform.sizeDelta=new Vector2(1740,900);
            Vector2 from=reverse?Target:Source,to=reverse?Source:Target;
            effect.Configure(school,BattleSchoolAttackOutcome.Hit,from,to,true);
            effect.ConfigureBoardingEnvironment(new Vector2(from.x,Floor),new Vector2(to.x,Floor),Sky);
            return effect;
        }
        Mesh Populate(BattleSchoolAttackGraphic effect,float p)
        {
            effect.SetProgress(p);var mesh=new Mesh();temporary.Add(mesh);
            using(var helper=new VertexHelper())
            {
                typeof(BattleSchoolAttackGraphic).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null).Invoke(effect,new object[]{helper});
                helper.FillMesh(mesh);
            }
            return mesh;
        }
        Vector3[] Visible(BattleSchoolAttackGraphic effect,float p)
        {
            var mesh=Populate(effect,p);var vertices=mesh.vertices;var colors=mesh.colors32;
            return vertices.Where((v,i)=>colors[i].a>5).ToArray();
        }
        float VisibleMeanX(BattleSchoolAttackGraphic effect,float p)
        {
            var mesh=Populate(effect,p);var vertices=mesh.vertices;var colors=mesh.colors32;float weight=0,value=0;
            for(int i=0;i<vertices.Length;i++)if(colors[i].a>5){weight+=colors[i].a;value+=vertices[i].x*colors[i].a;}
            Assert.That(weight,Is.GreaterThan(0));return value/weight;
        }
    }
}