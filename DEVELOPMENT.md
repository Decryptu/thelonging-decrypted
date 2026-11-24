# Development

Everything a contributor needs to inspect *The Longing*, run the player hub locally, and build the mod and Archipelago world. Tracked milestones live in [ROADMAP.md](ROADMAP.md).

## Repository layout

| Path | Contents |
| --- | --- |
| `data/` | Structured world catalog (`world_catalog.json`) extracted from game scenes and assemblies |
| `apworld/the_longing/` | Archipelago 0.6.8+ world package (options, items, locations, regions, rules, Universal Tracker map page, docs, generation & tracker tests) |
| `tracker/` | PopTracker pack (manifest, cavern map & icons, Lua logic & Archipelago autotracking for 160 checks, 7 discoveries, progressive mobility, and 3 recall rings) |
| `mod/` | BepInEx 5 x86 C# client plugin (`net472`, Harmony patches, Archipelago client, interface built from the game's own sprites and fonts) |
| `site/` | GitHub Pages player hub (Vite + TypeScript, option-synced YAML generator, interactive cavern tracker, branching setup checklist), deployed from `main` |
| `assets/` | Project emblem (`logo.png`) |
| `tools/` | Scripts: `extract_catalog.py`, `build_head_anchors.py`, `sync_world_data.py`, `build_apworld.py`, `build_tracker_pack.py`, `decode_save.py`, `decompile.ps1`, `check_public_metadata.py` |
| `.githooks/` | Publication metadata hooks (`commit-msg`, `pre-push`) |
| `local/` | Ignored local directory for save backups, decompiled classes (`local/code/`), local NuGet cache, and extracted research assets |

## Verified game internals (Steam Windows build)

- **Engine:** Unity `2018.4.36f1`, **32-bit x86** (`The Longing.exe`, PE machine `0x14c`), `MonoBleedingEdge` (.NET 4.x runtime).
- **Mod loader target:** **BepInEx 5 x86** (64-bit BepInEx will not load into the 32-bit executable).
- **Assemblies:** `The Longing_Data/Managed/Assembly-CSharp.dll` and `Assembly-CSharp-firstpass.dll`.
  - `Assembly-CSharp.dll` is processed with BeeByte Obfuscator: non-Unity method and field names are randomized 11-letter identifiers and classes contain injected decoy methods (recognizable by bogus constants or `i += 0` loops). Serialized `[SerializeField]` members, Unity lifecycle methods (`Awake`, `Start`, `Update`), and serialized inspector references remain unobfuscated.
  - `Assembly-CSharp-firstpass.dll` is unobfuscated and holds `FileBasedPrefs`, `FileBasedPrefsSaveFileModel`, and `SaveDataHandlerImplementation`.
- **Key gameplay classes spotted in `Assembly-CSharp.dll`:**
  - Core orchestration: `GameManagerScript`, `SceneManagerScript`, `WayPointManager`, `OverlayManager`
  - Time & anti-cheat: `UnixTimer`, `TimeLimit`, `GatherTimeMultis`, `CodeStage.AntiCheat.Detectors.SpeedHackDetector`, `CodeStage.AntiCheat.Detectors.TimeCheatingDetector`
  - Interactions & checks: `Interactable_GrabItemOnce`, `InteractableOutcome_Item`, `InteractableOutcome_GrabHackingTool`, `InteractableOutcome_GrabMushroom`, `InteractableOutcome_PlantMushroom`, `InteractableOutcome_MapReveal`, `Interactable_DoorOpening_*`, `Interactable_Home_*` (`BuildBed`, `Draw`, `Kamin`, `KaminSulfur`, `Music`, `Read`, `SleepBed`), `Interactable_King`, `Interactable_MysteryDoor`
  - Endings: `KingEnding`, `SuicideEnding`, `TragedyEnding`
- **Assets:** Packaged in `The Longing_Data/data.unity3d`, `sharedassets*.resource`, and `StreamingAssets/` (including `books_plaintext/` and room asset bundles). Notably, `data.unity3d` contains the game's full cavern map sprite (`secret_map_4k`), 81 scene setups, and `QUINTESSENCE` font assets.

## Save file format and tooling

