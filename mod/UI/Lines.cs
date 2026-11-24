using System;
using System.Collections.Generic;

namespace TheLongingDecrypted.UI
{
    /// <summary>
    /// Everything the Shade and the Peddler say. Written in the Shade's own voice: patient, dry, first person,
    /// and unaware of buttons, slots, percentages, or multiworlds.
    /// </summary>
    internal static class Lines
    {
        /// <summary>What an object is called, and what the Shade thinks of it.</summary>
        internal struct Thing
        {
            public readonly string Name;
            public readonly string Thought;

            public Thing(string name, string thought)
            {
                Name = name;
                Thought = thought;
            }
        }

        private static readonly Dictionary<string, Thing> Things = new Dictionary<string, Thing>
        {
            // Keepsakes carried in the frame
            ["AP_WALK_SPEED"] = new Thing("Light feet", "My legs feel lighter. I still have nowhere to be."),
            ["AP_WAYPOINT_RECALL"] = new Thing("Cave sense", "If I hold still and think of a place I have marked, I am almost there already."),
            ["AP_BONUS_TIME_RATE"] = new Thing("Quicker sand", "The hourglass runs faster than before. I try not to look at it."),
            ["AP_LIBRARY_PASS"] = new Thing("Library pass", "The library door will know me now."),
            ["AP_SURFACE_GATE_PASS"] = new Thing("Gate pass", "The way up is no longer barred. I am not sure I want to look."),
            ["SecretKey"] = new Thing("Secret key", "It opens something. I have time to find out what."),
            ["AP_CAVERN_MAP"] = new Thing("Cavern map", "Somebody has walked all of this before me, and drew it down."),
            ["AP_GIANT_SPORES"] = new Thing("Giant spores", "They tickle. Something large wants to grow."),

            // Pouch
            ["KSTTFF34"] = new Thing("Coal", "Little brothers, all of them asleep."),
            ["ITMM0023Z"] = new Thing("Moss", "My collection. I like moss."),
            ["FRST435"] = new Thing("Wood", "It must have been a tree once. I have never met one."),
            ["FRST986"] = new Thing("Firestones", "Strike two together and the dark takes a step back."),
            ["5U2LFU6"] = new Thing("Sulfur", "It smells like the end of something."),
            ["L45I5"] = new Thing("Lapis lazuli", "The blue of a sky I was told about."),
            ["P4H_FFT"] = new Thing("Paper", "Empty pages. I should think of a motif first."),
            ["PH534TLO0"] = new Thing("Mattock", "Every wall has an opinion about it."),
            ["MUL4L4L4"] = new Thing("Instrument part", "Part of a melody I cannot play yet."),

            // Relics
            ["AP_CUSTOM_LANTERN"] = new Thing("Lantern", "A small warm circle that follows me around. It is good company."),
            ["AP_CUSTOM_CHISEL"] = new Thing("Chisel", "With a fresh edge, even granite gives in a little sooner. The edge does not last."),
            ["AP_CUSTOM_MOSS_SACK"] = new Thing("Bottomless sack", "Where I gather one clump of moss, I find a second inside. I reach to the bottom now and then."),
            ["AP_ECHOING_CONCH"] = new Thing("Conch", "It whispers of things lying in the dark. It needs long to find its voice again."),
            ["AP_MUSHROOM_FERTILIZER"] = new Thing("Fertilizer", "I would rather not say where it comes from. The mushrooms do not mind."),
            ["TIME_SKIP_43200"] = new Thing("Snooze button", "The king stirs, I press it, we both drift off for a while. It is hard to do twice in a row."),
            ["AP_BUFF_ESPRESSO"] = new Thing("Double espresso", "Bitter. For one short minute I am in a hurry."),

            // Hats
            ["AP_HAT_CROWN"] = new Thing("Tiny crown", "It must be a copy. The king would have noticed."),
            ["AP_HAT_HELMET"] = new Thing("Miner's helmet", "The candle drips on my nose."),
            ["AP_HAT_MUSHROOM"] = new Thing("Mushroom cap", "I hope it is not still growing."),
            ["AP_HAT_MIFFY"] = new Thing("Rabbit beanie", "It has ears. I have never felt so listened to."),
            ["AP_HAT_TOPHAT"] = new Thing("Top hat", "For formal waiting."),
            ["AP_HAT_SPIDER"] = new Thing("Spider friend", "She likes the view from up there."),
            ["AP_HAT_FLOWER"] = new Thing("Flower crown", "Something grew without any sun. Now it grows on me."),
            ["AP_HAT_NIGHTCAP"] = new Thing("Nightcap", "Four hundred days is a long night."),

            // Companions
            ["AP_PET_ROCK"] = new Thing("Pebble", "It says nothing. We get along well."),
            ["AP_PET_SPIDER"] = new Thing("Little spider", "Eight small steps for every two of mine."),
        };

