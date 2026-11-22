# Roadmap

What is in progress and planned for *The Longing Decrypted*. Finished work moves to git history.

## Goal

A complete, approachable Archipelago multiworld and randomizer for *The Longing*: a playable `the_longing.apworld`, a BepInEx 5 x86 Unity mod with quality-of-life pacing tools, and a self-hosted player hub with a YAML generator and interactive cavern map.

## Milestones

1. **In-game progression and multiworld playtesting (`mod/`):**
   - Start with the countdown restart test. Connect two disposable slots with different countdowns, finish their intros, and capture each remaining time. Quit and relaunch the game, reconnect to both slots, and compare each clock against its saved remaining time minus elapsed offline time. Confirm that reconnecting to the same slot does not reapply the starting advance. The release build and a simulated restart and slot-switch test passed. The new checkpoint path still needs a live run.
   - Test the physical Library Door, Upper Exit Gate, and Great Cave giant mushroom in both locked and unlocked states. The Library overlay shortcut has already passed both checks in-game.
   - Trigger an in-game death and confirm that a second client receives DeathLink. Incoming DeathLink was verified by sending it during the intro and observing Shade return home after the intro.
2. **PopTracker & Universal Tracker pack:** Export a PopTracker / Universal Tracker package aligned with `data/world_catalog.json` and `apworld/the_longing/` logic rules.
3. **1.0 release:** Publish matched `TheLongingDecrypted` plugin and `the_longing.apworld` releases with verified solo and multiworld playthroughs, full player setup documentation, and save recovery notes.

## Decisions

- Target **BepInEx 5 x86** on Unity `2018.4.36f1` (`MonoBleedingEdge`, .NET 4.x).
- Target **Archipelago 0.6.8** or newer.
- Keep all player-facing tools (YAML generator, interactive map, setup guide) self-hosted on GitHub Pages via GitHub Actions.
- Keep game binaries, decompiled source, extracted raw game textures, and personal saves out of version control.
- Keep countdown checkpoints in the isolated slot save. A legacy slot without a checkpoint uses the configured starting countdown on its first reconnect. Checkpoint recovery currently counts offline time at one second per second.
