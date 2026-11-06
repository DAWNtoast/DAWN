// Display-only fixtures for the current production UI. Never installed in the main project.
using System;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    public static partial class SmallTextCapture
    {
        private static bool ArrangeOther(GameObject host, string variant)
        {
            if (variant != "BossGuidance" && variant != "TreasureCombine" && variant != "NavalChoice")
                return false;
            if (Application.isPlaying)
                throw new InvalidOperationException("These display-only samples require a stopped editor.");
            var controller = host.GetComponent<CoreGameplayDemoController>();
            if (controller == null || controller.Session == null)
                throw new InvalidOperationException("Configure a production gameplay preview first.");

            switch (variant)
            {
                case "BossGuidance":
                    ArrangeSmallTextBossGuidance(controller);
                    RequireOtherText(host, "Destination Boss Guidance Caption", "封锁首领 · 点击规划航线");
                    break;
                case "TreasureCombine":
                    // Production preview API restores four identified fragments only into its
                    // disposable session. No identify/craft button or persistence callback runs.
                    controller.ConfigureEditorTreasureState(TreasureMapPreviewState.FragmentsReady);
                    RequireOtherText(host, "Treasure Route Progress 0", "4/4 · 点击合成");
                    break;
                case "NavalChoice":
                    ArrangeSmallTextNavalChoice(controller);
                    RequireOtherText(host, "Naval choice instruction", "悬停预览");
                    break;
            }
            foreach (Button button in host.GetComponentsInChildren<Button>(true))
                button.onClick.RemoveAllListeners();
            return true;
        }

        private static void ArrangeSmallTextBossGuidance(CoreGameplayDemoController controller)
        {
            CoreVoyageSession session = controller.Session;
            if (session.Map == null || session.Phase != CoreDemoPhase.Sailing)
                throw new InvalidOperationException("BossGuidance requires the Navigation preview.");
            // Use the production checkpoint restore helper to disclose the existing route
            // guardian in the disposable model; no sailing or gameplay transaction occurs.
            Call(session, "RestoreDestinationPortGuidance", true, false);
            if (session.DestinationBossGuidanceNode == null)
                throw new InvalidOperationException("The preview route has no active destination guardian.");
            bool building = (bool)Get(controller, "editorPreviewBuilding");
            Field(controller, "editorPreviewBuilding", true);
            try
            {
                Call(controller, "ClearPhaseContentForRender");
                Call(controller, "BuildSailing");
            }
            finally { Field(controller, "editorPreviewBuilding", building); }
        }

        private static void ArrangeSmallTextNavalChoice(CoreGameplayDemoController controller)
        {
            CoreBattleState battle = controller.Session.Battle;
            if (battle == null || battle.Stage == NavalBattleStage.Boarding)
                throw new InvalidOperationException("NavalChoice requires the naval Battle preview.");
            int cardIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .First(index => CoreVoyageSession.CardLibrary[index].Id == "card_school_fire_n01");
            CoreCardPackProgress ownership = controller.Session.CaptureCardPackProgress();
            var copies = ownership.cards.ToList();
            CoreCardCopySave existing = copies.FirstOrDefault(item => item.id == "card_school_fire_n01");
            if (existing == null) copies.Add(new CoreCardCopySave { id = "card_school_fire_n01", count = 1 });
            else existing.count = Math.Max(existing.count, 1);
            ownership.cards = copies.ToArray();
            if (!controller.Session.RestoreCardPackProgress(ownership) ||
                controller.Session.GetBaseBattleCardOwnedCopies(cardIndex) < 1)
                throw new InvalidOperationException("The in-memory card ownership sample was rejected.");
            if (battle.Hand.Count == 0) battle.Hand.Add(cardIndex);
            else battle.Hand[0] = cardIndex;
            battle.Energy = Math.Max(battle.Energy, 3);
            bool building = (bool)Get(controller, "editorPreviewBuilding");
            Field(controller, "editorPreviewBuilding", true);
            try
            {
                // Rebuild only the production view; generic Render also involves save adapters.
                Call(controller, "ClearPhaseContentForRender");
                Call(controller, "BuildBattleOverlay");
                Call(controller, "RemoveLegacyBattleScaffolding");
            }
            finally { Field(controller, "editorPreviewBuilding", building); }
            Button source = controller.GetComponentsInChildren<Button>().First(item => item.name == "Battle Card 0");
            CoreBattleCard card = controller.Session.GetBattleCard(cardIndex);
            bool opened = (bool)Call(controller, "TryBeginNavalCardChoice", source, 0, card);
            if (!opened) throw new InvalidOperationException("The production field selector did not open.");
        }

        private static void RequireOtherText(GameObject host, string name, string contains)
        {
            Text text = host.GetComponentsInChildren<Text>().FirstOrDefault(item => item.name == name);
            if (text == null || !text.isActiveAndEnabled || text.color.a <= .01f ||
                string.IsNullOrEmpty(text.text) || !text.text.Contains(contains))
                throw new InvalidOperationException("Missing visible production text: " + name + " / " + contains);
        }
    }
}