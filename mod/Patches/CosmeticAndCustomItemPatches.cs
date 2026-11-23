using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TheLongingDecrypted.UI;
using UnityEngine;

namespace TheLongingDecrypted.Patches
{
    /// <summary>
    /// Attaches unlockable cosmetic hats to the Shade's head (shade_head SpriteRenderer PMDLBNBGMJC),
    /// spawns companion pets following the Shade, applies Shade color tints and Shade's Lantern light,
    /// and executes custom item upgrades and comedic traps.
    /// </summary>
    internal static class CosmeticAndCustomItemPatches
    {
        public const string ActiveHatPrefKey = "AP_EQUIPPED_HAT";
        public const string ActivePetPrefKey = "AP_EQUIPPED_PET";
        public const string ActiveTintPrefKey = "AP_EQUIPPED_TINT";

        public sealed class CosmeticOption
        {
            public string Key { get; }
            public string DisplayName { get; }
            public string AssetPath { get; }
            public Color AccentColor { get; }

            public CosmeticOption(string key, string displayName, string assetPath, Color accentColor)
            {
                Key = key;
                DisplayName = displayName;
                AssetPath = assetPath;
                AccentColor = accentColor;
            }
        }

        public static readonly CosmeticOption[] HatOptions =
        {
            new CosmeticOption("NONE", "No Hat", null, Color.clear),
            new CosmeticOption("AP_HAT_CROWN", "King's Tiny Crown", "hats/hat_crown.png", new Color(0.96f, 0.78f, 0.22f, 1f)),
            new CosmeticOption("AP_HAT_HELMET", "Miner's Candle Helmet", "hats/hat_helmet.png", new Color(0.78f, 0.56f, 0.24f, 1f)),
            new CosmeticOption("AP_HAT_MUSHROOM", "Glowing Mushroom Cap", "hats/hat_mushroom.png", new Color(0.32f, 0.88f, 0.54f, 1f)),
            new CosmeticOption("AP_HAT_MIFFY", "Miffy Beanie Hat", "hats/hat_miffy.png", new Color(0.91f, 0.72f, 0.52f, 1f)),
            new CosmeticOption("AP_HAT_TOPHAT", "Gentleman's Top Hat", "hats/hat_tophat.png", new Color(0.18f, 0.16f, 0.22f, 1f)),
            new CosmeticOption("AP_HAT_SPIDER", "Friendly Spider Perch", "hats/hat_spider.png", new Color(0.55f, 0.22f, 0.28f, 1f)),
            new CosmeticOption("AP_HAT_FLOWER", "Spider Flower Wreath", "hats/hat_flower.png", new Color(0.92f, 0.42f, 0.58f, 1f)),
            new CosmeticOption("AP_HAT_NIGHTCAP", "400-Day Nightcap", "hats/hat_nightcap.png", new Color(0.42f, 0.56f, 0.88f, 1f)),
        };

        public static readonly CosmeticOption[] PetOptions =
        {
            new CosmeticOption("NONE", "No Companion", null, Color.clear),
            new CosmeticOption("AP_PET_ROCK", "Pet Pebble Companion", "pets/pet_rock.png", new Color(0.58f, 0.54f, 0.50f, 1f)),
            new CosmeticOption("AP_PET_SPIDER", "Baby Cavern Spider", "pets/pet_spider.png", new Color(0.28f, 0.22f, 0.30f, 1f)),
        };

        public static readonly (string Name, Color Tint)[] ShadeTints =
        {
            ("Classic Ochre", Color.white),
            ("Amber Lantern", new Color(1.0f, 0.86f, 0.55f, 1f)),
            ("Emerald Moss", new Color(0.68f, 0.96f, 0.72f, 1f)),
            ("Rose Quartz", new Color(0.98f, 0.72f, 0.82f, 1f)),
            ("Deep Amethyst", new Color(0.80f, 0.68f, 0.98f, 1f)),
            ("Spectral Cyan", new Color(0.65f, 0.92f, 0.98f, 1f)),
        };

        private static readonly FieldInfo HeadRendererField = AccessTools.Field(typeof(Movement), "PMDLBNBGMJC");
        private static readonly FieldInfo RootTransformField = AccessTools.Field(typeof(Movement), "BHIEEIGMMHI");
        private static readonly FieldInfo AnimatorField = AccessTools.Field(typeof(Movement), "GJKNAKHCFGI");
        private static readonly FieldInfo HackPointsField = AccessTools.Field(typeof(InteractableOutcome_HackIntoWall), "HackPoints");

