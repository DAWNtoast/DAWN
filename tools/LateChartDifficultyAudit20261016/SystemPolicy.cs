using System;
using System.Linq;
using ShatteredSail.Core.Gameplay;

// Fixed, resource-aware policies for the six authored 10+2 player decks.
// Only the present hand, discard-independent public statuses and current enemy
// preview are inspected. Pair bonuses compare known numerical formulas; they do
// not execute hypothetical cards, read the draw pile or roll future criticals.
internal static class SystemPolicy
{
    public static double Score(CoreVoyageSession s, int slot, CoreBoardingEnemyPreview preview, string policy)
    {
        var b = s.Battle;
        var card = CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(b.Hand[slot]));
        string key = card.BoardingSpec?.Key ?? string.Empty;
        var effect = s.GetBattleCardEffects(card, NavalBattleStage.Boarding);
        int fee = s.GetBoardingHandEnergyCost(slot);
        int selfBurn = b.Boarding.SelfBurn.Sum(x => x.Amount);
        int endHealing = b.Boarding.HealingTicks.Sum(x => x.Amount);
        int totalThreat = Math.Max(0, preview.PlayerHealthLoss + preview.PlayerBurnDamage + selfBurn - endHealing);
        int blockable = preview.Actions.Sum(a => Math.Max(0, a.HealthLost - a.PiercingDamage));
        int missing = b.PlayerMaxBoardingHealth - b.PlayerBoardingHealth;
        bool danger = b.PlayerBoardingHealth <= totalThreat + 8;
        double damageWeight = policy == "aggressive" ? 1.45 : policy == "conservative" ? .95 : 1.2;
        double defenseWeight = policy == "aggressive" ? 1.25 : policy == "conservative" ? 2.35 : 1.8;
        double healWeight = policy == "aggressive" ? 1.2 : policy == "conservative" ? 2.15 : 1.65;
        if (danger) { defenseWeight += 3.2; healWeight += 3.0; }
        if (b.PlayerBoardingHealth <= totalThreat) { defenseWeight += 4; healWeight += 4; }
        double scale = (double)CoreBattleUpgradeBalance.MultiplierForLevel(card.UpgradeLevel);
        bool delayed = card.BoardingSpec?.IsDelayed == true;
        bool ignoreGuard = key == "curse_11" || key == "thunder_14";
        int raw = s.GetBoardingCardDamageQuote(card);
        int direct = Math.Max(0, raw - (ignoreGuard ? 0 : b.EnemyBlock));
        if (!delayed && direct >= b.EnemyBoardingHealth && direct > 0) return 100000d / (fee + .65);
        double damage = raw;
        if (card.BoardingSpec?.IsAttack == true && !delayed)
            damage *= 1d + s.BoardingCriticalChancePercent / 100d;
        damage = Math.Max(0, damage - (ignoreGuard ? 0 : b.EnemyBlock));
        if (delayed) damage *= .78;
        double value = damage * damageWeight + Math.Min(blockable, effect.Block) * defenseWeight;
        int heal = Math.Min(missing, effect.Repair);
        value += heal * healWeight;
        if (card.Draw > 0)
            value += Math.Min(card.Draw, Math.Max(0, 9 - b.Hand.Count)) * (b.Energy > fee ? 4.5 : .8);

        int recovery = s.GetBattleCardEnergyRecovery(b.Hand[slot]);
        if (recovery > 0)
        {
            // There is no energy cap. Do not spend health/resources just to end
            // with surplus energy and no other card to use it on.
            if (b.Hand.Count <= 1 && card.Draw == 0 || recovery <= fee) return -1;
            value += (recovery - fee) * 13;
        }
        int mana = Math.Min(3, b.Schools.AvailableMana);
        int marks = b.Boarding.CurseMarks;
        int tide = b.Boarding.TidePower;
        int chain = Math.Min(6, b.Schools.ThunderChain);
        int newSelfBurn = key == "fire_11" ? 3 : key == "fire_13" || key == "fire_14" ? 2 : 0;
        int immediateLife = key == "blood_02" ? 4 : key == "blood_01" || key == "blood_03" ||
            key == "blood_04" || key == "blood_05" ? 3 : key == "fire_14" ? 2 : 0;

