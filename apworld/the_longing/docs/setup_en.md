# The Longing Setup Guide

## Requirements

- *The Longing* on Steam (Windows, 32-bit Unity `2018.4.36f1` x86)
- **BepInEx 5 (x86 / 32-bit)** — 64-bit BepInEx will not load into `The Longing.exe`
- **The Longing Decrypted** plugin (`TheLongingDecrypted-v1.0.0-BepInEx5-x86.zip`, bundling `TheLongingDecrypted.dll`, `Archipelago.MultiClient.Net.dll`, and `Newtonsoft.Json.dll`)
- **Archipelago 0.6.8+** with the matching `the_longing.apworld`
- *(Optional)* **PopTracker** with `the_longing_poptracker.zip`, or **Universal Tracker** (supported directly by `the_longing.apworld` without needing a local YAML)

Download the matched release files from the [GitHub Releases](https://github.com/Decryptu/thelonging-decrypted/releases/latest) page.

## Installation

1. Back up `%USERPROFILE%\AppData\LocalLow\Studio Seufz\The Longing` before modding.
2. Extract **BepInEx 5 x86** into your *The Longing* Steam directory (next to `The Longing.exe`) and launch once to generate `BepInEx/plugins/`.
3. Extract `TheLongingDecrypted-v1.0.0-BepInEx5-x86.zip` into your *The Longing* Steam directory so `TheLongingDecrypted.dll`, `Archipelago.MultiClient.Net.dll`, and `Newtonsoft.Json.dll` sit inside `BepInEx/plugins/TheLongingDecrypted/`.
4. For hosting or generating seeds, copy `the_longing.apworld` into Archipelago's `custom_worlds` directory and restart the Archipelago Launcher.
5. Generate your YAML scroll with the [Player Hub](https://decryptu.github.io/thelonging-decrypted/#yaml) and launch the game.
6. At the title screen, press `F8` to open the Archipelago overlay, enter your server host, port, slot name, and optional password, and click **Connect to Archipelago**.
7. Start a new game for your slot. Open the native waypoint foldout to check your walking pace and recall ring; once `Progressive Waypoint Recall` is unlocked, **Shift-click Go or Home** to recall to any recorded waypoint in range.

## PopTracker & Universal Tracker

- **PopTracker:** Place `the_longing_poptracker.zip` in PopTracker's `packs/` folder, open the pack (`Standard` or `Items Only`), and connect to your Archipelago room. Options, progressive mobility, recall rings, and all 152 checks autotrack automatically from `slot_data`.
- **Universal Tracker:** Install `the_longing.apworld` in Archipelago's `custom_worlds` folder. Universal Tracker regenerates the exact slot configuration directly from `slot_data` with no local YAML required and displays the cavern map tab.

## Save Isolation & Recovery

- Randomizer saves are isolated by default (`IsolateRandomizerSaves = true` in `BepInEx/config/com.decrypt.thelongingdecrypted.cfg`) under:
  `%USERPROFILE%\AppData\LocalLow\Studio Seufz\The Longing\Archipelago\<seed>_<slot>\`
- Your vanilla 400-day save (`saveDataNew.txt` in the parent folder) is never modified while save isolation is enabled.
- **Switching slots or seeds:** Return to the title screen before connecting to another slot or seed.
- **Countdown checkpoint recovery:** After the intro finishes, the plugin stores a per-slot clock checkpoint (`AP_CLOCK_OFFSET_SECONDS`) inside the isolated slot save. Quitting and relaunching restores the remaining countdown minus real elapsed offline time (counted at 1 second per second) without re-applying the initial countdown jump.
- **Resetting a slot or restoring vanilla:** To restart a randomizer slot from scratch, close the game and delete only its `<seed>_<slot>` folder under `Archipelago/`. To return to completely unmodded play, disable or remove `BepInEx` (or restore your pre-modding backup of `%USERPROFILE%\AppData\LocalLow\Studio Seufz\The Longing`).

Use the matching plugin, `the_longing.apworld`, and PopTracker pack version when generating a seed. Leave save isolation enabled whenever playing more than one slot.
