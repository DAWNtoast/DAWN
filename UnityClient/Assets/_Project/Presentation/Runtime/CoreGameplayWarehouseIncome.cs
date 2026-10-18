using System;
using System.Collections.Generic;
using System.Globalization;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string WarehouseIncomeKey = "ShatteredSail.Warehouse.Income.v1";
        private static readonly HashSet<string> WarehouseIncomeLoginScopes = new HashSet<string>(StringComparer.Ordinal);
        private CoreWarehouseIncome warehouseIncome;
        private CoreVoyageSession warehouseIncomeSession;
        private decimal warehouseIncomeReceipt;
        private RectTransform warehouseIncomeOverlay;
        private VoyageSuspensionRecord warehouseIncomeFailedVoyage;

        private void ProtectVoyageAfterIncomeFailure(VoyageSuspensionRecord suspendedVoyage, string reason)
        {
            // Restoring a parked voyage consumes its snapshot. If the subsequent
            // income transaction fails, preserve that original voyage before
            // leaving; the profile journal will roll its wallet back on retry.
            if (suspendedVoyage != null && !VoyageSuspensionPersistence.TrySave(suspendedVoyage, out string notice))
            {
                warehouseIncomeFailedVoyage = suspendedVoyage;
                interfaceFont = CreateInterfaceFont();
                EnsureEventSystem();
                BlockAfterProfileSaveFailure(reason + "\n航程暂时无法重新保存，已保留在当前会话：" + notice);
                return;
            }
            ReturnToMenuAfterSuspensionFailure(reason);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetWarehouseIncomeLoginScopes() => WarehouseIncomeLoginScopes.Clear();

        private int CountDisplayedWarehouseTreasures()
        {
            int count = 0;
            foreach (CoreWarehouseCollection collection in warehouseCollections.Values)
                for (int slot = 0; slot < CoreWarehouseCollection.DisplaySlotCount; slot++)
                    if (!string.IsNullOrEmpty(collection.DisplayedAt(slot))) count++;
            return count;
        }

        // Run after the suspended voyage restores its wallet. A dedicated profile
        // transaction must not copy voyage cargo into the settled port save.
        private bool TryInitializeWarehouseIncome(DateTime utcNow, out string reason)
        {
            reason = null;
            if (!Application.isPlaying || editorPreviewBuilding || session == null) return true;
            try
            {
                var restored = new CoreWarehouseIncome();
                if (!restored.Restore(PlayerPrefs.GetString(WarehouseIncomeKey, string.Empty)))
                    throw new ProfileSaveException("藏品收益记录无法读取，已保留原存档，请修复后重试。");
                restored.Observe(utcNow, CountDisplayedWarehouseTreasures());
                string scope = PlayerPrefs.GetString(WorldEconomyScopeKey, string.Empty);
                // Returning to the menu, changing ports and rebuilding a scene
                // within this application session do not create another login.
                int reward = WarehouseIncomeLoginScopes.Contains(scope) ? 0 : restored.Claim(utcNow);
                decimal credited = Math.Min((decimal)reward,
                    Math.Max(0m, CoreEconomyBalance.MaximumProfileGold - session.ProfileGold));
                if (!ProfileSaveJournal.TryBegin(out reason)) throw new ProfileSaveException(reason);
                ProfileGoldPersistence.Save(session.ProfileGold + credited);
                PlayerPrefs.SetString(WarehouseIncomeKey, restored.Serialize());
                PlayerPrefs.Save();
                if (!ProfileSaveJournal.TryComplete(out reason)) throw new ProfileSaveException(reason);
                session.GrantCollectionIncome(reward);
                warehouseIncome = restored;
                warehouseIncomeSession = session;
                warehouseIncomeReceipt = credited;
                WarehouseIncomeLoginScopes.Add(scope);
                return true;
            }
            catch (Exception error)
            {
                profileSaveBlocked = true;
                reason = "藏品收益保存未完成：" + error.Message;
                return false;
            }
        }

        // Each successful display/remove action saves the collection. Observe
        // settles the previous count before adopting the new display count.
        private void SaveWarehouseIncome()
        {
            if (warehouseIncome == null || !ReferenceEquals(warehouseIncomeSession, session)) return;
            warehouseIncome.Observe(DateTime.UtcNow, CountDisplayedWarehouseTreasures());
            PlayerPrefs.SetString(WarehouseIncomeKey, warehouseIncome.Serialize());
        }

        private void ShowWarehouseIncomeReceipt()
        {
            if (warehouseIncomeReceipt <= 0m || profileSaveBlocked || warehouseIncomeOverlay != null) return;
            var canvasObject = new GameObject("Warehouse Income Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            warehouseIncomeOverlay = CreateRect("Warehouse Income Overlay", FixedGameViewport.Create(canvas), Vector2.zero, Vector2.zero);
            warehouseIncomeOverlay.anchorMin = Vector2.zero;
            warehouseIncomeOverlay.anchorMax = Vector2.one;
            warehouseIncomeOverlay.offsetMin = warehouseIncomeOverlay.offsetMax = Vector2.zero;
            Image shade = warehouseIncomeOverlay.gameObject.AddComponent<Image>();
            shade.color = new Color(.015f, .025f, .03f, .88f);
            RectTransform panel = CreateFixedPanel("Warehouse Income Receipt", warehouseIncomeOverlay,
                new Vector2(820f, 440f), Vector2.zero, Panel);
            Text title = CreateText("Warehouse Income Title", panel, new Vector2(740f, 54f),
                new Vector2(0f, 155f), 32, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            title.text = "船长收藏室 · 藏品收益";
            Text amount = CreateText("Warehouse Income Amount", panel, new Vector2(740f, 80f),
                new Vector2(0f, 75f), 46, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            amount.text = "+" + warehouseIncomeReceipt.ToString("0.####", CultureInfo.InvariantCulture) + " 金币";
            Text detail = CreateText("Warehouse Income Detail", panel, new Vector2(740f, 120f),
                new Vector2(0f, -30f), 22, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            detail.text = "所有港口已陈列的藏品，每件每现实小时产出 10 金币。\n下次上线自动发放，每次合计最多 300 金币。\n本次收益已存入账户，助你在航海失利后再次起航。";
            Button close = CreateButton("Close Warehouse Income", panel, new Vector2(340f, 62f),
                new Vector2(0f, -150f), Gold, DeepSea, 24);
            close.GetComponentInChildren<Text>(true).text = "收下收益，继续冒险";
            close.navigation = new Navigation { mode = Navigation.Mode.None };
            close.onClick.AddListener(() =>
            {
                warehouseIncomeReceipt = 0m;
                warehouseIncomeOverlay = null;
                canvasObject.SetActive(false);
                Destroy(canvasObject);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(close.gameObject);
        }
    }
}
