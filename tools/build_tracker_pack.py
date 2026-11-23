"""Generate and package the PopTracker & Universal Tracker pack for The Longing Decrypted.

Reads data/world_catalog.json and tools/sync_world_data.py so item IDs, location IDs,
region gates, discovery checks, progressive mobility upgrades, and recall rings stay
synchronized with the APWorld and BepInEx plugin.
"""
from __future__ import annotations

import argparse
import json
import math
import shutil
import sys
from pathlib import Path
from typing import Any
from zipfile import ZIP_DEFLATED, ZipFile

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT / "tools") not in sys.path:
    sys.path.insert(0, str(ROOT / "tools"))

import sync_world_data  # noqa: E402

CATALOG_PATH = ROOT / "data" / "world_catalog.json"
TRACKER_DIR = ROOT / "tracker"
APWORLD_TRACKER_DIR = ROOT / "apworld" / "the_longing" / "tracker"
APWORLD_MAPPING_PY = ROOT / "apworld" / "the_longing" / "tracker_mapping.py"

WORLD_VERSION = "1.0.0"
GAME_NAME = "The Longing"
PACK_NAME = "The Longing Decrypted"
PACK_UID = "the_longing_decrypted_ap"
AUTHOR = "Decrypt"

MAP_WIDTH = 960
MAP_HEIGHT = 680

# Exact scene build-index recall rings from mod/Patches/WaypointRecallPatches.cs (CavernRing).
RECALL_RING_BUILD_INDICES: dict[int, tuple[int, ...]] = {
    1: (3, 4, 6, 7, 8, 21, 24, 54),
    2: (
        9, 10, 11, 12, 15, 16, 17, 18, 19, 20, 22, 23, 25, 26,
        46, 47, 48, 49, 50, 51, 52, 53, 55, 56, 61, 72,
    ),
    3: (
        14, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
        40, 41, 42, 43, 44, 45, 60, 62, 63, 64, 65, 66, 67, 68, 69,
    ),
}

RECALL_RING_INFO: dict[int, dict[str, str]] = {
    1: {
        "code": "recall_ring_1",
        "title": "Ring 1: Inner Kingdom",
        "summary": "Shade's Home, King's Chamber, King's Stairs, Entrance Halls, Treasury, Gallery",
    },
    2: {
        "code": "recall_ring_2",
        "title": "Ring 2: Middle Caverns & Library",
        "summary": "Great Halls, Moss Cliff, Moss Grottos, Face Chamber, Secret Hall, Library Vestibule, The Library",
    },
    3: {
        "code": "recall_ring_3",
        "title": "Ring 3: Deep & Upper Caverns",
        "summary": "Labyrinth, Halls of Eternity, Great Cave, Mines, Secret Tower, The Well",
    },
}

# Map node layout on the 960x680 Cavern Map.
# Coordinates are chosen so Ring 1 encloses Home & King's Hall, Ring 2 encloses
# Great Halls / Moss Cliff / Grottos / Secret Hall / Library, and Ring 3 encloses
# Halls of Eternity / Mushroom Groves / Mines / Secret Tower / The Well.
# Mystery Rooms intentionally sit outside the Ring 1 boundary (ring 0 in the mod).
MAP_REGION_NODES: list[dict[str, Any]] = [
    {
        "key": "shades_home",
        "name": "Shade's Home",
        "subtitle": "Ring 1 · Starting Hearth",
        "ring": 1,
        "x": 490,
        "y": 370,
        "label_offset": (0, -34),
        "access_rules": [],
    },
    {
        "key": "kings_hall",
        "name": "The King's Hall",
        "subtitle": "Ring 1 · Deepest Throne",
        "ring": 1,
        "x": 465,
        "y": 490,
        "label_offset": (0, 32),
        "access_rules": [],
    },
    {
        "key": "mystery_rooms",
        "name": "Mystery Rooms",
        "subtitle": "Outside Recall Rings · Secret Key",
        "ring": 0,
        "x": 795,
        "y": 590,
        "label_offset": (0, 28),
        "access_rules": ["$has_secret_key"],
    },
    {
        "key": "great_halls",
        "name": "The Great Halls",
        "subtitle": "Ring 2 · Central Passages",
        "ring": 2,
        "x": 490,
        "y": 255,
        "label_offset": (0, -32),
        "access_rules": [],
    },
    {
        "key": "moss_cliff",
        "name": "Moss Cliff",
        "subtitle": "Ring 2 · East Cavern Ledge",
        "ring": 2,
        "x": 365,
        "y": 245,
        "label_offset": (0, -30),
        "access_rules": [],
    },
    {
        "key": "moss_grottos",
        "name": "Moss Grottos",
        "subtitle": "Ring 2 · Soft Moss Caverns",
        "ring": 2,
        "x": 360,
        "y": 330,
        "label_offset": (-8, 28),
        "access_rules": [],
    },
    {
        "key": "secret_hall",
        "name": "Secret Hall",
        "subtitle": "Ring 2 · Mystery Door (L05)",
        "ring": 2,
        "x": 300,
        "y": 415,
        "label_offset": (0, 28),
        "access_rules": ["$has_secret_key"],
    },
    {
        "key": "library",
        "name": "The Library",
        "subtitle": "Ring 2 · Library Door Pass",
        "ring": 2,
        "x": 195,
        "y": 295,
        "label_offset": (-10, -30),
        "access_rules": ["$has_library_pass"],
    },
    {
        "key": "halls_eternity",
        "name": "Halls of Eternity",
        "subtitle": "Ring 3 · Endless Labyrinth",
        "ring": 3,
        "x": 135,
        "y": 480,
        "label_offset": (0, 30),
        "access_rules": [],
    },
    {
        "key": "mushroom_groves",
        "name": "Mushroom Groves",
        "subtitle": "Ring 3 · Upper Exit Gate",
        "ring": 3,
        "x": 685,
        "y": 355,
        "label_offset": (-8, 32),
        "access_rules": ["$has_surface_gate"],
    },
    {
        "key": "crystal_mines",
        "name": "Crystal Cavern & Mines",
        "subtitle": "Ring 3 · Coal & Sulfur Veins",
        "ring": 3,
        "x": 835,
        "y": 345,
        "label_offset": (-4, -32),
        "access_rules": ["$has_surface_gate"],
    },
    {
        "key": "secret_tower",
        "name": "Secret Tower",
        "subtitle": "Ring 3 · Giant Mushroom Spores",
        "ring": 3,
        "x": 575,
        "y": 145,
        "label_offset": (-76, -12),
        "access_rules": ["$can_reach_secret_tower"],
    },
    {
        "key": "surface_well",
        "name": "The Well & Surface",
        "subtitle": "Ring 3 (Well) · Climb to Daylight",
        "ring": 3,
        "x": 685,
        "y": 88,
        "label_offset": (100, 0),
        "access_rules": ["$can_reach_secret_tower"],
    },
]

# Dedicated diamond pins on the Cavern Map for the seven discovery checks.
DISCOVERY_PIN_LAYOUT: dict[str, dict[str, Any]] = {
    "discover:home": {
        "node_name": "Discovery (Ring 1): Shade's Home",
        "ring": 1,
        "x": 524,
        "y": 392,
        "access_rules": [],
    },
    "discover:king": {
        "node_name": "Discovery (Ring 1): King's Chamber",
        "ring": 1,
        "x": 424,
        "y": 484,
        "access_rules": [],
    },
    "discover:entrance_hall": {
        "node_name": "Discovery (Ring 1): Entrance Hall",
        "ring": 1,
        "x": 506,
        "y": 484,
        "access_rules": [],
    },
    "discover:treasury": {
        "node_name": "Discovery (Ring 1): King's Treasury",
        "ring": 1,
        "x": 465,
        "y": 448,
        "access_rules": [],
    },
    "discover:moss_cliff": {
        "node_name": "Discovery (Ring 2): Moss Cliff",
        "ring": 2,
        "x": 330,
        "y": 266,
        "access_rules": [],
    },
    "discover:entrance_library": {
        "node_name": "Discovery (Ring 2): Library Vestibule",
        "ring": 2,
        "x": 248,
        "y": 295,
        "access_rules": [],
    },
    "discover:greatCave_complete": {
        "node_name": "Discovery (Ring 3): Great Cave",
        "ring": 3,
        "x": 738,
        "y": 318,
        "access_rules": ["$has_surface_gate"],
    },
}

MAP_PASSAGES: list[tuple[str, str, bool]] = [
    ("shades_home", "kings_hall", False),
    ("kings_hall", "mystery_rooms", True),
    ("kings_hall", "great_halls", False),
    ("shades_home", "great_halls", False),
    ("great_halls", "moss_cliff", False),
    ("moss_cliff", "moss_grottos", False),
    ("great_halls", "secret_hall", True),
    ("great_halls", "library", True),
    ("great_halls", "halls_eternity", False),
    ("great_halls", "mushroom_groves", True),
    ("mushroom_groves", "crystal_mines", False),
    ("mushroom_groves", "secret_tower", True),
    ("secret_tower", "surface_well", False),
]

# Palette inspired directly by The Longing (cave earth, parchment, ink, amber glow, brass).
C_BG = (17, 14, 12, 255)
C_PANEL = (30, 24, 20, 255)
C_PANEL_HI = (46, 38, 30, 255)
C_INK = (10, 8, 7, 255)
C_PARCHMENT = (222, 204, 168, 255)
C_PARCHMENT_DIM = (152, 136, 108, 255)
C_AMBER = (244, 212, 104, 255)
C_BRASS = (188, 148, 72, 255)
C_RING_1 = (244, 212, 104, 215)
C_RING_2 = (196, 146, 68, 205)
C_RING_3 = (126, 162, 194, 195)
C_GATED = (198, 108, 74, 225)
C_OPEN = (158, 142, 114, 190)


def _load_font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    candidates = [
        "C:/Windows/Fonts/georgiab.ttf" if bold else "C:/Windows/Fonts/georgia.ttf",
        "C:/Windows/Fonts/timesbd.ttf" if bold else "C:/Windows/Fonts/times.ttf",
        "C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf",
    ]
    for path in candidates:
        if Path(path).exists():
            try:
                return ImageFont.truetype(path, size=size)
            except OSError:
                pass
    return ImageFont.load_default()


def build_recall_ring_scenes(catalog: dict[str, Any]) -> dict[int, list[str]]:
    """Resolve the mod's scene build indices against data/world_catalog.json."""
    rooms_by_index = {int(room["index"]): str(room["name"]) for room in catalog["rooms"]}
    result: dict[int, list[str]] = {}
    for ring, indices in RECALL_RING_BUILD_INDICES.items():
        scenes: list[str] = []
        for idx in indices:
            if idx not in rooms_by_index:
                raise ValueError(f"Recall ring {ring} references unknown scene index {idx}")
            scenes.append(rooms_by_index[idx])
        result[ring] = scenes
    return result


