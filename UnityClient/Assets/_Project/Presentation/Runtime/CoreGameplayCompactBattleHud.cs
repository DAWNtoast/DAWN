using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void RefreshCompactBattleHud(RectTransform root, CoreBattleState battle)
        {
            // Retain legacy data bindings and observers, but present their data once.
            foreach (string name in new[] { "Player Resource HUD", "Enemy Resource HUD", "Enemy Intent HUD",
                "Battle School Status Panel", "Battle Curse Panel", "Battle Buff Panel", "Battle Ram Condition", "Battle Board Condition" })
            {
                Transform item = root.Find(name);
                if (item != null) item.gameObject.SetActive(false);
            }
            RefreshCompactBattleResources(root, battle);
            RefreshCompactBattlePile(root, battle, false);
            RefreshCompactBattlePile(root, battle, true);
            bool naval = battle.Stage != NavalBattleStage.Boarding;
            if (naval)
            {
                SetNavalCommandButton(root, "Battle Ram", NavalCommandButtonKind.Ram,
                    new Vector2(664.5f, -152f), new Vector2(129f, 78f));
                SetNavalCommandButton(root, "Battle Board", NavalCommandButtonKind.Board,
                    new Vector2(805.5f, -152f), new Vector2(129f, 78f));
                SetNavalCommandButton(root, "End Battle Turn", NavalCommandButtonKind.EndTurn,
                    new Vector2(735f, -311f), new Vector2(270f, 58f));
            }
            else SetCompactBattleButton(root, "End Battle Turn", new Vector2(735f, -303f), new Vector2(230f, 46f));
            RectTransform movement = root.Find("Naval movement controls") as RectTransform;
            if (movement != null)
            {
                movement.sizeDelta = new Vector2(270f, 46f);
                movement.anchoredPosition = new Vector2(735f, -233f);
                SetNavalCommandButton(movement, "Commit Naval Movement", NavalCommandButtonKind.Sail,
                    new Vector2(-30f, 0f), new Vector2(210f, 46f));
                SetNavalCommandButton(movement, "Clear Naval Movement", NavalCommandButtonKind.Clear,
                    new Vector2(105f, 0f), new Vector2(60f, 46f));
                Text clearLabel = movement.Find("Clear Naval Movement").GetComponentInChildren<Text>();
                if (clearLabel != null) clearLabel.text = "清空";
                RemoveCompactBattleHover(movement.Find("Commit Naval Movement").gameObject);
                RemoveCompactBattleHover(movement.Find("Clear Naval Movement").gameObject);
            }
            RectTransform retreat = root.Find("Emergency Battle Return") as RectTransform;
            if (retreat != null)
            {
                retreat.gameObject.SetActive(session.CanRequestEmergencyTow());
                SetCompactBattleButton(root, retreat.name, new Vector2(-770f, 340f), new Vector2(160f, 30f), 14);
            }
            if (battle.Stage == NavalBattleStage.Boarding)
            {
                foreach (Text label in root.GetComponentsInChildren<Text>(true))
                    if (label.name.EndsWith(" Formation Label", System.StringComparison.Ordinal)) label.gameObject.SetActive(false);
            }
            RefreshCompactEnemyIntent(root, battle);
        }

        private void RefreshCompactBattleResources(RectTransform root, CoreBattleState battle)
        {
            float iconSize = CompactBattleIconSize(root);
            RectTransform panel = root.Find("Battle Compact Resources") as RectTransform;
            if (panel == null) panel = CreateRect("Battle Compact Resources", root, new Vector2(260f, 120f), new Vector2(-735f, -250f));
            panel.sizeDelta = new Vector2(260f, 120f);
            panel.anchoredPosition = new Vector2(-735f, -250f);
            // Remove obsolete slots from a retained HUD before rebinding the two resource rows.
            foreach (string path in new[] { "Battle Resource Line 2/Resource Mana", "Battle Resource Line 2/Resource ThunderChain",
                "Battle Resource Line 2/Resource TidePower", "Battle Resource Line 2/Resource CurseMark", "Battle Resource Line 3" })
            {
                Transform obsolete = panel.Find(path);
                if (obsolete == null) continue;
                obsolete.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(obsolete.gameObject); else DestroyImmediate(obsolete.gameObject);
            }
            for (int line = 1; line <= 2; line++)
            {
                if (panel.Find("Battle Resource Line " + line) == null)
                    CreateRect("Battle Resource Line " + line, panel, new Vector2(260f, 54f), Vector2.zero);
                ((RectTransform)panel.Find("Battle Resource Line " + line)).anchoredPosition = new Vector2(0f, 90f - line * 60f);
            }
            RectTransform first = panel.Find("Battle Resource Line 1") as RectTransform;
            SetCompactResource(first, root, iconSize, "Energy", BattleCommandIconKind.Energy, "能量", battle.Energy + "/" + battle.MaxEnergy,
                "用于打出卡牌；每回合恢复至能量上限。", 0, 3, 84f, battle.Energy, limit: battle.MaxEnergy);
            int points = battle.Stage == NavalBattleStage.Boarding ? 0 : battle.NavalAwaitingHeadingSelection ? battle.NavalArrivalActionPoints : battle.NavalActionPoints;
            SetCompactResource(first, root, iconSize, "ActionPoint", BattleCommandIconKind.ActionPoint, "行动点", points.ToString(),
                battle.Stage == NavalBattleStage.Boarding ? "接舷战中无需航行行动点。" : battle.NavalAwaitingHeadingSelection
                    ? "抵达剩余行动点，可用于冲撞或接舷；选择到达朝向后清空。" : "用于海战航行、冲撞和接舷。", 1, 3, 84f, points);
            SetCompactResource(first, root, iconSize, "Mana", BattleCommandIconKind.Mana, "法力",
                battle.Schools.Mana + "/" + battle.Schools.ManaLimit,
                "秘术体系资源。己方回合开始时恢复至上限，回合中获得的法力可以超过上限，额外法力不保留到下回合。准备阶段获得的法力保留至首个海战回合。", 2, 3, 84f,
                current: battle.Schools.Mana, limit: battle.Schools.ManaLimit);
            RectTransform second = panel.Find("Battle Resource Line 2") as RectTransform;
            SetCompactResource(second, root, iconSize, "Powder", BattleCommandIconKind.Powder, "火药", session.Cargo.Powder.ToString(), "货舱火药库存，部分炮击与战术卡消耗。", 0, 4, current: session.Cargo.Powder);
            SetCompactResource(second, root, iconSize, "Cannonball", BattleCommandIconKind.Cannonball, "炮弹", session.Cargo.Cannonballs.ToString(), "货舱炮弹库存，炮击卡消耗。", 1, 4, current: session.Cargo.Cannonballs);
            SetCompactResource(second, root, iconSize, "Planks", BattleCommandIconKind.Planks, "木板", session.Cargo.Wood.ToString(), "货舱木板库存，用于修理船体。", 2, 4, current: session.Cargo.Wood);
            SetCompactResource(second, root, iconSize, "Medicine", BattleCommandIconKind.Medicine, "药品", session.Cargo.Medicine.ToString(), "货舱药品库存，治疗与接舷卡可能消耗。", 3, 4, current: session.Cargo.Medicine);
        }

        private static float CompactBattleIconSize(RectTransform root)
        {
            NavalTacticalBoardView board = root.GetComponentInChildren<NavalTacticalBoardView>();
            return BattleStatusRow.IconSizeForCell(board == null ? 0f : board.CellSizePixels);
        }

        private void SetCompactResource(RectTransform line, RectTransform root, float iconSize, string id, BattleCommandIconKind kind,
            string title, string value, string description, int index, int count, float step = 64f,
            long current = 0, long temporary = 0, long limit = -1, bool separateTemporary = false)
        {
            RectTransform slot = CompactBattleBadge(line, "Resource " + id, kind, value,
                new Vector2((index - (count - 1) * .5f) * step, 0f), new Vector2(step - 4f, 52f), iconSize);
            if (CompactBattleResourceHasTooltip(kind))
                ConfigureCompactBattleHover(slot.gameObject, root, title, "当前 " + value + "\n" + description);
            else
            {
                BattleStatusHover hover = slot.GetComponent<BattleStatusHover>();
                if (hover != null)
                {
                    // Retained HUDs may still own a popup; OnDisable dismisses it immediately.
                    hover.enabled = false;
                    if (Application.isPlaying) Destroy(hover); else DestroyImmediate(hover);
                }
            }
            BattleResourceChangeTracker tracker = slot.GetComponent<BattleResourceChangeTracker>();
            if (tracker == null) tracker = slot.gameObject.AddComponent<BattleResourceChangeTracker>();
            tracker.Observe(current, temporary, limit, interfaceFont, Application.isPlaying && !editorPreviewBuilding,
                separateTemporary, this);
        }

        private static bool CompactBattleResourceHasTooltip(BattleCommandIconKind kind)
        {
            switch (kind)
            {
                case BattleCommandIconKind.Energy:
                case BattleCommandIconKind.ActionPoint:
                case BattleCommandIconKind.Mana:
                case BattleCommandIconKind.Powder:
                case BattleCommandIconKind.Cannonball:
                case BattleCommandIconKind.Planks:
                case BattleCommandIconKind.Medicine:
                    return false;
                default:
                    return true;
            }
        }

        private void RefreshCompactBattlePile(RectTransform root, CoreBattleState battle, bool discard)
        {
            string name = discard ? "Battle Discard Pile" : "Battle Draw Pile";
            RectTransform pile = CompactBattleBadge(root, name, discard ? BattleCommandIconKind.DiscardPile : BattleCommandIconKind.DrawPile,
                (discard ? battle.GetDiscardPile(battle.Stage).Count : battle.GetDrawPile(battle.Stage).Count).ToString(),
                new Vector2(discard ? 785f : -785f, -387f), new Vector2(96f, 86f), 70f);
            ConfigureCompactBattlePileBack(pile);
            Text caption = pile.Find("Label")?.GetComponent<Text>();
            if (caption == null) caption = CreateText("Label", pile, new Vector2(100f, 24f), new Vector2(0f, -48f), 17, FontStyle.Bold, TextAnchor.MiddleCenter, Ink);
            caption.text = discard ? "弃牌堆" : "抽牌堆";
            caption.raycastTarget = false;
            if (discard && battle.Stage != NavalBattleStage.Boarding)
            {
                // The card stack belongs below the command bar, with its count
                // and caption alongside so the whole pile remains on screen.
                pile.anchoredPosition = new Vector2(797f, -394f);
                pile.sizeDelta = new Vector2(146f, 78f);
                ((RectTransform)pile.Find("Icon")).anchoredPosition = new Vector2(-40f, 0f);
                caption.rectTransform.sizeDelta = new Vector2(70f, 24f);
                caption.rectTransform.anchoredPosition = new Vector2(25f, -18f);
                caption.alignment = TextAnchor.MiddleLeft;
                caption.fontSize = 16;
                Text amount = pile.Find("Amount").GetComponent<Text>();
                amount.rectTransform.sizeDelta = new Vector2(70f, 30f);
                amount.rectTransform.anchoredPosition = new Vector2(25f, 15f);
                amount.alignment = TextAnchor.MiddleLeft;
            }
            Button button = pile.GetComponent<Button>();
            if (button == null)
            {
                button = pile.gameObject.AddComponent<Button>();
                button.targetGraphic = pile.GetComponent<Image>();
                button.onClick.AddListener(() => OpenBattlePile(discard));
            }
            BattleStatusHover hover = pile.GetComponent<BattleStatusHover>();
            if (hover != null)
            {
                hover.enabled = false;
                if (Application.isPlaying) Destroy(hover); else DestroyImmediate(hover);
            }
        }

        private void ConfigureCompactBattlePileBack(RectTransform pile)
        {
            RectTransform icon = (RectTransform)pile.Find("Icon");
            // Keep the shared badge and flight anchor, replacing only its visible artwork.
            icon.GetComponent<BattleCommandIconGraphic>().enabled = false;
            icon.anchoredPosition = new Vector2(-4f, 8f);
            if (icon.Find("Card Back") == null)
            {
                Vector2 size = CardFaceLayout.FrameSize * (48f / CardFaceLayout.FrameSize.x);
                for (int layer = 2; layer >= 0; layer--)
                {
                    RectTransform back = CreateRect(layer == 0 ? "Card Back" : "Stack Back " + layer,
                        icon, size, new Vector2(-2f * layer, 2f * layer));
                    BattleCardFlightBackGraphic graphic = back.gameObject.AddComponent<BattleCardFlightBackGraphic>();
                    graphic.Configure(layer == 0 ? Color.white : new Color(.72f, .76f, .8f));
                }
            }
            // Keep the authored card backs and flight anchor, with a small painted
            // direction emblem that distinguishes draw from discard at a glance.
            RectTransform direction = icon.Find("Pile Direction") as RectTransform;
            if (direction == null)
                direction = CreateRect("Pile Direction", icon, new Vector2(28f, 28f), new Vector2(17f, 19f));
            BattleCommandIconGraphic symbol = direction.GetComponent<BattleCommandIconGraphic>();
            if (symbol == null) symbol = direction.gameObject.AddComponent<BattleCommandIconGraphic>();
            symbol.Configure(icon.GetComponent<BattleCommandIconGraphic>().Kind);
        }

        private void RefreshCompactEnemyIntent(RectTransform root, CoreBattleState battle)
        {
            bool naval = battle.Stage != NavalBattleStage.Boarding;
            float rowHeight = naval ? 72f : 52f;
            RectTransform row = root.Find("Enemy Intent Icons") as RectTransform;
            if (row == null) row = CreateRect("Enemy Intent Icons", root, new Vector2(340f, rowHeight), Vector2.zero);
            row.sizeDelta = new Vector2(340f, rowHeight);
            BattleIntentAnchor anchor = row.GetComponent<BattleIntentAnchor>();
            if (anchor == null) anchor = row.gameObject.AddComponent<BattleIntentAnchor>();
            // Keep the next action clear of the enlarged hull at every heading.
            anchor.Configure(root.Find("Enemy Battle Hull Bar Track") as RectTransform,
                naval ? 175f : BoardingBattleLayout.PartySize.y + 96f, naval ? battle : null);
            IReadOnlyList<CoreBattleIntent> intents = CoreBattleIntentPresentation.Build(session);
            var current = new HashSet<string>();
            float iconSize = naval ? 64f : CompactBattleIconSize(root);
            float step = Mathf.Max(iconSize + (naval ? 16f : 8f), Mathf.Min(62f, 340f / Mathf.Max(1, intents.Count)));
            for (int i = 0; i < intents.Count; i++)
            {
                CoreBattleIntent intent = intents[i];
                string name = "Intent " + intent.Id;
                current.Add(name);
                RectTransform slot = CompactBattleBadge(row, name, CompactIntentIcon(intent.Kind, battle.Stage == NavalBattleStage.Boarding), intent.Value,
                    new Vector2((i - (intents.Count - 1) * .5f) * step, 0f), new Vector2(step - 2f, rowHeight), iconSize);
                if (naval)
                {
                    slot.Find("Icon").GetComponent<RectTransform>().anchoredPosition = new Vector2(-5f, 7f);
                    Text amount = slot.Find("Amount").GetComponent<Text>();
                    amount.fontSize = 24;
                    amount.rectTransform.anchoredPosition = new Vector2(0f, -22f);
                }
                ConfigureCompactBattleHover(slot.gameObject, root, intent.Name, intent.Tooltip);
                RemoveEnemyIntentInspection(slot);
            }
            var removed = new List<GameObject>();
            foreach (Transform child in row) if (!current.Contains(child.name)) removed.Add(child.gameObject);
            foreach (GameObject child in removed)
            {
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        private static BattleCommandIconKind CompactIntentIcon(CoreBattleIntentKind kind, bool boarding)
        {
            switch (kind)
            {
                case CoreBattleIntentKind.Cannon: return BattleCommandIconKind.Cannon;
                case CoreBattleIntentKind.Melee: return BattleCommandIconKind.Melee;
                case CoreBattleIntentKind.Ram: return BattleCommandIconKind.Ram;
                case CoreBattleIntentKind.Guard: return BattleCommandIconKind.Guard;
                case CoreBattleIntentKind.Immunity: return BattleCommandIconKind.Immunity;
                case CoreBattleIntentKind.Heal: return boarding ? BattleCommandIconKind.Medicine : BattleCommandIconKind.Repair;
                case CoreBattleIntentKind.Load: return BattleCommandIconKind.Load;
                case CoreBattleIntentKind.Move: return BattleCommandIconKind.Move;
                case CoreBattleIntentKind.Fire: return BattleCommandIconKind.Fire;
                case CoreBattleIntentKind.Arcane: return BattleCommandIconKind.Mana;
                case CoreBattleIntentKind.Thunder: return BattleCommandIconKind.ThunderChain;
                case CoreBattleIntentKind.Curse: return BattleCommandIconKind.Curse;
                case CoreBattleIntentKind.Tide: return BattleCommandIconKind.TidePower;
                case CoreBattleIntentKind.Blood: return BattleCommandIconKind.Blood;
                default: return BattleCommandIconKind.Wait;
            }
        }

        private RectTransform CompactBattleBadge(RectTransform parent, string name, BattleCommandIconKind kind,
            string amount, Vector2 position, Vector2 size, float iconSize)
        {
            RectTransform slot = parent.Find(name) as RectTransform;
            if (slot == null)
            {
                slot = CreateRect(name, parent, size, position);
                slot.gameObject.AddComponent<Image>().color = Color.clear;
                CreateRect("Icon", slot, new Vector2(iconSize, iconSize), new Vector2(-4f, 5f)).gameObject.AddComponent<BattleCommandIconGraphic>();
                Text label = CreateText("Amount", slot, new Vector2(size.x, 28f), new Vector2(0f, -15f), 22, FontStyle.Bold, TextAnchor.MiddleRight, Ink);
                label.raycastTarget = false;
                Outline outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(.025f, .035f, .045f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            slot.anchoredPosition = position;
            slot.sizeDelta = size;
            BattleCommandIconGraphic icon = slot.Find("Icon").GetComponent<BattleCommandIconGraphic>();
            icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            icon.Configure(kind);
            Text value = slot.Find("Amount").GetComponent<Text>();
            value.rectTransform.sizeDelta = new Vector2(size.x - 4f, 28f);
            value.fontSize = size.x > 90f ? 25 : 19;
            value.text = amount;
            return slot;
        }

        private static void RemoveCompactBattleHover(GameObject target)
        {
            BattleStatusHover hover = target.GetComponent<BattleStatusHover>();
            if (hover == null) return;
            // Retained buttons may still own a popup; OnDisable dismisses it immediately.
            hover.enabled = false;
            if (Application.isPlaying) Destroy(hover); else DestroyImmediate(hover);
        }

        private void ConfigureCompactBattleHover(GameObject target, RectTransform root, string title, string description)
        {
            BattleStatusHover hover = target.GetComponent<BattleStatusHover>();
            if (hover == null) hover = target.AddComponent<BattleStatusHover>();
            hover.Configure(null, BattleStatusTooltip.GetOrCreate(root, interfaceFont), title, description);
        }

        private static void SetNavalCommandButton(RectTransform root, string name, NavalCommandButtonKind kind,
            Vector2 position, Vector2 size)
        {
            RectTransform rect = root.Find(name) as RectTransform;
            if (rect == null) return;
            SetNavalHudRect(rect, size, position);
            NavalCommandButtonSkin.Apply(rect.GetComponent<Button>(), kind);
        }

        private static void SetCompactBattleButton(RectTransform root, string name, Vector2 position, Vector2 size, int fontSize = 18)
        {
            RectTransform rect = root.Find(name) as RectTransform;
            if (rect == null) return;
            SetNavalHudRect(rect, size, position);
            SetNavalHudText(rect, "Label", size - new Vector2(10f, 4f), Vector2.zero, fontSize);
        }
    }

    public sealed class BattleIntentAnchor : MonoBehaviour
    {
        private RectTransform health;
        private float offset;
        private CoreBattleState navalBattle;
        public void Configure(RectTransform target, float height, CoreBattleState battle = null)
        {
            health = target;
            offset = height;
            navalBattle = battle;
            Follow();
        }
        private void LateUpdate() => Follow();
        private void Follow()
        {
            if (health == null) return;
            float height = offset + (navalBattle == null ? 0f : NavalTacticalBoardView.HullReadoutOffset(navalBattle));
            transform.position = health.TransformPoint(new Vector3(0f, height, 0f));
            transform.rotation = health.rotation;
        }
    }
}
