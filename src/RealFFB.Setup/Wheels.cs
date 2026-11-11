// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// 0.14: deteccao UNIVERSAL do volante para o HidHide (substitui Fanatec.cs, que so via Fanatec/Logitech:
// com um MOZA R5 o instalador nao encontrava "volante suportado" e saia sem configurar NADA, nem sequer
// a autorizacao da RealFFB.exe).
//
// Como se identifica a base, por ordem de confianca:
//   1. o Windows (DirectInput) diz que o dispositivo tem force feedback  -> e a base (qualquer marca)
//   2. VID:PID conhecido (tabela abaixo; Fanatec e Logitech do historico do projeto)
//   3. ja estava escondido por nos numa configuracao anterior (reparacao)
// Pedais, shifters, travoes de mao, stalks, button boxes e tudo sem force feedback nunca sao escolhidos
// automaticamente. Se nada for identificado, o utilizador pode escolher a mao (com aviso) ou cancelar.
//
// Caminhos escondidos (documentacao oficial do HidHide, CLI Usage):
//   - os nos HID\ do dispositivo fisico (todas as coleccoes COLxx/MI_xx da mesma instancia);
//   - o contentor USB\ so se TODAS as funcoes desse contentor forem do volante (num dispositivo composto,
//     esconder o USB\ esconderia tambem as outras funcoes) ou se houver no XInput (xusb), que o abre;
//   - o no xusb, se existir.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using FS25RealFFB;

namespace RealFFBSetup
{
    // Um volante (dispositivo fisico) e os nos HID que o representam.
    internal sealed class WheelDevice
    {
        public int Vid, Pid;
        public string Model;
        public string Key;                       // VVVV:PPPP|instancia (sem a coleccao HID)
        public string Reason = "";               // ffb | known | previous | manual
        public List<string> InstanceIds = new List<string>();
        public string Label { get { return Model + " (" + Vid.ToString("X4") + ":" + Pid.ToString("X4") + ")"; } }
    }

    // Um dispositivo de jogo visto pelo DirectInput.
    internal sealed class FfbInfo
    {
        public int Vid, Pid;
        public bool Ffb;
        public string Product = "";
        public string Tag { get { return Vid.ToString("X4") + ":" + Pid.ToString("X4"); } }
    }

    internal static class Wheels
    {
        public const int Fanatec = 0x0EB7, Logitech = 0x046D;

        // Modelos conhecidos (historico do projeto). Nao e a lista de compatibilidade: qualquer base que o
        // Windows declare com force feedback tambem e reconhecida.
        public static string ModelName(int vid, int pid)
        {
            if (vid == Fanatec)
                switch (pid)
                {
                    case 0x0020: return "Fanatec CSL DD / GT DD Pro / ClubSport DD";
                    case 0x0006: return "Fanatec Podium DD1";
                    case 0x0007: return "Fanatec Podium DD2";
                    case 0x0001: return "Fanatec ClubSport Wheel Base V2";
                    case 0x0004: return "Fanatec ClubSport Wheel Base V2.5";
                    case 0x0E03: return "Fanatec CSL Elite Wheel Base";
                    case 0x0005: return "Fanatec CSL Elite Wheel Base (PS4)";
                    case 0x0011: return "Fanatec CSR Elite";
                }
            if (vid == Logitech)
                switch (pid)
                {
                    case 0xC24F: return "Logitech G29";
                    case 0xC262: return "Logitech G920";
                    case 0xC266: case 0xC267: return "Logitech G923 (PlayStation/PC)";
                    case 0xC26D: case 0xC26E: return "Logitech G923 (Xbox/PC)";
                    case 0xC268: case 0xC272: return "Logitech G PRO Racing Wheel";
                    case 0xC29B: return "Logitech G27";
                    case 0xC299: return "Logitech G25";
                    case 0xC29A: return "Logitech Driving Force GT";
                }
            return null;
        }

