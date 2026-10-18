using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Stable rules identity shared by canonical cards and save-compatible aliases.</summary>
    public sealed class CoreBoardingCardSpec
    {
        public CoreBoardingCardSpec(string key, bool isAttack = false, bool isDelayed = false)
        { Key = key ?? string.Empty; IsAttack = isAttack; IsDelayed = isDelayed; }
        public string Key { get; }
        public bool IsAttack { get; }
        public bool IsDelayed { get; }
    }

    public enum CoreBoardingChoiceKind { None, HandBurn, HandRetain, DiscardRecover, DrawChoose, DelayedEffect }

    public sealed class CoreBoardingPlayOptions
    {
        public int ResourceAmount;
        public List<int> SelectionIndexes = new List<int>();
        public List<int> BottomOrderIndexes = new List<int>();
        public int SnapshotToken;
    }

    public sealed class CoreBoardingChoiceCandidate
    {
        public int Value;
        public int CardIndex;
        public string Label;
    }

    public sealed class CoreBoardingPlayChoice
    {
        public string Title = string.Empty;
        public string ResourceLabel = string.Empty;
        public int MinResource;
        public int MaxResource;
        public List<int> AllowedResourceAmounts = new List<int>();
        public CoreBoardingChoiceKind Kind;
        public int MinSelection;
        public int MaxSelection;
        public List<CoreBoardingChoiceCandidate> Candidates = new List<CoreBoardingChoiceCandidate>();
        public int SnapshotToken;
        public string Description = string.Empty;
        public bool RequiresChoice;
    }

    public sealed class CoreBoardingTimedStacks
    {
        public int Amount;
        public int Remaining;
        public bool Extended;
        public CoreBoardingTimedStacks(int amount, int remaining)
        { Amount = amount; Remaining = remaining; }
    }

    public sealed class CoreBoardingDelayedDamage
    {
        public int Amount;
        public bool IgnoreBlock;
        public int Remaining = 1;
        public string Label = "延迟攻击";
        public string SourceSchoolId = string.Empty;
        public string SourceCardId = string.Empty;
    }

    public sealed class CoreBoardingHealingTick
    {
        public int Amount;
        public int Remaining;
    }

    public sealed class CoreBoardingHeldCard
    {
        public int CardIndex;
        public bool Retain;
        public bool RetainUsed;
        public int Discount;
        public int DiscountTurn;
    }

    /// <summary>Boarding-only transient state; voyage saves never serialize an active battle.</summary>
    public sealed partial class CoreBoardingBattleState
    {
        public List<CoreBoardingTimedStacks> EnemyBurn { get; } = new List<CoreBoardingTimedStacks>();
        public List<CoreBoardingTimedStacks> SelfBurn { get; } = new List<CoreBoardingTimedStacks>();
        public List<CoreBoardingDelayedDamage> DelayedDamage { get; } = new List<CoreBoardingDelayedDamage>();
        public List<CoreBoardingHealingTick> HealingTicks { get; } = new List<CoreBoardingHealingTick>();
        public List<CoreBoardingHeldCard> HeldCards { get; } = new List<CoreBoardingHeldCard>();
        public List<int> ExhaustedCards { get; } = new List<int>();
        internal HashSet<string> TurnUses { get; } = new HashSet<string>(StringComparer.Ordinal);
        internal HashSet<string> BattleUses { get; } = new HashSet<string>(StringComparer.Ordinal);
        public int FreePowderUses;
        public int FirePactTurns;
        public bool FirePactUsed;
        public int FireRetaliations;
        public List<CoreBoardingTimedStacks> FirePots { get; } = new List<CoreBoardingTimedStacks>();
        public int ArcaneRetainedGuard;
        public int ArcaneGuardExpiry;
        public bool ArcaneEchoReady;
        public CoreBattleCard ArcaneEchoCard;
        public int ThunderReady;
        public int ThunderResidualCharges;
        public int ThunderResidualDamage;
        public int ThunderWeakness;
        public int ThunderWeaknessTurns;
        public int ActiveAttackBonus;
        public bool ActiveCritical;
        public bool ActiveAttack;
        public int ActiveAuthoredDamage;
        public CoreBattleCard ActiveCard;
        public int BurnedAttackCount;
        internal List<CoreBoardingHeldCard> SelectedHeldCards = new List<CoreBoardingHeldCard>();
        public int Revision;
        public bool Initialized;
    }
}
