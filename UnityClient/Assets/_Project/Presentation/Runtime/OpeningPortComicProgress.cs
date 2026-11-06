using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Campaign-scoped reading acknowledgements and the unfinished opening/arrival page.</summary>
    public sealed class OpeningPortComicProgress
    {
        public const string PlayerPrefsKey = "ShatteredSail.OpeningPortComics.Progress.v1";
        public const string OpeningKey = "opening";
        private readonly string profileScope;
        private readonly Func<string> load;
        private readonly Action<string> save;

        [Serializable]
        private sealed class Record
        {
            public int version = 1;
            public string profileScope;
            public string[] readKeys;
            public string pendingStoryKey;
            public int pendingPage;
            public int pendingPanel;
        }

        public OpeningPortComicProgress(string profileScope, Func<string> load, Action<string> save)
        {
            if (string.IsNullOrWhiteSpace(profileScope)) throw new ArgumentException("A campaign scope is required.", nameof(profileScope));
            this.profileScope = profileScope;
            this.load = load ?? throw new ArgumentNullException(nameof(load));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
        }

        public static OpeningPortComicProgress ForCurrentProfile()
        {
            string scope = PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey, string.Empty);
            if (string.IsNullOrEmpty(scope)) return null;
            return new OpeningPortComicProgress(scope,
                () => PlayerPrefs.GetString(PlayerPrefsKey, string.Empty), json =>
                {
                    if (PlayerPrefs.GetString(CoreGameplayDemoController.WorldEconomyScopeKey, string.Empty) != scope) return;
                    PlayerPrefs.SetString(PlayerPrefsKey, json);
                    PlayerPrefs.Save();
                });
        }

        public static string ArrivalKey(string id) => "arrival:" + id;
        public string PendingStoryKey => Read().pendingStoryKey;
        public int PendingPage => Read().pendingPage;
        public int PendingPanel => Read().pendingPanel;
        public bool HasRead(string storyKey) => Read().readKeys.Contains(storyKey);

        public bool TryQueue(string storyKey)
        {
            if (PageCount(storyKey) == 0) return false;
            Record record = Read();
            if (record.readKeys.Contains(storyKey) || !string.IsNullOrEmpty(record.pendingStoryKey)) return false;
            record.pendingStoryKey = storyKey;
            record.pendingPage = 0;
            record.pendingPanel = 0;
            Write(record);
            return true;
        }

        // A page is acknowledged only after a real advance. Stale callbacks cannot
        // skip the resumed page, and missing art never consumes its reading slot.
        public bool Advance(string storyKey, int page)
        {
            Record record = Read();
            if (record.pendingStoryKey != storyKey || record.pendingPage != page || PageCount(storyKey) == 0) return false;
            FinishPage(record, storyKey, page);
            Write(record);
            return true;
        }

        // Source sheets stay stable for legacy saves. Only the last panel acknowledges
        // its sheet, so interrupted playback resumes the exact unread panel.
        public bool AdvancePanel(string storyKey, int page, int panel)
        {
            Record record = Read();
            if (record.pendingStoryKey != storyKey || record.pendingPage != page ||
                record.pendingPanel != panel || panel < 0 || panel >= ComicPanelCatalog.QuadPanelCount ||
                PageCount(storyKey) == 0) return false;
            if (panel + 1 < ComicPanelCatalog.QuadPanelCount) record.pendingPanel++;
            else FinishPage(record, storyKey, page);
            Write(record);
            return true;
        }

        private static void FinishPage(Record record, string storyKey, int page)
        {
            record.pendingPanel = 0;
            if (page + 1 < PageCount(storyKey)) record.pendingPage++;
            else
            {
                record.readKeys = record.readKeys.Concat(new[] { storyKey }).Distinct(StringComparer.Ordinal).ToArray();
                record.pendingStoryKey = string.Empty;
                record.pendingPage = 0;
            }
        }

        public static int PageCount(string storyKey)
        {
            if (storyKey == OpeningKey) return OpeningComicCatalog.Pages.Count;
            if (storyKey == null || !storyKey.StartsWith("arrival:", StringComparison.Ordinal)) return 0;
            return ArrivalComicCatalog.FindById(storyKey.Substring("arrival:".Length))?.Pages.Count ?? 0;
        }

        private Record Read()
        {
            Record record;
            try { record = JsonUtility.FromJson<Record>(load() ?? string.Empty); }
            catch (ArgumentException) { record = null; }
            if (record == null || record.version != 1 || record.profileScope != profileScope)
                record = new Record { profileScope = profileScope };
            record.readKeys = (record.readKeys ?? Array.Empty<string>()).Where(key => PageCount(key) > 0)
                .Distinct(StringComparer.Ordinal).ToArray();
            int count = PageCount(record.pendingStoryKey);
            if (count == 0 || record.pendingPage < 0 || record.pendingPage >= count || record.readKeys.Contains(record.pendingStoryKey))
            {
                record.pendingStoryKey = string.Empty;
                record.pendingPage = 0;
                record.pendingPanel = 0;
            }
            // A legacy record has no panel field and naturally resumes at panel zero.
            if (record.pendingPanel < 0 || record.pendingPanel >= ComicPanelCatalog.QuadPanelCount)
                record.pendingPanel = 0;
            return record;
        }

        private void Write(Record record)
        {
            record.readKeys = record.readKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
            save(JsonUtility.ToJson(record));
        }
    }
}
