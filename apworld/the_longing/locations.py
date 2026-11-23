"""Location definitions and option-filtered location selection for The Longing APWorld."""
from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Location

from .registry_data import LOCATION_DEFINITIONS

if TYPE_CHECKING:
    from . import LongingWorld


LOCATION_BY_NAME: dict[str, dict[str, object]] = {str(entry["name"]): entry for entry in LOCATION_DEFINITIONS}
LOCATION_NAME_TO_ID: dict[str, int] = {str(entry["name"]): int(entry["id"]) for entry in LOCATION_DEFINITIONS}

LOCATION_NAME_GROUPS: dict[str, set[str]] = {}
for _entry in LOCATION_DEFINITIONS:
    _region = str(_entry["ap_region"])
    LOCATION_NAME_GROUPS.setdefault(_region, set()).add(str(_entry["name"]))


class LongingLocation(Location):
    game: str = "The Longing"


def get_enabled_locations(world: LongingWorld) -> list[dict[str, object]]:
    """Return all locations enabled by the player's YAML shuffle options."""
    ut_ids = getattr(world, "ut_enabled_location_ids", None)
    if ut_ids is not None:
        return [entry for entry in LOCATION_DEFINITIONS if int(entry["id"]) in ut_ids]

    opts = world.options
    enabled: list[dict[str, object]] = []

    for entry in LOCATION_DEFINITIONS:
        pool = str(entry["pool"])
        depth = str(entry["depth"])

        if pool == "tools":
            if opts.shuffle_mattocks_and_keys:
                enabled.append(entry)
        elif pool == "books":
            if opts.shuffle_books.value == opts.shuffle_books.option_full:
                enabled.append(entry)
            elif opts.shuffle_books.value == opts.shuffle_books.option_progression and depth == "progression":
                enabled.append(entry)
        elif pool == "crystals":
            if opts.shuffle_crystals.value == opts.shuffle_crystals.option_full:
                enabled.append(entry)
            elif opts.shuffle_crystals.value == opts.shuffle_crystals.option_progression and depth == "progression":
                enabled.append(entry)
        elif pool == "mushrooms_moss":
            if opts.shuffle_mushrooms_and_moss:
                enabled.append(entry)
        elif pool == "comfort":
            if opts.shuffle_home_comfort:
                enabled.append(entry)
        elif pool == "instrument":
            if opts.shuffle_instrument_parts:
                enabled.append(entry)
        elif pool == "coal_sulfur":
            if opts.shuffle_coal_and_sulfur:
                enabled.append(entry)
        elif pool == "shop":
            if opts.shuffle_shop:
                enabled.append(entry)

    # Guarantee at least the core tool/milestone checks if every single pool was toggled off
    if len(enabled) < 10:
        existing_ids = {int(e["id"]) for e in enabled}
        for entry in LOCATION_DEFINITIONS:
            if str(entry["pool"]) in ("tools", "comfort") and int(entry["id"]) not in existing_ids:
                enabled.append(entry)
                existing_ids.add(int(entry["id"]))
            if len(enabled) >= 12:
                break

    enabled.extend(entry for entry in LOCATION_DEFINITIONS if entry["pool"] == "mobility")
    return enabled
