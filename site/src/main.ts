import "./styles/longing.css";

import {
  CAVERN_PASSAGES,
  CAVERN_REGIONS,
  type CavernRegion,
  type CheckCategory,
  formatCheckRequirements,
  getRegionChecks,
} from "./data/mapData";
import {
  type CountdownLength,
  DEFAULT_CONFIG,
  type GoalOption,
  type LongingYamlConfig,
  PRESETS,
  type ShuffleDepth,
  type TrapDensity,
  makeYamlFilename,
  serializeYaml,
  validateConfig,
} from "./data/options";
import { type InstallMethod, type PlayerRole, getSetupSteps } from "./data/setupSteps";

const STORAGE_KEY_SETUP = "thelonging_decrypted_setup_v1";
const STORAGE_KEY_TRACKER = "thelonging_decrypted_tracker_v1";
const SVG_NS = "http://www.w3.org/2000/svg";
const SCREENS = ["menu", "yaml", "map", "setup"] as const;
type ScreenId = (typeof SCREENS)[number];

const CLOCK_RATES = [1, 12, 60] as const;

function byId<T extends HTMLElement>(id: string): T {
  const el = document.getElementById(id);
  if (!el) {
    throw new Error(`Missing required element #${id}`);
  }
  return el as T;
}

/* ==========================================================================
   1. IN-GAME SCREEN ROUTER
   ========================================================================== */

