using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public static partial class CoreCardUpgradeDesign
    {
        // Each percentage is cumulative from +0; draw entries are cumulative additions.
        // Roles, costs, durations, trigger counts and resource caps retain their base rules.
        // Boarding interpreter values deliberately share one curve for each source card.
        private static void RegisterSpecial(Dictionary<string, CoreCardUpgradeProfile> entries)
        {
            // 36 crew: early specialists and late bloomers have different ceilings.
            Add(entries, "card_special_crew_broken_anchor_navigator_lorne", 2, "133/167", actionPoints: "1/1", note: "前期护航，格挡6/8/10，行动点1/2/2");
            Add(entries, "card_special_crew_bronze_bell_navigator_vick", 2, "133/200", actionPoints: "0/1", note: "固定抽2，强化格挡，第二阶增加行动点");
            Add(entries, "card_special_crew_gray_gull_navigator_evelyn", 3, "140/200/280", actionPoints: "0/1/1", note: "后期领航，格挡5/7/10/14，行动点1/1/2/2");
            Add(entries, "card_special_crew_salt_crown_navigator_ashir", 1, "133", actionPoints: "1", note: "前期重护航，格挡9/12，行动点1/2");
            Add(entries, "card_special_crew_gray_gull_gunner_leisa", 2, "140/180", block: "117/133", note: "装填5/7/9，兼顾格挡");
            Add(entries, "card_special_crew_salt_crown_gunner_rokan", 2, "125/150", block: "120/140", note: "强化每发灼烧，弹数与时长固定");
            Add(entries, "card_special_crew_salt_crown_cook_maz", 2, "133/183", draw: "1/2", note: "高费团队补牌，抽2/3/4");
            Add(entries, "card_special_crew_broken_anchor_carpenter_kia", 2, "125/163", repair: "117/133", note: "护板优先，修复缓慢增长");
            Add(entries, "card_special_crew_gray_gull_cook_pepe", 1, "133", note: "跨阶段即时补给，较早封顶");
            Add(entries, "card_special_crew_salt_crown_carpenter_saf", 1, "140", repair: "167", note: "前期厚甲，格挡10/14");
            Add(entries, "card_special_crew_white_shell_carpenter_atang", 3, "133/167/233", repair: "129/171/229", note: "后期修复，治疗7/9/12/16");
            Add(entries, "card_special_crew_gray_gull_carpenter_barlow", 3, "160/240/340", draw: "0/0/1", repair: "157/243/343", note: "高费重建，末阶获得额外补牌");
            Add(entries, "card_special_crew_broken_anchor_diver_udo", 1, "150", note: "早期近距破坏，伤害10/15");
            Add(entries, "card_special_crew_broken_anchor_gunner_mira", 1, "138", block: "150", note: "早期炮击，伤害13/18");
            Add(entries, "card_special_crew_bronze_bell_diver_duke", 2, "138/188", block: "150/225", note: "攻防并进的近距破坏");
            Add(entries, "card_special_crew_white_shell_diver_kela", 3, "130/170/220", note: "后期潜袭，伤害10/13/17/22");
            Add(entries, "card_special_crew_white_shell_gunner_gallo", 3, "115/138/177", block: "100/150/200", note: "后期炮术，伤害13/15/18/23");
            Add(entries, "card_special_crew_gray_gull_diver_noya", 2, "138/188", block: "140/200", note: "接舷攻防均衡成长");
            Add(entries, "card_special_crew_bronze_bell_cook_basin", 2, "140/200", repair: "150/200", note: "接舷护盾与补给协同");
            Add(entries, "card_special_crew_bronze_bell_doctor_ada", 2, "140/180", repair: "129/171", note: "医疗防护均衡成长");
            Add(entries, "card_special_crew_gray_gull_doctor_moer", 1, "125", repair: "143", note: "前期稳定诊疗，治疗7/10");
            Add(entries, "card_special_crew_white_shell_cook_flora", 3, "150/200/300", repair: "129/171/243", note: "后期疗愈，治疗7/9/12/17，反超莫尔");
            Add(entries, "card_special_crew_salt_crown_doctor_naji", 3, "133/200/267", repair: "124/159/200", note: "高费急救，治疗17/21/27/34");
            Add(entries, "card_special_crew_twilight_tide_cook_lume", 3, "150/225/325", draw: "0/1/1", repair: "150/225/313", note: "后期持续补给，第二阶增加补牌");
            Add(entries, "card_special_crew_twilight_tide_gunner_yilan", 3, "125/150/175", effect: "117/133/167", note: "蓝焰6/7/8/10，附火弹数固定");
            Add(entries, "card_special_crew_broken_anchor_cook_tam", 2, "130/170", note: "有药治疗10/13/17，无药恢复固定");
            Add(entries, "card_special_crew_twilight_tide_navigator_selene", 1, "160", effect: "100", note: "强化格挡，恢复3法力固定");
            Add(entries, "card_special_crew_twilight_tide_doctor_viya", 1, "131", note: "有药治疗13/17，法力费用固定");
            Add(entries, "card_special_crew_twilight_tide_diver_naive", 2, "129/171", effect: "100", note: "强化近距直伤，咒印层数和时长固定");
            Add(entries, "card_special_crew_twilight_tide_carpenter_orlan", 0, "100", note: "零费持续契约，固定机制无空强化");
            Add(entries, "card_special_crew_white_shell_navigator_saina", 3, "125/163/213", effect: "100", actionPoints: "0/1/1", note: "潮盾8/10/13/17，第二阶增加行动点，无木板格挡固定");
            Add(entries, "card_special_crew_white_shell_doctor_mirei", 3, "125/150/200", note: "每跳治疗8/10/12/16，固定两跳");
            Add(entries, "card_special_crew_bronze_bell_carpenter_osser", 2, "140/180", effect: "121/150", note: "蓄雷14/17/21，连锁规则固定");
            Add(entries, "card_special_crew_bronze_bell_gunner_helen", 3, "118/136/164", effect: "125/150/175", note: "强化基础炮伤与每层连锁伤害");
            Add(entries, "card_special_crew_salt_crown_diver_isha", 2, "117/142", note: "双击共用倍率，每击12/14/17，破血第二击16/19/23");
            Add(entries, "card_special_crew_broken_anchor_doctor_hector", 2, "129/171", note: "有药普通与绝境治疗共用倍率，无药恢复固定");

            // 30 rare components: modest early investment, with a few second-rank specialists.
            Add(entries, "card_special_bow_amber_albatross", 2, "117/150", actionPoints: "1/1", note: "稀有轻护航，格挡6/7/9，首阶增加行动点");
            Add(entries, "card_special_bow_guildbreaker_crest", 1, "125", actionPoints: "1", note: "稀有重护航，格挡8/10，行动点1/2");
            Add(entries, "card_special_bow_pearl_siren", 2, "114/143", actionPoints: "1/1", note: "稀有均衡护航，格挡7/8/10，首阶增加行动点");
            Add(entries, "card_special_bow_sunlion_figurehead", 2, "129/157", block: "114/143", note: "稀有攻防舰首，直伤优先");
            Add(entries, "card_special_bow_gilded_mermaid", 1, "125", note: "稀有附火4/5，低于史诗附火成长");
            Add(entries, "card_special_bow_sunwake_crest", 1, "129", note: "稀有高费护舷，格挡14/18");
            Add(entries, "card_special_sail_evening_silk", 2, "117/150", actionPoints: "1/1", note: "稀有渐进护航，格挡6/7/9，首阶增加行动点");
            Add(entries, "card_special_sail_guild_silk", 2, "120/160", draw: "0/1", actionPoints: "1/1", note: "稀有协作帆，首阶行动点，第二阶补牌");
            Add(entries, "card_special_sail_redroute_broadsail", 1, "125", actionPoints: "1", note: "稀有早期炮位防护，格挡8/10，行动点1/2");
            Add(entries, "card_special_sail_sunthread_spinnaker", 2, "140/180", actionPoints: "1/1", note: "稀有追风防护，格挡5/7/9，首阶增加行动点");
            Add(entries, "card_special_sail_whisperveil", 2, "120/160", draw: "0/1", actionPoints: "1/1", note: "稀有静航，首阶行动点，第二阶补牌");
            Add(entries, "card_special_sail_emerald_trade_wing", 1, "125", repair: "150", note: "稀有商路补修，护甲与修复同步");
            Add(entries, "card_special_cannon_chain_repeater", 2, "115/138", block: "150/200", note: "稀有带补牌炮击，伤害13/15/18");
            Add(entries, "card_special_cannon_redwake_repeater", 1, "138", block: "150", note: "稀有早期爆发炮击");
            Add(entries, "card_special_cannon_stormnail_array", 2, "115/138", block: "125/175", note: "稀有防守炮击，格挡4/5/7");
            Add(entries, "card_special_cannon_tradeguard_battery", 1, "133", block: "150", note: "稀有护商炮击，伤害15/20");
            Add(entries, "card_special_cannon_anchorbreaker", 1, "130", block: "200", note: "稀有高费重炮，伤害23/30");
            Add(entries, "card_special_cannon_echochamber", 2, "120/160", block: "117/150", note: "稀有装填5/6/8，装填上限固定");
            Add(entries, "card_special_plating_coralsteel_hide", 2, "133/167", repair: "129/157", note: "稀有修复装甲，治疗7/9/11");
            Add(entries, "card_special_plating_merchants_oath", 1, "125", repair: "150", note: "稀有商誓护甲，前期封顶");
            Add(entries, "card_special_plating_redbanner_wall", 2, "117/150", repair: "150/200", note: "稀有旧旗护舷，修补逐级改善");
            Add(entries, "card_special_plating_suncoin_rampart", 1, "150", note: "稀有防御增益，强化幅度有限");
            Add(entries, "card_special_plating_trade_oathwall", 2, "125/150", repair: "125/175", note: "稀有商队防墙，治疗4/5/7");
            Add(entries, "card_special_plating_gilded_bulwark", 1, "138", repair: "250", note: "稀有高费护壁，格挡13/18、治疗2/5");
            Add(entries, "card_special_instrument_corsair_decoder", 1, "160", actionPoints: "1", note: "稀有旗语机动，格挡5/8，行动点1/2");
            Add(entries, "card_special_instrument_guild_engine", 1, "150", actionPoints: "1", note: "稀有航路机动，格挡6/9，行动点1/2");
            Add(entries, "card_special_instrument_trade_wind_oracle", 2, "120/180", actionPoints: "1/1", note: "稀有信风机动，格挡5/6/9，首阶增加行动点");
            Add(entries, "card_special_instrument_starledger", 1, "140", draw: "1", note: "稀有即时补牌，抽2/3");
            Add(entries, "card_special_instrument_tradeledger_oracle", 1, "150", draw: "1", note: "稀有高费补牌，抽3/4");
            Add(entries, "card_special_instrument_deepwater_abacus", 2, "120/160", repair: "140/180", note: "稀有修补算盘，治疗5/7/9");

            // Fire field component cards retain their source-specific maximum ranks.
            Add(entries, "card_special_bow_nightkraken_tusk", 2, "129/171", effect: "150/200", block: "150/200", note: "史诗火场扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_starshear_rig", 2, "140/180", effect: "200/300", actionPoints: "1/1", note: "史诗火场延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_blueflare_organ", 2, "128/161", effect: "130/170", note: "史诗火场炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_starseal_panels", 2, "133/167", effect: "150/200", note: "史诗火场护舷，基础和场地格挡分别成长，场地免疫时长固定");
            Add(entries, "card_special_instrument_whalestar_receiver", 2, "100", effect: "150/200", note: "史诗火场脉冲，条件收益随强化成长");
            Add(entries, "card_special_bow_dragonwake_ram", 3, "131/169/215", effect: "150/200/250", block: "150/200/250", note: "传说火场扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_thunderphoenix", 3, "140/180/240", effect: "150/200/250", actionPoints: "1/1/1", note: "传说火场延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_dragonbreath_battery", 3, "127/162/208", effect: "133/172/222", note: "传说火场炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_dragonwake_armor", 3, "140/190/250", effect: "140/190/250", note: "传说火场护舷，基础和场地格挡分别成长，场地免疫时长固定");
            Add(entries, "card_special_instrument_celestial_engine", 2, "100", effect: "150/200", draw: "1/2", note: "传说火场统御，扩张4/6/8格，延长2回合固定，抽1/2/3");

            // Arcane field rewards grow without introducing additional resource costs.
            Add(entries, "card_special_bow_voidray_edge", 2, "129/171", effect: "150/200", block: "150/200", note: "史诗折射阵扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_moonweave", 2, "140/180", effect: "200/300", actionPoints: "1/1", note: "史诗折射阵延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_starfire_battery", 2, "128/161", effect: "130/170", note: "史诗折射阵炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_moonsilver_skin", 2, "133/167", effect: "150/200", note: "史诗折射阵护舷，基础和场地格挡分别成长，场地恢复法力同步成长");
            Add(entries, "card_special_instrument_lunar_astrolabe", 2, "100", effect: "150/200", draw: "0/1", note: "史诗折射阵脉冲，条件收益随强化成长");
            Add(entries, "card_special_bow_last_horizon", 3, "131/169/215", effect: "150/200/250", block: "150/200/250", note: "传说折射阵扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_last_free_wind", 2, "140/180", effect: "150/200", draw: "1/2", actionPoints: "1/1", note: "传说折射阵延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_worlds_end_volley", 3, "127/162/208", effect: "133/172/222", note: "传说折射阵炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_worldtree_bark", 3, "140/190/250", effect: "140/190/250", note: "传说折射阵护舷，基础和场地格挡分别成长，场地恢复法力同步成长");
            Add(entries, "card_special_instrument_last_horizon_lens", 2, "100", effect: "150/200", draw: "1/2", note: "传说折射阵统御，扩张4/6/8格，延长2回合固定，抽1/2/3");

            // Curse field expansion, timing and conditional rewards grow independently.
            Add(entries, "card_special_bow_gravecurrent_spur", 2, "129/171", effect: "150/200", block: "150/200", note: "史诗禁域扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_eclipse_shroud", 2, "140/180", effect: "200/300", actionPoints: "1/1", note: "史诗禁域延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_voidbell_mortar", 2, "128/161", effect: "130/170", note: "史诗禁域炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_eclipse_mail", 2, "133/167", effect: "150/200", note: "史诗禁域护舷，基础和场地格挡分别成长，场地免疫时长固定");
            Add(entries, "card_special_instrument_eclipse_orrery", 2, "100", effect: "150/200", draw: "0/1", note: "史诗禁域脉冲，条件收益随强化成长");
            Add(entries, "card_special_bow_crownless_colossus", 3, "131/169/215", effect: "150/200/250", block: "150/200/250", note: "传说禁域扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_crown_of_gales", 3, "140/180/240", effect: "150/200/250", actionPoints: "1/1/1", note: "传说禁域延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_krakens_thunder", 3, "127/162/208", effect: "133/172/222", note: "传说禁域炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_freedom_ark", 3, "140/190/250", effect: "140/190/250", note: "传说禁域护舷，基础和场地格挡分别成长，场地免疫时长固定");
            Add(entries, "card_special_instrument_eye_of_depths", 2, "100", effect: "150/200", draw: "1/2", note: "传说禁域统御，扩张4/6/8格，延长2回合固定，抽1/2/3");

            // Tide components now interact with directional currents.
            Add(entries, "card_special_bow_abyssal_fang", 2, "129/171", effect: "150/200", block: "150/200", note: "史诗定向水流扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_ghostcurrent_canvas", 2, "140/180", effect: "200/300", actionPoints: "1/1", note: "史诗定向水流延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_deepstar_lance", 2, "128/161", effect: "130/170", note: "史诗定向水流炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_ghostreef_carapace", 2, "133/167", effect: "150/200", note: "史诗定向水流护舷，基础和场地格挡分别成长，场地免疫时长固定");
            Add(entries, "card_special_instrument_reefstar_astrolabe", 2, "100", effect: "150/200", note: "史诗定向水流脉冲，条件收益随强化成长");
            Add(entries, "card_special_bow_leviathan_ram", 3, "131/169/215", effect: "150/200/250", block: "150/200/250", note: "传说定向水流扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_tempest_wing", 3, "140/180/240", effect: "150/200/250", actionPoints: "1/2/2", note: "传说定向水流延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_liberty_salute", 3, "127/162/208", effect: "133/172/222", note: "传说定向水流炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_leviathan_scales", 3, "140/190/250", effect: "140/190/250", note: "传说定向水流护舷，基础和场地格挡分别成长，场地免疫时长固定");
            Add(entries, "card_special_instrument_worldcompass", 2, "100", effect: "150/200", draw: "1/2", note: "传说定向水流统御，扩张4/6/8格，延长2回合固定，抽1/2/3");

            // Thunder components now interact with thunder barriers.
            Add(entries, "card_special_bow_blackreef_hammer", 2, "129/171", effect: "150/200", block: "150/200", note: "史诗雷栅扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_aurora_fan", 2, "140/180", effect: "200/300", actionPoints: "1/1", note: "史诗雷栅延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_sunforge_salvo", 2, "128/161", effect: "130/170", note: "史诗雷栅炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_aurora_laminate", 2, "133/167", effect: "150/200", note: "史诗雷栅护舷，基础和场地格挡分别成长，场地免疫时长固定");
            Add(entries, "card_special_instrument_aurora_calculator", 2, "100", effect: "150/200", note: "史诗雷栅脉冲，条件收益随强化成长");
            Add(entries, "card_special_bow_worldsplitter", 3, "131/169/215", effect: "150/200/250", block: "150/200/250", note: "传说雷栅扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_skybreaker_array", 2, "140/180", effect: "150/200", actionPoints: "1/1", note: "传说雷栅延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_stormcrown_broadsides", 3, "127/162/208", effect: "133/172/222", note: "传说雷栅炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_stormark_bulwark", 3, "140/190/250", effect: "140/190/250", note: "传说雷栅护舷，基础和场地格挡分别成长，场地免疫时长固定");
            Add(entries, "card_special_instrument_stormoracle", 2, "100", effect: "150/200", draw: "1/2", note: "传说雷栅统御，扩张4/6/8格，延长2回合固定，抽1/2/3");

            // Blood field rewards do not spend hull or repair supplies.
            Add(entries, "card_special_bow_deepcurrent_fang", 2, "129/171", effect: "150/200", block: "150/200", note: "史诗血誓阵扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_fogshroud_wing", 2, "140/180", effect: "200/300", actionPoints: "1/1", note: "史诗血誓阵延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_cometfall_battery", 2, "128/161", effect: "130/170", note: "史诗血誓阵炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_fogiron_carapace", 2, "133/167", effect: "150/200", note: "史诗血誓阵护舷，基础和场地格挡分别成长，场地修复船体同步成长");
            Add(entries, "card_special_instrument_ghostmap_projector", 2, "100", effect: "150/200", draw: "0/1", note: "史诗血誓阵脉冲，条件收益随强化成长");
            Add(entries, "card_special_bow_freewake_lance", 3, "131/169/215", effect: "150/200/250", block: "150/200/250", note: "传说血誓阵扩张，伤害、格挡与新增格数随强化成长");
            Add(entries, "card_special_sail_thunderreef_wing", 3, "140/180/240", effect: "150/200/250", actionPoints: "1/1/1", note: "传说血誓阵延时，延长回合逐阶增加，保留行动点成长");
            Add(entries, "card_special_cannon_queens_broadside", 3, "127/162/208", effect: "133/172/222", note: "传说血誓阵炮击，基础伤害和场地内目标增伤分别成长");
            Add(entries, "card_special_plating_living_hull", 3, "140/190/250", effect: "140/190/250", note: "传说血誓阵护舷，基础和场地格挡分别成长，场地修复船体同步成长");
            Add(entries, "card_special_instrument_allseeing_sextant", 2, "100", effect: "150/200", draw: "1/2", note: "传说血誓阵统御，扩张4/6/8格，延长2回合固定，抽1/2/3");
        }
    }
}
