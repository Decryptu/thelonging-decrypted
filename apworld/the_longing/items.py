"""Item definitions and pool construction for The Longing APWorld."""
from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Item, ItemClassification

from .registry_data import ITEM_DEFINITIONS

if TYPE_CHECKING:
    from . import LongingWorld


CLASSIFICATION_MAP: dict[str, ItemClassification] = {
    "progression": ItemClassification.progression,
    "progression_skip_balancing": ItemClassification.progression_skip_balancing,
    "useful": ItemClassification.useful,
    "filler": ItemClassification.filler,
    "trap": ItemClassification.trap,
}

ITEM_BY_NAME: dict[str, dict[str, object]] = {str(entry["name"]): entry for entry in ITEM_DEFINITIONS}
ITEM_NAME_TO_ID: dict[str, int] = {str(entry["name"]): int(entry["id"]) for entry in ITEM_DEFINITIONS}

CRYSTAL_ITEM_NAMES: tuple[str, ...] = tuple(
    str(entry["name"]) for entry in ITEM_DEFINITIONS if entry["category"] == "crystals"
)
BOOK_ITEM_NAMES: tuple[str, ...] = tuple(
    str(entry["name"]) for entry in ITEM_DEFINITIONS if entry["category"] == "books"
)
TRAP_ITEM_NAMES: tuple[str, ...] = tuple(
    str(entry["name"]) for entry in ITEM_DEFINITIONS if entry["category"] == "trap"
)
CUSTOM_ITEM_NAMES: tuple[str, ...] = tuple(
    str(entry["name"]) for entry in ITEM_DEFINITIONS if entry["category"] == "custom"
)
COSMETIC_ITEM_NAMES: tuple[str, ...] = tuple(
    str(entry["name"]) for entry in ITEM_DEFINITIONS if entry["category"] == "cosmetic"
)

ITEM_NAME_GROUPS: dict[str, set[str]] = {
    "Tools": {"Mattock", "Secret Key", "Library Door Pass", "Surface Gate Pass", "Giant Mushroom Spores", "Cavern Map"},
    "Crystals": set(CRYSTAL_ITEM_NAMES),
    "Books": set(BOOK_ITEM_NAMES),
    "Time Skips": {
        "Time Skip (15 Minutes)",
        "Time Skip (1 Hour)",
        "Time Skip (6 Hours)",
        "Time Skip (1 Day)",
        "Time Rate Boost (+2x)",
        "King's Snooze Button",
    },
    "Traps": set(TRAP_ITEM_NAMES),
    "Mobility": {"Progressive Walk Speed", "Progressive Waypoint Recall"},
    "Custom": set(CUSTOM_ITEM_NAMES),
    "Cosmetics": set(COSMETIC_ITEM_NAMES),
}

MOBILITY_ITEMS = (("Progressive Walk Speed", 4), ("Progressive Waypoint Recall", 3))


class LongingItem(Item):
    game: str = "The Longing"


def create_item(world: LongingWorld, name: str, classification: ItemClassification | None = None) -> LongingItem:
    entry = ITEM_BY_NAME[name]
    if classification is not None:
        cls = classification
    elif name == "Cavern Moss" and world.options.shuffle_mushrooms_and_moss and world.options.shuffle_home_comfort:
        cls = ItemClassification.progression_skip_balancing
    elif name == "Drawing Paper" and world.options.shuffle_home_comfort:
        cls = ItemClassification.progression
    elif name == "Sulfur" and world.options.shuffle_coal_and_sulfur and world.options.shuffle_home_comfort:
        cls = ItemClassification.progression
    else:
        cls = CLASSIFICATION_MAP[str(entry["classification"])]
    return LongingItem(name, cls, int(entry["id"]), world.player)


