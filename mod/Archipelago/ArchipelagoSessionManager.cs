using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using TheLongingDecrypted.Generated;
using TheLongingDecrypted.Patches;
using TheLongingDecrypted.UI;
using UnityEngine.SceneManagement;

namespace TheLongingDecrypted.Archipelago
{
    /// <summary>What lies wrapped on one of the Peddler's far shelves.</summary>
    public sealed class ShopOffer
    {
        public string ItemName { get; }
        public string PlayerName { get; }
        public bool IsMine { get; }
        public long ItemId { get; }

        public ShopOffer(string itemName, string playerName, bool isMine, long itemId)
        {
            ItemName = itemName;
            PlayerName = playerName;
            IsMine = isMine;
            ItemId = itemId;
        }
    }

    public sealed class ArchipelagoSessionManager
    {
        public const string GameName = "The Longing";
        private const string ReceivedIndexPrefKey = "AP_RECEIVED_ITEM_INDEX";
        private const string CountdownAppliedPrefKey = "AP_COUNTDOWN_INITIALIZED";
        private const string GoalSentPrefKey = "AP_GOAL_COMPLETED";

        private readonly ItemDeliveryService _delivery;
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private readonly HashSet<long> _checkedLocations = new HashSet<long>();
        private readonly ConcurrentDictionary<long, ShopOffer> _shopOffers = new ConcurrentDictionary<long, ShopOffer>();

        private ArchipelagoSession _session;
        private DeathLinkService _deathLinkService;
        private bool _initialSyncPending;
        private bool _initialCountdownPending;
        private DeathLink _pendingDeathLink;

        public bool IsConnected => _session != null && _session.Socket != null && _session.Socket.Connected;
        public string StatusMessage { get; private set; } = "Disconnected";
        public string ConnectedHost { get; private set; } = string.Empty;
        public string ConnectedSlot { get; private set; } = string.Empty;
        public string SeedName { get; private set; } = string.Empty;
        public SlotConfig ActiveConfig { get; private set; } = new SlotConfig();
        public int CheckedCount => _checkedLocations.Count;
        public int TotalLocations => ActiveConfig.EnabledLocationIds.Count;
        public bool GoalSent => FileBasedPrefs.GetInt(GoalSentPrefKey, 0) > 0;

        public bool IsLocationChecked(long locationId) => _checkedLocations.Contains(locationId);

        public ShopOffer GetShopOffer(long locationId)
        {
            return _shopOffers.TryGetValue(locationId, out var offer) ? offer : null;
        }

        /// <summary>True once this slot was sent at least one copy of the named item.</summary>
        public bool HasReceived(string itemName)
        {
            return _session != null && _session.Items.AllItemsReceived.Any(i =>
                WorldRegistryData.ItemsById.TryGetValue(i.ItemId, out var entry) && entry.Name == itemName);
        }

        public ArchipelagoSessionManager(ItemDeliveryService delivery)
        {
            _delivery = delivery;
        }

