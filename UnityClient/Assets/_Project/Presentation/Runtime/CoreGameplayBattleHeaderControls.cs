using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void BuildBattleHeaderControls(RectTransform header, Color accent)
        {
            Button deck = CreateBattleHeaderIconButton("Battle Deck", header, "卡组", false, accent);
            deck.onClick.AddListener(OpenBattleDeckOverview);
            Button settings = CreateBattleHeaderIconButton("Battle Settings", header, "设置", true, accent);
            settings.onClick.AddListener(() =>
            {
                if (session == null || session.Phase != CoreDemoPhase.Battle ||
                    battleAnimationPlaying || boardingCardChoiceOverlay != null ||
                    portSettingsOverlay != null || battleDeckOverlay != null || gameMasterOverlay != null) return;
                DismissBattleHandInspection();
                OpenPortSettings();
            });
        }

        private Button CreateBattleHeaderIconButton(string name, RectTransform header,
            string caption, bool settings, Color accent)
        {
            Button button = CreatePortHeaderButton(name, header, Vector2.one * NauticalHeaderSkin.IconSize,
                Vector2.zero, caption, 12, accent);
            NauticalHeaderSkin.ApplyIconButton(button, settings);
            return button;
        }
    }
}
