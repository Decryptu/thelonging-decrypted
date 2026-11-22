using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TheLongingDecrypted.Archipelago;
using TheLongingDecrypted.Patches;
using TheLongingDecrypted.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLongingDecrypted
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.decrypt.thelongingdecrypted";
        public const string PluginName = "The Longing Decrypted";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log { get; private set; }
        internal static ItemDeliveryService Delivery { get; private set; }
        internal static ArchipelagoSessionManager Session { get; private set; }
        internal static float WalkSpeedOverrideMultiplier { get; set; } = -1f;

        internal static ConfigEntry<bool> IsolateSavesConfig { get; private set; }
        internal static ConfigEntry<string> DefaultHostConfig { get; private set; }
        internal static ConfigEntry<int> DefaultPortConfig { get; private set; }
        internal static ConfigEntry<string> DefaultSlotConfig { get; private set; }
        internal static ConfigEntry<string> DefaultPasswordConfig { get; private set; }
        internal static ConfigEntry<int> FallbackTimeRateConfig { get; private set; }
        internal static ConfigEntry<float> FallbackWalkMultiplierConfig { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            IsolateSavesConfig = Config.Bind(
                "Saves",
                "IsolateRandomizerSaves",
                true,
                "Keep randomizer saves in AppData/LocalLow/Studio Seufz/The Longing/Archipelago/ so your vanilla 400-day save stays untouched."
            );
            DefaultHostConfig = Config.Bind("Archipelago", "Host", "archipelago.gg", "Archipelago server hostname.");
            DefaultPortConfig = Config.Bind("Archipelago", "Port", 38281, "Archipelago server port.");
            DefaultSlotConfig = Config.Bind("Archipelago", "SlotName", "Shade", "Player slot name from your YAML.");
            DefaultPasswordConfig = Config.Bind("Archipelago", "Password", "", "Optional room password.");
            FallbackTimeRateConfig = Config.Bind("Pacing", "OfflineTimeRate", 12, "Time multiplier before connecting to an Archipelago slot.");
            FallbackWalkMultiplierConfig = Config.Bind("Pacing", "OfflineWalkMultiplier", 1.75f, "Shade walk speed multiplier before connecting.");

            if (IsolateSavesConfig.Value)
            {
                SaveIsolationPatches.SetDefaultRandomizerProfile();
            }

            Delivery = new ItemDeliveryService();
            Session = new ArchipelagoSessionManager(Delivery);

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(SaveIsolationPatches));
            _harmony.PatchAll(typeof(AntiCheatPatches));
            _harmony.PatchAll(typeof(TimeAndMovementPatches));
            _harmony.PatchAll(typeof(LocationCheckPatches));
            _harmony.PatchAll(typeof(ProgressionPatches));

            SceneManager.sceneLoaded += LocationCheckPatches.OnSceneLoaded;

            var overlayGo = new GameObject("TheLongingDecryptedOverlay");
            DontDestroyOnLoad(overlayGo);
            overlayGo.AddComponent<TrackerOverlay>();

            Log.LogInfo($"{PluginName} v{PluginVersion} initialized (BepInEx 5 x86).");
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= LocationCheckPatches.OnSceneLoaded;
            Session?.Disconnect();
            _harmony?.UnpatchSelf();
        }

        internal static void ActivateSlotSaveIsolation(string seedName, string slotName)
        {
            if (IsolateSavesConfig.Value)
            {
                SaveIsolationPatches.SetIsolatedSlot(seedName, slotName);
            }
        }
    }
}
