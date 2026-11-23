using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLongingDecrypted.Patches
{
    [HarmonyPatch]
    internal static class WaypointRecallPatches
    {
        private const string VisitedHomeKey = "AP_VISITED_HOME";
        private const int MaxRecallRing = 3;
        private static GameObject _card;
        private static Text _label;
        private static string _feedback;
        private static GameManagerScript _recalling;
        private static string _recallSave;
        private static int _destination;
        private static int _waypoint;

        internal static int RecallTier => Mathf.Clamp(FileBasedPrefs.GetInt("AP_WAYPOINT_RECALL", 0), 0, MaxRecallRing);
        private static bool HasSlot => !string.IsNullOrEmpty(Plugin.Session?.ConnectedSlot);
        private static bool IsShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        internal static int CavernRing(int scene)
        {
            switch (scene)
            {
                // Home, the King's Hall, and the adjoining entrance halls.
                case 3: case 4: case 6: case 7: case 8: case 21: case 24: case 54:
                    return 1;
                // Great Halls, grottoes, and the Library.
                case 9: case 10: case 11: case 12: case 15: case 16: case 17:
                case 18: case 19: case 20: case 22: case 23: case 25: case 26:
                case 46: case 47: case 48: case 49: case 50: case 51: case 52:
                case 53: case 55: case 56: case 61: case 72:
                    return 2;
                // Labyrinth and Eternity, mines, Great Cave, tower, and Well Shafts.
                case 14: case 27: case 28: case 29: case 30: case 31: case 32:
                case 33: case 34: case 35: case 36: case 37: case 38: case 39:
                case 40: case 41: case 42: case 43: case 44: case 45: case 60:
                case 62: case 63: case 64: case 65: case 66: case 67: case 68: case 69:
                    return 3;
                default:
                    return 0;
            }
        }

        [HarmonyPatch(typeof(GameManagerScript), "EKFGJCEHPLG")]
        [HarmonyPrefix]
        private static void RememberWaypointPanelState(GameManagerScript __instance, out bool __state)
        {
            __state = __instance.ODEFEGGOFKP;
        }

        [HarmonyPatch(typeof(GameManagerScript), "EKFGJCEHPLG")]
        [HarmonyPostfix]
        private static void ShowMobilityInWaypointPanel(GameManagerScript __instance, Transform ___FEGOHOGGOHE, bool __state)
        {
            if (!HasSlot || !__instance.ODEFEGGOFKP || !___FEGOHOGGOHE)
            {
                return;
            }

            if (!_card || _card.transform.parent != ___FEGOHOGGOHE)
            {
                _card = new GameObject("Cavern Travel", typeof(RectTransform));
                _card.transform.SetParent(___FEGOHOGGOHE, false);
                var backdrop = _card.AddComponent<Image>();
                backdrop.color = new Color(0.11f, 0.09f, 0.07f, 0.94f);
                backdrop.raycastTarget = false;
                var border = _card.AddComponent<Outline>();
                border.effectColor = new Color(0.62f, 0.49f, 0.27f, 0.92f);
                border.effectDistance = new Vector2(2f, -2f);
                var cardRect = _card.GetComponent<RectTransform>();
                // Attach cleanly to the upper-right edge of the foldout so slot action buttons stay unobstructed.
                cardRect.anchorMin = new Vector2(1.02f, 0.80f);
                cardRect.anchorMax = new Vector2(1.92f, 0.96f);
                cardRect.offsetMin = cardRect.offsetMax = Vector2.zero;

                var labelObject = new GameObject("Cavern Travel Text", typeof(RectTransform));
                labelObject.transform.SetParent(_card.transform, false);
                _label = labelObject.AddComponent<Text>();
                _label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _label.fontSize = 18;
                _label.alignment = TextAnchor.MiddleCenter;
                _label.color = new Color(0.95f, 0.89f, 0.72f);
                _label.raycastTarget = false;
                var shadow = labelObject.AddComponent<Shadow>();
                shadow.effectDistance = new Vector2(1f, -1f);
                var textRect = _label.rectTransform;
                textRect.anchorMin = new Vector2(0.04f, 0.06f);
                textRect.anchorMax = new Vector2(0.96f, 0.94f);
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            }

            if (!__state)
            {
                _feedback = null;
            }
            UpdateLabel();
        }

        internal static void RefreshMobilityLabel()
        {
            _feedback = null;
            UpdateLabel();
        }

        private static void UpdateLabel()
        {
            if (!_label)
            {
                return;
            }

            int ring = RecallTier;
            _label.text = $"Walking pace {TimeAndMovementPatches.UnlockedWalkMultiplier:0.##}×\nRecall ring {ring}/{MaxRecallRing}\n"
                + (_feedback ?? (ring == 0 ? "Find a Waypoint Recall to travel." : "Shift-click Go or Home to recall."));
        }

        [HarmonyPatch(typeof(GameManagerScript), nameof(GameManagerScript.GoHome))]
        [HarmonyPrefix]
        private static bool RecallHome(GameManagerScript __instance)
        {
            if (!HasSlot || !IsShiftHeld)
            {
                return true;
            }

            TryRecall(__instance, 1, requireFoldoutOpen: false);
            return false;
        }

        [HarmonyPatch(typeof(GameManagerScript), nameof(GameManagerScript.GoToSaveSlotPlace))]
        [HarmonyPrefix]
        private static bool RecallSelectedSlot(GameManagerScript __instance, int OFLAKBJMDAL)
        {
            if (!HasSlot || !IsShiftHeld || OFLAKBJMDAL < 1 || OFLAKBJMDAL > 16)
            {
                return true;
            }

            TryRecall(__instance, OFLAKBJMDAL, requireFoldoutOpen: true);
            return false;
        }

        [HarmonyPatch(typeof(GameManagerScript), nameof(GameManagerScript.WaypointGotoInteraction))]
        [HarmonyPrefix]
        private static bool RecallSavedWaypoint(GameManagerScript __instance, BaseEventData NHKKAONFPNH)
        {
            if (!HasSlot || !IsShiftHeld)
            {
                return true;
            }

            var selected = NHKKAONFPNH?.selectedObject;
            if (!selected || !int.TryParse(selected.transform.parent?.name, out int slot))
            {
                return true;
            }

            TryRecall(__instance, slot, requireFoldoutOpen: true);
            return false;
        }

        private static void TryRecall(GameManagerScript game, int slot, bool requireFoldoutOpen)
        {
            if ((requireFoldoutOpen && !game.ODEFEGGOFKP) || !GameManagerScript.IsGameRunning() || _recalling
                || FileBasedPrefs.BlockNewSaves || FileBasedPrefs.HasKey("WasInside")
                || FileBasedPrefs.GetInt("FakeHudOff", 0) == 0
                || CavernRing(SceneManager.GetActiveScene().buildIndex) == 0
                || game.GetInputManager()?.GetMovementScript()?.IsInSpecialState() != false)
            {
                Deny("Shade cannot recall from here.");
                return;
            }

            // In vanilla the home bookmark is built in; the other slots are recorded on foot.
            int scene = slot == 1 ? 4 : FileBasedPrefs.GetInt("aAasf87" + slot, -1);
            int waypoint = slot == 1 ? 90 : FileBasedPrefs.GetInt("wpASf87532" + slot, -1);
            if (waypoint < 0 || (slot == 1 && !FileBasedPrefs.HasKey(VisitedHomeKey)))
            {
                Deny("Visit and mark this waypoint first.");
                return;
            }

            int ring = CavernRing(scene);
            if (ring == 0)
            {
                Deny("This place lies outside the recall rings.");
                return;
            }
            if (ring > RecallTier)
            {
                Deny($"This waypoint needs recall ring {ring}.");
                return;
            }

            _recalling = game;
            _recallSave = FileBasedPrefs.GetSaveFilePath();
            _destination = scene;
            _waypoint = waypoint;
            game.GetInputManager().StopAutoMoving();
            if (game.ODEFEGGOFKP)
            {
                game.CloseFoldout();
            }
            game.LoadNewScene(scene);
        }

        private static void Deny(string message)
        {
            _feedback = message;
            UpdateLabel();
            if (!_label || !_label.gameObject.activeInHierarchy)
            {
                Plugin.Delivery?.PushNotification(message);
            }
        }

        [HarmonyPatch(typeof(GameManagerScript), "JIJFJAAELBE")]
        [HarmonyPostfix]
        private static void SpawnAtSavedWaypoint(GameManagerScript __instance, ref Vector3 __result)
        {
            int scene = SceneManager.GetActiveScene().buildIndex;
            if (HasSlot && scene == 4 && !FileBasedPrefs.BlockNewSaves && !FileBasedPrefs.HasKey(VisitedHomeKey))
            {
                FileBasedPrefs.SetInt(VisitedHomeKey, 1);
            }

            if (_recalling != __instance)
            {
                return;
            }

            _recalling = null;
            if (scene == _destination && HasSlot && _recallSave == FileBasedPrefs.GetSaveFilePath()
                && CavernRing(scene) <= RecallTier)
            {
                var waypoints = __instance.GetWaypointManager()?.waypoints;
                if (waypoints != null && _waypoint < waypoints.Count && waypoints[_waypoint] != null)
                {
                    __result = waypoints[_waypoint].pos;
                }
            }
        }
    }
}
