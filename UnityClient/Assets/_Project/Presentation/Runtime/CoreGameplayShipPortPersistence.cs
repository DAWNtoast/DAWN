using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string ShipPortProgressKey = "ShatteredSail.ShipPortProgress.v1";
        private const string ShipPortReadFailureNotice = "船只停泊档案无法读取或版本不兼容，原档案已保留。请修复后重新载入游戏。";

        private bool RestoreShipPorts(int activeShipIndex)
        {
            CoreShipPortProgress progress = null;
            if (PlayerPrefs.HasKey(ShipPortProgressKey))
            {
                try
                {
                    string json = PlayerPrefs.GetString(ShipPortProgressKey, string.Empty);
                    if (string.IsNullOrWhiteSpace(json)) return false;
                    progress = JsonUtility.FromJson<CoreShipPortProgress>(json);
                    if (progress == null || progress.version != 1 || progress.ships == null) return false;
                }
                catch (ArgumentException) { return false; }
            }
            var owned = new List<int> { 0 };
            for (int index = 1; index < CoreVoyageSession.Ships.Count; index++)
                if (PlayerPrefs.GetInt(ShipOwnedKey(index), 0) == 1) owned.Add(index);
            return session.RestoreShipPortProgress(progress, owned, activeShipIndex);
        }

        private void SaveShipPorts()
        {
            PlayerPrefs.SetString(ShipPortProgressKey, JsonUtility.ToJson(session.CaptureShipPortProgress()));
        }
    }
}
