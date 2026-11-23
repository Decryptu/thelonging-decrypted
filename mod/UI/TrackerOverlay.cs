using System;
using System.IO;
using TheLongingDecrypted.Generated;
using TheLongingDecrypted.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLongingDecrypted.UI
{
    /// <summary>
    /// Renders custom multiworld items, hats, and pets directly inside the empty carved squares
    /// of the game's outer HUD frame (HUD.png, 1920x1080) with World of Warcraft 360-degree radial
    /// clock-sweep cooldown overlays, uses the game's hand-drawn dialogue card assets
    /// (shade_dialoge_notext_01.png & dialogbutton_0001.png) for text cards and shop interface,
    /// and provides the [F8] Archipelago connection panel.
    /// </summary>
    public sealed class TrackerOverlay : MonoBehaviour
    {
        private bool _visible;
        private bool _wardrobeOpen;
        private Rect _windowRect = new Rect(96f, 76f, 380f, 520f);
        private Rect _shopWindowRect = new Rect(520f, 110f, 680f, 540f);
        private Rect _wardrobeWindowRect = new Rect(490f, 76f, 380f, 470f);
        private Vector2 _shopScroll;
        private int _shopTab; // 0 = Multiworld Checks, 1 = Unlock Relics & Cosmetics
        private string _hostInput = "archipelago.gg";
        private string _portInput = "38281";
        private string _slotInput = "Shade";
        private string _passwordInput = "";
        private float _nextMilestonePoll;

        // 1920x1080 coordinates of the carved empty squares on the game's outer HUD frame (HUD.png)
        private static readonly float[] BottomSquareCentersX = { 792.5f, 854.5f, 916.0f, 978.0f, 1040.0f, 1101.5f, 1164.0f };
        private const float BottomSquareCenterY = 1042.5f;
        private static readonly float[] SideSquareCentersY = { 99.0f, 160.5f, 222.0f, 284.0f, 346.0f, 407.0f, 469.0f, 531.0f };
        private const float LeftSquareCenterX = 44.0f;
        private const float RightSquareCenterX = 1876.5f;

        // Cached 64-step World of Warcraft radial clock-sweep cooldown textures (48x48)
        private static readonly Texture2D[] RadialCooldownFrames = new Texture2D[65];
        private static Font _gameFont;
        private static Texture2D _nativeDialogCardTex;
        private static Texture2D _nativeDialogButtonTex;
        private static GUIStyle _cardTitleStyle;
        private static GUIStyle _cardBodyStyle;
        private static GUIStyle _cooldownTextStyle;
        private static GUIStyle _cardButtonStyle;

        private void Awake()
        {
            _hostInput = Plugin.DefaultHostConfig.Value;
            _portInput = Plugin.DefaultPortConfig.Value.ToString();
            _slotInput = Plugin.DefaultSlotConfig.Value;
            _passwordInput = Plugin.DefaultPasswordConfig.Value;
        }

        private void Update()
        {
            Plugin.Session?.PumpMainThread();
            CavernShopPatches.UpdateShopScene();
            CosmeticAndCustomItemPatches.UpdateActiveShade();

            if (Input.GetKeyDown(KeyCode.F8))
            {
                _visible = !_visible;
            }

            if (Time.unscaledTime >= _nextMilestonePoll)
            {
                _nextMilestonePoll = Time.unscaledTime + 3f;
                LocationCheckPatches.PollMilestonePrefs();
            }
        }

        private static Texture2D GetNativeDialogCardTexture()
        {
            if (_nativeDialogCardTex != null)
            {
                return _nativeDialogCardTex;
            }
            _nativeDialogCardTex = CustomAssetLoader.LoadTextureOrNull("ui/shade_dialoge_notext_01.png");
            if (_nativeDialogCardTex != null)
            {
                return _nativeDialogCardTex;
            }
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            if (textures != null)
            {
                foreach (var t in textures)
                {
                    if (t != null && string.Equals(t.name, "shade_dialoge_notext_01", StringComparison.OrdinalIgnoreCase))
                    {
                        _nativeDialogCardTex = t;
                        return _nativeDialogCardTex;
                    }
                }
            }
            return null;
        }

        private static Texture2D GetNativeDialogButtonTexture()
        {
            if (_nativeDialogButtonTex != null)
            {
                return _nativeDialogButtonTex;
            }
            _nativeDialogButtonTex = CustomAssetLoader.LoadTextureOrNull("ui/dialogbutton_0001.png");
            if (_nativeDialogButtonTex != null)
            {
                return _nativeDialogButtonTex;
            }
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            if (textures != null)
            {
                foreach (var t in textures)
                {
                    if (t != null && string.Equals(t.name, "dialogbutton_0001", StringComparison.OrdinalIgnoreCase))
                    {
                        _nativeDialogButtonTex = t;
                        return _nativeDialogButtonTex;
                    }
                }
            }
            return null;
        }

        private static Font FindGameFont()
        {
            if (_gameFont != null)
            {
                return _gameFont;
            }
            var fonts = Resources.FindObjectsOfTypeAll<Font>();
            if (fonts != null)
            {
                foreach (var f in fonts)
                {
                    if (f != null && f.name != null && f.name.IndexOf("QUINTESSENCE_180708", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _gameFont = f;
                        return _gameFont;
                    }
                }
                foreach (var f in fonts)
                {
                    if (f != null && f.name != null && f.name.IndexOf("QUINTESSENCE", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _gameFont = f;
                        return _gameFont;
                    }
                }
            }
            return null;
        }

        private static void EnsureStyles()
        {
            var font = FindGameFont();
            if (font != null && GUI.skin != null && GUI.skin.font != font)
            {
                GUI.skin.font = font;
            }

            if (_cardTitleStyle != null)
            {
                if (font != null && _cardTitleStyle.font != font)
                {
                    _cardTitleStyle.font = font;
                    _cardBodyStyle.font = font;
                    _cooldownTextStyle.font = font;
                    _cardButtonStyle.font = font;
                }
                return;
            }

            _cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 18,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
            };
            _cardTitleStyle.normal.textColor = new Color(0.98f, 0.89f, 0.52f, 1f);

            _cardBodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 15,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
            };
            _cardBodyStyle.normal.textColor = new Color(0.93f, 0.88f, 0.78f, 1f);

            _cooldownTextStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
            };
            _cooldownTextStyle.normal.textColor = new Color(1f, 0.94f, 0.62f, 1f);

            _cardButtonStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 15,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
            };
            _cardButtonStyle.normal.textColor = new Color(0.96f, 0.90f, 0.74f, 1f);
        }

        /// <summary>
        /// Returns a 48x48 Texture2D rendering the World of Warcraft 360-degree clockwise
        /// radial clock-sweep cooldown shade for the given remaining ratio (1.0 = full dark square,
        /// sweeping clockwise from 12 o'clock down to 0.0 = clear).
        /// </summary>
        private static Texture2D GetRadialCooldownTexture(float remainingRatio)
        {
            int step = Mathf.Clamp(Mathf.RoundToInt(remainingRatio * 64f), 1, 64);
            if (RadialCooldownFrames[step] != null)
            {
                return RadialCooldownFrames[step];
            }

            const int size = 48;
            float elapsedRatio = 1f - (step / 64f);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            Color darkShade = new Color(0.03f, 0.02f, 0.02f, 0.78f);
            Color clockHand = new Color(0.98f, 0.88f, 0.42f, 0.95f);
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center; // +dy is 12 o'clock (top) in Unity Texture2D
                    // Clockwise angle [0..1] starting at 12 o'clock (+Y)
                    float angle01 = Mathf.Repeat(Mathf.Atan2(dx, dy) / (2f * Mathf.PI), 1f);

                    if (angle01 < elapsedRatio)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (step < 64 && Mathf.Abs(angle01 - elapsedRatio) < 0.022f)
                    {
                        tex.SetPixel(x, y, clockHand);
                    }
                    else
                    {
                        tex.SetPixel(x, y, darkShade);
                    }
                }
            }

            tex.Apply();
            RadialCooldownFrames[step] = tex;
            return tex;
        }

        private static void DrawInnerHighlightFrame(Rect r, Color col, float thickness = 2f)
        {
            var prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static bool DrawNativeDialogButton(Rect rect, string text, bool enabled = true)
        {
            EnsureStyles();
            var btnTex = GetNativeDialogButtonTexture();
            var prev = GUI.color;
            bool hovered = enabled && rect.Contains(Event.current.mousePosition);
            GUI.color = !enabled
                ? new Color(0.55f, 0.50f, 0.45f, 0.65f)
                : (hovered ? new Color(1.0f, 0.95f, 0.75f, 1f) : Color.white);

            if (btnTex != null)
            {
                GUI.DrawTexture(rect, btnTex, ScaleMode.StretchToFill);
            }
            else
            {
                GUI.Box(rect, string.Empty);
            }

            _cardButtonStyle.normal.textColor = !enabled
                ? new Color(0.62f, 0.58f, 0.52f, 0.85f)
                : (hovered ? new Color(1f, 0.92f, 0.48f, 1f) : new Color(0.95f, 0.89f, 0.74f, 1f));
            GUI.Label(rect, text, _cardButtonStyle);
            GUI.color = prev;

            if (enabled && Event.current.type == EventType.MouseDown && Event.current.button == 0 && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawActiveTrapOverlays();
            DrawOuterFrameSlotsAndDialogueCards();

            if (_visible)
            {
                GUI.backgroundColor = new Color(0.14f, 0.12f, 0.10f, 0.96f);
                GUI.contentColor = new Color(0.95f, 0.91f, 0.78f, 1f);
                _windowRect = GUI.Window(400400, _windowRect, DrawMainWindow, "The Longing Decrypted [F8]");
            }

            if (CavernShopPatches.IsShopOpen)
            {
                DrawNativeShopkeeperInterface();
            }

            if (_wardrobeOpen)
            {
                GUI.backgroundColor = new Color(0.15f, 0.13f, 0.16f, 0.98f);
                GUI.contentColor = new Color(0.96f, 0.91f, 0.80f, 1f);
                _wardrobeWindowRect = GUI.Window(400402, _wardrobeWindowRect, DrawWardrobeWindow, "Shade's Wardrobe & Fanfic Library");
            }
        }

        private void DrawActiveTrapOverlays()
        {
            var delivery = Plugin.Delivery;
            if (delivery == null)
            {
                return;
            }

            if (delivery.IsDarknessTrapActive)
            {
                var prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.72f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            var dialogCard = GetNativeDialogCardTexture();

            if (delivery.IsExistentialTrapActive && !string.IsNullOrEmpty(delivery.ActiveExistentialThought))
            {
                Rect banner = new Rect((Screen.width - 560f) * 0.5f, Screen.height - 195f, 560f, 118f);
                if (dialogCard != null)
                {
                    GUI.DrawTexture(banner, dialogCard, ScaleMode.StretchToFill);
                }
                else
                {
                    GUI.Box(banner, string.Empty);
                }
                GUI.Label(new Rect(banner.x + 125f, banner.y + 18f, banner.width - 145f, 24f), "A Sudden Existential Thought...", _cardTitleStyle);
                GUI.Label(new Rect(banner.x + 125f, banner.y + 44f, banner.width - 145f, 60f), delivery.ActiveExistentialThought, _cardBodyStyle);
            }

            if (delivery.IsFalseWakeActive)
            {
                float shakeX = Mathf.Sin(Time.unscaledTime * 38f) * 6f;
                Rect alarmRect = new Rect((Screen.width - 520f) * 0.5f + shakeX, 74f, 520f, 108f);
                if (dialogCard != null)
                {
                    GUI.DrawTexture(alarmRect, dialogCard, ScaleMode.StretchToFill);
                }
                else
                {
                    GUI.Box(alarmRect, string.Empty);
                }
                GUI.Label(new Rect(alarmRect.x + 118f, alarmRect.y + 16f, alarmRect.width - 135f, 24f), "THE KING IS AWAKENING?!", _cardTitleStyle);
                GUI.Label(new Rect(alarmRect.x + 118f, alarmRect.y + 42f, alarmRect.width - 135f, 52f), "...Wait, no, he just rolled over and snored. (False Alarm Trap!)", _cardBodyStyle);
            }

            if (delivery.IsSpiderScareActive)
            {
                float progress = 1f - Mathf.Clamp01((delivery.SpiderScareUntil - Time.unscaledTime) / 8f);
                float dropY = Mathf.Sin(progress * Mathf.PI) * (Screen.height * 0.58f);
                float centerX = Screen.width * 0.5f;

                var prev = GUI.color;
                GUI.color = new Color(0.92f, 0.92f, 0.96f, 0.85f);
                GUI.DrawTexture(new Rect(centerX - 1f, 0f, 3f, dropY), Texture2D.whiteTexture);

                var spiderTex = CustomAssetLoader.LoadTextureOrNull("pets/pet_spider.png");
                if (spiderTex != null)
                {
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(centerX - 48f, dropY - 24f, 96f, 96f), spiderTex, ScaleMode.ScaleToFit);
                }
                GUI.color = prev;
            }
        }

        /// <summary>
        /// Draws:
        /// 1) Top native dialogue card toast (using shade_dialoge_notext_01.png)
        /// 2) 7 Custom Multiworld Items directly inside the 7 centered bottom outer-frame squares of HUD.png (no title, with WoW 360° radial clock-sweep cooldown)
        /// 3) 8 Cosmetic Hats directly inside the left outer-frame squares of HUD.png (click to equip/unequip)
        /// 4) 2 Companion Pets directly inside the right outer-frame squares of HUD.png (click to equip/unequip)
        /// </summary>
        private void DrawOuterFrameSlotsAndDialogueCards()
        {
            int sceneIdx = SceneManager.GetActiveScene().buildIndex;
            if (sceneIdx < 3)
            {
                return;
            }

            float sx = Screen.width / 1920f;
            float sy = Screen.height / 1080f;

            // 1. Native Dialogue Card Toast (using shade_dialoge_notext_01.png)
            if (Time.unscaledTime < CavernShopPatches.ActiveToastUntil && !string.IsNullOrEmpty(CavernShopPatches.ActiveToastTitle))
            {
                var cardTex = GetNativeDialogCardTexture();
                float cardW = Mathf.Max(500f, 560f * sx);
                float cardH = Mathf.Max(118f, 148f * sy);
                Rect cardRect = new Rect((Screen.width - cardW) * 0.5f, 70f * sy, cardW, cardH);
                if (cardTex != null)
                {
                    GUI.DrawTexture(cardRect, cardTex, ScaleMode.StretchToFill);
                }
                else
                {
                    GUI.Box(cardRect, string.Empty);
                }

                var iconTex = !string.IsNullOrEmpty(CavernShopPatches.ActiveToastAsset)
                    ? CustomAssetLoader.LoadTextureOrNull(CavernShopPatches.ActiveToastAsset)
                    : null;
                if (iconTex != null)
                {
                    GUI.DrawTexture(new Rect(cardRect.x + cardW * 0.21f, cardRect.y + (cardH - 48f) * 0.5f, 48f, 48f), iconTex, ScaleMode.ScaleToFit);
                }

                float textLeft = cardRect.x + (iconTex != null ? cardW * 0.21f + 58f : cardW * 0.22f);
                float textWidth = cardRect.xMax - textLeft - 28f;
                GUI.Label(new Rect(textLeft, cardRect.y + 14f, textWidth, 28f), CavernShopPatches.ActiveToastTitle, _cardTitleStyle);
                GUI.Label(new Rect(textLeft, cardRect.y + 42f, textWidth, cardH - 54f), CavernShopPatches.ActiveToastDesc ?? string.Empty, _cardBodyStyle);
            }

            if (!CavernShopPatches.ShowRelicHudBar)
            {
                return;
            }

            // 2. Bottom Outer-Frame Empty Squares -> 7 Custom Multiworld Items (icon only, no Unity box, no title, WoW radial cooldown)
            var relics = CavernShopPatches.CustomRelics;
            for (int i = 0; i < relics.Length && i < BottomSquareCentersX.Length; i++)
            {
                if (!CavernShopPatches.IsCustomRelicUnlocked(i))
                {
                    continue;
                }

                var r = relics[i];
                float cx = BottomSquareCentersX[i];
                float cy = BottomSquareCenterY;
                Rect slotRect = new Rect((cx - 22f) * sx, (cy - 22f) * sy, 44f * sx, 44f * sy);
                Rect iconRect = new Rect((cx - 19f) * sx, (cy - 19f) * sy, 38f * sx, 38f * sy);

                var icon = CustomAssetLoader.LoadTextureOrNull(r.AssetPath);
                if (icon != null)
                {
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
                }

                // Active toggle highlight (for Lantern / Chisel / Moss Sack / active Espresso buff)
                if (CavernShopPatches.IsCustomRelicActive(i))
                {
                    DrawInnerHighlightFrame(slotRect, new Color(0.95f, 0.82f, 0.32f, 0.85f), Mathf.Max(1f, 2f * sx));
                }

                // World of Warcraft 360-degree clockwise radial clock-sweep cooldown shade
                float remRatio = CavernShopPatches.GetRelicCooldownRemainingRatio(i);
                if (remRatio > 0.001f)
                {
                    var radialTex = GetRadialCooldownTexture(remRatio);
                    if (radialTex != null)
                    {
                        GUI.DrawTexture(slotRect, radialTex, ScaleMode.StretchToFill);
                    }
                    float remSec = CavernShopPatches.GetRelicCooldownRemainingSeconds(i);
                    string secText = remSec >= 10f ? $"{Mathf.CeilToInt(remSec)}" : $"{remSec:0.0}";
                    GUI.Label(slotRect, secText, _cooldownTextStyle);
                }

                // Click detection inside the carved outer-frame square
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && slotRect.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    CavernShopPatches.TriggerOrToggleCustomRelic(i);
                }
            }

            // 3. Left Outer-Frame Empty Squares -> 8 Cosmetic Hats (click to equip or unequip)
            var hats = CosmeticAndCustomItemPatches.HatOptions;
            string equippedHat = CosmeticAndCustomItemPatches.EquippedHatKey;
            int hatSlotIdx = 0;
            for (int i = 1; i < hats.Length && hatSlotIdx < SideSquareCentersY.Length; i++)
            {
                var hat = hats[i];
                bool unlocked = CavernShopPatches.ShowcaseUnlockAllInOuterUi || CosmeticAndCustomItemPatches.IsCosmeticUnlocked(hat.Key);
                int slotIdx = hatSlotIdx++;
                if (!unlocked)
                {
                    continue;
                }

                float cy = SideSquareCentersY[slotIdx];
                Rect slotRect = new Rect((LeftSquareCenterX - 26f) * sx, (cy - 21f) * sy, 52f * sx, 42f * sy);
                Rect iconRect = new Rect((LeftSquareCenterX - 22f) * sx, (cy - 18f) * sy, 44f * sx, 36f * sy);

                var tex = CustomAssetLoader.LoadTextureOrNull(hat.AssetPath);
                if (tex != null)
                {
                    GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
                }

                bool isEquipped = equippedHat == hat.Key;
                if (isEquipped)
                {
                    DrawInnerHighlightFrame(slotRect, new Color(0.98f, 0.85f, 0.35f, 0.90f), Mathf.Max(1f, 2f * sx));
                }

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && slotRect.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    CosmeticAndCustomItemPatches.EquippedHatKey = isEquipped ? "NONE" : hat.Key;
                    string msg = isEquipped ? $"Unequipped {hat.DisplayName}" : $"Equipped {hat.DisplayName}";
                    CavernShopPatches.ShowRelicToast(hat.DisplayName, msg, hat.AssetPath, 2.5f);
                }
            }

            // 4. Right Outer-Frame Empty Squares -> 2 Companion Pets (click to equip or unequip)
            var pets = CosmeticAndCustomItemPatches.PetOptions;
            string equippedPet = CosmeticAndCustomItemPatches.EquippedPetKey;
            int petSlotIdx = 0;
            for (int i = 1; i < pets.Length && petSlotIdx < SideSquareCentersY.Length; i++)
            {
                var pet = pets[i];
                bool unlocked = CavernShopPatches.ShowcaseUnlockAllInOuterUi || CosmeticAndCustomItemPatches.IsCosmeticUnlocked(pet.Key);
                int slotIdx = petSlotIdx++;
                if (!unlocked)
                {
                    continue;
                }

                float cy = SideSquareCentersY[slotIdx];
                Rect slotRect = new Rect((RightSquareCenterX - 26f) * sx, (cy - 21f) * sy, 52f * sx, 42f * sy);
                Rect iconRect = new Rect((RightSquareCenterX - 20f) * sx, (cy - 18f) * sy, 40f * sx, 36f * sy);

                var tex = CustomAssetLoader.LoadTextureOrNull(pet.AssetPath);
                if (tex != null)
                {
                    GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
                }

                bool isEquipped = equippedPet == pet.Key;
                if (isEquipped)
                {
                    DrawInnerHighlightFrame(slotRect, new Color(0.98f, 0.85f, 0.35f, 0.90f), Mathf.Max(1f, 2f * sx));
                }

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && slotRect.Contains(Event.current.mousePosition))
                {
                    Event.current.Use();
                    CosmeticAndCustomItemPatches.EquippedPetKey = isEquipped ? "NONE" : pet.Key;
                    string msg = isEquipped ? $"Sent {pet.DisplayName} to rest" : $"{pet.DisplayName} is now following you";
                    CavernShopPatches.ShowRelicToast(pet.DisplayName, msg, pet.AssetPath, 2.5f);
                }
            }
        }

        /// <summary>
        /// Renders the Cavern Peddler shop interface using the game's hand-drawn dialogue card
        /// (shade_dialoge_notext_01.png) and button strips (dialogbutton_0001.png).
        /// </summary>
        private void DrawNativeShopkeeperInterface()
        {
            var cardTex = GetNativeDialogCardTexture();
            float w = 720f;
            float h = 520f;
            Rect panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f - 20f, w, h);

            if (cardTex != null)
            {
                GUI.DrawTexture(panel, cardTex, ScaleMode.StretchToFill);
            }
            else
            {
                GUI.Box(panel, string.Empty);
            }

            // Cover the baked-in Shade portrait on the stretched dialogue card before drawing the Shopkeeper
            var prevCol = GUI.color;
            GUI.color = new Color(0.365f, 0.286f, 0.243f, 1f);
            GUI.DrawTexture(new Rect(panel.x + 12f, panel.y + 14f, 128f, 220f), Texture2D.whiteTexture);
            GUI.color = prevCol;

            var keeperTex = CustomAssetLoader.LoadTextureOrNull("shop/shopkeeper_idle.png");
            if (keeperTex != null)
            {
                GUI.DrawTexture(new Rect(panel.x + 18f, panel.y + 18f, 96f, 120f), keeperTex, ScaleMode.ScaleToFit);
            }

            // Header & Pouch balance
            GUI.Label(
                new Rect(panel.x + 124f, panel.y + 18f, panel.width - 170f, 28f),
                $"The Cavern Peddler - Coal: {CavernShopPatches.CurrentCoal} | Moss: {CavernShopPatches.CurrentMoss} | Mattocks: {CavernShopPatches.CurrentMattocks}",
                _cardTitleStyle
            );
            GUI.Label(
                new Rect(panel.x + 124f, panel.y + 46f, panel.width - 170f, 40f),
                CavernShopPatches.LastShopFeedback,
                _cardBodyStyle
            );

            // Top tab buttons using native dialogbutton_0001.png
            if (DrawNativeDialogButton(new Rect(panel.x + 124f, panel.y + 88f, 180f, 28f), _shopTab == 0 ? "* Multiworld Checks *" : "Multiworld Checks"))
            {
                _shopTab = 0;
            }
            if (DrawNativeDialogButton(new Rect(panel.x + 312f, panel.y + 88f, 195f, 28f), _shopTab == 1 ? "* Relics, Hats & Pets *" : "Relics, Hats & Pets"))
            {
                _shopTab = 1;
            }
            if (DrawNativeDialogButton(new Rect(panel.x + 515f, panel.y + 88f, 175f, 28f), "Trade 3 Moss -> 2 Coal", CavernShopPatches.CurrentMoss >= 3))
            {
                CavernShopPatches.TradeMossForCoal();
            }

            // Scrollable offers area using native dialogbutton_0001.png rows
            Rect scrollOuter = new Rect(panel.x + 28f, panel.y + 132f, panel.width - 56f, 305f);
            var session = Plugin.Session;
            bool connected = session != null && session.IsConnected;

            if (_shopTab == 0)
            {
                Rect contentRect = new Rect(0f, 0f, scrollOuter.width - 24f, 8 * 38f);
                _shopScroll = GUI.BeginScrollView(scrollOuter, _shopScroll, contentRect);

                for (int slot = 1; slot <= 8; slot++)
                {
                    string uid = $"shop:peddler_{slot}";
                    if (!WorldRegistryData.LocationsByUid.TryGetValue(uid, out var locEntry))
                    {
                        continue;
                    }

                    int cost = CavernShopPatches.ShopSlotCosts[slot - 1];
                    bool isChecked = (connected && session.IsLocationChecked(locEntry.Id))
                                     || (!connected && FileBasedPrefs.GetInt("AP_LOCAL_SHOP_" + slot, 0) > 0);
                    bool needsMattock = slot >= 5 && CavernShopPatches.CurrentMattocks < 1 && (!connected || session.ActiveConfig.ShuffleMattocksAndKeys);
                    string scouted = connected ? session.GetScoutedShopOffer(locEntry.Id) : null;

                    string offerTitle = !string.IsNullOrEmpty(scouted)
                        ? $"#{slot}: {scouted}"
                        : $"#{slot}: {locEntry.Name}";
                    string statusText = isChecked
                        ? "[SOLD OUT]"
                        : (needsMattock ? $"[{cost} Coal - Needs Mattock]" : $"[Buy: {cost} Coal]");

                    float rowY = (slot - 1) * 37f;
                    bool canBuy = !isChecked && !needsMattock && CavernShopPatches.CurrentCoal >= cost;
                    if (DrawNativeDialogButton(new Rect(4f, rowY, contentRect.width - 8f, 33f), $"{offerTitle}   {statusText}", canBuy))
                    {
                        CavernShopPatches.TryBuyShopLocation(slot);
                    }
                }

                GUI.EndScrollView();
            }
            else
            {
                int totalRows = CavernShopPatches.CustomRelics.Length
                                + (CosmeticAndCustomItemPatches.HatOptions.Length - 1)
                                + (CosmeticAndCustomItemPatches.PetOptions.Length - 1);
                Rect contentRect = new Rect(0f, 0f, scrollOuter.width - 24f, totalRows * 38f);
                _shopScroll = GUI.BeginScrollView(scrollOuter, _shopScroll, contentRect);

                int rowIdx = 0;
                for (int i = 0; i < CavernShopPatches.CustomRelics.Length; i++)
                {
                    var r = CavernShopPatches.CustomRelics[i];
                    bool unlocked = FileBasedPrefs.GetInt("AP_UNLOCK_" + r.Key, 0) > 0;
                    float rowY = rowIdx++ * 37f;

                    var icon = CustomAssetLoader.LoadTextureOrNull(r.AssetPath);
                    if (icon != null)
                    {
                        GUI.DrawTexture(new Rect(8f, rowY + 2f, 28f, 28f), icon, ScaleMode.ScaleToFit);
                    }
                    string label = unlocked
                        ? $"Relic: {r.Name} — [UNLOCKED in bottom frame]"
                        : $"Unlock Relic: {r.Name} ({r.Description}) — [{r.ShopUnlockCoalCost} Coal]";
                    bool canBuy = !unlocked && CavernShopPatches.CurrentCoal >= r.ShopUnlockCoalCost;
                    if (DrawNativeDialogButton(new Rect(42f, rowY, contentRect.width - 48f, 33f), label, canBuy))
                    {
                        CavernShopPatches.TryBuyDirectRelicUnlock(i);
                    }
                }

                for (int i = 1; i < CosmeticAndCustomItemPatches.HatOptions.Length; i++)
                {
                    var hOpt = CosmeticAndCustomItemPatches.HatOptions[i];
                    bool unlocked = CosmeticAndCustomItemPatches.IsCosmeticUnlocked(hOpt.Key);
                    float rowY = rowIdx++ * 37f;

                    var icon = CustomAssetLoader.LoadTextureOrNull(hOpt.AssetPath);
                    if (icon != null)
                    {
                        GUI.DrawTexture(new Rect(8f, rowY + 2f, 28f, 28f), icon, ScaleMode.ScaleToFit);
                    }
                    string label = unlocked
                        ? $"Hat: {hOpt.DisplayName} — [UNLOCKED in left frame]"
                        : $"Unlock Hat: {hOpt.DisplayName} — [2 Coal]";
                    if (DrawNativeDialogButton(new Rect(42f, rowY, contentRect.width - 48f, 33f), label, !unlocked && CavernShopPatches.CurrentCoal >= 2))
                    {
                        CavernShopPatches.TryBuyCosmeticUnlock(hOpt.Key, hOpt.DisplayName, hOpt.AssetPath, 2);
                    }
                }

                for (int i = 1; i < CosmeticAndCustomItemPatches.PetOptions.Length; i++)
                {
                    var pOpt = CosmeticAndCustomItemPatches.PetOptions[i];
                    bool unlocked = CosmeticAndCustomItemPatches.IsCosmeticUnlocked(pOpt.Key);
                    float rowY = rowIdx++ * 37f;

                    var icon = CustomAssetLoader.LoadTextureOrNull(pOpt.AssetPath);
                    if (icon != null)
                    {
                        GUI.DrawTexture(new Rect(8f, rowY + 2f, 28f, 28f), icon, ScaleMode.ScaleToFit);
                    }
                    string label = unlocked
                        ? $"Companion: {pOpt.DisplayName} — [UNLOCKED in right frame]"
                        : $"Unlock Companion: {pOpt.DisplayName} — [3 Coal]";
                    if (DrawNativeDialogButton(new Rect(42f, rowY, contentRect.width - 48f, 33f), label, !unlocked && CavernShopPatches.CurrentCoal >= 3))
                    {
                        CavernShopPatches.TryBuyCosmeticUnlock(pOpt.Key, pOpt.DisplayName, pOpt.AssetPath, 3);
                    }
                }

                GUI.EndScrollView();
            }

            // Bottom repeatable specials & close X button (positioned over the X corner of shade_dialoge_notext_01.png)
            if (DrawNativeDialogButton(new Rect(panel.x + 28f, panel.yMax - 64f, 195f, 32f), "Conch Hint (3 Coal)", CavernShopPatches.CurrentCoal >= 3))
            {
                CavernShopPatches.BuyConchHintWithCoal();
            }
            if (DrawNativeDialogButton(new Rect(panel.x + 232f, panel.yMax - 64f, 195f, 32f), "+1h Skip (2 Coal)", CavernShopPatches.CurrentCoal >= 2))
            {
                CavernShopPatches.BuyHourglassSkipWithCoal();
            }
            if (DrawNativeDialogButton(new Rect(panel.x + 436f, panel.yMax - 64f, 195f, 32f), "Espresso Rush (2 Coal)", CavernShopPatches.CurrentCoal >= 2))
            {
                CavernShopPatches.BuyEspressoShotWithCoal();
            }

            // Native bottom-right [X] corner area on shade_dialoge_notext_01.png
            Rect closeCorner = new Rect(panel.xMax - 54f, panel.yMax - 54f, 52f, 52f);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && closeCorner.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                CavernShopPatches.IsShopOpen = false;
            }
        }

        private void DrawMainWindow(int id)
        {
            var session = Plugin.Session;
            bool connected = session != null && session.IsConnected;

            GUILayout.BeginVertical();

            GUI.color = connected ? new Color(0.45f, 0.75f, 1f) : new Color(0.95f, 0.80f, 0.40f);
            GUILayout.Label(session != null ? session.StatusMessage : "Initializing...");
            GUI.color = Color.white;

            if (!connected)
            {
                GUILayout.Space(4f);
                DrawLabeledField("Host:", ref _hostInput);
                DrawLabeledField("Port:", ref _portInput);
                DrawLabeledField("Slot:", ref _slotInput);
                DrawLabeledField("Pass:", ref _passwordInput);

                GUILayout.Space(4f);
                if (GUILayout.Button("Connect to Archipelago"))
                {
                    if (int.TryParse(_portInput.Trim(), out int port))
                    {
                        Plugin.DefaultHostConfig.Value = _hostInput.Trim();
                        Plugin.DefaultPortConfig.Value = port;
                        Plugin.DefaultSlotConfig.Value = _slotInput.Trim();
                        Plugin.DefaultPasswordConfig.Value = _passwordInput;
                        session?.Connect(_hostInput.Trim(), port, _slotInput.Trim(), _passwordInput);
                    }
                }
            }
            else
            {
                var cfg = session.ActiveConfig;
                GUILayout.Label($"Goal: {cfg.Goal} | Checks: {session.CheckedCount} / {session.TotalLocations}");
                if (cfg.Goal == "crystal_book_hunt")
                {
                    GUILayout.Label($"Hunt: Crystals {Plugin.Delivery.ReceivedCrystals}/{cfg.RequiredCrystals} | Books {Plugin.Delivery.ReceivedBooks}/{cfg.RequiredBooks}");
                }

                long elapsed = Math.Max(0L, TMan.GetOverallTime());
                long remaining = Math.Max(0L, 34_560_000L - elapsed);
                var ts = TimeSpan.FromSeconds(remaining);
                GUILayout.Label($"Clock: {(int)ts.TotalDays}d {ts.Hours:00}h {ts.Minutes:00}m {ts.Seconds:00}s ({TimeAndMovementPatches.GetEffectiveTimeRate():0}x)");
                GUILayout.Label($"Mobility: {TimeAndMovementPatches.UnlockedWalkMultiplier:0.##}x Walk | Recall Ring {WaypointRecallPatches.RecallTier}/3");
            }

            GUILayout.Label($"Coal: {CavernShopPatches.CurrentCoal} (Floor Coals: {CavernShopPatches.CountActiveFloorCoals()}/{CavernShopPatches.MaxFloorCoals}) | Moss: {CavernShopPatches.CurrentMoss} | Mattocks: {CavernShopPatches.CurrentMattocks}");

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(CavernShopPatches.IsShopOpen ? "Hide Peddler [F7]" : "Cavern Peddler [F7]"))
            {
                CavernShopPatches.IsShopOpen = !CavernShopPatches.IsShopOpen;
                if (CavernShopPatches.IsShopOpen && connected && session.ActiveConfig.AutoHints)
                {
                    session.ScoutShopLocationsAsync();
                }
            }
            if (GUILayout.Button(_wardrobeOpen ? "Hide Wardrobe" : "Wardrobe & AO3 Books"))
            {
                _wardrobeOpen = !_wardrobeOpen;
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Teleport to Shopkeeper Gallery (level24)"))
            {
                CavernShopPatches.TeleportToShopGallery();
            }

            CavernShopPatches.ShowcaseUnlockAllInOuterUi = GUILayout.Toggle(
                CavernShopPatches.ShowcaseUnlockAllInOuterUi,
                "Preview All Items/Hats/Pets in Outer Frame Squares"
            );

            if (connected)
            {
                if (GUILayout.Button("Disconnect"))
                {
                    session.Disconnect();
                }
            }

            GUILayout.Space(4f);
            GUILayout.Label("Recent Activity:");
            if (Plugin.Delivery != null)
            {
                foreach (var note in Plugin.Delivery.RecentNotifications)
                {
                    GUILayout.Label(note);
                }
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawWardrobeWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Click the left/right outer-frame squares to equip Hats & Pets directly!");
            CavernShopPatches.ShowcaseUnlockAllInOuterUi = GUILayout.Toggle(
                CavernShopPatches.ShowcaseUnlockAllInOuterUi,
                "Show All Hats, Pets & Relics in Outer UI Squares"
            );
            CavernShopPatches.ShowRelicHudBar = GUILayout.Toggle(
                CavernShopPatches.ShowRelicHudBar,
                "Enable Outer UI Square Icons (Bottom/Left/Right)"
            );

            GUILayout.Space(6f);
            int tintIdx = CosmeticAndCustomItemPatches.EquippedTintIndex;
            GUILayout.Label($"Shade Color Tint: {CosmeticAndCustomItemPatches.ShadeTints[tintIdx].Name}");
            if (GUILayout.Button("Cycle Shade Color Tint"))
            {
                CosmeticAndCustomItemPatches.CycleTint();
            }

            GUILayout.Space(8f);
            bool ao3Active = FanficBookPatches.IsAo3ModeActive;
            GUI.color = ao3Active ? new Color(1f, 0.68f, 0.82f) : Color.white;
            GUILayout.Label($"AO3 Fanfiction Book Mode: {(ao3Active ? "ENABLED (Soft-NSFW Cavern Romances)" : "Disabled (Classic Books)")}");
            GUI.color = Color.white;
            if (GUILayout.Button(ao3Active ? "Restore Classic Literature Books" : "Enable AO3 Fanfiction Books"))
            {
                FanficBookPatches.ToggleAo3Mode();
            }

            GUILayout.Space(6f);
            if (GUILayout.Button("Reload Custom PNG/TXT Assets from Disk"))
            {
                CustomAssetLoader.ClearCache();
                CosmeticAndCustomItemPatches.RefreshHatSprite();
                CosmeticAndCustomItemPatches.RefreshPetSprite();
                FanficBookPatches.RefreshAllActiveShelfBooks();
                Plugin.Delivery?.PushNotification("Reloaded CustomAssets from disk!");
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("Close Wardrobe"))
            {
                _wardrobeOpen = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private static void DrawLabeledField(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(44f));
            value = GUILayout.TextField(value ?? string.Empty);
            GUILayout.EndHorizontal();
        }
    }
}
