using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed class TreasureComicDialogue
    {
        public TreasureComicDialogue(Rect bounds, string text)
        {
            Bounds = bounds;
            Text = text;
        }

        /// <summary>Safe lettering area in the source image's bottom-left-origin UV coordinates.</summary>
        public Rect Bounds { get; }
        public string Text { get; }
    }

    /// <summary>
    /// Lettering for the existing parchment captions and speech balloons.
    /// The six source pages use a 1672 x 941 canvas (approximately 16:9).
    /// Coordinates exclude balloon tails, rounded ends and ornamental frames.
    /// </summary>
    public static class TreasureComicDialogueCatalog
    {
        private static readonly IReadOnlyList<TreasureComicDialogue> empty =
            Array.Empty<TreasureComicDialogue>();

        private static readonly IReadOnlyList<TreasureComicDialogue>[] weathered =
        {
            Array.AsReadOnly(new[]
            {
                Area(55, 33, 262, 44, "盐封旧图，\n今夜重展。"),
                Area(450, 53, 207, 58, "铁甲守沉门，\n红帆守归途。"),
                Area(43, 432, 316, 42, "铁誓不沉，\n仍守旧约。"),
                Area(1452, 479, 157, 43, "归路在此，\n过路有价。"),
                Area(1330, 836, 286, 44, "一枚无针罗盘，\n牵起失落归途。")
            }),
            Array.AsReadOnly(new[]
            {
                Area(49, 35, 294, 36, "雾尽之处，\n沉门初现。"),
                Area(531, 62, 159, 49, "旧誓的尽头，\n就在前方。"),
                Area(448, 374, 142, 44, "铁壁不退，\n沉门不启。"),
                Area(1172, 382, 128, 44, "想回去？\n先付船资。"),
                Area(213, 675, 148, 44, "罗盘无针，\n旧路仍在。"),
                Area(1330, 783, 280, 68, "两道旧誓，\n守着同一扇门。")
            })
        };

        private static readonly IReadOnlyList<TreasureComicDialogue>[] gilded =
        {
            Array.AsReadOnly(new[]
            {
                Area(179, 49, 122, 42, "这笔旧税，\n还没销账。"),
                Area(1268, 273, 345, 42, "王庭账簿，\n藏着未清的债。"),
                Area(70, 377, 267, 41, "雷冠收港，\n炮声便是判词。"),
                Area(1010, 408, 111, 38, "听雷，\n领罪！"),
                Area(72, 667, 326, 44, "余烬焚证，\n旧誓化作飞灰。"),
                Area(1405, 691, 130, 40, "灰会散，\n火不会熄。")
            }),
            Array.AsReadOnly(new[]
            {
                Area(49, 37, 270, 42, "暴雨之中，\n雷冠挡住去路。"),
                Area(579, 87, 148, 58, "先稳住舵！\n齐射就要来了。"),
                Area(45, 480, 229, 36, "余烬炉门开，\n热浪逼近甲板。"),
                Area(243, 654, 111, 43, "压住火势！\n印记还在！"),
                Area(865, 480, 240, 36, "灰烬沉落后，\n王庭印记现身。"),
                Area(1495, 578, 119, 39, "找到了，\n带它回去！")
            })
        };

        private static readonly IReadOnlyList<TreasureComicDialogue>[] moonlit =
        {
            Array.AsReadOnly(new[]
            {
                Area(49, 42, 304, 37, "月蚀之夜，\n沉月王庭重现。"),
                Area(483, 380, 149, 43, "潮线合拢了，\n跟紧月光。"),
                Area(54, 819, 315, 34, "借来的航线，\n仍欠一程归途。"),
                Area(1463, 380, 149, 44, "谁还记得，\n他们的名字？"),
                Area(1289, 822, 325, 34, "幽灵船守在此，\n替亡者偿还归途。")
            }),
            Array.AsReadOnly(new[]
            {
                Area(48, 34, 242, 34, "残塔之间，\n最后一程月潮。"),
                Area(490, 328, 144, 39, "循着月光，\n再航一程。"),
                Area(1447, 34, 176, 31, "潮汐镇魂，\n仍候归人。"),
                Area(1021, 164, 133, 35, "赢下切磋，\n续回她的归路。"),
                Area(50, 554, 264, 37, "宝库已开，\n莫忘海上的名字。")
            })
        };

        public static IReadOnlyList<TreasureComicDialogue> GetPage(string rankId, int page)
        {
            IReadOnlyList<TreasureComicDialogue>[] pages;
            switch (rankId)
            {
                case "weathered": pages = weathered; break;
                case "gilded": pages = gilded; break;
                case "moonlit": pages = moonlit; break;
                default: return empty;
            }

            return page >= 0 && page < pages.Length ? pages[page] : empty;
        }

        private static TreasureComicDialogue Area(float x, float y, float width, float height, string text)
        {
            // Authored positions use top-left pixel coordinates, converted once to texture UVs.
            return new TreasureComicDialogue(new Rect(x / 1672f, 1f - (y + height) / 941f,
                width / 1672f, height / 941f), text);
        }
    }
}
