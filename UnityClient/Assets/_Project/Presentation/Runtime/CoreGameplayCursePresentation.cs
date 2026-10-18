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
            purify.onClick.AddListener(() => purifyAction());
            return purify;
        }
    }
}
