using System;

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

        public static bool IsValidDispatchRecord(int missionIndex, string deadline, out long endsAt)
        {
            return long.TryParse(deadline, out endsAt) && missionIndex >= 0 && missionIndex < 3 &&
                endsAt > 0 && endsAt <= DateTime.MaxValue.Ticks;
        }

        public static bool IsDispatchReady(int missionIndex, string deadline, long nowTicks)
        {
            return IsValidDispatchRecord(missionIndex, deadline, out long endsAt) && nowTicks >= endsAt;
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
                CoreEconomyBalance.HasNonNegativeCargo(Cargo) &&
                Cargo.UsedVolume <= ships[shipIndex].CargoCapacity + 0.001f;
        }

        public bool TryReceivePortRewards(int gold, int wood, int iron, int fame, string reason, out string failure)
        {
            failure = string.Empty;
            if (Phase != CoreDemoPhase.Port || gold < 0 || wood < 0 || iron < 0 || fame < 0 ||
                !CoreEconomyBalance.HasNonNegativeCargo(Cargo))
            {
                failure = "当前状态无法领取，请返回港口后重试。";
                return false;
            }
            if (ProfileGold + gold > CoreEconomyBalance.MaximumProfileGold ||
                (long)Cargo.Wood + wood > int.MaxValue || (long)Cargo.Iron + iron > int.MaxValue ||
                (long)Cargo.Fame + fame > int.MaxValue)
            {
                failure = "账户或资源已达数量上限；任务收益保留，使用部分资产后可再次领取。";
                return false;
            }
            if (Cargo.UsedVolume + wood + iron * 2d > CargoCapacityValue + 0.001d)
            {
                failure = "货舱空间不足；任务收益保留，请先在仓库出售材料后再领取。";
                return false;
            }
            ProfileGold += gold;
            Cargo.Wood += wood;
            Cargo.Iron += iron;
            Cargo.Fame += fame;
            AddLog((string.IsNullOrWhiteSpace(reason) ? "港口奖励" : reason.Trim()) + "：金币 +" + gold + "。");
            return true;
        }
    }
}
