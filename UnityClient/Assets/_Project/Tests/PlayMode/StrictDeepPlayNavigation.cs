using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation.Tests
{
    public sealed partial class CoreGameplayDemoSceneTests
    {
        private sealed partial class StrictDeepDriver
        {
            private int strictEncounterSequence;
            private int strictBattleSequence;
            private bool strictChartClickAccepted;
            private readonly HashSet<string> strictObservedFragmentNames = new HashSet<string>();
            private readonly HashSet<string> strictObservedClueNames = new HashSet<string>();
            private CoreVoyageMap strictKnownTreasureMap;
            private CoreEncounter strictKnownTreasureCore;

            // All state changes go through visible UI. In particular, surveying a
            // chart never reads an undisclosed node's position, identity or choices.
            public IEnumerator Navigate(string goal)
            {
                if (goal != "trade" && goal != "explore" && goal != "treasure" && goal != "legendary")
                    throw new ArgumentException("Unknown strict navigation goal: " + goal);
                var rejected = new HashSet<string>();
                var encounterVisits = new Dictionary<string, int>();
                int steps = 0;
                for (; steps < 1600 && Session.Phase != CoreDemoPhase.Settlement && Session.Phase != CoreDemoPhase.Port; steps++)
                {
                    if (Session.Phase == CoreDemoPhase.Battle)
                    {
                        yield return Fight();
                        if (Session.Phase == CoreDemoPhase.Battle) break;
                        continue;
                    }
                    if (Session.Phase == CoreDemoPhase.Encounter)
                    {
                        string key = Session.ActiveEncounter.Id + ":" + Session.Position.X.ToString("F1") + ":" + Session.Position.Y.ToString("F1");
                        encounterVisits.TryGetValue(key, out int visits);
                        encounterVisits[key] = visits + 1;
                        int visitLimit = Session.IsTreasureExpedition && StrictIsMoonlitAltar(Session.ActiveEncounter) ? 10 : 6;
                        if (visits >= visitLimit)
                        {
                            Record("Unresolved encounter reached " + visitLimit + " visits: " + key +
                                " / " + Session.TreasureExpeditionStatusSummary);
                            Shot("Strict_Encounter_Stuck");
                            if (StrictEnabled("Emergency Encounter Return")) yield return Click("Emergency Encounter Return");
                            break;
                        }
                        yield return StrictResolveEncounter(goal);
                        continue;
                    }
                    if (Session.Phase != CoreDemoPhase.Sailing || Session.Map == null)
                    { Record("Navigation stopped in unexpected phase: " + Session.Phase); break; }

                    // A real return is preferable to replacing depleted provisions.
                    // Exploration deliberately does not stop at four fragments.
                    StrictNavigationTarget target = StrictChooseTarget(goal, rejected);
                    bool approachingReadyCore = target != null && target.core;
                    if (!approachingReadyCore && (Session.WaterSupplyHours < 12 || Session.FoodSupplyHours < 12 || Session.Hull < Math.Max(10, Session.MaxHull / 7)))
                    {
                        Record("Return on public supply/hull reserve: " + goal);
                        yield return Click("Safe Return");
                        continue;
                    }
                    if (target == null)
                    {
                        Record("No further reachable public chart target: " + goal);
                        yield return Click("Safe Return");
                        continue;
                    }
                    if (target.button != null) yield return Click(target.button);
                    else
                    {
                        yield return StrictClickChartPoint(target.point);
                        if (!strictChartClickAccepted) { rejected.Add(target.key); continue; }
                    }
                    if (Session.Phase != CoreDemoPhase.Sailing) continue;
                    SailingForecast forecast = Session.GetForecast();
                    if (forecast == null || forecast.Blocked || forecast.ActualDistanceNm < 1)
                    {
                        rejected.Add(target.key);
                        Record("Rejected public route preview: " + target.key);
                        continue;
                    }
                    if (goal == "treasure" && !StrictCoreReady() && StrictRouteCrossesVisibleCore(forecast))
                    {
                        rejected.Add(target.key);
                        Record("Visible route crosses the locked treasure X; choose another public waypoint.");
                        continue;
                    }
                    Record("Public route " + target.key + ": " + forecast.ActualDistanceNm.ToString("F1") +
                        " nm / " + forecast.Hours.ToString("F1") + " h / water " + forecast.WaterCost + " / food " + forecast.FoodCost);
                    int openingRations = approachingReadyCore ? (int)Math.Ceiling(
                        Session.EstimateDailyRationConsumption(Session.EffectiveCrewCount, Session.HasCook) * StrictCoreOpeningHours() / 24d) : 0;
                    if (!StrictCanFundRouteAndReturn(Session.Cargo.Water, Session.Cargo.Food,
                        forecast.WaterCost, forecast.FoodCost, openingRations))
                    {
                        Record("Cannot fund displayed route plus opening rations=" + openingRations +
                            " and the normal return reserve (water 2 / food 1); return before spending the reserve.");
                        yield return Click("Safe Return");
                        continue;
                    }
                    VoyagePoint previous = Session.Position;
                    yield return Click("Execute Sail");
                    float distance = StrictDistance(previous, Session.Position);
                    if (Session.Phase == CoreDemoPhase.Sailing && distance < .01f)
                    { rejected.Add(target.key); Record("Sailing did not advance: " + target.key); }
                    else if (distance > 2f) rejected.Clear();
                    if (steps % 24 == 0 && Session.Phase == CoreDemoPhase.Sailing)
                        Shot("Strict_" + goal + "_Navigation_" + steps, "Navigation Chart");
                }
                if (steps >= 1600 && Session.Phase == CoreDemoPhase.Sailing)
                {
                    Record("Navigation action budget reached; choose normal return.");
                    yield return Click("Safe Return");
                }
                Record("Navigation finished: " + goal + " / " + Session.Phase +
                    (Session.Settlement == null ? "" : " / " + Session.Settlement.Outcome));
                if (Session.TotalDistanceNm < 1f) Record("NO VOYAGE DISTANCE: this attempt must not count as a completed exploration or trade route.");
                Shot("Strict_" + goal + "_Navigation_Result");
            }

            private StrictNavigationTarget StrictChooseTarget(string goal, HashSet<string> rejected)
            {
                CoreVoyageMap map = Session.Map;
                var visible = new List<StrictNavigationTarget>();
                bool fullCoreReady = goal == "treasure" && StrictCoreReady();
                foreach (CoreMapNode node in map.Nodes)
                {
                    Button marker = StrictButton("Event Node " + node.Id);
                    if (marker == null || !marker.IsInteractable()) continue;
                    // These exact glyphs and names are rendered on the public chart,
                    // including the treasure X, which is visible through the fog.
                    Text glyphText = marker.GetComponentInChildren<Text>();
                    Text labelText = GameObject.Find("Event Label " + node.Id)?.GetComponent<Text>();
                    string glyph = glyphText == null ? "" : glyphText.text;
                    string label = labelText == null ? "" : labelText.text;
                    bool readyCore = goal == "treasure" && glyph == "X" && fullCoreReady;
                    // Discovered named landmarks are public; an intel-only signal
                    // must never disclose its encounter or choices to this policy.
                    double progress = goal == "treasure" && Session.IsTreasureExpedition && node.Discovered && glyph != "X"
                        ? node.Encounter.Choices.Max(choice => StrictExpeditionProgressScore(choice.Effect)) : 0;
                    if (node.Visited || rejected.Contains("node:" + node.Id)) continue;
                    if (node.Skipped && !readyCore && !(goal == "treasure" && Session.IsTreasureExpedition)) continue;
                    double priority;
                    if (goal == "trade")
                    { if (glyph != "首" || Session.BossDefeated) continue; priority = 100; }
                    else if (goal == "legendary")
                    { if (glyph != "传") continue; priority = 100; }
                    else if (goal == "treasure")
                    {
                        if (glyph == "X" && !readyCore) continue;
                        if (Session.IsTreasureExpedition)
                        {
                            if (fullCoreReady && !readyCore) continue;
                            // Optional legendary fights are not required for the
                            // first full vault. Needed public supplies outrank a
                            // detour only while the vault remains locked.
                            if (glyph == "传" || glyph == "藏" || glyph == "首") continue;
                            bool needsRations = Math.Min(Session.WaterSupplyHours, Session.FoodSupplyHours) < 48d;
                            bool supply = !fullCoreReady && needsRations && node.Discovered &&
                                node.Encounter.Choices.Any(choice => !choice.StartsBattle && choice.Effect.TreasureHeat <= 0 &&
                                    choice.Effect.Curse <= 0 && (choice.Effect.Water > 0 || choice.Effect.Food > 0) &&
                                    Session.EncounterChoiceConditionReason(choice).Length == 0);
                            if (node.Discovered && !readyCore && progress <= 0 && !supply) continue;
                            priority = readyCore ? 1000000 : supply ? 500000 : progress > 0 ? progress : 10;
                        }
                        else priority = readyCore ? 1000 : strictObservedClueNames.Contains(label) ? 100 : 10;
                    }
                    else
                    {
                        if (glyph == "传" || glyph == "藏" || glyph == "首") continue;
                        // Names of previously observed fragment events are useful
                        // knowledge. Unknown intel signals disclose no hidden name.
                        priority = strictObservedFragmentNames.Contains(label) || label == "暗流推船" || label == "潮痕秘藏" ? 100 : 10;
                    }
                    visible.Add(new StrictNavigationTarget { key = "node:" + node.Id, button = marker.name,
                        point = node.Position, priority = priority, core = readyCore });
                }
                StrictNavigationTarget selected = visible.OrderByDescending(t => t.priority)
                    .ThenBy(t => StrictDistance(Session.Position, t.point)).FirstOrDefault();
                if (selected != null) return selected;
                if (goal == "trade" && !rejected.Contains("destination") &&
                    (Session.BossDefeated || StrictDistance(Session.Position, map.Destination) > 5f))
                    return new StrictNavigationTarget { key = "destination", button = "Destination Port", point = map.Destination };

                int UnknownNeighbours(int cell, int radius)
                {
                    int count = 0, column = cell % CoreVoyageMap.GridColumns, row = cell / CoreVoyageMap.GridColumns;
                    for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
                    {
                        int x = column + dx, y = row + dy;
                        if (x > 0 && x < CoreVoyageMap.GridColumns - 1 && y > 0 && y < CoreVoyageMap.GridRows - 1 &&
                            !map.ExploredCells[y * CoreVoyageMap.GridColumns + x]) count++;
                    }
                    return count;
                }
                // IsPassable is read only after the corresponding terrain cell has
                // been explored. Unknown probes below inspect coordinates alone.
                var frontier = Enumerable.Range(0, map.CellCount)
                    .Where(i => map.ExploredCells[i] && !rejected.Contains("cell:" + i))
                    .Where(i => map.IsPassable(i % CoreVoyageMap.GridColumns, i / CoreVoyageMap.GridColumns) && UnknownNeighbours(i, 1) > 0)
                    .Select(i => new StrictNavigationTarget { key = "cell:" + i,
                        point = map.CellCenter(i % CoreVoyageMap.GridColumns, i / CoreVoyageMap.GridColumns), priority = UnknownNeighbours(i, 3) })
                    .Where(t => StrictInterior(t.point) && StrictDistance(Session.Position, t.point) > 2f)
                    .OrderByDescending(t => t.priority / (2f + StrictDistance(Session.Position, t.point) +
                        (goal == "trade" ? StrictDistance(t.point, map.Destination) * 2f : 0f))).FirstOrDefault();
                if (frontier != null) return frontier;
                return Enumerable.Range(0, map.CellCount)
                    .Where(i => !map.ExploredCells[i] && !rejected.Contains("cell:" + i))
                    .Select(i => new StrictNavigationTarget { key = "cell:" + i,
                        point = map.CellCenter(i % CoreVoyageMap.GridColumns, i / CoreVoyageMap.GridColumns) })
                    .Where(t => StrictInterior(t.point) && StrictDistance(Session.Position, t.point) > 2f)
                    .OrderBy(t => StrictDistance(Session.Position, t.point) + (goal == "trade" ? StrictDistance(t.point, map.Destination) * 2f : 0f))
                    .FirstOrDefault();
            }

            private CoreEncounter StrictVisibleTreasureCore()
            {
                if (strictKnownTreasureMap != Session.Map)
                { strictKnownTreasureMap = Session.Map; strictKnownTreasureCore = null; }
                if (!Session.IsTreasureVoyage || Session.Map == null) return null;
                if (Session.ActiveEncounter?.Id == CoreTreasureHuntCatalog.CoreEncounterId)
                    strictKnownTreasureCore = Session.ActiveEncounter;
                if (strictKnownTreasureCore != null) return strictKnownTreasureCore;
                foreach (CoreMapNode node in Session.Map.Nodes)
                {
                    if (!node.Discovered) continue;
                    Button marker = StrictButton("Event Node " + node.Id);
                    if (marker == null || marker.GetComponentInChildren<Text>()?.text != "X") continue;
                    strictKnownTreasureCore = node.Encounter;
                    break;
                }
                return strictKnownTreasureCore;
            }

            private CoreEncounterChoice StrictCoreOpeningChoice()
            {
                CoreEncounter core = StrictVisibleTreasureCore();
                if (core == null) return null;
                // Current expeditions expose the complete vault first. Partial
                // salvage is a legal escape, but does not satisfy this acceptance.
                IEnumerable<CoreEncounterChoice> candidates = Session.IsTreasureExpedition ? core.Choices.Take(1) :
                    core.Choices.Where(choice => choice.Effect.TreasureComplete)
                        .OrderByDescending(choice => choice.RequiredCrew == "diver")
                        .ThenByDescending(choice => choice.Effect.Powder >= 0);
                return candidates.FirstOrDefault(choice => choice.Effect.TreasureComplete &&
                    Session.EncounterChoiceConditionReason(choice).Length == 0 &&
                    Session.TreasureClues >= choice.RequiredTreasureClues && Session.Hull + choice.Effect.Hull > 0 &&
                    Session.Cargo.Powder + choice.Effect.Powder >= 0);
            }

            private bool StrictCoreReady() => StrictCoreOpeningChoice() != null;
            private double StrictCoreOpeningHours() => StrictCoreOpeningChoice()?.Hours ?? 0d;

            private static bool StrictIsMoonlitAltar(CoreEncounter encounter) => encounter != null &&
                (encounter.Id == "moonlit_dawn" || encounter.Id == "moonlit_dusk" || encounter.Id == "moonlit_midnight");

            private double StrictExpeditionProgressScore(VoyageEffect effect) => StrictTreasureProgressScore(
                StrictVisibleTreasureCore()?.Choices.FirstOrDefault(), effect, Session.HasTreasureMark,
                Session.TreasureExpeditionCredit, Session.TreasureExpeditionHeat, Session.TreasureExpeditionResonance, Session.Cargo.Curse);

            private bool StrictRouteCrossesVisibleCore(SailingForecast forecast)
            {
                // Only inspect the position represented by the public X. Neither
                // undiscovered event identities nor their coordinates guide this route.
                foreach (CoreMapNode node in Session.Map.Nodes)
                {
                    Button marker = StrictButton("Event Node " + node.Id);
                    if (marker == null || marker.GetComponentInChildren<Text>()?.text != "X") continue;
                    Vector2 core = new Vector2(node.Position.X, node.Position.Y);
                    for (int i = 1; i < forecast.PathPoints.Count; i++)
                    {
                        Vector2 start = new Vector2(forecast.PathPoints[i - 1].X, forecast.PathPoints[i - 1].Y);
                        Vector2 end = new Vector2(forecast.PathPoints[i].X, forecast.PathPoints[i].Y);
                        Vector2 segment = end - start;
                        float t = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(core - start, segment) / segment.sqrMagnitude) : 0f;
                        // Leaving an already inspected core is allowed; approaching
                        // it from elsewhere before unlocking it is avoidable.
                        if (Vector2.Distance(start, core) > 1.5f && Vector2.Distance(start + t * segment, core) < 1.5f) return true;
                    }
                }
                return false;
            }

            private IEnumerator StrictClickChartPoint(VoyagePoint point)
            {
                strictChartClickAccepted = false;
                GameObject surface = GameObject.Find("Map Course Input");
                if (surface == null || EventSystem.current == null) yield break;
                Canvas.ForceUpdateCanvases();
                RectTransform rect = surface.GetComponent<RectTransform>();
                Canvas canvas = surface.GetComponentInParent<Canvas>().rootCanvas;
                Vector2 local = new Vector2(Mathf.Lerp(-580f, 580f, point.X / CoreVoyageMap.Width),
                    Mathf.Lerp(-315f, 315f, point.Y / CoreVoyageMap.Height));
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                        rect.TransformPoint(rect.rect.center + local)) };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0) yield break;
                GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                if (handler == null || !handler.transform.IsChildOf(surface.transform.parent)) yield break;
                Button button = handler.GetComponent<Button>();
                if (button != null && !button.IsInteractable()) yield break;
                strictChartClickAccepted = true;
                string name = handler.name;
                ExecuteEvents.Execute(handler, pointer, ExecuteEvents.pointerClickHandler);
                yield return null;
                Record("Actual chart pointer hit: " + name + " at " + point.X.ToString("F1") + "," + point.Y.ToString("F1"));
            }

            private IEnumerator StrictResolveEncounter(string goal)
            {
                yield return WaitForEncounterDossierPresentationCompletion();
                CoreEncounter encounter = Session.ActiveEncounter;
                Shot("Strict_Encounter_" + strictEncounterSequence++ + "_" + encounter.Id, "Encounter Card");
                int selected = -1;
                double best = double.NegativeInfinity;
                bool challenge = goal == "trade" && encounter.BossKind == CoreBossKind.Route ||
                    goal == "legendary" && encounter.BossKind == CoreBossKind.Legendary;
                bool expedition = goal == "treasure" && Session.IsTreasureExpedition;
                bool core = encounter.Id == CoreTreasureHuntCatalog.CoreEncounterId;
                bool ritual = expedition && StrictIsMoonlitAltar(encounter);
                bool fullCoreBlockedByCost = expedition && core && StrictCoreReady() && !Session.IsChoiceAvailable(0);
                if (expedition) Record("Public expedition state: " + Session.TreasureExpeditionStatusSummary);
                if (expedition && core && !Session.IsChoiceAvailable(0))
                    Record("Complete vault is unavailable: " + Session.ChoiceUnavailableReason(0));
                for (int index = 0; index < encounter.Choices.Count; index++)
                {
                    // The encounter is open; its reward/cost choices are now public.
                    VoyageEffect effect = Session.EffectiveChoiceEffect(index);
                    if (effect == null) continue;
                    if (effect.MapFragments > 0) strictObservedFragmentNames.Add(encounter.Name);
                    if (effect.TreasureClues > 0) strictObservedClueNames.Add(encounter.Name);
                    if (!Session.IsChoiceAvailable(index) || !StrictEnabled("Encounter Choice " + index)) continue;
                    CoreEncounterChoice choice = encounter.Choices[index];
                    double score = effect.MapFragments * 10000 + effect.TreasureClues * 5000 + effect.Reveal * 150 +
                        effect.Gold * .15 + effect.Hull * 4 + effect.Water + effect.Food - choice.Hours * 3 -
                        (effect.ForceReturn ? 100000 : 0) + (choice.StartsBattle ? challenge ? 1000000 : -2000 : 0);
                    if (expedition)
                    {
                        score += StrictExpeditionProgressScore(effect) - Math.Max(0, effect.TreasureHeat) * 200000 -
                            Math.Max(0, effect.Curse) * 200000 - Math.Max(0, -effect.TreasureCredit) * 200000;
                        if (core)
                        {
                            // Never accept a partial completion to make a failed
                            // full-vault attempt appear successful.
                            if (index == 0) score = 1000000;
                            else if (effect.PostponeTreasureCore) score = 900000;
                            else continue;
                        }
                        else if (ritual)
                        {
                            if (index == 0) score = 1000000;
                            else if (effect.PostponeTreasureCore) score = 900000;
                            else continue;
                        }
                    }
                    else if (core)
                        score = choice == StrictCoreOpeningChoice() ? 1000000 : effect.PostponeTreasureCore ? 900000 : -1000000;
                    if (score > best) { best = score; selected = index; }
                }
                if (!challenge && StrictEnabled("Skip Encounter") && (selected < 0 || best <= 0))
                { Record("Skip unhelpful public encounter: " + encounter.Name); yield return Click("Skip Encounter"); yield break; }
                if (selected >= 0)
                {
                    int fragments = Session.VoyageTreasureFragments, clues = Session.TreasureClues;
                    Record("Public choice: " + encounter.Name + " / " + encounter.Choices[selected].Label + " / reward multiplier " + Session.ActiveEncounterRewardMultiplier);
                    yield return Click("Encounter Choice " + selected);
                    Record("Observed encounter gain: fragments " + (Session.VoyageTreasureFragments - fragments) + ", clues " + (Session.TreasureClues - clues));
                    if (expedition) Record("Observed expedition result: " + Session.TreasureExpeditionStatusSummary);
                    if (fullCoreBlockedByCost && Session.Phase == CoreDemoPhase.Sailing)
                    {
                        Record("Full vault prerequisites earned but the real opening cost/capacity is unavailable; return without fallback completion.");
                        if (StrictEnabled("Safe Return")) yield return Click("Safe Return");
                    }
                    yield break;
                }
                if (StrictEnabled("Emergency Encounter Return")) yield return Click("Emergency Encounter Return");
                else if (StrictEnabled("Skip Encounter")) yield return Click("Skip Encounter");
                else Record("No legal visible encounter action: " + encounter.Name);
            }

            public IEnumerator Fight()
            {
                if (NoGmFirstTreasure) { yield return FightNoGmCannon(); yield break; }
                CoreBattleState fought = Session.Battle;
                int battleNumber = strictBattleSequence++;
                string lastTurn = "";
                int actions = 0;
                const int actionBudget = 600;
                while (Session.Phase == CoreDemoPhase.Battle && actions++ < actionBudget)
                {
                    CoreBattleState battle = Session.Battle;
                    string turn = battle.Stage + "_" + battle.Turn;
                    if (turn != lastTurn)
                    {
                        lastTurn = turn;
                        Record("Public battle intent: " + Session.GetEnemyIntent());
                        Shot("Strict_Battle_" + battleNumber + "_" + turn);
                    }
                    if (battle.Stage == NavalBattleStage.Preparation && battle.DistanceMeters <= 60 && battle.Alignment == NavalBattleAlignment.Parallel) { yield return Click("End Battle Turn"); continue; }
                    if (StrictEnabled("Battle Board")) { yield return Click("Battle Board"); continue; }
                    int selected = -1;
                    double best = 0;
                    // Read the displayed hand's current fees. Repeated copies are
                    // progressively dearer until the next turn or stage transition.
                    for (int hand = 0; hand < battle.Hand.Count; hand++)
                    {
                        if (!Session.CanPlayCard(hand) || !StrictEnabled("Battle Card " + hand)) continue;
                        double score = StrictCardScore(battle.Hand[hand]);
                        if (score > best) { best = score; selected = hand; }
                    }
                    if (Session.EnemyWillRam && StrictEnabled("Battle Ram") && Session.Hull > 30 && best < 450)
                    {
                        Record("Enemy announces ram: take the visible ram action first while hull can sustain recoil.");
                        yield return Click("Battle Ram");
                    }
                    else if (selected >= 0)
                    {
                        int libraryIndex = battle.Hand[selected];
                        Record("Choose public battle card " + Session.GetBattleCard(libraryIndex).Name +
                            "; actual fee=" + Session.GetBattleCardEnergyCost(libraryIndex) + "; energy=" + battle.Energy);
                        yield return Click("Battle Card " + selected);
                    }
                    else if (StrictEnabled("End Battle Turn")) yield return Click("End Battle Turn");
                    else if (StrictEnabled("Emergency Battle Return")) { yield return Click("Emergency Battle Return"); break; }
                    else { Record("No legal visible battle action."); break; }
                }
                if (Session.Phase == CoreDemoPhase.Battle && actions >= actionBudget)
                {
                    Record("Battle legal-action budget reached; request ordinary retreat when allowed.");
                    if (StrictEnabled("Emergency Battle Return")) yield return Click("Emergency Battle Return");
                    else if (StrictEnabled("Retreat Battle")) yield return Click("Retreat Battle");
                }
                // A real defeat stays a failure settlement; never inject health or
                // an enemy defeat, and never assert that every fight must be won.
                bool defeated = Session.Hull <= 0 || fought != null && fought.PlayerBoardingHealth <= 0;
                if (defeated) ConsecutiveDefeats++;
                else if (fought != null && (fought.EnemyHull <= 0 || fought.EnemyBoardingHealth <= 0)) ConsecutiveDefeats = 0;
                Record("Battle ended after " + actions + " actions: " + Session.Phase +
                    (Session.Settlement == null ? "" : " / " + Session.Settlement.Outcome));
                Shot("Strict_Battle_" + battleNumber + "_Result");
            }

            private double StrictCardScore(int libraryIndex)
            {
                CoreBattleCard card = Session.GetBattleCard(libraryIndex);
                CoreBattleState battle = Session.Battle;
                bool boarding = battle.Stage == NavalBattleStage.Boarding;
                bool preparing = battle.Stage == NavalBattleStage.Preparation;
                int health = boarding ? battle.PlayerBoardingHealth : Session.Hull;
                int maximum = boarding ? battle.PlayerMaxBoardingHealth : Session.MaxHull;
                int enemy = boarding ? battle.EnemyBoardingHealth : battle.EnemyHull;
                Match intent = Regex.Match(Session.GetEnemyIntent() ?? "", "(?:炮击：伤害 |白刃突击：船员伤害 )(\\d+)");
                int incoming = intent.Success ? int.Parse(intent.Groups[1].Value) : 0;
                bool enemyRam = Session.EnemyWillRam;
                int neededBlock = Math.Max(0, (preparing ? 10 : incoming) - battle.PlayerBlock);
                int repair = Math.Min(Math.Max(0, maximum - health), card.Repair);
                int block = Math.Min(neededBlock, card.Block + (card.Block > 0 && !boarding ? battle.PlayerDeckBuff : 0));
                int actualFee = Session.GetBattleCardEnergyCost(libraryIndex);
                double divisor = actualFee + .75;
                int unblocked = Math.Max(0, incoming - battle.PlayerBlock);
                double score = repair * (health < maximum * .6 ? 2.4 : .7) + block * (health <= unblocked * 2 ? 2.8 : 1.4);
                int damage = Session.GetBattleCardDamage(card, battle.Stage);
                if (damage > 0 && damage >= enemy + battle.EnemyBlock) return 10000 / divisor;
                if (health <= unblocked && repair + block > 0)
                    score += 500 + Math.Min(unblocked - health + 1, repair + block) * 20;
                score += damage * (boarding ? 1.8 : preparing ? 0 : 1.3);
                if (card.Draw > 0 && battle.Hand.Count < 8)
                    score += card.Draw * (battle.Hand.Count < 4 ? 7 : 2);
                switch (card.Action)
                {
                    case NavalBattleCardAction.Polish:
                        if (battle.PlayerDeckBuff < card.StrategyLimit) score += preparing ? 18 : 7;
                        break;
                    case NavalBattleCardAction.Load:
                    case NavalBattleCardAction.PrecisionLoad:
                        if (battle.PlayerLoadedShot < card.StrategyLimit && Session.Cargo.Cannonballs > 0)
                            score += Math.Min(card.StrategyAmount, card.StrategyLimit - battle.PlayerLoadedShot) * 1.1;
                        break;
                    case NavalBattleCardAction.FireRounds:
                        if (battle.PlayerFireRounds < card.StrategyLimit && Session.Cargo.Cannonballs > 0)
                            score += card.BurnDamage * 1.4;
                        break;
                    case NavalBattleCardAction.Approach:
                    case NavalBattleCardAction.Circle:
                    case NavalBattleCardAction.Turn:
                        if (battle.DistanceMeters > 60) score += Math.Min(battle.DistanceMeters - 60, card.MovementMeters) * .12 + 6;
                        else if (battle.Alignment == NavalBattleAlignment.HeadOn && card.Action != NavalBattleCardAction.Approach) score += 5;
                        if (enemyRam) score -= 20;
                        break;
                    case NavalBattleCardAction.Navigator:
                    case NavalBattleCardAction.Parallel:
                        if (battle.DistanceMeters > 60) score += Math.Min(battle.DistanceMeters - 60, card.MovementMeters) * .12 + 8;
                        if (battle.Alignment != NavalBattleAlignment.Parallel) score += 15;
                        if (!boarding && battle.DistanceMeters - card.MovementMeters <= 60)
                            score += enemyRam ? 2000 : 50;
                        break;
                    case NavalBattleCardAction.Retreat:
                        if (enemyRam && battle.DistanceMeters + card.MovementMeters > Session.EnemyRamRangeMeters)
                            score += Session.Hull <= 30 ? 1200 : 700;
                        else return 0;
                        break;
                }
                return score / divisor;
            }

            private static Button StrictButton(string name) => GameObject.Find(name)?.GetComponent<Button>();
            private static bool StrictEnabled(string name) => StrictButton(name)?.IsInteractable() == true;
            private static float StrictDistance(VoyagePoint a, VoyagePoint b) => Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));
            private static bool StrictInterior(VoyagePoint p) => p.X >= 2 && p.X <= 98 && p.Y >= 2 && p.Y <= 62;
            private sealed class StrictNavigationTarget
            {
                public string key, button;
                public VoyagePoint point;
                public double priority;
                public bool core;
            }
        }
    }
}



