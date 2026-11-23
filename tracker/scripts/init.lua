-- Entry point for The Longing Decrypted PopTracker pack.

local variant = Tracker.ActiveVariantUID or "standard"
local items_only = string.find(variant, "itemsonly") ~= nil

-- Load items & seed options
Tracker:AddItems("items/items.json")
Tracker:AddItems("items/settings.json")

-- Load mappings and logic before maps & locations so visibility/access rules resolve cleanly
ScriptHost:LoadScript("scripts/autotracking/location_mapping.lua")
ScriptHost:LoadScript("scripts/logic/logic.lua")

-- Load Cavern Map & locations for full map variant
if not items_only then
    Tracker:AddMaps("maps/maps.json")
    Tracker:AddLocations("locations/locations.json")
end

-- Load layouts
Tracker:AddLayouts("layouts/items.json")
Tracker:AddLayouts("layouts/settings.json")
Tracker:AddLayouts("layouts/tracker.json")
Tracker:AddLayouts("layouts/broadcast.json")

-- Watchers to keep recall rings and total crystal counter synced in both manual and AP modes
ScriptHost:AddWatchForCode("sync_recall_rings", "progressive_waypoint_recall", sync_recall_rings_from_progressive)
ScriptHost:AddWatchForCode("sync_ring_1", "recall_ring_1", function() sync_progressive_from_recall_ring("recall_ring_1") end)
ScriptHost:AddWatchForCode("sync_ring_2", "recall_ring_2", function() sync_progressive_from_recall_ring("recall_ring_2") end)
ScriptHost:AddWatchForCode("sync_ring_3", "recall_ring_3", function() sync_progressive_from_recall_ring("recall_ring_3") end)

for _, crystal_code in ipairs({
    "crystal_white",
    "crystal_rose",
    "crystal_blue",
    "crystal_purple",
    "crystal_black",
    "crystal_great_hall",
    "crystal_tower",
}) do
    ScriptHost:AddWatchForCode("sync_" .. crystal_code, crystal_code, sync_crystals_total)
end

-- Enable Archipelago autotracking when supported by PopTracker
if PopVersion and PopVersion >= "0.18.0" then
    ScriptHost:LoadScript("scripts/autotracking/archipelago.lua")
end
