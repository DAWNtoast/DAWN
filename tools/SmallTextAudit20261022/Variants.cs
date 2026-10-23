// Isolated Editor capture fixtures; no purchase/sale or save actions are invoked.
using System;
using System.Linq;
using System.Reflection;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    public static class SmallTextAuditVariants
    {
        public static void Arrange(GameObject host, string variant)
        {
            var controller = host.GetComponent<CoreGameplayDemoController>();
            if (controller == null) throw new InvalidOperationException("Expected CoreGameplayDemoController.");
            void ClickNamed(string name)
            {
                Button button = host.GetComponentsInChildren<Button>(false).SingleOrDefault(b => b.name == name);
                if (button == null) throw new MissingMemberException("Active button not found: " + name + "; available=" + string.Join(" | ", host.GetComponentsInChildren<Button>(false).Select(b => b.name)));
                button.onClick.Invoke();
            }
            void Invoke(string name)
            {
                MethodInfo method = typeof(CoreGameplayDemoController).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (method == null) throw new MissingMethodException(name);
                method.Invoke(controller, null);
            }
            switch (variant)
            {
                case "AuditDepartureCharts":
                    Toggle charts = host.GetComponentsInChildren<Toggle>(false).Single(t => t.name == "Use Chart Seed");
                    charts.isOn = true;
                    return;
                case "AuditDepartureTreasure":
                    ClickNamed("Departure Treasure Seed Picker");
                    return;
                case "AuditShipyardEquipment":
                    controller.ConfigureEditorShipyard(0, true, 2);
                    return;
                case "AuditFolioEmptyUnused":
                case "AuditFolioEmptyUsed":
                    controller.Session.RestoreTreasureProgress(new CoreTreasureProgress { pirateExperience = 1500 });
                    Invoke("Render");
                    ClickNamed("Treasure Open Folio");
                    ClickNamed(variant == "AuditFolioEmptyUsed" ? "Treasure Browse Used" : "Treasure Browse Unused");
                    return;
                case "AuditEnemyOverview":
                    var battle = controller.Session.Battle;
                    battle.Stage = NavalBattleStage.Boarding;
                    battle.Profile = new CoreBattleProfile(CoreBossKind.Route, CoreBattleArchetype.Balanced,
                        100, 10, 10, 5, 0, 20, 1, 1, boardingGuard: 5);
                    typeof(CoreBoardingEnemyState).GetProperty("Deck").SetValue(battle.BoardingEnemy, CoreBoardingEnemyCatalog.ForSchool("thunder"));
                    Invoke("Render");
                    Canvas.ForceUpdateCanvases();
                    Invoke("OpenBoardingEnemyOverview");
                    return;
                case "AuditResourceOverview":
                    typeof(CoreGameplayDemoController).GetMethod("OpenGuidedBattleInformation", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(controller, new object[] { "resources" });
                    return;
                case "AuditNavalMovement":
                    controller.Session.Battle.Stage = NavalBattleStage.Naval;
                    SetProperty(controller.Session.Battle, "NavalActionPoints", 3);
                    Invoke("Render");
                    return;
                case "AuditEncounterOverflow":
                    // Use unchanged authored event prose, selected for longest narrative.
                    // The staging follows the existing EncounterJournalCapture fixture.
                    controller.ConfigureEditorFlowState(GameplayFlowPreviewState.TreasureClue);
                    var session = controller.Session;
                    var encounter = session.Map.Nodes.Where(n => n.Encounter != null).Select(n => n.Encounter)
                        .Concat(CoreTreasureHuntCatalog.CreateEncounters(CoreTreasureHuntCatalog.FindRank("weathered")))
                        .OrderByDescending(e => e.Description.Length).First();
                    var node = new CoreMapNode { Id = 991, Position = session.Position,
                        TerrainConstraint = EventTerrainConstraint.Any, Encounter = encounter };
                    session.Map.Nodes.Add(node);
                    SetField(controller, "editorPreviewBuilding", true);
                    try
                    {
                        SetProperty(session, "ActiveEncounter", encounter);
                        SetProperty(session, "Phase", CoreDemoPhase.Encounter);
                        SetField(session, "activeNode", node);
                        node.Discovered = true;
                        Invoke("Render");
                        Invoke("CloseTreasureStoryOverlay");
                    }
                    finally { SetField(controller, "editorPreviewBuilding", false); }
                    Debug.Log("SMALL_TEXT_AUTHORED_EVENT: " + encounter.Id + " / " + encounter.Description.Length + " characters");
                    return;
                default: throw new ArgumentException("Unknown isolated audit variant: " + variant);
            }
        }

        public static void BeforeRender(GameObject host, string variant, Camera camera, int pass)
        {
            if (variant == "AuditEncounterOverflow")
            {
                foreach (NavigationDashboardLayout layout in host.GetComponentsInChildren<NavigationDashboardLayout>(false))
                    layout.SendMessage("LateUpdate", SendMessageOptions.RequireReceiver);
                if (pass == 3)
                {
                    ScrollRect narrative = host.GetComponentsInChildren<ScrollRect>(false)
                        .Single(scroll => scroll.name == "Encounter Description Viewport");
                    if (!narrative.vertical) throw new InvalidOperationException("Authored narrative does not overflow at this resolution; no scrolling state captured.");
                }
            }
            if (variant != "AuditNavalMovement" || pass != 2) return;
            GameObject eventHost = new GameObject("Small Text Movement Capture Events", typeof(EventSystem));
            SceneManager.MoveGameObjectToScene(eventHost, host.scene);
            Transform ship = host.GetComponentsInChildren<Transform>(false).Single(item => item.name == "Player Ship Art");
            var pointer = new PointerEventData(eventHost.GetComponent<EventSystem>())
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, ship.position)
            };
            if (!ExecuteEvents.Execute(ship.gameObject, pointer, ExecuteEvents.pointerClickHandler))
                throw new InvalidOperationException("Player ship did not accept real movement selection click.");
            var movement = host.GetComponentInChildren<NavalTacticalMovementInput>();
            movement.RefreshNow();
            if (!movement.IsShipSelected) throw new InvalidOperationException("Ship selection did not activate movement planning.");
            foreach (BattleStatusRow row in host.GetComponentsInChildren<BattleStatusRow>(false)) row.RefreshVisibility();
            foreach (Transform child in host.GetComponentsInChildren<Canvas>(false).SelectMany(c => c.GetComponentsInChildren<Transform>(true)))
                child.gameObject.layer = 31;
        }

        private static void SetProperty(object target, string name, object value) =>
            target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public).GetSetMethod(true).Invoke(target, new[] { value });
        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
