using BepInEx;
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
        public const string PluginVersion = "0.0.1";

        private static Harmony _harmony;
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            Log.LogInfo($"{PluginName} v{PluginVersion} loading...");

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
