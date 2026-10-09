using System.Collections;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private static readonly string[] RamComicResources =
        {
            "BattleComics/ram_player_head_on_v1", "BattleComics/ram_player_broadside_v1",
            "BattleComics/ram_boss_head_on_v1", "BattleComics/ram_boss_broadside_v1",
        };
        private readonly Texture2D[] ramComicTextures = new Texture2D[4];
        private readonly bool[] ramComicLoadAttempted = new bool[4];
        private RectTransform ramComicOverlay;
        private RawImage ramComicPage;
        private Text ramComicCaption, ramComicStep;
        private string[] ramComicCaptions;
        private bool ramComicSkipRequested, ramComicResumeAfterPause;

        private Texture2D RamComicTexture(bool enemyInitiated, NavalBattleAlignment impactAlignment)
        {
            int index = (enemyInitiated ? 2 : 0) + (impactAlignment == NavalBattleAlignment.HeadOn ? 0 : 1);
            if (!ramComicLoadAttempted[index])
            {
                ramComicLoadAttempted[index] = true;
                ramComicTextures[index] = Resources.Load<Texture2D>(RamComicResources[index]);
            }
            return ramComicTextures[index];
        }

        private string RamComicResult(BattleAnimationSnapshot before, BattleAnimationSnapshot after, bool enemyInitiated, int priorBurnDamage)
        {
            if (session.Phase != CoreDemoPhase.Battle)
            {
                // Win/loss handling can already include repairs or settlement
                // effects. That net hull delta is not the impact's raw damage.
                bool lost = after.PlayerHull <= 0 || after.Stage == NavalBattleStage.Boarding && after.PlayerCrew <= 0 ||
                    session.Settlement != null && session.Settlement.Outcome == VoyageOutcome.Failure;
                return lost ? "我方失去战斗力" : "敌舰沉没";
            }
            string damage = "敌舰船体 -" + Mathf.Max(0, before.EnemyHull - after.EnemyHull - priorBurnDamage) +
                "　·　我方船体 -" + Mathf.Max(0, before.PlayerHull - after.PlayerHull);
            return damage + "\n" + (enemyInitiated
                ? "敌舰撞击接舷，先手造成船员生命 -" + Mathf.Max(0, before.PlayerCrew - after.PlayerCrew)
                : "船只已连接，转入接舷近战");
        }

        // Also used by isolated Editor captures via reflection. It builds the
        // same presentation without committing a battle action or touching saves.
        private RawImage BuildRamComicOverlay(Texture2D art, bool enemyInitiated, bool headOn, string resultSummary)
        {
            CloseRamComic();
            if (art == null || contentRoot == null) return null;
            ramComicSkipRequested = false;
            ramComicCaptions = new[]
            {
                enemyInitiated ? "敌舰加速，舰艏直指我方。" : "我舰加速，舰艏直指敌舰。",
                headOn ? "两舰舰艏迎头相撞！" : enemyInitiated ? "敌舰舰艏撞入我方侧舷！" : "我舰舰艏撞入敌方侧舷！",
                "钩索越过船舷，两舰被牢牢拉住。",
                resultSummary,
            };
            ramComicOverlay = CreateStretchPanel("Battle Ram Comic Overlay", contentRoot, new Color(.015f, .018f, .021f, .985f));
            RectTransform overlay = ramComicOverlay;
            // Lift above the hand's temporary hover canvases as well as block
            // the old view. Core callbacks remain protected by the action lock.
            Canvas canvas = overlay.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 120;
            overlay.gameObject.AddComponent<GraphicRaycaster>();
            overlay.GetComponent<Image>().raycastTarget = true;
            Text title = CreateText("Battle Ram Comic Title", overlay, new Vector2(1280f, 44f), new Vector2(0f, 488f),
                28, FontStyle.Bold, TextAnchor.MiddleCenter, Gold);
            title.text = (enemyInitiated ? "敌舰冲撞" : "主动冲撞") + " · " + (headOn ? "舰艏对舰艏" : "舰艏对侧舷");
            title.raycastTarget = false;
            ramComicPage = CreateRawImage("Battle Ram Comic Page", overlay, new Vector2(1260f, 840f),
                new Vector2(0f, 24f), art, new Rect(0, 0, 1, 1));
            ramComicPage.raycastTarget = false;
            ramComicCaption = CreateText("Battle Ram Comic Caption", overlay, new Vector2(1580f, 60f), new Vector2(0f, -439f),
                22, FontStyle.Normal, TextAnchor.MiddleCenter, Ink);
            ramComicCaption.raycastTarget = false;
            ramComicStep = CreateText("Battle Ram Comic Step", overlay, new Vector2(450f, 28f), new Vector2(0f, -502f),
                16, FontStyle.Normal, TextAnchor.MiddleCenter, MutedInk);
            ramComicStep.raycastTarget = false;
            Button skip = CreateButton("Battle Ram Comic Skip", overlay, new Vector2(180f, 46f), new Vector2(790f, 488f), PanelRaised, Ink, 18);
            skip.GetComponentInChildren<Text>().text = "跳过漫画";
            skip.onClick.AddListener(() =>
            {
                if (overlay == null || !isActiveAndEnabled || !battleAnimationPlaying || ramComicOverlay != overlay || !overlay.gameObject.activeInHierarchy) return;
                ramComicSkipRequested = true;
                skip.interactable = false;
            });
            SetRamComicPanel(0);
            if (Application.isPlaying && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(skip.gameObject);
            return ramComicPage;
        }

        private void SetRamComicPanel(int panel)
        {
            if (ramComicPage == null || ramComicPage.texture == null) return;
            panel = Mathf.Clamp(panel, 0, 4);
            Rect uv = panel == 4 ? new Rect(0, 0, 1, 1) :
                new Rect((panel % 2) * .5f + .01f, (panel < 2 ? .5f : 0f) + .01f, .48f, .48f);
            ramComicPage.uvRect = uv;
            float aspect = ramComicPage.texture.width * uv.width / (ramComicPage.texture.height * uv.height);
            Vector2 available = new Vector2(1560f, 840f);
            float height = Mathf.Min(available.y, available.x / aspect);
            ramComicPage.rectTransform.sizeDelta = new Vector2(height * aspect, height);
            ramComicCaption.text = ramComicCaptions[Mathf.Min(panel, 3)];
            ramComicStep.text = panel == 4 ? "完整漫画" : (panel + 1) + " / 4";
        }

        private IEnumerator AnimateRamComic(Texture2D art, bool enemyInitiated, BattleAnimationSnapshot before, BattleAnimationSnapshot after, int priorBurnDamage)
        {
            if (art == null || BuildRamComicOverlay(art, enemyInitiated, before.Alignment == NavalBattleAlignment.HeadOn,
                RamComicResult(before, after, enemyInitiated, priorBurnDamage)) == null) yield break;
            try
            {
                // A hull-killing impact never reached the later boarding panels.
                // If boarding happened and its first strike killed the crew, the
                // full sequence remains truthful and ends in the real defeat.
                bool hullOutcome = session.Phase != CoreDemoPhase.Battle && after.Stage != NavalBattleStage.Boarding;
                int panelCount = hullOutcome ? 2 : 4;
                for (int panel = 0; panel < panelCount && !ramComicSkipRequested && ramComicOverlay != null; panel++)
                {
                    SetRamComicPanel(panel);
                    if (panel == 0) PlaySfx("battle_ram_charge", .85f);
                    else if (panel == 1) PlaySfx("battle_ram_impact", .95f);
                    yield return HoldRamComic(.85f);
                }
                if (!ramComicSkipRequested && ramComicOverlay != null)
                {
                    if (hullOutcome)
                    {
                        ramComicCaption.text = ramComicCaptions[3];
                        ramComicStep.text = "撞击结算";
                    }
                    else SetRamComicPanel(4);
                    yield return HoldRamComic(.5f);
                }
                if (session.Phase == CoreDemoPhase.Battle && session.Battle != null && session.Battle.Stage == NavalBattleStage.Boarding)
                {
                    yield return AnimateBoardingSceneTransition();
                }
                else if (ramComicOverlay != null)
                {
                    Image curtain = CreateStretchPanel("Battle Ram Comic Fade", ramComicOverlay, Color.clear).GetComponent<Image>();
                    curtain.raycastTarget = true;
                    const float duration = .22f;
                    for (float elapsed = 0f; elapsed < duration && curtain != null; elapsed += Time.unscaledDeltaTime)
                    {
                        curtain.color = new Color(0, 0, 0, Mathf.Clamp01(elapsed / duration));
                        yield return null;
                    }
                    if (curtain != null)
                    {
                        curtain.color = Color.black;
                        yield return null;
                    }
                }
            }
            finally { CloseRamComic(); }
        }

        private IEnumerator HoldRamComic(float duration)
        {
            for (float elapsed = 0f; elapsed < duration && !ramComicSkipRequested && ramComicOverlay != null;
                elapsed += Time.unscaledDeltaTime) yield return null;
        }

        private void CloseRamComic()
        {
            RectTransform overlay = ramComicOverlay;
            ramComicOverlay = null;
            ramComicPage = null;
            ramComicCaption = ramComicStep = null;
            ramComicCaptions = null;
            ramComicSkipRequested = false;
            if (overlay == null) return;
            GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(overlay)) EventSystem.current.SetSelectedGameObject(null);
            overlay.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject);
            // The four textures are shared Resources assets, never owned here.
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && (ramComicOverlay != null || boardingTransitionOverlay != null))
            {
                ramComicResumeAfterPause = true;
                SuspendBattlePresentation();
            }
            else if (!paused && ramComicResumeAfterPause && isActiveAndEnabled)
            {
                ramComicResumeAfterPause = false;
                ResumeBattlePresentation();
            }
        }
    }
}
