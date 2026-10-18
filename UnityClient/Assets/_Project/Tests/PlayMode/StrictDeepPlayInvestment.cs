using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShatteredSail.Core.Gameplay;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private sealed partial class StrictDeepDriver
        {
            public bool MustTradeAfterSupplyInvestment { get; private set; }

            // Runs only inside the parent PlayMode fixture, whose SetUp/TearDown
            // preserve and restore the player's original progression. Every
            // transaction below uses earned test-run money and actual UI controls.
            public IEnumerator InvestForSuppliesIfNeeded(int requiredRations = 0)
            {
                MustTradeAfterSupplyInvestment = false;
                Assert.That(Session.Phase, Is.EqualTo(CoreDemoPhase.Port));
                yield return OpenStrictMarket();
                int waterQuota = VisibleMarketRemaining("water"), foodQuota = VisibleMarketRemaining("food");
                int emergencyReserve = Math.Max(1, Session.CrewCountValue) * 2;
                bool sufficient = requiredRations > 0
                    ? waterQuota >= Math.Max(0, requiredRations - Session.Cargo.Water) && foodQuota >= Math.Max(0, requiredRations - Session.Cargo.Food)
                    : (waterQuota >= 100 && foodQuota >= 100) || Math.Min(Session.Cargo.Water, Session.Cargo.Food) >= emergencyReserve;
                if (sufficient)
                {
                    yield return EnsureHub();
                    yield break;
                }

                string port = Session.CurrentPort;
                yield return EnsureHub();
                yield return Click("Port Hotspot Maintenance");
                Match displayedLevel = Regex.Match(PortText("Port Investment Current Level")?.text ?? string.Empty, @"Lv\.(\d+)");
                Assert.That(displayedLevel.Success, Is.True, "Investment level must be visible to the player.");
                int level = int.Parse(displayedLevel.Groups[1].Value);
                if (level >= PortInvestmentCatalog.MaxLevel)
                {
                    Record("Supply investment unavailable: " + port + " already at maximum level; daily stock left unchanged.");
                    yield return EnsureHub();
                    yield break;
                }

                PortInvestmentCost cost = PortInvestmentCatalog.CostForLevel(port, level + 1);
                var requirements = new Dictionary<string, int>
                {
                    { "wood", cost.Wood }, { "iron", cost.Iron },
                    { PortMarketCatalog.Key(cost.RequiredItem), cost.RequiredQuantity }
                };
                if (NoGmFirstTreasure)
                {
                    foreach (CorePortInvestmentRequirement rare in cost.RareMaterials)
                    {
                        long held = Session.GetPortInvestmentMaterialAmount(rare.Key);
                        if (held >= rare.Quantity) continue;
                        Record("Supply investment blocked by earned rare material: " + rare.Key + "; required=" + rare.Quantity + "; held=" + held + "; no material grants.");
                        yield return EnsureHub();
                        yield break;
                    }
                }
                Record("Supply investment quoted: " + port + " Lv." + level + " -> " + (level + 1) +
                    "; gold=" + cost.Gold + "; wood=" + cost.Wood + "; iron=" + cost.Iron +
                    "; " + PortMarketCatalog.Key(cost.RequiredItem) + "=" + cost.RequiredQuantity +
                    "; water quota=" + waterQuota + "; food quota=" + foodQuota);
                yield return Click("Open Port Market");

                decimal procurementCost = 0m;
                double addedVolume = 0;
                var carriedTargets = new Dictionary<string, int>();
                foreach (var requirement in requirements)
                {
                    int missing = (int)Math.Max(0L, requirement.Value - Session.GetPortInvestmentMaterialAmount(requirement.Key));
                    PortMarketItem item = PortItem(requirement.Key);
                    carriedTargets[requirement.Key] = Session.GetMarketCargoAmount(requirement.Key) + missing;
                    if (missing > VisibleMarketRemaining(requirement.Key))
                    {
                        Record("Supply investment blocked by actual material quota: " + requirement.Key + " missing=" + missing +
                            "; remaining=" + VisibleMarketRemaining(requirement.Key) + "; no stock reset.");
                        yield return EnsureHub();
                        yield break;
                    }
                    if (missing > 0) procurementCost += PortMarketCatalog.CalculateBuyCost(port, item, Session.GameDateTime, missing);
                    addedVolume += missing * (double)PortMarketCatalog.CargoVolume(item);
                }
                decimal rationBudget = PortMarketCatalog.CalculateBuyCost(port, PortMarketItem.Water, Session.GameDateTime,
                    requiredRations > 0 ? Math.Max(0, requiredRations - Session.Cargo.Water) : 100) +
                    PortMarketCatalog.CalculateBuyCost(port, PortMarketItem.Food, Session.GameDateTime,
                    requiredRations > 0 ? Math.Max(0, requiredRations - Session.Cargo.Food) : 100);
                if (procurementCost + cost.Gold + rationBudget > Session.ProfileGold ||
                    Session.Cargo.UsedVolume + addedVolume > Session.CargoCapacityValue + .001d)
                {
                    Record("Supply investment cannot currently be assembled: material purchase=" + procurementCost +
                        "; upgrade=" + cost.Gold + "; ration reserve=" + rationBudget + "; gold=" + Session.ProfileGold +
                        "; hold after materials=" + (Session.Cargo.UsedVolume + addedVolume) + "/" + Session.CargoCapacityValue);
                    yield return EnsureHub();
                    yield break;
                }

                decimal goldBeforeProcurement = Session.ProfileGold;
                foreach (var target in carriedTargets) yield return BuyResource(target.Key, target.Value);
                Record("Investment material procurement completed through market: spent=" + (goldBeforeProcurement - Session.ProfileGold));
                yield return EnsureHub();
                yield return Click("Port Hotspot Maintenance");
                Assert.That(Session.CurrentPort, Is.EqualTo(port));
                foreach (var requirement in requirements)
                    Assert.That(Session.GetPortInvestmentMaterialAmount(requirement.Key), Is.GreaterThanOrEqualTo((long)requirement.Value));
                Button upgrade = PortButton("Confirm Port Investment");
                Assert.That(upgrade, Is.Not.Null);
                Assert.That(upgrade.IsInteractable(), Is.True, "Only the real affordable investment may extend daily supply limits.");
                Shot("Supply_Investment_Before_Lv" + (level + 1), "Port Investment Panel");
                var beforeMaterials = new Dictionary<string, long>();
                foreach (var requirement in requirements) beforeMaterials[requirement.Key] = Session.GetPortInvestmentMaterialAmount(requirement.Key);
                decimal beforeGold = Session.ProfileGold;
                yield return Click("Confirm Port Investment");
                Assert.That(Session.ProfileGold, Is.EqualTo(beforeGold - cost.Gold));
                foreach (var requirement in requirements)
                    Assert.That(Session.GetPortInvestmentMaterialAmount(requirement.Key), Is.EqualTo(beforeMaterials[requirement.Key] - requirement.Value));
                Match updatedLevel = Regex.Match(PortText("Port Investment Current Level")?.text ?? string.Empty, @"Lv\.(\d+)");
                Assert.That(updatedLevel.Success, Is.True);
                Assert.That(int.Parse(updatedLevel.Groups[1].Value), Is.EqualTo(level + 1));
                MustTradeAfterSupplyInvestment = true;
                Record("Legitimate supply investment completed: " + port + " Lv." + (level + 1) +
                    "; gold before=" + beforeGold + "; gold after=" + Session.ProfileGold +
                    "; material procurement=" + (goldBeforeProcurement - beforeGold) + "; no GM or inventory reset.");
                Shot("Supply_Investment_After_Lv" + (level + 1), "Port Investment Panel");
                yield return Click("Open Port Market");
                Record("Post-investment actual market allowance: water " + waterQuota + " -> " + VisibleMarketRemaining("water") +
                    "; food " + foodQuota + " -> " + VisibleMarketRemaining("food"));
                yield return BuyResource("water", Math.Max(100, emergencyReserve));
                yield return BuyResource("food", Math.Max(100, emergencyReserve));
                Shot("Supply_Investment_Normal_Resupply", "Market Panel");
                yield return EnsureHub();
            }
        }
    }
}