        // Nomes que nunca sao a base, mesmo que o dispositivo declare force feedback (pedais ativos, etc.).
        private static readonly string[] NotABase = { "pedal", "shifter", "gearbox", "handbrake", "hand brake", "stalk",
            "button box", "buttonbox", "control system", "dashboard", "keyboard", "mouse", "headset" };

        public static bool LooksLikeAccessory(string product)
        {
            string n = (product ?? "").ToLowerInvariant();
            foreach (string w in NotABase) if (n.Contains(w)) return true;
            return false;
        }

        // Chave do dispositivo fisico a partir de um instance id HID de QUALQUER fabricante, ex.:
        // HID\VID_346E&PID_0004&MI_00&COL01\8&1A2B&0&0000  ->  346E:0004|8&1A2B&0
        public static string GroupKey(string instanceId, out int vid, out int pid)
        {
            pid = -1; vid = -1;
            Match m = Regex.Match(instanceId ?? "", "^HID\\\\VID_([0-9A-F]{4})&PID_([0-9A-F]{4})[^\\\\]*\\\\(.+)$", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            vid = Convert.ToInt32(m.Groups[1].Value, 16);
            pid = Convert.ToInt32(m.Groups[2].Value, 16);
            string inst = m.Groups[3].Value;
            int amp = inst.LastIndexOf('&');
            if (amp > 0) inst = inst.Substring(0, amp);
            return vid.ToString("X4") + ":" + pid.ToString("X4") + "|" + inst.ToUpperInvariant();
        }

        public static string GroupKey(string instanceId, out int pid) { int vid; return GroupKey(instanceId, out vid, out pid); }

        public static bool HasFfb(List<FfbInfo> ffb, int vid, int pid)
        {
            if (ffb == null) return false;
            foreach (FfbInfo f in ffb) if (f.Ffb && f.Vid == vid && f.Pid == pid) return true;
            return false;
        }

        private static string FfbName(List<FfbInfo> ffb, int vid, int pid)
        {
            if (ffb == null) return null;
            foreach (FfbInfo f in ffb) if (f.Vid == vid && f.Pid == pid && f.Product.Length > 0) return f.Product;
            return null;
        }

        // Agrupa os dispositivos do HidHide (--dev-gaming) por dispositivo fisico e devolve as bases.
        // ffb: lista do DirectInput (pode ser null); previous: chave configurada antes (pode ser null).
        // others recebe os outros dispositivos de jogo (para a escolha manual), sem duplicados.
        public static List<WheelDevice> Find(List<HidHide.GamingDevice> gaming, List<FfbInfo> ffb, string previous, List<WheelDevice> others)
        {
            Dictionary<string, WheelDevice> map = new Dictionary<string, WheelDevice>(StringComparer.OrdinalIgnoreCase);
            List<WheelDevice> order = new List<WheelDevice>();
            foreach (HidHide.GamingDevice g in gaming)
            {
                int vid, pid;
                string key = GroupKey(g.InstancePath, out vid, out pid);
                if (key == null) continue;
                WheelDevice w;
                if (!map.TryGetValue(key, out w))
                {
                    w = new WheelDevice(); w.Vid = vid; w.Pid = pid; w.Key = key;
                    w.Model = ModelName(vid, pid) ?? FfbName(ffb, vid, pid) ?? g.Product ?? g.FriendlyName ?? "Game controller";
                    map[key] = w; order.Add(w);
                }
                if (!Config.Contains(w.InstanceIds, g.InstancePath)) w.InstanceIds.Add(g.InstancePath);
            }
            List<WheelDevice> wheels = new List<WheelDevice>();
            foreach (WheelDevice w in order)
            {
                bool known = ModelName(w.Vid, w.Pid) != null;
                bool ffbOk = HasFfb(ffb, w.Vid, w.Pid) && !LooksLikeAccessory(w.Model);
                bool prev = previous != null && previous.Equals(w.Key, StringComparison.OrdinalIgnoreCase);
                if (ffbOk) w.Reason = "ffb";
                else if (known) w.Reason = "known";
                else if (prev) w.Reason = "previous";
                if (w.Reason.Length > 0) wheels.Add(w);
                else if (others != null) others.Add(w);
            }
            return wheels;
        }

        // Caminhos a esconder para o volante escolhido (ver regras no cabecalho).
        public static List<string> PathsFor(WheelDevice w, List<HidHide.GamingDevice> gaming, Action<string> log)
        {
            List<string> r = new List<string>();
            List<HidHide.GamingDevice> mine = new List<HidHide.GamingDevice>();
            foreach (HidHide.GamingDevice g in gaming)
            {
                int vid, pid;
                string key = GroupKey(g.InstancePath, out vid, out pid);
                if (key == null || !key.Equals(w.Key, StringComparison.OrdinalIgnoreCase)) continue;
                mine.Add(g);
                if (!Config.Contains(r, g.InstancePath)) r.Add(g.InstancePath);
            }
            foreach (HidHide.GamingDevice g in mine)
            {
                if (!string.IsNullOrEmpty(g.XusbPath) && !Config.Contains(r, g.XusbPath)) r.Add(g.XusbPath);
                if (string.IsNullOrEmpty(g.ContainerPath) || Config.Contains(r, g.ContainerPath)) continue;
                string cu = g.ContainerPath.ToUpperInvariant();
                bool sameDevice = cu.Contains("VID_" + w.Vid.ToString("X4")) && cu.Contains("PID_" + w.Pid.ToString("X4"));
                // contentor so com as funcoes deste volante (contagem do HidHide; 0 = nao indicada)
                bool onlyWheel = g.ContainerCount <= 0 || g.ContainerCount <= mine.Count;
                if (sameDevice && (onlyWheel || !string.IsNullOrEmpty(g.XusbPath))) r.Add(g.ContainerPath);
                else if (log != null) log("Not hidden (shared USB container): " + g.ContainerPath);
            }
            return r;
        }

        // ------------------------------------------------------------------ DirectInput
        // Lista os controladores vistos pelo DirectInput e se tem force feedback.
        // appExe: se existir, pergunta a RealFFB.exe (que esta autorizada no HidHide e por isso ve o volante
        // mesmo escondido); senao, ou se falhar, enumera neste processo.
        public static List<FfbInfo> ScanFfb(string appExe, Action<string> log)
        {
            if (appExe != null && File.Exists(appExe))
            {
                try
                {
                    string tmp = Path.Combine(Path.GetTempPath(), "realffb_wheels_" + Guid.NewGuid().ToString("N") + ".txt");
                    ProcessStartInfo psi = new ProcessStartInfo(appExe, "--list-wheels \"" + tmp + "\"");
                    psi.UseShellExecute = false; psi.CreateNoWindow = true;
                    using (Process p = Process.Start(psi))
                    {
                        if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } throw new InvalidOperationException("no answer"); }
                    }
                    if (File.Exists(tmp))
                    {
                        List<FfbInfo> l = ParseList(File.ReadAllText(tmp, Encoding.UTF8));
                        try { File.Delete(tmp); } catch { }
                        if (log != null) log("DirectInput (RealFFB.exe): " + l.Count + " controller(s)");
                        return l;
                    }
                }
                catch (Exception ex) { if (log != null) log("RealFFB.exe --list-wheels failed: " + ex.Message); }
            }
            List<FfbInfo> own = ScanHere(log);
            if (log != null) log("DirectInput (setup): " + own.Count + " controller(s)");
            return own;
        }

