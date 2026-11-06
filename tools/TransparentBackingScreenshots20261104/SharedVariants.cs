// Disposable Editor PreviewScene fixtures. No transactions, saves or PlayerPrefs writes.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    public static partial class TransparentBackingCapture
    {
        private static bool ArrangeShared(GameObject host, string variant)
        {
            if (variant != "Achievements" && variant != "KeywordTooltip" && variant != "StatusTooltip" &&
                variant != "AchievementToast" && variant != "NavalChoice") return false;
            if (Application.isPlaying) throw new InvalidOperationException("Shared captures require a stopped editor.");

            if (variant == "Achievements")
            {
                ArrangeSharedAchievements(host.GetComponent<MainMenuController>());
            }
            else
            {
                var controller = host.GetComponent<CoreGameplayDemoController>();
                if (controller == null || controller.Session == null)
                    throw new InvalidOperationException("Configure the gameplay preview before arranging shared samples.");
                switch (variant)
                {
                    case "KeywordTooltip":
                        // Use the production collection and its pointer component. Cannon cards
                        // provide a stable glossary sample even if preparation cards have none.
                        controller.ConfigureEditorPreview(CoreGameplayPreviewScreen.DeckBuilder,
                            CoreGameplayPreviewPort.BrokenAnchor, 1);
                        if (SharedKeywordOwner(host) == null)
                            throw new InvalidOperationException("Deck-builder preview has no visible card glossary.");
                        break;
                    case "StatusTooltip":
                        controller.Session.Battle.PlayerBlock = 18;
                        Call(controller, "RefreshBattleStatusHud", SharedGet(controller, "retainedBattleRoot"),
                            controller.Session.Battle);
                        break;
                    case "AchievementToast":
                        object presenter = Call(controller, "BuildAchievementToast");
                        Field(controller, "achievementToast", presenter);
                        Call(presenter, "Show", AchievementCatalog.All[0], 1);
                        RequireSharedImage(host, "Achievement Notification");
                        break;
                    case "NavalChoice":
                        ArrangeSharedNavalChoice(controller);
                        RequireSharedImage(host, "Naval choice controls");
                        break;
                }
            }
            foreach (Button button in host.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            return true;
        }

        private static void BeforeRenderShared(GameObject host, string variant, Camera camera, int pass)
        {
            if (pass < 2) return;
            if (variant == "KeywordTooltip")
            {
                CollectionCardKeywordHover owner = SharedKeywordOwner(host);
                if (owner == null) throw new InvalidOperationException("The production keyword-hover owner is missing.");
                owner.Show();
                foreach (CardKeywordTooltip tooltip in host.GetComponentsInChildren<CardKeywordTooltip>())
                    tooltip.RefreshPosition();
                RequireSharedImage(host, "Keyword Entry");
            }
            else if (variant == "StatusTooltip")
            {
                BattleStatusHover hover = host.GetComponentsInChildren<BattleStatusHover>()
                    .FirstOrDefault(item => item.name == "Status guard" &&
                        item.transform.parent.name == "Player Battle Status Row");
                if (hover == null) throw new InvalidOperationException("The production player guard status is missing.");
                hover.OnPointerEnter(null);
                RequireSharedImage(host, "Battle Status Tooltip");
            }
        }

        private static void ArrangeSharedAchievements(MainMenuController menu)
        {
            if (menu == null) throw new InvalidOperationException("Achievements requires a MainMenuController preview.");
            // Construct a display-only snapshot rather than consulting or changing the
            // player's achievement journal. The rows and their binding are production UI.
            var progress = new AchievementProgressView[AchievementCatalog.All.Count];
            int unlocked = 0;
            for (int i = 0; i < progress.Length; i++)
            {
                bool earned = i % 3 == 0;
                if (earned) unlocked++;
                progress[i] = new AchievementProgressView(earned, earned ? 1m : 0m, 1m, false);
            }
            Field(menu, "achievementProgress", progress);
            var panel = SharedGet(menu, "achievementsPanel") as RectTransform;
            if (panel == null)
            {
                Call(menu, "BuildAchievementsPanel", SharedGet(menu, "menuCanvas"));
                panel = (RectTransform)SharedGet(menu, "achievementsPanel");
            }
            MethodInfo summary = typeof(MainMenuController).GetMethod("BuildAchievementProgress",
                BindingFlags.Static | BindingFlags.NonPublic);
            ((Text)SharedGet(menu, "achievementSummary")).text = (string)summary.Invoke(null, new object[] { unlocked });
            Call(menu, "FilterAchievements", false);
            panel.gameObject.SetActive(true);
            if (!menu.GetComponentsInChildren<Image>().Any(image => image.name.StartsWith("Achievement Row ", StringComparison.Ordinal)))
                throw new InvalidOperationException("The achievement preview did not create any visible rows.");
        }

        private static void ArrangeSharedNavalChoice(CoreGameplayDemoController controller)
        {
            CoreBattleState battle = controller.Session.Battle;
            if (battle == null || battle.Stage == NavalBattleStage.Boarding)
                throw new InvalidOperationException("NavalChoice requires the naval Battle editor preview.");
            int cardIndex = Enumerable.Range(0, CoreVoyageSession.CardLibrary.Count)
                .First(index => CoreVoyageSession.CardLibrary[index].Id == "card_school_fire_n01");
            // Restore one owned copy into this disposable session only. This is the
            // model's restore API, not a pack-opening, purchase or persistence action.
            CoreCardPackProgress ownership = controller.Session.CaptureCardPackProgress();
            var copies = ownership.cards.ToList();
            CoreCardCopySave existing = copies.FirstOrDefault(item => item.id == "card_school_fire_n01");
            if (existing == null) copies.Add(new CoreCardCopySave { id = "card_school_fire_n01", count = 1 });
            else existing.count = Math.Max(existing.count, 1);
            ownership.cards = copies.ToArray();
            if (!controller.Session.RestoreCardPackProgress(ownership) ||
                controller.Session.GetBaseBattleCardOwnedCopies(cardIndex) < 1)
                throw new InvalidOperationException("The in-memory naval card ownership fixture was rejected.");
            if (battle.Hand.Count == 0) battle.Hand.Add(cardIndex);
            else battle.Hand[0] = cardIndex;
            battle.Energy = Math.Max(battle.Energy, 3);
            // Rebuild only the view. BuildSelectedEditorPreview would stage a new battle
            // and overwrite the in-memory hand; Render includes unrelated save adapters.
            bool building = (bool)SharedGet(controller, "editorPreviewBuilding");
            Field(controller, "editorPreviewBuilding", true);
            try
            {
                Call(controller, "ClearPhaseContentForRender");
                Call(controller, "BuildBattleOverlay");
                Call(controller, "RemoveLegacyBattleScaffolding");
            }
            finally { Field(controller, "editorPreviewBuilding", building); }
            Button source = controller.GetComponentsInChildren<Button>().First(item => item.name == "Battle Card 0");
            CoreBattleCard card = controller.Session.GetBattleCard(cardIndex);
            bool opened = (bool)Call(controller, "TryBeginNavalCardChoice", source, 0, card);
            if (!opened) throw new InvalidOperationException("The production naval field selector did not open.");
            Text validation = controller.GetComponentsInChildren<Text>().First(item => item.name == "Naval choice validation");
            if (validation.text != "请选择海格")
                throw new InvalidOperationException("Expected ordinary naval placement selection: " + validation.text);
        }

        private static CollectionCardKeywordHover SharedKeywordOwner(GameObject host)
        {
            return host.GetComponentsInChildren<CollectionCardKeywordHover>()
                .Where(hover => hover.isActiveAndEnabled)
                .FirstOrDefault(hover => SharedGet(hover, "entries") is IReadOnlyList<CoreBattleCardKeyword> entries && entries.Count > 0);
        }

        private static object SharedGet(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            return field.GetValue(target);
        }

        private static void RequireSharedImage(GameObject host, string name)
        {
            Image image = host.GetComponentsInChildren<Image>().FirstOrDefault(item => item.name == name);
            if (image == null || !image.isActiveAndEnabled || image.color.a <= .01f)
                throw new InvalidOperationException("Missing visible shared backdrop: " + name);
            if (image.sprite != null || image.overrideSprite != null)
                throw new InvalidOperationException("Expected an untextured shared backdrop: " + name);
        }
    }
}
