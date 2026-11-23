-- Logic rules for The Longing Decrypted PopTracker pack.
-- Mirrors apworld/the_longing/rules.py, regions.py, and locations.py.

SLOT_DATA = {}
SLOT_ENABLED_LOCATIONS = nil
SYNCING_RINGS = false
SYNCING_CRYSTALS = false

RECALL_RING_SCENES = {
    [1] = { "king", "home", "king_stairs", "entrance_hall", "treasury", "entrancehall2", "gallery", "entrancehall3" },
    [2] = { "hall_R_02", "hall_R_04", "hall_R_03", "moss_cliff", "hall_R_01", "hall_R_05", "face", "hall_R_06", "hall_R_07", "hall_R_08", "hall_L_08", "hall_L_01", "hall_L_02", "hall_L_03", "library", "hall_L_04", "grotto_01", "grotto_02", "entrance_library", "hall_L_06", "hall_L_07", "hall_L_09", "up_01", "hall L 06A", "up_07", "hall_L_05" },
    [3] = { "labyrinth 11A", "entrance_labyrinth", "labyrinth_01", "labyrinth_02", "labyrinth_03", "labyrinth_04", "labyrinth_05", "labyrinth_06", "labyrinth_07", "labyrinth_08", "labyrinth_09", "labyrinth_10", "labyrinth_11", "labyrinth_12", "labyrinth_13", "labyrinth_14", "labyrinth_15", "labyrinth_16", "entrance_eternity", "halls_of_eternity", "tower", "exit", "mine_01", "labyrinth 14A", "mine_03", "mine_04", "greatCave_complete", "hall_L06_behind", "brunnen" },
}

local function opt_bool(code)
    local obj = Tracker:FindObjectForCode(code)
    return obj ~= nil and obj.Active == true
end

local function opt_stage(code)
    local obj = Tracker:FindObjectForCode(code)
    if obj == nil then
        return 0
    end
    return obj.CurrentStage or 0
end

local function item_count(code)
    return Tracker:ProviderCountForCode(code)
end

-- Sync individual Recall Ring badges with Progressive Waypoint Recall stage (0..3)
function sync_recall_rings_from_progressive()
    if SYNCING_RINGS then
        return
    end
    SYNCING_RINGS = true
    local prog = Tracker:FindObjectForCode("progressive_waypoint_recall")
    local tier = prog and (prog.CurrentStage or 0) or 0
    for ring = 1, 3 do
        local badge = Tracker:FindObjectForCode("recall_ring_" .. ring)
        if badge ~= nil then
            badge.Active = (tier >= ring)
        end
    end
    SYNCING_RINGS = false
end

function sync_progressive_from_recall_ring(ring_code)
    if SYNCING_RINGS then
        return
    end
    SYNCING_RINGS = true
    local prog = Tracker:FindObjectForCode("progressive_waypoint_recall")
    local clicked = Tracker:FindObjectForCode(ring_code)
    if prog ~= nil and clicked ~= nil then
        local ring_num = tonumber(string.sub(ring_code, -1)) or 0
        local target_tier = ring_num
        if not clicked.Active then
            target_tier = math.max(0, ring_num - 1)
        end
        prog.CurrentStage = target_tier
        for ring = 1, 3 do
            local badge = Tracker:FindObjectForCode("recall_ring_" .. ring)
            if badge ~= nil then
                badge.Active = (target_tier >= ring)
            end
        end
    end
    SYNCING_RINGS = false
end

function has_recall_ring(ring_str)
    local ring = tonumber(ring_str) or 1
    local prog = Tracker:FindObjectForCode("progressive_waypoint_recall")
    local tier = prog and (prog.CurrentStage or 0) or 0
    return tier >= ring
end

-- Sync Crystals Collected (Total) from the seven crystal types
local CRYSTAL_CODES = {
    "crystal_white",
    "crystal_rose",
    "crystal_blue",
    "crystal_purple",
    "crystal_black",
    "crystal_great_hall",
    "crystal_tower",
}

function total_crystals_from_types()
    local total = 0
    for _, code in ipairs(CRYSTAL_CODES) do
        total = total + item_count(code)
    end
    return total
end

function sync_crystals_total()
    if SYNCING_CRYSTALS then
        return
    end
    SYNCING_CRYSTALS = true
    local total_obj = Tracker:FindObjectForCode("crystals_total")
    if total_obj ~= nil then
        total_obj.AcquiredCount = total_crystals_from_types()
    end
    SYNCING_CRYSTALS = false
end

function crystal_count()
    local total_obj = Tracker:FindObjectForCode("crystals_total")
    local manual_total = total_obj and (total_obj.AcquiredCount or 0) or 0
    return math.max(manual_total, total_crystals_from_types())
end

function book_count()
    return item_count("books")
end

-- Core progression predicates matching apworld/the_longing/rules.py
function has_mattocks(count_str)
    local count = tonumber(count_str) or 1
    if not opt_bool("opt_shuffle_mattocks_and_keys") then
        return true
    end
    return item_count("mattock") >= count
end

function has_secret_key()
    if not opt_bool("opt_shuffle_mattocks_and_keys") then
        return true
    end
    return item_count("secret_key") >= 1
end

function has_library_pass()
    if not opt_bool("opt_shuffle_mattocks_and_keys") then
        return true
    end
    return item_count("library_door_pass") >= 1
end

function has_surface_gate()
    if not opt_bool("opt_shuffle_mattocks_and_keys") then
        return true
    end
    return item_count("surface_gate_pass") >= 1
