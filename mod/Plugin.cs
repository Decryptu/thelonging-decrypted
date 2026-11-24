using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TheLongingDecrypted.Archipelago;
using TheLongingDecrypted.Cosmetics;
using TheLongingDecrypted.Patches;
using TheLongingDecrypted.Peddler;
using TheLongingDecrypted.Relics;
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
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Log { get; private set; }
        internal static ItemDeliveryService Delivery { get; private set; }
        internal static ArchipelagoSessionManager Session { get; private set; }

        internal static ConfigEntry<bool> IsolateSavesConfig { get; private set; }
        internal static ConfigEntry<string> DefaultHostConfig { get; private set; }
        internal static ConfigEntry<int> DefaultPortConfig { get; private set; }
        internal static ConfigEntry<string> DefaultSlotConfig { get; private set; }
        internal static ConfigEntry<string> DefaultPasswordConfig { get; private set; }
        internal static ConfigEntry<int> FallbackTimeRateConfig { get; private set; }

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
            _harmony.PatchAll(typeof(WaypointRecallPatches));
            _harmony.PatchAll(typeof(WaypointPicturePatches));
            _harmony.PatchAll(typeof(PeddlerStall));
            _harmony.PatchAll(typeof(RelicBag));
            _harmony.PatchAll(typeof(MultiworldPanel));
            _harmony.PatchAll(typeof(FanficBookPatches));

            SceneManager.sceneLoaded += LocationCheckPatches.OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            var runtime = new GameObject("TheLongingDecrypted");
            DontDestroyOnLoad(runtime);
            runtime.AddComponent<ModRuntime>();
            runtime.AddComponent<FrameHud>();
            runtime.AddComponent<TroubleOverlay>();
            runtime.AddComponent<MultiworldPanel>();
            runtime.AddComponent<PeddlerPanel>();
            runtime.AddComponent<ShadeDresser>();

            Log.LogInfo($"{PluginName} v{PluginVersion} initialized (BepInEx 5 x86).");
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= LocationCheckPatches.OnSceneLoaded;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Session?.Disconnect();
            _harmony?.UnpatchSelf();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            NativeUi.OnSceneChanged();
            PeddlerStall.OnSceneLoaded(scene, mode);
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
