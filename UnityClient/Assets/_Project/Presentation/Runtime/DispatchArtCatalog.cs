using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Loads interactive charts and deployment frames. The table is painted into
    /// PortFacilityArtCatalog's room background, never a separate foreground sprite.
    /// </summary>
    public sealed class DispatchArtCatalog
    {
        private readonly Texture2D[] missionCharts;
        private readonly Texture2D[] missionFrames;

        public DispatchArtCatalog()
        {
            missionCharts = new[]
            {
                Load("dispatch_chart_salvage_v1"),
                Load("dispatch_chart_escort_v1"),
                Load("dispatch_chart_survey_v1"),
            };
            missionFrames = new[]
            {
                Load("dispatch_frame_salvage_v1"),
                Load("dispatch_frame_escort_v1"),
                Load("dispatch_frame_survey_v1"),
            };
        }

        public bool IsAvailable
        {
            get
            {
                for (int index = 0; index < missionCharts.Length; index += 1)
                {
                    if (missionCharts[index] == null || missionFrames[index] == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public Texture2D MissionChart(int index)
        {
            return missionCharts[Mathf.Clamp(index, 0, missionCharts.Length - 1)];
        }

        public Texture2D MissionFrame(int index)
        {
            return missionFrames[Mathf.Clamp(index, 0, missionFrames.Length - 1)];
        }

        private static Texture2D Load(string fileName)
        {
            Texture2D texture = Resources.Load<Texture2D>("DispatchV2/" + fileName);
            if (texture != null)
            {
                bool projectedChart = fileName.StartsWith("dispatch_chart_");
                texture.filterMode = projectedChart ? FilterMode.Trilinear : FilterMode.Bilinear;
                if (projectedChart) texture.anisoLevel = 4;
                texture.wrapMode = TextureWrapMode.Clamp;
            }

            return texture;
        }
    }
}