        private static SpriteRenderer _hatRenderer;
        private static GameObject _petObject;
        private static SpriteRenderer _petRenderer;
        private static Light _lanternLight;
        private static float _nextHiccupTime;

        public static string EquippedHatKey
        {
            get
            {
                string val = FileBasedPrefs.GetString(ActiveHatPrefKey, "NONE");
                return val == "AP_HAT_GLASSES" ? "AP_HAT_MIFFY" : val;
            }
            set
            {
                FileBasedPrefs.SetString(ActiveHatPrefKey, value ?? "NONE");
                FileBasedPrefs.ManualySave();
                RefreshHatSprite();
            }
        }

        public static string EquippedPetKey
        {
            get => FileBasedPrefs.GetString(ActivePetPrefKey, "NONE");
            set
            {
                FileBasedPrefs.SetString(ActivePetPrefKey, value ?? "NONE");
                FileBasedPrefs.ManualySave();
                RefreshPetSprite();
            }
        }

        public static int EquippedTintIndex
        {
            get => Mathf.Clamp(FileBasedPrefs.GetInt(ActiveTintPrefKey, 0), 0, ShadeTints.Length - 1);
            set
            {
                FileBasedPrefs.SetInt(ActiveTintPrefKey, Mathf.Clamp(value, 0, ShadeTints.Length - 1));
                FileBasedPrefs.ManualySave();
            }
        }

        public static bool IsCosmeticUnlocked(string key)
        {
            if (string.IsNullOrEmpty(key) || key == "NONE")
            {
                return true;
            }
            if (key == "AP_HAT_MIFFY")
            {
                return FileBasedPrefs.GetInt("AP_HAT_MIFFY", 0) > 0 || FileBasedPrefs.GetInt("AP_HAT_GLASSES", 0) > 0;
            }
            return FileBasedPrefs.GetInt(key, 0) > 0;
        }

        public static void AutoEquipIfNone(string gameKey)
        {
            if (string.IsNullOrEmpty(gameKey))
            {
                return;
            }
            if (gameKey.StartsWith("AP_HAT_") && EquippedHatKey == "NONE")
            {
                EquippedHatKey = gameKey;
            }
            else if (gameKey.StartsWith("AP_PET_") && EquippedPetKey == "NONE")
            {
                EquippedPetKey = gameKey;
            }
        }

        public static void CycleHat(bool allowAllPreview = false)
        {
            string current = EquippedHatKey;
            int idx = 0;
            for (int i = 0; i < HatOptions.Length; i++)
            {
                if (HatOptions[i].Key == current)
                {
                    idx = i;
                    break;
                }
            }
            for (int step = 1; step <= HatOptions.Length; step++)
            {
                var candidate = HatOptions[(idx + step) % HatOptions.Length];
                if (allowAllPreview || IsCosmeticUnlocked(candidate.Key))
                {
                    EquippedHatKey = candidate.Key;
                    return;
                }
            }
        }

        public static void CyclePet(bool allowAllPreview = false)
        {
            string current = EquippedPetKey;
            int idx = 0;
            for (int i = 0; i < PetOptions.Length; i++)
            {
                if (PetOptions[i].Key == current)
                {
                    idx = i;
                    break;
                }
            }
            for (int step = 1; step <= PetOptions.Length; step++)
            {
                var candidate = PetOptions[(idx + step) % PetOptions.Length];
                if (allowAllPreview || IsCosmeticUnlocked(candidate.Key))
                {
                    EquippedPetKey = candidate.Key;
                    return;
                }
            }
        }

        public static void CycleTint()
        {
            EquippedTintIndex = (EquippedTintIndex + 1) % ShadeTints.Length;
        }

