"""Archipelago options for The Longing."""
from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from Options import Choice, DeathLink, DefaultOnToggle, OptionGroup, PerGameCommonOptions, Range, Toggle


class Goal(Choice):
    """Determines the victory condition for completing your slot in The Longing.

    - wake_the_king: Reduce the countdown to zero and wake the sleeping King.
    - escape_surface: Climb through the upper caverns and escape via the Well to the Surface.
    - secret_ending: Reach the Secret Tower and unlock the hidden ending.
    - crystal_book_hunt: Collect the required number of Crystals and Classic Books and return Home.
    - any_ending: Complete any ending (Wake the King, Escape to the Surface, or Secret Ending).
    """

    display_name = "Goal"
    option_wake_the_king = 0
    option_escape_surface = 1
    option_secret_ending = 2
    option_crystal_book_hunt = 3
    option_any_ending = 4
    default = 0


class CountdownLength(Choice):
    """Starting countdown duration on the 400-day clock for this multiworld slot."""

    display_name = "Countdown Length"
    option_1_hour = 0
    option_6_hours = 1
    option_24_hours = 2
    option_40_days = 3
    option_400_days = 4
    default = 1

    def to_seconds(self) -> int:
        return {
            self.option_1_hour: 3_600,
            self.option_6_hours: 21_600,
            self.option_24_hours: 86_400,
            self.option_40_days: 40 * 86_400,
            self.option_400_days: 400 * 86_400,
        }[self.value]


class StartingTimeRate(Range):
    """Base rate at which the countdown progresses (1x to 100x real time)."""

    display_name = "Starting Time Rate"
    range_start = 1
    range_end = 100
    default = 12


class TimeSkipItems(DefaultOnToggle):
    """Include Time Skip and Time Rate Boost items in the multiworld item pool."""

    display_name = "Time Skip Items"


class ShuffleMattocksAndKeys(DefaultOnToggle):
    """Shuffle Mattocks, the Secret Key, and major cavern passage passes into the multiworld."""

    display_name = "Shuffle Mattocks and Keys"


class ShuffleBooks(Choice):
    """Controls how classic literature volumes across the caverns are randomized.

    - vanilla: Books remain vanilla pickups and are excluded from AP checks.
    - progression: Key shelf volumes and Halls of Eternity book milestones are shuffled.
    - full: All 31 placed cavern book pickups are shuffled into the multiworld.
    """

    display_name = "Shuffle Books"
    option_vanilla = 0
    option_progression = 1
    option_full = 2
    default = 1


class ShuffleCrystals(Choice):
    """Controls how cavern crystal veins and giant crystals are randomized.

    - vanilla: Crystals remain vanilla pickups and are excluded from AP checks.
    - progression: Major crystal types (1 per color + Big Crystals) are shuffled.
    - full: All 16 crystal deposits across the caverns are shuffled.
    """

    display_name = "Shuffle Crystals"
    option_vanilla = 0
    option_progression = 1
    option_full = 2
    default = 2


class ShuffleMushroomsAndMoss(DefaultOnToggle):
    """Shuffle cavern moss patches, Giant Mushroom Spores, and mushroom growth milestones."""

    display_name = "Shuffle Mushrooms and Moss"


class ShuffleHomeComfort(DefaultOnToggle):
    """Shuffle home comfort items, drawing supplies, relics, and home renovation milestones."""

    display_name = "Shuffle Home Comfort"


class ShuffleInstrumentParts(DefaultOnToggle):
    """Shuffle the 3 musical instrument (didgeridoo) parts and the home music milestone."""

    display_name = "Shuffle Instrument Parts"


class ShuffleCoalAndSulfur(Toggle):
    """Shuffle coal lumps, timber, flint, and mine sulfur deposits into the multiworld."""

    display_name = "Shuffle Coal and Sulfur"


class RequiredCrystals(Range):
    """Number of Crystals required when Goal is set to Crystal & Book Hunt."""

    display_name = "Required Crystals"
    range_start = 1
    range_end = 16
    default = 8


