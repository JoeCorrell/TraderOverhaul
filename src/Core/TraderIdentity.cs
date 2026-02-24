using System;
using UnityEngine;

namespace TraderOverhaul
{
    internal struct TraderPreviewProfile
    {
        internal Vector3 SpawnOffset;
        internal Vector3 CenterOffset;
        internal Vector3 CameraOffset;
        internal Quaternion Rotation;
        internal float AnimatorUpdateDelta;
        internal float CameraFov;
    }

    internal enum TraderKind
    {
        Unknown = 0,
        Haldor = 1,
        Hildir = 2,
        BogWitch = 3
    }

    internal static class TraderIdentity
    {
        internal static TraderKind Resolve(Trader trader)
        {
            if (trader == null) return TraderKind.Unknown;
            return ResolvePrefabName(Utils.GetPrefabName(trader.gameObject));
        }

        internal static TraderKind ResolvePrefabName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return TraderKind.Unknown;
            string normalized = prefabName.Replace("_", "").Replace(" ", "");
            if (string.Equals(normalized, "Haldor", StringComparison.OrdinalIgnoreCase)) return TraderKind.Haldor;
            if (string.Equals(normalized, "Hildir", StringComparison.OrdinalIgnoreCase)) return TraderKind.Hildir;
            if (string.Equals(normalized, "BogWitch", StringComparison.OrdinalIgnoreCase)) return TraderKind.BogWitch;
            return TraderKind.Unknown;
        }

        internal static string DisplayName(TraderKind kind)
        {
            switch (kind)
            {
                case TraderKind.Haldor: return "Haldor";
                case TraderKind.Hildir: return "Hildir";
                case TraderKind.BogWitch: return "Bog Witch";
                default: return "Trader";
            }
        }

        internal static bool KeepsVanillaBuyStock(TraderKind kind)
        {
            return kind == TraderKind.Hildir || kind == TraderKind.BogWitch;
        }

        internal static TraderPreviewProfile GetPreviewProfile(TraderKind kind)
        {
            switch (kind)
            {
                case TraderKind.Hildir:
                    return new TraderPreviewProfile
                    {
                        SpawnOffset = new Vector3(0f, 0.2625f, 0f),
                        CenterOffset = new Vector3(0f, 1.05f, 0f),
                        CameraOffset = new Vector3(0f, 0.35f, 4.2f),
                        Rotation = Quaternion.Euler(0f, 0f, 0f),
                        AnimatorUpdateDelta = 0.01f,
                        CameraFov = 30f
                    };
                case TraderKind.BogWitch:
                    return new TraderPreviewProfile
                    {
                        SpawnOffset = new Vector3(0f, 0.063f - 0.35f, 0f),
                        CenterOffset = new Vector3(0f, 1.05f * 1.30f, 0f),
                        CameraOffset = new Vector3(0f, 0.35f, 4.2f),
                        Rotation = Quaternion.Euler(0f, 0f, 0f),
                        AnimatorUpdateDelta = 0.01f,
                        CameraFov = 61f
                    };
                case TraderKind.Haldor:
                default:
                    return new TraderPreviewProfile
                    {
                        SpawnOffset = Vector3.zero,
                        CenterOffset = new Vector3(0f, 0.9f, 0f),
                        CameraOffset = new Vector3(0f, 0.2f, 4.5f),
                        Rotation = Quaternion.Euler(0f, 180f, 0f),
                        AnimatorUpdateDelta = 0.001f,
                        CameraFov = 30f
                    };
            }
        }
    }
}
