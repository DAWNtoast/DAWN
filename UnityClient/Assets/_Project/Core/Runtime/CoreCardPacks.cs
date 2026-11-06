using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    public sealed class CoreCardPackDefinition
    {
        internal CoreCardPackDefinition(string id, string name) { Id = id; Name = name; }
        public string Id { get; }
        public string Name { get; }
        public string SchoolId => Id == "neutral" ? string.Empty : Id;
        public IReadOnlyList<CoreCardPackSchoolRate> DropRates => CoreCardPackCatalog.DropRatesFor(Id);
    }

    public static partial class CoreCardPackCatalog
    {
        public const int CardsPerPack = 3;
        private static readonly IReadOnlyList<CoreCardPackDefinition> all = Array.AsReadOnly(new[]
        {
            new CoreCardPackDefinition("neutral", "普通卡包"),
            new CoreCardPackDefinition("fire", "火焰卡包"),
            new CoreCardPackDefinition("arcane", "秘术卡包"),
            new CoreCardPackDefinition("curse", "诅咒卡包"),
            new CoreCardPackDefinition("tide", "潮汐卡包"),
            new CoreCardPackDefinition("thunder", "雷霆卡包"),
            new CoreCardPackDefinition("blood", "血契卡包"),
        });
        public static IReadOnlyList<CoreCardPackDefinition> All => all;
        public static CoreCardPackDefinition Find(string id)
        {
            foreach (CoreCardPackDefinition pack in all) if (pack.Id == id) return pack;
            return null;
        }
    }

    [Serializable] public sealed class CoreCardPackCountSave { public string id; public int count; }
    [Serializable] public sealed class CoreCardCopySave { public string id; public int count; }

    // Version 2 stores all owned school copies and additional neutral copies.
    // Historic level grants are folded into these records once during migration.
    [Serializable] public sealed class CoreCardPackProgress
    {
        public int version = 2;
        public CoreCardPackCountSave[] packs = Array.Empty<CoreCardPackCountSave>();
        public CoreCardCopySave[] cards = Array.Empty<CoreCardCopySave>();
        public uint randomState;
        // Optional in v2: older saves begin with no pending guarantee.
        // Saturates at one while a fully collected pack has no legal useful copy.
        public int packsWithoutUsableCopy;
    }

    public sealed partial class CoreVoyageSession
    {
        private readonly Dictionary<string, int> cardPacks = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> packCardCopies = new Dictionary<string, int>(StringComparer.Ordinal);
        private Mulberry32Random cardPackRandom = new Mulberry32Random(unchecked((uint)Guid.NewGuid().GetHashCode()));
        private int cardPacksWithoutUsableCopy;

        public int GetCardPackCount(string id) => id != null && cardPacks.TryGetValue(id, out int count) ? count : 0;

        public IReadOnlyList<int> GetCardPackPool(string id)
        {
            CoreCardPackDefinition pack = CoreCardPackCatalog.Find(id);
            if (pack == null) return Array.Empty<int>();
            var cards = new List<int>();
            for (int index = 0; index < cardLibrary.Count; index++)
            {
                CoreBattleCard card = cardLibrary[index];
                if (!IsCardPackCard(index) || pack.Id != "neutral" && card.SchoolId.Length > 0 && card.SchoolId != pack.Id) continue;
                cards.Add(index);
            }
            return cards.AsReadOnly();
        }

        private static bool IsCardPackCard(int index)
        {
            if (index != CanonicalBattleCardIndex(index) || !IsBaseBattleCard(index)) return false;
            CoreBattleCard card = cardLibrary[index];
            if (IsSpecialBattleCard(card.Id) || card.SchoolId.Length > 0 && CoreSchoolUnlockCatalog.Find(card.SchoolId) == null) return false;
            return card.IsAllowedInStage(NavalBattleStage.Preparation) || card.IsAllowedInStage(NavalBattleStage.Naval) ||
                card.IsAllowedInStage(NavalBattleStage.Boarding) && !CoreBoardingCardCatalog.IsLegacyAlias(card);
        }

        public bool GrantCardPacks(string id, int count = 1)
        {
            if (CoreCardPackCatalog.Find(id) == null || count <= 0 || (long)GetCardPackCount(id) + count > int.MaxValue) return false;
            cardPacks[id] = GetCardPackCount(id) + count;
            return true;
        }

        public bool TryOpenCardPack(string id, out int[] cardIndexes, out string reason)
        {
            cardIndexes = Array.Empty<int>();
            reason = Phase != CoreDemoPhase.Port ? "只能在港口打开卡包。" :
                CoreCardPackCatalog.Find(id) == null ? "卡包不存在。" :
                GetCardPackCount(id) <= 0 ? "没有可打开的卡包。" : string.Empty;
            if (reason.Length > 0) return false;
            List<CardPackDrawBucket> buckets = CardPackDrawBuckets(id);
            if (buckets.Count == 0 || buckets.Any(bucket => bucket.Cards.Count == 0))
            { reason = "此卡包没有可用卡牌。"; return false; }
            int totalWeight = buckets.Sum(bucket => bucket.Rate.Weight);
            // Work on a copy until all three rewards fit. Failed actions consume
            // neither inventory nor random draws, including a full integer inventory.
            var random = new Mulberry32Random(cardPackRandom.State);
            var draws = new int[CoreCardPackCatalog.CardsPerPack];
            for (int draw = 0; draw < draws.Length; draw++)
            {
                int categoryRoll = CardPackRandomIndex(random, totalWeight);
                CardPackDrawBucket selected = buckets[buckets.Count - 1];
                foreach (CardPackDrawBucket bucket in buckets)
                {
                    if (categoryRoll < bucket.Rate.Weight) { selected = bucket; break; }
                    categoryRoll -= bucket.Rate.Weight;
                }
                draws[draw] = selected.Cards[CardPackRandomIndex(random, selected.Cards.Count)];
            }
            bool usefulCopy = draws.Any(index => GetBaseBattleCardOwnedCopies(index) < BaseBattleCardCopies);
            if (!usefulCopy && cardPacksWithoutUsableCopy > 0)
            {
                int[] missing = buckets.SelectMany(bucket => bucket.Cards)
                    .Where(index => GetBaseBattleCardOwnedCopies(index) < BaseBattleCardCopies).ToArray();
                if (missing.Length > 0)
                {
                    draws[draws.Length - 1] = missing[CardPackRandomIndex(random, missing.Length)];
                    usefulCopy = true;
                }
            }
            var additions = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (int index in draws)
            {
                string cardId = cardLibrary[index].Id;
                additions.TryGetValue(cardId, out int added);
                additions[cardId] = added + 1;
            }
            foreach (var pair in additions)
            {
                packCardCopies.TryGetValue(pair.Key, out int owned);
                if ((long)owned + pair.Value + NeutralBaseCardCopies(cardLibrary.First(card => card.Id == pair.Key)) > int.MaxValue)
                { reason = "卡牌拥有数量已达上限。"; return false; }
            }
            foreach (var pair in additions)
            {
                packCardCopies.TryGetValue(pair.Key, out int owned);
                packCardCopies[pair.Key] = owned + pair.Value;
            }
            cardPacks[id]--;
            cardPackRandom = random;
            cardPacksWithoutUsableCopy = usefulCopy ? 0 : 1;
            cardIndexes = draws;
            reason = "已打开" + CoreCardPackCatalog.Find(id).Name + "，获得 3 张卡牌。";
            return true;
        }

        public CoreCardPackProgress CaptureCardPackProgress() => new CoreCardPackProgress
        {
            packs = cardPacks.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new CoreCardPackCountSave { id = pair.Key, count = pair.Value }).ToArray(),
            cards = packCardCopies.Where(pair => pair.Value > 0).OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new CoreCardCopySave { id = pair.Key, count = pair.Value }).ToArray(),
            randomState = cardPackRandom.State,
            packsWithoutUsableCopy = cardPacksWithoutUsableCopy,
        };

        public bool RestoreCardPackProgress(CoreCardPackProgress progress, int legacyPirateLevel = 0)
        {
            // A v1 record needs the saved profile's old level. Never infer it from
            // experience that may have increased during this newly loaded session.
            if (legacyPirateLevel < 0 || legacyPirateLevel > MaximumPirateLevel ||
                progress != null && progress.version != 1 && progress.version != 2 ||
                progress != null && (progress.packsWithoutUsableCopy < 0 || progress.packsWithoutUsableCopy > 1) ||
                progress?.version == 1 && legacyPirateLevel == 0 ||
                (progress?.packs?.Length ?? 0) > CoreCardPackCatalog.All.Count ||
                (progress?.cards?.Length ?? 0) > cardLibrary.Count) return false;
            var packs = new Dictionary<string, int>(StringComparer.Ordinal);
            var cards = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (CoreCardPackCountSave item in progress?.packs ?? Array.Empty<CoreCardPackCountSave>())
            {
                if (item == null || CoreCardPackCatalog.Find(item.id) == null || item.count < 0 || packs.ContainsKey(item.id)) return false;
                packs.Add(item.id, item.count);
            }
            var savedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CoreCardCopySave item in progress?.cards ?? Array.Empty<CoreCardCopySave>())
            {
                if (item == null || item.id == null || !savedIds.Add(item.id) || item.count < 0) return false;
                string id = CoreBattleCardAliases.CanonicalId(item.id);
                int index = -1;
                for (int i = 0; i < cardLibrary.Count; i++) if (cardLibrary[i].Id == id) { index = i; break; }
                if (index < 0 || !IsOwnedCardRecord(index) || progress.version == 1 && !IsCardPackCard(index)) return false;
                cards.TryGetValue(id, out int count);
                if ((long)count + item.count + NeutralBaseCardCopies(cardLibrary[index]) > int.MaxValue) return false;
                cards[id] = count + item.count;
            }
            if ((progress == null || progress.version == 1) && legacyPirateLevel > 0)
                for (int index = 0; index < cardLibrary.Count; index++)
                {
                    CoreBattleCard card = cardLibrary[index];
                    CoreSchoolUnlockDefinition school = CoreSchoolUnlockCatalog.Find(card.SchoolId);
                    if (!IsOwnedCardRecord(index) || school == null || school.RequiredLevel > legacyPirateLevel) continue;
                    cards.TryGetValue(card.Id, out int count);
                    if ((long)count + BaseBattleCardCopies > int.MaxValue) return false;
                    cards[card.Id] = count + BaseBattleCardCopies;
                }
            cardPacks.Clear(); foreach (var pair in packs) cardPacks.Add(pair.Key, pair.Value);
            packCardCopies.Clear(); foreach (var pair in cards) packCardCopies.Add(pair.Key, pair.Value);
            if (progress != null) cardPackRandom = new Mulberry32Random(progress.randomState);
            cardPacksWithoutUsableCopy = progress?.packsWithoutUsableCopy ?? 0;
            return true;
        }

        private bool HasPackCard(CoreBattleCard card) => card != null &&
            packCardCopies.TryGetValue(CoreBattleCardAliases.CanonicalId(card.Id), out int count) && count > 0;

        private int CardPackExtraCopies(int index) => packCardCopies.TryGetValue(cardLibrary[index].Id, out int count) ? count : 0;

        private static int NeutralBaseCardCopies(CoreBattleCard card) => card.SchoolId.Length == 0 ? BaseBattleCardCopies : 0;

        private static bool IsOwnedCardRecord(int index) => index == CanonicalBattleCardIndex(index) &&
            IsBaseBattleCard(index) && !IsSpecialBattleCard(cardLibrary[index].Id) &&
            (cardLibrary[index].SchoolId.Length == 0 || CoreSchoolUnlockCatalog.Find(cardLibrary[index].SchoolId) != null);

        private void GrantVictoryCardPack()
        {
            if (Battle == null || Battle.Profile?.TrainingTarget != null || double.IsNaN(pendingSeaChartRewardMultiplier) ||
                double.IsInfinity(pendingSeaChartRewardMultiplier) || pendingSeaChartRewardMultiplier <= 0d) return;
            string id = ActiveEncounter?.Id;
            string pack = null;
            if (Battle.IsBoss || Battle.Profile != null && Battle.Profile.IsBoss)
            {
                string school = CoreBoardingEnemyCatalog.ResolveSchool(id);
                pack = CoreCardPackCatalog.Find(school)?.Id ?? "neutral";
            }
            else if (id == "random_ambush" || id == "random_ambush_warning" ||
                id == "event_reef_open_water_ambush" || id == "event_trade_open_water_ambush" ||
                id == "event_moon_open_water_ambush" || id == "event_reef_coral_ambush" ||
                id == CoreGuidedTutorialContent.AmbushEventId)
                pack = "neutral";
            if (pack == null) return;
            if (GrantCardPacks(pack))
                AddLog("战利品：" + CoreCardPackCatalog.Find(pack).Name + " ×1，可在卡牌工坊打开。");
            else AddLog("卡包数量已达上限，本次卡包未能入账；已有卡包保留。");
        }
    }
}