def build_item_pool(world: LongingWorld, total_locations: int) -> tuple[list[LongingItem], int, int]:
    """Build the multiworld item pool sized to exact total_locations.

    Returns (items, crystals_in_pool, books_in_pool).
    """
    opts = world.options
    pool: list[LongingItem] = []
    start_speed = max(0, min(4, int(getattr(opts, "starting_walk_speed", 0))))
    for _ in range(start_speed):
        world.multiworld.push_precollected(create_item(world, "Progressive Walk Speed"))

    mobility: list[LongingItem] = []
    for name, count in MOBILITY_ITEMS:
        actual_count = max(0, count - start_speed) if name == "Progressive Walk Speed" else count
        for _ in range(actual_count):
            mobility.append(create_item(world, name))
    total_locations -= len(mobility)

    # 1. Core progression tools & passes
    if opts.shuffle_mattocks_and_keys:
        for _ in range(5):
            pool.append(create_item(world, "Mattock", ItemClassification.progression))
        pool.append(create_item(world, "Secret Key", ItemClassification.progression))
        pool.append(create_item(world, "Library Door Pass", ItemClassification.progression))
        pool.append(create_item(world, "Surface Gate Pass", ItemClassification.progression))
        pool.append(create_item(world, "Cavern Map", ItemClassification.useful))

    if opts.shuffle_mushrooms_and_moss or opts.shuffle_mattocks_and_keys:
        pool.append(create_item(world, "Giant Mushroom Spores", ItemClassification.progression))

    # 2. Crystals
    crystals_in_pool = 0
    is_hunt = opts.goal.value == opts.goal.option_crystal_book_hunt
    crystal_cls = ItemClassification.progression_skip_balancing if is_hunt else ItemClassification.useful
    if opts.shuffle_crystals.value == opts.shuffle_crystals.option_progression:
        for cname in CRYSTAL_ITEM_NAMES:
            pool.append(create_item(world, cname, crystal_cls))
            crystals_in_pool += 1
    elif opts.shuffle_crystals.value == opts.shuffle_crystals.option_full:
        crystal_counts = {
            "White Crystal": 6,
            "Rose Crystal": 4,
            "Blue Crystal": 2,
            "Purple Crystal": 1,
            "Black Crystal": 1,
            "Great Hall Big Crystal": 1,
            "Tower Big Crystal": 1,
        }
        for cname, count in crystal_counts.items():
            for _ in range(count):
                pool.append(create_item(world, cname, crystal_cls))
                crystals_in_pool += 1

    # 3. Books
    books_in_pool = 0
    book_cls = ItemClassification.progression_skip_balancing if is_hunt else ItemClassification.useful
    if opts.shuffle_books.value == opts.shuffle_books.option_progression:
        for title in BOOK_ITEM_NAMES[:16]:
            pool.append(create_item(world, title, book_cls))
            books_in_pool += 1
    elif opts.shuffle_books.value == opts.shuffle_books.option_full:
        for title in BOOK_ITEM_NAMES:
            pool.append(create_item(world, title, book_cls))
            books_in_pool += 1

    # 4. Musical instrument parts
    if opts.shuffle_instrument_parts:
        for _ in range(3):
            pool.append(create_item(world, "Musical Instrument Part", ItemClassification.progression))

    # 5. Mushrooms & Moss
    if opts.shuffle_mushrooms_and_moss:
        moss_cls = (
            ItemClassification.progression_skip_balancing
            if opts.shuffle_home_comfort
            else ItemClassification.useful
        )
        for _ in range(15):
            pool.append(create_item(world, "Cavern Moss", moss_cls))
        for _ in range(3):
            pool.append(create_item(world, "Green Mushroom", ItemClassification.useful))
        for _ in range(3):
            pool.append(create_item(world, "Purple Mushroom", ItemClassification.useful))

    # 6. Home Comforts & Relics
    if opts.shuffle_home_comfort:
        comfort_items: list[tuple[str, int, ItemClassification]] = [
            ("Drawing Paper", 3, ItemClassification.progression),
            ("White Chalk", 2, ItemClassification.useful),
            ("Red Clay", 1, ItemClassification.useful),
            ("Lapis Lazuli", 5, ItemClassification.useful),
            ("Spider Flower", 1, ItemClassification.useful),
            ("Golden Coin", 1, ItemClassification.useful),
            ("Painting of the King", 1, ItemClassification.useful),
            ("Horse Head Trophy", 1, ItemClassification.useful),
            ("Birthday Hourglass", 1, ItemClassification.useful),
            ("Labyrinth Keepsake Stone", 1, ItemClassification.useful),
            ("Eternal Carpet II", 1, ItemClassification.useful),
            ("Eternal Carpet III", 1, ItemClassification.useful),
            ("Eternal Curtain", 1, ItemClassification.useful),
        ]
        for name, count, cls in comfort_items:
            for idx in range(count):
                if cls == ItemClassification.progression:
                    item_cls = cls if idx == 0 else ItemClassification.progression_skip_balancing
                else:
                    item_cls = cls
                pool.append(create_item(world, name, item_cls))

    # 7. Coal, Wood, Flint & Sulfur
    if opts.shuffle_coal_and_sulfur:
        for idx in range(2):
            if opts.shuffle_home_comfort:
                sulfur_cls = (
                    ItemClassification.progression
                    if idx == 0
                    else ItemClassification.progression_skip_balancing
                )
            else:
                sulfur_cls = ItemClassification.useful
            pool.append(create_item(world, "Sulfur", sulfur_cls))
        for _ in range(4):
            pool.append(create_item(world, "Flint", ItemClassification.useful))
        for _ in range(8):
            pool.append(create_item(world, "Coal", ItemClassification.filler))
        for _ in range(6):
            pool.append(create_item(world, "Wood", ItemClassification.filler))

    # 8. Custom utility items & cosmetics (if enabled)
    if getattr(opts, "custom_items", True):
        custom_pack: list[tuple[str, int, ItemClassification]] = [
            ("Shade's Lantern", 1, ItemClassification.useful),
            ("Chisel of Efficiency", 1, ItemClassification.useful),
            ("Bottomless Moss Sack", 1, ItemClassification.useful),
            ("Echoing Conch", 2, ItemClassification.useful),
            ("Mushroom Fertilizer", 2, ItemClassification.useful),
            ("King's Snooze Button", 2, ItemClassification.useful),
            ("Double Espresso", 2, ItemClassification.filler),
        ]
        for name, count, cls in custom_pack:
            for _ in range(count):
                if len(pool) >= total_locations:
                    break
                pool.append(create_item(world, name, cls))

        cosmetics_shuffled = list(COSMETIC_ITEM_NAMES)
        world.random.shuffle(cosmetics_shuffled)
        for cos_name in cosmetics_shuffled:
            if len(pool) >= total_locations:
                break
            pool.append(create_item(world, cos_name, ItemClassification.filler))

    # 9. Time-skip & pacing items (if space permits and enabled)
    remaining = total_locations - len(pool)
    if opts.time_skip_items and remaining > 0:
        pacing_pack: list[tuple[str, int, ItemClassification]] = [
            ("Time Skip (1 Day)", 2, ItemClassification.useful),
            ("Time Skip (6 Hours)", 3, ItemClassification.useful),
            ("Time Rate Boost (+2x)", 4, ItemClassification.useful),
            ("Time Skip (1 Hour)", 6, ItemClassification.useful),
            ("Time Skip (15 Minutes)", 6, ItemClassification.filler),
        ]
        # A single skip must never swallow more than a quarter of the countdown.
        longest_skip = opts.countdown_length.to_seconds() // 4
        for name, count, cls in pacing_pack:
            entry = ITEM_BY_NAME[name]
            if entry["action"] == "time_skip" and int(entry["amount"]) > longest_skip:
                continue
            for _ in range(count):
                if len(pool) >= total_locations:
                    break
                pool.append(create_item(world, name, cls))

    # Trim non-progression items only if pool ever exceeds total_locations (edge case)
    if len(pool) > total_locations:
        essential = [it for it in pool if it.advancement]
        optional = [it for it in pool if not it.advancement]
        world.random.shuffle(optional)
        keep_opt = max(0, total_locations - len(essential))
        pool = essential + optional[:keep_opt]
        if len(pool) > total_locations:
            pool = pool[:total_locations]

    # 9. Fill remaining slots with Traps + Filler
    remaining = total_locations - len(pool)
    trap_pct = opts.trap_density.to_percentage()
    trap_count = (remaining * trap_pct) // 100 if remaining > 0 else 0
    for _ in range(trap_count):
        trap_name = world.random.choice(TRAP_ITEM_NAMES)
        pool.append(create_item(world, trap_name, ItemClassification.trap))

    while len(pool) < total_locations:
        pool.append(create_item(world, world.get_filler_item_name(), ItemClassification.filler))

    return pool + mobility, crystals_in_pool, books_in_pool
