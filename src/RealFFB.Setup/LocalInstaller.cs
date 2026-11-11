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
            f.StartPosition = FormStartPosition.CenterScreen; f.ClientSize = new Size(640, 540);
            try { f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            PictureBox logo = new PictureBox(); logo.SizeMode = PictureBoxSizeMode.Zoom; logo.SetBounds(16, 8, 86, 62);
            logo.Image = Ui.Logo(); f.Controls.Add(logo);
            Label head = new Label(); head.Text = "FS25 RealFFB Installer"; head.ForeColor = Ui.Accent;
            head.Font = new Font("Segoe UI Semibold", 15f); head.SetBounds(110, 12, 510, 34); f.Controls.Add(head);
            Label sub = new Label(); sub.Text = "by OptimusZuka  |  " + L.T("Installs in your user account, no administrator needed (only HidHide asks for UAC)");
            sub.ForeColor = Color.Silver; sub.SetBounds(112, 46, 510, 20); f.Controls.Add(sub);
            string[] names = { L.T("1. System check"), L.T("2. Farming Simulator 25 detection"), L.T("3. Wheel check"),
                L.T("4. HidHide installation"), L.T("5. Device setup"), L.T("6. RealFFB installation"), L.T("7. Final check") };
            stepNames = names;
            steps = new Label[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Label l = new Label(); l.Text = "      " + names[i]; l.SetBounds(22, 90 + i * 26, 600, 24);
                l.ForeColor = Color.Gray; steps[i] = l; f.Controls.Add(l);
            }
            logBox = new TextBox(); logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical;
            logBox.BackColor = Color.FromArgb(28, 28, 28); logBox.ForeColor = Color.Gainsboro; logBox.BorderStyle = BorderStyle.None;
            logBox.Font = new Font("Consolas", 9f); logBox.SetBounds(20, 280, 600, 200); f.Controls.Add(logBox);
            Button go = new Button(); go.Text = L.T("Install"); go.SetBounds(380, 492, 116, 34);
            go.FlatStyle = FlatStyle.Flat; go.BackColor = Ui.Accent; go.ForeColor = Color.Black; go.FlatAppearance.BorderColor = Ui.Accent;
            Button close = new Button(); close.Text = L.T("Close"); close.SetBounds(504, 492, 116, 34);
            close.FlatStyle = FlatStyle.Flat; close.BackColor = Color.FromArgb(45, 45, 45); close.ForeColor = Ui.Fg; close.FlatAppearance.BorderColor = Ui.Accent;
            close.Click += delegate { f.Close(); };
            int result = 2;
            go.Click += delegate
            {
                go.Enabled = false; close.Enabled = false;
                try { result = Run(f); }
                catch (Exception ex) { Out("ERROR: " + ex.Message); result = 1; }
                close.Enabled = true; close.Text = L.T("Finish");
            };
            ComboBox lang = new ComboBox(); lang.DropDownStyle = ComboBoxStyle.DropDownList;
            lang.FlatStyle = FlatStyle.Flat; lang.BackColor = Color.FromArgb(28, 28, 28); lang.ForeColor = Ui.Fg;
            foreach (string n in L.Names) lang.Items.Add(n);
            lang.SelectedIndex = Math.Max(0, Array.IndexOf(L.Codes, L.Current));
            lang.SetBounds(20, 497, 200, 26);
            lang.SelectedIndexChanged += delegate
            {
                string code = L.Codes[Math.Max(0, lang.SelectedIndex)];
                if (code == L.Current || !go.Enabled) return;
                State.Load().SetLang(code);
                try { Process.Start(Application.ExecutablePath, "install"); } catch { }
                f.Close();
            };
            go.Click += delegate { lang.Enabled = false; };
            f.Controls.Add(lang);
            f.Controls.Add(go); f.Controls.Add(close);
            Out(L.F("Install folder: {0}", InstallDir));
            Out(L.T("Close Farming Simulator 25 and RealFFB before continuing."));
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
            if (!Environment.Is64BitOperatingSystem || major < 10) { Fail(0); Out(L.T("Requires 64-bit Windows 10 or 11.")); return 1; }
            Out("Windows " + (build >= 22000 ? "11" : "10") + " build " + build + " 64 bits");
            if (Program.GameRunning()) { Fail(0); Out(L.T("FS25 is open. Close it and click Install again.")); return 1; }
            Ok(0);

            // 2. FS25
            Busy(1);
            State st = State.Load();
            string src;
            string game = st.Get("GameDir");
            if (!GameLocator.IsValidGameDir(game)) game = GameLocator.Find(out src); else src = "previous installation";
            if (game == null)
            {
                Out(L.F("FS25 not found automatically ({0}). Choose the folder.", src));
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = L.T("Farming Simulator 25 folder (the one with FarmingSimulator2025.exe)");
                    if (d.ShowDialog(owner) == DialogResult.OK && GameLocator.IsValidGameDir(d.SelectedPath)) game = d.SelectedPath;
                }
            }
            if (game == null) { Fail(1); Out(L.T("Cannot continue without the FS25 folder.")); return 1; }
            string profile = GameLocator.ProfileDir();
            string mods = Program.ModsDir(profile);
            Out("FS25: " + game + " (" + src + ")");
            Out("Mods: " + mods);
            Ok(1);

            // 3. Fanatec
            Busy(2);
            List<FanatecBase> bases = new List<FanatecBase>();
            try { bases = Fanatec.Detect(null); } catch (Exception ex) { Out("Wheel: " + ex.Message); }
            foreach (FanatecBase b in bases) Out("Wheel: " + b.Label);
            if (bases.Count == 0) { Warn(2); Out(L.T("No supported wheel connected. You can set it up later (Start > FS25 RealFFB - Configure devices).")); }
            else Ok(2);

            // 4. HidHide
            Busy(3);
            bool reboot = false;
            string ver = HidHide.InstalledVersion();
            bool hidOk = ver != null && (ver == "desconhecida" || HidHide.CompareVersions(ver, HidHide.MinVersion) >= 0);
            if (hidOk) { Out(L.F("HidHide {0} already installed.", ver)); Ok(3); }
            else
            {
                if (!Ui.Confirm(L.T("HidHide installation"),
                    L.T("RealFFB needs HidHide (Nefarius Software Solutions, free, open source), a driver that hides the wheel from FS25.\n\nThe official installer will be downloaded from github.com/nefarius/HidHide. Its digital signature is checked before running and the file is not modified.\n\nWindows will ask for administrator permission (UAC). A restart may be needed.")))
                { Warn(3); Out(L.T("HidHide not installed. RealFFB does not work without it.")); }
                else
                {
                    Result r = new Result();
                    int c = Program.InstallHidHide(r);
                    while (c == 4)
                    {
                        Out(L.F("Could not get HidHide from GitHub: {0}", r.Get("error")));
                        if (MessageBox.Show(owner, L.F("Could not get HidHide from GitHub:\n{0}\n\nCheck your internet connection and try again.", r.Get("error")),
                            "FS25 RealFFB Installer", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) != DialogResult.Retry) break;
                        r = new Result();
                        c = Program.InstallHidHide(r);
                    }
                    if (c == 0) { Ok(3); Out(L.T("HidHide installed.")); }
                    else if (c == 3) { Warn(3); reboot = true; Out(L.T("HidHide installed: Windows needs a restart.")); }
                    else { Fail(3); Out(L.F("HidHide failed (code {0}). See the log at %APPDATA%\\FS25 RealFFB\\setup_log.txt.", c)); }
                }
            }

            // 6. copiar ficheiros (antes da 5, que precisa do caminho final da app)
            Busy(5);
            string here = Application.StartupPath;
            string dest = InstallDir;
            string[] files = { "RealFFB.exe", "RealFFB.Setup.exe" };
            foreach (string fn in files) if (!File.Exists(Path.Combine(here, fn))) { Fail(5); Out(L.F("Missing {0} in {1}. Run build.bat first.", fn, here)); return 1; }
            string modZip = Path.Combine(here, "FS25_RealFFBTelemetry.zip");
            if (!File.Exists(modZip)) { Fail(5); Out(L.F("Missing {0} in {1}. Run build.bat first.", "FS25_RealFFBTelemetry.zip", here)); return 1; }
            if (AppRunning())
            {
                Out(L.T("Close FS25 RealFFB first."));
                MessageBox.Show(owner, L.T("Close FS25 RealFFB and click OK."), "FS25 RealFFB Installer");
                if (AppRunning()) { Fail(5); Out(L.T("RealFFB is still open.")); return 1; }
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
            Shortcut(Path.Combine(StartMenuDir, "FS25 RealFFB - Configure devices.lnk"), helper, "configure", helper);
            Shortcut(Path.Combine(StartMenuDir, "FS25 RealFFB - Diagnostic report.lnk"), helper, "diagnose", helper);
            Shortcut(Path.Combine(StartMenuDir, "Uninstall FS25 RealFFB.lnk"), helper, "uninstall", helper);
            string fsIcon = Path.Combine(game, "FarmingSimulator2025.exe");
            if (MessageBox.Show(owner, L.T("Create a desktop shortcut?\n(Opens RealFFB and FS25)"), "FS25 RealFFB Installer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FS25 RealFFB.lnk"), exe, "--jogar", File.Exists(fsIcon) ? fsIcon : exe);
            RegisterUninstall(dest, helper, exe);
            Program.Opt["game"] = game; Program.Opt["mods"] = mods;
            Program.Remember(new Result());
            Out(L.F("Installed in {0}", dest));
            Ok(5);

            // 5. dispositivos + game.xml
            Busy(4);
            Program.Opt["app"] = exe;
            if (reboot)
            {
                Program.ScheduleResume(new Result());
                Program.PatchGameXml(new Result());
                Warn(4); Out(L.T("Device setup continues automatically after restarting Windows."));
            }
            else if (HidHide.Cli() != null && bases.Count > 0)
            {
                Result r = new Result();
                int c = Program.Configure(r, false);
                if (c == 0) Ok(4); else Warn(4);
                Out(c == 0 ? L.T("Devices: configured") : L.T("Devices: not configured (use 'Configure devices' in the Start menu)"));
            }
            else
            {
                Program.PatchGameXml(new Result());
                Warn(4); Out(L.T("Devices not configured yet: connect the wheel and use Start > FS25 RealFFB - Configure devices."));
            }

            // 7. verificacao
            Busy(6);
            int errors = 0;
            foreach (Diagnostics.Check c in Diagnostics.Verify(dest))
            {
                Out("[" + c.Status + "] " + c.Name + (c.Detail.Length > 0 ? ": " + c.Detail : ""));
                if (c.Status == "ERROR") errors++;
            }
            if (reboot) { Warn(6); Out("\r\n" + L.T("Restart Windows to finish.")); }
            else if (errors == 0) { Ok(6); Out("\r\n" + L.T("Installed and ready. Open FS25 RealFFB from the Start menu or the shortcut.")); }
            else { Warn(6); Out("\r\n" + L.F("Installed with {0} problem(s). Use 'Diagnostic report' to ask for help.", errors)); }
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
            if (MessageBox.Show(L.T("Uninstall FS25 RealFFB?\n\nHidHide and game.xml go back to how they were. HidHide stays installed."),
                "FS25 RealFFB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 2;
            if (AppRunning()) { MessageBox.Show(L.T("Close FS25 RealFFB first."), "FS25 RealFFB"); return 1; }
            bool purge = MessageBox.Show(L.T("Also delete your settings and profiles (FFB tuning, pedals, buttons)?\n\nNo = keep them (recommended if you will reinstall)."),
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
            MessageBox.Show(L.T("FS25 RealFFB uninstalled."), "FS25 RealFFB");
            return 0;
        }
    }
}
