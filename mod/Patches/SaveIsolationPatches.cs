using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TheLongingDecrypted.Patches
{
    [HarmonyPatch(typeof(FileBasedPrefs))]
    internal static class SaveIsolationPatches
    {
        private const string ClockOffsetKey = "AP_CLOCK_OFFSET_SECONDS";
        private static bool _clockReady;
        private static readonly FieldInfo ThePathField = AccessTools.Field(typeof(FileBasedPrefs), "thePath");
        private static readonly FieldInfo LastCheckTimeField = AccessTools.Field(typeof(FileBasedPrefs), "lastCheckTime");

        public static string IsolatedDirectory { get; private set; }

        public static void SetIsolatedSlot(string seedName, string slotName)
        {
            string safeSeed = SanitizeFolder(seedName);
            string safeSlot = SanitizeFolder(slotName);
            string folderName = $"{safeSeed}_{safeSlot}";
            string dir = Path.Combine(Application.persistentDataPath, "Archipelago", folderName);
            if (string.Equals(IsolatedDirectory, dir, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            Directory.CreateDirectory(dir);
            IsolatedDirectory = dir;

            ResetSaveCache();
            Plugin.Log.LogInfo($"Isolated randomizer save directory: {dir}");
        }

        public static void SetDefaultRandomizerProfile()
        {
            string dir = Path.Combine(Application.persistentDataPath, "Archipelago", "default_slot");
            Directory.CreateDirectory(dir);
            IsolatedDirectory = dir;
            ResetSaveCache();
        }

        private static void ResetSaveCache()
        {
            _clockReady = false;
            FileBasedPrefs.ResetLoadedSaves();
            // The game's existence cache is shared across save paths.
            LastCheckTimeField?.SetValue(null, -1f);
        }

        private static long UtcSeconds => DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;

        public static bool TryRestoreClock()
        {
            if (!Plugin.IsolateSavesConfig.Value)
            {
                return false;
            }
            if (_clockReady)
            {
                return true;
            }
            if (!long.TryParse(FileBasedPrefs.GetString(ClockOffsetKey, ""), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out long offset))
            {
                return false;
            }

            long elapsed = Math.Max(0L, UtcSeconds + offset);
            long currentElapsed = TMan.GetOverallTime();
            TMan.AddInstaTime(elapsed - currentElapsed);
            _clockReady = true;
            return true;
        }

        public static void EnableClockSaving()
        {
            _clockReady = Plugin.IsolateSavesConfig.Value;
        }

        [HarmonyPatch(nameof(FileBasedPrefs.ManualySave))]
        [HarmonyPrefix]
        private static void SaveClockPrefix()
        {
            if (_clockReady && !FileBasedPrefs.BlockNewSaves)
            {
                long offset = TMan.GetOverallTime() - UtcSeconds;
                FileBasedPrefs.SetString(ClockOffsetKey, offset.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static void EnsureSlotSaveExists()
        {
            if (Plugin.IsolateSavesConfig.Value && !File.Exists(FileBasedPrefs.GetSaveFilePath()))
            {
                FileBasedPrefs.CreateNewSaveFile();
                ResetSaveCache();
            }
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
