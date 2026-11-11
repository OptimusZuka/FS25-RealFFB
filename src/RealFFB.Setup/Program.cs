// RealFFB.Setup.exe - ajudante do instalador (chamado pelo Inno Setup, pela app e na desinstalacao).
// Cada comando faz uma coisa, regista o que alterou e escreve o resultado em --out (ini [r]).
//
//   detect            estado do sistema, FS25, Fanatec, HidHide
//   hidhide-install   descarrega o HidHide oficial, valida a assinatura e instala (UAC)
//   configure         autoriza a RealFFB.exe, identifica a base (qualquer marca), esconde-a e verifica (+ game.xml)
//   schedule-resume   retoma a configuracao depois de reiniciar o Windows
//   resume            (corre sozinho depois de reiniciar)
//   patch-gamexml     ajusta o game.xml (com copia de seguranca)
//   restore           desinstalacao: repoe HidHide e game.xml   [--purge]
//   uninstall-info    desinstalacao: o HidHide esta instalado? foi instalado por nos?
//   hidhide-uninstall desinstalacao (so com consentimento): desinstalador oficial do HidHide (UAC)
//   purge             apaga a pasta de dados (ultimo passo da desinstalacao)
//   verify            verificacao final
//   diagnose          relatorio anonimizado
//
// Codigos de saida: 0 ok, 1 erro, 2 cancelado/nada feito, 3 reiniciar o Windows, 4 sem ligacao ao GitHub (repetir).
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
            for (int a = 0; a + 1 < args.Length; a++) if (args[a] == "--lang") State.Load().SetLang(args[a + 1]);
            L.Set(State.Load().Get("Lang") ?? "en");
            if (args.Length == 0)
            {
                Ui.Info("Help", L.T("FS25 RealFFB utility.\nUse the 'Configure devices' shortcut or the diagnostics button in the app."));
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
                    case "uninstall-info": code = UninstallInfo(res); break;
                    case "hidhide-uninstall": code = UninstallHidHide(res); break;
                    case "purge": code = Purge(res); break;
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
            if (cmd != "purge") AppendSetupLog(cmd, code);
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

            // 0.14: qualquer base que o Windows declare com force feedback (nao so Fanatec/Logitech)
            List<FfbInfo> bases = new List<FfbInfo>();
            try { bases = Wheels.FfbBases(Wheels.ScanFfb(null, Log)); }
            catch (Exception ex) { res.Add("fanatec_error", ex.Message); }
            res.Add("fanatec_count", bases.Count.ToString());
            for (int i = 0; i < bases.Count; i++) res.Add("fanatec_" + i, Wheels.NameOf(bases[i]));
            res.Add("fanatec_other", "");

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
                State pre = State.Load();
                if (pre.Get("HH_InstalledByUs") == null) { pre.Set("HH_Preexisting", "1"); pre.Save(); }
                Log("HidHide " + ver + " already installed: nothing to do.");
                return Responding() ? 0 : Reboot(res);
            }

            if (ver != null) Log("HidHide " + ver + " is older than " + HidHide.MinVersion + ": updating to the latest official version.");
            HidHide.Asset asset;
            string file;
            try { file = HidHide.Download(Log, out asset); }
            catch (HidHide.DownloadException dx)
            {
                // Falha de ligacao/API nao e o mesmo que "nao ha versao": o instalador oferece tentar de novo.
                res.Add("status", "download-failed");
                res.Add("kind", dx.Kind);
                res.Add("error", dx.Message);
                Log("HidHide download failed [" + dx.Kind + "]: " + dx.Message);
                return 4;
            }
            res.Add("downloaded", asset.Version);
            res.Add("sha256", asset.Sha256);
            string signer;
            if (!HidHide.VerifySignature(file, out signer))
            {
                res.Add("status", "bad-signature");
                Log("Invalid signature or not issued to Nefarius: '" + signer + "'. Nothing was installed.");
                try { File.Delete(file); } catch { }
                return 1;
            }
            Log("Valid signature: " + signer);

            int rc = HidHide.RunInstaller(file, true);
            Log("HidHide installer (silent) -> " + rc);
            if (rc == HidHide.TimedOut)
            {
                res.Add("status", "timeout");
                res.Add("error", "the official HidHide installer did not finish within 5 minutes");
                Log("The official HidHide installer did not answer within 5 minutes. It was left running (stopping a driver installation half-way is unsafe). Restart Windows and run the FS25 RealFFB installer again.");
                return 1;
            }
            if (rc != 0 && rc != 3010 && rc != 1641 && rc != 1223)
            {
                Log("Retrying in interactive mode.");
                rc = HidHide.RunInstaller(file, false);
                Log("HidHide installer (interactive) -> " + rc);
            }
            res.Add("installer_exit", rc.ToString());
            if (rc == 1223) { res.Add("status", "uac-denied"); return 2; }
            if (rc == 1602) { res.Add("status", "cancelled"); return 2; }
            if (rc != 0 && rc != 3010 && rc != 1641) { res.Add("status", "failed"); return 1; }

            string after = HidHide.InstalledVersion();
            res.Add("version", after ?? "");
            if (after == null) { res.Add("status", "failed"); return 1; }
            res.Add("status", "installed");
            State mine = State.Load(); mine.Set("HH_InstalledByUs", "1"); mine.Set("HH_Preexisting", null); mine.Save();
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
            string title = L.T("Device setup");
            State st = State.Load();
            if (resume) { st.Set("PendingResume", null); st.Save(); }
            int code = ConfigureCore(res, resume, st, title);
            // resultado guardado para o diagnostico (distingue erro de configuracao de problema de hardware)
            st = State.Load();
            st.Set("HH_LastResult", res.Get("status"));
            st.Set("HH_LastTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            st.Set("HH_LastError", res.Get("error").Length > 0 ? Diagnostics.Anon(res.Get("error")) : null);
            st.Save();
            return code;
        }

        private static int ConfigureCore(Result res, bool resume, State st, string title)
        {
            bool quiet = Get("quiet") != null;
            string exe = AppExe();
            if (!File.Exists(exe)) { if (!quiet) Ui.Error(title, L.F("RealFFB.exe not found:\n{0}", exe)); res.Add("status", "no-app"); return 1; }
            if (HidHide.Cli() == null)
            {
                if (!quiet) Ui.Error(title, L.T("HidHide is not installed.\nRun the FS25 RealFFB installer again to install it."));
                res.Add("status", "no-hidhide"); return 1;
            }
            if (!Responding())
            {
                if (!quiet) Ui.Error(title, L.T("The HidHide driver is not responding.\n\nIf you just installed it, restart Windows.\nIf the 'HidHide Configuration Client' is open, close it and try again."));
                res.Add("status", "not-responding"); return 3;
            }

            // 1) game.xml e autorizacao da app: sempre, mesmo sem volante ligado (antes, sem volante
            //    "suportado", nada era feito e a RealFFB.exe ficava fora da lista do HidHide).
            if (!GameRunning()) PatchGameXml(new Result());
            st = State.Load();   // PatchGameXml gravou GX_Orig/GX_Backup: nao os perder ao gravar este estado
            try
            {
                Config.BackupOnce(st, Log);
                Config.EnsureApp(st, exe, Log);
            }
            catch (Exception ex)
            {
                res.Add("error", ex.Message);
                if (!quiet) Ui.Error(title, L.F("HidHide could not be changed:\n{0}\n\nClose the 'HidHide Configuration Client' if it is open and use the 'FS25 RealFFB - Configure devices' shortcut.", ex.Message));
                res.Add("status", "hidhide-error"); return 1;
            }
            res.Add("app_allowed", "1");

            // 2) identificar a base: HidHide (todos os dispositivos de jogo, mesmo escondidos) + DirectInput
            //    pela RealFFB.exe, que ja esta autorizada e por isso ve o volante mesmo escondido.
            List<HidHide.GamingDevice> gaming = HidHide.GamingDevices();
            List<FfbInfo> ffb = Wheels.ScanFfb(exe, Log);
            List<WheelDevice> others = new List<WheelDevice>();
            List<WheelDevice> wheels = Wheels.Find(gaming, ffb, st.Get("HH_Base"), others);
            foreach (WheelDevice w in wheels) Log("Wheel found [" + w.Reason + "]: " + w.Label);
            foreach (WheelDevice o in others) Log("Not a wheel base (not hidden): " + o.Label);

            WheelDevice b = null;
            if (wheels.Count == 1) b = wheels[0];
            else if (wheels.Count > 1)
            {
                if (quiet) { res.Add("status", "choose"); return 2; }
                List<string> labels = new List<string>();
                for (int i = 0; i < wheels.Count; i++) labels.Add((i + 1) + ". " + wheels[i].Label);
                int c = Ui.Choose(title, L.T("Several wheels were found. Choose the one you will use in FS25.\nOnly that one will be hidden from the game."), labels);
                if (c < 0) { res.Add("status", "cancelled"); return 2; }
                b = wheels[c];
            }
            else
            {
                // nenhuma base identificada com seguranca: escolha manual (com aviso) ou nada
                if (quiet || others.Count == 0)
                {
                    if (!quiet) Ui.Error(title, L.T("No force feedback wheel was found.\n\nRealFFB.exe is already allowed in HidHide. Connect and switch on the wheel base (Fanatec: red LED = PC mode), then use the 'FS25 RealFFB - Configure devices' shortcut.\nNo device was hidden."));
                    res.Add("status", "no-wheel"); return 2;
                }
                List<string> labels = new List<string>();
                for (int i = 0; i < others.Count; i++) labels.Add((i + 1) + ". " + others[i].Label);
                int c = Ui.Choose(title, L.T("No force feedback wheel was identified automatically.\nIf your wheel base is in this list, choose it. Choose only the wheel base, never pedals, shifters or other controllers.\nIf you are not sure, cancel: nothing will be hidden."), labels);
                if (c < 0) { res.Add("status", "no-wheel"); return 2; }
                b = others[c]; b.Reason = "manual";
            }

            List<string> safe = Wheels.PathsFor(b, gaming, Log);
            if (safe.Count == 0)
            {
                if (!quiet) Ui.Error(title, L.F("The HidHide entries of the {0} could not be identified safely.\nNothing was changed.\n\nCreate the diagnostic report and open an issue on GitHub.", b.Label));
                res.Add("status", "ambiguous"); res.Add("device", b.Label); return 1;
            }

            // reinstalacao/reparacao: ja esta tudo certo -> nada a perguntar
            List<string> pending = Config.Missing(null, safe);
            if (pending.Count == 0)
            {
                st.Set("HH_Base", b.Key); st.Set("HH_BaseLabel", b.Label); st.Save();
                res.Add("status", "unchanged"); res.Add("device", b.Label);
                Log("HidHide already configured for " + b.Label + ".");
                if (resume && !quiet) Ui.Info(title, L.F("Settings were already applied to {0}.", b.Label));
                return 0;
            }

            if (!quiet)
            {
                StringBuilder t = new StringBuilder();
                t.AppendLine(L.F("Wheel: {0}", b.Label));
                t.AppendLine();
                t.AppendLine(L.F("Hide only the {0} from games:", b.Label));
                foreach (string p in safe) t.AppendLine("   " + p);
                t.AppendLine();
                t.AppendLine(L.T("No other controller is changed. Hiding is only active while RealFFB is open (with 'AutoHide')."));
                t.AppendLine(L.T("The previous configuration is saved and restored on uninstall."));
                if (!Ui.Confirm(title, t.ToString())) { res.Add("status", "declined"); return 2; }
            }

            try { Config.HideDevices(st, b, safe, Log); }
            catch (Exception ex)
            {
                res.Add("error", ex.Message);
                if (!quiet) Ui.Error(title, L.F("HidHide could not be changed:\n{0}\n\nClose the 'HidHide Configuration Client' if it is open and use the 'FS25 RealFFB - Configure devices' shortcut.", ex.Message));
                res.Add("status", "hidhide-error"); return 1;
            }

            // 3) verificacao: ler de novo o HidHide
            List<string> miss = Config.Missing(exe, safe);
            if (miss.Count > 0)
            {
                res.Add("error", string.Join("; ", miss.ToArray()));
                if (!quiet) Ui.Error(title, L.F("HidHide did not keep these settings:\n{0}\n\nUse the 'FS25 RealFFB - Configure devices' shortcut to try again.", string.Join("\n", miss.ToArray())));
                res.Add("status", "verify-failed"); return 1;
            }
            res.Add("status", "configured");
            res.Add("device", b.Label);

            string gxr = GameRunning() ? "\n\n" + L.T("FS25 is open: game.xml will be adjusted next time (close the game and use this shortcut again).") : "";
            if (!quiet)
                Ui.Info(title, L.F("Settings applied to {0}.", b.Label) + "\n\n" +
                    L.T("Windows applies the change when the wheel reconnects: unplug the wheel's USB cable and plug it back in (or restart Windows) before playing.") +
                    gxr + (resume ? "\n\n" + L.T("Installation complete. Open FS25 RealFFB from the shortcut.") : ""));
            return 0;
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
            // entrada "--unhide" do arranque do Windows criada pela app (0.10): sem a app, deixa de fazer sentido
            try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) if (k != null) k.DeleteValue("FS25 RealFFB - show wheel", false); } catch { }
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

        // Desinstalacao: estado do HidHide para a pergunta "desinstalar tambem o HidHide?".
        internal static int UninstallInfo(Result res)
        {
            string ver = HidHide.InstalledVersion();
            State st = State.Load();
            res.Add("hidhide_installed", ver != null ? "1" : "0");
            res.Add("hidhide_version", ver ?? "");
            res.Add("installed_by_us", st.Get("HH_InstalledByUs") ?? "0");
            res.Add("preexisting", st.Get("HH_Preexisting") ?? "0");
            res.Add("status", "ok");
            return 0;
        }

        // Desinstala o HidHide pelo desinstalador OFICIAL (so chamado depois de o utilizador confirmar duas vezes).
        // Antes: desliga o esconderijo, para nenhum dispositivo ficar escondido se a remocao do driver falhar.
        internal static int UninstallHidHide(Result res)
        {
            if (HidHide.InstalledVersion() == null) { res.Add("status", "absent"); Log("HidHide is not installed."); return 0; }
            if (HidHide.Cli() != null)
            {
                try { HidHide.Run("--cloak-off"); Log("HidHide --cloak-off"); }
                catch (Exception ex) { Log("HidHide --cloak-off failed: " + ex.Message); }
            }
            HidHide.UninstallEntry e = HidHide.FindUninstall();
            string[] cmd = HidHide.UninstallCommand(e);
            if (cmd == null)
            {
                res.Add("status", "no-uninstaller");
                res.Add("error", "HidHide uninstall entry not found");
                Log("HidHide uninstall entry not found: remove it in Windows Settings > Apps.");
                return 1;
            }
            Log("Official HidHide uninstaller: " + cmd[0] + " " + cmd[1]);
            int rc = HidHide.RunUninstall(cmd);
            res.Add("uninstaller_exit", rc.ToString());
            Log("HidHide uninstaller -> " + rc);
            if (rc == HidHide.TimedOut) { res.Add("status", "timeout"); res.Add("error", "the official HidHide uninstaller did not finish within 10 minutes"); return 1; }
            if (rc == 1223) { res.Add("status", "uac-denied"); return 2; }
            if (rc == 1602) { res.Add("status", "cancelled"); return 2; }
            if (rc != 0 && rc != 3010 && rc != 1641) { res.Add("status", "failed"); res.Add("error", "exit code " + rc); return 1; }
            bool gone = HidHide.InstalledVersion() == null;
            if (rc == 3010 || rc == 1641 || !gone || HidHide.Cli() != null)
            {
                res.Add("status", "reboot");
                Log("HidHide removed; Windows must restart to finish removing the driver.");
                return 3;
            }
            res.Add("status", "removed");
            return 0;
        }

        // Ultimo passo da desinstalacao, se o utilizador pediu: apagar a pasta de dados (sem voltar a escrever nela).
        internal static int Purge(Result res)
        {
            try { Directory.Delete(State.Dir, true); res.Add("status", "purged"); return 0; }
            catch (Exception ex) { res.Add("status", "failed"); res.Add("error", ex.Message); return 1; }
        }

        // Guarda a pasta do jogo e dos mods escolhidas no instalador (usadas pela app e pela verificacao).
        internal static int Remember(Result res)
        {
            State st = State.Load();
            string g = Get("game");
            if (GameLocator.IsValidGameDir(g)) st.Set("GameDir", g);
            string lg = Get("lang");
            if (!string.IsNullOrEmpty(lg) && lg != "1") st.SetLang(lg);
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
                if (c.Status == "ERROR") errors++;
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
