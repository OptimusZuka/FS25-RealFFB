// Verificacao e relatorio de diagnostico anonimizado (para GitHub Issues).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace RealFFBSetup
{
    internal static class Diagnostics
    {
        public static string Sha256(string file)
        {
            using (SHA256 h = SHA256.Create())
            using (FileStream fs = File.OpenRead(file))
                return BitConverter.ToString(h.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }

        // Retira o nome de utilizador e o nome do PC de qualquer texto.
        public static string Anon(string s)
        {
            if (s == null) return "";
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home)) s = Replace(s, home, "%USERPROFILE%");
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(docs)) s = Replace(s, docs, "%DOCUMENTS%");
            if (Environment.UserName.Length > 2) s = Replace(s, Environment.UserName, "<user>");
            if (Environment.MachineName.Length > 2) s = Replace(s, Environment.MachineName, "<pc>");
            return s;
        }

        private static string Replace(string s, string what, string with)
        {
            int i;
            while ((i = s.IndexOf(what, StringComparison.OrdinalIgnoreCase)) >= 0) s = s.Substring(0, i) + with + s.Substring(i + what.Length);
            return s;
        }

        public sealed class Check
        {
            public string Name, Status, Detail;   // Status: OK / AVISO / ERRO
            public Check(string n, string s, string d) { Name = n; Status = s; Detail = d; }
        }

        // Verificacao completa. appDir = pasta da RealFFB.exe.
        public static List<Check> Verify(string appDir)
        {
            List<Check> r = new List<Check>();
            State st = State.Load();

            // Ficheiros da app
            string[] files = { "RealFFB.exe", "RealFFB.Setup.exe" };
            List<string> missing = new List<string>();
            foreach (string f in files) if (!File.Exists(Path.Combine(appDir, f))) missing.Add(f);
            r.Add(missing.Count == 0 ? new Check("App files", "OK", appDir) : new Check("App files", "ERROR", "missing: " + string.Join(", ", missing.ToArray())));

            // Permissoes da pasta de dados
            try
            {
                string probe = Path.Combine(State.Dir, "perm.tmp");
                File.WriteAllText(probe, "x"); File.Delete(probe);
                r.Add(new Check("Permissions (data folder)", "OK", State.Dir));
            }
            catch (Exception ex) { r.Add(new Check("Permissions (data folder)", "ERROR", ex.Message)); }

            // Jogo
            string src;
            string game = st.Get("GameDir");
            if (!GameLocator.IsValidGameDir(game)) game = GameLocator.Find(out src);
            r.Add(game != null ? new Check("Farming Simulator 25", "OK", game) : new Check("Farming Simulator 25", "ERROR", "not found"));

            // Mod
            string profile = GameLocator.ProfileDir();
            string mods = st.Get("ModsDir") ?? Program.ModsDir(profile);
            string modZip = Path.Combine(mods, "FS25_RealFFBTelemetry.zip");
            if (File.Exists(modZip)) r.Add(new Check("Mod RealFFB", "OK", modZip));
            else
            {
                string found = FindModElsewhere(profile);
                r.Add(new Check("Mod RealFFB", "ERROR", found != null ? "installed in the wrong folder: " + found + " (expected: " + mods + ")" : "missing in " + mods));
            }

            // game.xml
            string gx = Path.Combine(profile, "game.xml");
            if (!File.Exists(gx)) r.Add(new Check("game.xml", "WARN", "does not exist yet (open FS25 once)"));
            else
            {
                try
                {
                    string xml = File.ReadAllText(gx);
                    string j, v, f;
                    GameXml.Read(xml, out j, out v, out f);
                    string d = "joystick=" + j + " vibration=" + v + " forceFeedback=" + (f == "" ? "(missing)" : f);
                    r.Add(GameXml.NeedsPatch(xml) ? new Check("game.xml", "ERROR", "invalid settings: " + d) : new Check("game.xml", "OK", d));
                }
                catch (Exception ex) { r.Add(new Check("game.xml", "ERROR", ex.Message)); }
            }

            // HidHide (0.14): estado claro de cada passo, distinguindo erro de configuracao de problema de hardware
            VerifyHidHide(r, appDir, st);
            return r;
        }

        private static void VerifyHidHide(List<Check> r, string appDir, State st)
        {
            string exe = Path.Combine(appDir, "RealFFB.exe");
            string ver = HidHide.InstalledVersion();
            if (ver == null) { r.Add(new Check("HidHide installed", "ERROR", "not installed (run the FS25 RealFFB installer again)")); return; }
            bool old = ver != "desconhecida" && HidHide.CompareVersions(ver, HidHide.MinVersion) < 0;
            r.Add(new Check("HidHide installed", old ? "WARN" : "OK", "version " + ver + (old ? " (outdated, minimum " + HidHide.MinVersion + ")" : "")));

            List<string> apps, devs; List<HidHide.GamingDevice> gaming; bool cloak;
            try
            {
                apps = HidHide.Apps(); devs = HidHide.HiddenDevices(); cloak = HidHide.CloakOn();
                gaming = HidHide.GamingDevices();
                r.Add(new Check("HidHide driver", "OK", "responding"));
            }
            catch (Exception ex)
            {
                r.Add(new Check("HidHide driver", "ERROR", "not responding: " + ex.Message + " (restart Windows if you just installed it; close the HidHide Configuration Client)"));
                return;
            }
            r.Add(new Check("Device hiding", "OK", cloak ? "ENABLED" : "DISABLED (normal while FS25 RealFFB is closed: the app enables it when it opens)"));

            bool appOk = Config.Contains(apps, exe);
            string otherReg = null;
            foreach (string a in apps) if (Path.GetFileName(a).Equals("RealFFB.exe", StringComparison.OrdinalIgnoreCase) && !a.Equals(exe, StringComparison.OrdinalIgnoreCase)) otherReg = a;
            r.Add(appOk ? new Check("FS25 RealFFB whitelist", "OK", exe)
                        : new Check("FS25 RealFFB whitelist", "ERROR", "RealFFB.exe is not in the application list" + (otherReg != null ? " (an entry exists for another folder: " + otherReg + ")" : "") + " -> configuration problem: use 'FS25 RealFFB - Configure devices'"));

            List<FfbInfo> ffb = Wheels.ScanFfb(File.Exists(exe) ? exe : null, null);
            List<WheelDevice> others = new List<WheelDevice>();
            List<WheelDevice> wheels = Wheels.Find(gaming, ffb, st.Get("HH_Base"), others);
            if (wheels.Count == 0)
                r.Add(new Check("Steering wheel detected", "ERROR", "no force feedback wheel base found by Windows -> hardware/driver problem: check the USB cable and power, switch the base on, install the manufacturer's driver (Fanatec: PC mode)"));
            else
            {
                List<string> names = new List<string>();
                foreach (WheelDevice w in wheels) names.Add(w.Label + (w.Reason == "ffb" ? "" : " [" + w.Reason + "]"));
                r.Add(new Check("Steering wheel detected", "OK", string.Join("; ", names.ToArray())));
            }

            // volante escolhido: todas as entradas necessarias estao na lista?
            WheelDevice sel = null;
            foreach (WheelDevice w in wheels) if (w.Key.Equals(st.Get("HH_Base") ?? "", StringComparison.OrdinalIgnoreCase)) sel = w;
            if (sel == null)
                foreach (WheelDevice w in wheels)
                    foreach (string id in w.InstanceIds) if (Config.Contains(devs, id)) sel = w;
            bool hiddenOk = false;
            if (sel != null)
            {
                List<string> need = Wheels.PathsFor(sel, gaming, null);
                List<string> miss = new List<string>();
                foreach (string n in need) if (!Config.Contains(devs, n)) miss.Add(n);
                hiddenOk = miss.Count == 0 && need.Count > 0;
                r.Add(hiddenOk ? new Check("Steering wheel selected for hiding", "OK", sel.Label + " (" + need.Count + " entr(ies))")
                               : new Check("Steering wheel selected for hiding", "ERROR", sel.Label + ": " + miss.Count + " entr(ies) missing -> configuration problem: use 'FS25 RealFFB - Configure devices'"));
            }
            else if (wheels.Count > 0)
                r.Add(new Check("Steering wheel selected for hiding", "ERROR", "no wheel entry in the HidHide list -> configuration problem: use 'FS25 RealFFB - Configure devices'"));
            else
                r.Add(new Check("Steering wheel selected for hiding", "WARN", "no wheel connected, cannot check"));

            // a RealFFB.exe ve a base com force feedback? (perguntado a propria app, que esta autorizada)
            if (sel != null)
                r.Add(appOk && Wheels.HasFfb(ffb, sel.Vid, sel.Pid)
                    ? new Check("FS25 RealFFB device access", "OK", "RealFFB.exe sees " + sel.Label + " with force feedback")
                    : new Check("FS25 RealFFB device access", appOk ? "WARN" : "ERROR", appOk ? "RealFFB.exe does not report force feedback for " + sel.Label + " (wheel off, or the manufacturer's software is holding it)" : "RealFFB.exe is not allowed in HidHide, so it cannot see the hidden wheel"));

            int otherHidden = 0;
            foreach (string d in devs)
            {
                bool mine = false;
                foreach (WheelDevice w in wheels) if (Config.Contains(Wheels.PathsFor(w, gaming, null), d)) mine = true;
                if (!mine && st.GetList("HH_DevAdded").Count > 0 && Config.Contains(st.GetList("HH_DevAdded"), d)) mine = true;
                if (!mine) otherHidden++;
            }
            if (otherHidden > 0) r.Add(new Check("Other HidHide entries", "OK", otherHidden + " entr(ies) set by you or another program (left untouched)"));

            bool complete = appOk && hiddenOk;
            string last = st.Get("HH_LastResult");
            string lastTxt = last == null ? "" : " | last setup: " + last + " (" + (st.Get("HH_LastTime") ?? "") + ")" + (st.Get("HH_LastError") != null ? ": " + st.Get("HH_LastError") : "");
            r.Add(complete ? new Check("HidHide configuration", "OK", "COMPLETE" + lastTxt)
                           : new Check("HidHide configuration", wheels.Count == 0 && appOk ? "WARN" : "ERROR", "INCOMPLETE" + lastTxt));
        }

        private static string FindModElsewhere(string profile)
        {
            try
            {
                foreach (string f in Directory.GetFiles(profile, "FS25_RealFFBTelemetry*.zip", SearchOption.AllDirectories)) return f;
                foreach (string d in Directory.GetDirectories(profile, "FS25_RealFFBTelemetry*", SearchOption.AllDirectories)) return d;
            }
            catch { }
            return null;
        }

        public static string Report(string appDir)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("FS25 RealFFB - diagnostic report");
            sb.AppendLine("Created: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC");
            sb.AppendLine("Contains no user name, PC name or serial numbers.");
            sb.AppendLine();
            int wmaj, wbuild; WinVersion(out wmaj, out wbuild);
            sb.AppendLine("Windows: " + wmaj + " build " + wbuild + (Environment.Is64BitOperatingSystem ? " x64" : " x86"));
            string exe = Path.Combine(appDir, "RealFFB.exe");
            if (File.Exists(exe)) sb.AppendLine("RealFFB: " + FileVersionInfo.GetVersionInfo(exe).FileVersion);
            sb.AppendLine("Admin: " + (IsAdmin() ? "yes" : "no"));
            sb.AppendLine();
            sb.AppendLine("== Checks ==");
            try
            {
                foreach (Check c in Verify(appDir))
                    sb.AppendLine("[" + c.Status + "] " + c.Name + (c.Detail.Length > 0 ? ": " + c.Detail : ""));
            }
            catch (Exception ex) { sb.AppendLine("[ERROR] checks interrupted: " + ex.Message); }

            sb.AppendLine();
            sb.AppendLine("== Game controllers (vendor/product only) ==");
            try
            {
                List<string> hiddenNow = HidHide.HiddenDevices();
                foreach (HidHide.GamingDevice g in HidHide.GamingDevices())
                    sb.AppendLine(g.Vid.ToString("X4") + ":" + g.Pid.ToString("X4") + "  " + (g.Product ?? g.FriendlyName) + (Config.Contains(hiddenNow, g.InstancePath) ? "  [hidden]" : ""));
                sb.AppendLine("-- DirectInput (seen by RealFFB.exe) --");
                string exe2 = Path.Combine(appDir, "RealFFB.exe");
                foreach (FfbInfo f in Wheels.ScanFfb(File.Exists(exe2) ? exe2 : null, null))
                    sb.AppendLine(f.Tag + "  " + f.Product + (f.Ffb ? "  [force feedback]" : ""));
            }
            catch (Exception ex) { sb.AppendLine("(unavailable: " + ex.Message + ")"); }

            sb.AppendLine();
            sb.AppendLine("== Installation state ==");
            State st = State.Load();
            foreach (string k in new string[] { "Version", "GameDir", "ModsDir", "HH_BaseLabel", "HH_LastResult", "HH_LastTime", "HH_InstalledByUs", "HH_Preexisting", "GX_Patched", "PendingResume" })
                sb.AppendLine(k + "=" + (st.Get(k) ?? ""));

            sb.AppendLine();
            sb.AppendLine("== Last lines of the app log ==");
            Tail(sb, Path.Combine(State.Dir, "RealFFB_log.txt"), 40, null);
            sb.AppendLine();
            sb.AppendLine("== RealFFB lines from the FS25 log ==");
            Tail(sb, Path.Combine(GameLocator.ProfileDir(), "log.txt"), 30, "RealFFB");
            return Anon(sb.ToString());
        }

        private static void Tail(StringBuilder sb, string file, int n, string filter)
        {
            try
            {
                if (!File.Exists(file)) { sb.AppendLine("(does not exist)"); return; }
                List<string> lines = new List<string>();
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader rd = new StreamReader(fs))
                {
                    string l;
                    while ((l = rd.ReadLine()) != null)
                    {
                        if (filter != null && l.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        lines.Add(l);
                        if (lines.Count > n) lines.RemoveAt(0);
                    }
                }
                foreach (string l in lines) sb.AppendLine(l);
            }
            catch (Exception ex) { sb.AppendLine("(read error: " + ex.Message + ")"); }
        }

        // Environment.OSVersion devolve 6.2 no Windows 10/11 sem manifesto: ler do registo.
        public static void WinVersion(out int major, out int build)
        {
            major = 0; build = 0;
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    object m = k.GetValue("CurrentMajorVersionNumber");
                    if (m is int) major = (int)m;
                    int.TryParse(k.GetValue("CurrentBuild") as string, out build);
                }
            }
            catch { }
            if (major == 0) major = Environment.OSVersion.Version.Major;
        }

        public static bool IsAdmin()
        {
            try { return new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator); }
            catch { return false; }
        }
    }
}
