"""Extract structured cavern, room, item, book, and check catalog from The Longing."""
from __future__ import annotations

import argparse
import json
import os
import struct
from collections import defaultdict
from pathlib import Path

import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GAME_DIR = Path(
    os.environ.get(
        "LONGING_GAME_DIR",
        r"C:\Program Files (x86)\Steam\steamapps\common\The Longing",
    )
)
DEFAULT_OUTPUT = ROOT / "data" / "world_catalog.json"

# Verified internal ObscuredPrefs item keys -> metadata
ITEM_KEY_METADATA: dict[str, dict[str, object]] = {
    "PH534TLO0": {
        "name": "Mattock",
        "category": "tools",
        "secondary_key": None,
        "description": "Mining tool used to break weak cavern walls, crystals, coal, and sulfur veins.",
    },
    "SecretKey": {
        "name": "Secret Key",
        "category": "tools",
        "secondary_key": None,
        "description": "Key found atop the Secret Tower that unlocks the Mystery Room and hidden passages.",
    },
    "B00BZ": {
        "name": "Book",
        "category": "books",
        "secondary_key": None,
        "description": "Classic literature volume added to the Shade's home bookshelf.",
    },
    "WHSDGV&234": {
        "name": "White Crystal",
        "category": "crystals",
        "secondary_key": None,
        "description": "Pale crystal chipped from Moss Cliff walls.",
    },
    "SDGV&234": {
        "name": "Rose Crystal",
        "category": "crystals",
        "secondary_key": None,
        "description": "Pink crystal mined from the Entrance Hall veins.",
    },
    "SDGVBL": {
        "name": "Blue Crystal",
        "category": "crystals",
        "secondary_key": None,
        "description": "Azure crystal mined near the whispering Face.",
    },
    "SDGLV234": {
        "name": "Purple Crystal",
        "category": "crystals",
        "secondary_key": None,
        "description": "Violet crystal mined in the Great Cave.",
    },
    "SDGVBLA": {
        "name": "Black Crystal",
        "category": "crystals",
        "secondary_key": None,
        "description": "Dark crystal mined deep inside the Labyrinth.",
    },
    "HJUIASD&jI33": {
        "name": "Great Hall Big Crystal",
        "category": "crystals",
        "secondary_key": None,
        "description": "Giant crystal hidden in the secret chamber behind the Mystery Door.",
    },
    "KLKASJD&ASGD786h": {
        "name": "Tower Big Crystal",
        "category": "crystals",
        "secondary_key": None,
        "description": "Giant crystal resting in the Secret Tower.",
    },
    "ITMM0023Z": {
        "name": "Moss",
        "category": "mushrooms_moss",
        "secondary_key": None,
        "description": "Soft cavern moss used to build the Shade's bed and cushion falls.",
    },
    "MUL4L4L4": {
        "name": "Musical Instrument Part",
        "category": "instrument",
        "secondary_key": None,
        "description": "Piece of the Shade's cavern didgeridoo / instrument.",
    },
    "KSTTFF34": {
        "name": "Coal",
        "category": "coal_sulfur",
        "secondary_key": None,
        "description": "Lump of coal burned in the Shade's hearth to warm the home and speed time.",
    },
    "FRST435": {
        "name": "Wood",
        "category": "coal_sulfur",
        "secondary_key": None,
        "description": "Dry timber gathered for the Shade's hearth fire.",
    },
    "FRST986": {
        "name": "Flint",
        "category": "coal_sulfur",
        "secondary_key": None,
        "description": "Flint stone used to strike sparks for the hearth.",
    },
    "STN_CLRS3423": {
        "name": "Sulfur",
        "category": "coal_sulfur",
        "secondary_key": "5U2LFU6",
        "description": "Yellow sulfur pigment and smelly hearth fuel mined in Mine 03.",
    },
    "STN_CLRS3421": {
        "name": "White Chalk",
        "category": "comfort",
        "secondary_key": None,
        "description": "White drawing chalk for sketching on the home walls.",
    },
    "STN_CLRS3422": {
        "name": "Red Clay",
        "category": "comfort",
        "secondary_key": None,
        "description": "Red clay pigment for home drawings.",
    },
    "STN_CLRS3424": {
        "name": "Lapis Lazuli",
        "category": "comfort",
        "secondary_key": "L45I5",
        "description": "Blue lapis lazuli pigment and decorative gemstone.",
    },
    "P4H_FFT": {
        "name": "Drawing Paper",
        "category": "comfort",
        "secondary_key": None,
        "description": "Sheet of paper used to sketch pictures for the Shade's home.",
    },
    "FLWRSPDR": {
        "name": "Spider Flower",
        "category": "comfort",
        "secondary_key": None,
        "description": "Rare cavern bloom growing in the Great Cave.",
    },
    "GLDNCN": {
        "name": "Golden Coin",
        "category": "comfort",
        "secondary_key": None,
        "description": "Ancient gold piece from the King's Treasury.",
    },
    "TEKNGRISES777": {
        "name": "Painting of the King",
        "category": "comfort",
        "secondary_key": None,
        "description": "Framed royal portrait hidden in the secret chamber.",
    },
    "WIIHAHAHAHAH&234": {
        "name": "Horse Head Trophy",
        "category": "comfort",
        "secondary_key": None,
        "description": "Wall-mounted horse head trophy discovered in the Halls of Eternity.",
    },
    "HJUIASD&jIU_55": {
        "name": "Birthday Hourglass",
        "category": "comfort",
        "secondary_key": None,
        "description": "Gift-wrapped hourglass found on Moss Cliff.",
    },
    "WATHAMCC": {
        "name": "Labyrinth Keepsake Stone",
        "category": "comfort",
        "secondary_key": None,
        "description": "Movable relic stone deep in Labyrinth 16.",
    },
    "T33PCHZWO": {
        "name": "Eternal Carpet II",
        "category": "comfort",
        "secondary_key": None,
        "description": "Woven rug found along the Halls of Eternity.",
    },
    "T33PCHIERD": {
        "name": "Eternal Carpet III",
        "category": "comfort",
        "secondary_key": None,
        "description": "Large rug found along the Halls of Eternity.",
    },
    "VROHG": {
        "name": "Eternal Curtain",
        "category": "comfort",
        "secondary_key": None,
        "description": "Hanging curtain found along the Halls of Eternity.",
    },
}

