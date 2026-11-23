"""Generate synchronized world registries for APWorld, BepInEx mod, and player hub from data/world_catalog.json."""
from __future__ import annotations

import json
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CATALOG_PATH = ROOT / "data" / "world_catalog.json"
APWORLD_OUT = ROOT / "apworld" / "the_longing" / "registry_data.py"
MOD_OUT = ROOT / "mod" / "Generated" / "WorldRegistryData.cs"
SITE_OUT = ROOT / "site" / "src" / "data" / "catalogData.ts"

BASE_ITEM_ID = 400_400_000
BASE_LOCATION_ID = 400_401_000

SCENE_LABELS: dict[str, str] = {
    "home": "Shade's Home",
    "king": "King's Chamber",
    "king_stairs": "King's Stairs",
    "entrance_hall": "Entrance Hall",
    "treasury": "King's Treasury",
    "gallery": "Gallery",
    "entrancehall2": "Upper Entrance Hall",
    "entrancehall3": "North Entrance Hall",
    "mysteryroom_01": "Mystery Room I",
    "mysteryroom_02": "Mystery Room II",
    "hall_R_01": "East Hall I",
    "hall_R_02": "East Hall II",
    "hall_R_03": "East Hall III",
    "hall_R_04": "East Hall IV",
    "hall_R_05": "East Hall V",
    "hall_R_06": "East Hall VI",
    "hall_R_07": "East Hall VII",
    "hall_R_08": "East Hall VIII",
    "hall_L_01": "West Hall I",
    "hall_L_02": "West Hall II",
    "hall_L_03": "West Hall III",
    "hall_L_04": "West Hall IV",
    "hall_L_05": "Secret Hall",
    "hall_L_06": "West Hall VI",
    "hall L 06A": "West Hall VI-A",
    "hall_L06_behind": "West Hall VI Rear",
    "hall_L_07": "West Hall VII",
    "hall_L_08": "West Hall VIII",
    "hall_L_09": "West Hall IX",
    "moss_cliff": "Moss Cliff",
    "face": "Face Chamber",
    "grotto_01": "Moss Grotto I",
    "grotto_02": "Moss Grotto II",
    "up_01": "Upper Stairway",
    "up_07": "Upper Exit Gate",
    "entrance_library": "Library Vestibule",
    "library": "The Library",
    "entrance_labyrinth": "Labyrinth Entrance",
    "labyrinth_01": "Labyrinth I",
    "labyrinth_02": "Labyrinth II",
    "labyrinth_03": "Labyrinth III",
    "labyrinth_04": "Labyrinth IV",
    "labyrinth_05": "Labyrinth V",
    "labyrinth_06": "Labyrinth VI",
    "labyrinth_07": "Labyrinth VII",
    "labyrinth_08": "Labyrinth VIII",
    "labyrinth_09": "Labyrinth IX",
    "labyrinth_10": "Labyrinth X",
    "labyrinth_11": "Labyrinth XI",
    "labyrinth 11A": "Labyrinth XI",
    "labyrinth_12": "Labyrinth XII",
    "labyrinth_13": "Labyrinth XIII",
    "labyrinth_14": "Labyrinth XIV",
    "labyrinth 14A": "Labyrinth XIV",
    "labyrinth_15": "Labyrinth XV",
    "labyrinth_16": "Labyrinth XVI",
    "entrance_eternity": "Eternity Threshold",
    "halls_of_eternity": "Halls of Eternity",
    "exit": "Upper Cavern Exit",
    "greatCave_complete": "Great Cave",
    "mine_01": "Mine Entrance",
    "mine_03": "Sulfur Mine",
    "mine_04": "Coal Mine",
    "tower": "Secret Tower",
    "brunnen": "The Well",
    "oberwelt": "The Surface",
}

