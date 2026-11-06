using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Read pages belong to a campaign, independently of its current voyage or picture revision.</summary>
    public sealed class LegendaryFleetComicReadProgress
    {
        public const string PlayerPrefsKey = "ShatteredSail.LegendaryFleetComics.Read.v1";
        private readonly string profileScope;
        private readonly Func<string> load;
        private readonly Action<string> save;

        [Serializable]
        private sealed class Record
        {
            public int version = 1;
            public string profileScope;
            public string[] readKeys;
        }

        public LegendaryFleetComicReadProgress(string profileScope, Func<string> load, Action<string> save)
        {
            if (string.IsNullOrWhiteSpace(profileScope)) throw new ArgumentException("A campaign scope is required.", nameof(profileScope));
            this.profileScope = profileScope;
            this.load = load ?? throw new ArgumentNullException(nameof(load));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public static LegendaryFleetComicReadProgress ForCurrentProfile()
        {
            string scope = PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey, string.Empty);
            if (string.IsNullOrEmpty(scope)) return null;
            return new LegendaryFleetComicReadProgress(scope,
                () => PlayerPrefs.GetString(PlayerPrefsKey, string.Empty),
                json =>
                {
                    // An outgoing controller must never write into a newly started campaign.
                    if (PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey, string.Empty) != scope) return;
                    PlayerPrefs.SetString(PlayerPrefsKey, json);
                    // This is a standalone reading acknowledgement, not a port reward save.
                    PlayerPrefs.Save();
                });
        }

        public bool HasReadStoryKey(string storyKey) =>
            TryCanonicalKey(storyKey, out string key) && ReadKeys().Contains(key);

        public bool MarkReadStoryKey(string storyKey) => ImportReadStoryKeys(new[] { storyKey });

        public bool ImportReadStoryKeys(IEnumerable<string> storyKeys)
        {
            HashSet<string> keys = ReadKeys();
            bool changed = false;
            foreach (string storyKey in storyKeys ?? Array.Empty<string>())
                if (TryCanonicalKey(storyKey, out string key)) changed |= keys.Add(key);
            if (!changed) return false;
            save(JsonUtility.ToJson(new Record {
                profileScope = profileScope, readKeys = keys.OrderBy(key => key, StringComparer.Ordinal).ToArray()
            }));
            return true;
        }

        private HashSet<string> ReadKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            Record record;
            try { record = JsonUtility.FromJson<Record>(load() ?? string.Empty); }
            catch (ArgumentException) { return keys; }
            if (record == null || record.version != 1 || record.profileScope != profileScope || record.readKeys == null) return keys;
            foreach (string stored in record.readKeys)
                if (TryCanonicalKey(stored, out string key)) keys.Add(key);
            return keys;
        }

        private static bool TryCanonicalKey(string storyKey, out string key)
        {
            key = null;
            if (string.IsNullOrEmpty(storyKey)) return false;
            string[] parts = storyKey.Split(':');
            int phaseIndex;
            if (parts.Length == 2) phaseIndex = 0;
            else if (parts.Length == 4 && parts[0] == "legendary" && parts[1].Length > 1 && parts[1][0] == 'v' &&
                int.TryParse(parts[1].Substring(1), out int version) && version > 0) phaseIndex = 2;
            else return false;
            string phase = parts[phaseIndex], encounterId = parts[phaseIndex + 1];
            if ((phase != "before" && phase != "after") || LegendaryFleetComicCatalog.Find(encounterId) == null) return false;
            key = phase + ":" + encounterId;
            return true;
        }
    }
}
