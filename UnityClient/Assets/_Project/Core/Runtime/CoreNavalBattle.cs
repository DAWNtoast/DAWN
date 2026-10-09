using System;
using System.Collections.Generic;
using System.Text;

namespace ShatteredSail.Core.Gameplay
{
    public enum NavalBattleStage { Preparation, Naval, Boarding }
    public enum NavalBattleAlignment { HeadOn, EnemyBroadside, Parallel }
    public enum NavalBoardingApproach { Alongside, Quarter }
    public enum NavalBoardingConnection { Planned, RamBowToBow, RamBowToBroadside }

    [Flags]
    public enum NavalBattleStages
    {
        None = 0,
        Preparation = 1,
        Naval = 2,
        Boarding = 4,
        Ship = Preparation | Naval,
        All = Ship | Boarding,
    }

    public enum NavalBattleCardAction
    {
        Standard, Cannon, Turn, Polish, Load, Approach, Retreat, Circle, Parallel,
        Navigator, PrecisionLoad, Sabotage, FireRounds,
    }

    /// <summary>Semantic effect rows shared by card faces, dossiers and comparison previews.</summary>
    public static class CoreBattleCardText
    {
        private static readonly string[] EffectClauseStarts =
        {
            "格挡", "船员格挡", "抽 ", "恢复 ", "修复 ", "炮战修复 ", "白刃战恢复 ",
            "船体伤害 ", "船员伤害 ", "敌方船员 ", "消耗 ",
        };

        /// <summary>
        /// Keeps a condition and its result together, then starts a row for the next effect.
        /// Commas inside maneuvers, limits and healing/supply comparisons stay intact.
        /// The result is idempotent; the consuming Text control still wraps rows to its width.
        /// </summary>
        public static string FormatEffects(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
            StringBuilder text = new StringBuilder(normalized.Length + 16);
            for (int index = 0; index < normalized.Length; index++)
            {
                char current = normalized[index];
                text.Append(current);
                int next = index + 1;
                while (next < normalized.Length && normalized[next] == ' ') next++;
                if (next >= normalized.Length || normalized[next] == '\n') continue;
                bool sentenceEnd = current == '；' || current == '。' || current == '！' || current == '？';
                if (sentenceEnd || current == '，' && StartsEffectClause(normalized, next))
                    text.Append('\n');
            }
            return text.ToString();
        }

        private static bool StartsEffectClause(string value, int start)
        {
            foreach (string prefix in EffectClauseStarts)
                if (value.Length - start >= prefix.Length &&
                    string.CompareOrdinal(value, start, prefix, 0, prefix.Length) == 0) return true;
            return false;
        }
    }

    public sealed class CoreBattleCard
    {
        // The original constructor and first eight library positions remain save compatible.
        public CoreBattleCard(string id, string name, string type, int energy, int damage, int block,
            int repair, int draw, int powderCost, string description,
            NavalBattleStages allowedStages = NavalBattleStages.All,
            NavalBattleCardAction action = NavalBattleCardAction.Standard,
            string requiredCrew = "", string artId = "", int cannonballCost = 0,
            string requiredComponent = "", int upgradeLevel = 0)
        {
            Id = id;
            Name = name;
            Type = type;
            Energy = energy;
            Damage = damage;
            Block = block;
            Repair = repair;
            Draw = draw;
            PowderCost = powderCost;
            Description = CoreBattleCardText.FormatEffects(description);
            AllowedStages = allowedStages;
            Action = action;
            RequiredCrew = requiredCrew ?? string.Empty;
            ArtId = allowedStages == NavalBattleStages.None ? string.Empty : string.IsNullOrEmpty(artId) ? id : artId;
            CannonballCost = cannonballCost;
            RequiredComponent = requiredComponent ?? string.Empty;
            UpgradeLevel = Math.Max(0, Math.Min(3, upgradeLevel));
        }

