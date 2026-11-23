using System;
using System.Collections.Generic;

namespace TheLongingDecrypted.Archipelago
{
    public sealed class SlotConfig
    {
        public string WorldVersion { get; set; } = "0.1.0";
        public string Goal { get; set; } = "wake_the_king";
        public string CountdownLength { get; set; } = "6_hours";
        public long CountdownSeconds { get; set; } = 21_600;
        public int StartingTimeRate { get; set; } = 12;
        public bool TimeSkipItems { get; set; } = true;
        public int StartingWalkSpeed { get; set; } = 0;
        public bool ShuffleMattocksAndKeys { get; set; } = true;
        public string ShuffleBooks { get; set; } = "progression";
        public string ShuffleCrystals { get; set; } = "full";
        public bool ShuffleMushroomsAndMoss { get; set; } = true;
        public bool ShuffleHomeComfort { get; set; } = true;
        public bool ShuffleInstrumentParts { get; set; } = true;
        public bool ShuffleCoalAndSulfur { get; set; } = false;
        public bool ShuffleShop { get; set; } = true;
        public bool CustomItems { get; set; } = true;
        public bool Ao3FanficBooks { get; set; } = false;
        public bool AutoHints { get; set; } = true;
        public int RequiredCrystals { get; set; } = 8;
        public int RequiredBooks { get; set; } = 12;
        public string TrapDensity { get; set; } = "light";
        public bool DeathLink { get; set; } = false;
        public HashSet<long> EnabledLocationIds { get; } = new HashSet<long>();

        public static SlotConfig FromSlotData(Dictionary<string, object> slotData)
        {
            var cfg = new SlotConfig();
            if (slotData == null)
            {
                return cfg;
            }

            cfg.WorldVersion = GetString(slotData, "world_version", cfg.WorldVersion);
            cfg.Goal = GetString(slotData, "goal", cfg.Goal);
            cfg.CountdownLength = GetString(slotData, "countdown_length", cfg.CountdownLength);
            cfg.CountdownSeconds = GetLong(slotData, "countdown_seconds", cfg.CountdownSeconds);
            cfg.StartingTimeRate = (int)GetLong(slotData, "starting_time_rate", cfg.StartingTimeRate);
            cfg.TimeSkipItems = GetBool(slotData, "time_skip_items", cfg.TimeSkipItems);
            cfg.StartingWalkSpeed = (int)GetLong(slotData, "starting_walk_speed", cfg.StartingWalkSpeed);
            cfg.ShuffleMattocksAndKeys = GetBool(slotData, "shuffle_mattocks_and_keys", cfg.ShuffleMattocksAndKeys);
            cfg.ShuffleBooks = GetString(slotData, "shuffle_books", cfg.ShuffleBooks);
            cfg.ShuffleCrystals = GetString(slotData, "shuffle_crystals", cfg.ShuffleCrystals);
            cfg.ShuffleMushroomsAndMoss = GetBool(slotData, "shuffle_mushrooms_and_moss", cfg.ShuffleMushroomsAndMoss);
            cfg.ShuffleHomeComfort = GetBool(slotData, "shuffle_home_comfort", cfg.ShuffleHomeComfort);
            cfg.ShuffleInstrumentParts = GetBool(slotData, "shuffle_instrument_parts", cfg.ShuffleInstrumentParts);
            cfg.ShuffleCoalAndSulfur = GetBool(slotData, "shuffle_coal_and_sulfur", cfg.ShuffleCoalAndSulfur);
            cfg.ShuffleShop = GetBool(slotData, "shuffle_shop", cfg.ShuffleShop);
            cfg.CustomItems = GetBool(slotData, "custom_items", cfg.CustomItems);
            cfg.Ao3FanficBooks = GetBool(slotData, "ao3_fanfic_books", cfg.Ao3FanficBooks);
            cfg.AutoHints = GetBool(slotData, "auto_hints", cfg.AutoHints);
            cfg.RequiredCrystals = (int)GetLong(slotData, "required_crystals", cfg.RequiredCrystals);
            cfg.RequiredBooks = (int)GetLong(slotData, "required_books", cfg.RequiredBooks);
            cfg.TrapDensity = GetString(slotData, "trap_density", cfg.TrapDensity);
            cfg.DeathLink = GetBool(slotData, "death_link", cfg.DeathLink);

            if (slotData.TryGetValue("enabled_location_ids", out var rawIds) && rawIds is IEnumerable<object> seq)
            {
                foreach (var entry in seq)
                {
                    if (entry != null && long.TryParse(entry.ToString(), out var id))
                    {
                        cfg.EnabledLocationIds.Add(id);
                    }
                }
            }

            return cfg;
        }

        private static string GetString(Dictionary<string, object> data, string key, string fallback)
        {
            return data.TryGetValue(key, out var val) && val != null ? val.ToString() : fallback;
        }

        private static long GetLong(Dictionary<string, object> data, string key, long fallback)
        {
            if (data.TryGetValue(key, out var val) && val != null && long.TryParse(val.ToString(), out var parsed))
            {
                return parsed;
            }
            return fallback;
        }

        private static bool GetBool(Dictionary<string, object> data, string key, bool fallback)
        {
            if (!data.TryGetValue(key, out var val) || val == null)
            {
                return fallback;
            }
            if (val is bool b)
            {
                return b;
            }
            if (bool.TryParse(val.ToString(), out var parsed))
            {
                return parsed;
            }
            if (long.TryParse(val.ToString(), out var num))
            {
                return num != 0;
            }
            return fallback;
        }
    }
}
