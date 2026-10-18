using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private SettlementChestSequenceController settlementChestSequence;
        private VoyageSettlement settlementChestShown;

        private bool HasActiveSettlementChest => session != null && session.Phase == CoreDemoPhase.Settlement &&
            settlementChestSequence != null && settlementChestSequence.gameObject.activeInHierarchy &&
            !settlementChestSequence.IsComplete;

        private void BuildSettlementChestIntro(VoyageSettlement result, SettlementChestRewardPlan plan,
            RectTransform overview, Texture2D harbor, Button skip, bool play)
        {
            if (!plan.HasRewards) return;
            Texture2D chestArt = Resources.Load<Texture2D>("SettlementReveal/settlement_chest_v1");
            if (chestArt == null) return;
            if (play) settlementChestShown = result;

            CanvasGroup overviewGroup = overview.gameObject.AddComponent<CanvasGroup>();
            RectTransform intro = CreateRect("Settlement Chest Intro", contentRoot, new Vector2(1920, 1080), Vector2.zero);
            CanvasGroup introGroup = intro.gameObject.AddComponent<CanvasGroup>();
            RawImage backdrop = CreateRawImage("Settlement Chest Harbor", intro, intro.sizeDelta, Vector2.zero,
                harbor, new Rect(0, 0, 1, 1));
            backdrop.raycastTarget = false;
            backdrop.color = new Color(.42f, .46f, .48f);
            // This full-screen input surface also keeps clicks off the hidden ledger.
            CreateImage("Settlement Chest Shade", intro, intro.sizeDelta, Vector2.zero,
                new Color(.018f, .035f, .045f, .82f)).raycastTarget = true;
            Image frame = CreateImage("Settlement Chest Timber Rim", intro, new Vector2(1840, 1000), Vector2.zero,
                new Color(.65f, .53f, .34f, .75f), WoodButtonSkin.Face);
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false;
            frame.pixelsPerUnitMultiplier = 17f;
            frame.raycastTarget = false;
            SettlementText("Settlement Chest Chapter", intro, new Vector2(1300, 32), new Vector2(0, 458),
                19, Html("#C8B896")).text = plan.IsFailure ? "返 航 · 抢 救 所 得" : "归 航 · 开 启 战 利 箱";
            Text title = SettlementText("Settlement Chest Title", intro, new Vector2(1440, 76), new Vector2(0, 374),
                54, Html("#F5D88F"));
            GameTypography.Apply(title, 54, FontStyle.Bold);
            Text status = SettlementText("Settlement Chest Status", intro, new Vector2(1580, 38), new Vector2(0, -471),
                22, Html("#D5C5A7"));

            // A single shared chest and atlas icons; quantities never allocate one object per unit.
            RectTransform chest = CreateRect("Settlement Chest", intro, new Vector2(660, 440), new Vector2(-340, -45));
            RawImage chestImage = CreateRawImage("Settlement Chest Art", chest, chest.sizeDelta, Vector2.zero,
                chestArt, new Rect(0, 0, 1, 1));
            chestImage.raycastTarget = false;
            if (plan.IsFailure) chestImage.color = new Color(.72f, .76f, .79f);

            CreateImage("Settlement Collected Shade", intro, new Vector2(1640, 170), new Vector2(0, -355),
                new Color(.01f, .025f, .035f, .48f)).raycastTarget = false;
            CreateImage("Settlement Collected Brass Line", intro, new Vector2(1640, 1), new Vector2(0, -269),
                new Color(.76f, .60f, .32f, .38f)).raycastTarget = false;
            RectTransform fireworksRoot = CreateRect("Settlement Chest Fireworks", intro, new Vector2(1760, 840), Vector2.zero);
            SettlementFireworksGraphic fireworks = fireworksRoot.gameObject.AddComponent<SettlementFireworksGraphic>();
            fireworks.raycastTarget = false;
            fireworksRoot.SetSiblingIndex(title.transform.GetSiblingIndex());

            int count = plan.Items.Count;
            var rewards = new RectTransform[count];
            var groups = new CanvasGroup[count];
            var amounts = new Text[count];
            var landing = new Vector2[count];
            float spacing = Mathf.Min(208f, 1480f / Mathf.Max(1, count - 1));
            for (int i = 0; i < count; i++)
            {
                SettlementChestRewardItem item = plan.Items[i];
                rewards[i] = CreateRect("Settlement Chest Reward " + item.Id, intro, new Vector2(340, 320), Vector2.zero);
                groups[i] = rewards[i].gameObject.AddComponent<CanvasGroup>();
                groups[i].blocksRaycasts = false;
                Color rewardColor = item.IsJackpot ? Html("#FFE6A2") : Html("#F7EAD2");
                CreateImage("Settlement Chest Reward Halo " + item.Id, rewards[i], new Vector2(210, 210), new Vector2(0, 65),
                    new Color(.88f, .65f, .25f, item.IsJackpot ? .15f : .06f), circleSprite).raycastTarget = false;
                ItemIconArt icon = SettlementChestIcon(item.Id);
                if (icon.IsAvailable)
                    CreateItemIcon("Settlement Chest Item Icon " + item.Id, rewards[i], icon, new Vector2(0, 65), 160f);
                else
                    SettlementText("Settlement Chest Item Symbol " + item.Id, rewards[i], new Vector2(180, 180),
                        new Vector2(0, 65), 64, rewardColor).text = item.Name.Substring(0, 1);
                Text name = SettlementText("Settlement Chest Item Name " + item.Id, rewards[i], new Vector2(340, 48),
                    new Vector2(0, -55), 30, rewardColor);
                name.text = item.Name;
                GameTypography.Apply(name, 30, FontStyle.Bold);
                amounts[i] = SettlementText("Settlement Chest Item Amount " + item.Id, rewards[i], new Vector2(440, 80),
                    new Vector2(0, -115), item.Amount >= 1000000 ? 42 : 64, rewardColor);
                landing[i] = new Vector2((i - (count - 1) * .5f) * spacing, -335);
            }

            // Keep the existing public skip control identity for keyboard input and tutorial fixtures.
            RectTransform skipRect = (RectTransform)skip.transform;
            skipRect.SetParent(intro, false);
            skipRect.sizeDelta = new Vector2(196, 48);
            skipRect.anchoredPosition = new Vector2(790, 450);
            WoodButtonSkin.Apply(skip, Color.white);
            Text skipText = skip.GetComponentInChildren<Text>();
            skipText.rectTransform.sizeDelta = new Vector2(180, 40);
            skipText.text = "跳过 · 查看战报";
            skip.onClick.RemoveAllListeners();

            settlementChestSequence = overview.gameObject.AddComponent<SettlementChestSequenceController>();
            SettlementChestSequenceController sequence = settlementChestSequence;
            sequence.Configure(plan, introGroup, overviewGroup, chest, rewards, groups, amounts, landing,
                title, status, fireworks, skip, PlaySfx, () =>
                {
                    if (!play || session.Phase != CoreDemoPhase.Settlement || !ReferenceEquals(session.Settlement, result) ||
                        sequence == null || !sequence.gameObject.activeInHierarchy || settlementChestSequence != sequence) return;
                    // The presentation opens the report only. Banking remains the explicit confirmation action.
                    BuildGuidedTutorialOverlay();
                    EventSystem events = EventSystem.current;
                    if (events != null && events.currentSelectedGameObject == null)
                    {
                        Button next = settlementDrawChartButton;
                        if (next == null || !next.IsInteractable())
                            next = overview.Find("Return To Port")?.GetComponent<Button>();
                        if (next != null && next.IsInteractable()) events.SetSelectedGameObject(next.gameObject);
                    }
                }, play);
            if (play && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(skip.gameObject);
        }

        private static ItemIconArt SettlementChestIcon(string id)
        {
            if (id == "Gold") return ItemIconCatalog.ForSpecial("gold");
            if (id == "Fragments") return ItemIconCatalog.ForSpecial("treasurefragment");
            if (id == "Fame") return AchievementIconCatalog.ForId("fame");
            if (id == "PirateExperience") return AchievementIconCatalog.ForId("voyage_three_events");
            return ItemIconCatalog.ForCargo(id);
        }
    }
}