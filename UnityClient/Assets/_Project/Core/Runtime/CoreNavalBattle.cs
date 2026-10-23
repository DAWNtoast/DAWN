using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShatteredSail.Core.Gameplay
{
    public enum NavalBattleStage { Preparation, Naval, Boarding }
    public enum NavalBattleAlignment { HeadOn, EnemyBroadside, Parallel, Maneuvering }
    public enum NavalBoardingApproach { Alongside, Quarter }
    public enum NavalBoardingConnection { Planned, RamBowToBow, RamBowToBroadside, RamBowToStern }

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
        Navigator, PrecisionLoad, Sabotage, FireRounds, Energize,
        TurnPort, TurnStarboard, TurnAbout, SailForward,
    }

    /// <summary>Semantic effect rows shared by card faces, dossiers and comparison previews.</summary>
    public static class CoreBattleCardText
    {
        // Named effect values leave fixed rules (four-cell cannon range, boarding conditions,
        // duration, resource costs) independent of similarly-valued upgrades.
        // Every rank renders the same authored wording and semantic line breaks.
        internal static string RenderTemplate(string template, CoreBattleCard card, NavalBattleStage? stage = null, int? fullRepair = null)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            if (template.IndexOf('{') < 0) return FormatCardEffects(template, card, stage, fullRepair);
            return FormatCardEffects(template
                .Replace("{damage}", Number(card.Damage))
                .Replace("{block}", Number(card.Block))
                .Replace("{boardingBlock}", Number(card.BoardingBlock ?? card.Block))
                .Replace("{repair}", Number(card.Repair))
                .Replace("{draw}", Number(card.Draw))
                .Replace("{energyRecovery}", Number(CoreBattleUpgradeBalance.EnergyRecovery(card)))
                .Replace("{movement}", Number(card.MovementMeters))
                .Replace("{navalActionPoints}", Number(card.NavalActionPointGain))
                .Replace("{strategyAmount}", Number(card.StrategyAmount))
                .Replace("{strategyLimit}", Number(card.StrategyLimit))
                .Replace("{burnDuration}", Number(card.BurnDuration))
                .Replace("{burnDamage}", Number(card.BurnDamage))
                .Replace("{powder}", Number(card.PowderCost))
                .Replace("{cannonballs}", Number(card.CannonballCost))
                .Replace("{wood}", Number(card.SchoolEffect?.WoodCost ?? 0))
                .Replace("{schoolPower}", Number(card.SchoolEffect?.Power ?? 0))
                .Replace("{schoolSecondary}", Number(card.SchoolEffect?.Secondary ?? 0))
                .Replace("{schoolThreshold}", Number(card.SchoolEffect?.Threshold ?? 0))
                .Replace("{schoolDuration}", Number(card.SchoolEffect?.Duration ?? 0))
                .Replace("{schoolLimit}", Number(card.SchoolEffect?.Limit ?? 0)), card, stage, fullRepair);
        }

        internal static string FormatCardEffects(string value, CoreBattleCard card, NavalBattleStage? stage = null, int? fullRepair = null)
        {
            string text = FormatEffects(CoreBattleCardRuleText.RemoveCommonRules(value, card));
            text = CoreBattleCardSupplyText.Rewrite(text, card, stage, fullRepair);
            text = CoreBattleCardFormulaText.RemoveCalculatedExamples(text, card);
            text = CoreBattleCardCostText.RemoveIconCosts(text, card);
            return FormatEffects(CoreBattleCardAuthorStyleText.Rewrite(CoreBattleCardChineseText.Rewrite(text, card), card));
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static readonly string[] EffectClauseStarts =
        {
            "格挡", "船员格挡", "获得 ", "抽 ", "恢复 ", "修复 ", "海战修复 ", "接舷战恢复 ",
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
            string normalized = CoreBattleCardWording.Normalize(value.Replace("\r\n", "\n").Replace('\r', '\n'));
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
            string requiredComponent = "", int upgradeLevel = 0, string schoolId = "", CoreSchoolCardEffect schoolEffect = null,
            int? boardingBlock = null, CoreBoardingCardSpec boardingSpec = null)
        {
            Id = id;
            Name = name;
            Type = type;
            Energy = energy;
            Damage = damage;
            Block = block;
            BoardingBlock = boardingBlock;
            Repair = repair;
            Draw = draw;
            PowderCost = powderCost;
            AllowedStages = allowedStages;
            Action = action;
            RequiredCrew = requiredCrew ?? string.Empty;
            ArtId = allowedStages == NavalBattleStages.None ? string.Empty : string.IsNullOrEmpty(artId) ? id : artId;
            CannonballCost = cannonballCost;
            RequiredComponent = requiredComponent ?? string.Empty;
            UpgradeLevel = Math.Max(0, Math.Min(3, upgradeLevel));
            SchoolId = schoolId ?? string.Empty;
            SchoolEffect = schoolEffect;
            BoardingSpec = boardingSpec;
            DescriptionTemplate = CoreNavalActionPointRules.ConvertDescription(description ?? string.Empty, this);
            Description = CoreBattleCardText.RenderTemplate(DescriptionTemplate, this);
        }

        public string Id { get; }
        public string Name { get; }
        public string Type { get; }
        public int Energy { get; }
        public int Damage { get; }
        public int Block { get; }
        // Optional authored crew guard; null keeps the shared value in every phase.
        public int? BoardingBlock { get; }
        public int Repair { get; }
        public int Draw { get; }
        public int PowderCost { get; }
        public int CannonballCost { get; }
        public string Description { get; }
        public string DescriptionTemplate { get; }
        public int NavalActionPointGain => CoreNavalActionPointRules.IsMovementCard(this)
            ? (int)Math.Min(int.MaxValue, 1L + CoreCardUpgradeDesign.ActionPointBonus(this, UpgradeLevel)) : 0;
        public string RequiredCrew { get; }
        public string RequiredComponent { get; }
        public int UpgradeLevel { get; }
        public string SchoolId { get; }
        public CoreSchoolCardEffect SchoolEffect { get; }
        public CoreBoardingCardSpec BoardingSpec { get; }
        public decimal NumericalMultiplier => CoreCardUpgradeDesign.Multiplier(this, UpgradeLevel);
        public int MovementMeters => SchoolEffect != null && SchoolEffect.Movement != 0 ? (int)Math.Min(int.MaxValue, Math.Abs((long)SchoolEffect.Movement)) : BaseMovement(Action);
        public int StrategyAmount => Action == NavalBattleCardAction.FireRounds ? 2 :
            CoreCardUpgradeDesign.ScaleEffect(this, Action == NavalBattleCardAction.Polish ? 2 :
                Action == NavalBattleCardAction.PrecisionLoad || Action == NavalBattleCardAction.Load ? 5 : 0, UpgradeLevel);
        public int StrategyLimit => (Action == NavalBattleCardAction.Polish ? 6 : Action == NavalBattleCardAction.FireRounds ? 4 :
            Action == NavalBattleCardAction.Load || Action == NavalBattleCardAction.PrecisionLoad ? 12 : 0);
        public int BurnDamage => CoreCardUpgradeDesign.ScaleEffect(this, Action == NavalBattleCardAction.FireRounds ? 4 : 5, UpgradeLevel);
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
                case NavalBattleCardAction.SailForward: return 100;
                default: return 0;
            }
        }
    }

    public sealed partial class CoreBattleState
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
        public float DistanceMeters
        {
            get => tacticalDistanceMeters;
            set => SetLegacyTacticalDistance(value);
        }
        public NavalBattleAlignment Alignment { get; set; }
        public NavalBoardingApproach PlannedBoardingApproach { get; set; }
        public NavalBoardingApproach BoardingApproach { get; set; }
        public NavalBoardingConnection BoardingConnection { get; set; }
        public bool PlayerMovingTowardEnemy { get; set; }
        public bool EnemyMovingTowardPlayer { get; set; }
        public int PlayerBoardingHealth { get; set; }
        public int PlayerMaxBoardingHealth { get; set; }
        // Only the explicit training-continuation overload preserves an existing crew wound.
        public int? TrainingStartingCrewHealth { get; internal set; }
        // Actual additional crew HP lost to this training battle's naval damage on boarding.
        public int TrainingBoardingCarryover { get; internal set; }
        // Hull values at the start of the naval stage. They are used to carry
        // only this battle's ship damage into the boarding health snapshot.
        public int NavalStartPlayerHull { get; internal set; }
        public int NavalStartEnemyHull { get; internal set; }
        public int PlayerOrdinaryCrew { get; internal set; }
        public int EnemyOrdinaryCrew { get; internal set; }
        public bool BoardingInitiatedByEnemy { get; internal set; }
        public uint DecisionSeed { get; internal set; }
        public int EnemyBoardingHealth { get; set; }
        public int EnemyMaxBoardingHealth { get; set; }
        // Authored loading damage. School overflow is stored separately; UI must
        // use session.PlayerLoadedShotDamageBonus to display their combined total.
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
        /// <summary>Live, read-only battle piles, including stages that have not started yet.</summary>
        public IReadOnlyList<int> GetDrawPile(NavalBattleStage stage) =>
            (int)stage >= 0 && (int)stage < drawPiles.Length ? drawPiles[(int)stage].AsReadOnly() : Array.Empty<int>();
        public IReadOnlyList<int> GetDiscardPile(NavalBattleStage stage) =>
            (int)stage >= 0 && (int)stage < discardPiles.Length ? discardPiles[(int)stage].AsReadOnly() : Array.Empty<int>();
        public List<int> Hand { get; } = new List<int>();
        public string LastAction { get; set; }
        public CoreSchoolBattleState Schools { get; } = new CoreSchoolBattleState();
        public CoreBoardingBattleState Boarding { get; } = new CoreBoardingBattleState();
        public CoreBoardingEnemyState BoardingEnemy { get; } = new CoreBoardingEnemyState();
        internal List<int> DrawPileFor(NavalBattleStage stage) => drawPiles[(int)stage];
        internal int SuccessfulCardUses(string id) => id != null && successfulCardUses.TryGetValue(id, out int count) ? count : 0;
        internal void RecordSuccessfulCardUse(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            successfulCardUses[id] = (int)Math.Min(int.MaxValue, (long)SuccessfulCardUses(id) + 1);
            RecordAchievementCardUse();
        }
        internal void BeginNewCardTurn()
        {
            successfulCardUses.Clear();
            Schools.BeginWindow();
            SpecialCards.BeginTurn();
        }
    }

    public sealed partial class CoreVoyageSession
    {
        private static readonly IReadOnlyList<CoreBattleCard> cardLibrary = CreateBattleCardLibrary();

        private static IReadOnlyList<CoreBattleCard> CreateBattleCardLibrary()
        {
            List<CoreBattleCard> result = new List<CoreBattleCard>(new[]
        {
            new CoreBattleCard("card_chain_shot", "链弹", "炮击", 1, 10, 0, 0, 0, 1, "4 格内：{damage} 点船体伤害；消耗 {powder} 火药、{cannonballs} 炮弹。", NavalBattleStages.Naval, NavalBattleCardAction.Cannon, cannonballCost: 1),
            new CoreBattleCard("card_full_sail_turn", "满帆转向", "机动", 1, 0, 0, 0, 1, 0, "沿敌舰周围绕行 {movement} 米，再将船头转向敌舰；格挡 +{block}；抽 {draw} 张。", NavalBattleStages.Ship, NavalBattleCardAction.Turn),
            new CoreBattleCard("card_evasive_maneuver", "规避机动", "机动", 1, 0, 5, 0, 0, 0, "格挡 +{block}；保留当前航向与距离。", NavalBattleStages.Ship),
            new CoreBattleCard("card_boarding_hook", "钩索绊敌", "登船", 1, 4, 2, 0, 0, 0, "敌方船员 -{damage}，格挡 +{block}。", NavalBattleStages.Boarding),
            new CoreBattleCard("card_cutlass_raid", "弯刀突袭", "登船", 2, 16, 0, 0, 0, 0, "敌方船员 -{damage}。", NavalBattleStages.Boarding),
            new CoreBattleCard("card_emergency_repair", "紧急修理", "维修", 1, 0, 0, 5, 0, 0, "修复 {repair} 点船体。", NavalBattleStages.Ship),
            new CoreBattleCard("card_cannon_barrage", "炮火齐射", "炮击", 2, 20, 0, 0, 0, 1, "4 格内：{damage} 点船体伤害；消耗 {powder} 火药、{cannonballs} 炮弹。", NavalBattleStages.Naval, NavalBattleCardAction.Cannon, cannonballCost: 1),
            new CoreBattleCard("card_brace", "稳固船身", "防御", 0, 0, 2, 0, 0, 0, "格挡 +{block}；准备阶段的格挡保留到首轮炮战。", NavalBattleStages.Ship),
            new CoreBattleCard("card_deck_polish", "打磨甲板", "防御", 1, 0, 0, 0, 0, 0, "本场船体防御牌额外 +{strategyAmount} 格挡，可叠至 +{strategyLimit}。", NavalBattleStages.Ship, NavalBattleCardAction.Polish, artId: "card_brace"),
            new CoreBattleCard("card_load_shot", "装填弹药", "策略", 1, 0, 0, 0, 1, 1, "额外装药：下次炮击 +{strategyAmount}，最多 +{strategyLimit}；抽 {draw} 张；消耗 {powder} 火药；开炮另付弹药并清空增益。", NavalBattleStages.Ship, NavalBattleCardAction.Load, artId: "card_cannon_barrage"),
            new CoreBattleCard("card_approach", "迎敌推进", "机动", 0, 0, 0, 0, 0, 0, "获得 {navalActionPoints} 点行动点。", NavalBattleStages.Ship, NavalBattleCardAction.Approach, artId: "card_full_sail_turn"),
            new CoreBattleCard("card_retreat", "拉开距离", "机动", 1, 0, 3, 0, 0, 0, "获得 {navalActionPoints} 点行动点；格挡 +{block}。", NavalBattleStages.Ship, NavalBattleCardAction.Retreat, artId: "card_evasive_maneuver"),
            RetiredBattleCardSlot("card_circle_enemy"),
            RetiredBattleCardSlot("card_parallel_course"),
            new CoreBattleCard("card_crew_thrust", "水手刺击", "登船", 1, 8, 0, 0, 0, 0, "敌方船员 -{damage}。", NavalBattleStages.Boarding, artId: "card_cutlass_raid"),
            new CoreBattleCard("card_crew_guard", "盾阵掩护", "防御", 1, 0, 4, 0, 0, 0, "船员格挡 +{block}。", NavalBattleStages.Boarding, artId: "card_brace"),
            new CoreBattleCard("card_crew_rally", "集结队伍", "登船", 1, 0, 1, 0, 2, 0, "船员格挡 +{block}，抽 {draw} 张白刃战牌。", NavalBattleStages.Boarding, artId: "card_full_sail_turn"),
            new CoreBattleCard("card_crew_bandage", "战地包扎", "维修", 1, 0, 0, 6, 0, 0, "恢复 {repair} 点己方船员生命。", NavalBattleStages.Boarding, artId: "card_emergency_repair"),
            new CoreBattleCard("card_crew_pistol", "燧发手枪", "登船", 2, 12, 2, 0, 0, 0, "敌方船员 -{damage}，船员格挡 +{block}。", NavalBattleStages.Boarding, artId: "card_chain_shot"),
            new CoreBattleCard("card_navigator_course", "领航员 · 抢占航线", "机动", 1, 0, 1, 0, 1, 0, "沿船头前进 {movement} 米，再调整至平行；格挡 +{block}，抽 {draw} 张。", NavalBattleStages.Ship, NavalBattleCardAction.Navigator, "navigator", "card_full_sail_turn"),
            new CoreBattleCard("card_carpenter_shoring", "木匠 · 加固舱壁", "维修", 1, 0, 4, 6, 0, 0, "格挡 +{block}；修复 {repair} 点船体。", NavalBattleStages.Ship, requiredCrew: "carpenter", artId: "card_emergency_repair"),
            new CoreBattleCard("card_doctor_sutures", "医生 · 紧急缝合", "维修", 1, 0, 1, 7, 0, 0, "船员格挡 +{block}；恢复 {repair} 点船员生命。", NavalBattleStages.Boarding, requiredCrew: "doctor", artId: "card_emergency_repair"),
            new CoreBattleCard("card_gunner_precision", "炮手 · 精密装填", "策略", 1, 0, 1, 0, 0, 1, "额外装药：下次炮击 +{strategyAmount}，最多 +{strategyLimit}；格挡 +{block}；消耗 {powder} 火药；开炮另付弹药并清空增益。", NavalBattleStages.Ship, NavalBattleCardAction.PrecisionLoad, "gunner", "card_cannon_barrage"),
            new CoreBattleCard("card_cook_rations", "厨师 · 鼓舞口粮", "防御", 0, 0, 4, 2, 0, 0, "格挡 +{block}；炮战修复 {repair} 船体，白刃战恢复 {repair} 船员生命。", NavalBattleStages.All, requiredCrew: "cook", artId: "card_brace"),
            new CoreBattleCard("card_diver_sabotage", "潜水员 · 水下破坏", "登船", 1, 12, 0, 0, 0, 0, "150 米内：敌舰船体 -{damage}，无需弹药。", NavalBattleStages.Naval, NavalBattleCardAction.Sabotage, "diver", "card_boarding_hook"),
            ReservedBattleCardSlot(25),
            ReservedBattleCardSlot(26),
            new CoreBattleCard("card_component_bow", "舰艏 · 破浪切入", "登船", 1, 8, 3, 0, 0, 0, "150 米内：敌舰船体 -{damage}，格挡 +{block}，无需弹药。", NavalBattleStages.Naval, NavalBattleCardAction.Sabotage, artId: "card_boarding_hook", requiredComponent: "bow"),
            new CoreBattleCard("card_component_sail", "风帆 · 借风转舵", "机动", 1, 0, 1, 0, 1, 0, "沿敌舰周围绕行 {movement} 米，再将船头转向敌舰；格挡 +{block}，抽 {draw} 张。", NavalBattleStages.Naval, NavalBattleCardAction.Turn, artId: "card_full_sail_turn", requiredComponent: "sail"),
            new CoreBattleCard("card_component_cannon", "火炮 · 校准射击", "炮击", 1, 12, 0, 0, 0, 1, "4 格内：{damage} 点船体伤害；消耗 {powder} 火药、{cannonballs} 炮弹。", NavalBattleStages.Naval, NavalBattleCardAction.Cannon, artId: "card_chain_shot", cannonballCost: 1, requiredComponent: "cannon"),
            new CoreBattleCard("card_component_plating", "装甲 · 应急隔舱", "防御", 1, 0, 6, 3, 0, 0, "格挡 +{block}，修复 {repair} 点船体。", NavalBattleStages.Naval, artId: "card_brace", requiredComponent: "plating"),
            new CoreBattleCard("card_component_instrument", "仪器 · 火控解算", "策略", 1, 0, 0, 0, 1, 0, "校准火控：下次炮击伤害 +{strategyAmount}，可叠至 +{strategyLimit}；抽 {draw} 张；开炮时消耗增益。", NavalBattleStages.Naval, NavalBattleCardAction.Load, artId: "card_cannon_barrage", requiredComponent: "instrument"),
            });
            // Saved decks use integer positions: all historic positions must remain unchanged.
            foreach (CoreSpecialCardDefinition definition in CoreSpecialCardCatalog.All)
            {
                // Removed content leaves an inert slot, never shifting another saved card's index.
                if (result.Count == 56) result.Add(ReservedBattleCardSlot(56));
                result.Add(definition.Card);
            }
            // Source cards are inserted/replaced by their source catalog, preserving saved indices.
            foreach (CoreBattleCard card in CoreSchoolCardCatalog.All)
                if (card.RequiredCrew.Length == 0 && card.RequiredComponent.Length == 0 &&
                    CoreNavalSchoolExpansionCatalog.FindSpec(card.Id) == null) result.Add(card);
            result.Add(new CoreBattleCard("card_energy_order", "整顿号令", "策略", 0, 0, 0, 0, 0, 0,
                "恢复 {energyRecovery} 点能量。",
                NavalBattleStages.All, NavalBattleCardAction.Energize, artId: "card_brace"));
            result.Add(new CoreBattleCard("card_tactical_draw", "战术筹划", "策略", 1, 0, 0, 0, 2, 0,
                "抽 {draw} 张。", NavalBattleStages.All, artId: "card_full_sail_turn"));
            CoreBoardingCardCatalog.ApplyToLibrary(result);
            AppendRetiredNavalTacticsSlots(result);
            // New cards are appended after every historical slot: saved decks store indices.
            foreach (CoreBattleCard card in CoreNavalSchoolExpansionCatalog.All) result.Add(card);
            return result.AsReadOnly();
        }

        public const int LegacyBattleCardSlotCount = 32;
        public const int BaseBattleCardCopies = 2;
        public const int MinimumBaseBattleDeckSize = 10;
        public const int MaximumBaseBattleDeckSize = 20;

        private static CoreBattleCard ReservedBattleCardSlot(int index) =>
            new CoreBattleCard("retired_slot_" + index, string.Empty, string.Empty, 0, 0, 0, 0, 0, 0,
                string.Empty, NavalBattleStages.None);

        private static readonly int[][] defaultBattleDecks =
        {
            CanonicalStarterBattleDeck(WithStarterSupport(new[] { 8, 8, 9, 9, 10, 10, 11, 12, 13, 7 }), NavalBattleStage.Preparation),
            CanonicalStarterBattleDeck(WithStarterSupport(new[] { 0, 0, 6, 10, 11, 13, 12, 9, 2, 5 }), NavalBattleStage.Naval),
            WithStarterSupport(new[] { 3, 3, 4, 4, 14, 14, 15, 15, 16, 17 }),
        };
        private static int[] WithStarterSupport(int[] original)
        {
            var result = new List<int>(original);
            for (int index = 0; index < cardLibrary.Count; index++)
                if (cardLibrary[index].Id == "card_energy_order" || cardLibrary[index].Id == "card_tactical_draw") result.Add(index);
            return result.ToArray();
        }
        private readonly List<int>[] battleDecks =
        {
            new List<int>(defaultBattleDecks[0]), new List<int>(defaultBattleDecks[1]), new List<int>(defaultBattleDecks[2]),
        };
        private float battleBowQuality = 1f;
        private float battleHullQuality = 1f;

        public void ConfigureBattleEquipment(float bowQuality, float hullQuality)
        {
            if (Phase != CoreDemoPhase.Port) return;
            SynchronizeRestoredBattleEquipment(bowQuality, hullQuality);
        }

        /// <summary>
        /// Reconciles a resumed session with its saved physical loadout. The caller
        /// supplies current rarity strengths, replacing retired appraisal values
        /// carried by older voyage checkpoints without permitting ordinary refits.
        /// </summary>
        public void SynchronizeRestoredBattleEquipment(float bowQuality, float hullQuality)
        {
            battleBowQuality = SafeBattleQuality(bowQuality);
            battleHullQuality = SafeBattleQuality(hullQuality);
            if (Battle == null) return;
            Battle.PlayerBowQuality = battleBowQuality;
            Battle.PlayerHullQuality = battleHullQuality;
        }

        private static float SafeBattleQuality(float quality) =>
            float.IsNaN(quality) || float.IsInfinity(quality) ? 1f : Clamp(quality, 1f, 5.5f);

        public IReadOnlyList<int> GetBattleDeck(NavalBattleStage stage) =>
            IsBattleStage(stage) ? battleDecks[(int)stage].AsReadOnly() : Array.Empty<int>();

        public int GetBaseBattleCardOwnedCopies(int cardIndex) =>
            cardIndex == CanonicalBattleCardIndex(cardIndex) && IsBaseBattleCard(cardIndex) ? BaseBattleCardCopies : 0;

        public IReadOnlyList<int> GetAvailableBattleCards(NavalBattleStage stage)
        {
            List<int> result = new List<int>();
            for (int index = 0; index < cardLibrary.Count; index += 1)
                if (cardLibrary[index].IsAllowedInStage(stage) && cardLibrary[index].RequiredCrew.Length == 0 && cardLibrary[index].RequiredComponent.Length == 0 && IsBattleCardUnlocked(cardLibrary[index]) &&
                    (stage != NavalBattleStage.Boarding || !CoreBoardingCardCatalog.IsLegacyAlias(cardLibrary[index]))) result.Add(index);
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
            if (Phase != CoreDemoPhase.Port || !IsBattleStage(stage) || cardIndexes == null) return false;
            // Copy first: callers may pass the current read-only view of this very deck.
            List<int> copy = CanonicalBattleDeck(cardIndexes);
            if (!IsValidBaseDeck(copy, stage)) return false;
            foreach (int index in copy) if (!IsBattleCardUnlocked(cardLibrary[index])) return false;
            battleDecks[(int)stage].Clear();
            battleDecks[(int)stage].AddRange(copy);
            return true;
        }

        public bool SetBattleDeck(IReadOnlyList<int> cardIndexes)
        {
            if (Phase != CoreDemoPhase.Port || cardIndexes == null) return false;
            List<int> source = CanonicalBattleDeck(cardIndexes);
            if (!IsValidBaseDeck(source, null)) return false;
            foreach (int index in source) if (!IsBattleCardUnlocked(cardLibrary[index])) return false;
            for (int phase = 0; phase < 3; phase += 1)
            {
                NavalBattleStage stage = (NavalBattleStage)phase;
                SetBattleDeck(stage, BuildRestoredBattleDeck(stage, source));
            }
            return true;
        }

        // Saved decks predate the current inventory and size limits. Preserve usable
        // cards in order, trim extra copies, and fill only the missing minimum.
        public bool RestoreBattleDeck(NavalBattleStage stage, IReadOnlyList<int> cardIndexes)
        {
            if (Phase != CoreDemoPhase.Port || !IsBattleStage(stage)) return false;
            return SetBattleDeck(stage, BuildRestoredBattleDeck(stage, cardIndexes));
        }

        private List<int> BuildRestoredBattleDeck(NavalBattleStage stage, IReadOnlyList<int> cardIndexes)
        {
            List<int> restored = new List<int>();
            Dictionary<string, int> copies = new Dictionary<string, int>(StringComparer.Ordinal);
            void Append(int index)
            {
                index = CanonicalBattleCardIndex(index);
                if (restored.Count >= MaximumBaseBattleDeckSize || !IsBaseBattleCard(index) ||
                    !cardLibrary[index].IsAllowedInStage(stage) || !IsBattleCardUnlocked(cardLibrary[index])) return;
                string identity = BattleDeckCopyIdentity(index, stage);
                copies.TryGetValue(identity, out int count);
                if (count >= BaseBattleCardCopies) return;
                restored.Add(index);
                copies[identity] = count + 1;
            }
            if (cardIndexes != null) foreach (int index in cardIndexes) Append(index);
            foreach (int index in defaultBattleDecks[(int)stage])
            {
                if (restored.Count >= MinimumBaseBattleDeckSize) break;
                Append(index);
            }
            for (int copy = 0; copy < BaseBattleCardCopies && restored.Count < MinimumBaseBattleDeckSize; copy++)
                foreach (int index in GetAvailableBattleCards(stage))
                {
                    if (restored.Count >= MinimumBaseBattleDeckSize) break;
                    Append(index);
                }
            return restored;
        }

        public string GetBattleDeckSummary() =>
            "准备 " + GetBattleDeck(NavalBattleStage.Preparation).Count + "+" + GetBonusBattleCards(NavalBattleStage.Preparation).Count +
            " · 炮战 " + GetBattleDeck(NavalBattleStage.Naval).Count + "+" + GetBonusBattleCards(NavalBattleStage.Naval).Count +
            " · 白刃 " + GetBattleDeck(NavalBattleStage.Boarding).Count + "+" + GetBonusBattleCards(NavalBattleStage.Boarding).Count + "（基础 + 船员/部件）";

        private static bool IsBattleStage(NavalBattleStage stage) => (int)stage >= 0 && (int)stage <= 2;

        private static bool IsBaseBattleCard(int index)
        {
            index = CanonicalBattleCardIndex(index);
            return index >= 0 && index < cardLibrary.Count && cardLibrary[index].AllowedStages != NavalBattleStages.None &&
                cardLibrary[index].RequiredCrew.Length == 0 && cardLibrary[index].RequiredComponent.Length == 0;
        }

        // A saved alias and its new canonical card share the same two-copy inventory.
        // Other phases retain their existing card identity and resource rules.
        private static string BattleDeckCopyIdentity(int index, NavalBattleStage? stage)
        {
            CoreBattleCard card = cardLibrary[CanonicalBattleCardIndex(index)];
            if (!stage.HasValue || stage.Value == NavalBattleStage.Boarding)
                card = CoreBoardingCardCatalog.ForBoarding(card);
            return card.BoardingSpec?.Key ?? card.Id;
        }
        private static bool IsValidBaseDeck(IReadOnlyList<int> indexes, NavalBattleStage? stage,
            int minimum = MinimumBaseBattleDeckSize, int maximum = MaximumBaseBattleDeckSize, int maximumCopies = BaseBattleCardCopies,
            bool legacyDuplicateIdentities = false)
        {
            if (indexes == null || indexes.Count < minimum || indexes.Count > maximum) return false;
            Dictionary<string, int> copies = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (int index in indexes)
            {
                if (!IsBaseBattleCard(index)) return false;
                int canonical = CanonicalBattleCardIndex(index);
                CoreBattleCard card = cardLibrary[canonical];
                if (stage.HasValue && !card.IsAllowedInStage(stage.Value)) return false;
                string identity = legacyDuplicateIdentities && canonical != index ? cardLibrary[index].Id : BattleDeckCopyIdentity(index, stage);
                copies.TryGetValue(identity, out int count);
                if (count >= maximumCopies) return false;
                copies[identity] = count + 1;
            }
            return true;
        }
    }
}
