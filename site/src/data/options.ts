export type GoalOption =
  | "wake_the_king"
  | "escape_surface"
  | "secret_ending"
  | "crystal_book_hunt"
  | "any_ending";

export type CountdownLength = "1_hour" | "6_hours" | "24_hours" | "40_days" | "400_days";

export type WalkSpeed = "vanilla" | "brisk" | "swift" | "hasty";

export type ShuffleDepth = "vanilla" | "progression" | "full";

export type TrapDensity = "none" | "light" | "moderate" | "heavy";

export interface LongingYamlConfig {
  playerName: string;
  goal: GoalOption;
  countdownLength: CountdownLength;
  startingTimeRate: number;
  walkSpeed: WalkSpeed;
  timeSkipItems: boolean;
  shuffleMattocksAndKeys: boolean;
  shuffleBooks: ShuffleDepth;
  shuffleCrystals: ShuffleDepth;
  shuffleMushroomsAndMoss: boolean;
  shuffleHomeComfort: boolean;
  shuffleInstrumentParts: boolean;
  shuffleCoalAndSulfur: boolean;
  trapDensity: TrapDensity;
  deathLink: boolean;
}

export interface PresetDefinition {
  id: string;
  label: string;
  summary: string;
  config: Omit<LongingYamlConfig, "playerName">;
}

export const DEFAULT_CONFIG: LongingYamlConfig = {
  playerName: "Shade",
  goal: "wake_the_king",
  countdownLength: "6_hours",
  startingTimeRate: 12,
  walkSpeed: "brisk",
  timeSkipItems: true,
  shuffleMattocksAndKeys: true,
  shuffleBooks: "progression",
  shuffleCrystals: "full",
  shuffleMushroomsAndMoss: true,
  shuffleHomeComfort: true,
  shuffleInstrumentParts: true,
  shuffleCoalAndSulfur: false,
  trapDensity: "light",
  deathLink: false,
};

export const PRESETS: readonly PresetDefinition[] = [
  {
    id: "multiworld_evening",
    label: "Multiworld Evening",
    summary: "6-hour clock, time-skip items, brisk Shade, core checks shuffled.",
    config: {
      goal: "wake_the_king",
      countdownLength: "6_hours",
      startingTimeRate: 12,
      walkSpeed: "brisk",
      timeSkipItems: true,
      shuffleMattocksAndKeys: true,
      shuffleBooks: "progression",
      shuffleCrystals: "full",
      shuffleMushroomsAndMoss: true,
      shuffleHomeComfort: true,
      shuffleInstrumentParts: true,
      shuffleCoalAndSulfur: false,
      trapDensity: "light",
      deathLink: false,
    },
  },
  {
    id: "swift_escape",
    label: "Swift Escape",
    summary: "Short session aimed at climbing to the surface with swift walking.",
    config: {
      goal: "escape_surface",
      countdownLength: "1_hour",
      startingTimeRate: 24,
      walkSpeed: "swift",
      timeSkipItems: true,
      shuffleMattocksAndKeys: true,
      shuffleBooks: "vanilla",
      shuffleCrystals: "progression",
      shuffleMushroomsAndMoss: true,
      shuffleHomeComfort: true,
      shuffleInstrumentParts: false,
      shuffleCoalAndSulfur: false,
      trapDensity: "none",
      deathLink: false,
    },
  },
  {
    id: "full_cavern",
    label: "Full Cavern",
    summary: "Every book, crystal, mushroom, lump of coal, and comfort shuffled.",
    config: {
      goal: "crystal_book_hunt",
      countdownLength: "24_hours",
      startingTimeRate: 16,
      walkSpeed: "swift",
      timeSkipItems: true,
      shuffleMattocksAndKeys: true,
      shuffleBooks: "full",
      shuffleCrystals: "full",
      shuffleMushroomsAndMoss: true,
      shuffleHomeComfort: true,
      shuffleInstrumentParts: true,
      shuffleCoalAndSulfur: true,
      trapDensity: "moderate",
      deathLink: false,
    },
  },
  {
    id: "true_longing",
    label: "400 Days Async",
    summary: "Authentic 400-day countdown built for long-running async multiworlds.",
    config: {
      goal: "any_ending",
      countdownLength: "400_days",
      startingTimeRate: 1,
      walkSpeed: "vanilla",
      timeSkipItems: true,
      shuffleMattocksAndKeys: true,
      shuffleBooks: "full",
      shuffleCrystals: "full",
      shuffleMushroomsAndMoss: true,
      shuffleHomeComfort: true,
      shuffleInstrumentParts: true,
      shuffleCoalAndSulfur: true,
      trapDensity: "light",
      deathLink: false,
    },
  },
];

