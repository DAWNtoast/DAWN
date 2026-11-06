using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    // Facility values follow the navigation order, independently of regional page titles.
    public enum PortHeaderIconKind
    {
        Warehouse, Shipyard, Tavern, ComponentShop, CardWorkshop,
        TreasureMap, Investment, Departure, Market, Back, Cabin
    }

    /// <summary>Transparent destination and cabin symbols for the shared header.</summary>
    [DisallowMultipleComponent]
    public sealed class PortHeaderIconGraphic : RawImage
    {
        private static readonly string[] ResourceNames =
        {
            "warehouse", "shipyard", "tavern", "component_shop", "card_workshop",
            "treasure_map", "investment", "departure", "market", "back", "cabin"
        };
        [SerializeField] private PortHeaderIconKind kind;
        [SerializeField] private bool interactive = true;
        public PortHeaderIconKind Kind => kind;
        public const string ResourceFolder = "HeaderFacilityIcons/";

        public void Configure(PortHeaderIconKind value, bool interactive = true)
        {
            kind = value;
            this.interactive = interactive;
            texture = Resources.Load<Texture2D>(ResourceFolder + ResourceNames[(int)kind]);
            uvRect = new Rect(0f, 0f, 1f, 1f);
            color = Color.white;
            raycastTarget = interactive;
            SetAllDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Configure(kind, interactive);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if (texture == null) { mesh.Clear(); return; }
            base.OnPopulateMesh(mesh);
        }
    }
}