end

function has_giant_spores()
    if not (opt_bool("opt_shuffle_mushrooms_and_moss") or opt_bool("opt_shuffle_mattocks_and_keys")) then
        return true
    end
    return item_count("giant_mushroom_spores") >= 1
end

function can_reach_secret_tower()
    return has_surface_gate() and has_giant_spores()
end

function can_reach_mines()
    return has_surface_gate()
end

function has_moss_10()
    if not opt_bool("opt_shuffle_mushrooms_and_moss") then
        return true
    end
    return item_count("cavern_moss") >= 10
end

function has_sulfur()
    if opt_bool("opt_shuffle_coal_and_sulfur") then
        return item_count("sulfur") >= 1
    end
    return can_reach_mines() and has_mattocks(1)
end

function has_instrument_3()
    if not opt_bool("opt_shuffle_instrument_parts") then
        return true
    end
    return item_count("musical_instrument_part") >= 3
end

function has_paper()
    if not opt_bool("opt_shuffle_home_comfort") then
        return true
    end
    return item_count("drawing_paper") >= 1
end

-- Goal completion predicates matching apworld/the_longing/rules.py
function can_wake_king()
    return has_mattocks(1) and has_surface_gate()
end

function can_secret_ending()
    return can_reach_secret_tower() and has_secret_key()
end

function effective_required_crystals()
    if SLOT_DATA ~= nil and SLOT_DATA["required_crystals"] ~= nil then
        return tonumber(SLOT_DATA["required_crystals"]) or 0
    end
    local stage = opt_stage("opt_shuffle_crystals")
    local pool = 0
    if stage == 1 then
        pool = 7
    elseif stage == 2 then
        pool = 16
    end
    local req_obj = Tracker:FindObjectForCode("opt_required_crystals")
    local req = req_obj and (req_obj.AcquiredCount or 0) or 8
    return math.min(req, pool)
end

function effective_required_books()
    if SLOT_DATA ~= nil and SLOT_DATA["required_books"] ~= nil then
        return tonumber(SLOT_DATA["required_books"]) or 0
    end
    local stage = opt_stage("opt_shuffle_books")
    local pool = 0
    if stage == 1 then
        pool = 16
    elseif stage == 2 then
        pool = 31
    end
    local req_obj = Tracker:FindObjectForCode("opt_required_books")
    local req = req_obj and (req_obj.AcquiredCount or 0) or 12
    return math.min(req, pool)
end

function can_complete_hunt()
    local req_c = effective_required_crystals()
    local req_b = effective_required_books()
    if req_c == 0 and req_b == 0 then
        return has_library_pass() and can_reach_mines() and has_mattocks(1)
    end
    return crystal_count() >= req_c and book_count() >= req_b
end

-- Option-driven location visibility matching apworld/the_longing/locations.py
local function is_pool_enabled(meta)
    local pool = meta.pool
    local depth = meta.depth
    if pool == "mobility" then
        return true
    elseif pool == "tools" then
        return opt_bool("opt_shuffle_mattocks_and_keys")
    elseif pool == "books" then
        local st = opt_stage("opt_shuffle_books")
        if st == 2 then
            return true
        elseif st == 1 and depth == "progression" then
            return true
        end
        return false
    elseif pool == "crystals" then
        local st = opt_stage("opt_shuffle_crystals")
        if st == 2 then
            return true
        elseif st == 1 and depth == "progression" then
            return true
        end
        return false
    elseif pool == "mushrooms_moss" then
        return opt_bool("opt_shuffle_mushrooms_and_moss")
    elseif pool == "comfort" then
        return opt_bool("opt_shuffle_home_comfort")
    elseif pool == "instrument" then
        return opt_bool("opt_shuffle_instrument_parts")
    elseif pool == "coal_sulfur" then
        return opt_bool("opt_shuffle_coal_and_sulfur")
    end
    return false
end

local function compute_manual_enabled_set()
    local enabled = {}
    local count = 0
    for _, loc_id in ipairs(LOCATION_ORDER) do
        local meta = LOCATION_METADATA[loc_id]
        if meta ~= nil and meta.pool ~= "mobility" and is_pool_enabled(meta) then
            enabled[loc_id] = true
            count = count + 1
        end
    end
    -- Mirror locations.py fallback: guarantee at least 12 non-mobility checks if < 10 were enabled
    if count < 10 then
        for _, loc_id in ipairs(LOCATION_ORDER) do
            local meta = LOCATION_METADATA[loc_id]
            if meta ~= nil and (meta.pool == "tools" or meta.pool == "comfort") and not enabled[loc_id] then
                enabled[loc_id] = true
                count = count + 1
                if count >= 12 then
                    break
                end
            end
        end
    end
    for _, loc_id in ipairs(LOCATION_ORDER) do
        local meta = LOCATION_METADATA[loc_id]
        if meta ~= nil and meta.pool == "mobility" then
            enabled[loc_id] = true
        end
    end
    return enabled
end

function loc_enabled(id_str)
    local loc_id = tonumber(id_str)
    if loc_id == nil then
        return false
    end
    if SLOT_ENABLED_LOCATIONS ~= nil then
        return SLOT_ENABLED_LOCATIONS[loc_id] == true
    end
    local meta = LOCATION_METADATA[loc_id]
    if meta == nil then
        return false
    end
    if meta.pool == "mobility" then
        return true
    end
    local manual = compute_manual_enabled_set()
    return manual[loc_id] == true
end
