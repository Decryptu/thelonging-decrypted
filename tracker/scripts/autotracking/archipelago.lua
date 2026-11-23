-- Archipelago autotracking client for The Longing Decrypted PopTracker pack.

ScriptHost:LoadScript("scripts/autotracking/item_mapping.lua")
ScriptHost:LoadScript("scripts/autotracking/location_mapping.lua")

CUR_INDEX = -1

local GOAL_STAGE_MAP = {
    wake_the_king = 0,
    escape_surface = 1,
    secret_ending = 2,
    crystal_book_hunt = 3,
    any_ending = 4,
}

local DEPTH_STAGE_MAP = {
    vanilla = 0,
    progression = 1,
    full = 2,
}

local RESET_CODES = {
    { "mattock", "consumable" },
    { "secret_key", "toggle" },
    { "library_door_pass", "toggle" },
    { "surface_gate_pass", "toggle" },
    { "giant_mushroom_spores", "toggle" },
    { "cavern_map", "toggle" },
    { "progressive_walk_speed", "progressive" },
    { "progressive_waypoint_recall", "progressive" },
    { "recall_ring_1", "toggle" },
    { "recall_ring_2", "toggle" },
    { "recall_ring_3", "toggle" },
    { "cavern_moss", "consumable" },
    { "musical_instrument_part", "consumable" },
    { "drawing_paper", "consumable" },
    { "sulfur", "consumable" },
    { "books", "consumable" },
    { "crystals_total", "consumable" },
    { "crystal_white", "consumable" },
    { "crystal_rose", "consumable" },
    { "crystal_blue", "consumable" },
    { "crystal_purple", "toggle" },
    { "crystal_black", "toggle" },
    { "crystal_great_hall", "toggle" },
    { "crystal_tower", "toggle" },
    { "time_rate_boost", "consumable" },
    { "time_skips", "consumable" },
}

local function set_toggle(code, value)
    local obj = Tracker:FindObjectForCode(code)
    if obj ~= nil then
        obj.Active = (value == true or value == 1)
    end
end

local function set_stage(code, stage)
    local obj = Tracker:FindObjectForCode(code)
    if obj ~= nil and stage ~= nil then
        obj.CurrentStage = stage
    end
end

local function set_count(code, count)
    local obj = Tracker:FindObjectForCode(code)
    if obj ~= nil and count ~= nil then
        obj.AcquiredCount = count
    end
end

local function reset_items()
    for _, entry in ipairs(RESET_CODES) do
        local obj = Tracker:FindObjectForCode(entry[1])
        if obj ~= nil then
            if entry[2] == "toggle" then
                obj.Active = false
            elseif entry[2] == "consumable" then
                obj.AcquiredCount = obj.MinCount or 0
            elseif entry[2] == "progressive" then
                obj.CurrentStage = 0
                obj.Active = true
            end
        end
    end
end

local function reset_locations()
    for _, paths in pairs(LOCATION_MAPPING) do
        for _, path in ipairs(paths) do
            local obj = Tracker:FindObjectForCode(path)
            if obj ~= nil then
                if path:sub(1, 1) == "@" then
                    obj.AvailableChestCount = obj.ChestCount
                else
                    obj.Active = false
                end
            end
        end
    end
end

