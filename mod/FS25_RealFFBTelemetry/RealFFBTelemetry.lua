-- FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
-- FS25 Real FFB - Telemetria
-- Projeto FS25-RealFFB (OptimusZuka). Escrito de raiz para este projeto.
-- Envia dados do veiculo do jogador local para a app RealFFB por named pipe
-- (\\.\pipe\FS25RealFFB). Nao altera o jogo; so le dados.
RealFFBTelemetry = {}

local PIPE = "\\\\.\\pipe\\FS25RealFFB"
local SEND_INTERVAL_MS = 8       -- ~120 Hz no maximo (limitado pelos FPS do jogo)
local CONNECT_INTERVAL_MS = 2000 -- tenta (re)ligar a cada 2 s

local st = { file = nil, t = 0, acc = 0, nextTry = 0, seq = 0, probed = nil }

local function isNum(v)
    return type(v) == "number" and v == v and v ~= math.huge and v ~= -math.huge
end

local function fmt(v)
    if isNum(v) then return string.format("%.4f", v) end
    return "nan"
end

local function clean(s)
    s = tostring(s or "")
    s = s:gsub("[|;,=\r\n]", " ")
    return s
end

local function try(fn)
    local ok, a, b, c = pcall(fn)
    if ok then return a, b, c end
    return nil
end

local function wheelValue(w, key)
    local v = w[key]
    if v == nil and type(w.physics) == "table" then v = w.physics[key] end
    return v
end

local function currentVehicle()
    if g_currentMission == nil or g_localPlayer == nil then return nil end
    if type(g_localPlayer.getCurrentVehicle) ~= "function" then return nil end
    return try(function() return g_localPlayer:getCurrentVehicle() end)
end

local function isPaused()
    if g_currentMission == nil or g_currentMission.isPaused == true then return true end
    if g_gui ~= nil and type(g_gui.getIsGuiVisible) == "function" then
        if try(function() return g_gui:getIsGuiVisible() end) == true then return true end
    end
    return false
end

local function steeringInput(vehicle)
    local rt = vehicle.rotatedTime
    if not isNum(rt) then return nil end
    if rt >= 0 and isNum(vehicle.maxRotTime) and vehicle.maxRotTime ~= 0 then
        return rt / vehicle.maxRotTime
    end
    if rt < 0 and isNum(vehicle.minRotTime) and vehicle.minRotTime ~= 0 then
        return -rt / vehicle.minRotTime
    end
    return nil
end

local function groundName(w)
    local p = w.physics
    if type(p) ~= "table" then return "na" end
    if p.hasGroundContact ~= true then return "air" end
    if p.hasWaterContact == true then return "water" end
    if p.hasSnowContact == true then return "snow" end
    if WheelsUtil == nil or type(WheelsUtil.getGroundType) ~= "function"
        or FieldGroundType == nil or WheelContactType == nil then return "na" end
    if p.densityType == nil or p.contact == nil or not isNum(p.groundDepth) then return "na" end
    local g = try(function()
        return WheelsUtil.getGroundType(p.densityType ~= FieldGroundType.NONE,
            p.contact ~= WheelContactType.GROUND, p.groundDepth)
    end)
    if g == nil then return "na" end
    if g == WheelsUtil.GROUND_ROAD then return "road" end
    if g == WheelsUtil.GROUND_FIELD then return "field" end
    if g == WheelsUtil.GROUND_HARD_TERRAIN then return "hard" end
    if g == WheelsUtil.GROUND_SOFT_TERRAIN then return "soft" end
    return "other"
end