Saves live at `%USERPROFILE%\AppData\LocalLow\Studio Seufz\The Longing`:

- `saveDataNew.txt` (active save) and `saveData2.txt` (secondary / ending save)
- `LongingMap1.jpeg` (player's revealed map texture)

`FileBasedPrefs` stores a Unity `JsonUtility` object (`FileBasedPrefsSaveFileModel`) with four typed arrays (`StringData`, `IntData`, `FloatData`, `BoolData`), scrambled by a repeating-key character XOR with `"longingLALSD9034jokdsfmm"`. Some entries inside `StringData` are additionally wrapped with ACTk `ObscuredPrefs` (base64 keys/values).

Use `tools/decode_save.py` to inspect or re-encode saves locally:

```powershell
python tools/decode_save.py local/saves/finished-game/saveData2.txt --summary
python tools/decode_save.py local/saves/finished-game/saveData2.txt --filter book
python tools/decode_save.py local/saves/finished-game/saveData2.txt -o local/saves/decoded.json
python tools/decode_save.py local/saves/decoded.json --encode -o local/saves/reencoded.txt
```

Keep all personal saves and decoded dumps inside ignored `local/saves/`.

## Decompiling individual types

Inspect single types on demand with `tools/decompile.ps1` (requires `ilspycmd`, set `ILSPYCMD_PATH` if not at the default path, and `LONGING_GAME_DIR` if Steam is installed elsewhere):

```powershell
.\tools\decompile.ps1 -Type GameManagerScript
.\tools\decompile.ps1 -Type FileBasedPrefs -Assembly Assembly-CSharp-firstpass.dll
```

Output lands in ignored `local/code/`. Never commit decompiled source or extracted game data.

## Building the catalog, APWorld, and BepInEx plugin

```powershell
# Re-extract structured world catalog from local Steam install and sync IDs to APWorld, mod, and hub
python tools/extract_catalog.py
python tools/sync_world_data.py

# Run Archipelago 0.6.8+ solo & multiworld generation tests and tracker pack verification
python apworld/the_longing/test/test_generation.py -v
python apworld/the_longing/test/test_tracker_pack.py -v

# Regenerate and package the PopTracker & Universal Tracker pack
python tools/build_tracker_pack.py --output dist/the_longing_poptracker.zip

# Package the_longing.apworld from tracked files
python tools/build_apworld.py --output dist/the_longing.apworld

# Recompute where hats sit on every Shade animation frame (needs the game install)
python tools/build_head_anchors.py

# Build the BepInEx 5 x86 plugin (net472)
dotnet build mod/TheLongingDecrypted.csproj -c Release
```

For local playtesting, copy `TheLongingDecrypted.dll`, `Archipelago.MultiClient.Net.dll`, and `Newtonsoft.Json.dll` from `mod/bin/Release/net472/` into `BepInEx/plugins/TheLongingDecrypted/`. Use disposable slots and leave the vanilla save untouched. When inspecting a slot with `python tools/decode_save.py <save-path> --filter AP_`, look for `AP_CLOCK_OFFSET_SECONDS` in the save under `Archipelago/<seed>_<slot>/`.

The first countdown checkpoint is written after the intro's timer reset settles. To verify restoration, finish the intro on two disposable slots with different countdowns and a 1× starting rate. Quit and relaunch, then reconnect to each slot and compare the overlay clock against the saved checkpoint minus elapsed time. Reconnect to the same slot to confirm the starting advance is only applied once.

## Player hub (`site/`)

The player hub is a Vite + TypeScript static application in `site/`, built with pnpm and deployed to GitHub Pages by `.github/workflows/pages.yml`.

```powershell
cd site
pnpm install
pnpm run dev        # local dev server
pnpm run lint       # Biome lint
pnpm run typecheck  # TypeScript strict check
pnpm run test       # Vitest unit tests
pnpm run build      # production build to site/dist
```

## Contributions

- Branch from `main` using `feature/<task>`.
- Install repository hooks once per clone:

```powershell
git config core.hooksPath .githooks
```

- Check outgoing commit messages, branches, and PR metadata with `python tools/check_public_metadata.py --text "..."`.
- Keep game binaries, assemblies, decompiled code, extracted assets, and save files out of Git.
