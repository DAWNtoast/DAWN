using System;
using System.Collections.Generic;
using System.Text;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void BuildBattleSchoolHud(RectTransform root, CoreBattleState battle)
        {
            // The left outer margin stays clear of both ships and boarding parties.
            RectTransform panel = CreateFixedPanel("Battle School Status Panel", root,
                new Vector2(190f, battle.Stage == NavalBattleStage.Boarding ? 330f : 248f), new Vector2(-780f, 164f), PanelRaised);
            ApplyRetreatBattleWoodBoard(panel.GetComponent<Image>());
            panel.GetComponent<Image>().raycastTarget = false;
            Text text = CreateText("Battle School Status", panel, new Vector2(174f, battle.Stage == NavalBattleStage.Boarding ? 314f : 232f), Vector2.zero,
                14, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 13; text.resizeTextMaxSize = 14;
            RefreshBattleSchoolHud(root, battle);
        }

        private void RefreshBattleSchoolHud(RectTransform root, CoreBattleState battle)
        {
            Transform panel = root.Find("Battle School Status Panel");
            if (panel == null) return;
            bool visible = battle.Stage == NavalBattleStage.Boarding || BattleHasSchoolCards(battle);
            panel.gameObject.SetActive(visible);
            Text label = panel.GetComponentInChildren<Text>(true);
            if (label != null) label.text = CoreBattleStatusPresentation.BuildResourceSummary(session);
        }

        private static bool BattleHasSchoolCards(CoreBattleState battle)
        {
            if (battle == null) return false;
            foreach (IEnumerable<int> pile in new[] { battle.Hand, battle.DrawPile, battle.DiscardPile })
                foreach (int index in pile)
                    if (index >= 0 && index < CoreVoyageSession.CardLibrary.Count &&
                        !string.IsNullOrEmpty(CoreVoyageSession.CardLibrary[index].SchoolId)) return true;
            CoreSchoolBattleState s = battle.Schools;
            return s.Mana > 0 || s.TemporaryMana > 0 || s.ManaLimit > 3 || s.CurseMarks > 0 ||
                s.TidePower > 0 || s.TidePaidBlock > 0 || s.ThunderChain > 0 || s.ThunderReadyDamage > 0 ||
                s.NaturalFireGuardTurns > 0 || s.BoardingBurn > 0 || s.BoardingCover > 0 || s.ScorchCharges > 0;
        }

        private static string BattleSchoolStatusText(CoreBattleState battle)
        {
            if (battle == null) return string.Empty;
            CoreSchoolBattleState s = battle.Schools;
            var text = new StringBuilder("体系状态");
            text.Append("\n法力 ").Append(s.Mana).Append('/').Append(s.ManaLimit);
            if (s.TemporaryMana > 0) text.Append(" · 临时 +").Append(s.TemporaryMana);
            text.Append("\n本回合雷链 ").Append(s.ThunderChain).Append('/').Append(s.ThunderLimit);
            if (s.ThunderReadyDamage > 0) text.Append("\n下次雷击 +").Append(s.ThunderReadyDamage);
            if (s.CurseMarks > 0)
            {
                text.Append("\n敌附咒 ").Append(s.CurseMarks).Append(" 层 · ").Append(s.CurseTurns).Append(" 回合");
                int tick = (int)Math.Min(int.MaxValue, (long)s.CurseMarks *
                    ((long)s.CurseDamagePerMark + s.CurseNextTickBonus));
                text.Append("\n下次敌回合末扣血 ").Append(tick);
            }
            if (s.CurseGuard > 0) text.Append("\n本回合咒盾 ").Append(s.CurseGuard);
            if (battle.EnemyBurnTurns > 0)
                text.Append("\n敌燃烧 ").Append(battle.EnemyBurnDamage).Append(" × ").Append(battle.EnemyBurnTurns).Append(" 回合");
            text.Append("\n潮势 ").Append(s.TidePower).Append('/').Append(s.TideLimit);
            if (s.TidePaidBlock > 0) text.Append(" · 付木护盾 ").Append(s.TidePaidBlock);
            if (s.NaturalFireGuardTurns > 0)
                text.Append("\n焰盾 +").Append(s.NaturalFireGuard).Append(" · ").Append(s.NaturalFireGuardTurns).Append(" 回合");
            if (s.ScorchCharges > 0) text.Append("\n灼痕 +").Append(s.ScorchDamage).Append(" · ").Append(s.ScorchCharges).Append(" 次");
            if (s.BoardingBurn > 0) text.Append("\n入舷火种 ").Append(s.BoardingBurn).Append(" × ").Append(s.BoardingBurnTurns);
            if (s.BoardingCover > 0) text.Append("\n入舷掩护 ").Append(s.BoardingCover);
            if (s.OverflowAmount > 0) text.Append("\n余烬装填 ").Append(s.OverflowContribution).Append('/').Append(s.OverflowOwnLimit);
            CoreNavalFieldShipState field = battle.NavalFields.Player;
            CoreNavalExpansionBattleState naval = battle.NavalExpansion;
            if (field.FireImmunityCharges > 0) text.Append("\n隔焰免伤 ").Append(field.FireImmunityCharges).Append(" 次");
            if (field.ArcanePiercing > 0) text.Append("\n下张秘术穿盾 ").Append(field.ArcanePiercing);
            if (field.BloodAttackBonus > 0) text.Append("\n下张攻击首段 +").Append(field.BloodAttackBonus);
            if (field.AnchorCharges > 0) text.Append("\n定锚 ").Append(field.AnchorCharges).Append(" 次");
            if (naval.OilPlayerEndsRemaining > 0) text.Append("\n敌舰浸油 · 待火焰命中");
            if (naval.RuneClamp) text.Append("\n敌下次获盾 −6");
            if (naval.LoadSeal) text.Append("\n敌下次装填 −6");
            if (naval.PulseReduction > 0) text.Append("\n敌下次攻击 −").Append(naval.PulseReduction).Append("（最多减半）");
            if (battle.NavalFields.Enemy.MovementSlow > 0) text.Append("\n敌下次航行 −1 格");
            if (naval.Precharge) text.Append("\n下次雷攻额外 +2 链");
            if (naval.GroundingReady && !naval.GroundingUsed) text.Append("\n接地回流 · 待雷栅命中");
            if (naval.EnemyHealingHalved) text.Append("\n敌舰止愈 · 恢复减半");
            text.Append("\n<size=12>临时法力、未用潮势在结束回合时清空</size>");
            return text.ToString();
        }

        private string SchoolCardKeywordHelp(CoreBattleCard card)
        {
            if (card?.BoardingSpec != null) return BoardingCardKeywordHelp(card);
            if (CoreNavalSchoolExpansionCatalog.FindSpec(card?.Id) != null) return NavalExpansionKeywordHelp(card);
            if (card?.SchoolEffect == null || session?.Battle == null) return string.Empty;
            CoreBattleState battle = session.Battle;
            CoreSchoolBattleState s = battle.Schools;
            var text = new StringBuilder();
            switch (card.SchoolId)
            {
                case "fire": text.Append("<color=#6B251C>燃烧</color>\n火焰攻击与护盾支付卡面火药；炮击另付炮弹。\n烧伤在敌方行动前结算，不重复扣火药，最多持续 3 回合。\n当前：")
                    .Append(battle.EnemyBurnDamage).Append(" 点 × ").Append(battle.EnemyBurnTurns).Append(" 回合。\n额外结算与满额延烧奖励每回合各限一次。"); break;
                case "arcane": text.Append("<color=#6B251C>法力</color>\n当前 ").Append(s.Mana).Append('/').Append(s.ManaLimit)
                    .Append("，临时 ").Append(s.TemporaryMana).Append("，合计可用 ").Append(s.AvailableMana)
                    .Append("。\n初始法力 0、上限 3；提升上限不会恢复法力。\n优先支付临时法力，未用临时法力在结束回合时清空。\n法力不足的牌不能使用。"); break;
                case "curse": text.Append("<color=#6B251C>附咒</color>\n当前 ").Append(s.CurseMarks).Append(" 层，剩余 ").Append(s.CurseTurns)
                    .Append(" 回合。\n附咒不造成直接伤害，敌方回合末按层数扣血后递减持续时间。\n引爆只增强下一次末结算，不清空附咒。\n咒盾按支付前层数计算，无数值上限，不保留到下一回合；强咒盾另耗咒印。"); break;
                case "tide": text.Append("<color=#6B251C>潮势</color>\n当前 ").Append(s.TidePower).Append('/').Append(s.TideLimit)
                    .Append("。\n付木板形成的护盾实际吸收伤害才产生潮势；普通格挡、无木板保底不产潮势。\n反击需有潮势并消耗全部，治疗与破蓄按卡面支付。\n未用潮势在下次结束回合时清空。"); break;
                case "thunder": text.Append("<color=#6B251C>连锁</color>\n本回合 ").Append(s.ThunderChain).Append('/').Append(s.ThunderLimit)
                    .Append("。\n仅雷霆攻击推进 1 连锁，普通攻击、抽牌和回费不推进。\n伤害按已有连锁线性增加；终结先消耗全部，本次攻击再产生 1 层。\n新回合归零，雷鸣回响每回合只额外抽牌一次；雷牌不提供格挡。"); break;
                case "blood": text.Append("<color=#6B251C>血契</color>\n支付生命会直接扣除当前阶段生命，必须至少保留 1 点。\n吸血按实际造成的直接伤害计算，不能从格挡中吸取生命。"); break;
            }
            CoreSchoolMechanic mechanic = card.SchoolEffect.Mechanic;
            if (card.SchoolId == "fire" && (card.SchoolEffect.FallbackDamage > 0 || card.SchoolEffect.FallbackBlock > 0))
            {
                text.Append("\n\n<color=#6B251C>缺粉保底</color>\n不随强化增加；缺粉时关闭该牌全部火焰效果。");
                if (session.Cargo.Powder < card.PowderCost) text.Append("\n当前火药不足，本次使用保底。");
            }
            if (mechanic == CoreSchoolMechanic.BloodRisk || mechanic == CoreSchoolMechanic.BloodWard)
            {
                int health = battle.Stage == NavalBattleStage.Boarding ? battle.PlayerBoardingHealth : session.Hull;
                int payment = card.SchoolEffect.Threshold;
                text.Append("\n\n<color=#6B251C>生命支付预告</color>\n当前 ").Append(health).Append("；支付 ").Append(payment)
                    .Append("；支付后 ").Append(Math.Max(0, health - payment)).Append("。");
                if (health <= payment) text.Append("\n生命不足，不能出牌。");
            }
            CoreBattleCardEffects effects = session.GetBattleCardEffects(card, battle.Stage);
            text.Append("\n\n<color=#6B251C>当前结算</color>\n伤害 ").Append(session.GetBattleCardDamage(card, battle.Stage))
                .Append(" · 格挡 ").Append(effects.Block).Append(" · 实际治疗 ").Append(effects.ActualRepair).Append("。");
            if (battle.Stage == NavalBattleStage.Boarding)
                text.Append("\n卡牌效果不受人数影响。");
            return text.ToString();
        }

        private string NavalExpansionKeywordHelp(CoreBattleCard card)
        {
            CoreNavalExpansionCardSpec spec = CoreNavalSchoolExpansionCatalog.FindSpec(card.Id);
            var text = new StringBuilder(card.Description);
            text.Append("\n\n<color=#6B251C>选区与支付</color>\n场地与范围的每一格均须在横纵相加 4 格内。选区、方向和资源数量确认后才支付；取消不扣资源、不计出牌。相同 ID 的强化版与副本共享次数限制。");
            if (spec.IsField || spec.Key == "F05" || spec.Key == "F06" || spec.Key == "L06")
                text.Append("\n\n<color=#6B251C>海面场地</color>\n每格一种，新场地只覆盖重叠格。场地固定世界坐标，持续至所属方第 2 次后续回合开始；放置不触发。每大回合额度不因重铺、覆盖、移焰刷新，接舷或战斗结束清除。");
            switch (spec.SchoolId)
            {
                case "fire": text.Append("\n\n火场对双方每船每格每大回合造成一次 2 场地伤害；结束回合停留只补未触发格。浸油只被实际扣船体的主动火焰攻击触发。焚港引爆会伤及己舰，隔焰不能抵挡其直接伤害。"); break;
                case "arcane": text.Append("\n\n临时法力优先支付，己方回合结束清空。穿透部分直接扣船体，其余由格挡吸收；折射与星束取较高穿盾值。符文钳制只削减下一次新增格挡。"); break;
                case "curse": text.Append("\n\n禁域只强化域内敌方本次回合末咒印，额外最多 8。剥离只消耗所选咒印，不提前结算灾兆奖励；耗尽时清空整批咒印，后来新施加的咒印不继承旧状态。"); break;
                case "tide": text.Append("\n\n可选择部分潮势，剩余保留至回合结束。水流只在位移结束后推动，单结算链最多一次场地推动。定锚可在海图切换拒绝/允许推动。场地伤害不蓄潮，巨浪会伤及己舰。"); break;
                case "thunder": text.Append("\n\n雷栅双方每船每大回合触发一次，截断本次主动航行；强制推动不生成主动航线。主动雷霆攻击整张牌只增加 1 连锁，雷暴先耗尽再增加；接地回流同名共享一次成功回能。"); break;
                case "blood": text.Append("\n\n支付船体绕过格挡且须至少剩 1 点，不能触发受击收益。血誓首段增伤不叠加。背水重炮在支付前判断 40% 船体；止愈削减实际恢复，不影响格挡。"); break;
            }
            if (spec.HullCost > 0 && session?.Battle != null)
                text.Append("\n\n当前船体 ").Append(session.Hull).Append("；支付 ").Append(spec.HullCost).Append("；支付后 ").Append(Math.Max(0, session.Hull - spec.HullCost)).Append("。");
            return CoreBattleCardText.FormatEffects(text.ToString());
        }

        private string BoardingCardKeywordHelp(CoreBattleCard card)
        {
            if (card?.BoardingSpec == null) return string.Empty;
            var text = new StringBuilder(card.Description);
            text.Append("\n\n<color=#6B251C>接舷规则</color>\n卡牌数值不受人数影响。同名牌本回合每成功使用一次，后续费用增加1。资源按卡面固定数量或全部消耗。只有需要选择其他卡牌时打开选牌界面；取消不扣费用。");
            switch (card.SchoolId)
            {
                case "fire":
                    text.Append("\n\n<color=#6B251C>燃烧与燃料</color>\n每层在持有者回合结束扣1生命，共结算2次；各批独立计时，新增不刷新旧层。灼烧无视格挡。烧掉的手牌本场不能回收，战后恢复。移焰保留剩余次数，优先转移剩余次数较多的层数。"); break;
                case "arcane":
                    text.Append("\n\n<color=#6B251C>魔力与时序</color>\n魔力基础上限3，跨回合保留，不自然恢复。延迟伤害在下次己方回合开始结算，不暴击、不重复触发卡牌。封存只保留选中的实体牌；研读剩余牌按原顺序放回牌库底；收束自动结算最早的延迟攻击。"); break;
                case "curse":
                    text.Append("\n\n<color=#6B251C>咒痕</color>\n上限6层，每批持续3次敌方回合结束。咒痕本身不造成伤害；新增不刷新旧层，每层最多延长一次。消耗最早到期的层数。后续强化读取触发时层数；终祷与逆刃的咒术伤害无视格挡，不暴击。"); break;
                case "tide":
                    text.Append("\n\n<color=#6B251C>潮势</color>\n上限12。蓄潮格挡实际吸收敌方攻击，每吸收1伤害获得1潮势，不消耗木材。普通格挡先消耗且不产势。潮势在紧接着的己方回合结束过期；留潮每点只能延长一次。"); break;
                case "thunder":
                    text.Append("\n\n<color=#6B251C>连锁</color>\n上限6，己方回合结束清空。每张普通雷霆攻击结算后增加1链，多段只增加一次。终结牌消耗全部连锁，结算后不再加链。抽牌、回能与自动伤害不产链；本系不提供治疗或格挡。"); break;
                case "blood":
                    text.Append("\n\n<color=#6B251C>血契</color>\n生命支付无视格挡，支付后至少剩1生命；自付生命不算受到敌方攻击。生命不高于最大值40%时进入绝境，按支付后血量判断。吸血只按敌人实际损失的生命，格挡与过量击杀不计。"); break;
            }
            if (card.Repair > 0 || card.BoardingSpec.Key == "basic_07" || card.BoardingSpec.Key == "tide_12")
                text.Append("\n\n<color=#234921>治疗</color>\n完整治疗使用药物；无药按卡面保底值恢复。额外魔力、咒痕、潮势或火药不能替代药物。");
            string key = card.BoardingSpec.Key;
            int payment = key == "fire_14" ? 2 : key == "blood_02" ? 4 : key == "blood_01" || key == "blood_03" ||
                key == "blood_04" || key == "blood_05" ? 3 : 0;
            if (payment > 0 && session?.Battle?.Stage == NavalBattleStage.Boarding)
            {
                int health = session.Battle.PlayerBoardingHealth;
                text.Append("\n\n<color=#6B251C>生命支付预告</color>\n当前 ").Append(health).Append("；支付 ").Append(payment)
                    .Append("；支付后 ").Append(Math.Max(0, health - payment)).Append("。");
                if (health <= payment) text.Append("\n生命不足，不能出牌。");
            }
            return CoreBattleCardText.FormatEffects(text.ToString());
        }

        private static string CardKeywordCategory(CoreBattleCard card)
        {
            CoreSchoolUnlockDefinition school = CoreSchoolUnlockCatalog.Find(card.SchoolId);
            return school == null ? "分类 · " + card.Type : school.Name + "体系 · " + card.Type;
        }

        private string CardSchoolUnlockProgressText()
        {
            var names = new List<string>(); CoreSchoolUnlockDefinition next = null;
            foreach (CoreSchoolUnlockDefinition school in CoreSchoolUnlockCatalog.All)
            {
                if (session.PirateLevel >= school.RequiredLevel) names.Add(school.Name);
                else if (next == null) next = school;
            }
            return "海盗 Lv." + session.PirateLevel + "　已解锁：" + (names.Count == 0 ? "初始牌" : string.Join(" / ", names)) +
                (next == null ? "　六体系全部解锁" : "　下次 Lv." + next.RequiredLevel + " · " + next.Name + " " + next.BasicCardCount + " 张");
        }
    }
}
