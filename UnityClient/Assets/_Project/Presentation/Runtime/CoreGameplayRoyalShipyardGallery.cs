using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private Text BuildRoyalShipyardConsole(RectTransform panel, int index, out Button inspect)
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

            // The writing surface is below the texture's center because of the raised crest.
            const float contentCenterY = -21f;
            Text title = ShipyardText("Shipyard Ship Name " + index, plaque, new Vector2(260f, 48f),
                new Vector2(-420f, contentCenterY), 31, ivory, TextAnchor.MiddleLeft);
            GameTypography.Apply(title, 31, FontStyle.Bold);
            title.text = definition.Name;
            ShipyardText("Shipyard Gallery Price", plaque, new Vector2(150f, 30f),
                new Vector2(-212f, contentCenterY), 20, brass, TextAnchor.MiddleRight).text =
                "售价 " + CoreShipMarketCatalog.PurchaseCost(index) + " 金币";

            ShipyardConsoleRule(plaque, -117f, contentCenterY);
            ShipyardConsoleRule(plaque, 302f, contentCenterY);
            string[] captions = { "船体", "船速", "货舱" };
            string[] values = { definition.MaxHull.ToString("0"), definition.SpeedKnots.ToString("0.#") + " 节",
                definition.CargoCapacity.ToString("0") };
            for (int stat = 0; stat < captions.Length; stat++)
            {
                float x = -42f + stat * 130f;
                ShipyardText("Shipyard Gallery Stat Label " + stat, plaque, new Vector2(116f, 24f),
                    new Vector2(x, contentCenterY + 20f), 18, muted).text = captions[stat];
                ShipyardText("Shipyard Gallery Stat Value " + stat, plaque, new Vector2(116f, 40f),
                    new Vector2(x, contentCenterY - 18f), 30, ivory).text = values[stat];
            }
            inspect = CreateButton("Shipyard Inspect Vessel", plaque, new Vector2(252f, 64f),
                new Vector2(463f, contentCenterY), Gold, ivory, 24);
            Text inspectLabel = inspect.GetComponentInChildren<Text>();
            inspectLabel.text = "查看船只档案";
            return title;
        }

        private void ShipyardConsoleRule(Transform parent, float x, float centerY)
        {
            Image rule = CreateImage("Shipyard Console Divider", parent, new Vector2(1f, 94f),
                new Vector2(x, centerY), new Color(.76f, .61f, .36f, .45f));
            rule.raycastTarget = false;
        }
    }
}
