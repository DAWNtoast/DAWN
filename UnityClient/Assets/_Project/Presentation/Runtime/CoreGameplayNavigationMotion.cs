using System;
using System.Collections;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private bool navigationMotionPlaying;
        private bool navigationPresentationNeedsRecovery;
        private Coroutine navigationMotionRoutine;
        private RectTransform navigationMotionInputGuard;
        private readonly List<Image> navigationMotionWake = new List<Image>();

        private void SuspendNavigationPresentation()
        {
            if (!Application.isPlaying || !navigationMotionPlaying) return;
            navigationPresentationNeedsRecovery = true;
            if (navigationMotionRoutine != null) StopCoroutine(navigationMotionRoutine);
            navigationMotionRoutine = null;
            navigationMotionPlaying = false;
            if (navigationLookoutMotion != null) navigationLookoutMotion.CompleteVoyage();
            ClearNavigationWake();
            if (navigationMotionInputGuard != null) navigationMotionInputGuard.gameObject.SetActive(false);
        }

        private void ResumeNavigationPresentation()
        {
            if (!Application.isPlaying || !navigationPresentationNeedsRecovery || session == null || contentRoot == null) return;
            navigationPresentationNeedsRecovery = false;
            // Sail committed before the tween started. Reconcile the screen to
            // that position without charging time or supplies a second time.
            Render();
        }

        /// <summary>
        /// Commits the same core voyage step as before, then animates only the
        /// already-rendered map marker. The session remains authoritative while
        /// the short presentation plays, so events and resource costs cannot be
        /// duplicated by a second click.
        /// </summary>
        private void BeginNavigationMotion(Button source)
        {
            if (!isActiveAndEnabled || navigationMotionPlaying || session == null || session.Phase != CoreDemoPhase.Sailing)
            {
                return;
            }

            SailingForecast forecast = session.GetForecast();
            if (forecast == null || forecast.Blocked)
            {
                session.Sail();
                PlaySfx("sail_step", 0.44f);
                Render();
                return;
            }

            VoyagePoint start = session.Position;
            double startHours = session.ElapsedGameHours;
            double startVoyageHours = session.TotalHours;
            RectTransform mapRoot = FindNavigationMapRoot();
            RectTransform ship = FindNavigationVisual(mapRoot, "Player Ship");
            List<VoyagePoint> forecastPath = BuildNavigationMotionPath(forecast.PathPoints, start, forecast.Target);

            navigationMotionPlaying = true;
            bool sailed = session.Sail();
            PlaySfx("sail_step", 0.44f);
            if (!sailed)
            {
                navigationMotionPlaying = false;
                Render();
                return;
            }

            List<VoyagePoint> motionPath = BuildNavigationMotionPath(forecastPath, start, session.Position);
            if (ship == null || mapRoot == null || motionPath.Count < 2 || NavigationPathPixels(motionPath) < 1f)
            {
                navigationMotionPlaying = false;
                Render();
                return;
            }

            PrepareLookoutVoyage(motionPath, startHours, session.TotalHours - startVoyageHours);
            BuildNavigationMotionInputGuard();
            navigationMotionRoutine = StartCoroutine(AnimateNavigationMotion(motionPath, mapRoot, ship));
        }

        private void PrepareLookoutVoyage(IList<VoyagePoint> path, double startHours, double sailingHours)
        {
            if (navigationLookoutMotion == null || !navigationLookoutMotion.isActiveAndEnabled || session.Map == null) return;
            // A handful of route samples preserves turns and near/far parallax without
            // allocating a fresh map projection or loading resources in the frame loop.
            const int samples = 5;
            var frames = new NavigationLookoutFrame[samples];
            float vision = 120f + (session.HasNavigator ? 60f : 0f) + session.VisionBonusNm;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                VoyagePoint position = NavigationPointAt(path, t);
                frames[i] = new NavigationLookoutFrame
                {
                    Terrain = session.Map.TerrainAt(position),
                    Light = NavigationLookoutArtCatalog.BlendForVoyageHours(startHours + sailingHours * t),
                    Observations = i == 0 ? null : VoyageLookoutProjector.Project(session.Map, position, session.Heading, vision)
                };
            }
            navigationLookoutMotion.PrepareVoyage(frames, CreateLookoutScenery);
        }

        private RectTransform FindNavigationMapRoot()
        {
            if (contentRoot == null) return null;
            Transform map = contentRoot.Find("Navigation Chart/Procedural Sea Chart Frame/Map Layer");
            return map as RectTransform;
        }

        private static RectTransform FindNavigationVisual(RectTransform mapRoot, string name)
        {
            if (mapRoot == null) return null;
            Transform visual = mapRoot.Find(name);
            return visual as RectTransform;
        }

        private void BuildNavigationMotionInputGuard()
        {
            if (navigationMotionInputGuard != null)
            {
                navigationMotionInputGuard.gameObject.SetActive(true);
                navigationMotionInputGuard.SetAsLastSibling();
                return;
            }

            // Block repeat pointer input during the brief automatic move without
            // dimming the chart or showing a status message/skip interaction.
            navigationMotionInputGuard = CreateRect("Navigation Motion Input Guard", contentRoot,
                Vector2.zero, Vector2.zero);
            navigationMotionInputGuard.anchorMin = Vector2.zero;
            navigationMotionInputGuard.anchorMax = Vector2.one;
            Image guard = navigationMotionInputGuard.gameObject.AddComponent<Image>();
            guard.color = Color.clear;
            guard.raycastTarget = true;
            navigationMotionInputGuard.SetAsLastSibling();
        }

        private IEnumerator AnimateNavigationMotion(
            List<VoyagePoint> path,
            RectTransform mapRoot,
            RectTransform ship)
        {
            Vector2 origin = ship.anchoredPosition;

            CreateNavigationWake(mapRoot, ship);
            float routePixels = NavigationPathPixels(path);
            float duration = Mathf.Clamp(0.20f + routePixels / 2000f, 0.24f, 0.50f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (navigationLookoutMotion != null && navigationLookoutMotion.IsSuspended)
                {
                    yield return null;
                    continue;
                }
                elapsed += Mathf.Min(.05f, Time.unscaledDeltaTime);
                float raw = Mathf.Clamp01(elapsed / duration);
                float eased = raw * raw * (3f - 2f * raw);
                UpdateNavigationMotionVisuals(path, eased, ship, origin);
                yield return null;
            }

            UpdateNavigationMotionVisuals(path, 1f, ship, origin);
            ClearNavigationWake();
            if (session != null && session.Phase == CoreDemoPhase.Settlement && session.Settlement != null &&
                session.Settlement.Outcome == VoyageOutcome.ArrivedAtPort)
            {
                PlaySfx("harbor_arrive", 0.5f);
            }

            navigationMotionPlaying = false;
            navigationMotionRoutine = null;
            if (navigationMotionInputGuard != null) navigationMotionInputGuard.gameObject.SetActive(false);
            Render();
        }

        private void UpdateNavigationMotionVisuals(
            IList<VoyagePoint> path,
            float progress,
            RectTransform ship,
            Vector2 origin)
        {
            if (navigationLookoutMotion != null) navigationLookoutMotion.ApplyVoyageProgress(progress);
            VoyagePoint current = NavigationPointAt(path, progress);
            Vector2 currentMapPosition = MapPosition(current);
            Vector2 originMapPosition = MapPosition(path[0]);
            Vector2 delta = currentMapPosition - originMapPosition;
            if (ship != null)
            {
                ship.anchoredPosition = origin + delta;
                ship.parent.GetComponent<NavigationMapViewport>()?.EnsureVisible(ship.anchoredPosition);
            }

            for (int index = 0; index < navigationMotionWake.Count; index += 1)
            {
                Image wake = navigationMotionWake[index];
                if (wake == null) continue;
                float tailProgress = Mathf.Max(0f, progress - 0.055f * (index + 1));
                Vector2 tailPosition = MapPosition(NavigationPointAt(path, tailProgress));
                Vector2 wakeDelta = currentMapPosition - tailPosition;
                wake.rectTransform.anchoredPosition = Vector2.Lerp(currentMapPosition, tailPosition, 0.58f);
                if (wakeDelta.sqrMagnitude > 0.01f)
                    wake.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(wakeDelta.y, wakeDelta.x) * Mathf.Rad2Deg);
                float alpha = Mathf.Clamp01(0.36f * (1f - index * 0.18f) * (1f - progress * 0.35f));
                wake.color = new Color(0.66f, 0.9f, 0.92f, alpha);
            }
        }

        private void CreateNavigationWake(RectTransform mapRoot, RectTransform ship)
        {
            ClearNavigationWake();
            for (int index = 0; index < 3; index += 1)
            {
                Image wake = CreateImage("Voyage Wake " + index, mapRoot,
                    new Vector2(22f + index * 8f, 7f + index * 2f), ship.anchoredPosition,
                    new Color(0.66f, 0.9f, 0.92f, 0.32f), circleSprite);
                wake.raycastTarget = false;
                navigationMotionWake.Add(wake);
            }
        }

        private void ClearNavigationWake()
        {
            for (int index = navigationMotionWake.Count - 1; index >= 0; index -= 1)
            {
                Image wake = navigationMotionWake[index];
                if (wake != null)
                {
                    if (Application.isPlaying) Destroy(wake.gameObject); else DestroyImmediate(wake.gameObject);
                }
            }
            navigationMotionWake.Clear();
        }

        private static List<VoyagePoint> BuildNavigationMotionPath(
            IList<VoyagePoint> source,
            VoyagePoint start,
            VoyagePoint endpoint)
        {
            var path = new List<VoyagePoint>();
            path.Add(start);
            if (source == null || source.Count == 0)
            {
                if (NavigationPointDistance(start, endpoint) > 0.001f) path.Add(endpoint);
                return path;
            }

            int bestSegment = -1;
            float bestT = 0f;
            float bestDistance = float.MaxValue;
            for (int index = 0; index < source.Count - 1; index += 1)
            {
                Vector2 a = new Vector2(source[index].X, source[index].Y);
                Vector2 b = new Vector2(source[index + 1].X, source[index + 1].Y);
                Vector2 target = new Vector2(endpoint.X, endpoint.Y);
                Vector2 segment = b - a;
                float lengthSquared = segment.sqrMagnitude;
                float t = lengthSquared < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(target - a, segment) / lengthSquared);
                float distance = Vector2.Distance(target, a + segment * t);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestSegment = index;
                    bestT = t;
                }
            }

            if (bestSegment >= 0 && bestDistance <= 1.5f)
            {
                for (int index = 0; index <= bestSegment; index += 1)
                {
                    if (NavigationPointDistance(path[path.Count - 1], source[index]) > 0.001f)
                        path.Add(source[index]);
                }
                if (NavigationPointDistance(path[path.Count - 1], endpoint) > 0.001f) path.Add(endpoint);
                return path;
            }

            for (int index = 0; index < source.Count; index += 1)
            {
                if (NavigationPointDistance(path[path.Count - 1], source[index]) > 0.001f)
                    path.Add(source[index]);
            }
            if (NavigationPointDistance(path[path.Count - 1], endpoint) > 0.001f) path.Add(endpoint);
            return path;
        }

        private static VoyagePoint NavigationPointAt(IList<VoyagePoint> path, float progress)
        {
            if (path == null || path.Count == 0) return new VoyagePoint(0f, 0f);
            if (path.Count == 1 || progress <= 0f) return path[0];
            if (progress >= 1f) return path[path.Count - 1];

            float total = NavigationPathDistance(path);
            if (total <= 0.0001f) return path[path.Count - 1];
            float target = total * progress;
            float travelled = 0f;
            for (int index = 1; index < path.Count; index += 1)
            {
                VoyagePoint from = path[index - 1];
                VoyagePoint to = path[index];
                float segment = NavigationPointDistance(from, to);
                if (travelled + segment >= target)
                {
                    float t = segment <= 0.0001f ? 1f : (target - travelled) / segment;
                    return new VoyagePoint(Mathf.Lerp(from.X, to.X, t), Mathf.Lerp(from.Y, to.Y, t));
                }
                travelled += segment;
            }
            return path[path.Count - 1];
        }

        private float NavigationPathPixels(IList<VoyagePoint> path)
        {
            float pixels = 0f;
            if (path == null) return pixels;
            for (int index = 1; index < path.Count; index += 1) pixels += Vector2.Distance(MapPosition(path[index - 1]), MapPosition(path[index]));
            return pixels;
        }

        private static float NavigationPathDistance(IList<VoyagePoint> path)
        {
            float distance = 0f;
            if (path == null) return distance;
            for (int index = 1; index < path.Count; index += 1) distance += NavigationPointDistance(path[index - 1], path[index]);
            return distance;
        }

        private static float NavigationPointDistance(VoyagePoint a, VoyagePoint b)
        {
            return Mathf.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        }
    }
}
