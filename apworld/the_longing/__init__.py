"""Archipelago world implementation for The Longing (Studio Seufz)."""
from __future__ import annotations

from typing import Any, ClassVar

from worlds.AutoWorld import World

from .items import (
    ITEM_NAME_GROUPS,
    ITEM_NAME_TO_ID,
    LongingItem,
    build_item_pool,
    create_item,
)
from .locations import LOCATION_NAME_GROUPS, LOCATION_NAME_TO_ID
from .options import LongingOptions
from .regions import create_regions
from .rules import set_rules
from .tracker_mapping import POPTRACKER_LOCATION_MAPPING
from .web_world import LongingWebWorld


class LongingWorld(World):
    """Explore the Underground Kingdom, furnish the Shade's home, and exchange cavern relics across Archipelago."""

    game: ClassVar[str] = "The Longing"
    web: ClassVar[LongingWebWorld] = LongingWebWorld()
    options_dataclass = LongingOptions
    options: LongingOptions

    item_name_to_id: ClassVar[dict[str, int]] = ITEM_NAME_TO_ID
    location_name_to_id: ClassVar[dict[str, int]] = LOCATION_NAME_TO_ID
    item_name_groups: ClassVar[dict[str, set[str]]] = ITEM_NAME_GROUPS
    location_name_groups: ClassVar[dict[str, set[str]]] = LOCATION_NAME_GROUPS

    # Universal Tracker (UT) YAML-less regeneration & PopTracker map page integration
    ut_can_gen_without_yaml: ClassVar[bool] = True
    tracker_world: ClassVar[dict[str, Any]] = {
        "map_page_folder": "tracker",
        "map_page_maps": ["maps/maps.json"],
        "map_page_locations": ["locations/locations.json"],
        "poptracker_name_mapping": POPTRACKER_LOCATION_MAPPING,
    }

    def __init__(self, multiworld: Any, player: int) -> None:
        super().__init__(multiworld, player)
        self.enabled_locations: list[dict[str, object]] = []
        self.crystals_in_pool: int = 0
        self.books_in_pool: int = 0
        self.effective_required_crystals: int = 0
        self.effective_required_books: int = 0
        self.ut_enabled_location_ids: set[int] | None = None
        self.ut_required_crystals: int | None = None
        self.ut_required_books: int | None = None

    @staticmethod
    def interpret_slot_data(slot_data: dict[str, Any]) -> dict[str, Any]:
        """Pass server slot_data through to Universal Tracker regeneration."""
        return dict(slot_data)

    def _apply_ut_passthrough(self) -> dict[str, Any] | None:
        passthrough = getattr(self.multiworld, "re_gen_passthrough", None)
        if not isinstance(passthrough, dict) or self.game not in passthrough:
            return None
        slot_data = passthrough[self.game]
        if not isinstance(slot_data, dict):
            return None

        option_keys = (
            "goal",
            "countdown_length",
            "starting_time_rate",
            "time_skip_items",
            "shuffle_mattocks_and_keys",
            "shuffle_books",
            "shuffle_crystals",
            "shuffle_mushrooms_and_moss",
            "shuffle_home_comfort",
            "shuffle_instrument_parts",
            "shuffle_coal_and_sulfur",
            "required_crystals",
            "required_books",
            "trap_density",
            "death_link",
        )
        for key in option_keys:
            if key in slot_data and key in LongingOptions.type_hints:
                opt_cls = LongingOptions.type_hints[key]
                raw = slot_data[key]
                # Clamp range options safely if slot_data holds a 0 target (e.g. hunt with vanilla books)
                if key in ("required_crystals", "required_books") and isinstance(raw, int) and raw < opt_cls.range_start:
                    continue
                setattr(self.options, key, opt_cls.from_any(raw))

        raw_ids = slot_data.get("enabled_location_ids")
        if isinstance(raw_ids, list):
            self.ut_enabled_location_ids = {int(loc_id) for loc_id in raw_ids}
        return slot_data

    def generate_early(self) -> None:
        ut_slot_data = self._apply_ut_passthrough()

        # Compute effective crystal/book targets early so rules and slot_data always agree
        if self.options.shuffle_crystals.value == self.options.shuffle_crystals.option_full:
            self.crystals_in_pool = 16
        elif self.options.shuffle_crystals.value == self.options.shuffle_crystals.option_progression:
            self.crystals_in_pool = 7
        else:
            self.crystals_in_pool = 0

        if self.options.shuffle_books.value == self.options.shuffle_books.option_full:
            self.books_in_pool = 31
        elif self.options.shuffle_books.value == self.options.shuffle_books.option_progression:
            self.books_in_pool = 16
        else:
            self.books_in_pool = 0

        if ut_slot_data is not None and "required_crystals" in ut_slot_data:
            self.ut_required_crystals = int(ut_slot_data["required_crystals"])
            self.effective_required_crystals = self.ut_required_crystals
        else:
            self.effective_required_crystals = min(self.options.required_crystals.value, self.crystals_in_pool)

        if ut_slot_data is not None and "required_books" in ut_slot_data:
            self.ut_required_books = int(ut_slot_data["required_books"])
            self.effective_required_books = self.ut_required_books
        else:
            self.effective_required_books = min(self.options.required_books.value, self.books_in_pool)

    def create_regions(self) -> None:
        self.enabled_locations = create_regions(self)

    def create_item(self, name: str) -> LongingItem:
        return create_item(self, name)

    def get_filler_item_name(self) -> str:
        if self.options.time_skip_items:
            return self.random.choice(
                (
                    "Time Skip (15 Minutes)",
                    "Time Skip (15 Minutes)",
                    "Coal",
                    "Wood",
                    "Quiet Reflection",
                )
            )
        return self.random.choice(("Coal", "Wood", "Quiet Reflection"))

    def create_items(self) -> None:
        total_locations = len(self.enabled_locations)
        pool, crystals_count, books_count = build_item_pool(self, total_locations)
        self.crystals_in_pool = crystals_count
        self.books_in_pool = books_count
        self.effective_required_crystals = (
            self.ut_required_crystals
            if self.ut_required_crystals is not None
            else min(self.options.required_crystals.value, crystals_count)
        )
        self.effective_required_books = (
            self.ut_required_books
            if self.ut_required_books is not None
            else min(self.options.required_books.value, books_count)
        )
        self.multiworld.itempool.extend(pool)

    def set_rules(self) -> None:
        set_rules(self, self.enabled_locations)

    def fill_slot_data(self) -> dict[str, Any]:
        return {
            "world_version": "0.1.0",
            "goal": self.options.goal.current_key,
            "countdown_length": self.options.countdown_length.current_key,
            "countdown_seconds": self.options.countdown_length.to_seconds(),
            "starting_time_rate": self.options.starting_time_rate.value,
            "time_skip_items": bool(self.options.time_skip_items.value),
            "shuffle_mattocks_and_keys": bool(self.options.shuffle_mattocks_and_keys.value),
            "shuffle_books": self.options.shuffle_books.current_key,
            "shuffle_crystals": self.options.shuffle_crystals.current_key,
            "shuffle_mushrooms_and_moss": bool(self.options.shuffle_mushrooms_and_moss.value),
            "shuffle_home_comfort": bool(self.options.shuffle_home_comfort.value),
            "shuffle_instrument_parts": bool(self.options.shuffle_instrument_parts.value),
            "shuffle_coal_and_sulfur": bool(self.options.shuffle_coal_and_sulfur.value),
            "required_crystals": self.effective_required_crystals,
            "required_books": self.effective_required_books,
            "trap_density": self.options.trap_density.current_key,
            "death_link": bool(self.options.death_link.value),
            "enabled_location_ids": [int(loc["id"]) for loc in self.enabled_locations],
        }