# Macro-region assignment for every playable scene
SCENE_TO_REGION: dict[str, str] = {
    "home": "shades_home",
    "king": "kings_hall",
    "king_stairs": "kings_hall",
    "entrance_hall": "kings_hall",
    "treasury": "kings_hall",
    "gallery": "kings_hall",
    "entrancehall2": "kings_hall",
    "entrancehall3": "kings_hall",
    "mysteryroom_01": "kings_hall",
    "mysteryroom_02": "kings_hall",
    "hall_R_01": "great_cave",
    "hall_R_02": "great_cave",
    "hall_R_03": "great_cave",
    "hall_R_04": "great_cave",
    "hall_R_05": "great_cave",
    "hall_R_06": "great_cave",
    "hall_R_07": "great_cave",
    "hall_R_08": "great_cave",
    "hall_L_01": "great_cave",
    "hall_L_02": "great_cave",
    "hall_L_03": "great_cave",
    "hall_L_04": "great_cave",
    "hall_L_05": "great_cave",
    "hall_L_06": "great_cave",
    "hall L 06A": "great_cave",
    "hall_L06_behind": "great_cave",
    "hall_L_07": "great_cave",
    "hall_L_08": "great_cave",
    "hall_L_09": "great_cave",
    "moss_cliff": "great_cave",
    "face": "great_cave",
    "grotto_01": "great_cave",
    "grotto_02": "great_cave",
    "up_01": "great_cave",
    "up_07": "great_cave",
    "entrance_library": "library",
    "library": "library",
    "entrance_labyrinth": "halls_eternity",
    "labyrinth_01": "halls_eternity",
    "labyrinth_02": "halls_eternity",
    "labyrinth_03": "halls_eternity",
    "labyrinth_04": "halls_eternity",
    "labyrinth_05": "halls_eternity",
    "labyrinth_06": "halls_eternity",
    "labyrinth_07": "halls_eternity",
    "labyrinth_08": "halls_eternity",
    "labyrinth_09": "halls_eternity",
    "labyrinth_10": "halls_eternity",
    "labyrinth_11": "halls_eternity",
    "labyrinth 11A": "halls_eternity",
    "labyrinth_12": "halls_eternity",
    "labyrinth_13": "halls_eternity",
    "labyrinth_14": "halls_eternity",
    "labyrinth 14A": "halls_eternity",
    "labyrinth_15": "halls_eternity",
    "labyrinth_16": "halls_eternity",
    "entrance_eternity": "halls_eternity",
    "halls_of_eternity": "halls_eternity",
    "exit": "mushroom_groves",
    "greatCave_complete": "mushroom_groves",
    "mine_01": "crystal_cavern",
    "mine_03": "crystal_cavern",
    "mine_04": "crystal_cavern",
    "tower": "secret_tower",
    "brunnen": "surface_well",
    "oberwelt": "surface_well",
}

