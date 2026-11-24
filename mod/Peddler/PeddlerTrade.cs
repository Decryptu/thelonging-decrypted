using System;
using System.Collections.Generic;
using CodeStage.AntiCheat.ObscuredTypes;
using TheLongingDecrypted.Archipelago;
using TheLongingDecrypted.Cosmetics;
using TheLongingDecrypted.Generated;
using TheLongingDecrypted.Relics;
using TheLongingDecrypted.UI;

namespace TheLongingDecrypted.Peddler
{
    /// <summary>One thing on the Peddler's shelves.</summary>
    internal sealed class Offer
    {
        public string Icon;
        public string Name;
        public string Line;
        public string Price;
        public int Coal;
        public int Moss;

        /// <summary>Why it cannot be had right now, or null.</summary>
        public Func<string> Refusal;

        /// <summary>No longer on the shelf.</summary>
        public Func<bool> IsGone;

        /// <summary>Hands it over. Returns the Peddler's words.</summary>
        public Func<string> Hand;

        /// <summary>0 when fresh, 1 when the Peddler has only just done it.</summary>
        public Func<float> Rest;
    }

    /// <summary>What the Peddler sells and on which terms.</summary>
    internal static class PeddlerTrade
    {
        private const string MattockKey = "PH534TLO0";
        private static readonly int[] FarPrices = { 2, 3, 4, 5, 6, 8, 10, 12 };

        public static List<Offer> FarShelf()
        {
            var offers = new List<Offer>();
            var session = Plugin.Session;
            for (int i = 1; i <= FarPrices.Length; i++)
            {
                if (!WorldRegistryData.LocationsByUid.TryGetValue("shop:peddler_" + i, out var location))
                {
                    continue;
                }
                // The shelf stays empty when this world was generated without it.
                if (!string.IsNullOrEmpty(session.ConnectedSlot) && !session.ActiveConfig.EnabledLocationIds.Contains(location.Id))
                {
                    continue;
                }
                int price = FarPrices[i - 1];
                bool stuck = i >= 5;
                var known = session.GetShopOffer(location.Id);
                offers.Add(new Offer
                {
                    Icon = known == null ? "icons/ink_bundle.png" : OfferIcon(known),
                    Name = known == null ? Lines.WrappedName : known.ItemName,
                    Line = known == null ? Lines.WrappedLine : known.IsMine ? Lines.ForMe : Lines.ForSomebody(known.PlayerName),
                    Price = Lines.Price(price),
                    Coal = price,
                    IsGone = () => session.IsLocationChecked(location.Id),
                    Refusal = () => !session.IsConnected ? Lines.MultiworldAsleep : stuck && !HasMattock() ? Lines.Stuck : null,
                    Hand = () => session.ReportLocation(location.Id, location.Name) ? Lines.Sold : null,
                });
            }
            return offers;
        }

        public static List<Offer> WaresShelf()
        {
            var offers = new List<Offer>();
            foreach (var relic in RelicBag.All)
            {
                offers.Add(Ware(relic.Key, relic.Asset, relic.Price, () => RelicBag.Owned(relic.Key), () => RelicBag.Grant(relic.Key)));
            }
            foreach (var hat in Wardrobe.Hats)
            {
                offers.Add(Ware(hat.Key, hat.Asset, hat.Price, () => Wardrobe.Owned(hat.Key), () => Wardrobe.Grant(hat.Key)));
            }
            foreach (var pet in Wardrobe.Pets)
            {
                offers.Add(Ware(pet.Key, pet.Asset, pet.Price, () => Wardrobe.Owned(pet.Key), () => Wardrobe.Grant(pet.Key)));
            }
            return offers;
        }

