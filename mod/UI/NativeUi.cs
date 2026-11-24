using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheLongingDecrypted.UI
{
    /// <summary>
    /// Builds interface elements out of the sprites, fonts, and colors already loaded by the game.
    /// Nothing is shipped with the mod: every frame, plank, button, and glyph is looked up by name at runtime.
    /// </summary>
    internal static class NativeUi
    {
        // Ink and parchment tones sampled from the vanilla dialogue cards and the carved HUD frame.
        public static readonly Color Ink = Color.black;
        public static readonly Color Accent = new Color32(0xBC, 0x8A, 0x75, 0xFF);
        public static readonly Color Muted = new Color32(0x74, 0x5C, 0x4F, 0xFF);
        public static readonly Color Parchment = new Color32(0x9B, 0x78, 0x68, 0xFF);
        public static readonly Color DarkSquare = new Color32(0x1E, 0x14, 0x13, 0xFF);

        private static readonly Dictionary<string, Sprite> GameSprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> Derived = new Dictionary<string, Sprite>();
        private static TMP_FontAsset _font;
        private static Material _fontMaterial;
        private static float _nextScan;

        public static bool Ready => Font != null && GameSprite("shade_dialoge_notext_01") != null;

        /// <summary>Finds a sprite the game has loaded. Returns null while the owning scene is not in memory.</summary>
        public static Sprite GameSprite(string name)
        {
            if (GameSprites.TryGetValue(name, out var cached) && cached != null)
            {
                return cached;
            }
            if (Time.unscaledTime < _nextScan)
            {
                return null;
            }

            bool found = false;
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (sprite == null)
                {
                    continue;
                }
                if (!GameSprites.TryGetValue(sprite.name, out var known) || known == null)
                {
                    GameSprites[sprite.name] = sprite;
                }
                found |= sprite.name == name;
            }
            if (!found)
            {
                _nextScan = Time.unscaledTime + 2f;
            }
            return GameSprites.TryGetValue(name, out cached) ? cached : null;
        }

        /// <summary>Allows an immediate sprite lookup after a scene change.</summary>
        public static void OnSceneChanged()
        {
            _nextScan = 0f;
        }

        /// <summary>The ink drawing inside the left square of a vanilla interaction plank (mattock, book, door...).</summary>
        public static Sprite InkGlyph(string plank)
        {
            return Crop("glyph:" + plank, plank, new Rect(19f, 16f, 40f, 40f), Vector4.zero);
        }

        /// <summary>A carved interaction square with nothing drawn on it.</summary>
        public static Sprite IconFrame()
        {
            return BlankSquare("iconframe", new Rect(0f, 0f, 78f, 71f));
        }

        /// <summary>The blank parchment inside a carved interaction square.</summary>
        public static Sprite ParchmentSquare()
        {
            return BlankSquare("parchment", new Rect(12f, 9f, 54f, 54f));
        }

        private static Sprite BlankSquare(string key, Rect rect)
        {
            if (Derived.TryGetValue(key, out var cached) && cached != null && cached.texture != null)
            {
                return cached;
            }
            var source = GameSprite("icons_0020");
            if (source == null)
            {
                return null;
            }

            try
            {
                var texture = ReadSprite(source);
                // Wipe the drawing with parchment from the clean strip along its left edge.
                for (int y = 18; y < 54; y++)
                {
                    for (int x = 22; x < 57; x++)
                    {
                        texture.SetPixel(x, y, texture.GetPixel(20 + x % 2, y));
                    }
                }
                texture.Apply();
                return Keep(key, Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Could not prepare a blank square: {ex.Message}");
                return null;
            }
        }

        public static Sprite Plank(int state)
        {
            return GameSprite("menue_selection_000" + Mathf.Clamp(state, 1, 3));
        }

        public static Sprite DarkPlank()
        {
            return Crop("darkplank", "belowicon", new Rect(0f, 0f, 512f, 114f), new Vector4(22f, 22f, 22f, 22f));
        }

        private static Sprite Crop(string key, string source, Rect rect, Vector4 border)
        {
            if (Derived.TryGetValue(key, out var cached) && cached != null && cached.texture != null)
            {
                return cached;
            }
            var sprite = GameSprite(source);
            if (sprite == null)
            {
                return null;
            }

            try
            {
                var texture = ReadSprite(sprite);
                rect.width = Mathf.Min(rect.width, texture.width - rect.x);
                rect.height = Mathf.Min(rect.height, texture.height - rect.y);
                return Keep(key, Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Could not derive {key} from {source}: {ex.Message}");
                return null;
            }
        }

        private static Sprite Keep(string key, Sprite sprite)
        {
            sprite.name = "native_" + key;
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            sprite.texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Derived[key] = sprite;
            return sprite;
        }

        /// <summary>The vanilla dialogue card with its portrait and close box painted over, sliceable to any size.</summary>
        public static Sprite Card()
        {
            const string key = "card";
            if (Derived.TryGetValue(key, out var cached) && cached != null && cached.texture != null)
            {
                return cached;
            }
            var source = GameSprite("shade_dialoge_notext_01");
            if (source == null)
            {
                return null;
            }

            try
            {
                var texture = ReadSprite(source);
                int w = texture.width, h = texture.height;
                // Portrait: repeat the empty middle of the card. Close box: mirror the opposite side.
                texture.SetPixels(14, 16, 124, h - 32, texture.GetPixels(300, 16, 124, h - 32));
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < 62; x++)
                    {
                        texture.SetPixel(w - 1 - x, y, texture.GetPixel(x, y));
                    }
                }
                texture.Apply();
                return Keep(key, Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f)));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Could not prepare the dialogue card: {ex.Message}");
                return null;
            }
        }

        private static Texture2D ReadSprite(Sprite sprite)
        {
            var source = sprite.texture;
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Graphics.Blit(source, target);
            RenderTexture.active = target;

            // Sprites can be trimmed inside their atlas; rebuild the untrimmed rectangle.
            var full = sprite.rect;
            var used = sprite.textureRect;
            var offset = sprite.textureRectOffset;
            var result = new Texture2D(Mathf.RoundToInt(full.width), Mathf.RoundToInt(full.height), TextureFormat.RGBA32, false)
            {
                name = "native_" + sprite.name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            result.SetPixels32(new Color32[result.width * result.height]);
            int toX = Mathf.RoundToInt(offset.x), toY = Mathf.RoundToInt(offset.y);
            float width = Mathf.Min(used.width, result.width - toX);
            float height = Mathf.Min(used.height, result.height - toY);
            result.ReadPixels(new Rect(used.x, used.y, width, height), toX, toY, false);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            return result;
        }

        public static TMP_FontAsset Font
        {
            get
            {
                if (_font == null || _fontMaterial == null)
                {
                    // Several font assets and materials share one name; only a live label pairs them correctly.
                    foreach (var label in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
                    {
                        if (label != null && label.name == "TextItem" && label.font != null && label.fontSharedMaterial != null
                            && label.font.name == "QUINTESSENCE SDF_2" && label.fontSharedMaterial.mainTexture == label.font.material.mainTexture)
                        {
                            _font = label.font;
                            _fontMaterial = label.fontSharedMaterial;
                            break;
                        }
                    }
                }
                return _font;
            }
        }

        // ---- builders -------------------------------------------------------------------------------------------

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Places a rect by its top-left corner, in reference pixels, measured from its parent's top-left.</summary>
        public static RectTransform Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, bool raycast = false)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }
            return image;
        }

        public static Image Icon(string name, Transform parent, Sprite sprite)
        {
            var image = Image(name, parent, sprite);
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = true;
            return image;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (Font != null)
            {
                text.font = Font;
                text.fontSharedMaterial = _fontMaterial;
            }
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            text.richText = true;
            return text;
        }

        /// <summary>Shrinks long lines the way the vanilla dialogue labels do.</summary>
        public static TextMeshProUGUI Fit(this TextMeshProUGUI text, float min)
        {
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = min;
            text.enableAutoSizing = true;
            return text;
        }

        public static NativeButton PlankButton(string name, Transform parent, string label, float size, Action onClick)
        {
            var image = Image(name, parent, Plank(1), true);
            var button = image.gameObject.AddComponent<NativeButton>();
            button.Background = image;
            button.Normal = Plank(1);
            button.Hover = Plank(2);
            button.Pressed = Plank(3);
            button.Clicked = onClick;
            button.Label = Text("Label", image.transform, size, Ink, TextAlignmentOptions.Center).Fit(size * 0.55f);
            button.Label.text = label;
            Stretch(button.Label.rectTransform, 14f);
            return button;
        }

        public static NativeButton CloseButton(Transform parent, Action onClick)
        {
            var image = Image("Close", parent, GameSprite("exitbutton_0001"), true);
            var button = image.gameObject.AddComponent<NativeButton>();
            button.Background = image;
            button.Normal = GameSprite("exitbutton_0001");
            button.Hover = GameSprite("exitbutton_0002");
            button.Pressed = GameSprite("exitbutton_0003");
            button.Clicked = onClick;
            return button;
        }
    }

    /// <summary>Pointer handling shared by every native-styled control.</summary>
    internal sealed class NativeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler, IPointerClickHandler
    {
        public Image Background;
        public Sprite Normal;
        public Sprite Hover;
        public Sprite Pressed;
        public TextMeshProUGUI Label;
        public Action Clicked;
        public Action Entered;
        public Action Exited;

        private bool _inside;
        private bool _down;
        private bool _enabled = true;

        public bool Interactable
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Refresh();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _inside = true;
            Refresh();
            Entered?.Invoke();
            if (_enabled && Clicked != null && GameManagerScript.IsGameRunning())
            {
                FindObjectOfType<GameManagerScript>()?.CursorHud();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _inside = false;
            _down = false;
            Refresh();
            Exited?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _down = true;
            Refresh();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _down = false;
            Refresh();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_enabled && eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke();
            }
        }

        private void OnDisable()
        {
            if (_inside)
            {
                _inside = false;
                Exited?.Invoke();
            }
            _down = false;
        }

        public void Refresh()
        {
            if (Background != null && Normal != null)
            {
                Background.sprite = !_enabled ? Normal : _down && Pressed != null ? Pressed : _inside && Hover != null ? Hover : Normal;
                Background.color = _enabled ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
            }
            if (Label != null)
            {
                Label.color = _enabled ? NativeUi.Ink : NativeUi.Muted;
            }
        }
    }
}
