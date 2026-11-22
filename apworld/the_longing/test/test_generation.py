"""Standalone and Archipelago 0.6.8+ generation tests for The Longing APWorld."""
from __future__ import annotations

import logging
import os
import random
import sys
import types
import unittest
from pathlib import Path
import yaml

ROOT = Path(__file__).resolve().parents[3]
APWORLD_DIR = ROOT / "apworld"


def _bootstrap_archipelago() -> None:
    if str(APWORLD_DIR) not in sys.path:
        sys.path.insert(0, str(APWORLD_DIR))
    try:
        import BaseClasses  # noqa: F401
        return
    except ModuleNotFoundError:
        pass

    ap_root = Path(os.environ.get("ARCHIPELAGO_DIR", r"C:\ProgramData\Archipelago"))
    lib_dir = ap_root / "lib"
    lib_zip = lib_dir / "library.zip"
    if not lib_zip.exists():
        raise RuntimeError(f"Archipelago runtime not found at {ap_root}. Set ARCHIPELAGO_DIR.")

    # Frozen AP bundles binary extensions with dotted filenames; stub bsdiff4 for pure-Python world tests.
    sys.modules.setdefault("bsdiff4", types.ModuleType("bsdiff4"))
    for entry in (str(lib_zip), str(lib_dir)):
        if entry not in sys.path:
            sys.path.insert(0, entry)


_bootstrap_archipelago()

logging.disable(logging.ERROR)
from BaseClasses import CollectionState, MultiWorld  # noqa: E402
from Options import OptionError  # noqa: E402
import the_longing  # noqa: E402
from the_longing import LongingWorld  # noqa: E402
from the_longing.options import LongingOptions  # noqa: E402
import Fill  # noqa: E402
logging.disable(logging.NOTSET)


def _build_multiworld(player_configs: list[dict[str, object]], seed: int = 400) -> MultiWorld:
    players = len(player_configs)
    mw = MultiWorld(players)
    mw.set_seed(seed)
    mw.game = {i + 1: LongingWorld.game for i in range(players)}
    mw.player_name = {i + 1: f"Shade{i + 1}" for i in range(players)}
    mw.groups = {}
    mw.player_types = {i + 1: 1 for i in range(players)}
    mw.state = CollectionState(mw)

    for idx, cfg in enumerate(player_configs, start=1):
        opts = {}
        for name, opt_type in LongingOptions.type_hints.items():
            raw_val = cfg.get(name, opt_type.default)
            opts[name] = opt_type.from_any(raw_val)
        world = LongingWorld(mw, idx)
        world.options = LongingOptions(**opts)
        world.random = random.Random(seed + idx)
        mw.worlds[idx] = world

    for idx in range(1, players + 1):
        mw.worlds[idx].generate_early()
    for idx in range(1, players + 1):
        mw.worlds[idx].create_regions()
    for idx in range(1, players + 1):
        mw.worlds[idx].create_items()
    for idx in range(1, players + 1):
        mw.worlds[idx].set_rules()

    Fill.distribute_items_restrictive(mw)

    return mw