        public static void ApplyMushroomFertilizer()
        {
            try
            {
                // Accelerate any planted mushrooms in the current scene and advance growth timer
                var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                int accelerated = 0;
                foreach (var mb in behaviours)
                {
                    if (mb != null && mb.GetType().Name.IndexOf("Mushroom", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mb.SendMessage("GrowComplete", SendMessageOptions.DontRequireReceiver);
                        accelerated++;
                    }
                }
                TMan.AddInstaTime(1800L);
                TMan.SaveValues();
                Plugin.Delivery?.PushNotification($"Mushroom Fertilizer applied! (+30m growth boost, {accelerated} active sprouts matured)");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Mushroom Fertilizer effect warning: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(InteractableOutcome_HackIntoWall), "Event_HackInto")]
        [HarmonyPrefix]
        private static void BoostMiningWithChisel(InteractableOutcome_HackIntoWall __instance)
        {
            if (__instance == null || FileBasedPrefs.GetInt("AP_CUSTOM_CHISEL", 0) <= 0 || HackPointsField == null)
            {
                return;
            }
            try
            {
                object raw = HackPointsField.GetValue(__instance);
                if (raw is int hp && hp > 2)
                {
                    HackPointsField.SetValue(__instance, Math.Max(1, hp / 2));
                }
                else if (raw is float fhp && fhp > 2f)
                {
                    HackPointsField.SetValue(__instance, Mathf.Max(1f, fhp * 0.5f));
                }
            }
            catch
            {
                // Ignore if field type differs on specific subclasses
            }
        }

        private static Movement _cachedMovement;

        public static void UpdateActiveShade()
        {
            if (_cachedMovement == null || !_cachedMovement.gameObject.activeInHierarchy)
            {
                _cachedMovement = UnityEngine.Object.FindObjectOfType<Movement>();
            }
            if (_cachedMovement != null)
            {
                UpdateShadeCosmeticsAndTraps(_cachedMovement);
            }
        }

        private static void UpdateShadeCosmeticsAndTraps(Movement __instance)
        {
            if (__instance == null)
            {
                return;
            }

            var headRenderer = HeadRendererField?.GetValue(__instance) as SpriteRenderer;
            var rootTransform = (RootTransformField?.GetValue(__instance) as Transform) ?? __instance.transform;
            if (headRenderer == null || rootTransform == null)
            {
                return;
            }

            // 1. Ensure Hat child SpriteRenderer exists under shade_head and stays visible/rotates mid-turn
            EnsureHatAttached(headRenderer);
            bool isTurning = __instance.IsTurning();
            if (_hatRenderer != null)
            {
                bool showHat = (headRenderer.enabled || isTurning) && headRenderer.gameObject.activeInHierarchy && EquippedHatKey != "NONE";
                _hatRenderer.enabled = showHat;

                if (isTurning)
                {
                    var anim = AnimatorField?.GetValue(__instance) as Animator;
                    float normTime = anim != null ? Mathf.Clamp01(anim.GetCurrentAnimatorStateInfo(0).normalizedTime) : 0f;
                    bool scaleNotYetFlipped = Mathf.Sign(rootTransform.localScale.x) != Mathf.Sign(__instance.GetPlayerFacing());
                    float flipSign = (scaleNotYetFlipped && normTime >= 0.5f) ? -1f : 1f;
                    float turnWidth = Mathf.Lerp(0.35f, 1.0f, Mathf.Abs(normTime - 0.5f) * 2f);
                    _hatRenderer.transform.localScale = new Vector3(flipSign * turnWidth, 1f, 1f);
                    _hatRenderer.transform.localPosition = new Vector3(0.02f * flipSign, 0.205f, 0f);
                }
                else
                {
                    _hatRenderer.transform.localScale = Vector3.one;
                    _hatRenderer.transform.localPosition = new Vector3(0.02f, 0.205f, 0f);
                }
            }

            // 2. Apply optional Shade color tint
            Color tint = ShadeTints[EquippedTintIndex].Tint;
            headRenderer.color = tint;
            var bodyRenderer = headRenderer.transform.parent != null
                ? headRenderer.transform.parent.GetComponent<SpriteRenderer>()
                : null;
            if (bodyRenderer != null)
            {
                bodyRenderer.color = tint;
            }

            // 3. Shade's Lantern warm glow
            bool hasLantern = FileBasedPrefs.GetInt("AP_CUSTOM_LANTERN", 0) > 0;
            if (hasLantern)
            {
                if (_lanternLight == null)
                {
                    var lightGo = new GameObject("ShadeCustomLantern");
                    lightGo.transform.SetParent(rootTransform, false);
                    lightGo.transform.localPosition = new Vector3(0f, 0.85f, -0.6f);
                    _lanternLight = lightGo.AddComponent<Light>();
                    _lanternLight.type = LightType.Point;
                    _lanternLight.color = new Color(1.0f, 0.82f, 0.42f, 1f);
                    _lanternLight.range = 5.5f;
                    _lanternLight.intensity = 1.65f;
                }
                _lanternLight.enabled = true;
            }
            else if (_lanternLight != null)
            {
                _lanternLight.enabled = false;
            }

            // 4. Companion Pet following behind the Shade at exact Shade feet level
            UpdateCompanionPet(rootTransform, bodyRenderer, __instance.IsInWalkState());

            // 5. Tiny Shade Trap (shrinks Shade to 55% scale while active)
            if (Plugin.Delivery != null)
            {
                float targetAbsScale = Plugin.Delivery.IsTinyShadeActive ? 0.55f : 1.0f;
                Vector3 scale = rootTransform.localScale;
                float signX = Mathf.Sign(scale.x == 0f ? 1f : scale.x);
                if (Mathf.Abs(Mathf.Abs(scale.x) - targetAbsScale) > 0.01f || Mathf.Abs(scale.y - targetAbsScale) > 0.01f)
                {
                    rootTransform.localScale = new Vector3(signX * targetAbsScale, targetAbsScale, scale.z);
                }

                // 6. Hiccup Trap (gentle vertical hop every ~1.8s)
                if (Plugin.Delivery.IsHiccupTrapActive && bodyRenderer != null)
                {
                    if (Time.unscaledTime >= _nextHiccupTime)
                    {
                        _nextHiccupTime = Time.unscaledTime + 1.8f;
                    }
                    float dt = _nextHiccupTime - Time.unscaledTime;
                    float hop = (dt > 1.55f) ? Mathf.Sin((1.8f - dt) / 0.25f * Mathf.PI) * 0.14f : 0f;
                    bodyRenderer.transform.localPosition = new Vector3(0f, 0.71f + hop, 0f);
                }
            }
        }

        private static void EnsureHatAttached(SpriteRenderer headRenderer)
        {
            if (_hatRenderer == null || _hatRenderer.transform.parent != headRenderer.transform)
            {
                var existing = headRenderer.transform.Find("ShadeCustomHat");
                if (existing != null)
                {
                    _hatRenderer = existing.GetComponent<SpriteRenderer>();
                }
                else
                {
                    var hatGo = new GameObject("ShadeCustomHat");
                    hatGo.transform.SetParent(headRenderer.transform, false);
                    // Matches yellow 96x80 alignment box on shade_head (84x73 px @ 100 ppu)
                    hatGo.transform.localPosition = new Vector3(0.02f, 0.205f, 0f);
                    hatGo.transform.localScale = Vector3.one;
                    _hatRenderer = hatGo.AddComponent<SpriteRenderer>();
                    _hatRenderer.sortingLayerID = headRenderer.sortingLayerID;
                    _hatRenderer.sortingOrder = headRenderer.sortingOrder + 2;
                }
                RefreshHatSprite();
            }
        }

        public static void RefreshHatSprite()
        {
            if (_hatRenderer == null)
            {
                return;
            }
            string key = EquippedHatKey;
            CosmeticOption opt = null;
            foreach (var h in HatOptions)
            {
                if (h.Key == key)
                {
                    opt = h;
                    break;
                }
            }
            if (opt == null || string.IsNullOrEmpty(opt.AssetPath))
            {
                _hatRenderer.sprite = null;
                _hatRenderer.enabled = false;
                return;
            }

            _hatRenderer.sprite = CustomAssetLoader.LoadSprite(
                opt.AssetPath,
                96,
                80,
                new Vector2(0.5f, 0.2f),
                100f,
                tex => DrawFallbackHat(tex, opt.Key, opt.AccentColor)
            );
            _hatRenderer.enabled = true;
        }

        private static void UpdateCompanionPet(Transform shadeRoot, SpriteRenderer bodyRenderer, bool isWalking)
        {
            string petKey = EquippedPetKey;
            if (string.IsNullOrEmpty(petKey) || petKey == "NONE")
            {
                if (_petObject != null)
                {
                    _petObject.SetActive(false);
                }
                return;
            }

            float groundY = (bodyRenderer != null && bodyRenderer.sprite != null)
                ? bodyRenderer.bounds.min.y
                : shadeRoot.position.y;

            if (_petObject == null)
            {
                _petObject = new GameObject("ShadeCompanionPet");
                _petRenderer = _petObject.AddComponent<SpriteRenderer>();
                _petRenderer.sortingOrder = 5;
                _petObject.transform.position = new Vector3(shadeRoot.position.x + 1.1f, groundY, shadeRoot.position.z);
                RefreshPetSprite();
            }

            _petObject.SetActive(true);
            // When Shade localScale.x > 0, Shade faces left, so behind is +X; when < 0, Shade faces right, so behind is -X
            float behindDir = Mathf.Sign(shadeRoot.localScale.x == 0f ? 1f : shadeRoot.localScale.x);
            float bounce = isWalking ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5.2f)) * 0.04f : 0f;
            float targetX = shadeRoot.position.x + behindDir * 1.12f;
            float nextX = Mathf.Lerp(_petObject.transform.position.x, targetX, Time.unscaledDeltaTime * 4.5f);
            _petObject.transform.position = new Vector3(nextX, groundY + bounce, shadeRoot.position.z);
            _petObject.transform.localScale = new Vector3(behindDir, 1f, 1f);
        }

