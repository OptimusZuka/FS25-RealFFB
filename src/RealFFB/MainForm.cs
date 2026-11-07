// FS25 Real FFB - janela principal: configuracao, monitor e ciclo de atualizacao.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FS25RealFFB
{
    internal sealed class MainForm : Form
    {
        [DllImport("winmm.dll")] private static extern int timeBeginPeriod(int ms);
        [DllImport("winmm.dll")] private static extern int timeEndPeriod(int ms);

        private readonly Settings settings = Settings.Load();
        private readonly FfbEngine engine = new FfbEngine();
        private IWheelOutput device;
        private TelemetryServer telemetry;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly object logLock = new object();
        private readonly string logPath = AppInfo.DataFile("RealFFB_log.txt");

        private Timer loopTimer;
        private CheckBox enableBox;
        private CheckBox invertBox;
        private Label monitor;
        private TextBox logBox;

        private double lastTickMs;
        private long hzFrames;
        private double hzStartMs, telemetryHz, nextStatsLogMs;
        private FfbOutput lastOut = new FfbOutput();
        private double lastStatusCheckMs;
        private string lastStatus = "";

        private FfbDevice wheel;
        private Updater updater;
        private Updater.Release pendingUpdate;
        private Button updateBtn;
        private ButtonMapper mapper;
        private ListBox mapList;
        private int mapCount = -1;
        private InputBridge bridge;
        private double steerOut, thrOut, brkOut, cluOut;
        private int detectPedal = -1;
        private double detectStartMs;
        private int[] detectBase, detectPeakVal, detectPeakDelta;
        private readonly Label[] pedalLabels = new Label[3];
        private static readonly string[] PedalNames = { "Acelerador", "Travao", "Embraiagem" };
        private long bridgeServedLast;
        private double bridgeRate;

        [STAThread]
        private static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Crash(e.ExceptionObject as Exception); };
            bool first;
            using (System.Threading.Mutex single = new System.Threading.Mutex(true, "FS25RealFFB_unica", out first))
            {
                if (!first)
                {
                    // Ja ha uma RealFFB aberta: duas ao mesmo tempo disputam a base e a telemetria.
                    foreach (string arg in Environment.GetCommandLineArgs())
                        if (arg == "--jogar") { try { Process.Start("steam://rungameid/2300320"); } catch { } }
                    return;
                }
                RunApp();
            }
        }

        private static void RunApp()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex) { Crash(ex); }
        }

        private static void Crash(Exception ex)
        {
            try { if (HidHideControl.Available) HidHideControl.Off(); } catch { }   // nunca deixar o volante escondido
            string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + (ex == null ? "erro desconhecido" : ex.ToString());
            try { File.AppendAllText(AppInfo.DataFile("RealFFB_crash.txt"), text + Environment.NewLine + Environment.NewLine); } catch { }
            try { MessageBox.Show(text, "FS25 Real FFB - erro"); } catch { }
        }

        public MainForm()
        {
            Text = "FS25 RealFFB  v" + AppInfo.Version;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Width = 1100; Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            BackColor = Color.FromArgb(24, 24, 24);
            ForeColor = Color.Gainsboro;
            Font = new Font("Segoe UI", 9.5f);

            Panel left = new Panel();
            left.Dock = DockStyle.Left; left.Width = 380; left.Padding = new Padding(12); left.AutoScroll = true;

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.FlowDirection = FlowDirection.TopDown; flow.WrapContents = false;
            flow.Dock = DockStyle.Fill; flow.AutoScroll = true;

            enableBox = new CheckBox();
            enableBox.Text = "FFB ativo"; enableBox.AutoSize = true;
            enableBox.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            enableBox.ForeColor = Color.FromArgb(245, 197, 24);
            enableBox.CheckedChanged += delegate { Log(enableBox.Checked ? "FFB ligado." : "FFB desligado."); };

            Button stop = new Button();
            stop.Text = "PARAR  (Esc)"; stop.Width = 340; stop.Height = 38;
            stop.BackColor = Color.FromArgb(190, 30, 30); stop.ForeColor = Color.White;
            stop.FlatStyle = FlatStyle.Flat; stop.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            stop.Click += delegate { Panic("botao PARAR"); };

            flow.Controls.Add(enableBox);

            Button play = new Button();
            play.Text = "Abrir o Farming Simulator 25"; play.Width = 340; play.Height = 32; play.FlatStyle = FlatStyle.Flat;
            play.Click += delegate { LaunchGame(); };
            flow.Controls.Add(play);

            updateBtn = new Button();
            updateBtn.Width = 340; updateBtn.Height = 34; updateBtn.FlatStyle = FlatStyle.Flat; updateBtn.Visible = false;
            updateBtn.BackColor = Color.FromArgb(245, 197, 24); updateBtn.ForeColor = Color.Black;
            updateBtn.Click += delegate { InstallUpdate(); };
            flow.Controls.Add(updateBtn);

            Button diag = new Button();
            diag.Text = "Gerar relatorio de diagnostico"; diag.Width = 340; diag.Height = 30; diag.FlatStyle = FlatStyle.Flat;
            diag.Click += delegate { RunDiagnostics(); };
            flow.Controls.Add(diag);

            Button lnk = new Button();
            lnk.Text = "Criar atalho no ambiente de trabalho"; lnk.Width = 340; lnk.Height = 30; lnk.FlatStyle = FlatStyle.Flat;
            lnk.Click += delegate { CreateShortcut(); };
            flow.Controls.Add(lnk);

            CheckBox autoFfb = new CheckBox();
            autoFfb.Text = "Ligar o FFB automaticamente ao abrir a app"; autoFfb.AutoSize = true;
            autoFfb.Checked = settings.AutoFfb;
            autoFfb.CheckedChanged += delegate { settings.AutoFfb = autoFfb.Checked; try { settings.Save(); } catch { } };
            flow.Controls.Add(autoFfb);
            flow.Controls.Add(stop);
            flow.Controls.Add(Slider("Forca maxima", 5, (int)(settings.BaseNm * 10), (int)(settings.MaxNm * 10),
                delegate(int v) { settings.MaxNm = v / 10.0; }, delegate(int v) { return (v / 10.0).ToString("0.0") + " Nm"; }));
            flow.Controls.Add(Slider("Centragem / alinhamento", 0, 100, (int)(settings.Center * 100),
                delegate(int v) { settings.Center = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Amortecimento", 0, 100, (int)(settings.Damping * 100),
                delegate(int v) { settings.Damping = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Peso parado (atrito)", 0, 100, (int)(settings.Friction * 100),
                delegate(int v) { settings.Friction = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Pancadas da suspensao", 0, 100, (int)(settings.Bumps * 100),
                delegate(int v) { settings.Bumps = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Textura do terreno", 0, 100, (int)(settings.Texture * 100),
                delegate(int v) { settings.Texture = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Vibracao do motor", 0, 100, (int)(settings.Engine * 100),
                delegate(int v) { settings.Engine = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Velocidade de referencia", 5, 80, (int)settings.RefSpeedKmh,
                delegate(int v) { settings.RefSpeedKmh = v; }, delegate(int v) { return v + " km/h"; }));

            invertBox = new CheckBox();
            invertBox.Text = "Inverter sentido das pancadas"; invertBox.AutoSize = true;
            invertBox.Checked = settings.InvertBumps;
            invertBox.CheckedChanged += delegate { settings.InvertBumps = invertBox.Checked; };
            flow.Controls.Add(invertBox);

            CheckBox altBox = new CheckBox();
            altBox.Text = "Usar a 2.a interface da base"; altBox.AutoSize = true;
            altBox.Checked = settings.AltInterface;
            altBox.CheckedChanged += delegate { settings.AltInterface = altBox.Checked; Log("Interface da base: " + (altBox.Checked ? "2" : "1")); };
            flow.Controls.Add(altBox);

            Label sect = new Label();
            sect.Text = "Direcao e pedais (ponte para o jogo)"; sect.AutoSize = true;
            sect.Font = new Font("Segoe UI", 10f, FontStyle.Bold); sect.Padding = new Padding(0, 10, 0, 2);
            flow.Controls.Add(sect);

            CheckBox bridgeBox = new CheckBox();
            bridgeBox.Text = "Enviar volante e pedais ao FS25"; bridgeBox.AutoSize = true;
            bridgeBox.Checked = settings.BridgeOn;
            bridgeBox.CheckedChanged += delegate { settings.BridgeOn = bridgeBox.Checked; };
            flow.Controls.Add(bridgeBox);

            flow.Controls.Add(Slider("Sensibilidade da direcao", 50, 400, (int)(settings.SteerSens * 100),
                delegate(int v) { settings.SteerSens = v / 100.0; }, Pct));

            CheckBox steerInv = new CheckBox();
            steerInv.Text = "Inverter direcao"; steerInv.AutoSize = true;
            steerInv.Checked = settings.SteerInvert;
            steerInv.CheckedChanged += delegate { settings.SteerInvert = steerInv.Checked; };
            flow.Controls.Add(steerInv);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                FlowLayoutPanel row = new FlowLayoutPanel();
                row.FlowDirection = FlowDirection.LeftToRight; row.Width = 340; row.Height = 34; row.WrapContents = false;
                Button det = new Button();
                det.Text = "Detetar " + PedalNames[i]; det.Width = 150; det.Height = 28; det.FlatStyle = FlatStyle.Flat;
                det.Click += delegate { StartDetect(idx); };
                Label lab = new Label(); lab.AutoSize = true; lab.Padding = new Padding(4, 7, 0, 0);
                pedalLabels[i] = lab;
                row.Controls.Add(det); row.Controls.Add(lab);
                flow.Controls.Add(row);
            }
            UpdatePedalLabels();

            Label sectB = new Label();
            sectB.Text = "Comandos -> teclas (ButtonBox, TCA, A320, aro)"; sectB.AutoSize = true;
            sectB.Font = new Font("Segoe UI", 10f, FontStyle.Bold); sectB.Padding = new Padding(0, 10, 0, 2);
            flow.Controls.Add(sectB);
            Label hint = new Label();
            hint.Text = "Adicionar -> carregue no botao do comando -> carregue na tecla que o FS25 usa para essa funcao.";
            hint.Width = 340; hint.Height = 34;
            flow.Controls.Add(hint);
            CheckBox keysBox = new CheckBox();
            keysBox.Text = "Ativar (desligado com HidHide: o FS25 le os comandos)"; keysBox.AutoSize = true;
            keysBox.Checked = settings.KeysOn;
            keysBox.CheckedChanged += delegate { settings.KeysOn = keysBox.Checked; if (mapper != null && !keysBox.Checked) mapper.ReleaseAll(); try { settings.Save(); } catch { } };
            flow.Controls.Add(keysBox);
            FlowLayoutPanel brow = new FlowLayoutPanel();
            brow.FlowDirection = FlowDirection.LeftToRight; brow.Width = 340; brow.Height = 34; brow.WrapContents = false;
            Button addB = new Button(); addB.Text = "Adicionar botao"; addB.Width = 160; addB.Height = 28; addB.FlatStyle = FlatStyle.Flat;
            addB.Click += delegate { if (mapper != null) mapper.StartLearn(); };
            Button delB = new Button(); delB.Text = "Remover selecionado"; delB.Width = 170; delB.Height = 28; delB.FlatStyle = FlatStyle.Flat;
            delB.Click += delegate
            {
                if (mapper != null && mapList.SelectedItem != null) { mapper.Remove(mapList.SelectedItem as ButtonMapper.Mapping); RefreshMappings(); }
            };
            brow.Controls.Add(addB); brow.Controls.Add(delB);
            flow.Controls.Add(brow);
            Button impB = new Button(); impB.Text = "Importar tudo do FS25 (inputBinding.xml)"; impB.Width = 340; impB.Height = 30; impB.FlatStyle = FlatStyle.Flat;
            impB.Click += delegate { if (mapper != null) { mapper.ImportFromGame(); RefreshMappings(); } };
            flow.Controls.Add(impB);
            mapList = new ListBox(); mapList.Width = 340; mapList.Height = 140;
            mapList.BackColor = Color.FromArgb(16, 16, 16); mapList.ForeColor = Color.Gainsboro;
            flow.Controls.Add(mapList);

            CheckBox hideBox = new CheckBox();
            hideBox.Text = "Gerir o HidHide sozinha (esconder a Fanatec so com a app aberta)"; hideBox.AutoSize = true;
            hideBox.MaximumSize = new Size(340, 0);
            hideBox.Checked = settings.AutoHide;
            hideBox.CheckedChanged += delegate
            {
                settings.AutoHide = hideBox.Checked;
                if (HidHideControl.Available) Log("HidHide: " + (hideBox.Checked ? HidHideControl.On() : HidHideControl.Off()));
                try { settings.Save(); } catch { }
            };
            flow.Controls.Add(hideBox);

            Button save = new Button();
            save.Text = "Guardar configuracao"; save.Width = 340; save.Height = 32; save.FlatStyle = FlatStyle.Flat;
            save.Click += delegate
            {
                try { settings.Save(); Log("Configuracao guardada."); }
                catch (Exception ex) { Log("Erro ao guardar: " + ex.Message); }
            };
            flow.Controls.Add(save);
            left.Controls.Add(flow);

            monitor = new Label();
            monitor.Dock = DockStyle.Top; monitor.Height = 230;
            monitor.Font = new Font("Consolas", 10f);
            monitor.Padding = new Padding(10);
            monitor.BackColor = Color.FromArgb(16, 16, 16);

            logBox = new TextBox();
            logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill; logBox.Font = new Font("Consolas", 9f);
            logBox.BackColor = Color.FromArgb(12, 12, 12); logBox.ForeColor = Color.Silver;
            logBox.BorderStyle = BorderStyle.None;

            Panel right = new Panel(); right.Dock = DockStyle.Fill;
            right.Controls.Add(logBox);
            right.Controls.Add(monitor);

            Controls.Add(right);
            Controls.Add(left);

            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (mapper != null && mapper.LearnState != 0)
                {
                    e.Handled = true; e.SuppressKeyPress = true;
                    if (e.KeyCode == Keys.Escape || mapper.LearnState == 1) mapper.CancelLearn();
                    else mapper.LearnKey(e.KeyCode);
                    RefreshMappings();
                    return;
                }
                if (e.KeyCode == Keys.Escape) { e.Handled = true; e.SuppressKeyPress = true; Panic("tecla Esc"); }
            };
            Shown += delegate { Init(); };
            FormClosing += delegate { Shutdown(); };
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Log("ERRO: " + e.Exception.Message);
                Panic("excecao");
            };
        }

        private delegate void IntAction(int v);
        private delegate string IntFormat(int v);

        private static string Pct(int v) { return v + " %"; }

        private Control Slider(string title, int min, int max, int value, IntAction onChange, IntFormat fmt)
        {
            Panel p = new Panel(); p.Width = 340; p.Height = 62;
            Label l = new Label(); l.AutoSize = false; l.Width = 340; l.Height = 20; l.Top = 0;
            TrackBar t = new TrackBar();
            t.Minimum = min; t.Maximum = max; t.Value = Math.Max(min, Math.Min(max, value));
            t.TickStyle = TickStyle.None; t.Width = 340; t.Top = 22;
            l.Text = title + ":  " + fmt(t.Value);
            t.ValueChanged += delegate { onChange(t.Value); l.Text = title + ":  " + fmt(t.Value); };
            p.Controls.Add(l); p.Controls.Add(t);
            return p;
        }

        // ------------------------------------------------------------------ log
        private void Log(string s)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(Log), s); } catch { }
                return;
            }
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s;
            logBox.AppendText(line + Environment.NewLine);
            lock (logLock)
            {
                try { File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8); } catch { }
            }
        }

        // ------------------------------------------------------------------ ciclo
        private void Init()
        {
            Log("==================== FS25 RealFFB v" + AppInfo.Version + " - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " ====================");
            timeBeginPeriod(1);
            if (settings.AutoHide)
            {
                if (HidHideControl.Available) Log("HidHide: Fanatec escondida do FS25 -> " + HidHideControl.On());
                else Log("HidHide nao encontrado: a gestao automatica fica desligada.");
            }
            device = OpenOutput();
            if (!device.IsOpen) Log("A base nao abriu. Feche jogos/programas que usem o volante e reinicie a app.");

            for (int q = 1; q < 3; q++)
                if (settings.PedalAxis[q] >= 0 && settings.PedalAxis[q] == settings.PedalAxis[0])
                {
                    Log("ATENCAO: " + PedalNames[q] + " estava no mesmo eixo do acelerador (por isso o trator fazia re). Detete-o de novo.");
                    settings.PedalAxis[q] = -1;
                    UpdatePedalLabels();
                }
            mapper = new ButtonMapper(Log);
            mapper.Open(Handle);
            RefreshMappings();
            bridge = new InputBridge(Log);
            bridge.Start();

            telemetry = new TelemetryServer(Log);
            telemetry.Start();
            Log("A aguardar o FS25 (mod 'Real FFB Telemetry' ativo no save).");

            lastTickMs = clock.Elapsed.TotalMilliseconds;
            hzStartMs = lastTickMs;
            loopTimer = new Timer();
            loopTimer.Interval = 5;
            loopTimer.Tick += delegate { Tick(); };
            loopTimer.Start();

            if (settings.AutoFfb && device.IsOpen) enableBox.Checked = true;
            // (o atalho com o icone do FS25 e criado pelo instalador; o botao continua disponivel)
            updater = new Updater(Log);
            updater.CheckAsync(delegate(Updater.Release r) { BeginInvoke(new Action<Updater.Release>(ShowUpdate), r); });
            foreach (string a in Environment.GetCommandLineArgs())
                if (a == "--jogar") LaunchGame();
        }

        // Atalho "FS25 com FFB" com o icone do proprio jogo (lido do executavel instalado).
        private void CreateShortcut()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string link = Path.Combine(desktop, "FS25 com FFB.lnk");
                string game = ReadState("GameDir") ?? @"C:\Program Files (x86)\Steam\steamapps\common\Farming Simulator 25";
                string icon = Path.Combine(game, "FarmingSimulator2025.exe");
                if (!File.Exists(icon)) icon = Path.Combine(game, @"x64\FarmingSimulator2025Game.exe");
                if (!File.Exists(icon)) icon = Application.ExecutablePath;

                Type t = Type.GetTypeFromProgID("WScript.Shell");
                object shell = Activator.CreateInstance(t);
                object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { link });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { Application.ExecutablePath });
                st.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { "--jogar" });
                st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { Application.StartupPath });
                st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { icon + ",0" });
                st.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { "FS25 com force feedback (RealFFB)" });
                st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(sc);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
                Log("Atalho criado: " + link);
            }
            catch (Exception ex) { Log("Nao consegui criar o atalho: " + ex.Message); }
        }

        // Pasta do jogo descoberta pelo instalador (install-state.ini).
        private static string ReadState(string key)
        {
            try
            {
                string f = AppInfo.DataFile("install-state.ini");
                if (!File.Exists(f)) return null;
                foreach (string line in File.ReadAllLines(f))
                    if (line.StartsWith(key + "=")) { string v = line.Substring(key.Length + 1).Trim(); return v.Length > 0 ? v : null; }
            }
            catch { }
            return null;
        }

        private void ShowUpdate(Updater.Release r)
        {
            pendingUpdate = r;
            updateBtn.Text = "Nova versao disponivel: v" + r.Version + "  (instalar)";
            updateBtn.Visible = true;
            Log("Atualizacoes: ha uma versao nova (v" + r.Version + "). Notas: " + r.PageUrl);
        }

        private void InstallUpdate()
        {
            if (pendingUpdate == null) return;
            DialogResult d = MessageBox.Show(this,
                "Instalar a versao v" + pendingUpdate.Version + "?\n\nAs suas definicoes, pedais e comandos sao mantidos.\nNotas da versao: " + pendingUpdate.PageUrl,
                "Atualizar FS25 RealFFB", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (d != DialogResult.OK) return;
            try { if (updater.DownloadAndRun(pendingUpdate)) Close(); }
            catch (Exception ex) { Log("Atualizacao falhou: " + ex.Message); }
        }

        private void RunDiagnostics()
        {
            try
            {
                if (!File.Exists(AppInfo.SetupHelper)) { Log("Diagnostico: RealFFB.Setup.exe nao encontrado (instale com o instalador)."); return; }
                string report = AppInfo.DataFile("diagnostico.txt");
                ProcessStartInfo psi = new ProcessStartInfo(AppInfo.SetupHelper, "diagnose --quiet --report \"" + report + "\"");
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi)) p.WaitForExit(30000);
                Log("Diagnostico gravado em " + report + " (pode anexa-lo num Issue no GitHub).");
                Process.Start("notepad.exe", "\"" + report + "\"");
            }
            catch (Exception ex) { Log("Diagnostico falhou: " + ex.Message); }
        }

        private void LaunchGame()
        {
            try { Process.Start("steam://rungameid/2300320"); Log("A abrir o FS25 pela Steam..."); }
            catch (Exception ex) { Log("Nao consegui abrir o FS25: " + ex.Message); }
        }

        // Preferencia: Windows.Gaming.Input (nao disputa a base com o FS25).
        // Recurso: DirectInput (funciona so se nenhum jogo prender a base).
        // v0.17: a app controla a base diretamente (DirectInput); o FS25 nao toca na base.
        private IWheelOutput OpenOutput()
        {
            FfbDevice d = new FfbDevice(Log);
            d.Open(Handle);
            wheel = d;
            if (d.IsOpen) Log("Saida de forca: DirectInput direto (o FS25 nao pode estar a usar o volante).");
            return d;
        }

        private void Tick()
        {
            double now = clock.Elapsed.TotalMilliseconds;
            double dt = (now - lastTickMs) / 1000.0;
            lastTickMs = now;
            if (dt <= 0 || dt > 0.2) dt = 0.016;

            UpdateInputs(now);
            if (mapper != null)
            {
                if (settings.KeysOn) mapper.Poll(wheel != null && wheel.IsOpen ? wheel.Buttons : null);
                if (mapper.Mappings.Count != mapCount) RefreshMappings();
            }

            double age;
            TelemetryFrame f = telemetry.GetLatest(out age);
            engine.SentSteer = steerOut;
            FfbOutput o = engine.Update(f, age, enableBox.Checked && device.IsOpen, dt, settings);
            lastOut = o;

            if (device.IsOpen)
            {
                if (wheel != null) wheel.SetSpring(o.SpringCoef, o.SpringSat, o.SpringOffset);
                else device.SetSpring(o.SpringCoef, o.SpringSat);
                device.SetDamper(o.DamperCoef, o.DamperSat);
                device.SetFriction(o.FrictionCoef, o.FrictionSat);
                device.SetConstant(o.ConstMag);
                device.SetSine(o.SineMag, o.SinePeriodUs);
                if (wheel != null) wheel.SetEngine(o.EngineMag, o.EnginePeriodUs);
                device.Commit();
            }

            if (now - hzStartMs >= 1000)
            {
                long served = bridge.Served;
                bridgeRate = (served - bridgeServedLast) * 1000.0 / (now - hzStartMs);
                bridgeServedLast = served;
                long frames = telemetry.FrameCount;
                telemetryHz = (frames - hzFrames) * 1000.0 / (now - hzStartMs);
                hzFrames = frames; hzStartMs = now;
                UpdateMonitor(f, age);
            }
            else if (((int)now / 200) != ((int)(now - dt * 1000) / 200))
            {
                UpdateMonitor(f, age);
            }

            if (now - lastStatusCheckMs >= 1000)
            {
                lastStatusCheckMs = now;
                string st = device.DeviceName;
                if (st != lastStatus) { lastStatus = st; Log("Estado: " + st); }
            }

            if (now >= nextStatsLogMs && telemetry.Connected && f != null && f.Active)
            {
                nextStatsLogMs = now + 5000;
                Log(string.Format(CultureInfo.InvariantCulture,
                    "[stats] ponte {16:0}/s dir {17:+0.00;-0.00} acel {18:0.00} trav {19:0.00} | {0:0} Hz | {1:0.0} km/h | {2:0.0} t | piso {3} | rodas {4}/{5} | steer {6:0.00} | vAcc {7:0.0} | rollAcc {8:0.0} | susp {9:0.00} || mola {10:0.00} Nm | amort {11:0.00} | atrito {12:0.00} | pancada {13:+0.00;-0.00} | textura {14:0.00} | ganho {15:0%}",
                    telemetryHz, f.SpeedKmh, f.MassT, f.Surface, f.Contacts, f.Wheels, f.Steer, f.VertAcc, f.RollAcc, f.SuspSpreadRate,
                    o.SpringNm, o.DamperNm, o.FrictionNm, o.BumpNm, o.TextureNm, o.Gain, bridgeRate, steerOut, thrOut, brkOut));
            }
        }

        private void RefreshMappings()
        {
            if (mapper == null || mapList == null) return;
            mapCount = mapper.Mappings.Count;
            mapList.Items.Clear();
            foreach (ButtonMapper.Mapping m in mapper.Mappings) mapList.Items.Add(m);
        }

        // ------------------------------------------------------------------ ponte de comandos
        private double Pedal(int i)
        {
            int a = settings.PedalAxis[i];
            if (a < 0 || wheel == null) return 0;
            int rel = settings.PedalRel[i], full = settings.PedalFull[i];
            if (full == rel) return 0;
            double x = (wheel.Axes[a] - rel) / (double)(full - rel);
            x = (x - 0.04) / 0.96;     // zona morta de 4%: pedal solto = 0 (senao o FS25 trava/faz re)
            return x < 0 ? 0 : (x > 1 ? 1 : x);
        }

        private void UpdateInputs(double now)
        {
            if (wheel == null || !wheel.IsOpen || !wheel.ReadState()) return;
            double s = wheel.Axes[0] / 10000.0 * settings.SteerSens * (settings.SteerInvert ? -1 : 1);
            steerOut = s < -1 ? -1 : (s > 1 ? 1 : s);
            thrOut = Pedal(0); brkOut = Pedal(1); cluOut = Pedal(2);
            bridge.Publish(settings.BridgeOn, steerOut, thrOut, brkOut, cluOut);

            if (detectPedal >= 0)
            {
                for (int k = 0; k < FfbDevice.AxisCount; k++)
                {
                    if (k == 0) continue; // o eixo X e a direcao
                    int d = Math.Abs(wheel.Axes[k] - detectBase[k]);
                    if (d > detectPeakDelta[k]) { detectPeakDelta[k] = d; detectPeakVal[k] = wheel.Axes[k]; }
                }
                if (now - detectStartMs > 3000) FinishDetect();
            }
        }

        private void StartDetect(int i)
        {
            if (wheel == null || !wheel.IsOpen) { Log("Base nao esta aberta."); return; }
            wheel.ReadState();
            detectBase = (int[])wheel.Axes.Clone();
            detectPeakVal = new int[FfbDevice.AxisCount];
            detectPeakDelta = new int[FfbDevice.AxisCount];
            detectStartMs = clock.Elapsed.TotalMilliseconds;
            detectPedal = i;
            Log("Carregue a FUNDO no " + PedalNames[i] + " agora (3 s)...");
        }

        private void FinishDetect()
        {
            int i = detectPedal, best = -1, bestD = 0;
            detectPedal = -1;
            for (int k = 1; k < FfbDevice.AxisCount; k++)
            {
                bool used = false;
                for (int q = 0; q < 3; q++) if (q != i && settings.PedalAxis[q] == k) used = true;
                if (!used && detectPeakDelta[k] > bestD) { bestD = detectPeakDelta[k]; best = k; }
            }
            if (best < 0 || bestD < 2000) { Log(PedalNames[i] + ": nenhum movimento detetado. Tente de novo."); return; }
            settings.PedalAxis[i] = best;
            settings.PedalRel[i] = detectBase[best];
            settings.PedalFull[i] = detectPeakVal[best];
            Log(string.Format("{0}: eixo {1} (solto {2}, a fundo {3}). Carregue em 'Guardar configuracao'.",
                PedalNames[i], FfbDevice.AxisNames[best], detectBase[best], detectPeakVal[best]));
            UpdatePedalLabels();
        }

        private void UpdatePedalLabels()
        {
            for (int i = 0; i < 3; i++)
                if (pedalLabels[i] != null)
                    pedalLabels[i].Text = settings.PedalAxis[i] < 0 ? "(nao definido)" : "eixo " + FfbDevice.AxisNames[settings.PedalAxis[i]];
        }

        private void UpdateMonitor(TelemetryFrame f, double age)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Base:        " + (device.IsOpen ? device.DeviceName : "NAO ABERTA"));
            sb.AppendLine("Estado:      " + lastOut.State);
            if (telemetry.Connected && f != null)
                sb.AppendLine(string.Format(c, "Telemetria:  ligada | {0:0} Hz | idade {1:0} ms", telemetryHz, age));
            else
                sb.AppendLine("Telemetria:  sem ligacao (FS25 fechado ou mod desativado)");
            if (f != null && f.Active)
            {
                sb.AppendLine(string.Format(c, "Veiculo:     {0} | {1:0.0} t | {2:0.0} km/h | piso {3} | rodas no chao {4}/{5}",
                    f.Vehicle, f.MassT, f.SpeedKmh, f.Surface, f.Contacts, f.Wheels));
                sb.AppendLine(string.Format(c, "Direcao:     input {0:0.00} | volante {1:0} unid.", f.Steer, device.IsOpen ? device.ReadPosition() : 0));
            }
            else
            {
                sb.AppendLine("Veiculo:     -");
                sb.AppendLine("Direcao:     -");
            }
            sb.AppendLine(string.Format(c, "Comandos:    dir {0:+0.00;-0.00} | acel {1:0%} | trav {2:0%} | emb {3:0%} | ponte {4:0}/s {5}",
                steerOut, thrOut, brkOut, cluOut, bridgeRate, settings.BridgeOn ? "" : "(DESLIGADA)"));
            if (wheel != null)
            {
                StringBuilder ax = new StringBuilder("Eixos:      ");
                for (int i = 0; i < FfbDevice.AxisCount; i++) ax.Append(" " + FfbDevice.AxisNames[i] + " " + wheel.Axes[i]);
                sb.AppendLine(ax.ToString());
            }
            sb.AppendLine(string.Format(c, "Saida:       ganho {0:0%} | mola {1:0.00} Nm | amort {2:0.00} | atrito {3:0.00} | pancada {4:+0.00;-0.00} | textura {5:0.00} | motor {6:0.00}",
                lastOut.Gain, lastOut.SpringNm, lastOut.DamperNm, lastOut.FrictionNm, lastOut.BumpNm, lastOut.TextureNm, lastOut.EngineNm));
            sb.Append(string.Format(c, "Limite:      {0:0.0} Nm (base {1:0} Nm)", settings.MaxNm, settings.BaseNm));
            monitor.Text = sb.ToString();
        }

        private void Panic(string why)
        {
            enableBox.Checked = false;
            if (device != null) device.EmergencyStop();
            Log("PARAGEM IMEDIATA (" + why + ")");
        }

        private void Shutdown()
        {
            if (loopTimer != null) loopTimer.Stop();
            if (telemetry != null) telemetry.Stop();
            if (bridge != null) { bridge.Publish(false, 0, 0, 0, 0); bridge.Stop(); }
            if (mapper != null) mapper.Dispose();
            if (device != null) device.Dispose();
            timeEndPeriod(1);
            if (settings.AutoHide && HidHideControl.Available)
            {
                string r = HidHideControl.Off();
                lock (logLock) { try { File.AppendAllText(logPath, "HidHide: Fanatec visivel para os outros jogos -> " + r + Environment.NewLine, Encoding.UTF8); } catch { } }
            }
            lock (logLock)
            {
                try { File.AppendAllText(logPath, "---- app fechada, forcas paradas ----" + Environment.NewLine, Encoding.UTF8); } catch { }
            }
        }
    }
}
