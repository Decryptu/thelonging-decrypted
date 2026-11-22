using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TheLongingDecrypted.Patches
{
    [HarmonyPatch(typeof(FileBasedPrefs))]
    internal static class SaveIsolationPatches
    {
        private static readonly FieldInfo ThePathField = AccessTools.Field(typeof(FileBasedPrefs), "thePath");

        public static string IsolatedDirectory { get; private set; }

        public static void SetIsolatedSlot(string seedName, string slotName)
        {
            string safeSeed = SanitizeFolder(seedName);
            string safeSlot = SanitizeFolder(slotName);
            string folderName = $"{safeSeed}_{safeSlot}";
            string dir = Path.Combine(Application.persistentDataPath, "Archipelago", folderName);
            Directory.CreateDirectory(dir);
            IsolatedDirectory = dir;

            // Force FileBasedPrefs to reload from the isolated directory
            FileBasedPrefs.ResetLoadedSaves();
            Plugin.Log.LogInfo($"Isolated randomizer save directory: {dir}");
        }

        public static void SetDefaultRandomizerProfile()
        {
            string dir = Path.Combine(Application.persistentDataPath, "Archipelago", "default_slot");
            Directory.CreateDirectory(dir);
            IsolatedDirectory = dir;
            FileBasedPrefs.ResetLoadedSaves();
        }

        [HarmonyPatch("InitializePath")]
        [HarmonyPostfix]
        private static void InitializePathPostfix()
        {
            if (!Plugin.IsolateSavesConfig.Value || string.IsNullOrEmpty(IsolatedDirectory))
            {
                return;
            }
            if (ThePathField != null)
            {
                ThePathField.SetValue(null, IsolatedDirectory);
            }
        }

        [HarmonyPatch("DuplicateOldSave")]
        [HarmonyPrefix]
        private static bool DuplicateOldSavePrefix(ref bool __result)
        {
            if (Plugin.IsolateSavesConfig.Value)
            {
                __result = false;
                return false;
            }
            return true;
        }

        private static string SanitizeFolder(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "slot";
            }
            var chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                {
                    chars[i] = '_';
                }
            }
            string cleaned = new string(chars);
            return cleaned.Length > 32 ? cleaned.Substring(0, 32) : cleaned;
        }
    }
}
