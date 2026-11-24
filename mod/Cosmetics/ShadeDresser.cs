using System.Reflection;
using HarmonyLib;
using TheLongingDecrypted.Relics;
using TheLongingDecrypted.UI;
using UnityEngine;

namespace TheLongingDecrypted.Cosmetics
{
    /// <summary>Draws the wardrobe onto the live Shade after the animator has picked this frame's sprites.</summary>
    internal sealed class ShadeDresser : MonoBehaviour
    {
        private const float StandingHeight = 0.71f;

        private static readonly FieldInfo HeadField = AccessTools.Field(typeof(Movement), "PMDLBNBGMJC");
        private static readonly Vector2 HatPivot = new Vector2(0.5f, 0.2f);

        private Movement _movement;
        private SpriteRenderer _body;
        private SpriteRenderer _head;
        private SpriteRenderer _hat;
        private string _hatKey;
        private bool _hatFromBehind;
        private bool _tinted;
        private bool _shrunk;

        private GameObject _pet;
        private SpriteRenderer _petRenderer;
        private string _petKey;
        private float _petSpeed;
        private float _petStep;
        private Vector2 _petAt;
        private bool _petFacesRight;

        private SpriteRenderer _glow;
        private Vector3 _headAt;
        private float _hiccupAt;
        private bool _hiccuping;

        private void LateUpdate()
        {
            if (!FindShade())
            {
                if (_pet != null)
                {
                    _pet.SetActive(false);
                }
                return;
            }
            DressHat();
            TintShade();
            CarryLantern();
            LeadPet();
            ApplyBodyTraps();
        }

        private bool FindShade()
        {
            if (_movement != null && _body != null && _head != null)
            {
                return true;
            }
            _movement = FindObjectOfType<Movement>();
            _head = _movement != null ? HeadField?.GetValue(_movement) as SpriteRenderer : null;
            _body = _head != null && _head.transform.parent != null ? _head.transform.parent.GetComponent<SpriteRenderer>() : null;
            _tinted = _shrunk = _hiccuping = false;
            return _body != null;
        }

        // ---- hat ----------------------------------------------------------------------------------------------------

        private void DressHat()
        {
            string key = Wardrobe.Hat;
            // The separate head only exists while standing and walking. Otherwise the pose comes from the body frame.
            bool headShown = _head.enabled && _head.gameObject.activeInHierarchy && _head.sprite != null;
            var source = headShown ? _head : _body;
            bool known = HeadAnchors.TryGet(source.sprite, out var anchor);
            // Many frames are drawn far away from their own origin, so the head is the only reliable place to hang light on.
            _headAt = known ? source.transform.TransformPoint(anchor.Position) : _body.transform.position;
            bool visible = known && key != null && Wardrobe.Owned(key) && source.enabled && source.gameObject.activeInHierarchy;
            if (!visible)
            {
                if (_hat != null)
                {
                    _hat.enabled = false;
                }
                return;
            }

            if (_hat == null)
            {
                _hat = new GameObject("ShadeHat").AddComponent<SpriteRenderer>();
                _hatKey = null;
            }
            if (_hatKey != key || _hatFromBehind != anchor.FromBehind)
            {
                _hatKey = key;
                _hatFromBehind = anchor.FromBehind;
                _hat.sprite = HatSprite(key, anchor.FromBehind);
            }

            var place = _hat.transform;
            if (place.parent != source.transform)
            {
                place.SetParent(source.transform, false);
            }
            bool flipped = source.flipX;
            place.localPosition = new Vector3(flipped ? -anchor.Position.x : anchor.Position.x, anchor.Position.y, headShown ? 0f : -0.1f);
            place.localRotation = Quaternion.Euler(0f, 0f, flipped ? -anchor.Angle : anchor.Angle);
            place.localScale = new Vector3(anchor.Mirrored != flipped ? -anchor.Scale : anchor.Scale, anchor.Scale, 1f);

            var color = source.color;
            color.a *= anchor.Alpha;
            _hat.color = color;
            _hat.gameObject.layer = source.gameObject.layer;
            _hat.sharedMaterial = _body.sharedMaterial;
            _hat.sortingLayerID = _body.sortingLayerID;
            _hat.sortingOrder = Mathf.Max(_body.sortingOrder, _head.sortingOrder) + 1;
            _hat.enabled = true;
        }