function parseHashScreen(): ScreenId {
  const raw = window.location.hash.replace(/^#/, "").toLowerCase();
  if ((SCREENS as readonly string[]).includes(raw)) {
    return raw as ScreenId;
  }
  return "menu";
}

function initScreens(): void {
  const sections = document.querySelectorAll<HTMLElement>("[data-screen]");
  const buttons = document.querySelectorAll<HTMLButtonElement>("[data-screen-btn]");

  const activate = (screen: ScreenId, updateHash: boolean): void => {
    for (const sec of sections) {
      sec.classList.toggle("is-active", sec.dataset.screen === screen);
    }
    for (const btn of buttons) {
      const target = btn.dataset.screenBtn;
      const isNav = btn.classList.contains("hud-nav-tile") || btn.classList.contains("hud-shield");
      if (isNav) {
        btn.classList.toggle("is-active", target === screen);
      }
    }
    if (updateHash && window.location.hash !== `#${screen}`) {
      history.replaceState(null, "", `#${screen}`);
    }
    window.scrollTo(0, 0);
  };

  for (const btn of buttons) {
    btn.addEventListener("click", () => {
      const target = btn.dataset.screenBtn as ScreenId | undefined;
      if (target && (SCREENS as readonly string[]).includes(target)) {
        activate(target, true);
      }
    });
  }

  window.addEventListener("hashchange", () => {
    activate(parseHashScreen(), false);
  });

  activate(parseHashScreen(), false);

  const closeBtn = byId<HTMLButtonElement>("close-dialogue-btn");
  const dialogue = byId<HTMLElement>("menu-dialogue");
  closeBtn.addEventListener("click", () => {
    dialogue.hidden = true;
  });
}

/* ==========================================================================
   2. 13-CELL 400-DAY COUNTDOWN CLOCK (uhr.png)
   ========================================================================== */

function initCountdownClock(): void {
  const cells = {
    d0: byId<HTMLElement>("clk-d0"),
    d1: byId<HTMLElement>("clk-d1"),
    d2: byId<HTMLElement>("clk-d2"),
    h0: byId<HTMLElement>("clk-h0"),
    h1: byId<HTMLElement>("clk-h1"),
    m0: byId<HTMLElement>("clk-m0"),
    m1: byId<HTMLElement>("clk-m1"),
    s0: byId<HTMLElement>("clk-s0"),
    s1: byId<HTMLElement>("clk-s1"),
  };
  const rateBtn = byId<HTMLButtonElement>("clock-rate-btn");
  const rateLabel = byId<HTMLElement>("clock-rate-label");

  const FOUR_HUNDRED_DAYS = 400 * 86_400;
  let remainingSeconds = FOUR_HUNDRED_DAYS - 1;
  let rateIndex = 0;

  const renderClock = (): void => {
    const clamped = Math.max(0, Math.floor(remainingSeconds));
    const days = String(Math.floor(clamped / 86_400)).padStart(3, "0");
    const hours = String(Math.floor((clamped % 86_400) / 3600)).padStart(2, "0");
    const minutes = String(Math.floor((clamped % 3600) / 60)).padStart(2, "0");
    const seconds = String(clamped % 60).padStart(2, "0");

    cells.d0.textContent = days.charAt(0);
    cells.d1.textContent = days.charAt(1);
    cells.d2.textContent = days.charAt(2);
    cells.h0.textContent = hours.charAt(0);
    cells.h1.textContent = hours.charAt(1);
    cells.m0.textContent = minutes.charAt(0);
    cells.m1.textContent = minutes.charAt(1);
    cells.s0.textContent = seconds.charAt(0);
    cells.s1.textContent = seconds.charAt(1);
  };

  rateBtn.addEventListener("click", () => {
    rateIndex = (rateIndex + 1) % CLOCK_RATES.length;
    const rate = CLOCK_RATES[rateIndex] ?? 1;
    rateLabel.textContent = `${rate}x`;
  });

  renderClock();
  window.setInterval(() => {
    const rate = CLOCK_RATES[rateIndex] ?? 1;
    remainingSeconds = remainingSeconds <= 0 ? FOUR_HUNDRED_DAYS : remainingSeconds - rate;
    renderClock();
  }, 1000);
}

/* ==========================================================================
   3. BOOK-TABBED YAML GENERATOR
   ========================================================================== */

function initYamlGenerator(): void {
  let state: LongingYamlConfig = { ...DEFAULT_CONFIG };
  let activePresetId: string | null = PRESETS[0]?.id ?? null;

  const presetBar = byId<HTMLElement>("preset-bar");
  const bookTabs = document.querySelectorAll<HTMLButtonElement>("[data-yaml-tab]");
  const bookPanels = document.querySelectorAll<HTMLElement>("[data-yaml-panel]");

  const playerName = byId<HTMLInputElement>("player-name");
  const goalSelect = byId<HTMLSelectElement>("goal-select");
  const countdownSelect = byId<HTMLSelectElement>("countdown-select");
  const timeRateRange = byId<HTMLInputElement>("time-rate-range");
  const timeRateValue = byId<HTMLElement>("time-rate-value");
  const walkSpeedRange = byId<HTMLInputElement>("walk-speed-range");
  const walkSpeedValue = byId<HTMLElement>("walk-speed-value");
  const timeSkipItems = byId<HTMLInputElement>("time-skip-items");
  const shuffleMattocks = byId<HTMLInputElement>("shuffle-mattocks");
  const booksSelect = byId<HTMLSelectElement>("books-select");
  const crystalsSelect = byId<HTMLSelectElement>("crystals-select");
  const shuffleMushrooms = byId<HTMLInputElement>("shuffle-mushrooms");
  const shuffleComfort = byId<HTMLInputElement>("shuffle-comfort");
  const shuffleInstruments = byId<HTMLInputElement>("shuffle-instruments");
  const shuffleCoal = byId<HTMLInputElement>("shuffle-coal");
  const shuffleShop = byId<HTMLInputElement>("shuffle-shop");
  const customItems = byId<HTMLInputElement>("custom-items");
  const ao3FanficBooks = byId<HTMLInputElement>("ao3-fanfic-books");
  const autoHints = byId<HTMLInputElement>("auto-hints");
  const requiredCrystalsInput = byId<HTMLInputElement>("required-crystals");
  const requiredBooksInput = byId<HTMLInputElement>("required-books");
  const huntTargetsRow = byId<HTMLElement>("hunt-targets-row");
  const trapDensitySelect = byId<HTMLSelectElement>("trap-density-select");
  const deathLink = byId<HTMLInputElement>("death-link");

  const validationEl = byId<HTMLElement>("yaml-validation");
  const previewEl = byId<HTMLElement>("yaml-preview");
  const filenameEl = byId<HTMLElement>("yaml-filename");
  const checkCountEl = byId<HTMLElement>("yaml-check-count");
  const statusEl = byId<HTMLElement>("yaml-status");
  const downloadBtn = byId<HTMLButtonElement>("download-yaml-btn");
  const copyBtn = byId<HTMLButtonElement>("copy-yaml-btn");
  const resetBtn = byId<HTMLButtonElement>("reset-yaml-btn");

  for (const tab of bookTabs) {
    tab.addEventListener("click", () => {
      const target = tab.dataset.yamlTab;
      for (const t of bookTabs) {
        const active = t.dataset.yamlTab === target;
        t.classList.toggle("is-active", active);
        t.setAttribute("aria-selected", String(active));
      }
      for (const p of bookPanels) {
        p.classList.toggle("is-active", p.dataset.yamlPanel === target);
      }
    });
  }

  const syncControlsFromState = (): void => {
    playerName.value = state.playerName;
    goalSelect.value = state.goal;
    countdownSelect.value = state.countdownLength;
    timeRateRange.value = String(state.startingTimeRate);
    timeRateValue.textContent = `${state.startingTimeRate}x`;
    walkSpeedRange.value = String(state.startingWalkSpeed);
    walkSpeedValue.textContent = `${state.startingWalkSpeed} / 4`;
    timeSkipItems.checked = state.timeSkipItems;
    shuffleMattocks.checked = state.shuffleMattocksAndKeys;
    booksSelect.value = state.shuffleBooks;
    crystalsSelect.value = state.shuffleCrystals;
    shuffleMushrooms.checked = state.shuffleMushroomsAndMoss;
    shuffleComfort.checked = state.shuffleHomeComfort;
    shuffleInstruments.checked = state.shuffleInstrumentParts;
    shuffleCoal.checked = state.shuffleCoalAndSulfur;
    shuffleShop.checked = state.shuffleShop;
    customItems.checked = state.customItems;
    ao3FanficBooks.checked = state.ao3FanficBooks;
    autoHints.checked = state.autoHints;
    requiredCrystalsInput.value = String(state.requiredCrystals);
    requiredBooksInput.value = String(state.requiredBooks);
    huntTargetsRow.hidden = state.goal !== "crystal_book_hunt";
    trapDensitySelect.value = state.trapDensity;
    deathLink.checked = state.deathLink;
  };

  const renderPresets = (): void => {
    presetBar.replaceChildren();
    for (const preset of PRESETS) {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = `preset-btn${activePresetId === preset.id ? " is-active" : ""}`;
      btn.title = preset.summary;
      btn.textContent = preset.label.toUpperCase();
      btn.addEventListener("click", () => {
        activePresetId = preset.id;
        state = {
          playerName: state.playerName,
          ...preset.config,
        };
        syncControlsFromState();
        renderPresets();
        renderYaml();
        statusEl.textContent = `Loaded preset: ${preset.label}.`;
      });
      presetBar.appendChild(btn);
    }
  };

  const renderYaml = (): void => {
    const validation = validateConfig(state);
    validationEl.replaceChildren();

    for (const err of validation.errors) {
      const box = document.createElement("div");
      box.className = "validation-msg is-error";
      box.textContent = err;
      validationEl.appendChild(box);
    }
    for (const warn of validation.warnings) {
      const box = document.createElement("div");
      box.className = "validation-msg";
      box.textContent = warn;
      validationEl.appendChild(box);
    }

    previewEl.textContent = serializeYaml(state);
    filenameEl.textContent = makeYamlFilename(state.playerName);
    checkCountEl.textContent = `GENERATED FILE (${validation.enabledChecks} CHECKS)`;
    downloadBtn.disabled = !validation.valid;
  };

  const readStateFromForm = (): void => {
    activePresetId = null;
    state = {
      playerName: playerName.value,
      goal: goalSelect.value as GoalOption,
      countdownLength: countdownSelect.value as CountdownLength,
      startingTimeRate: Number(timeRateRange.value),
      timeSkipItems: timeSkipItems.checked,
      startingWalkSpeed: Number(walkSpeedRange.value),
      shuffleMattocksAndKeys: shuffleMattocks.checked,
      shuffleBooks: booksSelect.value as ShuffleDepth,
      shuffleCrystals: crystalsSelect.value as ShuffleDepth,
      shuffleMushroomsAndMoss: shuffleMushrooms.checked,
      shuffleHomeComfort: shuffleComfort.checked,
      shuffleInstrumentParts: shuffleInstruments.checked,
      shuffleCoalAndSulfur: shuffleCoal.checked,
      shuffleShop: shuffleShop.checked,
      customItems: customItems.checked,
      ao3FanficBooks: ao3FanficBooks.checked,
      autoHints: autoHints.checked,
      requiredCrystals: Number(requiredCrystalsInput.value),
      requiredBooks: Number(requiredBooksInput.value),
      trapDensity: trapDensitySelect.value as TrapDensity,
      deathLink: deathLink.checked,
    };
    huntTargetsRow.hidden = state.goal !== "crystal_book_hunt";
    timeRateValue.textContent = `${state.startingTimeRate}x`;
    walkSpeedValue.textContent = `${state.startingWalkSpeed} / 4`;
    renderPresets();
    renderYaml();
  };

  const form = byId<HTMLFormElement>("yaml-form");
  form.addEventListener("input", readStateFromForm);
  form.addEventListener("change", readStateFromForm);

  downloadBtn.addEventListener("click", () => {
    const validation = validateConfig(state);
    if (!validation.valid) {
      statusEl.textContent = validation.errors[0] ?? "Please fix the errors above.";
      return;
    }
    const content = serializeYaml(state);
    const filename = makeYamlFilename(state.playerName);
    const blob = new Blob([content], { type: "text/yaml;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    statusEl.textContent = `Downloaded ${filename}.`;
  });

  copyBtn.addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(serializeYaml(state));
      statusEl.textContent = "Copied YAML to clipboard.";
    } catch {
      statusEl.textContent = "Clipboard access unavailable. Use Download YAML instead.";
    }
  });

  resetBtn.addEventListener("click", () => {
    state = { ...DEFAULT_CONFIG };
    activePresetId = PRESETS[0]?.id ?? null;
    syncControlsFromState();
    renderPresets();
    renderYaml();
    statusEl.textContent = "Reset to defaults.";
  });

  syncControlsFromState();
  renderPresets();
  renderYaml();
}

