"""Reachability rules and victory conditions for The Longing APWorld."""
from __future__ import annotations

from typing import TYPE_CHECKING

from BaseClasses import CollectionState, ItemClassification
from worlds.generic.Rules import add_rule, set_rule

from .items import LongingItem

if TYPE_CHECKING:
    from . import LongingWorld


def _has_mattocks(state: CollectionState, world: LongingWorld, count: int) -> bool:
    if not world.options.shuffle_mattocks_and_keys:
        return True
    return state.has("Mattock", world.player, count)


def _has_secret_key(state: CollectionState, world: LongingWorld) -> bool:
    if not world.options.shuffle_mattocks_and_keys:
        return True
    return state.has("Secret Key", world.player)


def _has_library_pass(state: CollectionState, world: LongingWorld) -> bool:
    if not world.options.shuffle_mattocks_and_keys:
        return True
    return state.has("Library Door Pass", world.player)


def _has_surface_gate(state: CollectionState, world: LongingWorld) -> bool:
    if not world.options.shuffle_mattocks_and_keys:
        return True
    return state.has("Surface Gate Pass", world.player)


def _has_giant_spores(state: CollectionState, world: LongingWorld) -> bool:
    if not (world.options.shuffle_mushrooms_and_moss or world.options.shuffle_mattocks_and_keys):
        return True
    return state.has("Giant Mushroom Spores", world.player)


def set_rules(world: LongingWorld, enabled_locations: list[dict[str, object]]) -> None:
    """Attach region entrance rules, location check rules, and goal completion condition."""
    player = world.player
    multiworld = world.multiworld
    opts = world.options

    # Entrance rules
    set_rule(
        multiworld.get_entrance("Unlock Mystery Rooms", player),
        lambda state: _has_secret_key(state, world),
    )
    set_rule(
        multiworld.get_entrance("Open the Library Door", player),
        lambda state: _has_library_pass(state, world),
    )
    set_rule(
        multiworld.get_entrance("Unlock Secret Hall Door", player),
        lambda state: _has_secret_key(state, world),
    )
    set_rule(
        multiworld.get_entrance("Open Upper Exit Gate", player),
        lambda state: _has_surface_gate(state, world),
    )
    set_rule(
        multiworld.get_entrance("Ascend to the Secret Tower", player),
        lambda state: _has_giant_spores(state, world),
    )

    # Per-location rules
    for entry in enabled_locations:
        reqs = list(entry.get("requires", []))
        if not reqs:
            continue
        loc = multiworld.get_location(str(entry["name"]), player)
        for req in reqs:
            if req == "mattock_1":
                add_rule(loc, lambda state: _has_mattocks(state, world, 1))
            elif req == "mattock_2":
                add_rule(loc, lambda state: _has_mattocks(state, world, 2))
            elif req == "giant_spores":
                add_rule(loc, lambda state: _has_giant_spores(state, world))
            elif req == "moss_10":
                if opts.shuffle_mushrooms_and_moss:
                    add_rule(loc, lambda state: state.has("Cavern Moss", player, 10))
            elif req == "sulfur":
                if opts.shuffle_coal_and_sulfur:
                    add_rule(loc, lambda state: state.has("Sulfur", player, 1))
                else:
                    add_rule(
                        loc,
                        lambda state: state.can_reach_region("Crystal Cavern & Mines", player)
                        and _has_mattocks(state, world, 1),
                    )
            elif req == "instrument_3":
                if opts.shuffle_instrument_parts:
                    add_rule(loc, lambda state: state.has("Musical Instrument Part", player, 3))
            elif req == "paper":
                if opts.shuffle_home_comfort:
                    add_rule(loc, lambda state: state.has("Drawing Paper", player, 1))

    # Lock victory events
    wake_loc = multiworld.get_location("Wake the Sleeping King", player)
    wake_loc.place_locked_item(LongingItem("Victory: Wake the King", ItemClassification.progression, None, player))
    # Require meaningful cavern progression (Mattock + Surface Gate when shuffled) before waking the King
    set_rule(
        wake_loc,
        lambda state: _has_mattocks(state, world, 1) and _has_surface_gate(state, world),
    )

    escape_loc = multiworld.get_location("Escape via the Well", player)
    escape_loc.place_locked_item(LongingItem("Victory: Escape to the Surface", ItemClassification.progression, None, player))

    secret_loc = multiworld.get_location("Reach the Secret Ending", player)
    secret_loc.place_locked_item(LongingItem("Victory: Secret Ending", ItemClassification.progression, None, player))
    set_rule(secret_loc, lambda state: _has_secret_key(state, world))

    # Set multiworld completion condition according to selected goal
    goal_val = opts.goal.value
    if goal_val == opts.goal.option_wake_the_king:
        multiworld.completion_condition[player] = lambda state: state.has("Victory: Wake the King", player)
    elif goal_val == opts.goal.option_escape_surface:
        multiworld.completion_condition[player] = lambda state: state.has("Victory: Escape to the Surface", player)
    elif goal_val == opts.goal.option_secret_ending:
        multiworld.completion_condition[player] = lambda state: state.has("Victory: Secret Ending", player)
    elif goal_val == opts.goal.option_crystal_book_hunt:
        hunt_loc = multiworld.get_location("Complete Crystal & Book Collection", player)
        hunt_loc.place_locked_item(LongingItem("Victory: Crystal & Book Hunt", ItemClassification.progression, None, player))
        req_crystals = world.effective_required_crystals
        req_books = world.effective_required_books

        def hunt_rule(state: CollectionState) -> bool:
            if req_crystals == 0 and req_books == 0:
                return (
                    state.can_reach_region("The Library", player)
                    and state.can_reach_region("Crystal Cavern & Mines", player)
                    and _has_mattocks(state, world, 1)
                )
            return (
                state.has_group("Crystals", player, req_crystals)
                and state.has_group("Books", player, req_books)
            )

        set_rule(hunt_loc, hunt_rule)
        multiworld.completion_condition[player] = lambda state: state.has("Victory: Crystal & Book Hunt", player)
    else:
        multiworld.completion_condition[player] = lambda state: (
            state.has("Victory: Wake the King", player)
            or state.has("Victory: Escape to the Surface", player)
            or state.has("Victory: Secret Ending", player)
        )
