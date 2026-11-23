"""Verification suite for the PopTracker & Universal Tracker pack against fresh Archipelago seeds."""
from __future__ import annotations

import json
import random
import re
import sys
import tempfile
import unittest
from pathlib import Path
from zipfile import ZipFile

from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
if str(ROOT / "tools") not in sys.path:
    sys.path.insert(0, str(ROOT / "tools"))
if str(Path(__file__).resolve().parent) not in sys.path:
    sys.path.insert(0, str(Path(__file__).resolve().parent))

import build_tracker_pack  # noqa: E402
import test_generation  # noqa: E402
from BaseClasses import CollectionState, MultiWorld  # noqa: E402
from the_longing import LongingWorld  # noqa: E402
from the_longing.options import LongingOptions  # noqa: E402
from the_longing.registry_data import ITEM_DEFINITIONS, LOCATION_DEFINITIONS  # noqa: E402
from the_longing.tracker_mapping import POPTRACKER_LOCATION_MAPPING, RECALL_RING_SCENES  # noqa: E402

TRACKER_DIR = ROOT / "tracker"
WAYPOINT_PATCH_CS = ROOT / "mod" / "Patches" / "WaypointRecallPatches.cs"


def _parse_lua_item_mapping(text: str) -> dict[int, tuple[str, str]]:
    result: dict[int, tuple[str, str]] = {}
    for match in re.finditer(r'\[(\d+)\]\s*=\s*\{\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\}', text):
        result[int(match.group(1))] = (match.group(2), match.group(3))
    return result


def _parse_lua_location_mapping(text: str) -> dict[int, list[str]]:
    result: dict[int, list[str]] = {}
    block_match = re.search(r"LOCATION_MAPPING\s*=\s*\{(.*?)\n\}", text, re.S)
    if not block_match:
        return result
    for match in re.finditer(r'\[(\d+)\]\s*=\s*\{\s*"([^"]+)"\s*\}', block_match.group(1)):
        result[int(match.group(1))] = [match.group(2)]
    return result


def _parse_mod_recall_rings(cs_text: str) -> dict[int, set[int]]:
    match = re.search(r"internal static int CavernRing\(int scene\)\s*\{(.*?)\n\s{8}\}", cs_text, re.S)
    if not match:
        raise AssertionError("Could not locate CavernRing in WaypointRecallPatches.cs")
    body = match.group(1)
    rings: dict[int, set[int]] = {1: set(), 2: set(), 3: set()}
    current_cases: list[int] = []
    for token in re.finditer(r"case\s+(\d+)\s*:|return\s+(\d+)\s*;", body):
        if token.group(1) is not None:
            current_cases.append(int(token.group(1)))
        elif token.group(2) is not None:
            ring = int(token.group(2))
            if ring in rings:
                rings[ring].update(current_cases)
            current_cases = []
    return rings


