using System;
using HarmonyLib;
using TheLongingDecrypted.Cosmetics;
using TheLongingDecrypted.Patches;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLongingDecrypted.UI
{
    /// <summary>
    /// The card for joining an Archipelago room. Opens from the far worlds square in the frame, from the plank on the
    /// title screen, or with F8. Server fields stay plain and readable; everything else speaks with the Shade's voice.
    /// </summary>
    [HarmonyPatch]
    internal sealed class MultiworldPanel : MonoBehaviour
    {
        private const float Width = 800f;
        private const float Height = 690f;

        private static MultiworldPanel _instance;

        private Canvas _canvas;
        private RectTransform _titlePlank;
        private RectTransform _card;
        private TMP_InputField _host;
        private TMP_InputField _port;
        private TMP_InputField _slot;
        private TMP_InputField _password;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _details;
        private bool _builtConnected;
        private float _nextRefresh;

        private static float _closedAt = -10f;

        public static bool JustClosed => Time.unscaledTime - _closedAt < 0.4f;

        public static bool IsOpen => _instance != null && _instance._card != null;

        private void Awake()
        {
            _instance = this;
        }

        public static void Toggle()
        {
            if (_instance == null)
            {
                return;
            }
            if (IsOpen)
            {
                _instance.Close();
            }
            else
            {
                _instance.Open();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                Toggle();
            }
            UpdateTitlePlank();
            if (_card == null)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                Close();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                FocusNext();
            }
            if (Input.GetKeyDown(KeyCode.Return) && !Plugin.Session.IsConnected)
            {
                Connect();
            }
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 0.3f;
                if (Plugin.Session.IsConnected != _builtConnected)
                {
                    Close();
                    Open();
                }
                else
                {
                    RefreshText();
                }
            }
        }

        // ---- canvas -----------------------------------------------------------------------------------------------------

        private bool EnsureCanvas()
        {
            if (!NativeUi.Ready || NativeUi.Card() == null)
            {
                return false;
            }
            if (_canvas != null)
            {
                return true;
            }
            var go = new GameObject("DecryptedCanvas");
            DontDestroyOnLoad(go);
            go.layer = LayerMask.NameToLayer("UI");
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1001;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            return true;
        }

        // Escape closes the open card first and must not open the menu behind it.
        [HarmonyPatch(typeof(GameManagerScript), nameof(GameManagerScript.ClickMenuInteraction))]
        [HarmonyPrefix]
        private static bool KeepMenuClosed()
        {
            return !Input.GetKeyDown(KeyCode.Escape)
                || !(IsOpen || JustClosed || Peddler.PeddlerPanel.IsOpen || Peddler.PeddlerPanel.JustClosed);
        }

        private static bool OnTitle => !GameManagerScript.IsGameRunning() && SceneManager.GetActiveScene().buildIndex < 3;

        private void UpdateTitlePlank()
        {
            bool wanted = OnTitle && _card == null;
            if (!wanted)
            {
                if (_titlePlank != null)
                {
                    _titlePlank.gameObject.SetActive(false);
                }
                return;
            }
            if (_titlePlank == null)
            {
                if (!EnsureCanvas() || NativeUi.Plank(1) == null)
                {
                    return;
                }
                var plank = NativeUi.PlankButton("FarWorlds", _canvas.transform, Lines.MultiworldName, 30f, Open);
                _titlePlank = (RectTransform)plank.transform;
                _titlePlank.anchorMin = _titlePlank.anchorMax = new Vector2(1f, 0f);
                _titlePlank.pivot = new Vector2(1f, 0f);
                _titlePlank.anchoredPosition = new Vector2(-118f, 104f);
                _titlePlank.sizeDelta = new Vector2(330f, 92f);
            }
            _titlePlank.gameObject.SetActive(true);
        }

        // ---- card -----------------------------------------------------------------------------------------------------------

        private void Open()
        {
            if (_card != null || !EnsureCanvas())
            {
                return;
            }
            _builtConnected = Plugin.Session.IsConnected;
            var card = NativeUi.Image("MultiworldCard", _canvas.transform, NativeUi.Card(), true);
            _card = card.rectTransform;
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(Width, Height);

            var title = NativeUi.Text("Title", _card, 36f, NativeUi.Accent, TextAlignmentOptions.Top);
            title.text = Lines.MultiworldName;
            NativeUi.Place(title.rectTransform, 60f, 34f, Width - 120f, 48f);

            var close = NativeUi.CloseButton(_card, Close);
            NativeUi.Place((RectTransform)close.transform, Width - 46f, 0f, 46f, 42f);

            _status = NativeUi.Text("Status", _card, 24f, NativeUi.Ink, TextAlignmentOptions.Top).Fit(16f);
            NativeUi.Place(_status.rectTransform, 50f, 86f, Width - 100f, 60f);

            if (_builtConnected)
            {
                BuildConnected();
            }
            else
            {
                BuildFields();
            }
            BuildHabits();
            RefreshText();
        }

        private void Close()
        {
            if (_card != null)
            {
                Destroy(_card.gameObject);
                _card = null;
                _closedAt = Time.unscaledTime;
            }
        }

        private void BuildFields()
        {
            _host = Field("Server", Plugin.DefaultHostConfig.Value, 0, false);
            _port = Field("Port", Plugin.DefaultPortConfig.Value.ToString(), 1, false);
            _slot = Field("Player", Plugin.DefaultSlotConfig.Value, 2, false);
            _password = Field("Password", Plugin.DefaultPasswordConfig.Value, 3, true);
            var connect = NativeUi.PlankButton("Connect", _card, "Listen", 30f, Connect);
            NativeUi.Place((RectTransform)connect.transform, Width * 0.5f - 160f, 428f, 320f, 84f);
        }

        private void BuildConnected()
        {
            _details = NativeUi.Text("Details", _card, 25f, NativeUi.Ink, TextAlignmentOptions.Top).Fit(16f);
            _details.lineSpacing = 12f;
            NativeUi.Place(_details.rectTransform, 60f, 156f, Width - 120f, 260f);
            var leave = NativeUi.PlankButton("Leave", _card, "Stop listening", 28f, () => Plugin.Session.Disconnect());
            NativeUi.Place((RectTransform)leave.transform, Width * 0.5f - 160f, 428f, 320f, 84f);
        }

        /// <summary>Things that only concern how the cave looks to this player.</summary>
        private void BuildHabits()
        {
            var line = NativeUi.Image("Line", _card, FrameHud.SolidSprite());
            line.color = NativeUi.Muted;
            NativeUi.Place(line.rectTransform, 70f, 536f, Width - 140f, 2f);

            NativeButton shade = null;
            shade = NativeUi.PlankButton("Tint", _card, string.Empty, 24f, () =>
            {
                Wardrobe.Tint++;
                shade.Label.text = "My color: " + Wardrobe.Tints[Wardrobe.Tint].Name;
            });
            shade.Label.text = "My color: " + Wardrobe.Tints[Wardrobe.Tint].Name;
            NativeUi.Place((RectTransform)shade.transform, 50f, 560f, 340f, 84f);

            NativeButton books = null;
            string BooksLabel() => FanficBookPatches.IsAo3ModeActive ? "My books: indecent" : "My books: the classics";
            books = NativeUi.PlankButton("Books", _card, BooksLabel(), 24f, () =>
            {
                FanficBookPatches.ToggleAo3Mode();
                books.Label.text = BooksLabel();
            });
            NativeUi.Place((RectTransform)books.transform, Width - 390f, 560f, 340f, 84f);
        }

        private TMP_InputField Field(string label, string value, int row, bool secret)
        {
            float y = 158f + row * 64f;
            var name = NativeUi.Text("Label", _card, 25f, NativeUi.Accent, TextAlignmentOptions.MidlineRight);
            name.text = label;
            NativeUi.Place(name.rectTransform, 60f, y, 170f, 54f);

            var box = NativeUi.Image("Box", _card, NativeUi.DarkPlank(), true);
            NativeUi.Place(box.rectTransform, 250f, y, 460f, 54f);
            var area = NativeUi.Stretch(NativeUi.Rect("Area", box.transform), 14f);
            area.gameObject.AddComponent<RectMask2D>();
            var text = NativeUi.Text("Text", area, 24f, NativeUi.Accent, TextAlignmentOptions.MidlineLeft);
            text.enableWordWrapping = false;
            text.richText = false;
            NativeUi.Stretch(text.rectTransform);

            var field = box.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.fontAsset = NativeUi.Font;
            field.pointSize = 24f;
            field.caretColor = NativeUi.Accent;
            field.customCaretColor = true;
            field.selectionColor = new Color(0.74f, 0.54f, 0.46f, 0.35f);
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = secret ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            field.text = value ?? string.Empty;
            return field;
        }

        private void FocusNext()
        {
            if (_host == null)
            {
                return;
            }
            var order = new[] { _host, _port, _slot, _password };
            int at = Array.FindIndex(order, f => f != null && f.isFocused);
            var next = order[(at + 1) % order.Length];
            EventSystem.current?.SetSelectedGameObject(next.gameObject);
            next.ActivateInputField();
        }

        private void Connect()
        {
            if (_host == null || !int.TryParse(_port.text.Trim(), out int port))
            {
                return;
            }
            Plugin.DefaultHostConfig.Value = _host.text.Trim();
            Plugin.DefaultPortConfig.Value = port;
            Plugin.DefaultSlotConfig.Value = _slot.text.Trim();
            Plugin.DefaultPasswordConfig.Value = _password.text;
            Plugin.Session.Connect(_host.text.Trim(), port, _slot.text.Trim(), _password.text);
            RefreshText();
        }

        private void RefreshText()
        {
            var session = Plugin.Session;
            _status.text = session.IsConnected ? Lines.MultiworldAwake : session.StatusMessage == "Disconnected" ? Lines.MultiworldAsleep : session.StatusMessage;
            if (!_builtConnected || _details == null)
            {
                return;
            }
            var config = session.ActiveConfig;
            string text = $"{session.ConnectedSlot}, {session.ConnectedHost}\n{Lines.Goal(config.Goal)}\n";
            if (config.Goal == "crystal_book_hunt")
            {
                text += Lines.Hunt(Plugin.Delivery.ReceivedCrystals, config.RequiredCrystals, Plugin.Delivery.ReceivedBooks, config.RequiredBooks) + "\n";
            }
            text += Lines.Found(session.CheckedCount, session.TotalLocations) + "\n";
            text += $"{Lines.Pace(TimeAndMovementPatches.WalkSpeedTier)} {Lines.TimeRate(Mathf.RoundToInt(TimeAndMovementPatches.GetEffectiveTimeRate()))}";
            foreach (var note in Plugin.Delivery.LatestNotes(2))
            {
                text += $"\n<color=#BC8A75><size=80%>{note}</size></color>";
            }
            _details.text = text;
        }
    }
}
