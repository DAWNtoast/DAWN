using System;
using UnityEngine;

namespace ShatteredSail.Presentation
{
    /// <summary>
    /// Semantic identity for a UI element, independent of its displayed text,
    /// hierarchy name and parent. Lookups are confined to the supplied view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiId : MonoBehaviour
    {
        [SerializeField] private string value;

        public string Value => value;

        /// <summary>Explicitly rebind when a retained view represents a different action or slot.</summary>
        public static UiId Bind(GameObject target, string id)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A UI identifier must not be empty.", nameof(id));
            UiId identity = target.GetComponent<UiId>();
            if (identity == null) identity = target.AddComponent<UiId>();
            identity.value = id;
            return identity;
        }

        /// <summary>Compatibility for authored scenes that predate semantic IDs.</summary>
        public static string GetOrLegacyName(Component target)
        {
            if (target == null) return string.Empty;
            UiId identity = target.GetComponent<UiId>();
            return identity != null && !string.IsNullOrEmpty(identity.value) ? identity.value : target.name;
        }

        /// <summary>
        /// Returns the unique live element, or null if missing or ambiguous.
        /// Inactive retired views are excluded, including before deferred Destroy runs.
        /// </summary>
        public static Transform Find(Transform root, string id, bool includeInactive = false)
        {
            return FindUnique(root, id, includeInactive, false, out _);
        }

        /// <summary>
        /// Prefer a bound ID; only fall back to unbound authored names if no ID exists.
        /// An ambiguous ID must never fall through to an unrelated legacy element.
        /// </summary>
        public static Transform FindOrLegacyName(Transform root, string id, bool includeInactive = false)
        {
            Transform result = FindUnique(root, id, includeInactive, false, out int count);
            return count == 0 ? FindUnique(root, id, includeInactive, true, out _) : result;
        }

        private static Transform FindUnique(Transform root, string id, bool includeInactive, bool legacy, out int count)
        {
            count = 0;
            if (root == null || string.IsNullOrEmpty(id)) return null;
            Transform result = null;
            Collect(root, id, includeInactive, legacy, ref result, ref count);
            return count == 1 ? result : null;
        }

        private static void Collect(Transform node, string id, bool includeInactive, bool legacy,
            ref Transform result, ref int count)
        {
            if (count > 1 || (!includeInactive && !node.gameObject.activeInHierarchy)) return;
            UiId identity = node.GetComponent<UiId>();
            bool bound = identity != null && !string.IsNullOrEmpty(identity.value);
            if (legacy ? !bound && string.Equals(node.name, id, StringComparison.Ordinal)
                : bound && string.Equals(identity.value, id, StringComparison.Ordinal))
            {
                result = node;
                count++;
            }
            for (int index = 0; index < node.childCount && count < 2; index++)
                Collect(node.GetChild(index), id, includeInactive, legacy, ref result, ref count);
        }
    }
}