def resolve_map_node_key(loc: dict[str, Any]) -> str:
    ap_region = str(loc["ap_region"])
    scene = str(loc["scene"])
    if ap_region == "Shade's Home":
        return "shades_home"
    if ap_region == "The King's Hall":
        return "kings_hall"
    if ap_region == "Mystery Rooms":
        return "mystery_rooms"
    if ap_region == "The Great Halls":
        if scene == "moss_cliff":
            return "moss_cliff"
        if scene in ("grotto_01", "grotto_02"):
            return "moss_grottos"
        return "great_halls"
    if ap_region == "Secret Hall":
        return "secret_hall"
    if ap_region == "The Library":
        return "library"
    if ap_region == "Halls of Eternity":
        return "halls_eternity"
    if ap_region == "Mushroom Groves":
        return "mushroom_groves"
    if ap_region == "Crystal Cavern & Mines":
        return "crystal_mines"
    if ap_region == "Secret Tower":
        return "secret_tower"
    if ap_region == "The Well & Surface":
        return "surface_well"
    raise ValueError(f"Unhandled ap_region {ap_region!r} for location {loc['name']!r}")


def reqs_to_access_rules(requires: list[str]) -> list[str]:
    if not requires:
        return []
    parts: list[str] = []
    for req in requires:
        if req == "mattock_1":
            parts.append("$has_mattocks|1")
        elif req == "mattock_2":
            parts.append("$has_mattocks|2")
        elif req == "giant_spores":
            parts.append("$has_giant_spores")
        elif req == "moss_10":
            parts.append("$has_moss_10")
        elif req == "sulfur":
            parts.append("$has_sulfur")
        elif req == "instrument_3":
            parts.append("$has_instrument_3")
        elif req == "paper":
            parts.append("$has_paper")
        else:
            raise ValueError(f"Unknown requirement token: {req!r}")
    return [",".join(parts)]


def _draw_dashed_line(
    draw: ImageDraw.ImageDraw,
    p1: tuple[int, int],
    p2: tuple[int, int],
    fill: tuple[int, int, int, int],
    width: int = 3,
    dash: int = 10,
    gap: int = 7,
) -> None:
    dx = p2[0] - p1[0]
    dy = p2[1] - p1[1]
    dist = math.hypot(dx, dy)
    if dist < 1:
        return
    ux, uy = dx / dist, dy / dist
    pos = 0.0
    while pos < dist:
        end = min(pos + dash, dist)
        x0, y0 = p1[0] + ux * pos, p1[1] + uy * pos
        x1, y1 = p1[0] + ux * end, p1[1] + uy * end
        draw.line([(x0, y0), (x1, y1)], fill=fill, width=width)
        pos += dash + gap


def _draw_dashed_ellipse(
    draw: ImageDraw.ImageDraw,
    bbox: tuple[int, int, int, int],
    fill: tuple[int, int, int, int],
    width: int = 2,
    segments: int = 72,
) -> None:
    x0, y0, x1, y1 = bbox
    cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    rx, ry = (x1 - x0) / 2.0, (y1 - y0) / 2.0
    step = 2.0 * math.pi / segments
    for i in range(0, segments, 2):
        a0 = i * step
        a1 = (i + 1.15) * step
        p0 = (cx + rx * math.cos(a0), cy + ry * math.sin(a0))
        p1 = (cx + rx * math.cos(a1), cy + ry * math.sin(a1))
        draw.line([p0, p1], fill=fill, width=width)


