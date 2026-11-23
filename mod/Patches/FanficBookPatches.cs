using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TheLongingDecrypted.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLongingDecrypted.Patches
{
    /// <summary>
    /// Replaces the Shade's classic library books, shelf spine titles, cover icons (70x64),
    /// and page contents with hilarious romantic/soft-NSFW cavern parody fanfictions (AO3 Mode).
    /// Supports user-supplied .txt stories and cover PNGs from CustomAssets/books/.
    /// </summary>
    internal static class FanficBookPatches
    {
        public const string LocalOverridePrefKey = "AP_AO3_FANFIC_OVERRIDE";

        private static readonly FieldInfo ShelfTitleField = AccessTools.Field(typeof(BookInShelf), "EKHKHEMALCK");
        private static readonly FieldInfo ShelfFileField = AccessTools.Field(typeof(BookInShelf), "MGBFKNOFJCO");
        private static readonly FieldInfo ShelfLabelField = AccessTools.Field(typeof(BookInShelf), "OBILIGCIFOE");
        private static readonly FieldInfo ShelfImageField = AccessTools.Field(typeof(BookInShelf), "FGHIIBCMGJH");

        private static readonly FieldInfo ReadLinesField = AccessTools.Field(typeof(ReadingBehaviour), "NJECGNGCNEF");
        private static readonly FieldInfo ReadTitleLabelField = AccessTools.Field(typeof(ReadingBehaviour), "EKHKHEMALCK");
        private static readonly FieldInfo ReadPageLineIdxField = AccessTools.Field(typeof(ReadingBehaviour), "ICLMHFJCLAE");
        private static readonly FieldInfo ReadLeftPageField = AccessTools.Field(typeof(ReadingBehaviour), "JOKLDGCODBN");
        private static readonly FieldInfo ReadRightPageField = AccessTools.Field(typeof(ReadingBehaviour), "BBJDFBNLNPM");
        private static readonly FieldInfo ReadLinesPerPageField = AccessTools.Field(typeof(ReadingBehaviour), "ELGHCECLHJF");
        private static readonly MethodInfo SetPageCounterMethod = AccessTools.Method(typeof(ReadingBehaviour), "SetPageCounter");

        public sealed class FanficVolume
        {
            public string Title { get; }
            public string Author { get; }
            public string RatingAndTags { get; }
            public string CustomTxtFile { get; }
            public string CoverImageFile { get; }
            public string[] Paragraphs { get; }

            public FanficVolume(
                string title,
                string author,
                string ratingAndTags,
                string customTxtFile,
                string coverImageFile,
                string[] paragraphs)
            {
                Title = title;
                Author = author;
                RatingAndTags = ratingAndTags;
                CustomTxtFile = customTxtFile;
                CoverImageFile = coverImageFile;
                Paragraphs = paragraphs;
            }
        }

        public static readonly FanficVolume[] LibraryFanfics =
        {
            new FanficVolume(
                "Fifty Shades of Shade",
                "xX_LonelySubterranean_Xx",
                "Rating: Mature (Soft NSFW) | Tags: Slow Burn (400 Days), Cavern Romance, Consensual Hand-Holding, Extremely Soft Moss",
                "books/fanfics/fanfic_01.txt",
                "books/book_cover_01.png",
                new[]
                {
                    "Summary: He was a 3-foot tall yellow creature of pure patience. His mattress was fifteen bundles of damp cavern moss. What happens when the drafts from the Entrance Hall blow through his freshly hung Eternal Curtain?",
                    "Chapter 1: The Friction of Flint",
                    "The hearth fire crackled with an almost scandalous warmth. The Shade adjusted his tiny ochre shoulders, feeling the heat trace along his collarbone as he reclined upon fifteen layers of freshly harvested moss.",
                    "\"You've been waiting 399 days for me,\" whispered a voice like grinding limestone from across the room. It was the Mattock, leaning provocatively against the freshly chipped wall.",
                    "\"My handle is seasoned oak,\" the Mattock murmured, its iron head glinting in the amber firelight. \"Hold me with both hands. Strike the wall until our durability hits zero.\"",
                    "The Shade blushed a deep, luminous saffron. His oversized golden eyes fluttered shut as his fingers curled around the smooth wooden grip.",
                    "\"Careful,\" the Shade breathed, a shiver running all the way down to his stubby toes. \"If we chip this alcove any faster, the time multiplier will hit 24x and the neighbours in the Hollow Knight multiworld will hear us.\"",
                    "Outside, a single water drop fell from the ceiling. Plink. Inside, the moss bed had never felt softer."
                }
            ),
            new FanficVolume(
                "Stuck in the Well Shaft with the Troll (There Was Only One Bucket)",
                "MossyPillowPrincess",
                "Rating: Explicit Fluff & Spice | Tags: Enemies to Lovers, Only One Bucket, Size Difference, Rescue Romance",
                "books/fanfics/fanfic_02.txt",
                "books/book_cover_02.png",
                new[]
                {
                    "Summary: The Shade climbed into the old iron well bucket expecting freedom, or perhaps a tragic fall. He did NOT expect the muscular Cavern Troll to climb in right after him.",
                    "Chapter 1: Tight Quarters",
                    "The iron chain groaned under their combined weight as the bucket swayed in the narrow stone shaft. There was nowhere to look except directly against the Troll's broad, moss-dusted chest.",
                    "\"You're trembling, little yellow one,\" the Troll rumbled, his voice vibrating right through the bucket floor. \"Is it the draft from the surface... or is it me?\"",
                    "\"Your knee is pressing against my Lapis Lazuli,\" the Shade squeaked, pressing his palms flat against the Troll's surprisingly warm ribs.",
                    "The Troll chuckled, leaning down until his tusks brushed the rim of the Shade's ear. \"Keep holding onto my belt like that, little Shade, and I might pull this rope all the way to the stars.\"",
                    "By the time the bucket reached the mossy rim of the upper world, neither of them was in any hurry to climb out."
                }
            ),
            new FanficVolume(
                "The King's Secret Pillar: 400 Days of Yearning",
                "RoyalBedchamberScribe",
                "Rating: Mature | Tags: Pining, Asleep/Awake Dynamic, Stone Pillar Appreciation, Hair Brushing",
                "books/fanfics/fanfic_03.txt",
                "books/book_cover_03.png",
                new[]
                {
                    "Summary: Every day the Shade walks into the throne room to check the countdown. Every day the King pretends to be asleep so the cute little guy will stay and comb his beard.",
                    "Chapter 1: Beneath the Beard",
                    "It was Day 214 when the Shade noticed the King'sstone fingers twitching against the throne armrest. The massive monarch's beard cascaded across the cavern floor like a velvet waterfall.",
                    "Gently, almost reverently, the Shade nestled into a warm curl of the royal beard. It smelled of ancient cedar, sulfur incense, and raw regality.",
                    "\"Ten million seconds left,\" the Shade whispered against the King's colossal thumb, tracing the carved runes with a delicate yellow fingertip.",
                    "A low, resonant purr echoed from deep within the King's chest, shaking loose a shower of rose-quartz dust from the ceiling. In his dream, the King smiled."
                }
            ),
            new FanficVolume(
                "Mattock & Moss: A Subterranean Slow Burn",
                "CrystalVeinQueen",
                "Rating: Teen & Up / Steamy | Tags: Mining Sweat, Crystal Polishing, Tool Care, Hurt/Comfort",
                "books/fanfics/fanfic_04.txt",
                "books/book_cover_04.png",
                new[]
                {
                    "Summary: Five Mattocks. Sixteen Crystal veins. One Shade determined to polish every single facet until it gleams in the dark.",
                    "Chapter 1: Chipping Away at Your Heart",
                    "Clang. Clang. Each swing sent a warm tremor through the Shade's arms. The Rose Crystal vein was stubborn, glistening with a blush-pink sheen in the dim cavern.",
                    "He wiped a bead of imaginary sweat from his brow and leaned closer, running his palm over the freshly exposed crystal surface. It was warm to the touch—almost pulsing.",
                    "\"Just a little deeper,\" he coaxed, positioning the tip of his third Mattock right along the fault line. With one decisive, rhythmic thrust, the crystal yielded with a breathless chime."
                }
            ),
            new FanficVolume(
                "The Spider's Silk Boudoir",
                "EightLeggedEmbrace",
                "Rating: Mature (Sensual Silk) | Tags: Monster Romance, Silk Hammock, Gentle Webbing, Cozy Bondage",
                "books/fanfics/fanfic_05.txt",
                "books/book_cover_05.png",
                new[]
                {
                    "Summary: When the Shade waited beneath the high ledge for the cavern spider to weave a bridge, he didn't expect her to weave him a custom silk bathrobe first.",
                    "Chapter 1: Wrapped in Warmth",
                    "The thread was softer than cashmere and twice as strong. The cavern spider lowered herself on a single silver strand, her eight eyes gleaming with unmistakable affection.",
                    "With practiced grace, she looped a ribbon of warm silk around the Shade's waist, drawing him up onto the plush web hammock suspended above the chasm.",
                    "\"Rest your feet, darling,\" her mandibles clicked in a soothing rhythm. \"You've been walking at 1x speed since Tuesday. Let me tuck you in.\""
                }
            ),
            new FanficVolume(
                "Steamy Sulfur Springs & The Smelly Fire",
                "HotSpringsShade",
                "Rating: Mature | Tags: Spa Day, Aromatic Romance, Smoke & Mirrors, Teleportation Shenanigans",
                "books/fanfics/fanfic_06.txt",
                "books/book_cover_06.png",
                new[]
                {
                    "Summary: What happens when you throw a lump of yellow sulfur onto a roaring hearth fire with all the curtains drawn? A sensory experience that leaves the Shade breathless.",
                    "Chapter 1: Fumes of Passion",
                    "The greenish-gold vapor curled lazily around the armchair, thick and intoxicating. The Shade unbuttoned his invisible collar and sank deeper into the cushions.",
                    "As the sulfur smoke caressed his cheeks, the walls of his cavern home melted away into a hazy dreamscape where every stalagmite looked suspiciously suggestive."
                }
            ),
            new FanficVolume(
                "Cross-World Crush: The Knight's Nail & The Shade's Mattock",
                "ArchipelagoShipper99",
                "Rating: Mature | Tags: Multiworld Crossover, Long Distance Relationship, Item Swap, Hollow Knight x Shade",
                "books/fanfics/fanfic_07.txt",
                "books/book_cover_07.png",
                new[]
                {
                    "Summary: Every time the Shade chips a rock in the Pillared Gallery, a mysterious vessel in Hallownest receives a Mothwing Cloak—and sends back a very affectionate love letter wrapped around a lump of Coal.",
                    "Chapter 1: Connected by the Socket",
                    "\"Received Progressive Walk Speed from Knight_Slot2,\" the glowing rune announced in the corner of the Shade's vision. His heart skipped a beat. His legs felt lighter, faster, positively electric.",
                    "He hurried to the Cavern Peddler's stall, clutching four warm lumps of coal against his chest. \"Send him the Bargain Relic #3,\" the Shade told the Peddler breathlessly. \"And tell him... tell him I'm wearing the King's Tiny Crown tonight.\""
                }
            ),
            new FanficVolume(
                "Midnight at the Halls of Eternity (Shirtless Edition)",
                "InfiniteCorridorLover",
                "Rating: Mature | Tags: Endless Walking, Wardrobe Malfunction, Carpet Appreciation, Echoes",
                "books/fanfics/fanfic_08.txt",
                "books/book_cover_08.png",
                new[]
                {
                    "Summary: In a corridor that stretches forever, nobody can judge you for walking around in nothing but an Eternal Carpet II draped over your shoulders.",
                    "Chapter 1: Mile 42",
                    "The marble floor of the Halls of Eternity was cool beneath the Shade's bare soles. Ahead of him lay a rolled-up Eternal Curtain, practically begging to be carried all the way back to Ring 1.",
                    "He hoisted the plush fabric onto his shoulder, feeling the velvet fringe tickle the nape of his neck with every slow, deliberate stride."
                }
            ),
            new FanficVolume(
                "My Roommate is a Talking Horse HeadPlaque",
                "TaxidermyRomance",
                "Rating: Teen & Up | Tags: Crack Treated Seriously, Domestic Fluff, Whispering Wall Art, Late Night Chats",
                "books/fanfics/fanfic_09.txt",
                "books/book_cover_09.png",
                new[]
                {
                    "Summary: After hanging the Horse Head Trophy above his bed, the Shade discovers it has a very sultry baritone voice after midnight.",
                    "Chapter 1: Neigh Means Yes",
                    "\"Nice chalk drawing on the east wall,\" murmured the Horse Head Trophy as the fireplace embers dimmed to a ruby glow. \"Is that red clay, or are you just happy to see me?\"",
                    "The Shade pulled the moss blanket up to his chin, giggling into the dark."
                }
            ),
            new FanficVolume(
                "Two Mushrooms, One Dark Cave: The Secret Tower Experiment",
                "Mycelium Matchmaker",
                "Rating: Mature | Tags: Botanical Chemistry, Big Bounce, Secret Tower, Glowing Spores",
                "books/fanfics/fanfic_10.txt",
                "books/book_cover_10.png",
                new[]
                {
                    "Summary: When you plant a Green Mushroom right next to a Purple Mushroom in the damp soil of the Great Cave, the resulting fungal explosion is pure ecstasy.",
                    "Chapter 1: Spore Play",
                    "The purple cap pulsed with a bioluminescent violet glow, leaning ever so slightly toward its emerald neighbour. As the Shade sprinkled a pinch of Mushroom Fertilizer over their stems, the entire cavern filled with shimmering golden spores.",
                    "With a triumphant boing, the Giant Mushroom swelled to full size, launching the Shade straight toward the Secret Tower with tears of joy in his eyes."
                }
            ),
        };

        public static bool IsAo3ModeActive
        {
            get
            {
                int localOverride = FileBasedPrefs.GetInt(LocalOverridePrefKey, -1);
                if (localOverride == 1)
                {
                    return true;
                }
                if (localOverride == 0)
                {
                    return false;
                }
                return Plugin.Session != null && Plugin.Session.IsConnected && Plugin.Session.ActiveConfig.Ao3FanficBooks;
            }
            set
            {
                FileBasedPrefs.SetInt(LocalOverridePrefKey, value ? 1 : 0);
                FileBasedPrefs.ManualySave();
                RefreshAllActiveShelfBooks();
            }
        }

        public static void ToggleAo3Mode()
        {
            IsAo3ModeActive = !IsAo3ModeActive;
            Plugin.Delivery?.PushNotification(IsAo3ModeActive
                ? "AO3 Fanfiction Book Mode ENABLED! Check your bookshelf at Home."
                : "AO3 Fanfiction Book Mode disabled (classic literature restored).");
        }

        public static void RefreshAllActiveShelfBooks()
        {
            try
            {
                var books = UnityEngine.Object.FindObjectsOfType<BookInShelf>();
                foreach (var b in books)
                {
                    ApplyShelfBookOverride(b);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed refreshing shelf books: {ex.Message}");
            }
        }

        private static FanficVolume SelectFanficForKey(string keyOrFile)
        {
            if (string.IsNullOrEmpty(keyOrFile))
            {
                return LibraryFanfics[0];
            }
            int hash = 17;
            foreach (char c in keyOrFile.ToLowerInvariant())
            {
                hash = unchecked(hash * 31 + c);
            }
            int idx = Math.Abs(hash) % LibraryFanfics.Length;
            return LibraryFanfics[idx];
        }

        [HarmonyPatch(typeof(BookInShelf), nameof(BookInShelf.Init))]
        [HarmonyPostfix]
        private static void OnBookInShelfInit(BookInShelf __instance)
        {
            ApplyShelfBookOverride(__instance);
        }

        private static void ApplyShelfBookOverride(BookInShelf shelfBook)
        {
            if (shelfBook == null || !IsAo3ModeActive)
            {
                return;
            }

            try
            {
                string origTitle = ShelfTitleField?.GetValue(shelfBook) as string ?? shelfBook.gameObject.name;
                string origFile = ShelfFileField?.GetValue(shelfBook) as string ?? origTitle;
                var fanfic = SelectFanficForKey(origFile);

                if (ShelfLabelField?.GetValue(shelfBook) is TextMeshProUGUI label && label != null)
                {
                    label.text = fanfic.Title;
                }

                if (ShelfImageField?.GetValue(shelfBook) is Image img && img != null && !string.IsNullOrEmpty(fanfic.CoverImageFile))
                {
                    var customTex = CustomAssetLoader.LoadTextureOrNull(fanfic.CoverImageFile);
                    if (customTex != null)
                    {
                        var coverSprite = Sprite.Create(
                            customTex,
                            new Rect(0f, 0f, customTex.width, customTex.height),
                            new Vector2(0.5f, 0.5f),
                            100f
                        );
                        img.sprite = coverSprite;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"BookInShelf AO3 override error: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(ReadingBehaviour), nameof(ReadingBehaviour.OpenBook))]
        [HarmonyPostfix]
        private static void OnOpenBookPostfix(ReadingBehaviour __instance, string FKEBECGHCDH, string CBKOHMKMCNM)
        {
            if (__instance == null || !IsAo3ModeActive)
            {
                return;
            }

            try
            {
                var fanfic = SelectFanficForKey(FKEBECGHCDH ?? CBKOHMKMCNM);
                string[] formattedLines = BuildFormattedBookLines(fanfic);

                ReadLinesField?.SetValue(__instance, formattedLines);

                if (ReadTitleLabelField?.GetValue(__instance) is TextMeshProUGUI titleLabel && titleLabel != null)
                {
                    titleLabel.text = fanfic.Title;
                }

                int currentLine = (int)(ReadPageLineIdxField?.GetValue(__instance) ?? 0);
                if (currentLine < 0 || currentLine >= formattedLines.Length)
                {
                    ReadPageLineIdxField?.SetValue(__instance, 0);
                    currentLine = 0;
                }

                // Refresh left & right page text immediately
                int linesPerPage = (int)(ReadLinesPerPageField?.GetValue(__instance) ?? 20);
                var leftTmp = ReadLeftPageField?.GetValue(__instance) as TextMeshProUGUI;
                var rightTmp = ReadRightPageField?.GetValue(__instance) as TextMeshProUGUI;

                if (leftTmp != null)
                {
                    leftTmp.text = SlicePageLines(formattedLines, currentLine, linesPerPage);
                }
                if (rightTmp != null)
                {
                    rightTmp.text = SlicePageLines(formattedLines, currentLine + linesPerPage, linesPerPage);
                }

                SetPageCounterMethod?.Invoke(__instance, new object[] { currentLine });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"ReadingBehaviour AO3 override error: {ex.Message}");
            }
        }

        private static string SlicePageLines(string[] lines, int startLine, int count)
        {
            if (lines == null || startLine >= lines.Length)
            {
                return string.Empty;
            }
            var sb = new StringBuilder();
            int end = Math.Min(lines.Length, startLine + count);
            for (int i = startLine; i < end; i++)
            {
                sb.Append(lines[i]).Append('\n');
            }
            return sb.ToString();
        }

        private static string[] BuildFormattedBookLines(FanficVolume fanfic)
        {
            // Check if the user placed an edited .txt file in CustomAssets/books/fanfics/
            string customPath = CustomAssetLoader.ResolveAssetPath(fanfic.CustomTxtFile);
            if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
            {
                try
                {
                    string rawText = File.ReadAllText(customPath, Encoding.UTF8);
                    return WrapBookText(rawText, 38);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"Failed reading custom fanfic {customPath}: {ex.Message}");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Archive of Our Cavern (AO3)");
            sb.AppendLine($"Title: {fanfic.Title}");
            sb.AppendLine($"Author: {fanfic.Author}");
            sb.AppendLine(fanfic.RatingAndTags);
            sb.AppendLine("Kudos: 400 | Bookmarks: 69 | Hits: 4,000");
            sb.AppendLine("------------------------------------");
            sb.AppendLine();
            foreach (string para in fanfic.Paragraphs)
            {
                sb.AppendLine(para);
                sb.AppendLine();
            }
            sb.AppendLine("--- End of Chapter ---");
            sb.AppendLine("(Leave Kudos at the Cavern Peddler!)");

            return WrapBookText(sb.ToString(), 38);
        }

        private static string[] WrapBookText(string text, int maxCols)
        {
            var lines = new List<string>();
            string normalized = (text ?? string.Empty).Replace("\r\n", "\n");
            foreach (string rawLine in normalized.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    lines.Add(string.Empty);
                    continue;
                }

                string[] words = rawLine.Trim().Split(' ');
                var current = new StringBuilder();
                foreach (string word in words)
                {
                    if (current.Length == 0)
                    {
                        current.Append(word);
                    }
                    else if (current.Length + 1 + word.Length <= maxCols)
                    {
                        current.Append(' ').Append(word);
                    }
                    else
                    {
                        lines.Add(current.ToString());
                        current.Clear();
                        current.Append(word);
                    }
                }
                if (current.Length > 0)
                {
                    lines.Add(current.ToString());
                }
            }

            // Guarantee at least 20 lines so ReadingBehaviour's first page spread never index-faults
            while (lines.Count < 22)
            {
                lines.Add(string.Empty);
            }
            return lines.ToArray();
        }
    }
}
