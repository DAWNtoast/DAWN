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
            Button button = CreatePortHeaderButton(name, header, new Vector2(64f, 64f),
                Vector2.zero, caption, 12, accent);
            Text label = button.GetComponentInChildren<Text>();
            label.rectTransform.sizeDelta = new Vector2(52f, 17f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            label.resizeTextForBestFit = false;
            RectTransform icon = CreateRect(name + " Icon", button.transform,
                new Vector2(34f, 34f), new Vector2(0f, 8f));
            icon.gameObject.AddComponent<BattleHeaderIconGraphic>().Configure(settings, accent);
            return button;
        }
    }
}
