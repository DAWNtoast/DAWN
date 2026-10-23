using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const string GameMasterShopRefreshKeyPrefix = "ShatteredSail.GameMaster.ShopRefresh.v1.";
        public const string GameMasterShopScopeKey = "ShatteredSail.GameMaster.ShopScope.v1";
        private const float GameMasterActionDelay = 0.4f;
        private RectTransform gameMasterOverlay;
        private CanvasGroup gameMasterPageGroup;
        private bool gameMasterPreviousInteractable;
        private bool gameMasterPreviousRaycasts;
        private GameObject gameMasterPreviousSelection;
        private InputField gameMasterGoldInput;
        private InputField gameMasterTreasureFragmentsInput;
        private Toggle gameMasterUnlockToggle;
        private Button gameMasterSetGoldButton;
        private Button gameMasterSetTreasureFragmentsButton;
        private Button gameMasterRefreshShopButton;
        private Button gameMasterRefreshMarketButton;
        private Button gameMasterUnlockComponentsButton;
        private Button gameMasterLevelTenButton;
        private Button gameMasterPortPreviousButton;
        private Button gameMasterPortNextButton;
        private Button gameMasterMovePortButton;
        private Text gameMasterTargetPortLabel;
        private int gameMasterTargetPortIndex;
        private Text gameMasterStatus;
        private string gameMasterPortAtOpen;
        private float gameMasterNextActionTime;
        private int gameMasterLastActionFrame = -1;
        private bool gameMasterPageChanged;

        public bool IsGameMasterOpen => gameMasterOverlay != null;

        public static string GameMasterShopRefreshKey(string portName)
        {
            return GameMasterShopRefreshKeyPrefix + (portName ?? string.Empty).Trim();
        }

        private static int ComponentShopRefreshRevision(string portName)
        {
            return Math.Max(0, PlayerPrefs.GetInt(GameMasterShopRefreshKey(portName), 0));
        }

        private static string ComponentShopRefreshScope()
        {
            return PlayerPrefs.GetString(GameMasterShopScopeKey, string.Empty);
        }

        private bool CanOpenGameMaster => Application.isPlaying && !editorPreviewBuilding && !profileSaveBlocked && !tutorialActive && pendingMarketPurchase == null &&
            schoolUnlockNotice == null && session != null && session.Phase == CoreDemoPhase.Port && !DeckBuilderNeedsMoreCards;

        private bool CanUseGameMaster => CanOpenGameMaster && gameMasterOverlay != null &&
            string.Equals(gameMasterPortAtOpen, session.CurrentPort, StringComparison.Ordinal) &&
            gameMasterUnlockToggle != null && gameMasterUnlockToggle.isOn;

        // A separate Unity callback keeps the test tools independent of audio/UI Update.
        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (gameMasterOverlay == null && gameMasterPageChanged && CanOpenGameMaster)
            {
                gameMasterPageChanged = false;
                Render();
            }
            if (gameMasterOverlay != null && (!CanOpenGameMaster ||
                !string.Equals(gameMasterPortAtOpen, session.CurrentPort, StringComparison.Ordinal)))
                CloseGameMaster();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame)
            {
                if (gameMasterOverlay != null) CloseGameMaster();
                else OpenGameMaster();
            }
            if (gameMasterOverlay != null) RefreshGameMasterControls();
        }

        private void OpenGameMaster()
        {
            if (BlockIncompleteDeckBuilderNavigation()) return;
            if (!CanOpenGameMaster || contentRoot == null || contentRoot.parent == null) return;
            if (gameMasterOverlay != null) return;
            CloseVoyageCargo(false);
            // Two independent modals must not each restore the other's input barrier.
            CloseBattleDeckOverview(false);
            if (portSettingsOverlay != null) ClosePortSettings();
            gameMasterPortAtOpen = session.CurrentPort;
            gameMasterPreviousSelection = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            gameMasterPageGroup = contentRoot.GetComponent<CanvasGroup>();
            if (gameMasterPageGroup == null) gameMasterPageGroup = contentRoot.gameObject.AddComponent<CanvasGroup>();
            gameMasterPreviousInteractable = gameMasterPageGroup.interactable;
            gameMasterPreviousRaycasts = gameMasterPageGroup.blocksRaycasts;
            gameMasterPageGroup.interactable = false;
            gameMasterPageGroup.blocksRaycasts = false;

            gameMasterOverlay = CreateStretchPanel("Game Master Overlay", contentRoot.parent, new Color(0.015f, 0.025f, 0.04f, 0.9f));
            gameMasterOverlay.SetAsLastSibling();
            gameMasterOverlay.gameObject.AddComponent<PortSettingsBackgroundFocusHandler>().OnBackgroundPressed = FocusGameMaster;
            RectTransform sheet = CreateFixedPanel("Game Master Sheet", gameMasterOverlay, new Vector2(1040f, 980f), Vector2.zero, Panel);
            Text title = CreateText("Game Master Title", sheet, new Vector2(920f, 52f), new Vector2(0f, 425f), 32, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            title.text = "GAME MASTER · 测试工具";
            Text explanation = CreateText("Game Master Scope", sheet, new Vector2(900f, 56f), new Vector2(0f, 367f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            explanation.text = "仅港口可用 · 修改立即保存至当前存档\nF10 / Esc 关闭 · 每次打开须重新启用";

            RectTransform unlock = CreateRect("Game Master Unlock", sheet, new Vector2(900f, 48f), new Vector2(0f, 309f));
            Image hit = unlock.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            gameMasterUnlockToggle = unlock.gameObject.AddComponent<Toggle>();
            Image box = CreateImage("Game Master Unlock Box", unlock, new Vector2(34f, 34f), new Vector2(-423f, 0f), PanelRaised);
            Image check = CreateImage("Game Master Unlock Check", unlock, new Vector2(22f, 22f), new Vector2(-423f, 0f), Gold);
            check.raycastTarget = false;
            gameMasterUnlockToggle.targetGraphic = box;
            gameMasterUnlockToggle.graphic = check;
            gameMasterUnlockToggle.SetIsOnWithoutNotify(false);
            Text unlockLabel = CreateText("Game Master Unlock Label", unlock, new Vector2(800f, 44f), new Vector2(28f, 0f), 21, FontStyle.Bold, TextAnchor.MiddleLeft, Gold);
            unlockLabel.text = "启用本次 GM 测试修改（操作会写入存档）";
            unlockLabel.raycastTarget = false;
            gameMasterUnlockToggle.onValueChanged.AddListener(_ => RefreshGameMasterControls());

            Text goldLabel = CreateText("Game Master Gold Label", sheet, new Vector2(890f, 32f), new Vector2(0f, 253f), 19, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            goldLabel.text = "设置船长金币精确值 · 范围 0–1,000,000,000";
            gameMasterGoldInput = CreateInputField("Game Master Gold Input", sheet, new Vector2(570f, 58f), new Vector2(-160f, 202f), session.ProfileGold.ToString(CultureInfo.InvariantCulture));
            // Leave content validation off: filtering a pasted '-1' to '1' would
            // silently change the requested value. Reject the entire invalid input.
            gameMasterGoldInput.contentType = InputField.ContentType.Standard;
            gameMasterGoldInput.characterLimit = 0;
            ((Text)gameMasterGoldInput.placeholder).text = "输入非负金额，最多四位小数";
            gameMasterGoldInput.onValueChanged.AddListener(_ => RefreshGameMasterControls());
            gameMasterSetGoldButton = CreateButton("Game Master Set Gold", sheet, new Vector2(280f, 58f), new Vector2(305f, 202f), Teal, Ink, 21);
            gameMasterSetGoldButton.GetComponentInChildren<Text>().text = "设置金币并保存";
            gameMasterSetGoldButton.onClick.AddListener(ApplyGameMasterGold);

            Text fragmentsLabel = CreateText("Game Master Treasure Fragments Label", sheet, new Vector2(890f, 32f), new Vector2(0f, 145f), 19, FontStyle.Bold, TextAnchor.MiddleLeft, Ink);
            fragmentsLabel.text = "设置未鉴定藏宝图碎片数量 · 范围 0–2,147,483,647";
            gameMasterTreasureFragmentsInput = CreateInputField("Game Master Treasure Fragments Input", sheet, new Vector2(570f, 58f), new Vector2(-160f, 94f), session.TreasureFragments.ToString(CultureInfo.InvariantCulture));
            gameMasterTreasureFragmentsInput.contentType = InputField.ContentType.Standard;
            gameMasterTreasureFragmentsInput.characterLimit = 0;
            ((Text)gameMasterTreasureFragmentsInput.placeholder).text = "输入半角非负整数，0 可清空未鉴定碎片";
            gameMasterTreasureFragmentsInput.onValueChanged.AddListener(_ => RefreshGameMasterControls());
            gameMasterSetTreasureFragmentsButton = CreateButton("Game Master Set Treasure Fragments", sheet, new Vector2(280f, 58f), new Vector2(305f, 94f), Teal, Ink, 21);
            gameMasterSetTreasureFragmentsButton.GetComponentInChildren<Text>().text = "设置碎片并保存";
            gameMasterSetTreasureFragmentsButton.onClick.AddListener(ApplyGameMasterTreasureFragments);

            gameMasterRefreshShopButton = CreateButton("Game Master Refresh Shop", sheet, new Vector2(435f, 58f), new Vector2(-228f, 16f), PanelRaised, Gold, 19);
            gameMasterRefreshShopButton.GetComponentInChildren<Text>().text = "刷新本港部件商店";
            gameMasterRefreshShopButton.onClick.AddListener(ApplyGameMasterShopRefresh);
            gameMasterRefreshMarketButton = CreateButton("Game Master Refresh Market", sheet, new Vector2(435f, 58f), new Vector2(228f, 16f), PanelRaised, Gold, 19);
            gameMasterRefreshMarketButton.GetComponentInChildren<Text>().text = "补满本港市场 · 重置本月额度";
            gameMasterRefreshMarketButton.onClick.AddListener(ApplyGameMasterMarketRefresh);
            BuildGameMasterProgressionControls(sheet);
            Text stockNote = CreateText("Game Master Stock Note", sheet, new Vector2(900f, 76f), new Vector2(0f, -253f), 17, FontStyle.Normal, TextAnchor.MiddleCenter, MutedInk);
            stockNote.text = "全部部件：每种补齐一件，存入部件库；已装备部件保持原位。\n转港：当前船只一同移动，返回港口总览并清空采购草稿，不推进日期。\n市场刷新仅恢复本港本月库存与额度，沿用本月价格。";
            gameMasterStatus = CreateText("Game Master Status", sheet, new Vector2(920f, 76f), new Vector2(0f, -347f), 18, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            gameMasterStatus.text = GameMasterCurrentState();
            Button close = CreateButton("Close Game Master", sheet, new Vector2(420f, 48f), new Vector2(0f, -427f), PanelRaised, Ink, 20);
            close.GetComponentInChildren<Text>().text = "关闭测试工具 · F10 / Esc";
            close.onClick.AddListener(CloseGameMaster);
            Selectable[] controls = { gameMasterUnlockToggle, gameMasterGoldInput, gameMasterSetGoldButton,
                gameMasterTreasureFragmentsInput, gameMasterSetTreasureFragmentsButton,
                gameMasterRefreshShopButton, gameMasterRefreshMarketButton, gameMasterUnlockComponentsButton,
                gameMasterLevelTenButton, gameMasterPortPreviousButton, gameMasterPortNextButton, gameMasterMovePortButton, close };
            for (int index = 0; index < controls.Length; index++)
            {
                Navigation navigation = controls[index].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = controls[(index + controls.Length - 1) % controls.Length];
                navigation.selectOnDown = controls[(index + 1) % controls.Length];
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                controls[index].navigation = navigation;
                controls[index].gameObject.AddComponent<PortSettingsCancelHandler>().OnCancelRequested = CloseGameMaster;
            }
            RefreshGameMasterControls();
            FocusGameMaster();
        }

        private string GameMasterCurrentState()
        {
            return session.CurrentPort + " · 当前金币 " + PortMarketCatalog.FormatPrice(session.ProfileGold) +
                " · GM 库存批次 " + ComponentShopRefreshRevision(session.CurrentPort) +
                "\n未鉴定藏宝图碎片 " + session.TreasureFragments + " · 海盗等级 " + session.PirateLevel;
        }

        private void FocusGameMaster()
        {
            if (gameMasterOverlay == null || EventSystem.current == null) return;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(gameMasterOverlay)) return;
            EventSystem.current.SetSelectedGameObject(gameMasterUnlockToggle.gameObject);
        }

        private void RefreshGameMasterControls()
        {
            bool ready = CanUseGameMaster && Time.unscaledTime >= gameMasterNextActionTime &&
                Time.frameCount != gameMasterLastActionFrame;
            if (gameMasterGoldInput != null) gameMasterGoldInput.interactable = CanUseGameMaster;
            if (gameMasterSetGoldButton != null) gameMasterSetGoldButton.interactable = ready &&
                CoreGameMasterTools.TryParseProfileGold(gameMasterGoldInput.text, out _);
            if (gameMasterTreasureFragmentsInput != null) gameMasterTreasureFragmentsInput.interactable = CanUseGameMaster && !treasureProgressReadFailed;
            if (gameMasterSetTreasureFragmentsButton != null) gameMasterSetTreasureFragmentsButton.interactable = ready &&
                !treasureProgressReadFailed && CoreGameMasterTools.TryParseTreasureFragments(gameMasterTreasureFragmentsInput.text, out _);
            if (gameMasterRefreshShopButton != null) gameMasterRefreshShopButton.interactable = ready &&
                ComponentShopRefreshRevision(session.CurrentPort) < int.MaxValue;
            if (gameMasterRefreshMarketButton != null) gameMasterRefreshMarketButton.interactable = ready;
            if (gameMasterUnlockComponentsButton != null) gameMasterUnlockComponentsButton.interactable = ready &&
                !componentInventoryReadFailed && componentInventory != null && componentArt != null;
            if (gameMasterLevelTenButton != null) gameMasterLevelTenButton.interactable = ready && !treasureProgressReadFailed;
            if (gameMasterPortPreviousButton != null) gameMasterPortPreviousButton.interactable = CanUseGameMaster;
            if (gameMasterPortNextButton != null) gameMasterPortNextButton.interactable = CanUseGameMaster;
            if (gameMasterMovePortButton != null) gameMasterMovePortButton.interactable = ready &&
                GameMasterTargetPort != null && GameMasterTargetPort != session.CurrentPort;
            if (gameMasterTargetPortLabel != null) gameMasterTargetPortLabel.text = GameMasterTargetPort ?? "请选择港口";
        }

        private bool BeginGameMasterAction()
        {
            if (!CanUseGameMaster || Time.unscaledTime < gameMasterNextActionTime ||
                Time.frameCount == gameMasterLastActionFrame) return false;
            gameMasterLastActionFrame = Time.frameCount;
            gameMasterNextActionTime = Time.unscaledTime + GameMasterActionDelay;
            return true;
        }

        private string GameMasterTargetPort => gameMasterTargetPortIndex >= 0 &&
            gameMasterTargetPortIndex < CoreVoyageSession.DestinationPorts.Count
                ? CoreVoyageSession.DestinationPorts[gameMasterTargetPortIndex] : null;

        private void BuildGameMasterProgressionControls(RectTransform sheet)
        {
            gameMasterUnlockComponentsButton = CreateButton("Game Master Unlock Components", sheet,
                new Vector2(435f, 58f), new Vector2(-228f, -62f), Teal, Ink, 21);
            gameMasterUnlockComponentsButton.GetComponentInChildren<Text>().text = "解锁全部部件";
            gameMasterUnlockComponentsButton.onClick.AddListener(ApplyGameMasterUnlockComponents);
            gameMasterLevelTenButton = CreateButton("Game Master Level Ten", sheet,
                new Vector2(435f, 58f), new Vector2(228f, -62f), Teal, Ink, 21);
            gameMasterLevelTenButton.GetComponentInChildren<Text>().text = "海盗等级升到 10 级";
            gameMasterLevelTenButton.onClick.AddListener(ApplyGameMasterLevelTen);

            CreateText("Game Master Port Label", sheet, new Vector2(890f, 32f), new Vector2(0f, -123f),
                19, FontStyle.Bold, TextAnchor.MiddleLeft, Ink).text = "移动到指定港口 · 点击左右箭头选择";
            gameMasterTargetPortIndex = 0;
            for (int index = 0; index < CoreVoyageSession.DestinationPorts.Count; index++)
                if (CoreVoyageSession.DestinationPorts[index] == session.CurrentPort) gameMasterTargetPortIndex = index;
            gameMasterPortPreviousButton = CreateButton("Game Master Previous Port", sheet,
                new Vector2(78f, 58f), new Vector2(-406f, -174f), PanelRaised, Gold, 24);
            gameMasterPortPreviousButton.GetComponentInChildren<Text>().text = "‹";
            gameMasterPortPreviousButton.onClick.AddListener(() => SelectGameMasterPort(-1));
            gameMasterTargetPortLabel = CreateText("Game Master Target Port", sheet, new Vector2(390f, 58f),
                new Vector2(-160f, -174f), 23, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            gameMasterPortNextButton = CreateButton("Game Master Next Port", sheet,
                new Vector2(78f, 58f), new Vector2(86f, -174f), PanelRaised, Gold, 24);
            gameMasterPortNextButton.GetComponentInChildren<Text>().text = "›";
            gameMasterPortNextButton.onClick.AddListener(() => SelectGameMasterPort(1));
            gameMasterMovePortButton = CreateButton("Game Master Move Port", sheet,
                new Vector2(280f, 58f), new Vector2(305f, -174f), Teal, Ink, 21);
            gameMasterMovePortButton.GetComponentInChildren<Text>().text = "移动到所选港口";
            gameMasterMovePortButton.onClick.AddListener(ApplyGameMasterMovePort);
        }

        private void SelectGameMasterPort(int direction)
        {
            if (!CanUseGameMaster) return;
            int count = CoreVoyageSession.DestinationPorts.Count;
            gameMasterTargetPortIndex = (gameMasterTargetPortIndex + direction + count) % count;
            RefreshGameMasterControls();
        }

        private void ApplyGameMasterUnlockComponents()
        {
            if (!CanUseGameMaster || componentInventoryReadFailed || componentInventory == null || componentArt == null ||
                !BeginGameMasterAction()) return;
            var owned = new HashSet<string>(OwnedComponents.Select(item => item.ComponentId), StringComparer.Ordinal);
            var cards = new HashSet<string>(StringComparer.Ordinal);
            int added = 0;
            foreach (ComponentArtEntry entry in componentArt.ExpandedEntries)
            {
                if (owned.Add(entry.Id) && componentInventory.Grant(entry) != null) added++;
                CoreSpecialCardDefinition card = CoreSpecialCardCatalog.ForComponent(entry.Id);
                if (card != null) cards.Add(card.Card.Id);
            }
            session.AcquireSpecialCards(cards.ToArray());
            // Record the newly owned sources without saving unrelated card XP or crew drafts.
            cards.UnionWith(PlayerPrefs.GetString(AcquiredSpecialCardsKey, string.Empty)
                .Split(';').Where(id => !string.IsNullOrEmpty(id)));
            PlayerPrefs.SetString(AcquiredSpecialCardsKey, string.Join(";", cards.OrderBy(id => id, StringComparer.Ordinal)));
            SaveComponentInventory();
            PlayerPrefs.Save();
            gameMasterPageChanged = true;
            gameMasterStatus.text = "已解锁全部 " + owned.Count + " 种部件，本次补入 " + added + " 件并保存。\n关闭工具后可在皇家船坞装备。";
            RefreshGameMasterControls();
        }

        private void ApplyGameMasterLevelTen()
        {
            if (!CanUseGameMaster) return;
            if (!TryReadGameMasterTreasureProgress(out CoreTreasureProgress progress))
            {
                gameMasterStatus.text = "藏宝档案无法读取，原始档案已保留；请修复后重新载入。";
                RefreshGameMasterControls();
                return;
            }
            if (!BeginGameMasterAction() || !session.TryGameMasterRaisePirateLevelToTen()) return;
            progress.pirateExperience = Math.Max(progress.pirateExperience, session.PirateExperience);
            PlayerPrefs.SetString(TreasureProgressKey, JsonUtility.ToJson(progress));
            PlayerPrefs.Save();
            gameMasterPageChanged = true;
            gameMasterStatus.text = "海盗等级已升到 10 级并保存。" + GameMasterCurrentState();
            UpdatePortHeaderReadouts(true);
            RefreshGameMasterControls();
        }

        private void ApplyGameMasterMovePort()
        {
            if (!CanUseGameMaster || GameMasterTargetPort == null || GameMasterTargetPort == session.CurrentPort) return;
            if (!TryReadGameMasterShipPorts(out CoreShipPortProgress progress))
            {
                gameMasterStatus.text = ShipPortReadFailureNotice;
                return;
            }
            string target = GameMasterTargetPort;
            if (!BeginGameMasterAction() || !session.TryGameMasterMoveToPort(target)) return;
            var locations = new List<CoreShipPortLocation>(progress.ships);
            CoreShipPortLocation flagship = locations.Find(item => item.shipIndex == session.SelectedShipIndex);
            if (flagship == null)
            {
                flagship = new CoreShipPortLocation { shipIndex = session.SelectedShipIndex };
                locations.Add(flagship);
            }
            flagship.port = target;
            progress.ships = locations.ToArray();
            PlayerPrefs.SetString(ShipPortProgressKey, JsonUtility.ToJson(progress));
            PlayerPrefs.SetString(CurrentPortKey, target);
            PlayerPrefs.Save();
            pendingSupplyOrder.Clear();
            ResetCachedFacilityNavigation();
            BeginMarketPurchaseView();
            activePortFacility = -1;
            portNotice = "GM：已携当前船只移动到" + target + "，位置已保存。";
            gameMasterPageChanged = true;
            CloseGameMaster();
        }

        private bool TryReadGameMasterShipPorts(out CoreShipPortProgress progress)
        {
            progress = null;
            if (!PlayerPrefs.HasKey(ShipPortProgressKey))
            {
                progress = session.CaptureShipPortProgress();
                return true;
            }
            try
            {
                progress = JsonUtility.FromJson<CoreShipPortProgress>(PlayerPrefs.GetString(ShipPortProgressKey));
            }
            catch (ArgumentException) { }
            if (progress == null || progress.version != 1 || progress.ships == null ||
                progress.ships.Length > CoreVoyageSession.Ships.Count) return false;
            var seen = new HashSet<int>();
            return progress.ships.All(item => item != null && item.shipIndex >= 0 &&
                item.shipIndex < CoreVoyageSession.Ships.Count && seen.Add(item.shipIndex) &&
                CoreVoyageSession.DestinationPorts.Contains(item.port));
        }

        private void ApplyGameMasterGold()
        {
            if (!CanUseGameMaster) return;
            if (!CoreGameMasterTools.TryParseProfileGold(gameMasterGoldInput.text, out decimal gold))
            {
                gameMasterStatus.text = "无效金额：请输入 0–1,000,000,000 的半角非负金额，最多四位小数，不含空格。";
                return;
            }
            if (!BeginGameMasterAction() || !session.TryGameMasterSetProfileGold(gold)) return;
            // Write only the field this command owns. Saving an unrelated gameplay
            // draft here could overwrite progression that the tool did not change.
            ProfileGoldPersistence.Save(gold);
            PlayerPrefs.Save();
            gameMasterPageChanged = true;
            gameMasterStatus.text = "已保存。" + GameMasterCurrentState();
            UpdatePortHeaderReadouts(true);
            RefreshGameMasterControls();
        }

        private void ApplyGameMasterTreasureFragments()
        {
            if (!CanUseGameMaster) return;
            if (!CoreGameMasterTools.TryParseTreasureFragments(gameMasterTreasureFragmentsInput.text, out int fragments))
            {
                gameMasterStatus.text = "无效数量：请输入 0–2,147,483,647 的半角非负整数，不含空格或小数。";
                return;
            }
            if (!TryReadGameMasterTreasureProgress(out CoreTreasureProgress progress))
            {
                gameMasterStatus.text = "藏宝档案无法读取，原始档案已保留；请修复后重新载入。";
                RefreshGameMasterControls();
                return;
            }
            // Preserve persisted map records, identification state and rare cargo;
            // this command owns only the unidentified fragment count.
            progress.fragments = fragments;
            string json = JsonUtility.ToJson(progress);
            if (!BeginGameMasterAction() || !session.TryGameMasterSetTreasureFragments(fragments)) return;
            PlayerPrefs.SetString(TreasureProgressKey, json);
            PlayerPrefs.Save();
            gameMasterPageChanged = true;
            gameMasterStatus.text = "碎片数量已保存，关闭工具后显示。" + GameMasterCurrentState();
            RefreshGameMasterControls();
        }

        private bool TryReadGameMasterTreasureProgress(out CoreTreasureProgress progress)
        {
            progress = null;
            if (treasureProgressReadFailed) return false;
            if (!PlayerPrefs.HasKey(TreasureProgressKey))
            {
                progress = session.CaptureTreasureProgress();
                return true;
            }
            try
            {
                progress = JsonUtility.FromJson<CoreTreasureProgress>(PlayerPrefs.GetString(TreasureProgressKey, string.Empty));
            }
            catch (ArgumentException)
            {
                // Keep the same unreadable-record protection as ordinary saves.
            }
            treasureProgressReadFailed = progress == null;
            return progress != null;
        }

        private void ApplyGameMasterShopRefresh()
        {
            if (!CanUseGameMaster) return;
            int previous = ComponentShopRefreshRevision(session.CurrentPort);
            if (previous == int.MaxValue)
            {
                gameMasterStatus.text = "库存批次已达上限，未修改存档。";
                return;
            }
            if (!BeginGameMasterAction()) return;
            // New games clear this namespace, so repeating revision one in a new
            // save cannot inherit the old save's GM purchase receipts.
            if (string.IsNullOrEmpty(ComponentShopRefreshScope()))
                PlayerPrefs.SetString(GameMasterShopScopeKey, Guid.NewGuid().ToString("N"));
            PlayerPrefs.SetInt(GameMasterShopRefreshKey(session.CurrentPort), previous + 1);
            PlayerPrefs.Save();
            ResetComponentShopView();
            gameMasterPageChanged = true;
            gameMasterStatus.text = "已生成新批次，关闭工具后显示。" + GameMasterCurrentState();
            RefreshGameMasterControls();
        }

        private void ApplyGameMasterMarketRefresh()
        {
            if (!CanUseGameMaster || !BeginGameMasterAction()) return;
            foreach (PortMarketItem item in PortMarketCatalog.AllItems)
                PlayerPrefs.DeleteKey(MarketPurchaseKey(session.CurrentPort, MarketNow, item));
            PlayerPrefs.Save();
            gameMasterPageChanged = true;
            gameMasterStatus.text = "本港本月市场库存与采购额度已恢复；价格、金币及现有货物未变。";
            RefreshGameMasterControls();
        }

        private void CloseGameMaster()
        {
            if (gameMasterOverlay == null) return;
            bool changedPage = gameMasterPageChanged && CanOpenGameMaster;
            bool refreshPage = changedPage && isActiveAndEnabled;
            // OnDisable also closes the modal during scene teardown. Defer any
            // redraw until a later enabled frame; never build UI while unloading.
            gameMasterPageChanged = changedPage && !isActiveAndEnabled;
            if (gameMasterPageGroup != null)
            {
                gameMasterPageGroup.interactable = gameMasterPreviousInteractable;
                gameMasterPageGroup.blocksRaycasts = gameMasterPreviousRaycasts;
            }
            // Retain the restored CanvasGroup so same-frame reopen never reuses a
            // component queued for destruction, leaving the new modal unprotected.
            gameMasterPageGroup = null;
            GameObject overlay = gameMasterOverlay.gameObject;
            overlay.SetActive(false);
            gameMasterOverlay = null;
            if (Application.isPlaying) Destroy(overlay);
            else DestroyImmediate(overlay);
            gameMasterGoldInput = null;
            gameMasterTreasureFragmentsInput = null;
            gameMasterUnlockToggle = null;
            gameMasterSetGoldButton = null;
            gameMasterSetTreasureFragmentsButton = null;
            gameMasterRefreshShopButton = null;
            gameMasterRefreshMarketButton = null;
            gameMasterUnlockComponentsButton = null;
            gameMasterLevelTenButton = null;
            gameMasterPortPreviousButton = null;
            gameMasterPortNextButton = null;
            gameMasterMovePortButton = null;
            gameMasterTargetPortLabel = null;
            gameMasterStatus = null;
            gameMasterPortAtOpen = null;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameMasterPreviousSelection != null &&
                    gameMasterPreviousSelection.activeInHierarchy ? gameMasterPreviousSelection : null);
            gameMasterPreviousSelection = null;
            if (refreshPage) Render();
        }
    }
}