        private static Sprite HatSprite(string key, bool fromBehind)
        {
            var def = Wardrobe.Find(key);
            if (def == null)
            {
                return null;
            }
            var back = fromBehind ? CustomAssetLoader.LoadSprite(def.Asset.Replace(".png", "_back.png"), HatPivot) : null;
            return back != null ? back : CustomAssetLoader.LoadSprite(def.Asset, HatPivot);
        }

        // ---- color and lantern ----------------------------------------------------------------------------------------

        private void TintShade()
        {
            int index = Wardrobe.Tint;
            if (index == 0 && !_tinted)
            {
                return;
            }
            Recolor(_body, Wardrobe.Tints[index].Tint);
            Recolor(_head, Wardrobe.Tints[index].Tint);
            _tinted = index != 0;
        }

        private static void Recolor(SpriteRenderer renderer, Color tint)
        {
            // Some animations fade the Shade through the alpha channel.
            tint.a = renderer.color.a;
            renderer.color = tint;
        }

        private void CarryLantern()
        {
            if (!RelicBag.LanternLit)
            {
                if (_glow != null)
                {
                    _glow.enabled = false;
                }
                return;
            }
            if (_glow == null)
            {
                _glow = new GameObject("ShadeLanternGlow").AddComponent<SpriteRenderer>();
                _glow.sprite = SoftGlow.Sprite;
                _glow.sortingOrder = 198;
            }
            float flicker = 0.96f + Mathf.PerlinNoise(Time.time * 1.7f, 0.3f) * 0.08f;
            _glow.enabled = true;
            _glow.transform.position = _headAt + new Vector3(0f, -0.5f, -0.2f);
            _glow.transform.localScale = Vector3.one * 4.4f * flicker;
            _glow.color = new Color(1f, 0.72f, 0.36f, 0.18f);
        }

        // ---- companion ----------------------------------------------------------------------------------------------------