class TestLongingWorldGeneration(unittest.TestCase):
    def test_unique_ids_and_groups(self) -> None:
        item_ids = list(LongingWorld.item_name_to_id.values())
        loc_ids = list(LongingWorld.location_name_to_id.values())
        self.assertEqual(len(item_ids), len(set(item_ids)), "Duplicate item IDs detected")
        self.assertEqual(len(loc_ids), len(set(loc_ids)), "Duplicate location IDs detected")
        self.assertTrue(set(item_ids).isdisjoint(set(loc_ids)), "Item and location ID spaces overlap")
        self.assertIn("Crystals", LongingWorld.item_name_groups)
        self.assertIn("Books", LongingWorld.item_name_groups)

    def test_all_goals_generate_and_are_beatable(self) -> None:
        goals = ["wake_the_king", "escape_surface", "secret_ending", "crystal_book_hunt", "any_ending"]
        for i, goal in enumerate(goals):
            with self.subTest(goal=goal):
                mw = _build_multiworld([{"goal": goal}], seed=1000 + i)
                empty_state = CollectionState(mw)
                self.assertFalse(
                    mw.completion_condition[1](empty_state),
                    f"Goal {goal} should not satisfy completion_condition with 0 items",
                )
                self.assertTrue(
                    mw.can_beat_game(CollectionState(mw)),
                    f"Goal {goal} must be logically beatable from empty starting state",
                )
                self.assertTrue(mw.fulfills_accessibility())
                slot = mw.worlds[1].fill_slot_data()
                self.assertEqual(slot["goal"], goal)
                self.assertGreater(len(slot["enabled_location_ids"]), 0)

    def test_hub_presets_generate_cleanly(self) -> None:
        presets: dict[str, dict[str, object]] = {
            "multiworld_evening": {
                "goal": "wake_the_king",
                "countdown_length": "6_hours",
                "starting_time_rate": 12,
                "walk_speed": "brisk",
                "time_skip_items": True,
                "shuffle_mattocks_and_keys": True,
                "shuffle_books": "progression",
                "shuffle_crystals": "full",
                "shuffle_mushrooms_and_moss": True,
                "shuffle_home_comfort": True,
                "shuffle_instrument_parts": True,
                "shuffle_coal_and_sulfur": False,
                "trap_density": "light",
                "death_link": False,
            },
            "swift_escape": {
                "goal": "escape_surface",
                "countdown_length": "1_hour",
                "starting_time_rate": 24,
                "walk_speed": "swift",
                "time_skip_items": True,
                "shuffle_mattocks_and_keys": True,
                "shuffle_books": "vanilla",
                "shuffle_crystals": "progression",
                "shuffle_mushrooms_and_moss": True,
                "shuffle_home_comfort": True,
                "shuffle_instrument_parts": False,
                "shuffle_coal_and_sulfur": False,
                "trap_density": "none",
                "death_link": False,
            },
            "full_cavern": {
                "goal": "crystal_book_hunt",
                "countdown_length": "24_hours",
                "starting_time_rate": 16,
                "walk_speed": "swift",
                "time_skip_items": True,
                "shuffle_mattocks_and_keys": True,
                "shuffle_books": "full",
                "shuffle_crystals": "full",
                "shuffle_mushrooms_and_moss": True,
                "shuffle_home_comfort": True,
                "shuffle_instrument_parts": True,
                "shuffle_coal_and_sulfur": True,
                "trap_density": "moderate",
                "death_link": False,
            },
            "true_longing": {
                "goal": "any_ending",
                "countdown_length": "400_days",
                "starting_time_rate": 1,
                "walk_speed": "vanilla",
                "time_skip_items": True,
                "shuffle_mattocks_and_keys": True,
                "shuffle_books": "full",
                "shuffle_crystals": "full",
                "shuffle_mushrooms_and_moss": True,
                "shuffle_home_comfort": True,
                "shuffle_instrument_parts": True,
                "shuffle_coal_and_sulfur": True,
                "trap_density": "light",
                "death_link": False,
            },
        }
        for name, cfg in presets.items():
            with self.subTest(preset=name):
                mw = _build_multiworld([cfg], seed=2000)
                self.assertTrue(mw.can_beat_game(mw.get_all_state(False)))

    def test_minimal_shuffle_and_crystal_book_hunt_clamping(self) -> None:
        mw = _build_multiworld(
            [
                {
                    "goal": "crystal_book_hunt",
                    "shuffle_mattocks_and_keys": True,
                    "shuffle_books": "vanilla",
                    "shuffle_crystals": "progression",
                    "required_crystals": 16,
                    "required_books": 30,
                    "shuffle_mushrooms_and_moss": False,
                    "shuffle_home_comfort": False,
                    "shuffle_instrument_parts": False,
                    "shuffle_coal_and_sulfur": False,
                    "trap_density": "heavy",
                }
            ],
            seed=3000,
        )
        slot = mw.worlds[1].fill_slot_data()
        self.assertEqual(slot["required_crystals"], 7)
        self.assertEqual(slot["required_books"], 0)
        self.assertTrue(mw.can_beat_game(mw.get_all_state(False)))

    def test_three_player_multiworld_generation(self) -> None:
        mw = _build_multiworld(
            [
                {"goal": "wake_the_king", "shuffle_books": "progression", "shuffle_crystals": "full"},
                {"goal": "escape_surface", "shuffle_books": "vanilla", "shuffle_crystals": "progression"},
                {"goal": "crystal_book_hunt", "shuffle_books": "full", "shuffle_crystals": "full", "shuffle_coal_and_sulfur": True},
            ],
            seed=400400,
        )
        self.assertTrue(mw.can_beat_game(mw.get_all_state(False)))

    def test_exported_hub_yaml_parses_and_generates(self) -> None:
        sample_yaml = """
name: Shade
description: Generated with The Longing Decrypted player hub
game: The Longing

The Longing:
  progression_balancing: 50
  accessibility: full
  goal: crystal_book_hunt
  countdown_length: 24_hours
  starting_time_rate: 16
  time_skip_items: true
  walk_speed: swift
  required_crystals: 10
  required_books: 18
  shuffle_mattocks_and_keys: true
  shuffle_books: full
  shuffle_crystals: full
  shuffle_mushrooms_and_moss: true
  shuffle_home_comfort: true
  shuffle_instrument_parts: true
  shuffle_coal_and_sulfur: true
  trap_density: moderate
  death_link: false
"""
        parsed = yaml.safe_load(sample_yaml)
        self.assertEqual(parsed["game"], LongingWorld.game)
        game_opts = parsed["The Longing"]
        # Ensure every exported game option key is recognized by LongingOptions
        unknown = set(game_opts.keys()) - set(LongingOptions.type_hints.keys())
        self.assertEqual(unknown, set(), f"Unknown YAML options exported by hub: {unknown}")
        mw = _build_multiworld([game_opts], seed=500500)
        self.assertTrue(mw.can_beat_game(CollectionState(mw)))
        slot = mw.worlds[1].fill_slot_data()
        self.assertEqual(slot["required_crystals"], 10)
        self.assertEqual(slot["required_books"], 18)
        self.assertEqual(len(slot["enabled_location_ids"]), 145)


if __name__ == "__main__":
    unittest.main()
