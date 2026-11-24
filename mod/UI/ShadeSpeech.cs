using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheLongingDecrypted.UI
{
    /// <summary>Lets the Shade speak through the game's own dialogue card.</summary>
    internal static class ShadeSpeech
    {
        private static readonly Queue<string> Thoughts = new Queue<string>();

        public static void Think(string line)
        {
            if (!string.IsNullOrEmpty(line) && Thoughts.Count < 4)
            {
                Thoughts.Enqueue(line);
            }
        }

        /// <summary>Speaks as soon as nothing else is being said or done.</summary>
        public static void Tick()
        {
            if (Thoughts.Count == 0 || !GameManagerScript.IsGameRunning() || FileBasedPrefs.BlockNewSaves)
            {
                return;
            }
            var game = Object.FindObjectOfType<GameManagerScript>();
            var dialog = game != null ? game.GetDialogeSystem() : null;
            if (dialog == null || dialog.DMActive() || game.AreOptionsActive() || MultiworldPanel.IsOpen || Peddler.PeddlerPanel.IsOpen)
            {
                return;
            }

            string line = Thoughts.Dequeue();
            dialog.ShowDialogeWithoutOptions(null, new Button.ButtonClickedEvent(), 1, "coal");
            // The card is laid out by the game; only the words and the item count are ours.
            var label = dialog.BAJMOABFHLA != null ? dialog.BAJMOABFHLA.GetComponent<TextMeshProUGUI>() : null;
            if (label != null)
            {
                label.text = line;
            }
            if (dialog.IPEBGDBIHFA != null)
            {
                dialog.IPEBGDBIHFA.SetActive(false);
            }
        }
    }
}