export interface ValidationResult {
  valid: boolean;
  errors: string[];
  warnings: string[];
}

export function validateConfig(config: LongingYamlConfig): ValidationResult {
  const errors: string[] = [];
  const warnings: string[] = [];
  const trimmed = config.playerName.trim();

  if (trimmed.length === 0) {
    errors.push("Player name is required.");
  } else if (trimmed.length > 16) {
    errors.push("Player name must be 16 characters or fewer.");
  } else if (/[{}]/.test(trimmed)) {
    errors.push("Player name cannot contain curly braces.");
  }

  if (config.startingTimeRate < 1 || config.startingTimeRate > 100) {
    errors.push("Starting time rate must stay between 1x and 100x.");
  }

  if (
    config.goal === "wake_the_king" &&
    config.countdownLength === "400_days" &&
    !config.timeSkipItems &&
    config.startingTimeRate < 10
  ) {
    warnings.push(
      "A 400-day clock without time-skip items will take months of real time to reach zero.",
    );
  }

  if (
    config.goal === "crystal_book_hunt" &&
    config.shuffleBooks === "vanilla" &&
    config.shuffleCrystals === "vanilla"
  ) {
    warnings.push(
      "Crystal & Book Hunt plays best when books or crystals are shuffled into the multiworld.",
    );
  }

  return {
    valid: errors.length === 0,
    errors,
    warnings,
  };
}

function yamlQuote(value: string): string {
  const cleaned = value.trim();
  if (/^[A-Za-z0-9 _-]+$/.test(cleaned) && !/^(true|false|null|yes|no|on|off)$/i.test(cleaned)) {
    return cleaned;
  }
  return `'${cleaned.replace(/'/g, "''")}'`;
}

export function serializeYaml(config: LongingYamlConfig): string {
  const name = config.playerName.trim() || "Shade";
  const lines = [
    "# Draft Archipelago player file for The Longing Decrypted",
    "# Option names will lock to the shipped APWorld schema at first release.",
    `name: ${yamlQuote(name)}`,
    "description: Generated with The Longing Decrypted player hub",
    "game: The Longing",
    "",
    "The Longing:",
    "  progression_balancing: 50",
    "  accessibility: full",
    "",
    "  # Goal and time pacing",
    `  goal: ${config.goal}`,
    `  countdown_length: ${config.countdownLength}`,
    `  starting_time_rate: ${Math.round(config.startingTimeRate)}`,
    `  time_skip_items: ${config.timeSkipItems ? "true" : "false"}`,
    `  walk_speed: ${config.walkSpeed}`,
    "",
    "  # Cavern check pools",
    `  shuffle_mattocks_and_keys: ${config.shuffleMattocksAndKeys ? "true" : "false"}`,
    `  shuffle_books: ${config.shuffleBooks}`,
    `  shuffle_crystals: ${config.shuffleCrystals}`,
    `  shuffle_mushrooms_and_moss: ${config.shuffleMushroomsAndMoss ? "true" : "false"}`,
    `  shuffle_home_comfort: ${config.shuffleHomeComfort ? "true" : "false"}`,
    `  shuffle_instrument_parts: ${config.shuffleInstrumentParts ? "true" : "false"}`,
    `  shuffle_coal_and_sulfur: ${config.shuffleCoalAndSulfur ? "true" : "false"}`,
    "",
    "  # Multiworld modifiers",
    `  trap_density: ${config.trapDensity}`,
    `  death_link: ${config.deathLink ? "true" : "false"}`,
    "",
  ];
  return lines.join("\n");
}

export function makeYamlFilename(playerName: string): string {
  const safe = playerName
    .trim()
    .replace(/[^A-Za-z0-9_-]+/g, "_")
    .replace(/^_+|_+$/g, "");
  return `${safe || "Shade"}_TheLonging.yaml`;
}
