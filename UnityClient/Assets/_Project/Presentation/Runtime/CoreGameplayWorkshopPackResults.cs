using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void BuildWorkshopPackResults(RectTransform table)
        {
            bool reveal = workshopPackRevealPending;
            workshopPackRevealPending = false;
            CoreCardPackDefinition pack = CoreCardPackCatalog.Find(workshopOpenedPackId);
            Color tableInk = new Color(.97f, .92f, .80f);
            CreateText("Workshop Packs Results Heading", table, new Vector2(1160f, 62f), new Vector2(0f, 392f),
                38, FontStyle.Bold, TextAnchor.MiddleCenter, tableInk).text = "获得 " + workshopPackResultCards.Length + " 张卡牌";
            CreateImage("Workshop Packs Results Divider", table, new Vector2(1176f, 2f),
                new Vector2(0f, 340f), new Color(.89f, .69f, .37f, .32f)).raycastTarget = false;

            // Preserve draw order and duplicate cards: every independent draw owns a complete card face.
            var slots = new RectTransform[workshopPackResultCards.Length];
            var faces = new RectTransform[workshopPackResultCards.Length];
            for (int slot = 0; slot < workshopPackResultCards.Length; slot++)
                slots[slot] = BuildWorkshopPackResultCard(table, workshopPackResultCards[slot], slot,
                    new Vector2(-376f + slot * 376f, 24f), out faces[slot]);

            int remaining = pack == null ? 0 : session.GetCardPackCount(pack.Id);
            Button again = CreateButton("Workshop Packs Open Again", table, new Vector2(360f, 66f),
                new Vector2(-384f, -383f), Gold, Ink, 26);
            again.GetComponentInChildren<Text>().text = remaining > 0 ? "继续开包 · 剩余 " + remaining : "该卡包已开完";
            again.interactable = remaining > 0 && pack != null;
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            string packId = workshopOpenedPackId;
            again.onClick.AddListener(() => OpenWorkshopCardPack(packId, again, owner, port));

            Button list = CreateButton("Workshop Packs Back To List", table, new Vector2(360f, 66f),
                new Vector2(0f, -383f), PanelRaised, Ink, 26);
            list.GetComponentInChildren<Text>().text = "返回卡包列表";
            list.onClick.AddListener(() =>
            {
                if (!CanUseWorkshopPackButton(list, owner, port)) return;
                workshopOpenedPackId = null;
                workshopPackResultCards = null;
                workshopPackNotice = string.Empty;
                Render();
            });
            BuildWorkshopPackCollectionButton(table, new Vector2(384f, -383f), new Vector2(360f, 66f));
            if (reveal)
            {
                WorkshopPackRevealSequence motion = table.gameObject.AddComponent<WorkshopPackRevealSequence>();
                try { motion.Configure(slots, faces, again, this, WorkshopPackAccent(workshopOpenedPackId)); }
                catch (System.Exception error)
                {
                    motion.Cancel();
                    Debug.LogWarning("卡包已入库；揭晓动效暂未显示。" + error.Message);
                }
            }
        }

        private RectTransform BuildWorkshopPackResultCard(RectTransform table, int index, int slot, Vector2 center, out RectTransform face)
        {
            NavalBattleStage stage = WorkshopPackResultStage(index);
            CoreBattleCard card = session.GetBattleCard(index, stage);
            Rect bounds = CardSchoolArtCatalog.FrameBounds(card);
            const float scale = 1.20f;
            string prefix = "Workshop Pack Result " + slot;
            RectTransform holder = CreateRect(prefix + " Slot", table, bounds.size * scale, center);
            Image hit = holder.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            string description = session.BattleCardFaceDescription(card, stage);
            face = CreateCollectionCardFace(holder, card, prefix, prefix + " Art", prefix + " Name",
                scale, description, prefix + " Effect");
            RefreshCardSupplyCost(face, card, previewStage: stage);
            face.anchoredPosition = -bounds.center * scale;
            foreach (Graphic graphic in face.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            CollectionCardKeywordHover.Bind(holder.gameObject, face, card, FormatBattleCardDescription(description));
            CreateText(prefix + " Stage", table, new Vector2(344f, 36f),
                center - new Vector2(0f, bounds.height * scale * .5f + 38f),
                23, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(.88f, .80f, .65f)).text = WorkshopStageName(stage) + " · +1 张";
            return holder;
        }

        private NavalBattleStage WorkshopPackResultStage(int index)
        {
            CoreBattleCard definition = CoreVoyageSession.CardLibrary[index];
            if (definition.IsAllowedInStage(selectedBattleDeckStage)) return selectedBattleDeckStage;
            foreach (NavalBattleStage stage in WorkshopBattleStages)
                if (definition.IsAllowedInStage(stage)) return stage;
            return NavalBattleStage.Naval;
        }
    }
}
