using System;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        public const string CardPackProgressKey = "ShatteredSail.CardPacks.v1";
        private const string CardPackReadFailureNotice = "卡包与卡牌收藏档案无法读取或版本不兼容，原档案已保留。请修复后重新载入游戏。";
        private CoreVoyageSession cardPackProgressOwner;
        private string savedCardPackProgress;

        private bool RestoreCardPacks()
        {
            CoreCardPackProgress progress = null;
            try
            {
                if (PlayerPrefs.HasKey(CardPackProgressKey))
                {
                    string json = PlayerPrefs.GetString(CardPackProgressKey, string.Empty);
                    if (string.IsNullOrWhiteSpace(json) || !HasCardPackInventoryArrays(json)) return RejectCardPackProgress();
                    // Explicit sentinels distinguish a missing field from a valid empty inventory.
                    progress = new CoreCardPackProgress { version = 0, packs = null, cards = null };
                    JsonUtility.FromJsonOverwrite(json, progress);
                    if ((progress.version != 1 && progress.version != 2) || progress.packs == null || progress.cards == null)
                        return RejectCardPackProgress();
                }
                int legacyLevel = 0;
                if ((progress == null || progress.version == 1) && !TryReadLegacyCardCollectionLevel(out legacyLevel))
                    return RejectCardPackProgress();
                if (!session.RestoreCardPackProgress(progress, legacyLevel)) return RejectCardPackProgress();
            }
            catch (ArgumentException) { return RejectCardPackProgress(); }
            cardPackProgressOwner = session;
            // Mark legacy imports dirty so the first journal-protected voyage/profile
            // save commits v2 before a later level gain could be mistaken for old ownership.
            savedCardPackProgress = progress?.version == 2 ? JsonUtility.ToJson(session.CaptureCardPackProgress()) : null;
            return true;
        }

        private bool TryReadLegacyCardCollectionLevel(out int level)
        {
            level = 1;
            if (!PlayerPrefs.HasKey(TreasureProgressKey)) return true;
            string json = PlayerPrefs.GetString(TreasureProgressKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return false;
            var legacy = new CoreTreasureProgress { pirateExperience = -1 };
            JsonUtility.FromJsonOverwrite(json, legacy);
            if (legacy.pirateExperience < 0) return false;
            for (int candidate = 2; candidate <= session.MaximumPirateLevel; candidate++)
                if (legacy.pirateExperience >= CoreVoyageSession.PirateExperienceForLevel(candidate)) level = candidate;
            return true;
        }

        private bool RejectCardPackProgress()
        {
            BlockAfterProfileSaveFailure(CardPackReadFailureNotice);
            return false;
        }

        private static bool HasCardPackInventoryArrays(string json)
        {
            // Unity normalizes an explicit null array to [], which must not turn
            // a damaged inventory into an apparently valid empty collection.
            int depth = 0, packs = 0, cards = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char current = json[i];
                if (current == '"')
                {
                    int start = ++i;
                    while (i < json.Length && json[i] != '"')
                    {
                        if (json[i] == '\\') i++;
                        i++;
                    }
                    if (i >= json.Length) return false;
                    if (depth != 1) continue;
                    string key = json.Substring(start, i - start);
                    if (key != "packs" && key != "cards") continue;
                    int value = i + 1;
                    while (value < json.Length && char.IsWhiteSpace(json[value])) value++;
                    if (value >= json.Length || json[value] != ':') continue;
                    value++;
                    while (value < json.Length && char.IsWhiteSpace(json[value])) value++;
                    if (value >= json.Length || json[value] != '[') return false;
                    if (key == "packs") packs++; else cards++;
                }
                else if (current == '{' || current == '[') depth++;
                else if (current == '}' || current == ']') depth--;
            }
            return depth == 0 && packs == 1 && cards == 1;
        }

        private bool CardPackProgressChanged() => cardPackProgressOwner != null &&
            (!ReferenceEquals(cardPackProgressOwner, session) ||
             savedCardPackProgress != JsonUtility.ToJson(session.CaptureCardPackProgress()));

        private void SaveCardPacks()
        {
            if (profileSaveBlocked) throw new ProfileSaveException(profileSaveFailure ?? CardPackReadFailureNotice);
            string json = JsonUtility.ToJson(session.CaptureCardPackProgress());
            PlayerPrefs.SetString(CardPackProgressKey, json);
            cardPackProgressOwner = session;
            savedCardPackProgress = json;
        }

        private static string WorkshopOwnedCardCount(int count) => count > 9 ? "9+" : Mathf.Max(0, count).ToString();
    }
}
