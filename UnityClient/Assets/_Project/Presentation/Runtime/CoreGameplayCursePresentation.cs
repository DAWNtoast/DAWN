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
            // The wall above the cabinet crest is reserved across all six room
            // paintings. The compact tutorial form stays below its taller header.
            RectTransform panel = WarehousePanel("Treasure Curse Panel", inventory,
                cursed ? new Vector2(900f, tutorialActive ? 72f : 94f) : new Vector2(440f, tutorialActive ? 28f : 40f),
                new Vector2(-312f, cursed ? (tutorialActive ? 396f : 426f) : (tutorialActive ? 420f : 438f)), Color.clear);
            Image face = panel.GetComponent<Image>();
            ApplyWarehouseWood(face);
            WarehouseLabel("Treasure Curse Status", panel,
                cursed ? new Vector2(864f, 26f) : new Vector2(416f, tutorialActive ? 24f : 30f),
                cursed ? new Vector2(0f, tutorialActive ? 19f : 27f) : Vector2.zero, 18,
                CurseBattleStatus, WarehouseRoomTheme.Brass);
            Text rule = WarehouseLabel("Treasure Curse Rule", panel, new Vector2(590f, 38f),
                new Vector2(-129f, -14f), 16,
                "每层战斗承伤 +10%（含冲撞自损）。装甲后、格挡前结算。\n返航、换船与存仓不会清除。",
                WarehouseRoomTheme.Ink);
            rule.alignment = TextAnchor.MiddleLeft;
            Button purify = BuildCursePurificationButton(panel, new Vector2(242f, 36f), new Vector2(310f, -14f));
            rule.gameObject.SetActive(cursed);
            purify.gameObject.SetActive(cursed);
        }

        private Button BuildCursePurificationButton(RectTransform parent, Vector2 size, Vector2 position)
        {
            int quotedCount = session.CurseCount;
            CoreVoyageSession quotedSession = session;
            int quotedFacility = activePortFacility;
            Button purify = WarehouseButton("Treasure Purify Curse", parent, size, position, PanelRaised, Ink, 18);
            purify.GetComponentInChildren<Text>().text = quotedCount == 0 ? "无需净化 · 无诅咒" :
                "净化 1 层 · " + CoreVoyageSession.CursePurificationCost.ToString("0") + " 金";
            purify.interactable = session.CanPurifyCurse(out _);
            System.Action purifyAction = () =>
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
            };
            if (parent.name == "Treasure Curse Panel") AddWarehouseAction(purify, purifyAction);
            else purify.onClick.AddListener(() => purifyAction());
            return purify;
        }
    }
}