/* ==========================================================================
   4. UNDERGROUND KINGDOM MAP
   ========================================================================== */

function loadCheckedIds(): Set<number> {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY_TRACKER);
    if (!raw) {
      return new Set<number>();
    }
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed)) {
      return new Set<number>(parsed.filter((v): v is number => typeof v === "number"));
    }
  } catch {
    // Ignore storage errors in private contexts.
  }
  return new Set<number>();
}

function saveCheckedIds(ids: Set<number>): void {
  try {
    window.localStorage.setItem(STORAGE_KEY_TRACKER, JSON.stringify(Array.from(ids)));
  } catch {
    // Ignore storage errors in private contexts.
  }
}

function initCavernMap(): void {
  const passagesLayer = document.getElementById("map-passages-layer");
  const nodesLayer = document.getElementById("map-nodes-layer");
  if (!passagesLayer || !nodesLayer) {
    return;
  }

  const filterButtons = document.querySelectorAll<HTMLButtonElement>("[data-filter]");
  const regionDepth = byId<HTMLElement>("region-depth");
  const regionName = byId<HTMLElement>("region-name");
  const regionSummary = byId<HTMLElement>("region-summary");
  const regionGates = byId<HTMLElement>("region-gates");
  const regionChecks = byId<HTMLElement>("region-checks");
  const regionChecksHeading = byId<HTMLElement>("region-checks-heading");
  const toggleSpoilersBtn = byId<HTMLButtonElement>("toggle-spoilers-btn");
  const jumpList = byId<HTMLElement>("region-jump-list");

  const regionById = new Map<string, CavernRegion>(CAVERN_REGIONS.map((r) => [r.id, r]));
  const checkedIds = loadCheckedIds();
  let activeFilter: "all" | CheckCategory = "all";
  let selectedId = "shades_home";
  let showSpoilers = true;

  const renderDrawer = (region: CavernRegion): void => {
    const allRegionChecks = getRegionChecks(region.id, "all");
    const visibleChecks = getRegionChecks(region.id, activeFilter);
    const foundCount = allRegionChecks.filter((c) => checkedIds.has(c.id)).length;

    regionDepth.textContent = `${region.depthLabel} · ${foundCount}/${allRegionChecks.length} found`;
    regionName.textContent = region.name;
    regionSummary.textContent = region.summary;

    regionGates.replaceChildren();
    for (const gate of region.gates) {
      const li = document.createElement("li");
      li.textContent = gate;
      regionGates.appendChild(li);
    }

    regionChecksHeading.textContent = `Cataloged Checks (${visibleChecks.length})`;
    toggleSpoilersBtn.textContent = showSpoilers ? "HIDE SPOILERS" : "SHOW SPOILERS";

    regionChecks.replaceChildren();
    if (!showSpoilers) {
      const li = document.createElement("li");
      li.textContent = `${visibleChecks.length} checks hidden (spoilers off). Toggle Show Spoilers to inspect names and logic.`;
      regionChecks.appendChild(li);
    } else if (visibleChecks.length === 0 && region.endingNotes.length === 0) {
      const li = document.createElement("li");
      li.textContent = "No checks in this region match the current filter.";
      regionChecks.appendChild(li);
    } else {
      for (const ending of region.endingNotes) {
        if (activeFilter === "all" || activeFilter === "endings") {
          const li = document.createElement("li");
          li.textContent = `[Goal] ${ending}`;
          regionChecks.appendChild(li);
        }
      }
      for (const check of visibleChecks) {
        const li = document.createElement("li");
        const label = document.createElement("label");
        label.style.display = "flex";
        label.style.alignItems = "flex-start";
        label.style.gap = "0.45rem";
        label.style.cursor = "pointer";

        const box = document.createElement("input");
        box.type = "checkbox";
        box.checked = checkedIds.has(check.id);
        box.addEventListener("change", () => {
          if (box.checked) {
            checkedIds.add(check.id);
          } else {
            checkedIds.delete(check.id);
          }
          saveCheckedIds(checkedIds);
          renderDrawer(region);
          renderJumpList();
        });

        const textSpan = document.createElement("span");
        const reqSuffix =
          check.requires.length > 0 ? ` [${formatCheckRequirements(check.requires)}]` : "";
        textSpan.textContent = `${check.name}${reqSuffix}`;
        if (box.checked) {
          textSpan.style.textDecoration = "line-through";
          textSpan.style.opacity = "0.65";
        }

        label.appendChild(box);
        label.appendChild(textSpan);
        li.appendChild(label);
        regionChecks.appendChild(li);
      }
    }
  };

  toggleSpoilersBtn.addEventListener("click", () => {
    showSpoilers = !showSpoilers;
    const active = regionById.get(selectedId);
    if (active) {
      renderDrawer(active);
    }
  });

  const renderJumpList = (): void => {
    jumpList.replaceChildren();
    for (const region of CAVERN_REGIONS) {
      const regionTotal = getRegionChecks(region.id, "all").length;
      const regionFound = getRegionChecks(region.id, "all").filter((c) =>
        checkedIds.has(c.id),
      ).length;
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = `region-jump-btn${region.id === selectedId ? " is-active" : ""}`;
      btn.textContent = `${region.name} (${regionFound}/${regionTotal})`;
      btn.addEventListener("click", () => {
        selectedId = region.id;
        renderMap();
        renderJumpList();
      });
      jumpList.appendChild(btn);
    }
  };

  const renderMap = (): void => {
    passagesLayer.replaceChildren();
    nodesLayer.replaceChildren();

    for (const passage of CAVERN_PASSAGES) {
      const a = regionById.get(passage.from);
      const b = regionById.get(passage.to);
      if (!a || !b) {
        continue;
      }
      const line = document.createElementNS(SVG_NS, "line");
      line.setAttribute("x1", String(a.x));
      line.setAttribute("y1", String(a.y));
      line.setAttribute("x2", String(b.x));
      line.setAttribute("y2", String(b.y));
      line.setAttribute("class", `map-passage${passage.gated ? " is-gated" : ""}`);
      passagesLayer.appendChild(line);
    }

    for (const region of CAVERN_REGIONS) {
      const matches = activeFilter === "all" || region.categories.includes(activeFilter);
      const isSelected = region.id === selectedId;

      const group = document.createElementNS(SVG_NS, "g");
      group.setAttribute(
        "class",
        `map-node${isSelected ? " is-selected" : ""}${matches ? "" : " is-dimmed"}`,
      );
      group.setAttribute("tabindex", "0");
      group.setAttribute("role", "button");
      group.setAttribute("aria-label", `${region.name}: ${region.depthLabel}`);

      // Carved diamond marker (matches angular runic style)
      const pin = document.createElementNS(SVG_NS, "polygon");
      const r = isSelected ? 10 : 7;
      pin.setAttribute(
        "points",
        `${region.x},${region.y - r} ${region.x + r},${region.y} ${region.x},${region.y + r} ${region.x - r},${region.y}`,
      );
      pin.setAttribute("class", "map-node-pin");
      group.appendChild(pin);

      const label = document.createElementNS(SVG_NS, "text");
      label.setAttribute("x", String(region.x));
      label.setAttribute("y", String(region.y + (region.labelDy ?? -14)));
      label.setAttribute("class", "map-node-label");
      label.textContent = region.name;
      group.appendChild(label);

      const selectRegion = (): void => {
        selectedId = region.id;
        renderMap();
        renderJumpList();
      };

      group.addEventListener("click", selectRegion);
      group.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          selectRegion();
        }
      });

      nodesLayer.appendChild(group);
    }

    const active = regionById.get(selectedId);
    if (active) {
      renderDrawer(active);
    }
  };

  for (const btn of filterButtons) {
    btn.addEventListener("click", () => {
      activeFilter = (btn.dataset.filter ?? "all") as "all" | CheckCategory;
      for (const other of filterButtons) {
        other.classList.toggle("is-active", other === btn);
      }
      renderMap();
    });
  }

  renderMap();
  renderJumpList();
}

