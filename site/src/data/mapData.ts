import { type CatalogCheck, WORLD_CATALOG } from "./catalogData";

export type CheckCategory = "tools" | "books" | "crystals" | "comfort" | "endings";

export interface CavernRegion {
  id: string;
  name: string;
  depthLabel: string;
  x: number;
  y: number;
  labelDy?: number;
  summary: string;
  gates: string[];
  endingNotes: string[];
  categories: CheckCategory[];
}

export interface CavernPassage {
  from: string;
  to: string;
  gated?: boolean;
}

export const REQUIREMENT_LABELS: Readonly<Record<string, string>> = {
  mattock_1: "Mattock x1",
  mattock_2: "Mattock x2",
  giant_spores: "Giant Mushroom Spores",
  moss_10: "Cavern Moss x10",
  sulfur: "Sulfur",
  instrument_3: "Musical Instrument Part x3",
  paper: "Drawing Paper",
};

export const CAVERN_REGIONS: readonly CavernRegion[] = [
  {
    id: "surface_well",
    name: "The Well & Surface",
    depthLabel: "Upper Tunnels",
    x: 410,
    y: 64,
    summary:
      "The long winding climb beneath the well shaft (`brunnen` -> `oberwelt`). Waiting by the bucket can draw the Old Man or the Troll.",
    gates: ["Surface Gate Pass", "Giant Mushroom Spores"],
    endingNotes: ["Escape to the Surface Ending"],
    categories: ["tools", "comfort", "endings"],
  },
  {
    id: "secret_tower",
    name: "Secret Tower & Darkness",
    depthLabel: "High Crevice",
    x: 378,
    y: 162,
    summary:
      "The hidden tower above the Great Cave (`tower`) holding 6 shelf books, a Mattock, the Tower Big Crystal, and the Secret Key.",
    gates: ["Surface Gate Pass", "Giant Mushroom Spores"],
    endingNotes: ["Secret Ending (requires Secret Key)"],
    categories: ["tools", "books", "crystals", "endings"],
  },
  {
    id: "library",
    name: "The Library",
    depthLabel: "West Ruin",
    x: 212,
    y: 332,
    summary:
      "A three-story stone ruin (`library`) holding 19 classic literature volumes beyond the Library Vestibule door.",
    gates: ["Library Door Pass"],
    endingNotes: [],
    categories: ["books"],
  },
  {
    id: "halls_eternity",
    name: "Halls of Eternity",
    depthLabel: "Southwest Labyrinth",
    x: 118,
    y: 422,
    summary:
      "Sixteen labyrinth chambers leading to an endless white corridor (`halls_of_eternity`) with the Horse Head trophy, carpets, curtains, and Black Crystal.",
    gates: ["Labyrinth Traversal (Mattock x2 for Black Crystal)"],
    endingNotes: [],
    categories: ["crystals", "comfort"],
  },
  {
    id: "great_cave",
    name: "The Great Halls",
    depthLabel: "Central Crossroads",
    x: 398,
    y: 288,
    summary:
      "The arched east and west halls, Moss Cliff, Moss Grottos, Face Chamber, and Secret Hall (L05) behind the Mystery Door.",
    gates: ["Open Hub (Secret Key for Secret Hall L05; Mattock x1/x2 for White & Blue Crystals)"],
    endingNotes: [],
    categories: ["tools", "books", "crystals", "comfort"],
  },
  {
    id: "shades_home",
    name: "The Shade's Home",
    depthLabel: "Central Hearth",
    x: 396,
    y: 348,
    labelDy: 24,
    summary:
      "The Shade's private chamber (`home`). Burning coal and sulfur, building the bed, drawing, playing music, and digging side walls speed time.",
    gates: ["Starting Region (Mattock x1 for wall expansion & home mattock)"],
    endingNotes: ["Crystal & Book Hunt Goal Turn-In"],
    categories: ["tools", "comfort", "endings"],
  },
  {
    id: "mushroom_groves",
    name: "Great Cave & Mushroom Groves",
    depthLabel: "East Passages",
    x: 548,
    y: 350,
    labelDy: -16,
    summary:
      "The vast upper-east cavern (`exit` & `greatCave_complete`) with 58 mushroom grow spots, Spider Flower, Purple Crystal, Lapis, and fuel.",
    gates: ["Surface Gate Pass (Giant Mushroom Spores for trampoline climb)"],
    endingNotes: [],
    categories: ["tools", "crystals", "comfort"],
  },
  {
    id: "crystal_cavern",
    name: "Mines & Sulfur Veins",
    depthLabel: "Far East Grotto",
    x: 696,
    y: 338,
    labelDy: 28,
    summary:
      "The timbered mining tunnels (`mine_01`, `mine_03`, `mine_04`) rich in timber, yellow sulfur deposits, and a deep coal seam.",
    gates: ["Surface Gate Pass", "Mattock x1 (for Sulfur & Coal veins)"],
    endingNotes: [],
    categories: ["comfort"],
  },
  {
    id: "kings_hall",
    name: "The King's Hall",
    depthLabel: "Deepest Throne",
    x: 356,
    y: 484,
    labelDy: 48,
    summary:
      "The sleeping King's throne, King's Stairs, Entrance Halls, Treasury, and sealed Mystery Rooms (`king` through `entrancehall3`).",
    gates: ["Open Hub (Secret Key for Mystery Rooms; Mattock x2 for Rose Crystals)"],
    endingNotes: ["Wake the Sleeping King Ending"],
    categories: ["tools", "books", "crystals", "comfort", "endings"],
  },
];

export const CAVERN_PASSAGES: readonly CavernPassage[] = [
  { from: "kings_hall", to: "shades_home" },
  { from: "shades_home", to: "great_cave" },
  { from: "great_cave", to: "library", gated: true },
  { from: "great_cave", to: "halls_eternity" },
  { from: "great_cave", to: "mushroom_groves", gated: true },
  { from: "mushroom_groves", to: "crystal_cavern", gated: true },
  { from: "mushroom_groves", to: "secret_tower", gated: true },
  { from: "secret_tower", to: "surface_well", gated: true },
];

export function getRegionChecks(
  regionId: string,
  filter: "all" | CheckCategory = "all",
): readonly CatalogCheck[] {
  return WORLD_CATALOG.checks.filter(
    (c) => c.regionId === regionId && (filter === "all" || c.mapCategory === filter),
  );
}

export function formatCheckRequirements(reqs: readonly string[]): string {
  if (reqs.length === 0) {
    return "No extra items";
  }
  return reqs.map((r) => REQUIREMENT_LABELS[r] ?? r).join(" + ");
}
