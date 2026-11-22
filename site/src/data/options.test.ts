import { describe, expect, it } from "vitest";
import {
  DEFAULT_CONFIG,
  PRESETS,
  makeYamlFilename,
  serializeYaml,
  validateConfig,
} from "./options";

describe("YAML generator options", () => {
  it("serializes default options into a valid Archipelago player scroll", () => {
    const yaml = serializeYaml(DEFAULT_CONFIG);
    expect(yaml).toContain("name: Shade");
    expect(yaml).toContain("game: The Longing");
    expect(yaml).toContain("goal: wake_the_king");
    expect(yaml).toContain("countdown_length: 6_hours");
    expect(yaml).toContain("starting_time_rate: 12");
    expect(yaml).toContain("shuffle_crystals: full");
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

  it("validates every built-in preset cleanly", () => {
    for (const preset of PRESETS) {
      const result = validateConfig({ playerName: "Shade", ...preset.config });
      expect(result.valid).toBe(true);
    }
  });

  it("produces clean download filenames", () => {
    expect(makeYamlFilename("King's Shade!")).toBe("King_s_Shade_TheLonging.yaml");
    expect(makeYamlFilename("   ")).toBe("Shade_TheLonging.yaml");
  });
});
