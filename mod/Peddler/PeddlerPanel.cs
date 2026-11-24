using System;
using System.Collections.Generic;
using TheLongingDecrypted.Relics;
using TheLongingDecrypted.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLongingDecrypted.Peddler
{
    /// <summary>The Peddler's shelves, drawn with the game's own dialogue cards, planks, and drawing-menu squares.</summary>
    internal sealed class PeddlerPanel : MonoBehaviour
    {
        private const float Width = 1180f;
        private const float Height = 720f;
        private const float ShelfX = 372f;
        private const float ShelfY = 116f;
        private const float ShelfWidth = Width - ShelfX - 40f;

        private static PeddlerPanel _instance;

        private readonly List<Tile> _tiles = new List<Tile>();
        private readonly List<NativeButton> _tabs = new List<NativeButton>();
        private RectTransform _root;
        private RectTransform _shelf;
        private TextMeshProUGUI _speech;
        private TextMeshProUGUI _purse;
        private TextMeshProUGUI _detailName;
        private TextMeshProUGUI _detailLine;
        private TextMeshProUGUI _detailPrice;
        private NativeButton _trade;
        private Offer _selected;
        private Offer _hovered;
        private int _tab;
        private float _openedAt;
        private Vector3 _openedFrom;
        private float _nextRefresh;

        private sealed class Tile
        {
            public Offer Offer;
            public Image Glyph;
            public TextMeshProUGUI Price;
            public Image Sweep;
            public Image Ring;
        }

        private static float _closedAt = -10f;

        /// <summary>True right after closing, so the same click or key does not open something else.</summary>
        public static bool JustClosed => Time.unscaledTime - _closedAt < 0.4f;

        public static bool IsOpen => _instance != null && _instance._root != null;

        private void Awake()
        {
            _instance = this;
        }

        public static void Open()
        {
            if (_instance != null && !IsOpen && !JustClosed)
            {
                _instance.Build();
            }
        }

        public static void Close()
        {
            if (!IsOpen)
            {
                return;
            }
            Destroy(_instance._root.gameObject);
            _instance._root = null;
            _closedAt = Time.unscaledTime;
            InputManager.SkipQuickly();
        }

        private void Update()
        {
            if (_root == null)
            {
                return;
            }
            var shade = FindObjectOfType<Movement>();
            bool walkedOff = shade == null || Vector2.Distance(shade.transform.position, _openedFrom) > 2.2f;
            bool dismissed = Time.unscaledTime > _openedAt + 0.3f && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape));
            if (walkedOff || dismissed || !PeddlerStall.InGallery)
            {
                Close();
                return;
            }
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.25f;
                RefreshState();
            }
        }

        // ---- layout ---------------------------------------------------------------------------------------------------

        private void Build()
        {
            var canvas = FrameHud.FindHud();
            if (canvas == null || !NativeUi.Ready)
            {
                return;
            }
            _openedAt = Time.unscaledTime;
            var shade = FindObjectOfType<Movement>();
            _openedFrom = shade != null ? shade.transform.position : Vector3.zero;
            _selected = _hovered = null;
            _tiles.Clear();
            _tabs.Clear();

            // Sits above the cave and below the carved frame. The veil also keeps the Shade from walking off.
            _root = NativeUi.Stretch(NativeUi.Rect("PeddlerPanel", canvas));
            var frame = canvas.Find("ImageSurround");
            if (frame != null)
            {
                _root.SetSiblingIndex(frame.GetSiblingIndex());
            }
            var veil = NativeUi.Image("Veil", _root, FrameHud.SolidSprite(), true);
            veil.color = new Color(0.04f, 0.025f, 0.02f, 0.45f);
            NativeUi.Stretch(veil.rectTransform);

            var card = NativeUi.Image("Card", _root, NativeUi.Card(), true);
            var panel = card.rectTransform;
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(Width, Height);
            panel.anchoredPosition = new Vector2(0f, 2f);

            BuildPeddler(panel);
            BuildTabs(panel);

            _shelf = NativeUi.Place(NativeUi.Rect("Shelf", panel), ShelfX, ShelfY, ShelfWidth, 400f);
            BuildDetails(panel);

            var close = NativeUi.CloseButton(panel, Close);
            var closeRect = (RectTransform)close.transform;
            NativeUi.Place(closeRect, Width - 46f, 0f, 46f, 42f);

            ShowTab(_tab);
            _speech.text = Lines.Greetings[UnityEngine.Random.Range(0, Lines.Greetings.Length)];
        }

        private void BuildPeddler(RectTransform panel)
        {
            var frame = NativeUi.Image("Portrait", panel, NativeUi.Plank(1));
            NativeUi.Place(frame.rectTransform, 36f, 36f, 300f, 300f);
            var window = NativeUi.Rect("Window", frame.transform);
            NativeUi.Stretch(window, 15f);
            window.gameObject.AddComponent<RectMask2D>();
            var peddler = NativeUi.Icon("Peddler", window, CustomAssetLoader.LoadSprite("shop/shopkeeper_idle.png", new Vector2(0.5f, 0.5f)));
            var rect = peddler.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(280f, 350f);
            rect.anchoredPosition = new Vector2(6f, -92f);

            var name = NativeUi.Text("Name", panel, 30f, NativeUi.Accent, TextAlignmentOptions.Center);
            name.text = Lines.PeddlerName;
            NativeUi.Place(name.rectTransform, 36f, 346f, 300f, 40f);

            _speech = NativeUi.Text("Speech", panel, 26f, NativeUi.Ink, TextAlignmentOptions.Top).Fit(18f);
            NativeUi.Place(_speech.rectTransform, 40f, 392f, 292f, 178f);

            _purse = NativeUi.Text("Purse", panel, 22f, NativeUi.Accent, TextAlignmentOptions.Bottom).Fit(16f);
            NativeUi.Place(_purse.rectTransform, 40f, 586f, 292f, 92f);
        }

        private void BuildTabs(RectTransform panel)
        {
            string[] names = { Lines.ShelfFar, Lines.ShelfWares, Lines.ShelfFavors };
            float width = (ShelfWidth - 46f - 20f) / names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var tab = NativeUi.PlankButton("Tab", panel, names[i], 25f, () => ShowTab(index));
                NativeUi.Place((RectTransform)tab.transform, ShelfX + i * (width + 10f), 36f, width, 62f);
                _tabs.Add(tab);
            }
        }

        private void BuildDetails(RectTransform panel)
        {
            var plank = NativeUi.Image("Details", panel, NativeUi.DarkPlank());
            NativeUi.Place(plank.rectTransform, ShelfX, Height - 190f, ShelfWidth, 152f);
            _detailName = NativeUi.Text("Name", plank.transform, 27f, NativeUi.Accent).Fit(18f);
            NativeUi.Place(_detailName.rectTransform, 28f, 22f, ShelfWidth - 56f, 34f);
            _detailLine = NativeUi.Text("Line", plank.transform, 22f, new Color32(0x9B, 0x78, 0x68, 0xFF)).Fit(16f);
            NativeUi.Place(_detailLine.rectTransform, 28f, 60f, ShelfWidth - 270f, 76f);
            _trade = NativeUi.PlankButton("Trade", plank.transform, Lines.PayWord, 25f, TradeSelected);
            NativeUi.Place((RectTransform)_trade.transform, ShelfWidth - 226f, 74f, 200f, 58f);
            _detailPrice = NativeUi.Text("Price", plank.transform, 20f, NativeUi.Accent, TextAlignmentOptions.Center).Fit(14f);
            NativeUi.Place(_detailPrice.rectTransform, ShelfWidth - 240f, 46f, 228f, 28f);
        }

        private void ShowTab(int tab)
        {
            _tab = tab;
            _selected = _hovered = null;
            for (int i = _shelf.childCount - 1; i >= 0; i--)
            {
                Destroy(_shelf.GetChild(i).gameObject);
            }
            _tiles.Clear();

            var offers = tab == 0 ? PeddlerTrade.FarShelf() : tab == 1 ? PeddlerTrade.WaresShelf() : PeddlerTrade.FavorsShelf();
            int columns = tab == 1 ? 6 : 4;
            float size = tab == 1 ? 120f : 168f;
            float gap = (ShelfWidth - columns * size) / Math.Max(1, columns - 1);
            for (int i = 0; i < offers.Count; i++)
            {
                AddTile(offers[i], i % columns * (size + Mathf.Min(gap, 24f)), i / columns * (size * 0.95f + 10f), size);
            }
            for (int i = 0; i < _tabs.Count; i++)
            {
                // The open shelf looks pressed in.
                _tabs[i].Normal = NativeUi.Plank(i == tab ? 3 : 1);
                _tabs[i].Refresh();
            }
            _speech.text = tab == 0 ? (offers.Count == 0 ? Lines.ShelfFarEmpty : Lines.ShelfFarLine) : tab == 1 ? Lines.ShelfWaresLine : Lines.ShelfFavorsLine;
            RefreshState();
        }

        private void AddTile(Offer offer, float x, float y, float size)
        {
            var tile = new Tile { Offer = offer };
            var square = NativeUi.Image("Tile", _shelf, NativeUi.GameSprite("drawbutton_0001"), true);
            NativeUi.Place(square.rectTransform, x, y, size, size * 0.91f);
            var button = square.gameObject.AddComponent<NativeButton>();
            button.Background = square;
            button.Normal = NativeUi.GameSprite("drawbutton_0001");
            button.Hover = NativeUi.GameSprite("drawbutton_0002");
            button.Clicked = () =>
            {
                _selected = offer;
                RefreshState();
            };
            button.Entered = () =>
            {
                _hovered = offer;
                RefreshState();
            };
            button.Exited = () =>
            {
                if (_hovered == offer)
                {
                    _hovered = null;
                    RefreshState();
                }
            };

            tile.Glyph = NativeUi.Icon("Glyph", square.transform, Icons.Resolve(offer.Icon, out _));
            NativeUi.Stretch(tile.Glyph.rectTransform, size * 0.22f);
            tile.Glyph.rectTransform.anchoredPosition += new Vector2(-size * 0.02f, size * 0.05f);

            var price = NativeUi.Text("Price", square.transform, size * 0.155f, NativeUi.Ink, TextAlignmentOptions.BottomRight);
            NativeUi.Stretch(price.rectTransform, size * 0.155f);
            tile.Price = price;

            tile.Sweep = NativeUi.Image("Rest", square.transform, FrameHud.SolidSprite());
            NativeUi.Stretch(tile.Sweep.rectTransform, size * 0.125f);
            tile.Sweep.type = Image.Type.Filled;
            tile.Sweep.fillMethod = Image.FillMethod.Radial360;
            tile.Sweep.fillOrigin = (int)Image.Origin360.Top;
            tile.Sweep.fillClockwise = false;
            tile.Sweep.color = new Color(0.10f, 0.065f, 0.06f, 0.75f);


            tile.Ring = NativeUi.Image("Ring", square.transform, NativeUi.GameSprite("auswahlbild"));
            tile.Ring.fillCenter = false;
            NativeUi.Stretch(tile.Ring.rectTransform, size * 0.06f);
            tile.Ring.color = Color.white;
            _tiles.Add(tile);
        }

        // ---- state ---------------------------------------------------------------------------------------------------------

        private void RefreshState()
        {
            if (_root == null)
            {
                return;
            }
            _purse.text = Lines.Purse(RelicBag.Coal, RelicBag.Moss);
            foreach (var tile in _tiles)
            {
                bool gone = tile.Offer.IsGone != null && tile.Offer.IsGone();
                bool poor = RelicBag.Coal < tile.Offer.Coal || RelicBag.Moss < tile.Offer.Moss;
                float rest = tile.Offer.Rest != null ? tile.Offer.Rest() : 0f;
                tile.Glyph.color = gone ? new Color(0f, 0f, 0f, 0.28f) : poor ? new Color(1f, 1f, 1f, 0.72f) : Color.white;
                tile.Price.text = gone ? string.Empty : (tile.Offer.Coal > 0 ? tile.Offer.Coal : tile.Offer.Moss).ToString();
                tile.Price.alpha = poor ? 0.45f : 1f;
                tile.Sweep.enabled = rest > 0.001f;
                tile.Sweep.fillAmount = rest;
                tile.Ring.enabled = tile.Offer == _selected;
            }

            var shown = _hovered ?? _selected;
            bool any = shown != null;
            _detailName.text = any ? shown.Name : string.Empty;
            bool sold = any && shown.IsGone != null && shown.IsGone();
            _detailLine.text = !any ? string.Empty : sold ? (_tab == 1 ? Lines.Owned : Lines.Gone) : shown.Line;
            _detailPrice.text = any && !sold ? shown.Price : string.Empty;
            _trade.gameObject.SetActive(_selected != null && shown == _selected && !sold);
            if (_selected != null)
            {
                _trade.Interactable = RelicBag.Coal >= _selected.Coal && RelicBag.Moss >= _selected.Moss
                    && (_selected.Rest == null || _selected.Rest() <= 0f);
            }
            // Hovering something unselected still shows its price on the right.
            _detailPrice.rectTransform.anchoredPosition = new Vector2(ShelfWidth - 240f, _trade.gameObject.activeSelf ? -44f : -88f);
        }

        private void TradeSelected()
        {
            if (_selected == null)
            {
                return;
            }
            var offer = _selected;
            _speech.text = PeddlerTrade.Trade(offer, out bool done);
            if (done && _tab != 2)
            {
                _selected = null;
            }
            RefreshState();
        }
    }
}
