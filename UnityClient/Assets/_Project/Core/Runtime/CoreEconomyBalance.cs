namespace ShatteredSail.Core.Gameplay
{
    public static class CoreEconomyBalance
    {
        public const int MaximumProfileGold = 1_000_000_000;

        /// <summary>Market quotes use four decimal places; reject rather than round a payment.</summary>
        public static bool IsValidGoldAmount(decimal amount)
        {
            return amount >= 0m && amount <= MaximumProfileGold && decimal.Round(amount, 4) == amount;
        }

        /// <summary>Legacy saved assets remain spendable even above the limit for new income.</summary>
        public static bool IsValidGoldPayment(decimal amount)
        {
            return amount >= 0m && amount <= int.MaxValue && decimal.Round(amount, 4) == amount;
        }

        public static bool HasNonNegativeCargo(VoyageCargo cargo)
        {
            return cargo != null && cargo.Water >= 0 && cargo.Food >= 0 && cargo.Beer >= 0 &&
                cargo.Medicine >= 0 && cargo.Wood >= 0 && cargo.Iron >= 0 && cargo.Cloth >= 0 &&
                cargo.Powder >= 0 && cargo.Cannonballs >= 0 && cargo.Gold >= 0 && cargo.Fame >= 0 &&
                cargo.Gem >= 0 && cargo.AncientMaterial >= 0 && cargo.Relic >= 0 && cargo.BlackPearl >= 0 &&
                cargo.AncientCompass >= 0 && cargo.Lantern >= 0 && cargo.Curse >= 0;
        }
    }

    public sealed partial class CoreVoyageSession
    {
        public bool CanSelectShip(int shipIndex)
        {
            return Phase == CoreDemoPhase.Port && shipIndex >= 0 && shipIndex < ships.Count &&
                (!shipPorts.ContainsKey(shipIndex) || IsShipAtCurrentPort(shipIndex)) &&
                CoreEconomyBalance.HasNonNegativeCargo(Cargo) &&
                Cargo.UsedVolume <= ships[shipIndex].CargoCapacity + 0.001f;
        }
    }
}
