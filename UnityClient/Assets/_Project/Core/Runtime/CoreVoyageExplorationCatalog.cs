using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public static partial class CoreVoyageEventCatalog
    {
        public static string DiscoveryName(string id)
        {
            switch (id)
            {
                case "reef_dock_chart": return "潮工船坞图";
                case "trade_rescue_seal": return "商队互助凭证";
                case "moon_star_chart": return "月相星图";
                default: return "未知线索";
            }
        }

        public static string DiscoveryTargetName(string id)
        {
            switch (id)
            {
                case "event_reef_hidden_cache": return "潮痕秘藏";
                case "gold_wreck": return "香料沉船";
                case "moon_obelisk": return "月痕方碑";
                default: return "线索地点";
            }
        }

        internal static string RandomEventCategory(string id)
        {
            string expanded = ReefExpandedCategory(id) ?? TradeExpandedCategory(id) ?? MoonExpandedCategory(id);
            if (expanded != null) return expanded;
            switch (id)
            {
                case "event_reef_open_water_ambush": case "event_trade_open_water_ambush":
                case "event_moon_open_water_ambush": case "random_ambush": return "遭遇战";
                case "event_reef_favorable_current": case "event_trade_seasonal_wind": case "random_tailwind": return "航路机遇";
                case "event_reef_sudden_squall": case "event_reef_night_fog":
                case "event_moon_ghost_current": case "event_moon_silver_fog":
                case "event_moon_dead_calm": return "航路险情";
                case "event_reef_freshwater_slick": case "event_reef_shoal_fish_run":
                case "random_floating_supplies": return "海上采集";
                case "event_reef_drifting_crate": case "event_moon_spectral_debris": return "残骸打捞";
                case "event_trade_market_rumor": case "event_moon_relic_whisper": return "线索调查";
                case "event_trade_merchant_distress": case "event_trade_stowaway": return "救援抉择";
                case "event_trade_floating_auction": return "海上交易";
                case "event_trade_customs_patrol": return "交涉查验";
                case "event_moon_falling_starlight": case "event_moon_moon_sleepwalker":
                case "event_moon_curse_surge": return "潮咒异象";
                default: return "船务危机";
            }
        }

        // The three clue destinations belong to the always-present route pool.
        // Enrich choices in place of adding nodes: old seed geography, node IDs,
        // chart claims and checkpoint references retain their identity.
        public static CoreEncounter EnrichExploration(CoreEncounter encounter, string routeId)
        {
            if (encounter == null) return null;
            var choices = new List<CoreEncounterChoice>(encounter.Choices);
            string description = encounter.Description;
            string category = encounter.Category;
            switch (encounter.Id)
            {
                case "event_reef_broken_mast":
                    category = "救援 · 潮工线索";
                    description = "断桅船上的潮工守着一卷船坞图。他愿以秘藏位置和开启方法，换取修好小船的材料。";
                    choices.Add(C("用修船材料换潮工秘藏图", 2,
                        new VoyageEffect { Wood = -3, Cloth = -1, GrantDiscovery = "reef_dock_chart", RevealEncounterId = "event_reef_hidden_cache" }));
                    break;
                case "event_reef_hidden_cache":
                    category = "秘藏 · 潮工机关";
                    description = "潮痕指向旧船坞的封闭储藏室。普通石匣之外，还有一处须按潮工船坞图开启的机关隔舱。";
                    choices.Add(C("按船坞图开启机关隔舱", 2,
                        new VoyageEffect { Wood = -2, AncientMaterial = 2, Hull = 8, SailingBoostHours = 16 }, discovery: "reef_dock_chart"));
                    break;
                case "gold_wreck":
                    category = "打捞 · 商队托付";
                    description = "香料沉船上有普通货箱，也有商队托付的密封药箱。持互助凭证者知道安全货梯的位置，可领取保全酬劳。";
                    choices.Add(C("凭互助印章取回密封药箱", 2,
                        new VoyageEffect { Medicine = 2, Gem = 1, Fame = 8 }, discovery: "trade_rescue_seal"));
                    break;
                case "moon_obelisk":
                    category = "解谜 · 月相星图";
                    description = "方碑星槽缺少排列顺序。月相星图可以解开内匣；也能直接献出古代罗盘，让机关自行归位。";
                    choices.Add(C("依月相星图解开内匣", 3,
                        new VoyageEffect { AncientMaterial = -1, Relic = 1, SurveyBoostHours = 18 }, discovery: "moon_star_chart"));
                    choices.Add(C("嵌入古代罗盘强转星槽", 1,
                        new VoyageEffect { AncientCompass = -1, Relic = 1, Curse = 1 }));
                    break;
                case "common_beach_castaway":
                    if (routeId == Trade)
                    {
                        category = "救援 · 商队线索";
                        description = "沙洲上的商队领航员攥着互助印章。他知道香料沉船药箱的位置，却已没有渡海的补给。";
                        choices.Add(C("接济领航员，获得互助凭证", 2,
                            new VoyageEffect { Food = -4, Water = -4, GrantDiscovery = "trade_rescue_seal", RevealEncounterId = "gold_wreck" }));
                    }
                    else if (routeId == Moon)
                    {
                        description = "沙洲上的落难学者抱着浸湿的观测记录，愿以星图换取返船用的物资。";
                        choices.Add(C("救助学者，抢救月相星图", 3,
                            new VoyageEffect { Medicine = -1, Food = -3, GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk" }));
                    }
                    break;
                case "common_strait_beacon":
                    if (routeId == Moon)
                    {
                        category = "观测 · 月相星图";
                        description = "旧灯按月相闪出三组灯语。花时间抄下完整周期，可以复原解读月痕方碑所需的星图。";
                        choices.Add(C("守候完整灯语，复原星图", 4,
                            new VoyageEffect { GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk" }));
                        choices.Add(C("以航海灯接续缺失的灯语", 1,
                            new VoyageEffect { Lantern = -1, GrantDiscovery = "moon_star_chart", RevealEncounterId = "moon_obelisk", SurveyBoostHours = 8 }));
                    }
                    else
                    {
                        choices.Add(C("添油校准远望灯", 1,
                            new VoyageEffect { Beer = -1, SurveyBoostHours = 16 }, renewable: true));
                    }
                    break;
                case "common_whale_song":
                    category = "追踪 · 洋流";
                    description = "鲸群沿着稳定暖流迁徙。追随它们可以赶路；留在外围观测，则能扩大接下来一段时间的瞭望范围。";
                    choices[0] = C("追随鲸群借用暖流", .5, new VoyageEffect { SailingBoostHours = 12 });
                    choices[1] = C("循鲸声建立远望标记", 2, new VoyageEffect { SurveyBoostHours = 18 });
                    break;
            }
            return new CoreEncounter(encounter.Id, encounter.Name, category, description, encounter.Mandatory,
                encounter.Hidden, encounter.Risk, encounter.Power, choices.ToArray()) { RepeatPolicy = encounter.RepeatPolicy };
        }
    }
}
