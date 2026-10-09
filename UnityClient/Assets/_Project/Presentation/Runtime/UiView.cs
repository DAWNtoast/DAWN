using System;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    [Flags]
    public enum UiRefreshRegion
    {
        None = 0,
        Header = 1,
        Battle = 2,
        Content = 4,
        Layout = 8,
        All = Header | Battle | Content | Layout,
    }

    /// <summary>Model and layout identity, not the values a retained region displays.</summary>
    public readonly struct UiViewState : IEquatable<UiViewState>
    {
        private readonly object model;
        private readonly string screenKey;
        private readonly int variant;
        private readonly int layoutVersion;
        private readonly Vector2 viewportSize;

        public UiViewState(object model, string screenKey, int variant, int layoutVersion, Vector2 viewportSize)
        {
            this.model = model;
            this.screenKey = screenKey;
            this.variant = variant;
            this.layoutVersion = layoutVersion;
            this.viewportSize = viewportSize;
        }

        public bool Equals(UiViewState other) => ReferenceEquals(model, other.model) &&
            string.Equals(screenKey, other.screenKey, StringComparison.Ordinal) && variant == other.variant &&
            layoutVersion == other.layoutVersion && viewportSize == other.viewportSize;
    }

    /// <summary>
    /// Explicit build/refresh lifecycle. A view opts into only the regions it can
    /// update safely; callers rebuild when TryRefresh rejects a request.
    /// </summary>
    public sealed class UiView
    {
        private readonly Action build;
        private readonly Func<Transform> findAnchor;
        private readonly Func<UiRefreshRegion, bool> refresh;
        private readonly UiRefreshRegion supportedRegions;
        private Transform root;
        private Transform anchor;
        private UiViewState state;

        public UiView(Action build, Func<Transform> findAnchor, UiRefreshRegion supportedRegions,
            Func<UiRefreshRegion, bool> refresh = null)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.findAnchor = findAnchor ?? throw new ArgumentNullException(nameof(findAnchor));
            this.supportedRegions = supportedRegions & ~UiRefreshRegion.Layout;
            this.refresh = refresh;
        }

        public void Build(Transform root, UiViewState state)
        {
            Invalidate();
            if (root == null) throw new ArgumentNullException(nameof(root));
            build();
            this.root = root;
            anchor = findAnchor();
            this.state = state;
        }

        public bool TryRefresh(Transform root, UiViewState state, UiRefreshRegion dirty)
        {
            if (dirty == UiRefreshRegion.None || (dirty & ~supportedRegions) != 0 || refresh == null ||
                this.root == null || this.root != root || !this.root.gameObject.activeInHierarchy ||
                anchor == null || !anchor.gameObject.activeInHierarchy ||
                (anchor != root && !anchor.IsChildOf(root)) || !this.state.Equals(state))
                return false;
            return refresh(dirty);
        }

        public void Invalidate()
        {
            root = null;
            anchor = null;
            state = default;
        }
    }
}