        public bool Connect(string host, int port, string slotName, string password)
        {
            string address = $"{host}:{port}";
            bool inCavern = GameManagerScript.IsGameRunning() && SceneManager.GetActiveScene().buildIndex >= 3;
            if (inCavern && (ConnectedHost != address || ConnectedSlot != slotName))
            {
                StatusMessage = Lines.NeverKnock;
                return false;
            }

            Disconnect();
            try
            {
                StatusMessage = $"Connecting to {host}:{port}...";
                var session = ArchipelagoSessionFactory.CreateSession(host, port);
                session.Socket.ErrorReceived += (error, message) =>
                    Plugin.Log.LogWarning($"Archipelago socket: {message} {error}");
                var result = session.TryConnectAndLogin(
                    GameName,
                    slotName,
                    ItemsHandlingFlags.AllItems,
                    new Version(0, 6, 8),
                    null,
                    null,
                    string.IsNullOrEmpty(password) ? null : password,
                    true
                );

                if (!result.Successful)
                {
                    var failure = (LoginFailure)result;
                    StatusMessage = "Login failed: " + string.Join("; ", failure.Errors);
                    Plugin.Log.LogError(StatusMessage);
                    _ = session.Socket.DisconnectAsync();
                    return false;
                }

                var success = (LoginSuccessful)result;
                string seed = session.RoomState?.Seed ?? "default";
                if (inCavern && SeedName != seed)
                {
                    StatusMessage = Lines.NeverKnock;
                    _ = session.Socket.DisconnectAsync();
                    return false;
                }

                _session = session;
                ConnectedHost = address;
                ConnectedSlot = slotName;
                SeedName = seed;
                ActiveConfig = SlotConfig.FromSlotData(success.SlotData);

                // Isolate save folder per seed + slot before touching FileBasedPrefs
                Plugin.ActivateSlotSaveIsolation(SeedName, slotName);

                // Sync already-checked locations from server
                _checkedLocations.Clear();
                foreach (var locId in session.Locations.AllLocationsChecked)
                {
                    _checkedLocations.Add(locId);
                }

                _shopOffers.Clear();

                // Subscribe to incoming items
                session.Items.ItemReceived += OnItemReceived;
                session.MessageLog.OnMessageReceived += message => OnServerMessage(session, message);
                session.Socket.SocketClosed += reason =>
                {
                    _mainThreadQueue.Enqueue(() =>
                    {
                        if (_session != session)
                        {
                            return;
                        }
                        StatusMessage = $"Disconnected ({reason})";
                        _delivery.Note(StatusMessage);
                        FrameHud.Say(Lines.MultiworldName, Lines.MultiworldAsleep, "icons/worlds_dim.png");
                    });
                };

                // Configure DeathLink if enabled in YAML
                if (ActiveConfig.DeathLink)
                {
                    _deathLinkService = session.CreateDeathLinkService();
                    _deathLinkService.EnableDeathLink();
                    _deathLinkService.OnDeathLinkReceived += dl =>
                    {
                        _mainThreadQueue.Enqueue(() =>
                        {
                            if (_session == session && IsConnected)
                            {
                                _pendingDeathLink = dl;
                            }
                        });
                    };
                }

                _initialSyncPending = true;
                _initialCountdownPending = true;
                SyncSlotWhenWritable();
                if (ActiveConfig.AutoHints)
                {
                    ScoutShopLocationsAsync();
                }

                StatusMessage = $"Connected ({slotName} @ {host}:{port})";
                Plugin.Log.LogInfo(StatusMessage);
                FrameHud.Say(Lines.MultiworldName, $"{Lines.MultiworldAwake} {Lines.Goal(ActiveConfig.Goal)}", "icons/ink_worlds.png");
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection error: {ex.Message}";
                Plugin.Log.LogError(ex);
                return false;
            }
        }

        public void Disconnect()
        {
            _initialSyncPending = false;
            _initialCountdownPending = false;
            _pendingDeathLink = null;
            if (_session != null)
            {
                try
                {
                    _session.Items.ItemReceived -= OnItemReceived;
                    _session.Socket.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"Error disconnecting session: {ex.Message}");
                }
                _session = null;
                _deathLinkService = null;
            }
            StatusMessage = "Disconnected";
        }