# Logical AP sub-region for every playable scene
SCENE_TO_AP_REGION: dict[str, str] = {
    "home": "Shade's Home",
    "king": "The King's Hall",
    "king_stairs": "The King's Hall",
    "entrance_hall": "The King's Hall",
    "treasury": "The King's Hall",
    "gallery": "The King's Hall",
    "entrancehall2": "The King's Hall",
    "entrancehall3": "The King's Hall",
    "mysteryroom_01": "Mystery Rooms",
    "mysteryroom_02": "Mystery Rooms",
    "hall_R_01": "The Great Halls",
    "hall_R_02": "The Great Halls",
    "hall_R_03": "The Great Halls",
    "hall_R_04": "The Great Halls",
    "hall_R_05": "The Great Halls",
    "hall_R_06": "The Great Halls",
    "hall_R_07": "The Great Halls",
    "hall_R_08": "The Great Halls",
    "hall_L_01": "The Great Halls",
    "hall_L_02": "The Great Halls",
    "hall_L_03": "The Great Halls",
    "hall_L_04": "The Great Halls",
    "hall_L_05": "Secret Hall",
    "hall_L_06": "The Great Halls",
    "hall L 06A": "The Great Halls",
    "hall_L06_behind": "The Great Halls",
    "hall_L_07": "The Great Halls",
    "hall_L_08": "The Great Halls",
    "hall_L_09": "The Great Halls",
    "moss_cliff": "The Great Halls",
    "face": "The Great Halls",
    "grotto_01": "The Great Halls",
    "grotto_02": "The Great Halls",
    "up_01": "The Great Halls",
    "up_07": "The Great Halls",
    "entrance_library": "The Library",
    "library": "The Library",
    "entrance_labyrinth": "Halls of Eternity",
    "labyrinth_01": "Halls of Eternity",
    "labyrinth_02": "Halls of Eternity",
    "labyrinth_03": "Halls of Eternity",
    "labyrinth_04": "Halls of Eternity",
    "labyrinth_05": "Halls of Eternity",
    "labyrinth_06": "Halls of Eternity",
    "labyrinth_07": "Halls of Eternity",
    "labyrinth_08": "Halls of Eternity",
    "labyrinth_09": "Halls of Eternity",
    "labyrinth_10": "Halls of Eternity",
    "labyrinth_11": "Halls of Eternity",
    "labyrinth 11A": "Halls of Eternity",
    "labyrinth_12": "Halls of Eternity",
    "labyrinth_13": "Halls of Eternity",
    "labyrinth_14": "Halls of Eternity",
    "labyrinth 14A": "Halls of Eternity",
    "labyrinth_15": "Halls of Eternity",
    "labyrinth_16": "Halls of Eternity",
    "entrance_eternity": "Halls of Eternity",
    "halls_of_eternity": "Halls of Eternity",
    "exit": "Mushroom Groves",
    "greatCave_complete": "Mushroom Groves",
    "mine_01": "Crystal Cavern & Mines",
    "mine_03": "Crystal Cavern & Mines",
    "mine_04": "Crystal Cavern & Mines",
    "tower": "Secret Tower",
    "brunnen": "The Well & Surface",
    "oberwelt": "The Well & Surface",
}

# Canonical progression/featured book volumes from BookInShelf (31 distinct titles)
BOOK_TITLES: list[str] = [
    "Book: Thus Spoke Zarathustra II",
    "Book: Thus Spoke Zarathustra III",
    "Book: Thus Spoke Zarathustra IV",
    "Book: The Cask of Amontillado",
    "Book: Metamorphoses",
    "Book: The Six Swans",
    "Book: Fables I",
    "Book: Fables II",
    "Book: The Iliad I-VIII",
    "Book: The Iliad IX-XVII",
    "Book: The Iliad XVIII-XXIV",
    "Book: Nibelungenlied",
    "Book: The Raven",
    "Book: Three Tales",
    "Book: The Golden Bird",
    "Book: Morella",
    "Book: Ligeia",
    "Book: Narrative of A. Gordon Pym",
    "Book: Hans Pfaall",
    "Book: The Time Machine",
    "Book: The Secret Garden",
    "Book: Count of Monte Cristo I",
    "Book: Count of Monte Cristo II",
    "Book: Count of Monte Cristo III",
    "Book: Count of Monte Cristo IV",
    "Book: Count of Monte Cristo V",
    "Book: Count of Monte Cristo VI",
    "Book: The Wind in the Willows",
    "Book: King Pest",
    "Book: The House on the Borderland",
    "Book: The Anatomy of Melancholy",
]

