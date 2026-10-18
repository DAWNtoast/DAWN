using System.Collections;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private void ReleaseGeneratedVisualResources()
        {
            if (portHeaderArt != null) portHeaderArt.Dispose();
            portHeaderArt = null;
            DestroyGeneratedVisual(seaSprite);
            DestroyGeneratedVisual(circleSprite);
            DestroyGeneratedVisual(seaTexture);
            DestroyGeneratedVisual(circleTexture);
            seaSprite = null;
            circleSprite = null;
            seaTexture = null;
            circleTexture = null;
        }

        private static void DestroyGeneratedVisual(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private string runtimeArtScope;
        private int runtimeArtRevision;
        private int runtimeArtCollectionCount;
        private Coroutine runtimeArtCollection;

        private void SuspendUnusedArtCollection()
        {
            if (runtimeArtCollection != null) StopCoroutine(runtimeArtCollection);
            runtimeArtCollection = null;
            runtimeArtScope = null;
        }

        private void QueueUnusedArtCollection(bool force = false)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || editorPreviewBuilding || session == null) return;
            string scope = session.Phase + ":" + (session.Phase == CoreDemoPhase.Port
                ? session.CurrentPort + ":" + activePortFacility : string.Empty);
            // Closing a retained modal can release large art without changing
            // the underlying page. Forced requests share the same revision and
            // coroutine, so rapid close/reopen still produces one deferred scan.
            if (scope == runtimeArtScope && !force) return;
            runtimeArtScope = scope;
            runtimeArtRevision++;
            if (runtimeArtCollection == null) runtimeArtCollection = StartCoroutine(CollectUnusedArt());
        }

        private IEnumerator CollectUnusedArt()
        {
            // Coalesce rapid navigation. Wait until the old body's deferred
            // destruction has completed; ordinary quantity/price refreshes do
            // not enqueue this coroutine.
            while (true)
            {
                int revision = runtimeArtRevision;
                yield return new WaitForSecondsRealtime(.35f);
                // A comic closes before the boarding reveal finishes. Its
                // animation iterators can still hold the first page until the
                // presentation exits; scan after those owners retire, without
                // interrupting the transition with resource collection.
                while (battleAnimationPlaying && revision == runtimeArtRevision) yield return null;
                yield return null;
                // An action may start in the extra frame above, after the first
                // animation check but before its comic pins the loaded pages.
                if (revision != runtimeArtRevision || battleAnimationPlaying) continue;
                ReleaseArtReferences();
                yield return Resources.UnloadUnusedAssets();
                runtimeArtCollectionCount++;
                if (revision == runtimeArtRevision) break;
            }
            runtimeArtCollection = null;
        }

        private void ReleaseArtReferences()
        {
            // These are borrowed Resources assets. Live Graphics, Sprites and
            // Materials retain their visible textures during the engine scan.
            // Never destroy or forcibly unload an asset in use by a view.
            art?.ReleaseLoadedTextures();
            CardSchoolArtCatalog.ReleaseLoadedTextures();
            DeckListArtCatalog.ReleaseLoadedTextures();
            portArt?.ReleaseLoadedTextures();
            portHeaderArt?.ReleaseUnusedThemes(session.Phase == CoreDemoPhase.Port ? session.CurrentPort : null);
            portMapIcons?.ReleaseLoadedTextures();
            battleEnvironmentArt?.ReleaseLoadedTextures();
            componentArt?.ReleaseLoadedTextures();
            componentShopArt?.ReleaseLoadedTextures();
            cardWorkshopArt?.ReleaseLoadedTextures();
            maintenanceArt?.ReleaseLoadedTextures();
            facilityArt?.ReleaseLoadedTextures();
            voyageMapArt?.ReleaseLoadedTextures();
            // A visible comic retains its own four-page sequence until it closes.
            System.Array.Clear(ramComicTextures, 0, ramComicTextures.Length);
            System.Array.Clear(ramComicLoadAttempted, 0, ramComicLoadAttempted.Length);
            BoardingBattleArtCatalog.ReleaseLoadedTextures();
            SpecialWarshipArtCatalog.ReleaseLoadedTextures();
            NavigationLookoutArtCatalog.ReleaseLoadedTextures();
            ReleaseAudioClipReferences();
            RegionalShopFurniture.ReleaseUnusedGeneratedResources(session.Phase == CoreDemoPhase.Port &&
                activePortFacility == PortFacilityArtCatalog.ComponentShopFacilityIndex ? session.CurrentPort : null);
        }
    }

    internal static class RuntimeArtMemory
    {
        internal static IEnumerator CollectAfterReturningToMenu()
        {
            yield return null;
            yield return null;
            BoardingBattleArtCatalog.ReleaseLoadedTextures();
            SpecialWarshipArtCatalog.ReleaseLoadedTextures();
            NavigationLookoutArtCatalog.ReleaseLoadedTextures();
            CardSchoolArtCatalog.ReleaseLoadedTextures();
            DeckListArtCatalog.ReleaseLoadedTextures();
            // The previous gameplay scene is gone. Release generated furniture
            // materials and references to the imported atlas/coverage textures.
            RegionalShopFurniture.ReleaseGeneratedResources();
            ItemIconCatalog.ReleaseLoadedTextures();
            AchievementIconCatalog.ReleaseLoadedTextures();
            NauticalSurfaceSkin.ReleaseGameplayPaperReferences();
            NavigationHeaderSkin.ReleaseGameplayReferences();
            NavigationInterfaceSkin.ReleaseGameplayReferences();
            yield return Resources.UnloadUnusedAssets();
        }
    }
}