ENDING_SCENES = {"ende_king", "ende oberwelt", "ende suicide", "ende tragedy"}
UTILITY_SCENES = {
    "01_SplashScene",
    "02_StartMenu",
    "03_Placeholder",
    "intro",
    "extra loading",
    "HUD test",
    "king_entrance",
    "prison",
    "PasswordScene",
    "BetaExpired",
    "LoaderScene",
}

GATE_CLASSES = {
    "Interactable_DoorOpening_Library",
    "Interactable_DoorOpening_KingStairs",
    "Interactable_DoorOpening_Mystery",
    "Interactable_DoorOpening_Up07",
    "Interactable_MysteryDoor",
    "Interactable_NeedsMattock",
    "Interactable_NeedsMattockFreeze",
    "InteractableOutcome_HackIntoMine",
    "InteractableOutcome_HackIntoWall",
    "InteractableOutcome_Treasury",
    "InteractableOutcome_MapReveal",
    "Interactable_SpiderNet",
    "Interactable_Stalagtit",
    "Interactable_RisingWater",
    "Interactable_MosscliffFall",
    "Interactable_Face",
    "Interactable_Monster",
    "Interactable_King",
    "Interactable_Suicide",
    "Interactable_Present",
    "Interactable_Flower",
    "Interactable_Sanduhr",
    "Interactable_Home_BuildBed",
    "Interactable_Home_Draw",
    "Interactable_Home_Music",
    "Interactable_Home_Read",
    "Interactable_Home_Kamin",
    "Interactable_Home_KaminSulfur",
    "Interactable_Home_SleepBed",
}


def resolve_script(
    monoscripts: dict[tuple[str, int], tuple[str, str]],
    sf: object,
    level_key: str,
    file_id: int,
    path_id: int,
) -> tuple[str, str] | None:
    if path_id == 0:
        return None
    if file_id == 0:
        target = level_key.lower()
    else:
        idx = file_id - 1
        externals = getattr(sf, "externals", [])
        if idx < 0 or idx >= len(externals):
            return None
        target = externals[idx].path.lower()
    return monoscripts.get((target, path_id))


