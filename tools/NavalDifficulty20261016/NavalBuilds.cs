using System;
using System.Collections.Generic;
using System.Linq;
using ShatteredSail.Core.Gameplay;

internal static class NavalBuilds
{
    public static CoreVoyageSession Create(string build, int route, int seed)
    {
        bool starter = string.Equals(build, "starter", StringComparison.OrdinalIgnoreCase);
        bool mixed = string.Equals(build, "mixed", StringComparison.OrdinalIgnoreCase);
        bool mature = !starter && !mixed;
        string[] parts = build.Split('_');
        string tier = parts[0].ToUpperInvariant();
        string school = parts.Length == 1 ? "tide" : parts[1].ToLowerInvariant();
        if (mature && (parts.Length > 2 || (tier != "P" && tier != "H" && tier != "M") ||
            (school != "tide" && school != "thunder" && school != "blood")))
            throw new ArgumentException("Unknown naval fixture: " + build);
        int rank = mature ? (tier == "M" ? 3 : 2) : (mixed ? 1 : 0);
        int sourceExperience = !mature ? 0 : tier == "P" ? 120 : tier == "H" ? 360 : 840;
        int minimumLevel = mature ? (school == "tide" ? 5 : school == "thunder" ? 7 : 10) : route == 2 ? 5 : 1;
        var session = new CoreVoyageSession();
        session.SelectShip(starter ? 0 : mixed ? 4 : tier == "P" ? 2 : 7);
        session.RestoreTreasureProgress(new CoreTreasureProgress {
            pirateExperience = CoreVoyageSession.PirateExperienceForLevel(Math.Max(minimumLevel,
                CoreVoyageSession.Routes[route].UnlockLevel)) });
        session.SelectRoute(route);
        session.SetSeed("Naval-Audit-20261016-" + seed);
        string[] crew = starter ? Array.Empty<string>() : mixed ? new[] {
            "crew_broken_anchor_cook_tam", "crew_salt_crown_diver_isha" } : school == "tide" ? new[] {
            "crew_white_shell_doctor_mirei", "crew_bronze_bell_cook_basin" } : school == "thunder" ? new[] {
            "crew_bronze_bell_doctor_ada", "crew_gray_gull_diver_noya" } : new[] {
            "crew_broken_anchor_doctor_hector", "crew_salt_crown_diver_isha" };
        session.ConfigureDepartureCrew(starter ? 10 : mixed ? 14 : tier == "P" ? 20 : 26, crew.Length);
        session.ConfigureCrewCapabilities(false, false, mature, false, mixed || (mature && school == "tide"),
            mixed || (mature && school != "tide"));
        session.ConfigureSpecialCardSources(crew, Array.Empty<string>());
        session.ConfigureComponentBonuses(0, 0, 0, 0, 0);
        session.ConfigureBattleEquipment(1f, 1f);
        var upgrades = new Dictionary<string, int>();
        if (mixed)
            foreach (string id in new[] { "card_chain_shot", "card_cannon_barrage", "card_evasive_maneuver" })
                upgrades[id] = Math.Min(1, session.GetCardMaximumUpgradeLevel(id));
        if (mature)
            foreach (string id in PreparationDeck(school).Concat(NavalDeck(school)).Distinct())
                upgrades[id] = Math.Min(rank, session.GetCardMaximumUpgradeLevel(id));
        session.ConfigureCardUpgrades(upgrades);
        // Both hired sources are Boarding-only. The real default Naval/Preparation
        // decks are kept; no boarding card or fictional source bonus is injected.
        session.RestoreSpecialCardExperience(session.AcquiredSpecialCardIds.ToDictionary(id => id, id => sourceExperience));
        if (mature)
        {
            SetDeck(session, NavalBattleStage.Preparation, PreparationDeck(school));
            SetDeck(session, NavalBattleStage.Naval, NavalDeck(school));
        }
        SetSupplies(session, starter);
        session.StartVoyage();
        return session;
    }

    // Six resource-producing/consuming school cards plus four ordinary support
    // cards. The two real crew sources remain Boarding-only and add no naval cards.
    private static string[] NavalDeck(string school) => new[] {
        "card_school_" + school + "_b03", "card_school_" + school + "_b03",
        "card_school_" + school + "_b04", "card_school_" + school + "_b04",
        "card_school_" + school + "_b05", "card_school_" + school + "_b06",
        "card_energy_order", "card_energy_order", "card_tactical_draw", "card_tactical_draw"
    };
    private static string[] PreparationDeck(string school) => new[] {
        "card_school_" + school + "_b01", "card_school_" + school + "_b01",
        "card_school_" + school + "_b02", "card_school_" + school + "_b02",
        "card_load_shot", "card_load_shot",
        "card_energy_order", "card_energy_order", "card_tactical_draw", "card_tactical_draw"
    };

    public static int PaidUpgradeGold(CoreVoyageSession session) => session.CardUpgradeLevels.Sum(pair =>
        Enumerable.Range(0, pair.Value).Sum(CoreBattleUpgradeBalance.CostForNextLevel));

    private static void SetDeck(CoreVoyageSession session, NavalBattleStage stage, IEnumerable<string> ids)
    {
        int[] available = session.GetAvailableBattleCards(stage).ToArray();
        int[] deck = ids.Select(id => available.Single(index => session.GetBattleCard(index).Id == id)).ToArray();
        if (!session.SetBattleDeck(stage, deck))
            throw new InvalidOperationException("Illegal naval fixture deck for " + stage);
    }

    private static void SetSupplies(CoreVoyageSession session, bool starter)
    {
        session.Cargo.Water = session.Cargo.Food = starter ? 8 : 20;
        session.Cargo.Wood = starter ? 8 : 20;
        session.Cargo.Medicine = starter ? 4 : 8;
        session.Cargo.Powder = starter ? 12 : 25;
        session.Cargo.Cannonballs = starter ? 16 : 40;
        if (session.Cargo.UsedVolume > session.CargoCapacityValue)
            throw new InvalidOperationException("Naval fixture exceeds real cargo capacity: " +
                session.Cargo.UsedVolume + "/" + session.CargoCapacityValue);
    }
}
