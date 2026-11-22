using CodeStage.AntiCheat.Detectors;
using HarmonyLib;

namespace TheLongingDecrypted.Patches
{
    /// <summary>
    /// Cleanly disables ACTk speed/time/obscured detectors and prison warps so
    /// multiworld time-skips, accelerated time rates, and brisk walking never lock the player in prison.
    /// </summary>
    internal static class AntiCheatPatches
    {
        [HarmonyPatch(typeof(SplashScreen), "Awake")]
        [HarmonyPrefix]
        private static void SkipSplashDetectorStartup(ref bool ___BCBOGFCDFGP)
        {
            // Use the game's detector-free startup branch, which still initializes the save crypto key.
            ___BCBOGFCDFGP = false;
        }

        [HarmonyPatch(typeof(SpeedHackDetector), "StartDetectionInternal")]
        [HarmonyPrefix]
        private static bool SkipSpeedHackDetector()
        {
            return false;
        }

        [HarmonyPatch(typeof(TimeCheatingDetector), "StartDetectionInternal")]
        [HarmonyPrefix]
        private static bool SkipTimeCheatingDetector()
        {
            return false;
        }

        [HarmonyPatch(typeof(ObscuredCheatingDetector), "StartDetectionInternal")]
        [HarmonyPrefix]
        private static bool SkipObscuredCheatingDetector()
        {
            return false;
        }

        [HarmonyPatch(typeof(DetectionClass), "TimeSpaceContinuumDetected")]
        [HarmonyPrefix]
        private static bool SuppressTimeSpaceDetection()
        {
            return false;
        }

        [HarmonyPatch(typeof(DetectionClass), "MemoryHackDetected")]
        [HarmonyPrefix]
        private static bool SuppressMemoryHackDetection()
        {
            return false;
        }

        [HarmonyPatch(typeof(DetectionClass), "ObscuredHoneypotDetected")]
        [HarmonyPrefix]
        private static bool SuppressObscuredHoneypot()
        {
            return false;
        }

        [HarmonyPatch(typeof(GameManagerScript), nameof(GameManagerScript.LoadPrision))]
        [HarmonyPrefix]
        private static bool SuppressPrisonLoad()
        {
            Plugin.Log.LogInfo("Suppressed anti-cheat prison redirect in randomizer mode.");
            return false;
        }
    }
}