-- Regista no log.txt que campos existem neste veiculo (para validar dados reais).
local function probe(vehicle)
    local function keys(t)
        if type(t) ~= "table" then return "(nil)" end
        local out = {}
        for k, v in pairs(t) do
            if type(v) ~= "function" and type(v) ~= "table" then
                table.insert(out, tostring(k) .. ":" .. type(v))
            elseif type(v) == "table" then
                table.insert(out, tostring(k) .. ":table")
            end
        end
        table.sort(out)
        return table.concat(out, " ")
    end
    print("[RealFFB] veiculo: " .. tostring(try(function() return vehicle:getFullName() end)))
    print("[RealFFB] rotatedTime=" .. tostring(vehicle.rotatedTime) .. " maxRotTime=" .. tostring(vehicle.maxRotTime)
        .. " minRotTime=" .. tostring(vehicle.minRotTime) .. " movingDirection=" .. tostring(vehicle.movingDirection))
    print("[RealFFB] getTotalMass=" .. tostring(type(vehicle.getTotalMass)) .. " getLastSpeed=" .. tostring(type(vehicle.getLastSpeed)))
    local spec = vehicle.spec_wheels
    if spec ~= nil and type(spec.wheels) == "table" and spec.wheels[1] ~= nil then
        local w = spec.wheels[1]
        print("[RealFFB] wheel[1]: " .. keys(w))
        print("[RealFFB] wheel[1].physics: " .. keys(w.physics))
        local ni = w.netInfo or (type(w.physics) == "table" and w.physics.netInfo)
        print("[RealFFB] wheel[1].netInfo: " .. keys(ni))
    else
        print("[RealFFB] veiculo sem spec_wheels")
    end
end


-- ------------------------------------------------------------------ ponte de comandos
-- A app RealFFB tem a base Fanatec em exclusivo (para o FFB). Por isso o jogo recebe
-- direcao e pedais daqui: a cada frame le-se uma linha de \\.\pipe\FS25RealFFB_In.
local PIPE_IN = "\\\\.\\pipe\\FS25RealFFB_In"
local inp = { active = false, steer = 0, thr = 0, brk = 0, clu = 0, seq = -1, lastSeq = -1, lastChange = 0, probed = nil, mode = "" }

local diag = { ok = 0, fail = 0, bad = 0, nextPrint = 5000, prints = 0, lastLine = "" }

local inputPath = nil

local function getInputPath()
    if inputPath ~= nil then return inputPath end
    local dir = g_modSettingsDirectory
    if type(dir) ~= "string" and type(getUserProfileAppPath) == "function" then
        dir = getUserProfileAppPath() .. "modSettings/"
    end
    if type(dir) ~= "string" then return nil end
    if string.sub(dir, -1) ~= "/" and string.sub(dir, -1) ~= "\\" then dir = dir .. "/" end
    inputPath = dir .. "FS25_RealFFB_input.xml"
    print("[RealFFB] ponte: ficheiro de comandos " .. inputPath)
    return inputPath
end

-- Le os comandos que a app escreve (direcao, pedais) com a API XML do jogo.
local function readInput(nowMs)
    if nowMs >= diag.nextPrint and diag.prints < 12 then
        diag.nextPrint = nowMs + 5000
        diag.prints = diag.prints + 1
        print(string.format("[RealFFB] ponte: lidas %d, falhadas %d, invalidas %d, ativa %s, dir %.2f acel %.2f trav %.2f %s",
            diag.ok, diag.fail, diag.bad, tostring(inp.active), inp.steer, inp.thr, inp.brk, diag.lastLine))
    end
    -- Se uma leitura falhar (a app estava a substituir o ficheiro), mantem-se o ultimo valor;
    -- so se desliga se nao houver dados novos ha mais de 0,5 s. (Evita o volante a piscar.)
    if (nowMs - inp.lastChange) > 500 then inp.active = false end
    local path = getInputPath()
    if path == nil or (fileExists ~= nil and not fileExists(path)) then diag.fail = diag.fail + 1; return end
    local id = loadXMLFile("RealFFBInput", path)
    if id == nil or id == 0 then diag.fail = diag.fail + 1; return end
    local seq = getXMLInt(id, "i#q")
    local act = getXMLInt(id, "i#a")
    local s = getXMLFloat(id, "i#s")
    local th = getXMLFloat(id, "i#t")
    local br = getXMLFloat(id, "i#b")
    local cl = getXMLFloat(id, "i#c")
    delete(id)
    if seq == nil or s == nil then diag.bad = diag.bad + 1; diag.lastLine = "xml incompleto"; return end
    diag.ok = diag.ok + 1
    diag.lastLine = ""
    if seq ~= inp.lastSeq then inp.lastSeq = seq; inp.lastChange = nowMs end
    inp.active = act == 1 and (nowMs - inp.lastChange) < 500
    inp.steer = s
    inp.thr = th or 0
    inp.brk = br or 0
    inp.clu = cl or 0
end