        switch (key)
        {
            case "basic_07":
            case "tide_12":
                value += Math.Min(effect.Repair, Math.Max(0, missing - heal) + totalThreat * .6) * healWeight * .7;
                break;
            case "fire_02": value += N(card, 4) * 1.7 * damageWeight; break;
            case "fire_03": value += N(card, 8) * 1.7 * damageWeight; break;
            case "fire_04": value += N(card, 6) * 4.2 * damageWeight; break;
            case "fire_12":
            {
                // Three player turns, at most one copy of newly added self-burn
                // per turn. An affordable source in hand supports this turn;
                // later turns get a discounted, deck-independent setup value.
                int immediate = MaxAffordableSelfBurn(s, slot, b.Energy - fee);
                value += ((immediate > 0 ? Math.Min(3, immediate) * 1.7 : 0) + 3.0) * damageWeight;
                if (immediate > 0) value += 10; // order the contract before its visible trigger
                break;
            }
            case "fire_13":
                if (blockable > 0)
                    value += Math.Max(0, Math.Min(8, (selfBurn + 2) * 2) - b.EnemyBlock) * damageWeight;
                break;
            case "fire_15":
            {
                int remaining = 4, prevented = 0, thisEnd = 0;
                foreach (var batch in b.Boarding.SelfBurn.OrderByDescending(x => x.Remaining))
                {
                    int moved = Math.Min(remaining, batch.Amount);
                    prevented += moved * batch.Remaining; thisEnd += moved; remaining -= moved;
                }
                value += prevented * (defenseWeight * .8 + damageWeight * .9);
                if (b.PlayerBoardingHealth <= totalThreat && b.PlayerBoardingHealth > totalThreat - thisEnd) value += 35;
                break;
            }
            case "arcane_01":
            case "arcane_02":
                value += Math.Max(0, Math.Min(1, 3 - mana)) * 4.5 * scale;
                value += PairGain(s, slot, "mana", mana, Math.Min(3, mana + 1)) * damageWeight * 2;
                break;
            case "arcane_03":
                value += Math.Max(0, 3 - mana) * 4.5 * scale;
                value += PairGain(s, slot, "mana", mana, 3) * damageWeight * 2;
                break;
            case "arcane_08":
                value += Math.Min(Math.Max(0, effect.Block - blockable), N(card, 4)) * .8;
                break;
            case "curse_01":
            case "curse_03":
                value += Math.Max(0, Math.Min(1, 6 - marks)) * 3.3 * scale;
                value += PairGain(s, slot, "marks", marks, Math.Min(6, marks + 1)) * damageWeight * 2;
                break;
            case "curse_02":
                value += Math.Max(0, Math.Min(3, 6 - marks)) * 3.3 * scale;
                value += PairGain(s, slot, "marks", marks, Math.Min(6, marks + 3)) * damageWeight * 2;
                break;
            case "curse_04":
                // Only actual attack health loss triggers the contract; burn
                // and fully absorbed attacks never generate marks.
                value += Math.Min(6 - marks, preview.PlayerHealthLoss > 0 ? 4 : 2) * 3.0 * scale;
                break;
            case "curse_07":
                value += Math.Min(preview.AttackDamage / 2d, N(card, 3 + marks)) * defenseWeight;
                break;
            case "curse_09":
            {
                int potentialMarks = Math.Min(6, marks + MaxAffordableMarks(s, slot, b.Energy - fee));
                double triggers = preview.AttackDamage > 0 ? 1.65 : .75;
                value += Math.Min(N(card, 10), N(card, potentialMarks * 2)) * triggers * damageWeight;
                break;
            }
            case "curse_11":
                // Existing counterattack effects still use marks after a cast.
                // We do not consult future enemy turns to choose their timing.
                value -= marks * .6 * scale;
                break;
            case "curse_13": value -= 1.5 * scale; break;
            case "curse_14": value -= 3 * scale; break;
            case "tide_01":
            case "tide_02":
            case "tide_03":
                // Actual absorption generates tide next turn; an empty enemy
                // turn cannot make this resource, even if the card gives block.
                value += Math.Min(Math.Max(0, 12 - tide), Math.Min(blockable, effect.Block)) * 1.1 * scale;
                break;
            case "tide_09":
                value += Math.Max(0, N(card, 16) - b.EnemyBlock) * (preview.AttackDamage > 0 ? 1.0 : .55) * damageWeight;
                break;
            case "tide_10": value += Math.Min(preview.AttackDamage / 2d, N(card, 3)) * defenseWeight; break;
            case "thunder_01":
            case "thunder_02":
            case "thunder_03":
            case "thunder_04":
            case "thunder_05":
            case "thunder_11":
            case "thunder_12":
                if (b.Energy > fee) value += Math.Max(0, Math.Min(1, 6 - chain)) * 2.5 * scale;
                value += PairGain(s, slot, "chain", chain, Math.Min(6, chain + 1)) * damageWeight * 2;
                if (key == "thunder_12")
                    value += Math.Min(preview.AttackDamage / 2d, N(card, 2 + chain / 2)) * defenseWeight;
                break;
            case "thunder_06":
                if (HasAffordableAttack(s, slot, b.Energy - fee, "thunder")) value += N(card, 8) * damageWeight * 1.8;
                break;
            case "thunder_09":
                // Spent chain can reduce a visible finisher, but restored energy
                // can also permit more strikes; finite turn budget stays real.
                value -= Math.Min(2, chain) * 1.5 * scale;
                break;
            case "blood_06":
                if (direct > 0 && !b.Boarding.TurnUses.Contains(key))
                    value += Math.Min(missing, Math.Min(direct, N(card, 2))) * healWeight;
                break;
            case "blood_08":
                value += Math.Max(0, N(card, 8) - b.EnemyBlock) * (preview.PlayerHealthLoss > 0 ? 1.6 : .45) * damageWeight;
                break;
            case "blood_10":
                if (direct == 0) value -= card.Draw * (b.Energy > fee ? 4.5 : .8);
                break;
            case "blood_15":
            {
                bool desperate = (long)b.PlayerBoardingHealth * 100 <= (long)b.PlayerMaxBoardingHealth * 40;
                bool nearDesperate = (long)(b.PlayerBoardingHealth - totalThreat) * 100 <= (long)b.PlayerMaxBoardingHealth * 40;
                if (desperate && HasAffordableAttack(s, slot, b.Energy - fee, null)) value += N(card, 8) * damageWeight * 1.8;
                else if (nearDesperate) value += N(card, 8) * damageWeight * .6;
                break;
            }
        }

