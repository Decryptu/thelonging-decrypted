using System;
using System.Collections.Generic;
using CodeStage.AntiCheat.ObscuredTypes;
using TheLongingDecrypted.Generated;
using UnityEngine;

namespace TheLongingDecrypted.Archipelago
{
    public sealed class ItemDeliveryService
    {
        public const string BonusTimeRatePrefKey = "AP_BONUS_TIME_RATE";
        public const string ReceivedCrystalsPrefKey = "AP_RECEIVED_CRYSTALS";
        public const string ReceivedBooksPrefKey = "AP_RECEIVED_BOOKS";

        private readonly Queue<string> _recentNotifications = new Queue<string>();
        private const int MaxNotifications = 10;

        public float SlownessTrapUntil { get; private set; }
        public float DarknessTrapUntil { get; private set; }
        public float TimeStallTrapUntil { get; private set; }
        public float EspressoBuffUntil { get; private set; }
        public float ExistentialTrapUntil { get; private set; }
        public float HiccupTrapUntil { get; private set; }
        public float SpiderScareUntil { get; private set; }
        public float FalseWakeUntil { get; private set; }
        public float TinyShadeUntil { get; private set; }
        public string ActiveExistentialThought { get; private set; } = string.Empty;

        public IEnumerable<string> RecentNotifications => _recentNotifications;

        public bool IsSlownessTrapActive => Time.unscaledTime < SlownessTrapUntil;
        public bool IsDarknessTrapActive => Time.unscaledTime < DarknessTrapUntil;
        public bool IsTimeStallTrapActive => Time.unscaledTime < TimeStallTrapUntil;
        public bool IsEspressoBuffActive => Time.unscaledTime < EspressoBuffUntil;
        public bool IsExistentialTrapActive => Time.unscaledTime < ExistentialTrapUntil;
        public bool IsHiccupTrapActive => Time.unscaledTime < HiccupTrapUntil;
        public bool IsSpiderScareActive => Time.unscaledTime < SpiderScareUntil;
        public bool IsFalseWakeActive => Time.unscaledTime < FalseWakeUntil;
        public bool IsTinyShadeActive => Time.unscaledTime < TinyShadeUntil;

        private static readonly string[] ExistentialThoughts =
        {
            "\"If I wait 400 days to wake a King who immediately ends the world... am I the villain, or just middle management?\"",
            "\"What if the Mattock isn't breaking because the rock is hard, but because it simply lacks the emotional support to continue?\"",
            "\"I have walked 40,000 steps in total darkness to stare at a stalactite. Peak subterranean gameplay.\"",
            "\"Do the spiders think I'm a weird yellow roommate who refuses to pay rent?\"",
            "\"If time flows faster when my home is cozy, does a fourth carpet bend the space-time continuum?\""
        };

        public int BonusTimeRate => FileBasedPrefs.GetInt(BonusTimeRatePrefKey, 0);
        public int ReceivedCrystals => FileBasedPrefs.GetInt(ReceivedCrystalsPrefKey, 0);
        public int ReceivedBooks => FileBasedPrefs.GetInt(ReceivedBooksPrefKey, 0);

        public void DeliverItem(long itemId, string senderName, bool replaySilent = false)
        {
            if (!WorldRegistryData.ItemsById.TryGetValue(itemId, out var entry))
            {
                Plugin.Log.LogWarning($"Received unknown AP item id {itemId}");
                return;
            }

            switch (entry.Action)
            {
                case "item":
                    GrantInventoryItem(entry);
                    break;
                case "unlock":
                case "cosmetic":
                    FileBasedPrefs.SetInt(entry.GameKey, FileBasedPrefs.GetInt(entry.GameKey, 0) + entry.Amount);
                    FileBasedPrefs.SetInt("AP_UNLOCK_" + entry.GameKey, 1);
                    if (entry.Category == "mobility")
                    {
                        TheLongingDecrypted.Patches.WaypointRecallPatches.RefreshMobilityLabel();
                    }
                    else if (entry.Category == "cosmetic" || entry.Action == "cosmetic")
                    {
                        TheLongingDecrypted.Patches.CosmeticAndCustomItemPatches.AutoEquipIfNone(entry.GameKey);
                    }
                    break;
                case "conch":
                    FileBasedPrefs.SetInt(entry.GameKey, FileBasedPrefs.GetInt(entry.GameKey, 0) + entry.Amount);
                    FileBasedPrefs.SetInt("AP_UNLOCK_" + entry.GameKey, 1);
                    if (!replaySilent)
                    {
                        Plugin.Session?.ScoutRandomUncheckedLocation("Echoing Conch");
                    }
                    break;
                case "fertilizer":
                    FileBasedPrefs.SetInt(entry.GameKey, FileBasedPrefs.GetInt(entry.GameKey, 0) + entry.Amount);
                    FileBasedPrefs.SetInt("AP_UNLOCK_" + entry.GameKey, 1);
                    if (!replaySilent)
                    {
                        TheLongingDecrypted.Patches.CosmeticAndCustomItemPatches.ApplyMushroomFertilizer();
                    }
                    break;
                case "buff":
                    FileBasedPrefs.SetInt(entry.GameKey, FileBasedPrefs.GetInt(entry.GameKey, 0) + 1);
                    FileBasedPrefs.SetInt("AP_UNLOCK_" + entry.GameKey, 1);
                    if (!replaySilent && (entry.GameKey == "AP_BUFF_ESPRESSO" || entry.GameKey == "BUFF_ESPRESSO"))
                    {
                        TriggerEspressoBuff(entry.Amount);
                    }
                    break;
                case "mushroom":
                    FileBasedPrefs.SetInt(entry.GameKey, FileBasedPrefs.GetInt(entry.GameKey, 0) + entry.Amount);
                    break;
                case "time_skip":
                    if (entry.GameKey == "TIME_SKIP_43200")
                    {
                        FileBasedPrefs.SetInt("AP_UNLOCK_TIME_SKIP_43200", 1);
                    }
                    if (!replaySilent)
                    {
                        TMan.AddInstaTime((long)entry.Amount);
                        TMan.SaveValues();
                    }
                    break;
                case "time_rate":
                    FileBasedPrefs.SetInt(BonusTimeRatePrefKey, BonusTimeRate + entry.Amount);
                    break;
                case "trap":
                    if (!replaySilent)
                    {
                        TriggerTrap(entry);
                    }
                    break;
                case "filler":
                default:
                    break;
            }

            if (entry.Category == "crystals")
            {
                FileBasedPrefs.SetInt(ReceivedCrystalsPrefKey, ReceivedCrystals + entry.Amount);
            }
            else if (entry.Category == "books")
            {
                FileBasedPrefs.SetInt(ReceivedBooksPrefKey, ReceivedBooks + entry.Amount);
            }

            if (!replaySilent)
            {
                var from = string.IsNullOrEmpty(senderName) ? "Multiworld" : senderName;
                PushNotification($"Received {entry.Name} (from {from})");
            }
        }