local function apply_slot_data(slot_data)
    SLOT_DATA = slot_data or {}
    SLOT_ENABLED_LOCATIONS = nil

    if type(slot_data) ~= "table" then
        return
    end

    if slot_data["goal"] ~= nil then
        local g = slot_data["goal"]
        if type(g) == "string" and GOAL_STAGE_MAP[g] ~= nil then
            set_stage("opt_goal", GOAL_STAGE_MAP[g])
        elseif type(g) == "number" then
            set_stage("opt_goal", g)
        end
    end

    if slot_data["shuffle_mattocks_and_keys"] ~= nil then
        set_toggle("opt_shuffle_mattocks_and_keys", slot_data["shuffle_mattocks_and_keys"])
    end

    if slot_data["shuffle_books"] ~= nil then
        local sb = slot_data["shuffle_books"]
        if type(sb) == "string" and DEPTH_STAGE_MAP[sb] ~= nil then
            set_stage("opt_shuffle_books", DEPTH_STAGE_MAP[sb])
        elseif type(sb) == "number" then
            set_stage("opt_shuffle_books", sb)
        end
    end

    if slot_data["shuffle_crystals"] ~= nil then
        local sc = slot_data["shuffle_crystals"]
        if type(sc) == "string" and DEPTH_STAGE_MAP[sc] ~= nil then
            set_stage("opt_shuffle_crystals", DEPTH_STAGE_MAP[sc])
        elseif type(sc) == "number" then
            set_stage("opt_shuffle_crystals", sc)
        end
    end

    if slot_data["shuffle_mushrooms_and_moss"] ~= nil then
        set_toggle("opt_shuffle_mushrooms_and_moss", slot_data["shuffle_mushrooms_and_moss"])
    end
    if slot_data["shuffle_home_comfort"] ~= nil then
        set_toggle("opt_shuffle_home_comfort", slot_data["shuffle_home_comfort"])
    end
    if slot_data["shuffle_instrument_parts"] ~= nil then
        set_toggle("opt_shuffle_instrument_parts", slot_data["shuffle_instrument_parts"])
    end
    if slot_data["shuffle_coal_and_sulfur"] ~= nil then
        set_toggle("opt_shuffle_coal_and_sulfur", slot_data["shuffle_coal_and_sulfur"])
    end
    if slot_data["shuffle_shop"] ~= nil then
        set_toggle("opt_shuffle_shop", slot_data["shuffle_shop"])
    end
    if slot_data["required_crystals"] ~= nil then
        set_count("opt_required_crystals", tonumber(slot_data["required_crystals"]) or 0)
    end
    if slot_data["required_books"] ~= nil then
        set_count("opt_required_books", tonumber(slot_data["required_books"]) or 0)
    end

    if type(slot_data["enabled_location_ids"]) == "table" then
        SLOT_ENABLED_LOCATIONS = {}
        for _, loc_id in ipairs(slot_data["enabled_location_ids"]) do
            local num = tonumber(loc_id)
            if num ~= nil then
                SLOT_ENABLED_LOCATIONS[num] = true
            end
        end
    end
end

function onClear(slot_data)
    if Tracker.BulkUpdate ~= nil then
        Tracker.BulkUpdate = true
    end
    CUR_INDEX = -1
    reset_items()
    reset_locations()
    apply_slot_data(slot_data)
    sync_recall_rings_from_progressive()
    sync_crystals_total()
    if Tracker.BulkUpdate ~= nil then
        Tracker.BulkUpdate = false
    end
end

function onItem(index, item_id, item_name, player_number)
    if index <= CUR_INDEX then
        return
    end
    CUR_INDEX = index

    local mapping = ITEM_MAPPING[item_id]
    if mapping == nil then
        return
    end

    local code = mapping[1]
    local kind = mapping[2]
    local obj = Tracker:FindObjectForCode(code)
    if obj == nil then
        return
    end

    if kind == "toggle" then
        obj.Active = true
    elseif kind == "consumable" then
        obj.AcquiredCount = obj.AcquiredCount + (obj.Increment or 1)
    elseif kind == "progressive" then
        local max_stage = (code == "progressive_walk_speed") and 4 or 3
        local current = obj.CurrentStage or 0
        if current < max_stage then
            obj.CurrentStage = current + 1
        end
    end

    if code == "progressive_waypoint_recall" then
        sync_recall_rings_from_progressive()
    elseif string.sub(code, 1, 8) == "crystal_" then
        sync_crystals_total()
    end
end

function onLocation(location_id, location_name)
    local paths = LOCATION_MAPPING[location_id]
    if paths == nil then
        return
    end
    for _, path in ipairs(paths) do
        local obj = Tracker:FindObjectForCode(path)
        if obj ~= nil then
            if path:sub(1, 1) == "@" then
                if obj.AvailableChestCount > 0 then
                    obj.AvailableChestCount = obj.AvailableChestCount - 1
                end
            else
                obj.Active = true
            end
        end
    end
end

function onScout(location_id, location_name, item_id, item_name, item_player)
end

function onBounce(json)
end

Archipelago:AddClearHandler("the_longing_clear", onClear)
Archipelago:AddItemHandler("the_longing_item", onItem)
Archipelago:AddLocationHandler("the_longing_location", onLocation)
if Archipelago.AddScoutHandler then
    Archipelago:AddScoutHandler("the_longing_scout", onScout)
end
if Archipelago.AddBouncedHandler then
    Archipelago:AddBouncedHandler("the_longing_bounce", onBounce)
end