        public string Id { get; }
        public string Name { get; }
        public string Type { get; }
        public int Energy { get; }
        public int Damage { get; }
        public int Block { get; }
        public int Repair { get; }
        public int Draw { get; }
        public int PowderCost { get; }
        public int CannonballCost { get; }
        public string Description { get; }
        public string RequiredCrew { get; }
        public string RequiredComponent { get; }
        public int UpgradeLevel { get; }
        public int NumericalMultiplier => UpgradeLevel >= 1 ? CoreBattleUpgradeBalance.NumericalMultiplier : 1;
        public int MovementMeters => BaseMovement(Action) * NumericalMultiplier;
        public int StrategyAmount => (Action == NavalBattleCardAction.Polish ? 2 : Action == NavalBattleCardAction.PrecisionLoad ? 8 :
            Action == NavalBattleCardAction.Load ? 4 : Action == NavalBattleCardAction.FireRounds ? 2 : 0) * NumericalMultiplier;
        public int StrategyLimit => (Action == NavalBattleCardAction.Polish ? 6 : Action == NavalBattleCardAction.FireRounds ? 4 :
            Action == NavalBattleCardAction.Load || Action == NavalBattleCardAction.PrecisionLoad ? 12 : 0) * NumericalMultiplier;
        public int BurnDamage => 5 * NumericalMultiplier;
        public int BurnDuration => 2;
        public string ArtId { get; }
        public NavalBattleStages AllowedStages { get; }
        public NavalBattleCardAction Action { get; }
        public bool IsAllowedInStage(NavalBattleStage stage) =>
            (int)stage >= 0 && (int)stage <= 2 && (AllowedStages & (NavalBattleStages)(1 << (int)stage)) != 0;

        private static int BaseMovement(NavalBattleCardAction action)
        {
            switch (action)
            {
                case NavalBattleCardAction.Approach: return 150;
                case NavalBattleCardAction.Retreat: return 130;
                case NavalBattleCardAction.Turn: return 80;
                case NavalBattleCardAction.Circle:
                case NavalBattleCardAction.Parallel: return 60;
                case NavalBattleCardAction.Navigator: return 180;
                default: return 0;
            }
        }
    }

    public sealed class CoreBattleState
    {
        private readonly List<int>[] drawPiles = { new List<int>(), new List<int>(), new List<int>() };
        private readonly List<int>[] discardPiles = { new List<int>(), new List<int>(), new List<int>() };
        private readonly Dictionary<string, int> successfulCardUses = new Dictionary<string, int>(StringComparer.Ordinal);

        public string EnemyName { get; set; }
        public int EnemyHull { get; set; }
        public int EnemyMaxHull { get; set; }
        public int EnemyBlock { get; set; }
        public int PlayerBlock { get; set; }
        // Actual absorption before end-of-turn guard expiration, for hit feedback.
        public int LastEnemyGuardAbsorbed { get; internal set; }
        public int Energy { get; set; }
        public int MaxEnergy { get; set; }
        public int Turn { get; set; }
        public int PhaseTurn { get; set; }
        public bool IsBoss { get; set; }
        public CoreBattleProfile Profile { get; set; }
        public NavalBattleStage Stage { get; set; }
        public float DistanceMeters { get; set; }
        public NavalBattleAlignment Alignment { get; set; }
        public NavalBoardingApproach PlannedBoardingApproach { get; set; }
        public NavalBoardingApproach BoardingApproach { get; set; }
        public NavalBoardingConnection BoardingConnection { get; set; }
        public bool PlayerMovingTowardEnemy { get; set; }
        public bool EnemyMovingTowardPlayer { get; set; }
        public int PlayerBoardingHealth { get; set; }
        public int PlayerMaxBoardingHealth { get; set; }
        public int PlayerOrdinaryCrew { get; internal set; }
        public int EnemyOrdinaryCrew { get; internal set; }
        public bool BoardingInitiatedByEnemy { get; internal set; }
        public uint DecisionSeed { get; internal set; }
        public int EnemyBoardingHealth { get; set; }
        public int EnemyMaxBoardingHealth { get; set; }
        public int PlayerLoadedShot { get; set; }
        public int PlayerFireRounds { get; set; }
        public int EnemyLoadedShot { get; set; }
        public int EnemyBurnTurns { get; set; }
        public int EnemyBurnDamage { get; set; } = 5;
        public int PlayerFireRoundDamage { get; set; } = 5;
        public int PlayerDeckBuff { get; set; }
        public float PlayerBowQuality { get; set; }
        public float PlayerHullQuality { get; set; }
        public float EnemyBowQuality { get; set; }
        public float EnemyHullQuality { get; set; }
        public int CardsPlayedThisTurn { get; set; }
        public List<int> DrawPile => drawPiles[(int)Stage];
        public List<int> DiscardPile => discardPiles[(int)Stage];
        public List<int> Hand { get; } = new List<int>();
        public string LastAction { get; set; }
        internal List<int> DrawPileFor(NavalBattleStage stage) => drawPiles[(int)stage];
        internal int SuccessfulCardUses(string id) => id != null && successfulCardUses.TryGetValue(id, out int count) ? count : 0;
        internal void RecordSuccessfulCardUse(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            successfulCardUses[id] = (int)Math.Min(int.MaxValue, (long)SuccessfulCardUses(id) + 1);
        }
        internal void BeginNewCardTurn() => successfulCardUses.Clear();
    }

