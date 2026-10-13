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
                new Vector2(190f, 248f), new Vector2(-780f, 164f), PanelRaised);
            ApplyRetreatBattleWoodBoard(panel.GetComponent<Image>());
            panel.GetComponent<Image>().raycastTarget = false;
            Text text = CreateText("Battle School Status", panel, new Vector2(174f, 232f), Vector2.zero,
                14, FontStyle.Normal, TextAnchor.UpperLeft, Ink);
            text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 13; text.resizeTextMaxSize = 14;
            RefreshBattleSchoolHud(root, battle);
        }

        private void RefreshBattleSchoolHud(RectTransform root, CoreBattleState battle)
        {
            Transform panel = root.Find("Battle School Status Panel");
            if (panel == null) return;
            bool visible = BattleHasSchoolCards(battle);
            panel.gameObject.SetActive(visible);
            Text label = panel.GetComponentInChildren<Text>(true);
            if (label != null) label.text = BattleSchoolStatusText(battle);
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
            text.Append("\n<size=12>临时法力、未用潮势在结束回合时清空</size>");
            return text.ToString();
        }

        private string SchoolCardKeywordHelp(CoreBattleCard card)
        {
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
