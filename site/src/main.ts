import "./styles/longing.css";
import { CAVERN_PASSAGES, CAVERN_REGIONS, type CheckCategory } from "./data/mapData";
import {
  type CountdownLength,
  DEFAULT_CONFIG,
  type GoalOption,
  type LongingYamlConfig,
  PRESETS,
  type ShuffleDepth,
  type TrapDensity,
  type WalkSpeed,
  makeYamlFilename,
  serializeYaml,
  validateConfig,
} from "./data/options";
import { type InstallMethod, type PlayerRole, getSetupSteps } from "./data/setupSteps";

const STORAGE_KEY = "longing-decrypted-setup-v1";
const SVG_NS = "http://www.w3.org/2000/svg";

/* -------------------------------------------------------------------------- */
/* 1. Signature 400-day live countdown clock                                  */
/* -------------------------------------------------------------------------- */
function initCountdown(): void {
  const readout = document.getElementById("countdown-readout");
  const buttons = document.querySelectorAll<HTMLButtonElement>(".rate-btn");
  if (!readout) return;

  let remainingSeconds = 400 * 86400 - 1;
  let rate = 1;
  let lastTick = performance.now();

  const render = (): void => {
    const clamped = Math.max(0, Math.floor(remainingSeconds));
    const days = Math.floor(clamped / 86400);
    const hours = Math.floor((clamped % 86400) / 3600);
    const mins = Math.floor((clamped % 3600) / 60);
    const secs = clamped % 60;
    const pad = (n: number): string => String(n).padStart(2, "0");
    readout.textContent = `${days}d · ${pad(hours)}:${pad(mins)}:${pad(secs)}`;
  };

  for (const btn of buttons) {
    btn.addEventListener("click", () => {
      const next = Number(btn.dataset.rate || "1");
      if (!Number.isFinite(next) || next < 1) return;
      rate = next;
      for (const b of buttons) {
        b.classList.toggle("is-active", b === btn);
      }
    });
  }

  render();
  window.setInterval(() => {
    const now = performance.now();
    const dt = (now - lastTick) / 1000;
    lastTick = now;
    remainingSeconds = Math.max(0, remainingSeconds - dt * rate);
    render();
  }, 250);
}