        // Archipelago item names that deserve a plainer name on the card.
        private static readonly Dictionary<string, string> ItemThoughts = new Dictionary<string, string>
        {
            ["crystals"] = "It glows a little. It must have been lonely.",
            ["books"] = "A new book for my collection. I should be reading more.",
            ["comfort"] = "This will look better in my home.",
            ["mushrooms"] = "A mushroom, ready to be planted. Or eaten. I will think about it.",
            ["instrument"] = "Part of a melody I cannot play yet.",
        };

        public static Thing Of(string key)
        {
            return key != null && Things.TryGetValue(key, out var thing) ? thing : new Thing(string.Empty, string.Empty);
        }

        public static string NameOf(string key, string fallback)
        {
            var thing = Of(key);
            return string.IsNullOrEmpty(thing.Name) ? fallback : thing.Name;
        }

        public static string ThoughtOf(string key, string category)
        {
            var thing = Of(key);
            if (!string.IsNullOrEmpty(thing.Thought))
            {
                return thing.Thought;
            }
            return category != null && ItemThoughts.TryGetValue(category, out var line) ? line : "I better take this with me.";
        }

        // ---- things arriving and leaving --------------------------------------------------------------------------

        public static string ReceivedFrom(string sender, bool self)
        {
            if (self)
            {
                return "I found this myself.";
            }
            return string.IsNullOrEmpty(sender) ? "It was lying there as if it had waited for me." : $"{sender} left this for me.";
        }

        public static string SentAway(string owner)
        {
            return $"It belongs to {owner}. I sent it on its way.";
        }

        public static string WhileAway(int count)
        {
            return count == 1
                ? "Something arrived while I was not looking."
                : $"{count} things arrived while I was not looking.";
        }

        public const string WhileAwayTitle = "How the time flies";

        // ---- frame --------------------------------------------------------------------------------------------------

        private static readonly string[] Paces =
        {
            "There is no need to hurry.",
            "I walk a little faster than I used to.",
            "I walk quite a bit faster than I used to.",
            "I nearly trot. It is undignified.",
            "I have never been this fast. I hope nobody sees it.",
        };

        public static string Pace(int tier) => Paces[Math.Max(0, Math.Min(Paces.Length - 1, tier))];

        public static string Recall(int tier)
        {
            switch (tier)
            {
                case 1: return "Holding shift, I can think myself back to places near the king. The outer caves are still too far.";
                case 2: return "Holding shift, I can think myself back as far as the great halls and the library.";
                case 3: return "Holding shift, I can think myself back to any place I have marked, however deep.";
                default: return "I have to walk everywhere. There is plenty of time to walk.";
            }
        }

        public static string TimeRate(int rate)
        {
            return $"For every second I count, {Number(rate)} run through the hourglass.";
        }

        public static string Count(int amount)
        {
            return amount == 1 ? "I have just one." : $"I have {Number(amount)}.";
        }

        public static string Wait(double seconds)
        {
            if (seconds < 60)
            {
                return "Only a moment longer.";
            }
            int minutes = (int)Math.Ceiling(seconds / 60.0);
            if (minutes == 1)
            {
                return "About a minute of waiting. I am good at that.";
            }
            if (minutes < 60)
            {
                return $"I should wait another {Number(minutes)} minutes. That is nothing.";
            }
            int hours = (int)Math.Round(minutes / 60.0);
            return hours <= 1 ? "I should wait another hour. Then we will see." : $"I should wait another {Number(hours)} hours. Then we will see.";
        }

        public const string Wearing = "I am wearing it.";
        public const string FollowingMe = "It follows me wherever I go.";

        public const string MultiworldName = "Far worlds";
        public const string MultiworldAwake = "I can feel other worlds through the stone.";
        public const string MultiworldAsleep = "The stone is quiet. No other world can hear me.";

