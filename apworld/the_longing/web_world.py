"""WebWorld definition and player documentation links for The Longing."""
from __future__ import annotations

from BaseClasses import Tutorial
from worlds.AutoWorld import WebWorld

from .options import OPTION_GROUPS


class LongingWebWorld(WebWorld):
    theme = "dirt"
    option_groups = OPTION_GROUPS
    tutorials = [
        Tutorial(
            tutorial_name="Setup Guide",
            description="A guide to setting up The Longing Decrypted for Archipelago multiworld play.",
            language="English",
            file_name="setup_en.md",
            link="setup/en",
            authors=["Decrypt"],
        )
    ]
