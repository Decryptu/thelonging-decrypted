"""Locate the Shade's head on every animation frame and write mod/Data/head_anchors.csv.

Hats cannot simply follow the head object: the game only uses it while the Shade stands or walks and draws the head
into the body sprite everywhere else. This reads the frames from a local game install, finds the skull (the thickest
part of the silhouette) and the eyes, and stores where a hat has to sit. Frames with closed eyes take their tilt from
KEYS and everything in between is interpolated.

Each CSV row is: sprite, x, y, angle, scale, flags (1 mirrored, 2 seen from behind), alpha.
"""
from __future__ import annotations

import argparse
import math
import os
import re
from collections import OrderedDict
from multiprocessing import Pool
from pathlib import Path

import UnityPy

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GAME_DIR = Path(os.environ.get("LONGING_GAME_DIR", r"C:\Program Files (x86)\Steam\steamapps\common\The Longing"))
DEFAULT_OUTPUT = ROOT / "mod" / "Data" / "head_anchors.csv"

SKULL = 0.29  # Radius in world units at normal size.
LEFT, RIGHT = -1.0, 1.0

# Frames whose head faces away from the viewer whenever no eyes are visible.
BACK_FAMILIES = {
    "shade_door_in_", "shade_door_in_01_", "shade_inout_draw_", "shade_drawing_", "into_building_bett_",
    "building_bett_loop_", "outof_building_bett_", "shade_leiter_up+down_", "shade_leiter_down_",
    "shade_door_entrancehall_", "shade_door_oberwelt_", "into_bett_", "out of bett_01_",
}
BACK_RANGES = {"shade_netz_": (18, 39), "shade_netz_down_": (122, 143)}

# Frames drawn smaller on purpose, where a small skull is not a misread.
PERSPECTIVE = ("shade_door_out", "sessel", "shade_door_oberwelt", "bett", "out", "into_bett")

# Families where slits of half-closed eyes mislead the tilt, so only the keys count between the first and last one.
KEYS_ONLY = {"shade_idle_07_", "shade_idle_09_"}

# family -> {frame: (tilt in degrees, facing or None to keep what the eyes say)}
KEYS: dict[str, dict[int, tuple[float, float | None]]] = {
    "shade_idle_07_": {79: (5, None), 80: (-5, None), 84: (-5, None), 88: (-12, None), 92: (-25, None), 96: (-35, None), 102: (-38, None), 153: (-38, None)},
    "shade_idle_08_": {1: (-38, LEFT), 31: (-38, LEFT)},
    "shade_idle_09_": {1: (-38, None), 13: (-38, None), 17: (-7, None), 21: (0, None), 41: (0, None), 46: (0, None)},
    "shade_netz2_shade_": {54: (0, RIGHT), 58: (3, RIGHT), 60: (8, RIGHT), 63: (8, RIGHT), 74: (50, RIGHT), 84: (2, RIGHT), 98: (-8, RIGHT)},
    "shade_pilz_tripwake_": {1: (60, LEFT), 18: (60, LEFT), 21: (50, LEFT), 23: (34, LEFT), 26: (18, LEFT), 28: (0, LEFT)},
    "shade_pilz_intotrip_": {49: (-7, LEFT), 51: (-8, LEFT), 53: (-2, LEFT), 55: (5, LEFT), 57: (18, LEFT), 59: (40, LEFT), 61: (45, LEFT), 65: (60, LEFT), 67: (60, LEFT)},
    "bett_05_loop_": {1: (40, LEFT), 39: (40, LEFT)},
    "bett_05_": {4: (40, LEFT)},
    "out_of_bett_04_": {10: (40, LEFT), 27: (40, LEFT)},
    "sessel_schlafen_": {1: (8, LEFT), 30: (8, LEFT)},
    "shade_turn_eyesclosed_mid_": {1: (0, -0.4), 2: (0, -0.4), 3: (0, 0.02), 4: (0, 0.02), 5: (0, 0.75), 6: (0, 0.75)},
}

# Walking and standing bodies have no head of their own.
HEADLESS = re.compile(r"^shade_(stand|walk_start|walk|walk_single|walk_double)_(pilz0?\d?_|pilz2?_|pilz_)?\d+$|^shade_stand_pilz0\d_\d+$")