    public sealed partial class CoreVoyageSession
    {
        private static readonly IReadOnlyList<CoreBattleCard> cardLibrary = CreateBattleCardLibrary();

        private static IReadOnlyList<CoreBattleCard> CreateBattleCardLibrary()
        {
            List<CoreBattleCard> result = new List<CoreBattleCard>(new[]
        {
            new CoreBattleCard("card_chain_shot", "链弹", "炮击", 1, 8, 0, 0, 0, 0, "350 米内：8 点船体伤害；消耗 1 炮弹。", NavalBattleStages.Naval, NavalBattleCardAction.Cannon, cannonballCost: 1),
            new CoreBattleCard("card_full_sail_turn", "满帆转向", "机动", 1, 0, 4, 0, 1, 0, "转向敌舰侧舷，靠近 80 米并进入冲撞航行；格挡 +4；抽 1 张。", NavalBattleStages.Ship, NavalBattleCardAction.Turn),
            new CoreBattleCard("card_evasive_maneuver", "规避机动", "机动", 1, 0, 8, 0, 0, 0, "格挡 +8；保留当前航向与距离。", NavalBattleStages.Ship),
            new CoreBattleCard("card_boarding_hook", "钩索绊敌", "登船", 1, 6, 3, 0, 0, 0, "敌方船员 -6，格挡 +3。", NavalBattleStages.Boarding),
            new CoreBattleCard("card_cutlass_raid", "弯刀突袭", "登船", 2, 16, 0, 0, 0, 0, "敌方船员 -16。", NavalBattleStages.Boarding),
            new CoreBattleCard("card_emergency_repair", "紧急修理", "维修", 1, 0, 0, 6, 0, 0, "修复 6 点船体。", NavalBattleStages.Ship),
            new CoreBattleCard("card_cannon_barrage", "炮火齐射", "炮击", 2, 18, 0, 0, 0, 1, "350 米内：18 点船体伤害；消耗 1 火药、1 炮弹。", NavalBattleStages.Naval, NavalBattleCardAction.Cannon, cannonballCost: 1),
            new CoreBattleCard("card_brace", "稳固船身", "防御", 0, 0, 3, 0, 0, 0, "格挡 +3；准备阶段的格挡保留到首轮炮战。", NavalBattleStages.Ship),
            new CoreBattleCard("card_deck_polish", "打磨甲板", "防御", 1, 0, 0, 0, 0, 0, "本场船体防御牌额外 +2 格挡，可叠至 +6。", NavalBattleStages.Ship, NavalBattleCardAction.Polish, artId: "card_brace"),
            new CoreBattleCard("card_load_shot", "装填弹药", "策略", 1, 0, 0, 0, 0, 0, "下次炮击伤害 +4，可叠至 +12；发炮时消耗装填增益。", NavalBattleStages.Ship, NavalBattleCardAction.Load, artId: "card_cannon_barrage"),
            new CoreBattleCard("card_approach", "迎敌推进", "机动", 1, 0, 0, 0, 0, 0, "朝敌舰靠近 150 米，进入冲撞航行状态。", NavalBattleStages.Ship, NavalBattleCardAction.Approach, artId: "card_full_sail_turn"),
            new CoreBattleCard("card_retreat", "拉开距离", "机动", 1, 0, 5, 0, 0, 0, "远离 130 米，取消冲撞航行；格挡 +5。", NavalBattleStages.Ship, NavalBattleCardAction.Retreat, artId: "card_evasive_maneuver"),
            new CoreBattleCard("card_circle_enemy", "绕至侧舷", "机动", 1, 0, 3, 0, 0, 0, "靠近 60 米，舰艏对准敌舰侧舷；须再次推进才能冲撞。", NavalBattleStages.Ship, NavalBattleCardAction.Circle, artId: "card_full_sail_turn"),
            new CoreBattleCard("card_parallel_course", "并排行船", "机动", 1, 0, 0, 0, 0, 0, "靠近 60 米并转为平行；平行且距离不超过 60 米可接舷。", NavalBattleStages.Ship, NavalBattleCardAction.Parallel, artId: "card_boarding_hook"),
            new CoreBattleCard("card_crew_thrust", "水手刺击", "登船", 1, 9, 0, 0, 0, 0, "敌方船员 -9。", NavalBattleStages.Boarding, artId: "card_cutlass_raid"),
            new CoreBattleCard("card_crew_guard", "盾阵掩护", "防御", 1, 0, 9, 0, 0, 0, "船员格挡 +9。", NavalBattleStages.Boarding, artId: "card_brace"),
            new CoreBattleCard("card_crew_rally", "集结队伍", "登船", 1, 0, 3, 0, 2, 0, "船员格挡 +3，抽 2 张白刃战牌。", NavalBattleStages.Boarding, artId: "card_full_sail_turn"),
            new CoreBattleCard("card_crew_bandage", "战地包扎", "维修", 1, 0, 0, 8, 0, 0, "恢复 8 点己方船员生命。", NavalBattleStages.Boarding, artId: "card_emergency_repair"),
            new CoreBattleCard("card_crew_pistol", "燧发手枪", "登船", 2, 15, 2, 0, 0, 0, "敌方船员 -15，船员格挡 +2。", NavalBattleStages.Boarding, artId: "card_chain_shot"),
            new CoreBattleCard("card_navigator_course", "领航员 · 抢占航线", "机动", 1, 0, 3, 0, 1, 0, "靠近 180 米并调整至平行；格挡 +3，抽 1 张。", NavalBattleStages.Ship, NavalBattleCardAction.Navigator, "navigator", "card_full_sail_turn"),
            new CoreBattleCard("card_carpenter_shoring", "木匠 · 加固舱壁", "维修", 1, 0, 6, 8, 0, 0, "修复 8 点船体，格挡 +6。", NavalBattleStages.Ship, requiredCrew: "carpenter", artId: "card_emergency_repair"),
            new CoreBattleCard("card_doctor_sutures", "医生 · 紧急缝合", "维修", 1, 0, 4, 14, 0, 0, "恢复 14 点船员生命，船员格挡 +4。", NavalBattleStages.Boarding, requiredCrew: "doctor", artId: "card_emergency_repair"),
            new CoreBattleCard("card_gunner_precision", "炮手 · 精密装填", "策略", 1, 0, 3, 0, 0, 0, "下次炮击伤害 +8，装填最多 +12；格挡 +3。", NavalBattleStages.Ship, NavalBattleCardAction.PrecisionLoad, "gunner", "card_cannon_barrage"),
            new CoreBattleCard("card_cook_rations", "厨师 · 鼓舞口粮", "防御", 0, 0, 5, 3, 0, 0, "格挡 +5；炮战修复 3 船体，白刃战恢复 3 船员生命。", NavalBattleStages.All, requiredCrew: "cook", artId: "card_brace"),
            new CoreBattleCard("card_diver_sabotage", "潜水员 · 水下破坏", "登船", 1, 10, 0, 0, 0, 0, "150 米内：敌舰船体 -10，无需弹药。", NavalBattleStages.Naval, NavalBattleCardAction.Sabotage, "diver", "card_boarding_hook"),
            ReservedBattleCardSlot(25),
            ReservedBattleCardSlot(26),
            new CoreBattleCard("card_component_bow", "舰艏 · 破浪切入", "登船", 1, 8, 4, 0, 0, 0, "150 米内：敌舰船体 -8，格挡 +4，无需弹药。", NavalBattleStages.Naval, NavalBattleCardAction.Sabotage, artId: "card_boarding_hook", requiredComponent: "bow"),
            new CoreBattleCard("card_component_sail", "风帆 · 借风转舵", "机动", 1, 0, 3, 0, 1, 0, "转向敌舰侧舷，靠近 80 米；格挡 +3，抽 1 张。", NavalBattleStages.Ship, NavalBattleCardAction.Turn, artId: "card_full_sail_turn", requiredComponent: "sail"),
            new CoreBattleCard("card_component_cannon", "火炮 · 校准射击", "炮击", 1, 12, 0, 0, 0, 0, "350 米内：12 点船体伤害，消耗 1 炮弹。", NavalBattleStages.Naval, NavalBattleCardAction.Cannon, artId: "card_chain_shot", cannonballCost: 1, requiredComponent: "cannon"),
            new CoreBattleCard("card_component_plating", "装甲 · 应急隔舱", "防御", 1, 0, 8, 3, 0, 0, "格挡 +8，修复 3 点船体。", NavalBattleStages.Ship, artId: "card_brace", requiredComponent: "plating"),
            new CoreBattleCard("card_component_instrument", "仪器 · 火控解算", "策略", 1, 0, 0, 0, 1, 0, "下次炮击伤害 +4，可叠至 +12；抽 1 张。", NavalBattleStages.Ship, NavalBattleCardAction.Load, artId: "card_cannon_barrage", requiredComponent: "instrument"),
            });
            // Saved decks use integer positions: all historic positions must remain unchanged.
            foreach (CoreSpecialCardDefinition definition in CoreSpecialCardCatalog.All)
            {
                // Removed content leaves an inert slot, never shifting another saved card's index.
                if (result.Count == 56) result.Add(ReservedBattleCardSlot(56));
                result.Add(definition.Card);
            }
            return result.AsReadOnly();
        }

