using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool CanPlayPortActionVfx => Application.isPlaying && !editorPreviewBuilding &&
            isActiveAndEnabled && !profileSaveBlocked && session != null && session.Phase == CoreDemoPhase.Port && contentRoot != null;

        private RectTransform FindPortActionAnchor(string name) => contentRoot == null ? null :
            contentRoot.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(item => item.name == name);

        private Vector2 CapturePortActionOrigin(string name, RectTransform fallback = null)
        {
            RectTransform target = FindPortActionAnchor(name) ?? fallback;
            return PortTransactionMotion.Anchor(contentRoot, target);
        }

        private void PlayMarketTradeFeedback(PortMarketItem item, decimal goldDelta, int itemDelta, bool warehouse, Vector2 origin)
        {
            // Only the successful commit paths call this, after the real values have been refreshed.
            if (!CanPlayPortActionVfx || activePortFacility != PortFacilityArtCatalog.MarketFacilityIndex || itemDelta == 0) return;
            ItemIconArt art = ItemIconCatalog.ForCargo(PortMarketCatalog.Key(item));
            RectTransform holdings = marketDetailCargo != null && marketDetailCargo.gameObject.activeInHierarchy
                ? marketDetailCargo.rectTransform : marketInventoryToggle != null && marketInventoryToggle.gameObject.activeInHierarchy
                    ? (RectTransform)marketInventoryToggle.transform : null;
            RectTransform rowIcon = marketRowViews.TryGetValue(item, out MarketRowView row) && row.Icon != null && row.Icon.gameObject.activeInHierarchy
                ? (RectTransform)row.Icon.transform : null;
            Vector2 to = itemDelta > 0
                ? (holdings == null ? origin + new Vector2(0f, -110f) : PortTransactionMotion.Anchor(contentRoot, holdings))
                : (rowIcon == null ? origin + new Vector2(0f, 110f) : PortTransactionMotion.Anchor(contentRoot, rowIcon));
            string destination = warehouse ? "本港仓储" : "随船物资";
            string receipt = destination + " · " + PortMarketCatalog.Name(item) + " " + (itemDelta > 0 ? "+" : "−") + System.Math.Abs(itemDelta);
            string gold = "金币 " + (goldDelta > 0m ? "+" : "−") + PortMarketCatalog.FormatPrice(System.Math.Abs(goldDelta));
            PortTransactionMotion.Play(contentRoot, origin, to, art, receipt, interfaceFont, this,
                accent: holdings, secondaryAnchor: portHeaderGold == null ? null : portHeaderGold.rectTransform, secondaryText: gold,
                destinationAnchor: itemDelta > 0 ? holdings : rowIcon, receiptSpace: contentRoot, receiptNormalized: new Vector2(.56f, .20f));
        }

        private void PlayShipyardRefitFeedback(int shipIndex, ComponentSlot slot, ComponentArtEntry before,
            ComponentArtEntry after, string movedInstance, Vector2 origin)
        {
            if (!CanPlayPortActionVfx || !IsCurrentShipyardEquipmentView(shipIndex)) return;
            bool equipped = after != null;
            ComponentArtEntry moved = equipped ? after : before;
            if (moved == null) return;
            RectTransform destination = FindPortActionAnchor(equipped ? "Shipyard Installed Art " + slot : "Shipyard Inventory Art " + movedInstance);
            if (destination == null) destination = FindPortActionAnchor(equipped ? "Shipyard Equipment Slot " + slot : "Shipyard Equipment Inventory");
            if (destination == null) return;
            RectTransform stats = FindPortActionAnchor("Shipyard Equipment Current Comparison Stats");
            string deltas = PortRefitDeltaText(before, after);
            string receipt = (equipped ? "已装配 · " : "已卸下 · ") + moved.Name;
            if (deltas.Length > 0) receipt += "\n" + deltas;
            PortTransactionMotion.Play(contentRoot, origin, PortTransactionMotion.Anchor(contentRoot, destination),
                ItemIconCatalog.ForComponent(moved), receipt, interfaceFont, this, true, deltas.Length > 0 ? stats : null,
                destinationAnchor: destination, receiptSpace: FindPortActionAnchor("Shipyard Equipment Vessel Surface"),
                receiptNormalized: new Vector2(.5f, .34f));
        }

        private static string PortRefitDeltaText(ComponentArtEntry before, ComponentArtEntry after)
        {
            float[] oldValues = before == null ? new float[5] : new[] { (float)before.HullBonus, before.ArmorBonus, before.FirepowerBonus, before.SpeedBonus, before.VisionBonus };
            float[] newValues = after == null ? new float[5] : new[] { (float)after.HullBonus, after.ArmorBonus, after.FirepowerBonus, after.SpeedBonus, after.VisionBonus };
            string[] labels = { "船体", "装甲", "火力", "航速", "视野" };
            var deltas = new List<string>();
            for (int i = 0; i < labels.Length; i++)
                if (Mathf.Abs(newValues[i] - oldValues[i]) > .001f)
                    deltas.Add(labels[i] + " " + ShipyardComponentComparison.SignedNumber(newValues[i] - oldValues[i]));
            return string.Join("  ·  ", deltas);
        }
    }
}
