using System;
using System.Collections.Generic;
using A = ShatteredSail.Core.Gameplay.NavalBattleCardAction;
using S = ShatteredSail.Core.Gameplay.NavalBattleStages;
using M = ShatteredSail.Core.Gameplay.CoreSchoolMechanic;
using U = ShatteredSail.Core.Gameplay.CoreSchoolUpgradeFields;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Current source-card rules. Identity, illustrations and historical slots stay stable.</summary>
    public static class CoreSpecialCardRedesign
    {
        public static IReadOnlyList<CoreBattleCard> ApplyAll(CoreBattleCard[] cards)
        {
            for (int i = 0; i < cards.Length; i++) cards[i] = Apply(cards[i]);
            return Array.AsReadOnly(cards);
        }

        public static CoreBattleCard Apply(CoreBattleCard card)
        {
            if (card == null || !card.Id.StartsWith("card_special_", StringComparison.Ordinal)) return card;
            if (card.SchoolId.Length == 0) return Neutral(card);
            string key = card.ArtId.Substring(card.ArtId.LastIndexOf('_') + 1);
            if (card.RequiredComponent.Length > 0 && key.Length == 3 &&
                (key[0] == 'e' || key[0] == 'l') && key[1] == '0' && key[2] >= '1' && key[2] <= '5')
                return CoreComponentFieldCards.Apply(card, key);
            if (card.RequiredCrew.Length > 0 && card.AllowedStages == S.Boarding) return BoardingCrew(card);
            bool legendary = key.StartsWith("l", StringComparison.Ordinal);
            if (key == "l05") return Doctrine(card);
            int slot = key.Length == 3 ? key[2] - '0' : 0;
            if (card.RequiredCrew.Length > 0) return SchoolCrew(card, key);
            switch (card.SchoolId)
            {
                case "fire":
                    switch (slot)
                    {
                        case 1: return School(card, legendary ? M.FireExtraTick : M.FireIgniteOrExtend, legendary ? 2 : 1,
                            damage: legendary ? 13 : 7, block: legendary ? 4 : 2, power: legendary ? 5 : 4, secondary: 1, duration: 2, powder: legendary ? 2 : 1, action: A.Sabotage);
                        case 2: return School(card, legendary ? M.FireScorch : M.FireExtend, 1, block: legendary ? 7 : 5,
                            draw: 1, power: legendary ? 4 : 1, secondary: 2, movement: legendary ? 20 : -20, powder: 1);
                        case 3: return School(card, legendary ? M.FireExtraTick : M.FireIgnite, legendary ? 3 : 2,
                            damage: legendary ? 29 : 19, power: legendary ? 6 : 5, duration: 2, powder: legendary ? 3 : 2, action: A.Cannon);
                        case 4: return School(card, legendary ? M.FireScorch : M.FireNaturalGuard, legendary ? 2 : 1,
                            block: legendary ? 10 : 7, power: legendary ? 5 : 4, secondary: 3, duration: 2, powder: 1);
                        case 5: return School(card, M.FireLoad, 1, block: 3, draw: 1, power: 5, secondary: 2, duration: 2, limit: 4, powder: 1);
                    }
                    break;
                case "arcane":
                    switch (slot)
                    {
                        case 1: return School(card, M.ArcaneRelease, legendary ? 2 : 1, damage: legendary ? 14 : 7,
                            block: legendary ? 4 : 2, power: legendary ? 17 : 9, threshold: legendary ? 2 : 1, action: A.Sabotage);
                        case 2: return School(card, legendary ? M.ArcaneEcho : M.ArcaneTemporary, 1,
                            block: legendary ? 5 : 4, draw: legendary ? 0 : 1, power: 2, threshold: legendary ? 1 : 0, movement: legendary ? 20 : -20);
                        case 3: return School(card, M.ArcaneRelease, legendary ? 3 : 2, damage: legendary ? 28 : 18,
                            power: legendary ? 22 : 13, threshold: legendary ? 2 : 1, powder: 1, action: A.Cannon);
                        case 4: return School(card, legendary ? M.ArcaneMend : M.ArcaneWard, legendary ? 2 : 1,
                            block: legendary ? 8 : 5, repair: legendary ? 5 : 0, power: legendary ? 9 : 10, threshold: 1);
                        case 5: return School(card, M.ArcaneCharge, 1, block: 4, draw: 1, power: 3, upgrades: U.None);
                    }
                    break;
                case "curse":
                    switch (slot)
                    {
                        case 1: return School(card, legendary ? M.CurseDetonate : M.CurseMark, legendary ? 2 : 1,
                            damage: legendary ? 13 : 7, block: 3, power: legendary ? 5 : 2, secondary: 2, duration: 2, action: A.Sabotage);
                        case 2: return School(card, legendary ? M.CurseWard : M.CurseMark, 1, block: legendary ? 5 : 4,
                            draw: 1, power: legendary ? 3 : 2, secondary: 1, threshold: legendary ? 1 : 0, duration: 2, movement: legendary ? 20 : -20);
                        case 3: return School(card, legendary ? M.CurseDetonate : M.CurseMark, legendary ? 3 : 2,
                            damage: legendary ? 27 : 18, power: legendary ? 6 : 3, secondary: 2, duration: 2, powder: 1, action: A.Cannon);
                        case 4: return School(card, M.CurseWard, legendary ? 2 : 1, block: legendary ? 9 : 5,
                            draw: legendary ? 1 : 0, power: legendary ? 4 : 3, threshold: 1);
                        case 5: return School(card, M.CurseMark, 1, block: 3, draw: 1, power: 3, secondary: 2, duration: 3);
                    }
                    break;
                case "tide":
                    switch (slot)
                    {
                        case 1: return School(card, M.TideRiposte, legendary ? 2 : 1, damage: legendary ? 13 : 7,
                            block: legendary ? 4 : 2, power: legendary ? 175 : 150, threshold: 1, action: A.Sabotage, upgrades: U.None);
                        case 2: return School(card, M.TideWard, legendary ? 2 : 1, block: legendary ? 13 : 7,
                            draw: 1, movement: legendary ? 20 : -20, wood: 1, fallbackBlock: 2, upgrades: U.None);
                        case 3: return School(card, M.TideRiposte, legendary ? 3 : 2, damage: legendary ? 24 : 17,
                            power: legendary ? 175 : 150, threshold: 1, powder: 1, action: A.Cannon, upgrades: U.None);
                        case 4: return School(card, M.TideWard, legendary ? 2 : 1, block: legendary ? 15 : 8,
                            draw: legendary ? 1 : 0, wood: 1, fallbackBlock: 2, upgrades: U.None);
                        case 5: return School(card, M.TideCleanse, 1, draw: 1, power: 12, threshold: 3);
                    }
                    break;
                case "thunder":
                    switch (slot)
                    {
                        case 1: return School(card, legendary ? M.ThunderFinish : M.ThunderChain, legendary ? 2 : 1,
                            damage: legendary ? 13 : 7, block: 2, power: legendary ? 9 : 4, action: A.Sabotage);
                        case 2: return School(card, legendary ? M.ThunderReady : M.ThunderEcho, legendary ? 1 : 0,
                            block: legendary ? 6 : 2, power: legendary ? 13 : 2, threshold: legendary ? 0 : 2, movement: legendary ? 20 : 10);
                        case 3: return School(card, legendary ? M.ThunderFinish : M.ThunderChain, legendary ? 3 : 2,
                            damage: legendary ? 28 : 21, power: legendary ? 7 : 4, powder: 1, action: A.Cannon);
                        case 4: return School(card, M.ThunderReady, legendary ? 2 : 1, block: legendary ? 10 : 6,
                            draw: 1, power: legendary ? 20 : 10);
                        case 5: return School(card, M.ThunderReady, 1, block: 3, draw: 2, power: 7);
                    }
                    break;
                case "blood":
                    switch (slot)
                    {
                        case 1: return School(card, legendary ? M.BloodExecute : M.BloodDrain, legendary ? 2 : 1,
                            damage: legendary ? 15 : 9, block: 2, power: legendary ? 15 : 60, threshold: legendary ? 40 : 0, limit: 6, action: A.Sabotage,
                            upgrades: legendary ? U.Power : U.None);
                        case 2: return School(card, M.BloodWard, 1, block: legendary ? 7 : 5, draw: legendary ? 0 : 1,
                            power: legendary ? 12 : 9, threshold: legendary ? 3 : 2, movement: legendary ? 20 : -20);
                        case 3: return School(card, legendary ? M.BloodDrain : M.BloodRisk, legendary ? 3 : 2,
                            damage: legendary ? 32 : 21, power: legendary ? 40 : 14, threshold: legendary ? 0 : 4, limit: 12, powder: 1, action: A.Cannon,
                            upgrades: legendary ? U.None : U.Power);
                        case 4: return School(card, legendary ? M.BloodMend : M.BloodWard, legendary ? 2 : 1,
                            block: legendary ? 10 : 6, draw: legendary ? 0 : 1, power: legendary ? 30 : 9, threshold: legendary ? 0 : 3, limit: 12,
                            upgrades: legendary ? U.None : U.Power);
                        case 5: return School(card, M.BloodMend, 1, block: 3, draw: 1, power: 30, limit: 8, upgrades: U.None);
                    }
                    break;
            }
            throw new InvalidOperationException("Missing special source design: " + card.Id);
        }

        private static CoreBattleCard SchoolCrew(CoreBattleCard c, string key)
        {
            switch (c.SchoolId)
            {
                case "fire": return School(c, M.FireLoad, 1, block: 4, draw: 1, power: 6, secondary: 3, duration: 2, limit: 4, powder: 1, stages: S.Ship);
                case "arcane": return School(c, M.ArcaneCharge, 1, block: 5, draw: 1, power: 3, stages: S.Ship, upgrades: U.None);
                case "curse": return School(c, M.CurseMark, 1, damage: 7, draw: 1, power: 2, secondary: 2, duration: 3, action: A.Sabotage);
                case "tide": return School(c, M.TideWard, 1, block: 8, draw: 1, movement: 20, wood: 1, fallbackBlock: 2, upgrades: U.None);
                case "thunder": return key == "c01"
                    ? School(c, M.ThunderChain, 2, damage: 22, draw: 1, power: 4, powder: 1, action: A.Cannon)
                    : School(c, M.ThunderReady, 1, block: 5, draw: 1, power: 14, stages: S.Ship);
                default: throw new InvalidOperationException("Missing crew design: " + c.Id);
            }
        }

        private static CoreBattleCard BoardingCrew(CoreBattleCard c)
        {
            string key = c.SchoolId == "fire" ? "fire_05" : c.SchoolId == "arcane" ? "arcane_05" :
                c.SchoolId == "curse" ? "curse_04" : c.SchoolId == "tide" ? "tide_12" :
                c.RequiredCrew.Contains("_doctor_") ? "blood_09" : "blood_07";
            CoreBattleCard b = CoreBoardingCardCatalog.FindByKey(key);
            return new CoreBattleCard(c.Id, c.Name, b.Type, Math.Max(0, b.Energy - 1), b.Damage, b.Block, b.Repair,
                b.Draw, b.PowderCost, b.DescriptionTemplate, S.Boarding, b.Action, c.RequiredCrew, c.ArtId,
                b.CannonballCost, c.RequiredComponent, schoolId: c.SchoolId, boardingSpec: b.BoardingSpec);
        }

        private static CoreBattleCard Doctrine(CoreBattleCard c)
        {
            M m; string text;
            switch (c.SchoolId)
            {
                case "fire": m = M.SpecialFireDoctrine; text = "每回合首张其他火焰牌成功后，施加 3 层燃烧"; break;
                case "arcane": m = M.SpecialArcaneDoctrine; text = "每回合首张其他秘术牌成功后，恢复 1 法力，不超过上限"; break;
                case "curse": m = M.SpecialCurseDoctrine; text = "每回合首张其他诅咒牌成功后，施加 1 层咒印，最多增至 6 层"; break;
                case "tide": m = M.SpecialTideDoctrine; text = "每回合首张其他潮汐牌成功后，获得 3 潮势，上限 20"; break;
                case "thunder": m = M.SpecialThunderDoctrine; text = "每回合首张其他雷霆攻击成功后，额外获得 1 连锁，不超过上限"; break;
                default: m = M.SpecialBloodDoctrine; text = "每回合首张其他血契牌免除生命费用，保留完整效果"; break;
            }
            return Create(c, 2, 0, 0, 0, 1, 0, S.Naval, A.Standard,
                "抽 {draw} 张；激活本场海战专属规则：" + text + "；同类不叠加，接舷或战斗结束清除。",
                new CoreSchoolCardEffect(m, upgrades: U.None));
        }

        private static CoreBattleCard School(CoreBattleCard c, M mechanic, int energy, int damage = 0, int block = 0,
            int repair = 0, int draw = 0, int power = 0, int secondary = 0, int threshold = 0, int duration = 0,
            int limit = 0, int movement = 0, int powder = 0, int wood = 0, int fallbackBlock = 0,
            A action = A.Standard, S stages = S.Naval, U upgrades = U.Power)
        {
            var e = new CoreSchoolCardEffect(mechanic, power, secondary, threshold, duration, limit, movement,
                upgrades, fallbackBlock: fallbackBlock, woodCost: wood);
            string rule;
            switch (mechanic)
            {
                case M.FireLoad: rule = "获得 {schoolSecondary} 发附火，上限 {schoolLimit}；命中后施加 {schoolPower} 层燃烧"; break;
                case M.FireIgnite: rule = "实际造成直伤后，施加 {schoolPower} 层燃烧"; break;
                case M.FireIgniteOrExtend: rule = "实际造成直伤后，若目标未燃烧，施加 {schoolPower} 层燃烧；否则增加 {schoolSecondary} 层燃烧"; break;
                case M.FireExtend: rule = "目标已燃烧时，增加 {schoolPower} 层燃烧"; break;
                case M.FireExtraTick: rule = "实际造成直伤后，施加 {schoolPower} 层燃烧；目标出牌前已燃烧时，额外结算一次燃烧，每回合一次"; break;
                case M.FireNaturalGuard: rule = "接下来 {schoolDuration} 回合，敌方自然燃烧结算时获得 {schoolPower} 格挡"; break;
                case M.FireScorch: rule = "接下来 {schoolSecondary} 次对燃烧敌舰造成有效直伤时，额外造成 {schoolPower} 伤害"; break;
                case M.ArcaneCharge: rule = "恢复 {schoolPower} 法力，不超过上限"; break;
                case M.ArcaneTemporary: rule = "获得 {schoolPower} 临时法力，本回合结束清除"; break;
                case M.ArcaneRelease: rule = "消耗 {schoolThreshold} 法力，额外伤害 +{schoolPower}"; break;
                case M.ArcaneWard: rule = "消耗 {schoolThreshold} 法力，额外格挡 +{schoolPower}"; break;
                case M.ArcaneMend: rule = "消耗 {schoolThreshold} 法力，额外恢复 {schoolPower} 船体"; break;
                case M.ArcaneEcho: rule = "消耗 {schoolThreshold} 法力，抽 {schoolPower} 张"; break;
                case M.CurseMark: rule = "施加 {schoolPower} 层咒印"; break;
                case M.CurseDetonate: rule = "需要已有咒印；下次结算咒印伤害时，额外扣除 {schoolPower} 点生命值，不消耗咒印"; break;
                case M.CurseWard: rule = "消耗 {schoolThreshold} 咒印；按消耗前每层咒印获得 {schoolPower} 额外格挡"; break;
                case M.TideWard: rule = "消耗 {wood} 木板；本牌格挡实际吸收敌方攻击时等量获得潮势，上限 20；无木板时仅获得 2 普通格挡"; break;
                case M.TideRiposte: rule = "需至少 {schoolThreshold} 潮势；消耗全部潮势，按 {schoolPower}% 追加伤害"; break;
                case M.TideCleanse: rule = "消耗 {schoolThreshold} 潮势；清除敌舰装填，获得 {schoolPower} 格挡，不蓄积潮势"; break;
                case M.ThunderChain: rule = "每层已有连锁追加 {schoolPower} 伤害；本次攻击后连锁 +1，上限 6，回合结束清空"; break;
                case M.ThunderFinish: rule = "消耗全部已有连锁，每层追加 {schoolPower} 伤害；本次攻击后连锁 +1，上限 6"; break;
                case M.ThunderReady: rule = "下一次雷霆攻击伤害 +{schoolPower}，同类取最高，不增加连锁"; break;
                case M.ThunderEcho: rule = "已有至少 {schoolThreshold} 连锁时抽 {schoolPower} 张，每回合一次，不增加连锁"; break;
                case M.BloodRisk: rule = "支付 {schoolThreshold} 船体，额外伤害 +{schoolPower}，支付后至少留 1 点"; break;
                case M.BloodWard: rule = "支付 {schoolThreshold} 船体，额外格挡 +{schoolPower}，支付后至少留 1 点"; break;
                case M.BloodDrain: rule = "修复本次有效直伤的 {schoolPower}%，上限 {schoolLimit} 船体"; break;
                case M.BloodMend: rule = "恢复已失生命的 {schoolPower}%，上限 {schoolLimit}"; break;
                case M.BloodExecute: rule = "敌舰船体不高于 {schoolThreshold}% 时，额外伤害 +{schoolPower}"; break;
                default: throw new InvalidOperationException("Missing effect text: " + mechanic);
            }
            string movementText = movement == 0 ? string.Empty : movement > 0
                ? "沿船头前进 {movement} 米，再转为平行；" : "先转向背敌，再远离 {movement} 米，取消冲撞；";
            // Tide guard expands into a free shield plus a wood-dependent bonus.
            // Keep its independent draw and movement before that entire effect group.
            string text = mechanic == M.TideWard
                ? BasicText(stages, action, damage, 0, repair, draw, powder) + movementText + "获得 {block} 格挡；" + rule + "。"
                : BasicText(stages, action, damage, block, repair, draw, powder) + rule + "。" + movementText.TrimEnd('；') + (movement == 0 ? string.Empty : "。");
            return Create(c, energy, damage, block, repair, draw, powder, stages, action, text, e);
        }

        private static CoreBattleCard Neutral(CoreBattleCard c)
        {
            bool component = c.RequiredComponent.Length > 0;
            string source = component ? c.RequiredComponent : c.RequiredCrew;
            S stage = component ? S.Naval : c.AllowedStages;
            int damage = c.Damage, block = c.Block, repair = c.Repair, draw = c.Draw;
            int energy = c.Energy; A action = c.Action;
            // Each role keeps its authored action and gains a complementary tactical payoff.
            if (action == A.Cannon) { damage += 3; block += 2; }
            else if (action == A.Sabotage) { damage += 3; draw = Math.Max(1, draw); }
            else if (source.Contains("navigator") || source.StartsWith("sail_", StringComparison.Ordinal))
            { block += 3; draw = Math.Max(1, draw); }
            else if (source.Contains("carpenter") || source.StartsWith("plating_", StringComparison.Ordinal))
            { block += 3; repair += 2; }
            else if (source.Contains("doctor")) { repair += 3; block += 3; }
            else if (source.Contains("cook")) { block += 2; repair += 2; draw = Math.Max(1, draw); }
            else if (source.StartsWith("instrument_", StringComparison.Ordinal)) { block += 3; draw = Math.Max(1, draw); }
            else { damage += damage > 0 ? 3 : 0; block += 4; draw = Math.Max(1, draw); }
            // Regional pairs previously shared all rules; their new jobs are deliberately different.
            // Legacy maneuver actions all grant one action point now. Keep the navigator
            // identities distinct through their actual card payoffs, not retired travel distances.
            if (source == "crew_bronze_bell_navigator_vick") { block = 3; draw = 2; }
            if (source == "crew_salt_crown_navigator_ashir") { block = 9; draw = 0; }
            if (source == "crew_salt_crown_carpenter_saf") { block = 10; repair = 3; draw = 0; }
            if (source == "crew_gray_gull_carpenter_barlow") { block = 5; repair = 7; draw = 1; energy = 2; }
            if (source == "crew_salt_crown_cook_maz") { block = 6; repair = 0; draw = 2; energy = 2; }
            if (source == "crew_gray_gull_cook_pepe") { block = 6; repair = 3; draw = 1; }
            if (source == "crew_twilight_tide_cook_lume") { block = 4; repair = 8; draw = 1; energy = 2; }
            int powder = action == A.Cannon || action == A.Load || action == A.PrecisionLoad ? 1 : c.PowderCost;
            string text = BasicText(stage, action, damage, block, repair, draw, powder);
            switch (action)
            {
                case A.Load: case A.PrecisionLoad: text += "下次炮击额外伤害 +{strategyAmount}，最多 +{strategyLimit}；开炮后清空。"; break;
                case A.Polish: text += "本场船体防御牌额外格挡 +{strategyAmount}，最多 +{strategyLimit}。"; break;
                case A.FireRounds: text += "获得 {strategyAmount} 发烈焰强化，上限 {strategyLimit}；炮击造成有效直伤后，施加 {burnDamage} 层燃烧。"; break;
                case A.Turn: case A.Circle: case A.Approach: case A.Retreat: case A.Parallel: case A.Navigator:
                    text += "获得 {navalActionPoints} 点行动点，用于海图移动或转向。"; break;
            }
            return Create(c, energy, damage, block, repair, draw, powder, stage, action, text);
        }

        private static string BasicText(S stage, A action, int damage, int block, int repair, int draw, int powder)
        {
            string health = stage == S.Boarding ? "船员生命" : stage == S.All ? "当前阶段生命" : "船体";
            string range = action == A.Cannon ? "范围 1-4 格：" : action == A.Sabotage ? "范围 1-2 格：" : string.Empty;
            var parts = new List<string>();
            if (damage > 0) parts.Add("造成 {damage} 点" + health + "伤害");
            if (block > 0) parts.Add("获得 {block} 格挡");
            if (draw > 0) parts.Add("抽 {draw} 张");
            // Repair expands into stage-specific base healing and optional supply bonuses.
            if (repair > 0) parts.Add("恢复 {repair} 点" + health);
            if (powder > 0) parts.Add("消耗 {powder} 火药");
            if (action == A.Cannon) parts.Add("消耗 {cannonballs} 炮弹");
            if (action == A.Sabotage && powder == 0) parts.Add("无需弹药");
            return parts.Count == 0 ? string.Empty : range + string.Join("；", parts) + "；";
        }

        private static CoreBattleCard Create(CoreBattleCard c, int energy, int damage, int block, int repair, int draw,
            int powder, S stages, A action, string text, CoreSchoolCardEffect effect = null) =>
            new CoreBattleCard(c.Id, c.Name, action == A.Cannon ? "炮击" : action == A.Sabotage ? "突袭" :
                (effect != null && effect.Movement != 0) || action == A.Turn || action == A.Circle || action == A.Approach ||
                action == A.Retreat || action == A.Parallel || action == A.Navigator ? "机动" : repair > 0 ? "维修" : block > 0 ? "防御" : "策略",
                energy, damage, block, repair, draw, powder, text, stages, action, c.RequiredCrew, c.ArtId,
                action == A.Cannon ? 1 : 0, c.RequiredComponent, schoolId: c.SchoolId, schoolEffect: effect);
    }
}
