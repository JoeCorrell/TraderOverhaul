using UnityEngine;

namespace TraderOverhaul
{
    internal static class BankBalanceStore
    {
        private const string SharedKey = "TraderSharedBank_Balance";
        private const string LegacyHaldorKey = "HaldorBank_Balance";
        private const string LegacyHildirKey = "HildirBank_Balance";
        private const string LegacyBogWitchKey = "BogWitchBank_Balance";

        internal static int Read(Player player)
        {
            if (player == null) return 0;

            if (TryRead(player, SharedKey, out int shared))
                return shared;

            bool hasHaldor = TryRead(player, LegacyHaldorKey, out int haldor);
            bool hasHildir = TryRead(player, LegacyHildirKey, out int hildir);
            bool hasBogWitch = TryRead(player, LegacyBogWitchKey, out int bogWitch);
            int value = 0;
            if (hasHaldor) value = Mathf.Max(value, haldor);
            if (hasHildir) value = Mathf.Max(value, hildir);
            if (hasBogWitch) value = Mathf.Max(value, bogWitch);

            Write(player, value);
            return value;
        }

        internal static void Write(Player player, int value)
        {
            if (player == null) return;
            value = Mathf.Max(0, value);
            string serialized = value.ToString();
            player.m_customData[SharedKey] = serialized;
            player.m_customData[LegacyHaldorKey] = serialized;
            player.m_customData[LegacyHildirKey] = serialized;
            player.m_customData[LegacyBogWitchKey] = serialized;
        }

        private static bool TryRead(Player player, string key, out int value)
        {
            value = 0;
            return player.m_customData.TryGetValue(key, out string raw) && int.TryParse(raw, out value);
        }
    }
}