# Items table (order defines stable AP item IDs!)
STATIC_ITEMS: list[dict[str, object]] = [
    # Progression tools & keys
    {"name": "Mattock", "classification": "progression", "category": "tools", "game_key": "PH534TLO0", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Secret Key", "classification": "progression", "category": "tools", "game_key": "SecretKey", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Library Door Pass", "classification": "progression", "category": "tools", "game_key": "AP_LIBRARY_PASS", "secondary_key": None, "amount": 1, "action": "unlock"},
    {"name": "Surface Gate Pass", "classification": "progression", "category": "tools", "game_key": "AP_SURFACE_GATE_PASS", "secondary_key": None, "amount": 1, "action": "unlock"},
    {"name": "Giant Mushroom Spores", "classification": "progression", "category": "mushrooms_moss", "game_key": "AP_GIANT_SPORES", "secondary_key": None, "amount": 1, "action": "unlock"},
    {"name": "Cavern Map", "classification": "useful", "category": "tools", "game_key": "AP_CAVERN_MAP", "secondary_key": None, "amount": 1, "action": "unlock"},
    # Crystals (grouped as "Crystals")
    {"name": "White Crystal", "classification": "progression_skip_balancing", "category": "crystals", "game_key": "WHSDGV&234", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Rose Crystal", "classification": "progression_skip_balancing", "category": "crystals", "game_key": "SDGV&234", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Blue Crystal", "classification": "progression_skip_balancing", "category": "crystals", "game_key": "SDGVBL", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Purple Crystal", "classification": "progression_skip_balancing", "category": "crystals", "game_key": "SDGLV234", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Black Crystal", "classification": "progression_skip_balancing", "category": "crystals", "game_key": "SDGVBLA", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Great Hall Big Crystal", "classification": "progression_skip_balancing", "category": "crystals", "game_key": "HJUIASD&jI33", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Tower Big Crystal", "classification": "progression_skip_balancing", "category": "crystals", "game_key": "KLKASJD&ASGD786h", "secondary_key": None, "amount": 1, "action": "item"},
    # Musical instrument
    {"name": "Musical Instrument Part", "classification": "progression", "category": "instrument", "game_key": "MUL4L4L4", "secondary_key": None, "amount": 1, "action": "item"},
    # Mushrooms & Moss
    {"name": "Cavern Moss", "classification": "useful", "category": "mushrooms_moss", "game_key": "ITMM0023Z", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Green Mushroom", "classification": "useful", "category": "mushrooms_moss", "game_key": "AP_GREEN_MUSHROOM", "secondary_key": None, "amount": 1, "action": "mushroom"},
    {"name": "Purple Mushroom", "classification": "useful", "category": "mushrooms_moss", "game_key": "AP_PURPLE_MUSHROOM", "secondary_key": None, "amount": 1, "action": "mushroom"},
    # Home comforts & relics
    {"name": "White Chalk", "classification": "useful", "category": "comfort", "game_key": "STN_CLRS3421", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Red Clay", "classification": "useful", "category": "comfort", "game_key": "STN_CLRS3422", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Lapis Lazuli", "classification": "useful", "category": "comfort", "game_key": "STN_CLRS3424", "secondary_key": "L45I5", "amount": 1, "action": "item"},
    {"name": "Drawing Paper", "classification": "useful", "category": "comfort", "game_key": "P4H_FFT", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Spider Flower", "classification": "useful", "category": "comfort", "game_key": "FLWRSPDR", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Golden Coin", "classification": "useful", "category": "comfort", "game_key": "GLDNCN", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Painting of the King", "classification": "useful", "category": "comfort", "game_key": "TEKNGRISES777", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Horse Head Trophy", "classification": "useful", "category": "comfort", "game_key": "WIIHAHAHAHAH&234", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Birthday Hourglass", "classification": "useful", "category": "comfort", "game_key": "HJUIASD&jIU_55", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Labyrinth Keepsake Stone", "classification": "useful", "category": "comfort", "game_key": "WATHAMCC", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Eternal Carpet II", "classification": "useful", "category": "comfort", "game_key": "T33PCHZWO", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Eternal Carpet III", "classification": "useful", "category": "comfort", "game_key": "T33PCHIERD", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Eternal Curtain", "classification": "useful", "category": "comfort", "game_key": "VROHG", "secondary_key": None, "amount": 1, "action": "item"},
    # Fuel (Coal, Wood, Flint, Sulfur)
    {"name": "Coal", "classification": "filler", "category": "coal_sulfur", "game_key": "KSTTFF34", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Wood", "classification": "filler", "category": "coal_sulfur", "game_key": "FRST435", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Flint", "classification": "useful", "category": "coal_sulfur", "game_key": "FRST986", "secondary_key": None, "amount": 1, "action": "item"},
    {"name": "Sulfur", "classification": "useful", "category": "coal_sulfur", "game_key": "STN_CLRS3423", "secondary_key": "5U2LFU6", "amount": 1, "action": "item"},
    # Time pacing items
    {"name": "Time Skip (15 Minutes)", "classification": "filler", "category": "time", "game_key": "TIME_SKIP_900", "secondary_key": None, "amount": 900, "action": "time_skip"},
    {"name": "Time Skip (1 Hour)", "classification": "useful", "category": "time", "game_key": "TIME_SKIP_3600", "secondary_key": None, "amount": 3600, "action": "time_skip"},
    {"name": "Time Skip (6 Hours)", "classification": "progression_skip_balancing", "category": "time", "game_key": "TIME_SKIP_21600", "secondary_key": None, "amount": 21600, "action": "time_skip"},
    {"name": "Time Skip (1 Day)", "classification": "progression_skip_balancing", "category": "time", "game_key": "TIME_SKIP_86400", "secondary_key": None, "amount": 86400, "action": "time_skip"},
    {"name": "Time Rate Boost (+2x)", "classification": "useful", "category": "time", "game_key": "TIME_RATE_2", "secondary_key": None, "amount": 2, "action": "time_rate"},
    # Traps
    {"name": "Slowness Trap", "classification": "trap", "category": "trap", "game_key": "TRAP_SLOWNESS", "secondary_key": None, "amount": 20, "action": "trap"},
    {"name": "Darkness Trap", "classification": "trap", "category": "trap", "game_key": "TRAP_DARKNESS", "secondary_key": None, "amount": 20, "action": "trap"},
    {"name": "Disappointment Trap", "classification": "trap", "category": "trap", "game_key": "DISUAD&JSAD§232", "secondary_key": None, "amount": 1, "action": "trap"},
    {"name": "Time Stall Trap", "classification": "trap", "category": "trap", "game_key": "TRAP_TIME_STALL", "secondary_key": None, "amount": 30, "action": "trap"},
    # Generic filler
    {"name": "Quiet Reflection", "classification": "filler", "category": "filler", "game_key": "FILLER_REFLECTION", "secondary_key": None, "amount": 1, "action": "filler"},
]

MILESTONE_LOCATIONS: list[dict[str, object]] = [
    {
        "name": "North Entrance Hall - Cavern Map Reveal",
        "game_uid": "event:map_reveal",
        "scene": "entrancehall3",
        "map_region": "kings_hall",
        "ap_region": "The King's Hall",
        "pool": "tools",
        "depth": "core",
        "requires": [],
    },
    {
        "name": "Shade's Home - Build the Bed",
        "game_uid": "event:build_bed",
        "scene": "home",
        "map_region": "shades_home",
        "ap_region": "Shade's Home",
        "pool": "comfort",
        "depth": "core",
        "requires": ["moss_10"],
    },
    {
        "name": "Shade's Home - Light Hearth Fire",
        "game_uid": "event:hearth_fire",
        "scene": "home",
        "map_region": "shades_home",
        "ap_region": "Shade's Home",
        "pool": "comfort",
        "depth": "core",
        "requires": [],
    },
    {
        "name": "Shade's Home - Light Sulfur Fire",
        "game_uid": "event:sulfur_fire",
        "scene": "home",
        "map_region": "shades_home",
        "ap_region": "Shade's Home",
        "pool": "comfort",
        "depth": "core",
        "requires": ["sulfur"],
    },
    {
        "name": "Shade's Home - Play Complete Instrument",
        "game_uid": "event:home_music",
        "scene": "home",
        "map_region": "shades_home",
        "ap_region": "Shade's Home",
        "pool": "instrument",
        "depth": "core",
        "requires": ["instrument_3"],
    },
    {
        "name": "Shade's Home - Complete First Drawing",
        "game_uid": "event:home_draw",
        "scene": "home",
        "map_region": "shades_home",
        "ap_region": "Shade's Home",
        "pool": "comfort",
        "depth": "core",
        "requires": ["paper"],
    },
    {
        "name": "Shade's Home - Expand Home Chamber",
        "game_uid": "event:home_wall",
        "scene": "home",
        "map_region": "shades_home",
        "ap_region": "Shade's Home",
        "pool": "comfort",
        "depth": "core",
        "requires": ["mattock_1"],
    },
    {
        "name": "Great Cave - Grow Giant Purple Mushroom",
        "game_uid": "event:giant_mushroom",
        "scene": "greatCave_complete",
        "map_region": "mushroom_groves",
        "ap_region": "Mushroom Groves",
        "pool": "mushrooms_moss",
        "depth": "core",
        "requires": ["giant_spores"],
    },
    {
        "name": "Halls of Eternity - Eternal Carpet II",
        "game_uid": "eternity:T33PCHZWO",
        "scene": "halls_of_eternity",
        "map_region": "halls_eternity",
        "ap_region": "Halls of Eternity",
        "pool": "comfort",
        "depth": "core",
        "requires": [],
    },
    {
        "name": "Halls of Eternity - Eternal Carpet III",
        "game_uid": "eternity:T33PCHIERD",
        "scene": "halls_of_eternity",
        "map_region": "halls_eternity",
        "ap_region": "Halls of Eternity",
        "pool": "comfort",
        "depth": "core",
        "requires": [],
    },
    {
        "name": "Halls of Eternity - Eternal Curtain",
        "game_uid": "eternity:VROHG",
        "scene": "halls_of_eternity",
        "map_region": "halls_eternity",
        "ap_region": "Halls of Eternity",
        "pool": "comfort",
        "depth": "core",
        "requires": [],
    },
    {
        "name": "The Well - Reach the Well Shaft",
        "game_uid": "event:well_arrival",
        "scene": "brunnen",
        "map_region": "surface_well",
        "ap_region": "The Well & Surface",
        "pool": "tools",
        "depth": "core",
        "requires": [],
    },
]


def determine_requires(pickup: dict[str, object]) -> list[str]:
    uid = str(pickup["canonical_id"])
    scene = str(pickup["scene"])
    item_key = str(pickup["item_key"])
    reqs: list[str] = []

    if uid == "grab_hacke_01_home":
        reqs.append("mattock_1")
    elif item_key in ("WHSDGV&234", "SDGLV234"):
        reqs.append("mattock_1")
    elif item_key in ("SDGV&234", "SDGVBL", "SDGVBLA"):
        reqs.append("mattock_2")
    elif scene in ("mine_03", "mine_04") and item_key in ("STN_CLRS3423", "KSTTFF34"):
        reqs.append("mattock_1")

    return reqs


def build_registries(catalog: dict[str, object]) -> tuple[list[dict[str, object]], list[dict[str, object]]]:
    items_out: list[dict[str, object]] = []
    next_item_id = BASE_ITEM_ID
    for entry in STATIC_ITEMS:
        items_out.append({"id": next_item_id, **entry})
        next_item_id += 1
    for title in BOOK_TITLES:
        items_out.append(
            {
                "id": next_item_id,
                "name": title,
                "classification": "progression_skip_balancing",
                "category": "books",
                "game_key": "B00BZ",
                "secondary_key": None,
                "amount": 1,
                "action": "item",
            }
        )
        next_item_id += 1

    # Append new items after the existing registry to preserve every published ID.
    for name, key in (
        ("Progressive Walk Speed", "AP_WALK_SPEED"),
        ("Progressive Waypoint Recall", "AP_WAYPOINT_RECALL"),
    ):
        items_out.append({
            "id": next_item_id,
            "name": name,
            "classification": "useful",
            "category": "mobility",
            "game_key": key,
            "secondary_key": None,
            "amount": 1,
            "action": "unlock",
        })
        next_item_id += 1

    item_keys = catalog["item_keys"]
    assert isinstance(item_keys, dict)
    pickups = catalog["pickups"]
    assert isinstance(pickups, list)

    # Number repeated pickups cleanly per (scene, item_name)
    counters: dict[tuple[str, str], int] = defaultdict(int)
    totals: dict[tuple[str, str], int] = defaultdict(int)
    for p in pickups:
        iname = str(item_keys[p["item_key"]]["name"])
        totals[(str(p["scene"]), iname)] += 1

    # Track which books/crystals count as "progression" depth vs "full" depth
    book_progression_scenes = {"entrancehall2", "mysteryroom_01", "hall_L_05", "tower"}
    library_prog_seen = 0
    crystal_color_seen: set[str] = set()

    locations_out: list[dict[str, object]] = []
    next_loc_id = BASE_LOCATION_ID

    for p in pickups:
        scene = str(p["scene"])
        scene_label = SCENE_LABELS.get(scene, scene)
        ik = str(p["item_key"])
        meta = item_keys[ik]
        iname = str(meta["name"])
        pool = str(meta["category"])

        key = (scene, iname)
        counters[key] += 1
        suffix = f" #{counters[key]}" if totals[key] > 1 else ""
        loc_name = f"{scene_label} - {iname}{suffix}"

        # Depth classification for Books & Crystals (progression vs full)
        if pool == "books":
            if scene in book_progression_scenes:
                depth = "progression"
            elif scene == "library" and library_prog_seen < 6:
                library_prog_seen += 1
                depth = "progression"
            else:
                depth = "full"
        elif pool == "crystals":
            if ik not in crystal_color_seen:
                crystal_color_seen.add(ik)
                depth = "progression"
            else:
                depth = "full"
        else:
            depth = "core"

        locations_out.append(
            {
                "id": next_loc_id,
                "name": loc_name,
                "game_uid": str(p["canonical_id"]),
                "scene": scene,
                "alias_scenes": list(p.get("alias_scenes", [])),
                "map_region": str(p["region"]),
                "ap_region": SCENE_TO_AP_REGION.get(scene, "The Great Halls"),
                "pool": pool,
                "depth": depth,
                "vanilla_item_key": ik,
                "requires": determine_requires(p),
            }
        )
        next_loc_id += 1

    rooms = {room["name"]: room for room in catalog["rooms"]}
    discoveries = [
        {
            "name": f"Discover {SCENE_LABELS[scene]}",
            "game_uid": f"discover:{scene}",
            "scene": scene,
            "map_region": rooms[scene]["region"],
            "ap_region": "The Great Halls" if scene == "entrance_library" else SCENE_TO_AP_REGION[scene],
            "pool": "mobility",
            "depth": "core",
            "requires": [],
        }
        for scene in ("home", "king", "entrance_hall", "treasury", "moss_cliff", "entrance_library", "greatCave_complete")
    ]
    for m in [*MILESTONE_LOCATIONS, *discoveries]:
        locations_out.append(
            {
                "id": next_loc_id,
                "name": m["name"],
                "game_uid": m["game_uid"],
                "scene": m["scene"],
                "alias_scenes": [],
                "map_region": m["map_region"],
                "ap_region": m["ap_region"],
                "pool": m["pool"],
                "depth": m["depth"],
                "vanilla_item_key": None,
                "requires": list(m["requires"]),
            }
        )
        next_loc_id += 1

    return items_out, locations_out


def write_apworld_registry(items: list[dict[str, object]], locations: list[dict[str, object]]) -> None:
    lines = [
        '"""Auto-generated deterministic item and location registries for The Longing APWorld.',
        "",
        "Generated by tools/sync_world_data.py from data/world_catalog.json.",
        '"""',
        "from __future__ import annotations",
        "",
        f"BASE_ITEM_ID = {BASE_ITEM_ID}",
        f"BASE_LOCATION_ID = {BASE_LOCATION_ID}",
        "",
        "ITEM_DEFINITIONS: tuple[dict[str, object], ...] = (",
    ]
    for it in items:
        lines.append(f"    {it!r},")
    lines.append(")")
    lines.append("")
    lines.append("LOCATION_DEFINITIONS: tuple[dict[str, object], ...] = (")
    for loc in locations:
        lines.append(f"    {loc!r},")
    lines.append(")")
    lines.append("")
    APWORLD_OUT.parent.mkdir(parents=True, exist_ok=True)
    APWORLD_OUT.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"Wrote {APWORLD_OUT} ({len(items)} items, {len(locations)} locations)")


def cs_escape(value: str | None) -> str:
    if value is None:
        return "null"
    escaped = value.replace("\\", "\\\\").replace('"', '\\"')
    return f'"{escaped}"'


def write_mod_registry(items: list[dict[str, object]], locations: list[dict[str, object]]) -> None:
    lines = [
        "// Auto-generated by tools/sync_world_data.py from data/world_catalog.json.",
        "// Do not edit manually; re-run python tools/sync_world_data.py instead.",
        "using System.Collections.Generic;",
        "",
        "namespace TheLongingDecrypted.Generated",
        "{",
        "    public sealed class ItemRegistryEntry",
        "    {",
        "        public long Id { get; }",
        "        public string Name { get; }",
        "        public string Category { get; }",
        "        public string GameKey { get; }",
        "        public string SecondaryKey { get; }",
        "        public int Amount { get; }",
        "        public string Action { get; }",
        "",
        "        public ItemRegistryEntry(long id, string name, string category, string gameKey, string secondaryKey, int amount, string action)",
        "        {",
        "            Id = id;",
        "            Name = name;",
        "            Category = category;",
        "            GameKey = gameKey;",
        "            SecondaryKey = secondaryKey;",
        "            Amount = amount;",
        "            Action = action;",
        "        }",
        "    }",
        "",
        "    public sealed class LocationRegistryEntry",
        "    {",
        "        public long Id { get; }",
        "        public string Name { get; }",
        "        public string GameUid { get; }",
        "        public string Scene { get; }",
        "        public string Pool { get; }",
        "",
        "        public LocationRegistryEntry(long id, string name, string gameUid, string scene, string pool)",
        "        {",
        "            Id = id;",
        "            Name = name;",
        "            GameUid = gameUid;",
        "            Scene = scene;",
        "            Pool = pool;",
        "        }",
        "    }",
        "",
        "    public static class WorldRegistryData",
        "    {",
        "        public static readonly IReadOnlyDictionary<long, ItemRegistryEntry> ItemsById = new Dictionary<long, ItemRegistryEntry>",
        "        {",
    ]
    for it in items:
        lines.append(
            f"            [{it['id']}L] = new ItemRegistryEntry({it['id']}L, {cs_escape(str(it['name']))}, {cs_escape(str(it['category']))}, {cs_escape(str(it['game_key']))}, {cs_escape(it['secondary_key'] if it['secondary_key'] is not None else None)}, {it['amount']}, {cs_escape(str(it['action']))}),"
        )
    lines.extend(
        [
            "        };",
            "",
            "        public static readonly IReadOnlyDictionary<string, LocationRegistryEntry> LocationsByUid = new Dictionary<string, LocationRegistryEntry>",
            "        {",
        ]
    )
    for loc in locations:
        lines.append(
            f"            [{cs_escape(str(loc['game_uid']))}] = new LocationRegistryEntry({loc['id']}L, {cs_escape(str(loc['name']))}, {cs_escape(str(loc['game_uid']))}, {cs_escape(str(loc['scene']))}, {cs_escape(str(loc['pool']))}),"
        )
    lines.extend(
        [
            "        };",
            "",
            "        public static readonly IReadOnlyDictionary<long, LocationRegistryEntry> LocationsById = new Dictionary<long, LocationRegistryEntry>",
            "        {",
        ]
    )
    for loc in locations:
        lines.append(
            f"            [{loc['id']}L] = new LocationRegistryEntry({loc['id']}L, {cs_escape(str(loc['name']))}, {cs_escape(str(loc['game_uid']))}, {cs_escape(str(loc['scene']))}, {cs_escape(str(loc['pool']))}),"
        )
    lines.extend(
        [
            "        };",
            "    }",
            "}",
            "",
        ]
    )
    MOD_OUT.parent.mkdir(parents=True, exist_ok=True)
    MOD_OUT.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"Wrote {MOD_OUT}")


