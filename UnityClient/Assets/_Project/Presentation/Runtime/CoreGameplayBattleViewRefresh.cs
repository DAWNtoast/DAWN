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
            public Text Label, Energy, Powder, Explanation, Title, Surcharge;
            public RectTransform Bubble, BubblePointer;
            public Outline Outline;
            public BattleCardSurfaceGraphic Surface;
            public BattleCardHoverFeedback Hover;
            public Transform[] IndexedChildren;
            public bool Available;
        }

        private CoreBattleState retainedBattleState;
        private NavalBattleStage retainedBattleStage;
        private RectTransform retainedBattleRoot, retainedBattleHand, retainedBattleReadouts;
        private HorizontalLayoutGroup retainedBattleHandLayout;
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
            Color accent = CardEffectColor(data);
            Button play = CreateButton("Battle Card " + index, hand, new Vector2(206f, 300f), Vector2.zero, PanelRaised, Ink, 17);
            Outline frame = play.gameObject.AddComponent<Outline>();
            frame.effectColor = accent;
            frame.effectDistance = new Vector2(2f, -2f);
            Image accentLine = CreateImage("Card Accent " + index, play.transform, new Vector2(194f, 6f), new Vector2(0f, 144f), accent);
            accentLine.raycastTarget = false;
            RawImage cardArt = CreateRawImage("Card Art " + index, play.transform, new Vector2(174f, 174f), new Vector2(0f, 46f), art.CardSheet, art.CardUv(data.ArtId));
            cardArt.raycastTarget = false;
            Text label = play.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.UpperCenter;
            RectTransform costBadge = CreateFixedPanel("Card Energy Cost " + index, play.transform, new Vector2(40f, 40f), new Vector2(-78f, 124f), DeepSea);
            costBadge.GetComponent<Image>().raycastTarget = false;
            Text cost = CreateText("Card Energy Cost Text " + index, costBadge, new Vector2(36f, 36f), Vector2.zero, 18, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            RectTransform surchargePlate = CreateFixedPanel("Card Repeat Cost Plate " + index, play.transform,
                new Vector2(178f, 23f), new Vector2(0f, -28f), BattleCardStylePalette.ForType(data.Type).Parchment);
            surchargePlate.GetComponent<Image>().raycastTarget = false;
            Text surcharge = CreateText("Card Repeat Cost Text " + index, surchargePlate,
                new Vector2(172f, 21f), Vector2.zero, 11, FontStyle.Bold, TextAnchor.MiddleCenter,
                BattleCardStylePalette.ForType(data.Type).TitleInk);
            surcharge.raycastTarget = false;
            RefreshBattleCardCost(cost, surcharge, battle.Hand[index], data);
            Text powder = null;
            if (data.PowderCost > 0)
            {
                powder = CreateText("Card Powder Cost " + index, play.transform, new Vector2(74f, 30f), new Vector2(57f, 124f), 13, FontStyle.Bold, TextAnchor.MiddleCenter, Coral);
                powder.text = "火药 " + data.PowderCost;
            }
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
                Label = label, Energy = cost, Powder = powder, Bubble = bubble, Surcharge = surcharge,
                BubblePointer = bubble.Find("Keyword Bubble Pointer") as RectTransform,
                Explanation = bubble.Find("Keyword Bubble Text").GetComponent<Text>(), Title = bubble.Find("Keyword Bubble Title").GetComponent<Text>(),
                Outline = frame, Surface = play.GetComponentInChildren<BattleCardSurfaceGraphic>(),
                Hover = play.GetComponent<BattleCardHoverFeedback>(), Available = available,
                IndexedChildren = indexedChildren.ToArray()
            };
            ConfigureReadableCardBubble(bubble, view.Explanation, view.Title, index < battle.Hand.Count / 2);
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
            retainedBattleHandLayout = hand.GetComponent<HorizontalLayoutGroup>();
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
                !retainedBattleRoot.IsChildOf(contentRoot) || retainedBattleHand == null || retainedBattleHandLayout == null)
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
            SetRetainedBattleText("Battle Status", battle.LastAction);
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
            SetRetainedBattleText("Player Ammunition", boarding ? "药品仅在有效增疗时消耗" : "炮弹 " + session.Cargo.Cannonballs + " · 装填 +" + battle.PlayerLoadedShot);
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
                SetRetainedBattleText("Battle Board Condition", allowed ? "普通水手数影响接舷生命上限" : reason);
            }
            if (retainedBattleEndTurn != null) retainedBattleEndTurn.interactable = true;
            if (retainedBattleRetreat != null)
            {
                bool emergency = session.CanRequestEmergencyTow();
                retainedBattleRetreat.name = emergency ? "Emergency Battle Return" : "Retreat Battle";
                UiId.Bind(retainedBattleRetreat.gameObject, retainedBattleRetreat.name);
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
            int count = battle.Hand.Count;
            retainedBattleHandLayout.spacing = count <= 5 ? 16f : (1120f - count * 206f) / (count - 1);
            // Rebuild this small layout, not the whole canvas. Hover offsets must
            // be re-based after a slot is removed, otherwise cards jump back.
            LayoutRebuilder.ForceRebuildLayoutImmediate(retainedBattleHand);
            foreach (RetainedBattleHandCard view in retainedBattleHandViews)
                if (view.Hover != null) view.Hover.ResetAfterHandLayout();
        }

        private void RefreshRetainedHandCard(RetainedBattleHandCard view, int index, int count)
        {
            if (view.Index != index)
            {
                string oldSuffix = " " + view.Index;
                foreach (Transform child in view.IndexedChildren)
                    if (child != null && child.name.EndsWith(oldSuffix))
                    {
                        child.name = child.name.Substring(0, child.name.Length - oldSuffix.Length) + " " + index;
                        UiId.Bind(child.gameObject, child.name);
                    }
            }
            view.Index = index;
            view.Button.name = "Battle Card " + index;
            UiId.Bind(view.Button.gameObject, view.Button.name);
            CoreBattleCard data = session.GetBattleCard(view.CardId);
            bool available = session.CanPlayCard(index);
            BattleCardPalette palette = BattleCardStylePalette.ForType(data.Type);
            view.Button.interactable = available;
            view.Outline.effectColor = available ? Color.Lerp(palette.Accent, palette.Surface, .5f) : Color.Lerp(palette.Accent, palette.Surface, 0.4f);
            if (view.Available != available)
            {
                view.Surface.Configure(data.Type, available);
                view.Available = available;
            }
            RefreshBattleCardCost(view.Energy, view.Surcharge, view.CardId, data);
            if (view.Powder != null) view.Powder.text = "火药 " + data.PowderCost;
            view.Label.text = BattleCardDisplayTextForCurrentStage(data, palette);
            RefreshBattleCardTextLayout(view.Label);
            if (view.Title != null) view.Title.text = data.Name + "\n<size=18>分类 · " + data.Type + "</size>";
            Outline bubbleOutline = view.Bubble == null ? null : view.Bubble.GetComponent<Outline>();
            if (bubbleOutline != null) bubbleOutline.effectColor = palette.Accent;
            view.Explanation.text = BattleCardTooltipText(data, index);
            bool right = index < count / 2;
            ConfigureReadableCardBubble(view.Bubble, view.Explanation, view.Title, right);
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
