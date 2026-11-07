// RealFFB.Setup.exe - ajudante do instalador (chamado pelo Inno Setup, pela app e na desinstalacao).
// Cada comando faz uma coisa, regista o que alterou e escreve o resultado em --out (ini [r]).
//
//   detect            estado do sistema, FS25, Fanatec, HidHide
//   hidhide-install   descarrega o HidHide oficial, valida a assinatura e instala (UAC)
//   configure         escolhe a base Fanatec, confirma e configura o HidHide (+ game.xml)
//   schedule-resume   retoma a configuracao depois de reiniciar o Windows
//   resume            (corre sozinho depois de reiniciar)
//   patch-gamexml     ajusta o game.xml (com copia de seguranca)
//   restore           desinstalacao: repoe HidHide e game.xml   [--purge]
//   verify            verificacao final
//   diagnose          relatorio anonimizado
//
// Codigos de saida: 0 ok, 1 erro, 2 cancelado/nada feito, 3 reiniciar o Windows.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RealFFBSetup
{
    internal static class Program
    {
        internal static readonly Dictionary<string, string> Opt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly StringBuilder LogText = new StringBuilder();
        private const string RunOnceKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
        private const string RunOnceName = "FS25RealFFB_Resume";

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            if (args.Length == 0)
            {
                Ui.Info("Ajuda", "Utilitario do FS25 RealFFB.\nUse o atalho 'Configurar dispositivos' ou o botao de diagnostico da app.");
                return 2;
            }
            string cmd = args[0].ToLowerInvariant();
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--")) continue;
                string k = args[i].Substring(2);
                string v = (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? args[++i] : "1";
                Opt[k] = v;
            }
            Result res = new Result();
            int code;
            try
            {
                switch (cmd)
                {
                    case "detect": code = Detect(res); break;
                    case "hidhide-install": code = InstallHidHide(res); break;
                    case "configure": code = Configure(res, false); break;
                    case "resume": code = Configure(res, true); break;
                    case "schedule-resume": code = ScheduleResume(res); break;
                    case "patch-gamexml": code = PatchGameXml(res); break;
                    case "restore": code = Restore(res); break;
                    case "verify": code = VerifyCmd(res); break;
                    case "diagnose": code = Diagnose(res); break;
                    case "remember": code = Remember(res); break;
                    case "install": code = LocalInstaller.Install(); break;
                    case "uninstall": code = LocalInstaller.Uninstall(); break;
                    default: res.Add("error", "comando desconhecido: " + cmd); code = 1; break;
                }
            }
            catch (Exception ex)
            {
                res.Add("error", ex.Message);
                Log("ERRO " + cmd + ": " + ex);
                code = 1;
            }
            res.Add("exit", code.ToString());
            res.Add("log", Diagnostics.Anon(LogText.ToString().Replace("\r\n", " | ").Replace("\n", " | ")));
            try { res.Write(Get("out")); } catch { }
            AppendSetupLog(cmd, code);
            return code;
        }

        private static string Get(string k) { string v; return Opt.TryGetValue(k, out v) ? v : null; }

        internal static void Log(string s) { LogText.AppendLine(s); }

        private static void AppendSetupLog(string cmd, int code)
        {
            try
            {
                File.AppendAllText(Path.Combine(State.Dir, "setup_log.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + cmd + "] exit " + code + "\r\n" + LogText.ToString());
            }
            catch { }
        }

        private static string AppDir() { return Get("app-dir") ?? Application.StartupPath; }
        private static string AppExe() { return Get("app") ?? Path.Combine(AppDir(), "RealFFB.exe"); }

        public static bool GameRunning()
        {
            return Process.GetProcessesByName("FarmingSimulator2025Game").Length > 0 || Process.GetProcessesByName("FarmingSimulator2025").Length > 0;
        }

        // Pasta de mods; respeita <modsDirectoryOverride active="true" directory="..."/> do game.xml.
        public static string ModsDir(string profile)
        {
            try
            {
                string gx = Path.Combine(profile, "game.xml");
                if (File.Exists(gx))
                {
                    Match m = Regex.Match(File.ReadAllText(gx), "<modsDirectoryOverride\\b[^>]*>", RegexOptions.IgnoreCase);
                    if (m.Success && GameXml.GetAttr(m.Value, "active") == "true")
                    {
                        string d = GameXml.GetAttr(m.Value, "directory");
                        if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return d;
                    }
                }
            }
            catch { }
            return Path.Combine(profile, "mods");
        }

        // ------------------------------------------------------------------ detect
        private static int Detect(Result res)
        {
            int major, build;
            Diagnostics.WinVersion(out major, out build);
            bool osOk = Environment.Is64BitOperatingSystem && major >= 10;
            res.Add("os_ok", osOk ? "1" : "0");
            res.Add("os", (major >= 10 ? (build >= 22000 ? "Windows 11" : "Windows 10") : "Windows " + major) + " build " + build + (Environment.Is64BitOperatingSystem ? " 64 bits" : " 32 bits"));

            string src;
            string game = GameLocator.Find(out src);
            res.Add("game_dir", game ?? "");
            res.Add("game_source", src);
            string profile = GameLocator.ProfileDir();
            res.Add("profile_dir", profile);
            res.Add("mods_dir", ModsDir(profile));
            res.Add("game_running", GameRunning() ? "1" : "0");
            string gx = Path.Combine(profile, "game.xml");
            string gxs = "absent";
            try { if (File.Exists(gx)) gxs = GameXml.NeedsPatch(File.ReadAllText(gx)) ? "needs-patch" : "ok"; }
            catch (Exception ex) { gxs = "error: " + ex.Message; }
            res.Add("gamexml", gxs);

            List<string> other = new List<string>();
            List<FanatecBase> bases = new List<FanatecBase>();
            try { bases = Fanatec.Detect(other); }
            catch (Exception ex) { res.Add("fanatec_error", ex.Message); }
            res.Add("fanatec_count", bases.Count.ToString());
            for (int i = 0; i < bases.Count; i++) res.Add("fanatec_" + i, bases[i].Label);
            res.Add("fanatec_other", string.Join(", ", other.ToArray()));

            string ver = HidHide.InstalledVersion();
            string status = ver == null ? "missing" : (ver != "desconhecida" && HidHide.CompareVersions(ver, HidHide.MinVersion) < 0 ? "outdated" : "ok");
            res.Add("hidhide", status);
            res.Add("hidhide_version", ver ?? "");
            res.Add("admin", Diagnostics.IsAdmin() ? "1" : "0");
            return 0;
        }

        // ------------------------------------------------------------------ HidHide
        internal static int InstallHidHide(Result res)
        {
            string ver = HidHide.InstalledVersion();
            if (ver != null && (ver == "desconhecida" || HidHide.CompareVersions(ver, HidHide.MinVersion) >= 0))
            {
                res.Add("status", "present");
                res.Add("version", ver);
                Log("HidHide " + ver + " ja instalado: nada a fazer.");
                return Responding() ? 0 : Reboot(res);
            }

            string newVer, sha;
            string file = HidHide.Download(Log, out newVer, out sha);
            res.Add("downloaded", newVer);
            res.Add("sha256", sha);
            string signer;
            if (!HidHide.VerifySignature(file, out signer))
            {
                res.Add("status", "bad-signature");
                Log("Assinatura invalida ou nao emitida para a Nefarius: '" + signer + "'. Nada foi instalado.");
                try { File.Delete(file); } catch { }
                return 1;
            }
            Log("Assinatura valida: " + signer);

            int rc = HidHide.RunInstaller(file, true);
            Log("Instalador HidHide (silencioso) -> " + rc);
            if (rc != 0 && rc != 3010 && rc != 1641 && rc != 1223)
            {
                Log("A repetir em modo interativo.");
                rc = HidHide.RunInstaller(file, false);
                Log("Instalador HidHide (interativo) -> " + rc);
            }
            res.Add("installer_exit", rc.ToString());
            if (rc == 1223) { res.Add("status", "uac-denied"); return 2; }
            if (rc == 1602) { res.Add("status", "cancelled"); return 2; }
            if (rc != 0 && rc != 3010 && rc != 1641) { res.Add("status", "failed"); return 1; }

            string after = HidHide.InstalledVersion();
            res.Add("version", after ?? "");
            if (after == null) { res.Add("status", "failed"); return 1; }
            res.Add("status", "installed");
            if (rc == 3010 || rc == 1641) return Reboot(res);
            return Responding() ? 0 : Reboot(res);
        }

        private static bool Responding()
        {
            try { HidHide.Apps(); return true; }
            catch (Exception ex) { Log("HidHideCLI ainda nao responde: " + ex.Message); return false; }
        }

        private static int Reboot(Result res) { res.Add("reboot", "1"); return 3; }

        // ------------------------------------------------------------------ configure / resume
        internal static int Configure(Result res, bool resume)
        {
            string title = "Configuracao dos dispositivos";
            State st = State.Load();
            if (resume) { st.Set("PendingResume", null); st.Save(); }
            string exe = AppExe();
            if (!File.Exists(exe)) { Ui.Error(title, "RealFFB.exe nao encontrada:\n" + exe); res.Add("status", "no-app"); return 1; }
            if (HidHide.Cli() == null)
            {
                Ui.Error(title, "O HidHide nao esta instalado.\nVolte a executar o instalador do FS25 RealFFB para o instalar.");
                res.Add("status", "no-hidhide"); return 1;
            }
            if (!Responding())
            {
                Ui.Error(title, "O driver HidHide nao responde.\n\nSe acabou de o instalar, reinicie o Windows.\nSe o 'HidHide Configuration Client' estiver aberto, feche-o e tente de novo.");
                res.Add("status", "not-responding"); return 3;
            }

            List<string> other = new List<string>();
            List<FanatecBase> bases = Fanatec.Detect(other);
            if (bases.Count == 0)
            {
                Ui.Error(title, "Nenhuma base Fanatec em modo PC foi detetada." + (other.Count > 0 ? "\n(Ha dispositivos Fanatec que nao sao bases: " + string.Join(", ", other.ToArray()) + ")" : "") +
                    "\n\nLigue a base (LED vermelho = modo PC) e use o atalho 'FS25 RealFFB - Configurar dispositivos'.\nNada foi alterado.");
                res.Add("status", "no-fanatec"); return 2;
            }
            // Atualizacao/reinstalacao: se a base configurada antes continua corretamente configurada, nao perguntar nada.
            string prev = st.Get("HH_Base");
            if (prev != null && Get("force") == null)
                foreach (FanatecBase pb in bases)
                {
                    if (!pb.Key.Equals(prev, StringComparison.OrdinalIgnoreCase)) continue;
                    List<string> need = SafePaths(pb);
                    List<string> hidden = HidHide.HiddenDevices();
                    bool all = need.Count > 0 && Config.Contains(HidHide.Apps(), exe);
                    foreach (string n in need) if (!Config.Contains(hidden, n)) all = false;
                    if (all)
                    {
                        res.Add("status", "unchanged"); res.Add("device", pb.Label);
                        Log("HidHide ja configurado para " + pb.Label + ": nada alterado.");
                        if (!GameRunning()) PatchGameXml(new Result());
                        if (resume) Ui.Info(title, "Configuracao ja estava aplicada a " + pb.Label + ".");
                        return 0;
                    }
                }

            FanatecBase b;
            if (bases.Count == 1) b = bases[0];
            else
            {
                List<string> labels = new List<string>();
                for (int i = 0; i < bases.Count; i++) labels.Add((i + 1) + ". " + bases[i].Label);
                int c = Ui.Choose(title, "Foram detetadas varias bases Fanatec. Escolha a que vai usar no FS25.\nSo essa sera escondida do jogo.", labels);
                if (c < 0) { res.Add("status", "cancelled"); return 2; }
                b = bases[c];
            }

            List<string> safe = SafePaths(b);
            if (safe.Count == 0)
            {
                Ui.Error(title, "Nao foi possivel identificar com seguranca as entradas do HidHide da " + b.Label + ".\nNada foi alterado.\n\nGere o relatorio de diagnostico e abra um Issue no GitHub.");
                res.Add("status", "ambiguous"); return 1;
            }

            StringBuilder t = new StringBuilder();
            t.AppendLine("Vao ser feitas estas alteracoes no HidHide:");
            t.AppendLine();
            t.AppendLine("1) Autorizar a aplicacao:");
            t.AppendLine("   " + exe);
            t.AppendLine();
            t.AppendLine("2) Esconder dos jogos apenas a " + b.Label + ":");
            foreach (string p in safe) t.AppendLine("   " + p);
            t.AppendLine();
            t.AppendLine("Nenhum outro controlador e alterado. A ocultacao so fica ativa enquanto a RealFFB estiver aberta (com 'AutoHide').");
            t.AppendLine("A configuracao anterior e guardada e reposta ao desinstalar.");
            if (!Ui.Confirm(title, t.ToString())) { res.Add("status", "declined"); return 2; }

            Config.Apply(st, b, safe, exe, Log);
            res.Add("status", "configured");
            res.Add("device", b.Label);

            string gxr = "";
            if (!GameRunning()) PatchGameXml(new Result());
            else gxr = "\n\nO FS25 esta aberto: o game.xml sera ajustado na proxima vez (feche o jogo e volte a usar este atalho).";
            Ui.Info(title, "Configuracao aplicada a " + b.Label + "." + gxr + (resume ? "\n\nInstalacao concluida. Abra o FS25 RealFFB pelo atalho." : ""));
            return 0;
        }

        private static List<string> SafePaths(FanatecBase b)
        {
            List<string> safe = new List<string>();
            foreach (string p in Config.PathsFor(b, HidHide.GamingDevices()))
                if (p.ToUpperInvariant().Contains("VID_0EB7")) safe.Add(p); else Log("Ignorado (nao e Fanatec): " + p);
            return safe;
        }

        internal static int ScheduleResume(Result res)
        {
            string me = Path.Combine(Application.StartupPath, "RealFFB.Setup.exe");
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunOnceKey))
                k.SetValue(RunOnceName, "\"" + me + "\" resume --app \"" + AppExe() + "\"");
            State st = State.Load(); st.Set("PendingResume", "1"); st.Save();
            res.Add("status", "scheduled");
            return 0;
        }

        // ------------------------------------------------------------------ game.xml
        internal static int PatchGameXml(Result res)
        {
            string gx = Path.Combine(GameLocator.ProfileDir(), "game.xml");
            if (!File.Exists(gx)) { res.Add("status", "absent"); Log("game.xml ainda nao existe (o FS25 cria-o na primeira execucao)."); return 2; }
            if (GameRunning()) { res.Add("status", "game-running"); Log("FS25 aberto: o jogo reescreve o game.xml ao fechar. Feche-o primeiro."); return 1; }
            byte[] raw = File.ReadAllBytes(gx);
            bool bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            string xml = new UTF8Encoding(false).GetString(raw, bom ? 3 : 0, raw.Length - (bom ? 3 : 0));
            if (!GameXml.NeedsPatch(xml)) { res.Add("status", "ok"); Log("game.xml ja esta correto."); return 0; }

            State st = State.Load();
            string dir = Path.Combine(State.Dir, "backup");
            Directory.CreateDirectory(dir);
            string bak = Path.Combine(dir, "game.xml." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak");
            File.Copy(gx, bak, true);
            if (st.Get("GX_Orig") == null)
            {
                string j, v, f;
                GameXml.Read(xml, out j, out v, out f);
                st.Set("GX_Orig", j + ";" + v + ";" + f);
                st.Set("GX_Backup", bak);
            }
            string patched = GameXml.Apply(xml, "true", "false", "false");
            WriteSafe(gx, patched, bom);
            st.Set("GX_Patched", "1");
            st.Save();
            res.Add("status", "patched");
            res.Add("backup", bak);
            Log("game.xml ajustado (copia: " + bak + ")");
            return 0;
        }

        private static void WriteSafe(string file, string text, bool bom)
        {
            string tmp = file + ".realffb.tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(bom));
            File.Copy(tmp, file, true);
            File.Delete(tmp);
        }

        private static void RestoreGameXml(State st)
        {
            string orig = st.Get("GX_Orig");
            if (orig == null) return;
            string gx = Path.Combine(GameLocator.ProfileDir(), "game.xml");
            if (!File.Exists(gx)) return;
            if (GameRunning()) { Log("FS25 aberto: game.xml nao foi reposto."); return; }
            string[] o = orig.Split(';');
            if (o.Length != 3) return;
            byte[] raw = File.ReadAllBytes(gx);
            bool bom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            string xml = new UTF8Encoding(false).GetString(raw, bom ? 3 : 0, raw.Length - (bom ? 3 : 0));
            string j, v, f;
            GameXml.Read(xml, out j, out v, out f);
            if (GameXml.NeedsPatch(xml)) { Log("game.xml foi alterado depois da instalacao: mantido como esta."); return; }
            // forceFeedback: repor o valor original (ou remover o elemento, se nao existia)
            WriteSafe(gx, GameXml.Apply(xml, o[0], o[1], o[2]), bom);
            st.Set("GX_Orig", null); st.Set("GX_Patched", null);
            Log("game.xml reposto: joystick=" + o[0] + " vibration=" + o[1] + " forceFeedback=" + (o[2] == "" ? "(removido)" : o[2]));
        }

        // ------------------------------------------------------------------ desinstalacao
        internal static int Restore(Result res)
        {
            State st = State.Load();
            try { Config.Restore(st, Log); } catch (Exception ex) { Log("HidHide: " + ex.Message); }
            try { RestoreGameXml(st); } catch (Exception ex) { Log("game.xml: " + ex.Message); }
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunOnceKey, true)) if (k != null) k.DeleteValue(RunOnceName, false); } catch { }
            st.Save();
            if (Get("purge") != null)
            {
                try { Directory.Delete(State.Dir, true); } catch (Exception ex) { Log("Pasta de dados: " + ex.Message); }
            }
            else
            {
                try { File.Delete(State.FilePath); } catch { }
            }
            res.Add("status", "restored");
            return 0;
        }

        // Guarda a pasta do jogo e dos mods escolhidas no instalador (usadas pela app e pela verificacao).
        internal static int Remember(Result res)
        {
            State st = State.Load();
            string g = Get("game");
            if (GameLocator.IsValidGameDir(g)) st.Set("GameDir", g);
            string m = Get("mods");
            if (!string.IsNullOrEmpty(m) && m != "1") st.Set("ModsDir", m);
            st.Set("Version", typeof(Program).Assembly.GetName().Version.ToString(3));
            st.Save();
            res.Add("status", "ok");
            return 0;
        }

        // ------------------------------------------------------------------ verify / diagnose
        private static int VerifyCmd(Result res)
        {
            int errors = 0, i = 0;
            foreach (Diagnostics.Check c in Diagnostics.Verify(AppDir()))
            {
                res.Add("check_" + i++, "[" + c.Status + "] " + c.Name + (c.Detail.Length > 0 ? ": " + c.Detail : ""));
                if (c.Status == "ERRO") errors++;
            }
            res.Add("checks", i.ToString());
            res.Add("errors", errors.ToString());
            return errors == 0 ? 0 : 1;
        }

        private static int Diagnose(Result res)
        {
            string text = Diagnostics.Report(AppDir());
            string outFile = Get("report") ?? Path.Combine(State.Dir, "diagnostico.txt");
            File.WriteAllText(outFile, text, new UTF8Encoding(true));
            res.Add("report", outFile);
            if (Get("out") == null && Get("quiet") == null) Process.Start("notepad.exe", "\"" + outFile + "\"");
            return 0;
        }
    }
}