        // ---- relics ---------------------------------------------------------------------------------------------------

        public const string LanternLit = "That is better.";
        public const string LanternOut = "I know my way in the dark.";
        public const string ChiselHoned = "A fresh edge. I should find a wall before it dulls.";
        public const string SackEmptied = "A clump of moss, right at the bottom. I must have missed it.";
        public const string ConchSilent = "Only the sea. I have never seen the sea.";
        public const string ConchNothingLeft = "It has nothing left to tell me.";
        public const string FertilizerNothing = "Nothing is growing here. I would only be making a smell.";
        public const string FertilizerUsed = "They seem to like it. They are growing while I watch.";
        public const string SnoozeUsed = "The king turned over in his sleep. I must have dozed off as well.";
        public const string SnoozeTooLate = "It would be a pity to oversleep now.";
        public const string EspressoUsed = "My heart! So that is what it sounds like.";
        public const string NotNow = "Not now.";

        public static string ConchWhisper(string place, string item, string owner, bool own)
        {
            return own ? $"\"{place}...\" it whispers. \"{item}, for you.\"" : $"\"{place}...\" it whispers. \"{item}. {owner} is missing it.\"";
        }

        public const string BonusCoal = "It split cleanly in two. Twins.";
        public const string BonusMoss = "And another one, inside the sack.";

        // ---- peddler ----------------------------------------------------------------------------------------------------

        public const string PeddlerName = "The Peddler";
        public const string TalkToPeddler = "Talk to the peddler";

        public static readonly string[] Greetings =
        {
            "Ah. A customer. I had almost stopped waiting.",
            "You have coal dust on your beak. Good. Come closer.",
            "I was here before the king slept. Business was slow then, too.",
            "No hurry. Nothing here spoils.",
            "The windows are pretty, are they not? They are not for sale.",
        };

        public const string ShelfFar = "From afar";
        public const string ShelfWares = "My wares";
        public const string ShelfFavors = "Favors";

        public const string ShelfFarLine = "These washed up from other worlds. I do not ask how.";
        public const string ShelfFarEmpty = "Nothing has washed up. It comes and goes.";
        public const string ShelfWaresLine = "All made down here. Mostly by me. Mostly by accident.";
        public const string ShelfFavorsLine = "I can do small things for you. Small ones.";

        public const string NoCoal = "You do not have the coal for that. It falls from the ceiling over there, if you are patient. You look patient.";
        public const string NoMoss = "That is hardly a handful. Come back with more moss.";
        public const string Stuck = "This one is still stuck in the rock. Bring a mattock and we will talk.";
        public const string Gone = "Gone. Somebody is very happy about it, somewhere.";
        public const string Owned = "You have that already. I remember, because I was sad to see it go.";
        public const string Sold = "A fair trade. It will find its way.";
        public const string SoldKeep = "Yours. Be good to it.";
        public const string Resting = "I just did that for you. Let me rest.";
        public const string HourglassTipped = "There. A little less king to wait for.";
        public const string HourglassRefused = "He is nearly awake. I will not touch it now.";
        public const string MossTaken = "Soft. I will sleep on it.";
        public const string EspressoPoured = "Drink it while it is hot. Then run.";
        public const string WhisperGiven = "Listen closely. I will only say it once.";

        public const string WrappedName = "Something wrapped in cloth";
        public const string WrappedLine = "I have not opened it. That would be rude.";
        public const string PayWord = "Trade";

        public static string ForSomebody(string player)
        {
            return $"It belongs to {player}. They would be glad to have it back.";
        }

        public const string ForMe = "It has your name on it. I can read, you know.";

        public static string Price(int coal)
        {
            return coal == 1 ? "a lump of coal" : $"{Number(coal)} lumps of coal";
        }

        public static string MossPrice(int moss)
        {
            return $"{Number(moss)} clumps of moss";
        }

        public static string Purse(int coal, int moss)
        {
            string c = coal == 0 ? "no coal" : coal == 1 ? "one lump of coal" : $"{Number(coal)} lumps of coal";
            string m = moss == 0 ? "no moss" : moss == 1 ? "one clump of moss" : $"{Number(moss)} clumps of moss";
            return $"I have {c} and {m}.";
        }

