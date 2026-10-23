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
            SetCompactBattleButton(root, "End Battle Turn", new Vector2(735f, -303f), new Vector2(230f, 46f));
            SetCompactBattleButton(root, "Battle Ram", new Vector2(675.5f, -191f), new Vector2(111f, 44f));
            SetCompactBattleButton(root, "Battle Board", new Vector2(794.5f, -191f), new Vector2(111f, 44f));
            RectTransform movement = root.Find("Naval movement controls") as RectTransform;
            if (movement != null)
            {
                movement.sizeDelta = new Vector2(230f, 44f);
                movement.anchoredPosition = new Vector2(735f, -247f);
                ConfigureCompactBattleHover(movement.Find("Commit Naval Movement").gameObject, root,
                    "确定航行", "按已规划路线航行。终点为敌舰所在格时主动冲撞，最后一步消耗 1 行动点；双方存活则自动进入白刃战。普通航行抵达后选择朝向，剩余点数可先用于冲撞或接舷。");
                ConfigureCompactBattleHover(movement.Find("Clear Naval Movement").gameObject, root,
                    "清空路线", "清除当前预览路线，重新规划；不会移动船只或消耗行动点。");
            }
            RectTransform retreat = root.Find("Emergency Battle Return") as RectTransform ?? root.Find("Retreat Battle") as RectTransform;
            if (retreat != null)
            {
                retreat.gameObject.SetActive(battle.Stage != NavalBattleStage.Boarding);
                SetCompactBattleButton(root, retreat.name, new Vector2(-770f, 340f), new Vector2(160f, 30f), 14);
            }
            if (battle.Stage == NavalBattleStage.Boarding)
            {
                foreach (Text label in root.GetComponentsInChildren<Text>(true))
                    if (label.name.EndsWith(" Formation Label", System.StringComparison.Ordinal)) label.gameObject.SetActive(false);
                foreach (string side in new[] { "Player", "Enemy" })
                {
                    Vector2 position = new Vector2(side == "Player" ? -480f : 480f, 425f);
                    SetNavalHudRect(root.Find(side + " Battle Name Board") as RectTransform, new Vector2(380f, 46f), position);
                    SetNavalHudText(root, side + " Battle Name", new Vector2(368f, 42f), position, 18);
                }
            }
            RefreshCompactEnemyIntent(root, battle);
        }

        private void RefreshCompactBattleResources(RectTransform root, CoreBattleState battle)
        {
            RectTransform panel = root.Find("Battle Compact Resources") as RectTransform;
            if (panel == null) panel = CreateRect("Battle Compact Resources", root, new Vector2(260f, 180f), new Vector2(-735f, -240f));
            for (int line = 1; line <= 3; line++)
                if (panel.Find("Battle Resource Line " + line) == null)
                    CreateRect("Battle Resource Line " + line, panel, new Vector2(260f, 54f), new Vector2(0f, (2 - line) * 60f));
            RectTransform first = panel.Find("Battle Resource Line 1") as RectTransform;
            SetCompactResource(first, root, "Energy", BattleCommandIconKind.Energy, "能量", battle.Energy + "/" + battle.MaxEnergy,
                "用于打出卡牌；每回合恢复至能量上限。", 0, 2, 110f, battle.Energy, limit: battle.MaxEnergy);
            int points = battle.Stage == NavalBattleStage.Boarding ? 0 : battle.NavalAwaitingHeadingSelection ? battle.NavalArrivalActionPoints : battle.NavalActionPoints;
            SetCompactResource(first, root, "ActionPoint", BattleCommandIconKind.ActionPoint, "行动点", points.ToString(),
                battle.Stage == NavalBattleStage.Boarding ? "接舷战中无需航行行动点。" : battle.NavalAwaitingHeadingSelection
                    ? "抵达剩余行动点，可用于冲撞或接舷；选择到达朝向后清空。" : "用于海战航行、冲撞和接舷。", 1, 2, 110f, points);
            RectTransform second = panel.Find("Battle Resource Line 2") as RectTransform;
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            SetCompactResource(second, root, "Mana", BattleCommandIconKind.Mana, "法力",
                battle.Schools.TemporaryMana > 0 ? battle.Schools.Mana + "+" + battle.Schools.TemporaryMana : battle.Schools.Mana + "/" + battle.Schools.ManaLimit,
                "奥术体系资源。常规法力 " + battle.Schools.Mana + "/" + battle.Schools.ManaLimit +
                "；临时法力 " + battle.Schools.TemporaryMana + "（图标中加号后的数值），会在回合结束时清空。", 0, 4,
                current: battle.Schools.Mana, temporary: battle.Schools.TemporaryMana, limit: battle.Schools.ManaLimit, separateTemporary: true);
            SetCompactResource(second, root, "ThunderChain", BattleCommandIconKind.ThunderChain, "雷链",
                battle.Schools.ThunderChain + "/" + battle.Schools.ThunderLimit, "雷霆体系积累的雷链。", 1, 4,
                current: battle.Schools.ThunderChain, limit: battle.Schools.ThunderLimit);
            SetCompactResource(second, root, "TidePower", BattleCommandIconKind.TidePower, "潮势",
                (boarding ? battle.Boarding.TidePower : battle.Schools.TidePower) + "/" + (boarding ? 12 : battle.Schools.TideLimit),
                "潮汐体系资源，用于强化相关卡牌与效果。", 2, 4,
                current: boarding ? battle.Boarding.TidePower : battle.Schools.TidePower, limit: boarding ? 12 : battle.Schools.TideLimit);
            SetCompactResource(second, root, "CurseMark", BattleCommandIconKind.CurseMark, "咒印",
                (boarding ? battle.Boarding.CurseMarks : battle.Schools.CurseMarks).ToString(),
                "施加在敌方的咒印。" + CoreBattleStatusPresentation.DecayingDamageRule +
                (battle.Schools.CurseTickBonus > 0 ? "\n每次结算时额外造成 " + battle.Schools.CurseTickBonus +
                    " 点伤害，咒印消失时清除；这项加成不改变层数与持续时间。" : string.Empty) +
                (battle.Schools.CurseNextTickBonus > 0 ? "\n下次结算时额外造成 " + battle.Schools.CurseNextTickBonus +
                    " 点伤害；这项加成不改变层数与持续时间。" : string.Empty), 3, 4,
                current: boarding ? battle.Boarding.CurseMarks : battle.Schools.CurseMarks);
            RectTransform third = panel.Find("Battle Resource Line 3") as RectTransform;
            SetCompactResource(third, root, "Powder", BattleCommandIconKind.Powder, "火药", session.Cargo.Powder.ToString(), "货舱火药库存，部分炮击与战术卡消耗。", 0, 4, current: session.Cargo.Powder);
            SetCompactResource(third, root, "Cannonball", BattleCommandIconKind.Cannonball, "炮弹", session.Cargo.Cannonballs.ToString(), "货舱炮弹库存，炮击卡消耗。", 1, 4, current: session.Cargo.Cannonballs);
            SetCompactResource(third, root, "Planks", BattleCommandIconKind.Planks, "木板", session.Cargo.Wood.ToString(), "货舱木板库存，用于修理船体。", 2, 4, current: session.Cargo.Wood);
            SetCompactResource(third, root, "Medicine", BattleCommandIconKind.Medicine, "药品", session.Cargo.Medicine.ToString(), "货舱药品库存，治疗与接舷卡可能消耗。", 3, 4, current: session.Cargo.Medicine);
        }

        private void SetCompactResource(RectTransform line, RectTransform root, string id, BattleCommandIconKind kind,
            string title, string value, string description, int index, int count, float step = 64f,
            long current = 0, long temporary = 0, long limit = -1, bool separateTemporary = false)
        {
            RectTransform slot = CompactBattleBadge(line, "Resource " + id, kind, value,
                new Vector2((index - (count - 1) * .5f) * step, 0f), new Vector2(step - 4f, 52f), 38f);
            ConfigureCompactBattleHover(slot.gameObject, root, title, "当前 " + value + "\n" + description);
            BattleResourceChangeTracker tracker = slot.GetComponent<BattleResourceChangeTracker>();
            if (tracker == null) tracker = slot.gameObject.AddComponent<BattleResourceChangeTracker>();
            tracker.Observe(current, temporary, limit, interfaceFont, Application.isPlaying && !editorPreviewBuilding,
                separateTemporary, this);
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
            Button button = pile.GetComponent<Button>();
            if (button == null)
            {
                button = pile.gameObject.AddComponent<Button>();
                button.targetGraphic = pile.GetComponent<Image>();
                button.onClick.AddListener(() => OpenBattlePile(discard));
            }
            ConfigureCompactBattleHover(pile.gameObject, root, caption.text,
                discard ? "本阶段已弃置的卡牌。点击查看；抽牌堆耗尽后会重新洗入。" : "本阶段尚未抽取的卡牌。点击查看，列表不透露实际抽取顺序。");
        }

        private void ConfigureCompactBattlePileBack(RectTransform pile)
        {
            RectTransform icon = (RectTransform)pile.Find("Icon");
            // Keep the shared badge and flight anchor, replacing only its visible artwork.
            icon.GetComponent<BattleCommandIconGraphic>().enabled = false;
            icon.anchoredPosition = new Vector2(-4f, 8f);
            if (icon.Find("Card Back") != null) return;

            Vector2 size = CardFaceLayout.FrameSize * (48f / CardFaceLayout.FrameSize.x);
            for (int layer = 2; layer >= 0; layer--)
            {
                RectTransform back = CreateRect(layer == 0 ? "Card Back" : "Stack Back " + layer,
                    icon, size, new Vector2(-2f * layer, 2f * layer));
                BattleCardFlightBackGraphic graphic = back.gameObject.AddComponent<BattleCardFlightBackGraphic>();
                graphic.Configure(layer == 0 ? Color.white : new Color(.72f, .76f, .8f));
            }
        }

        private void RefreshCompactEnemyIntent(RectTransform root, CoreBattleState battle)
        {
            RectTransform row = root.Find("Enemy Intent Icons") as RectTransform;
            if (row == null) row = CreateRect("Enemy Intent Icons", root, new Vector2(340f, 52f), Vector2.zero);
            BattleIntentAnchor anchor = row.GetComponent<BattleIntentAnchor>();
            if (anchor == null) anchor = row.gameObject.AddComponent<BattleIntentAnchor>();
            // Naval health bars sit 105px below the ship. Leave room above the
            // full 112x168 portrait at every rotation, plus half the 52px intent row.
            anchor.Configure(root.Find("Enemy Battle Hull Bar Track") as RectTransform, battle.Stage == NavalBattleStage.Boarding ? 338f : 250f);
            IReadOnlyList<CoreBattleIntent> intents = CoreBattleIntentPresentation.Build(session);
            var current = new HashSet<string>();
            float step = Mathf.Min(62f, 340f / Mathf.Max(1, intents.Count));
            for (int i = 0; i < intents.Count; i++)
            {
                CoreBattleIntent intent = intents[i];
                string name = "Intent " + intent.Id;
                current.Add(name);
                RectTransform slot = CompactBattleBadge(row, name, CompactIntentIcon(intent.Kind, battle.Stage == NavalBattleStage.Boarding), intent.Value,
                    new Vector2((i - (intents.Count - 1) * .5f) * step, 0f), new Vector2(step - 2f, 52f), Mathf.Min(42f, step - 8f));
                ConfigureCompactBattleHover(slot.gameObject, root, intent.Name, intent.Tooltip +
                    (HasBoardingEnemySchool(battle) ? "\n点击查看敌方完整行动与体系资源。" : string.Empty));
                Button inspect = slot.GetComponent<Button>();
                if (inspect == null)
                {
                    inspect = slot.gameObject.AddComponent<Button>();
                    inspect.targetGraphic = slot.GetComponent<Image>();
                    inspect.onClick.AddListener(OpenBoardingEnemyOverview);
                }
                inspect.interactable = HasBoardingEnemySchool(battle);
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
                case CoreBattleIntentKind.Cannon: return BattleCommandIconKind.Cannonball;
                case CoreBattleIntentKind.Melee: return BattleCommandIconKind.Melee;
                case CoreBattleIntentKind.Ram: return BattleCommandIconKind.Ram;
                case CoreBattleIntentKind.Guard: return BattleCommandIconKind.Guard;
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

        private void ConfigureCompactBattleHover(GameObject target, RectTransform root, string title, string description)
        {
            BattleStatusHover hover = target.GetComponent<BattleStatusHover>();
            if (hover == null) hover = target.AddComponent<BattleStatusHover>();
            hover.Configure(null, BattleStatusTooltip.GetOrCreate(root, interfaceFont), title, description);
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
        public void Configure(RectTransform target, float height) { health = target; offset = height; Follow(); }
        private void LateUpdate() => Follow();
        private void Follow()
        {
            if (health == null) return;
            transform.position = health.TransformPoint(new Vector3(0f, offset, 0f));
            transform.rotation = health.rotation;
        }
    }
}