        public static List<Offer> FavorsShelf()
        {
            return new List<Offer>
            {
                Favor("WHISPER", "items/item_conch.png", "A whisper", "I hear things. I could tell you one of them.", 5, 30, () =>
                    Plugin.Session.Whisper(line => FrameHud.Say(Lines.PeddlerName, line, "items/item_conch.png")) ?? Lines.WhisperGiven,
                    () => Plugin.Session.CanWhisper ? null : Lines.ConchSilent),
                Favor("HOURGLASS", "ink:icons_0023", "A nudge", "I tip my hourglass a little. Only a little. He must not notice.", 6, 60, () =>
                {
                    TMan.AddInstaTime(RelicBag.NapSeconds(0.015));
                    TMan.SaveValues();
                    return Lines.HourglassTipped;
                }, () => RelicBag.NapSeconds(0.015) > 0L ? null : Lines.HourglassRefused),
                Favor("ESPRESSO", "items/item_espresso.png", "A small cup", "Hot, black, and gone too soon. Like a good day.", 4, 12, () =>
                {
                    RelicBag.Brew(45);
                    return Lines.EspressoPoured;
                }, null),
                new Offer
                {
                    Icon = "icons/ink_moss.png",
                    Name = "A soft bargain",
                    Line = "My knees are old. Bring me something soft and I will part with a lump of coal.",
                    Price = Lines.MossPrice(3),
                    Moss = 3,
                    Hand = () =>
                    {
                        RelicBag.Coal += 1;
                        return Lines.MossTaken;
                    },
                },
            };
        }

        /// <summary>Attempts the trade. Returns the Peddler's words.</summary>
        public static string Trade(Offer offer, out bool done)
        {
            done = false;
            if (offer.IsGone != null && offer.IsGone())
            {
                return Lines.Gone;
            }
            string refusal = offer.Refusal?.Invoke();
            if (refusal != null)
            {
                return refusal;
            }
            if (RelicBag.Coal < offer.Coal)
            {
                return Lines.NoCoal;
            }
            if (RelicBag.Moss < offer.Moss)
            {
                return Lines.NoMoss;
            }

            string words = offer.Hand();
            if (words == null)
            {
                return Lines.MultiworldAsleep;
            }
            if (offer.Coal > 0)
            {
                RelicBag.Coal -= offer.Coal;
            }
            if (offer.Moss > 0)
            {
                RelicBag.Moss -= offer.Moss;
            }
            FileBasedPrefs.ManualySave();
            done = true;
            return words;
        }

        private static Offer Ware(string key, string icon, int price, Func<bool> owned, Action grant)
        {
            var thing = Lines.Of(key);
            return new Offer
            {
                Icon = icon,
                Name = thing.Name,
                Line = thing.Thought,
                Price = Lines.Price(price),
                Coal = price,
                IsGone = owned,
                Hand = () =>
                {
                    grant();
                    return Lines.SoldKeep;
                },
            };
        }

        private static Offer Favor(string id, string icon, string name, string line, int price, int restMinutes, Func<string> hand, Func<string> refusal)
        {
            string stamp = "AP_FAVOR_" + id;
            return new Offer
            {
                Icon = icon,
                Name = name,
                Line = line,
                Price = Lines.Price(price),
                Coal = price,
                Rest = () => (float)Math.Min(1d, RelicBag.Remaining(stamp) / (restMinutes * 60d)),
                Refusal = () => RelicBag.Remaining(stamp) > 0d ? Lines.Resting : refusal?.Invoke(),
                Hand = () =>
                {
                    string words = hand();
                    RelicBag.StartWait(stamp, restMinutes * 60d);
                    return words;
                },
            };
        }

        private static string OfferIcon(ShopOffer offer)
        {
            if (offer.IsMine && WorldRegistryData.ItemsById.TryGetValue(offer.ItemId, out var entry))
            {
                return Icons.ForItem(entry.GameKey, entry.Category);
            }
            return "icons/ink_bundle.png";
        }

        /// <summary>A mattock that has since broken still counts as having brought one.</summary>
        private static bool HasMattock()
        {
            return !Plugin.Session.ActiveConfig.ShuffleMattocksAndKeys
                || ObscuredPrefs.GetInt(MattockKey, 0) > 0
                || Plugin.Session.HasReceived("Mattock");
        }
    }
}
