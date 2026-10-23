using System;
using System.Collections.Generic;
using ShatteredSail.Core.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ShatteredSail.Presentation
{
    public sealed partial class CoreGameplayDemoController
    {
        private const string ComponentLoadoutKeyPrefix = "ShatteredSail.ComponentLoadout.v1.";
        public const string ComponentInventoryKey = "ShatteredSail.ComponentInventory.v2";
        private OwnedShipComponentInventory componentInventory;
        private bool componentInventoryReadFailed;
        private const string ComponentInventoryReadFailureNotice = "船只配件档案无法读取或版本不兼容，原档案已保留。请修复后重新载入游戏。";
        public IReadOnlyList<OwnedShipComponent> OwnedComponents => componentInventory == null
            ? (IReadOnlyList<OwnedShipComponent>)Array.Empty<OwnedShipComponent>() : componentInventory.Items;

        public ComponentArtEntry EquippedComponent(ComponentSlot slot)
        {
            return ShipComponent(session == null ? 0 : session.SelectedShipIndex, slot);
        }

        public OwnedShipComponent InstalledShipComponent(int shipIndex, ComponentSlot slot) => componentInventory?.Installed(shipIndex, slot);

        public ComponentArtEntry ShipComponent(int shipIndex, ComponentSlot slot)
        {
            OwnedShipComponent item = InstalledShipComponent(shipIndex, slot);
            return item == null ? null : componentArt.GetById(item.ComponentId);
        }

        private bool OwnsComponentVessel(int index) => index >= 0 && index < CoreVoyageSession.Ships.Count &&
            (index == 0 || (Application.isPlaying && !editorPreviewBuilding && PlayerPrefs.GetInt(ShipOwnedKey(index), 0) == 1));

        public void EnsureShipComponentLoadout(int index)
        {
            if (componentInventoryReadFailed || componentInventory == null || !OwnsComponentVessel(index)) return;
            componentInventory.EnsureLoadout(index);
            SaveComponentInventory();
        }

        public OwnedShipComponent GrantOwnedComponent(ComponentArtEntry entry)
        {
            if (!Application.isPlaying || editorPreviewBuilding || componentInventoryReadFailed || componentInventory == null) return null;
            OwnedShipComponent item = componentInventory.Grant(entry);
            SaveComponentInventory();
            return item;
        }

        /// <summary>
        /// Grants each queued reward before acknowledging it. Missing inventory or
        /// catalog data leaves the exact saved reward available for a later retry.
        /// </summary>
        private void ResolveBoardingComponentDrop()
        {
            if (!Application.isPlaying || editorPreviewBuilding || session == null || componentArt == null ||
                componentInventoryReadFailed || componentInventory == null)
                return;
            while (BoardingComponentDropSelection.TryGrantNext(session, componentArt.ExpandedEntries,
                selected => GrantOwnedComponent(selected) != null, out ComponentArtEntry granted))
            {
                session.RecordBoardingComponentDrop(granted.Name);
            }
        }

        public bool TryEquipOwnedComponent(int shipIndex, string instanceId)
        {
            if (!CanRefitShip(shipIndex) || !componentInventory.TryEquip(shipIndex, instanceId)) return false;
            CommitShipRefit(shipIndex);
            return true;
        }

        public bool TryUnequipShipComponent(int shipIndex, ComponentSlot slot)
        {
            if (!CanRefitShip(shipIndex) || !componentInventory.TryUnequip(shipIndex, slot)) return false;
            CommitShipRefit(shipIndex);
            return true;
        }

        private bool CanRefitShip(int index) => Application.isPlaying && !editorPreviewBuilding &&
            session != null && session.Phase == CoreDemoPhase.Port && !componentInventoryReadFailed && componentInventory != null &&
            OwnsComponentVessel(index) && session.IsShipAtCurrentPort(index);

        private void CommitShipRefit(int shipIndex)
        {
            if (shipIndex == session.SelectedShipIndex) RefreshComponentBonuses(true);
            SaveProgress();
        }

        private void SaveComponentInventory()
        {
            if (!Application.isPlaying || editorPreviewBuilding || componentInventoryReadFailed || componentInventory == null) return;
            PlayerPrefs.SetString(ComponentInventoryKey, componentInventory.Export());
        }

        public static string ComponentLoadoutKey(ComponentSlot slot)
        {
            return ComponentLoadoutKeyPrefix + slot;
        }

        public static string ComponentConditionKey(ComponentSlot slot) => "ShatteredSail.ComponentCondition.v1." + slot;

        private void EquipPurchasedComponent(ComponentArtEntry entry)
        {
            if (entry == null || !CanRefitShip(session.SelectedShipIndex)) return;
            OwnedShipComponent item = GrantOwnedComponent(entry);
            if (item != null) TryEquipOwnedComponent(session.SelectedShipIndex, item.InstanceId);
        }

        private float BattleEquipmentQuality(ComponentSlot slot)
        {
            ComponentArtEntry entry = EquippedComponent(slot);
            OwnedShipComponent item = InstalledShipComponent(session.SelectedShipIndex, slot);
            return entry == null || item == null ? 0f : 1f + (int)entry.Rarity;
        }

        private void RestoreComponentLoadout()
        {
            componentInventory = new OwnedShipComponentInventory(componentArt);
            bool restored = componentInventory.Import(PlayerPrefs.GetString(ComponentInventoryKey, string.Empty), OwnsComponentVessel);
            componentInventoryReadFailed = PlayerPrefs.HasKey(ComponentInventoryKey) && !restored;
            if (componentInventoryReadFailed)
            {
                portNotice = ComponentInventoryReadFailureNotice;
                return;
            }
            int previousFlagship = Mathf.Clamp(PlayerPrefs.GetInt(SelectedShipKey, 0), 0, CoreVoyageSession.Ships.Count - 1);
            if (!OwnsComponentVessel(previousFlagship)) previousFlagship = 0;

            // A brand-new profile starts with the actual hull only. Legacy
            // profiles that stored one or more explicit fittings still receive
            // the old migration behaviour (missing slots are filled with the
            // catalogue baseline), but a clean profile must not be handed a
            // complete free loadout before the player has interacted with the
            // shipyard.
            ComponentArtEntry[] legacyFittings = new ComponentArtEntry[ComponentArtCatalog.SlotCount];
            bool hasLegacyFittings = false;
            if (!restored)
            {
                for (int index = 0; index < ComponentArtCatalog.SlotCount; index++)
                {
                    ComponentSlot slot = (ComponentSlot)index;
                    ComponentArtEntry entry = componentArt.GetById(PlayerPrefs.GetString(ComponentLoadoutKey(slot), string.Empty));
                    if (entry == null || entry.Slot != slot) continue;
                    legacyFittings[index] = entry;
                    hasLegacyFittings = true;
                }
            }

            for (int ship = 0; ship < CoreVoyageSession.Ships.Count; ship++)
            {
                if (!OwnsComponentVessel(ship)) continue;
                if (!restored && ship == previousFlagship && hasLegacyFittings)
                    componentInventory.EnsureLoadout(ship, slot => legacyFittings[(int)slot]);
                else if (!restored && ship == previousFlagship)
                    componentInventory.InitializeEmptyLoadout(ship);
                else componentInventory.EnsureLoadout(ship);
            }
            SaveComponentInventory();
            RefreshComponentBonuses();
        }

        private void RefreshComponentBonuses(bool preserveCurrentHull = false)
        {
            float hull = 0f;
            float armor = 0f;
            float firepower = 0f;
            float speed = 0f;
            float vision = 0f;
            for (int slotIndex = 0; slotIndex < ComponentArtCatalog.SlotCount; slotIndex += 1)
            {
                ComponentArtEntry equipped = EquippedComponent((ComponentSlot)slotIndex);
                if (equipped == null) continue;
                ComponentArtEntry baseline = componentArt.Get(slotIndex * ComponentArtCatalog.EntriesPerSlot);
                hull += equipped.HullBonus - baseline.HullBonus;
                armor += equipped.ArmorBonus - baseline.ArmorBonus;
                firepower += equipped.FirepowerBonus - baseline.FirepowerBonus;
                speed += equipped.SpeedBonus - baseline.SpeedBonus;
                vision += equipped.VisionBonus - baseline.VisionBonus;
            }
            session.ConfigureComponentBonuses(
                Mathf.RoundToInt(hull),
                Mathf.RoundToInt(armor),
                Mathf.RoundToInt(firepower),
                speed,
                vision, preserveCurrentHull);
            // Environmental effects are evaluated when a voyage forecast is
            // built. Do not apply them in port: this preserves old loadout
            // totals and keeps refitting from becoming a hidden repair loop.
            if (session.Phase != CoreDemoPhase.Port)
                RefreshComponentEnvironmentEffect();
            session.ConfigureBattleEquipment(BattleEquipmentQuality(ComponentSlot.Bow), BattleEquipmentQuality(ComponentSlot.Plating));
            // Only the actual blue-or-better equipped item supplies its identity-bound tactic.
            RefreshSpecialCardSources();
        }

        private void RefreshComponentEnvironmentEffect()
        {
            if (session == null || componentArt == null || session.Map == null)
                return;

            ComponentVoyageContext context = CurrentComponentVoyageContext();
            int hull = 0;
            int armor = 0;
            int firepower = 0;
            float speed = 0f;
            float vision = 0f;
            float rationMultiplier = 1f;
            for (int slotIndex = 0; slotIndex < ComponentArtCatalog.SlotCount; slotIndex += 1)
            {
                ComponentArtEntry entry = EquippedComponent((ComponentSlot)slotIndex);
                if (entry == null) continue;
                ComponentContextEffect effect = entry.Evaluate(context);
                hull += effect.HullBonus;
                armor += effect.ArmorBonus;
                firepower += effect.FirepowerBonus;
                speed += effect.SpeedBonus;
                vision += effect.VisionBonus;
                rationMultiplier *= effect.RationMultiplier <= 0f ? 1f : effect.RationMultiplier;
            }
            session.ConfigureComponentContextEffect(hull, armor, firepower, speed, vision, rationMultiplier);
        }

        private ComponentVoyageContext CurrentComponentVoyageContext()
        {
            return ComponentVoyageContextResolver.FromSession(session);
        }

        private RectTransform BuildModularShipPreview(
            string previewName,
            Transform parent,
            Vector2 availableSize,
            Vector2 position)
        {
            RectTransform root = CreateRect(previewName, parent, availableSize, position);
            Vector2 baseSize = UiImageAspect.FitSize(componentArt.BaseShipTexture, new Rect(0f, 0f, 1f, 1f), availableSize);
            RawImage baseLayer = CreateRawImage(
                previewName + " Base",
                root,
                baseSize,
                Vector2.zero,
                componentArt.BaseShipTexture,
                new Rect(0f, 0f, 1f, 1f));
            baseLayer.raycastTarget = false;

            return root;
        }

        private RectTransform BuildActiveShipPreview(
            string previewName,
            Transform parent,
            Vector2 availableSize,
            Vector2 position)
        {
            if (session == null || session.SelectedShipIndex == 0)
            {
                return BuildModularShipPreview(previewName, parent, availableSize, position);
            }

            RectTransform root = CreateRect(previewName, parent, availableSize, position);
            Texture2D texture = portArt.ShipTexture(session.SelectedShipIndex);
            // Crop atlas gutters once, before measuring the visible source.
            Rect uv = UiImageAspect.InsetUv(texture, portArt.ShipUv(session.SelectedShipIndex), 10f);
            Vector2 baseSize = UiImageAspect.FitSize(texture, uv, availableSize);
            RawImage ship = CreateRawImage(
                previewName + " Hull",
                root,
                baseSize,
                Vector2.zero,
                texture,
                uv);
            ship.color = portArt.ShipTint(session.SelectedShipIndex);
            BattleShipCutout.Attach(ship);
            ship.raycastTarget = false;
            return root;
        }

        private static void AlignShipHullToWaterline(RectTransform ship, RawImage hull, float waterlineY)
        {
            if (ship == null || hull == null || ship.parent == null) return;
            Vector3 contact = ship.parent.InverseTransformPoint(
                hull.rectTransform.TransformPoint(ShipWaterlineLayout.HullContactLocal(hull)));
            // Move the hull root so atlas padding never becomes an air gap.
            ship.anchoredPosition += new Vector2(0f, waterlineY - contact.y);
        }
    }
}
