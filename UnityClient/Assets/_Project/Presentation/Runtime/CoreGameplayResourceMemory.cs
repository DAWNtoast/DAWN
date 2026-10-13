using System.Collections;
using ShatteredSail.Core.Gameplay;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
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

        private void QueueUnusedArtCollection()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || editorPreviewBuilding || session == null) return;
            string scope = session.Phase + ":" + (session.Phase == CoreDemoPhase.Port
                ? session.CurrentPort + ":" + activePortFacility : string.Empty);
            if (scope == runtimeArtScope) return;
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
                yield return null;
                if (revision != runtimeArtRevision) continue;
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
            NavigationLookoutArtCatalog.ReleaseLoadedTextures();
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
            NavigationLookoutArtCatalog.ReleaseLoadedTextures();
            CardSchoolArtCatalog.ReleaseLoadedTextures();
            DeckListArtCatalog.ReleaseLoadedTextures();
            // The previous gameplay scene is gone. Its generated furniture
            // masks/materials can now be released without affecting a shop.
            RegionalShopFurniture.ReleaseGeneratedResources();
            ItemIconCatalog.ReleaseLoadedTextures();
            AchievementIconCatalog.ReleaseLoadedTextures();
            NauticalSurfaceSkin.ReleaseGameplayPaperReferences();
            yield return Resources.UnloadUnusedAssets();
        }
    }
}
