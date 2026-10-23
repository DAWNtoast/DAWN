using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Content;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class DepartureCrewAvatarTests
    {
        [Test]
        public void AllCanonicalCrew_HaveDistinctSmallDepartureCropsAndFullImageUvs()
        {
            var catalog = new PortArtCatalog();
            var identities = new HashSet<string>();
            var textures = new HashSet<Texture2D>();
            foreach (CrewVariantDefinition crew in CrewNarrativeCatalog.AllCrew)
            {
                string resource = "PortArt/CrewAvatars/" + crew.Id + "_avatar_v1";
                Texture2D avatar = Resources.Load<Texture2D>(resource);
                Assert.That(avatar, Is.Not.Null, crew.Id);
                Assert.That(avatar.width, Is.EqualTo(256), crew.Id);
                Assert.That(avatar.height, Is.EqualTo(336), crew.Id);
                Assert.That(identities.Add(crew.Id), Is.True);
                Assert.That(textures.Add(avatar), Is.True, "Every identity requires its own dossier crop.");
                Assert.That(catalog.DepartureCrewPortrait(crew), Is.SameAs(avatar), crew.Id);
                Assert.That(catalog.DepartureCrewPortraitUv(crew), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)), crew.Id);
            }
            Assert.That(identities.Count, Is.EqualTo(36));
            catalog.ReleaseLoadedTextures();
        }

        [Test]
        public void DeparturePortrait_LoadsOnlyRequestedSmallIdentityAndCachesBothTextureAndUvLookup()
        {
            var requests = new List<string>();
            var avatar = new Texture2D(2, 2);
            CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession("断锚港", 0);
            string expected = "PortArt/CrewAvatars/" + crew.Id + "_avatar_v1";
            var catalog = Create(path => { requests.Add(path); return path == expected ? avatar : null; });
            try
            {
                Assert.That(requests, Is.Empty);
                Assert.That(catalog.DepartureCrewPortrait(crew), Is.SameAs(avatar));
                Assert.That(catalog.DepartureCrewPortraitUv(crew), Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                Assert.That(catalog.DepartureCrewPortrait(crew), Is.SameAs(avatar));
                Assert.That(requests, Is.EqualTo(new[] { expected }), "Opening a small roster portrait must not load full dossier paintings or old atlases.");
                catalog.ReleaseLoadedTextures();
                Assert.That(avatar != null, Is.True, "The live roster may still own the borrowed image.");
                Assert.That(catalog.DepartureCrewPortrait(crew), Is.SameAs(avatar));
                Assert.That(requests.Count, Is.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(avatar); }
        }

        [Test]
        public void MissingSmallCrop_KeepsLegacyTextureAndHeadUvTogetherWithoutRepeatedMissingLoads()
        {
            var requests = new List<string>();
            var atlas = new Texture2D(6, 4);
            CrewVariantDefinition crew = CrewNarrativeCatalog.ForPortAndProfession("白贝港", 3);
            var catalog = Create(path =>
            {
                requests.Add(path);
                return path == "PortArt/CrewRegional/crew_white_shell_portraits_v2" ? atlas : null;
            });
            try
            {
                Assert.That(catalog.DepartureCrewPortrait(crew), Is.SameAs(atlas));
                Rect expectedHeadUv = catalog.CrewPortraitUv(crew, true);
                Assert.That(catalog.DepartureCrewPortraitUv(crew), Is.EqualTo(expectedHeadUv));
                Assert.That(expectedHeadUv, Is.Not.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                catalog.DepartureCrewPortrait(crew);
                catalog.DepartureCrewPortraitUv(crew);
                Assert.That(requests, Is.EqualTo(new[] {
                    "PortArt/CrewAvatars/" + crew.Id + "_avatar_v1",
                    "PortArt/CrewRegional/crew_white_shell_portraits_v2"
                }));
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }
        }

        [Test]
        public void NullCrew_PreservesLegacyFallbackWithoutInventingAnAvatarPath()
        {
            var requests = new List<string>();
            var fallback = new Texture2D(2, 2);
            var catalog = Create(path => { requests.Add(path); return fallback; });
            try
            {
                Assert.That(catalog.DepartureCrewPortrait(null), Is.SameAs(fallback));
                Assert.That(catalog.DepartureCrewPortraitUv(null), Is.EqualTo(catalog.CrewPortraitUv(null, true)));
                Assert.That(requests, Is.EqualTo(new[] { "PortArt/CrewFullBody/crew_navigator_evelyn_fullbody" }));
            }
            finally { UnityEngine.Object.DestroyImmediate(fallback); }
        }

        private static PortArtCatalog Create(Func<string, Texture2D> loader)
        {
            ConstructorInfo constructor = typeof(PortArtCatalog).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(Func<string, Texture2D>) }, null);
            Assert.That(constructor, Is.Not.Null);
            return (PortArtCatalog)constructor.Invoke(new object[] { loader });
        }
    }
}