class PopTrackerSimulator:
    """Evaluates the generated PopTracker JSON + Lua mappings against a live AP seed."""

    def __init__(self, tracker_dir: Path) -> None:
        self.tracker_dir = tracker_dir
        self.items_def = json.loads((tracker_dir / "items" / "items.json").read_text(encoding="utf-8"))
        self.settings_def = json.loads((tracker_dir / "items" / "settings.json").read_text(encoding="utf-8"))
        self.locations_def = json.loads((tracker_dir / "locations" / "locations.json").read_text(encoding="utf-8"))
        self.item_mapping = _parse_lua_item_mapping(
            (tracker_dir / "scripts" / "autotracking" / "item_mapping.lua").read_text(encoding="utf-8")
        )
        self.location_mapping = _parse_lua_location_mapping(
            (tracker_dir / "scripts" / "autotracking" / "location_mapping.lua").read_text(encoding="utf-8")
        )

        self.loc_meta: dict[int, dict[str, object]] = {int(loc["id"]): loc for loc in LOCATION_DEFINITIONS}
        self.loc_order: list[int] = [int(loc["id"]) for loc in LOCATION_DEFINITIONS]

        self.section_index: dict[str, tuple[dict[str, object], dict[str, object]]] = {}
        self.chests: dict[str, int] = {}
        for node in self.locations_def:
            for section in node.get("sections", []):
                path = f"@{node['name']}/{section['name']}"
                self.section_index[path] = (node, section)
                self.chests[path] = 1

        self.toggles: dict[str, bool] = {}
        self.counts: dict[str, int] = {}
        self.stages: dict[str, int] = {}
        self.slot_data: dict[str, object] = {}
        self.slot_enabled: set[int] | None = None
        self.reset()

    def reset(self) -> None:
        self.toggles = {it["codes"]: bool(it.get("initial_active_state", False)) for it in self.items_def + self.settings_def if it["type"] == "toggle"}
        self.counts = {it["codes"]: int(it.get("initial_quantity", 0)) for it in self.items_def + self.settings_def if it["type"] == "consumable"}
        self.stages = {it["codes"]: int(it.get("initial_stage_idx", 0)) for it in self.items_def + self.settings_def if it["type"] == "progressive"}
        for path in self.chests:
            self.chests[path] = 1
        self.slot_data = {}
        self.slot_enabled = None

    def on_clear(self, slot_data: dict[str, object], use_slot_enabled: bool = True) -> None:
        self.reset()
        self.slot_data = dict(slot_data)
        goal_map = {
            "wake_the_king": 0,
            "escape_surface": 1,
            "secret_ending": 2,
            "crystal_book_hunt": 3,
            "any_ending": 4,
        }
        depth_map = {"vanilla": 0, "progression": 1, "full": 2}
        if "goal" in slot_data:
            self.stages["opt_goal"] = goal_map[str(slot_data["goal"])]
        if "shuffle_mattocks_and_keys" in slot_data:
            self.toggles["opt_shuffle_mattocks_and_keys"] = bool(slot_data["shuffle_mattocks_and_keys"])
        if "shuffle_books" in slot_data:
            self.stages["opt_shuffle_books"] = depth_map[str(slot_data["shuffle_books"])]
        if "shuffle_crystals" in slot_data:
            self.stages["opt_shuffle_crystals"] = depth_map[str(slot_data["shuffle_crystals"])]
        if "shuffle_mushrooms_and_moss" in slot_data:
            self.toggles["opt_shuffle_mushrooms_and_moss"] = bool(slot_data["shuffle_mushrooms_and_moss"])
        if "shuffle_home_comfort" in slot_data:
            self.toggles["opt_shuffle_home_comfort"] = bool(slot_data["shuffle_home_comfort"])
        if "shuffle_instrument_parts" in slot_data:
            self.toggles["opt_shuffle_instrument_parts"] = bool(slot_data["shuffle_instrument_parts"])
        if "shuffle_coal_and_sulfur" in slot_data:
            self.toggles["opt_shuffle_coal_and_sulfur"] = bool(slot_data["shuffle_coal_and_sulfur"])
        if "shuffle_shop" in slot_data:
            self.toggles["opt_shuffle_shop"] = bool(slot_data["shuffle_shop"])
        if "required_crystals" in slot_data:
            self.counts["opt_required_crystals"] = int(slot_data["required_crystals"])
        if "required_books" in slot_data:
            self.counts["opt_required_books"] = int(slot_data["required_books"])
        if use_slot_enabled and isinstance(slot_data.get("enabled_location_ids"), list):
            self.slot_enabled = {int(i) for i in slot_data["enabled_location_ids"]}
        self._sync_rings()
        self._sync_crystals()

    def _sync_rings(self) -> None:
        tier = self.stages["progressive_waypoint_recall"]
        for ring in (1, 2, 3):
            self.toggles[f"recall_ring_{ring}"] = tier >= ring

    def _sync_crystals(self) -> None:
        total = (
            self.counts["crystal_white"]
            + self.counts["crystal_rose"]
            + self.counts["crystal_blue"]
            + int(self.toggles["crystal_purple"])
            + int(self.toggles["crystal_black"])
            + int(self.toggles["crystal_great_hall"])
            + int(self.toggles["crystal_tower"])
        )
        self.counts["crystals_total"] = total

    def on_item(self, item_id: int) -> None:
        mapping = self.item_mapping.get(item_id)
        if mapping is None:
            return
        code, kind = mapping
        if kind == "toggle":
            self.toggles[code] = True
        elif kind == "consumable":
            self.counts[code] = self.counts.get(code, 0) + 1
        elif kind == "progressive":
            max_stage = 4 if code == "progressive_walk_speed" else 3
            self.stages[code] = min(max_stage, self.stages.get(code, 0) + 1)
        if code == "progressive_waypoint_recall":
            self._sync_rings()
        elif code.startswith("crystal_"):
            self._sync_crystals()

    def on_location(self, loc_id: int) -> None:
        for path in self.location_mapping[loc_id]:
            self.chests[path] = max(0, self.chests[path] - 1)

    def _eval_token(self, token: str) -> bool:
        if not token.startswith("$"):
            goal_codes = [
                "goal_wake_the_king",
                "goal_escape_surface",
                "goal_secret_ending",
                "goal_crystal_book_hunt",
                "goal_any_ending",
            ]
            if token in goal_codes:
                return self.stages["opt_goal"] == goal_codes.index(token)
            raise AssertionError(f"Unexpected bare rule token: {token!r}")

        parts = token[1:].split("|")
        fn = parts[0]
        args = parts[1:]

        if fn == "loc_enabled":
            loc_id = int(args[0])
            if self.slot_enabled is not None:
                return loc_id in self.slot_enabled
            return loc_id in self._manual_enabled_ids()
        if fn == "has_mattocks":
            req = int(args[0]) if args else 1
            return (not self.toggles["opt_shuffle_mattocks_and_keys"]) or self.counts["mattock"] >= req
        if fn == "has_secret_key":
            return (not self.toggles["opt_shuffle_mattocks_and_keys"]) or self.toggles["secret_key"]
        if fn == "has_library_pass":
            return (not self.toggles["opt_shuffle_mattocks_and_keys"]) or self.toggles["library_door_pass"]
        if fn == "has_surface_gate":
            return (not self.toggles["opt_shuffle_mattocks_and_keys"]) or self.toggles["surface_gate_pass"]
        if fn == "has_giant_spores":
            if not (self.toggles["opt_shuffle_mushrooms_and_moss"] or self.toggles["opt_shuffle_mattocks_and_keys"]):
                return True
            return self.toggles["giant_mushroom_spores"]
        if fn == "can_reach_secret_tower":
            return self._eval_token("$has_surface_gate") and self._eval_token("$has_giant_spores")
        if fn == "has_moss_10":
            return (not self.toggles["opt_shuffle_mushrooms_and_moss"]) or self.counts["cavern_moss"] >= 10
        if fn == "has_sulfur":
            if self.toggles["opt_shuffle_coal_and_sulfur"]:
                return self.counts["sulfur"] >= 1
            return self._eval_token("$has_surface_gate") and self._eval_token("$has_mattocks|1")
        if fn == "has_instrument_3":
            return (not self.toggles["opt_shuffle_instrument_parts"]) or self.counts["musical_instrument_part"] >= 3
        if fn == "has_paper":
            return (not self.toggles["opt_shuffle_home_comfort"]) or self.counts["drawing_paper"] >= 1
        if fn == "can_wake_king":
            return self._eval_token("$has_mattocks|1") and self._eval_token("$has_surface_gate")
        if fn == "can_secret_ending":
            return self._eval_token("$can_reach_secret_tower") and self._eval_token("$has_secret_key")
        if fn == "can_complete_hunt":
            req_c = int(self.slot_data["required_crystals"]) if "required_crystals" in self.slot_data else self.counts["opt_required_crystals"]
            req_b = int(self.slot_data["required_books"]) if "required_books" in self.slot_data else self.counts["opt_required_books"]
            if req_c == 0 and req_b == 0:
                return self._eval_token("$has_library_pass") and self._eval_token("$has_surface_gate") and self._eval_token("$has_mattocks|1")
            return self.counts["crystals_total"] >= req_c and self.counts["books"] >= req_b
        raise AssertionError(f"Unhandled Lua rule function: {token!r}")

    def _manual_enabled_ids(self) -> set[int]:
        enabled: set[int] = set()
        for loc_id in self.loc_order:
            meta = self.loc_meta[loc_id]
            pool = str(meta["pool"])
            depth = str(meta["depth"])
            if pool == "mobility":
                continue
            ok = False
            if pool == "tools":
                ok = self.toggles["opt_shuffle_mattocks_and_keys"]
            elif pool == "books":
                st = self.stages["opt_shuffle_books"]
                ok = st == 2 or (st == 1 and depth == "progression")
            elif pool == "crystals":
                st = self.stages["opt_shuffle_crystals"]
                ok = st == 2 or (st == 1 and depth == "progression")
            elif pool == "mushrooms_moss":
                ok = self.toggles["opt_shuffle_mushrooms_and_moss"]
            elif pool == "comfort":
                ok = self.toggles["opt_shuffle_home_comfort"]
            elif pool == "instrument":
                ok = self.toggles["opt_shuffle_instrument_parts"]
            elif pool == "coal_sulfur":
                ok = self.toggles["opt_shuffle_coal_and_sulfur"]
            elif pool == "shop":
                ok = self.toggles["opt_shuffle_shop"]
            if ok:
                enabled.add(loc_id)
        if len(enabled) < 10:
            for loc_id in self.loc_order:
                meta = self.loc_meta[loc_id]
                if str(meta["pool"]) in ("tools", "comfort") and loc_id not in enabled:
                    enabled.add(loc_id)
                if len(enabled) >= 12:
                    break
        for loc_id in self.loc_order:
            if str(self.loc_meta[loc_id]["pool"]) == "mobility":
                enabled.add(loc_id)
        return enabled

    def eval_rule_list(self, rules: list[str]) -> bool:
        if not rules:
            return True
        return any(all(self._eval_token(part.strip()) for part in clause.split(",") if part.strip()) for clause in rules)

    def is_section_visible(self, path: str) -> bool:
        node, section = self.section_index[path]
        return self.eval_rule_list(list(node.get("visibility_rules", []))) and self.eval_rule_list(
            list(section.get("visibility_rules", []))
        )

    def is_section_accessible(self, path: str) -> bool:
        node, section = self.section_index[path]
        return self.eval_rule_list(list(node.get("access_rules", []))) and self.eval_rule_list(
            list(section.get("access_rules", []))
        )