local function probeInput(vehicle)
    local spec = vehicle.spec_drivable
    local keys = {}
    if spec ~= nil and type(spec.lastInputValues) == "table" then
        for k, v in pairs(spec.lastInputValues) do table.insert(keys, tostring(k) .. ":" .. type(v)) end
    end
    table.sort(keys)
    print("[RealFFB] ponte: setSteeringInput=" .. type(vehicle.setSteeringInput)
        .. " setAccelerationPedalInput=" .. type(vehicle.setAccelerationPedalInput)
        .. " setBrakePedalInput=" .. type(vehicle.setBrakePedalInput)
        .. " spec_drivable=" .. tostring(spec ~= nil))
    print("[RealFFB] ponte: lastInputValues: " .. table.concat(keys, " "))
end

local function applyInput(vehicle)
    if not inp.active then return end
    local spec = vehicle.spec_drivable
    if spec == nil then return end
    if inp.probed ~= vehicle then inp.probed = vehicle; pcall(probeInput, vehicle) end
    local cat = nil
    if InputDevice ~= nil and InputDevice.CATEGORY ~= nil then cat = InputDevice.CATEGORY.WHEEL end
    local liv = spec.lastInputValues
    if type(vehicle.setSteeringInput) == "function" then
        pcall(vehicle.setSteeringInput, vehicle, inp.steer, true, cat)
    elseif type(liv) == "table" then
        liv.axisSteer = inp.steer
        liv.axisSteerIsAnalog = true
        liv.axisSteerDeviceCategory = cat
    end
    if type(vehicle.setAccelerationPedalInput) == "function" then
        pcall(vehicle.setAccelerationPedalInput, vehicle, inp.thr)
    elseif type(liv) == "table" then
        liv.axisAccelerate = inp.thr
    end
    if type(vehicle.setBrakePedalInput) == "function" then
        pcall(vehicle.setBrakePedalInput, vehicle, inp.brk)
    elseif type(liv) == "table" then
        liv.axisBrake = inp.brk
    end
end

function RealFFBTelemetry:buildLine()
    st.seq = st.seq + 1
    local parts = { "RFFB", "1", "seq=" .. st.seq, "t=" .. fmt(st.t) }
    local vehicle = currentVehicle()
    if vehicle == nil or isPaused() then
        table.insert(parts, "act=0")
        return table.concat(parts, "|")
    end
    table.insert(parts, "act=1")

    if st.probed ~= vehicle then
        st.probed = vehicle
        try(function() probe(vehicle) end)
    end

    table.insert(parts, "veh=" .. clean(try(function() return vehicle:getFullName() end) or "?"))
    if type(vehicle.getLastSpeed) == "function" then
        table.insert(parts, "spd=" .. fmt(try(function() return vehicle:getLastSpeed() end)))
    end
    if type(vehicle.getTotalMass) == "function" then
        table.insert(parts, "mass=" .. fmt(try(function() return vehicle:getTotalMass() end)))
    end
    table.insert(parts, "steer=" .. fmt(steeringInput(vehicle)))
    table.insert(parts, "dir=" .. fmt(vehicle.movingDirection))
    local ms = vehicle.spec_motorized
    if ms ~= nil and ms.motor ~= nil then
        local motor = ms.motor
        local rpm = try(function() return motor:getLastModulatedMotorRpm() end)
        if not isNum(rpm) then rpm = try(function() return motor:getLastMotorRpm() end) end
        local load = try(function() return motor:getSmoothLoadPercentage() end)
        local on = false
        if type(vehicle.getIsMotorStarted) == "function" then on = try(function() return vehicle:getIsMotorStarted() end) == true end
        table.insert(parts, "rpm=" .. fmt(rpm))
        table.insert(parts, "load=" .. fmt(load))
        table.insert(parts, "eng=" .. (on and "1" or "0"))
    end

    local node = vehicle.components ~= nil and vehicle.components[1] ~= nil and vehicle.components[1].node or nil
    if node ~= nil and node ~= 0 and (entityExists == nil or entityExists(node)) then
        if getLinearVelocity ~= nil and worldDirectionToLocal ~= nil then
            local x, y, z = try(function() return getLinearVelocity(node) end)
            if isNum(x) and isNum(y) and isNum(z) then
                local lx, ly, lz = worldDirectionToLocal(node, x, y, z)
                table.insert(parts, "vx=" .. fmt(lx)); table.insert(parts, "vy=" .. fmt(ly)); table.insert(parts, "vz=" .. fmt(lz))
            end
        end
        if getAngularVelocity ~= nil and worldDirectionToLocal ~= nil then
            local x, y, z = try(function() return getAngularVelocity(node) end)
            if isNum(x) and isNum(y) and isNum(z) then
                local lx, ly, lz = worldDirectionToLocal(node, x, y, z)
                table.insert(parts, "wx=" .. fmt(lx)); table.insert(parts, "wy=" .. fmt(ly)); table.insert(parts, "wz=" .. fmt(lz))
            end
        end
    end

    local wheels = {}
    local spec = vehicle.spec_wheels
    if spec ~= nil and type(spec.wheels) == "table" then
        for i, w in ipairs(spec.wheels) do
            if i > 16 then break end
            local contact = wheelValue(w, "hasGroundContact") == true and "1" or "0"
            local ni = w.netInfo or (type(w.physics) == "table" and w.physics.netInfo) or nil
            local susp = ni ~= nil and ni.y or nil
            local steer = wheelValue(w, "steeringAngle")
            table.insert(wheels, contact .. "," .. groundName(w) .. "," .. fmt(susp) .. "," .. fmt(steer))
        end
    end
    table.insert(parts, "w=" .. table.concat(wheels, ";"))
    return table.concat(parts, "|")
