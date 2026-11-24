using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TheLongingDecrypted.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TheLongingDecrypted.Peddler
{
    /// <summary>
    /// Sets up the Peddler's stall and the coal seam in the Pillared Gallery. The stall is a regular game interactable:
    /// the Shade walks up to it and the vanilla interaction plank appears.
    /// </summary>
    [HarmonyPatch]
    internal static class PeddlerStall
    {
        private const string GalleryScene = "gallery";
        private const int MaxFloorCoal = 5;

        // Shares its place in the order with the Face; nothing else in the gallery comes close.
        private const int TalkPriority = 666;

        private static readonly Vector3 StallPosition = new Vector3(0.50f, -0.55f, 0f);
        private static readonly Vector3 SeamPosition = new Vector3(-2.25f, -1.72f, 0f);

        private static readonly FieldInfo DropPrefabField = AccessTools.Field(typeof(TropfenSpawn), "INFGMCIGJCF");
        private static readonly FieldInfo LandedCoalField = AccessTools.Field(typeof(DestroyTropfenScript), "ACPKKHCFJGE");
        private static readonly FieldInfo LandingField = AccessTools.Field(typeof(DestroyTropfenScript), "JONNKJEFNMH");
        private static readonly FieldInfo SplashField = AccessTools.Field(typeof(DestroyTropfenScript), "MLHPFOOJLAH");
        private static readonly FieldInfo InCameraField = AccessTools.Field(typeof(DestroyTropfenScript), "KBBBPNCKBEL");
        private static readonly FieldInfo MinRollField = AccessTools.Field(typeof(DestroyTropfenScript), "HOFGOHBBDMN");
        private static readonly FieldInfo MaxRollField = AccessTools.Field(typeof(DestroyTropfenScript), "CIEMOBGDACA");
        private static readonly FieldInfo ThresholdField = AccessTools.Field(typeof(DestroyTropfenScript), "KIADJDOIPIM");
        private static readonly FieldInfo PlankField = AccessTools.Field(typeof(GameManagerScript), "BFABPJGACKB");

        // Which prompt the plank currently shows. Cleared so the next prompt redraws its own picture.
        private static readonly FieldInfo DrawnIconField = AccessTools.Field(typeof(GameManagerScript), "OCPLNBACHIE");

        private static readonly List<DestroyTropfenScript> Catchers = new List<DestroyTropfenScript>();
        private static GameObject _dropPrefab;
        private static Transform _keeper;
        private static float _nextDrop;

        public static bool InGallery => SceneManager.GetActiveScene().name == GalleryScene;

        public static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _keeper = null;
            Catchers.Clear();
            if (scene.name != GalleryScene)
            {
                return;
            }
            try
            {
                BuildStall();
                PrepareSeam();
                if (Plugin.Session.ActiveConfig.AutoHints)
                {
                    Plugin.Session.ScoutShopLocationsAsync();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Could not set up the Peddler: {ex}");
            }
        }

        private static void BuildStall()
        {
            var stall = Sketch("PeddlerStall", null, StallPosition, "shop/shop_stall.png", 2);
            // Behind the counter, in front of the window.
            _keeper = Sketch("Peddler", stall.transform, StallPosition, "shop/shopkeeper_idle.png", 1).transform;

            var glow = new GameObject("PeddlerLantern").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(stall.transform, false);
            glow.transform.localPosition = new Vector3(0.94f, 0.2f, -0.1f);
            glow.transform.localScale = Vector3.one * 2.4f;
            glow.sprite = Cosmetics.SoftGlow.Sprite;
            glow.color = new Color(1f, 0.72f, 0.36f, 0.22f);
            glow.sortingOrder = 3;

            Sketch("CoalSeam", null, SeamPosition, "shop/coal_vein_infinite.png", 3);

            var spot = new GameObject("PeddlerInteraction");
            spot.transform.position = new Vector3(StallPosition.x, -1.6f, 0f);
            var talk = spot.AddComponent<PeddlerInteractable>();
            talk.Prepare(TalkPriority);
        }

        private static GameObject Sketch(string name, Transform parent, Vector3 position, string asset, int order)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = CustomAssetLoader.LoadSprite(asset, new Vector2(0.5f, 0.5f));
            renderer.sortingOrder = order;
            return go;
        }

        // ---- falling coal -------------------------------------------------------------------------------------------

        private static void PrepareSeam()
        {
            var spawner = UnityEngine.Object.FindObjectOfType<TropfenSpawn>();
            _dropPrefab = spawner != null ? DropPrefabField?.GetValue(spawner) as GameObject : null;

            var all = UnityEngine.Object.FindObjectsOfType<DestroyTropfenScript>();
            GameObject coal = null, landing = null, splash = null;
            foreach (var catcher in all)
            {
                coal = coal != null ? coal : LandedCoalField?.GetValue(catcher) as GameObject;
                landing = landing != null ? landing : LandingField?.GetValue(catcher) as GameObject;
                splash = splash != null ? splash : SplashField?.GetValue(catcher) as GameObject;
            }
            foreach (var catcher in all)
            {
                float x = catcher.transform.position.x;
                // Around the seam, but not in front of the counter.
                if (x < SeamPosition.x - 2.2f || x > SeamPosition.x + 1.4f)
                {
                    continue;
                }
                FillIfEmpty(LandedCoalField, catcher, coal);
                FillIfEmpty(LandingField, catcher, landing);
                FillIfEmpty(SplashField, catcher, splash);
                Catchers.Add(catcher);
            }
            _nextDrop = Time.time + 6f;
        }

        private static void FillIfEmpty(FieldInfo field, object target, GameObject value)
        {
            if (field != null && value != null && field.GetValue(target) as GameObject == null)
            {
                field.SetValue(target, value);
            }
        }

        private static void DropCoal()
        {
            if (Time.time < _nextDrop || _dropPrefab == null || Catchers.Count == 0)
            {
                return;
            }
            _nextDrop = Time.time + UnityEngine.Random.Range(20f, 32f);

            int onFloor = 0;
            var free = new List<DestroyTropfenScript>();
            foreach (var coal in UnityEngine.Object.FindObjectsOfType<Interactable_GrabTropfen>())
            {
                if (coal.IsInteractableAtAll())
                {
                    onFloor++;
                }
            }
            if (onFloor >= MaxFloorCoal)
            {
                return;
            }
            foreach (var catcher in Catchers)
            {
                var lying = catcher != null ? catcher.GetComponentInChildren<Interactable_GrabTropfen>(false) : null;
                if (catcher != null && (lying == null || !lying.IsInteractableAtAll()))
                {
                    free.Add(catcher);
                }
            }
            if (free.Count == 0)
            {
                return;
            }

            var chosen = free[UnityEngine.Random.Range(0, free.Count)];
            InCameraField?.SetValue(chosen, true);
            MinRollField?.SetValue(chosen, 8);
            MaxRollField?.SetValue(chosen, 10);
            ThresholdField?.SetValue(chosen, 5);
            var from = chosen.transform.parent != null
                ? chosen.transform.parent.position
                : new Vector3(chosen.transform.position.x, 1.85f, 0f);
            UnityEngine.Object.Instantiate(_dropPrefab, from, Quaternion.identity);
        }

        public static void Tick()
        {
            if (_keeper == null)
            {
                return;
            }
            // Slow breathing rather than bobbing.
            float breath = Mathf.Sin(Time.time * 1.3f);
            _keeper.localPosition = new Vector3(0f, -0.05f + breath * 0.012f, 0f);
            _keeper.localScale = new Vector3(1f - breath * 0.004f, 1f + breath * 0.008f, 1f);
            DropCoal();
        }

        // ---- prompt -----------------------------------------------------------------------------------------------------

        [HarmonyPatch(typeof(GameManagerScript), "CMDBEAFBOMH")]
        [HarmonyPostfix]
        private static void NamePeddlerPrompt(GameManagerScript __instance)
        {
            if (!InGallery || !(__instance.GetCurrentInteractable() is PeddlerInteractable))
            {
                return;
            }
            var plank = PlankField?.GetValue(__instance) as GameObject;
            if (plank == null)
            {
                return;
            }
            // An open hand suits a trade better than the Face's frown.
            var hand = NativeUi.GameSprite("icons_0005");
            var image = plank.GetComponent<Image>();
            if (hand != null && image != null)
            {
                image.sprite = hand;
                DrawnIconField?.SetValue(__instance, -1);
            }
            foreach (var label in plank.GetComponentsInChildren<Text>(true))
            {
                if (label.name == "InteractionText")
                {
                    label.text = Lines.TalkToPeddler;
                }
            }
        }
    }

    /// <summary>The spot in front of the counter.</summary>
    internal sealed class PeddlerInteractable : Interactable
    {
        private bool _ready;

        public void Prepare(int priority)
        {
            KKFIJEOECEM = priority;
            MGNCEFEMFJG = priority;
            EADPODFFHMB = 1.35f;
            BGCFAHJOGOE = CAMKNGOHEIK.triggerAction;
            state = new InteractableStateClass { outcomes = new InteractableOutcome[0] };
            AIHFFFNKDNN = true;
        }

        private void Update()
        {
            if (_ready)
            {
                return;
            }
            var game = FindObjectOfType<GameManagerScript>();
            if (game != null && GameManagerScript.IsGameRunning())
            {
                IOBFMOECLLM = game;
                game.RefreshInteractables();
                _ready = true;
            }
        }

        public override bool IsInteractableAtAll()
        {
            return _ready && !PeddlerPanel.IsOpen && !PeddlerPanel.JustClosed;
        }

        public override void Interact()
        {
            if (PeddlerPanel.IsOpen || PeddlerPanel.JustClosed)
            {
                return;
            }
            if (IOBFMOECLLM != null)
            {
                IOBFMOECLLM.GetInputManager()?.StopAutoMoving();
            }
            PeddlerPanel.Open();
        }
    }
}
