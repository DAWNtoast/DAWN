using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public readonly struct NavigationLookoutPose
    {
        public readonly Vector2 Position, Size;
        public readonly Color Color;
        public NavigationLookoutPose(Vector2 position, Vector2 size, Color color)
        { Position = position; Size = size; Color = color; }
        public NavigationLookoutPose Hidden() => new NavigationLookoutPose(Position, Size,
            new Color(Color.r, Color.g, Color.b, 0f));
        public static NavigationLookoutPose Lerp(NavigationLookoutPose a, NavigationLookoutPose b, float t) =>
            new NavigationLookoutPose(Vector2.Lerp(a.Position, b.Position, t), Vector2.Lerp(a.Size, b.Size, t),
                UnityEngine.Color.Lerp(a.Color, b.Color, t));
    }

    public sealed class NavigationLookoutFrame
    {
        public SeaTerrainType Terrain;
        public NavigationLookoutBlend Light;
        public IReadOnlyList<LookoutObservation> Observations;
    }

    /// <summary>Presentation-only sampled voyage. Projection, art loading and allocation happen before playback.</summary>
    public sealed class NavigationLookoutMotion : MonoBehaviour
    {
        private sealed class Scenery
        {
            public string Key;
            public RawImage Image;
            public LookoutSceneryKind Kind;
            public float Aspect, ContactRatio, Distance;
            public NavigationLookoutPose Current;
            public NavigationLookoutPose[] Poses;
            public bool[] Present;
        }
        private sealed class Ocean
        {
            public NavigationLookoutOceanGraphic Image;
            public float[] Weights;
        }
        private readonly List<Scenery> scenery = new List<Scenery>();
        private readonly List<Ocean> oceans = new List<Ocean>();
        private MonoBehaviour owner;
        private RectTransform viewport;
        private Vector2 fittedSize;
        private float clock, progress;
        private int frameCount;
        private bool paused, focusLost;
        public bool IsSuspended => paused || focusLost || (owner != null && !owner.isActiveAndEnabled);
        public float VoyageProgress => progress;
        public bool VoyagePrepared => frameCount > 1;

        public void Initialize(MonoBehaviour controller, NavigationLookoutOceanGraphic ocean,
            NavigationLookoutOceanGraphic transition)
        {
            owner = controller;
            viewport = ocean.transform.parent as RectTransform;
            oceans.Add(new Ocean { Image = ocean });
            oceans.Add(new Ocean { Image = transition });
            FitOceans();
        }

        public void Register(LookoutObservation item, RawImage image)
        {
            bool ship = item.Kind == LookoutSceneryKind.Vessel || SpecialWarshipArtCatalog.IsSpecialTexture(image.texture);
            float contact = ship ? SpecialWarshipArtCatalog.HullContactLocal(image).y / image.rectTransform.rect.height :
                (item.Kind == LookoutSceneryKind.PirateShip ? .055f : item.Kind == LookoutSceneryKind.Fog ? .30f : .18f) - .5f;
            scenery.Add(new Scenery
            {
                Key = item.Key, Image = image, Kind = item.Kind, Distance = item.DistanceNm,
                Aspect = image.rectTransform.sizeDelta.x / Mathf.Max(.01f, image.rectTransform.sizeDelta.y),
                ContactRatio = contact,
                Current = new NavigationLookoutPose(image.rectTransform.anchoredPosition, image.rectTransform.sizeDelta, image.color)
            });
        }

        public static NavigationLookoutPose ProjectPose(LookoutObservation item, float aspect, float contactRatio, Color tint)
        {
            bool weather = item.Kind == LookoutSceneryKind.Storm || item.Kind == LookoutSceneryKind.Fog;
            bool ship = item.Kind == LookoutSceneryKind.Vessel || item.Kind == LookoutSceneryKind.PirateShip;
            float width = (weather ? 920f : ship ? 365f : 730f) * item.Scale;
            float height = width / Mathf.Max(.01f, aspect);
            float waterline = 64f - item.Depth * item.Depth * 192f;
            Color color = Color.Lerp(new Color(.66f, .78f, .83f, 1f), Color.white, item.Proximity) * tint;
            color.a *= item.Visibility;
            return new NavigationLookoutPose(new Vector2(item.Horizontal * 440f, waterline - height * contactRatio),
                new Vector2(width, height), color);
        }

        public void PrepareVoyage(NavigationLookoutFrame[] frames, Func<LookoutObservation, Color, RawImage> createScenery)
        {
            if (frames == null || frames.Length < 2 || viewport == null) return;
            frameCount = frames.Length;
            progress = 0f;
            var lookup = new Dictionary<string, Scenery>(StringComparer.Ordinal);
            foreach (Scenery track in scenery)
            {
                track.Poses = new NavigationLookoutPose[frameCount];
                track.Present = new bool[frameCount];
                track.Poses[0] = track.Current;
                track.Present[0] = true;
                lookup[track.Key] = track;
            }
            for (int frame = 1; frame < frameCount; frame++)
            {
                NavigationLookoutFrame sample = frames[frame];
                Color tint = NavigationLookoutArtCatalog.EnvironmentTint(sample.Terrain, sample.Light);
                foreach (LookoutObservation item in sample.Observations)
                {
                    if (NavigationLookoutArtCatalog.ShouldSkipLocalWeather(item, sample.Terrain)) continue;
                    if (!lookup.TryGetValue(item.Key, out Scenery track))
                    {
                        RawImage image = createScenery(item, tint);
                        Register(item, image);
                        track = scenery[scenery.Count - 1];
                        track.Poses = new NavigationLookoutPose[frameCount];
                        track.Present = new bool[frameCount];
                        lookup.Add(item.Key, track);
                    }
                    track.Poses[frame] = ProjectPose(item, track.Aspect, track.ContactRatio, tint);
                    track.Present[frame] = true;
                    track.Distance = item.DistanceNm;
                }
            }
            // A target entering/leaving the field of view fades from its nearest valid projection.
            // Never draw an uninitialized pose at the center or stretch a transparent atlas cell.
            foreach (Scenery track in scenery)
                for (int frame = 0; frame < frameCount; frame++)
                    if (!track.Present[frame])
                    {
                        int nearest = -1;
                        for (int candidate = 0; candidate < frameCount; candidate++)
                            if (track.Present[candidate] && (nearest < 0 || Math.Abs(candidate - frame) < Math.Abs(nearest - frame)))
                                nearest = candidate;
                        track.Poses[frame] = nearest < 0 ? track.Current.Hidden() : track.Poses[nearest].Hidden();
                    }

            // Load only the painted environments used by this short route, before its first frame.
            for (int frame = 0; frame < frameCount; frame++)
            {
                EnsureOcean(NavigationLookoutArtCatalog.ForEnvironment(frames[frame].Terrain, frames[frame].Light.From));
                EnsureOcean(NavigationLookoutArtCatalog.ForEnvironment(frames[frame].Terrain, frames[frame].Light.To));
            }
            foreach (Ocean ocean in oceans) ocean.Weights = new float[frameCount];
            for (int frame = 0; frame < frameCount; frame++)
            {
                NavigationLookoutFrame sample = frames[frame];
                FindOcean(NavigationLookoutArtCatalog.ForEnvironment(sample.Terrain, sample.Light.From)).Weights[frame] += 1f - sample.Light.Amount;
                FindOcean(NavigationLookoutArtCatalog.ForEnvironment(sample.Terrain, sample.Light.To)).Weights[frame] += sample.Light.Amount;
            }
            scenery.Sort((a, b) => { int order = b.Distance.CompareTo(a.Distance); return order != 0 ? order : string.CompareOrdinal(a.Key, b.Key); });
            for (int i = 0; i < scenery.Count; i++) scenery[i].Image.transform.SetSiblingIndex(oceans.Count + i);
            ApplyVoyageProgress(0f);
        }

        private Ocean FindOcean(Texture texture)
        {
            foreach (Ocean layer in oceans) if (layer.Image.texture == texture) return layer;
            return null;
        }

        private void EnsureOcean(Texture texture)
        {
            if (FindOcean(texture) != null) return;
            var host = new GameObject("Lookout Ocean Voyage " + oceans.Count, typeof(RectTransform));
            var rect = (RectTransform)host.transform;
            rect.SetParent(viewport, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = viewport.rect.size;
            var image = host.AddComponent<NavigationLookoutOceanGraphic>();
            image.texture = texture;
            image.uvRect = NavigationLookoutArtCatalog.PanoramaUv(rect.sizeDelta, texture);
            image.color = Color.clear;
            image.raycastTarget = false;
            rect.SetSiblingIndex(oceans.Count);
            oceans.Add(new Ocean { Image = image });
        }

        public void ApplyVoyageProgress(float value)
        {
            if (frameCount < 2) return;
            progress = Mathf.Clamp01(value);
            float frame = progress * (frameCount - 1);
            int from = Mathf.Min(frameCount - 2, Mathf.FloorToInt(frame));
            float t = frame - from;
            foreach (Scenery track in scenery) track.Current = NavigationLookoutPose.Lerp(track.Poses[from], track.Poses[from + 1], t);
            float accumulated = 0f;
            foreach (Ocean ocean in oceans)
            {
                float weight = Mathf.Lerp(ocean.Weights[from], ocean.Weights[from + 1], t);
                accumulated += weight;
                // Source-over alpha normalized by all lower layers gives the exact weighted image,
                // avoiding a dim flash when both weather and time bands change together.
                ocean.Image.color = new Color(1f, 1f, 1f, accumulated > .00001f ? weight / accumulated : 0f);
            }
            ApplyScenery();
        }

        public void CompleteVoyage() => ApplyVoyageProgress(1f);

        private void LateUpdate()
        {
            if (!Application.isPlaying || IsSuspended || viewport == null) return;
            clock += Mathf.Min(.05f, Time.unscaledDeltaTime);
            if (fittedSize != viewport.rect.size) FitOceans();
            foreach (Ocean ocean in oceans) if (ocean.Image != null) ocean.Image.SetMotionTime(clock);
            ApplyScenery();
        }

        private void FitOceans()
        {
            if (viewport == null) return;
            fittedSize = viewport.rect.size;
            foreach (Ocean ocean in oceans)
            {
                if (ocean.Image == null) continue;
                ocean.Image.rectTransform.sizeDelta = fittedSize;
                ocean.Image.uvRect = NavigationLookoutArtCatalog.PanoramaUv(fittedSize, ocean.Image.texture);
            }
        }

        private void ApplyScenery()
        {
            for (int i = 0; i < scenery.Count; i++)
            {
                Scenery track = scenery[i];
                if (track.Image == null) continue;
                Vector2 drift = Vector2.zero;
                float phase = i * 1.73f;
                if (Application.isPlaying)
                {
                    if (track.Kind == LookoutSceneryKind.Fog || track.Kind == LookoutSceneryKind.Storm)
                        drift = new Vector2(Mathf.Sin(clock * .22f + phase) * 4f, Mathf.Sin(clock * .18f + phase) * .8f);
                    else if (track.Kind == LookoutSceneryKind.Vessel || track.Kind == LookoutSceneryKind.PirateShip)
                        drift.y = Mathf.Sin(clock * 1.25f + phase) * .9f;
                }
                track.Image.rectTransform.anchoredPosition = track.Current.Position + drift;
                track.Image.rectTransform.sizeDelta = track.Current.Size;
                track.Image.color = track.Current.Color;
            }
        }

        private void OnApplicationPause(bool value) => paused = value;
        private void OnApplicationFocus(bool focused) => focusLost = !focused;
        private void OnDisable() { paused = false; focusLost = false; }
        private void OnDestroy()
        {
            scenery.Clear();
            oceans.Clear();
            owner = null;
            viewport = null;
        }
    }
}
