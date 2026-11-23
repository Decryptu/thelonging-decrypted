using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TheLongingDecrypted.Generated;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLongingDecrypted.Patches
{
    /// <summary>
    /// Intercepts item pickups, milestone events, and ending triggers to report Archipelago checks
    /// and suppress vanilla item grants for shuffled locations.
    /// </summary>
    internal static class LocationCheckPatches
    {
        private static readonly FieldInfo ProgressUidField = AccessTools.Field(typeof(ProgressClass), "ENMHNKMPDCI");
        private static readonly FieldInfo OutcomeItemKeyField = AccessTools.Field(typeof(InteractableOutcome_Item), "OPMCGHDHJHA");
        private static readonly LocationRegistryEntry[] Discoveries = WorldRegistryData.LocationsById.Values
            .Where(entry => entry.Pool == "mobility").ToArray();

        [HarmonyPatch(typeof(InteractableOutcome_Item), nameof(InteractableOutcome_Item.FullfillOutcome))]
        [HarmonyPrefix]
        private static bool InterceptItemOutcome(InteractableOutcome_Item __instance, ref IEnumerator __result)
        {
            if (Plugin.Session == null || !Plugin.Session.IsConnected || __instance == null)
            {
                return true;
            }

            string uid = ResolveLocationUid(__instance);
            if (string.IsNullOrEmpty(uid))
            {
                return true;
            }

            if (WorldRegistryData.LocationsByUid.TryGetValue(uid, out var locEntry)
                && Plugin.Session.IsLocationShuffled(locEntry.Id))
            {
                Plugin.Session.ReportLocation(locEntry.Id, locEntry.Name);
                // Suppress vanilla item grant; AP server delivers the randomized item instead.
                __result = EmptyOutcome();
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(InteractableOutcome_MapReveal), nameof(InteractableOutcome_MapReveal.FullfillOutcome))]
        [HarmonyPostfix]
        private static void OnMapReveal()
        {
            Plugin.Session?.ReportLocationByUid("event:map_reveal");
        }

        [HarmonyPatch(typeof(InteractableOutcome_HackIntoWall), nameof(InteractableOutcome_HackIntoWall.FullfillOutcome))]
        [HarmonyPostfix]
        private static void OnWallHacked(InteractableOutcome_HackIntoWall __instance)
        {
            if (__instance != null && SceneManager.GetActiveScene().name == "home")
            {
                Plugin.Session?.ReportLocationByUid("event:home_wall");
            }
        }

        [HarmonyPatch(typeof(KingEnding), "Start")]
        [HarmonyPostfix]
        private static void OnKingEndingStart()
        {
            Plugin.Session?.NotifyEndingReached("wake_the_king");
        }

        [HarmonyPatch(typeof(SuicideEnding), "Start")]
        [HarmonyPostfix]
        private static void OnSuicideEndingStart()
        {
            Plugin.Session?.NotifyEndingReached("secret_ending");
            Plugin.Session?.SendDeathLink("The Shade leaped from the cliff.");
        }

        [HarmonyPatch(typeof(TragedyEnding), "Start")]
        [HarmonyPostfix]
        private static void OnTragedyEndingStart()
        {
            Plugin.Session?.NotifyEndingReached("escape_surface");
            Plugin.Session?.SendDeathLink("The Shade fell victim to the Troll at the Well.");
        }

        public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RecordDiscovery(scene.name);
            if (Plugin.Session == null || !Plugin.Session.IsConnected)
            {
                return;
            }

            switch (scene.name)
            {
                case "brunnen":
                    Plugin.Session.ReportLocationByUid("event:well_arrival");
                    break;
                case "oberwelt":
                case "ende oberwelt":
                    Plugin.Session.NotifyEndingReached("escape_surface");
                    break;
                case "ende_king":
                    Plugin.Session.NotifyEndingReached("wake_the_king");
                    break;
            }

            PollMilestonePrefs();
        }

        public static void PollMilestonePrefs()
        {
            if (GameManagerScript.IsGameRunning())
            {
                RecordDiscovery(SceneManager.GetActiveScene().name);
            }
            if (Plugin.Session == null || !Plugin.Session.IsConnected)
            {
                return;
            }

            foreach (var discovery in Discoveries)
            {
                if (FileBasedPrefs.HasKey(discovery.GameUid))
                {
                    Plugin.Session.ReportLocation(discovery.Id, discovery.Name);
                }
            }

            if (FileBasedPrefs.GetInt("home_kamin_feuer_10", 0) > 0 || FileBasedPrefs.HasKey("xv897_ASDf7home_kamin_feuer_10STS"))
            {
                Plugin.Session.ReportLocationByUid("event:hearth_fire");
            }
            if (FileBasedPrefs.HasKey("xv897_ASDf7sulfur_smellyfire_5STS"))
            {
                Plugin.Session.ReportLocationByUid("event:sulfur_fire");
            }
            if (FileBasedPrefs.HasKey("xv897_ASDf7pilz1_greatcave_trampolin_01STS"))
            {
                Plugin.Session.ReportLocationByUid("event:giant_mushroom");
            }
            if (FileBasedPrefs.GetInt("DRAARRIN31", 0) > 0)
            {
                Plugin.Session.ReportLocationByUid("event:home_draw");
            }
            if (FileBasedPrefs.GetInt("BedBuilt", 0) > 0 || FileBasedPrefs.GetFloat("slpt", 0f) > 0f)
            {
                Plugin.Session.ReportLocationByUid("event:build_bed");
            }
            if (FileBasedPrefs.GetInt("musicNoteClicked", 0) > 0)
            {
                Plugin.Session.ReportLocationByUid("event:home_music");
            }
        }

        private static void RecordDiscovery(string scene)
        {
            var session = Plugin.Session;
            if (string.IsNullOrEmpty(session?.ConnectedSlot) || FileBasedPrefs.BlockNewSaves)
            {
                return;
            }

            if (WorldRegistryData.LocationsByUid.TryGetValue($"discover:{scene}", out var discovery)
                && session.ActiveConfig.EnabledLocationIds.Contains(discovery.Id)
                && !FileBasedPrefs.HasKey(discovery.GameUid))
            {
                FileBasedPrefs.SetInt(discovery.GameUid, 1);
                FileBasedPrefs.ManualySave();
            }
        }

        private static string ResolveLocationUid(InteractableOutcome_Item outcome)
        {
            var progress = outcome.GetComponent<ProgressClass>();
            if (progress != null && ProgressUidField != null)
            {
                string uid = ProgressUidField.GetValue(progress) as string;
                if (!string.IsNullOrEmpty(uid))
                {
                    return uid;
                }
            }

            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName == "halls_of_eternity" && OutcomeItemKeyField != null)
            {
                string itemKey = OutcomeItemKeyField.GetValue(outcome) as string;
                if (itemKey == "T33PCHZWO" || itemKey == "T33PCHIERD" || itemKey == "VROHG")
                {
                    return "eternity:" + itemKey;
                }
            }

            return $"{sceneName}:{outcome.gameObject.name}";
        }

        private static IEnumerator EmptyOutcome()
        {
            yield break;
        }
    }
}
