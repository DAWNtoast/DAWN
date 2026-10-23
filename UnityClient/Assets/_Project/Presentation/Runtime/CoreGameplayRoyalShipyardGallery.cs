using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private Text BuildRoyalShipyardConsole(RectTransform panel, int index, List<int> visibleShips, out Button inspect)
        {
            Color brass = Html("#DEC18B");
            Color ivory = Html("#F5EBD3");
            Color muted = Html("#AFBDB8");
            CoreShipDefinition definition = CoreVoyageSession.Ships[index];
            RectTransform plaque = CreateRect("Shipyard Gallery Plaque", panel,
                new Vector2(1450f, 274f), new Vector2(0f, -345f));
            // UVs remove only transparent export margins; the authored PNG remains intact.
            Texture2D console = Resources.Load<Texture2D>("PortFacilities/royal_shipyard_console_v1");
            RawImage frame = CreateRawImage("Shipyard Royal Brass Console", plaque, plaque.sizeDelta,
                Vector2.zero, console, new Rect(0f, .16f, 1f, .76f));
            frame.raycastTarget = false;

            ShipyardText("Shipyard Registry Caption", plaque, new Vector2(400f, 22f),
                new Vector2(-350f, 32f), 15, brass, TextAnchor.MiddleLeft).text =
                session.CurrentPort + "  /  皇家船籍";
            Text title = ShipyardText("Shipyard Ship Name " + index, plaque, new Vector2(400f, 46f),
                new Vector2(-350f, -3f), 31, ivory, TextAnchor.MiddleLeft);
            GameTypography.Apply(title, 31, FontStyle.Bold);
            title.text = definition.Name;
            ShipyardText("Shipyard Gallery Role", plaque, new Vector2(400f, 28f),
                new Vector2(-350f, -42f), 20, muted, TextAnchor.MiddleLeft).text = CoreShipMarketCatalog.Role(index);

            ShipyardConsoleRule(plaque, -117f);
            ShipyardConsoleRule(plaque, 302f);
            ShipyardText("Shipyard Base Specification Caption", plaque, new Vector2(360f, 22f),
                new Vector2(88f, 35f), 15, brass).text = "基 础 船 型";
            string[] captions = { "船体", "航速", "货舱" };
            string[] values = { definition.MaxHull.ToString("0"), definition.SpeedKnots.ToString("0.#") + " 节",
                definition.CargoCapacity.ToString("0") };
            for (int stat = 0; stat < captions.Length; stat++)
            {
                float x = -42f + stat * 130f;
                ShipyardText("Shipyard Gallery Stat Label " + stat, plaque, new Vector2(116f, 22f),
                    new Vector2(x, 8f), 16, muted).text = captions[stat];
                ShipyardText("Shipyard Gallery Stat Value " + stat, plaque, new Vector2(116f, 34f),
                    new Vector2(x, -23f), 26, ivory).text = values[stat];
            }
            ShipyardText("Shipyard Gallery Ownership", plaque, new Vector2(390f, 28f),
                new Vector2(88f, -65f), 18, brass).text = ShipyardOwnershipLabel(index);
            inspect = CreateButton("Shipyard Inspect Vessel", plaque, new Vector2(252f, 64f),
                new Vector2(463f, -10f), Gold, ivory, 24);
            inspect.GetComponentInChildren<Text>().text = "查看船只档案";
            inspect.GetComponent<Image>().pixelsPerUnitMultiplier = 14f;
            WoodButtonSkin.SetRole(inspect, WoodButtonRole.Primary);
            Text count = ShipyardText("Shipyard Gallery Page", panel, new Vector2(180f, 30f),
                new Vector2(0f, -188f), 21, ivory);
            count.text = (visibleShips.IndexOf(index) + 1).ToString("00") + "   /   " + visibleShips.Count.ToString("00");
            Shadow countShadow = count.gameObject.AddComponent<Shadow>();
            countShadow.effectColor = new Color(0f, .035f, .045f, .95f);
            countShadow.effectDistance = new Vector2(1f, -2f);
            foreach (float side in new[] { -1f, 1f })
            {
                Image rule = CreateImage("Shipyard Page Rule", panel, new Vector2(96f, 1f),
                    new Vector2(side * 129f, -188f), new Color(brass.r, brass.g, brass.b, .7f));
                rule.raycastTarget = false;
            }
            return title;
        }

        private void ShipyardConsoleRule(Transform parent, float x)
        {
            Image rule = CreateImage("Shipyard Console Divider", parent, new Vector2(1f, 102f),
                new Vector2(x, -16f), new Color(.76f, .61f, .36f, .45f));
            rule.raycastTarget = false;
        }
    }
}
