using TheLongingDecrypted.Patches;
using TheLongingDecrypted.Peddler;
using TheLongingDecrypted.Relics;
using TheLongingDecrypted.UI;
using UnityEngine;

namespace TheLongingDecrypted
{
    /// <summary>Drives everything that has to happen on the main thread every frame.</summary>
    internal sealed class ModRuntime : MonoBehaviour
    {
        private float _nextMilestonePoll;

        private void Update()
        {
            Plugin.Session?.PumpMainThread();
            PeddlerStall.Tick();
            ShadeSpeech.Tick();
            if (GameManagerScript.IsGameRunning() && !FileBasedPrefs.BlockNewSaves)
            {
                RelicBag.WatchPickups();
            }
            if (Time.unscaledTime >= _nextMilestonePoll)
            {
                _nextMilestonePoll = Time.unscaledTime + 3f;
                LocationCheckPatches.PollMilestonePrefs();
            }
        }
    }
}