        public const int LegacyBattleCardSlotCount = 32;

        private static CoreBattleCard ReservedBattleCardSlot(int index) =>
            new CoreBattleCard("retired_slot_" + index, string.Empty, string.Empty, 0, 0, 0, 0, 0, 0,
                string.Empty, NavalBattleStages.None);

        private static readonly int[][] defaultBattleDecks =
        {
            new[] { 8, 8, 9, 9, 10, 10, 11, 12, 13, 7 },
            new[] { 0, 0, 6, 10, 11, 13, 12, 9, 2, 5 },
            new[] { 3, 3, 4, 4, 14, 14, 15, 15, 16, 17 },
        };
        private readonly List<int>[] battleDecks =
        {
            new List<int>(defaultBattleDecks[0]), new List<int>(defaultBattleDecks[1]), new List<int>(defaultBattleDecks[2]),
        };
        private float battleBowQuality = 1f;
        private float battleHullQuality = 1f;

        public void ConfigureBattleEquipment(float bowQuality, float hullQuality)
        {
            if (Phase != CoreDemoPhase.Port) return;
            battleBowQuality = SafeBattleQuality(bowQuality);
            battleHullQuality = SafeBattleQuality(hullQuality);
        }

        private static float SafeBattleQuality(float quality) =>
            float.IsNaN(quality) || float.IsInfinity(quality) ? 1f : Clamp(quality, 1f, 5.5f);

