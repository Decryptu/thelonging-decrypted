using HarmonyLib;

namespace TheLongingDecrypted.Patches
{
    [HarmonyPatch]
    internal static class ProgressionPatches
    {
        private const string GiantMushroomUid = "pilz1_greatcave_trampolin_01";

        [HarmonyPatch(typeof(Interactable_DoorOpening_Library), nameof(Interactable_DoorOpening_Library.Interact))]
        [HarmonyPrefix]
        internal static bool CanEnterLibrary()
        {
            return AllowInteraction(Plugin.Session?.ActiveConfig.ShuffleMattocksAndKeys == true,
                "AP_LIBRARY_PASS", "Library Door Pass");
        }

        [HarmonyPatch(typeof(Interactable_DoorOpening_Up07), nameof(Interactable_DoorOpening_Up07.Interact))]
        [HarmonyPrefix]
        private static bool CheckUpperGate()
        {
            return AllowInteraction(Plugin.Session?.ActiveConfig.ShuffleMattocksAndKeys == true,
                "AP_SURFACE_GATE_PASS", "Surface Gate Pass");
        }

        [HarmonyPatch(typeof(Interactable_MushroomGrow), nameof(Interactable_MushroomGrow.Interact))]
        [HarmonyPrefix]
        private static bool CheckGiantMushroom(Interactable_MushroomGrow __instance)
        {
            var config = Plugin.Session?.ActiveConfig;
            bool shuffled = config != null && (config.ShuffleMattocksAndKeys || config.ShuffleMushroomsAndMoss);
            // The mushroom caches this ID lazily. Read it from the progress component on first interaction.
            return __instance.GetComponent<ProgressClass>()?.GetUID() != GiantMushroomUid
                || AllowInteraction(shuffled, "AP_GIANT_SPORES", "Giant Mushroom Spores");
        }

        private static bool AllowInteraction(bool shuffled, string unlockKey, string itemName)
        {
            // The selected slot and its save remain active on disconnect.
            if (!shuffled || string.IsNullOrEmpty(Plugin.Session?.ConnectedSlot) || Plugin.Delivery.HasUnlock(unlockKey))
            {
                return true;
            }

            Plugin.Delivery.PushNotification($"Requires {itemName}.");
            return false;
        }
    }
}
