using System.Collections.Generic;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Loads only the deck and cast needed by the current boarding view.</summary>
    public static class BoardingBattleArtCatalog
    {
        private static readonly string[] Environments = { "open", "coast", "strait", "reef", "fog", "storm" };
        private static readonly string[] Times = { "day", "dusk", "night" };
        private static readonly Texture2D[] Backgrounds = new Texture2D[18];
        private static readonly bool[] BackgroundLoaded = new bool[18];
        private static readonly Texture2D[] Parties = new Texture2D[2];
        private static readonly bool[] PartyLoaded = new bool[2];
        private static readonly Dictionary<string, Texture2D> Cast = new Dictionary<string, Texture2D>(System.StringComparer.Ordinal);
        private const string CharacterRoot = "BoardingBattle/Characters/";
        private static readonly string[] LegacyCrew = { "crew_navigator_evelyn_fullbody", "crew_carpenter_barlow_fullbody",
            "crew_doctor_moer_fullbody", "crew_gunner_leisa_fullbody", "crew_cook_pepe_fullbody", "crew_diver_noya_fullbody" };

        /// <summary>Releases shared art references and attempted-load flags without destroying borrowed textures.</summary>
        public static void ReleaseLoadedTextures()
        {
            System.Array.Clear(Backgrounds, 0, Backgrounds.Length);
            System.Array.Clear(BackgroundLoaded, 0, BackgroundLoaded.Length);
            System.Array.Clear(Parties, 0, Parties.Length);
            System.Array.Clear(PartyLoaded, 0, PartyLoaded.Length);
            Cast.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => ReleaseLoadedTextures();

        public static string EnvironmentResourcePath(SeaTerrainType terrain, NavigationTimeOfDay time) =>
            "BoardingBattle/Backgrounds/boarding_" + Environments[EnvironmentIndex(terrain)] + "_" + Times[TimeIndex(time)] + "_v1";

        public static Texture2D ForEnvironment(SeaTerrainType terrain, NavigationTimeOfDay time)
        {
            int index = EnvironmentIndex(terrain) * Times.Length + TimeIndex(time);
            if (!BackgroundLoaded[index])
            {
                Backgrounds[index] = Load(EnvironmentResourcePath(terrain, time));
                BackgroundLoaded[index] = true;
            }
            return Backgrounds[index];
        }

        public static Texture2D ForParty(bool enemy)
        {
            int index = enemy ? 1 : 0;
            if (!PartyLoaded[index])
            {
                Parties[index] = Load("BoardingBattle/Characters/boarding_" + (enemy ? "enemy" : "player") + "_party_v1");
                PartyLoaded[index] = true;
            }
            return Parties[index];
        }

        public static Texture2D ForEnemy(CoreBattleState battle, CoreEncounter encounter, int routeIndex)
        {
            Texture2D texture = LoadCast(EnemyResourcePath(battle, encounter, routeIndex));
            return texture != null ? texture : ForParty(true);
        }

        public static string EnemyResourcePath(CoreBattleState battle, CoreEncounter encounter, int routeIndex)
        {
            string identity = encounter == null ? string.Empty : encounter.Id;
            switch (identity)
            {
                case "gold_customs": return EnemyPath("imperial");
                case "moon_ghost": return EnemyPath("ghost");
                case "boss_route_shattered_sail_reef": return EnemyPath("route_reef");
                case "boss_route_golden_tide_trade": return EnemyPath("route_gold");
                case "boss_route_moon_tide_ruins": return EnemyPath("route_moon");
                case CoreBattleBalance.TreasureBossId: return EnemyPath("treasure");
                case "legendary_pirate_iron_oath": return EnemyPath("iron_oath");
                case "legendary_pirate_thunder_crown": return EnemyPath("thunder_crown");
                case "legendary_pirate_red_widow": return EnemyPath("red_widow");
                case "legendary_pirate_ember_ark": return EnemyPath("ember_ark");
                case "legendary_pirate_tide_requiem": return EnemyPath("tide_requiem");
                case CoreGuidedTutorialContent.BossEventId: return EnemyPath(null);
            }
            CoreBattleProfile profile = battle == null ? null : battle.Profile;
            // An exact immutable profile can recover a named enemy in an old
            // preview/session with no encounter pointer. Merely sharing an
            // archetype does not turn an unknown captain into a named legend.
            if (profile != null)
            {
                if (ReferenceEquals(profile, CoreGuidedTutorialContent.BossProfile)) return EnemyPath(null);
                foreach (CoreLegendaryWarship ship in CoreLegendaryWarships.All)
                    if (ReferenceEquals(profile, ship.Profile))
                        return EnemyPath(ship.Id.Substring("legendary_pirate_".Length));
                if (profile.BossKind == CoreBossKind.Treasure) return EnemyPath("treasure");
                if (profile.BossKind == CoreBossKind.Route)
                    return EnemyPath(routeIndex <= 0 ? "route_reef" : routeIndex == 1 ? "route_gold" : "route_moon");
            }
            else if (battle != null && battle.IsBoss)
                return EnemyPath(routeIndex <= 0 ? "route_reef" : routeIndex == 1 ? "route_gold" : "route_moon");
            return EnemyPath(null);
        }

        public static Texture2D ForCrew(CrewVariantDefinition crew)
        {
            if (crew == null) return null;
            Texture2D atlas = CrewAtlas(crew);
            return atlas != null ? atlas : LoadCast("PortArt/CrewFullBody/" + LegacyCrew[Mathf.Clamp(crew.ProfessionIndex, 0, 5)]);
        }

        public static Rect CrewUv(CrewVariantDefinition crew)
        {
            if (crew == null || CrewAtlas(crew) == null) return new Rect(0f, 0f, 1f, 1f);
            int index = Mathf.Clamp(crew.ProfessionIndex, 0, 5);
            const float width = 1f / 3f, height = .5f, inset = .01f;
            return new Rect((index % 3 + inset) * width, (1 - index / 3 + inset) * height,
                width * (1f - inset * 2f), height * (1f - inset * 2f));
        }

        private static Texture2D CrewAtlas(CrewVariantDefinition crew)
        {
            string port;
            switch (crew.PortName)
            {
                case "断锚港": port = "broken_anchor"; break;
                case "白贝港": port = "white_shell"; break;
                case "铜钟港": port = "bronze_bell"; break;
                case "盐冠港": port = "salt_crown"; break;
                case "暮潮港": port = "twilight_tide"; break;
                case "灰鸥港": port = "gray_gull"; break;
                default: return null;
            }
            return LoadCast(CharacterRoot + "boarding_crew_" + port + "_v2");
        }

        private static string EnemyPath(string key) => CharacterRoot + "boarding_enemy_" + (key ?? "party") + "_v1";

        private static Texture2D LoadCast(string path)
        {
            if (!Cast.TryGetValue(path, out Texture2D texture))
            {
                texture = Load(path);
                Cast.Add(path, texture);
            }
            return texture;
        }

        private static int EnvironmentIndex(SeaTerrainType terrain)
        {
            switch (terrain)
            {
                case SeaTerrainType.Beach:
                case SeaTerrainType.Shoal:
                case SeaTerrainType.KelpField:
                case SeaTerrainType.Island: return 1;
                case SeaTerrainType.Strait: return 2;
                case SeaTerrainType.Reef: return 3;
                case SeaTerrainType.FogBank: return 4;
                case SeaTerrainType.StormFront: return 5;
                default: return 0;
            }
        }

        private static int TimeIndex(NavigationTimeOfDay time) =>
            time == NavigationTimeOfDay.Dusk ? 1 : time == NavigationTimeOfDay.Night ? 2 : 0;

        private static Texture2D Load(string path)
        {
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
            }
            return texture;
        }
    }
}
