using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>
    /// Player-facing catalogue order is independent of saved library indices.
    /// Ordinary / fire / arcane / curse / tide / thunder / blood occupy 1xxx–7xxx;
    /// crew and component cards occupy 8xxx and 9xxx, regardless of their school.
    /// </summary>
    public static class CoreBattleCardNumbering
    {
        private static readonly Dictionary<string, int> numbers = Build();

        public static int Number(CoreBattleCard card) => card == null ? 0 : Number(card.Id);

        public static int Number(string cardId) => !string.IsNullOrEmpty(cardId) &&
            numbers.TryGetValue(CoreBattleCardAliases.CanonicalId(cardId), out int value) ? value : 0;

        public static string Label(CoreBattleCard card)
        {
            int number = Number(card);
            return number == 0 ? string.Empty : number.ToString("D4", CultureInfo.InvariantCulture);
        }

        public static int CompareLibraryIndices(int left, int right)
        {
            int leftNumber = NumberAt(left), rightNumber = NumberAt(right);
            int order = (leftNumber == 0 ? int.MaxValue : leftNumber).CompareTo(
                rightNumber == 0 ? int.MaxValue : rightNumber);
            return order != 0 ? order : left.CompareTo(right);
        }

        public static int Category(CoreBattleCard card)
        {
            if (card == null) return 0;
            if (!string.IsNullOrEmpty(card.RequiredCrew)) return 8;
            if (!string.IsNullOrEmpty(card.RequiredComponent)) return 9;
            return SchoolCategory(card.SchoolId);
        }

        private static int NumberAt(int index) => index < 0 || index >= CoreVoyageSession.CardLibrary.Count
            ? 0 : Number(CoreVoyageSession.CardLibrary[index]);

        private static int SchoolCategory(string school)
        {
            switch (school)
            {
                case "fire": return 2;
                case "arcane": return 3;
                case "curse": return 4;
                case "tide": return 5;
                case "thunder": return 6;
                case "blood": return 7;
                default: return 1;
            }
        }

        private static Dictionary<string, int> Build()
        {
            var cards = new List<CoreBattleCard>();
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary)
                if (card.AllowedStages != NavalBattleStages.None && !CoreBattleCardAliases.IsAlias(card.Id) &&
                    !IsRetiredBoardingIdentity(card)) cards.Add(card);
            cards.Sort(CompareDesigns);
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            var counts = new int[10];
            foreach (CoreBattleCard card in cards)
            {
                int category = Category(card), sequence = ++counts[category];
                if (sequence >= 1000) throw new InvalidOperationException("Battle card number range exhausted: " + category);
                result.Add(card.Id, category * 1000 + sequence);
            }
            // Boarding aliases still occur in old decks. They share the current design's
            // number without occupying another position in the playable catalogue.
            foreach (CoreBattleCard card in CoreVoyageSession.CardLibrary)
                if (IsRetiredBoardingIdentity(card))
                {
                    CoreBattleCard current = CoreBoardingCardCatalog.FindByKey(card.BoardingSpec.Key);
                    if (current != null && result.TryGetValue(current.Id, out int value)) result.Add(card.Id, value);
                }
            return result;
        }

        private static bool IsRetiredBoardingIdentity(CoreBattleCard card) =>
            card.AllowedStages == NavalBattleStages.Boarding && card.RequiredCrew.Length == 0 &&
            card.RequiredComponent.Length == 0 && card.BoardingSpec != null && CoreBoardingCardCatalog.IsLegacyAlias(card);

        private static int CompareDesigns(CoreBattleCard left, CoreBattleCard right)
        {
            int order = Category(left).CompareTo(Category(right));
            if (order != 0) return order;
            // Source cards are collected together by their combat school as well.
            order = SchoolCategory(left.SchoolId).CompareTo(SchoolCategory(right.SchoolId));
            if (order != 0) return order;
            order = FirstStage(left).CompareTo(FirstStage(right));
            if (order != 0) return order;
            order = Role(left).CompareTo(Role(right));
            if (order != 0) return order;
            order = left.Energy.CompareTo(right.Energy);
            return order != 0 ? order : StringComparer.Ordinal.Compare(left.Id, right.Id);
        }

        private static int FirstStage(CoreBattleCard card) => card.IsAllowedInStage(NavalBattleStage.Preparation)
            ? 0 : card.IsAllowedInStage(NavalBattleStage.Naval) ? 1 : 2;

        private static int Role(CoreBattleCard card)
        {
            if (card.Type == "策略") return 0;
            if (card.Type == "机动") return 1;
            if (card.Type == "炮击" || card.Type == "登船" || card.Damage > 0) return 2;
            if (card.Type == "防御") return 3;
            if (card.Type == "维修") return 4;
            return 5;
        }
    }
}
