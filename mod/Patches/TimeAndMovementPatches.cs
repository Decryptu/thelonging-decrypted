using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using TheLongingDecrypted.Relics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLongingDecrypted.Patches
{
    /// <summary>
    /// Applies multiworld time-rate pacing and Shade walk-speed controls.
    /// </summary>
    internal static class TimeAndMovementPatches
    {
        private static readonly float[] WalkMultipliers = { 1f, 1.25f, 1.5f, 1.75f, 2f };
        private static float? _lastAppliedTimeRate;
        private static float _nativeTimeRate;

        public static float GetEffectiveTimeRate()
        {
            if (Plugin.Delivery != null && Plugin.Delivery.IsTimeStallTrapActive)
            {
                return 1f;
            }
            int baseRate = Plugin.Session != null && Plugin.Session.IsConnected
                ? Plugin.Session.ActiveConfig.StartingTimeRate
                : SceneManager.GetActiveScene().buildIndex >= 3 ? Plugin.FallbackTimeRateConfig.Value : 1;
            int bonusRate = Plugin.Delivery != null ? Plugin.Delivery.BonusTimeRate : 0;
            return Mathf.Clamp(baseRate + bonusRate, 1f, 120f);
        }

        internal static int WalkSpeedTier => Mathf.Clamp(FileBasedPrefs.GetInt("AP_WALK_SPEED", 0), 0, WalkMultipliers.Length - 1);
        internal static float UnlockedWalkMultiplier => WalkMultipliers[WalkSpeedTier];

        [HarmonyPatch(typeof(TMan), nameof(TMan.ResetTimeModificator))]
        [HarmonyPostfix]
        private static void KeepMinimumTimeRateOnReset()
        {
            ApplyTimeRateFloor(timerReset: true);
        }

        [HarmonyPatch(typeof(TMan), "Update")]
        [HarmonyPrefix]
        private static void KeepMinimumTimeRateOnUpdate()
        {
            ApplyTimeRateFloor();
        }

        private static void ApplyTimeRateFloor(bool timerReset = false)
        {
            float current = TMan.DFMLPLGGPLG;
            // Keep the game's rate separately so lowering the AP floor can take effect.
            if (timerReset || current != _lastAppliedTimeRate)
            {
                _nativeTimeRate = current;
            }

            float effectiveRate = Plugin.Delivery != null && Plugin.Delivery.IsTimeStallTrapActive
                ? 1f
                : Mathf.Max(_nativeTimeRate, GetEffectiveTimeRate());
            if (current != effectiveRate)
            {
                TMan.DFMLPLGGPLG = effectiveRate;
            }
            _lastAppliedTimeRate = effectiveRate;
        }

        [HarmonyPatch(typeof(Movement), "APJLCJOMOBM")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ScaleWalkDisplacement(IEnumerable<CodeInstruction> instructions)
        {
            var speedField = AccessTools.Field(typeof(Movement), nameof(Movement.MPPKGMBHPAN));
            var scaledSpeed = AccessTools.Method(typeof(TimeAndMovementPatches), nameof(GetWalkDisplacementSpeed));
            foreach (var instruction in instructions)
            {
                if (instruction.LoadsField(speedField))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = scaledSpeed;
                }
                yield return instruction;
            }
        }

        private static float GetWalkDisplacementSpeed(Movement movement)
        {
            float speed = movement.MPPKGMBHPAN;
            if (string.IsNullOrEmpty(Plugin.Session?.ConnectedSlot) || movement.KOOPDFHCIIF
                || movement.IsCrouching() || !GameManagerScript.IsGameRunning())
            {
                return speed;
            }
            float mult = Plugin.Delivery.IsSlownessTrapActive ? 0.65f : UnlockedWalkMultiplier;
            if (RelicBag.EspressoActive)
            {
                mult *= 1.5f;
            }
            return speed * mult;
        }
    }
}
