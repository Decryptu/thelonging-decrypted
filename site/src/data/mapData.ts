export type CheckCategory = "tools" | "books" | "crystals" | "comfort" | "endings";

export interface CavernRegion {
  id: string;
  name: string;
  depthLabel: string;
  x: number;
  y: number;
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
    depthLabel: "0 m · Upper Crust",
    x: 470,
    y: 66,
    summary:
      "The narrow shaft leading out of the Underground Kingdom. Waiting by the bucket can draw the Old Man or the Troll.",
    gates: ["Upper Cavern Climb", "Darkness / Cliff Crossing"],
    checks: ["Escape to the Surface Ending", "Well Bucket Encounter", "Upper Shaft Overlook"],
    categories: ["endings"],
  },
  {
    id: "secret_tower",
    name: "Secret Tower & Darkness",
    depthLabel: "90 m · High Crevice",
    x: 245,
    y: 126,
    summary:
      "A pitch-black stretch beyond the cliff where the Shade must close his eyes to conquer fear and reach the hidden tower.",
    gates: ["Closed Eyes / Fear Threshold", "Purple Mushroom Growth"],
    checks: ["Tower Summit Keepsakes", "Cliffside Moss Ledge", "Secret Ending Trigger"],
    categories: ["tools", "comfort", "endings"],
  },
  {
    id: "halls_eternity",
    name: "Halls of Eternity",
    depthLabel: "180 m · Timeless Corridor",
    x: 680,
    y: 158,
    summary:
      "An infinite gallery where the 400-day clock stands still and scattered books, curtains, and paintings appear along the walk.",
    gates: ["Halls Entrance Passage"],
    checks: [
      "Endless Books (12+ planned checks)",
      "Eternal Paintings & Curtains",
      "Carpet & Comfort Relics",
    ],
    categories: ["books", "comfort"],
  },
  {
    id: "library",
    name: "The Library",
    depthLabel: "220 m · Archive",
    x: 635,
    y: 256,
    summary:
      "Shelves stacked with classic literature — Nietzsche, Poe, Melville, and fairy tales that enrich the Shade's home.",
    gates: ["Library Door Key or Wait Timer"],
    checks: [
      "Shelf Volumes (20+ planned book checks)",
      "Library Reading Alcove",
      "Hidden Bookmark Notes",
    ],
    categories: ["books", "comfort"],
  },
  {
    id: "great_cave",
    name: "The Great Cave",
    depthLabel: "270 m · Central Crossroads",
    x: 410,
    y: 248,
    summary:
      "The vast hub linking the upper climbs, moss cliffs, mushroom beds, spider web, and sealed subterranean doors.",
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
    id: "crystal_cavern",
    name: "Crystal Cavern & Mines",
    depthLabel: "320 m · Deep Veins",
    x: 165,
    y: 282,
    summary:
      "Glittering seams of blue, rose, purple, dark crystals, and lapis lazuli waiting to be pried loose with sturdy mattocks.",
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
    id: "mushroom_groves",
    name: "Sulfur & Mushroom Groves",
    depthLabel: "335 m · Damp Tunnels",
    x: 270,
    y: 372,
    summary:
      "Damp lower galleries where green and purple mushrooms can be harvested, carried, and planted to reach high ledges.",
    gates: ["Green Mushroom Seed"],
    checks: ["Wild Mushroom Harvests", "Planted Giant Mushroom Ledge", "Coal & Sulfur Piles"],
    categories: ["tools", "comfort"],
  },
  {
    id: "shades_home",
    name: "The Shade's Home",
    depthLabel: "375 m · Hearth & Shelf",
    x: 445,
    y: 368,
    summary:
      "The Shade's private cavern room. Lighting coal, reading, drawing, hanging crystals, and playing music accelerate time itself.",
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
    id: "kings_hall",
    name: "The King's Hall",
    depthLabel: "440 m · Throne Chamber",
    x: 615,
    y: 410,
    summary:
      "The deepest chamber where the ancient stone King sleeps for 400 days until the Shade returns to wake him.",
    gates: ["Countdown Reaches Zero (or Goal Unlock)"],
    checks: ["King's Stairs Coal & Relics", "Wake the King Ending", "Throne Chamber Inspection"],
    categories: ["endings", "comfort"],
  },
];

export const CAVERN_PASSAGES: readonly CavernPassage[] = [
  { from: "kings_hall", to: "shades_home" },
  { from: "shades_home", to: "great_cave" },
  { from: "shades_home", to: "mushroom_groves" },
  { from: "mushroom_groves", to: "crystal_cavern" },
  { from: "great_cave", to: "crystal_cavern", gated: true },
  { from: "great_cave", to: "library", gated: true },
  { from: "great_cave", to: "halls_eternity" },
  { from: "library", to: "halls_eternity" },
  { from: "great_cave", to: "secret_tower", gated: true },
  { from: "secret_tower", to: "surface_well", gated: true },
  { from: "great_cave", to: "surface_well", gated: true },
];
