namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        /// <summary>True only after this voyage has actually sighted its destination.</summary>
        public bool DestinationPortDiscovered { get; private set; }

        public bool IsDestinationBossGuidanceActive => DestinationBossGuidanceNode != null;

        /// <summary>The regional guardian still blocking the sighted destination, if any.</summary>
        public CoreMapNode DestinationBossGuidanceNode
        {
            get
            {
                if (!SupportsDestinationBossGuidance || !DestinationPortDiscovered || bossDefeated) return null;
                return Map.Nodes.Find(node => node.IsBoss && !node.Visited && node.Encounter != null &&
                    node.Encounter.BossKind == CoreBossKind.Route);
            }
        }

        private bool SupportsDestinationBossGuidance => Map != null && !IsTreasureVoyage &&
            GuidedTutorialLeg == 0 && activeSeaChart?.TutorialWaters != true;

        private void ObserveDestinationPort(VoyagePoint observer, float visionRadius)
        {
            if (!SupportsDestinationBossGuidance || DestinationPortDiscovered ||
                Distance(observer, Map.Destination) > visionRadius ||
                !CanSeePoint(observer, Map.Destination, visionRadius, 0f)) return;

            // Historical chart fog and navigator intelligence are deliberately
            // ignored: this is an observation from the current ship's real course.
            DestinationPortDiscovered = true;
            CoreMapNode guardian = DestinationBossGuidanceNode;
            if (guardian == null) return;
            guardian.Discovered = true;
            AddLog("发现目的港「" + Map.DestinationName + "」，航道仍被「" + guardian.Encounter.Name +
                "」封锁。首领位置已标在海图上，请先前往挑战。");
        }

        private void RestoreDestinationPortGuidance(bool discovered, bool legacyCheckpoint)
        {
            DestinationPortDiscovered = discovered && SupportsDestinationBossGuidance;
            if (DestinationPortDiscovered)
            {
                // A persisted guide survives moving away from the port. Repair
                // only the marker disclosure, without replaying its first-sighting log.
                CoreMapNode guardian = DestinationBossGuidanceNode;
                if (guardian != null) guardian.Discovered = true;
            }
            else if (legacyCheckpoint && SupportsDestinationBossGuidance)
            {
                // Old checkpoints lack the flag. Migrate from a current visual
                // observation, never from accumulated exploration or old node intel.
                ObserveDestinationPort(Position,
                    (120f + (HasNavigator ? 60f : 0f) + VisionBonusNm) / Map.Route.NauticalMilesPerMapUnit);
            }
        }
    }
}