        public void PumpMainThread()
        {
            // The game suppresses writes during loading and scripted sequences.
            if (FileBasedPrefs.BlockNewSaves)
            {
                return;
            }

            SyncSlotWhenWritable();
            ApplyInitialCountdownIfReady();
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Main-thread AP callback error: {ex}");
                }
            }

            ApplyPendingDeathLink();
            if (IsConnected && !GoalSent)
            {
                CheckPassiveGoalCompletion();
            }
        }

        public bool IsLocationShuffled(long locationId)
        {
            return IsConnected && ActiveConfig.EnabledLocationIds.Contains(locationId);
        }

        public bool ReportLocationByUid(string gameUid)
        {
            if (string.IsNullOrEmpty(gameUid))
            {
                return false;
            }
            if (!WorldRegistryData.LocationsByUid.TryGetValue(gameUid, out var entry))
            {
                return false;
            }
            return ReportLocation(entry.Id, entry.Name);
        }

        public bool ReportLocation(long locationId, string displayName = null)
        {
            if (!IsConnected || !ActiveConfig.EnabledLocationIds.Contains(locationId))
            {
                return false;
            }

            if (_checkedLocations.Add(locationId))
            {
                _session.Locations.CompleteLocationChecksAsync(locationId);
                var label = displayName ?? (WorldRegistryData.LocationsById.TryGetValue(locationId, out var e) ? e.Name : locationId.ToString());
                Plugin.Log.LogInfo($"Checked {label}");
            }
            return true;
        }

        public void NotifyEndingReached(string endingType)
        {
            if (!IsConnected || GoalSent)
            {
                return;
            }

            var goal = ActiveConfig.Goal;
            bool match =
                goal == "any_ending"
                || (goal == "wake_the_king" && endingType == "wake_the_king")
                || (goal == "escape_surface" && endingType == "escape_surface")
                || (goal == "secret_ending" && endingType == "secret_ending");

            if (match)
            {
                SendGoalCompletion($"Goal achieved ({endingType})!");
            }
        }

        public void SendDeathLink(string cause)
        {
            if (!IsConnected || _deathLinkService == null || !ActiveConfig.DeathLink)
            {
                return;
            }
            _deathLinkService.SendDeathLink(new DeathLink(ConnectedSlot, cause));
        }

        private void CheckPassiveGoalCompletion()
        {
            if (ActiveConfig.Goal == "crystal_book_hunt")
            {
                if (_delivery.ReceivedCrystals >= ActiveConfig.RequiredCrystals
                    && _delivery.ReceivedBooks >= ActiveConfig.RequiredBooks)
                {
                    SendGoalCompletion("Crystal & Book Hunt complete!");
                }
            }
        }

        private void SendGoalCompletion(string reason)
        {
            if (!IsConnected || GoalSent)
            {
                return;
            }
            _session.SetGoalAchieved();
            FileBasedPrefs.SetInt(GoalSentPrefKey, 1);
            FileBasedPrefs.ManualySave();
            _delivery.Note(reason);
            FrameHud.Say(Lines.GoalReachedName, Lines.GoalReached, "ink:icons_0035");
        }

        private void OnItemReceived(IReceivedItemsHelper helper)
        {
            _mainThreadQueue.Enqueue(() =>
            {
                if (_session?.Items == helper && IsConnected)
                {
                    DrainReceivedItems(helper, replaySilent: false);
                }
            });
        }

        private void SyncSlotWhenWritable()
        {
            if (!_initialSyncPending || !IsConnected || FileBasedPrefs.BlockNewSaves)
            {
                return;
            }

            SaveIsolationPatches.EnsureSlotSaveExists();
            ApplyInitialCountdownIfReady();
            DrainReceivedItems(_session.Items, replaySilent: true);
            _initialSyncPending = false;
            _delivery.AnnounceQuietArrivals();
        }

        private void DrainReceivedItems(IReceivedItemsHelper helper, bool replaySilent)
        {
            int appliedIndex = FileBasedPrefs.GetInt(ReceivedIndexPrefKey, 0);
            var allItems = helper.AllItemsReceived;
            while (appliedIndex < allItems.Count)
            {
                var info = allItems[appliedIndex];
                bool fromMyself = info.Player != null && info.Player.Slot == _session.ConnectionInfo.Slot;
                _delivery.DeliverItem(info.ItemId, info.Player?.Alias, replaySilent, fromMyself);
                appliedIndex++;
                FileBasedPrefs.SetInt(ReceivedIndexPrefKey, appliedIndex);
                FileBasedPrefs.ManualySave();
            }
        }

        private void ApplyInitialCountdownIfReady()
        {
            if (!_initialCountdownPending || !IsConnected || FileBasedPrefs.BlockNewSaves)
            {
                return;
            }

            // The intro sets this flag before the timer processes its queued reset.
            if (FileBasedPrefs.GetInt("FIRSTFIRSTGAMESTART", 0) == 0)
            {
                return;
            }
            bool previouslyInitialized = FileBasedPrefs.GetInt(CountdownAppliedPrefKey, 0) > 0;
            if (SaveIsolationPatches.TryRestoreClock()
                || (previouslyInitialized && !Plugin.IsolateSavesConfig.Value))
            {
                _initialCountdownPending = false;
                return;
            }

            // The game sets FakeHudOff two seconds after starting the timer. Wait for
            // that handoff before saving the first checkpoint for a fresh slot.
            if (!previouslyInitialized && FileBasedPrefs.GetInt("FakeHudOff", 0) == 0)
            {
                return;
            }

            const long fourHundredDays = 34_560_000L;
            long targetSeconds = Math.Max(60L, Math.Min(fourHundredDays, ActiveConfig.CountdownSeconds));
            long initialAdvance = fourHundredDays - targetSeconds - TMan.GetOverallTime();
            if (initialAdvance != 0)
            {
                TMan.AddInstaTime(initialAdvance);
                TMan.SaveValues();
            }
            FileBasedPrefs.SetInt(CountdownAppliedPrefKey, 1);
            SaveIsolationPatches.EnableClockSaving();
            FileBasedPrefs.ManualySave();
            _initialCountdownPending = false;
        }

        private void ApplyPendingDeathLink()
        {
            if (_pendingDeathLink == null || !IsConnected || SceneManager.GetActiveScene().buildIndex < 3)
            {
                return;
            }

            var game = UnityEngine.Object.FindObjectOfType<GameManagerScript>();
            if (game == null)
            {
                return;
            }

            var dl = _pendingDeathLink;
            _pendingDeathLink = null;
            var cause = string.IsNullOrEmpty(dl.Cause) ? $"{dl.Source} succumbed" : dl.Cause;
            _delivery.Note($"DeathLink from {dl.Source}: {cause}");
            FrameHud.Say(dl.Source, Lines.DeathLink, "ink:icons_0032");
            game.LoadNewScene(4);
        }

        /// <summary>Tells the Shade what became of something it just found for somebody else.</summary>
        private void OnServerMessage(ArchipelagoSession session, LogMessage message)
        {
            if (message is HintItemSendLogMessage || !(message is ItemSendLogMessage sent)
                || !sent.IsSenderTheActivePlayer || sent.IsReceiverTheActivePlayer)
            {
                return;
            }
            string item = sent.Item.ItemDisplayName ?? sent.Item.ItemName;
            string owner = sent.Receiver.Alias;
            _mainThreadQueue.Enqueue(() =>
            {
                if (_session == session)
                {
                    _delivery.Note($"I sent {item} to {owner}.");
                    FrameHud.Say(item, Lines.SentAway(owner), "icons/ink_bundle.png");
                }
            });
        }

        public void ScoutShopLocationsAsync()
        {
            if (!IsConnected)
            {
                return;
            }
            var session = _session;
            var shopIds = WorldRegistryData.LocationsById.Values
                .Where(e => e.Pool == "shop" && ActiveConfig.EnabledLocationIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToArray();
            if (shopIds.Length == 0)
            {
                return;
            }
            try
            {
                session.Locations.ScoutLocationsAsync(shopIds).ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled || task.Result == null)
                    {
                        return;
                    }
                    foreach (var found in task.Result)
                    {
                        _shopOffers[found.Key] = Describe(session, found.Value);
                    }
                });
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Shop scouting failed: {ex.Message}");
            }
        }

        private static ShopOffer Describe(ArchipelagoSession session, ScoutedItemInfo info)
        {
            bool mine = info.Player != null && info.Player.Slot == session.ConnectionInfo.Slot;
            return new ShopOffer(
                info.ItemDisplayName ?? info.ItemName ?? Lines.WrappedName,
                info.Player?.Alias ?? Lines.Somebody,
                mine,
                info.ItemId);
        }

        private List<long> UnheardLocations => ActiveConfig.EnabledLocationIds.Where(id => !_checkedLocations.Contains(id)).ToList();

        /// <summary>Whether there is anything left to whisper about.</summary>
        public bool CanWhisper => IsConnected && ActiveConfig.AutoHints && UnheardLocations.Count > 0;

        /// <summary>
        /// Asks what lies at a random unchecked location. Returns why that is impossible, or null once the answer is on
        /// its way to <paramref name="heard"/> on the main thread.
        /// </summary>
        public string Whisper(Action<string> heard)
        {
            if (!IsConnected || !ActiveConfig.AutoHints)
            {
                return Lines.ConchSilent;
            }
            var unheard = UnheardLocations;
            if (unheard.Count == 0)
            {
                return Lines.ConchNothingLeft;
            }
            var session = _session;
            long target = unheard[UnityEngine.Random.Range(0, unheard.Count)];
            string place = WorldRegistryData.LocationsById.TryGetValue(target, out var entry) ? entry.Name : target.ToString();
            try
            {
                session.Locations.ScoutLocationsAsync(new[] { target }).ContinueWith(task =>
                {
                    if (task.IsFaulted || task.IsCanceled || task.Result == null || !task.Result.TryGetValue(target, out var info))
                    {
                        return;
                    }
                    var offer = Describe(session, info);
                    string line = Lines.ConchWhisper(place, offer.ItemName, offer.PlayerName, offer.IsMine);
                    _mainThreadQueue.Enqueue(() =>
                    {
                        _delivery.Note(line);
                        heard(line);
                    });
                });
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Whisper failed: {ex.Message}");
                return Lines.ConchSilent;
            }
        }
    }
}