        public static void RefreshPetSprite()
        {
            if (_petRenderer == null)
            {
                return;
            }
            string key = EquippedPetKey;
            CosmeticOption opt = null;
            foreach (var p in PetOptions)
            {
                if (p.Key == key)
                {
                    opt = p;
                    break;
                }
            }
            if (opt == null || string.IsNullOrEmpty(opt.AssetPath))
            {
                _petRenderer.sprite = null;
                return;
            }

            // Pivot (0.5f, 0.0f) anchors bottom-most pixel row (y=63) directly to Shade's feet groundY
            _petRenderer.sprite = CustomAssetLoader.LoadSprite(
                opt.AssetPath,
                64,
                64,
                new Vector2(0.5f, 0.0f),
                100f,
                tex => DrawFallbackPet(tex, opt.Key, opt.AccentColor)
            );
        }

        private static void DrawFallbackHat(Texture2D tex, string key, Color col)
        {
            Color outline = new Color(0.08f, 0.06f, 0.05f, 1f);
            switch (key)
            {
                case "AP_HAT_CROWN":
                    CustomAssetLoader.FillRect(tex, 24, 16, 48, 16, outline);
                    CustomAssetLoader.FillRect(tex, 26, 18, 44, 12, col);
                    CustomAssetLoader.FillRect(tex, 26, 30, 10, 14, col);
                    CustomAssetLoader.FillRect(tex, 43, 30, 10, 18, col);
                    CustomAssetLoader.FillRect(tex, 60, 30, 10, 14, col);
                    break;
                case "AP_HAT_TOPHAT":
                    CustomAssetLoader.FillRect(tex, 16, 14, 64, 10, col);
                    CustomAssetLoader.FillRect(tex, 28, 24, 40, 38, col);
                    CustomAssetLoader.FillRect(tex, 28, 24, 40, 6, new Color(0.78f, 0.22f, 0.22f, 1f));
                    break;
                case "AP_HAT_MUSHROOM":
                    CustomAssetLoader.FillEllipse(tex, 48, 28, 32, 18, col);
                    CustomAssetLoader.FillEllipse(tex, 36, 32, 6, 5, new Color(0.92f, 0.98f, 0.85f, 1f));
                    CustomAssetLoader.FillEllipse(tex, 58, 26, 7, 5, new Color(0.92f, 0.98f, 0.85f, 1f));
                    break;
                default:
                    CustomAssetLoader.FillEllipse(tex, 48, 26, 28, 16, outline);
                    CustomAssetLoader.FillEllipse(tex, 48, 26, 25, 13, col);
                    break;
            }
        }

        private static void DrawFallbackPet(Texture2D tex, string key, Color col)
        {
            CustomAssetLoader.FillEllipse(tex, 32, 24, 18, 14, col);
            CustomAssetLoader.FillEllipse(tex, 25, 27, 3, 3, new Color(0.98f, 0.88f, 0.30f, 1f));
            CustomAssetLoader.FillEllipse(tex, 37, 27, 3, 3, new Color(0.98f, 0.88f, 0.30f, 1f));
        }
    }
}