def generate_cavern_map_image(path: Path) -> None:
    """Render an original dark-parchment Underground Kingdom map showing all 3 recall rings."""
    img = Image.new("RGBA", (MAP_WIDTH, MAP_HEIGHT), C_BG)
    draw = ImageDraw.Draw(img)

    # Subtle vertical cave-depth gradient strata (fully opaque dark earth)
    for y in range(MAP_HEIGHT):
        t = y / max(1, MAP_HEIGHT - 1)
        r = int(28 - 11 * t)
        g = int(22 - 9 * t)
        b = int(17 - 6 * t)
        draw.line([(0, y), (MAP_WIDTH, y)], fill=(r, g, b, 255))

    # Translucent cavern strata + concentric recall ring fills composited properly
    overlay = Image.new("RGBA", (MAP_WIDTH, MAP_HEIGHT), (0, 0, 0, 0))
    od = ImageDraw.Draw(overlay)

    for idx in range(1, 7):
        y_base = idx * 96
        pts = [
            (x, y_base + int(8 * math.sin(x / 85.0 + idx)))
            for x in range(16, MAP_WIDTH - 15, 24)
        ]
        od.line(pts, fill=(76, 62, 46, 55), width=1)

    # Recall Ring 3 (outer deep & upper caverns; Mystery Rooms at 795,590 sit outside all rings)
    ring3_box = (42, 34, 922, 588)
    od.ellipse(ring3_box, fill=(58, 86, 114, 34))

    # Recall Ring 2 (middle caverns & Library)
    ring2_box = (144, 178, 608, 568)
    od.ellipse(ring2_box, fill=(146, 106, 46, 36))

    # Recall Ring 1 (inner kingdom: Home & King's Hall)
    ring1_box = (392, 326, 564, 544)
    od.ellipse(ring1_box, fill=(196, 158, 62, 44))

    img = Image.alpha_composite(img, overlay)
    draw = ImageDraw.Draw(img)

    _draw_dashed_ellipse(draw, ring3_box, C_RING_3, width=2, segments=96)
    _draw_dashed_ellipse(draw, ring2_box, C_RING_2, width=2, segments=76)
    draw.ellipse(ring1_box, outline=C_RING_1, width=2)

    # Double hand-sketched parchment frame
    draw.rectangle((10, 10, MAP_WIDTH - 11, MAP_HEIGHT - 11), outline=C_BRASS, width=2)
    draw.rectangle((16, 16, MAP_WIDTH - 17, MAP_HEIGHT - 17), outline=(92, 74, 46, 255), width=1)

    font_title = _load_font(19, bold=True)
    font_ring = _load_font(12, bold=True)
    font_node = _load_font(13, bold=True)
    font_sub = _load_font(11, bold=False)

    # Title cartouche (top-left, sized dynamically to text)
    title_text = "THE UNDERGROUND KINGDOM"
    tb = draw.textbbox((0, 0), title_text, font=font_title)
    title_w = tb[2] - tb[0]
    cart_r = max(360, 26 + title_w + 34)
    draw.rectangle((26, 24, cart_r, 76), fill=(22, 18, 15, 255), outline=C_BRASS, width=2)
    draw.rectangle((30, 28, cart_r - 4, 72), outline=(96, 76, 46, 255), width=1)
    draw.text((42, 32), title_text, fill=C_AMBER, font=font_title)
    draw.text((42, 55), "Cavern Regions · 7 Discoveries · 3 Recall Rings", fill=C_PARCHMENT_DIM, font=font_sub)

    # Recall ring contour labels
    draw.text((406, 548), "RING I · INNER KINGDOM", fill=C_RING_1, font=font_ring)
    draw.text((228, 184), "RING II · MIDDLE CAVERNS & LIBRARY", fill=C_RING_2, font=font_ring)
    draw.text((662, 24), "RING III · DEEP & UPPER CAVERNS", fill=C_RING_3, font=font_ring)

    # Connecting passages
    node_pos = {node["key"]: (int(node["x"]), int(node["y"])) for node in MAP_REGION_NODES}
    for src, dst, gated in MAP_PASSAGES:
        p1, p2 = node_pos[src], node_pos[dst]
        if gated:
            _draw_dashed_line(draw, p1, p2, fill=C_GATED, width=3, dash=9, gap=6)
        else:
            draw.line([p1, p2], fill=C_OPEN, width=3)

    # Region medallions + labels
    ring_colors = {0: C_GATED, 1: C_RING_1, 2: C_RING_2, 3: C_RING_3}
    for node in MAP_REGION_NODES:
        x, y = int(node["x"]), int(node["y"])
        ring = int(node["ring"])
        accent = ring_colors[ring]
        draw.ellipse((x - 16, y - 16, x + 16, y + 16), fill=(24, 19, 15, 255), outline=accent, width=2)
        draw.ellipse((x - 6, y - 6, x + 6, y + 6), fill=accent)

        lx = x + int(node["label_offset"][0])
        ly = y + int(node["label_offset"][1])
        name = str(node["name"])
        bbox = draw.textbbox((0, 0), name, font=font_node)
        tw = bbox[2] - bbox[0]
        th = bbox[3] - bbox[1]
        pad_x, pad_y = 6, 3
        bx0 = max(22, min(MAP_WIDTH - 24 - tw - pad_x * 2, lx - tw // 2 - pad_x))
        by0 = max(22, min(MAP_HEIGHT - 24 - th - pad_y * 2, ly - th // 2 - pad_y))
        draw.rectangle(
            (bx0, by0, bx0 + tw + pad_x * 2, by0 + th + pad_y * 2),
            fill=(18, 14, 11, 255),
            outline=(96, 78, 50, 255),
            width=1,
        )
        draw.text((bx0 + pad_x, by0 + pad_y - 1), name, fill=C_PARCHMENT, font=font_node)

    # Note under Mystery Rooms clarifying outside recall rings
    draw.text((735, 636), "(Outside Recall Rings)", fill=C_GATED, font=font_sub)

    # Subtle diamond sockets under the 7 discovery check pins
    for pin in DISCOVERY_PIN_LAYOUT.values():
        x, y = int(pin["x"]), int(pin["y"])
        accent = ring_colors[int(pin["ring"])]
        pts = [(x, y - 10), (x + 10, y), (x, y + 10), (x - 10, y)]
        draw.polygon(pts, fill=(24, 20, 14, 255), outline=accent)

    # Legend (bottom-left)
    draw.rectangle((26, 586, 382, 656), fill=(22, 18, 15, 255), outline=C_BRASS, width=1)
    draw.text((38, 594), "MAP LEGEND", fill=C_AMBER, font=font_ring)
    draw.rectangle((38, 616, 50, 628), fill=C_PANEL_HI, outline=C_PARCHMENT, width=1)
    draw.text((58, 615), "Region Checks", fill=C_PARCHMENT, font=font_sub)
    draw.polygon([(174, 615), (181, 622), (174, 629), (167, 622)], fill=C_AMBER, outline=C_INK)
    draw.text((188, 615), "Discovery Check (7)", fill=C_PARCHMENT, font=font_sub)
    draw.line([(38, 642), (62, 642)], fill=C_OPEN, width=3)
    draw.text((68, 636), "Open Passage", fill=C_PARCHMENT_DIM, font=font_sub)
    _draw_dashed_line(draw, (172, 642), (198, 642), fill=C_GATED, width=3, dash=6, gap=4)
    draw.text((204, 636), "Item-Gated Passage", fill=C_PARCHMENT_DIM, font=font_sub)

    path.parent.mkdir(parents=True, exist_ok=True)
    img.convert("RGB").save(path, format="PNG", optimize=True)


def _new_icon(border: tuple[int, int, int, int] = C_BRASS, bg: tuple[int, int, int, int] = C_PANEL) -> tuple[Image.Image, ImageDraw.ImageDraw]:
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img, "RGBA")
    draw.rounded_rectangle((3, 3, 60, 60), radius=8, fill=bg, outline=border, width=2)
    draw.rounded_rectangle((6, 6, 57, 57), radius=5, outline=(72, 58, 38, 180), width=1)
    return img, draw


def _draw_centered_text(
    draw: ImageDraw.ImageDraw,
    text: str,
    cy: int,
    fill: tuple[int, int, int, int],
    size: int = 16,
    bold: bool = True,
) -> None:
    font = _load_font(size, bold=bold)
    bbox = draw.textbbox((0, 0), text, font=font)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    draw.text(((64 - tw) // 2, cy - th // 2 - 1), text, fill=fill, font=font)


def _draw_crystal_shape(
    draw: ImageDraw.ImageDraw,
    color: tuple[int, int, int, int],
    hi: tuple[int, int, int, int],
    big: bool = False,
) -> None:
    if big:
        pts = [(32, 9), (47, 24), (44, 52), (32, 56), (20, 52), (17, 24)]
        draw.polygon(pts, fill=color, outline=C_INK)
        draw.polygon([(32, 9), (42, 24), (32, 54), (25, 24)], fill=hi)
    else:
        pts = [(32, 12), (43, 27), (38, 52), (26, 52), (21, 27)]
        draw.polygon(pts, fill=color, outline=C_INK)
        draw.polygon([(32, 12), (38, 27), (32, 50)], fill=hi)


def generate_item_icons(items_dir: Path) -> None:
    """Generate crisp 64x64 original icons styled after The Longing."""
    items_dir.mkdir(parents=True, exist_ok=True)

    def save(name: str, img: Image.Image) -> None:
        img.save(items_dir / f"{name}.png", format="PNG", optimize=True)

    # Mattock
    img, d = _new_icon(C_AMBER)
    d.line([(18, 48), (45, 21)], fill=(154, 112, 66, 255), width=6)
    d.arc((22, 11, 54, 41), start=220, end=25, fill=(196, 198, 206, 255), width=6)
    d.polygon([(36, 14), (49, 25), (44, 30), (32, 19)], fill=(224, 224, 232, 255))
    save("mattock", img)

    # Secret Key
    img, d = _new_icon(C_AMBER)
    d.ellipse((14, 16, 32, 34), outline=C_AMBER, width=4)
    d.line([(30, 25), (50, 45)], fill=C_AMBER, width=4)
    d.line([(41, 36), (36, 42)], fill=C_AMBER, width=4)
    d.line([(47, 42), (42, 48)], fill=C_AMBER, width=4)
    save("secret_key", img)

    # Library Door Pass
    img, d = _new_icon(C_BRASS)
    d.rectangle((15, 14, 49, 50), fill=(72, 46, 28, 255), outline=C_PARCHMENT, width=2)
    d.line([(32, 15), (32, 49)], fill=C_PARCHMENT, width=2)
    for y in (22, 29, 36, 43):
        d.line([(19, y), (28, y)], fill=C_AMBER, width=2)
        d.line([(36, y), (45, y)], fill=C_AMBER, width=2)
    save("library_pass", img)

    # Surface Gate Pass
    img, d = _new_icon(C_RING_3)
    d.pieslice((14, 14, 50, 50), 180, 360, fill=(44, 54, 64, 255), outline=C_PARCHMENT, width=2)
    d.rectangle((14, 32, 50, 52), fill=(44, 54, 64, 255), outline=C_PARCHMENT, width=2)
    for x in (22, 32, 42):
        d.line([(x, 18), (x, 52)], fill=C_AMBER, width=3)
    d.line([(15, 36), (49, 36)], fill=C_PARCHMENT, width=2)
    save("surface_gate_pass", img)

    # Giant Mushroom Spores
    img, d = _new_icon((188, 122, 214, 255))
    d.rectangle((27, 32, 37, 51), fill=(214, 198, 174, 255), outline=C_INK, width=1)
    d.pieslice((12, 14, 52, 44), 180, 360, fill=(154, 82, 192, 255), outline=C_PARCHMENT, width=2)
    for sx, sy in ((22, 24), (32, 20), (41, 25), (18, 42), (46, 40), (44, 48)):
        d.ellipse((sx - 2, sy - 2, sx + 2, sy + 2), fill=C_AMBER)
    save("giant_spores", img)

    # Cavern Map
    img, d = _new_icon(C_PARCHMENT)
    d.rectangle((14, 16, 50, 48), fill=(182, 156, 114, 255), outline=C_INK, width=2)
    d.line([(20, 42), (30, 26), (42, 34), (46, 22)], fill=(64, 42, 24, 255), width=3)
    d.ellipse((42, 18, 50, 26), fill=(184, 52, 42, 255))
    save("cavern_map", img)

    # Progressive Walk Speed (5 stages: 1x .. 2x)
    walk_specs = [
        ("walk_speed_0", "1×", (110, 100, 86, 255), (26, 22, 18, 255), 0),
        ("walk_speed_1", "1.25×", (196, 168, 108, 255), (34, 28, 20, 255), 1),
        ("walk_speed_2", "1.5×", (218, 184, 96, 255), (42, 34, 20, 255), 2),
        ("walk_speed_3", "1.75×", (236, 200, 96, 255), (50, 40, 20, 255), 3),
        ("walk_speed_4", "2×", C_AMBER, (62, 48, 22, 255), 4),
    ]
    for fname, label, accent, bg, level in walk_specs:
        img, d = _new_icon(accent, bg=bg)
        # Shade eyes + stride chevrons
        d.ellipse((20, 14, 27, 21), fill=accent)
        d.ellipse((37, 14, 44, 21), fill=accent)
        for i in range(4):
            cx = 16 + i * 10
            col = accent if i < level else (66, 58, 48, 180)
            d.line([(cx, 28), (cx + 5, 33), (cx, 38)], fill=col, width=3)
        _draw_centered_text(d, label, 48, accent if level > 0 else C_PARCHMENT_DIM, size=14)
        save(fname, img)

    # Progressive Waypoint Recall (4 stages: 0/3 .. 3/3 concentric rings)
    recall_specs = [
        ("recall_tier_0", "0/3", 0),
        ("recall_tier_1", "R1", 1),
        ("recall_tier_2", "R2", 2),
        ("recall_tier_3", "R3", 3),
    ]
    for fname, label, tier in recall_specs:
        border = C_AMBER if tier == 3 else (C_BRASS if tier > 0 else (98, 86, 72, 255))
        img, d = _new_icon(border)
        c1 = C_RING_1 if tier >= 1 else (70, 62, 52, 160)
        c2 = C_RING_2 if tier >= 2 else (70, 62, 52, 150)
        c3 = C_RING_3 if tier >= 3 else (70, 62, 52, 140)
        d.ellipse((14, 7, 50, 43), outline=c3, width=3)
        d.ellipse((20, 13, 44, 37), outline=c2, width=3)
        d.ellipse((26, 19, 38, 31), outline=c1, width=3)
        if tier >= 1:
            d.ellipse((30, 23, 34, 27), fill=C_AMBER)
        _draw_centered_text(d, label, 51, border, size=12)
        save(fname, img)

    # Individual Recall Ring badges (Ring I, Ring II, Ring III)
    for ring_num, roman, color in (
        (1, "I", C_RING_1),
        (2, "II", C_RING_2),
        (3, "III", C_RING_3),
    ):
        img, d = _new_icon(color)
        d.ellipse((14, 7, 50, 43), outline=color, width=3)
        _draw_centered_text(d, roman, 25, color, size=18)
        _draw_centered_text(d, f"RING {ring_num}", 51, C_PARCHMENT, size=9)
        save(f"ring_{ring_num}", img)

    # Crystals
    crystal_specs = [
        ("crystal_white", (228, 228, 232, 255), (255, 255, 255, 255), False),
        ("crystal_rose", (222, 126, 154, 255), (248, 178, 198, 255), False),
        ("crystal_blue", (96, 164, 228, 255), (168, 214, 252, 255), False),
        ("crystal_purple", (166, 106, 222, 255), (212, 168, 248, 255), False),
        ("crystal_black", (56, 54, 68, 255), (122, 118, 146, 255), False),
        ("crystal_great_hall", (102, 206, 192, 255), (186, 244, 236, 255), True),
        ("crystal_tower", (242, 196, 92, 255), (255, 234, 168, 255), True),
        ("crystals_total", (142, 192, 232, 255), (222, 242, 255, 255), True),
    ]
    for fname, col, hi, big in crystal_specs:
        img, d = _new_icon(hi if big else col)
        _draw_crystal_shape(d, col, hi, big=big)
        save(fname, img)

    # Books
    img, d = _new_icon(C_PARCHMENT)
    d.rectangle((16, 14, 48, 50), fill=(118, 68, 42, 255), outline=C_PARCHMENT, width=2)
    d.rectangle((16, 14, 23, 50), fill=(86, 46, 28, 255), outline=C_PARCHMENT, width=1)
    d.line([(28, 24), (43, 24)], fill=C_AMBER, width=2)
    d.line([(28, 31), (43, 31)], fill=C_PARCHMENT, width=2)
    d.line([(28, 38), (39, 38)], fill=C_PARCHMENT, width=2)
    save("books", img)

    # Cavern Moss
    img, d = _new_icon((118, 174, 96, 255))
    for cx, cy, r in ((24, 38, 12), (34, 32, 14), (42, 39, 11), (30, 42, 11)):
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=(76, 132, 62, 255), outline=(44, 82, 36, 255))
    for cx, cy in ((26, 30), (35, 26), (42, 34)):
        d.ellipse((cx - 3, cy - 3, cx + 3, cy + 3), fill=(148, 206, 112, 255))
    save("cavern_moss", img)

    # Musical Instrument Part
    img, d = _new_icon(C_BRASS)
    d.polygon([(14, 44), (44, 14), (50, 20), (22, 50)], fill=(166, 122, 68, 255), outline=C_PARCHMENT)
    d.line([(24, 34), (30, 40)], fill=C_AMBER, width=3)
    d.line([(34, 24), (40, 30)], fill=C_AMBER, width=3)
    save("instrument_part", img)

    # Drawing Paper
    img, d = _new_icon(C_PARCHMENT)
    d.rectangle((17, 13, 47, 51), fill=(214, 198, 166, 255), outline=C_INK, width=2)
    d.line([(23, 23), (41, 23)], fill=(96, 78, 56, 255), width=2)
    d.line([(23, 31), (41, 31)], fill=(96, 78, 56, 255), width=2)
    d.line([(23, 39), (35, 39)], fill=(96, 78, 56, 255), width=2)
    save("drawing_paper", img)

    # Sulfur
    img, d = _new_icon((236, 214, 68, 255))
    d.polygon([(18, 46), (26, 20), (42, 18), (48, 36), (38, 50)], fill=(224, 196, 56, 255), outline=C_INK)
    d.polygon([(26, 20), (38, 24), (34, 44)], fill=(252, 234, 112, 255))
    save("sulfur", img)

    # Time Rate Boost (+2x) & Time Skips
    img, d = _new_icon(C_AMBER)
    d.ellipse((14, 14, 50, 50), outline=C_AMBER, width=3)
    d.line([(32, 32), (32, 20)], fill=C_PARCHMENT, width=3)
    d.line([(32, 32), (42, 32)], fill=C_AMBER, width=3)
    _draw_centered_text(d, "+2×", 49, C_AMBER, size=11)
    save("time_rate", img)

    img, d = _new_icon(C_BRASS)
    d.polygon([(18, 14), (46, 14), (32, 32)], fill=C_AMBER, outline=C_PARCHMENT)
    d.polygon([(18, 50), (46, 50), (32, 32)], fill=(124, 96, 52, 255), outline=C_PARCHMENT)
    save("time_skip", img)

    # Discovery icon
    img, d = _new_icon(C_AMBER)
    d.polygon([(32, 10), (54, 32), (32, 54), (10, 32)], fill=(66, 52, 24, 255), outline=C_AMBER, width=3)
    d.ellipse((24, 28, 30, 34), fill=C_AMBER)
    d.ellipse((34, 28, 40, 34), fill=C_AMBER)
    save("discovery", img)

    # Goal stage icons
    goal_specs = [
        ("goal_wake_king", "KING", C_AMBER),
        ("goal_escape", "WELL", C_RING_3),
        ("goal_secret", "TOWER", (196, 134, 224, 255)),
        ("goal_hunt", "HUNT", (134, 212, 198, 255)),
        ("goal_any", "ANY", C_PARCHMENT),
    ]
    for fname, label, accent in goal_specs:
        img, d = _new_icon(accent)
        # Crown / crest silhouette
        d.polygon([(14, 32), (18, 16), (27, 25), (32, 13), (37, 25), (46, 16), (50, 32)], fill=accent, outline=C_INK)
        _draw_centered_text(d, label, 46, C_PARCHMENT, size=12)
        save(fname, img)

    # Settings icons
    opt_specs = [
        ("opt_tools", "TOOLS", C_AMBER),
        ("opt_books_vanilla", "BK:OFF", C_PARCHMENT_DIM),
        ("opt_books_prog", "BK:PRG", C_BRASS),
        ("opt_books_full", "BK:ALL", C_AMBER),
        ("opt_crystals_vanilla", "CR:OFF", C_PARCHMENT_DIM),
        ("opt_crystals_prog", "CR:PRG", C_RING_3),
        ("opt_crystals_full", "CR:ALL", C_AMBER),
        ("opt_moss", "MOSS", (118, 174, 96, 255)),
        ("opt_comfort", "HOME", C_PARCHMENT),
        ("opt_instrument", "MUSIC", C_BRASS),
        ("opt_coal", "COAL", (236, 214, 68, 255)),
        ("req_crystals", "REQ CR", C_RING_3),
        ("req_books", "REQ BK", C_PARCHMENT),
    ]
    for fname, label, accent in opt_specs:
        img, d = _new_icon(accent)
        _draw_centered_text(d, label, 32, accent, size=12)
        save(fname, img)


def build_manifest() -> dict[str, Any]:
    return {
        "name": PACK_NAME,
        "game_name": GAME_NAME,
        "package_version": WORLD_VERSION,
        "package_uid": PACK_UID,
        "author": AUTHOR,
        "min_poptracker_version": "0.25.9",
        "target_poptracker_version": "0.31.0",
        "variants": {
            "standard": {
                "display_name": "Cavern Map Tracker",
                "flags": ["ap"],
            },
            "var_itemsonly": {
                "display_name": "Items Only",
                "flags": ["ap"],
            },
        },
    }


def build_items_json() -> list[dict[str, Any]]:
    return [
        # Core tools & passes
        {
            "name": "Mattock",
            "type": "consumable",
            "img": "images/items/mattock.png",
            "max_quantity": 5,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "mattock",
        },
        {
            "name": "Secret Key",
            "type": "toggle",
            "img": "images/items/secret_key.png",
            "codes": "secret_key",
        },
        {
            "name": "Library Door Pass",
            "type": "toggle",
            "img": "images/items/library_pass.png",
            "codes": "library_door_pass",
        },
        {
            "name": "Surface Gate Pass",
            "type": "toggle",
            "img": "images/items/surface_gate_pass.png",
            "codes": "surface_gate_pass",
        },
        {
            "name": "Giant Mushroom Spores",
            "type": "toggle",
            "img": "images/items/giant_spores.png",
            "codes": "giant_mushroom_spores",
        },
        {
            "name": "Cavern Map",
            "type": "toggle",
            "img": "images/items/cavern_map.png",
            "codes": "cavern_map",
        },
        # Progressive mobility upgrades
        {
            "name": "Progressive Walk Speed",
            "type": "progressive",
            "allow_disabled": False,
            "loop": False,
            "codes": "progressive_walk_speed",
            "stages": [
                {
                    "name": "Walk Speed: 1× (Vanilla)",
                    "img": "images/items/walk_speed_0.png",
                    "codes": "walk_speed_100",
                    "inherit_codes": True,
                },
                {
                    "name": "Walk Speed: 1.25× (1/4)",
                    "img": "images/items/walk_speed_1.png",
                    "codes": "walk_speed_125",
                    "inherit_codes": True,
                },
                {
                    "name": "Walk Speed: 1.5× (2/4)",
                    "img": "images/items/walk_speed_2.png",
                    "codes": "walk_speed_150",
                    "inherit_codes": True,
                },
                {
                    "name": "Walk Speed: 1.75× (3/4)",
                    "img": "images/items/walk_speed_3.png",
                    "codes": "walk_speed_175",
                    "inherit_codes": True,
                },
                {
                    "name": "Walk Speed: 2× (4/4)",
                    "img": "images/items/walk_speed_4.png",
                    "codes": "walk_speed_200",
                    "inherit_codes": True,
                },
            ],
        },
        {
            "name": "Progressive Waypoint Recall",
            "type": "progressive",
            "allow_disabled": False,
            "loop": False,
            "codes": "progressive_waypoint_recall",
            "stages": [
                {
                    "name": "Waypoint Recall: Locked (0/3)",
                    "img": "images/items/recall_tier_0.png",
                    "codes": "recall_tier_0",
                    "inherit_codes": True,
                },
                {
                    "name": "Waypoint Recall: Ring 1 — Inner Kingdom (1/3)",
                    "img": "images/items/recall_tier_1.png",
                    "codes": "recall_tier_1",
                    "inherit_codes": True,
                },
                {
                    "name": "Waypoint Recall: Ring 2 — Middle Caverns & Library (2/3)",
                    "img": "images/items/recall_tier_2.png",
                    "codes": "recall_tier_2",
                    "inherit_codes": True,
                },
                {
                    "name": "Waypoint Recall: Ring 3 — Deep & Upper Caverns (3/3)",
                    "img": "images/items/recall_tier_3.png",
                    "codes": "recall_tier_3",
                    "inherit_codes": True,
                },
            ],
        },
        # Three recall ring badges (auto-synced with Progressive Waypoint Recall)
        {
            "name": f"{RECALL_RING_INFO[1]['title']} ({RECALL_RING_INFO[1]['summary']})",
            "type": "toggle",
            "img": "images/items/ring_1.png",
            "codes": "recall_ring_1",
        },
        {
            "name": f"{RECALL_RING_INFO[2]['title']} ({RECALL_RING_INFO[2]['summary']})",
            "type": "toggle",
            "img": "images/items/ring_2.png",
            "codes": "recall_ring_2",
        },
        {
            "name": f"{RECALL_RING_INFO[3]['title']} ({RECALL_RING_INFO[3]['summary']})",
            "type": "toggle",
            "img": "images/items/ring_3.png",
            "codes": "recall_ring_3",
        },
        # Home & check requirements
        {
            "name": "Cavern Moss",
            "type": "consumable",
            "img": "images/items/cavern_moss.png",
            "max_quantity": 15,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "cavern_moss",
        },
        {
            "name": "Musical Instrument Part",
            "type": "consumable",
            "img": "images/items/instrument_part.png",
            "max_quantity": 3,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "musical_instrument_part",
        },
        {
            "name": "Drawing Paper",
            "type": "consumable",
            "img": "images/items/drawing_paper.png",
            "max_quantity": 3,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "drawing_paper",
        },
        {
            "name": "Sulfur",
            "type": "consumable",
            "img": "images/items/sulfur.png",
            "max_quantity": 2,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "sulfur",
        },
        # Collectibles (Books & Crystals)
        {
            "name": "Classic Books",
            "type": "consumable",
            "img": "images/items/books.png",
            "max_quantity": 31,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "books",
        },
        {
            "name": "Crystals Collected (Total)",
            "type": "consumable",
            "img": "images/items/crystals_total.png",
            "max_quantity": 16,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "crystals_total",
        },
        {
            "name": "White Crystal",
            "type": "consumable",
            "img": "images/items/crystal_white.png",
            "max_quantity": 6,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "crystal_white",
        },
        {
            "name": "Rose Crystal",
            "type": "consumable",
            "img": "images/items/crystal_rose.png",
            "max_quantity": 4,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "crystal_rose",
        },
        {
            "name": "Blue Crystal",
            "type": "consumable",
            "img": "images/items/crystal_blue.png",
            "max_quantity": 2,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "crystal_blue",
        },
        {
            "name": "Purple Crystal",
            "type": "toggle",
            "img": "images/items/crystal_purple.png",
            "codes": "crystal_purple",
        },
        {
            "name": "Black Crystal",
            "type": "toggle",
            "img": "images/items/crystal_black.png",
            "codes": "crystal_black",
        },
        {
            "name": "Great Hall Big Crystal",
            "type": "toggle",
            "img": "images/items/crystal_great_hall.png",
            "codes": "crystal_great_hall",
        },
        {
            "name": "Tower Big Crystal",
            "type": "toggle",
            "img": "images/items/crystal_tower.png",
            "codes": "crystal_tower",
        },
        # Time pacing items
        {
            "name": "Time Rate Boost (+2x)",
            "type": "consumable",
            "img": "images/items/time_rate.png",
            "max_quantity": 10,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "time_rate_boost",
        },
        {
            "name": "Time Skips Received",
            "type": "consumable",
            "img": "images/items/time_skip.png",
            "max_quantity": 40,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 0,
            "codes": "time_skips",
        },
    ]


def build_settings_json() -> list[dict[str, Any]]:
    return [
        {
            "name": "Goal",
            "type": "progressive",
            "allow_disabled": False,
            "loop": True,
            "codes": "opt_goal",
            "stages": [
                {
                    "name": "Goal: Wake the King",
                    "img": "images/items/goal_wake_king.png",
                    "codes": "goal_wake_the_king",
                    "inherit_codes": False,
                },
                {
                    "name": "Goal: Escape to the Surface",
                    "img": "images/items/goal_escape.png",
                    "codes": "goal_escape_surface",
                    "inherit_codes": False,
                },
                {
                    "name": "Goal: Secret Ending",
                    "img": "images/items/goal_secret.png",
                    "codes": "goal_secret_ending",
                    "inherit_codes": False,
                },
                {
                    "name": "Goal: Crystal & Book Hunt",
                    "img": "images/items/goal_hunt.png",
                    "codes": "goal_crystal_book_hunt",
                    "inherit_codes": False,
                },
                {
                    "name": "Goal: Any Ending",
                    "img": "images/items/goal_any.png",
                    "codes": "goal_any_ending",
                    "inherit_codes": False,
                },
            ],
        },
        {
            "name": "Shuffle Mattocks & Keys",
            "type": "toggle",
            "img": "images/items/opt_tools.png",
            "initial_active_state": True,
            "codes": "opt_shuffle_mattocks_and_keys",
        },
        {
            "name": "Shuffle Books",
            "type": "progressive",
            "allow_disabled": False,
            "loop": True,
            "initial_stage_idx": 1,
            "codes": "opt_shuffle_books",
            "stages": [
                {
                    "name": "Shuffle Books: Vanilla",
                    "img": "images/items/opt_books_vanilla.png",
                    "codes": "books_vanilla",
                    "inherit_codes": False,
                },
                {
                    "name": "Shuffle Books: Progression",
                    "img": "images/items/opt_books_prog.png",
                    "codes": "books_progression",
                    "inherit_codes": False,
                },
                {
                    "name": "Shuffle Books: Full",
                    "img": "images/items/opt_books_full.png",
                    "codes": "books_full",
                    "inherit_codes": False,
                },
            ],
        },
        {
            "name": "Shuffle Crystals",
            "type": "progressive",
            "allow_disabled": False,
            "loop": True,
            "initial_stage_idx": 2,
            "codes": "opt_shuffle_crystals",
            "stages": [
                {
                    "name": "Shuffle Crystals: Vanilla",
                    "img": "images/items/opt_crystals_vanilla.png",
                    "codes": "crystals_vanilla",
                    "inherit_codes": False,
                },
                {
                    "name": "Shuffle Crystals: Progression",
                    "img": "images/items/opt_crystals_prog.png",
                    "codes": "crystals_progression",
                    "inherit_codes": False,
                },
                {
                    "name": "Shuffle Crystals: Full",
                    "img": "images/items/opt_crystals_full.png",
                    "codes": "crystals_full",
                    "inherit_codes": False,
                },
            ],
        },
        {
            "name": "Shuffle Mushrooms & Moss",
            "type": "toggle",
            "img": "images/items/opt_moss.png",
            "initial_active_state": True,
            "codes": "opt_shuffle_mushrooms_and_moss",
        },
        {
            "name": "Shuffle Home Comfort",
            "type": "toggle",
            "img": "images/items/opt_comfort.png",
            "initial_active_state": True,
            "codes": "opt_shuffle_home_comfort",
        },
        {
            "name": "Shuffle Instrument Parts",
            "type": "toggle",
            "img": "images/items/opt_instrument.png",
            "initial_active_state": True,
            "codes": "opt_shuffle_instrument_parts",
        },
        {
            "name": "Shuffle Coal & Sulfur",
            "type": "toggle",
            "img": "images/items/opt_coal.png",
            "initial_active_state": False,
            "codes": "opt_shuffle_coal_and_sulfur",
        },
        {
            "name": "Shuffle Cavern Peddler Shop",
            "type": "toggle",
            "img": "images/items/opt_comfort.png",
            "initial_active_state": True,
            "codes": "opt_shuffle_shop",
        },
        {
            "name": "Required Crystals (Hunt Goal)",
            "type": "consumable",
            "img": "images/items/req_crystals.png",
            "min_quantity": 0,
            "max_quantity": 16,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 8,
            "codes": "opt_required_crystals",
        },
        {
            "name": "Required Books (Hunt Goal)",
            "type": "consumable",
            "img": "images/items/req_books.png",
            "min_quantity": 0,
            "max_quantity": 30,
            "increment": 1,
            "decrement": 1,
            "initial_quantity": 12,
            "codes": "opt_required_books",
        },
    ]


def build_maps_json() -> list[dict[str, Any]]:
    return [
        {
            "name": "cavern_map",
            "location_size": 22,
            "location_border_thickness": 2,
            "img": "images/maps/cavern_map.png",
        }
    ]


def build_locations_and_mappings(
    locations: list[dict[str, Any]],
) -> tuple[list[dict[str, Any]], dict[int, str], dict[str, int]]:
    """Build PopTracker locations.json plus AP<->PopTracker location mappings."""
    grouped: dict[str, list[dict[str, Any]]] = {node["key"]: [] for node in MAP_REGION_NODES}
    discoveries: list[dict[str, Any]] = []

    for loc in locations:
        if str(loc["pool"]) == "mobility":
            discoveries.append(loc)
        else:
            grouped[resolve_map_node_key(loc)].append(loc)

    pop_locations: list[dict[str, Any]] = []
    id_to_pop_path: dict[int, str] = {}
    pop_path_to_id: dict[str, int] = {}

    for node in MAP_REGION_NODES:
        sections: list[dict[str, Any]] = []
        for loc in grouped[node["key"]]:
            loc_id = int(loc["id"])
            loc_name = str(loc["name"])
            section: dict[str, Any] = {
                "name": loc_name,
                "visibility_rules": [f"$loc_enabled|{loc_id}"],
            }
            rules = reqs_to_access_rules(list(loc.get("requires", [])))
            if rules:
                section["access_rules"] = rules
            sections.append(section)

            rel_path = f"{node['name']}/{loc_name}"
            id_to_pop_path[loc_id] = f"@{rel_path}"
            pop_path_to_id[rel_path] = loc_id

        # Attach victory / ending goal markers to their logical cavern regions
        if node["key"] == "kings_hall":
            sections.append(
                {
                    "name": "Wake the Sleeping King",
                    "access_rules": ["$can_wake_king"],
                    "visibility_rules": ["goal_wake_the_king", "goal_any_ending"],
                }
            )
        elif node["key"] == "surface_well":
            sections.append(
                {
                    "name": "Escape via the Well",
                    "visibility_rules": ["goal_escape_surface", "goal_any_ending"],
                }
            )
        elif node["key"] == "secret_tower":
            sections.append(
                {
                    "name": "Reach the Secret Ending",
                    "access_rules": ["$can_secret_ending"],
                    "visibility_rules": ["goal_secret_ending", "goal_any_ending"],
                }
            )
        elif node["key"] == "shades_home":
            sections.append(
                {
                    "name": "Complete Crystal & Book Collection",
                    "access_rules": ["$can_complete_hunt"],
                    "visibility_rules": ["goal_crystal_book_hunt"],
                }
            )

        entry: dict[str, Any] = {
            "name": node["name"],
            "map_locations": [
                {
                    "map": "cavern_map",
                    "x": int(node["x"]),
                    "y": int(node["y"]),
                    "size": 22,
                }
            ],
            "sections": sections,
        }
        if node["access_rules"]:
            entry["access_rules"] = list(node["access_rules"])
        pop_locations.append(entry)

    # Dedicated diamond pins for all seven discovery checks
    for loc in discoveries:
        uid = str(loc["game_uid"])
        if uid not in DISCOVERY_PIN_LAYOUT:
            raise ValueError(f"Missing discovery pin layout for {uid!r}")
        pin = DISCOVERY_PIN_LAYOUT[uid]
        loc_id = int(loc["id"])
        loc_name = str(loc["name"])
        node_name = str(pin["node_name"])

        entry = {
            "name": node_name,
            "map_locations": [
                {
                    "map": "cavern_map",
                    "x": int(pin["x"]),
                    "y": int(pin["y"]),
                    "size": 16,
                    "shape": "diamond",
                }
            ],
            "sections": [
                {
                    "name": loc_name,
                    "visibility_rules": [f"$loc_enabled|{loc_id}"],
                }
            ],
        }
        if pin["access_rules"]:
            entry["access_rules"] = list(pin["access_rules"])
        pop_locations.append(entry)

        rel_path = f"{node_name}/{loc_name}"
        id_to_pop_path[loc_id] = f"@{rel_path}"
        pop_path_to_id[rel_path] = loc_id

    return pop_locations, id_to_pop_path, pop_path_to_id


def build_layouts() -> dict[str, dict[str, Any]]:
    items_layout = {
        "item_grid": {
            "type": "array",
            "orientation": "vertical",
            "margin": "0,0",
            "content": [
                {
                    "type": "group",
                    "header": "Cavern Tools & Passes",
                    "content": {
                        "type": "itemgrid",
                        "item_margin": "3,3",
                        "h_alignment": "left",
                        "rows": [
                            [
                                "mattock",
                                "secret_key",
                                "library_door_pass",
                                "surface_gate_pass",
                                "giant_mushroom_spores",
                                "cavern_map",
                            ]
                        ],
                    },
                },
                {
                    "type": "group",
                    "header": "Progressive Mobility & Recall Rings",
                    "content": {
                        "type": "itemgrid",
                        "item_margin": "3,3",
                        "h_alignment": "left",
                        "rows": [
                            [
                                "progressive_walk_speed",
                                "progressive_waypoint_recall",
                                "",
                                "recall_ring_1",
                                "recall_ring_2",
                                "recall_ring_3",
                            ]
                        ],
                    },
                },
                {
                    "type": "group",
                    "header": "Home Requirements & Pacing",
                    "content": {
                        "type": "itemgrid",
                        "item_margin": "3,3",
                        "h_alignment": "left",
                        "rows": [
                            [
                                "cavern_moss",
                                "musical_instrument_part",
                                "drawing_paper",
                                "sulfur",
                                "time_rate_boost",
                                "time_skips",
                            ]
                        ],
                    },
                },
                {
                    "type": "group",
                    "header": "Crystals & Classic Books",
                    "content": {
                        "type": "itemgrid",
                        "item_margin": "3,3",
                        "h_alignment": "left",
                        "rows": [
                            [
                                "crystals_total",
                                "books",
                                "crystal_white",
                                "crystal_rose",
                                "crystal_blue",
                                "crystal_purple",
                            ],
                            [
                                "crystal_black",
                                "crystal_great_hall",
                                "crystal_tower",
                                "",
                                "",
                                "",
                            ],
                        ],
                    },
                },
            ],
        }
    }

    settings_layout = {
        "settings_grid": {
            "type": "group",
            "header": "Seed Options (Auto-Synced from Archipelago)",
            "content": {
                "type": "itemgrid",
                "item_margin": "3,3",
                "h_alignment": "left",
                "rows": [
                    [
                        "opt_goal",
                        "opt_shuffle_mattocks_and_keys",
                        "opt_shuffle_books",
                        "opt_shuffle_crystals",
                        "opt_shuffle_mushrooms_and_moss",
                    ],
                    [
                        "opt_shuffle_home_comfort",
                        "opt_shuffle_instrument_parts",
                        "opt_shuffle_coal_and_sulfur",
                        "opt_shuffle_shop",
                        "opt_required_crystals",
                        "opt_required_books",
                    ],
                ],
            },
        }
    }

    tracker_layout = {
        "tracker_default": {
            "type": "container",
            "background": "#110e0c",
            "content": {
                "type": "dock",
                "dropshadow": True,
                "content": [
                    {
                        "type": "dock",
                        "dock": "left",
                        "v_alignment": "stretch",
                        "content": [
                            {
                                "type": "tabbed",
                                "tabs": [
                                    {
                                        "title": "Items & Mobility",
                                        "content": {
                                            "type": "layout",
                                            "key": "item_grid",
                                        },
                                    },
                                    {
                                        "title": "Options",
                                        "content": {
                                            "type": "layout",
                                            "key": "settings_grid",
                                        },
                                    },
                                ],
                            }
                        ],
                    },
                    {
                        "type": "tabbed",
                        "tabs": [
                            {
                                "title": "Underground Kingdom",
                                "content": {
                                    "type": "map",
                                    "maps": ["cavern_map"],
                                },
                            }
                        ],
                    },
                ],
            },
        }
    }

    itemsonly_tracker_layout = {
        "tracker_default": {
            "type": "container",
            "background": "#110e0c",
            "content": {
                "type": "tabbed",
                "tabs": [
                    {
                        "title": "Items & Mobility",
                        "content": {
                            "type": "layout",
                            "key": "item_grid",
                        },
                    },
                    {
                        "title": "Options",
                        "content": {
                            "type": "layout",
                            "key": "settings_grid",
                        },
                    },
                ],
            },
        }
    }

    broadcast_layout = {
        "tracker_broadcast": {
            "type": "container",
            "background": "#110e0c",
            "content": {
                "type": "itemgrid",
                "item_margin": "3,3",
                "h_alignment": "left",
                "rows": [
                    [
                        "mattock",
                        "secret_key",
                        "library_door_pass",
                        "surface_gate_pass",
                        "giant_mushroom_spores",
                        "crystals_total",
                        "books",
                    ],
                    [
                        "progressive_walk_speed",
                        "progressive_waypoint_recall",
                        "recall_ring_1",
                        "recall_ring_2",
                        "recall_ring_3",
                        "cavern_moss",
                        "musical_instrument_part",
                    ],
                ],
            },
        }
    }

    return {
        "layouts/items.json": items_layout,
        "layouts/settings.json": settings_layout,
        "layouts/tracker.json": tracker_layout,
        "layouts/broadcast.json": broadcast_layout,
        "var_itemsonly/layouts/tracker.json": itemsonly_tracker_layout,
    }


def _lua_escape(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"')


def build_item_mapping_lua(items: list[dict[str, Any]]) -> str:
    name_to_pop: dict[str, tuple[str, str]] = {
        "Mattock": ("mattock", "consumable"),
        "Secret Key": ("secret_key", "toggle"),
        "Library Door Pass": ("library_door_pass", "toggle"),
        "Surface Gate Pass": ("surface_gate_pass", "toggle"),
        "Giant Mushroom Spores": ("giant_mushroom_spores", "toggle"),
        "Cavern Map": ("cavern_map", "toggle"),
        "White Crystal": ("crystal_white", "consumable"),
        "Rose Crystal": ("crystal_rose", "consumable"),
        "Blue Crystal": ("crystal_blue", "consumable"),
        "Purple Crystal": ("crystal_purple", "toggle"),
        "Black Crystal": ("crystal_black", "toggle"),
        "Great Hall Big Crystal": ("crystal_great_hall", "toggle"),
        "Tower Big Crystal": ("crystal_tower", "toggle"),
        "Musical Instrument Part": ("musical_instrument_part", "consumable"),
        "Cavern Moss": ("cavern_moss", "consumable"),
        "Drawing Paper": ("drawing_paper", "consumable"),
        "Sulfur": ("sulfur", "consumable"),
        "Time Skip (15 Minutes)": ("time_skips", "consumable"),
        "Time Skip (1 Hour)": ("time_skips", "consumable"),
        "Time Skip (6 Hours)": ("time_skips", "consumable"),
        "Time Skip (1 Day)": ("time_skips", "consumable"),
        "King's Snooze Button": ("time_skips", "consumable"),
        "Time Rate Boost (+2x)": ("time_rate_boost", "consumable"),
        "Progressive Walk Speed": ("progressive_walk_speed", "progressive"),
        "Progressive Waypoint Recall": ("progressive_waypoint_recall", "progressive"),
    }

    lines = [
        "-- Auto-generated by tools/build_tracker_pack.py from data/world_catalog.json.",
        "-- Maps Archipelago item IDs to PopTracker item codes and types.",
        "ITEM_MAPPING = {",
    ]
    for it in items:
        item_id = int(it["id"])
        name = str(it["name"])
        category = str(it["category"])
        if name in name_to_pop:
            code, kind = name_to_pop[name]
            lines.append(f'    [{item_id}] = {{"{code}", "{kind}"}}, -- {name}')
        elif category == "books":
            lines.append(f'    [{item_id}] = {{"books", "consumable"}}, -- {name}')
    lines.append("}")
    lines.append("")
    return "\n".join(lines)


def build_location_mapping_lua(
    locations: list[dict[str, Any]],
    id_to_pop_path: dict[int, str],
) -> str:
    lines = [
        "-- Auto-generated by tools/build_tracker_pack.py from data/world_catalog.json.",
        "-- Maps Archipelago location IDs to PopTracker section paths and metadata.",
        "LOCATION_MAPPING = {",
    ]
    for loc in locations:
        loc_id = int(loc["id"])
        pop_path = _lua_escape(id_to_pop_path[loc_id])
        lines.append(f'    [{loc_id}] = {{"{pop_path}"}},')
    lines.append("}")
    lines.append("")
    lines.append("LOCATION_METADATA = {")
    for loc in locations:
        loc_id = int(loc["id"])
        pool = _lua_escape(str(loc["pool"]))
        depth = _lua_escape(str(loc["depth"]))
        ap_region = _lua_escape(str(loc["ap_region"]))
        scene = _lua_escape(str(loc["scene"]))
        lines.append(
            f'    [{loc_id}] = {{ pool = "{pool}", depth = "{depth}", ap_region = "{ap_region}", scene = "{scene}" }},'
        )
    lines.append("}")
    lines.append("")
    lines.append("LOCATION_ORDER = {")
    for loc in locations:
        lines.append(f"    {int(loc['id'])},")
    lines.append("}")
    lines.append("")
    return "\n".join(lines)


def build_logic_lua(recall_ring_scenes: dict[int, list[str]]) -> str:
    ring_lines = ["RECALL_RING_SCENES = {"]
    for ring in (1, 2, 3):
        joined = ", ".join(f'"{_lua_escape(s)}"' for s in recall_ring_scenes[ring])
        ring_lines.append(f"    [{ring}] = {{ {joined} }},")
    ring_lines.append("}")
    ring_block = "\n".join(ring_lines)

    return f'''-- Logic rules for The Longing Decrypted PopTracker pack.
-- Mirrors apworld/the_longing/rules.py, regions.py, and locations.py.

SLOT_DATA = {{}}
SLOT_ENABLED_LOCATIONS = nil
SYNCING_RINGS = false
SYNCING_CRYSTALS = false

{ring_block}

local function opt_bool(code)
    local obj = Tracker:FindObjectForCode(code)
    return obj ~= nil and obj.Active == true
end

local function opt_stage(code)
    local obj = Tracker:FindObjectForCode(code)
    if obj == nil then
        return 0
    end
    return obj.CurrentStage or 0
end

local function item_count(code)
    return Tracker:ProviderCountForCode(code)
end

-- Sync individual Recall Ring badges with Progressive Waypoint Recall stage (0..3)
function sync_recall_rings_from_progressive()
    if SYNCING_RINGS then
        return
    end
    SYNCING_RINGS = true
    local prog = Tracker:FindObjectForCode("progressive_waypoint_recall")
    local tier = prog and (prog.CurrentStage or 0) or 0
    for ring = 1, 3 do
        local badge = Tracker:FindObjectForCode("recall_ring_" .. ring)
        if badge ~= nil then
            badge.Active = (tier >= ring)
        end
    end
    SYNCING_RINGS = false
end

function sync_progressive_from_recall_ring(ring_code)
    if SYNCING_RINGS then
        return
    end
    SYNCING_RINGS = true
    local prog = Tracker:FindObjectForCode("progressive_waypoint_recall")
    local clicked = Tracker:FindObjectForCode(ring_code)
    if prog ~= nil and clicked ~= nil then
        local ring_num = tonumber(string.sub(ring_code, -1)) or 0
        local target_tier = ring_num
        if not clicked.Active then
            target_tier = math.max(0, ring_num - 1)
        end
        prog.CurrentStage = target_tier
        for ring = 1, 3 do
            local badge = Tracker:FindObjectForCode("recall_ring_" .. ring)
            if badge ~= nil then
                badge.Active = (target_tier >= ring)
            end
        end
    end
    SYNCING_RINGS = false
end

function has_recall_ring(ring_str)
    local ring = tonumber(ring_str) or 1
    local prog = Tracker:FindObjectForCode("progressive_waypoint_recall")
    local tier = prog and (prog.CurrentStage or 0) or 0
    return tier >= ring
end

-- Sync Crystals Collected (Total) from the seven crystal types
local CRYSTAL_CODES = {{
    "crystal_white",
    "crystal_rose",
    "crystal_blue",
    "crystal_purple",
    "crystal_black",
    "crystal_great_hall",
    "crystal_tower",
}}

function total_crystals_from_types()
    local total = 0
    for _, code in ipairs(CRYSTAL_CODES) do
        total = total + item_count(code)
    end
    return total
end

function sync_crystals_total()
    if SYNCING_CRYSTALS then
        return
    end
    SYNCING_CRYSTALS = true
    local total_obj = Tracker:FindObjectForCode("crystals_total")
    if total_obj ~= nil then
        total_obj.AcquiredCount = total_crystals_from_types()
    end
    SYNCING_CRYSTALS = false
end

function crystal_count()
    local total_obj = Tracker:FindObjectForCode("crystals_total")
    local manual_total = total_obj and (total_obj.AcquiredCount or 0) or 0
    return math.max(manual_total, total_crystals_from_types())
end

function book_count()
    return item_count("books")
end

-- Core progression predicates matching apworld/the_longing/rules.py
function has_mattocks(count_str)
    local count = tonumber(count_str) or 1
    if not opt_bool("opt_shuffle_mattocks_and_keys") then
        return true
    end
    return item_count("mattock") >= count
end

function has_secret_key()
    if not opt_bool("opt_shuffle_mattocks_and_keys") then
        return true
    end
    return item_count("secret_key") >= 1
end

function has_library_pass()
    if not opt_bool("opt_shuffle_mattocks_and_keys") then
        return true
    end
    return item_count("library_door_pass") >= 1
end

function has_surface_gate()
    if not opt_bool("opt_shuffle_mattocks_and_keys") then
        return true
    end
    return item_count("surface_gate_pass") >= 1
end

function has_giant_spores()
    if not (opt_bool("opt_shuffle_mushrooms_and_moss") or opt_bool("opt_shuffle_mattocks_and_keys")) then
        return true
    end
    return item_count("giant_mushroom_spores") >= 1
end

function can_reach_secret_tower()
    return has_surface_gate() and has_giant_spores()
end

function can_reach_mines()
    return has_surface_gate()
end

function has_moss_10()
    if not opt_bool("opt_shuffle_mushrooms_and_moss") then
        return true
    end
    return item_count("cavern_moss") >= 10
end

function has_sulfur()
    if opt_bool("opt_shuffle_coal_and_sulfur") then
        return item_count("sulfur") >= 1
    end
    return can_reach_mines() and has_mattocks(1)
end

function has_instrument_3()
    if not opt_bool("opt_shuffle_instrument_parts") then
        return true
    end
    return item_count("musical_instrument_part") >= 3
end

function has_paper()
    if not opt_bool("opt_shuffle_home_comfort") then
        return true
    end
    return item_count("drawing_paper") >= 1
end

-- Goal completion predicates matching apworld/the_longing/rules.py
function can_wake_king()
    return has_mattocks(1) and has_surface_gate()
end

function can_secret_ending()
    return can_reach_secret_tower() and has_secret_key()
end

function effective_required_crystals()
    if SLOT_DATA ~= nil and SLOT_DATA["required_crystals"] ~= nil then
        return tonumber(SLOT_DATA["required_crystals"]) or 0
    end
    local stage = opt_stage("opt_shuffle_crystals")
    local pool = 0
    if stage == 1 then
        pool = 7
    elseif stage == 2 then
        pool = 16
    end
    local req_obj = Tracker:FindObjectForCode("opt_required_crystals")
    local req = req_obj and (req_obj.AcquiredCount or 0) or 8
    return math.min(req, pool)
end

function effective_required_books()
    if SLOT_DATA ~= nil and SLOT_DATA["required_books"] ~= nil then
        return tonumber(SLOT_DATA["required_books"]) or 0
    end
    local stage = opt_stage("opt_shuffle_books")
    local pool = 0
    if stage == 1 then
        pool = 16
    elseif stage == 2 then
        pool = 31
    end
    local req_obj = Tracker:FindObjectForCode("opt_required_books")
    local req = req_obj and (req_obj.AcquiredCount or 0) or 12
    return math.min(req, pool)
end

function can_complete_hunt()
    local req_c = effective_required_crystals()
    local req_b = effective_required_books()
    if req_c == 0 and req_b == 0 then
        return has_library_pass() and can_reach_mines() and has_mattocks(1)
    end
    return crystal_count() >= req_c and book_count() >= req_b
end

-- Option-driven location visibility matching apworld/the_longing/locations.py
local function is_pool_enabled(meta)
    local pool = meta.pool
    local depth = meta.depth
    if pool == "mobility" then
        return true
    elseif pool == "tools" then
        return opt_bool("opt_shuffle_mattocks_and_keys")
    elseif pool == "books" then
        local st = opt_stage("opt_shuffle_books")
        if st == 2 then
            return true
        elseif st == 1 and depth == "progression" then
            return true
        end
        return false
    elseif pool == "crystals" then
        local st = opt_stage("opt_shuffle_crystals")
        if st == 2 then
            return true
        elseif st == 1 and depth == "progression" then
            return true
        end
        return false
    elseif pool == "mushrooms_moss" then
        return opt_bool("opt_shuffle_mushrooms_and_moss")
    elseif pool == "comfort" then
        return opt_bool("opt_shuffle_home_comfort")
    elseif pool == "instrument" then
        return opt_bool("opt_shuffle_instrument_parts")
    elseif pool == "coal_sulfur" then
        return opt_bool("opt_shuffle_coal_and_sulfur")
    elseif pool == "shop" then
        return opt_bool("opt_shuffle_shop")
    end
    return false
end

local function compute_manual_enabled_set()
    local enabled = {{}}
    local count = 0
    for _, loc_id in ipairs(LOCATION_ORDER) do
        local meta = LOCATION_METADATA[loc_id]
        if meta ~= nil and meta.pool ~= "mobility" and is_pool_enabled(meta) then
            enabled[loc_id] = true
            count = count + 1
        end
    end
    -- Mirror locations.py fallback: guarantee at least 12 non-mobility checks if < 10 were enabled
    if count < 10 then
        for _, loc_id in ipairs(LOCATION_ORDER) do
            local meta = LOCATION_METADATA[loc_id]
            if meta ~= nil and (meta.pool == "tools" or meta.pool == "comfort") and not enabled[loc_id] then
                enabled[loc_id] = true
                count = count + 1
                if count >= 12 then
                    break
                end
            end
        end
    end
    for _, loc_id in ipairs(LOCATION_ORDER) do
        local meta = LOCATION_METADATA[loc_id]
        if meta ~= nil and meta.pool == "mobility" then
            enabled[loc_id] = true
        end
    end
    return enabled
end

function loc_enabled(id_str)
    local loc_id = tonumber(id_str)
    if loc_id == nil then
        return false
    end
    if SLOT_ENABLED_LOCATIONS ~= nil then
        return SLOT_ENABLED_LOCATIONS[loc_id] == true
    end
    local meta = LOCATION_METADATA[loc_id]
    if meta == nil then
        return false
    end
    if meta.pool == "mobility" then
        return true
    end
    local manual = compute_manual_enabled_set()
    return manual[loc_id] == true
end
'''


def build_archipelago_lua() -> str:
    return '''-- Archipelago autotracking client for The Longing Decrypted PopTracker pack.

ScriptHost:LoadScript("scripts/autotracking/item_mapping.lua")
ScriptHost:LoadScript("scripts/autotracking/location_mapping.lua")

CUR_INDEX = -1

local GOAL_STAGE_MAP = {
    wake_the_king = 0,
    escape_surface = 1,
    secret_ending = 2,
    crystal_book_hunt = 3,
    any_ending = 4,
}

local DEPTH_STAGE_MAP = {
    vanilla = 0,
    progression = 1,
    full = 2,
}

local RESET_CODES = {
    { "mattock", "consumable" },
    { "secret_key", "toggle" },
    { "library_door_pass", "toggle" },
    { "surface_gate_pass", "toggle" },
    { "giant_mushroom_spores", "toggle" },
    { "cavern_map", "toggle" },
    { "progressive_walk_speed", "progressive" },
    { "progressive_waypoint_recall", "progressive" },
    { "recall_ring_1", "toggle" },
    { "recall_ring_2", "toggle" },
    { "recall_ring_3", "toggle" },
    { "cavern_moss", "consumable" },
    { "musical_instrument_part", "consumable" },
    { "drawing_paper", "consumable" },
    { "sulfur", "consumable" },
    { "books", "consumable" },
    { "crystals_total", "consumable" },
    { "crystal_white", "consumable" },
    { "crystal_rose", "consumable" },
    { "crystal_blue", "consumable" },
    { "crystal_purple", "toggle" },
    { "crystal_black", "toggle" },
    { "crystal_great_hall", "toggle" },
    { "crystal_tower", "toggle" },
    { "time_rate_boost", "consumable" },
    { "time_skips", "consumable" },
}

local function set_toggle(code, value)
    local obj = Tracker:FindObjectForCode(code)
    if obj ~= nil then
        obj.Active = (value == true or value == 1)
    end
end

local function set_stage(code, stage)
    local obj = Tracker:FindObjectForCode(code)
    if obj ~= nil and stage ~= nil then
        obj.CurrentStage = stage
    end
end

local function set_count(code, count)
    local obj = Tracker:FindObjectForCode(code)
    if obj ~= nil and count ~= nil then
        obj.AcquiredCount = count
    end
end

local function reset_items()
    for _, entry in ipairs(RESET_CODES) do
        local obj = Tracker:FindObjectForCode(entry[1])
        if obj ~= nil then
            if entry[2] == "toggle" then
                obj.Active = false
            elseif entry[2] == "consumable" then
                obj.AcquiredCount = obj.MinCount or 0
            elseif entry[2] == "progressive" then
                obj.CurrentStage = 0
                obj.Active = true
            end
        end
    end
end

local function reset_locations()
    for _, paths in pairs(LOCATION_MAPPING) do
        for _, path in ipairs(paths) do
            local obj = Tracker:FindObjectForCode(path)
            if obj ~= nil then
                if path:sub(1, 1) == "@" then
                    obj.AvailableChestCount = obj.ChestCount
                else
                    obj.Active = false
                end
            end
        end
    end
end

local function apply_slot_data(slot_data)
    SLOT_DATA = slot_data or {}
    SLOT_ENABLED_LOCATIONS = nil

    if type(slot_data) ~= "table" then
        return
    end

    if slot_data["goal"] ~= nil then
        local g = slot_data["goal"]
        if type(g) == "string" and GOAL_STAGE_MAP[g] ~= nil then
            set_stage("opt_goal", GOAL_STAGE_MAP[g])
        elseif type(g) == "number" then
            set_stage("opt_goal", g)
        end
    end

    if slot_data["shuffle_mattocks_and_keys"] ~= nil then
        set_toggle("opt_shuffle_mattocks_and_keys", slot_data["shuffle_mattocks_and_keys"])
    end

    if slot_data["shuffle_books"] ~= nil then
        local sb = slot_data["shuffle_books"]
        if type(sb) == "string" and DEPTH_STAGE_MAP[sb] ~= nil then
            set_stage("opt_shuffle_books", DEPTH_STAGE_MAP[sb])
        elseif type(sb) == "number" then
            set_stage("opt_shuffle_books", sb)
        end
    end

    if slot_data["shuffle_crystals"] ~= nil then
        local sc = slot_data["shuffle_crystals"]
        if type(sc) == "string" and DEPTH_STAGE_MAP[sc] ~= nil then
            set_stage("opt_shuffle_crystals", DEPTH_STAGE_MAP[sc])
        elseif type(sc) == "number" then
            set_stage("opt_shuffle_crystals", sc)
        end
    end

    if slot_data["shuffle_mushrooms_and_moss"] ~= nil then
        set_toggle("opt_shuffle_mushrooms_and_moss", slot_data["shuffle_mushrooms_and_moss"])
    end
    if slot_data["shuffle_home_comfort"] ~= nil then
        set_toggle("opt_shuffle_home_comfort", slot_data["shuffle_home_comfort"])
    end
    if slot_data["shuffle_instrument_parts"] ~= nil then
        set_toggle("opt_shuffle_instrument_parts", slot_data["shuffle_instrument_parts"])
    end
    if slot_data["shuffle_coal_and_sulfur"] ~= nil then
        set_toggle("opt_shuffle_coal_and_sulfur", slot_data["shuffle_coal_and_sulfur"])
    end
    if slot_data["shuffle_shop"] ~= nil then
        set_toggle("opt_shuffle_shop", slot_data["shuffle_shop"])
    end
    if slot_data["required_crystals"] ~= nil then
        set_count("opt_required_crystals", tonumber(slot_data["required_crystals"]) or 0)
    end
    if slot_data["required_books"] ~= nil then
        set_count("opt_required_books", tonumber(slot_data["required_books"]) or 0)
    end

    if type(slot_data["enabled_location_ids"]) == "table" then
        SLOT_ENABLED_LOCATIONS = {}
        for _, loc_id in ipairs(slot_data["enabled_location_ids"]) do
            local num = tonumber(loc_id)
            if num ~= nil then
                SLOT_ENABLED_LOCATIONS[num] = true
            end
        end
    end
end

function onClear(slot_data)
    if Tracker.BulkUpdate ~= nil then
        Tracker.BulkUpdate = true
    end
    CUR_INDEX = -1
    reset_items()
    reset_locations()
    apply_slot_data(slot_data)
    sync_recall_rings_from_progressive()
    sync_crystals_total()
    if Tracker.BulkUpdate ~= nil then
        Tracker.BulkUpdate = false
    end
end

function onItem(index, item_id, item_name, player_number)
    if index <= CUR_INDEX then
        return
    end
    CUR_INDEX = index

    local mapping = ITEM_MAPPING[item_id]
    if mapping == nil then
        return
    end

    local code = mapping[1]
    local kind = mapping[2]
    local obj = Tracker:FindObjectForCode(code)
    if obj == nil then
        return
    end

    if kind == "toggle" then
        obj.Active = true
    elseif kind == "consumable" then
        obj.AcquiredCount = obj.AcquiredCount + (obj.Increment or 1)
    elseif kind == "progressive" then
        local max_stage = (code == "progressive_walk_speed") and 4 or 3
        local current = obj.CurrentStage or 0
        if current < max_stage then
            obj.CurrentStage = current + 1
        end
    end

    if code == "progressive_waypoint_recall" then
        sync_recall_rings_from_progressive()
    elseif string.sub(code, 1, 8) == "crystal_" then
        sync_crystals_total()
    end
end

function onLocation(location_id, location_name)
    local paths = LOCATION_MAPPING[location_id]
    if paths == nil then
        return
    end
    for _, path in ipairs(paths) do
        local obj = Tracker:FindObjectForCode(path)
        if obj ~= nil then
            if path:sub(1, 1) == "@" then
                if obj.AvailableChestCount > 0 then
                    obj.AvailableChestCount = obj.AvailableChestCount - 1
                end
            else
                obj.Active = true
            end
        end
    end
end

function onScout(location_id, location_name, item_id, item_name, item_player)
end

function onBounce(json)
end

Archipelago:AddClearHandler("the_longing_clear", onClear)
Archipelago:AddItemHandler("the_longing_item", onItem)
Archipelago:AddLocationHandler("the_longing_location", onLocation)
if Archipelago.AddScoutHandler then
    Archipelago:AddScoutHandler("the_longing_scout", onScout)
end
if Archipelago.AddBouncedHandler then
    Archipelago:AddBouncedHandler("the_longing_bounce", onBounce)
end
'''


def build_init_lua() -> str:
    return '''-- Entry point for The Longing Decrypted PopTracker pack.

local variant = Tracker.ActiveVariantUID or "standard"
local items_only = string.find(variant, "itemsonly") ~= nil

-- Load items & seed options
Tracker:AddItems("items/items.json")
Tracker:AddItems("items/settings.json")

-- Load mappings and logic before maps & locations so visibility/access rules resolve cleanly
ScriptHost:LoadScript("scripts/autotracking/location_mapping.lua")
ScriptHost:LoadScript("scripts/logic/logic.lua")

-- Load Cavern Map & locations for full map variant
if not items_only then
    Tracker:AddMaps("maps/maps.json")
    Tracker:AddLocations("locations/locations.json")
end

-- Load layouts
Tracker:AddLayouts("layouts/items.json")
Tracker:AddLayouts("layouts/settings.json")
Tracker:AddLayouts("layouts/tracker.json")
Tracker:AddLayouts("layouts/broadcast.json")

-- Watchers to keep recall rings and total crystal counter synced in both manual and AP modes
ScriptHost:AddWatchForCode("sync_recall_rings", "progressive_waypoint_recall", sync_recall_rings_from_progressive)
ScriptHost:AddWatchForCode("sync_ring_1", "recall_ring_1", function() sync_progressive_from_recall_ring("recall_ring_1") end)
ScriptHost:AddWatchForCode("sync_ring_2", "recall_ring_2", function() sync_progressive_from_recall_ring("recall_ring_2") end)
ScriptHost:AddWatchForCode("sync_ring_3", "recall_ring_3", function() sync_progressive_from_recall_ring("recall_ring_3") end)

for _, crystal_code in ipairs({
    "crystal_white",
    "crystal_rose",
    "crystal_blue",
    "crystal_purple",
    "crystal_black",
    "crystal_great_hall",
    "crystal_tower",
}) do
    ScriptHost:AddWatchForCode("sync_" .. crystal_code, crystal_code, sync_crystals_total)
end

-- Enable Archipelago autotracking when supported by PopTracker
if PopVersion and PopVersion >= "0.18.0" then
    ScriptHost:LoadScript("scripts/autotracking/archipelago.lua")
end
'''


def write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def write_apworld_tracker_mapping(
    pop_path_to_id: dict[str, int],
    recall_ring_scenes: dict[int, list[str]],
) -> None:
    lines = [
        '"""Auto-generated Universal Tracker & PopTracker map alignment data.',
        "",
        "Generated by tools/build_tracker_pack.py from data/world_catalog.json.",
        '"""',
        "from __future__ import annotations",
        "",
        "POPTRACKER_LOCATION_MAPPING: dict[str, int] = {",
    ]
    for path, loc_id in pop_path_to_id.items():
        lines.append(f"    {path!r}: {loc_id},")
    lines.append("}")
    lines.append("")
    lines.append("RECALL_RING_SCENES: dict[int, tuple[str, ...]] = {")
    for ring in (1, 2, 3):
        lines.append(f"    {ring}: {tuple(recall_ring_scenes[ring])!r},")
    lines.append("}")
    lines.append("")
    write_text(APWORLD_MAPPING_PY, "\n".join(lines))


def sync_apworld_tracker_assets() -> None:
    """Copy the minimal PopTracker map/location files needed by Universal Tracker into apworld/the_longing/tracker/."""
    for rel in (
        Path("maps") / "maps.json",
        Path("locations") / "locations.json",
        Path("images") / "maps" / "cavern_map.png",
    ):
        src = TRACKER_DIR / rel
        dst = APWORLD_TRACKER_DIR / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)


def package_tracker_zip(output: Path) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    files = sorted(p for p in TRACKER_DIR.rglob("*") if p.is_file())
    with ZipFile(output, "w", ZIP_DEFLATED) as archive:
        for file_path in files:
            rel = file_path.relative_to(TRACKER_DIR).as_posix()
            archive.write(file_path, rel)
    print(f"Packaged {output} ({len(files)} files)")


def generate_all(output_zip: Path | None = None) -> None:
    catalog = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    items, locations = sync_world_data.build_registries(catalog)
    recall_ring_scenes = build_recall_ring_scenes(catalog)

    # 1. Generate original artwork
    generate_cavern_map_image(TRACKER_DIR / "images" / "maps" / "cavern_map.png")
    generate_item_icons(TRACKER_DIR / "images" / "items")

    # 2. Generate PopTracker JSON definitions
    write_json(TRACKER_DIR / "manifest.json", build_manifest())
    write_json(TRACKER_DIR / "items" / "items.json", build_items_json())
    write_json(TRACKER_DIR / "items" / "settings.json", build_settings_json())
    write_json(TRACKER_DIR / "maps" / "maps.json", build_maps_json())

    pop_locations, id_to_pop_path, pop_path_to_id = build_locations_and_mappings(locations)
    write_json(TRACKER_DIR / "locations" / "locations.json", pop_locations)

    for rel_path, layout_data in build_layouts().items():
        write_json(TRACKER_DIR / rel_path, layout_data)

    # 3. Generate Lua scripts
    write_text(TRACKER_DIR / "scripts" / "init.lua", build_init_lua())
    write_text(TRACKER_DIR / "scripts" / "logic" / "logic.lua", build_logic_lua(recall_ring_scenes))
    write_text(TRACKER_DIR / "scripts" / "autotracking" / "item_mapping.lua", build_item_mapping_lua(items))
    write_text(
        TRACKER_DIR / "scripts" / "autotracking" / "location_mapping.lua",
        build_location_mapping_lua(locations, id_to_pop_path),
    )
    write_text(TRACKER_DIR / "scripts" / "autotracking" / "archipelago.lua", build_archipelago_lua())

    # 4. Sync Universal Tracker map assets & mapping into apworld/the_longing/
    write_apworld_tracker_mapping(pop_path_to_id, recall_ring_scenes)
    sync_apworld_tracker_assets()

    print(
        f"Generated PopTracker & UT pack in {TRACKER_DIR} "
        f"({len(items)} registry items, {len(locations)} locations, 7 discoveries, 3 recall rings)"
    )

    if output_zip is not None:
        package_tracker_zip(output_zip)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help="Optional output path for the packaged PopTracker .zip archive (e.g. dist/the_longing_poptracker.zip).",
    )
    args = parser.parse_args()
    generate_all(output_zip=args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
