using UnityEngine;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        // All facilities share the same layout, including the component shop.
        // Apply after assigning live labels, without caching or restoring stale poses.
        private void ApplyComponentShopHeaderLayout(Transform root, int facility)
        {
            if (!(root is RectTransform header)) return;
            EnsurePortHeaderArt();
            NauticalHeaderSkin.ApplyPort(header, portHeaderArt.ForPort(session.CurrentPort).Accent, false, session.CurrentPort);
            NauticalHeaderSkin.ApplyFacilityNavigation(header, facility, PortFacilityNames.Length);
        }
    }
}
