using System;
using System.Collections.Generic;
using System.Linq;
using CodeStage.AntiCheat.ObscuredTypes;
using TheLongingDecrypted.Cosmetics;
using TheLongingDecrypted.Generated;
using TheLongingDecrypted.Patches;
using TheLongingDecrypted.Relics;
using TheLongingDecrypted.UI;
using UnityEngine;

namespace TheLongingDecrypted.Archipelago
{
    public sealed class ItemDeliveryService
    {
        public const string BonusTimeRatePrefKey = "AP_BONUS_TIME_RATE";
        public const string ReceivedCrystalsPrefKey = "AP_RECEIVED_CRYSTALS";
        public const string ReceivedBooksPrefKey = "AP_RECEIVED_BOOKS";

        private const int MaxNotes = 10;
        private readonly Queue<string> _notes = new Queue<string>();
        private readonly Dictionary<string, float> _trapUntil = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _trapLength = new Dictionary<string, float>();
        private int _quietArrivals;

        public bool IsSlownessTrapActive => TrapFractionLeft("TRAP_SLOWNESS") > 0f;
        public bool IsDarknessTrapActive => TrapFractionLeft("TRAP_DARKNESS") > 0f;
        public bool IsTimeStallTrapActive => TrapFractionLeft("TRAP_TIME_STALL") > 0f;
        public bool IsHiccupTrapActive => TrapFractionLeft("TRAP_HICCUP") > 0f;
        public bool IsSpiderScareActive => TrapFractionLeft("TRAP_SPIDER_SCARE") > 0f;
        public bool IsTinyShadeActive => TrapFractionLeft("TRAP_TINY_SHADE") > 0f;

        public int BonusTimeRate => FileBasedPrefs.GetInt(BonusTimeRatePrefKey, 0);
        public int ReceivedCrystals => FileBasedPrefs.GetInt(ReceivedCrystalsPrefKey, 0);
        public int ReceivedBooks => FileBasedPrefs.GetInt(ReceivedBooksPrefKey, 0);

        public IEnumerable<string> LatestNotes(int count) => _notes.Reverse().Take(count);

        public bool HasUnlock(string unlockKey) => FileBasedPrefs.GetInt(unlockKey, 0) > 0;

        public void DeliverItem(long itemId, string senderName, bool replaySilent = false, bool fromMyself = false)
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
                    if (RelicBag.Find(entry.GameKey) != null)
                    {
                        RelicBag.Grant(entry.GameKey);
                        break;
                    }
                    FileBasedPrefs.SetInt(entry.GameKey, FileBasedPrefs.GetInt(entry.GameKey, 0) + entry.Amount);
                    if (entry.Category == "mobility")
                    {
                        WaypointRecallPatches.RefreshMobilityLabel();
                    }
                    break;
                case "cosmetic":
                    Wardrobe.Grant(entry.GameKey);
                    break;
                case "conch":
                case "fertilizer":
                case "buff":
                    RelicBag.Grant(entry.GameKey);
                    break;
                case "mushroom":
                    FileBasedPrefs.SetInt(entry.GameKey, FileBasedPrefs.GetInt(entry.GameKey, 0) + entry.Amount);
                    break;
                case "time_skip":
                    if (entry.GameKey == RelicBag.Snooze)
                    {
                        RelicBag.Grant(entry.GameKey);
                    }
                    else if (!replaySilent)
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
            }

            if (entry.Category == "crystals")
            {
                FileBasedPrefs.SetInt(ReceivedCrystalsPrefKey, ReceivedCrystals + entry.Amount);
            }
            else if (entry.Category == "books")
            {
                FileBasedPrefs.SetInt(ReceivedBooksPrefKey, ReceivedBooks + entry.Amount);
            }

            if (replaySilent)
            {
                _quietArrivals++;
                return;
            }
            Announce(entry, senderName, fromMyself);
        }

        /// <summary>Sums up what arrived while the game was closed.</summary>
        public void AnnounceQuietArrivals()
        {
            if (_quietArrivals > 0)
            {
                Note(Lines.WhileAway(_quietArrivals));
                FrameHud.Say(Lines.WhileAwayTitle, Lines.WhileAway(_quietArrivals), "ink:icons_0023");
                _quietArrivals = 0;
            }
        }

        private void Announce(ItemRegistryEntry entry, string senderName, bool fromMyself)
        {
            Note(fromMyself || string.IsNullOrEmpty(senderName) ? $"I found {entry.Name}." : $"{senderName} left {entry.Name}.");
            if (entry.Action == "trap")
            {
                return;
            }
            string key = entry.Action == "time_rate" ? BonusTimeRatePrefKey : entry.GameKey;
            string thought = entry.Action == "time_skip" && entry.GameKey != RelicBag.Snooze
                ? "How the time flies!"
                : entry.Action == "filler" ? "A quiet moment. I like those." : Lines.ThoughtOf(key, entry.Category);
            FrameHud.Say(Lines.NameOf(key, entry.Name), $"{Lines.ReceivedFrom(senderName, fromMyself)} {thought}", Icons.ForItem(entry.GameKey, entry.Category));
        }

        /// <summary>Keeps a line for the log and the far worlds card.</summary>
        public void Note(string message)
        {
            _notes.Enqueue(message);
            while (_notes.Count > MaxNotes)
            {
                _notes.Dequeue();
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
                ObscuredPrefs.SetInt(entry.SecondaryKey, ObscuredPrefs.GetInt(entry.SecondaryKey, 0) + entry.Amount);
            }
            RelicBag.SyncPouch();
        }

        // ---- traps --------------------------------------------------------------------------------------------------

        /// <summary>1 when a trap has just sprung, 0 when it is over.</summary>
        public float TrapFractionLeft(string key)
        {
            if (!_trapUntil.TryGetValue(key, out float until) || !_trapLength.TryGetValue(key, out float length))
            {
                return 0f;
            }
            return Mathf.Clamp01((until - Time.unscaledTime) / Mathf.Max(0.1f, length));
        }

        private void TriggerTrap(ItemRegistryEntry entry)
        {
            string key = entry.GameKey;
            switch (key)
            {
                case "DISUAD&JSAD§232":
                    ObscuredPrefs.SetInt(key, ObscuredPrefs.GetInt(key, 0) + 1);
                    FrameHud.Say(Lines.TrapName(key), Lines.TrapLine(key), "ink:icons_0032");
                    return;
                case "TRAP_EXISTENTIAL":
                    var thoughts = Lines.GloomyThoughts;
                    ShadeSpeech.Think(thoughts[UnityEngine.Random.Range(0, thoughts.Length)]);
                    return;
            }

            float left = TrapFractionLeft(key) * (_trapLength.TryGetValue(key, out float old) ? old : 0f);
            _trapLength[key] = left + entry.Amount;
            _trapUntil[key] = Time.unscaledTime + left + entry.Amount;
            ShadeSpeech.Think(Lines.TrapLine(key));
        }
    }
}
