using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using ShatteredSail.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Editor
{
    /// <summary>Disposable editor-only views. Never invokes transaction or save callbacks.</summary>
    public static class WoodFrameExtraVariants
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        public static bool TryArrange(GameObject host, string variant)
        {
            var controller = host.GetComponent<CoreGameplayDemoController>();
            if (controller == null) return false;
            if (Application.isPlaying) throw new InvalidOperationException("Capture fixtures require a stopped editor.");
            switch (variant)
            {
                case "ComponentResale": ArrangeResale(controller, false); break;
                case "ComponentSaleConfirm": ArrangeResale(controller, true); break;
                case "Crew": ArrangeCrew(controller); break;
                case "TreasureSeed": ArrangeTreasureSeed(controller); break;
                case "BattleDeck":
                    Call(controller, "OpenBattleDeckOverview");
                    Require(controller, "battleDeckOverlay");
                    break;
                case "PortSettings":
                    Call(controller, "OpenPortSettings");
                    Require(controller, "portSettingsOverlay");
                    break;
                case "SettlementChest":
                    var sequence = host.GetComponentInChildren<SettlementChestSequenceController>(true);
                    if (sequence == null) throw new InvalidOperationException("Settlement preview did not build its chest sequence.");
                    sequence.SetElapsed(sequence.JackpotStart >= 0 ? sequence.JackpotStart + 2.5f : 2.3f);
                    break;
                case "SchoolUnlock": ArrangeSchool(controller); break;
                case "SaveRecovery":
                    // The failure view disables its controller. Keep this disposable preview's
                    // font/resources alive through OnDisable; no scene is ever saved.
                    Set(controller, "editorPreviewPersistGeneratedUi", true);
                    Call(controller, "BlockAfterProfileSaveFailure", "截图展示：角色档案保存失败示例（未执行保存）。");
                    Require(controller, "profileSaveFailurePanel");
                    break;
                case "GameMaster": ArrangeGameMaster(controller); break;
                default: return false;
            }
            // Screenshots cannot trigger a production action even if accidentally clicked.
            foreach (var button in host.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            return true;
        }

        public static void BeforeRender(GameObject host, string variant, Camera camera, int pass)
        {
            if (variant != "SettlementChest") return;
            var sequence = host.GetComponentInChildren<SettlementChestSequenceController>(true);
            if (sequence != null) sequence.SetElapsed(sequence.JackpotStart >= 0 ? sequence.JackpotStart + 2.5f : 2.3f);
        }

        private static void ArrangeResale(CoreGameplayDemoController controller, bool confirmation)
        {
            var catalog = (ComponentArtCatalog)Get(controller, "componentArt");
            int serial = 0;
            var inventory = new OwnedShipComponentInventory(catalog, () => "wood-capture-spare-" + serial++);
            foreach (var entry in catalog.Entries.Take(7)) inventory.Grant(entry);
            Set(controller, "componentInventory", inventory);
            Set(controller, "componentResaleOpen", true);
            Set(controller, "componentResalePage", 0);
            Set(controller, "pendingComponentSaleId", confirmation ? inventory.Items[0].InstanceId : null);
            var root = (RectTransform)Get(controller, "contentRoot");
            // Keep the facility backdrop/header and reproduce its exact parent bounds.
            foreach (Transform child in root)
                if (child.name == "Component Shop Panel") child.gameObject.SetActive(false);
            Set(controller, "selectedComponentResaleInstanceId", null);
            Call(controller, "BuildComponentShopFacility");
        }

        private static void ArrangeCrew(CoreGameplayDemoController controller)
        {
            // The isolated source adapter consults only this temporary AppDomain fixture.
            // The production recruitment registry and all PlayerPrefs remain untouched.
            const string fixtureKey = "WoodFrameCaptureCrewIds";
            object previous = AppDomain.CurrentDomain.GetData(fixtureKey);
            var ids = new HashSet<string>(Enumerable.Range(0, 6).Select(profession =>
                CrewNarrativeCatalog.ForPortAndProfession(controller.Session.CurrentPort, profession).Id),
                StringComparer.Ordinal);
            try
            {
                AppDomain.CurrentDomain.SetData(fixtureKey, ids);
                Set(controller, "departureCrewConfigurationOpen", true);
                Set(controller, "selectedDepartureOrdinaryCrew", -1);
                var setup = controller.GetComponentsInChildren<RectTransform>(true)
                    .First(rect => rect.name == "Voyage Setup");
                Call(controller, "BuildDepartureCrewConfiguration", setup);
                if (!controller.GetComponentsInChildren<Image>(true).Any(image =>
                    image.name.StartsWith("Departure Special Crew Portrait Frame ", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Crew fixture did not produce portrait frames; isolated recruitment hook is required.");
            }
            finally { AppDomain.CurrentDomain.SetData(fixtureKey, previous); }
        }

        private static void ArrangeTreasureSeed(CoreGameplayDemoController controller)
        {
            // Match the game's own decoded-map editor sample, without a grant or save action.
            const string seed = "TREASURE-PREVIEW-WEATHERED-01";
            const string id = "preview-treasure-map-01";
            controller.Session.RestoreTreasureProgress(new CoreTreasureProgress
            {
                pirateExperience = 100,
                fragments = 1,
                selectedMapId = id,
                maps = new[] { new CoreTreasureMapSave { id = id, seed = seed, rankId = "weathered" } },
                issuedSeeds = new[] { seed },
                completedSeeds = new string[0],
            });
            Call(controller, "OpenDepartureDockTreasureSelection");
        }

        private static void ArrangeSchool(CoreGameplayDemoController controller)
        {
            var root = (RectTransform)Get(controller, "legacyCanvasRoot");
            var overlay = (RectTransform)Call(controller, "CreateRect", "School Unlock Notice", root,
                Vector2.zero, Vector2.zero);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.SetAsLastSibling();
            var canvas = overlay.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 20010;
            overlay.gameObject.AddComponent<GraphicRaycaster>();
            var shade = overlay.gameObject.AddComponent<Image>();
            shade.color = new Color(.015f, .025f, .03f, .82f);
            shade.raycastTarget = true;
            Set(controller, "schoolUnlockNotice", overlay.gameObject);
            Set(controller, "schoolUnlockPage", null);
            var pending = (IList)Get(controller, "pendingSchoolUnlocks");
            pending.Clear();
            pending.Add(CoreSchoolUnlockCatalog.All.First());
            Set(controller, "schoolUnlockPageIndex", 0);
            Set(controller, "schoolUnlockPreviousLevel", 1);
            Set(controller, "schoolUnlockCurrentLevel", CoreSchoolUnlockCatalog.All.First().RequiredLevel);
            Call(controller, "BuildSchoolUnlockPage");
        }

        // The capture host adapts only the isolated editor preview entry guard.
        // This calls the current production builder; no GM action is invoked.
        private static void ArrangeGameMaster(CoreGameplayDemoController controller)
        {
            Call(controller, "OpenGameMaster");
            Require(controller, "gameMasterOverlay");
        }
        private static object Get(object target, string name)
        {
            var field = target.GetType().GetField(name, Flags);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            return field.GetValue(target);
        }
        private static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, Flags);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }
        private static void Require(object target, string name)
        {
            if (Get(target, name) == null) throw new InvalidOperationException("Capture variant did not create " + name);
        }
        private static object Call(object target, string name, params object[] args)
        {
            var method = target.GetType().GetMethods(Flags).FirstOrDefault(candidate => candidate.Name == name &&
                candidate.GetParameters().Length == args.Length && candidate.GetParameters().Select((parameter, index) =>
                    args[index] == null || parameter.ParameterType.IsInstanceOfType(args[index])).All(value => value));
            if (method == null) throw new MissingMethodException(target.GetType().Name, name + " / " + args.Length);
            return method.Invoke(target, args);
        }
    }
}
