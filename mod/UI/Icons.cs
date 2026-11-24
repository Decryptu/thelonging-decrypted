using System;
using UnityEngine;

namespace TheLongingDecrypted.UI
{
    /// <summary>
    /// Resolves icon references. "ink:" crops a drawing out of a vanilla interaction plank, "game:" uses a game sprite
    /// as it is, and anything else is a file in CustomAssets (files named "ink_*" are line art for a parchment square).
    /// </summary>
    internal static class Icons
    {
        public static Sprite Resolve(string reference, out bool ink)
        {
            ink = false;
            if (string.IsNullOrEmpty(reference))
            {
                return null;
            }
            if (reference.StartsWith("ink:", StringComparison.Ordinal))
            {
                ink = true;
                return NativeUi.InkGlyph(reference.Substring(4));
            }
            if (reference.StartsWith("game:", StringComparison.Ordinal))
            {
                ink = true;
                return NativeUi.GameSprite(reference.Substring(5));
            }
            ink = reference.IndexOf("/ink_", StringComparison.Ordinal) >= 0;
            return CustomAssetLoader.LoadSprite(reference, new Vector2(0.5f, 0.5f));
        }

        /// <summary>The empty parchment square the game draws its ink icons on.</summary>
        public static Sprite Parchment() => NativeUi.ParchmentSquare();

        /// <summary>The picture that stands for a multiworld item.</summary>
        public static string ForItem(string key, string category)
        {
            switch (key)
            {
                case "AP_WALK_SPEED": return "ink:icons_0020";
                case "AP_WAYPOINT_RECALL": return "icons/ink_recall.png";
                case "AP_LIBRARY_PASS": return "ink:icons_0008";
                case "AP_SURFACE_GATE_PASS": return "ink:icons_0013";
                case "SecretKey": return "icons/ink_key.png";
                case "AP_CAVERN_MAP": return "icons/ink_map.png";
                case "AP_GIANT_SPORES":
                case "AP_GREEN_MUSHROOM":
                case "AP_PURPLE_MUSHROOM": return "ink:icons_0006";
                case "MUL4L4L4": return "ink:icons_0010";
                case "PH534TLO0": return "ink:icons_0002";
                case "P4H_FFT": return "ink:icons_0009";
                case "KSTTFF34": return "icons/ink_coal.png";
                case "ITMM0023Z": return "icons/ink_moss.png";
                case "FRST435": return "icons/ink_wood.png";
                case "FRST986": return "icons/ink_flint.png";
                case "5U2LFU6":
                case "STN_CLRS3423": return "ink:icons_0014";
            }
            var relic = Relics.RelicBag.Find(key);
            if (relic != null)
            {
                return relic.Asset;
            }
            var worn = Cosmetics.Wardrobe.Find(key);
            if (worn != null)
            {
                return worn.Asset;
            }
            switch (category)
            {
                case "books": return "ink:icons_0008";
                case "crystals": return "icons/ink_crystal.png";
                case "time": return "ink:icons_0023";
                case "trap": return "ink:icons_0032";
                case "filler": return "ink:icons_0031";
                default: return "icons/ink_bundle.png";
            }
        }
    }
}
