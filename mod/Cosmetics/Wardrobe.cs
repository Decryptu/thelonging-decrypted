using System;
using UnityEngine;

namespace TheLongingDecrypted.Cosmetics
{
    internal sealed class CosmeticDef
    {
        public string Key { get; }
        public string Asset { get; }
        public int Price { get; }
        public bool IsPet { get; }

        public CosmeticDef(string key, string asset, int price, bool isPet = false)
        {
            Key = key;
            Asset = asset;
            Price = price;
            IsPet = isPet;
        }
    }

    /// <summary>Hats, companions, and colors the Shade owns and wears.</summary>
    internal static class Wardrobe
    {
        private const string HatKey = "AP_EQUIPPED_HAT";
        private const string PetKey = "AP_EQUIPPED_PET";
        private const string TintKey = "AP_EQUIPPED_TINT";
        private const string None = "NONE";

        public static readonly CosmeticDef[] Hats =
        {
            new CosmeticDef("AP_HAT_CROWN", "hats/hat_crown.png", 9),
            new CosmeticDef("AP_HAT_HELMET", "hats/hat_helmet.png", 6),
            new CosmeticDef("AP_HAT_MUSHROOM", "hats/hat_mushroom.png", 6),
            new CosmeticDef("AP_HAT_MIFFY", "hats/hat_miffy.png", 6),
            new CosmeticDef("AP_HAT_TOPHAT", "hats/hat_tophat.png", 6),
            new CosmeticDef("AP_HAT_SPIDER", "hats/hat_spider.png", 6),
            new CosmeticDef("AP_HAT_FLOWER", "hats/hat_flower.png", 6),
            new CosmeticDef("AP_HAT_NIGHTCAP", "hats/hat_nightcap.png", 6),
        };

        public static readonly CosmeticDef[] Pets =
        {
            new CosmeticDef("AP_PET_ROCK", "pets/pet_rock.png", 8, true),
            new CosmeticDef("AP_PET_SPIDER", "pets/pet_spider.png", 10, true),
        };

        public static readonly (string Name, Color Tint)[] Tints =
        {
            ("As black as ever", Color.white),
            ("By lamplight", new Color(1.0f, 0.86f, 0.55f, 1f)),
            ("Mossy", new Color(0.68f, 0.96f, 0.72f, 1f)),
            ("Rose quartz", new Color(0.98f, 0.72f, 0.82f, 1f)),
            ("Amethyst", new Color(0.80f, 0.68f, 0.98f, 1f)),
            ("Moonlit", new Color(0.65f, 0.92f, 0.98f, 1f)),
        };

        public static string Hat
        {
            get => Normalize(FileBasedPrefs.GetString(HatKey, None));
            set => Store(HatKey, value);
        }

        public static string Pet
        {
            get => Normalize(FileBasedPrefs.GetString(PetKey, None));
            set => Store(PetKey, value);
        }

        public static int Tint
        {
            get => Mathf.Clamp(FileBasedPrefs.GetInt(TintKey, 0), 0, Tints.Length - 1);
            set => FileBasedPrefs.SetInt(TintKey, value % Tints.Length);
        }

        private static string Normalize(string key)
        {
            return string.IsNullOrEmpty(key) || key == None ? null : key == "AP_HAT_GLASSES" ? "AP_HAT_MIFFY" : key;
        }

        private static void Store(string pref, string key)
        {
            FileBasedPrefs.SetString(pref, string.IsNullOrEmpty(key) ? None : key);
            FileBasedPrefs.ManualySave();
        }

        public static bool Owned(string key)
        {
            return FileBasedPrefs.GetInt(key, 0) > 0 || (key == "AP_HAT_MIFFY" && FileBasedPrefs.GetInt("AP_HAT_GLASSES", 0) > 0);
        }

        /// <summary>Takes a hat or companion, and puts it on if nothing is worn yet.</summary>
        public static void Grant(string key)
        {
            FileBasedPrefs.SetInt(key, FileBasedPrefs.GetInt(key, 0) + 1);
            if (key.StartsWith("AP_HAT_", StringComparison.Ordinal) && Hat == null)
            {
                Hat = key;
            }
            else if (key.StartsWith("AP_PET_", StringComparison.Ordinal) && Pet == null)
            {
                Pet = key;
            }
        }

        public static CosmeticDef Find(string key)
        {
            return Array.Find(Hats, h => h.Key == key) ?? Array.Find(Pets, p => p.Key == key);
        }

        public static bool Equipped(CosmeticDef def) => def.Key == (def.IsPet ? Pet : Hat);

        public static void Toggle(CosmeticDef def)
        {
            string next = Equipped(def) ? null : def.Key;
            if (def.IsPet)
            {
                Pet = next;
            }
            else
            {
                Hat = next;
            }
        }
    }
}
