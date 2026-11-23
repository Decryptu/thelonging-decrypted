using System;
using System.Collections.Generic;
using System.Reflection;
using CodeStage.AntiCheat.ObscuredTypes;
using HarmonyLib;
using TheLongingDecrypted.Generated;
using TheLongingDecrypted.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLongingDecrypted.Patches
{
    /// <summary>
    /// Spawns "The Cavern Peddler" multiworld shop stall (centered on the middle glass window)
    /// and the Coal Vein in the Pillared Gallery (scene "gallery", buildIndex 24),
    /// triggers real ceiling-to-floor falling coal drops (TropfenSpawn / DestroyTropfenScript -> Interactable_GrabTropfen)
    /// around the coal vein capped at MaxFloorCoals, tracks World of Warcraft radial clock-sweep cooldowns,
    /// and manages multiworld + shop unlocks for all custom items, hats, and pets.
    /// </summary>
    [HarmonyPatch]
    internal static class CavernShopPatches
    {
        public const string CoalPrefKey = "KSTTFF34";
        public const string MossPrefKey = "ITMM0023Z";
        public const string MattockPrefKey = "DISUAD&JSAD§232";

        public const int MaxFloorCoals = 5;
        public static readonly Vector3 StallWorldPos = new Vector3(0.50f, -0.55f, 0f);
        public static readonly Vector3 VeinWorldPos = new Vector3(-2.25f, -1.72f, 0f);

        public sealed class CustomRelicDef
        {
            public string Key { get; }
            public string Name { get; }
            public string ShortLabel { get; }
            public string Description { get; }
            public string AssetPath { get; }
            public float CooldownSeconds { get; }
            public int ShopUnlockCoalCost { get; }

            public CustomRelicDef(string key, string name, string shortLabel, string description, string assetPath, float cooldownSeconds, int shopUnlockCoalCost)
            {
                Key = key;
                Name = name;
                ShortLabel = shortLabel;
                Description = description;
                AssetPath = assetPath;
                CooldownSeconds = cooldownSeconds;
                ShopUnlockCoalCost = shopUnlockCoalCost;
            }
        }

        public static readonly CustomRelicDef[] CustomRelics =
        {
            new CustomRelicDef("AP_CUSTOM_LANTERN", "Shade's Lantern", "Lantern", "Warm amber lantern aura around the Shade", "items/item_lantern.png", 4f, 3),
            new CustomRelicDef("AP_CUSTOM_CHISEL", "Chisel of Efficiency", "2x Chisel", "Doubles wall mining speed & +1 bonus Coal on floor pickups", "items/item_chisel.png", 4f, 4),
            new CustomRelicDef("AP_CUSTOM_MOSS_SACK", "Bottomless Moss Sack", "Moss Sack", "Doubles all Cavern Moss pickups (+3 Moss burst on use)", "items/item_moss_sack.png", 15f, 4),
            new CustomRelicDef("AP_ECHOING_CONCH", "Echoing Conch", "Conch", "Whispers an unchecked multiworld location hint", "items/item_conch.png", 20f, 3),
            new CustomRelicDef("AP_MUSHROOM_FERTILIZER", "Mushroom Fertilizer", "Fertilizer", "Instantly matures active mushroom sprouts & +30m growth", "items/item_fertilizer.png", 30f, 5),
            new CustomRelicDef("TIME_SKIP_43200", "King's Snooze Button", "Snooze 12h", "Skips 12 hours on the 400-day countdown", "items/item_snooze_button.png", 45f, 6),
            new CustomRelicDef("AP_BUFF_ESPRESSO", "Double Espresso", "Espresso", "3-minute walk speed zoomies rush", "items/item_espresso.png", 60f, 4),
        };

        public static readonly int[] ShopSlotCosts = { 2, 3, 4, 5, 6, 8, 10, 12 };
        public static bool IsShopOpen { get; set; }
        public static bool ShowRelicHudBar { get; set; } = true;
        public static bool ShowcaseUnlockAllInOuterUi { get; set; } = true;
        public static string LastShopFeedback { get; private set; } = "Welcome, little Shade. Gather fallen coal around the seam to trade for multiworld relics!";

        public static string ActiveToastTitle { get; private set; }
        public static string ActiveToastDesc { get; private set; }
        public static string ActiveToastAsset { get; private set; }
        public static float ActiveToastUntil { get; private set; }

        private static readonly float[] _relicCooldownStart = new float[7];
        private static readonly float[] _relicCooldownUntil = new float[7];

        private static GameObject _shopRoot;
        private static Transform _shopkeeperTransform;
        private static bool _autoShowcaseTriggered;
        private static float _autoTeleportAtTime = -1f;
        private static bool _teleportingToShopGallery;

        // Real ceiling-to-floor coal drop state (TropfenSpawn + DestroyTropfenScript -> Interactable_GrabTropfen)
        private static TropfenSpawn _galleryTropfenSpawn;
        private static GameObject _fallingTropfenPrefab;
        private static readonly List<DestroyTropfenScript> _veinCoalReceivers = new List<DestroyTropfenScript>();
        private static DestroyTropfenScript[] _allGalleryReceivers = Array.Empty<DestroyTropfenScript>();
        private static float _nextVeinCoalDropTime;
        private static int _lastObservedCoal = -1;
        private static int _lastObservedMoss = -1;

        private static readonly FieldInfo TropfenPrefabField = AccessTools.Field(typeof(TropfenSpawn), "INFGMCIGJCF");
        private static readonly FieldInfo DestroyCoalLandAnimField = AccessTools.Field(typeof(DestroyTropfenScript), "JONNKJEFNMH");
        private static readonly FieldInfo DestroyCoalPickableField = AccessTools.Field(typeof(DestroyTropfenScript), "ACPKKHCFJGE");
        private static readonly FieldInfo DestroySplashSoundField = AccessTools.Field(typeof(DestroyTropfenScript), "MLHPFOOJLAH");
        private static readonly FieldInfo DestroyInCameraField = AccessTools.Field(typeof(DestroyTropfenScript), "KBBBPNCKBEL");
        private static readonly FieldInfo DestroyMinRollField = AccessTools.Field(typeof(DestroyTropfenScript), "HOFGOHBBDMN");
        private static readonly FieldInfo DestroyMaxRollField = AccessTools.Field(typeof(DestroyTropfenScript), "CIEMOBGDACA");
        private static readonly FieldInfo DestroyThresholdField = AccessTools.Field(typeof(DestroyTropfenScript), "KIADJDOIPIM");

        public static int CurrentCoal
        {
            get => Math.Max(0, ObscuredPrefs.GetInt(CoalPrefKey, 0));
            set
            {
                int clamped = Math.Max(0, value);
                _lastObservedCoal = clamped;
                ObscuredPrefs.SetInt(CoalPrefKey, clamped);
            }
        }

        public static int CurrentMoss
        {
            get => Math.Max(0, ObscuredPrefs.GetInt(MossPrefKey, 0));
            set
            {
                int clamped = Math.Max(0, value);
                _lastObservedMoss = clamped;
                ObscuredPrefs.SetInt(MossPrefKey, clamped);
            }
        }

        public static int CurrentMattocks => Math.Max(0, ObscuredPrefs.GetInt(MattockPrefKey, 0));

        public static void ShowRelicToast(string title, string description, string assetPath, float duration = 4.2f)
        {
            ActiveToastTitle = title;
            ActiveToastDesc = description;
            ActiveToastAsset = assetPath;
            ActiveToastUntil = Time.unscaledTime + duration;
        }

        public static bool IsCustomRelicUnlocked(int index)
        {
            if (index < 0 || index >= CustomRelics.Length)
            {
                return false;
            }
            if (ShowcaseUnlockAllInOuterUi)
            {
                return true;
            }
            string key = CustomRelics[index].Key;
            return FileBasedPrefs.GetInt("AP_UNLOCK_" + key, 0) > 0 || FileBasedPrefs.GetInt(key, 0) > 0;
        }

        public static void UnlockCustomRelic(int index)
        {
            if (index < 0 || index >= CustomRelics.Length)
            {
                return;
            }
            string key = CustomRelics[index].Key;
            FileBasedPrefs.SetInt("AP_UNLOCK_" + key, 1);
            if (FileBasedPrefs.GetInt(key, 0) <= 0 && (key == "AP_CUSTOM_LANTERN" || key == "AP_CUSTOM_CHISEL" || key == "AP_CUSTOM_MOSS_SACK"))
            {
                FileBasedPrefs.SetInt(key, 1);
            }
            FileBasedPrefs.ManualySave();
        }

        public static float GetRelicCooldownRemainingSeconds(int index)
        {
            if (index < 0 || index >= CustomRelics.Length)
            {
                return 0f;
            }
            return Mathf.Max(0f, _relicCooldownUntil[index] - Time.unscaledTime);
        }

        public static float GetRelicCooldownRemainingRatio(int index)
        {
            if (index < 0 || index >= CustomRelics.Length)
            {
                return 0f;
            }
            float rem = _relicCooldownUntil[index] - Time.unscaledTime;
            if (rem <= 0f)
            {
                return 0f;
            }
            float dur = Mathf.Max(0.1f, _relicCooldownUntil[index] - _relicCooldownStart[index]);
            return Mathf.Clamp01(rem / dur);
        }

        public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _lastObservedCoal = CurrentCoal;
            _lastObservedMoss = CurrentMoss;

            if (!_autoShowcaseTriggered && scene.buildIndex >= 3 && scene.name != "menu" && scene.name != "intro" && scene.name != "credits")
            {
                _autoShowcaseTriggered = true;
                if (CosmeticAndCustomItemPatches.EquippedHatKey == "NONE")
                {
                    CosmeticAndCustomItemPatches.EquippedHatKey = "AP_HAT_MIFFY";
                }
                if (CosmeticAndCustomItemPatches.EquippedPetKey == "NONE")
                {
                    CosmeticAndCustomItemPatches.EquippedPetKey = "AP_PET_SPIDER";
                }
            }

            if (scene.name == "gallery" || scene.buildIndex == 24)
            {
                SetupGalleryShopScene();
                ShowRelicToast(
                    "The Cavern Peddler's Bazaar",
                    "Coal drops from the ceiling around the seam (max 5 on the floor). Pick it up or click the outer frame slots!",
                    "shop/shopkeeper_idle.png",
                    5.0f
                );
            }
        }

        public static void TeleportToShopGallery()
        {
            try
            {
                var gm = UnityEngine.Object.FindObjectOfType<GameManagerScript>();
                if (gm == null)
                {
                    return;
                }
                if (CosmeticAndCustomItemPatches.EquippedHatKey == "NONE")
                {
                    CosmeticAndCustomItemPatches.EquippedHatKey = "AP_HAT_MIFFY";
                }
                if (CosmeticAndCustomItemPatches.EquippedPetKey == "NONE" || CosmeticAndCustomItemPatches.EquippedPetKey == "AP_PET_ROCK")
                {
                    CosmeticAndCustomItemPatches.EquippedPetKey = "AP_PET_SPIDER";
                }
                _teleportingToShopGallery = true;
                gm.GetInputManager()?.StopAutoMoving();
                if (gm.ODEFEGGOFKP)
                {
                    gm.CloseFoldout();
                }
                gm.LoadNewScene(24);
                LastShopFeedback = "Teleported to the Pillared Gallery (The Cavern Peddler)!";
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"TeleportToShopGallery failed: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(GameManagerScript), "JIJFJAAELBE")]
        [HarmonyPostfix]
        private static void SpawnInFrontOfShopkeeper(GameManagerScript __instance, ref Vector3 __result)
        {
            if (!_teleportingToShopGallery)
            {
                return;
            }
            _teleportingToShopGallery = false;
            if (SceneManager.GetActiveScene().buildIndex == 24)
            {
                var waypoints = __instance?.GetWaypointManager()?.waypoints;
                if (waypoints != null && waypoints.Count > 0)
                {
                    int bestIdx = -1;
                    float bestDist = float.MaxValue;
                    for (int i = 0; i < waypoints.Count; i++)
                    {
                        if (waypoints[i] == null)
                        {
                            continue;
                        }
                        // Spawn between the Coal Vein (-2.25f) and the centered Shop Stall (+0.50f)
                        float d = Mathf.Abs(waypoints[i].pos.x - (-0.65f));
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestIdx = i;
                        }
                    }
                    if (bestIdx >= 0)
                    {
                        __result = waypoints[bestIdx].pos;
                    }
                }
            }
        }

        private static void SetupGalleryShopScene()
        {
            try
            {
                // 1. Optional full background replacement if CustomAssets/scenes/gallery_shop_full.png exists
                var customBgTex = CustomAssetLoader.LoadTextureOrNull("scenes/gallery_shop_full.png");
                if (customBgTex != null)
                {
                    var galleryGo = GameObject.Find("gallery");
                    var bgRenderer = galleryGo != null ? galleryGo.GetComponent<SpriteRenderer>() : null;
                    if (bgRenderer != null && bgRenderer.sprite != null)
                    {
                        var replacement = Sprite.Create(
                            customBgTex,
                            new Rect(0f, 0f, customBgTex.width, customBgTex.height),
                            new Vector2(0.5f, 0.5f),
                            100f
                        );
                        replacement.name = "gallery_shop_custom";
                        bgRenderer.sprite = replacement;
                    }
                }

                // 2. Spawn shop stall overlay (centered at x = +0.50f on the middle glass window) + shopkeeper + coal vein (-2.25f)
                if (_shopRoot != null)
                {
                    UnityEngine.Object.Destroy(_shopRoot);
                }

                _shopRoot = new GameObject("CavernPeddlerShopRoot");
                _shopRoot.transform.position = Vector3.zero;

                var stallGo = new GameObject("CavernPeddlerStall");
                stallGo.transform.SetParent(_shopRoot.transform, false);
                stallGo.transform.position = StallWorldPos;
                var stallRenderer = stallGo.AddComponent<SpriteRenderer>();
                stallRenderer.sortingOrder = 2;
                stallRenderer.sprite = CustomAssetLoader.LoadSprite(
                    "shop/shop_stall.png",
                    320,
                    360,
                    new Vector2(0.5f, 0.5f),
                    100f,
                    DrawFallbackStall
                );

                // Shopkeeper sprite standing behind the wooden stall counter (sortingOrder 1 < stall sortingOrder 2)
                var keeperGo = new GameObject("CavernPeddlerKeeper");
                keeperGo.transform.SetParent(stallGo.transform, false);
                keeperGo.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                var keeperRenderer = keeperGo.AddComponent<SpriteRenderer>();
                keeperRenderer.sortingOrder = 1;
                keeperRenderer.sprite = CustomAssetLoader.LoadSprite(
                    "shop/shopkeeper_idle.png",
                    160,
                    200,
                    new Vector2(0.5f, 0.5f),
                    100f,
                    DrawFallbackShopkeeper
                );
                _shopkeeperTransform = keeperGo.transform;

                // Warm amber shop lantern glow
                var lightGo = new GameObject("CavernPeddlerLanternLight");
                lightGo.transform.SetParent(stallGo.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.7f, -0.7f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1.0f, 0.78f, 0.35f, 1f);
                light.range = 4.5f;
                light.intensity = 1.9f;

                // Coal Vein at worldX = -2.25f, worldY = -1.72f (spawns real falling ceiling coal around it)
                var veinGo = new GameObject("InfiniteCoalVein");
                veinGo.transform.SetParent(_shopRoot.transform, false);
                veinGo.transform.position = VeinWorldPos;
                var veinRenderer = veinGo.AddComponent<SpriteRenderer>();
                veinRenderer.sortingOrder = 3;
                veinRenderer.sprite = CustomAssetLoader.LoadSprite(
                    "shop/coal_vein_infinite.png",
                    140,
                    140,
                    new Vector2(0.5f, 0.5f),
                    100f,
                    DrawFallbackCoalVein
                );

                // 3. Configure real ceiling-to-floor coal drop receivers around the Coal Vein
                SetupRealCoalDropsAroundVein();

                if (Plugin.Session != null && Plugin.Session.IsConnected && Plugin.Session.ActiveConfig.AutoHints)
                {
                    Plugin.Session.ScoutShopLocationsAsync();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Failed to set up Cavern Peddler scene objects: {ex.Message}");
            }
        }

        private static void SetupRealCoalDropsAroundVein()
        {
            _veinCoalReceivers.Clear();
            _galleryTropfenSpawn = UnityEngine.Object.FindObjectOfType<TropfenSpawn>();
            _fallingTropfenPrefab = _galleryTropfenSpawn != null
                ? TropfenPrefabField?.GetValue(_galleryTropfenSpawn) as GameObject
                : null;

            _allGalleryReceivers = UnityEngine.Object.FindObjectsOfType<DestroyTropfenScript>() ?? Array.Empty<DestroyTropfenScript>();

            GameObject templateLandAnim = null;
            GameObject templatePickableCoal = null;
            GameObject templateSound = null;

            foreach (var r in _allGalleryReceivers)
            {
                if (r == null)
                {
                    continue;
                }
                var pickable = DestroyCoalPickableField?.GetValue(r) as GameObject;
                var landAnim = DestroyCoalLandAnimField?.GetValue(r) as GameObject;
                var snd = DestroySplashSoundField?.GetValue(r) as GameObject;
                if (pickable != null && templatePickableCoal == null)
                {
                    templatePickableCoal = pickable;
                }
                if (landAnim != null && templateLandAnim == null)
                {
                    templateLandAnim = landAnim;
                }
                if (snd != null && templateSound == null)
                {
                    templateSound = snd;
                }
            }

            foreach (var r in _allGalleryReceivers)
            {
                if (r == null)
                {
                    continue;
                }
                float rx = r.transform.position.x;
                // Receivers around the Coal Vein (-2.25f) and Shop area (-4.5f .. +1.8f)
                if (rx >= -4.5f && rx <= 1.8f)
                {
                    if (templatePickableCoal != null && DestroyCoalPickableField?.GetValue(r) == null)
                    {
                        DestroyCoalPickableField?.SetValue(r, templatePickableCoal);
                    }
                    if (templateLandAnim != null && DestroyCoalLandAnimField?.GetValue(r) == null)
                    {
                        DestroyCoalLandAnimField?.SetValue(r, templateLandAnim);
                    }
                    if (templateSound != null && DestroySplashSoundField?.GetValue(r) == null)
                    {
                        DestroySplashSoundField?.SetValue(r, templateSound);
                    }
                    _veinCoalReceivers.Add(r);
                }
            }

            // Trigger first ceiling coal drop shortly after entering if fewer than 2 coals are on the floor
            _nextVeinCoalDropTime = Time.unscaledTime + 0.9f;
        }

        public static int CountActiveFloorCoals()
        {
            var coals = UnityEngine.Object.FindObjectsOfType<Interactable_GrabTropfen>();
            if (coals == null)
            {
                return 0;
            }
            int activeCount = 0;
            for (int i = 0; i < coals.Length; i++)
            {
                if (coals[i] != null && coals[i].gameObject.activeInHierarchy && coals[i].IsInteractableAtAll())
                {
                    activeCount++;
                }
            }
            return activeCount;
        }

        private static void UpdateRealCoalSpawning()
        {
            if (SceneManager.GetActiveScene().name != "gallery" && SceneManager.GetActiveScene().buildIndex != 24)
            {
                return;
            }

            int activeCoals = CountActiveFloorCoals();

            // Enforce strict MaxFloorCoals cap across all DestroyTropfenScript receivers in the room
            if (activeCoals >= MaxFloorCoals)
            {
                for (int i = 0; i < _allGalleryReceivers.Length; i++)
                {
                    var r = _allGalleryReceivers[i];
                    if (r != null)
                    {
                        DestroyThresholdField?.SetValue(r, 999);
                    }
                }
                return;
            }

            if (Time.unscaledTime < _nextVeinCoalDropTime)
            {
                return;
            }

            _nextVeinCoalDropTime = Time.unscaledTime + UnityEngine.Random.Range(5.0f, 8.5f);

            if (_fallingTropfenPrefab == null || _veinCoalReceivers.Count == 0)
            {
                return;
            }

            // Find receivers near the Coal Vein that do not already have an unpicked coal sitting on them
            var candidates = new List<DestroyTropfenScript>();
            for (int i = 0; i < _veinCoalReceivers.Count; i++)
            {
                var r = _veinCoalReceivers[i];
                if (r == null)
                {
                    continue;
                }
                var existingCoal = r.GetComponentInChildren<Interactable_GrabTropfen>(false);
                if (existingCoal != null && existingCoal.gameObject.activeInHierarchy && existingCoal.IsInteractableAtAll())
                {
                    continue;
                }
                candidates.Add(r);
            }

            if (candidates.Count == 0)
            {
                return;
            }

            var chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            // Ensure this receiver will convert the falling ceiling drop into a real landing coal -> pickable floor coal
            DestroyInCameraField?.SetValue(chosen, true);
            DestroyMinRollField?.SetValue(chosen, 8);
            DestroyMaxRollField?.SetValue(chosen, 10);
            DestroyThresholdField?.SetValue(chosen, 5);

            Vector3 spawnPos = chosen.transform.parent != null
                ? chosen.transform.parent.position
                : new Vector3(chosen.transform.position.x, 1.85f, 0f);
            UnityEngine.Object.Instantiate(_fallingTropfenPrefab, spawnPos, Quaternion.identity);
        }

        private static void CheckNaturalPickupBonuses()
        {
            int curCoal = Math.Max(0, ObscuredPrefs.GetInt(CoalPrefKey, 0));
            if (_lastObservedCoal >= 0 && curCoal > _lastObservedCoal)
            {
                int diff = curCoal - _lastObservedCoal;
                // Natural floor pickup adds +1 Coal; if Chisel of Efficiency is active, grant +1 bonus Coal
                if (diff == 1 && IsCustomRelicActive(1))
                {
                    curCoal += 1;
                    ObscuredPrefs.SetInt(CoalPrefKey, curCoal);
                    ShowRelicToast("Chisel of Efficiency", "Floor coal split cleanly: +1 bonus Coal! (Total: " + curCoal + ")", "items/item_chisel.png", 2.6f);
                }
            }
            _lastObservedCoal = curCoal;

            int curMoss = Math.Max(0, ObscuredPrefs.GetInt(MossPrefKey, 0));
            if (_lastObservedMoss >= 0 && curMoss > _lastObservedMoss)
            {
                int diff = curMoss - _lastObservedMoss;
                if (diff == 1 && IsCustomRelicActive(2))
                {
                    curMoss += 1;
                    ObscuredPrefs.SetInt(MossPrefKey, curMoss);
                    ShowRelicToast("Bottomless Moss Sack", "Doubled moss pickup: +1 bonus Cavern Moss! (Total: " + curMoss + ")", "items/item_moss_sack.png", 2.6f);
                }
            }
            _lastObservedMoss = curMoss;
        }

        public static void UpdateShopScene()
        {
            if (_autoTeleportAtTime > 0f && Time.unscaledTime >= _autoTeleportAtTime)
            {
                _autoTeleportAtTime = -1f;
                if (SceneManager.GetActiveScene().buildIndex != 24)
                {
                    TeleportToShopGallery();
                }
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                IsShopOpen = !IsShopOpen;
                if (IsShopOpen && Plugin.Session != null && Plugin.Session.IsConnected && Plugin.Session.ActiveConfig.AutoHints)
                {
                    Plugin.Session.ScoutShopLocationsAsync();
                }
            }

            if (_shopkeeperTransform != null)
            {
                float bob = Mathf.Sin(Time.unscaledTime * 2.2f) * 0.035f;
                _shopkeeperTransform.localPosition = new Vector3(0f, -0.05f + bob, 0f);
            }

            UpdateRealCoalSpawning();
            CheckNaturalPickupBonuses();

            // World-space click detection in the Pillared Gallery for the centered Shop Stall (+0.50, -0.55)
            if (SceneManager.GetActiveScene().name == "gallery" && Input.GetMouseButtonDown(0) && Camera.main != null)
            {
                // Ignore if mouse is over the outer UI frame borders
                Vector3 mp = Input.mousePosition;
                float sx = Screen.width / 1920f;
                float sy = Screen.height / 1080f;
                if (mp.x < 88f * sx || mp.x > (1920f - 88f) * sx || mp.y < 82f * sy || mp.y > (1080f - 75f) * sy)
                {
                    return;
                }

                Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mp);
                Vector2 pt = new Vector2(mouseWorld.x, mouseWorld.y);

                // Shop Stall bounds around (0.50, -0.55)
                if (Mathf.Abs(pt.x - StallWorldPos.x) <= 1.60f && Mathf.Abs(pt.y - StallWorldPos.y) <= 1.80f)
                {
                    IsShopOpen = true;
                    if (Plugin.Session != null && Plugin.Session.IsConnected && Plugin.Session.ActiveConfig.AutoHints)
                    {
                        Plugin.Session.ScoutShopLocationsAsync();
                    }
                }
            }
        }

        public static bool IsCustomRelicActive(int index)
        {
            if (index < 0 || index >= CustomRelics.Length || !IsCustomRelicUnlocked(index))
            {
                return false;
            }
            string key = CustomRelics[index].Key;
            if (key == "AP_BUFF_ESPRESSO")
            {
                return Plugin.Delivery != null && Plugin.Delivery.IsEspressoBuffActive;
            }
            if (key == "AP_CUSTOM_LANTERN" || key == "AP_CUSTOM_CHISEL" || key == "AP_CUSTOM_MOSS_SACK")
            {
                return FileBasedPrefs.GetInt(key, 0) > 0;
            }
            return false;
        }

        public static void TriggerOrToggleCustomRelic(int index)
        {
            if (index < 0 || index >= CustomRelics.Length)
            {
                return;
            }
            var relic = CustomRelics[index];
            if (!IsCustomRelicUnlocked(index))
            {
                ShowRelicToast(relic.Name, "Locked — receive from the Multiworld or unlock at the Cavern Peddler!", relic.AssetPath, 3.0f);
                return;
            }

            float rem = GetRelicCooldownRemainingSeconds(index);
            if (rem > 0.05f)
            {
                ShowRelicToast(relic.Name, $"On cooldown ({rem:0.0}s remaining)", relic.AssetPath, 2.0f);
                return;
            }

            // Start World of Warcraft radial clock-sweep cooldown
            _relicCooldownStart[index] = Time.unscaledTime;
            _relicCooldownUntil[index] = Time.unscaledTime + relic.CooldownSeconds;

            switch (relic.Key)
            {
                case "AP_CUSTOM_LANTERN":
                {
                    int next = FileBasedPrefs.GetInt(relic.Key, 0) > 0 ? 0 : 1;
                    FileBasedPrefs.SetInt(relic.Key, next);
                    FileBasedPrefs.ManualySave();
                    string state = next > 0 ? "Lit warm lantern aura around the Shade" : "Extinguished lantern aura";
                    LastShopFeedback = $"{relic.Name}: {state}";
                    ShowRelicToast(relic.Name, state, relic.AssetPath);
                    break;
                }
                case "AP_CUSTOM_CHISEL":
                {
                    int next = FileBasedPrefs.GetInt(relic.Key, 0) > 0 ? 0 : 1;
                    FileBasedPrefs.SetInt(relic.Key, next);
                    FileBasedPrefs.ManualySave();
                    string state = next > 0 ? "Enabled 2x wall mining speed & +1 bonus floor Coal" : "Disabled Chisel bonus";
                    LastShopFeedback = $"{relic.Name}: {state}";
                    ShowRelicToast(relic.Name, state, relic.AssetPath);
                    break;
                }
                case "AP_CUSTOM_MOSS_SACK":
                {
                    FileBasedPrefs.SetInt(relic.Key, 1);
                    CurrentMoss += 3;
                    FileBasedPrefs.ManualySave();
                    LastShopFeedback = $"{relic.Name}: Gathered +3 fresh Cavern Moss (Total: {CurrentMoss} Moss)";
                    ShowRelicToast(relic.Name, LastShopFeedback, relic.AssetPath);
                    break;
                }
                case "AP_ECHOING_CONCH":
                {
                    FileBasedPrefs.SetInt(relic.Key, FileBasedPrefs.GetInt(relic.Key, 0) + 1);
                    Plugin.Session?.ScoutRandomUncheckedLocation("Echoing Conch");
                    LastShopFeedback = "You hold the Echoing Conch to your ear... a multiworld hint echoes in the dark!";
                    ShowRelicToast(relic.Name, LastShopFeedback, relic.AssetPath);
                    break;
                }
                case "AP_MUSHROOM_FERTILIZER":
                {
                    FileBasedPrefs.SetInt(relic.Key, FileBasedPrefs.GetInt(relic.Key, 0) + 1);
                    CosmeticAndCustomItemPatches.ApplyMushroomFertilizer();
                    LastShopFeedback = "Sprinkled Mushroom Fertilizer! Active sprouts matured & +30m growth boost!";
                    ShowRelicToast(relic.Name, LastShopFeedback, relic.AssetPath);
                    break;
                }
                case "TIME_SKIP_43200":
                {
                    FileBasedPrefs.SetInt(relic.Key, FileBasedPrefs.GetInt(relic.Key, 0) + 1);
                    TMan.AddInstaTime(43200L);
                    TMan.SaveValues();
                    FileBasedPrefs.ManualySave();
                    LastShopFeedback = "Pressed the King's Snooze Button! Skipped 12 hours closer to awakening!";
                    ShowRelicToast(relic.Name, LastShopFeedback, relic.AssetPath);
                    break;
                }
                case "AP_BUFF_ESPRESSO":
                {
                    Plugin.Delivery?.TriggerEspressoBuff(180);
                    LastShopFeedback = "Gulped a boiling Double Espresso! Walk speed boosted for 3 minutes!";
                    ShowRelicToast(relic.Name, LastShopFeedback, relic.AssetPath);
                    break;
                }
            }
            Plugin.Delivery?.PushNotification(LastShopFeedback);
        }

        public static void TryBuyShopLocation(int slotNumber)
        {
            if (slotNumber < 1 || slotNumber > 8)
            {
                return;
            }
            string uid = $"shop:peddler_{slotNumber}";
            if (!WorldRegistryData.LocationsByUid.TryGetValue(uid, out var locEntry))
            {
                return;
            }

            var session = Plugin.Session;
            bool connected = session != null && session.IsConnected;

            if (connected && session.IsLocationChecked(locEntry.Id))
            {
                LastShopFeedback = $"Slot #{slotNumber} is already sold out!";
                return;
            }
            if (!connected && FileBasedPrefs.GetInt("AP_LOCAL_SHOP_" + slotNumber, 0) > 0)
            {
                LastShopFeedback = $"Slot #{slotNumber} is already sold out!";
                return;
            }

            if (slotNumber >= 5 && CurrentMattocks < 1 && (!connected || session.ActiveConfig.ShuffleMattocksAndKeys))
            {
                LastShopFeedback = $"Deep Relic #{slotNumber} is encased in stone — requires at least 1 Mattock to excavate!";
                return;
            }

            int cost = ShopSlotCosts[slotNumber - 1];
            if (CurrentCoal < cost)
            {
                LastShopFeedback = $"Need {cost} Coal for Bargain Relic #{slotNumber} (you have {CurrentCoal} Coal). Pick up fallen coal near the seam!";
                return;
            }

            CurrentCoal -= cost;
            if (connected)
            {
                session.ReportLocation(locEntry.Id, locEntry.Name);
                string scouted = session.GetScoutedShopOffer(locEntry.Id);
                LastShopFeedback = !string.IsNullOrEmpty(scouted)
                    ? $"Purchased {scouted} for {cost} Coal!"
                    : $"Purchased {locEntry.Name} for {cost} Coal!";
            }
            else
            {
                FileBasedPrefs.SetInt("AP_LOCAL_SHOP_" + slotNumber, 1);
                if (slotNumber <= CustomRelics.Length)
                {
                    UnlockCustomRelic(slotNumber - 1);
                    LastShopFeedback = $"Unlocked {CustomRelics[slotNumber - 1].Name} for {cost} Coal!";
                }
                else
                {
                    FileBasedPrefs.SetInt("AP_HAT_CROWN", 1);
                    LastShopFeedback = $"Unlocked King's Tiny Crown for {cost} Coal!";
                }
            }
            FileBasedPrefs.ManualySave();
            ShowRelicToast("Cavern Peddler Purchase", LastShopFeedback, "shop/shopkeeper_idle.png", 3.5f);
        }

        public static void TryBuyDirectRelicUnlock(int index)
        {
            if (index < 0 || index >= CustomRelics.Length)
            {
                return;
            }
            var r = CustomRelics[index];
            if (FileBasedPrefs.GetInt("AP_UNLOCK_" + r.Key, 0) > 0)
            {
                LastShopFeedback = $"{r.Name} is already unlocked in your outer inventory frame!";
                return;
            }
            if (CurrentCoal < r.ShopUnlockCoalCost)
            {
                LastShopFeedback = $"Need {r.ShopUnlockCoalCost} Coal to unlock {r.Name} (you have {CurrentCoal} Coal).";
                return;
            }
            CurrentCoal -= r.ShopUnlockCoalCost;
            UnlockCustomRelic(index);
            LastShopFeedback = $"Unlocked {r.Name} for {r.ShopUnlockCoalCost} Coal! Click its icon in the bottom outer frame to use it.";
            ShowRelicToast(r.Name, LastShopFeedback, r.AssetPath, 4.0f);
        }

        public static void TryBuyCosmeticUnlock(string key, string displayName, string assetPath, int coalCost)
        {
            if (CosmeticAndCustomItemPatches.IsCosmeticUnlocked(key))
            {
                LastShopFeedback = $"{displayName} is already unlocked! Click it in the outer side frame to equip/unequip.";
                return;
            }
            if (CurrentCoal < coalCost)
            {
                LastShopFeedback = $"Need {coalCost} Coal to unlock {displayName} (you have {CurrentCoal} Coal).";
                return;
            }
            CurrentCoal -= coalCost;
            FileBasedPrefs.SetInt(key, 1);
            FileBasedPrefs.SetInt("AP_UNLOCK_" + key, 1);
            FileBasedPrefs.ManualySave();
            CosmeticAndCustomItemPatches.AutoEquipIfNone(key);
            LastShopFeedback = $"Unlocked {displayName} for {coalCost} Coal! Click its slot on the outer side frame to toggle.";
            ShowRelicToast(displayName, LastShopFeedback, assetPath, 4.0f);
        }

        public static void TradeMossForCoal()
        {
            if (CurrentMoss < 3)
            {
                LastShopFeedback = $"Need at least 3 Cavern Moss to trade (you have {CurrentMoss}).";
                return;
            }
            CurrentMoss -= 3;
            CurrentCoal += 2;
            FileBasedPrefs.ManualySave();
            LastShopFeedback = $"Traded 3 Cavern Moss for +2 Coal! (Total: {CurrentCoal} Coal)";
            ShowRelicToast("Moss-for-Coal Trade", LastShopFeedback, "items/item_moss_sack.png", 3.0f);
        }

        public static void BuyConchHintWithCoal()
        {
            const int cost = 3;
            if (CurrentCoal < cost)
            {
                LastShopFeedback = $"Need {cost} Coal for an Echoing Conch Hint (you have {CurrentCoal} Coal).";
                return;
            }
            CurrentCoal -= cost;
            FileBasedPrefs.ManualySave();
            Plugin.Session?.ScoutRandomUncheckedLocation("Peddler's Whisper");
            LastShopFeedback = "The Peddler holds a hollow shell to your ear... check Recent Activity for the hint!";
            ShowRelicToast("Echoing Conch", LastShopFeedback, "items/item_conch.png");
        }

        public static void BuyHourglassSkipWithCoal()
        {
            const int cost = 2;
            if (CurrentCoal < cost)
            {
                LastShopFeedback = $"Need {cost} Coal for a 1-Hour Time Skip (you have {CurrentCoal} Coal).";
                return;
            }
            CurrentCoal -= cost;
            TMan.AddInstaTime(3600L);
            TMan.SaveValues();
            FileBasedPrefs.ManualySave();
            LastShopFeedback = "The Peddler tips his brass hourglass: skipped 1 hour closer to the King's awakening!";
            ShowRelicToast("King's Snooze Button", LastShopFeedback, "items/item_snooze_button.png");
        }

        public static void BuyEspressoShotWithCoal()
        {
            const int cost = 2;
            if (CurrentCoal < cost)
            {
                LastShopFeedback = $"Need {cost} Coal for a Cavern Espresso Shot (you have {CurrentCoal} Coal).";
                return;
            }
            CurrentCoal -= cost;
            FileBasedPrefs.ManualySave();
            Plugin.Delivery?.TriggerEspressoBuff(180);
            LastShopFeedback = "Gulped a boiling Cavern Espresso! Walk speed boosted for 3 minutes!";
            ShowRelicToast("Double Espresso", LastShopFeedback, "items/item_espresso.png");
        }

        private static void DrawFallbackStall(Texture2D tex)
        {
            Color woodDark = new Color(0.26f, 0.17f, 0.10f, 0.96f);
            Color woodMid = new Color(0.42f, 0.28f, 0.16f, 0.98f);
            Color brass = new Color(0.84f, 0.67f, 0.28f, 1f);
            Color canopyA = new Color(0.48f, 0.16f, 0.16f, 0.96f);
            Color canopyB = new Color(0.75f, 0.58f, 0.24f, 0.96f);

            CustomAssetLoader.FillRect(tex, 24, 10, 272, 110, woodDark);
            CustomAssetLoader.FillRect(tex, 16, 112, 288, 18, brass);
            CustomAssetLoader.FillRect(tex, 34, 22, 252, 86, woodMid);
            CustomAssetLoader.FillRect(tex, 28, 128, 18, 160, woodDark);
            CustomAssetLoader.FillRect(tex, 274, 128, 18, 160, woodDark);

            for (int i = 0; i < 8; i++)
            {
                CustomAssetLoader.FillRect(tex, 12 + i * 37, 284, 37, 56, (i % 2 == 0) ? canopyA : canopyB);
            }
            CustomAssetLoader.FillRect(tex, 10, 278, 300, 8, brass);
        }

        private static void DrawFallbackShopkeeper(Texture2D tex)
        {
            Color cloak = new Color(0.14f, 0.11f, 0.18f, 0.98f);
            Color hood = new Color(0.24f, 0.18f, 0.28f, 1f);
            Color eye = new Color(0.98f, 0.84f, 0.25f, 1f);

            CustomAssetLoader.FillEllipse(tex, 80, 75, 52, 68, cloak);
            CustomAssetLoader.FillEllipse(tex, 80, 142, 36, 36, hood);
            CustomAssetLoader.FillEllipse(tex, 68, 144, 7, 5, eye);
            CustomAssetLoader.FillEllipse(tex, 92, 144, 7, 5, eye);
        }

        private static void DrawFallbackCoalVein(Texture2D tex)
        {
            Color rock = new Color(0.18f, 0.16f, 0.17f, 0.98f);
            Color coal = new Color(0.06f, 0.06f, 0.08f, 1f);
            Color glint = new Color(0.88f, 0.72f, 0.28f, 0.95f);

            CustomAssetLoader.FillEllipse(tex, 70, 56, 58, 46, rock);
            CustomAssetLoader.FillEllipse(tex, 52, 62, 22, 18, coal);
            CustomAssetLoader.FillEllipse(tex, 86, 54, 24, 20, coal);
            CustomAssetLoader.FillEllipse(tex, 68, 82, 18, 15, coal);
            CustomAssetLoader.FillEllipse(tex, 48, 68, 4, 4, glint);
            CustomAssetLoader.FillEllipse(tex, 90, 60, 5, 4, glint);
        }
    }
}
