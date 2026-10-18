using System;
using System.Linq;
using System.Reflection;
using ShatteredSail.Content;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public enum GameplayFlowPreviewState
    {
        None, RecruitmentOffer, ContractSigned, UpgradePreview, UpgradeComplete,
        TreasureDeparture, TreasureNavigation, TreasureClue, TreasureCoreLocked,
        TreasureCoreReady, TreasureSettlement,
    }

    public sealed partial class CoreGameplayDemoController
    {
        public void ConfigureEditorFlowState(GameplayFlowPreviewState state)
        {
            if (Application.isPlaying) return;
            editorPreviewFlowState = state;
            switch (state)
            {
                case GameplayFlowPreviewState.RecruitmentOffer:
                case GameplayFlowPreviewState.ContractSigned: editorPreviewScreen = CoreGameplayPreviewScreen.TavernCrewDetail; break;
                case GameplayFlowPreviewState.UpgradePreview:
                case GameplayFlowPreviewState.UpgradeComplete: editorPreviewScreen = CoreGameplayPreviewScreen.CardUpgrade; break;
                case GameplayFlowPreviewState.TreasureDeparture: editorPreviewScreen = CoreGameplayPreviewScreen.VoyageDeparture; break;
                case GameplayFlowPreviewState.TreasureNavigation: editorPreviewScreen = CoreGameplayPreviewScreen.Navigation; break;
                case GameplayFlowPreviewState.TreasureClue:
                case GameplayFlowPreviewState.TreasureCoreLocked:
                case GameplayFlowPreviewState.TreasureCoreReady: editorPreviewScreen = CoreGameplayPreviewScreen.Encounter; break;
                case GameplayFlowPreviewState.TreasureSettlement: editorPreviewScreen = CoreGameplayPreviewScreen.Settlement; break;
            }
            if (isActiveAndEnabled) RebuildEditorPreview();
        }

        private bool IsRecruitmentFlowPreview => !Application.isPlaying &&
            (editorPreviewFlowState == GameplayFlowPreviewState.RecruitmentOffer || editorPreviewFlowState == GameplayFlowPreviewState.ContractSigned);

        private bool IsCrewRecruitedForView(CrewVariantDefinition crew) => IsRecruitmentFlowPreview
            ? editorPreviewFlowState == GameplayFlowPreviewState.ContractSigned && crew.ProfessionIndex == 1
            : IsCrewRecruited(crew);

        private bool BuildFlowStateEditorPreview()
        {
            if (Application.isPlaying || editorPreviewFlowState == GameplayFlowPreviewState.None) return false;
            session.RestoreProfile(1200, PreviewPortName(editorPreviewPort));
            portNotice = "编辑样本 · 金币、契约、强化与探险进度均不会写入玩家存档。";
            if (IsRecruitmentFlowPreview)
            {
                activePortFacility = 2;
                selectedTavernCandidate = 1;
                tavernDossierCrewId = CurrentTavernCrew(1).Id;
                tavernDossierPage = 2;
                if (editorPreviewFlowState == GameplayFlowPreviewState.ContractSigned)
                    session.TrySpendProfileGold(CurrentTavernCrew(1).ContractCost, "编辑样本契约");
                BuildPort();
            }
            else if (editorPreviewFlowState == GameplayFlowPreviewState.UpgradePreview || editorPreviewFlowState == GameplayFlowPreviewState.UpgradeComplete)
            {
                activePortFacility = 4;
                selectedBattleDeckStage = NavalBattleStage.Preparation;
                selectedBattleCardUpgrade = session.GetAvailableBattleCards(selectedBattleDeckStage).First(index =>
                    string.IsNullOrEmpty(session.GetBattleCard(index).RequiredCrew) && string.IsNullOrEmpty(session.GetBattleCard(index).RequiredComponent));
                if (editorPreviewFlowState == GameplayFlowPreviewState.UpgradeComplete)
                {
                    if (!session.TryUpgradeBattleCard(selectedBattleCardUpgrade, out string result)) throw new InvalidOperationException(result);
                    portNotice = "编辑样本 · " + result;
                }
                cardWorkshopDeckBuilderOpen = true;
                battleCardUpgradeOpen = true;
                BuildPort();
            }
            else
            {
                const string seed = "TREASURE-FLOW-PREVIEW-01";
                session.RestoreTreasureProgress(new CoreTreasureProgress {
                    pirateExperience = 100,
                    selectedMapId = "flow-map",
                    maps = new[] { new CoreTreasureMapSave { id = "flow-map", seed = seed, rankId = "weathered" } },
                    issuedSeeds = new[] { seed },
                });
                session.Cargo.Water = 180; session.Cargo.Food = 180;
                // This noncombat sample needs room for 19 units of recovered
                // machinery (10 materials, a compass and a lantern). Carrying
                // the former 20-powder breach loadout filled the 80-unit sloop
                // before the real complete-salvage choice could be resolved.
                session.Cargo.Wood = 8; session.Cargo.Iron = 0; session.Cargo.Powder = 8;
                if (editorPreviewFlowState == GameplayFlowPreviewState.TreasureDeparture)
                {
                    activePortFacility = PortFacilityArtCatalog.VoyageFacilityIndex;
                    BuildPort();
                }
                else
                {
                    if (!session.TryStartTreasureVoyage(out string reason)) throw new InvalidOperationException(reason);
                    if (editorPreviewFlowState != GameplayFlowPreviewState.TreasureNavigation)
                    {
                        bool clue = editorPreviewFlowState == GameplayFlowPreviewState.TreasureClue;
                        if (editorPreviewFlowState == GameplayFlowPreviewState.TreasureCoreReady || editorPreviewFlowState == GameplayFlowPreviewState.TreasureSettlement)
                        {
                            // Stage the two guaranteed landmarks, then earn the tools
                            // through their real material-free repair choices. No legacy
                            // clue counter or expedition progress is injected here.
                            foreach (string id in new[] { "weathered_winch", "weathered_bell" })
                            {
                                StageFlowPreviewEncounter(session.Map.Nodes.Single(item => item.Encounter.Id == id));
                                if (!session.ResolveChoice(2))
                                    throw new InvalidOperationException("The treasure tool fixture could not repair " + id + ": " + session.ChoiceUnavailableReason(2));
                            }
                        }
                        CoreMapNode node = session.Map.Nodes.Single(item => item.Encounter.Id ==
                            (clue ? "weathered_winch" : CoreTreasureHuntCatalog.CoreEncounterId));
                        StageFlowPreviewEncounter(node);
                        if (editorPreviewFlowState == GameplayFlowPreviewState.TreasureSettlement && !session.ResolveChoice(0))
                            throw new InvalidOperationException("The treasure settlement fixture could not open the core: " + session.ChoiceUnavailableReason(0));
                    }
                    if (editorPreviewFlowState == GameplayFlowPreviewState.TreasureSettlement)
                        BuildSettlementPresentation(session.Settlement, true);
                    else
                    {
                        BuildSailing();
                        if (session.Phase == CoreDemoPhase.Encounter) BuildEncounterOverlay();
                    }
                }
            }
            TextPreviewSampleLabel();
            return true;
        }

        private void StageFlowPreviewEncounter(CoreMapNode node)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Editor fixtures cannot stage a live voyage.");
            // Deterministic editor staging only. Live validation reaches these
            // landmarks by sailing and uses the same production choice resolver.
            SetFlowPreviewValue("Position", node.Position);
            SetFlowPreviewValue("ActiveEncounter", node.Encounter);
            SetFlowPreviewValue("Phase", CoreDemoPhase.Encounter);
            typeof(CoreVoyageSession).GetField("activeNode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(session, node);
            node.Discovered = true;
            typeof(CoreVoyageSession).GetMethod("RevealAt", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, new object[] { node.Position });
        }

        private void SetFlowPreviewValue(string name, object value)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Editor fixtures cannot change a live voyage.");
            typeof(CoreVoyageSession).GetProperty(name).GetSetMethod(true).Invoke(session, new[] { value });
        }

        private void TextPreviewSampleLabel()
        {
            if (editorPreviewFlowState == GameplayFlowPreviewState.TreasureSettlement) return;
            var label = CreateText("Flow Preview Sample Badge", contentRoot, new Vector2(1420f, 22f), new Vector2(0f, 414f),
                14, FontStyle.Normal, TextAnchor.MiddleCenter, Gold);
            label.text = "编辑样本 · " + editorPreviewFlowState + " · 不写入玩家金币、契约或探险档案";
            label.raycastTarget = false;
        }
    }
}
