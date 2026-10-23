using System;
using System.Linq;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        // Version zero preserves the five-legend layout and one-shot guardian
        // behaviour of checkpoints written before the six-school ship roster.
        private int shipRosterVersion = 1;
        private string voyageRouteBossId = string.Empty;
        private bool currentRouteBossDefeated;

        public string CurrentRouteBossId => voyageRouteBossId;
        public bool CurrentRouteBossDefeated => currentRouteBossDefeated;

        private void AssignNewVoyageRouteBoss(CoreVoyageMap map)
        {
            uint selection = unchecked((uint)Guid.NewGuid().GetHashCode());
            CoreRouteBoss boss = CoreRouteBossCatalog.Choose(map.Route.Id, selection);
            voyageRouteBossId = boss?.Id ?? string.Empty;
            AssignRouteBossEncounter(map, boss);
        }

        private static void AssignRouteBossEncounter(CoreVoyageMap map, CoreRouteBoss boss)
        {
            if (boss == null || map == null || boss.RouteId != map.Route.Id) return;
            CoreMapNode node = map.Nodes.Find(candidate => candidate.IsBoss);
            // Geography was generated using the historical primary guardian.
            // Replacing only its encounter leaves terrain, currents and anchors fixed.
            if (node != null) node.Encounter = boss.CreateEncounter();
        }

        private bool ShouldRemoveDefeatedChartBoss(CoreSeaChartState state, CoreMapNode node)
        {
            if (!state.defeatedBosses.Contains(node.Encounter.Id)) return false;
            if (shipRosterVersion > 0 && CoreRouteBossCatalog.Find(node.Encounter.Id) != null)
                return currentRouteBossDefeated;
            return true;
        }
    }
}
