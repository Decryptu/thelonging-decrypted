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
      title: "Install The Longing on Steam (Windows)",
      detail:
        "Launch the game once so Steam finishes setup. The Windows build runs on 32-bit Unity 2018.4 (x86), so keep a backup of AppData\\LocalLow\\Studio Seufz\\The Longing before modding.",
    },
  ];

  if (method === "manager") {
    steps.push(
      {
        id: "manager_profile",
        title: "Create a dedicated mod profile in r2modman or Gale",
        detail:
          "Use a clean profile with BepInEx 5 x86 so your vanilla 400-day save stays untouched.",
      },
      {
        id: "manager_plugin",
        title: "Install the The Longing Decrypted plugin package",
        detail:
          "Once the first plugin release ships, import the release package into your profile so BepInEx loads the Archipelago client automatically.",
      },
    );
  } else {
    steps.push(
      {
        id: "manual_bepinex",
        title: "Extract BepInEx 5 (x86 / 32-bit) into the game folder",
        detail:
          "Important: The Longing.exe is 32-bit x86. Copy winhttp.dll, doorstop_config.ini, and the BepInEx folder next to The Longing.exe, then launch once to generate BepInEx/plugins.",
      },
      {
        id: "manual_plugin",
        title: "Drop the The Longing Decrypted plugin into BepInEx/plugins",
        detail:
          "Place the compiled plugin DLL and its Archipelago.MultiClient.Net dependency into BepInEx/plugins/TheLongingDecrypted/.",
      },
    );
  }

  if (role === "host") {
    steps.push(
      {
        id: "host_ap",
        title: "Install Archipelago 0.6.8+ and add the_longing.apworld",
        detail:
          "Drop the matching the_longing.apworld into Archipelago's custom_worlds folder and restart the Archipelago Launcher.",
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
      title: "Start a fresh randomizer slot and connect in-game",
      detail:
        "Open the in-game Archipelago overlay, enter the host address, port, slot name, and optional password, and verify the connection banner turns lapis blue.",
    },
    {
      id: "first_check",
      title: "Pick up your first check and confirm item delivery",
      detail:
        "Grab a nearby home or cavern check to verify checks report to the room and received items or time skips apply cleanly.",
    },
  );

  return steps;
}
