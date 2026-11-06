using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed class OpeningComicPage
    {
        public OpeningComicPage(string imageResource)
        {
            ImageResource = imageResource;
        }

        public string ImageResource { get; }
        // Compatibility for offline audit consumers; wordless pages have no lettering.
        public string Narration => string.Empty;
        public Rect NarrativeBounds => default;
    }

    /// <summary>Opening story and a one-shot request from an explicitly started new game.</summary>
    public static class OpeningComicCatalog
    {
        public const float SecondsPerPage = 12f;
        private static bool playbackRequested;

        public static IReadOnlyList<OpeningComicPage> Pages { get; } = Array.AsReadOnly(new[]
        {
            new OpeningComicPage("OpeningComics/opening_page01"),
            new OpeningComicPage("OpeningComics/opening_page02"),
            new OpeningComicPage("OpeningComics/opening_page03"),
            new OpeningComicPage("OpeningComics/opening_page04"),
        });

        public static void RequestPlayback() => playbackRequested = true;

        public static void ClearPlaybackRequest() => playbackRequested = false;

        public static bool ConsumePlaybackRequest()
        {
            bool requested = playbackRequested;
            playbackRequested = false;
            return requested;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlaybackRequest() => ClearPlaybackRequest();
    }
}
