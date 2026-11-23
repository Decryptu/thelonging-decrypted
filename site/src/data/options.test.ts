import { describe, expect, it } from "vitest";
import { getRegionChecks } from "./mapData";
import {
  DEFAULT_CONFIG,
  PRESETS,
  countEnabledChecks,
  makeYamlFilename,
  serializeYaml,
  validateConfig,
} from "./options";

describe("YAML generator options", () => {
  it("serializes default options into a valid Archipelago 0.6.8+ player scroll", () => {
    const yaml = serializeYaml(DEFAULT_CONFIG);
    expect(yaml).toContain("name: Shade");
    expect(yaml).toContain("game: The Longing");
    expect(yaml).toContain("goal: wake_the_king");
    expect(yaml).toContain("countdown_length: 6_hours");
    expect(yaml).toContain("starting_time_rate: 12");
    expect(yaml).not.toContain("walk_speed:");
    expect(yaml).toContain("shuffle_crystals: full");
    expect(yaml).toContain("required_crystals: 8");
    expect(yaml).toContain("required_books: 12");
  });

  it("quotes YAML-reserved or punctuated player names safely", () => {
    const yaml = serializeYaml({ ...DEFAULT_CONFIG, playerName: "O'Shade:400" });
    expect(yaml).toContain("name: 'O''Shade:400'");
  });

  it("rejects empty, overlong, or brace-containing player names", () => {
    expect(validateConfig({ ...DEFAULT_CONFIG, playerName: "   " }).valid).toBe(false);
    expect(validateConfig({ ...DEFAULT_CONFIG, playerName: "A".repeat(17) }).valid).toBe(false);
    expect(validateConfig({ ...DEFAULT_CONFIG, playerName: "Shade{1}" }).valid).toBe(false);
  });

  it("warns on 400-day countdown without time-skip items at low time rate", () => {
    const result = validateConfig({
      ...DEFAULT_CONFIG,
      goal: "wake_the_king",
      countdownLength: "400_days",
      startingTimeRate: 1,
      timeSkipItems: false,
    });
    expect(result.valid).toBe(true);
    expect(result.warnings.length).toBeGreaterThan(0);
  });

  it("warns when crystal_book_hunt requirements exceed shuffled pool size", () => {
    const result = validateConfig({
      ...DEFAULT_CONFIG,
      goal: "crystal_book_hunt",
      shuffleCrystals: "progression",
      requiredCrystals: 12,
    });
    expect(result.valid).toBe(true);
    expect(result.warnings.some((w) => w.includes("clamp required_crystals"))).toBe(true);
  });

  it("computes real enabled check counts from the synced cavern catalog", () => {
    const fullPreset = PRESETS.find((p) => p.id === "full_cavern");
    expect(fullPreset).toBeDefined();
    if (fullPreset) {
      expect(countEnabledChecks({ playerName: "Shade", ...fullPreset.config })).toBe(152);
    }
    expect(getRegionChecks("library", "books").length).toBe(19);
  });

  it("validates every built-in preset cleanly", () => {
    for (const preset of PRESETS) {
      const result = validateConfig({ playerName: "Shade", ...preset.config });
      expect(result.valid).toBe(true);
      expect(result.enabledChecks).toBeGreaterThanOrEqual(12);
    }
  });

  it("produces clean download filenames", () => {
    expect(makeYamlFilename("King's Shade!")).toBe("King_s_Shade_TheLonging.yaml");
    expect(makeYamlFilename("   ")).toBe("Shade_TheLonging.yaml");
  });
});