def load_frames(game_dir: Path) -> list[dict]:
    env = UnityPy.load(str(game_dir / "The Longing_Data" / "data.unity3d"))
    frames: OrderedDict[str, dict] = OrderedDict()

    def add(sprite) -> None:
        name = sprite.m_Name
        if name in frames or name.startswith(("doubleclick", "papier")):
            return
        offset = sprite.m_RD.textureRectOffset
        frames[name] = {
            "name": name, "image": sprite.image.convert("RGBA"), "w": sprite.m_Rect.width, "h": sprite.m_Rect.height,
            "px": sprite.m_Pivot.x, "py": sprite.m_Pivot.y, "ppu": sprite.m_PixelsToUnits, "off": (offset.x, offset.y),
        }

    for obj in env.objects:
        if obj.assets_file.name != "sharedassets1.assets":
            continue
        if obj.type.name == "AnimatorController":
            for clip in obj.read().m_AnimationClips:
                for pointer in clip.read().m_ClipBindingConstant.pptrCurveMapping:
                    try:
                        add(pointer.read())
                    except Exception:  # noqa: BLE001 - clips may point at stripped objects
                        continue
        elif obj.type.name == "Sprite" and obj.peek_name().startswith("shade_head"):
            add(obj.read())
    return list(frames.values())


