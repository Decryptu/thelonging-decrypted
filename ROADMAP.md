# Roadmap

What is planned for *The Longing Decrypted* beyond `1.0.0`. Shipped work lives in git history and [GitHub Releases](https://github.com/Decryptu/thelonging-decrypted/releases).

## Goal

A complete, approachable Archipelago multiworld and randomizer for *The Longing*: a playable `the_longing.apworld`, a BepInEx 5 x86 Unity mod with quality-of-life pacing tools, autotracking PopTracker and Universal Tracker support, and a self-hosted player hub with a YAML generator and interactive cavern map.

## Current Status

- **`1.0.0` shipped:** Verified across live solo and cross-game multiworld playthroughs, with matched `TheLongingDecrypted` plugin, `the_longing.apworld` (Archipelago 0.6.8+ and Universal Tracker), `the_longing_poptracker.zip`, and updated player hub and save recovery documentation.

## Post-1.0 Ideas

- Community balance and quality-of-life refinements based on multiworld feedback (for example, additional pacing presets or optional hint/cosmetic toggles).
- Keep compatibility aligned with future Archipelago releases as needed.

## Decisions

- Target **BepInEx 5 x86** on Unity `2018.4.36f1` (`MonoBleedingEdge`, .NET 4.x).
- Target **Archipelago 0.6.8** or newer.
- Keep all player-facing tools (YAML generator, interactive map, setup guide) self-hosted on GitHub Pages via GitHub Actions.
- Keep game binaries, decompiled source, extracted raw game textures, and personal saves out of version control.
- Keep countdown checkpoints in the isolated slot save. A legacy slot without a checkpoint uses the configured starting countdown on its first reconnect. Checkpoint recovery currently counts offline time at one second per second.
- Mobility upgrades belong to the slot. Waypoint recall unlocks from the inner kingdom outward and requires a saved destination.
