// Instalacao local sem Inno Setup: "RealFFB.Setup.exe install" a partir da pasta dist\app gerada pelo
// build.bat. Faz as mesmas 7 etapas e regista a desinstalacao em Definicoes > Aplicacoes (HKCU).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace RealFFBSetup
{
    internal static class LocalInstaller
    {
        private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FS25RealFFB";
        private static TextBox logBox;
        private static Label[] steps;

        public static string InstallDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\FS25 RealFFB"); }
        }

        private static string StartMenuDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "FS25 RealFFB"); }
        }

        // ------------------------------------------------------------------ janela
        public static int Install()
        {
            Form f = new Form();
            f.Text = "FS25 RealFFB Installer";
            f.BackColor = Ui.Bg; f.ForeColor = Ui.Fg; f.Font = new Font("Segoe UI", 9.5f);
            f.FormBorderStyle = FormBorderStyle.FixedDialog; f.MaximizeBox = false;
            f.StartPosition = FormStartPosition.CenterScreen; f.ClientSize = new Size(640, 520);
            try { f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Label head = new Label(); head.Text = "FS25 RealFFB Installer"; head.ForeColor = Ui.Accent;
            head.Font = new Font("Segoe UI Semibold", 15f); head.SetBounds(20, 14, 600, 34); f.Controls.Add(head);
            Label sub = new Label(); sub.Text = "by OptimusZuka  |  instala na sua conta, sem administrador (so o HidHide pede UAC)";
            sub.ForeColor = Color.Silver; sub.SetBounds(22, 48, 600, 20); f.Controls.Add(sub);
            string[] names = { "1. Verificacao do sistema", "2. Detecao do Farming Simulator 25", "3. Verificacao do volante",
                "4. Instalacao do HidHide", "5. Configuracao dos dispositivos", "6. Instalacao do RealFFB", "7. Verificacao final" };
            stepNames = names;
            steps = new Label[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Label l = new Label(); l.Text = "      " + names[i]; l.SetBounds(22, 80 + i * 26, 600, 24);
                l.ForeColor = Color.Gray; steps[i] = l; f.Controls.Add(l);
            }
            logBox = new TextBox(); logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical;
            logBox.BackColor = Color.FromArgb(28, 28, 28); logBox.ForeColor = Color.Gainsboro; logBox.BorderStyle = BorderStyle.None;
            logBox.Font = new Font("Consolas", 9f); logBox.SetBounds(20, 270, 600, 190); f.Controls.Add(logBox);
            Button go = new Button(); go.Text = "Instalar"; go.SetBounds(380, 472, 116, 34);
            go.FlatStyle = FlatStyle.Flat; go.BackColor = Ui.Accent; go.ForeColor = Color.Black; go.FlatAppearance.BorderColor = Ui.Accent;
            Button close = new Button(); close.Text = "Fechar"; close.SetBounds(504, 472, 116, 34);
            close.FlatStyle = FlatStyle.Flat; close.BackColor = Color.FromArgb(45, 45, 45); close.ForeColor = Ui.Fg; close.FlatAppearance.BorderColor = Ui.Accent;
            close.Click += delegate { f.Close(); };
            int result = 2;
            go.Click += delegate
            {
                go.Enabled = false; close.Enabled = false;
                try { result = Run(f); }
                catch (Exception ex) { Out("ERRO: " + ex.Message); result = 1; }
                close.Enabled = true; close.Text = "Concluir";
            };
            f.Controls.Add(go); f.Controls.Add(close);
            Out("Pasta de instalacao: " + InstallDir);
            Out("Feche o Farming Simulator 25 e o RealFFB antes de continuar.");
            Application.Run(f);
            return result;
        }

        private static void Out(string s) { logBox.AppendText(s + "\r\n"); Program.Log(s); Application.DoEvents(); }

        private static string[] stepNames;

        private static void Mark(int i, string status, Color c)
        {
            steps[i].Text = status.PadRight(6) + stepNames[i];
            steps[i].ForeColor = c;
            Application.DoEvents();
        }

        private static void Ok(int i) { Mark(i, "[OK]", Color.FromArgb(80, 210, 100)); }
        private static void Warn(int i) { Mark(i, "[!]", Ui.Accent); }
        private static void Fail(int i) { Mark(i, "[X]", Color.FromArgb(235, 70, 60)); }
        private static void Busy(int i) { Mark(i, "[..]", Ui.Fg); }

        // ------------------------------------------------------------------ etapas
        private static int Run(Form owner)
        {
            // 1. sistema
            Busy(0);
            int major, build;
            Diagnostics.WinVersion(out major, out build);
            if (!Environment.Is64BitOperatingSystem || major < 10) { Fail(0); Out("Precisa do Windows 10 ou 11 de 64 bits."); return 1; }
            Out("Windows " + (build >= 22000 ? "11" : "10") + " build " + build + " 64 bits");
            if (Program.GameRunning()) { Fail(0); Out("O FS25 esta aberto. Feche-o e carregue de novo em Instalar."); return 1; }
            Ok(0);

            // 2. FS25
            Busy(1);
            State st = State.Load();
            string src;
            string game = st.Get("GameDir");
            if (!GameLocator.IsValidGameDir(game)) game = GameLocator.Find(out src); else src = "instalacao anterior";
            if (game == null)
            {
                Out("FS25 nao encontrado automaticamente (" + src + "). Indique a pasta.");
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = "Pasta do Farming Simulator 25 (a que tem FarmingSimulator2025.exe)";
                    if (d.ShowDialog(owner) == DialogResult.OK && GameLocator.IsValidGameDir(d.SelectedPath)) game = d.SelectedPath;
                }
            }
            if (game == null) { Fail(1); Out("Sem a pasta do FS25 nao e possivel continuar."); return 1; }
            string profile = GameLocator.ProfileDir();
            string mods = Program.ModsDir(profile);
            Out("FS25: " + game + " (" + src + ")");
            Out("Mods: " + mods);
            Ok(1);

            // 3. Fanatec
            Busy(2);
            List<FanatecBase> bases = new List<FanatecBase>();
            try { bases = Fanatec.Detect(null); } catch (Exception ex) { Out("Volante: " + ex.Message); }
            foreach (FanatecBase b in bases) Out("Volante: " + b.Label);
            if (bases.Count == 0) { Warn(2); Out("Nenhum volante suportado ligado. Pode configurar depois (Iniciar > FS25 RealFFB - Configurar dispositivos)."); }
            else Ok(2);

            // 4. HidHide
            Busy(3);
            bool reboot = false;
            string ver = HidHide.InstalledVersion();
            bool hidOk = ver != null && (ver == "desconhecida" || HidHide.CompareVersions(ver, HidHide.MinVersion) >= 0);
            if (hidOk) { Out("HidHide " + ver + " ja instalado."); Ok(3); }
            else
            {
                if (!Ui.Confirm("Instalacao do HidHide",
                    "O RealFFB precisa do HidHide (Nefarius Software Solutions, gratuito, codigo aberto), um driver que esconde o volante do FS25.\n\n" +
                    "Vai ser descarregado o instalador oficial de github.com/nefarius/HidHide. A assinatura digital e verificada antes de executar e o ficheiro nao e alterado.\n\n" +
                    "O Windows vai pedir autorizacao de administrador (UAC). Pode ser preciso reiniciar."))
                { Warn(3); Out("HidHide nao instalado. O RealFFB nao funciona sem ele."); }
                else
                {
                    Result r = new Result();
                    int c = Program.InstallHidHide(r);
                    if (c == 0) { Ok(3); Out("HidHide instalado."); }
                    else if (c == 3) { Warn(3); reboot = true; Out("HidHide instalado: e preciso reiniciar o Windows."); }
                    else { Fail(3); Out("HidHide falhou (codigo " + c + "). Veja o registo em %APPDATA%\\FS25 RealFFB\\setup_log.txt."); }
                }
            }

            // 6. copiar ficheiros (antes da 5, que precisa do caminho final da app)
            Busy(5);
            string here = Application.StartupPath;
            string dest = InstallDir;
            string[] files = { "RealFFB.exe", "RealFFB.Setup.exe" };
            foreach (string fn in files) if (!File.Exists(Path.Combine(here, fn))) { Fail(5); Out("Falta " + fn + " em " + here + ". Corra o build.bat primeiro."); return 1; }
            string modZip = Path.Combine(here, "FS25_RealFFBTelemetry.zip");
            if (!File.Exists(modZip)) { Fail(5); Out("Falta o mod (FS25_RealFFBTelemetry.zip) em " + here + "."); return 1; }
            if (AppRunning())
            {
                Out("O RealFFB esta aberto: feche-o.");
                MessageBox.Show(owner, "Feche o FS25 RealFFB e carregue em OK.", "FS25 RealFFB Installer");
                if (AppRunning()) { Fail(5); Out("O RealFFB continua aberto."); return 1; }
            }
            Directory.CreateDirectory(dest);
            bool sameDir = string.Equals(Path.GetFullPath(here).TrimEnd('\\'), Path.GetFullPath(dest).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
            if (!sameDir) foreach (string fn in files) File.Copy(Path.Combine(here, fn), Path.Combine(dest, fn), true);
            Directory.CreateDirectory(mods);
            File.Copy(modZip, Path.Combine(mods, "FS25_RealFFBTelemetry.zip"), true);
            string exe = Path.Combine(dest, "RealFFB.exe");
            string helper = Path.Combine(dest, "RealFFB.Setup.exe");
            Directory.CreateDirectory(StartMenuDir);
            Shortcut(Path.Combine(StartMenuDir, "FS25 RealFFB.lnk"), exe, "", exe);
            Shortcut(Path.Combine(StartMenuDir, "FS25 RealFFB - Configurar dispositivos.lnk"), helper, "configure", helper);
            Shortcut(Path.Combine(StartMenuDir, "FS25 RealFFB - Relatorio de diagnostico.lnk"), helper, "diagnose", helper);
            Shortcut(Path.Combine(StartMenuDir, "Desinstalar FS25 RealFFB.lnk"), helper, "uninstall", helper);
            string fsIcon = Path.Combine(game, "FarmingSimulator2025.exe");
            if (MessageBox.Show(owner, "Criar atalho no ambiente de trabalho?\n(Abre o RealFFB e o FS25)", "FS25 RealFFB Installer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FS25 RealFFB.lnk"), exe, "--jogar", File.Exists(fsIcon) ? fsIcon : exe);
            RegisterUninstall(dest, helper, exe);
            Program.Opt["game"] = game; Program.Opt["mods"] = mods;
            Program.Remember(new Result());
            Out("Instalado em " + dest);
            Ok(5);

            // 5. dispositivos + game.xml
            Busy(4);
            Program.Opt["app"] = exe;
            if (reboot)
            {
                Program.ScheduleResume(new Result());
                Program.PatchGameXml(new Result());
                Warn(4); Out("A configuracao dos dispositivos continua sozinha depois de reiniciar o Windows.");
            }
            else if (HidHide.Cli() != null && bases.Count > 0)
            {
                Result r = new Result();
                int c = Program.Configure(r, false);
                if (c == 0) Ok(4); else Warn(4);
                Out("Dispositivos: " + (c == 0 ? "configurados" : "nao configurados (use 'Configurar dispositivos' no menu Iniciar)"));
            }
            else
            {
                Program.PatchGameXml(new Result());
                Warn(4); Out("Dispositivos por configurar: ligue o volante e use Iniciar > FS25 RealFFB - Configurar dispositivos.");
            }

            // 7. verificacao
            Busy(6);
            int errors = 0;
            foreach (Diagnostics.Check c in Diagnostics.Verify(dest))
            {
                Out("[" + c.Status + "] " + c.Name + (c.Detail.Length > 0 ? ": " + c.Detail : ""));
                if (c.Status == "ERRO") errors++;
            }
            if (reboot) { Warn(6); Out("\r\nReinicie o Windows para concluir."); }
            else if (errors == 0) { Ok(6); Out("\r\nInstalado e pronto a configurar. Abra o FS25 RealFFB pelo menu Iniciar ou pelo atalho."); }
            else { Warn(6); Out("\r\nInstalado com " + errors + " problema(s). Use 'Relatorio de diagnostico' para pedir ajuda."); }
            return 0;
        }

        private static bool AppRunning() { return Process.GetProcessesByName("RealFFB").Length > 0; }

        private static void Shortcut(string lnk, string target, string args, string icon)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { target });
            st.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { args });
            st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(target) });
            st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { icon + ",0" });
            st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
        }

        private static void RegisterUninstall(string dir, string helper, string exe)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                k.SetValue("DisplayName", "FS25 RealFFB");
                k.SetValue("DisplayVersion", typeof(Program).Assembly.GetName().Version.ToString(3));
                k.SetValue("Publisher", "OptimusZuka");
                k.SetValue("DisplayIcon", exe);
                k.SetValue("InstallLocation", dir);
                k.SetValue("UninstallString", "\"" + helper + "\" uninstall");
                k.SetValue("URLInfoAbout", "https://github.com/OptimusZuka/FS25-RealFFB");
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        // ------------------------------------------------------------------ desinstalacao
        public static int Uninstall()
        {
            if (MessageBox.Show("Desinstalar o FS25 RealFFB?\n\nO HidHide e o game.xml voltam ao estado anterior. O HidHide continua instalado.",
                "FS25 RealFFB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 2;
            if (AppRunning()) { MessageBox.Show("Feche o FS25 RealFFB primeiro.", "FS25 RealFFB"); return 1; }
            bool purge = MessageBox.Show("Apagar tambem as suas definicoes e perfis (afinacoes, pedais, comandos)?\n\nNao = manter (recomendado se for reinstalar).",
                "FS25 RealFFB", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            State st = State.Load();
            string mods = st.Get("ModsDir") ?? Program.ModsDir(GameLocator.ProfileDir());
            if (purge) Program.Opt["purge"] = "1";
            Program.Restore(new Result());
            try { File.Delete(Path.Combine(mods, "FS25_RealFFBTelemetry.zip")); } catch { }
            try { Directory.Delete(StartMenuDir, true); } catch { }
            try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FS25 RealFFB.lnk")); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }
            // a propria pasta e apagada por um processo a parte, depois de este terminar
            string dir = InstallDir;
            if (Directory.Exists(dir))
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + dir + "\"");
                psi.CreateNoWindow = true; psi.UseShellExecute = false;
                Process.Start(psi);
            }
            MessageBox.Show("FS25 RealFFB desinstalado.", "FS25 RealFFB");
            return 0;
        }
    }
}
