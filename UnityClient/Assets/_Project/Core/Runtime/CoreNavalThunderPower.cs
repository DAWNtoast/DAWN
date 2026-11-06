using System.Globalization;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>Naval thunder circulation and threshold rewards share one authored source.</summary>
    public static class CoreNavalThunderPower
    {
        public const string RelayId = "card_school_thunder_n07";
        public const string CircuitId = "card_school_thunder_n08";
        public const string PayoffId = "card_school_thunder_n09";
        public const int CircuitThreshold = 3;

        public static bool IsNavalFinisher(CoreBattleCard card) => card != null &&
            (card.Id == "card_school_thunder_b06" || card.Id == "card_school_thunder_n06" || card.Id == PayoffId);

        public static int CircuitEnergy(int upgradeLevel) => upgradeLevel <= 0 ? 1 : 2;
        public static int PayoffFive(int upgradeLevel) => upgradeLevel <= 1 ? 12 : 100;
        public static int PayoffTen(int upgradeLevel) => upgradeLevel <= 0 ? 18 : upgradeLevel == 1 ? 24 : upgradeLevel == 2 ? 164 : 176;

        // Thresholds read the reserve before this card grants its normal attack chain.
        public static int PayoffBonus(int upgradeLevel, int chain) =>
            (chain >= 5 ? PayoffFive(upgradeLevel) : 0) + (chain >= 10 ? PayoffTen(upgradeLevel) : 0);

        public static string RenderTemplate(string template, CoreBattleCard card)
        {
            if (string.IsNullOrEmpty(template) || card == null) return template ?? string.Empty;
            return template
                .Replace("{thunderCircuitEnergy}", CircuitEnergy(card.UpgradeLevel).ToString(CultureInfo.InvariantCulture))
                .Replace("{thunderPayoffFive}", PayoffFive(card.UpgradeLevel).ToString(CultureInfo.InvariantCulture))
                .Replace("{thunderPayoffTen}", PayoffTen(card.UpgradeLevel).ToString(CultureInfo.InvariantCulture));
        }
    }

    public sealed partial class CoreVoyageSession
    {
        // Call from the successful play's finally block: lethal damage, curse
        // execution and overload can all leave the ordinary completion path early.
        // Exemption belongs to the field occupied before the play, including a
        // Collapse that destroys that cell while resolving its own effect.
        private void CompleteNavalThunderFinisher(CoreBattleCard card, bool retainedAtPlay)
        {
            if (Battle == null || Battle.Stage != NavalBattleStage.Naval ||
                !CoreNavalThunderPower.IsNavalFinisher(card) || retainedAtPlay) return;
            SetThunderChain(true, 0);
        }

        private string NavalThunderPowerUnavailableReason(CoreBattleCard card) =>
            card?.Id == CoreNavalThunderPower.CircuitId && Battle != null &&
            Battle.Schools.ThunderChain < CoreNavalThunderPower.CircuitThreshold
                ? "连锁至少 3 才能使用" : string.Empty;

        private int NavalThunderPowerDamageBonus(CoreBattleCard card, NavalBattleStage stage) =>
            card?.Id == CoreNavalThunderPower.PayoffId && stage == NavalBattleStage.Naval && NavalExpansionActive
                ? CoreNavalThunderPower.PayoffBonus(card.UpgradeLevel, Battle.Schools.ThunderChain) : 0;

        private void ResolveNavalThunderCircuit(CoreBattleCard card)
        {
            if (!NavalExpansionActive || card?.Id != CoreNavalThunderPower.CircuitId) return;
            Battle.Energy = SchoolSum(Battle.Energy, CoreNavalThunderPower.CircuitEnergy(card.UpgradeLevel));
        }
    }
}