/* -------------------------------------------------------------------------- */
/* 2. Archipelago YAML Generator                                              */
/* -------------------------------------------------------------------------- */
function initYamlGenerator(): void {
  const presetStrip = document.getElementById("preset-strip");
  const playerInput = document.getElementById("opt-player") as HTMLInputElement | null;
  const goalSelect = document.getElementById("opt-goal") as HTMLSelectElement | null;
  const countdownSelect = document.getElementById("opt-countdown") as HTMLSelectElement | null;
  const walkSelect = document.getElementById("opt-walk") as HTMLSelectElement | null;
  const rateInput = document.getElementById("opt-rate") as HTMLInputElement | null;
  const rateLabel = document.getElementById("opt-rate-label");
  const timeSkipInput = document.getElementById("opt-timeskip") as HTMLInputElement | null;
  const booksSelect = document.getElementById("opt-books") as HTMLSelectElement | null;
  const crystalsSelect = document.getElementById("opt-crystals") as HTMLSelectElement | null;
  const toolsInput = document.getElementById("opt-tools") as HTMLInputElement | null;
  const mushroomsInput = document.getElementById("opt-mushrooms") as HTMLInputElement | null;
  const comfortInput = document.getElementById("opt-comfort") as HTMLInputElement | null;
  const instrumentsInput = document.getElementById("opt-instruments") as HTMLInputElement | null;
  const coalInput = document.getElementById("opt-coal") as HTMLInputElement | null;
  const deathLinkInput = document.getElementById("opt-deathlink") as HTMLInputElement | null;
  const trapsSelect = document.getElementById("opt-traps") as HTMLSelectElement | null;

  const filenameEl = document.getElementById("yaml-filename");
  const outputEl = document.getElementById("yaml-output");
  const messagesEl = document.getElementById("yaml-messages");
  const copyBtn = document.getElementById("copy-yaml") as HTMLButtonElement | null;
  const downloadBtn = document.getElementById("download-yaml") as HTMLButtonElement | null;

  if (
    !presetStrip ||
    !playerInput ||
    !goalSelect ||
    !countdownSelect ||
    !walkSelect ||
    !rateInput ||
    !rateLabel ||
    !timeSkipInput ||
    !booksSelect ||
    !crystalsSelect ||
    !toolsInput ||
    !mushroomsInput ||
    !comfortInput ||
    !instrumentsInput ||
    !coalInput ||
    !deathLinkInput ||
    !trapsSelect ||
    !filenameEl ||
    !outputEl ||
    !messagesEl ||
    !copyBtn ||
    !downloadBtn
  ) {
    return;
  }

  let activePresetId: string | null = PRESETS[0]?.id ?? null;
  let statusFlash = "";

  const readConfig = (): LongingYamlConfig => ({
    playerName: playerInput.value,
    goal: goalSelect.value as GoalOption,
    countdownLength: countdownSelect.value as CountdownLength,
    startingTimeRate: Number(rateInput.value),
    walkSpeed: walkSelect.value as WalkSpeed,
    timeSkipItems: timeSkipInput.checked,
    shuffleMattocksAndKeys: toolsInput.checked,
    shuffleBooks: booksSelect.value as ShuffleDepth,
    shuffleCrystals: crystalsSelect.value as ShuffleDepth,
    shuffleMushroomsAndMoss: mushroomsInput.checked,
    shuffleHomeComfort: comfortInput.checked,
    shuffleInstrumentParts: instrumentsInput.checked,
    shuffleCoalAndSulfur: coalInput.checked,
    trapDensity: trapsSelect.value as TrapDensity,
    deathLink: deathLinkInput.checked,
  });

  const applyConfigToForm = (cfg: Omit<LongingYamlConfig, "playerName">): void => {
    goalSelect.value = cfg.goal;
    countdownSelect.value = cfg.countdownLength;
    walkSelect.value = cfg.walkSpeed;
    rateInput.value = String(cfg.startingTimeRate);
    timeSkipInput.checked = cfg.timeSkipItems;
    booksSelect.value = cfg.shuffleBooks;
    crystalsSelect.value = cfg.shuffleCrystals;
    toolsInput.checked = cfg.shuffleMattocksAndKeys;
    mushroomsInput.checked = cfg.shuffleMushroomsAndMoss;
    comfortInput.checked = cfg.shuffleHomeComfort;
    instrumentsInput.checked = cfg.shuffleInstrumentParts;
    coalInput.checked = cfg.shuffleCoalAndSulfur;
    deathLinkInput.checked = cfg.deathLink;
    trapsSelect.value = cfg.trapDensity;
  };

  const renderPresets = (): void => {
    presetStrip.innerHTML = "";
    for (const preset of PRESETS) {
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = `preset-btn${activePresetId === preset.id ? " is-active" : ""}`;
      btn.innerHTML = `<strong>${preset.label}</strong><small>${preset.summary}</small>`;
      btn.addEventListener("click", () => {
        activePresetId = preset.id;
        statusFlash = "";
        applyConfigToForm(preset.config);
        renderPresets();
        updateYaml();
      });
      presetStrip.appendChild(btn);
    }
  };

  const updateYaml = (): void => {
    const cfg = readConfig();
    rateLabel.textContent = `${cfg.startingTimeRate}x`;
    const validation = validateConfig(cfg);
    const yaml = serializeYaml(cfg);
    filenameEl.textContent = makeYamlFilename(cfg.playerName);
    outputEl.textContent = yaml;

    downloadBtn.disabled = !validation.valid;
    copyBtn.disabled = !validation.valid;

    messagesEl.innerHTML = "";
    for (const err of validation.errors) {
      const p = document.createElement("p");
      p.className = "yaml-msg is-error";
      p.textContent = err;
      messagesEl.appendChild(p);
    }
    for (const warn of validation.warnings) {
      const p = document.createElement("p");
      p.className = "yaml-msg";
      p.textContent = warn;
      messagesEl.appendChild(p);
    }
    if (statusFlash && validation.valid) {
      const p = document.createElement("p");
      p.className = "yaml-msg is-ok";
      p.textContent = statusFlash;
      messagesEl.appendChild(p);
    }
  };

  const form = document.getElementById("yaml-form");
  form?.addEventListener("input", () => {
    activePresetId = null;
    statusFlash = "";
    renderPresets();
    updateYaml();
  });

  copyBtn.addEventListener("click", async () => {
    const cfg = readConfig();
    if (!validateConfig(cfg).valid) return;
    try {
      await navigator.clipboard.writeText(serializeYaml(cfg));
      statusFlash = "Copied YAML scroll to clipboard.";
    } catch {
      statusFlash = "Clipboard blocked — use Download .yaml instead.";
    }
    updateYaml();
  });

  downloadBtn.addEventListener("click", () => {
    const cfg = readConfig();
    if (!validateConfig(cfg).valid) return;
    const blob = new Blob([serializeYaml(cfg)], { type: "text/yaml;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = makeYamlFilename(cfg.playerName);
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    statusFlash = `Downloaded ${makeYamlFilename(cfg.playerName)}.`;
    updateYaml();
  });

  applyConfigToForm(DEFAULT_CONFIG);
  renderPresets();
  updateYaml();
}

/* -------------------------------------------------------------------------- */
/* 3. Interactive Subterranean Cavern Map                                     */
/* -------------------------------------------------------------------------- */
function initCavernMap(): void {
  const svg = document.getElementById("cavern-svg");
  const notebook = document.getElementById("region-notebook");
  const filterWrap = document.getElementById("map-filters");
  if (!svg || !notebook || !filterWrap) return;

  let activeFilter: "all" | CheckCategory = "all";
  let selectedId = "shades_home";
  const byId = new Map(CAVERN_REGIONS.map((r) => [r.id, r]));

  const renderNotebook = (): void => {
    const region = byId.get(selectedId) ?? CAVERN_REGIONS[0];
    if (!region) return;
    notebook.innerHTML = "";

    const depth = document.createElement("p");
    depth.className = "notebook-depth";
    depth.textContent = region.depthLabel;

    const title = document.createElement("h3");
    title.textContent = region.name;

    const summary = document.createElement("p");
    summary.textContent = region.summary;

    const makeSection = (heading: string, items: string[]): HTMLDivElement => {
      const box = document.createElement("div");
      box.className = "notebook-section";
      const h4 = document.createElement("h4");
      h4.textContent = heading;
      const ul = document.createElement("ul");
      ul.className = "notebook-list";
      for (const item of items) {
        const li = document.createElement("li");
        li.textContent = item;
        ul.appendChild(li);
      }
      box.append(h4, ul);
      return box;
    };

    const tags = document.createElement("div");
    tags.className = "tag-row";
    for (const cat of region.categories) {
      const span = document.createElement("span");
      span.className = "region-tag";
      span.textContent = cat;
      tags.appendChild(span);
    }

    notebook.append(
      depth,
      title,
      summary,
      makeSection("Progression Gates", region.gates),
      makeSection("Planned Check Pool", region.checks),
      tags,
    );
  };

  const renderSvg = (): void => {
    svg.innerHTML = "";

    // Subtle geological strata lines
    const strata = [98, 200, 315, 425];
    for (const y of strata) {
      const path = document.createElementNS(SVG_NS, "path");
      path.setAttribute("d", `M 16 ${y} Q 210 ${y - 10}, 410 ${y + 6} T 804 ${y - 4}`);
      path.setAttribute("fill", "none");
      path.setAttribute("stroke", "#2a1e18");
      path.setAttribute("stroke-width", "1.4");
      svg.appendChild(path);
    }

    // Subterranean passages
    for (const passage of CAVERN_PASSAGES) {
      const a = byId.get(passage.from);
      const b = byId.get(passage.to);
      if (!a || !b) continue;
      const line = document.createElementNS(SVG_NS, "line");
      line.setAttribute("x1", String(a.x));
      line.setAttribute("y1", String(a.y));
      line.setAttribute("x2", String(b.x));
      line.setAttribute("y2", String(b.y));
      line.setAttribute("stroke", passage.gated ? "#c89d4e" : "#7c624a");
      line.setAttribute("stroke-width", passage.gated ? "2.4" : "2");
      line.setAttribute("stroke-opacity", passage.gated ? "0.72" : "0.52");
      if (passage.gated) {
        line.setAttribute("stroke-dasharray", "7 5");
      }
      svg.appendChild(line);
    }

    // Interactive chamber nodes
    for (const region of CAVERN_REGIONS) {
      const matches = activeFilter === "all" || region.categories.includes(activeFilter);
      const g = document.createElementNS(SVG_NS, "g");
      const classes = ["cavern-node"];
      if (!matches) classes.push("is-dimmed");
      if (region.id === selectedId) classes.push("is-selected");
      if (region.id === "shades_home") classes.push("is-home");
      if (region.categories.includes("endings")) classes.push("is-ending");
      g.setAttribute("class", classes.join(" "));
      g.setAttribute("tabindex", "0");
      g.setAttribute("role", "button");
      g.setAttribute("aria-label", `${region.name} (${region.depthLabel})`);

      const ring = document.createElementNS(SVG_NS, "circle");
      ring.setAttribute("class", "node-ring");
      ring.setAttribute("cx", String(region.x));
      ring.setAttribute("cy", String(region.y));
      ring.setAttribute("r", region.id === selectedId ? "16" : "13");

      const core = document.createElementNS(SVG_NS, "circle");
      core.setAttribute("class", "node-core");
      core.setAttribute("cx", String(region.x));
      core.setAttribute("cy", String(region.y));
      core.setAttribute("r", "5.5");

      const label = document.createElementNS(SVG_NS, "text");
      label.setAttribute("class", "node-label");
      label.setAttribute("x", String(region.x));
      label.setAttribute("y", String(region.y + 30));
      label.textContent = region.name;

      const select = (): void => {
        selectedId = region.id;
        renderSvg();
        renderNotebook();
      };

      g.addEventListener("click", select);
      g.addEventListener("keydown", (event) => {
        if (event.key === "Enter" || event.key === " ") {
          event.preventDefault();
          select();
        }
      });

      g.append(ring, core, label);
      svg.appendChild(g);
    }
  };

  for (const btn of filterWrap.querySelectorAll<HTMLButtonElement>(".filter-chip")) {
    btn.addEventListener("click", () => {
      activeFilter = (btn.dataset.filter as "all" | CheckCategory) || "all";
      for (const b of filterWrap.querySelectorAll<HTMLButtonElement>(".filter-chip")) {
        b.classList.toggle("is-active", b === btn);
      }
      renderSvg();
    });
  }

  renderSvg();
  renderNotebook();
}

/* -------------------------------------------------------------------------- */
/* 4. Branching Setup Checklist (persisted in localStorage)                   */
/* -------------------------------------------------------------------------- */
interface SavedChecklistState {
  role?: PlayerRole;
  method?: InstallMethod;
  done: Record<string, boolean>;
}

function loadChecklistState(): SavedChecklistState {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return { done: {} };
    const parsed = JSON.parse(raw) as Partial<SavedChecklistState>;
    return {
      role: parsed.role === "join" || parsed.role === "host" ? parsed.role : undefined,
      method: parsed.method === "manager" || parsed.method === "manual" ? parsed.method : undefined,
      done: parsed.done && typeof parsed.done === "object" ? parsed.done : {},
    };
  } catch {
    return { done: {} };
  }
}

function saveChecklistState(state: SavedChecklistState): void {
  try {
    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
  } catch {
    // Ignore storage quota or private-mode restrictions.
  }
}

function initSetupChecklist(): void {
  const listEl = document.getElementById("setup-steps");
  const hintEl = document.getElementById("setup-hint");
  const fillEl = document.getElementById("setup-fill");
  const countEl = document.getElementById("setup-count");
  const resetBtn = document.getElementById("setup-reset");
  const roleRadios = document.querySelectorAll<HTMLInputElement>('input[name="role"]');
  const methodRadios = document.querySelectorAll<HTMLInputElement>('input[name="method"]');
  if (!listEl || !hintEl || !fillEl || !countEl || !resetBtn) return;

  let state = loadChecklistState();

  const syncRadios = (): void => {
    for (const r of roleRadios) r.checked = r.value === state.role;
    for (const m of methodRadios) m.checked = m.value === state.method;
  };

  const render = (): void => {
    listEl.innerHTML = "";
    if (!state.role || !state.method) {
      hintEl.hidden = false;
      fillEl.style.width = "0%";
      countEl.textContent = "0 / 0";
      return;
    }

    hintEl.hidden = true;
    const steps = getSetupSteps(state.role, state.method);
    let completed = 0;

    for (const step of steps) {
      const checked = Boolean(state.done[step.id]);
      if (checked) completed += 1;

      const li = document.createElement("li");
      const label = document.createElement("label");
      const checkbox = document.createElement("input");
      checkbox.type = "checkbox";
      checkbox.checked = checked;
      checkbox.addEventListener("change", () => {
        state.done[step.id] = checkbox.checked;
        saveChecklistState(state);
        render();
      });

      const body = document.createElement("div");
      const strong = document.createElement("strong");
      strong.textContent = step.title;
      const span = document.createElement("span");
      span.textContent = step.detail;
      body.append(strong, span);

      label.append(checkbox, body);
      li.appendChild(label);
      listEl.appendChild(li);
    }

    const pct = steps.length > 0 ? Math.round((completed / steps.length) * 100) : 0;
    fillEl.style.width = `${pct}%`;
    countEl.textContent = `${completed} / ${steps.length}`;
  };

  for (const radio of roleRadios) {
    radio.addEventListener("change", () => {
      state.role = radio.value as PlayerRole;
      saveChecklistState(state);
      render();
    });
  }

  for (const radio of methodRadios) {
    radio.addEventListener("change", () => {
      state.method = radio.value as InstallMethod;
      saveChecklistState(state);
      render();
    });
  }

  resetBtn.addEventListener("click", () => {
    state = { done: {} };
    saveChecklistState(state);
    syncRadios();
    render();
  });

  syncRadios();
  render();
}

initCountdown();
initYamlGenerator();
initCavernMap();
initSetupChecklist();
