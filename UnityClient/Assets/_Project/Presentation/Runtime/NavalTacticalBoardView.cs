using System.Collections;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    /// <summary>Projects the nautical world around the enemy while keeping the player's ship on the left.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(RectMask2D))]
    public sealed class NavalTacticalBoardView : MonoBehaviour
    {
        private const float InitialPixelsPerMetre = .75f;
        private const float ShipScreenHeight = 170f;
        // A complete sea painting covers a fixed world square. Camera and UI
        // changes crop the painting instead of stretching waves or tiling edges.
        private const float SeaWorldSpanMeters = 4000f;
        private RectTransform board, root, worldLayer, backgroundRect, gridRect, seaViewport;
        private RawImage sea;
        private Image seaVeil;
        private NavalSeaLightGraphic seaLight;
        private CoreVoyageSession environmentSession;
        private CoreBattleState displayedBattle;
        private SeaTerrainType displayedTerrain;
        private double displayedGameHours;
        private bool hasEnvironment;
        private RectTransform playerShip, enemyShip, playerBar, enemyBar;
        private NavalTacticalGridGraphic grid;
        private NavalTacticalTrackGraphic track;
        private NavalTacticalCompassGraphic compassGraphic;
        private RectTransform compassNorth;
        private readonly List<Vector2> playerTrack = new List<Vector2>();
        private readonly List<Vector2> enemyTrack = new List<Vector2>();
        private IReadOnlyList<CoreNavalTacticalFrame> pathFrames;
        private int lastMovementRevision = -1, animationGeneration;
        private bool built, hasFrame;
        private float pixelsPerMetre = InitialPixelsPerMetre;
        private float shipScreenHeight = ShipScreenHeight;
        private CoreNavalTacticalFrame currentFrame;

        public Vector2 PlayerScreenPosition { get; private set; }
        public Vector2 EnemyScreenPosition { get; private set; }
        public float CameraRotationDegrees { get; private set; }
        public float CellSizePixels => pixelsPerMetre * CoreVoyageSession.NavalGridCellMeters;
        public bool IsAnimating { get; private set; }
        public VoyagePoint DisplayedPlayerPositionMeters => currentFrame.PlayerPositionMeters;
        public VoyagePoint DisplayedEnemyPositionMeters => currentFrame.EnemyPositionMeters;
        // Identifies the movement being presented, including its in-progress frames.
        public int DisplayedMovementRevision => lastMovementRevision;
        public NavalEnvironmentLight EnvironmentLight { get; private set; }

        internal static float HullReadoutOffset(CoreBattleState battle) =>
            battle != null && battle.NavalAwaitingHeadingSelection ? 200f : 125f;

        public void SetVerticalFraming(float height)
        {
            shipScreenHeight = height;
            if (hasFrame) ApplyFrame(currentFrame);
        }

        public void ExtendSeaToBattleSurface()
        {
            if (!built || root == null || seaViewport != null) return;
            // Keep the ocean continuous behind the lesson banner while the
            // grid and its input stay clipped below the reading area.
            seaViewport = CreateRect("Tactical sea viewport", root, new Vector2(1860f, 900f));
            seaViewport.gameObject.AddComponent<RectMask2D>();
            seaViewport.SetAsFirstSibling();
            backgroundRect.SetParent(seaViewport, false);
            if (hasFrame) ApplyFrame(currentFrame);
        }

        public Vector2 WorldToBoard(VoyagePoint position) => ToBoard(ProjectToRoot(position));

        public Vector2 WorldToRoot(VoyagePoint position) => ProjectToRoot(position);

        public VoyagePoint BoardToWorld(Vector2 local)
        {
            Vector2 relative = (local - ToBoard(EnemyScreenPosition)) / pixelsPerMetre;
            float radians = -CameraRotationDegrees * Mathf.Deg2Rad;
            return new VoyagePoint(relative.x * Mathf.Cos(radians) - relative.y * Mathf.Sin(radians) + currentFrame.EnemyPositionMeters.X,
                relative.x * Mathf.Sin(radians) + relative.y * Mathf.Cos(radians) + currentFrame.EnemyPositionMeters.Y);
        }

        public bool TryScreenToWorldCell(Vector2 screenPoint, Camera eventCamera, out VoyagePoint cell)
        {
            cell = default;
            if (!TryScreenToWorldPoint(screenPoint, eventCamera, out VoyagePoint world)) return false;
            float size = CoreVoyageSession.NavalGridCellMeters;
            // Match drag traversal's half-open cells, including exact boundaries
            // where midpoint-to-even rounding would select a different cell.
            cell = new VoyagePoint(Mathf.Floor(world.X / size + .5f) * size, Mathf.Floor(world.Y / size + .5f) * size);
            return true;
        }

        public bool TryScreenToWorldPoint(Vector2 screenPoint, Camera eventCamera, out VoyagePoint world)
        {
            world = default;
            if (!built || !RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screenPoint, eventCamera, out Vector2 local) ||
                !board.rect.Contains(local)) return false;
            world = BoardToWorld(local);
            return true;
        }

        public void Configure(CoreBattleState battle, RectTransform player, RectTransform enemy,
            RectTransform playerHullBar, RectTransform enemyHullBar, Font font, CoreVoyageSession session = null)
        {
            playerShip = player;
            enemyShip = enemy;
            playerBar = playerHullBar;
            enemyBar = enemyHullBar;
            root = player != null ? player.parent as RectTransform : transform.parent as RectTransform;
            board = (RectTransform)transform;
            board.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1860f);
            board.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 900f);
            hasFrame = false;
            BuildVisuals(font);
            ApplyShipArt(playerShip, NavalTacticalArtCatalog.ForPlayer(session?.SelectedShip.Id));
            ApplyShipArt(enemyShip, NavalTacticalArtCatalog.ForEnemy(battle, session?.ActiveEncounter, session?.SelectedRouteIndex ?? 0));
            environmentSession = session;
            hasEnvironment = false;
            RefreshEnvironment();
            SnapToBattle(battle);
        }

        /// <summary>Refreshes retained views without rebuilding or disturbing an in-progress tactical move.</summary>
        public void RefreshEnvironment()
        {
            if (!built) return;
            SeaTerrainType terrain = environmentSession?.BattleTerrain ?? SeaTerrainType.OpenWater;
            double hours = environmentSession?.ElapsedGameHours ?? 0d;
            if (hasEnvironment && displayedTerrain == terrain && displayedGameHours.Equals(hours)) return;
            if (!hasEnvironment || displayedTerrain != terrain)
                sea.texture = NavalTacticalArtCatalog.ForSea(terrain);
            EnvironmentLight = NavalEnvironmentLighting.ForVoyageHours(terrain, hours);
            sea.color = (sea.texture == null ? new Color(.018f, .19f, .23f, 1f) : Color.white) * EnvironmentLight.SeaTint;
            seaVeil.color = new Color(.025f, .105f, .13f, Mathf.Lerp(
                EnvironmentLight.Blend.From == NavigationTimeOfDay.Night ? .08f : .18f,
                EnvironmentLight.Blend.To == NavigationTimeOfDay.Night ? .08f : .18f,
                EnvironmentLight.Blend.Amount));
            seaLight.color = EnvironmentLight.ReflectionColor;
            ApplyShipLight(playerShip);
            ApplyShipLight(enemyShip);
            displayedTerrain = terrain;
            displayedGameHours = hours;
            hasEnvironment = true;
        }

        private void ApplyShipLight(RectTransform ship)
        {
            Image image = ship == null ? null : ship.GetComponent<Image>();
            if (image != null) image.color = EnvironmentLight.ShipTint;
        }

        private void LateUpdate() => RefreshEnvironment();
        private void OnEnable() => RefreshEnvironment();

        public void SnapToBattle(CoreBattleState battle)
        {
            RefreshEnvironment();
            animationGeneration++;
            IsAnimating = false;
            if (battle == null) return;
            displayedBattle = battle;
            lastMovementRevision = battle.TacticalRevision;
            pathFrames = battle.LastTacticalMovement?.Frames;
            ApplyFrame(battle.TacticalFrame);
        }

        public void ApplyFrame(CoreNavalTacticalFrame frame)
        {
            if (!built) return;
            currentFrame = frame;
            Vector2 delta = Point(frame.PlayerPositionMeters) - Point(frame.EnemyPositionMeters);
            if (delta.sqrMagnitude > .0001f)
            {
                float desired = 180f - Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                CameraRotationDegrees = hasFrame
                    ? CameraRotationDegrees + Mathf.DeltaAngle(CameraRotationDegrees, desired)
                    : desired;
            }
            hasFrame = true;
            // Preserve the requested 75-pixel cell at normal range, zooming out only when a retreat needs room.
            Vector2 leftEdge = ToRoot(new Vector2(board.rect.xMin + 100f, 0f));
            Vector2 rightEdge = ToRoot(new Vector2(board.rect.xMax - 100f, 0f));
            float availableWidth = Mathf.Max(450f, 2f * Mathf.Min(-leftEdge.x, rightEdge.x));
            pixelsPerMetre = Mathf.Min(InitialPixelsPerMetre, availableWidth / Mathf.Max(1f, delta.magnitude));
            // Compose the exact projected separation symmetrically, including boarding range.
            // The rotating world still pivots on the enemy; its screen anchor follows the camera framing.
            EnemyScreenPosition = new Vector2(delta.magnitude * pixelsPerMetre * .5f, shipScreenHeight);
            PlayerScreenPosition = ProjectToRoot(frame.PlayerPositionMeters);
            SetShipPose(playerShip, PlayerScreenPosition, frame.PlayerHeadingDegrees + CameraRotationDegrees - 90f);
            SetShipPose(enemyShip, EnemyScreenPosition, frame.EnemyHeadingDegrees + CameraRotationDegrees - 90f);
            // Health and defense remain readable when the ships meet at boarding
            // range. Only the readouts spread apart; ship/world distances stay exact.
            float hullOffset = HullReadoutOffset(displayedBattle);
            SetBarPose(playerBar, new Vector2(Mathf.Clamp(PlayerScreenPosition.x, -430f, -185f), PlayerScreenPosition.y - hullOffset));
            SetBarPose(enemyBar, new Vector2(Mathf.Clamp(EnemyScreenPosition.x, 185f, 430f), EnemyScreenPosition.y - hullOffset));

            Vector2 pivot = ToBoard(EnemyScreenPosition);
            worldLayer.anchoredPosition = pivot;
            worldLayer.localRotation = Quaternion.Euler(0f, 0f, CameraRotationDegrees);
            float coveringRadius = MaximumCornerDistance(board.rect, pivot) + CellSizePixels * 2f;
            if (seaViewport != null)
            {
                backgroundRect.position = worldLayer.position;
                backgroundRect.rotation = worldLayer.rotation;
                Vector2 seaPivot = seaViewport.InverseTransformPoint(worldLayer.position);
                coveringRadius = Mathf.Max(coveringRadius,
                    MaximumCornerDistance(seaViewport.rect, seaPivot) + CellSizePixels * 2f);
            }
            float side = Mathf.Max(2200f, coveringRadius * 2f);
            backgroundRect.sizeDelta = gridRect.sizeDelta = new Vector2(side, side);
            float uvSide = side / (pixelsPerMetre * SeaWorldSpanMeters);
            sea.uvRect = new Rect(.5f + frame.EnemyPositionMeters.X / SeaWorldSpanMeters - uvSide * .5f,
                .5f + frame.EnemyPositionMeters.Y / SeaWorldSpanMeters - uvSide * .5f, uvSide, uvSide);
            // Keeping the modulo offset preserves world grid intersections as the enemy and camera translate.
            // Half a cell puts whole-hundred-metre world positions in cell centres instead of intersections.
            grid.SetGrid(CellSizePixels, new Vector2(
                CellSizePixels * .5f - frame.EnemyPositionMeters.X % CoreVoyageSession.NavalGridCellMeters * pixelsPerMetre,
                CellSizePixels * .5f - frame.EnemyPositionMeters.Y % CoreVoyageSession.NavalGridCellMeters * pixelsPerMetre));
            UpdateCompass();
            UpdateTrack();
        }

        public IEnumerator AnimateMovement(CoreNavalTacticalMovement movement)
        {
            if (movement == null || movement.Frames == null || movement.Frames.Count == 0 ||
                movement.Revision == lastMovementRevision) yield break;
            lastMovementRevision = movement.Revision;
            int generation = ++animationGeneration;
            pathFrames = movement.Frames;
            IsAnimating = true;
            float distance = 0f;
            for (int index = 1; index < movement.Frames.Count; index++)
            {
                distance += Vector2.Distance(Point(movement.Frames[index - 1].PlayerPositionMeters), Point(movement.Frames[index].PlayerPositionMeters));
                distance += Vector2.Distance(Point(movement.Frames[index - 1].EnemyPositionMeters), Point(movement.Frames[index].EnemyPositionMeters));
            }
            float duration = Mathf.Clamp(.65f + distance * .0007f, .65f, 1.2f);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (generation != animationGeneration) yield break;
                float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                float cursor = progress * (movement.Frames.Count - 1);
                int first = Mathf.FloorToInt(cursor);
                int second = Mathf.Min(first + 1, movement.Frames.Count - 1);
                ApplyFrame(Interpolate(movement.Frames[first], movement.Frames[second], cursor - first));
                yield return null;
            }
            if (generation != animationGeneration) yield break;
            ApplyFrame(movement.Frames[movement.Frames.Count - 1]);
            IsAnimating = false;
        }

        private void BuildVisuals(Font font)
        {
            if (built) return;
            if (GetComponent<RectMask2D>() == null) gameObject.AddComponent<RectMask2D>();
            worldLayer = CreateRect("Tactical world", board, Vector2.zero);
            backgroundRect = CreateRect("Shared tactical sea", worldLayer, new Vector2(2600f, 2600f));
            sea = backgroundRect.gameObject.AddComponent<RawImage>();
            sea.raycastTarget = false;
            RectTransform veilRect = CreateRect("Tactical sea veil", backgroundRect, Vector2.zero);
            veilRect.anchorMin = Vector2.zero;
            veilRect.anchorMax = Vector2.one;
            seaVeil = veilRect.gameObject.AddComponent<Image>();
            seaVeil.raycastTarget = false;
            RectTransform lightRect = CreateRect("Tactical sea light", backgroundRect, Vector2.zero);
            lightRect.anchorMin = Vector2.zero;
            lightRect.anchorMax = Vector2.one;
            seaLight = lightRect.gameObject.AddComponent<NavalSeaLightGraphic>();
            seaLight.raycastTarget = false;
            gridRect = CreateRect("100 metre grid", worldLayer, new Vector2(2600f, 2600f));
            grid = gridRect.gameObject.AddComponent<NavalTacticalGridGraphic>();
            grid.raycastTarget = false;
            grid.color = new Color(.48f, .74f, .76f, .13f);

            RectTransform trackRect = CreateRect("Tactical course", board, board.rect.size);
            track = trackRect.gameObject.AddComponent<NavalTacticalTrackGraphic>();
            track.raycastTarget = false;
            RectTransform compass = CreateRect("Tactical compass", board, new Vector2(164f, 164f));
            compass.anchorMin = compass.anchorMax = Vector2.one;
            compass.anchoredPosition = new Vector2(-195f, -205f);
            compassGraphic = compass.gameObject.AddComponent<NavalTacticalCompassGraphic>();
            compassGraphic.raycastTarget = false;
            Text north = CreateText("Compass North", compass, "N", font, 16, new Vector2(20f, 22f));
            GameTypography.Apply(north, 16, FontStyle.Bold);
            north.color = new Color(.29f, .22f, .13f, 1f);
            compassNorth = north.rectTransform;
            built = true;
        }

        private void ApplyShipArt(RectTransform ship, Sprite sprite)
        {
            if (ship == null) return;
            Image image = ship.GetComponent<Image>();
            if (image == null || sprite == null) return;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private void UpdateCompass()
        {
            compassGraphic.SetWorldRotation(CameraRotationDegrees);
            // World north uses the same projection as the tactical grid. The
            // rose follows the camera, while its single north initial stays upright.
            float diameter = Mathf.Min(compassGraphic.rectTransform.rect.width, compassGraphic.rectTransform.rect.height);
            compassNorth.anchoredPosition = compassGraphic.NorthDirection * (diameter * .342f);
            compassNorth.localRotation = Quaternion.identity;
        }

        private void UpdateTrack()
        {
            playerTrack.Clear();
            enemyTrack.Clear();
            if (pathFrames != null)
                for (int index = 0; index < pathFrames.Count; index++)
                {
                    playerTrack.Add(ToBoard(ProjectToRoot(pathFrames[index].PlayerPositionMeters)));
                    enemyTrack.Add(ToBoard(ProjectToRoot(pathFrames[index].EnemyPositionMeters)));
                }
            track.SetTrack(playerTrack, enemyTrack, ToBoard(PlayerScreenPosition), ToBoard(EnemyScreenPosition),
                currentFrame.PlayerHeadingDegrees + CameraRotationDegrees,
                currentFrame.EnemyHeadingDegrees + CameraRotationDegrees);
        }

        private Vector2 ProjectToRoot(VoyagePoint position)
        {
            Vector2 relative = Point(position) - Point(currentFrame.EnemyPositionMeters);
            float radians = CameraRotationDegrees * Mathf.Deg2Rad;
            return EnemyScreenPosition + new Vector2(relative.x * Mathf.Cos(radians) - relative.y * Mathf.Sin(radians),
                relative.x * Mathf.Sin(radians) + relative.y * Mathf.Cos(radians)) * pixelsPerMetre;
        }

        private Vector2 ToBoard(Vector2 rootPoint) => root == null ? rootPoint :
            (Vector2)board.InverseTransformPoint(root.TransformPoint(rootPoint));
        private Vector2 ToRoot(Vector2 boardPoint) => root == null ? boardPoint :
            (Vector2)root.InverseTransformPoint(board.TransformPoint(boardPoint));
        private static Vector2 Point(VoyagePoint point) => new Vector2(point.X, point.Y);

        private void SetShipPose(RectTransform ship, Vector2 position, float heading)
        {
            if (ship == null) return;
            ship.position = root != null ? root.TransformPoint(position) : (Vector3)position;
            ship.localRotation = Quaternion.Euler(0f, 0f, heading);
        }

        private void SetBarPose(RectTransform bar, Vector2 position)
        {
            if (bar == null) return;
            bar.position = root != null ? root.TransformPoint(position) : (Vector3)position;
            bar.rotation = root != null ? root.rotation : Quaternion.identity;
        }

        private static float MaximumCornerDistance(Rect rect, Vector2 pivot)
        {
            float x = Mathf.Max(Mathf.Abs(rect.xMin - pivot.x), Mathf.Abs(rect.xMax - pivot.x));
            float y = Mathf.Max(Mathf.Abs(rect.yMin - pivot.y), Mathf.Abs(rect.yMax - pivot.y));
            return Mathf.Sqrt(x * x + y * y);
        }

        private static CoreNavalTacticalFrame Interpolate(CoreNavalTacticalFrame from, CoreNavalTacticalFrame to, float t)
        {
            Vector2 player = Vector2.Lerp(Point(from.PlayerPositionMeters), Point(to.PlayerPositionMeters), t);
            Vector2 enemy = Vector2.Lerp(Point(from.EnemyPositionMeters), Point(to.EnemyPositionMeters), t);
            return new CoreNavalTacticalFrame(new VoyagePoint(player.x, player.y), new VoyagePoint(enemy.x, enemy.y),
                Mathf.LerpAngle(from.PlayerHeadingDegrees, to.PlayerHeadingDegrees, t),
                Mathf.LerpAngle(from.EnemyHeadingDegrees, to.EnemyHeadingDegrees, t));
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 size)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            return rect;
        }

        private static Text CreateText(string name, RectTransform parent, string value, Font font, int fontSize, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent, size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void OnDisable()
        {
            animationGeneration++;
            IsAnimating = false;
        }
    }
}
