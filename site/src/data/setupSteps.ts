export type PlayerRole = "join" | "host";
export type InstallMethod = "manager" | "manual";

export interface SetupStep {
  id: string;
  title: string;
  detail: string;
}

export function getSetupSteps(role: PlayerRole, method: InstallMethod): SetupStep[] {
  const steps: SetupStep[] = [
    {
      id: "own_game",
      title: "Install The Longing on Steam (Windows) and back up your save",
      detail:
        "Launch once so Steam finishes setup. Copy %USERPROFILE%\\AppData\\LocalLow\\Studio Seufz\\The Longing somewhere safe first. Randomizer saves stay isolated under Archipelago\\<seed>_<slot>\\, leaving your vanilla 400-day save untouched.",
    },
  ];

  if (method === "manager") {
    steps.push(
      {
        id: "manager_profile",
        title: "Create a dedicated BepInEx 5 x86 profile in r2modman or Gale",
        detail:
          "The Longing.exe is 32-bit Unity 2018.4 (x86). Use a clean BepInEx 5 x86 profile so your vanilla installation stays separate.",
      },
      {
        id: "manager_plugin",
        title: "Import TheLongingDecrypted-v1.1.0-BepInEx5-x86.zip",
        detail:
          "Download the v1.1.0 plugin zip from GitHub Releases and import it into your profile so TheLongingDecrypted.dll, Archipelago.MultiClient.Net.dll, and Newtonsoft.Json.dll load automatically.",
      },
    );
  } else {
    steps.push(
      {
        id: "manual_bepinex",
        title: "Extract BepInEx 5 (x86 / 32-bit) into the game folder",
        detail:
          "Important: The Longing.exe is 32-bit x86 (64-bit BepInEx will not load). Copy winhttp.dll, doorstop_config.ini, and BepInEx next to The Longing.exe, then launch once.",
      },
      {
        id: "manual_plugin",
        title: "Extract TheLongingDecrypted-v1.1.0-BepInEx5-x86.zip into the game folder",
        detail:
          "Download the v1.1.0 plugin archive from GitHub Releases and place TheLongingDecrypted.dll, Archipelago.MultiClient.Net.dll, and Newtonsoft.Json.dll inside BepInEx/plugins/TheLongingDecrypted/.",
      },
    );
  }

  if (role === "host") {
    steps.push(
      {
        id: "host_ap",
        title: "Install Archipelago 0.6.8+ and add the_longing.apworld",
        detail:
          "Drop the matching v1.1.0 the_longing.apworld into Archipelago's custom_worlds folder and restart the Archipelago Launcher.",
      },
      {
        id: "host_yaml",
        title: "Collect every player's YAML scroll",
        detail:
          "Use the Player YAML screen to craft your own player file, gather your friends' YAMLs in one folder, and run ArchipelagoGenerate.",
      },
      {
        id: "host_room",
        title: "Host the generated multiworld archive",
        detail:
          "Upload the .zip output to archipelago.gg or run ArchipelagoServer locally, then share the server address, port, and slot names.",
      },
    );
  } else {
    steps.push({
      id: "join_yaml",
      title: "Generate your YAML scroll and send it to the host",
      detail:
        "Pick a preset in the Player YAML screen, enter your exact player name, download the .yaml file, and hand it to whoever is generating the seed.",
    });
  }

  steps.push(
    {
      id: "connect_room",
      title: "Connect from the title screen",
      detail:
        "Choose Far worlds on the title screen (or press F8), enter the server, port, player name, and optional password, and choose Listen. Each seed + slot gets its own isolated save and countdown checkpoint; return to the title screen before switching slots.",
    },
    {
      id: "first_check",
      title: "Explore, mark waypoints, and Shift-click Go or Home to recall",
      detail:
        "Grab a cavern check or enter a discovery room to confirm live syncing. Record waypoints in the native waypoint panel and Shift-click Go or Home once Progressive Waypoint Recall unlocks.",
    },
    {
      id: "tracker_optional",
      title: "(Optional) Load PopTracker or Universal Tracker",
      detail:
        "Drop the_longing_poptracker.zip from GitHub Releases into PopTracker's packs folder, or use Universal Tracker directly with the_longing.apworld (no local YAML needed) to autotrack checks, mobility, and recall rings.",
    },
  );

  return steps;
}
