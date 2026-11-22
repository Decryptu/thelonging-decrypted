# The Longing Setup Guide

## Requirements

- *The Longing* on Steam (Windows, 32-bit Unity `2018.4.36f1` x86)
- **BepInEx 5 (x86 / 32-bit)** — 64-bit BepInEx will not load into `The Longing.exe`
- **The Longing Decrypted** plugin and its bundled dependencies (`Archipelago.MultiClient.Net.dll` and `Newtonsoft.Json.dll`)
- **Archipelago 0.6.8+** with `the_longing.apworld`

## Installation

1. Back up `%USERPROFILE%\AppData\LocalLow\Studio Seufz\The Longing` before modding.
2. Extract **BepInEx 5 x86** into your *The Longing* Steam directory (next to `The Longing.exe`) and launch once.
3. Place `TheLongingDecrypted.dll`, `Archipelago.MultiClient.Net.dll`, and the bundled `Newtonsoft.Json.dll` inside `BepInEx/plugins/TheLongingDecrypted/`.
4. For hosting/generating seeds, copy `the_longing.apworld` into Archipelago's `custom_worlds` directory.
5. Generate your YAML scroll with the [Player Hub](https://decryptu.github.io/thelonging-decrypted/#yaml) and launch the game.
6. Press `F8` in-game to open the Archipelago overlay, enter your server host, port, slot name, and optional password, and connect.
