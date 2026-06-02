-- FRESTAS — OSC Transport Receiver
-- Listens for /frestas/play and /frestas/stop from Unity and drives Reaper transport.
-- Install: copy to your Reaper Scripts folder, run via Actions > Run script.
-- Runs as a background defer loop until toggled off in the Actions menu.

local LISTEN_PORT = 9000  -- must match OscSender.targetPort in Unity

-- ── OSC decode ────────────────────────────────────────────────────────────────

-- Extract the OSC address string (first null-terminated token starting with '/')
local function parseAddress(data)
    if data:sub(1,1) ~= "/" then return nil end
    local term = data:find("\0")
    return term and data:sub(1, term - 1) or nil
end

-- ── UDP socket ────────────────────────────────────────────────────────────────

local ok, socket = pcall(require, "socket")
if not ok then
    reaper.ShowMessageBox(
        "LuaSocket not found.\nCheck that Reaper's Lua environment includes LuaSocket.",
        "FRESTAS OSC", 0)
    return
end

local udp = socket.udp()
udp:settimeout(0)  -- non-blocking

local bound, err = udp:setsockname("*", LISTEN_PORT)
if not bound then
    reaper.ShowMessageBox(
        "Could not bind to port " .. LISTEN_PORT .. ":\n" .. tostring(err),
        "FRESTAS OSC", 0)
    return
end

reaper.ShowConsoleMsg("[FRESTAS OSC] listening on port " .. LISTEN_PORT .. "\n")

-- ── Transport commands ────────────────────────────────────────────────────────

-- Reaper action IDs for transport
local CMD_PLAY = 1007  -- Transport: Play
local CMD_STOP = 1016  -- Transport: Stop

local function dispatch(address)
    if address == "/frestas/play" then
        reaper.ShowConsoleMsg("[FRESTAS OSC] ← play\n")
        reaper.Main_OnCommand(CMD_PLAY, 0)
    elseif address == "/frestas/stop" then
        reaper.ShowConsoleMsg("[FRESTAS OSC] ← stop\n")
        reaper.Main_OnCommand(CMD_STOP, 0)
    end
end

-- ── Defer loop ────────────────────────────────────────────────────────────────

local function tick()
    local data = udp:receive()
    if data then
        local address = parseAddress(data)
        if address then dispatch(address) end
    end
    reaper.defer(tick)
end

tick()
