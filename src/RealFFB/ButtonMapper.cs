// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - comandos -> teclado.
// Com os joysticks desligados no FS25 (necessario para o FFB), a app le a ButtonBox, o TCA,
// o A320, o aro Fanatec, etc., e carrega nas teclas que o FS25 usa para cada funcao.
// "Importar do FS25" le o inputBinding.xml do jogo e cria as atribuicoes automaticamente:
// para cada acao, o botao/eixo que estava no comando passa a carregar a tecla dessa acao.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FS25RealFFB
{
    internal sealed class ButtonMapper : IDisposable
    {
        public const string WheelId = "FANATEC";
        private const int Axes = 8, Buttons = 128, Size = Axes * 4 + Buttons;

        private sealed class Joy
        {
            public string Id, Name; public int Vid, Pid;
            public IDirectInputDevice8W Dev;
            public int[] Axis = new int[Axes], Base = new int[Axes];
            public bool HasBase;
            public bool[] Now = new bool[Buttons];
        }

        public sealed class Mapping
        {
            public string DeviceId, DeviceName, Action = "";
            public int Kind;      // 0 = botao, 1 = eixo
            public int Index;     // botao ou eixo (base 0)
            public int Sign = 1;  // eixo: +1 / -1
            public int[] Vks = new int[0];
            public bool Down;
            public override string ToString()
            {
                StringBuilder k = new StringBuilder();
                foreach (int v in Vks) { if (k.Length > 0) k.Append("+"); k.Append(((Keys)v).ToString()); }
                string src = Kind == 0 ? "botao " + (Index + 1) : "eixo " + (Index + 1) + (Sign > 0 ? "+" : "-");
                return DeviceName + " " + src + " -> " + k + (Action.Length > 0 ? "  (" + Action + ")" : "");
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct INPUT
        {
            [FieldOffset(0)] public int type;
            [FieldOffset(8)] public ushort wVk;
            [FieldOffset(10)] public ushort wScan;
            [FieldOffset(12)] public uint dwFlags;
            [FieldOffset(16)] public uint time;
            [FieldOffset(24)] public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")] private static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint code, uint mapType);

        private readonly Action<string> log;
        private readonly List<Joy> joys = new List<Joy>();
        private readonly bool[] wheelNow = new bool[Buttons];
        private readonly List<IntPtr> allocs = new List<IntPtr>();
        private IDirectInput8W di;
        private IntPtr format, buf;
        private readonly string path = AppInfo.DataFile("RealFFB_comandos.txt");
        private readonly Dictionary<int, int> keyRefs = new Dictionary<int, int>();

        public readonly List<Mapping> Mappings = new List<Mapping>();

        public int LearnState;   // 1 = a espera de botao, 2 = a espera de tecla
        private string learnDev, learnName;
        private int learnButton;

        public ButtonMapper(Action<string> log) { this.log = log; }

        private IntPtr Alloc(int n)
        {
            IntPtr p = Marshal.AllocHGlobal(n);
            for (int i = 0; i < n; i++) Marshal.WriteByte(p, i, 0);
            allocs.Add(p);
            return p;
        }

        public void Open(IntPtr hwnd)
        {
            Guid[] ag = {
                new Guid("A36D02E0-C9F3-11CF-BFC7-444553540000"), new Guid("A36D02E1-C9F3-11CF-BFC7-444553540000"),
                new Guid("A36D02E2-C9F3-11CF-BFC7-444553540000"), new Guid("A36D02F4-C9F3-11CF-BFC7-444553540000"),
                new Guid("A36D02F5-C9F3-11CF-BFC7-444553540000"), new Guid("A36D02E3-C9F3-11CF-BFC7-444553540000"),
                new Guid("A36D02E4-C9F3-11CF-BFC7-444553540000"), new Guid("A36D02E4-C9F3-11CF-BFC7-444553540000") };
            int objs = Axes + Buttons;
            IntPtr odf = Alloc(24 * objs);
            for (int i = 0; i < Axes; i++)
            {
                IntPtr g = Alloc(16); Marshal.StructureToPtr(ag[i], g, false);
                Marshal.WriteIntPtr(odf, i * 24, g);
                Marshal.WriteInt32(odf, i * 24 + 8, i * 4);
                Marshal.WriteInt32(odf, i * 24 + 12, unchecked((int)0x80FFFF03));
            }
            for (int j = 0; j < Buttons; j++)
            {
                int k = Axes + j;
                Marshal.WriteIntPtr(odf, k * 24, IntPtr.Zero);
                Marshal.WriteInt32(odf, k * 24 + 8, Axes * 4 + j);
                Marshal.WriteInt32(odf, k * 24 + 12, unchecked((int)0x80FFFF0C));
            }
            format = Alloc(32);
            Marshal.WriteInt32(format, 0, 32); Marshal.WriteInt32(format, 4, 24);
            Marshal.WriteInt32(format, 8, 1); Marshal.WriteInt32(format, 12, Size);
            Marshal.WriteInt32(format, 16, objs); Marshal.WriteIntPtr(format, 24, odf);
            buf = Alloc(Size);

            Guid iid = DI.IID_IDirectInput8W;
            if (DI.DirectInput8Create(DI.GetModuleHandle(null), 0x0800, ref iid, out di, IntPtr.Zero) < 0 || di == null) { log("Controllers: DirectInput failed."); return; }
            List<DIDEVICEINSTANCEW> list = new List<DIDEVICEINSTANCEW>();
            EnumDevicesCallback cb = delegate(IntPtr p, IntPtr r)
            {
                list.Add((DIDEVICEINSTANCEW)Marshal.PtrToStructure(p, typeof(DIDEVICEINSTANCEW))); return 1;
            };
            di.EnumDevices(DI.DI8DEVCLASS_GAMECTRL, cb, IntPtr.Zero, DI.DIEDFL_ATTACHEDONLY);
            GC.KeepAlive(cb);
            foreach (DIDEVICEINSTANCEW d in list)
            {
                uint d1 = BitConverter.ToUInt32(d.guidProduct.ToByteArray(), 0);
                int vid = (int)(d1 & 0xFFFF), pid = (int)(d1 >> 16);
                if (vid == 0x0EB7) continue;   // a Fanatec e lida pela saida de FFB
                Guid g = d.guidInstance;
                IDirectInputDevice8W dev;
                if (di.CreateDevice(ref g, out dev, IntPtr.Zero) < 0 || dev == null) continue;
                if (dev.SetDataFormat(format) < 0) continue;
                SetRange(dev);
                dev.SetCooperativeLevel(hwnd, 0x2 | 0x8);   // NONEXCLUSIVE | BACKGROUND
                dev.Acquire();
                Joy j = new Joy();
                j.Id = d.guidInstance.ToString(); j.Name = d.tszProductName.Trim(); j.Dev = dev; j.Vid = vid; j.Pid = pid;
                joys.Add(j);
                log(string.Format("Controllers: connected {0} (VID {1:X4} PID {2:X4})", j.Name, vid, pid));
            }
            Load();
        }

        private static void SetRange(IDirectInputDevice8W dev)
        {
            IntPtr p = Marshal.AllocHGlobal(24);
            try
            {
                Marshal.WriteInt32(p, 0, 24); Marshal.WriteInt32(p, 4, 16);
                Marshal.WriteInt32(p, 8, 0); Marshal.WriteInt32(p, 12, 0);
                Marshal.WriteInt32(p, 16, -10000); Marshal.WriteInt32(p, 20, 10000);
                dev.SetProperty(new IntPtr(4), p);
            }
            finally { Marshal.FreeHGlobal(p); }
        }

        // ------------------------------------------------------------------ leitura
        public void Poll(bool[] wheelButtons)
        {
            if (wheelButtons != null)
            {
                for (int i = 0; i < Buttons; i++) wheelNow[i] = i < wheelButtons.Length && wheelButtons[i];
                Apply(WheelId, "Volante Fanatec", wheelNow, null, null);
            }
            foreach (Joy j in joys)
            {
                j.Dev.Poll();
                if (j.Dev.GetDeviceState(Size, buf) < 0) { j.Dev.Acquire(); continue; }
                for (int i = 0; i < Axes; i++) j.Axis[i] = Marshal.ReadInt32(buf, i * 4);
                for (int i = 0; i < Buttons; i++) j.Now[i] = (Marshal.ReadByte(buf, Axes * 4 + i) & 0x80) != 0;
                if (!j.HasBase) { Array.Copy(j.Axis, j.Base, Axes); j.HasBase = true; }
                Apply(j.Id, j.Name, j.Now, j.Axis, j.Base);
            }
        }

        private readonly Dictionary<string, bool[]> lastButtons = new Dictionary<string, bool[]>();

        private void Apply(string id, string name, bool[] now, int[] axis, int[] bas)
        {
            bool[] prev;
            if (!lastButtons.TryGetValue(id, out prev)) { prev = new bool[Buttons]; lastButtons[id] = prev; }
            if (LearnState == 1)
                for (int b = 0; b < Buttons; b++)
                    if (now[b] && !prev[b])
                    {
                        learnDev = id; learnName = name; learnButton = b; LearnState = 2;
                        log("Button " + (b + 1) + " of " + name + ". Now press the KEY (Esc cancels).");
                        break;
                    }
            Array.Copy(now, prev, Buttons);

            foreach (Mapping m in Mappings)
            {
                if (m.DeviceId != id) continue;
                bool want;
                if (m.Kind == 0) want = m.Index < Buttons && now[m.Index];
                else
                {
                    if (axis == null || m.Index >= Axes) continue;
                    int dv = (axis[m.Index] - bas[m.Index]) * m.Sign;
                    want = m.Down ? dv > 3500 : dv > 5000;   // histerese
                }
                if (want != m.Down) { Press(m.Vks, want); m.Down = want; }
            }
        }

        // ------------------------------------------------------------------ teclado
        private static readonly int[] Extended = { 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2D, 0x2E, 0x6F, 0xA3, 0xA5, 0x5B, 0x5C, 0x5D };

        private void Press(int[] vks, bool down)
        {
            if (down) { for (int i = 0; i < vks.Length; i++) Key(vks[i], true); }
            else { for (int i = vks.Length - 1; i >= 0; i--) Key(vks[i], false); }
        }

        // Contagem por tecla: dois botoes que partilhem uma tecla (ex.: Shift) nao a largam um ao outro.
        private void Key(int vk, bool down)
        {
            int n;
            keyRefs.TryGetValue(vk, out n);
            if (down) { keyRefs[vk] = n + 1; if (n > 0) return; }
            else { if (n <= 0) return; keyRefs[vk] = n - 1; if (n > 1) return; }
            INPUT[] inp = new INPUT[1];
            inp[0].type = 1;
            inp[0].wScan = (ushort)MapVirtualKey((uint)vk, 0);
            uint flags = 0x0008;
            if (Array.IndexOf(Extended, vk) >= 0) flags |= 0x0001;
            if (!down) flags |= 0x0002;
            inp[0].dwFlags = flags;
            SendInput(1, inp, 40);
        }

        public void ReleaseAll()
        {
            foreach (Mapping m in Mappings) if (m.Down) { Press(m.Vks, false); m.Down = false; }
        }

        // ------------------------------------------------------------------ atribuicao manual
        public void StartLearn() { LearnState = 1; log("Press the controller button you want to set up..."); }
        public void CancelLearn() { if (LearnState != 0) log("Setup cancelled."); LearnState = 0; }

        public void LearnKey(Keys key)
        {
            if (LearnState != 2) return;
            Mappings.RemoveAll(delegate(Mapping x) { return x.DeviceId == learnDev && x.Kind == 0 && x.Index == learnButton; });
            Mapping m = new Mapping();
            m.DeviceId = learnDev; m.DeviceName = learnName; m.Kind = 0; m.Index = learnButton; m.Vks = new int[] { (int)key };
            Mappings.Add(m);
            LearnState = 0;
            Save();
            log("Saved: " + m);
        }

        public void Remove(Mapping m)
        {
            if (m == null) return;
            if (m.Down) Press(m.Vks, false);
            Mappings.Remove(m);
            Save();
        }

        // ------------------------------------------------------------------ importar do FS25
        private static int KeyToVk(string k)
        {
            if (!k.StartsWith("KEY_")) return 0;
            string n = k.Substring(4);
            if (n.Length == 1 && n[0] >= 'a' && n[0] <= 'z') return 0x41 + (n[0] - 'a');
            if (n.Length == 1 && n[0] >= '0' && n[0] <= '9') return 0x30 + (n[0] - '0');
            if (n.StartsWith("KP_") && n.Length == 4 && char.IsDigit(n[3])) return 0x60 + (n[3] - '0');
            if (n.Length >= 2 && n[0] == 'f' && char.IsDigit(n[1])) { int fn; if (int.TryParse(n.Substring(1), out fn) && fn >= 1 && fn <= 24) return 0x6F + fn; }
            switch (n)
            {
                case "space": return 0x20; case "return": return 0x0D; case "tab": return 0x09; case "backspace": return 0x08;
                case "esc": return 0x1B; case "lshift": return 0xA0; case "rshift": return 0xA1; case "lctrl": return 0xA2;
                case "rctrl": return 0xA3; case "lalt": return 0xA4; case "ralt": return 0xA5; case "up": return 0x26;
                case "down": return 0x28; case "left": return 0x25; case "right": return 0x27; case "insert": return 0x2D;
                case "delete": return 0x2E; case "home": return 0x24; case "end": return 0x23; case "pageup": return 0x21;
                case "pagedown": return 0x22; case "comma": return 0xBC; case "period": return 0xBE; case "minus": return 0xBD;
                case "plus": return 0xBB; case "slash": return 0xBF; case "backslash": return 0xDC; case "pause": return 0x13;
                case "print": return 0x2C; case "KP_plus": return 0x6B; case "KP_minus": return 0x6D; case "KP_multiply": return 0x6A;
                case "KP_divide": return 0x6F; case "KP_period": return 0x6E; case "KP_enter": return 0x0D;
            }
            return 0;
        }

        private static int[] Keys2Vks(string input)
        {
            string[] parts = input.Split(' ');
            List<int> v = new List<int>();
            foreach (string p in parts) { int k = KeyToVk(p); if (k == 0) return null; v.Add(k); }
            return v.ToArray();
        }

        public void ImportFromGame()
        {
            string file = Path.Combine(Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games"), "FarmingSimulator2025"), "inputBinding.xml");
            if (!File.Exists(file)) { log("Import: not found " + file); return; }
            string xml = File.ReadAllText(file);
            Regex actRx = new Regex("<actionBinding action=\"([^\"]+)\">(.*?)</actionBinding>", RegexOptions.Singleline);
            Regex bRx = new Regex("<binding device=\"([^\"]+)\" input=\"([^\"]+)\" axisComponent=\"([+-])\"");
            Regex btnRx = new Regex("^BUTTON_(\\d+)$"), axRx = new Regex("^AXIS_(\\d+)([+-])$");
            ReleaseAll();
            Mappings.RemoveAll(delegate(Mapping x) { return x.Action.Length > 0; });
            int made = 0, noKey = 0, noDev = 0;
            List<string> missing = new List<string>();
            foreach (Match am in actRx.Matches(xml))
            {
                string action = am.Groups[1].Value;
                List<string[]> kb = new List<string[]>(), pads = new List<string[]>();
                foreach (Match bm in bRx.Matches(am.Groups[2].Value))
                {
                    string dev = bm.Groups[1].Value, inp = bm.Groups[2].Value, comp = bm.Groups[3].Value;
                    if (dev == "KB_MOUSE_DEFAULT") { if (inp.StartsWith("KEY_")) kb.Add(new string[] { inp, comp }); }
                    else if (dev.StartsWith("0_") && dev.Length > 15 && dev[6] == '-') pads.Add(new string[] { dev, inp, comp });
                }
                foreach (string[] pb in pads)
                {
                    int vid, pid;
                    if (!int.TryParse(pb[0].Substring(2, 4), System.Globalization.NumberStyles.HexNumber, null, out vid)) continue;
                    if (!int.TryParse(pb[0].Substring(7, 4), System.Globalization.NumberStyles.HexNumber, null, out pid)) continue;
                    Joy joy = null;
                    foreach (Joy j in joys) if (j.Vid == vid && j.Pid == pid) { joy = j; break; }
                    if (joy == null) { noDev++; continue; }   // comando que ja nao esta ligado (ex.: G29 antigo)
                    int kind, index, sign = 1;
                    Match mb = btnRx.Match(pb[1]), ma = axRx.Match(pb[1]);
                    if (mb.Success) { kind = 0; index = int.Parse(mb.Groups[1].Value) - 1; }
                    else if (ma.Success) { kind = 1; index = int.Parse(ma.Groups[1].Value) - 1; sign = ma.Groups[2].Value == "+" ? 1 : -1; }
                    else continue;   // combinacoes (ex.: botao + eixo) nao suportadas
                    int[] vks = null;
                    foreach (string[] k in kb) if (k[1] == pb[2]) { vks = Keys2Vks(k[0]); if (vks != null) break; }
                    if (vks == null) { noKey++; missing.Add(action + " (" + joy.Name + " " + pb[1] + ")"); continue; }
                    Mapping m = new Mapping();
                    m.DeviceId = joy.Id; m.DeviceName = joy.Name; m.Kind = kind; m.Index = index; m.Sign = sign; m.Vks = vks; m.Action = action;
                    Mappings.Add(m);
                    made++;
                }
            }
            Save();
            log(string.Format("Import: {0} bindings created from FS25. Skipped: {1} from controllers not connected, {2} with no key in FS25.", made, noDev, noKey));
            foreach (string s in missing) log("  no key in FS25 (bind a key to this action in the game and import again): " + s);
        }

        // ------------------------------------------------------------------ ficheiro
        private void Save()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Mapping m in Mappings)
            {
                StringBuilder k = new StringBuilder();
                foreach (int v in m.Vks) { if (k.Length > 0) k.Append(","); k.Append(v); }
                sb.AppendLine(m.DeviceId + "|" + m.Kind + "|" + m.Index + "|" + m.Sign + "|" + k + "|" + m.DeviceName.Replace("|", " ") + "|" + m.Action);
            }
            try { File.WriteAllText(path, sb.ToString()); } catch (Exception ex) { log("Controllers: could not save - " + ex.Message); }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(path)) return;
                foreach (string line in File.ReadAllLines(path))
                {
                    string[] p = line.Split('|');
                    int kind, idx, sign;
                    if (p.Length < 7 || !int.TryParse(p[1], out kind) || !int.TryParse(p[2], out idx) || !int.TryParse(p[3], out sign)) continue;
                    List<int> v = new List<int>();
                    foreach (string s in p[4].Split(',')) { int x; if (int.TryParse(s, out x)) v.Add(x); }
                    Mapping m = new Mapping();
                    m.DeviceId = p[0]; m.Kind = kind; m.Index = idx; m.Sign = sign; m.Vks = v.ToArray(); m.DeviceName = p[5]; m.Action = p[6];
                    Mappings.Add(m);
                }
                log("Controllers: " + Mappings.Count + " bindings loaded.");
            }
            catch { }
        }

        public void Dispose()
        {
            ReleaseAll();
            foreach (Joy j in joys) { try { j.Dev.Unacquire(); Marshal.ReleaseComObject(j.Dev); } catch { } }
            joys.Clear();
            if (di != null) { try { Marshal.ReleaseComObject(di); } catch { } di = null; }
            foreach (IntPtr p in allocs) Marshal.FreeHGlobal(p);
            allocs.Clear();
        }
    }
}
