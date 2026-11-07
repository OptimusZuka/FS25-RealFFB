// FS25 Real FFB - Teste A: comunicacao DirectInput direta com a Fanatec CSL DD
// Objetivo: provar que um programa externo controla o motor da base (sem o jogo).
// Compilar com build.bat (usa o csc.exe que ja vem com o Windows / .NET Framework 4.x).
// Seguranca: forca maxima 1,0 Nm (com base de 8 Nm e FFB a 100%), pulsos curtos com
// duracao limitada pela propria base, ESPACO / ESC = paragem imediata.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FS25RealFFB.TestA
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int EnumDevicesCallback(IntPtr lpddi, IntPtr pvRef);

    [ComImport, Guid("BF798031-483A-4DA2-AA99-5D64ED369700"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDirectInput8W
    {
        [PreserveSig] int CreateDevice(ref Guid rguid, [MarshalAs(UnmanagedType.Interface)] out IDirectInputDevice8W device, IntPtr outer);
        [PreserveSig] int EnumDevices(int devType, [MarshalAs(UnmanagedType.FunctionPtr)] EnumDevicesCallback callback, IntPtr pvRef, int flags);
        [PreserveSig] int GetDeviceStatus(ref Guid rguid);
        [PreserveSig] int RunControlPanel(IntPtr hwnd, int flags);
        [PreserveSig] int Initialize(IntPtr hinst, int version);
        [PreserveSig] int FindDevice(ref Guid rguidClass, IntPtr name, out Guid instance);
        [PreserveSig] int EnumDevicesBySemantics(IntPtr a, IntPtr b, IntPtr c, IntPtr d, int e);
        [PreserveSig] int ConfigureDevices(IntPtr a, IntPtr b, int c, IntPtr d);
    }

    [ComImport, Guid("54D41081-DC15-4833-A41B-748F73A38179"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDirectInputDevice8W
    {
        [PreserveSig] int GetCapabilities(IntPtr caps);
        [PreserveSig] int EnumObjects(IntPtr cb, IntPtr pvRef, int flags);
        [PreserveSig] int GetProperty(IntPtr rguidProp, IntPtr pdiph);
        [PreserveSig] int SetProperty(IntPtr rguidProp, IntPtr pdiph);
        [PreserveSig] int Acquire();
        [PreserveSig] int Unacquire();
        [PreserveSig] int GetDeviceState(int cbData, IntPtr data);
        [PreserveSig] int GetDeviceData(int cbObjectData, IntPtr rgdod, IntPtr pdwInOut, int flags);
        [PreserveSig] int SetDataFormat(IntPtr lpdf);
        [PreserveSig] int SetEventNotification(IntPtr hEvent);
        [PreserveSig] int SetCooperativeLevel(IntPtr hwnd, int flags);
        [PreserveSig] int GetObjectInfo(IntPtr pdidoi, int obj, int how);
        [PreserveSig] int GetDeviceInfo(IntPtr pdidi);
        [PreserveSig] int RunControlPanel(IntPtr hwnd, int flags);
        [PreserveSig] int Initialize(IntPtr hinst, int version, ref Guid rguid);
        [PreserveSig] int CreateEffect(ref Guid rguid, IntPtr lpeff, [MarshalAs(UnmanagedType.Interface)] out IDirectInputEffect effect, IntPtr outer);
        [PreserveSig] int EnumEffects(IntPtr cb, IntPtr pvRef, int effType);
        [PreserveSig] int GetEffectInfo(IntPtr pdei, ref Guid rguid);
        [PreserveSig] int GetForceFeedbackState(out int state);
        [PreserveSig] int SendForceFeedbackCommand(int flags);
        [PreserveSig] int EnumCreatedEffectObjects(IntPtr cb, IntPtr pvRef, int flags);
        [PreserveSig] int Escape(IntPtr pesc);
        [PreserveSig] int Poll();
    }

    [ComImport, Guid("E7E1F7C0-88D2-11D0-9AD0-00A0C9A06E35"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDirectInputEffect
    {
        [PreserveSig] int Initialize(IntPtr hinst, int version, ref Guid rguid);
        [PreserveSig] int GetEffectGuid(out Guid guid);
        [PreserveSig] int GetParameters(IntPtr peff, int flags);
        [PreserveSig] int SetParameters(IntPtr peff, int flags);
        [PreserveSig] int Start(int iterations, int flags);
        [PreserveSig] int Stop();
        [PreserveSig] int GetEffectStatus(out int flags);
        [PreserveSig] int Download();
        [PreserveSig] int Unload();
        [PreserveSig] int Escape(IntPtr pesc);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DIDEVICEINSTANCEW
    {
        public int dwSize;
        public Guid guidInstance;
        public Guid guidProduct;
        public int dwDevType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string tszInstanceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string tszProductName;
        public Guid guidFFDriver;
        public short wUsagePage;
        public short wUsage;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DIEFFECT
    {
        public int dwSize;
        public int dwFlags;
        public uint dwDuration;
        public int dwSamplePeriod;
        public int dwGain;
        public uint dwTriggerButton;
        public int dwTriggerRepeatInterval;
        public int cAxes;
        public IntPtr rgdwAxes;
        public IntPtr rglDirection;
        public IntPtr lpEnvelope;
        public int cbTypeSpecificParams;
        public IntPtr lpvTypeSpecificParams;
        public int dwStartDelay;
    }

    internal static class DI
    {
        public const int DI8DEVCLASS_GAMECTRL = 4;
        public const int DIEDFL_ATTACHEDONLY = 0x1;
        public const int DIEDFL_FORCEFEEDBACK = 0x100;
        public const int DISCL_EXCLUSIVE = 0x1;
        public const int DISCL_FOREGROUND = 0x4;
        public const int DISCL_BACKGROUND = 0x8;
        public const int DIEFF_OBJECTOFFSETS = 0x2;
        public const int DIEFF_CARTESIAN = 0x10;
        public const uint DIEB_NOTRIGGER = 0xFFFFFFFF;
        public const int DIEP_DURATION = 0x1;
        public const int DIEP_TYPESPECIFICPARAMS = 0x100;
        public const int DIEP_START = 0x20000000;
        public const int DISFFC_RESET = 0x1;
        public const int DISFFC_STOPALL = 0x2;
        public const int DISFFC_SETACTUATORSON = 0x10;
        public const int DIPROP_RANGE = 4;
        public const int DIPROP_FFGAIN = 7;
        public const int DIPROP_AUTOCENTER = 9;
        public const int DIPH_DEVICE = 0;
        public const int DIPH_BYOFFSET = 1;
        public const int DIDC_FORCEFEEDBACK = 0x100;

        public static readonly Guid IID_IDirectInput8W = new Guid("BF798031-483A-4DA2-AA99-5D64ED369700");
        public static readonly Guid GUID_XAxis = new Guid("A36D02E0-C9F3-11CF-BFC7-444553540000");
        public static readonly Guid GUID_ConstantForce = new Guid("13541C20-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_RampForce = new Guid("13541C21-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Square = new Guid("13541C22-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Sine = new Guid("13541C23-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Triangle = new Guid("13541C24-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Spring = new Guid("13541C27-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Damper = new Guid("13541C28-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Inertia = new Guid("13541C29-8E33-11D0-9AD0-00A0C9A06E35");
        public static readonly Guid GUID_Friction = new Guid("13541C2A-8E33-11D0-9AD0-00A0C9A06E35");

        [DllImport("dinput8.dll")]
        public static extern int DirectInput8Create(IntPtr hinst, int version, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IDirectInput8W ppv, IntPtr outer);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string name);

        public static string Hr(int hr) { return "0x" + hr.ToString("X8"); }
    }

    internal sealed class TestForm : Form
    {
        // ---- Limites de seguranca ----
        private const double BaseNm = 8.0;     // CSL DD com Boost Kit
        private const int MaxMag = 1250;       // 1250/10000 * 8 Nm = 1,0 Nm
        private const int PulseMs = 200;       // duracao de cada pulso (a base corta sozinha)

        private IDirectInput8W di;
        private IDirectInputDevice8W dev;
        private IDirectInputEffect constEff;
        private IDirectInputEffect springEff;
        private readonly List<IntPtr> allocs = new List<IntPtr>();
        private IntPtr dataFormat, stateBuf, effPtr, axesPtr, dirPtr, constParam, condParam, periodicParam, rampParam;
        private bool ready;
        private string logPath;

        private TextBox logBox;
        private Label posLabel;
        private ComboBox strengthBox;
        private NumericUpDown rotationBox;
        private System.Windows.Forms.Timer posTimer;

        private static readonly double[] StrengthNm = { 0.25, 0.50, 0.75, 1.00 };

        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TestForm());
        }

        public TestForm()
        {
            Text = "FS25 Real FFB - Teste A (Fanatec CSL DD)";
            Width = 980; Height = 680;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            logPath = Path.Combine(Application.StartupPath, "TesteA_resultado.txt");

            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Top; top.Height = 92; top.Padding = new Padding(6); top.WrapContents = true;

            Button stopBtn = MakeButton("PARAR  (Espaco / Esc)", 210);
            stopBtn.BackColor = Color.FromArgb(200, 30, 30); stopBtn.ForeColor = Color.White;
            stopBtn.Font = new Font(stopBtn.Font, FontStyle.Bold);
            stopBtn.Click += delegate { EmergencyStop("botao PARAR"); };

            Button negBtn = MakeButton("Pulso  -  (negativo)", 160);
            negBtn.Click += delegate { ManualPulse(-1); };
            Button posBtn = MakeButton("Pulso  +  (positivo)", 160);
            posBtn.Click += delegate { ManualPulse(+1); };
            Button springBtn = MakeButton("Mola de centragem 5 s", 170);
            springBtn.Click += delegate { SpringTest(5); };
            Button seqBtn = MakeButton("Sequencia automatica", 170);
            seqBtn.Click += delegate { AutoSequence(); };

            strengthBox = new ComboBox();
            strengthBox.DropDownStyle = ComboBoxStyle.DropDownList; strengthBox.Width = 90;
            foreach (double nm in StrengthNm) strengthBox.Items.Add(nm.ToString("0.00") + " Nm");
            strengthBox.SelectedIndex = 0;

            rotationBox = new NumericUpDown();
            rotationBox.Minimum = 90; rotationBox.Maximum = 2520; rotationBox.Value = 900; rotationBox.Width = 70;

            posLabel = new Label(); posLabel.AutoSize = true; posLabel.Padding = new Padding(0, 8, 0, 0);
            posLabel.Text = "Posicao: --";

            top.Controls.Add(stopBtn);
            top.Controls.Add(negBtn);
            top.Controls.Add(posBtn);
            top.Controls.Add(springBtn);
            top.Controls.Add(seqBtn);
            top.Controls.Add(MakeLabel("Forca:"));
            top.Controls.Add(strengthBox);
            top.Controls.Add(MakeLabel("Rotacao da base (graus):"));
            top.Controls.Add(rotationBox);
            top.Controls.Add(posLabel);

            logBox = new TextBox();
            logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill; logBox.Font = new Font("Consolas", 9.5f);
            logBox.BackColor = Color.FromArgb(20, 20, 20); logBox.ForeColor = Color.Gainsboro;

            Controls.Add(logBox);
            Controls.Add(top);

            posTimer = new System.Windows.Forms.Timer();
            posTimer.Interval = 50;
            posTimer.Tick += delegate { UpdatePosLabel(); };

            KeyDown += OnKeyDownHandler;
            Shown += delegate { Init(); };
            FormClosing += delegate { Shutdown(); };
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                Log("ERRO: " + e.Exception);
                EmergencyStop("excecao");
            };
        }

        private static Button MakeButton(string text, int width)
        {
            Button b = new Button(); b.Text = text; b.Width = width; b.Height = 34; return b;
        }

        private static Label MakeLabel(string text)
        {
            Label l = new Label(); l.Text = text; l.AutoSize = true; l.Padding = new Padding(8, 8, 0, 0); return l;
        }

        private void OnKeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Escape)
            {
                e.Handled = true; e.SuppressKeyPress = true;
                EmergencyStop("tecla " + e.KeyCode);
            }
        }

        // ------------------------------------------------------------------ log
        private void Log(string s)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s;
            logBox.AppendText(line + Environment.NewLine);
            try { File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8); } catch { }
        }

        private IntPtr Alloc(int size)
        {
            IntPtr p = Marshal.AllocHGlobal(size);
            for (int i = 0; i < size; i++) Marshal.WriteByte(p, i, 0);
            allocs.Add(p);
            return p;
        }

        // ------------------------------------------------------------------ init
        private void Init()
        {
            Log("==================== TESTE A - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " ====================");
            Log("Windows: " + Environment.OSVersion + " | processo 64-bit: " + Environment.Is64BitProcess);
            Log("Limite de seguranca: " + (MaxMag / 10000.0 * BaseNm).ToString("0.00") + " Nm (assumindo base 8 Nm e FFB 100%).");
            Log("Feche o FS25 antes deste teste. Espaco ou Esc param tudo de imediato.");

            BuildNativeBuffers();

            Guid iid = DI.IID_IDirectInput8W;
            int hr = DI.DirectInput8Create(DI.GetModuleHandle(null), 0x0800, ref iid, out di, IntPtr.Zero);
            if (hr < 0 || di == null) { Log("FALHA: DirectInput8Create " + DI.Hr(hr)); return; }
            Log("DirectInput 8 inicializado.");

            List<DIDEVICEINSTANCEW> all = new List<DIDEVICEINSTANCEW>();
            List<DIDEVICEINSTANCEW> ff = new List<DIDEVICEINSTANCEW>();
            EnumDevicesCallback cbAll = delegate(IntPtr p, IntPtr r)
            {
                all.Add((DIDEVICEINSTANCEW)Marshal.PtrToStructure(p, typeof(DIDEVICEINSTANCEW))); return 1;
            };
            EnumDevicesCallback cbFf = delegate(IntPtr p, IntPtr r)
            {
                ff.Add((DIDEVICEINSTANCEW)Marshal.PtrToStructure(p, typeof(DIDEVICEINSTANCEW))); return 1;
            };
            di.EnumDevices(DI.DI8DEVCLASS_GAMECTRL, cbAll, IntPtr.Zero, DI.DIEDFL_ATTACHEDONLY);
            di.EnumDevices(DI.DI8DEVCLASS_GAMECTRL, cbFf, IntPtr.Zero, DI.DIEDFL_ATTACHEDONLY | DI.DIEDFL_FORCEFEEDBACK);
            GC.KeepAlive(cbAll); GC.KeepAlive(cbFf);

            Log("Controladores ligados: " + all.Count + " | com force feedback: " + ff.Count);
            foreach (DIDEVICEINSTANCEW d in all)
            {
                bool hasFf = false;
                foreach (DIDEVICEINSTANCEW f in ff) if (f.guidInstance == d.guidInstance) hasFf = true;
                Log(string.Format("  - {0,-28} VID {1:X4} PID {2:X4}  FFB: {3}", d.tszProductName, Vid(d), Pid(d), hasFf ? "SIM" : "nao"));
            }

            List<DIDEVICEINSTANCEW> candidates = new List<DIDEVICEINSTANCEW>();
            foreach (DIDEVICEINSTANCEW f in ff) if (Vid(f) == 0x0EB7 && Pid(f) == 0x0020) candidates.Add(f);
            if (candidates.Count == 0) foreach (DIDEVICEINSTANCEW f in ff) if (Vid(f) == 0x0EB7) candidates.Add(f);
            if (candidates.Count == 0)
            {
                Log("RESULTADO: nenhuma base Fanatec com force feedback encontrada pelo DirectInput.");
                return;
            }

            foreach (DIDEVICEINSTANCEW c in candidates)
            {
                Log(string.Format("A abrir: {0} (PID {1:X4})", c.tszProductName, Pid(c)));
                if (OpenDevice(c)) { ready = true; break; }
            }
            if (!ready) { Log("RESULTADO: nao foi possivel abrir a base para force feedback."); return; }

            ProbeEffects();
            CreateConstantEffect();
            posTimer.Start();
            Log("PRONTO. Recomendado: 'Sequencia automatica' com as maos FORA do volante.");
        }

        private static int Vid(DIDEVICEINSTANCEW d) { return (int)(BitConverter.ToUInt32(d.guidProduct.ToByteArray(), 0) & 0xFFFF); }
        private static int Pid(DIDEVICEINSTANCEW d) { return (int)(BitConverter.ToUInt32(d.guidProduct.ToByteArray(), 0) >> 16); }

        private void BuildNativeBuffers()
        {
            // Formato de dados minimo: apenas o eixo X (direcao), 4 bytes, offset 0.
            IntPtr guidX = Alloc(16);
            Marshal.StructureToPtr(DI.GUID_XAxis, guidX, false);
            IntPtr odf = Alloc(24);                                  // DIOBJECTDATAFORMAT (x64)
            Marshal.WriteIntPtr(odf, 0, guidX);
            Marshal.WriteInt32(odf, 8, 0);                           // dwOfs
            Marshal.WriteInt32(odf, 12, unchecked((int)0x00FFFF03)); // DIDFT_AXIS | DIDFT_ANYINSTANCE
            Marshal.WriteInt32(odf, 16, 0);
            dataFormat = Alloc(32);                                  // DIDATAFORMAT (x64)
            Marshal.WriteInt32(dataFormat, 0, 32);
            Marshal.WriteInt32(dataFormat, 4, 24);
            Marshal.WriteInt32(dataFormat, 8, 1);                    // DIDF_ABSAXIS
            Marshal.WriteInt32(dataFormat, 12, 4);
            Marshal.WriteInt32(dataFormat, 16, 1);
            Marshal.WriteIntPtr(dataFormat, 24, odf);

            stateBuf = Alloc(4);
            effPtr = Alloc(Marshal.SizeOf(typeof(DIEFFECT)));
            axesPtr = Alloc(4); Marshal.WriteInt32(axesPtr, 0, 0);   // eixo = offset 0 (X)
            dirPtr = Alloc(4); Marshal.WriteInt32(dirPtr, 0, 0);     // 1 eixo: direcao pelo sinal da magnitude
            constParam = Alloc(4);
            condParam = Alloc(24);
            periodicParam = Alloc(16);
            rampParam = Alloc(8);
        }

        private bool OpenDevice(DIDEVICEINSTANCEW inst)
        {
            Guid g = inst.guidInstance;
            int hr = di.CreateDevice(ref g, out dev, IntPtr.Zero);
            if (hr < 0 || dev == null) { Log("  CreateDevice falhou " + DI.Hr(hr)); return false; }

            hr = dev.SetDataFormat(dataFormat);
            if (hr < 0) { Log("  SetDataFormat falhou " + DI.Hr(hr)); return false; }

            hr = dev.SetCooperativeLevel(Handle, DI.DISCL_EXCLUSIVE | DI.DISCL_BACKGROUND);
            if (hr < 0)
            {
                Log("  Modo exclusivo em segundo plano recusado " + DI.Hr(hr) + " - a tentar em primeiro plano.");
                hr = dev.SetCooperativeLevel(Handle, DI.DISCL_EXCLUSIVE | DI.DISCL_FOREGROUND);
                if (hr < 0) { Log("  SetCooperativeLevel falhou " + DI.Hr(hr)); return false; }
            }

            IntPtr caps = Alloc(44);
            Marshal.WriteInt32(caps, 0, 44);
            hr = dev.GetCapabilities(caps);
            if (hr >= 0)
            {
                int flags = Marshal.ReadInt32(caps, 4);
                Log(string.Format("  Capacidades: FFB={0} eixos={1} botoes={2} FFSamplePeriod={3}us FFMinTimeRes={4}us firmware={5} driverFF={6}",
                    (flags & DI.DIDC_FORCEFEEDBACK) != 0 ? "SIM" : "NAO",
                    Marshal.ReadInt32(caps, 12), Marshal.ReadInt32(caps, 16),
                    Marshal.ReadInt32(caps, 24), Marshal.ReadInt32(caps, 28),
                    Marshal.ReadInt32(caps, 32), Marshal.ReadInt32(caps, 40)));
            }

            hr = SetRange(-10000, 10000);
            Log("  Escala do eixo X -10000..10000: " + (hr >= 0 ? "ok" : DI.Hr(hr)));
            hr = SetDword(DI.DIPROP_AUTOCENTER, DI.DIPH_DEVICE, 0, 0);
            Log("  Autocentragem do driver desligada: " + (hr >= 0 ? "ok" : DI.Hr(hr)));
            hr = SetDword(DI.DIPROP_FFGAIN, DI.DIPH_DEVICE, 0, 10000);
            Log("  Ganho FF 100%: " + (hr >= 0 ? "ok" : DI.Hr(hr)));

            hr = dev.Acquire();
            if (hr < 0)
            {
                Log("  Acquire falhou " + DI.Hr(hr) + " -> outra aplicacao pode ter o volante em modo exclusivo (FS25 aberto? jogo/simulador em segundo plano?).");
                return false;
            }
            Log("  Base adquirida em modo exclusivo.");

            dev.SendForceFeedbackCommand(DI.DISFFC_RESET);
            dev.SendForceFeedbackCommand(DI.DISFFC_SETACTUATORSON);
            int st;
            hr = dev.GetForceFeedbackState(out st);
            Log("  Estado FFB: " + (hr >= 0 ? "0x" + st.ToString("X") : DI.Hr(hr)) + " | posicao atual: " + ReadPos());
            return true;
        }

        private int SetDword(int prop, int how, int obj, int value)
        {
            IntPtr p = Marshal.AllocHGlobal(20);
            try
            {
                Marshal.WriteInt32(p, 0, 20); Marshal.WriteInt32(p, 4, 16);
                Marshal.WriteInt32(p, 8, obj); Marshal.WriteInt32(p, 12, how);
                Marshal.WriteInt32(p, 16, value);
                return dev.SetProperty(new IntPtr(prop), p);
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        private int SetRange(int min, int max)
        {
            IntPtr p = Marshal.AllocHGlobal(24);
            try
            {
                Marshal.WriteInt32(p, 0, 24); Marshal.WriteInt32(p, 4, 16);
                Marshal.WriteInt32(p, 8, 0); Marshal.WriteInt32(p, 12, DI.DIPH_BYOFFSET);
                Marshal.WriteInt32(p, 16, min); Marshal.WriteInt32(p, 20, max);
                return dev.SetProperty(new IntPtr(DI.DIPROP_RANGE), p);
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        private int ReadPos()
        {
            if (dev == null) return 0;
            dev.Poll();
            int hr = dev.GetDeviceState(4, stateBuf);
            if (hr < 0) { dev.Acquire(); dev.Poll(); dev.GetDeviceState(4, stateBuf); }
            return Marshal.ReadInt32(stateBuf, 0);
        }

        private double ToDeg(int units) { return units / 20000.0 * (double)rotationBox.Value; }

        private void UpdatePosLabel()
        {
            if (!ready) return;
            int p = ReadPos();
            posLabel.Text = string.Format("Posicao: {0,6}  (~{1,6:0.0} graus)", p, ToDeg(p));
        }

        private void WriteEffect(uint durationUs, IntPtr tsp, int cb)
        {
            DIEFFECT e = new DIEFFECT();
            e.dwSize = Marshal.SizeOf(typeof(DIEFFECT));
            e.dwFlags = DI.DIEFF_CARTESIAN | DI.DIEFF_OBJECTOFFSETS;
            e.dwDuration = durationUs;
            e.dwSamplePeriod = 0;
            e.dwGain = 10000;
            e.dwTriggerButton = DI.DIEB_NOTRIGGER;
            e.dwTriggerRepeatInterval = 0;
            e.cAxes = 1;
            e.rgdwAxes = axesPtr;
            e.rglDirection = dirPtr;
            e.lpEnvelope = IntPtr.Zero;
            e.cbTypeSpecificParams = cb;
            e.lpvTypeSpecificParams = tsp;
            e.dwStartDelay = 0;
            Marshal.StructureToPtr(e, effPtr, false);
        }

        private void WriteCondition(int coef, int saturation)
        {
            Marshal.WriteInt32(condParam, 0, 0);          // lOffset (centro)
            Marshal.WriteInt32(condParam, 4, coef);       // lPositiveCoefficient
            Marshal.WriteInt32(condParam, 8, coef);       // lNegativeCoefficient
            Marshal.WriteInt32(condParam, 12, saturation);// dwPositiveSaturation
            Marshal.WriteInt32(condParam, 16, saturation);// dwNegativeSaturation
            Marshal.WriteInt32(condParam, 20, 0);         // lDeadBand
        }

        // ------------------------------------------------------------------ efeitos suportados
        private void ProbeEffects()
        {
            Log("Efeitos suportados pela base (criados com forca zero, nunca iniciados):");
            Marshal.WriteInt32(constParam, 0, 0);
            WriteCondition(0, 0);
            Marshal.WriteInt32(periodicParam, 0, 0); Marshal.WriteInt32(periodicParam, 4, 0);
            Marshal.WriteInt32(periodicParam, 8, 0); Marshal.WriteInt32(periodicParam, 12, 100000);
            Marshal.WriteInt32(rampParam, 0, 0); Marshal.WriteInt32(rampParam, 4, 0);

            Probe("Constant Force", DI.GUID_ConstantForce, constParam, 4);
            Probe("Spring", DI.GUID_Spring, condParam, 24);
            Probe("Damper", DI.GUID_Damper, condParam, 24);
            Probe("Friction", DI.GUID_Friction, condParam, 24);
            Probe("Inertia", DI.GUID_Inertia, condParam, 24);
            Probe("Sine", DI.GUID_Sine, periodicParam, 16);
            Probe("Square", DI.GUID_Square, periodicParam, 16);
            Probe("Triangle", DI.GUID_Triangle, periodicParam, 16);
            Probe("Ramp", DI.GUID_RampForce, rampParam, 8);
        }

        private void Probe(string name, Guid g, IntPtr tsp, int cb)
        {
            WriteEffect(100000, tsp, cb);
            IDirectInputEffect e;
            int hr = dev.CreateEffect(ref g, effPtr, out e, IntPtr.Zero);
            Log(string.Format("  {0,-15} {1}", name, (hr >= 0 && e != null) ? "SIM" : "NAO (" + DI.Hr(hr) + ")"));
            if (e != null) { e.Stop(); e.Unload(); Marshal.ReleaseComObject(e); }
        }

        private void CreateConstantEffect()
        {
            Marshal.WriteInt32(constParam, 0, 0);
            WriteEffect((uint)(PulseMs * 1000), constParam, 4);
            Guid g = DI.GUID_ConstantForce;
            int hr = dev.CreateEffect(ref g, effPtr, out constEff, IntPtr.Zero);
            if (hr < 0 || constEff == null) { Log("FALHA ao criar Constant Force " + DI.Hr(hr)); ready = false; }
        }

        // ------------------------------------------------------------------ testes
        private int NmToMag(double nm)
        {
            int mag = (int)Math.Round(nm / BaseNm * 10000.0);
            if (mag > MaxMag) mag = MaxMag;
            if (mag < -MaxMag) mag = -MaxMag;
            return mag;
        }

        private void ManualPulse(int sign)
        {
            if (!ready) { Log("Base nao esta pronta."); return; }
            Pulse(sign * StrengthNm[strengthBox.SelectedIndex]);
        }

        // Aplica um pulso curto e mede a resposta fisica do eixo (maos fora do volante).
        // Devolve o deslocamento maximo em unidades (-10000..10000 = rotacao total).
        private int Pulse(double nm)
        {
            int mag = NmToMag(nm);
            Marshal.WriteInt32(constParam, 0, mag);
            WriteEffect((uint)(PulseMs * 1000), constParam, 4);

            int start = ReadPos();
            Stopwatch sw = Stopwatch.StartNew();
            int hr = constEff.SetParameters(effPtr, DI.DIEP_DURATION | DI.DIEP_TYPESPECIFICPARAMS | DI.DIEP_START);
            if (hr < 0) { Log("SetParameters/Start falhou " + DI.Hr(hr)); return 0; }

            double firstMove = -1;
            int maxD = 0, samples = 0;
            while (sw.ElapsedMilliseconds < PulseMs + 600)
            {
                int d = ReadPos() - start;
                samples++;
                if (firstMove < 0 && Math.Abs(d) > 30) firstMove = sw.Elapsed.TotalMilliseconds;
                if (Math.Abs(d) > Math.Abs(maxD)) maxD = d;
            }
            constEff.Stop();

            string dir = maxD == 0 ? "sem movimento" : (maxD > 0 ? "eixo X aumentou" : "eixo X diminuiu");
            Log(string.Format("Pulso {0,5:+0.00;-0.00} Nm / {1} ms -> desloc. max {2,6} ({3,6:0.0} graus), {4}, resposta em {5}, {6} leituras",
                mag / 10000.0 * BaseNm, PulseMs, maxD, ToDeg(maxD), dir,
                firstMove < 0 ? "--" : firstMove.ToString("0.0") + " ms", samples));
            return maxD;
        }

        private void SpringTest(int seconds)
        {
            if (!ready) { Log("Base nao esta pronta."); return; }
            int sat = NmToMag(StrengthNm[strengthBox.SelectedIndex]);
            WriteCondition(10000, sat);
            WriteEffect((uint)(seconds * 1000000), condParam, 24);
            if (springEff != null) { springEff.Stop(); Marshal.ReleaseComObject(springEff); springEff = null; }
            Guid g = DI.GUID_Spring;
            int hr = dev.CreateEffect(ref g, effPtr, out springEff, IntPtr.Zero);
            if (hr < 0 || springEff == null) { Log("Spring falhou " + DI.Hr(hr)); return; }
            hr = springEff.Start(1, 0);
            Log(string.Format("Mola ativa {0} s (max {1:0.00} Nm). Rode o volante: deve resistir e voltar ao centro. Start: {2}",
                seconds, sat / 10000.0 * BaseNm, hr >= 0 ? "ok" : DI.Hr(hr)));
        }

        private void AutoSequence()
        {
            if (!ready) { Log("Base nao esta pronta."); return; }
            DialogResult r = MessageBox.Show(this,
                "Tire as maos do volante.\n\nVao ser aplicados 4 pulsos curtos (0,25 e 0,50 Nm, 200 ms cada).\nO volante vai mexer um pouco para cada lado.\n\nEspaco ou Esc param tudo.",
                "Sequencia automatica", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (r != DialogResult.OK) return;

            Log("---- Sequencia automatica (maos fora do volante) ----");
            double[] seq = { +0.25, -0.25, +0.50, -0.50 };
            int[] res = new int[seq.Length];
            for (int i = 0; i < seq.Length; i++)
            {
                res[i] = Pulse(seq[i]);
                Application.DoEvents();
                Thread.Sleep(1000);
            }

            bool moved = false;
            foreach (int v in res) if (Math.Abs(v) > 30) moved = true;
            bool dirOk = Math.Sign(res[0]) != 0 && Math.Sign(res[0]) == -Math.Sign(res[1])
                      && Math.Sign(res[2]) == -Math.Sign(res[3]);
            bool scales = Math.Abs(res[2]) > Math.Abs(res[0]) && Math.Abs(res[3]) > Math.Abs(res[1]);

            Log("==== RESULTADO TESTE A ====");
            Log("  Base responde fisicamente a comandos externos: " + (moved ? "SIM" : "NAO"));
            Log("  Inverte de direcao com o sinal da forca:      " + (dirOk ? "SIM" : "NAO"));
            Log("  Mais forca = mais deslocamento:              " + (scales ? "SIM" : "NAO"));
            Log("===========================");
        }

        // ------------------------------------------------------------------ seguranca
        private void EmergencyStop(string why)
        {
            try
            {
                if (constEff != null) constEff.Stop();
                if (springEff != null) springEff.Stop();
                if (dev != null) dev.SendForceFeedbackCommand(DI.DISFFC_STOPALL);
            }
            catch (Exception ex) { Log("Erro na paragem: " + ex.Message); }
            Log("PARAGEM IMEDIATA (" + why + ")");
        }

        private void Shutdown()
        {
            posTimer.Stop();
            try
            {
                if (dev != null)
                {
                    dev.SendForceFeedbackCommand(DI.DISFFC_STOPALL);
                    dev.SendForceFeedbackCommand(DI.DISFFC_RESET);
                }
                if (constEff != null) Marshal.ReleaseComObject(constEff);
                if (springEff != null) Marshal.ReleaseComObject(springEff);
                if (dev != null) { dev.Unacquire(); Marshal.ReleaseComObject(dev); }
                if (di != null) Marshal.ReleaseComObject(di);
            }
            catch { }
            constEff = null; springEff = null; dev = null; di = null;
            foreach (IntPtr p in allocs) Marshal.FreeHGlobal(p);
            allocs.Clear();
            try { File.AppendAllText(logPath, "---- programa fechado, forcas paradas ----" + Environment.NewLine, Encoding.UTF8); } catch { }
        }
    }
}