def analyse(frame: dict) -> tuple[str, dict | None]:
    image = frame["image"]
    width, height = image.size
    if width < 8 or height < 8:
        return frame["name"], None
    pixels = image.load()
    scale = frame["ppu"] / 100.0

    solid = [[0] * (width + 2) for _ in range(height + 2)]
    yellow = [[0] * width for _ in range(height)]
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a < 60:
                continue
            high, low = max(r, g, b), min(r, g, b)
            if r > 120 and g > 110 and b < min(r, g) * 0.75:
                yellow[y][x] = 1
            eye = yellow[y][x] or low > 140 or (abs(r - g) <= max(14, high * 0.16) and b < 0.8 * min(r, g))
            if eye or (high < 85 and high - low <= 9):
                solid[y + 1][x + 1] = 1

    _fill_holes(solid, width, height, 70 * scale * scale)
    distance = _distance(solid, width, height)
    best = max(max(row) for row in distance)
    near = [(x, y, distance[y][x]) for y in range(1, height + 1) for x in range(1, width + 1) if distance[y][x] >= best - 3]
    peak_x, peak_y, _ = max(near, key=lambda p: p[2])
    near = [p for p in near if abs(p[0] - peak_x) < 8 * scale and abs(p[1] - peak_y) < 8 * scale]
    weight = sum(p[2] - best + 4 for p in near)
    cx = sum(p[0] * (p[2] - best + 4) for p in near) / weight - 0.5
    cy = sum(p[1] * (p[2] - best + 4) for p in near) / weight - 0.5
    radius = best / 3.0

    eyes = []
    for part in _parts(yellow, width, height):
        xs, ys = [p[0] for p in part], [p[1] for p in part]
        ex, ey = (min(xs) + max(xs) + 1) / 2, (min(ys) + max(ys) + 1) / 2
        if len(part) >= 14 * scale * scale and math.hypot(ex - cx, ey - cy) < radius * 1.7:
            eyes.append((len(part), ex, ey, max(xs) - min(xs) + 1, max(ys) - min(ys) + 1))
    eyes.sort(reverse=True)

    inside = sorted(
        pixels[x, y][3]
        for x in range(max(0, int(cx - radius)), min(width, int(cx + radius) + 1))
        for y in range(max(0, int(cy - radius)), min(height, int(cy + radius) + 1))
        if (x - cx) ** 2 + (y - cy) ** 2 < radius * radius * 0.6
    )
    left, bottom, ppu = frame["off"][0] - frame["px"] * frame["w"], frame["off"][1] - frame["py"] * frame["h"], frame["ppu"]
    return frame["name"], {
        "x": (left + cx) / ppu, "y": (bottom + height - cy) / ppu, "r": radius / ppu,
        "alpha": inside[len(inside) * 3 // 4] if inside else 255,
        "eyes": [((left + e[1]) / ppu, (bottom + height - e[2]) / ppu, e[3] / ppu, e[4] / ppu) for e in eyes[:2]],
    }


def _parts(mask, width, height):
    seen = [[0] * width for _ in range(height)]
    for y in range(height):
        for x in range(width):
            if not mask[y][x] or seen[y][x]:
                continue
            stack, part = [(x, y)], []
            seen[y][x] = 1
            while stack:
                a, b = stack.pop()
                part.append((a, b))
                for u in (a - 1, a, a + 1):
                    for v in (b - 1, b, b + 1):
                        if 0 <= u < width and 0 <= v < height and mask[v][u] and not seen[v][u]:
                            seen[v][u] = 1
                            stack.append((u, v))
            yield part


def _fill_holes(solid, width, height, largest) -> None:
    seen = [[0] * (width + 2) for _ in range(height + 2)]
    for y in range(height + 2):
        for x in range(width + 2):
            if solid[y][x] or seen[y][x]:
                continue
            stack, part, open_edge = [(x, y)], [], False
            seen[y][x] = 1
            while stack:
                a, b = stack.pop()
                part.append((a, b))
                open_edge |= a in (0, width + 1) or b in (0, height + 1)
                for u, v in ((a + 1, b), (a - 1, b), (a, b + 1), (a, b - 1)):
                    if 0 <= u < width + 2 and 0 <= v < height + 2 and not solid[v][u] and not seen[v][u]:
                        seen[v][u] = 1
                        stack.append((u, v))
            if not open_edge and len(part) <= largest:
                for a, b in part:
                    solid[b][a] = 1


def _distance(solid, width, height):
    far = 9999
    d = [[far if cell else 0 for cell in row] for row in solid]
    for y in range(1, height + 1):
        for x in range(1, width + 1):
            if d[y][x]:
                d[y][x] = min(d[y][x - 1] + 3, d[y - 1][x] + 3, d[y - 1][x - 1] + 4, d[y - 1][x + 1] + 4)
    for y in range(height, 0, -1):
        for x in range(width, 0, -1):
            if d[y][x]:
                d[y][x] = min(d[y][x], d[y][x + 1] + 3, d[y + 1][x] + 3, d[y + 1][x - 1] + 4, d[y + 1][x + 1] + 4)
    return d


def _wrap(angle: float) -> float:
    return (angle + 540) % 360 - 180


def _number(name: str, family: str) -> int:
    digits = name[len(family):]
    return int(digits) if digits else 0


def anchors(poses: dict[str, dict | None]) -> dict[str, tuple]:
    families: OrderedDict[str, list[str]] = OrderedDict()
    for name in poses:
        if not HEADLESS.match(name):
            families.setdefault(re.sub(r"\d+$", "", name), []).append(name)

    result: dict[str, tuple] = {}
    for family, names in families.items():
        names.sort(key=lambda n: _number(n, family))
        keys = KEYS.get(family, {})
        rows = [_pose(name, family, poses[name], keys) for name in names]
        for field, default in (("x", 0.0), ("y", 0.0), ("r", SKULL), ("yaw", LEFT), ("rot", 0.0)):
            _interpolate(rows, field, default, wrap=family not in KEYS_ONLY)
        tilts = [row["rot"] for row in rows]
        for i in range(1, len(rows) - 1):
            window = sorted(tilts[i - 1:i + 2])
            if window[2] - window[0] < 25:
                rows[i]["rot"] = window[1]
        for row in rows:
            result[row["name"]] = _place(row)
    return result


def _pose(name: str, family: str, pose: dict | None, keys: dict) -> dict:
    frame = _number(name, family)
    row = {"name": name, "frame": frame, "x": None, "y": None, "r": None, "rot": None, "yaw": None, "back": 0, "alpha": 255}
    if pose:
        row["alpha"] = pose["alpha"]
        eyes = sorted(pose["eyes"])
        if pose["r"] >= (0.2 if family.startswith(PERSPECTIVE) else 0.255):
            row.update(x=pose["x"], y=pose["y"], r=pose["r"])
        behind = family in BACK_RANGES and BACK_RANGES[family][0] <= frame <= BACK_RANGES[family][1]
        if behind or (family in BACK_FAMILIES and not eyes):
            row.update(rot=0.0, yaw=0.0, back=1)
        elif pose["r"] >= 0.2 and len(eyes) == 2 and min(min(e[2], e[3]) for e in eyes) > 0.07:
            # Three-quarter or front view: the line through both eyes gives the tilt, its middle the facing.
            tilt = math.degrees(math.atan2(eyes[1][1] - eyes[0][1], eyes[1][0] - eyes[0][0]))
            c, s = math.cos(math.radians(-tilt)), math.sin(math.radians(-tilt))
            mx = ((eyes[0][0] + eyes[1][0]) / 2 - pose["x"]) / pose["r"]
            my = ((eyes[0][1] + eyes[1][1]) / 2 - pose["y"]) / pose["r"]
            yaw = max(-1.0, min(1.0, (mx * c - my * s - 0.02) / 0.39))
            row.update(yaw=yaw, rot=tilt + 3 if yaw < 0.05 else tilt - 3)
        elif pose["r"] >= 0.2 and len(eyes) == 1:
            # Profile: the single eye points where the head looks.
            ex, ey = (eyes[0][0] - pose["x"]) / pose["r"], (eyes[0][1] - pose["y"]) / pose["r"]
            if math.hypot(ex, ey) > 0.45 and eyes[0][3] > 0.1:
                angle = math.degrees(math.atan2(ey, ex))
                row.update(yaw=RIGHT, rot=_wrap(angle - 8)) if ex > 0.25 else row.update(yaw=LEFT, rot=_wrap(angle - 180 + 8))
    if frame in keys:
        tilt, facing = keys[frame]
        row["rot"] = float(tilt)
        if facing is not None:
            row["yaw"] = facing
        elif row["yaw"] is None:
            row["yaw"] = LEFT
    elif family in KEYS_ONLY and min(keys) <= frame <= max(keys):
        row["rot"] = row["yaw"] = None
    return row


def _interpolate(rows: list[dict], field: str, default: float, wrap: bool) -> None:
    known = [i for i, row in enumerate(rows) if row[field] is not None]
    for i, row in enumerate(rows):
        if row[field] is not None:
            continue
        before = max((k for k in known if k < i), default=None)
        after = min((k for k in known if k > i), default=None)
        if before is None and after is None:
            row[field] = default
        elif before is None or after is None:
            row[field] = rows[after if before is None else before][field]
        else:
            a, b = rows[before][field], rows[after][field]
            t = (row["frame"] - rows[before]["frame"]) / max(1, rows[after]["frame"] - rows[before]["frame"])
            row[field] = a + (_wrap(b - a) if field == "rot" and wrap else b - a) * t


def _place(row: dict) -> tuple:
    """Turn a head pose into the hat's pivot. Hats are drawn for the left-facing three-quarter head."""
    mirrored = row["yaw"] > 0.02
    turned = min(1.0, abs(row["yaw"]))
    side = -1 if mirrored else 1
    scale = min(1.06, row["r"] / SKULL)
    ox, oy = 0.09 * turned * side, 0.135 + 0.03 * (1 - turned)
    angle = row["rot"] + 11 * (1 - turned) * side
    c, s = math.cos(math.radians(row["rot"])), math.sin(math.radians(row["rot"]))
    x = row["x"] + scale * (ox * c - oy * s)
    y = row["y"] + scale * (ox * s + oy * c)
    return round(x, 3), round(y, 3), round(_wrap(angle), 1), round(scale, 2), int(mirrored) + 2 * row["back"], row["alpha"]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--game-dir", type=Path, default=DEFAULT_GAME_DIR)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()

    frames = load_frames(args.game_dir)
    with Pool() as pool:
        poses = dict(pool.map(analyse, frames, chunksize=32))
    table = anchors(poses)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8", newline="\n") as handle:
        for name in sorted(table):
            handle.write(name + "," + ",".join(f"{value:g}" for value in table[name]) + "\n")
    print(f"Wrote {len(table)} head poses to {args.output}")


if __name__ == "__main__":
    main()
