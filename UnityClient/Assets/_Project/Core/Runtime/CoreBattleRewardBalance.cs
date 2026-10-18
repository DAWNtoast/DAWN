using System;

namespace ShatteredSail.Core.Gameplay
{
    /// <summary>All physical battle spoils are sold before loading the ship.
    /// Quotes and settlement share fixed prices, hundred-gold rounding and capture bonuses.</summary>
    public static class CoreBattleRewardBalance
    {
        public static decimal MaterialGoldValue(VoyageEffect reward, bool doctor = false, bool diver = false)
        {
            if (reward == null) return 0m;
            decimal Basic(string key, int amount) => Math.Max(0, amount) * CorePortEconomy.BuyBasePrice(key);
            decimal Rare(string key, int amount) => Math.Max(0, amount) * CoreRareItemEconomy.SaleUnitPrice(key);
            long Salvage(int amount) => amount <= 0 ? 0L : diver && !reward.CrewBonusesApplied
                ? (long)amount + Math.Max(1, amount / 4) : amount;
            // A doctor discounts medicine costs; it never increases positive medicine rewards.
            return Basic("water", reward.Water) + Basic("food", reward.Food) + Basic("beer", reward.Beer) +
                Basic("medicine", reward.Medicine) + Salvage(reward.Wood) * CorePortEconomy.BuyBasePrice("wood") +
                Salvage(reward.Iron) * CorePortEconomy.BuyBasePrice("iron") + Basic("cloth", reward.Cloth) +
                Basic("powder", reward.Powder) + Basic("cannonballs", reward.Cannonballs) +
                Rare("gem", reward.Gem) + Rare("ancientmaterial", reward.AncientMaterial) + Rare("relic", reward.Relic) +
                Rare("blackpearl", reward.BlackPearl) + Rare("ancientcompass", reward.AncientCompass) + Rare("lantern", reward.Lantern);
        }

        public static long GoldReward(bool boss, VoyageEffect reward, bool captured, double rewardMultiplier = 1d,
            bool doctor = false, bool diver = false)
        {
            decimal amount = (boss ? CoreBattleBalance.BossVictoryGold : 45) +
                (decimal)Math.Max(0, reward?.Gold ?? 0) + MaterialGoldValue(reward, doctor, diver);
            amount *= RewardMultiplier(rewardMultiplier);
            if (amount <= 0m) return 0L;
            // Round once, after repeat-encounter reductions, so every cash payout
            // is a whole hundred and capturing always doubles the same naval quote.
            decimal rounded = Math.Max(100m, decimal.Round(amount / 100m, 0, MidpointRounding.AwayFromZero) * 100m);
            return (long)Math.Min(long.MaxValue, captured ? rounded * 2m : rounded);
        }

        public static long FameReward(bool boss, VoyageEffect reward, bool captured, double rewardMultiplier = 1d)
        {
            long amount = (boss ? CoreBattleBalance.BossVictoryFame : 6) + (long)Math.Max(0, reward?.Fame ?? 0);
            if (captured) amount *= 2;
            return (long)decimal.Floor(amount * RewardMultiplier(rewardMultiplier));
        }

        private static decimal RewardMultiplier(double multiplier) => double.IsNaN(multiplier) ? 0m :
            (decimal)Math.Max(0d, Math.Min(1d, multiplier));
    }
}
