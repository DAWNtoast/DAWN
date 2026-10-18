using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ShatteredSail.Core.Gameplay;

internal static class ExportRewards
{
    private static int Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : "docs/BoardingBossRewards20261015.csv";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        using (var writer = new StreamWriter(output, false, new UTF8Encoding(true)))
        {
            writer.WriteLine("id,name,kind,school,boardingHp,meleeDamage,boardingGuard,authoredGold,materialGold,materialGoldWithDiver,navalGold,boardingGold,navalGoldWithDiver,boardingGoldWithDiver,navalFame,boardingFame,navalGoldHalf,boardingGoldHalf,navalGoldQuarter,boardingGoldQuarter,baseComponentChanceOnCapture,bossEquipmentChanceOnCapture,bossEquipmentRarity,bossEquipmentIds");
            foreach (CoreRouteBoss boss in CoreRouteBossCatalog.All)
                Write(writer, boss.CreateEncounter(), boss.Profile, "route", boss.SchoolId);
            foreach (CoreLegendaryWarship boss in CoreLegendaryWarships.All)
                Write(writer, boss.CreateEncounter(), boss.Profile, "legendary", boss.SchoolId);
            Write(writer, CoreLegendaryWarships.CreateTreasureEncounter(), CoreBattleBalance.ThirdTreasureBoss, "treasure", "curse");
            Write(writer, CoreGuidedTutorialContent.BossEvent(), CoreGuidedTutorialContent.BossProfile, "tutorial", "neutral");
        }
        Console.WriteLine("Exported 14 real boss encounters to " + output);
        return 0;
    }

    private static void Write(StreamWriter writer, CoreEncounter encounter, CoreBattleProfile profile, string kind, string school)
    {
        VoyageEffect reward = encounter.Choices.First(choice => choice.StartsBattle).Effect;
        var equipment = CoreBoardingLootBalance.BossEquipment(encounter.Id, out CoreBoardingComponentRarity rarity);
        writer.WriteLine(Csv(encounter.Id, encounter.Name, kind, school, profile.BoardingHealth, profile.MeleeDamage,
            profile.BoardingGuard, reward.Gold, CoreBattleRewardBalance.MaterialGoldValue(reward),
            CoreBattleRewardBalance.MaterialGoldValue(reward, diver: true),
            CoreBattleRewardBalance.GoldReward(true, reward, false), CoreBattleRewardBalance.GoldReward(true, reward, true),
            CoreBattleRewardBalance.GoldReward(true, reward, false, diver: true), CoreBattleRewardBalance.GoldReward(true, reward, true, diver: true),
            CoreBattleRewardBalance.FameReward(true, reward, false), CoreBattleRewardBalance.FameReward(true, reward, true),
            CoreBattleRewardBalance.GoldReward(true, reward, false, .5d), CoreBattleRewardBalance.GoldReward(true, reward, true, .5d),
            CoreBattleRewardBalance.GoldReward(true, reward, false, .25d), CoreBattleRewardBalance.GoldReward(true, reward, true, .25d),
            CoreVoyageSession.BoardingComponentDropChance, equipment.Count > 0 ? CoreBoardingLootBalance.BossEquipmentDropChance : 0d,
            equipment.Count > 0 ? rarity.ToString() : string.Empty, string.Join("|", equipment)));
    }

    private static string Csv(params object[] values) => string.Join(",", values.Select(value => "\"" +
        Convert.ToString(value, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\""));
}
