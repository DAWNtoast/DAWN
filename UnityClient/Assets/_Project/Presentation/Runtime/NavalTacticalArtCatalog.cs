using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Resolves tactical art from authored identities, independently of combat geometry.</summary>
    public static class NavalTacticalArtCatalog
    {
        private const string ShipRoot = "NavalTactics/Ships/";

        public static string PlayerShipResourcePath(string shipId)
        {
            foreach (CoreShipDefinition ship in CoreVoyageSession.Ships)
                if (ship.Id == shipId) return ShipPath(ship.Id);
            return ShipPath("sloop");
        }

        public static string EnemyShipResourcePath(CoreBattleState battle, CoreEncounter encounter, int routeIndex)
        {
            string identity = encounter?.Id;
            CoreLegendaryWarship legendary = CoreLegendaryWarships.Find(identity);
            if (legendary != null) return ShipPath("legendary_" + legendary.SchoolId);
            CoreRouteBoss boss = CoreRouteBossCatalog.Find(identity);
            if (boss != null) return ShipPath("boss_" + boss.SchoolId);
            switch (identity)
            {
                case "gold_customs": return ShipPath("patrol");
                case "moon_ghost":
                case "event_moon_open_water_ambush":
                case "event_moon_v2_moon_taxcollector":
                case "treasure_ghost_fleet": return ShipPath("occult");
                case "treasure_rival_sloop":
                case "event_reef_open_water_ambush": return ShipPath("sloop");
                case CoreBattleBalance.TreasureBossId: return ShipPath("relic");
                case CoreGuidedTutorialContent.BossEventId:
                case CoreGuidedTutorialContent.AmbushEventId:
                case "event_reef_coral_ambush":
                case "event_trade_open_water_ambush":
                case "event_trade_merchant_distress":
                case "event_trade_v2_powder_barge":
                case "event_reef_v2_whaler_signal":
                case "random_ambush":
                case "random_ambush_warning": return ShipPath("privateer");
            }

            CoreBattleProfile profile = battle?.Profile;
            if (profile != null)
            {
                // Only an exact authored profile recovers a named identity when
                // an old preview or checkpoint omits the encounter pointer.
                if (ReferenceEquals(profile, CoreGuidedTutorialContent.BossProfile)) return ShipPath("privateer");
                foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All)
                    if (ReferenceEquals(profile, ship.Profile)) return ShipPath("legendary_" + ship.SchoolId);
                foreach (CoreRouteBoss routeBoss in CoreRouteBossCatalog.All)
                    if (ReferenceEquals(profile, routeBoss.Profile)) return ShipPath("boss_" + routeBoss.SchoolId);
                if (profile.BossKind == CoreBossKind.Treasure) return ShipPath("relic");
                if (profile.BossKind == CoreBossKind.Route) return DefaultRouteBossPath(routeIndex);
                switch (profile.Archetype)
                {
                    case CoreBattleArchetype.Ironclad:
                    case CoreBattleArchetype.Artillery:
                    case CoreBattleArchetype.Ember: return ShipPath("gunship");
                    case CoreBattleArchetype.Raider: return ShipPath("privateer");
                    case CoreBattleArchetype.Tempest: return ShipPath("clipper");
                }
            }
            else if (battle != null && battle.IsBoss) return DefaultRouteBossPath(routeIndex);
            return ShipPath(routeIndex == 2 ? "relic" : "merchant");
        }

        public static string SeaResourcePath(SeaTerrainType terrain)
        {
            string key;
            switch (terrain)
            {
                case SeaTerrainType.Beach:
                case SeaTerrainType.Shoal:
                case SeaTerrainType.KelpField:
                case SeaTerrainType.Island: key = "coast"; break;
                case SeaTerrainType.Reef: key = "reef"; break;
                case SeaTerrainType.Strait: key = "strait"; break;
                case SeaTerrainType.FogBank: key = "fog"; break;
                case SeaTerrainType.StormFront: key = "storm"; break;
                default: key = "open"; break;
            }
            return "NavalTactics/Seas/" + key + "_v2";
        }

        public static Sprite ForPlayer(string shipId) => LoadShip(PlayerShipResourcePath(shipId));
        public static Sprite ForEnemy(CoreBattleState battle, CoreEncounter encounter, int routeIndex) =>
            LoadShip(EnemyShipResourcePath(battle, encounter, routeIndex));

        public static Texture2D ForSea(SeaTerrainType terrain)
        {
            Texture2D texture = Resources.Load<Texture2D>(SeaResourcePath(terrain)) ??
                Resources.Load<Texture2D>("NavalTactics/tactical_sea_v1");
            if (texture != null)
            {
                // Complete paintings are clamped, never repeated across a visible seam.
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
            }
            return texture;
        }

        private static Sprite LoadShip(string path) => Resources.Load<Sprite>(path) ??
            Resources.Load<Sprite>("NavalTactics/tactical_ship_v1");
        private static string ShipPath(string key) => ShipRoot + key + "_v2";
        private static string DefaultRouteBossPath(int routeIndex) =>
            ShipPath("boss_" + (routeIndex <= 0 ? "fire" : routeIndex == 1 ? "blood" : "curse"));
    }
}
