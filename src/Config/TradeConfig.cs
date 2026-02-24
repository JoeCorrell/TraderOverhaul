using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Newtonsoft.Json;

namespace TraderOverhaul
{
    public static class ConfigLoader
    {
        private static readonly string BuyFile = Path.Combine(Paths.ConfigPath, "TraderOverhaul.buy.json");
        private static readonly string SellFile = Path.Combine(Paths.ConfigPath, "TraderOverhaul.sell.json");

        private static readonly List<TradeEntry> EmptyEntries = new List<TradeEntry>();

        private static List<TradeEntry> BuyEntries = new List<TradeEntry>();
        private static List<TradeEntry> SellEntries = new List<TradeEntry>();

        public static void Initialize()
        {
            EnsureConfigFilesExist();
            LoadAllConfigs();
            ValidateAndLogStats();
        }

        internal static IReadOnlyList<TradeEntry> GetBuyEntries()
        {
            return BuyEntries ?? EmptyEntries;
        }

        internal static IReadOnlyList<TradeEntry> GetSellEntries()
        {
            return SellEntries ?? EmptyEntries;
        }

        private static void EnsureConfigFilesExist()
        {
            EnsureBuyConfigFile();
            EnsureSellConfigFile();
        }

        private static void EnsureBuyConfigFile()
        {
            if (File.Exists(BuyFile)) return;

            File.WriteAllText(BuyFile, DefaultBuyJson());
            TraderOverhaulPlugin.Log.LogInfo("[ConfigLoader] Created default buy config.");
        }

        private static void EnsureSellConfigFile()
        {
            if (File.Exists(SellFile)) return;

            File.WriteAllText(SellFile, DefaultSellJson());
            TraderOverhaulPlugin.Log.LogInfo("[ConfigLoader] Created default sell config.");
        }

        private static void LoadAllConfigs()
        {
            BuyEntries = LoadEntries(BuyFile, "BUY");
            SellEntries = LoadEntries(SellFile, "SELL");
        }

        private static List<TradeEntry> LoadEntries(string path, string label)
        {
            try
            {
                var entries = JsonConvert.DeserializeObject<List<TradeEntry>>(File.ReadAllText(path)) ?? new List<TradeEntry>();
                TraderOverhaulPlugin.Log.LogInfo($"[ConfigLoader] Loaded {entries.Count} {label} entries.");
                return entries;
            }
            catch (Exception ex)
            {
                TraderOverhaulPlugin.Log.LogError($"[ConfigLoader] Error loading {label} config: {ex}");
                return new List<TradeEntry>();
            }
        }

        private static void ValidateAndLogStats()
        {
            BuyEntries = ValidateEntries(BuyEntries, "BUY");
            SellEntries = ValidateEntries(SellEntries, "SELL");
        }

        private static List<TradeEntry> ValidateEntries(List<TradeEntry> entries, string type)
        {
            int valid = 0;
            int invalid = 0;
            entries = entries ?? new List<TradeEntry>();

            foreach (var entry in entries.ToList())
            {
                if (!ValidateEntry(entry, type))
                {
                    entries.Remove(entry);
                    invalid++;
                }
                else
                {
                    valid++;
                }
            }

            if (invalid > 0)
                TraderOverhaulPlugin.Log.LogWarning($"[ConfigLoader] Removed {invalid} invalid {type} entries.");
            TraderOverhaulPlugin.Log.LogInfo($"[ConfigLoader] {type}: {valid} valid entries.");
            return entries;
        }

        private static bool ValidateEntry(TradeEntry entry, string type)
        {
            if (entry == null)
            {
                TraderOverhaulPlugin.Log.LogWarning($"[ConfigLoader] {type} entry is null, skipping.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.prefab))
            {
                TraderOverhaulPlugin.Log.LogWarning($"[ConfigLoader] {type} entry has empty prefab name, skipping.");
                return false;
            }

            if (entry.price < 0)
            {
                TraderOverhaulPlugin.Log.LogWarning($"[ConfigLoader] {type} entry '{entry.prefab}' has negative price ({entry.price}), setting to 0.");
                entry.price = 0;
            }

            if (entry.stack <= 0)
            {
                TraderOverhaulPlugin.Log.LogWarning($"[ConfigLoader] {type} entry '{entry.prefab}' has invalid stack ({entry.stack}), setting to 1.");
                entry.stack = 1;
            }

            return true;
        }

        private static string DefaultBuyJson() => JsonConvert.SerializeObject(new List<TradeEntry>
        {
            new TradeEntry() { prefab = "Wood", stack = 50, price = 25, requiredGlobalKey = "defeated_eikthyr" }
        }, Formatting.Indented);

        private static string DefaultSellJson() => JsonConvert.SerializeObject(new List<TradeEntry>
        {
            new TradeEntry() { prefab = "Wood", stack = 1, price = 1 }
        }, Formatting.Indented);
    }

    public class TradeEntry
    {
        [JsonProperty("item_prefab")]
        public string prefab = "";

        [JsonProperty("item_quantity")]
        public int stack = 1;

        [JsonProperty("item_price")]
        public int price = 1;

        [JsonProperty("must_defeated_boss")]
        public string requiredGlobalKey = "";
    }
}
