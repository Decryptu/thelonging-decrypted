using System;
using System.Collections.Generic;
using System.Globalization;
using CodeStage.AntiCheat.ObscuredTypes;
using HarmonyLib;
using TheLongingDecrypted.UI;
using UnityEngine;

namespace TheLongingDecrypted.Relics
{
    internal sealed class RelicDef
    {
        public string Key { get; }
        public string Asset { get; }

        /// <summary>Real seconds before the relic can be used again. The wait keeps running while the game is closed.</summary>
        public int Cooldown { get; }

        /// <summary>Real seconds the effect stays active after use.</summary>
        public int Duration { get; }

        /// <summary>Lumps of coal the Peddler asks for it.</summary>
        public int Price { get; }

        public RelicDef(string key, string asset, int cooldown, int duration, int price)
        {
            Key = key;
            Asset = asset;
            Cooldown = cooldown;
            Duration = duration;
            Price = price;
        }
    }

    /// <summary>Ownership, cooldowns, and effects of the reusable relics carried in the bottom of the frame.</summary>
    [HarmonyPatch]
    internal static class RelicBag
    {
        public const string Lantern = "AP_CUSTOM_LANTERN";
        public const string Chisel = "AP_CUSTOM_CHISEL";
        public const string MossSack = "AP_CUSTOM_MOSS_SACK";
        public const string Conch = "AP_ECHOING_CONCH";
        public const string Fertilizer = "AP_MUSHROOM_FERTILIZER";
        public const string Snooze = "TIME_SKIP_43200";
        public const string Espresso = "AP_BUFF_ESPRESSO";

        public const string CoalKey = "KSTTFF34";
        public const string MossKey = "ITMM0023Z";

        private const int Minute = 60;
        private const long FourHundredDays = 34_560_000L;
        private const string LanternLitKey = "AP_LANTERN_LIT";

        public static readonly RelicDef[] All =
        {
            new RelicDef(Lantern, "items/item_lantern.png", 0, 0, 10),
            new RelicDef(Chisel, "items/item_chisel.png", 25 * Minute, 4 * Minute, 14),
            new RelicDef(MossSack, "items/item_moss_sack.png", 30 * Minute, 0, 14),
            new RelicDef(Conch, "items/item_conch.png", 40 * Minute, 0, 16),
            new RelicDef(Fertilizer, "items/item_fertilizer.png", 60 * Minute, 0, 16),
            new RelicDef(Snooze, "items/item_snooze_button.png", 90 * Minute, 0, 24),
            new RelicDef(Espresso, "items/item_espresso.png", 20 * Minute, 1 * Minute, 12),
        };

        private static readonly Dictionary<string, long> StampCache = new Dictionary<string, long>();
        private static string _cachedSave;
        private static int _seenCoal = -1;
        private static int _seenMoss = -1;

        // ---- ownership ------------------------------------------------------------------------------------------

        public static RelicDef Find(string key) => Array.Find(All, relic => relic.Key == key);

        public static bool Owned(string key) => FileBasedPrefs.GetInt("AP_UNLOCK_" + key, 0) > 0;

        /// <summary>How many copies arrived. Every extra copy shortens the wait.</summary>
        public static int Rank(string key) => Math.Max(1, FileBasedPrefs.GetInt("AP_RANK_" + key, 1));

        /// <summary>Takes a relic. A copy of one already owned readies it and shortens its wait for good.</summary>
        public static void Grant(string key)
        {
            if (!Owned(key))
            {
                FileBasedPrefs.SetInt("AP_UNLOCK_" + key, 1);
                FileBasedPrefs.SetInt("AP_RANK_" + key, 1);
                if (key == Lantern)
                {
                    FileBasedPrefs.SetInt(LanternLitKey, 1);
                }
                return;
            }
            FileBasedPrefs.SetInt("AP_RANK_" + key, Math.Min(9, Rank(key) + 1));
            SetStamp("AP_READY_" + key, 0L);
        }

