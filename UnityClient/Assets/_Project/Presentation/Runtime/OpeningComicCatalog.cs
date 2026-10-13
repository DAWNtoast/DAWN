using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed class OpeningComicPage
    {
        public OpeningComicPage(string imageResource, string narration, Rect narrativeBounds)
        {
            ImageResource = imageResource;
            Narration = narration;
            NarrativeBounds = narrativeBounds;
        }

        public string ImageResource { get; }
        public string Narration { get; }
        /// <summary>Safe lettering area in bottom-left-origin source UV coordinates.</summary>
        public Rect NarrativeBounds { get; }
    }

    /// <summary>Opening story and a one-shot request from an explicitly started new game.</summary>
    public static class OpeningComicCatalog
    {
        public const float SecondsPerPage = 6f;
        private static bool playbackRequested;

        public static IReadOnlyList<OpeningComicPage> Pages { get; } = Array.AsReadOnly(new[]
        {
            new OpeningComicPage("OpeningComics/opening_page01",
                "老船长曾带着六港的流亡者，升起自由旗。\n他要让每个无处可去的人，都有一个泊位。",
                new Rect(.20f, .085f, .60f, .10f)),
            new OpeningComicPage("OpeningComics/opening_page02",
                "潮门开启，旧日同伴却换上黑帆。\n自由的航路，成了索取过路费的关卡。",
                new Rect(.20f, .085f, .60f, .10f)),
            new OpeningComicPage("OpeningComics/opening_page03",
                "老船长留下海图、罗盘、铜钟和未竟的约定。\n随后，独自驶向无人灯塔。",
                new Rect(.20f, .095f, .60f, .10f)),
            new OpeningComicPage("OpeningComics/opening_page04",
                "如今，你在断锚港接下这段未竟的航程。\n为后来者，留一个能够安心靠岸的泊位。",
                new Rect(.20f, .11f, .60f, .10f)),
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
