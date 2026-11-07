// FS25 Real FFB - controlador da base (DirectInput). Uma instancia = uma base.
// Efeitos de condicao (mola, amortecedor, atrito) sao calculados pela propria base a ~1 kHz;
// a forca constante e o seno levam as componentes dinamicas (pancadas, textura).
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FS25RealFFB
{
    internal sealed class FfbDevice : IWheelOutput
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
        private int lastRecoverMs;
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
            if (hr < 0 || di == null) { log("DirectInput8Create falhou " + DI.Hr(hr)); return false; }

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
            if (candidates.Count == 0) { log("Nenhum volante com force feedback encontrado."); return false; }

            Available.Clear();
            foreach (DIDEVICEINSTANCEW d in candidates)
            {
                string id = Vid(d).ToString("X4") + ":" + Pid(d).ToString("X4");
                bool seen = false;
                foreach (string[] a in Available) if (a[0] == id) seen = true;
                if (!seen) Available.Add(new string[] { id, d.tszProductName + " (" + id + ")" });
            }
            // Volante escolhido pelo utilizador: so as interfaces dele (a recuperacao nunca salta para outro volante)
            if (!string.IsNullOrEmpty(Preferred))
            {
                List<DIDEVICEINSTANCEW> only = new List<DIDEVICEINSTANCEW>();
                foreach (DIDEVICEINSTANCEW d in candidates)
                    if ((Vid(d).ToString("X4") + ":" + Pid(d).ToString("X4")) == Preferred) only.Add(d);
                if (only.Count > 0) { candidates.Clear(); candidates.AddRange(only); }
                else log("O volante escolhido (" + Preferred + ") nao esta ligado: a usar o primeiro encontrado.");
            }

            log("DirectInput: volantes/interfaces com FFB: " + candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                DIDEVICEINSTANCEW c = candidates[i];
                if (TryOpen(c, hwnd))
                {
                    current = i;
                    DeviceName = string.Format("{0} ({1:X4}:{2:X4})", c.tszProductName, Vid(c), Pid(c));
                    OpenVid = Vid(c); OpenPid = Pid(c);
                    IsOpen = true;
                    log("Base aberta: " + DeviceName);
                    return true;
                }
            }
            return false;
        }

        public string Preferred;   // VID:PID escolhido pelo utilizador ("" = automatico)
        public readonly List<string[]> Available = new List<string[]>();   // {VID:PID, nome}
        public int OpenVid { get; private set; }
        public int OpenPid { get; private set; }

        // Torque de pico de fabrica (Nm) dos volantes conhecidos; 0 = desconhecido.
        public static double KnownNm(int vid, int pid)
        {
            if (vid == 0x0EB7)
                switch (pid)
                {
                    case 0x0020: return 8;     // CSL DD com Boost Kit (5 Nm sem); GT DD Pro 8; ClubSport DD 12 (ajustar)
                    case 0x0006: return 20;    // Podium DD1
                    case 0x0007: return 25;    // Podium DD2
                    case 0x0001: case 0x0004: return 8;   // ClubSport V2 / V2.5
                    case 0x0E03: case 0x0005: return 6;   // CSL Elite
                    case 0x0011: return 4;     // CSR Elite
                }
            if (vid == 0x046D)
                switch (pid)
                {
                    case 0xC24F: case 0xC262: return 2.2;                          // G29 / G920
                    case 0xC266: case 0xC267: case 0xC26D: case 0xC26E: return 2.2; // G923
                    case 0xC268: case 0xC272: return 11;                           // G PRO
                    case 0xC29B: case 0xC299: return 2.5;                          // G27 / G25
                    case 0xC29A: return 2;                                         // Driving Force GT
                }
            return 0;
        }

        private static int Vid(DIDEVICEINSTANCEW d) { return (int)(BitConverter.ToUInt32(d.guidProduct.ToByteArray(), 0) & 0xFFFF); }
        private static int Pid(DIDEVICEINSTANCEW d) { return (int)(BitConverter.ToUInt32(d.guidProduct.ToByteArray(), 0) >> 16); }

        private bool TryOpen(DIDEVICEINSTANCEW inst, IntPtr hwnd)
        {
            Guid g = inst.guidInstance;
            int hr = di.CreateDevice(ref g, out dev, IntPtr.Zero);
            if (hr < 0 || dev == null) { log("CreateDevice falhou " + DI.Hr(hr)); return false; }
            hr = dev.SetDataFormat(dataFormat);
            if (hr < 0) { log("SetDataFormat falhou " + DI.Hr(hr)); return false; }
            hr = dev.SetCooperativeLevel(hwnd, DI.DISCL_EXCLUSIVE | DI.DISCL_BACKGROUND);
            if (hr < 0) { log("SetCooperativeLevel falhou " + DI.Hr(hr)); return false; }
            SetRange(-10000, 10000);
            SetRangeDevice(-10000, 10000);
            SetDword(DI.DIPROP_AUTOCENTER, 0);
            SetDword(DI.DIPROP_FFGAIN, 10000);
            hr = dev.Acquire();
            if (hr < 0)
            {
                log("Acquire falhou " + DI.Hr(hr) + " (outra aplicacao tem a base em modo exclusivo?)");
                return false;
            }
            dev.SendForceFeedbackCommand(DI.DISFFC_RESET);
            dev.SendForceFeedbackCommand(DI.DISFFC_SETACTUATORSON);

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
            return fx[SlotSpring] != null && fx[SlotConst] != null;
        }

        private void SetDword(int prop, int value)
        {
            IntPtr p = Marshal.AllocHGlobal(20);
            try
            {
                Marshal.WriteInt32(p, 0, 20); Marshal.WriteInt32(p, 4, 16);
                Marshal.WriteInt32(p, 8, 0); Marshal.WriteInt32(p, 12, DI.DIPH_DEVICE);
                Marshal.WriteInt32(p, 16, value);
                dev.SetProperty(new IntPtr(prop), p);
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
            if (hr < 0 || e == null) { log("Efeito " + name + " falhou " + DI.Hr(hr)); return null; }
            return e;
        }

        private void StartAll()
        {
            foreach (IDirectInputEffect e in fx) if (e != null) e.Start(1, 0);
            stopped = false;
        }

        // ------------------------------------------------------------------ atualizacao
        private void Apply(int slot, long key, IntPtr tsp, int cb)
        {
            IDirectInputEffect e = fx[slot];
            if (e == null || cache[slot] == key) return;
            WriteEffect(tsp, cb);
            int hr = e.SetParameters(effPtr, DI.DIEP_TYPESPECIFICPARAMS);
            if (hr < 0)
            {
                errorCount++;
                if (errorCount == 1 || errorCount % 2000 == 0)
                {
                    if (hr == unchecked((int)0x80040205))
                        log("CONFLITO: outra aplicacao (provavelmente o FS25) tomou o controlo exclusivo do FFB da base. Desligue o Force Feedback do jogo.");
                    else
                        log("SetParameters falhou " + DI.Hr(hr) + " - a recuperar a base");
                }
                Recover();
                return;
            }
            cache[slot] = key;
        }

        // Recuperacao: volta a adquirir; se outro programa tiver esta interface,
        // tenta a outra interface HID da base (a CSL DD expoe duas).
        private void Recover()
        {
            if (dev == null) return;
            int now = Environment.TickCount;
            if (now - lastRecoverMs < 500) return;
            lastRecoverMs = now;
            int hr = dev.Acquire();
            if (hr >= 0)
            {
                if (lostLogged) log("Controlo da base recuperado (interface " + (current + 1) + ").");
                lostLogged = false;
                ResetCache();
                StartAll();
                return;
            }
            if (!lostLogged)
            {
                lostLogged = true;
                log("Base ocupada por outro programa (" + DI.Hr(hr) + "). Janela em primeiro plano: " + ForegroundProcess());
            }
            if (candidates.Count > 1)
            {
                int next = (current + 1) % candidates.Count;
                ReleaseDevice();
                if (TryOpen(candidates[next], window))
                {
                    current = next;
                    log("Mudei para a interface " + (next + 1) + " da base.");
                    lostLogged = false;
                    ResetCache();
                }
                else
                {
                    log("Interface " + (next + 1) + " tambem ocupada.");
                    ReleaseDevice();
                    TryOpen(candidates[current], window);
                }
            }
        }

        private void ReleaseDevice()
        {
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

        public void SetSpring(int coef, int sat) { SetCondition(SlotSpring, coef, sat); }

        // Mola com centro deslocado (o volante alinha com a trajetoria real do trator).
        public void SetSpring(int coef, int sat, int offset)
        {
            if (!IsOpen) return;
            if (stopped) StartAll();
            coef = Clamp(coef, 0, 10000); sat = Clamp(sat, 0, 10000); offset = Clamp(offset, -10000, 10000);
            WriteCondition(coef, sat, offset);
            Apply(SlotSpring, ((long)coef << 40) ^ ((long)sat << 20) ^ (long)(offset & 0xFFFFF), condBuf, 24);
        }
        public void SetDamper(int coef, int sat) { SetCondition(SlotDamper, coef, sat); }
        public void SetFriction(int coef, int sat) { SetCondition(SlotFriction, coef, sat); }

        private void SetCondition(int slot, int coef, int sat)
        {
            if (!IsOpen) return;
            if (stopped) StartAll();
            coef = Clamp(coef, 0, 10000); sat = Clamp(sat, 0, 10000);
            WriteCondition(coef, sat);
            Apply(slot, ((long)coef << 32) | (uint)sat, condBuf, 24);
        }

        public void SetConstant(int mag)
        {
            if (!IsOpen) return;
            if (stopped) StartAll();
            mag = Clamp(mag, -10000, 10000);
            Marshal.WriteInt32(constBuf, 0, mag);
            Apply(SlotConst, mag, constBuf, 4);
        }

        // Vibracao do motor: segundo seno, independente da textura do piso.
        public void SetEngine(int mag, int periodUs)
        {
            if (!IsOpen) return;
            if (stopped) StartAll();
            mag = Clamp(mag, 0, 10000);
            periodUs = Clamp(periodUs, 6000, 200000);
            WritePeriodic(mag, periodUs);
            Apply(SlotEngine, ((long)mag << 32) | (uint)periodUs, periodicBuf, 16);
        }

        public void SetSine(int mag, int periodUs)
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
    }
}