        // Formato escrito pela RealFFB.exe --list-wheels: VVVV:PPPP|ffb(0/1)|nome
        public static List<FfbInfo> ParseList(string text)
        {
            List<FfbInfo> l = new List<FfbInfo>();
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                Match m = Regex.Match(line, "^([0-9A-Fa-f]{4}):([0-9A-Fa-f]{4})\\|([01])\\|(.*)$");
                if (!m.Success) continue;
                FfbInfo f = new FfbInfo();
                f.Vid = Convert.ToInt32(m.Groups[1].Value, 16); f.Pid = Convert.ToInt32(m.Groups[2].Value, 16);
                f.Ffb = m.Groups[3].Value == "1"; f.Product = m.Groups[4].Value.Trim();
                l.Add(f);
            }
            return l;
        }

        private static List<FfbInfo> ScanHere(Action<string> log)
        {
            List<FfbInfo> list = new List<FfbInfo>();
            IDirectInput8W di = null;
            try
            {
                Guid iid = DI.IID_IDirectInput8W;
                int hr = DI.DirectInput8Create(DI.GetModuleHandle(null), 0x0800, ref iid, out di, IntPtr.Zero);
                if (hr < 0 || di == null) { if (log != null) log("DirectInput8Create " + DI.Hr(hr)); return list; }
                List<DIDEVICEINSTANCEW> all = new List<DIDEVICEINSTANCEW>(), ff = new List<DIDEVICEINSTANCEW>();
                EnumDevicesCallback cbAll = delegate(IntPtr p, IntPtr r) { all.Add((DIDEVICEINSTANCEW)Marshal.PtrToStructure(p, typeof(DIDEVICEINSTANCEW))); return 1; };
                EnumDevicesCallback cbFf = delegate(IntPtr p, IntPtr r) { ff.Add((DIDEVICEINSTANCEW)Marshal.PtrToStructure(p, typeof(DIDEVICEINSTANCEW))); return 1; };
                di.EnumDevices(DI.DI8DEVCLASS_GAMECTRL, cbAll, IntPtr.Zero, DI.DIEDFL_ATTACHEDONLY);
                di.EnumDevices(DI.DI8DEVCLASS_GAMECTRL, cbFf, IntPtr.Zero, DI.DIEDFL_ATTACHEDONLY | DI.DIEDFL_FORCEFEEDBACK);
                GC.KeepAlive(cbAll); GC.KeepAlive(cbFf);
                foreach (DIDEVICEINSTANCEW d in all)
                {
                    FfbInfo f = new FfbInfo();
                    uint v = BitConverter.ToUInt32(d.guidProduct.ToByteArray(), 0);
                    f.Vid = (int)(v & 0xFFFF); f.Pid = (int)(v >> 16);
                    f.Product = d.tszProductName ?? "";
                    foreach (DIDEVICEINSTANCEW x in ff) if (x.guidInstance == d.guidInstance) f.Ffb = true;
                    list.Add(f);
                }
            }
            catch (Exception ex) { if (log != null) log("DirectInput scan failed: " + ex.Message); }
            finally { if (di != null) { try { Marshal.ReleaseComObject(di); } catch { } } }
            return list;
        }

        // Bases com force feedback vistas pelo DirectInput (para o passo 3 do instalador e o diagnostico).
        public static List<FfbInfo> FfbBases(List<FfbInfo> scan)
        {
            List<FfbInfo> r = new List<FfbInfo>();
            foreach (FfbInfo f in scan)
            {
                bool known = ModelName(f.Vid, f.Pid) != null;
                if ((f.Ffb && !LooksLikeAccessory(f.Product)) || known)
                {
                    bool dup = false;
                    foreach (FfbInfo x in r) if (x.Vid == f.Vid && x.Pid == f.Pid) dup = true;
                    if (!dup) r.Add(f);
                }
            }
            return r;
        }

        public static string NameOf(FfbInfo f)
        {
            return (ModelName(f.Vid, f.Pid) ?? (f.Product.Length > 0 ? f.Product : "Wheel")) + " (" + f.Tag + ")";
        }
    }
}