        // ---- traps ---------------------------------------------------------------------------------------------------------

        private static readonly string[] Gloom =
        {
            "If the king ends the world when he wakes, what exactly am I guarding?",
            "Perhaps the mattock does not break because the rock is hard. Perhaps it just loses heart.",
            "I walked for a whole day to look at a stalactite. It was the best day this week.",
            "I wonder whether the spiders think of me as a tenant.",
            "If a home grows more pleasant with every carpet, why do I still feel like this?",
        };

        public static string[] GloomyThoughts => Gloom;

        public static string TrapName(string key)
        {
            switch (key)
            {
                case "TRAP_SLOWNESS": return "Heavy legs";
                case "TRAP_DARKNESS": return "Deep dark";
                case "TRAP_TIME_STALL": return "Stuck sand";
                case "TRAP_EXISTENTIAL": return "A gloomy thought";
                case "TRAP_HICCUP": return "Hiccups";
                case "TRAP_SPIDER_SCARE": return "A visitor";
                case "TRAP_FALSE_WAKE": return "A rumble";
                case "TRAP_TINY_SHADE": return "Smaller than usual";
                default: return "A disappointment";
            }
        }

        public static string TrapLine(string key)
        {
            switch (key)
            {
                case "TRAP_SLOWNESS": return "My legs are as heavy as stone. I will just go slower.";
                case "TRAP_DARKNESS": return "Somebody has put out the little light there was.";
                case "TRAP_TIME_STALL": return "The sand has stopped. Of all the things to be stuck with.";
                case "TRAP_HICCUP": return "Hic.";
                case "TRAP_SPIDER_SCARE": return "Oh. Hello.";
                case "TRAP_FALSE_WAKE": return "Is he waking? ...No. He only turned over.";
                case "TRAP_TINY_SHADE": return "Did the cave grow? Or is it me?";
                default: return "I will add this to my list of disappointments.";
            }
        }

        // ---- multiworld panel -----------------------------------------------------------------------------------------------

        public static string Goal(string goal)
        {
            switch (goal)
            {
                case "wake_the_king": return "I am to wake the king when the time has come.";
                case "escape_surface": return "I am to find a way to the surface.";
                case "secret_ending": return "There is a secret somewhere in this kingdom. I am to find it.";
                case "crystal_book_hunt": return "I am to gather crystals and books.";
                default: return "Any ending will do.";
            }
        }

        public static string Hunt(int crystals, int neededCrystals, int books, int neededBooks)
        {
            return $"{Capital(Number(crystals))} of {Number(neededCrystals)} crystals, {Number(books)} of {Number(neededBooks)} books.";
        }

        public static string Found(int found, int total)
        {
            return $"I have found {Number(found)} of {Number(total)} things.";
        }

        public const string NeverKnock = "I have to leave the caves before I can listen to a different world.";

        // ---- shut doors --------------------------------------------------------------------------------------------------------

        public const string LibraryShut = "The library does not know me. I would need some kind of pass.";
        public const string GateShut = "The way up stays shut. Somebody would have to grant me passage.";
        public const string SporesMissing = "Nothing large wants to grow here. I lack the right spores.";

        public const string Somebody = "somebody";
        public const string DeathLink = "Somebody far away has died. I felt faint, and woke up at home.";
        public const string GoalReachedName = "The end";
        public const string GoalReached = "So this is what I was waiting for.";

        // ---- travel ----------------------------------------------------------------------------------------------------------

        public const string RecallNotHere = "I cannot find the quiet I need here.";
        public const string RecallUnknown = "I have never been there. I would not know where to think of.";
        public const string RecallOutside = "That place lies outside of everything I can picture.";
        public const string RecallTooFar = "It is too far away for now. My thoughts get lost on the way.";

        // ---- numbers -----------------------------------------------------------------------------------------------------------

        private static readonly string[] Small =
        {
            "no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
        };

        /// <summary>Small numbers are spelled out, as the Shade would.</summary>
        public static string Number(int value)
        {
            return value > 0 && value < Small.Length ? Small[value] : value == 0 ? "none" : value.ToString();
        }

        private static string Capital(string word)
        {
            return string.IsNullOrEmpty(word) ? word : char.ToUpperInvariant(word[0]) + word.Substring(1);
        }
    }
}
