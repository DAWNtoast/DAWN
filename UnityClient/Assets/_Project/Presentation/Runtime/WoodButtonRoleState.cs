using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public enum WoodButtonRole { Secondary, Primary, Danger }

    /// <summary>Stores semantic emphasis and tab selection; its border shares the button's state tint.</summary>
    [DisallowMultipleComponent]
    public sealed class WoodButtonRoleState : Outline
    {
        [SerializeField] private WoodButtonRole role;
        [SerializeField] private bool explicitRole;
        [SerializeField] private bool selected;

        public WoodButtonRole Role => role;
        public bool HasExplicitRole => explicitRole;
        public bool IsSelected => selected;

        internal void AssignRole(WoodButtonRole value, bool isExplicit)
        {
            role = value;
            explicitRole = isExplicit;
        }

        internal void Select(bool value) => selected = value;
    }
}
