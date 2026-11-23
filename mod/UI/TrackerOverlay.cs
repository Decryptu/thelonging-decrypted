using System;
using TheLongingDecrypted.Patches;
using UnityEngine;

namespace TheLongingDecrypted.UI
{
    /// <summary>
    /// Compact IMGUI Archipelago connection panel and tracker HUD (toggle with F8).
    /// </summary>
    public sealed class TrackerOverlay : MonoBehaviour
    {
        private bool _visible = true;
        private Rect _windowRect = new Rect(18f, 18f, 360f, 470f);
        private string _hostInput = "archipelago.gg";
        private string _portInput = "38281";
        private string _slotInput = "Shade";
        private string _passwordInput = "";
        private float _nextMilestonePoll;

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

        private void OnGUI()
        {
            if (Plugin.Delivery != null && Plugin.Delivery.IsDarknessTrapActive)
            {
                var prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.72f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            if (!_visible)
            {
                DrawCompactBadge();
                return;
            }

            GUI.backgroundColor = new Color(0.14f, 0.12f, 0.10f, 0.96f);
            GUI.contentColor = new Color(0.95f, 0.91f, 0.78f, 1f);
            _windowRect = GUI.Window(400400, _windowRect, DrawMainWindow, "The Longing Decrypted [F8]");
        }

        private void DrawCompactBadge()
        {
            bool connected = Plugin.Session != null && Plugin.Session.IsConnected;
            GUI.backgroundColor = connected
                ? new Color(0.14f, 0.28f, 0.50f, 0.92f)
                : new Color(0.24f, 0.19f, 0.14f, 0.90f);
            GUI.contentColor = new Color(0.98f, 0.93f, 0.72f, 1f);
            string label = connected
                ? $"AP: {Plugin.Session.CheckedCount}/{Plugin.Session.TotalLocations} | {TimeAndMovementPatches.GetEffectiveTimeRate():0}x [F8]"
                : "AP Disconnected [F8]";
            if (GUI.Button(new Rect(14f, 14f, 225f, 26f), label))
            {
                _visible = true;
            }
        }

        private void DrawMainWindow(int id)
        {
            var session = Plugin.Session;
            bool connected = session != null && session.IsConnected;

            GUILayout.BeginVertical();

            // Connection status bar
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
                GUILayout.Space(4f);
                if (GUILayout.Button("Disconnect"))
                {
                    session.Disconnect();
                }
            }

            GUILayout.Space(6f);
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

        private static void DrawLabeledField(string label, ref string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(44f));
            value = GUILayout.TextField(value ?? string.Empty);
            GUILayout.EndHorizontal();
        }
    }
}
