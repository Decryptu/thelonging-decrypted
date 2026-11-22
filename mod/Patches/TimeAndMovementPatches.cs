using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TheLongingDecrypted.Patches
{
    /// <summary>
    /// Applies multiworld time-rate pacing and Shade walk-speed controls.
    /// </summary>
    internal static class TimeAndMovementPatches
    {
        private static readonly FieldInfo BaseWalkSpeedField = AccessTools.Field(typeof(Movement), "PNCLPHAIAJJ");

        public static float GetEffectiveTimeRate()
        {
            if (Plugin.Delivery != null && Plugin.Delivery.IsTimeStallTrapActive)
            {
                return 1f;
            }
            int baseRate = Plugin.Session != null && Plugin.Session.IsConnected
                ? Plugin.Session.ActiveConfig.StartingTimeRate
                : Plugin.FallbackTimeRateConfig.Value;
            int bonusRate = Plugin.Delivery != null ? Plugin.Delivery.BonusTimeRate : 0;
            return Mathf.Clamp(baseRate + bonusRate, 1f, 120f);
        }

        public static float GetEffectiveWalkMultiplier()
        {
            if (Plugin.Delivery != null && Plugin.Delivery.IsSlownessTrapActive)
            {
                return 0.65f;
            }
            if (Plugin.WalkSpeedOverrideMultiplier > 0f)
            {
                return Plugin.WalkSpeedOverrideMultiplier;
            }
            if (Plugin.Session != null && Plugin.Session.IsConnected)
            {
                return Plugin.Session.ActiveConfig.WalkSpeedMultiplier;
            }
            return Plugin.FallbackWalkMultiplierConfig.Value;
        }

        [HarmonyPatch(typeof(TMan), nameof(TMan.ResetTimeModificator))]
        [HarmonyPostfix]
        private static void KeepMinimumTimeRateOnReset()
        {
            ApplyTimeRateFloor();
        }

        [HarmonyPatch(typeof(TMan), "Update")]
        [HarmonyPrefix]
        private static void KeepMinimumTimeRateOnUpdate()
        {
            ApplyTimeRateFloor();
        }

        private static void ApplyTimeRateFloor()
        {
            float target = GetEffectiveTimeRate();
            if ((float)TMan.DFMLPLGGPLG < target)
            {
                TMan.DFMLPLGGPLG = target;
            }
            else if (Plugin.Delivery != null && Plugin.Delivery.IsTimeStallTrapActive)
            {
                TMan.DFMLPLGGPLG = 1f;
            }
        }

        [HarmonyPatch(typeof(Movement), "Update")]
        [HarmonyPrefix]
        private static void ApplyWalkSpeedOverride(Movement __instance)
        {
            if (__instance == null || BaseWalkSpeedField == null)
            {
                return;
            }
            float mult = GetEffectiveWalkMultiplier();
            if (Math.Abs(mult - 1f) < 0.01f)
            {
                return;
            }
            if (BaseWalkSpeedField.GetValue(__instance) is float baseSpeed && baseSpeed > 0f)
            {
                __instance.OverwritePlayerSpeed(baseSpeed * mult);
                var animator = __instance.GetAnimator();
                if (animator != null && __instance.IsInWalkState())
                {
                    animator.speed = Mathf.Clamp(mult, 0.65f, 2.5f);
                }
            }
        }
    }
}