        public IReadOnlyList<int> GetBattleDeck(NavalBattleStage stage) =>
            IsBattleStage(stage) ? battleDecks[(int)stage].AsReadOnly() : Array.Empty<int>();

        public IReadOnlyList<int> GetAvailableBattleCards(NavalBattleStage stage)
        {
            List<int> result = new List<int>();
            for (int index = 0; index < cardLibrary.Count; index += 1)
                if (cardLibrary[index].IsAllowedInStage(stage) && cardLibrary[index].RequiredCrew.Length == 0 && cardLibrary[index].RequiredComponent.Length == 0) result.Add(index);
            return result.AsReadOnly();
        }

        public IReadOnlyList<int> GetCrewBattleCards(NavalBattleStage stage)
        {
            List<int> result = new List<int>();
            for (int index = 0; index < cardLibrary.Count; index += 1)
            {
                CoreBattleCard card = cardLibrary[index];
                if (card.IsAllowedInStage(stage) && card.RequiredCrew.Length > 0 && HasBattleCrew(card.RequiredCrew)) result.Add(index);
            }
            return result.AsReadOnly();
        }

        private bool HasBattleCrew(string crew)
        {
            if (explicitSpecialCardSources && !string.IsNullOrEmpty(crew)) return activeSpecialCrewIds.Contains(crew);
            switch (crew)
            {
                case "navigator": return HasNavigator;
                case "carpenter": return HasCarpenter;
                case "doctor": return HasDoctor;
                case "gunner": return HasGunner;
                case "cook": return HasCook;
                case "diver": return HasDiver;
                default: return string.IsNullOrEmpty(crew);
            }
        }

