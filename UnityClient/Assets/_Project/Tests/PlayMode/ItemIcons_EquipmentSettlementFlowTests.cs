using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ItemIcons_EquipmentAndSettlementKeepPicturesClearOfNamesAndAmounts()
        {
            yield return LoadProvisioningFixture();
            var controller = Object.FindFirstObjectByType<CoreGameplayDemoController>();
            var catalog = new ComponentArtCatalog();
            // This is an equipment-art fixture, so explicitly create the
            // catalogue loadout rather than relying on the new bare starter.
            PopulateBareStarterForFixture(controller);
            ComponentArtEntry[] samples = new[] { ComponentSlot.Bow, ComponentSlot.Cannon }
                .Select(slot => catalog.Entries.First(entry => entry.Slot == slot && entry.Rarity == ComponentRarity.Rare)).ToArray();
            OwnedShipComponent[] inventory = samples.Select(entry => controller.GrantOwnedComponent(entry, 1f)).ToArray();
            yield return ClickShipyardControl("Port Hotspot Shipyard");
            yield return ClickShipyardControl("Shipyard Ship 0");
            yield return ClickShipyardControl("Shipyard Dossier Tab Equipment");
            yield return WaitForItemIconView(controller);
            for (int i = 0; i < ComponentArtCatalog.SlotCount; i++)
            {
                ComponentSlot slot = (ComponentSlot)i;
                RawImage icon = AssertItemIcon("Shipyard Installed Art " + slot,
                    ItemIconCatalog.ForComponent(controller.ShipComponent(0, slot)), 48f);
                AssertIconDoesNotCoverRowText(icon);
                AssertIconPointerReachesButton(icon, "Shipyard Equipment Slot " + slot, false);
            }
            for (int i = 0; i < inventory.Length; i++)
            {
                RawImage icon = AssertItemIcon("Shipyard Inventory Art " + inventory[i].InstanceId,
                    ItemIconCatalog.ForComponent(samples[i]), 48f);
                AssertIconDoesNotCoverRowText(icon);
                AssertIconPointerReachesButton(icon, "Shipyard Equipment Owned " + inventory[i].InstanceId, false);
            }
            NavalBattleVisualCapture.Capture(controller, "ItemIcons_11_Equipment_Inventory.png",
                "隔离部件库存夹具；五个已装槽位与两件闲置部件使用48像素图标", "Shipyard Detail");

            // Use the real Core settlement boundary and production renderer.
            // Reward values below are explicitly a visual fixture, not earned loot.
            CoreVoyageSession session = controller.Session;
            session.Cargo.Water = session.Cargo.Food = 400;
            session.StartVoyage();
            Assert.That(session.Phase, Is.EqualTo(CoreDemoPhase.Sailing));
            Assert.That(session.SafeReturn(), Is.True);
            VoyageSettlement result = session.Settlement;
            result.BankedGold = 1268; result.BankedWood = 32; result.BankedIron = 18; result.BankedFame = 47;
            result.TreasureCompleted = true; result.BankedFragments = 2; result.BankedPirateExperience = 15;
            result.BankedGem = 3; result.BankedAncientMaterial = 4; result.BankedRelic = 1; result.BankedBlackPearl = 2;
            decimal profileGold = session.ProfileGold;
            typeof(CoreGameplayDemoController).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            yield return null;
            yield return WaitForItemIconView(controller);
            if (ActiveButton("Skip Settlement Reveal")?.IsInteractable() == true)
            {
                ClickAcceptanceButton("Skip Settlement Reveal"); yield return null;
            }
            Assert.That(Object.FindFirstObjectByType<SettlementRevealController>().IsComplete, Is.True);
            RawImage gold = AssertItemIcon("Settlement Featured Icon Gold", ItemIconCatalog.ForSpecial("gold"), 48f);
            AssertIconDoesNotCoverRowText(gold);
            foreach (string key in new[] { "Wood", "Iron" })
                AssertIconDoesNotCoverRowText(AssertItemIcon("Settlement Resource Icon " + key, ItemIconCatalog.ForCargo(key), 32f));
            foreach (string key in new[] { "gem", "ancientmaterial", "relic", "blackpearl", "treasurefragment" })
                AssertIconDoesNotCoverRowText(AssertItemIcon("Settlement Treasure Icon " + key,
                    key == "treasurefragment" ? ItemIconCatalog.ForSpecial(key) : ItemIconCatalog.ForCargo(key), 32f));
            string[] ids = { "Gold", "Wood", "Iron", "Fame" };
            int[] amounts = { result.BankedGold, result.BankedWood, result.BankedIron, result.BankedFame };
            for (int i = 0; i < ids.Length; i++)
                Assert.That(GameObject.Find("Settlement Reward " + ids[i]).GetComponent<Text>().text,
                    Is.EqualTo("+" + amounts[i].ToString("N0")), ids[i] + " retains its exact reward amount.");
            Text manifest = GameObject.Find("Settlement Treasure Rewards").GetComponent<Text>();
            Assert.That(manifest.text, Does.Contain("藏宝图碎片 +2"));
            Assert.That(manifest.preferredHeight, Is.LessThanOrEqualTo(manifest.rectTransform.rect.height + 1f));
            Assert.That(session.ProfileGold, Is.EqualTo(profileGold), "Rendering and skipping the reveal must not bank rewards.");
            NavalBattleVisualCapture.Capture(controller, "ItemIcons_12_Settlement_Rewards.png",
                "真实结算界面的隔离奖励展示夹具；金币、材料、珍宝与碎片图标不遮挡数量", "Settlement Card");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
