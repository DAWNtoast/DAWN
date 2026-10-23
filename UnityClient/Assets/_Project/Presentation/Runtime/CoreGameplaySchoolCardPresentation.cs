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
                text.Append("\n敌方咒印 ").Append(s.CurseMarks).Append(" 层");
                if (s.CurseTickBonus > 0)
                    text.Append("\n每次咒印结算额外伤害 +").Append(s.CurseTickBonus);
                if (s.CurseNextTickBonus > 0)
                    text.Append("\n下次咒印结算额外伤害 +").Append(s.CurseNextTickBonus);
            }
            if (s.CurseGuard > 0) text.Append("\n本回合咒盾 ").Append(s.CurseGuard);
            if (battle.EnemyBurnTurns > 0)
                text.Append("\n敌方燃烧 ").Append(battle.EnemyBurnDamage).Append(" 层");
            text.Append("\n潮势 ").Append(s.TidePower).Append('/').Append(s.TideLimit);
            if (s.TidePaidBlock > 0) text.Append(" · 付木护盾 ").Append(s.TidePaidBlock);
            if (s.NaturalFireGuardTurns > 0)
                text.Append("\n焰盾 +").Append(s.NaturalFireGuard).Append(" · ").Append(s.NaturalFireGuardTurns).Append(" 回合");
            if (s.ScorchCharges > 0) text.Append("\n灼痕 +").Append(s.ScorchDamage).Append(" · ").Append(s.ScorchCharges).Append(" 次");
            if (s.BoardingBurn > 0) text.Append("\n入舷火种 ").Append(s.BoardingBurn).Append(" 层");
            if (s.BoardingCover > 0) text.Append("\n入舷掩护 ").Append(s.BoardingCover);
            if (s.OverflowAmount > 0) text.Append("\n余烬装填 ").Append(s.OverflowContribution).Append('/').Append(s.OverflowOwnLimit);
            CoreNavalFieldShipState field = battle.NavalFields.Player;
            CoreNavalExpansionBattleState naval = battle.NavalExpansion;
            if (field.FireImmunityCharges > 0) text.Append("\n隔焰免伤 ").Append(field.FireImmunityCharges).Append(" 次");
            if (field.FieldImmunityExpires > battle.NavalFields.PlayerTurn)
                foreach (CoreNavalFieldKind kind in new[] { CoreNavalFieldKind.Fire, CoreNavalFieldKind.Curse,
                    CoreNavalFieldKind.Tide, CoreNavalFieldKind.Thunder })
                    if ((field.FieldImmunityMask & (1 << (int)kind)) != 0)
                        text.Append('\n').Append(CoreComponentFieldCards.FieldName(kind)).Append("免疫 · 至下回合开始");
            if (field.ArcanePiercing > 0) text.Append("\n下张秘术穿盾 ").Append(field.ArcanePiercing);
            if (field.BloodAttackBonus > 0) text.Append("\n下张攻击首段 +").Append(field.BloodAttackBonus);
            if (field.AnchorCharges > 0) text.Append("\n定锚 ").Append(field.AnchorCharges).Append(" 次");
            if (naval.OilPlayerEndsRemaining > 0) text.Append("\n敌舰浸油 · 待火焰命中");
            if (naval.RuneClamp) text.Append("\n敌下次获盾 −").Append(naval.RuneClampReduction > 0 ? naval.RuneClampReduction : 6);
            if (naval.LoadSeal) text.Append("\n敌下次装填 −6");
            if (naval.PulseReduction > 0) text.Append("\n敌每次攻击 −").Append(naval.PulseReduction).Append("（至敌回合末）");
            if (battle.NavalFields.Enemy.MovementSlow > 0) text.Append("\n敌下次航行 −").Append(battle.NavalFields.Enemy.MovementSlow).Append(" 格");
            if (naval.Precharge) text.Append("\n下次雷攻额外 +").Append(naval.PrechargeBonus > 0 ? naval.PrechargeBonus : 2).Append(" 链");
            if (naval.GroundingReady && !naval.GroundingUsed) text.Append("\n接地回流 · 待雷栅命中");
            if (naval.EnemyHealingHalved) text.Append("\n敌舰止愈 · 恢复减半");
            text.Append("\n<size=12>临时法力、未用潮势在结束回合时清空</size>");
            return text.ToString();
        }

        private string SchoolCardKeywordHelp(CoreBattleCard card) => BattleCardTooltipText(card, -1);

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
