using System;
using UnityEditor;
using UnityEngine;

namespace ShatteredSail.Editor
{
    public static partial class GameplayPreviewSceneBuilder
    {
        public static string[] MarketQuantityReferencePaths => new[]
        {
            "UI_Market.unity",
            "UIMarket/UIMarket_Buy.unity",
            "UIMarket/UIMarket_Sell.unity",
            "UIMarket/UIMarket_TransactionComplete.unity",
        };

        public static void SyncAndCaptureMarketQuantityFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Market quantity previews require edit mode.");
            foreach (string reference in MarketQuantityReferencePaths)
                SyncDepartureProvisionScene(reference, "Artifacts/MarketQuantity20261002/preview-backup");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MARKET_QUANTITY_PREVIEW_SYNC: updated four existing market ledgers; authored backgrounds retained.");
            UIPreviewSceneAcceptance.CaptureMarketQuantityFromCommandLine();
        }

        public static void SyncMarketLayoutFromCommandLine()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Market layout previews require edit mode.");
            foreach (string reference in MarketQuantityReferencePaths)
                SyncDepartureProvisionScene(reference, "Artifacts/MarketCalm20261011/preview-backup");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("MARKET_LAYOUT_PREVIEW_SYNC: updated four existing market previews.");
        }
    }
}
