using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    [Serializable]
    public sealed class CoreBoardingField
    {
        public CoreNavalFieldKind Kind;
        public bool PlayerOwned;
        public int RemainingTurns;
        public CoreBoardingField Copy() => (CoreBoardingField)MemberwiseClone();
    }

    /// <summary>Shared boarding terrain, detached from naval coordinates and their clocks.</summary>
    [Serializable]
    public sealed class CoreBoardingFieldState
    {
        public List<CoreBoardingField> Fields = new List<CoreBoardingField>();

        public int Remaining(CoreNavalFieldKind kind)
        {
            int remaining = 0;
            foreach (var field in Fields)
                if (field != null && field.Kind == kind && kind != CoreNavalFieldKind.Tide)
                    remaining = Math.Max(remaining, field.RemainingTurns);
            return remaining;
        }

        public bool Contains(CoreNavalFieldKind kind) => Remaining(kind) > 0;

        public void EndTurn(bool player)
        {
            foreach (var field in Fields)
                if (field != null && field.PlayerOwned != player)
                    field.RemainingTurns = Math.Max(0, field.RemainingTurns - 1);
            Fields.RemoveAll(field => field == null || field.RemainingTurns <= 0 || field.Kind == CoreNavalFieldKind.Tide);
        }

        public CoreBoardingFieldState Copy()
        {
            var result = new CoreBoardingFieldState();
            foreach (var field in Fields) if (field != null) result.Fields.Add(field.Copy());
            return result;
        }
    }

    public sealed partial class CoreBattleState
    {
        public CoreBoardingFieldState BoardingFields { get; internal set; } = new CoreBoardingFieldState();
    }

    public sealed partial class CoreVoyageSession
    {
        // Capture before ram changes DistanceMeters: that setter also relocates the player.
        private CoreBoardingFieldState CaptureBoardingFields(bool includePlayer, bool includeEnemy)
        {
            var result = new CoreBoardingFieldState();
            if (!NavalFieldsActive) return result;
            if (includePlayer) AddBoardingFieldSource(result, NavalFieldAt(Battle.PlayerPositionMeters));
            if (includeEnemy) AddBoardingFieldSource(result, NavalFieldAt(Battle.EnemyPositionMeters));
            return result;
        }

        private void AddBoardingFieldSource(CoreBoardingFieldState result, CoreNavalFieldCell source)
        {
            if (source == null || source.Kind == CoreNavalFieldKind.Tide) return;
            int remaining = GetNavalFieldRemainingTurns(source);
            if (remaining <= 0) return;
            var existing = result.Fields.Find(field => field.Kind == source.Kind);
            if (existing != null)
            {
                if (remaining < existing.RemainingTurns) return;
                // Only ownership differs on a tie; choose once when entering, never in a preview.
                if (remaining == existing.RemainingTurns &&
                    (existing.PlayerOwned == source.PlayerOwned || voyageRandom.NextDouble() >= .5d)) return;
                existing.RemainingTurns = remaining;
                existing.PlayerOwned = source.PlayerOwned;
            }
            else result.Fields.Add(new CoreBoardingField {
                Kind = source.Kind, PlayerOwned = source.PlayerOwned, RemainingTurns = remaining
            });
        }

        private void BBoardingFieldDamage(bool player, int amount)
        {
            if (!BActive || amount <= 0) return;
            if (player)
            {
                int absorbed = Math.Min(Battle.PlayerBlock, amount);
                Battle.PlayerBlock -= absorbed;
                BRecordBlockedDamage(absorbed);
                int lost = Math.Min(Battle.PlayerBoardingHealth, amount - absorbed);
                Battle.PlayerBoardingHealth -= lost;
                if (BResolveCurseExecutions()) return;
                if (lost > 0) CTBAddMarks(BCurseContractAmount);
            }
            else
            {
                int absorbed = Math.Min(Battle.EnemyBlock, amount);
                Battle.EnemyBlock -= absorbed;
                int lost = Math.Min(Battle.EnemyBoardingHealth, amount - absorbed);
                Battle.EnemyBoardingHealth -= lost;
                OnBoardingEnemyDamaged(absorbed, lost);
            }
            BResolveCurseExecutions();
        }
    }
}
