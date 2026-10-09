using System;
using System.Collections.Generic;

namespace ShatteredSail.Presentation
{
    public sealed class TreasureStoryChapter
    {
        internal TreasureStoryChapter(string rankId, string rankName, string title,
            string[] pages, string[] imageResources, string[] legendaryIds)
        {
            RankId = rankId;
            RankName = rankName;
            Title = title;
            Pages = Array.AsReadOnly(pages ?? Array.Empty<string>());
            ImageResources = Array.AsReadOnly(imageResources ?? Array.Empty<string>());
            LegendaryIds = Array.AsReadOnly(legendaryIds ?? Array.Empty<string>());
        }

        public string RankId { get; }
        public string RankName { get; }
        public string Title { get; }
        public IReadOnlyList<string> Pages { get; }
        public IReadOnlyList<string> ImageResources { get; }
        public IReadOnlyList<string> LegendaryIds { get; }
    }

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
    /// The three two-page treasure chapters and the five ship-specific pre-battle
    /// readings share stable ids with the core encounter catalog.
    /// </summary>
    public static class CoreTreasureStoryCatalog
    {
        private static readonly IReadOnlyList<TreasureStoryChapter> chapters =
            Array.AsReadOnly(new[]
            {
                new TreasureStoryChapter("weathered", "残旧", "盐封旧誓",
                    new[]
                    {
                        "盐封地图在舱灯下展开，纸角簌簌落盐。旧日志还剩两行能读：铁甲守沉门，红帆守归途。",
                        "雾里，两面旧帆一前一后擦过。炮口没有亮，一枚缺了指针的罗盘却被推到你的船头，铜壳仍湿着。"
                    },
                    new[]
                    {
                        "TreasureHunt/Comics/weathered_page01",
                        "TreasureHunt/Comics/weathered_page02"
                    },
                    new[] { "legendary_pirate_iron_oath", "legendary_pirate_red_widow" }),
                new TreasureStoryChapter("gilded", "鎏金", "王庭账簿",
                    new[]
                    {
                        "鎏金账簿有一笔税始终未销：雷冠收港，余烬焚证。宝库的那一栏没有坐标，只留一道伸出潮线的笔痕。",
                        "闪电照出两艘旧部战舰，它们正争夺同一枚王庭印记。谁先沉下去，线索便会跟谁一起入海。"
                    },
                    new[]
                    {
                        "TreasureHunt/Comics/gilded_page01",
                        "TreasureHunt/Comics/gilded_page02"
                    },
                    new[] { "legendary_pirate_thunder_crown", "legendary_pirate_ember_ark" }),
                new TreasureStoryChapter("moonlit", "月蚀", "月潮偿还",
                    new[]
                    {
                        "月蚀图的潮线正慢慢合拢。沉月王庭外没有守门人，只有一艘幽灵船，仍在替旧船长偿还未尽的归途。",
                        "潮汐镇魂号转过最后一座残塔，没有索要货物。船上传来问话：宝库找到以后，谁还会替海浪记着死者的名字？"
                    },
                    new[]
                    {
                        "TreasureHunt/Comics/moonlit_page01",
                        "TreasureHunt/Comics/moonlit_page02"
                    },
                    new[] { "legendary_pirate_tide_requiem" })
            });

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
                })
            });

        public static IReadOnlyList<TreasureStoryChapter> Chapters => chapters;
        public static IReadOnlyList<TreasureBattleStory> Battles => battles;

        public static TreasureStoryChapter FindChapter(string rankId)
        {
            for (int index = 0; index < chapters.Count; index++)
                if (chapters[index].RankId == rankId) return chapters[index];
            return null;
        }

        public static TreasureBattleStory FindBattle(string encounterId)
        {
            for (int index = 0; index < battles.Count; index++)
                if (battles[index].EncounterId == encounterId) return battles[index];
            return null;
        }
    }
}