def _regenerate_via_ut(slot_data: dict[str, object]) -> MultiWorld:
    """Simulate Universal Tracker YAML-less regeneration from slot_data alone."""
    mw = MultiWorld(1)
    mw.set_seed(999999)
    mw.game = {1: LongingWorld.game}
    mw.player_name = {1: "TrackerShade"}
    mw.groups = {}
    mw.player_types = {1: 1}
    mw.state = CollectionState(mw)

    # Start with default options only (no player YAML on disk)
    opts = {name: opt_cls.from_any(opt_cls.default) for name, opt_cls in LongingOptions.type_hints.items()}
    world = LongingWorld(mw, 1)
    world.options = LongingOptions(**opts)
    world.random = random.Random(999999)
    mw.worlds[1] = world

    mw.re_gen_passthrough = {LongingWorld.game: LongingWorld.interpret_slot_data(slot_data)}
    world.generate_early()
    world.create_regions()
    world.create_items()
    world.set_rules()
    return mw


class TestTrackerPackAndUT(unittest.TestCase):
    def test_pack_structure_assets_and_mappings_are_complete(self) -> None:
        manifest = json.loads((TRACKER_DIR / "manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(manifest["game_name"], LongingWorld.game)
        self.assertIn("ap", manifest["variants"]["standard"]["flags"])
        self.assertIn("ap", manifest["variants"]["var_itemsonly"]["flags"])

        items = json.loads((TRACKER_DIR / "items" / "items.json").read_text(encoding="utf-8"))
        settings = json.loads((TRACKER_DIR / "items" / "settings.json").read_text(encoding="utf-8"))
        maps = json.loads((TRACKER_DIR / "maps" / "maps.json").read_text(encoding="utf-8"))
        locations = json.loads((TRACKER_DIR / "locations" / "locations.json").read_text(encoding="utf-8"))

        # Validate every referenced image exists and is a readable PNG
        known_codes: set[str] = set()
        for entry in items + settings:
            for code in str(entry["codes"]).split(","):
                known_codes.add(code.strip())
            if "img" in entry:
                img_path = TRACKER_DIR / entry["img"]
                self.assertTrue(img_path.exists(), f"Missing item image {entry['img']}")
                with Image.open(img_path) as im:
                    self.assertGreater(im.width, 0)
            for stage in entry.get("stages", []):
                for code in str(stage["codes"]).split(","):
                    known_codes.add(code.strip())
                img_path = TRACKER_DIR / stage["img"]
                self.assertTrue(img_path.exists(), f"Missing stage image {stage['img']}")
                with Image.open(img_path) as im:
                    self.assertGreater(im.width, 0)

        self.assertEqual(len(maps), 1)
        map_img_path = TRACKER_DIR / maps[0]["img"]
        with Image.open(map_img_path) as map_img:
            map_w, map_h = map_img.size

        # Validate layouts only reference defined item codes
        for rel in (
            "layouts/items.json",
            "layouts/settings.json",
            "layouts/tracker.json",
            "layouts/broadcast.json",
            "var_itemsonly/layouts/tracker.json",
        ):
            data = json.loads((TRACKER_DIR / rel).read_text(encoding="utf-8"))
            stack = [data]
            while stack:
                cur = stack.pop()
                if isinstance(cur, dict):
                    if cur.get("type") == "itemgrid":
                        for row in cur.get("rows", []):
                            for code in row:
                                if code:
                                    self.assertIn(code, known_codes, f"Unknown item code {code!r} in {rel}")
                    stack.extend(cur.values())
                elif isinstance(cur, list):
                    stack.extend(cur)

        # Validate all map coordinates sit inside cavern_map.png
        section_paths: set[str] = set()
        discovery_nodes: list[dict[str, object]] = []
        for node in locations:
            for ml in node.get("map_locations", []):
                self.assertEqual(ml["map"], "cavern_map")
                self.assertGreaterEqual(ml["x"], 0)
                self.assertLessEqual(ml["x"], map_w)
                self.assertGreaterEqual(ml["y"], 0)
                self.assertLessEqual(ml["y"], map_h)
                if ml.get("shape") == "diamond":
                    discovery_nodes.append(node)
            for section in node.get("sections", []):
                section_paths.add(f"@{node['name']}/{section['name']}")

        # All 7 discovery checks are dedicated diamond pins
        self.assertEqual(len(discovery_nodes), 7)

        # Every APWorld location ID (152) is mapped in location_mapping.lua and UT poptracker_name_mapping
        loc_map = _parse_lua_location_mapping(
            (TRACKER_DIR / "scripts" / "autotracking" / "location_mapping.lua").read_text(encoding="utf-8")
        )
        all_loc_ids = {int(loc["id"]) for loc in LOCATION_DEFINITIONS}
        self.assertEqual(set(loc_map.keys()), all_loc_ids)
        for loc_id, paths in loc_map.items():
            for path in paths:
                self.assertIn(path, section_paths, f"Unresolved PopTracker section path {path} for {loc_id}")

        self.assertEqual(set(POPTRACKER_LOCATION_MAPPING.values()), all_loc_ids)
        for rel_path, loc_id in POPTRACKER_LOCATION_MAPPING.items():
            self.assertIn(f"@{rel_path}", section_paths)

        # Both progressive mobility items and all 3 recall rings are present and mapped
        item_map = _parse_lua_item_mapping(
            (TRACKER_DIR / "scripts" / "autotracking" / "item_mapping.lua").read_text(encoding="utf-8")
        )
        walk_id = LongingWorld.item_name_to_id["Progressive Walk Speed"]
        recall_id = LongingWorld.item_name_to_id["Progressive Waypoint Recall"]
        self.assertEqual(item_map[walk_id], ("progressive_walk_speed", "progressive"))
        self.assertEqual(item_map[recall_id], ("progressive_waypoint_recall", "progressive"))
        for code in ("recall_ring_1", "recall_ring_2", "recall_ring_3"):
            self.assertIn(code, known_codes)
        for item_id, (code, _) in item_map.items():
            self.assertIn(code, known_codes, f"Mapped item {item_id} points to unknown code {code!r}")

    def test_recall_rings_match_mod_and_catalog(self) -> None:
        catalog = json.loads((ROOT / "data" / "world_catalog.json").read_text(encoding="utf-8"))
        rooms_by_index = {int(r["index"]): str(r["name"]) for r in catalog["rooms"]}
        mod_rings = _parse_mod_recall_rings(WAYPOINT_PATCH_CS.read_text(encoding="utf-8"))

        for ring in (1, 2, 3):
            expected_scenes = {rooms_by_index[idx] for idx in mod_rings[ring]}
            self.assertEqual(set(RECALL_RING_SCENES[ring]), expected_scenes, f"Ring {ring} drift from WaypointRecallPatches.cs")

    def test_fresh_seeds_logic_parity_mobility_and_discoveries(self) -> None:
        scenarios: list[tuple[str, dict[str, object], int]] = [
            ("default_wake_king", {}, 77101),
            (
                "full_crystal_book_hunt",
                {
                    "goal": "crystal_book_hunt",
                    "shuffle_books": "full",
                    "shuffle_crystals": "full",
                    "shuffle_coal_and_sulfur": True,
                    "required_crystals": 10,
                    "required_books": 18,
                },
                77102,
            ),
            (
                "secret_ending_no_coal_shuffle",
                {
                    "goal": "secret_ending",
                    "shuffle_books": "progression",
                    "shuffle_crystals": "progression",
                    "shuffle_coal_and_sulfur": False,
                },
                77103,
            ),
            (
                "minimal_mobility_only",
                {
                    "goal": "any_ending",
                    "shuffle_mattocks_and_keys": False,
                    "shuffle_books": "vanilla",
                    "shuffle_crystals": "vanilla",
                    "shuffle_mushrooms_and_moss": False,
                    "shuffle_home_comfort": False,
                    "shuffle_instrument_parts": False,
                    "shuffle_coal_and_sulfur": False,
                },
                77104,
            ),
            (
                "hunt_clamped_zero_books",
                {
                    "goal": "crystal_book_hunt",
                    "shuffle_books": "vanilla",
                    "shuffle_crystals": "progression",
                    "required_crystals": 14,
                    "required_books": 20,
                },
                77105,
            ),
        ]

        sim = PopTrackerSimulator(TRACKER_DIR)
        discovery_ids = {int(loc["id"]) for loc in LOCATION_DEFINITIONS if loc["pool"] == "mobility"}
        self.assertEqual(len(discovery_ids), 7)

        for label, cfg, seed in scenarios:
            with self.subTest(scenario=label):
                mw = test_generation._build_multiworld([cfg], seed=seed)
                world = mw.worlds[1]
                slot_data = world.fill_slot_data()
                expected_enabled = set(slot_data["enabled_location_ids"])

                # 1. Verify enabled locations match both with slot_data and in manual option mode
                for use_slot_enabled in (True, False):
                    sim.on_clear(slot_data, use_slot_enabled=use_slot_enabled)
                    visible_ids = {
                        loc_id
                        for loc_id, paths in sim.location_mapping.items()
                        if sim.is_section_visible(paths[0])
                    }
                    self.assertEqual(
                        visible_ids,
                        expected_enabled,
                        f"Visible locations mismatch (slot_enabled={use_slot_enabled}) in {label}",
                    )
                    # All 7 discovery checks are always enabled and visible
                    self.assertTrue(discovery_ids.issubset(visible_ids))

                # 2. Replay items sphere-by-sphere from the fresh seed and compare reachability at every sphere
                sim.on_clear(slot_data, use_slot_enabled=True)
                ap_state = CollectionState(mw)

                spheres = list(mw.get_spheres())
                for sphere_idx, sphere_locations in enumerate(spheres):
                    # Compare every enabled location's reachability BEFORE collecting this sphere
                    for loc in mw.get_locations(1):
                        if loc.address is None:
                            continue
                        path = sim.location_mapping[loc.address][0]
                        self.assertEqual(
                            sim.is_section_accessible(path),
                            loc.can_reach(ap_state),
                            f"{label} sphere {sphere_idx}: logic mismatch on {loc.name}",
                        )

                    # Collect only this sphere's items & check locations in both AP state and PopTracker
                    for loc in sphere_locations:
                        if loc.player == 1 and loc.address is not None:
                            sim.on_location(loc.address)
                        if loc.item is not None and loc.item.player == 1:
                            ap_state.collect(loc.item, prevent_sweep=True)
                            if loc.item.code is not None:
                                sim.on_item(loc.item.code)
                    # Sweep only locked victory events (not placed physical items)
                    for loc in mw.get_locations(1):
                        if loc.address is None and loc.item is not None and loc.can_reach(ap_state):
                            ap_state.collect(loc.item, prevent_sweep=True)

                # After collecting every sphere in the fresh seed:
                # - All 4 Progressive Walk Speed upgrades & all 3 Progressive Waypoint Recall upgrades received
                self.assertEqual(sim.stages["progressive_walk_speed"], 4)
                self.assertEqual(sim.stages["progressive_waypoint_recall"], 3)
                self.assertTrue(sim.toggles["recall_ring_1"])
                self.assertTrue(sim.toggles["recall_ring_2"])
                self.assertTrue(sim.toggles["recall_ring_3"])
                # - All 7 discovery checks were cleared
                for did in discovery_ids:
                    self.assertEqual(sim.chests[sim.location_mapping[did][0]], 0)
                # - Goal section is visible and accessible
                goal_paths = [
                    "@The King's Hall/Wake the Sleeping King",
                    "@The Well & Surface/Escape via the Well",
                    "@Secret Tower/Reach the Secret Ending",
                    "@Shade's Home/Complete Crystal & Book Collection",
                ]
                visible_goals = [p for p in goal_paths if sim.is_section_visible(p)]
                self.assertGreater(len(visible_goals), 0)
                self.assertTrue(any(sim.is_section_accessible(p) for p in visible_goals))

    def test_recall_rings_unlock_incrementally(self) -> None:
        sim = PopTrackerSimulator(TRACKER_DIR)
        mw = test_generation._build_multiworld([{}], seed=88101)
        sim.on_clear(mw.worlds[1].fill_slot_data())

        walk_id = LongingWorld.item_name_to_id["Progressive Walk Speed"]
        recall_id = LongingWorld.item_name_to_id["Progressive Waypoint Recall"]

        self.assertEqual(sim.stages["progressive_walk_speed"], 0)
        self.assertEqual(sim.stages["progressive_waypoint_recall"], 0)
        self.assertEqual((sim.toggles["recall_ring_1"], sim.toggles["recall_ring_2"], sim.toggles["recall_ring_3"]), (False, False, False))

        for step in range(1, 5):
            sim.on_item(walk_id)
            self.assertEqual(sim.stages["progressive_walk_speed"], step)

        sim.on_item(recall_id)
        self.assertEqual((sim.toggles["recall_ring_1"], sim.toggles["recall_ring_2"], sim.toggles["recall_ring_3"]), (True, False, False))
        sim.on_item(recall_id)
        self.assertEqual((sim.toggles["recall_ring_1"], sim.toggles["recall_ring_2"], sim.toggles["recall_ring_3"]), (True, True, False))
        sim.on_item(recall_id)
        self.assertEqual((sim.toggles["recall_ring_1"], sim.toggles["recall_ring_2"], sim.toggles["recall_ring_3"]), (True, True, True))

    def test_universal_tracker_regen_without_yaml_on_fresh_seeds(self) -> None:
        self.assertTrue(LongingWorld.ut_can_gen_without_yaml)
        tw = LongingWorld.tracker_world
        ap_tracker_root = ROOT / "apworld" / "the_longing" / tw["map_page_folder"]
        for rel in tw["map_page_maps"] + tw["map_page_locations"]:
            self.assertTrue((ap_tracker_root / rel).exists(), f"Missing UT bundled file {rel}")

        configs = [
            {
                "goal": "crystal_book_hunt",
                "shuffle_books": "full",
                "shuffle_crystals": "full",
                "shuffle_coal_and_sulfur": True,
                "required_crystals": 11,
                "required_books": 21,
            },
            {
                "goal": "crystal_book_hunt",
                "shuffle_books": "vanilla",
                "shuffle_crystals": "progression",
                "required_crystals": 16,
                "required_books": 30,
            },
            {
                "goal": "secret_ending",
                "shuffle_mattocks_and_keys": False,
                "shuffle_mushrooms_and_moss": False,
                "shuffle_home_comfort": False,
                "shuffle_instrument_parts": False,
            },
        ]

        for idx, cfg in enumerate(configs):
            with self.subTest(ut_case=idx):
                fresh_mw = test_generation._build_multiworld([cfg], seed=99100 + idx)
                slot_data = fresh_mw.worlds[1].fill_slot_data()

                ut_mw = _regenerate_via_ut(slot_data)
                ut_world = ut_mw.worlds[1]
                ut_slot_data = ut_world.fill_slot_data()

                self.assertEqual(ut_slot_data["goal"], slot_data["goal"])
                self.assertEqual(ut_slot_data["required_crystals"], slot_data["required_crystals"])
                self.assertEqual(ut_slot_data["required_books"], slot_data["required_books"])
                self.assertEqual(set(ut_slot_data["enabled_location_ids"]), set(slot_data["enabled_location_ids"]))

                # Verify UT logic parity against the fresh seed at empty state and full state
                for collect_all in (False, True):
                    fresh_state = fresh_mw.get_all_state(False) if collect_all else CollectionState(fresh_mw)
                    ut_state = CollectionState(ut_mw)
                    if collect_all:
                        # Replay every received item via ut_world.create_item(), matching Universal Tracker's client loop
                        for filled_loc in fresh_mw.get_filled_locations(1):
                            if filled_loc.address is not None and filled_loc.item is not None and filled_loc.item.player == 1:
                                ut_state.collect(ut_world.create_item(filled_loc.item.name), prevent_sweep=True)
                        for ut_event in ut_mw.get_locations(1):
                            if ut_event.address is None and ut_event.item is not None and ut_event.can_reach(ut_state):
                                ut_state.collect(ut_event.item, prevent_sweep=True)

                    for loc in fresh_mw.get_locations(1):
                        if loc.address is None:
                            continue
                        ut_loc = ut_mw.get_location(loc.name, 1)
                        self.assertEqual(
                            ut_loc.can_reach(ut_state),
                            loc.can_reach(fresh_state),
                            f"UT logic mismatch on {loc.name} (collect_all={collect_all})",
                        )
                    self.assertEqual(
                        ut_mw.completion_condition[1](ut_state),
                        fresh_mw.completion_condition[1](fresh_state),
                    )

    def test_exported_poptracker_zip_archive(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            out_zip = Path(tmp) / "the_longing_poptracker.zip"
            build_tracker_pack.package_tracker_zip(out_zip)
            with ZipFile(out_zip, "r") as zf:
                names = set(zf.namelist())
                self.assertIn("manifest.json", names)
                self.assertIn("scripts/init.lua", names)
                self.assertIn("scripts/logic/logic.lua", names)
                self.assertIn("scripts/autotracking/archipelago.lua", names)
                self.assertIn("scripts/autotracking/item_mapping.lua", names)
                self.assertIn("scripts/autotracking/location_mapping.lua", names)
                self.assertIn("maps/maps.json", names)
                self.assertIn("locations/locations.json", names)
                self.assertIn("images/maps/cavern_map.png", names)
                self.assertIn("var_itemsonly/layouts/tracker.json", names)


if __name__ == "__main__":
    unittest.main()
