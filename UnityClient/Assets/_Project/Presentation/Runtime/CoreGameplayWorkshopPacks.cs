using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private string selectedWorkshopPackId = "neutral";
        private string workshopOpenedPackId;
        private int[] workshopPackResultCards;
        private CoreVoyageSession workshopPackViewSession;
        private string workshopPackViewPort;
        private bool workshopPackOpening;
        private bool workshopPackRevealPending;
        private string workshopPackNotice = string.Empty;

        private void ResetWorkshopPackPresentation()
        {
            selectedWorkshopPackId = "neutral";
            workshopOpenedPackId = null;
            workshopPackResultCards = null;
            workshopPackViewSession = null;
            workshopPackViewPort = null;
            workshopPackOpening = false;
            workshopPackRevealPending = false;
            workshopPackNotice = string.Empty;
        }

        private void BuildWorkshopPacksPage()
        {
            if (!ReferenceEquals(workshopPackViewSession, session) || workshopPackViewPort != session.CurrentPort)
            {
                ResetWorkshopPackPresentation();
                workshopPackViewSession = session;
                workshopPackViewPort = session.CurrentPort;
            }
            if (CoreCardPackCatalog.Find(selectedWorkshopPackId) == null) selectedWorkshopPackId = "neutral";
            RectTransform root = CreateWorkshopWindowPanel("Workshop Packs Panel");
            RectTransform library = CreateWorkshopPreviewPaper("Workshop Packs Library", root,
                new Vector2(536f, 900f), new Vector2(-680f, 0f));
            CreateText("Workshop Packs Library Heading", library, new Vector2(464f, 54f), new Vector2(0f, 397f),
                34, FontStyle.Bold, TextAnchor.MiddleLeft, WorkshopPaperInk).text = "卡包收纳";
            long total = 0;
            foreach (CoreCardPackDefinition pack in CoreCardPackCatalog.All) total += session.GetCardPackCount(pack.Id);
            CreateImage("Workshop Packs Library Divider", library, new Vector2(464f, 2f),
                new Vector2(0f, 340f), new Color(.43f, .29f, .15f, .28f)).raycastTarget = false;
            for (int i = 0; i < CoreCardPackCatalog.All.Count; i++)
                BuildWorkshopPackListRow(library, CoreCardPackCatalog.All[i], new Vector2(0f, 270f - i * 100f));

            RectTransform table = CreateWorkshopPackTable(root);
            if (workshopPackResultCards != null && workshopPackResultCards.Length == CoreCardPackCatalog.CardsPerPack)
                BuildWorkshopPackResults(table);
            else BuildWorkshopPackSelection(table, CoreCardPackCatalog.Find(selectedWorkshopPackId), total);
        }

        private void BuildWorkshopPackListRow(RectTransform library, CoreCardPackDefinition pack, Vector2 position)
        {
            string id = pack.Id;
            Button row = CreateButton("Workshop Pack Select " + id, library, new Vector2(464f, 86f),
                position, PanelRaised, Ink, 25);
            row.GetComponentInChildren<Text>().text = string.Empty;
            WoodButtonSkin.SetSelected(row, selectedWorkshopPackId == id);
            CreateImage("Workshop Pack School Stripe " + id, row.transform, new Vector2(5f, 56f),
                new Vector2(-224f, 0f), WorkshopPackAccent(id)).raycastTarget = false;
            RawImage icon = CreateRawImage("Workshop Pack List Art " + id, row.transform, new Vector2(74f, 68f),
                new Vector2(-179f, 0f), cardWorkshopArt?.CardPack(id), new Rect(0f, 0f, 1f, 1f));
            icon.raycastTarget = false;
            FitCrewPortrait(icon, new Vector2(74f, 68f));
            Text name = CreateText("Workshop Pack Name " + id, row.transform, new Vector2(220f, 34f),
                new Vector2(-20f, 0f), 28, FontStyle.Bold, TextAnchor.MiddleLeft, WoodButtonSkin.Ink);
            name.text = pack.Name;
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 21;
            name.resizeTextMaxSize = 28;
            Text count = CreateText("Workshop Pack Count " + id, row.transform, new Vector2(112f, 46f),
                new Vector2(164f, 0f), 28, FontStyle.Bold, TextAnchor.MiddleRight, Gold);
            count.text = session.GetCardPackCount(id).ToString();
            count.resizeTextForBestFit = true;
            count.resizeTextMinSize = 14;
            count.resizeTextMaxSize = 28;
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            row.onClick.AddListener(() =>
            {
                if (!CanUseWorkshopPackButton(row, owner, port)) return;
                selectedWorkshopPackId = id;
                workshopOpenedPackId = null;
                workshopPackResultCards = null;
                workshopPackNotice = string.Empty;
                Render();
            });
        }

        private RectTransform CreateWorkshopPackTable(RectTransform root)
        {
            Vector2 tableSize = new Vector2(1336f, 900f);
            RectTransform surface = CreateWorkshopRegion("Workshop Packs Opening Table Surface", root,
                tableSize, new Vector2(280f, 0f));
            Image wood = CreateImage("Workshop Packs Opening Table Wood", surface, tableSize,
                Vector2.zero, new Color(.16f, .10f, .06f, .88f));
            wood.rectTransform.anchorMin = Vector2.zero;
            wood.rectTransform.anchorMax = Vector2.one;
            wood.rectTransform.offsetMin = wood.rectTransform.offsetMax = Vector2.zero;
            NauticalSurfaceSkin.ApplyPanel(wood, wood.color);
            wood.color = new Color(.50f, .43f, .34f, .90f);
            wood.raycastTarget = false;
            RectTransform table = CreateRect("Workshop Packs Opening Table", surface, tableSize, Vector2.zero);
            surface.GetComponent<WorkshopResponsiveSurface>().Configure(tableSize, table);
            return table;
        }

        private void BuildWorkshopPackSelection(RectTransform table, CoreCardPackDefinition pack, long total)
        {
            Color tableInk = new Color(.97f, .92f, .80f);
            Color tableMuted = new Color(.80f, .72f, .59f);
            int count = session.GetCardPackCount(pack.Id);
            int poolCount = session.GetCardPackPool(pack.Id).Count;
            CreateText("Workshop Packs Heading", table, new Vector2(1160f, 62f), new Vector2(0f, 392f),
                38, FontStyle.Bold, TextAnchor.MiddleCenter, tableInk).text = "打开卡包 · " + pack.Name;
            CreateImage("Workshop Packs Divider", table, new Vector2(1176f, 2f),
                new Vector2(0f, 340f), new Color(.89f, .69f, .37f, .32f)).raycastTarget = false;

            RawImage packArt = CreateRawImage("Workshop Packs Selected Pack Art", table, new Vector2(620f, 500f),
                new Vector2(0f, 56f), cardWorkshopArt?.CardPack(pack.Id), new Rect(0f, 0f, 1f, 1f));
            packArt.raycastTarget = false;
            FitCrewPortrait(packArt, new Vector2(620f, 500f));

            CreateText("Workshop Packs Empty Title", table, new Vector2(1100f, 60f), new Vector2(0f, -260f),
                40, FontStyle.Bold, TextAnchor.MiddleCenter, tableInk).text = count > 0 ? "持有 " + count + " 包" :
                    total == 0 ? "暂无卡包" : "暂无" + pack.Name;
            if (workshopPackNotice.Length > 0)
                CreateText("Workshop Packs Empty Description", table, new Vector2(1100f, 40f), new Vector2(0f, -314f),
                    25, FontStyle.Normal, TextAnchor.MiddleCenter, tableMuted).text = workshopPackNotice;

            Button open = CreateButton("Workshop Packs Open Selected", table, new Vector2(450f, 66f),
                new Vector2(-248f, -388f), Gold, Ink, 28);
            open.GetComponentInChildren<Text>().text = count > 0 ? "打开 1 包" : "暂无可打开的卡包";
            open.interactable = count > 0 && poolCount > 0;
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            open.onClick.AddListener(() => OpenWorkshopCardPack(pack.Id, open, owner, port));
            BuildWorkshopPackCollectionButton(table, new Vector2(248f, -388f), new Vector2(450f, 66f));
        }

        private void BuildWorkshopPackCollectionButton(RectTransform parent, Vector2 position, Vector2 size)
        {
            Button collection = CreateButton("Workshop Packs View Collection", parent,
                size, position, Gold, Ink, 27);
            collection.GetComponentInChildren<Text>().text = "查看已有卡牌";
            CoreVoyageSession owner = session;
            string port = session.CurrentPort;
            collection.onClick.AddListener(() =>
            {
                if (!CanUseWorkshopPackButton(collection, owner, port)) return;
                SelectWorkshopPage(WorkshopPage.Collection);
            });
        }

        private bool CanUseWorkshopPackButton(Button button, CoreVoyageSession owner, string port) =>
            !workshopPackOpening && !profileSaveBlocked && !returnMenuSaving &&
            button != null && button.gameObject.activeInHierarchy && button.IsInteractable() &&
            ReferenceEquals(session, owner) && session != null && session.Phase == CoreDemoPhase.Port &&
            session.CurrentPort == port && activePortFacility == 4 && CurrentWorkshopPage == WorkshopPage.Packs;

        private void OpenWorkshopCardPack(string id, Button button, CoreVoyageSession owner, string port)
        {
            if (!CanUseWorkshopPackButton(button, owner, port) || selectedWorkshopPackId != id) return;
            button.interactable = false;
            workshopPackOpening = true;
            try
            {
                if (session.TryOpenCardPack(id, out int[] cards, out string reason))
                {
                    workshopOpenedPackId = id;
                    workshopPackResultCards = (int[])cards.Clone();
                    // Persist the atomic inventory/RNG/card-copy transaction before offering another pack.
                    SaveProgress();
                    workshopPackRevealPending = Application.isPlaying && !editorPreviewBuilding && !profileSaveBlocked;
                }
                else
                {
                    // A failed repeat returns to the selected pack with its reason,
                    // rather than presenting the previous rewards as another opening.
                    workshopOpenedPackId = null;
                    workshopPackResultCards = null;
                }
                workshopPackNotice = reason;
                portNotice = reason;
            }
            finally { workshopPackOpening = false; }
            try { Render(); }
            finally { workshopPackRevealPending = false; }
        }

        private static Color WorkshopPackAccent(string id)
        {
            switch (id)
            {
                case "fire": return new Color(.94f, .40f, .20f);
                case "arcane": return new Color(.68f, .48f, .92f);
                case "curse": return new Color(.46f, .74f, .42f);
                case "tide": return new Color(.31f, .70f, .82f);
                case "thunder": return new Color(.60f, .69f, 1f);
                case "blood": return new Color(.86f, .31f, .37f);
                default: return new Color(.85f, .72f, .48f);
            }
        }
    }
}
