using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>Identifies a grounded boarding formation independently of optional UI labels.</summary>
    [DisallowMultipleComponent]
    public sealed class BoardingPartyPresentation : MonoBehaviour
    {
        public static Vector2 LungeOffset(float progress, float direction)
        {
            float step = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress));
            // Ease both ends without scaling or rocking the bodies about their
            // centres, which would lift their soles off the deck.
            return Vector2.right * (64f * direction * step * step);
        }
    }
}
