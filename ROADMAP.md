# Roadmap

What is planned for *The Longing Decrypted* beyond `1.1.0`. Shipped work lives in git history and [GitHub Releases](https://github.com/Decryptu/thelonging-decrypted/releases).

## Goal

A complete, approachable Archipelago multiworld and randomizer for *The Longing*: a playable `the_longing.apworld`, a BepInEx 5 x86 Unity mod with quality-of-life pacing tools, autotracking PopTracker and Universal Tracker support, and a self-hosted player hub with a YAML generator and interactive cavern map.

## Current Status

- **`1.1.0` shipped:** The Cavern Peddler, relics, hats, companions, and an interface built from the game's own art, with matched `TheLongingDecrypted` plugin, `the_longing.apworld`, and `the_longing_poptracker.zip`.

## Ideas

- Community balance and quality-of-life refinements based on multiworld feedback (for example, additional pacing presets or optional hint/cosmetic toggles).
- Keep compatibility aligned with future Archipelago releases as needed.

## Decisions

- Target **BepInEx 5 x86** on Unity `2018.4.36f1` (`MonoBleedingEdge`, .NET 4.x).
- Target **Archipelago 0.6.8** or newer.
- Keep all player-facing tools (YAML generator, interactive map, setup guide) self-hosted on GitHub Pages via GitHub Actions.
- Keep game binaries, decompiled source, extracted raw game textures, and personal saves out of version control.
- Keep countdown checkpoints in the isolated slot save. A legacy slot without a checkpoint uses the configured starting countdown on its first reconnect. Checkpoint recovery currently counts offline time at one second per second.
- Build every in-game interface from sprites and fonts the game already has loaded, and write every line in the Shade's voice.
- Relic waits run on real time and are stored in the slot save, so quitting does not refresh them.
- Mobility upgrades belong to the slot. Waypoint recall unlocks from the inner kingdom outward and requires a saved destination.