/* ==========================================================================
   5. SETUP CHECKLIST
   ========================================================================== */

interface SavedSetupState {
  role: PlayerRole;
  method: InstallMethod;
  checked: string[];
}

function loadSetupState(): SavedSetupState {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY_SETUP);
    if (!raw) {
      return { role: "join", method: "manager", checked: [] };
    }
    const parsed = JSON.parse(raw) as Partial<SavedSetupState>;
    return {
      role: parsed.role === "host" ? "host" : "join",
      method: parsed.method === "manual" ? "manual" : "manager",
      checked: Array.isArray(parsed.checked)
        ? parsed.checked.filter((id): id is string => typeof id === "string")
        : [],
    };
  } catch {
    return { role: "join", method: "manager", checked: [] };
  }
}

function saveSetupState(state: SavedSetupState): void {
  try {
    window.localStorage.setItem(STORAGE_KEY_SETUP, JSON.stringify(state));
  } catch {
    // Ignore storage errors in private contexts.
  }
}

function initSetupChecklist(): void {
  const saved = loadSetupState();
  const checkedSet = new Set<string>(saved.checked);
  let role: PlayerRole = saved.role;
  let method: InstallMethod = saved.method;

  const stepsList = byId<HTMLOListElement>("setup-steps-list");
  const progressFill = byId<HTMLElement>("setup-progress-fill");
  const progressCount = byId<HTMLElement>("setup-progress-count");
  const resetBtn = byId<HTMLButtonElement>("reset-setup-btn");

  const roleInputs = document.querySelectorAll<HTMLInputElement>('input[name="setupRole"]');
  const methodInputs = document.querySelectorAll<HTMLInputElement>('input[name="installMethod"]');

  for (const input of roleInputs) {
    input.checked = input.value === role;
  }
  for (const input of methodInputs) {
    input.checked = input.value === method;
  }

  const persist = (): void => {
    saveSetupState({ role, method, checked: Array.from(checkedSet) });
  };

  const renderSteps = (): void => {
    const steps = getSetupSteps(role, method);
    stepsList.replaceChildren();

    let completed = 0;
    for (const step of steps) {
      const isChecked = checkedSet.has(step.id);
      if (isChecked) {
        completed += 1;
      }

      const li = document.createElement("li");
      const label = document.createElement("label");

      const checkbox = document.createElement("input");
      checkbox.type = "checkbox";
      checkbox.checked = isChecked;
      checkbox.addEventListener("change", () => {
        if (checkbox.checked) {
          checkedSet.add(step.id);
        } else {
          checkedSet.delete(step.id);
        }
        persist();
        renderSteps();
      });

      const box = document.createElement("span");
      box.className = "step-box";
      box.setAttribute("aria-hidden", "true");

      const body = document.createElement("span");
      const title = document.createElement("span");
      title.className = "step-title";
      title.textContent = step.title;

      const detail = document.createElement("span");
      detail.className = "step-detail";
      detail.textContent = step.detail;

      body.appendChild(title);
      body.appendChild(detail);

      label.appendChild(checkbox);
      label.appendChild(box);
      label.appendChild(body);
      li.appendChild(label);
      stepsList.appendChild(li);
    }

    const total = steps.length;
    const pct = total === 0 ? 0 : Math.round((completed / total) * 100);
    progressFill.style.width = `${pct}%`;
    progressCount.textContent = `${completed} / ${total}`;
  };

  for (const input of roleInputs) {
    input.addEventListener("change", () => {
      if (input.checked) {
        role = input.value === "host" ? "host" : "join";
        persist();
        renderSteps();
      }
    });
  }

  for (const input of methodInputs) {
    input.addEventListener("change", () => {
      if (input.checked) {
        method = input.value === "manual" ? "manual" : "manager";
        persist();
        renderSteps();
      }
    });
  }

  resetBtn.addEventListener("click", () => {
    checkedSet.clear();
    persist();
    renderSteps();
  });

  renderSteps();
}

/* ==========================================================================
   BOOT
   ========================================================================== */

initScreens();
initCountdownClock();
initYamlGenerator();
initCavernMap();
initSetupChecklist();
