using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace TheLongingDecrypted.Patches
{
    /// <summary>
    /// Keeps the pictures of marked places (LongingMap1.jpeg and so on) next to the randomizer save, so a slot never
    /// replaces the ones that belong to the vanilla save.
    /// </summary>
    [HarmonyPatch]
    internal static class WaypointPicturePatches
    {
        [HarmonyPatch(typeof(AutoMoveWPSystem), "LateUpdate")]
        [HarmonyPatch(typeof(AutoMoveWPSystem), nameof(AutoMoveWPSystem.GetWaypointPic))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> UseSlotDirectory(IEnumerable<CodeInstruction> instructions)
        {
            var shared = AccessTools.PropertyGetter(typeof(Application), nameof(Application.persistentDataPath));
            var isolated = AccessTools.Method(typeof(WaypointPicturePatches), nameof(PictureDirectory));
            return instructions.Select(i => i.Calls(shared) ? new CodeInstruction(OpCodes.Call, isolated) { labels = i.labels, blocks = i.blocks } : i);
        }

        private static string PictureDirectory()
        {
            return Plugin.IsolateSavesConfig.Value && !string.IsNullOrEmpty(SaveIsolationPatches.IsolatedDirectory)
                ? SaveIsolationPatches.IsolatedDirectory
                : Application.persistentDataPath;
        }
    }
}
