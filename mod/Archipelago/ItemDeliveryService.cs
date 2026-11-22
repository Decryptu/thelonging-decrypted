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

        public IEnumerable<string> RecentNotifications => _recentNotifications;

        public bool IsSlownessTrapActive => Time.unscaledTime < SlownessTrapUntil;
        public bool IsDarknessTrapActive => Time.unscaledTime < DarknessTrapUntil;
        public bool IsTimeStallTrapActive => Time.unscaledTime < TimeStallTrapUntil;

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
                    FileBasedPrefs.SetInt(entry.GameKey, FileBasedPrefs.GetInt(entry.GameKey, 0) + entry.Amount);
                    break;
                case "mushroom":
                    FileBasedPrefs.SetInt(entry.GameKey, FileBasedPrefs.GetInt(entry.GameKey, 0) + entry.Amount);
                    break;
                case "time_skip":
                    TMan.AddInstaTime((long)entry.Amount);
                    TMan.SaveValues();
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

            FileBasedPrefs.ManualySave();

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
            }
        }
    }
}
