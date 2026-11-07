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

            // HidHide
            string ver = HidHide.InstalledVersion();
            if (ver == null) r.Add(new Check("HidHide", "ERROR", "not installed"));
            else
            {
                bool old = ver != "desconhecida" && HidHide.CompareVersions(ver, HidHide.MinVersion) < 0;
                r.Add(new Check("HidHide", old ? "WARN" : "OK", "version " + ver + (old ? " (outdated, minimum " + HidHide.MinVersion + ")" : "")));
                try
                {
                    List<string> apps = HidHide.Apps();
                    List<string> devs = HidHide.HiddenDevices();
                    r.Add(new Check("HidHide driver", "OK", "responding; hiding " + (HidHide.CloakOn() ? "on" : "off (the app turns it on when it opens)")));
                    string exe = Path.Combine(appDir, "RealFFB.exe");
                    r.Add(Config.Contains(apps, exe) ? new Check("RealFFB allowed in HidHide", "OK", "") : new Check("RealFFB allowed in HidHide", "ERROR", "RealFFB.exe is not in the application list"));

                    List<FanatecBase> bases = Fanatec.Detect(null);
                    int fanHidden = 0; int otherHidden = 0;
                    foreach (string d in devs)
                    {
                        int pid;
                        if (Fanatec.GroupKey(d, out pid) != null) fanHidden++;
                        else if (d.StartsWith("HID\\", StringComparison.OrdinalIgnoreCase) && !Fanatec.IsWheelVendor(d)) otherHidden++;
                    }
                    if (bases.Count == 0) r.Add(new Check("Wheel", "WARN", "no supported wheel connected"));
                    else r.Add(new Check("Wheel", "OK", Labels(bases)));
                    r.Add(fanHidden > 0 ? new Check("Wheel hidden from the game", "OK", fanHidden + " entr(ies)") : new Check("Wheel hidden from the game", "ERROR", "no wheel entry in the HidHide list (use 'Configure devices')"));
                    if (otherHidden > 0) r.Add(new Check("Other controllers hidden", "WARN", otherHidden + " entr(ies) that are not the wheel (set by you or another program)"));
                }
                catch (Exception ex) { r.Add(new Check("HidHide driver", "ERROR", "not responding: " + ex.Message + " (restart Windows if you just installed it)")); }
            }
            return r;
        }

        private static string Labels(List<FanatecBase> l)
        {
            List<string> s = new List<string>();
            foreach (FanatecBase b in l) s.Add(b.Label);
            return string.Join("; ", s.ToArray());
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
                foreach (HidHide.GamingDevice g in HidHide.GamingDevices())
                    sb.AppendLine(g.Vid.ToString("X4") + ":" + g.Pid.ToString("X4") + "  " + (g.Product ?? g.FriendlyName));
            }
            catch (Exception ex) { sb.AppendLine("(unavailable: " + ex.Message + ")"); }

            sb.AppendLine();
            sb.AppendLine("== Installation state ==");
            State st = State.Load();
            foreach (string k in new string[] { "Version", "GameDir", "ModsDir", "HH_BaseLabel", "GX_Patched", "PendingResume" })
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
