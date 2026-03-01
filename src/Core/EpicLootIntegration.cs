using System;
using System.Reflection;

namespace TraderOverhaul
{
    /// <summary>
    /// Soft integration with Epic Loot via reflection.
    /// No compile-time dependency — all calls are resolved at runtime.
    /// Uses Epic Loot's proper SaveMagicItem API through the ItemInfo/CustomItemData framework.
    /// </summary>
    internal static class EpicLootIntegration
    {
        private static bool _initialized;
        private static bool _available;

        // Cached reflection targets
        private static Type _itemRarityEnum;
        private static MethodInfo _rollMagicItem;   // LootRoller.RollMagicItem(ItemRarity, ItemData, float, float)
        private static MethodInfo _canBeMagicItem;  // EpicLoot.CanBeMagicItem(ItemData)
        private static MethodInfo _saveMagicItem;   // ItemDataExtensions.SaveMagicItem(ItemData, MagicItem)

        internal static bool IsAvailable
        {
            get
            {
                if (!_initialized) Initialize();
                return _available;
            }
        }

        private static void Initialize()
        {
            _initialized = true;
            _available = false;

            try
            {
                Assembly epicLootAssembly = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "EpicLoot")
                    {
                        epicLootAssembly = asm;
                        break;
                    }
                }

                if (epicLootAssembly == null)
                {
                    TraderOverhaulPlugin.Log.LogInfo("[EpicLootIntegration] Epic Loot not found — rarity enchantments disabled.");
                    return;
                }

                // Resolve ItemRarity enum
                _itemRarityEnum = epicLootAssembly.GetType("EpicLoot.ItemRarity");
                if (_itemRarityEnum == null)
                {
                    TraderOverhaulPlugin.Log.LogWarning("[EpicLootIntegration] Could not find ItemRarity enum.");
                    return;
                }

                // Resolve MagicItem type (needed for SaveMagicItem parameter matching)
                var magicItemType = epicLootAssembly.GetType("EpicLoot.MagicItem");
                if (magicItemType == null)
                {
                    TraderOverhaulPlugin.Log.LogWarning("[EpicLootIntegration] Could not find MagicItem type.");
                    return;
                }

                // Resolve LootRoller.RollMagicItem(ItemRarity, ItemData, float, float)
                var lootRollerType = epicLootAssembly.GetType("EpicLoot.LootRoller");
                if (lootRollerType == null)
                {
                    TraderOverhaulPlugin.Log.LogWarning("[EpicLootIntegration] Could not find LootRoller type.");
                    return;
                }

                _rollMagicItem = lootRollerType.GetMethod("RollMagicItem",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { _itemRarityEnum, typeof(ItemDrop.ItemData), typeof(float), typeof(float) },
                    null);

                if (_rollMagicItem == null)
                {
                    TraderOverhaulPlugin.Log.LogWarning("[EpicLootIntegration] Could not find RollMagicItem method.");
                    return;
                }

                // Resolve ItemDataExtensions.SaveMagicItem(ItemData, MagicItem)
                // This is the proper API that goes through ItemInfo/CustomItemData framework
                var itemDataExtType = epicLootAssembly.GetType("EpicLoot.ItemDataExtensions");
                if (itemDataExtType == null)
                {
                    TraderOverhaulPlugin.Log.LogWarning("[EpicLootIntegration] Could not find ItemDataExtensions type.");
                    return;
                }

                _saveMagicItem = itemDataExtType.GetMethod("SaveMagicItem",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(ItemDrop.ItemData), magicItemType },
                    null);

                if (_saveMagicItem == null)
                {
                    TraderOverhaulPlugin.Log.LogWarning("[EpicLootIntegration] Could not find SaveMagicItem method.");
                    return;
                }

                // Resolve EpicLoot.CanBeMagicItem(ItemData) — optional safety check
                var epicLootMainType = epicLootAssembly.GetType("EpicLoot.EpicLoot");
                if (epicLootMainType != null)
                {
                    _canBeMagicItem = epicLootMainType.GetMethod("CanBeMagicItem",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(ItemDrop.ItemData) },
                        null);
                }

                _available = true;
                TraderOverhaulPlugin.Log.LogInfo("[EpicLootIntegration] Epic Loot detected — rarity enchantments enabled.");
            }
            catch (Exception ex)
            {
                TraderOverhaulPlugin.Log.LogWarning($"[EpicLootIntegration] Failed to initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies Epic Loot magic enchantments to an item based on the given rarity string.
        /// Rolls random effects via LootRoller, then saves through Epic Loot's SaveMagicItem API
        /// which properly initializes the ItemInfo/MagicItemComponent pipeline.
        /// </summary>
        internal static bool ApplyRarity(ItemDrop.ItemData item, string rarity)
        {
            if (item == null || string.IsNullOrEmpty(rarity)) return false;
            if (!IsAvailable) return false;

            try
            {
                // Parse rarity string → Epic Loot enum
                object rarityValue;
                try
                {
                    rarityValue = Enum.Parse(_itemRarityEnum, rarity, ignoreCase: true);
                }
                catch
                {
                    TraderOverhaulPlugin.Log.LogWarning($"[EpicLootIntegration] Unknown rarity: {rarity}");
                    return false;
                }

                // Safety check: can this item type be enchanted?
                if (_canBeMagicItem != null)
                {
                    bool canBeMagic = (bool)_canBeMagicItem.Invoke(null, new object[] { item });
                    if (!canBeMagic)
                    {
                        TraderOverhaulPlugin.Log.LogInfo($"[EpicLootIntegration] {item.m_shared?.m_name} cannot be made magic.");
                        return false;
                    }
                }

                // Roll random magic effects for this rarity
                object magicItem = _rollMagicItem.Invoke(null, new object[] { rarityValue, item, 0f, 1f });
                if (magicItem == null)
                {
                    TraderOverhaulPlugin.Log.LogWarning($"[EpicLootIntegration] RollMagicItem returned null for {item.m_shared?.m_name}");
                    return false;
                }

                // Save through Epic Loot's proper API:
                // ItemDataExtensions.SaveMagicItem(itemData, magicItem)
                //   → itemData.Data().GetOrCreate<MagicItemComponent>().SetMagicItem(magicItem)
                // This properly initializes the ItemInfo framework, creates the MagicItemComponent,
                // serializes with the correct m_customData key, and ensures Epic Loot's patches
                // (stats, tooltips, item backgrounds, name coloring) all recognize the item.
                _saveMagicItem.Invoke(null, new object[] { item, magicItem });

                TraderOverhaulPlugin.Log.LogInfo($"[EpicLootIntegration] Applied {rarity} to {item.m_shared?.m_name}");
                return true;
            }
            catch (TargetInvocationException ex)
            {
                TraderOverhaulPlugin.Log.LogWarning($"[EpicLootIntegration] Epic Loot API threw: {ex.InnerException?.Message ?? ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                TraderOverhaulPlugin.Log.LogWarning($"[EpicLootIntegration] Failed to apply rarity: {ex.Message}");
                return false;
            }
        }
    }
}