def write_site_catalog(catalog: dict[str, object], items: list[dict[str, object]], locations: list[dict[str, object]]) -> None:
    # Build clean per-region check list for the interactive cavern map & tracker
    pool_to_map_cat = {
        "tools": "tools",
        "books": "books",
        "crystals": "crystals",
        "mushrooms_moss": "tools",
        "comfort": "comfort",
        "instrument": "comfort",
        "coal_sulfur": "comfort",
        "mobility": "tools",
    }
    summary = catalog["summary"]
    assert isinstance(summary, dict)

    lines = [
        "// Auto-generated by tools/sync_world_data.py from data/world_catalog.json.",
        "// Do not edit manually; re-run python tools/sync_world_data.py instead.",
        "",
        "export interface CatalogCheck {",
        "  readonly id: number;",
        "  readonly name: string;",
        "  readonly scene: string;",
        "  readonly regionId: string;",
        "  readonly apRegion: string;",
        "  readonly pool: string;",
        "  readonly depth: string;",
        '  readonly mapCategory: "tools" | "books" | "crystals" | "comfort" | "endings";',
        "  readonly requires: readonly string[];",
        "}",
        "",
        "export const WORLD_CATALOG = {",
        '  worldVersion: "0.1.0",',
        '  archipelagoTarget: "0.6.8+",',
        "  summary: {",
    ]
    for sk, sv in summary.items():
        lines.append(f"    {sk}: {json.dumps(sv)},")
    lines.extend(
        [
            "  },",
            f"  itemCount: {len(items)},",
            f"  locationCount: {len(locations)},",
            "  checks: [",
        ]
    )
    for loc in locations:
        reqs = ", ".join(json.dumps(str(r)) for r in loc["requires"])
        map_cat = pool_to_map_cat.get(str(loc["pool"]), "comfort")
        lines.extend(
            [
                "    {",
                f"      id: {loc['id']},",
                f"      name: {json.dumps(str(loc['name']))},",
                f"      scene: {json.dumps(str(loc['scene']))},",
                f"      regionId: {json.dumps(str(loc['map_region']))},",
                f"      apRegion: {json.dumps(str(loc['ap_region']))},",
                f"      pool: {json.dumps(str(loc['pool']))},",
                f"      depth: {json.dumps(str(loc['depth']))},",
                f"      mapCategory: {json.dumps(map_cat)},",
                f"      requires: [{reqs}],",
                "    },",
            ]
        )
    lines.extend(
        [
            "  ],",
            "} as const;",
            "",
        ]
    )
    SITE_OUT.parent.mkdir(parents=True, exist_ok=True)
    SITE_OUT.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"Wrote {SITE_OUT}")


def main() -> int:
    catalog = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    items, locations = build_registries(catalog)
    write_apworld_registry(items, locations)
    write_mod_registry(items, locations)
    write_site_catalog(catalog, items, locations)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
