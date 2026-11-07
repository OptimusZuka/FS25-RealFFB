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
            r.Add(missing.Count == 0 ? new Check("Ficheiros da aplicacao", "OK", appDir) : new Check("Ficheiros da aplicacao", "ERRO", "em falta: " + string.Join(", ", missing.ToArray())));

            // Permissoes da pasta de dados
            try
            {
                string probe = Path.Combine(State.Dir, "perm.tmp");
                File.WriteAllText(probe, "x"); File.Delete(probe);
                r.Add(new Check("Permissoes (pasta de dados)", "OK", State.Dir));
            }
            catch (Exception ex) { r.Add(new Check("Permissoes (pasta de dados)", "ERRO", ex.Message)); }

            // Jogo
            string src;
            string game = st.Get("GameDir");
            if (!GameLocator.IsValidGameDir(game)) game = GameLocator.Find(out src);
            r.Add(game != null ? new Check("Farming Simulator 25", "OK", game) : new Check("Farming Simulator 25", "ERRO", "nao encontrado"));

            // Mod
            string profile = GameLocator.ProfileDir();
            string mods = st.Get("ModsDir") ?? Program.ModsDir(profile);
            string modZip = Path.Combine(mods, "FS25_RealFFBTelemetry.zip");
            if (File.Exists(modZip)) r.Add(new Check("Mod RealFFB", "OK", modZip));
            else
            {
                string found = FindModElsewhere(profile);
                r.Add(new Check("Mod RealFFB", "ERRO", found != null ? "instalado na pasta errada: " + found + " (esperado: " + mods + ")" : "em falta em " + mods));
            }

            // game.xml
            string gx = Path.Combine(profile, "game.xml");
            if (!File.Exists(gx)) r.Add(new Check("game.xml", "AVISO", "ainda nao existe (abra o FS25 uma vez)"));
            else
            {
                try
                {
                    string xml = File.ReadAllText(gx);
                    string j, v, f;
                    GameXml.Read(xml, out j, out v, out f);
                    string d = "joystick=" + j + " vibration=" + v + " forceFeedback=" + (f == "" ? "(ausente)" : f);
                    r.Add(GameXml.NeedsPatch(xml) ? new Check("game.xml", "ERRO", "configuracao invalida: " + d) : new Check("game.xml", "OK", d));
                }
                catch (Exception ex) { r.Add(new Check("game.xml", "ERRO", ex.Message)); }
            }

            // HidHide
            string ver = HidHide.InstalledVersion();
            if (ver == null) r.Add(new Check("HidHide", "ERRO", "nao instalado"));
            else
            {
                bool old = ver != "desconhecida" && HidHide.CompareVersions(ver, HidHide.MinVersion) < 0;
                r.Add(new Check("HidHide", old ? "AVISO" : "OK", "versao " + ver + (old ? " (desatualizada, minimo " + HidHide.MinVersion + ")" : "")));
                try
                {
                    List<string> apps = HidHide.Apps();
                    List<string> devs = HidHide.HiddenDevices();
                    r.Add(new Check("Driver HidHide", "OK", "a responder; ocultacao " + (HidHide.CloakOn() ? "ligada" : "desligada (a app liga ao abrir)")));
                    string exe = Path.Combine(appDir, "RealFFB.exe");
                    r.Add(Config.Contains(apps, exe) ? new Check("RealFFB autorizada no HidHide", "OK", "") : new Check("RealFFB autorizada no HidHide", "ERRO", "RealFFB.exe nao esta na lista de aplicacoes"));

                    List<FanatecBase> bases = Fanatec.Detect(null);
                    int fanHidden = 0; int otherHidden = 0;
                    foreach (string d in devs)
                    {
                        int pid;
                        if (Fanatec.GroupKey(d, out pid) != null) fanHidden++;
                        else if (d.StartsWith("HID\\", StringComparison.OrdinalIgnoreCase) && !Fanatec.IsWheelVendor(d)) otherHidden++;
                    }
                    if (bases.Count == 0) r.Add(new Check("Volante", "AVISO", "nenhum volante suportado ligado"));
                    else r.Add(new Check("Volante", "OK", Labels(bases)));
                    r.Add(fanHidden > 0 ? new Check("Volante escondido do jogo", "OK", fanHidden + " entrada(s)") : new Check("Volante escondido do jogo", "ERRO", "nenhuma entrada do volante na lista do HidHide (use 'Configurar dispositivos')"));
                    if (otherHidden > 0) r.Add(new Check("Outros controladores escondidos", "AVISO", otherHidden + " entrada(s) que nao sao o volante (configuradas por si ou por outro programa)"));
                }
                catch (Exception ex) { r.Add(new Check("Driver HidHide", "ERRO", "nao responde: " + ex.Message + " (reinicie o Windows se acabou de o instalar)")); }
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
            sb.AppendLine("FS25 RealFFB - relatorio de diagnostico");
            sb.AppendLine("Gerado: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC");
            sb.AppendLine("Nao contem nome de utilizador, nome do PC nem numeros de serie.");
            sb.AppendLine();
            int wmaj, wbuild; WinVersion(out wmaj, out wbuild);
            sb.AppendLine("Windows: " + wmaj + " build " + wbuild + (Environment.Is64BitOperatingSystem ? " x64" : " x86"));
            string exe = Path.Combine(appDir, "RealFFB.exe");
            if (File.Exists(exe)) sb.AppendLine("RealFFB: " + FileVersionInfo.GetVersionInfo(exe).FileVersion);
            sb.AppendLine("Admin: " + (IsAdmin() ? "sim" : "nao"));
            sb.AppendLine();
            sb.AppendLine("== Verificacao ==");
            try
            {
                foreach (Check c in Verify(appDir))
                    sb.AppendLine("[" + c.Status + "] " + c.Name + (c.Detail.Length > 0 ? ": " + c.Detail : ""));
            }
            catch (Exception ex) { sb.AppendLine("[ERRO] verificacao interrompida: " + ex.Message); }

            sb.AppendLine();
            sb.AppendLine("== Controladores de jogo (so fabricante/produto) ==");
            try
            {
                foreach (HidHide.GamingDevice g in HidHide.GamingDevices())
                    sb.AppendLine(g.Vid.ToString("X4") + ":" + g.Pid.ToString("X4") + "  " + (g.Product ?? g.FriendlyName));
            }
            catch (Exception ex) { sb.AppendLine("(indisponivel: " + ex.Message + ")"); }

            sb.AppendLine();
            sb.AppendLine("== Estado da instalacao ==");
            State st = State.Load();
            foreach (string k in new string[] { "Version", "GameDir", "ModsDir", "HH_BaseLabel", "GX_Patched", "PendingResume" })
                sb.AppendLine(k + "=" + (st.Get(k) ?? ""));

            sb.AppendLine();
            sb.AppendLine("== Ultimas linhas do registo da app ==");
            Tail(sb, Path.Combine(State.Dir, "RealFFB_log.txt"), 40, null);
            sb.AppendLine();
            sb.AppendLine("== Linhas RealFFB do log do FS25 ==");
            Tail(sb, Path.Combine(GameLocator.ProfileDir(), "log.txt"), 30, "RealFFB");
            return Anon(sb.ToString());
        }

        private static void Tail(StringBuilder sb, string file, int n, string filter)
        {
            try
            {
                if (!File.Exists(file)) { sb.AppendLine("(nao existe)"); return; }
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
            catch (Exception ex) { sb.AppendLine("(erro a ler: " + ex.Message + ")"); }
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
