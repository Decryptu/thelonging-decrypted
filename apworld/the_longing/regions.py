"""Region graph construction for The Longing APWorld."""
from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import Region

from .locations import LongingLocation, get_enabled_locations

if TYPE_CHECKING:
    from . import LongingWorld


REGION_NAMES: tuple[str, ...] = (
    "Menu",
    "Shade's Home",
    "The King's Hall",
    "Mystery Rooms",
    "The Great Halls",
    "Secret Hall",
    "The Library",
    "Halls of Eternity",
    "Mushroom Groves",
    "Crystal Cavern & Mines",
    "Secret Tower",
    "The Well & Surface",
)


def create_regions(world: LongingWorld) -> list[dict[str, object]]:
    """Create all Underground Kingdom regions, connections, enabled checks, and victory events."""
    regions: dict[str, Region] = {name: Region(name, world.player, world.multiworld) for name in REGION_NAMES}
    world.multiworld.regions.extend(regions.values())

    # Connect cavern passages
    regions["Menu"].connect(regions["Shade's Home"], "Enter the Underground Kingdom")
    regions["Shade's Home"].connect(regions["The King's Hall"], "Descend to the King's Hall")
    regions["The King's Hall"].connect(regions["The Great Halls"], "Enter the Great Halls")
    regions["The King's Hall"].connect(regions["Mystery Rooms"], "Unlock Mystery Rooms")
    regions["The Great Halls"].connect(regions["The Library"], "Open the Library Door")
    regions["The Great Halls"].connect(regions["Halls of Eternity"], "Traverse the Labyrinth")
    regions["The Great Halls"].connect(regions["Secret Hall"], "Unlock Secret Hall Door")
    regions["The Great Halls"].connect(regions["Mushroom Groves"], "Open Upper Exit Gate")
    regions["Mushroom Groves"].connect(regions["Crystal Cavern & Mines"], "Enter the Mines")
    regions["Mushroom Groves"].connect(regions["Secret Tower"], "Ascend to the Secret Tower")
    regions["Secret Tower"].connect(regions["The Well & Surface"], "Climb toward the Well")

    # Populate enabled physical & milestone locations
    enabled_locations = get_enabled_locations(world)
    for entry in enabled_locations:
        region_name = str(entry["ap_region"])
        loc = LongingLocation(world.player, str(entry["name"]), int(entry["id"]), regions[region_name])
        regions[region_name].locations.append(loc)

    # Add victory event locations (None ID so they are locked to event items and excluded from itempool)
    wake_king = LongingLocation(world.player, "Wake the Sleeping King", None, regions["The King's Hall"])
    regions["The King's Hall"].locations.append(wake_king)

    escape_surface = LongingLocation(world.player, "Escape via the Well", None, regions["The Well & Surface"])
    regions["The Well & Surface"].locations.append(escape_surface)

    secret_ending = LongingLocation(world.player, "Reach the Secret Ending", None, regions["Secret Tower"])
    regions["Secret Tower"].locations.append(secret_ending)

    hunt_complete = LongingLocation(world.player, "Complete Crystal & Book Collection", None, regions["Shade's Home"])
    regions["Shade's Home"].locations.append(hunt_complete)

    return enabled_locations