class RequiredBooks(Range):
    """Number of Classic Books required when Goal is set to Crystal & Book Hunt."""

    display_name = "Required Books"
    range_start = 1
    range_end = 30
    default = 12


class TrapDensity(Choice):
    """How heavily filler slots are replaced by cavern traps."""

    display_name = "Trap Density"
    option_none = 0
    option_light = 1
    option_moderate = 2
    option_heavy = 3
    default = 1

    def to_percentage(self) -> int:
        return {
            self.option_none: 0,
            self.option_light: 12,
            self.option_moderate: 25,
            self.option_heavy: 40,
        }[self.value]


@dataclass
class LongingOptions(PerGameCommonOptions):
    goal: Goal
    countdown_length: CountdownLength
    starting_time_rate: StartingTimeRate
    time_skip_items: TimeSkipItems
    shuffle_mattocks_and_keys: ShuffleMattocksAndKeys
    shuffle_books: ShuffleBooks
    shuffle_crystals: ShuffleCrystals
    shuffle_mushrooms_and_moss: ShuffleMushroomsAndMoss
    shuffle_home_comfort: ShuffleHomeComfort
    shuffle_instrument_parts: ShuffleInstrumentParts
    shuffle_coal_and_sulfur: ShuffleCoalAndSulfur
    required_crystals: RequiredCrystals
    required_books: RequiredBooks
    trap_density: TrapDensity
    death_link: DeathLink


OPTION_GROUPS = [
    OptionGroup(
        "Goal & Pacing",
        [
            Goal,
            CountdownLength,
            StartingTimeRate,
            TimeSkipItems,
            RequiredCrystals,
            RequiredBooks,
        ],
    ),
    OptionGroup(
        "Cavern Check Pools",
        [
            ShuffleMattocksAndKeys,
            ShuffleBooks,
            ShuffleCrystals,
            ShuffleMushroomsAndMoss,
            ShuffleHomeComfort,
            ShuffleInstrumentParts,
            ShuffleCoalAndSulfur,
        ],
    ),
    OptionGroup(
        "Multiworld Modifiers",
        [
            TrapDensity,
            DeathLink,
        ],
    ),
]


def export_option_schema() -> dict[str, Any]:
    """Export canonical option metadata for syncing the player hub YAML generator."""
    return {
        "game": "The Longing",
        "world_version": "0.1.0",
        "options": {
            "goal": {
                "type": "choice",
                "values": [
                    "wake_the_king",
                    "escape_surface",
                    "secret_ending",
                    "crystal_book_hunt",
                    "any_ending",
                ],
                "default": "wake_the_king",
            },
            "countdown_length": {
                "type": "choice",
                "values": ["1_hour", "6_hours", "24_hours", "40_days", "400_days"],
                "default": "6_hours",
            },
            "starting_time_rate": {
                "type": "range",
                "min": 1,
                "max": 100,
                "default": 12,
            },
            "time_skip_items": {"type": "toggle", "default": True},
            "shuffle_mattocks_and_keys": {"type": "toggle", "default": True},
            "shuffle_books": {
                "type": "choice",
                "values": ["vanilla", "progression", "full"],
                "default": "progression",
            },
            "shuffle_crystals": {
                "type": "choice",
                "values": ["vanilla", "progression", "full"],
                "default": "full",
            },
            "shuffle_mushrooms_and_moss": {"type": "toggle", "default": True},
            "shuffle_home_comfort": {"type": "toggle", "default": True},
            "shuffle_instrument_parts": {"type": "toggle", "default": True},
            "shuffle_coal_and_sulfur": {"type": "toggle", "default": False},
            "required_crystals": {"type": "range", "min": 1, "max": 16, "default": 8},
            "required_books": {"type": "range", "min": 1, "max": 30, "default": 12},
            "trap_density": {
                "type": "choice",
                "values": ["none", "light", "moderate", "heavy"],
                "default": "light",
            },
            "death_link": {"type": "toggle", "default": False},
        },
    }