        // ---- waiting ----------------------------------------------------------------------------------------------

        private static double Now => (DateTime.UtcNow - new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        public static double Cooldown(RelicDef relic)
        {
            return relic.Cooldown * Math.Max(0.4, Math.Pow(0.75, Rank(relic.Key) - 1));
        }

        /// <summary>Seconds until the relic can be used again.</summary>
        public static double Waiting(string key) => Remaining("AP_READY_" + key);

        /// <summary>Seconds the effect of the relic still lasts.</summary>
        public static double Active(string key) => Remaining("AP_ACTIVE_" + key);

        public static double Remaining(string stampKey)
        {
            double left = Stamp(stampKey) - Now;
            return left > 0d ? left : 0d;
        }

        /// <summary>Starts a wait that survives quitting. Shared with the Peddler's favors.</summary>
        public static void StartWait(string stampKey, double seconds)
        {
            SetStamp(stampKey, (long)Math.Ceiling(Now + seconds));
        }

        private static void FollowSave()
        {
            string save = FileBasedPrefs.GetSaveFilePath();
            if (save != _cachedSave)
            {
                StampCache.Clear();
                _cachedSave = save;
                _seenCoal = _seenMoss = -1;
            }
        }

        private static long Stamp(string key)
        {
            FollowSave();
            if (!StampCache.TryGetValue(key, out long value))
            {
                long.TryParse(FileBasedPrefs.GetString(key, "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
                // A clock set back between sessions must not lock a relic away.
                if (value - Now > 24 * 3600)
                {
                    value = 0L;
                }
                StampCache[key] = value;
            }
            return value;
        }

        private static void SetStamp(string key, long value)
        {
            FollowSave();
            StampCache[key] = value;
            FileBasedPrefs.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        // ---- using ------------------------------------------------------------------------------------------------

        public static bool LanternLit => Owned(Lantern) && FileBasedPrefs.GetInt(LanternLitKey, 0) > 0;
        public static bool ChiselHoned => Active(Chisel) > 0d;
        public static bool EspressoActive => Active(Espresso) > 0d;

        public static bool IsLit(string key) => key == Lantern ? LanternLit : Active(key) > 0d;

        /// <summary>Uses a relic. Returns what the Shade has to say about it.</summary>
        public static string Use(string key)
        {
            var relic = Find(key);
            if (relic == null || !Owned(key) || !GameManagerScript.IsGameRunning() || FileBasedPrefs.BlockNewSaves)
            {
                return Lines.NotNow;
            }
            double waiting = Waiting(key);
            if (waiting > 0d)
            {
                return Lines.Wait(waiting);
            }

            string said;
            switch (key)
            {
                case Lantern:
                    bool lit = !LanternLit;
                    FileBasedPrefs.SetInt(LanternLitKey, lit ? 1 : 0);
                    said = lit ? Lines.LanternLit : Lines.LanternOut;
                    break;
                case Chisel:
                    said = Lines.ChiselHoned;
                    break;
                case MossSack:
                    Moss += 1;
                    said = Lines.SackEmptied;
                    break;
                case Conch:
                    string silence = Plugin.Session.Whisper(whisper => FrameHud.Say(Lines.NameOf(Conch, null), whisper, relic.Asset));
                    if (silence != null)
                    {
                        return silence;
                    }
                    said = null;
                    break;
                case Fertilizer:
                    if (FeedPlants() == 0)
                    {
                        return Lines.FertilizerNothing;
                    }
                    said = Lines.FertilizerUsed;
                    break;
                case Snooze:
                    long skip = NapSeconds(0.03);
                    if (skip <= 0L)
                    {
                        return Lines.SnoozeTooLate;
                    }
                    TMan.AddInstaTime(skip);
                    TMan.SaveValues();
                    said = Lines.SnoozeUsed;
                    break;
                case Espresso:
                    said = Lines.EspressoUsed;
                    break;
                default:
                    return Lines.NotNow;
            }

            if (relic.Duration > 0)
            {
                StartWait("AP_ACTIVE_" + key, relic.Duration);
            }
            if (relic.Cooldown > 0)
            {
                StartWait("AP_READY_" + key, Cooldown(relic));
            }
            FileBasedPrefs.ManualySave();
            return said;
        }

        public static void Brew(int seconds)
        {
            StartWait("AP_ACTIVE_" + Espresso, Active(Espresso) + seconds);
        }

        /// <summary>A nap never swallows more than a sliver of what is left, and never the end itself.</summary>
        public static long NapSeconds(double share)
        {
            long left = FourHundredDays - Math.Max(0L, TMan.GetOverallTime());
            return left < 600L ? 0L : Math.Max(30L, Math.Min(43_200L, (long)(left * share)));
        }

        /// <summary>Moves everything that is currently growing in this cave a quarter of its growth ahead.</summary>
        private static int FeedPlants()
        {
            int fed = 0;
            foreach (var interactable in UnityEngine.Object.FindObjectsOfType<Interactable>())
            {
                if (!(interactable is Interactable_MushroomGrow) && !(interactable is Interactable_MossGrow))
                {
                    continue;
                }
                var progress = interactable.GetComponent<ProgressClass>();
                if (progress == null || progress.GetStatus() != ProgressClass.AEIBALBILJF.running || progress.PDHKCPKPOKJ <= 0L)
                {
                    continue;
                }
                long grown = TMan.GetOverallTimeProgress() - progress.GetStartedTime();
                progress.RedefineStartValue(grown + progress.PDHKCPKPOKJ / 4L);
                fed++;
            }
            return fed;
        }

        // ---- coal and moss ----------------------------------------------------------------------------------------

        public static int Coal
        {
            get => Math.Max(0, ObscuredPrefs.GetInt(CoalKey, 0));
            set
            {
                ObscuredPrefs.SetInt(CoalKey, Math.Max(0, value));
                _seenCoal = Coal;
            }
        }

        public static int Moss
        {
            get => Math.Max(0, ObscuredPrefs.GetInt(MossKey, 0));
            set
            {
                ObscuredPrefs.SetInt(MossKey, Math.Max(0, value));
                _seenMoss = Moss;
            }
        }

        /// <summary>Call after the mod itself changed the pouch, so deliveries are not mistaken for pickups.</summary>
        public static void SyncPouch()
        {
            _seenCoal = Coal;
            _seenMoss = Moss;
        }

        /// <summary>Doubles what the Shade gathers by hand while the chisel is honed or the sack is carried.</summary>
        public static void WatchPickups()
        {
            FollowSave();
            int coal = Coal, moss = Moss;
            if (_seenCoal >= 0 && coal == _seenCoal + 1 && ChiselHoned)
            {
                Coal = coal + 1;
                FrameHud.Say(Lines.NameOf(Chisel, null), Lines.BonusCoal, Find(Chisel).Asset);
            }
            if (_seenMoss >= 0 && moss == _seenMoss + 1 && Owned(MossSack))
            {
                Moss = moss + 1;
                FrameHud.Say(Lines.NameOf(MossSack, null), Lines.BonusMoss, Find(MossSack).Asset);
            }
            SyncPouch();
        }

        // Mining adds one frame of work at a time; offline catch-up arrives as whole seconds and stays untouched.
        [HarmonyPatch(typeof(ProgressClass), nameof(ProgressClass.AddProgress))]
        [HarmonyPrefix]
        private static void MineFasterWithHonedChisel(ProgressClass __instance, ref float KBALKPHBPFK)
        {
            if (KBALKPHBPFK > 0f && KBALKPHBPFK < 1f && ChiselHoned
                && (__instance.GetComponent<InteractableOutcome_HackIntoWall>() || __instance.GetComponent<InteractableOutcome_HackIntoMine>()))
            {
                KBALKPHBPFK *= 2f;
            }
        }
    }
}
