using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace TraderOverhaul
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class TraderOverhaulPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.profmags.traderoverhaul";
        public const string PluginName = "Trader Overhaul";
        public const string PluginVersion = "0.0.2";

        private static Harmony _harmony;
        internal static ManualLogSource Log;

        private static ConfigEntry<bool> _enableHaldor;
        private static ConfigEntry<bool> _enableHildir;
        private static ConfigEntry<bool> _enableBogWitch;

        internal static bool IsCustomUIEnabled(TraderKind kind)
        {
            switch (kind)
            {
                case TraderKind.Haldor: return _enableHaldor.Value;
                case TraderKind.Hildir: return _enableHildir.Value;
                case TraderKind.BogWitch: return _enableBogWitch.Value;
                default: return false;
            }
        }

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{PluginName} v{PluginVersion} loading...");

            _enableHaldor = Config.Bind("Custom UI", "EnableHaldor", true,
                "Use the custom trader UI for Haldor.");
            _enableHildir = Config.Bind("Custom UI", "EnableHildir", true,
                "Use the custom trader UI for Hildir.");
            _enableBogWitch = Config.Bind("Custom UI", "EnableBogWitch", true,
                "Use the custom trader UI for the Bog Witch.");

            ConfigLoader.Initialize();

            var traderUI = gameObject.AddComponent<TraderUI>();
            TraderPatches.SetTraderUI(traderUI);

            var bankUI = gameObject.AddComponent<BankUI>();
            TraderPatches.SetBankUI(bankUI);

            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll(typeof(TraderPatches));

            Log.LogInfo($"{PluginName} loaded successfully!");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            Log.LogInfo($"{PluginName} unloaded.");
        }
    }
}