        public bool HasUnlock(string unlockKey)
        {
            return FileBasedPrefs.GetInt(unlockKey, 0) > 0;
        }

        public void PushNotification(string message)
        {
            _recentNotifications.Enqueue($"[{DateTime.Now:HH:mm:ss}] {message}");
            while (_recentNotifications.Count > MaxNotifications)
            {
                _recentNotifications.Dequeue();
            }
            Plugin.Log.LogInfo(message);
        }

        private static void GrantInventoryItem(ItemRegistryEntry entry)
        {
            var key = entry.GameKey;
            if (key == "B00BZ")
            {
                int currentBooks = ObscuredPrefs.GetInt("B00BZ", 0);
                if (currentBooks < 40)
                {
                    ObscuredPrefs.SetInt("B00BZ", Math.Min(40, currentBooks + entry.Amount));
                }
            }
            else if (key == "ITMM0023Z")
            {
                int mult = FileBasedPrefs.GetInt("AP_CUSTOM_MOSS_SACK", 0) > 0 ? 2 : 1;
                ObscuredPrefs.SetInt(key, ObscuredPrefs.GetInt(key, 0) + entry.Amount * mult);
            }
            else
            {
                ObscuredPrefs.SetInt(key, ObscuredPrefs.GetInt(key, 0) + entry.Amount);
            }

            if (!string.IsNullOrEmpty(entry.SecondaryKey))
            {
                ObscuredPrefs.SetInt(
                    entry.SecondaryKey,
                    ObscuredPrefs.GetInt(entry.SecondaryKey, 0) + entry.Amount
                );
            }
        }

        public void TriggerEspressoBuff(int durationSeconds = 180)
        {
            EspressoBuffUntil = Math.Max(EspressoBuffUntil, Time.unscaledTime) + durationSeconds;
        }

        private void TriggerTrap(ItemRegistryEntry entry)
        {
            switch (entry.GameKey)
            {
                case "TRAP_SLOWNESS":
                    SlownessTrapUntil = Math.Max(SlownessTrapUntil, Time.unscaledTime) + entry.Amount;
                    break;
                case "TRAP_DARKNESS":
                    DarknessTrapUntil = Math.Max(DarknessTrapUntil, Time.unscaledTime) + entry.Amount;
                    break;
                case "TRAP_TIME_STALL":
                    TimeStallTrapUntil = Math.Max(TimeStallTrapUntil, Time.unscaledTime) + entry.Amount;
                    break;
                case "DISUAD&JSAD§232":
                    ObscuredPrefs.SetInt("DISUAD&JSAD§232", ObscuredPrefs.GetInt("DISUAD&JSAD§232", 0) + 1);
                    break;
                case "TRAP_EXISTENTIAL":
                    ExistentialTrapUntil = Math.Max(ExistentialTrapUntil, Time.unscaledTime) + entry.Amount;
                    ActiveExistentialThought = ExistentialThoughts[UnityEngine.Random.Range(0, ExistentialThoughts.Length)];
                    break;
                case "TRAP_HICCUP":
                    HiccupTrapUntil = Math.Max(HiccupTrapUntil, Time.unscaledTime) + entry.Amount;
                    break;
                case "TRAP_SPIDER_SCARE":
                    SpiderScareUntil = Math.Max(SpiderScareUntil, Time.unscaledTime) + entry.Amount;
                    break;
                case "TRAP_FALSE_WAKE":
                    FalseWakeUntil = Math.Max(FalseWakeUntil, Time.unscaledTime) + entry.Amount;
                    break;
                case "TRAP_TINY_SHADE":
                    TinyShadeUntil = Math.Max(TinyShadeUntil, Time.unscaledTime) + entry.Amount;
                    break;
            }
        }
    }
}
