using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private VoyageSettlement settlementWarehouseQuote;
        private bool storeAllSettlementCargo;

        private void BuildSettlementStorageOption(RectTransform card, VoyageSettlement result, bool previewSample)
        {
            if (!ReferenceEquals(settlementWarehouseQuote, result))
            {
                settlementWarehouseQuote = result;
                storeAllSettlementCargo = false;
            }
            Button store = CreateButton("Settlement Store All Cargo", card,
                new Vector2(600f, 38f), new Vector2(-510f, -374f), PanelRaised, Ink, 20);
            MarketShopSkin.ApplyTextButton(store);
            Text caption = store.GetComponentInChildren<Text>();
            caption.rectTransform.anchoredPosition = new Vector2(18f, 0f);
            caption.rectTransform.sizeDelta = new Vector2(530f, 36f);
            caption.alignment = TextAnchor.MiddleLeft;
            CenterSettlementText(caption);
            Image checkbox = CreateImage("Settlement Cargo Checkbox", store.transform,
                new Vector2(24f, 24f), new Vector2(-267f, 0f), MarketShopSkin.MutedInk);
            Image inset = CreateImage("Settlement Cargo Checkbox Interior", checkbox.transform,
                new Vector2(18f, 18f), Vector2.zero, new Color(.96f, .92f, .82f));
            CreateImage("Settlement Cargo Selected", inset.transform,
                new Vector2(12f, 12f), Vector2.zero, new Color(.16f, .36f, .32f)).raycastTarget = false;
            checkbox.raycastTarget = inset.raycastTarget = false;
            Text note = SettlementText("Settlement Note", card, new Vector2(570f, 55f),
                new Vector2(-495f, -427f), 17, MarketShopSkin.MutedInk, TextAnchor.MiddleLeft);
            UpdateSettlementStorageLabels(caption, note, result);
            store.interactable = Application.isPlaying && !editorPreviewBuilding && !previewSample && !tutorialActive;
            store.onClick.AddListener(() =>
            {
                if (!Application.isPlaying || editorPreviewBuilding || previewSample || tutorialActive || HasActiveSettlementChest ||
                    session.Phase != CoreDemoPhase.Settlement || !ReferenceEquals(session.Settlement, result) ||
                    !ReferenceEquals(settlementWarehouseQuote, result)) return;
                storeAllSettlementCargo = !storeAllSettlementCargo;
                // Updating the checkbox must not restart the reward reveal.
                UpdateSettlementStorageLabels(caption, note, result);
            });
        }

        private void UpdateSettlementStorageLabels(Text caption, Text note, VoyageSettlement result)
        {
            caption.text = "是否将所有货物置入仓库";
            Transform selected = caption.transform.parent.Find("Settlement Cargo Checkbox/Settlement Cargo Checkbox Interior/Settlement Cargo Selected");
            if (selected != null) selected.gameObject.SetActive(storeAllSettlementCargo);
            string port = result.Outcome == VoyageOutcome.ArrivedAtPort ? result.Destination : session.CurrentPort;
            note.text = tutorialActive
                ? "教学物资保留随船；完成教学后可选择全货入库。\n确认一次即可完成结算。"
                : storeAllSettlementCargo
                    ? "基础与珍稀货物存入" + port + "（含水粮、弹药）。\n金币照常入账；出航前请重新装船补给。"
                    : "未勾选：结算后货物保留在船舱。\n勾选后，基础与珍稀货物存入" + port + "。";
        }

        private string StoreSettledCargoIfRequested(VoyageSettlement result)
        {
            bool requested = ReferenceEquals(settlementWarehouseQuote, result) && storeAllSettlementCargo;
            storeAllSettlementCargo = false;
            settlementWarehouseQuote = null;
            if (!requested) return string.Empty;
            return session.TryDepositAllPortCargo()
                ? "结算后保留的基础与珍稀货物已存入" + session.CurrentPort + "仓库；出航前请重新装船补给。"
                : "货物未能全部入库，已完整保留在船舱。请检查本港仓储数量后再存入。";
        }
    }
}
