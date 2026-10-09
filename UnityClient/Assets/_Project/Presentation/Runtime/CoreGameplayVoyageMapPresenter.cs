using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private sealed class TerrainFeatureVisual
        {
            public int FeatureId;
            public SeaTerrainType Terrain;
            public int MinColumn;
            public int MaxColumn;
            public int MinRow;
            public int MaxRow;
            public bool HasExploredCell;
            public byte Variant;
        }

        private const float ChartGridWidth = 1160f;
        private const float ChartGridHeight = 630f;
        private const int OceanPatchCellSpan = 8;

        private VoyageMapArtCatalog voyageMapArt;

        public VoyageMapArtCatalog VoyageMapArt
        {
            get
            {
                if (voyageMapArt == null)
                {
                    voyageMapArt = new VoyageMapArtCatalog();
                }
                return voyageMapArt;
            }
        }

        /// <summary>
        /// Builds a deterministic square-cell chart beneath routes and markers.
        /// Core owns cell contents; this method only chooses visual variants.
        /// </summary>
        private void BuildVoyageTerrainLayers(RectTransform mapRoot)
        {
            if (session == null || session.Map == null)
            {
                return;
            }

            CoreVoyageMap map = session.Map;
            float cellWidth = ChartGridWidth / CoreVoyageMap.GridColumns;
            float cellHeight = ChartGridHeight / CoreVoyageMap.GridRows;
            Vector2 tileSize = new Vector2(cellWidth + 0.7f, cellHeight + 0.7f);

            RectTransform waterLayer = CreateRect("Ocean Tile Layer", mapRoot, new Vector2(ChartGridWidth, ChartGridHeight), Vector2.zero);
            waterLayer.gameObject.AddComponent<RectMask2D>();
            RectTransform terrainFillLayer = CreateRect("Terrain Fill Layer", mapRoot, new Vector2(ChartGridWidth, ChartGridHeight), Vector2.zero);
            RectTransform terrainArtLayer = CreateRect("Terrain Art Layer", mapRoot, new Vector2(ChartGridWidth, ChartGridHeight), Vector2.zero);
            terrainArtLayer.gameObject.AddComponent<RectMask2D>();
            RectTransform detailLayer = CreateRect("Chart Detail Layer", mapRoot, new Vector2(ChartGridWidth, ChartGridHeight), Vector2.zero);
            RectTransform edgeLayer = CreateRect("Terrain Edge Layer", mapRoot, new Vector2(ChartGridWidth, ChartGridHeight), Vector2.zero);
            RectTransform gridLayer = CreateRect("Chart Grid Layer", mapRoot, new Vector2(ChartGridWidth, ChartGridHeight), Vector2.zero);
            RectTransform visibilityLayer = CreateRect("Visibility Mask Layer", mapRoot, new Vector2(ChartGridWidth, ChartGridHeight), Vector2.zero);
            VoyageMapTileLayerGraphic fogTiles = CreateChartQuadLayer("Chart Fog Tiles", visibilityLayer);

            Dictionary<long, TerrainFeatureVisual> naturalFeatures = new Dictionary<long, TerrainFeatureVisual>();
            BuildOceanChartBase(waterLayer, map);
            BuildOceanPatches(waterLayer, map, cellWidth, cellHeight);

            for (int row = 0; row < CoreVoyageMap.GridRows; row += 1)
            {
                for (int column = 0; column < CoreVoyageMap.GridColumns; column += 1)
                {
                    CoreVoyageMapCell cell = map.GetCell(column, row);
                    bool explored = cell.Explored;
                    SeaTerrainType terrain = cell.Terrain;
                    Vector2 cellPosition = MapPosition(cell.Center);
                    uint visualHash = VoyageMapArtCatalog.VisualHash(
                        map.Hash,
                        column,
                        row,
                        (uint)cell.TerrainVariant * 0x27D4EB2Du);

                    if (terrain != SeaTerrainType.OpenWater && cell.TerrainFeatureId >= 0 && cell.AnchorNodeId < 0)
                    {
                        CollectNaturalFeature(naturalFeatures, cell);
                    }

                    if (explored && terrain != SeaTerrainType.OpenWater &&
                        terrain != SeaTerrainType.Island && terrain != SeaTerrainType.Beach &&
                        terrain != SeaTerrainType.Shoal && terrain != SeaTerrainType.Strait)
                    {
                        BuildTerrainTile(
                            terrainFillLayer,
                            detailLayer,
                            edgeLayer,
                            cellPosition,
                            tileSize,
                            cellWidth,
                            cellHeight,
                            column,
                            row,
                            terrain,
                            cell.TerrainFeatureId,
                            cell.AnchorNodeId,
                            visualHash);
                    }

                    BuildVisibilityTile(
                        fogTiles,
                        cellPosition,
                        tileSize,
                        explored,
                        visualHash);

                    if (explored && terrain == SeaTerrainType.OpenWater && visualHash % 13u == 0u)
                    {
                        BuildWaterInkDetail(detailLayer, cellPosition, cellWidth, cellHeight, column, row, visualHash);
                    }
                }
            }
            fogTiles.Complete();

            // Every solid coast is drawn from the complete logical grid. This
            // closes the outer ends of strait banks and preserves concave bays;
            // no impassable terrain is stretched into a rectangular footprint.
            RectTransform coastRect = CreateRect("Continuous Coast Surface", terrainArtLayer,
                new Vector2(ChartGridWidth, ChartGridHeight), Vector2.zero);
            VoyageChartSurfaceGraphic coast = coastRect.gameObject.AddComponent<VoyageChartSurfaceGraphic>();
            coast.Initialize(map, VoyageMapArt, session.SelectedRouteIndex);
            BuildNaturalFeatureArtwork(terrainArtLayer, naturalFeatures, cellWidth, cellHeight);
            BuildEventFootprintArtwork(terrainArtLayer, cellWidth, cellHeight);
            BuildChartGrid(gridLayer, cellWidth, cellHeight);
            BuildVisibilityFrontier(visibilityLayer, cellWidth, cellHeight);
        }

        private void BuildOceanChartBase(RectTransform waterLayer, CoreVoyageMap map)
        {
            Texture2D texture = VoyageMapArt.ChartBase;
            RawImage chartBase = CreateRawImage(
                "Ocean Chart Base",
                waterLayer,
                new Vector2(ChartGridWidth + 1f, ChartGridHeight + 1f),
                Vector2.zero,
                texture,
                new Rect(0f, 0f, 1f, 1f));
            chartBase.raycastTarget = false;
            chartBase.color = texture == null
                ? FallbackWaterColor(session.SelectedRouteIndex, map.Hash)
                : RouteWaterTint(session.SelectedRouteIndex);
        }

        private void BuildOceanPatches(RectTransform waterLayer, CoreVoyageMap map, float cellWidth, float cellHeight)
        {
            List<Texture2D> waterTextures = FindDistinctWaterTextures();
            int patchIndex = 0;
            int seedOffset = waterTextures.Count == 0 ? 0 : (int)(map.Hash % (uint)waterTextures.Count);

            for (int startRow = 0; startRow < CoreVoyageMap.GridRows; startRow += OceanPatchCellSpan)
            {
                int coveredRows = Mathf.Min(OceanPatchCellSpan, CoreVoyageMap.GridRows - startRow);
                for (int startColumn = 0; startColumn < CoreVoyageMap.GridColumns; startColumn += OceanPatchCellSpan)
                {
                    int coveredColumns = Mathf.Min(OceanPatchCellSpan, CoreVoyageMap.GridColumns - startColumn);
                    int patchColumn = startColumn / OceanPatchCellSpan;
                    int patchRow = startRow / OceanPatchCellSpan;
                    uint visualHash = VoyageMapArtCatalog.VisualHash(map.Hash, patchColumn, patchRow, 0xC2B2AE35u);
                    Texture2D texture = null;
                    if (waterTextures.Count > 0)
                    {
                        // Seed the first variants as a complete cycle so every available
                        // water painting is guaranteed to appear, then vary the remainder.
                        int textureIndex = patchIndex < waterTextures.Count
                            ? (patchIndex + seedOffset) % waterTextures.Count
                            : (int)(visualHash % (uint)waterTextures.Count);
                        texture = waterTextures[textureIndex];
                    }

                    Vector2 position = new Vector2(
                        -ChartGridWidth * 0.5f + (startColumn + coveredColumns * 0.5f) * cellWidth,
                        -ChartGridHeight * 0.5f + (startRow + coveredRows * 0.5f) * cellHeight);
                    Vector2 size = new Vector2(
                        coveredColumns * cellWidth * 1.32f,
                        coveredRows * cellHeight * 1.32f);
                    bool flipX = (visualHash & 1u) != 0u;
                    bool flipY = (visualHash & 2u) != 0u;
                    Rect uv = new Rect(flipX ? 1f : 0f, flipY ? 1f : 0f, flipX ? -1f : 1f, flipY ? -1f : 1f);
                    RawImage patch = CreateBlendedChartImage(
                        "Ocean Patch " + patchColumn + " " + patchRow,
                        waterLayer,
                        size,
                        position,
                        texture,
                        uv, visualHash, .30f);
                    patch.raycastTarget = false;
                    if (texture == null)
                    {
                        patch.color = FallbackWaterColor(session.SelectedRouteIndex, visualHash);
                    }
                    else
                    {
                        Color tint = RouteWaterTint(session.SelectedRouteIndex);
                        tint.a = VoyageMapArt.ChartBase == null ? 1f : 0.17f;
                        patch.color = tint;
                    }
                    patchIndex += 1;
                }
            }
        }

        private List<Texture2D> FindDistinctWaterTextures()
        {
            List<Texture2D> textures = new List<Texture2D>(3);
            for (int index = 0; index < VoyageMapArt.WaterVariantCount; index += 1)
            {
                Texture2D texture = VoyageMapArt.WaterVariant(index);
                if (texture != null && !textures.Contains(texture))
                {
                    textures.Add(texture);
                }
            }
            return textures;
        }

        private void BuildTerrainTile(
            RectTransform fillLayer,
            RectTransform detailLayer,
            RectTransform edgeLayer,
            Vector2 position,
            Vector2 size,
            float cellWidth,
            float cellHeight,
            int column,
            int row,
            SeaTerrainType terrain,
            int terrainFeatureId,
            int anchorNodeId,
            uint visualHash)
        {
            bool hasFeatureArtwork = HasFeatureArtwork(terrain, terrainFeatureId, anchorNodeId);
            Color fillColor = TerrainFillColor(terrain, visualHash);
            if (hasFeatureArtwork)
            {
                // The colour layer is only a quiet underpaint when a proper
                // illustrated terrain cutout exists. Keeping it faint avoids the
                // old block-map silhouette around irregular coastlines.
                fillColor.a *= 0.34f;
            }
            Image fill = CreateImage(
                "Sea Terrain " + column + " " + row,
                fillLayer,
                size,
                position,
                fillColor);
            fill.raycastTarget = false;

            if (!hasFeatureArtwork)
            {
                BuildFallbackTerrainDetail(detailLayer, position, cellWidth, cellHeight, column, row, terrain, visualHash);
                BuildTerrainCellEdges(edgeLayer, position, cellWidth, cellHeight, column, row, terrain, terrainFeatureId);
            }
        }

        private void CollectNaturalFeature(IDictionary<long, TerrainFeatureVisual> features, CoreVoyageMapCell cell)
        {
            long key = ((long)cell.TerrainFeatureId << 32) | (uint)cell.Terrain;
            if (!features.TryGetValue(key, out TerrainFeatureVisual feature))
            {
                feature = new TerrainFeatureVisual
                {
                    FeatureId = cell.TerrainFeatureId,
                    Terrain = cell.Terrain,
                    MinColumn = cell.Column,
                    MaxColumn = cell.Column,
                    MinRow = cell.Row,
                    MaxRow = cell.Row,
                    Variant = cell.TerrainVariant,
                };
                features.Add(key, feature);
            }

            feature.MinColumn = Mathf.Min(feature.MinColumn, cell.Column);
            feature.MaxColumn = Mathf.Max(feature.MaxColumn, cell.Column);
            feature.MinRow = Mathf.Min(feature.MinRow, cell.Row);
            feature.MaxRow = Mathf.Max(feature.MaxRow, cell.Row);
            feature.HasExploredCell |= cell.Explored;
        }

        private void BuildNaturalFeatureArtwork(
            RectTransform artLayer,
            IDictionary<long, TerrainFeatureVisual> features,
            float cellWidth,
            float cellHeight)
        {
            foreach (KeyValuePair<long, TerrainFeatureVisual> entry in features)
            {
                TerrainFeatureVisual feature = entry.Value;
                if (!feature.HasExploredCell || feature.Terrain == SeaTerrainType.Strait ||
                    feature.Terrain == SeaTerrainType.Island || feature.Terrain == SeaTerrainType.Beach ||
                    feature.Terrain == SeaTerrainType.Shoal)
                {
                    continue;
                }

                Texture2D texture = VoyageMapArt.TerrainFor(feature.Terrain);
                if (texture == null)
                {
                    continue;
                }

                Vector2 center = FeatureCenter(feature.MinColumn, feature.MaxColumn, feature.MinRow, feature.MaxRow);
                Vector2 size = FeatureSize(feature.MinColumn, feature.MaxColumn, feature.MinRow, feature.MaxRow, cellWidth, cellHeight);
                uint visualHash = VoyageMapArtCatalog.VisualHash(
                    session.Map.Hash,
                    feature.MinColumn,
                    feature.MinRow,
                    (uint)feature.Variant * 0x27D4EB2Du);
                int quarterTurns = (int)((visualHash >> 3) & 3u);
                if (feature.Terrain == SeaTerrainType.Shoal)
                {
                    size *= 1.08f;
                }
                RawImage artwork = CreateFeatureArtwork(
                    "Terrain Feature " + feature.FeatureId + " " + feature.Terrain,
                    artLayer,
                    texture,
                    center,
                    size,
                    quarterTurns);
                artwork.color = NaturalFeatureTint(feature.Terrain);
            }
        }

        private void BuildEventFootprintArtwork(RectTransform artLayer, float cellWidth, float cellHeight)
        {
            IReadOnlyList<CoreEventFootprint> footprints = session.Map.EventFootprints;
            for (int index = 0; index < footprints.Count; index += 1)
            {
                CoreEventFootprint footprint = footprints[index];
                if (footprint.TerrainConstraint == EventTerrainConstraint.Beach ||
                    footprint.TerrainConstraint == EventTerrainConstraint.Strait)
                {
                    // The coast field uses the footprint's actual cells, not
                    // the cropped double-bank illustration or a central island
                    // overlay that could cover a passable beach event anchor.
                    continue;
                }
                if (!FootprintHasExploredCell(footprint))
                {
                    continue;
                }

                SeaTerrainType visualTerrain = TerrainForConstraint(footprint.TerrainConstraint);
                Texture2D texture = VoyageMapArt.TerrainFor(visualTerrain);
                if (texture == null)
                {
                    continue;
                }

                Vector2 center = FeatureCenter(footprint.MinColumn, footprint.MaxColumn, footprint.MinRow, footprint.MaxRow);
                Vector2 finalSize = FeatureSize(
                    footprint.MinColumn,
                    footprint.MaxColumn,
                    footprint.MinRow,
                    footprint.MaxRow,
                    cellWidth,
                    cellHeight);
                int quarterTurns = ArtworkQuarterTurns(footprint);
                CreateFeatureArtwork(
                    ConstraintFeatureName(footprint.TerrainConstraint) + " Feature " + footprint.NodeId,
                    artLayer,
                    texture,
                    center,
                    finalSize,
                    quarterTurns);

            }
        }

        private RawImage CreateFeatureArtwork(
            string name,
            Transform parent,
            Texture2D texture,
            Vector2 center,
            Vector2 finalSize,
            int quarterTurns)
        {
            bool swapDimensions = (quarterTurns & 1) != 0;
            Vector2 unrotatedSize = swapDimensions ? new Vector2(finalSize.y, finalSize.x) : finalSize;
            uint seed = VoyageMapArtCatalog.VisualHash(session.Map.Hash,
                Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y), 0x7F4A7C15u);
            RawImage artwork = CreateBlendedChartImage(name, parent, unrotatedSize, center,
                texture, new Rect(0f, 0f, 1f, 1f), seed, .20f);
            artwork.raycastTarget = false;
            artwork.rectTransform.localRotation = Quaternion.Euler(0f, 0f, quarterTurns * 90f);
            return artwork;
        }

        private VoyageMapBlendGraphic CreateBlendedChartImage(string name, Transform parent,
            Vector2 size, Vector2 position, Texture2D texture, Rect uv, uint seed, float feather)
        {
            RectTransform rect = CreateRect(name, parent, size, position);
            VoyageMapBlendGraphic image = rect.gameObject.AddComponent<VoyageMapBlendGraphic>();
            image.texture = texture;
            image.uvRect = uv;
            image.Configure(feather, seed);
            return image;
        }

        private static Color NaturalFeatureTint(SeaTerrainType terrain)
        {
            switch (terrain)
            {
                case SeaTerrainType.Shoal: return new Color(1f, 1f, 1f, 0.12f);
                case SeaTerrainType.Reef: return new Color(1f, 1f, 1f, 0.78f);
                case SeaTerrainType.KelpField: return new Color(1f, 1f, 1f, 0.68f);
                case SeaTerrainType.FogBank: return new Color(1f, 1f, 1f, 0.74f);
                case SeaTerrainType.StormFront: return new Color(1f, 1f, 1f, 0.86f);
                default: return Color.white;
            }
        }

        private bool FootprintHasExploredCell(CoreEventFootprint footprint)
        {
            IReadOnlyList<int> cells = footprint.CellIndices;
            for (int index = 0; index < cells.Count; index += 1)
            {
                int cellIndex = cells[index];
                if (cellIndex >= 0 && cellIndex < session.Map.ExploredCells.Length && session.Map.ExploredCells[cellIndex])
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasFeatureArtwork(SeaTerrainType terrain, int terrainFeatureId, int anchorNodeId)
        {
            if (terrainFeatureId < 0)
            {
                return false;
            }

            if (terrain == SeaTerrainType.Strait)
            {
                return anchorNodeId >= 0 && VoyageMapArt.TerrainFor(SeaTerrainType.Strait) != null;
            }
            return VoyageMapArt.TerrainFor(terrain) != null;
        }

        private Vector2 FeatureCenter(int minColumn, int maxColumn, int minRow, int maxRow)
        {
            Vector2 first = MapPosition(session.Map.CellCenter(minColumn, minRow));
            Vector2 last = MapPosition(session.Map.CellCenter(maxColumn, maxRow));
            return (first + last) * 0.5f;
        }

        private static Vector2 FeatureSize(
            int minColumn,
            int maxColumn,
            int minRow,
            int maxRow,
            float cellWidth,
            float cellHeight)
        {
            return new Vector2(
                (maxColumn - minColumn + 1) * cellWidth + 3f,
                (maxRow - minRow + 1) * cellHeight + 3f);
        }

        private static SeaTerrainType TerrainForConstraint(EventTerrainConstraint constraint)
        {
            switch (constraint)
            {
                case EventTerrainConstraint.Beach: return SeaTerrainType.Beach;
                case EventTerrainConstraint.Strait: return SeaTerrainType.Strait;
                case EventTerrainConstraint.Reef: return SeaTerrainType.Reef;
                case EventTerrainConstraint.Fog: return SeaTerrainType.FogBank;
                case EventTerrainConstraint.Storm: return SeaTerrainType.StormFront;
                default: return SeaTerrainType.OpenWater;
            }
        }

        private static int ArtworkQuarterTurns(CoreEventFootprint footprint)
        {
            if (footprint.TerrainConstraint == EventTerrainConstraint.Strait)
            {
                // Strait art has a north-south channel; orientation zero stamps
                // the event footprint east-west and therefore needs one turn.
                return (footprint.Orientation & 1) == 0 ? 1 : 0;
            }
            return footprint.Orientation & 3;
        }

        private static string ConstraintFeatureName(EventTerrainConstraint constraint)
        {
            switch (constraint)
            {
                case EventTerrainConstraint.Beach: return "Beach";
                case EventTerrainConstraint.Strait: return "Strait";
                case EventTerrainConstraint.Reef: return "Reef";
                case EventTerrainConstraint.Fog: return "Fog Bank";
                case EventTerrainConstraint.Storm: return "Storm Front";
                default: return "Open Sea";
            }
        }

        private void BuildVisibilityTile(
            VoyageMapTileLayerGraphic fogTiles,
            Vector2 position,
            Vector2 size,
            bool explored,
            uint visualHash)
        {
            float hiddenVariation = ((visualHash >> 24) & 0x0Fu) / 600f;
            Color color = explored
                ? new Color(Fog.r, Fog.g, Fog.b, 0.025f)
                : new Color(Fog.r, Fog.g, Fog.b, 0.965f + hiddenVariation);
            fogTiles.AddTile(position, size, color);
        }

        private void BuildChartGrid(RectTransform parent, float cellWidth, float cellHeight)
        {
            VoyageMapTileLayerGraphic lines = CreateChartQuadLayer("Chart Grid Lines", parent);
            Color minor = new Color(0.58f, 0.78f, 0.78f, 0.018f);
            Color major = new Color(0.72f, 0.87f, 0.82f, 0.044f);
            for (int column = 0; column <= CoreVoyageMap.GridColumns; column += 1)
            {
                bool isMajor = column % 8 == 0;
                float x = -ChartGridWidth * 0.5f + column * cellWidth;
                lines.AddTile(
                    new Vector2(x, 0f),
                    new Vector2(isMajor ? 1.15f : 0.55f, ChartGridHeight),
                    isMajor ? major : minor);
            }

            for (int row = 0; row <= CoreVoyageMap.GridRows; row += 1)
            {
                bool isMajor = row % 4 == 0;
                float y = -ChartGridHeight * 0.5f + row * cellHeight;
                lines.AddTile(
                    new Vector2(0f, y),
                    new Vector2(ChartGridWidth, isMajor ? 1.15f : 0.55f),
                    isMajor ? major : minor);
            }
            lines.Complete();
        }

        private void BuildTerrainCellEdges(
            RectTransform parent,
            Vector2 position,
            float cellWidth,
            float cellHeight,
            int column,
            int row,
            SeaTerrainType terrain,
            int terrainFeatureId)
        {
            Color edgeColor = TerrainEdgeColor(terrain);
            float thickness = terrain == SeaTerrainType.Island || terrain == SeaTerrainType.Beach ? 1.9f : 1.25f;
            if (!TerrainConnectsTo(column, row + 1, terrain, terrainFeatureId))
            {
                CreateChartStroke("Terrain Edge " + column + " " + row + " North", parent, new Vector2(cellWidth, thickness), position + new Vector2(0f, cellHeight * 0.5f), edgeColor);
            }
            if (!TerrainConnectsTo(column + 1, row, terrain, terrainFeatureId))
            {
                CreateChartStroke("Terrain Edge " + column + " " + row + " East", parent, new Vector2(thickness, cellHeight), position + new Vector2(cellWidth * 0.5f, 0f), edgeColor);
            }
            if (!TerrainConnectsTo(column, row - 1, terrain, terrainFeatureId))
            {
                CreateChartStroke("Terrain Edge " + column + " " + row + " South", parent, new Vector2(cellWidth, thickness), position - new Vector2(0f, cellHeight * 0.5f), edgeColor);
            }
            if (!TerrainConnectsTo(column - 1, row, terrain, terrainFeatureId))
            {
                CreateChartStroke("Terrain Edge " + column + " " + row + " West", parent, new Vector2(thickness, cellHeight), position - new Vector2(cellWidth * 0.5f, 0f), edgeColor);
            }
        }

        private void BuildVisibilityFrontier(RectTransform parent, float cellWidth, float cellHeight)
        {
            // Keep the frontier after every fog tile, preserving their original
            // alpha-compositing order while avoiding thousands of UI objects.
            VoyageMapTileLayerGraphic lines = CreateChartQuadLayer("Chart Fog Frontier", parent);
            Color frontier = new Color(0.3f, 0.54f, 0.57f, 0.22f);
            for (int row = 0; row < CoreVoyageMap.GridRows; row += 1)
            {
                for (int column = 0; column < CoreVoyageMap.GridColumns; column += 1)
                {
                    if (!CellIsExplored(column, row)) continue;
                    Vector2 position = MapPosition(session.Map.CellCenter(column, row));
                    if (row + 1 < CoreVoyageMap.GridRows && !CellIsExplored(column, row + 1))
                    {
                        lines.AddTile(position + new Vector2(0f, cellHeight * 0.5f), new Vector2(cellWidth, 2.8f), frontier);
                    }
                    if (column + 1 < CoreVoyageMap.GridColumns && !CellIsExplored(column + 1, row))
                    {
                        lines.AddTile(position + new Vector2(cellWidth * 0.5f, 0f), new Vector2(2.8f, cellHeight), frontier);
                    }
                    if (row > 0 && !CellIsExplored(column, row - 1))
                    {
                        lines.AddTile(position - new Vector2(0f, cellHeight * 0.5f), new Vector2(cellWidth, 2.8f), frontier);
                    }
                    if (column > 0 && !CellIsExplored(column - 1, row))
                    {
                        lines.AddTile(position - new Vector2(cellWidth * 0.5f, 0f), new Vector2(2.8f, cellHeight), frontier);
                    }
                }
            }
            lines.Complete();
        }

        private static VoyageMapTileLayerGraphic CreateChartQuadLayer(string name, RectTransform parent)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(ChartGridWidth, ChartGridHeight), Vector2.zero);
            VoyageMapTileLayerGraphic graphic = rect.gameObject.AddComponent<VoyageMapTileLayerGraphic>();
            graphic.Initialize(null);
            return graphic;
        }

        private void BuildWaterInkDetail(
            RectTransform parent,
            Vector2 position,
            float cellWidth,
            float cellHeight,
            int column,
            int row,
            uint visualHash)
        {
            Color ink = new Color(0.62f, 0.86f, 0.88f, 0.2f);
            float xOffset = ((visualHash >> 9) % 7u - 3f) * cellWidth * 0.045f;
            float yOffset = ((visualHash >> 13) % 5u - 2f) * cellHeight * 0.05f;
            Vector2 center = position + new Vector2(xOffset, yOffset);
            Image first = CreateChartStroke(
                "Water Detail " + column + " " + row,
                parent,
                new Vector2(cellWidth * 0.42f, 1.1f),
                center,
                ink);
            first.rectTransform.localRotation = Quaternion.Euler(0f, 0f, visualHash % 2u == 0u ? 4f : -4f);
            CreateChartStroke(
                "Water Detail Echo " + column + " " + row,
                parent,
                new Vector2(cellWidth * 0.24f, 0.8f),
                center + new Vector2(cellWidth * 0.08f, -cellHeight * 0.18f),
                new Color(ink.r, ink.g, ink.b, ink.a * 0.72f));
        }

        private void BuildFallbackTerrainDetail(
            RectTransform parent,
            Vector2 position,
            float cellWidth,
            float cellHeight,
            int column,
            int row,
            SeaTerrainType terrain,
            uint visualHash)
        {
            string prefix = "Terrain Detail " + column + " " + row;
            Color light = Color.Lerp(TerrainEdgeColor(terrain), Color.white, 0.24f);
            Color dark = Color.Lerp(TerrainFillColor(terrain, visualHash), Color.black, 0.34f);
            switch (terrain)
            {
                case SeaTerrainType.Island:
                    CreateChartStroke(prefix + " Ridge", parent, new Vector2(cellWidth * 0.58f, 2.2f), position + new Vector2(0f, cellHeight * 0.08f), light);
                    CreateChartStroke(prefix + " Shadow", parent, new Vector2(cellWidth * 0.36f, 2f), position - new Vector2(cellWidth * 0.08f, cellHeight * 0.2f), dark);
                    break;
                case SeaTerrainType.Beach:
                case SeaTerrainType.Shoal:
                    CreateChartStroke(prefix + " Sandbar", parent, new Vector2(cellWidth * 0.64f, 1.5f), position, light);
                    CreateChartStroke(prefix + " Ripple", parent, new Vector2(cellWidth * 0.42f, 1f), position - new Vector2(0f, cellHeight * 0.2f), light);
                    break;
                case SeaTerrainType.Reef:
                    Image reef = CreateChartStroke(prefix + " Coral", parent, new Vector2(cellWidth * 0.55f, 2.4f), position, light);
                    reef.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 28f);
                    Image reefCross = CreateChartStroke(prefix + " Coral Cross", parent, new Vector2(cellWidth * 0.42f, 1.7f), position, dark);
                    reefCross.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -34f);
                    break;
                case SeaTerrainType.KelpField:
                    for (int blade = -1; blade <= 1; blade += 1)
                    {
                        Image kelp = CreateChartStroke(prefix + " Kelp " + blade, parent, new Vector2(1.7f, cellHeight * (0.46f + (blade + 1) * 0.08f)), position + new Vector2(blade * cellWidth * 0.2f, 0f), light);
                        kelp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, blade * 9f);
                    }
                    break;
                case SeaTerrainType.FogBank:
                    CreateChartStroke(prefix + " Mist A", parent, new Vector2(cellWidth * 0.75f, 2.4f), position + new Vector2(0f, cellHeight * 0.16f), light);
                    CreateChartStroke(prefix + " Mist B", parent, new Vector2(cellWidth * 0.58f, 1.8f), position - new Vector2(cellWidth * 0.08f, cellHeight * 0.15f), light);
                    break;
                case SeaTerrainType.StormFront:
                    Image storm = CreateChartStroke(prefix + " Squall", parent, new Vector2(cellWidth * 0.78f, 2.4f), position, light);
                    storm.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -32f);
                    break;
                case SeaTerrainType.Strait:
                    CreateChartStroke(prefix + " Bank A", parent, new Vector2(2f, cellHeight * 0.8f), position - new Vector2(cellWidth * 0.23f, 0f), light);
                    CreateChartStroke(prefix + " Bank B", parent, new Vector2(2f, cellHeight * 0.8f), position + new Vector2(cellWidth * 0.23f, 0f), light);
                    break;
            }
        }

        private void DecorateVoyageEventMarker(Button marker, CoreMapNode node)
        {
            if (marker == null || node == null)
            {
                return;
            }

            Image frame = marker.targetGraphic as Image;
            if (frame != null)
            {
                Outline frameOutline = frame.gameObject.AddComponent<Outline>();
                frameOutline.effectColor = new Color(0.03f, 0.08f, 0.09f, 0.9f);
                frameOutline.effectDistance = new Vector2(2f, -2f);
                frameOutline.useGraphicAlpha = true;
            }

            Text glyph = marker.GetComponentInChildren<Text>();
            if (glyph != null)
            {
                Outline glyphOutline = glyph.gameObject.AddComponent<Outline>();
                glyphOutline.effectColor = new Color(0.02f, 0.05f, 0.06f, 0.92f);
                glyphOutline.effectDistance = new Vector2(1f, -1f);
                glyphOutline.useGraphicAlpha = true;
            }

            if (!node.IsChartVisible || !node.Discovered || art == null || node.Encounter == null)
            {
                return;
            }

            Texture2D eventSheet = art.EventSheet(session.SelectedRouteIndex);
            if (eventSheet == null)
            {
                return;
            }

            Vector2 markerSize = marker.GetComponent<RectTransform>().sizeDelta;
            RawImage icon = CreateRawImage(
                "Event Icon " + node.Id,
                marker.transform,
                markerSize - new Vector2(7f, 7f),
                Vector2.zero,
                eventSheet,
                art.EncounterUv(node.Encounter));
            UiImageAspect.Fit(icon, markerSize - new Vector2(7f, 7f));
            icon.color = node.Visited ? new Color(0.52f, 0.58f, 0.56f, 0.88f) : Color.white;
            icon.raycastTarget = false;
            icon.transform.SetAsFirstSibling();
        }

        private Image CreateChartStroke(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            Image stroke = CreateImage(name, parent, size, position, color);
            stroke.raycastTarget = false;
            return stroke;
        }

        private bool CellIsExplored(int column, int row)
        {
            if (column < 0 || column >= CoreVoyageMap.GridColumns || row < 0 || row >= CoreVoyageMap.GridRows)
            {
                return false;
            }
            return session.Map.ExploredCells[row * CoreVoyageMap.GridColumns + column];
        }

        private bool TerrainConnectsTo(int column, int row, SeaTerrainType terrain, int terrainFeatureId)
        {
            if (!session.Map.TryGetCell(column, row, out CoreVoyageMapCell neighbor))
            {
                return false;
            }

            if (terrainFeatureId >= 0 || neighbor.TerrainFeatureId >= 0)
            {
                return terrainFeatureId >= 0 && terrainFeatureId == neighbor.TerrainFeatureId;
            }
            return TerrainConnects(terrain, neighbor.Terrain);
        }

        private static bool TerrainConnects(SeaTerrainType left, SeaTerrainType right)
        {
            if (left == right) return true;
            return IsCoastLand(left) && IsCoastLand(right);
        }

        private static bool IsCoastLand(SeaTerrainType terrain)
        {
            return terrain == SeaTerrainType.Island || terrain == SeaTerrainType.Beach;
        }

        private static Color FallbackWaterColor(int routeIndex, uint visualHash)
        {
            Color baseColor = ProceduralWaterColor(routeIndex);
            float amount = ((visualHash >> 20) & 0x1Fu) / 31f;
            Color low = Color.Lerp(baseColor, Color.black, 0.14f);
            Color high = Color.Lerp(baseColor, Html("#3C8190"), 0.19f);
            return Color.Lerp(low, high, amount);
        }

        private static Color RouteWaterTint(int routeIndex)
        {
            switch (routeIndex)
            {
                case 1: return new Color(0.84f, 0.94f, 0.85f, 1f);
                case 2: return new Color(0.77f, 0.86f, 0.95f, 1f);
                default: return new Color(0.90f, 0.94f, 0.95f, 1f);
            }
        }

        private static Color TerrainFillColor(SeaTerrainType terrain, uint visualHash)
        {
            float variation = ((visualHash >> 17) & 0x0Fu) / 15f;
            Color low;
            Color high;
            switch (terrain)
            {
                case SeaTerrainType.Shoal:
                    low = new Color(0.43f, 0.62f, 0.48f, 0.12f);
                    high = new Color(0.65f, 0.72f, 0.48f, 0.18f);
                    break;
                case SeaTerrainType.Reef:
                    low = new Color(0.48f, 0.2f, 0.18f, 0.14f);
                    high = new Color(0.78f, 0.38f, 0.25f, 0.21f);
                    break;
                case SeaTerrainType.KelpField:
                    low = new Color(0.08f, 0.28f, 0.19f, 0.11f);
                    high = new Color(0.16f, 0.48f, 0.28f, 0.18f);
                    break;
                case SeaTerrainType.FogBank:
                    low = new Color(0.48f, 0.6f, 0.62f, 0.08f);
                    high = new Color(0.72f, 0.8f, 0.79f, 0.14f);
                    break;
                case SeaTerrainType.StormFront:
                    low = new Color(0.24f, 0.18f, 0.38f, 0.14f);
                    high = new Color(0.45f, 0.31f, 0.58f, 0.22f);
                    break;
                case SeaTerrainType.Island:
                    low = new Color(0.12f, 0.24f, 0.12f, 0.18f);
                    high = new Color(0.28f, 0.39f, 0.18f, 0.28f);
                    break;
                case SeaTerrainType.Beach:
                    low = new Color(0.64f, 0.49f, 0.24f, 0.14f);
                    high = new Color(0.84f, 0.7f, 0.38f, 0.22f);
                    break;
                case SeaTerrainType.Strait:
                    low = new Color(0.15f, 0.5f, 0.55f, 0.09f);
                    high = new Color(0.28f, 0.72f, 0.74f, 0.16f);
                    break;
                default:
                    return Color.clear;
            }
            return Color.Lerp(low, high, variation);
        }

        private static Color TerrainEdgeColor(SeaTerrainType terrain)
        {
            switch (terrain)
            {
                case SeaTerrainType.Shoal: return new Color(0.84f, 0.82f, 0.52f, 0.72f);
                case SeaTerrainType.Reef: return new Color(0.95f, 0.48f, 0.34f, 0.82f);
                case SeaTerrainType.KelpField: return new Color(0.31f, 0.72f, 0.42f, 0.72f);
                case SeaTerrainType.FogBank: return new Color(0.82f, 0.9f, 0.88f, 0.52f);
                case SeaTerrainType.StormFront: return new Color(0.72f, 0.58f, 0.9f, 0.76f);
                case SeaTerrainType.Island: return new Color(0.68f, 0.75f, 0.42f, 0.85f);
                case SeaTerrainType.Beach: return new Color(0.94f, 0.8f, 0.46f, 0.9f);
                case SeaTerrainType.Strait: return new Color(0.54f, 0.9f, 0.91f, 0.7f);
                default: return Color.clear;
            }
        }
    }
}
