using ShatteredSail.Core.Gameplay;

namespace ShatteredSail.Presentation
{
    /// <summary>Builds fitting conditions from the current voyage location and environment.</summary>
    public static class ComponentVoyageContextResolver
    {
        public static ComponentVoyageContext FromSession(CoreVoyageSession session)
        {
            CoreVoyageTimeBand coreTime = session == null ? CoreVoyageTimeBand.Day : session.CurrentVoyageTimeBand;
            ComponentTimeBand time = coreTime == CoreVoyageTimeBand.Dawn ? ComponentTimeBand.Dawn :
                coreTime == CoreVoyageTimeBand.Day ? ComponentTimeBand.Day :
                coreTime == CoreVoyageTimeBand.Dusk ? ComponentTimeBand.Dusk : ComponentTimeBand.Night;
            CoreVoyageWeather coreWeather = session == null ? CoreVoyageWeather.Calm : session.CurrentVoyageWeather;
            ComponentWeatherType weather = coreWeather == CoreVoyageWeather.Storm ? ComponentWeatherType.Storm :
                coreWeather == CoreVoyageWeather.Fog ? ComponentWeatherType.Fog :
                coreWeather == CoreVoyageWeather.Wind ? ComponentWeatherType.Wind :
                coreWeather == CoreVoyageWeather.Rain ? ComponentWeatherType.Rain : ComponentWeatherType.Calm;
            return new ComponentVoyageContext(time, RouteAtCurrentLocation(session), weather);
        }

        private static ComponentRouteType RouteAtCurrentLocation(CoreVoyageSession session)
        {
            if (session == null || session.Map == null) return ComponentRouteType.OpenSea;
            // The core resolves the active encounter's authored terrain first,
            // otherwise the terrain cell at the ship's current position.
            switch (session.CurrentEncounterTerrain)
            {
                case EventTerrainConstraint.Beach: return ComponentRouteType.Coastal;
                case EventTerrainConstraint.Strait: return ComponentRouteType.Strait;
                case EventTerrainConstraint.Reef: return ComponentRouteType.Reef;
            }
            // Fog and storms are weather conditions, not a separate route type.
            // Only water without a local terrain classification uses the voyage's lane.
            if (session.SelectedRouteIndex == 1) return ComponentRouteType.Trade;
            if (session.SelectedRouteIndex >= 2) return ComponentRouteType.DeepSea;
            return ComponentRouteType.OpenSea;
        }
    }
}