def extract_catalog(game_dir: Path) -> dict[str, object]:
    game_data = game_dir / "The Longing_Data"
    bundle_path = game_data / "data.unity3d"
    managed_dir = game_data / "Managed"
    if not bundle_path.exists():
        raise FileNotFoundError(f"Missing data.unity3d at {bundle_path}")

    env = UnityPy.load(str(bundle_path))
    gen = TypeTreeGenerator("2018.4.36f1")
    gen.load_local_dll_folder(str(managed_dir))
    env.typetree_generator = gen

    monoscripts: dict[tuple[str, int], tuple[str, str]] = {}
    for fname, sf in env.file.files.items():
        if not hasattr(sf, "objects"):
            continue
        for pid, obj in sf.objects.items():
            if obj.type.name == "MonoScript":
                d = obj.read()
                cls = getattr(d, "m_ClassName", "")
                ns = getattr(d, "m_Namespace", "")
                asm = getattr(d, "m_AssemblyName", "")
                full_cls = f"{ns}.{cls}" if ns else cls
                monoscripts[(fname.lower(), pid)] = (full_cls, asm)

    build_scenes: list[str] = []
    for obj in env.objects:
        if obj.type.name == "BuildSettings":
            build_scenes = list(obj.read().scenes)
            break

    scene_by_idx = {i: Path(p).stem for i, p in enumerate(build_scenes)}

    rooms: list[dict[str, object]] = []
    raw_pickups: list[dict[str, object]] = []
    books: list[dict[str, object]] = []
    gates: list[dict[str, object]] = []
    eternity_spawns: list[dict[str, object]] = []

    for idx, scene_path in enumerate(build_scenes):
        level_key = f"level{idx}"
        sf = env.file.files.get(level_key)
        sname = scene_by_idx[idx]
        if sf is None:
            continue

        go_names: dict[int, str] = {}
        for pid, obj in sf.objects.items():
            if obj.type.name == "GameObject":
                try:
                    go_names[pid] = getattr(obj.read(), "m_Name", "")
                except Exception:
                    pass

        by_go: dict[int, list[dict[str, object]]] = defaultdict(list)
        behaviours: list[dict[str, object]] = []

        for pid, obj in sf.objects.items():
            if obj.type.name != "MonoBehaviour":
                continue
            raw = obj.get_raw_data()
            if len(raw) < 28:
                continue
            _, go_pid, enabled, sc_fid, sc_pid = struct.unpack("<iqiiq", raw[:28])
            info = resolve_script(monoscripts, sf, level_key, sc_fid, sc_pid)
            if not info:
                continue
            cls, asm = info
            if "Assembly-CSharp" not in asm:
                continue
            if cls not in (
                "SceneManagerScript",
                "WayPointManager",
                "ProgressClass",
                "InteractableOutcome_Item",
                "InteractableOutcome_GoThroughDoor",
                "Interactable_OW_Well",
                "Interactable_MushroomGrow",
                "BookInShelf",
                "SpawnItems",
            ) and cls not in GATE_CLASSES:
                continue
            try:
                tree = obj.read_typetree()
            except Exception:
                continue
            entry = {
                "path_id": pid,
                "go_path_id": go_pid,
                "go_name": go_names.get(go_pid, ""),
                "class": cls,
                "enabled": bool(enabled),
                "tree": tree,
            }
            behaviours.append(entry)
            by_go[go_pid].append(entry)

        has_scene_mgr = any(b["class"] == "SceneManagerScript" for b in behaviours)
        transitions: list[dict[str, object]] = []
        seen_edges: set[tuple[int, str]] = set()
        waypoint_count = 0
        mushroom_spots = 0

        for b in behaviours:
            cls = str(b["class"])
            tree = b["tree"]
            sibs = by_go[int(b["go_path_id"])]
            prog = next((s["tree"] for s in sibs if s["class"] == "ProgressClass"), None)
            prog_uid = prog.get("ENMHNKMPDCI") if isinstance(prog, dict) else None
            prog_dur = prog.get("PDHKCPKPOKJ", 0) if isinstance(prog, dict) else 0

            if cls == "WayPointManager":
                wps = tree.get("waypoints", [])
                waypoint_count = len(wps)
                for wi, wp in enumerate(wps):
                    dst = wp.get("loadSceneNrOnReach", -1)
                    if dst >= 0 and (dst, "waypoint") not in seen_edges:
                        seen_edges.add((dst, "waypoint"))
                        transitions.append(
                            {
                                "to_index": dst,
                                "to": scene_by_idx.get(dst, f"scene_{dst}"),
                                "via": "waypoint",
                                "waypoint_id": wi,
                                "initially_reachable": bool(wp.get("isReachable", 1)),
                            }
                        )
            elif cls == "InteractableOutcome_GoThroughDoor":
                dst = tree.get("BEDLLGBMAAC", -1)
                if dst >= 0 and (dst, "door") not in seen_edges:
                    seen_edges.add((dst, "door"))
                    transitions.append(
                        {
                            "to_index": dst,
                            "to": scene_by_idx.get(dst, f"scene_{dst}"),
                            "via": "door",
                            "progress_uid": prog_uid,
                        }
                    )
            elif cls == "Interactable_OW_Well":
                dst = tree.get("OMNMMFPCEEI", -1)
                if dst >= 0 and (dst, "well") not in seen_edges:
                    seen_edges.add((dst, "well"))
                    transitions.append(
                        {
                            "to_index": dst,
                            "to": scene_by_idx.get(dst, f"scene_{dst}"),
                            "via": "well",
                        }
                    )
            elif cls == "Interactable_MushroomGrow":
                mushroom_spots += 1
            elif cls == "InteractableOutcome_Item":
                item_key = str(tree.get("OPMCGHDHJHA", ""))
                if item_key == "DISUAD&JSAD§232":
                    continue  # Disappointment counter, not a collectible item
                secondary = str(tree.get("FJILMMBKLCD", ""))
                raw_pickups.append(
                    {
                        "scene_index": idx,
                        "scene": sname,
                        "region": SCENE_TO_REGION.get(sname, "other"),
                        "go_name": b["go_name"],
                        "progress_uid": prog_uid,
                        "item_key": item_key,
                        "secondary_key": None if secondary in ("", "Constant.Name") else secondary,
                        "amount": int(tree.get("HHEBBPFGLOB", 1)),
                    }
                )
            elif cls == "BookInShelf":
                books.append(
                    {
                        "book_id": int(tree.get("BBKBLGOEDOE", -99)),
                        "title": str(tree.get("EKHKHEMALCK", "")),
                        "filename": str(tree.get("MGBFKNOFJCO", "")),
                    }
                )
            elif cls == "SpawnItems" and sname == "halls_of_eternity":
                seen_prefabs: set[str] = set()
                for pf in tree.get("EDIGEBABLKJ", []):
                    fid, ppid = pf["m_FileID"], pf["m_PathID"]
                    target_file = sf.externals[fid - 1].path if fid > 0 else level_key
                    tsf = env.file.files.get(target_file)
                    if not tsf or ppid not in tsf.objects:
                        continue
                    go = tsf.objects[ppid].read()
                    gname = getattr(go, "m_Name", "")
                    if gname in seen_prefabs or gname == "fake item":
                        continue
                    seen_prefabs.add(gname)
                    for comp in getattr(go, "m_Component", []):
                        cpid = comp.component.path_id
                        if cpid in tsf.objects and tsf.objects[cpid].type.name == "MonoBehaviour":
                            co = tsf.objects[cpid]
                            craw = co.get_raw_data()
                            if len(craw) >= 28:
                                _, _, _, csfid, cspid = struct.unpack("<iqiiq", craw[:28])
                                cinfo = resolve_script(monoscripts, tsf, target_file, csfid, cspid)
                                if cinfo and cinfo[0] == "InteractableOutcome_Item":
                                    ct = co.read_typetree()
                                    eternity_spawns.append(
                                        {
                                            "prefab": gname,
                                            "item_key": str(ct.get("OPMCGHDHJHA", "")),
                                            "amount": int(ct.get("HHEBBPFGLOB", 1)),
                                        }
                                    )
            elif cls in GATE_CLASSES:
                gates.append(
                    {
                        "scene_index": idx,
                        "scene": sname,
                        "region": SCENE_TO_REGION.get(sname, "other"),
                        "class": cls,
                        "go_name": b["go_name"],
                        "progress_uid": prog_uid,
                        "duration_seconds": int(prog_dur),
                    }
                )

        if sname in ENDING_SCENES:
            kind = "ending"
        elif sname in UTILITY_SCENES or not has_scene_mgr:
            kind = "menu_or_utility"
        else:
            kind = "playable"

        rooms.append(
            {
                "index": idx,
                "name": sname,
                "kind": kind,
                "region": SCENE_TO_REGION.get(sname, kind),
                "waypoint_count": waypoint_count,
                "mushroom_spots": mushroom_spots,
                "transitions": sorted(transitions, key=lambda t: (int(t["to_index"]), str(t["via"]))),
            }
        )

    # Deduplicate shared-UID pickups across alternate scene angles (e.g. hall_L_06 / hall L 06A / hall_L06_behind)
    # while keeping distinct placements in the same scene or distinct UIDs.
    deduped_pickups: list[dict[str, object]] = []
    seen_uids: dict[str, dict[str, object]] = {}
    for p in raw_pickups:
        uid = p["progress_uid"]
        if uid is None:
            p["canonical_id"] = f"{p['scene']}:{p['go_name']}"
            p["alias_scenes"] = []
            deduped_pickups.append(p)
        elif uid in seen_uids:
            existing = seen_uids[uid]
            aliases = existing["alias_scenes"]
            assert isinstance(aliases, list)
            if p["scene"] != existing["scene"] and p["scene"] not in aliases:
                aliases.append(p["scene"])
        else:
            p["canonical_id"] = str(uid)
            p["alias_scenes"] = []
            seen_uids[uid] = p
            deduped_pickups.append(p)

    # Sort deterministically
    deduped_pickups.sort(key=lambda x: (int(x["scene_index"]), str(x["item_key"]), str(x["canonical_id"])))
    books.sort(key=lambda x: (int(x["book_id"]), str(x["filename"])))
    gates.sort(key=lambda x: (int(x["scene_index"]), str(x["class"]), str(x["progress_uid"] or x["go_name"])))
    eternity_spawns.sort(key=lambda x: str(x["prefab"]))

    # Compute summary counts
    playable_rooms = [r for r in rooms if r["kind"] == "playable"]
    ending_rooms = [r for r in rooms if r["kind"] == "ending"]

    return {
        "schema_version": 1,
        "engine": {
            "unity_version": "2018.4.36f1",
            "architecture": "x86",
            "bepinex_target": "BepInEx 5 x86",
            "archipelago_target": "0.6.8+",
        },
        "summary": {
            "total_scenes": len(rooms),
            "playable_rooms": len(playable_rooms),
            "ending_rooms": len(ending_rooms),
            "unique_pickups": len(deduped_pickups),
            "books_in_shelf": len(books),
            "gates_and_events": len(gates),
            "item_keys": len(ITEM_KEY_METADATA),
        },
        "item_keys": ITEM_KEY_METADATA,
        "rooms": rooms,
        "pickups": deduped_pickups,
        "books": books,
        "eternity_spawns": eternity_spawns,
        "gates_and_events": gates,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-dir", type=Path, default=DEFAULT_GAME_DIR)
    parser.add_argument("--output", "-o", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()

    catalog = extract_catalog(args.game_dir)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(catalog, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"Wrote {args.output}")
    print(json.dumps(catalog["summary"], indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
