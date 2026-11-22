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
  checks: string[];
  categories: CheckCategory[];
}

export interface CavernPassage {
  from: string;
  to: string;
  gated?: boolean;
}

export const CAVERN_REGIONS: readonly CavernRegion[] = [
  {
    id: "surface_well",
    name: "The Well & Surface",
    depthLabel: "Upper Tunnels",
    x: 410,
    y: 64,
    summary:
      "The long winding climb beneath the well shaft. Waiting by the bucket can draw the Old Man or the Troll.",
    gates: ["Upper Cavern Climb", "Darkness Crossing"],
    checks: ["Escape to the Surface Ending", "Well Bucket Encounter", "Upper Shaft Overlook"],
    categories: ["endings"],
  },
  {
    id: "secret_tower",
    name: "Secret Tower & Darkness",
    depthLabel: "High Crevice",
    x: 378,
    y: 162,
    summary:
      "A dark ridge above the spiral stair where the Shade must close his eyes to face fear and reach the hidden tower.",
    gates: ["Closed Eyes / Fear Threshold", "Purple Mushroom Growth"],
    checks: ["Tower Summit Keepsakes", "Cliffside Moss Ledge", "Secret Ending Trigger"],
    categories: ["tools", "comfort", "endings"],
  },
  {
    id: "library",
    name: "The Library",
    depthLabel: "West Ruin",
    x: 212,
    y: 332,
    summary:
      "A three-story stone ruin stacked with classic literature: Nietzsche, Poe, Melville, and tales that enrich the Shade's home.",
    gates: ["Library Door Key or Wait Timer"],
    checks: [
      "Shelf Volumes (20+ planned book checks)",
      "Library Reading Alcove",
      "Hidden Bookmark Notes",
    ],
    categories: ["books", "comfort"],
  },
  {
    id: "halls_eternity",
    name: "Halls of Eternity",
    depthLabel: "Southwest Labyrinth",
    x: 118,
    y: 422,
    summary:
      "An endless stone corridor beyond the labyrinth where the 400-day clock stands still and books, curtains, and paintings appear.",
    gates: ["Labyrinth Entrance"],
    checks: [
      "Endless Books (12+ planned checks)",
      "Eternal Paintings & Curtains",
      "Carpet & Comfort Relics",
    ],
    categories: ["books", "comfort"],
  },
  {
    id: "great_cave",
    name: "The Great Halls",
    depthLabel: "Central Crossroads",
    x: 398,
    y: 288,
    summary:
      "The arched stone corridors linking the upper spiral stair, the library, the moss cliffs, the spider web, and sealed cavern doors.",
    gates: ["None (Hub)", "Mattock for Weak Walls"],
    checks: [
      "Moss & Mushroom Patches",
      "Spider Web Crossing",
      "Breakable Cavern Alcoves",
      "Musical Instrument Parts",
    ],
    categories: ["tools", "comfort"],
  },
  {
    id: "shades_home",
    name: "The Shade's Home",
    depthLabel: "Central Hearth",
    x: 396,
    y: 348,
    labelDy: 24,
    summary:
      "The Shade's private cavern room. Burning coal, reading, drawing, hanging crystals, and playing music make time move faster.",
    gates: ["Starting Region"],
    checks: [
      "Hearth & Sulfur Fire Milestones",
      "Drawing Paper & Charcoal Sketches",
      "Bed Construction & Chair Comfort",
      "Home Wall Excavations",
    ],
    categories: ["comfort", "books", "crystals"],
  },
  {
    id: "mushroom_groves",
    name: "Sulfur & Mushroom Groves",
    depthLabel: "East Passages",
    x: 548,
    y: 350,
    labelDy: -16,
    summary:
      "Damp eastern tunnels where green and purple mushrooms can be harvested, carried, and planted to reach high ledges.",
    gates: ["Green Mushroom Seed"],
    checks: ["Wild Mushroom Harvests", "Planted Giant Mushroom Ledge", "Coal & Sulfur Piles"],
    categories: ["tools", "comfort"],
  },
  {
    id: "crystal_cavern",
    name: "Crystal Cavern & Waterfall",
    depthLabel: "Far East Grotto",
    x: 696,
    y: 338,
    labelDy: 28,
    summary:
      "Glittering seams of blue, rose, purple, and dark crystals beside the underground waterfall, pried loose with sturdy mattocks.",
    gates: ["Mattock (Tier 1 / 2)", "Dripstone Bridge"],
    checks: [
      "Blue & Rose Crystal Veins",
      "Lapis Lazuli Deposit",
      "Dark Crystal Shard",
      "Minecart & Sulfur Lumps",
    ],
    categories: ["tools", "crystals"],
  },
  {
    id: "kings_hall",
    name: "The King's Hall",
    depthLabel: "Deepest Throne",
    x: 356,
    y: 484,
    labelDy: 48,
    summary:
      "The deepest cavern chamber where the ancient stone King sleeps for 400 days until the Shade returns to wake him.",
    gates: ["Countdown Reaches Zero (or Goal Unlock)"],
    checks: ["King's Stairs Coal & Relics", "Wake the King Ending", "Throne Chamber Inspection"],
    categories: ["endings", "comfort"],
  },
];

export const CAVERN_PASSAGES: readonly CavernPassage[] = [
  { from: "kings_hall", to: "shades_home" },
  { from: "shades_home", to: "great_cave" },
  { from: "great_cave", to: "library", gated: true },
  { from: "library", to: "halls_eternity" },
  { from: "shades_home", to: "mushroom_groves" },
  { from: "mushroom_groves", to: "crystal_cavern", gated: true },
  { from: "great_cave", to: "crystal_cavern", gated: true },
  { from: "great_cave", to: "secret_tower", gated: true },
  { from: "secret_tower", to: "surface_well", gated: true },
];
