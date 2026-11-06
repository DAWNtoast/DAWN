using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        // Both intent and execution choose from the same legal anchors after movement.
        // Field footprints grow north/east, and the anchor must be within four cells.
        private static bool TryNavalBossFieldPlacement(CoreNavalBossMove move, VoyagePoint enemy,
            VoyagePoint player, out int x, out int y, out int dx, out int dy)
        {
            x = y = dx = dy = 0;
            if (move?.FieldKind == null) return false;
            CoreNavalFieldKind kind = move.FieldKind.Value;
            int size = GetNavalFieldSize(kind), ex = FieldGrid(enemy.X), ey = FieldGrid(enemy.Y);
            int px = FieldGrid(player.X), py = FieldGrid(player.Y);
            bool hostile = kind == CoreNavalFieldKind.Fire || kind == CoreNavalFieldKind.Curse || kind == CoreNavalFieldKind.Tide;
            int tx = hostile ? px : ex, ty = hostile ? py : ey;

            // Keep thunder's broad, pursuit-oriented self-covering footprint.
            if (kind == CoreNavalFieldKind.Thunder)
            {
                NavalBossThunderFieldAnchor(enemy, player, out x, out y);
                if (NavalBossFieldAnchorValid(ex, ey, x, y, size)) return true;
            }

            long best = long.MaxValue;
            for (int ox = -NavalCannonRangeCells; ox <= NavalCannonRangeCells; ox++)
                for (int oy = -NavalCannonRangeCells; oy <= NavalCannonRangeCells; oy++)
                {
                    int ax = ex + ox, ay = ey + oy;
                    if (!NavalBossFieldAnchorValid(ex, ey, ax, ay, size)) continue;
                    int gap = NavalBossFieldDistance(tx, ty, ax, ay, size);
                    bool unwantedCoverage = hostile
                        ? NavalBossFieldDistance(ex, ey, ax, ay, size) == 0
                        : NavalBossFieldDistance(px, py, ax, ay, size) == 0;
                    // Cover the intended ship first, avoid sharing the field next,
                    // then center the target for a useful footprint on every bearing.
                    long score = gap * 10000L + (unwantedCoverage ? 1000L : 0L) +
                        (Math.Abs(2L * tx - (2L * ax + size - 1)) + Math.Abs(2L * ty - (2L * ay + size - 1))) * 10L +
                        Math.Abs(ox) + Math.Abs(oy);
                    if (score >= best) continue;
                    best = score; x = ax; y = ay;
                }
            if (best == long.MaxValue) return false;
            if (kind == CoreNavalFieldKind.Tide)
            {
                // Prefer the dominant approach axis. The ordinary forced-movement
                // validator still prevents landing on either occupied ship cell.
                if (Math.Abs(ex - px) >= Math.Abs(ey - py) && ex != px) dx = Math.Sign(ex - px);
                else if (ey != py) dy = Math.Sign(ey - py);
                else dx = 1;
            }
            return true;
        }

        private static bool NavalBossFieldAnchorValid(int ex, int ey, int x, int y, int size) =>
            Math.Abs((long)x - ex) + Math.Abs((long)y - ey) <= NavalCannonRangeCells &&
            FieldCellValid(x, y) && FieldCellValid(x + size - 1, y + size - 1);

        private static int NavalBossFieldDistance(int tx, int ty, int x, int y, int size) =>
            Math.Max(0, Math.Max(x - tx, tx - (x + size - 1))) +
            Math.Max(0, Math.Max(y - ty, ty - (y + size - 1)));

        private static void NavalBossThunderFieldAnchor(VoyagePoint enemy, VoyagePoint player, out int x, out int y)
        {
            x = FieldGrid(enemy.X); y = FieldGrid(enemy.Y);
            bool west = player.X < enemy.X, south = player.Y < enemy.Y;
            int reach = Math.Min(4, Math.Max(0, GetNavalFieldSize(CoreNavalFieldKind.Thunder) - 1));
            if (west) x -= south ? Math.Min(2, reach) : reach;
            if (south) y -= west ? Math.Min(2, reach) : reach;
        }

        public const int NavalBossFieldMaxRemainingTurns = 6;
        public static int GetNavalBossFieldCellLimit(CoreNavalFieldKind kind) =>
            GetNavalFieldSize(kind) * GetNavalFieldSize(kind) + 16;

        private sealed class NavalBossFieldMutation
        {
            internal bool Recast;
            internal int Added, Extended, X, Y, DirectionX, DirectionY;
        }

        private static List<CoreNavalFieldCell> NavalBossFieldSources(CoreNavalFieldState state,
            CoreNavalFieldKind kind, VoyagePoint enemy)
        {
            var instances = new HashSet<int>();
            foreach (CoreNavalFieldCell cell in state.Cells)
                if (!cell.PlayerOwned && cell.Kind == kind && cell.ExpiresAtOwnerTurn > state.CompletedPlayerTurns &&
                    NavalGridDistance(cell.PositionMeters, enemy) <= NavalCannonRangeCells) instances.Add(cell.InstanceId);
            var sources = new List<CoreNavalFieldCell>();
            foreach (CoreNavalFieldCell cell in state.Cells)
                if (!cell.PlayerOwned && cell.Kind == kind && cell.ExpiresAtOwnerTurn > state.CompletedPlayerTurns &&
                    instances.Contains(cell.InstanceId)) sources.Add(cell);
            sources.Sort((left, right) => {
                int order = left.InstanceId.CompareTo(right.InstanceId);
                if (order == 0) order = left.X.CompareTo(right.X);
                return order == 0 ? left.Y.CompareTo(right.Y) : order;
            });
            return sources;
        }

        // The same deterministic transformation drives live execution, all damage/risk
        // quotes and tooltip descriptions. Only live execution emits presentation events.
        private static NavalBossFieldMutation ApplyNavalBossFieldAction(CoreNavalFieldState state,
            CoreNavalBossMove move, VoyagePoint enemy, VoyagePoint player, bool recordEvents)
        {
            var result = new NavalBossFieldMutation();
            if (move?.FieldKind == null || move.FieldAction == CoreNavalBossFieldAction.None) return result;
            CoreNavalFieldKind kind = move.FieldKind.Value;
            List<CoreNavalFieldCell> sources = NavalBossFieldSources(state, kind, enemy);
            if (move.FieldAction == CoreNavalBossFieldAction.Place || sources.Count == 0)
            {
                if (!TryNavalBossFieldPlacement(move, enemy, player, out int x, out int y, out int dx, out int dy)) return result;
                result.Recast = true; result.X = x; result.Y = y; result.DirectionX = dx; result.DirectionY = dy;
                int size = GetNavalFieldSize(kind);
                // Refreshing replaces the boss's previous same-school footprint, so
                // consecutive chains cannot accumulate distant permanent field islands.
                state.Cells.RemoveAll(cell => {
                    bool overlapped = cell.X >= x && cell.X < x + size && cell.Y >= y && cell.Y < y + size;
                    bool retired = !cell.PlayerOwned && cell.Kind == kind;
                    if (recordEvents && (overlapped || retired)) state.RecordPresentation(
                        overlapped ? CoreNavalFieldChangeKind.Overwritten : CoreNavalFieldChangeKind.Removed, cell);
                    return overlapped || retired;
                });
                int instance = state.NextInstanceId++;
                int expires = SchoolSum(state.CompletedPlayerTurns, Math.Min(NavalBossFieldMaxRemainingTurns, Math.Max(1, move.FieldTurns)));
                for (int ix = 0; ix < size; ix++) for (int iy = 0; iy < size; iy++)
                {
                    var cell = new CoreNavalFieldCell { X = x + ix, Y = y + iy, Kind = kind, PlayerOwned = false,
                        InstanceId = instance, ExpiresAtOwnerTurn = expires, DirectionX = dx, DirectionY = dy };
                    state.Cells.Add(cell); result.Added++;
                    if (recordEvents) state.RecordPresentation(CoreNavalFieldChangeKind.Placed, cell);
                }
                return result;
            }
            if (move.FieldAction == CoreNavalBossFieldAction.Extend)
            {
                int cap = SchoolSum(state.CompletedPlayerTurns, NavalBossFieldMaxRemainingTurns);
                foreach (CoreNavalFieldCell cell in sources)
                {
                    int expiry = Math.Max(cell.ExpiresAtOwnerTurn,
                        Math.Min(cap, SchoolSum(cell.ExpiresAtOwnerTurn, Math.Max(0, move.FieldExtendTurns))));
                    if (expiry == cell.ExpiresAtOwnerTurn) continue;
                    cell.ExpiresAtOwnerTurn = expiry; result.Extended++;
                    if (recordEvents) state.RecordPresentation(CoreNavalFieldChangeKind.Extended, cell);
                }
                return result;
            }
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            int existing = 0;
            foreach (CoreNavalFieldCell cell in state.Cells)
            {
                occupied.Add(cell.X + ":" + cell.Y);
                if (!cell.PlayerOwned && cell.Kind == kind && cell.ExpiresAtOwnerTurn > state.CompletedPlayerTurns) existing++;
            }
            int room = Math.Min(Math.Max(0, move.FieldExpansionCells), Math.Max(0, GetNavalBossFieldCellLimit(kind) - existing));
            if (room == 0) return result;
            var candidates = new Dictionary<string, CoreNavalFieldCell>(StringComparer.Ordinal);
            int radius = NavalCannonRangeCells + GetNavalFieldSize(kind) - 1;
            foreach (CoreNavalFieldCell source in sources)
                foreach (var offset in new[] { new[] { -1, 0 }, new[] { 0, -1 }, new[] { 0, 1 }, new[] { 1, 0 } })
                {
                    CoreNavalFieldCell cell = source.Copy(); cell.X += offset[0]; cell.Y += offset[1];
                    string key = cell.X + ":" + cell.Y;
                    if (!FieldCellValid(cell.X, cell.Y) || occupied.Contains(key) || NavalGridDistance(cell.PositionMeters, enemy) > radius) continue;
                    if (!candidates.TryGetValue(key, out CoreNavalFieldCell prior) || cell.ExpiresAtOwnerTurn > prior.ExpiresAtOwnerTurn ||
                        cell.ExpiresAtOwnerTurn == prior.ExpiresAtOwnerTurn && cell.InstanceId < prior.InstanceId) candidates[key] = cell;
                }
            bool offensive = kind == CoreNavalFieldKind.Fire || kind == CoreNavalFieldKind.Curse || kind == CoreNavalFieldKind.Tide;
            VoyagePoint target = offensive ? player : enemy;
            var ordered = new List<CoreNavalFieldCell>(candidates.Values);
            ordered.Sort((left, right) => {
                int order = NavalGridDistance(left.PositionMeters, target).CompareTo(NavalGridDistance(right.PositionMeters, target));
                if (order == 0) order = left.X.CompareTo(right.X);
                return order == 0 ? left.Y.CompareTo(right.Y) : order;
            });
            for (int i = 0; i < ordered.Count && i < room; i++)
            {
                CoreNavalFieldCell cell = ordered[i]; state.Cells.Add(cell); result.Added++;
                if (recordEvents) state.RecordPresentation(CoreNavalFieldChangeKind.Placed, cell);
            }
            return result;
        }

        private void PlaceNavalBossField(CoreNavalBossMove move) => ApplyNavalBossFieldAction(
            Battle.NavalFields, move, Battle.EnemyPositionMeters, NavalPlayerPositionForEnemyPlan, true);

        private void PreviewNavalBossPlacedField(CoreNavalFieldState state, VoyagePoint enemy, VoyagePoint player)
        {
            EnemyBattlePlan plan = BuildEnemyPlan();
            if (plan.Action == EnemyBattleAction.Cannon && !NavalCannonInRange(player, enemy)) return;
            ApplyNavalBossFieldAction(state, plan.BossMove, enemy, player, false);
        }

        private string NavalBossFieldDescription(CoreNavalBossMove move, VoyagePoint enemy, VoyagePoint player)
        {
            if (move?.FieldKind == null) return string.Empty;
            CoreNavalFieldState state = CreateNavalFieldPreviewContext(false).State;
            NavalBossFieldMutation change = ApplyNavalBossFieldAction(state, move, enemy, player, false);
            string name = CoreComponentFieldCards.FieldName(move.FieldKind.Value);
            if (!change.Recast)
            {
                if (move.FieldAction == CoreNavalBossFieldAction.Extend)
                    return "行动前延长4格内相连的自有" + name + "整处 " + move.FieldExtendTurns +
                        " 回合（最多剩余 " + NavalBossFieldMaxRemainingTurns + " 个玩家回合），本次延长 " + change.Extended + " 格";
                return "行动前将自有" + name + "原边界向相邻空格扩展，本次新增 " + change.Added + " 格（单次最多 " +
                    move.FieldExpansionCells + " 格，总计最多 " + GetNavalBossFieldCellLimit(move.FieldKind.Value) + " 格）";
            }
            int size = GetNavalFieldSize(move.FieldKind.Value);
            string direction = move.FieldKind == CoreNavalFieldKind.Tide
                ? "，水流向" + (change.DirectionX > 0 ? "东" : change.DirectionX < 0 ? "西" : change.DirectionY > 0 ? "北" : "南") + "，推动遇船受阻" : string.Empty;
            return (move.FieldAction == CoreNavalBossFieldAction.Place ? "行动前" : "4格内没有可用自有场地，行动前补场：") +
                "以（" + change.X + "，" + change.Y + "）为左下角，向东北铺设 " + size + "×" + size + " " + name +
                "，持续 " + Math.Min(NavalBossFieldMaxRemainingTurns, Math.Max(1, move.FieldTurns)) + " 个玩家回合" + direction;
        }
    }
}