        if (newSelfBurn > 0)
        {
            value -= newSelfBurn * 1.65 * defenseWeight;
            if (b.Boarding.FirePactTurns > 0 && !b.Boarding.FirePactUsed)
                value += Math.Min(3, newSelfBurn) * 1.7 * damageWeight;
            // Transfer is a present-hand two-card option, not an assumption that
            // a helpful card will be drawn later.
            if (HasAffordableKey(s, slot, b.Energy - fee + recovery, "fire_15"))
                value += Math.Min(newSelfBurn, Math.Max(0, 4 - selfBurn)) * (defenseWeight + damageWeight) * 1.25;
        }
        value -= immediateLife * defenseWeight;
        if (immediateLife + newSelfBurn > 0 && b.PlayerBoardingHealth - immediateLife - newSelfBurn <= totalThreat &&
            effect.Block == 0 && b.Energy - fee + recovery <= 0) value -= 100;
        return value / (fee + .65);
    }

    private static int N(CoreBattleCard card, int value) => CoreBattleUpgradeBalance.ScaleNumerical(value, card.UpgradeLevel);

    private static double PairGain(CoreVoyageSession s, int sourceSlot, string resource, int before, int after)
    {
        if (after <= before) return 0;
        int available = s.Battle.Energy - s.GetBoardingHandEnergyCost(sourceSlot);
        double best = 0;
        for (int slot = 0; slot < s.Battle.Hand.Count; slot++)
        {
            if (slot == sourceSlot || s.GetBoardingHandEnergyCost(slot) > available) continue;
            var card = CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(s.Battle.Hand[slot]));
            string k = card.BoardingSpec?.Key ?? string.Empty;
            double gain = 0;
            if (resource == "mana")
            {
                if (k == "arcane_04") gain = N(card, 24 + 6 * after) - N(card, 24 + 6 * before);
                if (k == "arcane_06") gain = N(card, 5) * (after - before);
                if (k == "arcane_11") gain = (N(card, 36 + 8 * after) - N(card, 36 + 8 * before)) * .78;
                if (k == "arcane_10" && before < 2 && after >= 2 && s.Battle.SuccessfulCardUses(k) == 0)
                    gain = s.GetBattleCardEnergyRecovery(s.Battle.Hand[slot]) * 8;
                if (k == "arcane_05" && before < 2 && after >= 2 && s.Cargo.Medicine > 0 && s.Cargo.Powder >= 0)
                    gain = Math.Min(s.Battle.PlayerMaxBoardingHealth - s.Battle.PlayerBoardingHealth, N(card, 13));
            }
            if (resource == "marks")
            {
                if (k == "curse_06") gain = N(card, 8 + after) - N(card, 8 + before);
                if (k == "curse_11") gain = N(card, 20 + 8 * after) - N(card, 20 + 8 * before);
                if (k == "curse_09") gain = N(card, Math.Min(10, after * 2)) - N(card, Math.Min(10, before * 2));
                if (k == "curse_14" && before < 2 && after >= 2 && s.Battle.SuccessfulCardUses(k) == 0)
                    gain = s.GetBattleCardEnergyRecovery(s.Battle.Hand[slot]) * 8;
                if (k == "curse_13" && before == 0) gain = card.Draw * 3;
            }
            if (resource == "chain")
            {
                if (k == "thunder_02" || k == "thunder_05") gain = N(card, after) - N(card, before);
                if (k == "thunder_11") gain = 2 * (N(card, 3 + after) - N(card, 3 + before));
                if (k == "thunder_13") gain = N(card, 20 + 5 * after) - N(card, 20 + 5 * before);
                if (k == "thunder_14") gain = N(card, 16 + 4 * after) - N(card, 16 + 4 * before);
                if (k == "thunder_15") gain = N(card, 28 + 6 * after) - N(card, 28 + 6 * before);
                if (k == "thunder_07" && before < 2 && after >= 2 && s.Battle.SuccessfulCardUses(k) == 0) gain = card.Draw * 4;
                if (k == "thunder_09" && before < 2 && after >= 2 && s.Battle.SuccessfulCardUses(k) == 0)
                    gain = s.GetBattleCardEnergyRecovery(s.Battle.Hand[slot]) * 8;
            }
            best = Math.Max(best, gain);
        }
        return best;
    }

    private static bool HasAffordableKey(CoreVoyageSession s, int excluded, int available, string key)
    {
        for (int slot = 0; slot < s.Battle.Hand.Count; slot++)
            if (slot != excluded && s.GetBoardingHandEnergyCost(slot) <= available &&
                CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(s.Battle.Hand[slot])).BoardingSpec?.Key == key) return true;
        return false;
    }

    private static bool HasAffordableAttack(CoreVoyageSession s, int excluded, int available, string school)
    {
        for (int slot = 0; slot < s.Battle.Hand.Count; slot++)
        {
            if (slot == excluded || s.GetBoardingHandEnergyCost(slot) > available) continue;
            var card = CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(s.Battle.Hand[slot]));
            if (card.BoardingSpec?.IsAttack == true && (school == null || card.SchoolId == school)) return true;
        }
        return false;
    }

    private static int MaxAffordableSelfBurn(CoreVoyageSession s, int excluded, int available)
    {
        int amount = 0;
        for (int slot = 0; slot < s.Battle.Hand.Count; slot++)
        {
            if (slot == excluded || s.GetBoardingHandEnergyCost(slot) > available || !s.CanPlayCard(slot)) continue;
            string k = CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(s.Battle.Hand[slot])).BoardingSpec?.Key;
            amount = Math.Max(amount, k == "fire_11" ? 3 : k == "fire_13" || k == "fire_14" ? 2 : 0);
        }
        return amount;
    }

    private static int MaxAffordableMarks(CoreVoyageSession s, int excluded, int available)
    {
        int amount = 0;
        for (int slot = 0; slot < s.Battle.Hand.Count; slot++)
        {
            if (slot == excluded || s.GetBoardingHandEnergyCost(slot) > available || !s.CanPlayCard(slot)) continue;
            string k = CoreBoardingCardCatalog.ForBoarding(s.GetBattleCard(s.Battle.Hand[slot])).BoardingSpec?.Key;
            amount = Math.Max(amount, k == "curse_02" ? 3 : k == "curse_01" || k == "curse_03" ? 1 : 0);
        }
        return amount;
    }
}
