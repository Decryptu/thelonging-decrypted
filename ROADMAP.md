# Roadmap

What is in progress and planned for *The Longing Decrypted*. Finished work moves to git history.

## Goal

A complete, approachable Archipelago multiworld and randomizer for *The Longing*: a playable `the_longing.apworld`, a BepInEx 5 x86 Unity mod with quality-of-life pacing tools, and a self-hosted player hub with a YAML generator and interactive cavern map.

## Milestones

1. **Cavern & state catalog** — Map all 78+ cavern rooms, scene transitions, pickups, doors, mattocks, books, crystals, mushrooms, moss, home comforts, drawings, and endings from the game assemblies and serialized scenes into structured world data.
2. **BepInEx 5 x86 client plugin (`mod/`)** — Hook `FileBasedPrefs`, `GameManagerScript`, `UnixTimer`, and `Interactable*` classes for isolated randomizer saves, Archipelago server connection (`Archipelago.MultiClient.Net`), location check interception, remote item delivery, and clean handling of the built-in time/speed detectors.
3. **Quality-of-life & pacing controls** — Configurable starting countdown, time-rate multiplier, time-skip items, adjustable Shade walking speed, fast-travel/waypoint conveniences, and a compact in-game check/connection overlay.
4. **Archipelago world (`apworld/the_longing/`)** — Implement regions, reachability logic, item and location registries, multiple goals (Wake the King, Escape to the Surface, Secret Ending, Crystal/Book Hunt, Any Ending), randomization toggles, traps, and solo/multiworld generation tests targeting Archipelago 0.6.8+.
5. **Interactive cavern map & tracker** — Upgrade the hub's cavern map into a full check/logic explorer with item filters, spoiler controls, and Universal Tracker / PopTracker integration.
6. **Option-synced YAML generator** — Wire the player hub's YAML generator directly to the shipped APWorld option schema, add incompatible-option validation, and verify exported YAMLs against Archipelago generation.
7. **1.0 release** — Publish matched plugin and `.apworld` releases with verified solo and multiworld playthroughs, full player setup documentation, and save recovery notes.

## Decisions

- Target **BepInEx 5 x86** on Unity `2018.4.36f1` (`MonoBleedingEdge`, .NET 4.x).
- Target **Archipelago 0.6.8** or newer.
- Keep all player-facing tools (YAML generator, interactive map, setup guide) self-hosted on GitHub Pages via GitHub Actions.
- Keep game binaries, decompiled source, extracted raw game textures, and personal saves out of version control.
