using TheLongingDecrypted.Archipelago;
using TheLongingDecrypted.Relics;
using UnityEngine;
using UnityEngine.UI;

namespace TheLongingDecrypted.UI
{
    /// <summary>Darkness, visitors, rumbles, and gloomy thoughts, shown inside the cave opening of the game's HUD.</summary>
    internal sealed class TroubleOverlay : MonoBehaviour
    {
        private RectTransform _root;
        private RawImage _dark;
        private Image _thread;
        private Image _spider;
        private float _darkness;
        private bool _shaking;
        private Vector3 _shake;
        private Camera _shaken;
        private static Texture2D _hole;

        private void LateUpdate()
        {
            ShakeCamera();
            var canvas = FrameHud.FindHud();
            if (canvas == null)
            {
                _root = null;
                return;
            }
            if (_root == null)
            {
                Build(canvas);
            }
            UpdateDarkness();
            UpdateVisitor();
        }

        private void Build(Transform canvas)
        {
            _root = NativeUi.Stretch(NativeUi.Rect("DecryptedTroubles", canvas));
            // Under every card and the frame itself.
            _root.SetSiblingIndex(0);
            var rect = NativeUi.Rect("Dark", _root);
            _dark = rect.gameObject.AddComponent<RawImage>();
            _dark.texture = Hole();
            _dark.raycastTarget = false;
            NativeUi.Stretch(rect, -400f);
            _dark.enabled = false;

            _thread = NativeUi.Image("Thread", _root, FrameHud.SolidSprite());
            _thread.color = new Color(0.75f, 0.72f, 0.70f, 0.55f);
            _thread.enabled = false;
            _spider = NativeUi.Icon("Visitor", _root, CustomAssetLoader.LoadSprite("pets/pet_spider.png", new Vector2(0.5f, 0.5f)));
            _spider.rectTransform.sizeDelta = new Vector2(104f, 104f);
            _spider.enabled = false;
        }

        private void UpdateDarkness()
        {
            bool dark = Plugin.Delivery.IsDarknessTrapActive;
            _darkness = Mathf.MoveTowards(_darkness, dark ? 1f : 0f, Time.deltaTime * 0.7f);
            _dark.enabled = _darkness > 0f;
            if (!_dark.enabled)
            {
                return;
            }
            // A lit lantern keeps a wider circle around the Shade.
            float reach = RelicBag.LanternLit ? 0.36f : 0.19f;
            var shade = FindObjectOfType<Movement>();
            var camera = Camera.main;
            Vector2 center = new Vector2(0.5f, 0.5f);
            if (shade != null && camera != null)
            {
                center = camera.WorldToViewportPoint(shade.transform.position + Vector3.up * 0.8f);
            }
            float aspect = _root.rect.width / Mathf.Max(1f, _root.rect.height);
            float w = (_root.rect.width + 800f) / _root.rect.height / (reach * 4f);
            float h = (_root.rect.height + 800f) / _root.rect.height / (reach * 4f);
            float cx = (center.x * _root.rect.width + 400f) / (_root.rect.width + 800f);
            float cy = (center.y * _root.rect.height + 400f) / (_root.rect.height + 800f);
            _dark.uvRect = new Rect(0.5f - cx * w, 0.5f - cy * h, w, h);
            _dark.color = new Color(0f, 0f, 0f, _darkness * 0.97f);
        }

        private void UpdateVisitor()
        {
            bool visiting = Plugin.Delivery.IsSpiderScareActive;
            _spider.enabled = _thread.enabled = visiting;
            if (!visiting)
            {
                return;
            }
            float through = 1f - Plugin.Delivery.TrapFractionLeft("TRAP_SPIDER_SCARE");
            // Drops fast, dangles, and climbs back up.
            float drop = through < 0.08f ? through / 0.08f : through > 0.75f ? Mathf.Clamp01((1f - through) / 0.25f) : 1f;
            float depth = 70f + drop * 420f + Mathf.Sin(Time.time * 3f) * 6f * drop;
            float sway = Mathf.Sin(Time.time * 1.6f) * 10f * drop;
            Place(_thread.rectTransform, new Vector2(sway * 0.5f, -depth * 0.5f), new Vector2(2f, depth));
            _spider.rectTransform.anchorMin = _spider.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _spider.rectTransform.anchoredPosition = new Vector2(sway, -depth - 22f);
            _spider.rectTransform.localRotation = Quaternion.Euler(0f, 0f, sway * 0.6f);
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void ShakeCamera()
        {
            if (_shaking && _shaken != null)
            {
                _shaken.transform.position -= _shake;
                _shaking = false;
            }
            float left = Plugin.Delivery.TrapFractionLeft("TRAP_FALSE_WAKE");
            if (left <= 0.35f)
            {
                return;
            }
            _shaken = Camera.main;
            if (_shaken == null)
            {
                return;
            }
            float strength = Mathf.Sin((left - 0.35f) / 0.65f * Mathf.PI) * 0.07f;
            _shake = new Vector3((Mathf.PerlinNoise(Time.time * 22f, 0f) - 0.5f) * strength, (Mathf.PerlinNoise(0f, Time.time * 22f) - 0.5f) * strength, 0f);
            _shaken.transform.position += _shake;
            _shaking = true;
        }

        private static Texture2D Hole()
        {
            if (_hole == null)
            {
                const int size = 256;
                _hole = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontUnloadUnusedAsset,
                };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = (x - 127.5f) / 127.5f, dy = (y - 127.5f) / 127.5f;
                        float edge = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - 0.25f) / 0.7f);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * edge * edge * (3f - 2f * edge)));
                    }
                }
                _hole.SetPixels32(pixels);
                _hole.Apply();
            }
            return _hole;
        }
    }
}
