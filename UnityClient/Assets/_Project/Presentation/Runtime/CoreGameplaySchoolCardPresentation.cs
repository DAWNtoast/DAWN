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
            return s.Mana > 0 || s.ManaLimit > 3 || s.CurseMarks > 0 ||
                s.TidePower > 0 || s.TidePaidBlock > 0 || s.ThunderChain > 0 || s.ThunderReadyDamage > 0 ||
                s.NaturalFireGuardTurns > 0 || s.BoardingBurn > 0 || s.BoardingCover > 0 || s.ScorchCharges > 0;
        }

        private static string BattleSchoolStatusText(CoreBattleState battle)
        {
            if (battle == null) return string.Empty;
            CoreSchoolBattleState s = battle.Schools;
            var text = new StringBuilder("体系状态");
            text.Append("\n法力 ").Append(s.Mana).Append('/').Append(s.ManaLimit);
            text.Append("\n连锁 ").Append(s.ThunderChain);
            if (s.ThunderReadyDamage > 0) text.Append("\n下次雷击 +").Append(s.ThunderReadyDamage);
            if (s.CurseMarks > 0)
            {
                text.Append("\n敌方咒印 ").Append(s.CurseMarks).Append(" 层");
                text.Append(" · 消灭阈值 ").Append(s.EnemyCurseExecutionThresholdPercent).Append('%');
            }
            if (battle.PlayerCurseMarks > 0) text.Append("\n自身咒印 ").Append(battle.PlayerCurseMarks).Append(" 层");
            if (battle.EnemyBurnTurns > 0)
                text.Append("\n敌方燃烧 ").Append(battle.EnemyBurnDamage).Append(" 层");
            text.Append("\n潮势 ").Append(s.TidePower);
            if (s.TidePaidBlock > 0) text.Append(" · 蓄潮格挡 ").Append(s.TidePaidBlock);
            if (battle.Boarding.TideGuardConversionActive) text.Append("\n留潮 · 本场后续格挡转换为蓄潮格挡");
            if (s.NaturalFireGuardTurns > 0)
                text.Append("\n焰盾 +").Append(s.NaturalFireGuard).Append(" · ").Append(s.NaturalFireGuardTurns).Append(" 回合");
            if (s.ScorchCharges > 0) text.Append("\n灼痕 +").Append(s.ScorchDamage).Append(" · ").Append(s.ScorchCharges).Append(" 次");
            if (s.BoardingBurn > 0) text.Append("\n入舷火种 ").Append(s.BoardingBurn).Append(" 层");
            if (s.BoardingCover > 0) text.Append("\n入舷掩护 ").Append(s.BoardingCover);
            if (s.OverflowAmount > 0) text.Append("\n余烬装填 ").Append(s.OverflowContribution).Append('/').Append(s.OverflowOwnLimit);
            CoreNavalFieldShipState field = battle.NavalFields.Player;
            CoreNavalExpansionBattleState naval = battle.NavalExpansion;
            int ownerTurn = battle.NavalFields.PlayerTurn;
            if (field.BurnPreventionExpiresAtOwnerTurn > ownerTurn ||
                (field.BurnPreventionExpiresAtTurnEnd && field.BurnPreventionExpiresAtOwnerTurn == ownerTurn))
                text.Append("\n火场负面效果免疫 · 至")
                    .Append(field.BurnPreventionExpiresAtOwnerTurn == ownerTurn ? "本回合" : "下回合")
                    .Append(field.BurnPreventionExpiresAtTurnEnd ? "结束" : "开始");
            if (field.FieldImmunityExpires > battle.NavalFields.PlayerTurn || field.TideImmunityExpiresAfterPlayerTurnEnd > 0)
                foreach (CoreNavalFieldKind kind in new[] { CoreNavalFieldKind.Curse,
                    CoreNavalFieldKind.Tide, CoreNavalFieldKind.Thunder, CoreNavalFieldKind.Blood })
                    if (kind == CoreNavalFieldKind.Tide && field.TideImmunityExpiresAfterPlayerTurnEnd > 0)
                        text.Append('\n').Append("定向水流免疫 · 至")
                            .Append(field.TideImmunityExpiresAfterPlayerTurnEnd <= battle.NavalFields.PlayerTurn ? "本回合" : "下回合").Append("结束");
                    else if (field.FieldImmunityExpires > battle.NavalFields.PlayerTurn && (field.FieldImmunityMask & (1 << (int)kind)) != 0)
                        text.Append('\n').Append(CoreComponentFieldCards.FieldName(kind)).Append("负面效果免疫 · 至下回合开始");
            if (field.AnchorCharges > 0) text.Append("\n定锚 ").Append(field.AnchorCharges).Append(" 次");
            if (naval.OilPlayerEndsRemaining > 0) text.Append("\n敌舰浸油 · 下次新增燃烧翻倍一次");
            if (naval.RuneClamp) text.Append("\n敌下次获盾 −").Append(naval.RuneClampReduction > 0 ? naval.RuneClampReduction : 6);
            if (naval.LoadSeal) text.Append("\n敌下次炮击 −").Append(naval.LoadSealReduction > 0 ? naval.LoadSealReduction : 6);
            if (naval.PulseReduction > 0) text.Append("\n敌每次攻击 −").Append(naval.PulseReduction).Append("（至敌回合末）");
            if (battle.NavalFields.Enemy.MovementSlow > 0) text.Append("\n敌下次航行 −").Append(battle.NavalFields.Enemy.MovementSlow).Append(" 格");
            if (battle.NavalFields.Enemy.NextMovementMaxCells > 0)
                text.Append("\n敌下次航行至多 ").Append(battle.NavalFields.Enemy.NextMovementMaxCells).Append(" 格");
            if (naval.Precharge) text.Append("\n下次雷攻额外 +").Append(naval.PrechargeBonus > 0 ? naval.PrechargeBonus : 2).Append(" 链");
            if (naval.GroundingReady && !naval.GroundingUsed) text.Append("\n接地回流 · 待雷霆牌造成伤害");
            if (naval.EnemyHealingBanTurns > 0) text.Append("\n敌方禁疗 · 无法恢复 · 剩余 ").Append(naval.EnemyHealingBanTurns).Append(" 次敌方回合结束");
            if (naval.PlayerHealingBanTurns > 0) text.Append("\n己方禁疗 · 无法恢复 · 剩余 ").Append(naval.PlayerHealingBanTurns).Append(" 次己方回合结束");
            text.Append("\n<size=12>法力在己方回合开始时恢复至上限；潮势保留至战斗结束</size>");
            return text.ToString();
        }

        private string SchoolCardKeywordHelp(CoreBattleCard card) => BattleCardTooltipText(card, -1);

        private string CardSchoolUnlockProgressText()
        {
            return "海盗 Lv." + session.PirateLevel + "　体系卡牌通过打开卡包获取";
        }
    }
}