        private void LeadPet()
        {
            string key = Wardrobe.Pet;
            if (key == null || !Wardrobe.Owned(key) || !_body.enabled || !_body.gameObject.activeInHierarchy)
            {
                if (_pet != null)
                {
                    _pet.SetActive(false);
                }
                return;
            }

            // The Shade is drawn facing left; a positive scale means walking left, so "behind" is to the right.
            var shade = _movement.transform.position;
            float behind = _body.transform.lossyScale.x >= 0f ? 1f : -1f;
            var heel = new Vector2(shade.x + behind * 1.05f, shade.y);

            if (_pet == null)
            {
                _pet = new GameObject("ShadeCompanion");
                _petRenderer = _pet.AddComponent<SpriteRenderer>();
                _petKey = null;
                _pet.SetActive(false);
            }
            if (!_pet.activeSelf)
            {
                _pet.SetActive(true);
                _petAt = heel;
                _petSpeed = 0f;
            }
            if (_petKey != key)
            {
                _petKey = key;
                _petRenderer.sprite = CustomAssetLoader.LoadSprite(Wardrobe.Find(key).Asset, new Vector2(0.5f, 0f));
            }

            // Kept apart from the drawn position, which also hops.
            var at = _petAt;
            var gap = heel - at;
            if (gap.magnitude > 4.5f || Mathf.Abs(gap.y) > 1.2f)
            {
                // Left behind by a ladder, a fall, or a new room: wait at the Shade's heel instead of sliding through walls.
                at = heel;
                _petSpeed = 0f;
            }
            else
            {
                float far = Mathf.Max(0f, Mathf.Abs(gap.x) - 0.18f);
                _petSpeed = Mathf.MoveTowards(_petSpeed, Mathf.Min(far * 3.2f, 4f), Time.deltaTime * 6f);
                float step = Mathf.Min(_petSpeed * Time.deltaTime, Mathf.Abs(gap.x));
                at.x += Mathf.Sign(gap.x) * step;
                at.y = Mathf.Lerp(at.y, shade.y, Time.deltaTime * 8f);
                _petStep += step;
                _petFacesRight = _petSpeed > 0.05f ? gap.x > 0f : shade.x > at.x;
            }

            _petAt = at;
            bool rolls = key == "AP_PET_ROCK";
            float hop = rolls ? Mathf.Abs(Mathf.Sin(_petStep * 5f)) * 0.06f : Mathf.Abs(Mathf.Sin(_petStep * 16f)) * 0.018f;
            float breathe = !rolls && _petSpeed < 0.05f ? Mathf.Sin(Time.time * 2.1f) * 0.02f : 0f;
            _pet.transform.position = new Vector3(at.x, at.y + hop, shade.z);
            _pet.transform.rotation = Quaternion.Euler(0f, 0f, rolls ? Mathf.Sin(_petStep * 5f) * 5f : 0f);
            _pet.transform.localScale = new Vector3(_petFacesRight ? -1f : 1f, 1f + breathe, 1f);
            _petRenderer.sharedMaterial = _body.sharedMaterial;
            _petRenderer.sortingLayerID = _body.sortingLayerID;
            _petRenderer.sortingOrder = _body.sortingOrder - 2;
            _petRenderer.color = new Color(1f, 1f, 1f, _body.color.a);
        }

        // ---- traps that act on the body ---------------------------------------------------------------------------------------

        private void ApplyBodyTraps()
        {
            var delivery = Plugin.Delivery;
            if (delivery == null)
            {
                return;
            }

            var shape = _body.transform.parent;
            if (shape != null && (delivery.IsTinyShadeActive || _shrunk))
            {
                float goal = delivery.IsTinyShadeActive ? 0.55f : 1f;
                var scale = shape.localScale;
                float size = Mathf.MoveTowards(Mathf.Abs(scale.y), goal, Time.deltaTime * 1.5f);
                shape.localScale = new Vector3(Mathf.Sign(scale.x) * size, size, scale.z);
                _shrunk = delivery.IsTinyShadeActive || size < 0.999f;
            }

            if (!delivery.IsHiccupTrapActive)
            {
                if (_hiccuping)
                {
                    LiftBody(0f);
                    _hiccuping = false;
                }
                return;
            }
            if (Time.time > _hiccupAt + 0.25f)
            {
                _hiccupAt = Time.time + Random.Range(1.2f, 2.6f);
            }
            float through = (Time.time - _hiccupAt) / 0.25f;
            bool hop = through > 0f && through < 1f && _movement.IsInInteractState();
            if (hop || _hiccuping)
            {
                LiftBody(hop ? Mathf.Sin(through * Mathf.PI) * 0.12f : 0f);
                _hiccuping = hop;
            }
        }

        private void LiftBody(float by)
        {
            var at = _body.transform.localPosition;
            _body.transform.localPosition = new Vector3(at.x, StandingHeight + by, at.z);
        }
    }

    /// <summary>A soft round light, painted once.</summary>
    internal static class SoftGlow
    {
        private static Sprite _sprite;

        public static Sprite Sprite
        {
            get
            {
                if (_sprite == null)
                {
                    const int size = 128;
                    var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                    var pixels = new Color32[size * size];
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f;
                            float fade = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255f * fade * fade * (3f - 2f * fade)));
                        }
                    }
                    texture.SetPixels32(pixels);
                    texture.Apply();
                    _sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
                    texture.hideFlags = _sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
                return _sprite;
            }
        }
    }
}
