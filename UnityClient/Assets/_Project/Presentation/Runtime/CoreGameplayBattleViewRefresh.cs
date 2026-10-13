using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // A turn changes numbers and cards, not the entire ocean/ship hierarchy.
        // Keep the expensive authored scene and surviving card canvases alive.
        private sealed class RetainedBattleHandCard
        {
            public CoreBattleState Battle;
            public int CardId, Index;
            public Button Button;
            public Text Label, Name, Energy, Powder, Cannonballs, SpecialCost, Explanation, Title, Surcharge;
            public RectTransform Bubble, BubblePointer;
            public Outline Outline;
            public BattleCardSurfaceGraphic Surface;
            public CardSchoolFrameGraphic SchoolFrame;
            public BattleCardHoverFeedback Hover;
            public Transform[] IndexedChildren;
            public bool Available;
        }

        private CoreBattleState retainedBattleState;
        private NavalBattleStage retainedBattleStage;
        private RectTransform retainedBattleRoot, retainedBattleHand, retainedBattleReadouts;
        private Button retainedBattleRetreat, retainedBattleEndTurn, retainedBattleRam, retainedBattleBoard;
        private BattleDefenseHud retainedPlayerDefense, retainedEnemyDefense;
        private Outline retainedEnemyIntentOutline;
        private Image retainedEnemyIntentIcon;
        private readonly List<RetainedBattleHandCard> retainedBattleHandViews = new List<RetainedBattleHandCard>();
        private readonly List<RetainedBattleHandCard> reusableBattleHandViews = new List<RetainedBattleHandCard>();
        private readonly Dictionary<string, Text> retainedBattleLabels = new Dictionary<string, Text>();

        private RetainedBattleHandCard CreateRetainedBattleHandCard(RectTransform hand, CoreBattleState battle, int index)
        {
            CoreBattleCard data = session.GetBattleCard(battle.Hand[index]);
            bool available = session.CanPlayCard(index);
            Color accent = CardSchoolArtCatalog.Palette(data.SchoolId).Accent;
            Button play = CreateButton("Battle Card " + index, hand, CardFaceLayout.CardSize, Vector2.zero, PanelRaised, Ink, 17);
            Outline frame = play.gameObject.AddComponent<Outline>();
            frame.effectColor = accent;
            frame.effectDistance = new Vector2(2f, -2f);
            CreateCardIllustration("Card Art " + index, play.transform, data);
            Text label = play.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.UpperCenter;
            Text cost = CreateCardCostIcon("Card Energy Cost " + index, "Card Energy Cost Text " + index,
                play.transform, CardFaceLayout.EnergyIconSize, CardSchoolArtCatalog.CostPosition(data, CardOrnamentKind.Energy),
                CardOrnamentKind.Energy, data.Type, data.Energy);
            RectTransform surchargePlate = CreateFixedPanel("Card Repeat Cost Plate " + index, play.transform,
                new Vector2(30f, 18f), CardFaceLayout.RepeatCostPosition, CardSchoolArtCatalog.Palette(data.SchoolId).Parchment);
            surchargePlate.GetComponent<Image>().raycastTarget = false;
            Text surcharge = CreateText("Card Repeat Cost Text " + index, surchargePlate,
                new Vector2(28f, 18f), Vector2.zero, 11, FontStyle.Bold, TextAnchor.MiddleCenter,
                CardSchoolArtCatalog.Palette(data.SchoolId).TitleInk);
            surcharge.raycastTarget = false;
            RefreshBattleCardCost(cost, surcharge, battle.Hand[index], data);
            CreateCardResourceCostIcons(play.transform, data,
                "Card Powder Plate " + index, "Card Powder Cost " + index,
                "Card Cannonball Plate " + index, "Card Cannonball Cost " + index,
                out Text powder, out Text cannonballs);
            if (powder != null) powder.text = session.GetBattleCardPowderCost(battle.Hand[index]).ToString();
            Text specialCost = CreateCardSpecialCostIcon(play.transform, data,
                "Card Special Resource Plate " + index, "Card Special Resource Cost " + index, battle);
            ApplyBattleCardStyle(play, data, available, index);
            RectTransform bubble = CreateCardKeywordBubble(play.transform, index, data, index < battle.Hand.Count / 2);
            ConfigureBattleCardPresentation(play, accent, bubble);
            play.interactable = available;
            var indexedChildren = new List<Transform>();
            foreach (Transform child in play.GetComponentsInChildren<Transform>(true))
                if (child != play.transform && child.name.StartsWith("Card ")) indexedChildren.Add(child);
            var view = new RetainedBattleHandCard
            {
                Battle = battle, CardId = battle.Hand[index], Index = index, Button = play,
                Label = label, Energy = cost, Powder = powder, Cannonballs = cannonballs, SpecialCost = specialCost,
                Bubble = bubble, Surcharge = surcharge,
                Name = play.transform.Find("Card Name Plate " + index)?.GetComponentInChildren<Text>(),
                BubblePointer = bubble == null ? null : bubble.Find("Keyword Bubble Pointer") as RectTransform,
                Explanation = bubble == null ? null : bubble.Find("Keyword Bubble Text")?.GetComponent<Text>(),
                Title = bubble == null ? null : bubble.Find("Keyword Bubble Title")?.GetComponent<Text>(),
                Outline = frame, Surface = play.GetComponentInChildren<BattleCardSurfaceGraphic>(),
                SchoolFrame = play.GetComponentInChildren<CardSchoolFrameGraphic>(),
                Hover = play.GetComponent<BattleCardHoverFeedback>(), Available = available,
                IndexedChildren = indexedChildren.ToArray()
            };
            ArrangeBattleCardResourceCosts(view, data);
            // The index is deliberately read at click-time: removing an earlier
            // card shifts every later slot, and duplicate IDs are not identities.
            play.onClick.AddListener(() => PlayRetainedBattleCard(view));
            return view;
        }

        private void PlayRetainedBattleCard(RetainedBattleHandCard view)
        {
            if (!isActiveAndEnabled || battleAnimationPlaying || session.Phase != CoreDemoPhase.Battle ||
                retainedBattleRoot == null || !retainedBattleRoot.gameObject.activeInHierarchy ||
                !ReferenceEquals(view.Battle, session.Battle) || !ReferenceEquals(retainedBattleState, session.Battle) ||
                view.Button == null || !view.Button.gameObject.activeInHierarchy || !view.Button.interactable ||
                view.Button.transform.parent != retainedBattleHand || view.Index < 0 ||
                view.Index >= retainedBattleHandViews.Count || view.Index >= session.Battle.Hand.Count ||
                !ReferenceEquals(retainedBattleHandViews[view.Index], view) || session.Battle.Hand[view.Index] != view.CardId)
                return;
            BeginBattleCardAnimation(view.Button, view.Index, session.GetBattleCard(view.CardId));
        }

        private void CacheRetainedBattleView(RectTransform root, RectTransform hand, CoreBattleState battle, Button retreat, RectTransform readouts)
        {
            ApplyBattleDecisionLayout(root, battle);
            retainedBattleRoot = root;
            retainedBattleHand = hand;
            retainedBattleReadouts = readouts;
            retainedBattleState = battle;
            retainedBattleStage = battle.Stage;
            retainedBattleRetreat = retreat;
            retainedBattleEndTurn = root.Find("End Battle Turn")?.GetComponent<Button>();
            retainedBattleRam = root.Find("Battle Ram")?.GetComponent<Button>();
            retainedBattleBoard = root.Find("Battle Board")?.GetComponent<Button>();
            retainedPlayerDefense = root.Find("Player Battle Hull Bar Track")?.GetComponent<BattleDefenseHud>();
            retainedEnemyDefense = root.Find("Enemy Battle Hull Bar Track")?.GetComponent<BattleDefenseHud>();
            retainedEnemyIntentOutline = root.Find("Enemy Intent HUD")?.GetComponent<Outline>();
            retainedEnemyIntentIcon = root.Find("Enemy Intent HUD/Enemy Intent Icon")?.GetComponent<Image>();
            retainedBattleLabels.Clear();
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
            {
                if (label.transform.IsChildOf(hand) || label.name == "Label") continue;
                retainedBattleLabels[label.name] = label;
            }
            if (retainedBattleReadouts != null)
            {
                foreach (Text label in retainedBattleReadouts.GetComponentsInChildren<Text>(true))
                    retainedBattleLabels[label.name] = label;
            }
        }

        private bool TryRefreshRetainedBattleView()
        {
            CoreBattleState battle = session.Battle;
            if (session.Phase != CoreDemoPhase.Battle || battle == null ||
                !ReferenceEquals(battle, retainedBattleState) || battle.Stage != retainedBattleStage ||
                retainedBattleRoot == null || !retainedBattleRoot.gameObject.activeInHierarchy ||
                !retainedBattleRoot.IsChildOf(contentRoot) || retainedBattleHand == null)
                return false;

            RefreshRetainedBattleHand(battle);
            ApplyBattleViewPose(retainedBattleRoot, battle.DistanceMeters, battle.Stage, battle.Alignment);
            bool boarding = battle.Stage == NavalBattleStage.Boarding;
            if (retainedPlayerDefense != null)
            {
                int hull = boarding ? battle.PlayerBoardingHealth : session.Hull;
                retainedPlayerDefense.SetHull(hull, hull);
                retainedPlayerDefense.SetGuard(battle.PlayerBlock);
            }
            if (retainedEnemyDefense != null)
            {
                int hull = boarding ? battle.EnemyBoardingHealth : battle.EnemyHull;
                retainedEnemyDefense.SetHull(hull, hull);
                retainedEnemyDefense.SetGuard(battle.EnemyBlock);
            }
            CoreBossKind bossKind = battle.Profile == null ? (battle.IsBoss ? CoreBossKind.Route : CoreBossKind.None) : battle.Profile.BossKind;
            SetRetainedBattleText("Battle Badge", (bossKind == CoreBossKind.Legendary ? "传奇战舰挑战" : bossKind == CoreBossKind.Treasure ? "藏宝图首领战" : battle.IsBoss ? "区域首领战" : "航路遭遇战") + " · " + BattleEnvironmentArtCatalog.LabelFor(session.BattleTerrain) + " · 回合 " + (session.IsGuidedBossBattle ? battle.PhaseTurn : battle.Turn));
            SetRetainedBattleText("Battle Curse Status", CurseBattleStatus);
            SetRetainedBattleText("Battle Range", boarding ? "接舷交战" : battle.DistanceMeters.ToString("0") + " 米");
            SetRetainedBattleText("Battle Alignment", boarding ? BoardingConnectionLabel(battle) : battle.Alignment == NavalBattleAlignment.HeadOn ? "→ 舰艏 ⇄ 舰艏 ←\n迎头相对" : battle.Alignment == NavalBattleAlignment.EnemyBroadside ? "→ 舰艏 ⊥ 侧舷\n我方对准敌舰侧面" : "舰艏 →    → 舰艏\n并排行船");
            SetRetainedBattleText("Battle Active Buffs", BattleActiveBuffText(battle));
            SetRetainedBattleText("Player Battle Name", BattleCrewName(battle, true));
            SetRetainedBattleText("Enemy Battle Name", BattleCrewName(battle, false));
            string intent = session.GetEnemyIntent();
            bool defending = !session.EnemyWillRam && (intent.Contains("格挡设为") || battle.Stage == NavalBattleStage.Preparation);
            SetRetainedBattleText("Enemy Intent Text", "敌方意图\n" + intent);
            SetRetainedBattleText("Enemy Intent Glyph", session.EnemyWillRam ? "撞" : defending ? "策" : boarding ? "刃" : "攻");
            if (retainedBattleLabels.TryGetValue("Enemy Intent Text", out Text intentLabel))
                ConfigureEnemyIntentLayout(retainedBattleRoot, retainedBattleRoot.Find("Enemy Intent HUD") as RectTransform,
                    intentLabel, retainedEnemyIntentIcon, session.EnemyWillRam);
            if (retainedEnemyIntentOutline != null) retainedEnemyIntentOutline.effectColor = defending ? Gold : Coral;
            if (retainedEnemyIntentIcon != null) retainedEnemyIntentIcon.color = defending ? Gold : Coral;
            SetRetainedBattleText("Player Resource Title", "旗舰资源 · 格挡 " + battle.PlayerBlock);
            SetRetainedBattleText("Resource 能 Value", battle.Energy + "/" + battle.MaxEnergy);
            SetRetainedBattleText("Resource 药 Value", (boarding ? session.Cargo.Medicine : session.Cargo.Powder).ToString());
            SetRetainedBattleText("Resource 回 Value", (session.IsGuidedBossBattle ? battle.PhaseTurn : battle.Turn).ToString());
            SetRetainedBattleText("Resource 盾 Value", battle.EnemyBlock.ToString());
            SetRetainedBattleText("Player Ammunition", BattleSupplyReserveText(boarding));
            SetRetainedBattleText("Enemy Burning", "燃烧 " + battle.EnemyBurnTurns + " 回合");
            if (retainedBattleRam != null)
            {
                bool allowed = session.CanRam(out string reason);
                retainedBattleRam.interactable = allowed;
                SetRetainedBattleText("Battle Ram Condition", allowed ? "撞击方先手 · 舰艏品质对拼" : reason);
            }
            if (retainedBattleBoard != null)
            {
                bool allowed = session.CanBoard(out string reason);
                retainedBattleBoard.interactable = allowed;
                SetRetainedBattleText("Battle Board Condition", allowed ? "普通水手数决定接舷生命上限；海战船体损伤按一半带入" : reason);
            }
            if (retainedBattleEndTurn != null) retainedBattleEndTurn.interactable = true;
            if (retainedBattleRetreat != null)
            {
                bool emergency = session.CanRequestEmergencyTow();
                retainedBattleRetreat.name = emergency ? "Emergency Battle Return" : "Retreat Battle";
                retainedBattleRetreat.interactable = emergency || session.CanRetreatBattle;
                retainedBattleRetreat.GetComponentInChildren<Text>().text = emergency ? "应急撤离 · 保留 35%" : session.CanRetreatBattle ? "脱离战斗 · 撤回港口" : session.IsGuidedBossBattle ? "完成首领战斗教学" : "接舷中无法撤退";
            }
            if (headerStatus != null) headerStatus.text = BuildHeaderStatus();
            ApplyBattleDecisionLayout(retainedBattleRoot, battle);
            return true;
        }

        private void RefreshRetainedBattleHand(CoreBattleState battle)
        {
            reusableBattleHandViews.Clear();
            reusableBattleHandViews.AddRange(retainedBattleHandViews);
            retainedBattleHandViews.Clear();
            for (int index = 0; index < battle.Hand.Count; index++)
            {
                RetainedBattleHandCard view = null;
                for (int candidate = 0; candidate < reusableBattleHandViews.Count; candidate++)
                {
                    RetainedBattleHandCard existing = reusableBattleHandViews[candidate];
                    if (existing.CardId != battle.Hand[index] || existing.Button == null ||
                        !existing.Button.gameObject.activeSelf || existing.Button.transform.parent != retainedBattleHand) continue;
                    view = existing;
                    reusableBattleHandViews.RemoveAt(candidate);
                    break;
                }
                if (view == null) view = CreateRetainedBattleHandCard(retainedBattleHand, battle, index);
                retainedBattleHandViews.Add(view);
                RefreshRetainedHandCard(view, index, battle.Hand.Count);
                view.Button.transform.SetSiblingIndex(index);
            }
            foreach (RetainedBattleHandCard discarded in reusableBattleHandViews)
            {
                if (discarded.Button == null) continue;
                discarded.Button.interactable = false;
                discarded.Button.gameObject.SetActive(false);
                Destroy(discarded.Button.gameObject);
            }
            reusableBattleHandViews.Clear();
            ArrangeBattleHand(retainedBattleHand);
        }

        private void ArrangeBattleHand(RectTransform hand)
        {
            int count = retainedBattleHandViews.Count;
            if (count == 0) return;
            const float gap = 8f;
            float scale = Mathf.Min(1f, hand.rect.height / CardFaceLayout.FrameSize.y,
                (hand.rect.width - gap * (count - 1)) / (count * CardFaceLayout.FrameSize.x));
            float step = CardFaceLayout.FrameSize.x * scale + gap;
            // Align complete frames to one baseline. Their painted corner costs
            // never overlap another card, including the maximum eight-card hand.
            float y = hand.rect.yMin + scale * CardFaceLayout.FrameSize.y * .5f;
            for (int index = 0; index < count; index++)
            {
                RetainedBattleHandCard view = retainedBattleHandViews[index];
                RectTransform rect = (RectTransform)view.Button.transform;
                rect.anchoredPosition = new Vector2((index - (count - 1) * .5f) * step, y);
                rect.localScale = Vector3.one * scale;
                if (view.Hover != null) view.Hover.SetRestingScale(scale);
            }
        }

        private void RefreshRetainedHandCard(RetainedBattleHandCard view, int index, int count)
        {
            if (view.Index != index)
            {
                string oldSuffix = " " + view.Index;
                foreach (Transform child in view.IndexedChildren)
                    if (child != null && child.name.EndsWith(oldSuffix))
                        child.name = child.name.Substring(0, child.name.Length - oldSuffix.Length) + " " + index;
            }
            view.Index = index;
            view.Button.name = "Battle Card " + index;
            CoreBattleCard data = session.GetBattleCard(view.CardId);
            bool available = session.CanPlayCard(index);
            BattleCardPalette palette = CardSchoolArtCatalog.Palette(data.SchoolId);
            view.Button.interactable = available;
            view.Outline.effectColor = available ? Color.Lerp(palette.Accent, palette.Surface, .5f) : Color.Lerp(palette.Accent, palette.Surface, 0.4f);
            if (view.Available != available)
            {
                view.Surface.Configure(data.Type, available, data.SchoolId);
                view.SchoolFrame.Configure(data.SchoolId, available, data.PowderCost, data.CannonballCost,
                    CardSpecialCostPresentation.ResourceType(data));
                view.Available = available;
            }
            RefreshBattleCardCost(view.Energy, view.Surcharge, view.CardId, data);
            if (view.Powder != null) view.Powder.text = session.GetBattleCardPowderCost(view.CardId).ToString();
            if (view.Cannonballs != null) view.Cannonballs.text = data.CannonballCost.ToString();
            if (view.SpecialCost != null) view.SpecialCost.text = CardSpecialCostPresentation.CostText(data, view.Battle, session.Cargo.Wood);
            if (view.Name != null) view.Name.text = CardFaceLayout.DisplayName(data);
            view.Label.text = BattleCardEffectTextForCurrentStage(data);
            RefreshBattleCardTextLayout(view.Label);
            ArrangeBattleCardResourceCosts(view, data);
            RefreshRetainedCardKeywordBubble(view, data, palette, index, count);
        }

        private static void ArrangeBattleCardResourceCosts(RetainedBattleHandCard view, CoreBattleCard data)
        {
            PlaceResource(view.Powder, CardSchoolArtCatalog.CostPosition(data, CardOrnamentKind.Gunpowder));
            PlaceResource(view.Cannonballs, CardSchoolArtCatalog.CostPosition(data, CardOrnamentKind.Cannonball));
            PlaceResource(view.SpecialCost, CardSchoolArtCatalog.CostPosition(data, CardSpecialCostPresentation.OrnamentKind(data)));
            RectTransform repeat = view.Surcharge.transform.parent as RectTransform;
            repeat.anchoredPosition = CardFaceLayout.RepeatCostPosition;

            void PlaceResource(Text number, Vector2 position)
            {
                if (number == null) return;
                RectTransform icon = number.transform.parent as RectTransform;
                icon.localScale = Vector3.one;
                icon.anchoredPosition = position;
            }
        }

        private void RefreshRetainedCardKeywordBubble(RetainedBattleHandCard view, CoreBattleCard data,
            BattleCardPalette palette, int index, int count)
        {
            string explanation = BattleCardTooltipText(data, index);
            if (string.IsNullOrWhiteSpace(explanation))
            {
                if (view.Hover != null) view.Hover.SetKeywordTooltip(null);
                if (view.Bubble != null)
                {
                    view.Bubble.gameObject.SetActive(false);
                    Destroy(view.Bubble.gameObject);
                }
                view.Bubble = view.BubblePointer = null;
                view.Explanation = view.Title = null;
                return;
            }

            bool right = index < count / 2;
            if (view.Bubble == null)
            {
                view.Bubble = CreateCardKeywordBubble(view.Button.transform, index, data, right);
                view.BubblePointer = view.Bubble == null ? null : view.Bubble.Find("Keyword Bubble Pointer") as RectTransform;
                view.Explanation = view.Bubble == null ? null : view.Bubble.Find("Keyword Bubble Text")?.GetComponent<Text>();
                view.Title = view.Bubble == null ? null : view.Bubble.Find("Keyword Bubble Title")?.GetComponent<Text>();
                if (view.Hover != null) view.Hover.SetKeywordTooltip(view.Bubble);
            }
            if (view.Bubble == null) return;
            view.Bubble.name = "Card Keyword Bubble " + index;
            if (view.Title != null) view.Title.text = data.Name + "\n<size=18>" + CardKeywordCategory(data) + "</size>";
            Outline bubbleOutline = view.Bubble.GetComponent<Outline>();
            if (bubbleOutline != null) bubbleOutline.effectColor = palette.Accent;
            if (view.Explanation != null) view.Explanation.text = explanation;
            ConfigureCardKeywordBubbleGeometry(view.Bubble, right,
                session.Battle.Stage == NavalBattleStage.Boarding && data.Repair > 0 ? 110f : 0f);
        }

        private void SetRetainedBattleText(string name, string value)
        {
            if (retainedBattleLabels.TryGetValue(name, out Text label) && label != null) label.text = value;
        }

        private void RetreatFromRetainedBattle(RectTransform root, CoreBattleState battle)
        {
            if (!isActiveAndEnabled || battleAnimationPlaying || root == null || !root.gameObject.activeInHierarchy ||
                root != retainedBattleRoot || session.Phase != CoreDemoPhase.Battle || !ReferenceEquals(session.Battle, battle)) return;
            if (session.CanRequestEmergencyTow())
            {
                if (session.RequestEmergencyTow()) Render();
            }
            else RetreatCurrentBattle();
        }
    }
}
