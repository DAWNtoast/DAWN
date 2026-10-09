using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ShatteredSail.Presentation.Tests
{
    public sealed class WarehouseSerializedSceneTests
    {
        [TestCase("UI_Warehouse.unity")]
        [TestCase("UIWarehouse/UIWarehouse_EmptySlots.unity")]
        [TestCase("UIWarehouse/UIWarehouse_MultipleCollectibles.unity")]
        [TestCase("UIWarehouse/UIWarehouse_CollectionDetail.unity")]
        [TestCase("TutorialVoyage/29_Warehouse.unity")]
        [TestCase("TutorialVoyage/30_SelectTreasure.unity")]
        [TestCase("TutorialVoyage/31_DisplayTreasure.unity")]
        public void AuthoredWarehouseScene_HasCompleteLocalReferencesAndUniqueComponentOwnership(string relativePath)
        {
            // Loading a corrupt scene lets Unity discard orphaned components.
            // Inspect the saved references first so that silent repair cannot
            // hide missing authored collectible cards or their artwork.
            string path = Path.Combine(Application.dataPath, "_Project/Scenes/UIPreviewsForMe", relativePath);
            string source = File.ReadAllText(path);
            Match[] documents = Regex.Matches(source,
                @"(?ms)^--- !u!(?<type>\d+) &(?<id>\d+)\r?\n(?<body>.*?)(?=^--- !u!|\z)")
                .Cast<Match>().ToArray();
            Assert.That(documents, Is.Not.Empty, relativePath);
            Assert.That(documents.Select(item => item.Groups["id"].Value), Is.Unique,
                "Every saved object must have a unique fileID in " + relativePath);
            Dictionary<string, Match> byId = documents.ToDictionary(item => item.Groups["id"].Value);
            foreach (Match reference in Regex.Matches(source, @"\{fileID: (?<id>-?\d+)\}"))
            {
                string id = reference.Groups["id"].Value;
                if (id == "0") continue;
                Assert.That(byId.ContainsKey(id), Is.True,
                    "Unresolved local object reference " + id + " in " + relativePath);
            }

            var ownerByComponent = new Dictionary<string, string>();
            foreach (Match gameObject in documents.Where(item => item.Groups["type"].Value == "1"))
            {
                string owner = gameObject.Groups["id"].Value;
                MatchCollection components = Regex.Matches(gameObject.Groups["body"].Value,
                    @"component: \{fileID: (?<id>\d+)\}");
                foreach (Match component in components)
                {
                    string id = component.Groups["id"].Value;
                    Assert.That(ownerByComponent.ContainsKey(id), Is.False,
                        "Component " + id + " is attached more than once in " + relativePath);
                    ownerByComponent.Add(id, owner);
                    string actualOwner = Regex.Match(byId[id].Groups["body"].Value,
                        @"m_GameObject: \{fileID: (?<id>\d+)\}").Groups["id"].Value;
                    Assert.That(actualOwner, Is.EqualTo(owner),
                        "Component " + id + " points to another GameObject in " + relativePath);
                }
            }
            foreach (Match document in documents)
            {
                Match owner = Regex.Match(document.Groups["body"].Value,
                    @"m_GameObject: \{fileID: (?<id>\d+)\}");
                if (!owner.Success || owner.Groups["id"].Value == "0") continue;
                Assert.That(ownerByComponent.ContainsKey(document.Groups["id"].Value), Is.True,
                    "Dangling component " + document.Groups["id"].Value + " in " + relativePath);
            }

            foreach (Match transform in documents.Where(item =>
                item.Groups["type"].Value == "224" || item.Groups["type"].Value == "4"))
            {
                string id = transform.Groups["id"].Value;
                string parent = Regex.Match(transform.Groups["body"].Value,
                    @"m_Father: \{fileID: (?<id>\d+)\}").Groups["id"].Value;
                if (parent != "0")
                {
                    Assert.That(byId[parent].Groups["type"].Value, Is.EqualTo("224").Or.EqualTo("4"),
                        "Transform " + id + " must reference a parent Transform, not a GameObject in " + relativePath);
                    Assert.That(TransformChildren(byId[parent]).Count(child => child == id), Is.EqualTo(1),
                        "Parent " + parent + " must list child " + id + " exactly once in " + relativePath);
                }
                foreach (string child in TransformChildren(transform))
                {
                    Assert.That(byId[child].Groups["type"].Value, Is.EqualTo("224").Or.EqualTo("4"),
                        "Transform children must reference Transform objects in " + relativePath);
                    string childParent = Regex.Match(byId[child].Groups["body"].Value,
                        @"m_Father: \{fileID: (?<id>\d+)\}").Groups["id"].Value;
                    Assert.That(childParent, Is.EqualTo(id),
                        "Transform " + child + " must point back to its listed parent " + id + " in " + relativePath);
                }
            }
        }

        private static IEnumerable<string> TransformChildren(Match transform)
        {
            string list = Regex.Match(transform.Groups["body"].Value,
                @"(?m)^  m_Children:\r?\n(?<list>(?:  - \{fileID: \d+\}\r?\n)+)").Groups["list"].Value;
            return Regex.Matches(list, @"fileID: (?<id>\d+)").Cast<Match>()
                .Select(item => item.Groups["id"].Value);
        }
    }
}
