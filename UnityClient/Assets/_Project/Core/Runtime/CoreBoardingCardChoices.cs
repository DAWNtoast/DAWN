using System;
using System.Collections.Generic;
using ShatteredSail.Core.Random;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private sealed class BoardingReading
        {
            public readonly List<int> Cards = new List<int>();
            public List<int> Draw;
            public List<int> Discard;
            public uint RandomState;
        }
        private BoardingReading boardingReading;

        private int BRequiredPowder(CoreBattleCard card)
        {
            string key = card?.BoardingSpec?.Key;
            if (key == "fire_05") return Cargo.Medicine > 0 ? 1 : 0;
            return card?.PowderCost ?? 0;
        }

        private int BFixedAmount(CoreBattleCard card, CoreBoardingPlayOptions options) => BFixedAmount(card?.BoardingSpec?.Key, options);
        private int BFixedAmount(string key, CoreBoardingPlayOptions options)
        {
            switch (key)
            {
                case "fire_06": case "fire_09": return 1;
                case "fire_07": case "fire_08": return options?.SelectionIndexes?.Count ?? 0;
                case "fire_15": return Math.Min(4, BStackTotal(BState.SelfBurn));
                case "arcane_04": case "arcane_06": case "arcane_11": return Math.Min(3, Battle.Schools.AvailableMana);
                case "arcane_05": return Cargo.Medicine > 0 ? 2 : 0;
                case "arcane_07": case "arcane_08": case "arcane_09": case "arcane_12": case "arcane_13": case "arcane_14": return 1;
                case "arcane_10": case "arcane_15": return 2;
                case "curse_11": return BState.CurseMarks;
                case "curse_12": return Cargo.Medicine > 0 ? 2 : 0;
                case "curse_13": return 1;
                case "curse_14": case "curse_15": return 2;
                case "tide_05": return Math.Min(6, BState.ExtendableTidePower);
                case "tide_06": case "tide_07": case "tide_08": return BState.TidePower;
                case "tide_09": return 4;
                case "tide_10": return 2;
                case "tide_11": return Cargo.Medicine > 0 ? 6 : 0;
                case "tide_12": return Cargo.Medicine > 0 ? 8 : 0;
                case "tide_13": case "tide_14": return 8;
                case "tide_15": return 6;
                case "thunder_09": return 2;
                case "thunder_13": case "thunder_14": case "thunder_15": return Battle.Schools.ThunderChain;
                case "blood_01": case "blood_03": case "blood_04": case "blood_05": return 3;
                case "blood_02": return 4;
                default: return 0;
            }
        }

        public CoreBoardingPlayChoice GetBoardingPlayChoice(int handIndex, CoreBoardingPlayOptions options = null)
        {
            if (!BActive || handIndex < 0 || handIndex >= Battle.Hand.Count) return null;
            CoreBattleCard card = GetBattleCard(Battle.Hand[handIndex]);
            if (card?.BoardingSpec == null) return null;
            string key = card.BoardingSpec.Key;
            var choice = new CoreBoardingPlayChoice { Title = card.Name, Description = card.Description, SnapshotToken = BSnapshotToken() };
            choice.MinResource = choice.MaxResource = BFixedAmount(card, options);
            switch (key)
            {
                case "fire_06": case "fire_07": case "fire_08": case "fire_09":
                    choice.Kind = CoreBoardingChoiceKind.HandBurn;
                    choice.MinSelection = 1; choice.MaxSelection = key == "fire_07" || key == "fire_08" ? 3 : 1;
                    for (int i = 0; i < Battle.Hand.Count; i++)
                        if (i != handIndex) BAddCandidate(choice, i, Battle.Hand[i]);
                    choice.MaxSelection = Math.Min(choice.MaxSelection, choice.Candidates.Count);
                    break;
                case "arcane_12":
                    choice.Kind = CoreBoardingChoiceKind.HandRetain; choice.MinSelection = choice.MaxSelection = 1;
                    for (int i = 0; i < Battle.Hand.Count; i++)
                        if (i != handIndex && (i >= BState.HeldCards.Count || !BState.HeldCards[i].Retain)) BAddCandidate(choice, i, Battle.Hand[i]);
                    break;
                case "arcane_14":
                    choice.Kind = CoreBoardingChoiceKind.DiscardRecover; choice.MinSelection = choice.MaxSelection = 1;
                    for (int i = 0; i < Battle.DiscardPile.Count; i++)
                    {
                        CoreBattleCard other = GetBattleCard(Battle.DiscardPile[i]);
                        if (other?.SchoolId == "arcane" && other.BoardingSpec?.IsAttack == true) BAddCandidate(choice, i, Battle.DiscardPile[i]);
                    }
                    break;
                case "arcane_09":
                    choice.Kind = CoreBoardingChoiceKind.DrawChoose;
                    BoardingReading reading = BPreviewReading(Battle.Hand[handIndex]);
                    for (int i = 0; i < reading.Cards.Count; i++) BAddCandidate(choice, i, reading.Cards[i]);
                    choice.MinSelection = choice.MaxSelection = Math.Min(card.Draw, Math.Min(reading.Cards.Count, Math.Max(0, 9 - Battle.Hand.Count)));
                    break;
            }
            choice.RequiresChoice = choice.Kind != CoreBoardingChoiceKind.None;
            return choice;
        }

        private void BAddCandidate(CoreBoardingPlayChoice choice, int value, int cardIndex)
        { choice.Candidates.Add(new CoreBoardingChoiceCandidate { Value = value, CardIndex = cardIndex, Label = GetBattleCard(cardIndex)?.Name ?? "卡牌" }); }

        private CoreBoardingPlayOptions BDefaultOptions(int handIndex)
        {
            CoreBoardingPlayChoice choice = GetBoardingPlayChoice(handIndex);
            var options = new CoreBoardingPlayOptions();
            if (choice == null) return options;
            options.SnapshotToken = choice.SnapshotToken;
            for (int i = 0; i < Math.Min(choice.MinSelection, choice.Candidates.Count); i++) options.SelectionIndexes.Add(choice.Candidates[i].Value);
            return options;
        }

        // Every check is read-only. A rejected or cancelled selection cannot pay a cost,
        // move a card, advance the random stream, or count as a successful play.
        public string GetBoardingChoiceUnavailableReason(int handIndex, CoreBoardingPlayOptions options)
        {
            if (Phase != CoreDemoPhase.Battle || !BActive) return "当前不是接舷战";
            if (handIndex < 0 || handIndex >= Battle.Hand.Count) return "手牌不存在";
            CoreBattleCard card = GetBattleCard(Battle.Hand[handIndex]);
            if (card?.BoardingSpec == null) return "卡牌不存在";
            string tutorialRestriction = GuidedBossCardRestriction(card);
            if (tutorialRestriction.Length > 0) return tutorialRestriction;
            if (options != null && options.SelectionIndexes == null) return "卡牌选择数据无效";
            string reason = GetBattleCardUnlockReason(card);
            if (reason.Length > 0) return reason;
            if (!card.IsAllowedInStage(Battle.Stage)) return "该牌不适用于当前阶段";
            if (!HasBattleCrew(card.RequiredCrew)) return "所需船员不在队伍中";
            if (!HasBattleComponent(card.RequiredComponent)) return "所需部件未装备";
            if (BHandEnergyCost(handIndex) > Battle.Energy) return "能量不足";
            if (BState.FreePowderUses <= 0 && BRequiredPowder(card) > Cargo.Powder) return "火药不足";
            if (card.BoardingSpec.Key == "basic_06" && SchoolPlayerHealth >= SchoolPlayerMaximum) return "当前生命已满";
            int amount = BFixedAmount(card, options);
            reason = FATUnavailableReason(card, amount);
            if (reason.Length > 0) return reason;
            reason = CTBUnavailableReason(card, amount);
            if (reason.Length > 0) return reason;
            CoreBoardingPlayChoice choice = GetBoardingPlayChoice(handIndex, options);
            if (options != null && options.SnapshotToken != 0 && options.SnapshotToken != choice.SnapshotToken) return "战斗状态已变化，请重新选择";
            if (!choice.RequiresChoice) return string.Empty;
            if (choice.Candidates.Count < choice.MinSelection || choice.MaxSelection < choice.MinSelection) return "没有足够的可选卡牌";
            if (options == null) return "请先选择卡牌";
            if (options.SelectionIndexes == null || options.SelectionIndexes.Count < choice.MinSelection || options.SelectionIndexes.Count > choice.MaxSelection) return "请选择规定数量的卡牌";
            var selected = new HashSet<int>();
            foreach (int value in options.SelectionIndexes)
                if (!selected.Add(value) || !choice.Candidates.Exists(candidate => candidate.Value == value)) return "选择的卡牌已失效";
            return string.Empty;
        }

        private int BSnapshotToken()
        {
            unchecked
            {
                int hash = 17;
                foreach (int value in new[] { BState.Revision, Battle.Turn, Battle.Energy, Battle.PlayerBoardingHealth,
                    Battle.EnemyBoardingHealth, Battle.PlayerBlock, Battle.EnemyBlock, Cargo.Powder, Cargo.Medicine,
                    Battle.Schools.AvailableMana, BState.CurseMarks, BState.TidePower, Battle.Schools.ThunderChain }) hash = hash * 31 + value;
                foreach (var pile in new[] { Battle.Hand, Battle.DrawPile, Battle.DiscardPile })
                { hash = hash * 31 + pile.Count; foreach (int value in pile) hash = hash * 31 + value; }
                hash = hash * 31 + (int)voyageRandom.State;
                return hash == 0 ? 1 : hash;
            }
        }

        private BoardingReading BPreviewReading(int playedCardIndex)
        {
            var reading = new BoardingReading { Draw = new List<int>(Battle.DrawPile), Discard = new List<int>(Battle.DiscardPile) };
            reading.Discard.Add(playedCardIndex);
            var random = new Mulberry32Random(voyageRandom.State);
            for (int n = 0; n < 5; n++)
            {
                if (reading.Draw.Count == 0)
                {
                    if (reading.Discard.Count == 0) break;
                    reading.Draw.AddRange(reading.Discard); reading.Discard.Clear();
                    for (int i = reading.Draw.Count - 1; i > 0; i--)
                    { int j = (int)(random.NextDouble() * (i + 1)); int temp = reading.Draw[i]; reading.Draw[i] = reading.Draw[j]; reading.Draw[j] = temp; }
                }
                int top = reading.Draw.Count - 1;
                reading.Cards.Add(reading.Draw[top]); reading.Draw.RemoveAt(top);
            }
            reading.RandomState = random.State;
            return reading;
        }

        private void BPrepareReading(int playedCardIndex) => boardingReading = BPreviewReading(playedCardIndex);
        private void BReadSelected(CoreBoardingPlayOptions options)
        {
            BoardingReading reading = boardingReading; boardingReading = null;
            if (reading == null) return;
            Battle.DrawPile.Clear(); Battle.DrawPile.AddRange(reading.Draw);
            Battle.DiscardPile.Clear(); Battle.DiscardPile.AddRange(reading.Discard);
            voyageRandom = new Mulberry32Random(reading.RandomState);
            var selected = new HashSet<int>(options.SelectionIndexes);
            foreach (int slot in options.SelectionIndexes) Battle.Hand.Add(reading.Cards[slot]);
            // Top is the last list item. Inserting each unselected card at zero
            // preserves its original top-to-bottom relative order at the bottom.
            for (int i = 0; i < reading.Cards.Count; i++) if (!selected.Contains(i)) Battle.DrawPile.Insert(0, reading.Cards[i]);
            BSyncHeldCards();
        }

        private void BSealSelected(CoreBoardingPlayOptions options, bool discount)
        {
            foreach (var held in BState.SelectedHeldCards)
            { held.Retain = true; held.RetainUsed = false; held.Discount = discount ? 1 : 0; held.DiscountTurn = Battle.Turn + 1; }
        }

        private void BRecoverSelected(CoreBoardingPlayOptions options)
        {
            int slot = options.SelectionIndexes[0];
            int card = Battle.DiscardPile[slot]; Battle.DiscardPile.RemoveAt(slot); Battle.Hand.Add(card); BSyncHeldCards();
        }

        private void BAddRetainedGuard(int amount)
        {
            BState.RetainedGuardBatches.Add(new CoreBoardingRetainedGuard { Amount = amount, Expiry = Battle.Turn + 1 });
            BState.ArcaneRetainedGuard = SchoolSum(BState.ArcaneRetainedGuard, amount);
        }

        private int BRemainingGuardAtNewTurn()
        {
            BState.RetainedGuardBatches.RemoveAll(batch => batch.Expiry < Battle.Turn || batch.Amount <= 0);
            int total = 0; foreach (var batch in BState.RetainedGuardBatches) total = SchoolSum(total, batch.Amount);
            BState.ArcaneRetainedGuard = total;
            return Math.Min(Battle.PlayerBlock, total);
        }
    }
}
