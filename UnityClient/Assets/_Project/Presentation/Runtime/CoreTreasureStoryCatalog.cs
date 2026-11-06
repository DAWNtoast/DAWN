using System;
using System.Collections.Generic;

namespace ShatteredSail.Presentation
{
    public sealed class TreasureBattleStory
    {
        internal TreasureBattleStory(string encounterId, string shipName, string[] pages)
        {
            EncounterId = encounterId;
            ShipName = shipName;
            Pages = Array.AsReadOnly(pages ?? Array.Empty<string>());
        }

        public string EncounterId { get; }
        public string ShipName { get; }
        public IReadOnlyList<string> Pages { get; }
    }

    /// <summary>
    /// The six ship-specific pre-battle readings
    /// share stable ids with the core encounter catalog.
    /// </summary>
    public static class CoreTreasureStoryCatalog
    {
        private static readonly IReadOnlyList<TreasureBattleStory> battles =
            Array.AsReadOnly(new[]
            {
                new TreasureBattleStory("legendary_pirate_iron_oath", "铁誓不沉号", new[]
                {
                    "铁誓·达恩将撤退令连同旧铁熔入船壳。那以后，他总说帝国还在，只要手下敲一敲这层装甲。",
                    "炮口缓缓抬起，甲片逐层合拢。沉门就在铁壁之后，海面只剩装填声与你们的呼吸。"
                }),
                new TreasureBattleStory("legendary_pirate_red_widow", "绯刃寡妇号", new[]
                {
                    "红帆曾接回一船孩子。如今绯刃·玛拉仍放下舷梯，只是在每一个登船的人面前伸出了收钱的手。",
                    "钩索落在舷边，绷得笔直。那面救过人的红帆就在眼前，甲板上已没有多少退让的地方。"
                }),
                new TreasureBattleStory("legendary_pirate_thunder_crown", "雷冠审判号", new[]
                {
                    "索尔听雷判罪，把炮声记作神谕。翻开王庭账簿，每一场齐射都有一行整齐的判词。",
                    "炮舱里先闪过一道天光。齐射将至，账簿上还没有你们的名字，船员已在等开火的手势。"
                }),
                new TreasureBattleStory("legendary_pirate_ember_ark", "余烬方舟号", new[]
                {
                    "余烬·伊诺从海底带回永燃之火。旧誓词烧掉以后，灰散进海里，那团火却始终不肯熄。",
                    "方舟炉门敞开，热气逼近甲板。王庭印记仍埋在灰中，得先压住那团不肯停歇的火。"
                }),
                new TreasureBattleStory("legendary_pirate_tide_requiem", "潮汐镇魂号", new[]
                {
                    "潮汐·塞勒涅还在寻找老船长。她借得月潮的航线，却将自己的回程押给了海巨人，至今没有赎回。",
                    "她不会先开炮，只让潮水反复推偏你们的船头。赢下这场切磋，替她在图上续回那条归路。"
                }),
                new TreasureBattleStory("legendary_pirate_last_horizon", "终界观星号", new[]
                {
                    "终界观星者不理解老船长最后的行为，却从未反叛。老船长离开后，他与学者们再次踏上探险之路，在沉月王庭找到一幅没有终点的星图，始终保持中立。",
                    "观测舱传来切磋邀请，双方约定一方认输便收炮，不夺船、不索贡。星轨校准完毕，观星者问：若地平线真的有尽头，你们准备带谁一起抵达？"
                })
            });

        public static IReadOnlyList<TreasureBattleStory> Battles => battles;

        public static TreasureBattleStory FindBattle(string encounterId)
        {
            for (int index = 0; index < battles.Count; index++)
                if (battles[index].EncounterId == encounterId) return battles[index];
            return null;
        }
    }
}
