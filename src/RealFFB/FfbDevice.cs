// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - controlador da base (DirectInput). Uma instancia = uma base.
// Efeitos de condicao (mola, amortecedor, atrito) sao calculados pela propria base a ~1 kHz;
// a forca constante e o seno levam as componentes dinamicas (pancadas, textura).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FS25RealFFB
{
    internal sealed class FfbDevice : IWheelOutput, IEffectSink
    {
        private const int SlotSpring = 0, SlotDamper = 1, SlotFriction = 2, SlotConst = 3, SlotSine = 4, SlotEngine = 5;

        private readonly Action<string> log;
        private IDirectInput8W di;
        private IDirectInputDevice8W dev;
        private readonly IDirectInputEffect[] fx = new IDirectInputEffect[6];
        private readonly long[] cache = new long[6];
        private readonly List<IntPtr> allocs = new List<IntPtr>();
        private IntPtr dataFormat, stateBuf, effPtr, axesPtr, dirPtr, condBuf, constBuf, periodicBuf;
        private bool stopped;
        private int errorCount;
        private readonly List<DIDEVICEINSTANCEW> candidates = new List<DIDEVICEINSTANCEW>();
        private int current = -1;
        private IntPtr window;
        // relogio proprio: Environment.TickCount fica negativo apos ~25 dias ligado e as contas de intervalo falhavam
        private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        private long lastRecoverMs = -100000, lastReopenMs = -100000;
        private bool reopenLogged;
        private bool lostLogged;

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr hwnd, out int pid);

        private static string ForegroundProcess()
        {
            try
            {
                int pid;
                GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                return System.Diagnostics.Process.GetProcessById(pid).ProcessName;
            }
            catch { return "?"; }
        }

        public bool IsOpen { get; private set; }
        public string DeviceName { get; private set; }

        public FfbDevice(Action<string> log)
        {
            this.log = log;
            DeviceName = "-";
            ResetCache();
            BuildBuffers();
        }

        private void ResetCache() { for (int i = 0; i < cache.Length; i++) cache[i] = long.MinValue; }

        private IntPtr Alloc(int size)
        {
            IntPtr p = Marshal.AllocHGlobal(size);
            for (int i = 0; i < size; i++) Marshal.WriteByte(p, i, 0);
            allocs.Add(p);
            return p;
        }

        public const int AxisCount = 8, ButtonCount = 128, StateSize = AxisCount * 4 + ButtonCount;
        public static readonly string[] AxisNames = { "X", "Y", "Z", "Rx", "Ry", "Rz", "S0", "S1" };

        // Formato de dados: 8 eixos (X,Y,Z,Rx,Ry,Rz,2 sliders) + 32 botoes, todos opcionais.
        // O eixo X fica no offset 0 (e o eixo dos efeitos de FFB).
        private void BuildBuffers()
        {
            Guid[] guids = {
                new Guid("A36D02E0-C9F3-11CF-BFC7-444553540000"), new Guid("A36D02E1-C9F3-11CF-BFC7-444553540000"),
                new Guid("A36D02E2-C9F3-11CF-BFC7-444553540000"), new Guid("A36D02F4-C9F3-11CF-BFC7-444553540000"),
                new Guid("A36D02F5-C9F3-11CF-BFC7-444553540000"), new Guid("A36D02E3-C9F3-11CF-BFC7-444553540000"),
                new Guid("A36D02E4-C9F3-11CF-BFC7-444553540000"), new Guid("A36D02E4-C9F3-11CF-BFC7-444553540000") };
            int objs = AxisCount + ButtonCount;
            IntPtr odf = Alloc(24 * objs);
            for (int i = 0; i < AxisCount; i++)
            {
                IntPtr g = Alloc(16);
                Marshal.StructureToPtr(guids[i], g, false);
                Marshal.WriteIntPtr(odf, i * 24, g);
                Marshal.WriteInt32(odf, i * 24 + 8, i * 4);
                Marshal.WriteInt32(odf, i * 24 + 12, unchecked((int)0x80FFFF03));  // eixo, qualquer instancia, opcional
            }
            for (int j = 0; j < ButtonCount; j++)
            {
                int k = AxisCount + j;
                Marshal.WriteIntPtr(odf, k * 24, IntPtr.Zero);
                Marshal.WriteInt32(odf, k * 24 + 8, AxisCount * 4 + j);
                Marshal.WriteInt32(odf, k * 24 + 12, unchecked((int)0x80FFFF0C)); // botao, qualquer instancia, opcional
            }
            dataFormat = Alloc(32);
            Marshal.WriteInt32(dataFormat, 0, 32);
            Marshal.WriteInt32(dataFormat, 4, 24);
            Marshal.WriteInt32(dataFormat, 8, 1);
            Marshal.WriteInt32(dataFormat, 12, StateSize);
            Marshal.WriteInt32(dataFormat, 16, objs);
            Marshal.WriteIntPtr(dataFormat, 24, odf);
            stateBuf = Alloc(StateSize);
            effPtr = Alloc(Marshal.SizeOf(typeof(DIEFFECT)));
            axesPtr = Alloc(4);
            dirPtr = Alloc(4);
            condBuf = Alloc(24);
            constBuf = Alloc(4);
            periodicBuf = Alloc(16);
        }

        // ------------------------------------------------------------------ abrir
        public bool Open(IntPtr hwnd)
        {
            Guid iid = DI.IID_IDirectInput8W;
            int hr = DI.DirectInput8Create(DI.GetModuleHandle(null), 0x0800, ref iid, out di, IntPtr.Zero);
            if (hr < 0 || di == null) { log("DirectInput8Create failed " + DI.Hr(hr)); return false; }

            List<DIDEVICEINSTANCEW> ff = new List<DIDEVICEINSTANCEW>();
            EnumDevicesCallback cb = delegate(IntPtr p, IntPtr r)
            {
                ff.Add((DIDEVICEINSTANCEW)Marshal.PtrToStructure(p, typeof(DIDEVICEINSTANCEW)));
                return 1;
            };
            di.EnumDevices(DI.DI8DEVCLASS_GAMECTRL, cb, IntPtr.Zero, DI.DIEDFL_ATTACHEDONLY | DI.DIEDFL_FORCEFEEDBACK);
            GC.KeepAlive(cb);

            window = hwnd;
            candidates.Clear();
            // Ordem: Fanatec CSL DD, outros volantes conhecidos (Fanatec, Logitech), depois qualquer volante com FFB.
            foreach (DIDEVICEINSTANCEW d in ff) if (Vid(d) == 0x0EB7 && Pid(d) == 0x0020) candidates.Add(d);
            foreach (DIDEVICEINSTANCEW d in ff) if (!candidates.Contains(d) && KnownNm(Vid(d), Pid(d)) > 0) candidates.Add(d);
            foreach (DIDEVICEINSTANCEW d in ff) if (!candidates.Contains(d)) candidates.Add(d);
            if (candidates.Count == 0) { log("No force feedback wheel found."); return false; }

            Available.Clear();
            Dictionary<string, int> sameId = new Dictionary<string, int>();
            foreach (DIDEVICEINSTANCEW d in candidates)
            {
                string id = Vid(d).ToString("X4") + ":" + Pid(d).ToString("X4");
                bool seen = false;
                foreach (string[] a in Available) if (a[0] == id && a[2] == d.guidInstance.ToString()) seen = true;
                if (seen) continue;
                int n; sameId.TryGetValue(id, out n); n++; sameId[id] = n;
                // {VID:PID, nome, GUID de instancia}; uma segunda base igual fica "#2"
                Available.Add(new string[] { id, d.tszProductName + " (" + id + ")" + (n > 1 ? " #" + n : ""), d.guidInstance.ToString() });
            }
            // Volante escolhido pelo utilizador: so as interfaces dele (a recuperacao nunca salta para outro volante)
            if (!string.IsNullOrEmpty(Preferred))
            {
                List<DIDEVICEINSTANCEW> only = new List<DIDEVICEINSTANCEW>();
                foreach (DIDEVICEINSTANCEW d in candidates)
                    if ((Vid(d).ToString("X4") + ":" + Pid(d).ToString("X4")) == Preferred
                        && (string.IsNullOrEmpty(PreferredInstance) || d.guidInstance.ToString() == PreferredInstance)) only.Add(d);
                if (only.Count == 0 && !string.IsNullOrEmpty(PreferredInstance))
                    foreach (DIDEVICEINSTANCEW d in candidates)
                        if ((Vid(d).ToString("X4") + ":" + Pid(d).ToString("X4")) == Preferred) only.Add(d);
                if (only.Count > 0) { candidates.Clear(); candidates.AddRange(only); }
                else log("The chosen wheel (" + Preferred + ") is not connected: using the first one found.");
            }

            log("DirectInput: FFB wheels/interfaces: " + candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                DIDEVICEINSTANCEW c = candidates[i];
                if (TryOpen(c, hwnd))
                {
                    current = i;
                    DeviceName = string.Format("{0} ({1:X4}:{2:X4})", c.tszProductName, Vid(c), Pid(c));
                    OpenVid = Vid(c); OpenPid = Pid(c);
                    OpenInstance = c.guidInstance.ToString(); OpenProduct = c.tszProductName;
                    IsOpen = true;
                    log("Wheel opened: " + DeviceName);
                    return true;
                }
            }
            return false;
        }

        public string Preferred;   // VID:PID escolhido pelo utilizador ("" = automatico)
        public string PreferredInstance = "";   // 0.13: GUID de instancia (distingue duas bases iguais)
        public string OpenInstance { get; private set; }
        public string OpenProduct { get; private set; }
        private DeviceQuirks quirks = new DeviceQuirks();
        public DeviceQuirks Quirks { get { return quirks; } }
        private readonly Capabilities caps = new Capabilities();
        public Capabilities Caps { get { return caps; } }
        private readonly object sync = new object();
        private readonly int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        private bool OnOwner { get { return System.Threading.Thread.CurrentThread.ManagedThreadId == ownerThread; } }
        public readonly List<string[]> Available = new List<string[]>();   // {VID:PID, nome}
        public int OpenVid { get; private set; }
        public int OpenPid { get; private set; }

        // Torque de pico de fabrica (Nm) dos volantes conhecidos; 0 = desconhecido. Tabela em WheelCatalog.
        public static double KnownNm(int vid, int pid)
        {
            WheelInfo w = WheelCatalog.Find(vid, pid);
            return w.Known ? w.PeakNm : 0;
        }

        // Reforco da mola dos volantes com engrenagens (compensa o atrito interno). Definido pela app
        // conforme o catalogo; por omissao segue a marca, como antes da 0.10.0.
        public bool GearBoost = true;

        private static int Vid(DIDEVICEINSTANCEW d) { return (int)(BitConverter.ToUInt32(d.guidProduct.ToByteArray(), 0) & 0xFFFF); }
        private static int Pid(DIDEVICEINSTANCEW d) { return (int)(BitConverter.ToUInt32(d.guidProduct.ToByteArray(), 0) >> 16); }

        private bool TryOpen(DIDEVICEINSTANCEW inst, IntPtr hwnd)
        {
            Guid g = inst.guidInstance;
            // 0.13: particularidades por fabricante/modelo vem do catalogo (Devices/devices.ini), nao de "if (vid == ...)"
            CatalogEntry ce = DeviceCatalog.Find(Vid(inst), Pid(inst), inst.tszProductName);
            quirks = ce.Quirks;
            log("[ffb] catalog: " + (ce.Model.Length > 0 ? ce.Model : "unknown device") + " | validation " + ce.Validation + " | quirks " + quirks);
            int hr = di.CreateDevice(ref g, out dev, IntPtr.Zero);
            if (hr < 0 || dev == null) { log("CreateDevice failed " + DI.Hr(hr)); return false; }
            LogCapabilities(inst);
            hr = dev.SetDataFormat(dataFormat);
            if (hr < 0) { log("SetDataFormat failed " + DI.Hr(hr)); ReleaseDevice(); return false; }
            hr = dev.SetCooperativeLevel(hwnd, DI.DISCL_EXCLUSIVE | DI.DISCL_BACKGROUND);
            if (hr < 0) { log("SetCooperativeLevel failed " + DI.Hr(hr)); ReleaseDevice(); return false; }
            SetRange(-10000, 10000);
            SetRangeDevice(-10000, 10000);
            SetDword(DI.DIPROP_AUTOCENTER, 0);
            SetDword(DI.DIPROP_FFGAIN, 10000);
            hr = dev.Acquire();
            if (hr < 0)
            {
                log("Acquire failed " + DI.Hr(hr) + " (another program holds the wheel in exclusive mode?)");
                ReleaseDevice();
                return false;
            }
            log("[ffb] acquired in exclusive + background mode.");
            // Logitech: o RESET volta a ligar a mola de centragem do driver; usar STOPALL (limpa os efeitos
            // sem repor o estado de fabrica). Fanatec e outros: RESET como sempre.
            if (quirks.StopAllOnOpen) dev.SendForceFeedbackCommand(DI.DISFFC_STOPALL);
            else dev.SendForceFeedbackCommand(DI.DISFFC_RESET);
            if (quirks.AutocenterOffEachAcquire)
            {
                // Logitech: o RESET repoe a mola de centragem de fabrica do driver. Desligar de novo
                // (com o dispositivo libertado, como o DirectInput exige) e voltar a adquirir.
                dev.Unacquire();
                int ac = SetDword(DI.DIPROP_AUTOCENTER, 0);
                hr = dev.Acquire();
                log("[ffb] quirk AutocenterOffEachAcquire: driver auto-centering off -> " + (ac >= 0 ? "ok" : "refused " + DI.Hr(ac) + " (refused by the Logitech driver)") + ", re-acquire " + (hr >= 0 ? "ok" : DI.Hr(hr)));
                if (hr < 0) { ReleaseDevice(); return false; }
            }
            dev.SendForceFeedbackCommand(DI.DISFFC_SETACTUATORSON);
            if (quirks.AutocenterOffEachAcquire)
            {
                // alguns drivers so aplicam a propriedade com o dispositivo adquirido: tentar tambem assim
                int ac2 = SetDword(DI.DIPROP_AUTOCENTER, 0);
                log("[ffb] quirk: auto-centering off while acquired -> " + (ac2 >= 0 ? "ok" : "refused " + DI.Hr(ac2)));
            }

            fx[SlotSpring] = CreateCondition(DI.GUID_Spring, "Spring");
            fx[SlotDamper] = CreateCondition(DI.GUID_Damper, "Damper");
            fx[SlotFriction] = CreateCondition(DI.GUID_Friction, "Friction");
            Marshal.WriteInt32(constBuf, 0, 0);
            fx[SlotConst] = Create(DI.GUID_ConstantForce, constBuf, 4, "Constant");
            WritePeriodic(0, 50000);
            fx[SlotSine] = Create(DI.GUID_Sine, periodicBuf, 16, "Sine");
            WritePeriodic(0, 25000);
            fx[SlotEngine] = Create(DI.GUID_Sine, periodicBuf, 16, "Motor");
            StartAll();
            LogEffectStatus();
            ProbeCapabilities();
            SetupRaw();
            // 0.13: basta a constant force (sem efeitos de condicao usa-se o modo de binario calculado)
            return fx[SlotConst] != null;
        }

        private int SetDword(int prop, int value)
        {
            IntPtr p = Marshal.AllocHGlobal(20);
            try
            {
                Marshal.WriteInt32(p, 0, 20); Marshal.WriteInt32(p, 4, 16);
                Marshal.WriteInt32(p, 8, 0); Marshal.WriteInt32(p, 12, DI.DIPH_DEVICE);
                Marshal.WriteInt32(p, 16, value);
                return dev.SetProperty(new IntPtr(prop), p);
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        private void SetRangeDevice(int min, int max)
        {
            IntPtr p = Marshal.AllocHGlobal(24);
            try
            {
                Marshal.WriteInt32(p, 0, 24); Marshal.WriteInt32(p, 4, 16);
                Marshal.WriteInt32(p, 8, 0); Marshal.WriteInt32(p, 12, DI.DIPH_DEVICE);
                Marshal.WriteInt32(p, 16, min); Marshal.WriteInt32(p, 20, max);
                dev.SetProperty(new IntPtr(DI.DIPROP_RANGE), p);
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        private void SetRange(int min, int max)
        {
            IntPtr p = Marshal.AllocHGlobal(24);
            try
            {
                Marshal.WriteInt32(p, 0, 24); Marshal.WriteInt32(p, 4, 16);
                Marshal.WriteInt32(p, 8, 0); Marshal.WriteInt32(p, 12, DI.DIPH_BYOFFSET);
                Marshal.WriteInt32(p, 16, min); Marshal.WriteInt32(p, 20, max);
                dev.SetProperty(new IntPtr(DI.DIPROP_RANGE), p);
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        private void WriteEffect(IntPtr tsp, int cb)
        {
            DIEFFECT e = new DIEFFECT();
            e.dwSize = Marshal.SizeOf(typeof(DIEFFECT));
            e.dwFlags = DI.DIEFF_CARTESIAN | DI.DIEFF_OBJECTOFFSETS;
            e.dwDuration = DI.INFINITE;
            e.dwGain = 10000;
            e.dwTriggerButton = DI.DIEB_NOTRIGGER;
            e.cAxes = 1;
            e.rgdwAxes = axesPtr;
            e.rglDirection = dirPtr;
            e.lpEnvelope = IntPtr.Zero;
            e.cbTypeSpecificParams = cb;
            e.lpvTypeSpecificParams = tsp;
            Marshal.StructureToPtr(e, effPtr, false);
        }

        private void WriteCondition(int coef, int sat) { WriteCondition(coef, sat, 0); }

        private void WriteCondition(int coef, int sat, int offset)
        {
            Marshal.WriteInt32(condBuf, 0, offset);
            Marshal.WriteInt32(condBuf, 4, coef);
            Marshal.WriteInt32(condBuf, 8, coef);
            Marshal.WriteInt32(condBuf, 12, sat);
            Marshal.WriteInt32(condBuf, 16, sat);
            Marshal.WriteInt32(condBuf, 20, 0);
        }

        private void WritePeriodic(int mag, int periodUs)
        {
            Marshal.WriteInt32(periodicBuf, 0, mag);
            Marshal.WriteInt32(periodicBuf, 4, 0);
            Marshal.WriteInt32(periodicBuf, 8, 0);
            Marshal.WriteInt32(periodicBuf, 12, periodUs);
        }

        private IDirectInputEffect CreateCondition(Guid g, string name)
        {
            WriteCondition(0, 0);
            return Create(g, condBuf, 24, name);
        }

        private IDirectInputEffect Create(Guid g, IntPtr tsp, int cb, string name)
        {
            WriteEffect(tsp, cb);
            IDirectInputEffect e;
            int hr = dev.CreateEffect(ref g, effPtr, out e, IntPtr.Zero);
            if (hr < 0 || e == null) { log("[ffb] effect " + name + ": NOT SUPPORTED / not created " + DI.Hr(hr)); return null; }
            log("[ffb] effect " + name + ": created");
            return e;
        }

        private void StartAll()
        {
            for (int i = 0; i < fx.Length; i++)
            {
                if (fx[i] == null) continue;
                int hr = fx[i].Start(1, 0);
                if (hr < 0) { startFails++; log("[ffb] effect " + SlotNames[i] + ": start failed " + DI.Hr(hr)); }
            }
            stopped = false;
        }

        private static readonly string[] SlotNames = { "Spring", "Damper", "Friction", "Constant", "Sine", "Motor" };
        private int startFails, sendOk, sendFail, lastSendHr, recoveries;

        // Capacidades reportadas pelo driver (DIDEVCAPS): distingue "sem FFB" de "FFB presente".
        private void LogCapabilities(DIDEVICEINSTANCEW inst)
        {
            IntPtr caps = Marshal.AllocHGlobal(44);
            try
            {
                for (int i = 0; i < 44; i += 4) Marshal.WriteInt32(caps, i, 0);
                Marshal.WriteInt32(caps, 0, 44);
                int hr = dev.GetCapabilities(caps);
                if (hr < 0) { log("[ffb] GetCapabilities failed " + DI.Hr(hr)); return; }
                int flags = Marshal.ReadInt32(caps, 4);
                log(string.Format("[ffb] device {0:X4}:{1:X4} '{2}' | axes {3} buttons {4} | force feedback: {5} | FF sample period {6} us | FF driver 0x{7:X}",
                    Vid(inst), Pid(inst), inst.tszProductName, Marshal.ReadInt32(caps, 12), Marshal.ReadInt32(caps, 16),
                    (flags & 0x100) != 0 ? "YES" : "NO", Marshal.ReadInt32(caps, 24), Marshal.ReadInt32(caps, 40)));
            }
            catch (Exception ex) { log("[ffb] capabilities: " + ex.Message); }
            finally { Marshal.FreeHGlobal(caps); }
        }

        // Estado de cada efeito depois de iniciado (DIEGES_PLAYING = 1).
        private void LogEffectStatus()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder("[ffb] effects:");
            for (int i = 0; i < fx.Length; i++)
            {
                if (fx[i] == null) { sb.Append(" " + SlotNames[i] + "=missing"); continue; }
                int st;
                int hr = fx[i].GetEffectStatus(out st);
                sb.Append(" " + SlotNames[i] + "=" + (hr < 0 ? "status-error" : ((st & 1) != 0 ? "playing" : "NOT playing")));
            }
            log(sb.ToString());
        }

        // Resumo para o registo periodico: envios aceites/recusados pelo driver.
        public string Health
        {
            get
            {
                int missing = 0;
                foreach (IDirectInputEffect e in fx) if (e == null) missing++;
                return string.Format("ffb sent ok {0} fail {1}{2} | start fails {3} | effects missing {4} | re-acquired {5}",
                    sendOk, sendFail, sendFail > 0 ? " (last " + DI.Hr(lastSendHr) + ")" : "", startFails, missing, recoveries);
            }
        }

        // ------------------------------------------------------------------ atualizacao
        private void Apply(int slot, long key, IntPtr tsp, int cb)
        {
            IDirectInputEffect e = fx[slot];
            if (e == null)
            {
                // 0.10.0: se uma recuperacao falhou, os efeitos ficaram a null e antes nada voltava a tentar
                // (FFB morto ate reiniciar a app, com o cabecalho a dizer "conectado").
                if (needReopen) Recover();
                return;
            }
            if (cache[slot] == key) return;
            WriteEffect(tsp, cb);
            int hr = e.SetParameters(effPtr, DI.DIEP_TYPESPECIFICPARAMS);
            if (hr < 0)
            {
                sendFail++; lastSendHr = hr;
                errorCount++;
                if (errorCount == 1 || errorCount % 2000 == 0)
                {
                    if (hr == unchecked((int)0x80040205))
                        log("CONFLICT: another program (probably FS25) took exclusive FFB control of the wheel. HidHide must hide the wheel from FS25 (installer or 'Configure devices') and the game's own force feedback must be off.");
                    else
                        log("SetParameters failed " + DI.Hr(hr) + " - recovering the wheel");
                }
                Recover();
                return;
            }
            sendOk++;
            cache[slot] = key;
        }

        // Recuperacao: volta a adquirir; se outro programa tiver esta interface,
        // tenta a outra interface HID da base (a CSL DD expoe duas).
        // true quando o volante esta aberto mas sem efeitos (recuperacao falhada): a app volta a tentar.
        private bool needReopen;
        public bool Responding { get { return IsOpen && !needReopen; } }

        private void Recover()
        {
            long now = clock.ElapsedMilliseconds;
            if (now - lastRecoverMs < 500) return;
            lastRecoverMs = now;
            if (dev == null || needReopen)
            {
                // reabrir do zero a interface atual (efeitos incluidos), tentando de novo a cada ~2 s
                if (now - lastReopenMs < 2000) return;
                lastReopenMs = now;
                ReleaseDevice();
                if (current >= 0 && current < candidates.Count && TryOpen(candidates[current], window))
                {
                    needReopen = false;
                    recoveries++;
                    ResetCache();
                    log("[ffb] wheel reopened after a failed recovery (interface " + (current + 1) + ").");
                }
                else
                {
                    ReleaseDevice();
                    needReopen = true;
                    if (!reopenLogged) { reopenLogged = true; log("[ffb] wheel not responding - retrying every 2 s (is another program holding it?)."); }
                }
                if (!needReopen) reopenLogged = false;
                return;
            }
            // Liberta antes de voltar a adquirir: Acquire() num dispositivo que o DirectInput ainda
            // considera adquirido devolve S_FALSE e nao recupera a exclusividade (CONFLICT sem fim).
            dev.Unacquire();
            if (quirks.AutocenterOffEachAcquire) SetDword(DI.DIPROP_AUTOCENTER, 0);
            int hr = dev.Acquire();
            if (hr >= 0)
            {
                recoveries++;
                if (lostLogged || recoveries <= 3 || recoveries % 100 == 0) log("[ffb] wheel re-acquired (interface " + (current + 1) + ", recovery " + recoveries + ").");
                lostLogged = false;
                ResetCache();
                StartAll();
                LogEffectStatus();
                return;
            }
            if (!lostLogged)
            {
                lostLogged = true;
                log("Wheel busy with another program (" + DI.Hr(hr) + "). Foreground window: " + ForegroundProcess());
            }
            if (candidates.Count > 1)
            {
                int next = (current + 1) % candidates.Count;
                ReleaseDevice();
                if (TryOpen(candidates[next], window))
                {
                    current = next;
                    log("Switched to wheel interface " + (next + 1) + ".");
                    lostLogged = false;
                    ResetCache();
                }
                else
                {
                    log("Interface " + (next + 1) + " is busy too.");
                    ReleaseDevice();
                    // 0.10.0: o resultado era ignorado; se falhar, fica marcado para nova tentativa
                    if (TryOpen(candidates[current], window)) ResetCache();
                    else { ReleaseDevice(); needReopen = true; }
                }
            }
        }

        private void ReleaseDevice()
        {
            ReleaseRaw();
            try
            {
                for (int i = 0; i < fx.Length; i++)
                {
                    if (fx[i] != null) { try { fx[i].Stop(); } catch { } Marshal.ReleaseComObject(fx[i]); fx[i] = null; }
                }
                if (dev != null) { dev.Unacquire(); Marshal.ReleaseComObject(dev); dev = null; }
            }
            catch { }
        }

        public void SetSpring(int coef, int sat) { lock (sync) SetCondition(SlotSpring, coef, sat); }
        public void SetSpring(int coef, int sat, int offset) { if (!OnOwner) return; lock (sync) SetSpringCore(coef, sat, offset); }
        public void SetSine(int mag, int periodUs) { if (!OnOwner) return; lock (sync) SetSineCore(mag, periodUs); }
        public void SetEngine(int mag, int periodUs) { if (!OnOwner) return; lock (sync) SetEngineCore(mag, periodUs); }
        // constant force: tambem pode vir da thread do modo torque (chamada direta a tabela COM, sem passar pela RCW)
        public void SetConstant(int mag)
        {
            if (OnOwner) { lock (sync) SetConstantCore(mag); return; }
            lock (sync) RawConstant(mag);
        }

        // Mola com centro deslocado (o volante alinha com a trajetoria real do trator).
        private void SetSpringCore(int coef, int sat, int offset)
        {
            if (!IsOpen) return;
            if (stopped) StartAll();
            coef = Clamp(coef, 0, 10000); sat = Clamp(sat, 0, 10000); offset = Clamp(offset, -10000, 10000);
            if (quirks.SpringFloor1)
            {
                // Logitech: uma mola da aplicacao a zero faz o driver voltar a mola de centragem dele
                // (centrava parado). Manter sempre uma mola minima ativa (forca desprezavel)...
                if (coef <= 0 || sat <= 0) { coef = 1; sat = 1; }
                // ...e, a andar, compensar o atrito interno das engrenagens (forcas pequenas perdiam-se).
                else if (GearBoost && quirks.SpringBoost > 0) sat = Clamp(sat + quirks.SpringBoost, 0, 10000);
            }
            WriteCondition(coef, sat, offset);
            Apply(SlotSpring, ((long)coef << 40) ^ ((long)sat << 20) ^ (long)(offset & 0xFFFFF), condBuf, 24);
        }
        public void SetDamper(int coef, int sat) { if (!OnOwner) return; lock (sync) SetCondition(SlotDamper, coef, sat); }
        public void SetFriction(int coef, int sat) { if (!OnOwner) return; lock (sync) SetCondition(SlotFriction, coef, sat); }

        private void SetCondition(int slot, int coef, int sat)
        {
            if (!IsOpen) return;
            if (stopped) StartAll();
            coef = Clamp(coef, 0, 10000); sat = Clamp(sat, 0, 10000);
            WriteCondition(coef, sat);
            Apply(slot, ((long)coef << 32) | (uint)sat, condBuf, 24);
        }

        private void SetConstantCore(int mag)
        {
            if (!IsOpen) return;
            if (stopped) StartAll();
            mag = Clamp(mag, -10000, 10000);
            Marshal.WriteInt32(constBuf, 0, mag);
            Apply(SlotConst, mag, constBuf, 4);
        }

        // Vibracao do motor: segundo seno, independente da textura do piso.
        private void SetEngineCore(int mag, int periodUs)
        {
            if (!IsOpen) return;
            if (stopped) StartAll();
            mag = Clamp(mag, 0, 10000);
            periodUs = Clamp(periodUs, 6000, 200000);
            WritePeriodic(mag, periodUs);
            Apply(SlotEngine, ((long)mag << 32) | (uint)periodUs, periodicBuf, 16);
        }

        private void SetSineCore(int mag, int periodUs)
        {
            if (!IsOpen) return;
            if (stopped) StartAll();
            mag = Clamp(mag, 0, 10000);
            periodUs = Clamp(periodUs, 10000, 1000000);
            WritePeriodic(mag, periodUs);
            Apply(SlotSine, ((long)mag << 32) | (uint)periodUs, periodicBuf, 16);
        }

        public void Commit() { }

        private readonly int[] axes = new int[AxisCount];
        private readonly bool[] buttons = new bool[ButtonCount];
        public int[] Axes { get { return axes; } }
        public bool[] Buttons { get { return buttons; } }

        // Le volante, pedais e botoes (a app tem a base em exclusivo).
        public bool ReadState()
        {
            if (!OnOwner) { int p; return Poll(out p); }
            lock (sync) return ReadStateCore();
        }

        private bool ReadStateCore()
        {
            if (dev == null) return false;
            dev.Poll();
            if (dev.GetDeviceState(StateSize, stateBuf) < 0) { dev.Acquire(); return false; }
            for (int i = 0; i < AxisCount; i++) axes[i] = Marshal.ReadInt32(stateBuf, i * 4);
            for (int j = 0; j < ButtonCount; j++) buttons[j] = (Marshal.ReadByte(stateBuf, AxisCount * 4 + j) & 0x80) != 0;
            return true;
        }

        public int ReadPosition() { return axes[0]; }

        // Paragem total imediata (botao PARAR, Esc, erro, fecho).
        public void EmergencyStop()
        {
            // 0.13: pode ser chamado pelo watchdog noutra thread enquanto a janela esta bloqueada.
            if (!OnOwner)
            {
                bool got = System.Threading.Monitor.TryEnter(sync, 50);
                try { RawStopAll(); }
                finally { if (got) System.Threading.Monitor.Exit(sync); }
                stopped = true;
                ResetCache();
                return;
            }
            lock (sync) EmergencyStopCore();
        }

        private void EmergencyStopCore()
        {
            if (dev == null) return;
            try
            {
                foreach (IDirectInputEffect e in fx) if (e != null) e.Stop();
                dev.SendForceFeedbackCommand(DI.DISFFC_STOPALL);
            }
            catch { }
            stopped = true;
            ResetCache();
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        public void Dispose()
        {
            EmergencyStop();
            ReleaseRaw();
            try
            {
                if (dev != null) dev.SendForceFeedbackCommand(DI.DISFFC_RESET);
                for (int i = 0; i < fx.Length; i++)
                {
                    if (fx[i] != null) { Marshal.ReleaseComObject(fx[i]); fx[i] = null; }
                }
                if (dev != null) { dev.Unacquire(); Marshal.ReleaseComObject(dev); dev = null; }
                if (di != null) { Marshal.ReleaseComObject(di); di = null; }
            }
            catch { }
            foreach (IntPtr p in allocs) Marshal.FreeHGlobal(p);
            allocs.Clear();
            IsOpen = false;
        }

        // ================================================================== 0.13: capacidades
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int EnumEffectsCallback(IntPtr pdei, IntPtr pvRef);

        // Le o que o dispositivo declara (EnumEffects, propriedades) e o estado real de cada efeito criado.
        // "Declarado" nao prova que funciona: o estado "Playing" e o maximo que o software consegue verificar;
        // a confirmacao fisica vem do teste guiado (Hardware > Testar o meu volante).
        private void ProbeCapabilities()
        {
            caps.Effects.Clear(); caps.DeclaredEffectNames.Clear();
            caps.ForceFeedback = true;
            try
            {
                IntPtr c = Marshal.AllocHGlobal(44);
                try
                {
                    for (int i = 0; i < 44; i += 4) Marshal.WriteInt32(c, i, 0);
                    Marshal.WriteInt32(c, 0, 44);
                    if (dev.GetCapabilities(c) >= 0)
                    {
                        caps.ForceFeedback = (Marshal.ReadInt32(c, 4) & DI.DIDC_FORCEFEEDBACK) != 0;
                        caps.FfSamplePeriodUs = Marshal.ReadInt32(c, 24);
                        caps.FfMinTimeResolutionUs = Marshal.ReadInt32(c, 28);
                    }
                }
                finally { Marshal.FreeHGlobal(c); }

                List<Guid> declared = new List<Guid>();
                EnumEffectsCallback cb = delegate(IntPtr pdei, IntPtr pv)
                {
                    try
                    {
                        byte[] gb = new byte[16];
                        Marshal.Copy(new IntPtr(pdei.ToInt64() + 4), gb, 0, 16);
                        declared.Add(new Guid(gb));
                        caps.DeclaredEffectNames.Add(Marshal.PtrToStringUni(new IntPtr(pdei.ToInt64() + 32)));
                    }
                    catch { }
                    return 1;   // DIENUM_CONTINUE
                };
                IntPtr fp = Marshal.GetFunctionPointerForDelegate(cb);
                dev.EnumEffects(fp, IntPtr.Zero, 0 /* DIEFT_ALL */);
                GC.KeepAlive(cb);
                Guid[] g = { DI.GUID_ConstantForce, DI.GUID_Spring, DI.GUID_Damper, DI.GUID_Friction, DI.GUID_Sine };
                int[] slot = { SlotConst, SlotSpring, SlotDamper, SlotFriction, SlotSine };
                for (int i = 0; i < g.Length; i++)
                {
                    string n = Capabilities.Names[i];
                    if (declared.Contains(g[i])) caps.Mark(n, EffectStatus.Declared);
                    IDirectInputEffect e = fx[slot[i]];
                    if (e == null) { if (declared.Contains(g[i])) caps.Mark(n, EffectStatus.Failed); continue; }
                    caps.Mark(n, EffectStatus.Created);
                    int st;
                    if (e.GetEffectStatus(out st) >= 0 && (st & 1) != 0) caps.Mark(n, EffectStatus.Playing);
                }
                // ganho global: propriedade legivel?
                IntPtr pd = Marshal.AllocHGlobal(20);
                try
                {
                    Marshal.WriteInt32(pd, 0, 20); Marshal.WriteInt32(pd, 4, 16);
                    Marshal.WriteInt32(pd, 8, 0); Marshal.WriteInt32(pd, 12, DI.DIPH_DEVICE); Marshal.WriteInt32(pd, 16, 0);
                    caps.GainProperty = dev.GetProperty(new IntPtr(DI.DIPROP_FFGAIN), pd) >= 0;
                    Marshal.WriteInt32(pd, 16, 0);
                    caps.AutocenterProperty = dev.GetProperty(new IntPtr(DI.DIPROP_AUTOCENTER), pd) >= 0;
                }
                finally { Marshal.FreeHGlobal(pd); }
            }
            catch (Exception ex) { log("[ffb] capability probe: " + ex.Message); }
            log("[ffb] capabilities: " + caps.Summary() + (caps.DeclaredEffectNames.Count > 0 ? " | declared: " + string.Join(", ", caps.DeclaredEffectNames.ToArray()) : ""));
        }

        // ================================================================== 0.13: posicao (qualquer thread)
        public bool Poll(out int position)
        {
            position = 0;
            if (OnOwner)
            {
                lock (sync) { if (!ReadStateCore()) return false; position = axes[0]; return true; }
            }
            lock (sync)
            {
                if (rawDev == IntPtr.Zero || rawState == IntPtr.Zero) return false;
                try
                {
                    rawPoll(rawDev);
                    if (rawGetState(rawDev, StateSize, rawState) < 0) return false;
                    position = Marshal.ReadInt32(rawState, 0);
                    return true;
                }
                catch { return false; }
            }
        }

        // ================================================================== 0.13: chamadas diretas a tabela COM
        // As interfaces COM da DirectInput foram criadas na thread da janela (STA). Chamadas de outra thread
        // pela RCW do .NET teriam de ser "marshalled" (e a DirectInput nao regista proxies), por isso o
        // watchdog e o modo torque usam ponteiros crus para os metodos (sempre sob o mesmo lock).
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FnInt(IntPtr self, int a);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FnVoid(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FnIntPtr(IntPtr self, int a, IntPtr b);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FnPtrInt(IntPtr self, IntPtr a, int b);

        private IntPtr rawDev, rawState, rawEff, rawConstBuf;
        private readonly IntPtr[] rawFx = new IntPtr[6];
        private FnInt rawSendCmd; private FnVoid rawPoll; private FnIntPtr rawGetState;
        private FnPtrInt rawConstSet; private readonly FnVoid[] rawFxStop = new FnVoid[6];

        private static Delegate VFn(IntPtr obj, int index, Type t)
        {
            IntPtr vt = Marshal.ReadIntPtr(obj);
            return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(vt, index * IntPtr.Size), t);
        }

        private void SetupRaw()
        {
            ReleaseRaw();
            try
            {
                rawDev = Marshal.GetComInterfaceForObject(dev, typeof(IDirectInputDevice8W));
                // IDirectInputDevice8: 3 IUnknown + GetCapabilities(3) ... GetDeviceState(9) ... SendForceFeedbackCommand(22) ... Poll(25)
                rawGetState = (FnIntPtr)VFn(rawDev, 9, typeof(FnIntPtr));
                rawSendCmd = (FnInt)VFn(rawDev, 22, typeof(FnInt));
                rawPoll = (FnVoid)VFn(rawDev, 25, typeof(FnVoid));
                if (rawState == IntPtr.Zero) rawState = Alloc(StateSize);
                if (rawEff == IntPtr.Zero) rawEff = Alloc(Marshal.SizeOf(typeof(DIEFFECT)));
                if (rawConstBuf == IntPtr.Zero) rawConstBuf = Alloc(4);
                for (int i = 0; i < fx.Length; i++)
                {
                    if (fx[i] == null) continue;
                    rawFx[i] = Marshal.GetComInterfaceForObject(fx[i], typeof(IDirectInputEffect));
                    // IDirectInputEffect: 3 IUnknown + Initialize(3) GetEffectGuid(4) GetParameters(5) SetParameters(6) Start(7) Stop(8)
                    rawFxStop[i] = (FnVoid)VFn(rawFx[i], 8, typeof(FnVoid));
                }
                if (rawFx[SlotConst] != IntPtr.Zero) rawConstSet = (FnPtrInt)VFn(rawFx[SlotConst], 6, typeof(FnPtrInt));
            }
            catch (Exception ex) { log("[ffb] raw COM setup: " + ex.Message); ReleaseRaw(); }
        }

        private void ReleaseRaw()
        {
            for (int i = 0; i < rawFx.Length; i++)
            {
                if (rawFx[i] != IntPtr.Zero) { try { Marshal.Release(rawFx[i]); } catch { } rawFx[i] = IntPtr.Zero; }
                rawFxStop[i] = null;
            }
            if (rawDev != IntPtr.Zero) { try { Marshal.Release(rawDev); } catch { } rawDev = IntPtr.Zero; }
            rawSendCmd = null; rawPoll = null; rawGetState = null; rawConstSet = null;
        }

        private void RawStopAll()
        {
            try
            {
                for (int i = 0; i < rawFx.Length; i++) if (rawFx[i] != IntPtr.Zero && rawFxStop[i] != null) rawFxStop[i](rawFx[i]);
                if (rawDev != IntPtr.Zero && rawSendCmd != null) rawSendCmd(rawDev, DI.DISFFC_STOPALL);
            }
            catch { }
        }

        private void RawConstant(int mag)
        {
            if (!IsOpen || stopped || rawConstSet == null || rawFx[SlotConst] == IntPtr.Zero) return;
            mag = Clamp(mag, -10000, 10000);
            if (cache[SlotConst] == mag) return;
            Marshal.WriteInt32(rawConstBuf, 0, mag);
            DIEFFECT e = new DIEFFECT();
            e.dwSize = Marshal.SizeOf(typeof(DIEFFECT));
            e.dwFlags = DI.DIEFF_CARTESIAN | DI.DIEFF_OBJECTOFFSETS;
            e.dwDuration = DI.INFINITE;
            e.dwGain = 10000;
            e.dwTriggerButton = DI.DIEB_NOTRIGGER;
            e.cAxes = 1;
            e.rgdwAxes = axesPtr;
            e.rglDirection = dirPtr;
            e.lpEnvelope = IntPtr.Zero;
            e.cbTypeSpecificParams = 4;
            e.lpvTypeSpecificParams = rawConstBuf;
            Marshal.StructureToPtr(e, rawEff, false);
            int hr;
            try { hr = rawConstSet(rawFx[SlotConst], rawEff, DI.DIEP_TYPESPECIFICPARAMS); } catch { hr = -1; }
            if (hr >= 0) { sendOk++; cache[SlotConst] = mag; }
            else { sendFail++; lastSendHr = hr; }
        }
    }
}
