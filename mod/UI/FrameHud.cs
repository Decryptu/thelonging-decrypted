using System;
using System.Collections.Generic;
using CodeStage.AntiCheat.ObscuredTypes;
using TheLongingDecrypted.Cosmetics;
using TheLongingDecrypted.Patches;
using TheLongingDecrypted.Relics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLongingDecrypted.UI
{
    /// <summary>
    /// Fills the empty carved squares of the game's own outer frame: keepsakes and pouch along the bottom, hats on the
    /// left, companions and passing troubles on the right. Lives inside the game's HUD canvas, so it scales, hides, and
    /// blocks clicks exactly like the rest of the frame.
    /// </summary>
    internal sealed class FrameHud : MonoBehaviour
    {
        // Centers of the carved squares in the 1920x1080 frame, left to right and top to bottom.
        private static readonly float[] BottomX =
        {
            111f, 173f, 235f, 296.5f, 358f, 420f, 482f, 544f, 606f, 667.5f, 729f, 791f, 853f, 915f, 976.5f, 1038f,
            1100f, 1163f, 1226f, 1293.5f, 1364.5f, 1434f, 1500f, 1562f, 1624f, 1686f, 1747.5f, 1809f,
        };

        private static readonly float[] SideY =
        {
            96f, 158f, 220f, 281f, 343f, 405f, 467f, 529f, 590f, 655f, 723f, 790.5f, 854.5f, 918f, 980.5f,
        };

        private const int FirstRelic = 11;
        private const int FirstPouch = 19;
        private const float BottomY = 1043f;
        private const float LeftX = 42f;
        private const float RightX = 1877f;
        private const float TopX = 1437f;
        private const float TopY = 35f;

        private static readonly Queue<Notice> Waiting = new Queue<Notice>();
        private static FrameHud _instance;

        private readonly List<Slot> _slots = new List<Slot>();
        private readonly List<NoticeView> _notices = new List<NoticeView>();
        private RectTransform _root;
        private RectTransform _tooltip;
        private TextMeshProUGUI _tooltipTitle;
        private TextMeshProUGUI _tooltipBody;
        private Slot _hovered;
        private float _nextRefresh;

        private struct Notice
        {
            public string Title;
            public string Body;
            public string Icon;
        }

        private sealed class NoticeView
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public float Until;
            public float Y;
        }

        private struct Face
        {
            public bool Visible;
            public string Icon;
            public bool Lit;
            public float Sweep;
            public string Count;
            public string Title;
            public string Body;
        }

        private sealed class Slot
        {
            public RectTransform Rect;
            public Image Back;
            public Image Glyph;
            public Image Sweep;
            public Image Ring;
            public TextMeshProUGUI Count;
            public NativeButton Button;
            public Func<Face> Look;
            public Func<string> Click;
            public Vector2 TooltipPivot;
            public Vector2 TooltipOffset;
            public Face Last;
            public string Answer;
            public float AnswerUntil;
        }

        // ---- speaking -------------------------------------------------------------------------------------------------

        /// <summary>Shows a small plank in the lower left corner of the cave.</summary>
        public static void Say(string title, string body, string icon)
        {
            Waiting.Enqueue(new Notice { Title = title, Body = body, Icon = icon });
        }

        private void Awake()
        {
            _instance = this;
        }

        private void Update()
        {
            var canvas = FindHud();
            if (canvas == null)
            {
                _root = null;
                return;
            }
            if (_root == null)
            {
                Build(canvas);
            }
            if (_root == null)
            {
                return;
            }

            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.15f;
                foreach (var slot in _slots)
                {
                    Refresh(slot);
                }
            }
            UpdateTooltip();
            UpdateNotices();
        }

        private static GameObject _hud;

        internal static Transform FindHud()
        {
            if (_hud == null)
            {
                _hud = GameObject.Find("uiHudSingleTestPrefab(Clone)");
            }
            return _hud != null ? _hud.transform : null;
        }

        // ---- building -----------------------------------------------------------------------------------------------------

        private void Build(Transform canvas)
        {
            if (!NativeUi.Ready || NativeUi.Card() == null)
            {
                return;
            }
            _slots.Clear();
            _notices.Clear();
            _hovered = null;

            _root = NativeUi.Stretch(NativeUi.Rect("DecryptedFrame", canvas));
            var frame = canvas.Find("ImageSurroundASParent");
            if (frame != null)
            {
                _root.SetSiblingIndex(frame.GetSiblingIndex() + 1);
            }

            int b = 0;
            AddBottom(b++, WalkFace);
            AddBottom(b++, RecallFace);
            AddBottom(b++, TimeRateFace);
            AddBottom(b++, () => UnlockFace("AP_LIBRARY_PASS", "ink:icons_0008"));
            AddBottom(b++, () => UnlockFace("AP_SURFACE_GATE_PASS", "ink:icons_0013"));
            AddBottom(b++, () => PouchFace("SecretKey", "icons/ink_key.png", false));
            AddBottom(b++, () => UnlockFace("AP_CAVERN_MAP", "icons/ink_map.png"));
            AddBottom(b++, () => UnlockFace("AP_GIANT_SPORES", "ink:icons_0006"));
            AddBottom(b++, () => PouchFace("MUL4L4L4", "ink:icons_0010", true));
            AddBottom(b, HuntFace);

            for (int i = 0; i < RelicBag.All.Length; i++)
            {
                var relic = RelicBag.All[i];
                AddBottom(FirstRelic + i, () => RelicFace(relic), () => RelicBag.Use(relic.Key));
            }

            int p = FirstPouch;
            AddBottom(p++, () => PouchFace(RelicBag.CoalKey, "icons/ink_coal.png", true));
            AddBottom(p++, () => PouchFace(RelicBag.MossKey, "icons/ink_moss.png", true));
            AddBottom(p++, () => PouchFace("FRST435", "icons/ink_wood.png", true));
            AddBottom(p++, () => PouchFace("FRST986", "icons/ink_flint.png", true));
            AddBottom(p++, () => PouchFace("5U2LFU6", "ink:icons_0014", true));
            AddBottom(p++, () => PouchFace("L45I5", "icons/ink_crystal.png", true));
            AddBottom(p++, () => PouchFace("P4H_FFT", "ink:icons_0009", true));
            AddBottom(p, () => PouchFace("PH534TLO0", "ink:icons_0002", true));

            for (int i = 0; i < Wardrobe.Hats.Length; i++)
            {
                var hat = Wardrobe.Hats[i];
                AddSide(true, i, () => WornFace(hat), () => Wear(hat));
            }
            for (int i = 0; i < Wardrobe.Pets.Length; i++)
            {
                var pet = Wardrobe.Pets[i];
                AddSide(false, i, () => WornFace(pet), () => Wear(pet));
            }

            string[] troubles = { "TRAP_SLOWNESS", "TRAP_DARKNESS", "TRAP_TIME_STALL", "TRAP_HICCUP", "TRAP_TINY_SHADE" };
            string[] glyphs = { "ink:icons_0011", "ink:icons_0030", "ink:icons_0023", "ink:icons_0031", "game:shade_head_0009" };
            for (int i = 0; i < troubles.Length; i++)
            {
                string trouble = troubles[i], glyph = glyphs[i];
                AddSide(false, SideY.Length - 1 - i, () => TroubleFace(trouble, glyph));
            }

            var far = AddSlot(new Vector2(0.5f, 1f), new Vector2(TopX - 960f, -TopY), new Vector2(56f, 54f), FarWorldsFace, () =>
            {
                MultiworldPanel.Toggle();
                return null;
            });
            far.TooltipPivot = new Vector2(0.5f, 1f);
            far.TooltipOffset = new Vector2(0f, -42f);

            BuildTooltip();
            foreach (var slot in _slots)
            {
                Refresh(slot);
            }
        }

        private void AddBottom(int index, Func<Face> look, Func<string> click = null)
        {
            var slot = AddSlot(new Vector2(0.5f, 0f), new Vector2(BottomX[index] - 960f, 1080f - BottomY), new Vector2(50f, 50f), look, click);
            slot.TooltipPivot = new Vector2(0.5f, 0f);
            slot.TooltipOffset = new Vector2(0f, 44f);
        }

        private void AddSide(bool left, int index, Func<Face> look, Func<string> click = null)
        {
            var slot = AddSlot(new Vector2(left ? 0f : 1f, 0.5f), new Vector2(left ? LeftX : RightX - 1920f, 540f - SideY[index]),
                new Vector2(62f, 50f), look, click);
            slot.TooltipPivot = new Vector2(left ? 0f : 1f, 0.5f);
            slot.TooltipOffset = new Vector2(left ? 56f : -56f, 0f);
        }

        private Slot AddSlot(Vector2 anchor, Vector2 position, Vector2 size, Func<Face> look, Func<string> click)
        {
            var slot = new Slot { Look = look, Click = click };
            var hit = NativeUi.Image("Slot", _root, null, true);
            hit.color = Color.clear;
            slot.Rect = hit.rectTransform;
            slot.Rect.anchorMin = slot.Rect.anchorMax = anchor;
            slot.Rect.anchoredPosition = position;
            slot.Rect.sizeDelta = size;

            slot.Back = NativeUi.Image("Back", slot.Rect, null);
            NativeUi.Stretch(slot.Back.rectTransform);
            slot.Glyph = NativeUi.Icon("Glyph", slot.Rect, null);
            NativeUi.Stretch(slot.Glyph.rectTransform, 2f);

            slot.Sweep = NativeUi.Image("Sweep", slot.Rect, SolidSprite());
            NativeUi.Stretch(slot.Sweep.rectTransform);
            slot.Sweep.type = Image.Type.Filled;
            slot.Sweep.fillMethod = Image.FillMethod.Radial360;
            slot.Sweep.fillOrigin = (int)Image.Origin360.Top;
            slot.Sweep.fillClockwise = false;
            slot.Sweep.color = new Color(0.10f, 0.065f, 0.06f, 0.66f);

            slot.Ring = NativeUi.Image("Ring", slot.Rect, NativeUi.GameSprite("auswahlbild"));
            slot.Ring.color = NativeUi.Accent;
            slot.Ring.fillCenter = false;
            NativeUi.Stretch(slot.Ring.rectTransform, -1f);

            slot.Count = NativeUi.Text("Count", slot.Rect, 18f, NativeUi.Accent, TextAlignmentOptions.BottomRight);
            slot.Count.enableWordWrapping = false;
            slot.Count.overflowMode = TextOverflowModes.Overflow;
            NativeUi.Stretch(slot.Count.rectTransform);
            slot.Count.rectTransform.offsetMax = new Vector2(-6f, 0f);
            slot.Count.rectTransform.offsetMin = new Vector2(0f, 2f);

            slot.Button = slot.Rect.gameObject.AddComponent<NativeButton>();
            slot.Button.Entered = () => _hovered = slot;
            slot.Button.Exited = () =>
            {
                if (_hovered == slot)
                {
                    _hovered = null;
                }
            };
            if (click != null)
            {
                slot.Button.Clicked = () =>
                {
                    slot.Answer = click();
                    slot.AnswerUntil = Time.unscaledTime + 4f;
                    Refresh(slot);
                };
            }
            _slots.Add(slot);
            return slot;
        }

        private static Sprite _solid;

        internal static Sprite SolidSprite()
        {
            if (_solid == null)
            {
                _solid = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
                _solid.hideFlags = HideFlags.DontUnloadUnusedAsset;
            }
            return _solid;
        }

        private static void Refresh(Slot slot)
        {
            var face = slot.Look();
            slot.Last = face;
            if (slot.Rect.gameObject.activeSelf != face.Visible)
            {
                slot.Rect.gameObject.SetActive(face.Visible);
            }
            if (!face.Visible)
            {
                return;
            }
            var glyph = Icons.Resolve(face.Icon, out bool ink);
            slot.Glyph.sprite = glyph;
            slot.Glyph.enabled = glyph != null;
            NativeUi.Stretch(slot.Glyph.rectTransform, ink ? 5f : 3f);
            if (ink && !string.IsNullOrEmpty(face.Count))
            {
                // Leave room for the number.
                slot.Glyph.rectTransform.offsetMin = new Vector2(4f, 13f);
                slot.Glyph.rectTransform.offsetMax = new Vector2(-10f, -3f);
            }
            slot.Back.sprite = ink ? Icons.Parchment() : SolidSprite();
            slot.Back.color = ink ? Color.white : new Color(0.353f, 0.263f, 0.227f, face.Lit ? 1f : 0f);
            slot.Ring.enabled = face.Lit && !ink;
            slot.Sweep.enabled = face.Sweep > 0.001f;
            slot.Sweep.fillAmount = face.Sweep;
            slot.Count.text = face.Count ?? string.Empty;
            slot.Count.color = ink ? NativeUi.Ink : NativeUi.Accent;
        }

        // ---- what each square shows --------------------------------------------------------------------------------------------

        private static readonly string[] Roman = { "", "I", "II", "III", "IV" };

        private static Face WalkFace()
        {
            int tier = TimeAndMovementPatches.WalkSpeedTier;
            return new Face
            {
                Visible = tier > 0,
                Icon = "ink:icons_0020",
                Count = Roman[tier],
                Title = Lines.Of("AP_WALK_SPEED").Name,
                Body = Lines.Pace(tier),
            };
        }

        private static Face RecallFace()
        {
            int tier = WaypointRecallPatches.RecallTier;
            return new Face
            {
                Visible = tier > 0,
                Icon = "icons/ink_recall.png",
                Count = Roman[tier],
                Title = Lines.Of("AP_WAYPOINT_RECALL").Name,
                Body = Lines.Recall(tier),
            };
        }

        private static Face TimeRateFace()
        {
            int rate = Mathf.RoundToInt(TimeAndMovementPatches.GetEffectiveTimeRate());
            return new Face
            {
                Visible = Plugin.Delivery.BonusTimeRate > 0,
                Icon = "ink:icons_0023",
                Count = rate.ToString(),
                Title = Lines.Of("AP_BONUS_TIME_RATE").Name,
                Body = Lines.TimeRate(rate),
            };
        }

        private static Face UnlockFace(string key, string icon)
        {
            return new Face
            {
                Visible = FileBasedPrefs.GetInt(key, 0) > 0,
                Icon = icon,
                Title = Lines.Of(key).Name,
                Body = Lines.Of(key).Thought,
            };
        }

        private static Face PouchFace(string key, string icon, bool counted)
        {
            int amount = ObscuredPrefs.GetInt(key, 0);
            return new Face
            {
                Visible = amount > 0,
                Icon = icon,
                Count = counted && amount > 1 ? amount.ToString() : null,
                Title = Lines.Of(key).Name,
                Body = counted ? Lines.Of(key).Thought + " " + Lines.Count(amount) : Lines.Of(key).Thought,
            };
        }

        private static Face HuntFace()
        {
            var config = Plugin.Session.ActiveConfig;
            bool hunting = !string.IsNullOrEmpty(Plugin.Session.ConnectedSlot) && config.Goal == "crystal_book_hunt";
            if (!hunting)
            {
                return default;
            }
            return new Face
            {
                Visible = true,
                Icon = "icons/ink_crystal.png",
                Count = (Plugin.Delivery.ReceivedCrystals + Plugin.Delivery.ReceivedBooks).ToString(),
                Title = Lines.Goal(config.Goal),
                Body = Lines.Hunt(Plugin.Delivery.ReceivedCrystals, config.RequiredCrystals, Plugin.Delivery.ReceivedBooks, config.RequiredBooks),
            };
        }

        private static Face RelicFace(RelicDef relic)
        {
            if (!RelicBag.Owned(relic.Key))
            {
                return default;
            }
            double waiting = RelicBag.Waiting(relic.Key);
            double active = RelicBag.Active(relic.Key);
            int rank = RelicBag.Rank(relic.Key);
            return new Face
            {
                Visible = true,
                Icon = relic.Asset,
                Lit = RelicBag.IsLit(relic.Key),
                // While it works, the lit square drains. Afterwards the dark sweep shows the wait.
                Sweep = active > 0d || relic.Cooldown == 0 ? 0f : Mathf.Clamp01((float)(waiting / RelicBag.Cooldown(relic))),
                Count = rank > 1 ? Roman[Mathf.Min(rank, Roman.Length - 1)] : null,
                Title = Lines.Of(relic.Key).Name,
                Body = Lines.Of(relic.Key).Thought + (waiting > 0d && active <= 0d ? " " + Lines.Wait(waiting) : string.Empty),
            };
        }

        private static Face WornFace(CosmeticDef def)
        {
            bool worn = Wardrobe.Equipped(def);
            return new Face
            {
                Visible = Wardrobe.Owned(def.Key),
                Icon = def.Asset,
                Lit = worn,
                Title = Lines.Of(def.Key).Name,
                Body = Lines.Of(def.Key).Thought + (worn ? " " + (def.IsPet ? Lines.FollowingMe : Lines.Wearing) : string.Empty),
            };
        }

        private static string Wear(CosmeticDef def)
        {
            Wardrobe.Toggle(def);
            return null;
        }

        private static Face TroubleFace(string key, string icon)
        {
            float left = Plugin.Delivery.TrapFractionLeft(key);
            return new Face
            {
                Visible = left > 0f,
                Icon = icon,
                Sweep = 1f - left,
                Title = Lines.TrapName(key),
                Body = Lines.TrapLine(key),
            };
        }

        private static Face FarWorldsFace()
        {
            bool awake = Plugin.Session.IsConnected;
            return new Face
            {
                Visible = true,
                Icon = awake ? "icons/ink_worlds.png" : "icons/worlds_dim.png",
                Title = Lines.MultiworldName,
                Body = awake ? Lines.MultiworldAwake : Lines.MultiworldAsleep,
            };
        }

        // ---- tooltip ----------------------------------------------------------------------------------------------------------------

        private void BuildTooltip()
        {
            var card = NativeUi.Image("Tooltip", _root, NativeUi.Card());
            _tooltip = card.rectTransform;
            _tooltipTitle = NativeUi.Text("Title", _tooltip, 25f, NativeUi.Accent);
            _tooltipBody = NativeUi.Text("Body", _tooltip, 22f, NativeUi.Ink);
            _tooltipTitle.overflowMode = _tooltipBody.overflowMode = TextOverflowModes.Overflow;
            _tooltip.gameObject.SetActive(false);
        }

        private void UpdateTooltip()
        {
            bool show = _hovered != null && _hovered.Last.Visible && !string.IsNullOrEmpty(_hovered.Last.Title);
            if (_tooltip.gameObject.activeSelf != show)
            {
                _tooltip.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }

            var slot = _hovered;
            bool answered = !string.IsNullOrEmpty(slot.Answer) && Time.unscaledTime < slot.AnswerUntil;
            _tooltipTitle.text = slot.Last.Title;
            _tooltipBody.text = answered ? slot.Answer : slot.Last.Body;

            const float width = 430f, pad = 26f;
            float inner = width - pad * 2f;
            float titleHeight = _tooltipTitle.GetPreferredValues(_tooltipTitle.text, inner, 0f).y;
            float bodyHeight = _tooltipBody.GetPreferredValues(_tooltipBody.text, inner, 0f).y;
            float height = pad * 2f + titleHeight + bodyHeight + 4f;
            NativeUi.Place(_tooltipTitle.rectTransform, pad, pad - 2f, inner, titleHeight);
            NativeUi.Place(_tooltipBody.rectTransform, pad, pad + titleHeight + 2f, inner, bodyHeight);

            _tooltip.anchorMin = _tooltip.anchorMax = slot.Rect.anchorMin;
            _tooltip.pivot = slot.TooltipPivot;
            _tooltip.sizeDelta = new Vector2(width, height);
            var position = slot.Rect.anchoredPosition + slot.TooltipOffset;
            if (Mathf.Approximately(slot.TooltipPivot.x, 0.5f))
            {
                // Keep bottom and top cards inside the cave opening.
                float limit = 872f - width * 0.5f;
                position.x = Mathf.Clamp(position.x, -limit, limit);
            }
            else
            {
                float limit = 466f - height * 0.5f;
                position.y = Mathf.Clamp(position.y, -limit, limit);
            }
            _tooltip.anchoredPosition = position;
            _tooltip.SetAsLastSibling();
        }

        // ---- notices -------------------------------------------------------------------------------------------------------------------

        private void UpdateNotices()
        {
            bool busy = Peddler.PeddlerPanel.IsOpen || MultiworldPanel.IsOpen;
            while (!busy && Waiting.Count > 0 && _notices.Count < 4)
            {
                ShowNotice(Waiting.Dequeue());
            }

            float y = 92f;
            for (int i = _notices.Count - 1; i >= 0; i--)
            {
                var view = _notices[i];
                if (busy)
                {
                    // Wait out of sight until the card is closed.
                    view.Until += Time.unscaledDeltaTime;
                    view.Group.alpha = 0f;
                    continue;
                }
                float left = view.Until - Time.unscaledTime;
                if (left <= 0f || view.Rect == null)
                {
                    if (view.Rect != null)
                    {
                        Destroy(view.Rect.gameObject);
                    }
                    _notices.RemoveAt(i);
                    continue;
                }
                view.Group.alpha = Mathf.Min(Mathf.MoveTowards(view.Group.alpha, 1f, Time.unscaledDeltaTime * 4f), left * 2f);
                view.Y = view.Y <= 0f ? y : Mathf.Lerp(view.Y, y, Time.unscaledDeltaTime * 10f);
                view.Rect.anchoredPosition = new Vector2(104f, view.Y);
                y += view.Rect.sizeDelta.y + 8f;
            }
        }

        private void ShowNotice(Notice notice)
        {
            const float width = 560f, iconBox = 78f, pad = 20f;
            var root = NativeUi.Rect("Notice", _root);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.zero;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var plank = NativeUi.Image("Plank", root, NativeUi.Card());
            float inner = width - iconBox - pad * 2f;
            var title = NativeUi.Text("Title", plank.transform, 25f, NativeUi.Accent);
            var body = NativeUi.Text("Body", plank.transform, 22f, NativeUi.Ink);
            title.text = notice.Title;
            body.text = notice.Body;
            float titleHeight = string.IsNullOrEmpty(notice.Title) ? 0f : title.GetPreferredValues(notice.Title, inner, 0f).y;
            float bodyHeight = body.GetPreferredValues(notice.Body, inner, 0f).y;
            float height = Mathf.Max(84f, titleHeight + bodyHeight + pad * 2f);

            root.sizeDelta = new Vector2(width, height);
            NativeUi.Place(plank.rectTransform, iconBox - 6f, 0f, width - iconBox + 6f, height);
            NativeUi.Place(title.rectTransform, pad + 4f, pad - 2f, inner, titleHeight);
            NativeUi.Place(body.rectTransform, pad + 4f, pad + titleHeight, inner, bodyHeight + 4f);
            title.overflowMode = body.overflowMode = TextOverflowModes.Overflow;

            var frame = NativeUi.Image("Frame", root, NativeUi.IconFrame());
            frame.type = Image.Type.Simple;
            NativeUi.Place(frame.rectTransform, 0f, (height - 71f) * 0.5f, 78f, 71f);
            var glyph = Icons.Resolve(notice.Icon, out _);
            if (glyph != null)
            {
                var icon = NativeUi.Icon("Glyph", frame.transform, glyph);
                NativeUi.Place(icon.rectTransform, 18f, 14f, 42f, 42f);
            }

            float seconds = 4.5f + (notice.Title?.Length ?? 0) * 0.03f + (notice.Body?.Length ?? 0) * 0.045f;
            _notices.Add(new NoticeView { Rect = root, Group = group, Until = Time.unscaledTime + seconds });
        }
    }
}