        public bool SetBattleDeck(NavalBattleStage stage, IReadOnlyList<int> cardIndexes)
        {
            if (Phase != CoreDemoPhase.Port || !IsBattleStage(stage) || !IsValidBaseDeck(cardIndexes, stage)) return false;
            // Copy first: callers may pass the current read-only view of this very deck.
            List<int> copy = new List<int>(cardIndexes);
            battleDecks[(int)stage].Clear();
            battleDecks[(int)stage].AddRange(copy);
            return true;
        }

        public bool SetBattleDeck(IReadOnlyList<int> cardIndexes)
        {
            if (Phase != CoreDemoPhase.Port || !IsValidBaseDeck(cardIndexes, null)) return false;
            List<int> source = new List<int>(cardIndexes);
            for (int phase = 0; phase < 3; phase += 1)
            {
                NavalBattleStage stage = (NavalBattleStage)phase;
                List<int> migrated = new List<int>();
                foreach (int index in source) if (cardLibrary[index].IsAllowedInStage(stage)) migrated.Add(index);
                foreach (int index in defaultBattleDecks[phase])
                {
                    if (migrated.Count >= 10) break;
                    int copies = 0;
                    foreach (int existing in migrated) if (existing == index) copies += 1;
                    if (copies < 3) migrated.Add(index);
                }
                SetBattleDeck(stage, migrated);
            }
            return true;
        }

        public string GetBattleDeckSummary() =>
            "准备 " + GetBattleDeck(NavalBattleStage.Preparation).Count + "+" + GetBonusBattleCards(NavalBattleStage.Preparation).Count +
            " · 炮战 " + GetBattleDeck(NavalBattleStage.Naval).Count + "+" + GetBonusBattleCards(NavalBattleStage.Naval).Count +
            " · 白刃 " + GetBattleDeck(NavalBattleStage.Boarding).Count + "+" + GetBonusBattleCards(NavalBattleStage.Boarding).Count + "（基础 + 船员/部件）";

        private static bool IsBattleStage(NavalBattleStage stage) => (int)stage >= 0 && (int)stage <= 2;

        private static bool IsValidBaseDeck(IReadOnlyList<int> indexes, NavalBattleStage? stage)
        {
            if (indexes == null || indexes.Count < 8 || indexes.Count > 10) return false;
            Dictionary<int, int> copies = new Dictionary<int, int>();
            foreach (int index in indexes)
            {
                if (index < 0 || index >= cardLibrary.Count) return false;
                CoreBattleCard card = cardLibrary[index];
                if (card.AllowedStages == NavalBattleStages.None || card.RequiredCrew.Length > 0 || card.RequiredComponent.Length > 0 || (stage.HasValue && !card.IsAllowedInStage(stage.Value))) return false;
                copies.TryGetValue(index, out int count);
                if (count >= 3) return false;
                copies[index] = count + 1;
            }
            return true;
        }
    }
}
