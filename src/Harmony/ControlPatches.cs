using HarmonyLib;
using UnityEngine;

namespace TraderOverhaul
{
    public static class TraderPatches
    {
        private static TraderUI _traderUI;
        private static BankUI _bankUI;

        internal static void SetTraderUI(TraderUI ui) => _traderUI = ui;
        internal static void SetBankUI(BankUI ui) => _bankUI = ui;
        internal static TraderUI GetTraderUI() => _traderUI;
        internal static TraderKind GetTraderKind(Trader trader) => TraderIdentity.Resolve(trader);

        [HarmonyPatch(typeof(StoreGui), "Show")]
        [HarmonyPrefix]
        private static bool StoreGui_Show_Prefix(StoreGui __instance, Trader trader)
        {
            if (_traderUI == null) return true;
            if (GetTraderKind(trader) == TraderKind.Unknown) return true;
            _traderUI.Show(trader, __instance);
            return false;
        }

        [HarmonyPatch(typeof(StoreGui), "Hide")]
        [HarmonyPostfix]
        private static void StoreGui_Hide_Postfix()
        {
            if (_traderUI != null && _traderUI.IsVisible)
                _traderUI.Hide();
        }

        [HarmonyPatch(typeof(StoreGui), "IsVisible")]
        [HarmonyPostfix]
        private static void StoreGui_IsVisible_Postfix(ref bool __result)
        {
            if (_traderUI != null && _traderUI.IsVisible)
                __result = true;
            if (_bankUI != null && _bankUI.IsVisible)
                __result = true;
        }

        [HarmonyPatch(typeof(Chat), "SetNpcText")]
        [HarmonyPrefix]
        private static bool Chat_SetNpcText_Prefix()
        {
            if ((_traderUI != null && _traderUI.IsVisible) ||
                (_bankUI != null && _bankUI.IsVisible))
                return false;
            return true;
        }

        [HarmonyPatch(typeof(Chat), "HasFocus")]
        [HarmonyPostfix]
        private static void Chat_HasFocus_Postfix(ref bool __result)
        {
            if (!__result && _traderUI != null && _traderUI.IsSearchFocused)
                __result = true;
        }

        [HarmonyPatch(typeof(Player), "TakeInput")]
        [HarmonyPrefix]
        private static bool Player_TakeInput_Prefix(ref bool __result)
        {
            if ((_traderUI != null && _traderUI.IsVisible) ||
                (_bankUI != null && _bankUI.IsVisible))
            {
                __result = false;
                return false;
            }
            return true;
        }

        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        [HarmonyPostfix]
        private static void Terminal_InitTerminal_Postfix()
        {
            new Terminal.ConsoleCommand("setbankbalance",
                "Set shared trader bank balance. Usage: setbankbalance <amount>  or  setbankbalance = <amount>",
                (Terminal.ConsoleEventArgs args) =>
                {
                    string raw = null;
                    if (args.Length >= 3 && args[1] == "=") raw = args[2];
                    else if (args.Length >= 2 && args[1] != "=") raw = args[1];

                    if (!int.TryParse(raw, out int amount) || amount < 0)
                    {
                        args.Context.AddString("Usage: setbankbalance <amount>");
                        return;
                    }

                    var player = Player.m_localPlayer;
                    if (player == null) { args.Context.AddString("No player found."); return; }

                    int previous = BankBalanceStore.Read(player);
                    BankBalanceStore.Write(player, amount);
                    GetTraderUI()?.ReloadBankBalance();

                    ((Character)player).Message(MessageHud.MessageType.Center, $"Bank balance set to {amount:N0}");
                    args.Context.AddString($"Bank balance set to {amount:N0} (was {previous:N0})");
                });
        }
    }
}
