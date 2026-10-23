using System;
using System.Collections.Generic;

namespace ShatteredSail.Core.Gameplay
{
    public sealed partial class CoreVoyageSession
    {
        private int requestedDistanceNm = MinimumDistanceNm;
        private bool usesDefaultDistance = true;

        private sealed class SailingCoursePlan
        {
            public List<VoyagePoint> Path;
            public bool Clipped, DockingApproach;
            public int PlannedDistanceNm, MaximumDistanceNm, DefaultDistanceNm;
            public float SpeedKnots, BaseSpeedKnots, CurrentStrength, CurrentSpeedModifier, WindStrength, WindSpeedModifier;
            public SailingDirection CurrentDirection, WindDirection;
            public string CurrentRelation, WindRelation;
        }

        private SailingCoursePlan BuildCurrentCoursePlan()
        {
            VoyagePoint direction = DirectionVector(Heading);
            SailingCoursePlan plan = ConditionsForCourse(direction);
            if (Map == null) return plan;

            VoyagePoint target;
            bool clipped = false;
            if (HasDirectCourseTarget) target = NormalizeDirectCourseTarget(DirectCourseTarget);
            else
            {
                // Resolve the provisional manual endpoint without calling a
                // public distance getter: rerouting may change the first heading.
                float distance = plan.PlannedDistanceNm / Map.Route.NauticalMilesPerMapUnit;
                var requested = new VoyagePoint(Position.X + direction.X * distance, Position.Y + direction.Y * distance);
                target = new VoyagePoint(Clamp(requested.X, 2f, Map.WorldWidth - 2f), Clamp(requested.Y, 2f, Map.WorldHeight - 2f));
                clipped = Distance(Position, target) + .01f < distance;
            }
            List<VoyagePoint> path = FindNavigablePath(Position, target);
            List<VoyagePoint> dockingPath = SnapPathToDestination(path);
            bool dockingApproach = !ReferenceEquals(path, dockingPath);
            for (int index = 1; index < dockingPath.Count; index++)
            {
                float dx = dockingPath[index].X - dockingPath[index - 1].X;
                float dy = dockingPath[index].Y - dockingPath[index - 1].Y;
                if (Math.Abs(dx) + Math.Abs(dy) < .0001f) continue;
                plan = ConditionsForCourse(new VoyagePoint(dx, dy));
                break;
            }
            // Manual port capture retains its final approach, subject to the
            // same 24-hour safety limit. Direct courses retain their chosen budget.
            int budget = dockingApproach && !HasDirectCourseTarget ? plan.MaximumDistanceNm : plan.PlannedDistanceNm;
            plan.Path = TruncateCoursePath(dockingPath, budget / Map.Route.NauticalMilesPerMapUnit);
            plan.Clipped = clipped && !dockingApproach && ReferenceEquals(plan.Path, dockingPath);
            plan.DockingApproach = dockingApproach;
            return plan;
        }

        private SailingCoursePlan ConditionsForCourse(VoyagePoint direction)
        {
            double angle = Math.Abs(direction.X) + Math.Abs(direction.Y) > .0001f
                ? Math.Atan2(direction.Y, direction.X) : DirectionAngle(Heading);
            SeaCondition condition = GetSeaConditionAt(Position);
            double currentProjection = condition == null ? 0d :
                condition.CurrentVector.X * Math.Cos(angle) + condition.CurrentVector.Y * Math.Sin(angle);
            double windProjection = condition == null ? 0d :
                condition.WindVector.X * Math.Cos(angle) + condition.WindVector.Y * Math.Sin(angle);
            float currentModifier = condition == null ? 0f : condition.CurrentStrength * .72f * (float)currentProjection;
            float windModifier = condition == null ? 0f : condition.WindStrength * .28f * (float)windProjection;
            double speed = (double)EffectiveSpeedKnots + (HasNavigator ? 1d : 0d) + currentModifier + windModifier -
                (1d - Hull / (double)MaxHull) * 3.5d;
            if (double.IsNaN(speed) || double.IsInfinity(speed)) speed = 2.5d;
            float finiteSpeed = (float)Math.Max(2.5d, Math.Min(float.MaxValue, speed));
            int maximum = DistanceForHours(finiteSpeed, 24d, true);
            int defaultDistance = Math.Min(maximum, DistanceForHours(finiteSpeed, 12d, false));
            return new SailingCoursePlan
            {
                Path = new List<VoyagePoint> { Position },
                BaseSpeedKnots = finiteSpeed,
                SpeedKnots = sailingBoostHoursRemaining > 0d ? finiteSpeed * 1.25f : finiteSpeed,
                MaximumDistanceNm = maximum,
                DefaultDistanceNm = defaultDistance,
                PlannedDistanceNm = usesDefaultDistance ? defaultDistance : Math.Max(MinimumDistanceNm, Math.Min(maximum, requestedDistanceNm)),
                CurrentStrength = condition?.CurrentStrength ?? 0f,
                CurrentDirection = condition?.CurrentDirection ?? Heading,
                CurrentSpeedModifier = currentModifier,
                CurrentRelation = currentProjection > .25d ? "顺流" : currentProjection < -.25d ? "逆流" : "横流",
                WindStrength = condition?.WindStrength ?? 0f,
                WindDirection = condition?.WindDirection ?? Heading,
                WindSpeedModifier = windModifier,
                WindRelation = windProjection > .25d ? "顺风" : windProjection < -.25d ? "逆风" : "侧风",
            };
        }

        private int DistanceForHours(float speed, double hours, bool roundDown)
        {
            double distance = speed * (hours + Math.Min(hours, sailingBoostHoursRemaining) * .25d);
            if (double.IsNaN(distance)) return MinimumDistanceNm;
            if (distance >= int.MaxValue) return int.MaxValue;
            return Math.Max(MinimumDistanceNm, (int)(roundDown ? Math.Floor(distance) : Math.Round(distance)));
        }

        private SailingForecast CreateInterruptedForecast(SailingForecast planned, VoyagePoint target, float actualDistanceNm)
        {
            // An interruption shortens the original leg. Its side-offset marker
            // must not change wind/current projections or charge a different speed.
            double hours = ExplorationSailingHours(actualDistanceNm, planned.BaseSpeedKnots);
            int rations = RationCostFor(hours);
            return new SailingForecast
            {
                Start = planned.Start, Target = target, PlannedDistanceNm = planned.PlannedDistanceNm,
                ActualDistanceNm = actualDistanceNm,
                EffectiveSpeedKnots = hours > 0d ? (float)(actualDistanceNm / hours) : planned.EffectiveSpeedKnots,
                BaseSpeedKnots = planned.BaseSpeedKnots,
                Hours = hours, WaterCost = rations, FoodCost = rations,
                CurrentStrength = planned.CurrentStrength, CurrentDirection = planned.CurrentDirection,
                CurrentRelation = planned.CurrentRelation, CurrentSpeedModifier = planned.CurrentSpeedModifier,
                WindStrength = planned.WindStrength, WindDirection = planned.WindDirection,
                WindRelation = planned.WindRelation, WindSpeedModifier = planned.WindSpeedModifier,
                Clipped = planned.Clipped,
            };
        }
    }
}
