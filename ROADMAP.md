# Roadmap

What is in progress and planned for *The Longing Decrypted*. Finished work moves to git history.

## Goal

A complete, approachable Archipelago multiworld and randomizer for *The Longing*: a playable `the_longing.apworld`, a BepInEx 5 x86 Unity mod with quality-of-life pacing tools, and a self-hosted player hub with a YAML generator and interactive cavern map.

## Milestones

1. **In-game runtime playtesting & door/pass hooks (`mod/`):** Verify live BepInEx 5 x86 injection against Steam *The Longing*, wire virtual progression passes (`Library Door Pass`, `Surface Gate Pass`, `Giant Mushroom Spores`) directly to in-game door/growth checks, and test save isolation and DeathLink in a live multiworld room.
2. **PopTracker & Universal Tracker pack:** Export a PopTracker / Universal Tracker package aligned with `data/world_catalog.json` and `apworld/the_longing/` logic rules.
3. **1.0 release:** Publish matched `TheLongingDecrypted` plugin and `the_longing.apworld` releases with verified solo and multiworld playthroughs, full player setup documentation, and save recovery notes.

## Decisions

- Target **BepInEx 5 x86** on Unity `2018.4.36f1` (`MonoBleedingEdge`, .NET 4.x).
- Target **Archipelago 0.6.8** or newer.
- Keep all player-facing tools (YAML generator, interactive map, setup guide) self-hosted on GitHub Pages via GitHub Actions.
- Keep game binaries, decompiled source, extracted raw game textures, and personal saves out of version control.