end

function RealFFBTelemetry:close()
    if st.file ~= nil then
        local f = st.file
        st.file = nil
        pcall(function() f:close() end)
    end
end

function RealFFBTelemetry:tryConnect()
    -- O servidor da app aceita 1 cliente. Se a ligacao atual estiver viva, a abertura
    -- falha (pipe ocupado). Se a app foi reiniciada, abre-se a nova e troca-se.
    local f = io.open(PIPE, "w")
    if f ~= nil then
        self:close()
        st.file = f
        print("[RealFFB] ligado a app RealFFB")
    end
end

function RealFFBTelemetry:update(dt)
    if g_dedicatedServer ~= nil then return end
    if not isNum(dt) or dt <= 0 then return end
    st.t = st.t + dt
    st.acc = st.acc + dt
    pcall(function()
        readInput(st.t)
        local v = currentVehicle()
        if v ~= nil and not isPaused() then applyInput(v) end
    end)
    if st.t >= st.nextTry then
        st.nextTry = st.t + CONNECT_INTERVAL_MS
        self:tryConnect()
    end
    if st.acc < SEND_INTERVAL_MS or st.file == nil then return end
    st.acc = 0
    local line = self:buildLine()
    local ok = pcall(function()
        local r1, r2 = st.file:write(line .. "\n")
        if r1 == false or r2 ~= nil then error(tostring(r2)) end
        st.file:flush()
    end)
    if not ok then self:close() end
end

function RealFFBTelemetry:deleteMap()
    self:close()
end

-- Diagnostico: procura no motor do jogo funcoes de force feedback acessiveis a mods.
local function probeEngineFFB()
    local pats = { "orceFeedback", "ForceFeed", "FFB", "ffb", "ibration", "umble", "aptic" }
    local function match(n)
        for _, p in ipairs(pats) do if string.find(n, p, 1, true) then return true end end
        return false
    end
    local found = {}
    for k, v in pairs(_G) do
        if type(k) == "string" then
            if type(v) == "function" and match(k) then
                table.insert(found, k)
            elseif type(v) == "table" and k ~= "_G" then
                local ok = pcall(function()
                    for k2, v2 in pairs(v) do
                        if type(k2) == "string" and match(k2) then
                            table.insert(found, k .. "." .. k2 .. ":" .. type(v2))
                        end
                    end
                end)
            end
        end
    end
    table.sort(found)
    print("[RealFFB] FFB-probe: " .. #found .. " nomes encontrados")
    local line = {}
    for i, n in ipairs(found) do
        table.insert(line, n)
        if #line == 12 or i == #found then
            print("[RealFFB] FFB-probe: " .. table.concat(line, " "))
            line = {}
        end
    end
end

function RealFFBTelemetry:loadMap(name)
    pcall(probeEngineFFB)
end

addModEventListener(RealFFBTelemetry)
