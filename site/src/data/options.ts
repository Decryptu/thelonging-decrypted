import { WORLD_CATALOG } from "./catalogData";

export type GoalOption =
  | "wake_the_king"
  | "escape_surface"
  | "secret_ending"
  | "crystal_book_hunt"
  | "any_ending";

export type CountdownLength = "1_hour" | "6_hours" | "24_hours" | "40_days" | "400_days";

export type ShuffleDepth = "vanilla" | "progression" | "full";

export type TrapDensity = "none" | "light" | "moderate" | "heavy";

export interface LongingYamlConfig {
  playerName: string;
  goal: GoalOption;
  countdownLength: CountdownLength;
  startingTimeRate: number;
  timeSkipItems: boolean;
  shuffleMattocksAndKeys: boolean;
  shuffleBooks: ShuffleDepth;
  shuffleCrystals: ShuffleDepth;
  shuffleMushroomsAndMoss: boolean;
  shuffleHomeComfort: boolean;
  shuffleInstrumentParts: boolean;
  shuffleCoalAndSulfur: boolean;
  requiredCrystals: number;
  requiredBooks: number;
  trapDensity: TrapDensity;
  deathLink: boolean;
}

export interface PresetDefinition {
  id: string;
  label: string;
  summary: string;
  config: Omit<LongingYamlConfig, "playerName">;
}

export const APWORLD_SCHEMA_VERSION = WORLD_CATALOG.worldVersion;

export const DEFAULT_CONFIG: LongingYamlConfig = {
  playerName: "Shade",
  goal: "wake_the_king",
  countdownLength: "6_hours",
  startingTimeRate: 12,
  timeSkipItems: true,
  shuffleMattocksAndKeys: true,
  shuffleBooks: "progression",
  shuffleCrystals: "full",
  shuffleMushroomsAndMoss: true,
  shuffleHomeComfort: true,
  shuffleInstrumentParts: true,
  shuffleCoalAndSulfur: false,
  requiredCrystals: 8,
  requiredBooks: 12,
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
      timeSkipItems: true,
      shuffleMattocksAndKeys: true,
      shuffleBooks: "progression",
      shuffleCrystals: "full",
      shuffleMushroomsAndMoss: true,
      shuffleHomeComfort: true,
      shuffleInstrumentParts: true,
      shuffleCoalAndSulfur: false,
      requiredCrystals: 8,
      requiredBooks: 12,
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
      timeSkipItems: true,
      shuffleMattocksAndKeys: true,
      shuffleBooks: "vanilla",
      shuffleCrystals: "progression",
      shuffleMushroomsAndMoss: true,
      shuffleHomeComfort: true,
      shuffleInstrumentParts: false,
      shuffleCoalAndSulfur: false,
      requiredCrystals: 7,
      requiredBooks: 12,
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
      timeSkipItems: true,
      shuffleMattocksAndKeys: true,
      shuffleBooks: "full",
      shuffleCrystals: "full",
      shuffleMushroomsAndMoss: true,
      shuffleHomeComfort: true,
      shuffleInstrumentParts: true,
      shuffleCoalAndSulfur: true,
      requiredCrystals: 10,
      requiredBooks: 18,
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
      timeSkipItems: true,
      shuffleMattocksAndKeys: true,
      shuffleBooks: "full",
      shuffleCrystals: "full",
      shuffleMushroomsAndMoss: true,
      shuffleHomeComfort: true,
      shuffleInstrumentParts: true,
      shuffleCoalAndSulfur: true,
      requiredCrystals: 8,
      requiredBooks: 12,
      trapDensity: "light",
      deathLink: false,
    },
  },
];

export interface ValidationResult {
  valid: boolean;
  errors: string[];
  warnings: string[];
  enabledChecks: number;
}

export function countEnabledChecks(config: LongingYamlConfig): number {
  let count = 0;
  for (const check of WORLD_CATALOG.checks) {
    if (check.pool === "tools" && config.shuffleMattocksAndKeys) {
      count += 1;
    } else if (check.pool === "books") {
      if (
        config.shuffleBooks === "full" ||
        (config.shuffleBooks === "progression" && check.depth === "progression")
      ) {
        count += 1;
      }
    } else if (check.pool === "crystals") {
      if (
        config.shuffleCrystals === "full" ||
        (config.shuffleCrystals === "progression" && check.depth === "progression")
      ) {
        count += 1;
      }
    } else if (check.pool === "mushrooms_moss" && config.shuffleMushroomsAndMoss) {
      count += 1;
    } else if (check.pool === "comfort" && config.shuffleHomeComfort) {
      count += 1;
    } else if (check.pool === "instrument" && config.shuffleInstrumentParts) {
      count += 1;
    } else if (check.pool === "coal_sulfur" && config.shuffleCoalAndSulfur) {
      count += 1;
    }
  }
  const discoveries = WORLD_CATALOG.checks.filter((check) => check.pool === "mobility").length;
  return (count < 10 ? 12 : count) + discoveries;
}

export function validateConfig(config: LongingYamlConfig): ValidationResult {
  const errors: string[] = [];
  const warnings: string[] = [];
  const trimmed = config.playerName.trim();
  const enabledChecks = countEnabledChecks(config);

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

  if (config.requiredCrystals < 1 || config.requiredCrystals > 16) {
    errors.push("Required crystals must stay between 1 and 16.");
  }

  if (config.requiredBooks < 1 || config.requiredBooks > 30) {
    errors.push("Required books must stay between 1 and 30.");
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

  if (config.goal === "crystal_book_hunt") {
    const availCrystals =
      config.shuffleCrystals === "full" ? 16 : config.shuffleCrystals === "progression" ? 7 : 0;
    const availBooks =
      config.shuffleBooks === "full" ? 31 : config.shuffleBooks === "progression" ? 16 : 0;

    if (availCrystals === 0 && availBooks === 0) {
      warnings.push(
        "Crystal & Book Hunt plays best when books or crystals are shuffled into the multiworld.",
      );
    } else {
      if (availCrystals > 0 && config.requiredCrystals > availCrystals) {
        warnings.push(
          `Only ${availCrystals} crystals are shuffled; the APWorld will clamp required_crystals to ${availCrystals}.`,
        );
      }
      if (availBooks > 0 && config.requiredBooks > availBooks) {
        warnings.push(
          `Only ${availBooks} books are shuffled; the APWorld will clamp required_books to ${availBooks}.`,
        );
      }
    }
  }

  if (
    !config.shuffleMattocksAndKeys &&
    config.shuffleBooks === "vanilla" &&
    config.shuffleCrystals === "vanilla" &&
    !config.shuffleMushroomsAndMoss &&
    !config.shuffleHomeComfort &&
    !config.shuffleInstrumentParts &&
    !config.shuffleCoalAndSulfur
  ) {
    warnings.push(
      "All check pools are off; the APWorld will enable 12 fallback tool and comfort checks.",
    );
  }

  return {
    valid: errors.length === 0,
    errors,
    warnings,
    enabledChecks,
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
    `# Archipelago ${WORLD_CATALOG.archipelagoTarget} player file for The Longing Decrypted (v${APWORLD_SCHEMA_VERSION})`,
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
    `  required_crystals: ${Math.round(config.requiredCrystals)}`,
    `  required_books: ${Math.round(config.requiredBooks)}`,
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
