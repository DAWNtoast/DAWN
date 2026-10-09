using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private string CurseBattleStatus => session.CurseCount == 0 ? "无诅咒 · 战斗承伤正常" :
            "诅咒 " + session.CurseCount + " 层 · 战斗承伤 +" + session.CurseDamageBonusPercent +
            "%（最高 +100%）· 回港可净化";

        private void BuildWarehouseCurseService(RectTransform inventory)
        {
            bool cursed = session.CurseCount > 0;
            RectTransform panel = TreasureWoodPanel("Treasure Curse Panel", inventory,
                cursed ? new Vector2(1030f, 106f) : new Vector2(320f, 40f),
                cursed ? new Vector2(-374f, 320f) : new Vector2(-729f, 353f));
            TreasureLabel("Treasure Curse Status", panel,
                cursed ? new Vector2(970f, 28f) : new Vector2(296f, 30f),
                cursed ? new Vector2(0f, 29f) : Vector2.zero, cursed ? 20 : 18,
                Gold, CurseBattleStatus, TextAnchor.MiddleCenter);
            Text rule = TreasureLabel("Treasure Curse Rule", panel, new Vector2(616f, 48f),
                new Vector2(-173f, -18f), 17, Ink,
                "每层战斗承伤 +10%（含冲撞自损）。装甲后、格挡前结算。\n返航、换船与存仓不会清除。",
                TextAnchor.MiddleLeft);
            Button purify = BuildCursePurificationButton(panel, new Vector2(308f, 44f), new Vector2(326f, -18f));
            rule.gameObject.SetActive(cursed);
            purify.gameObject.SetActive(cursed);
        }

        private Button BuildCursePurificationButton(RectTransform parent, Vector2 size, Vector2 position)
        {
            int quotedCount = session.CurseCount;
            CoreVoyageSession quotedSession = session;
            int quotedFacility = activePortFacility;
            Button purify = CreateButton("Treasure Purify Curse", parent, size, position, PanelRaised, Ink, 18);
            purify.GetComponentInChildren<Text>().text = quotedCount == 0 ? "无需净化 · 无诅咒" :
                "净化 1 层 · " + CoreVoyageSession.CursePurificationCost.ToString("0") + " 金";
            purify.interactable = session.CanPurifyCurse(out _);
            purify.onClick.AddListener(() =>
            {
                if (this == null || !isActiveAndEnabled || !ReferenceEquals(session, quotedSession) ||
                    activePortFacility != quotedFacility) return;
                if (session.TryPurifyCurse(quotedCount, out string result))
                {
                    PlaySfx("curse_purify", 0.52f);
                    SaveProgress();
                }
                portNotice = result;
                Render();
            });
            return purify;
        }
    }
}
