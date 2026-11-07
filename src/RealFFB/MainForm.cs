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

        private const int W = 360;           // largura dos controlos do painel de definicoes
        private LiveView live;
        private StatusChip chipBase, chipTel, chipBridge;
        private Panel leftPanel, rightPanel;
        private Button pinBtn;
        private Control chipsBox;
        private bool pinned;
        private Rectangle normalBounds;
        private double lastLiveMs;

        public MainForm()
        {
            Text = "FS25 RealFFB  v" + AppInfo.Version;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Width = 1180; Height = 780;
            MinimumSize = new Size(640, 400);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.UI(9.5f);

            // ---------------------------------------------------------- cabecalho
            Panel header = new Panel();
            header.Dock = DockStyle.Top; header.Height = 78; header.BackColor = Theme.Bg;
            Panel accent = new Panel();
            accent.Dock = DockStyle.Top; accent.Height = 2; accent.BackColor = Theme.Yellow;

            PictureBox logo = new PictureBox();
            logo.Image = Theme.Logo(); logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.SetBounds(12, 6, 96, 66);
            header.Controls.Add(logo);

            Label title = new Label();
            title.Text = "FS25 RealFFB"; title.Font = Theme.Bold(17f); title.ForeColor = Theme.Text;
            title.AutoSize = true; title.Location = new Point(114, 10);
            header.Controls.Add(title);
            Label sub = new Label();
            sub.Text = "by OptimusZuka   |   v" + AppInfo.Version; sub.Font = Theme.UI(9f); sub.ForeColor = Theme.Yellow;
            sub.AutoSize = true; sub.Location = new Point(117, 46);
            header.Controls.Add(sub);

            FlowLayoutPanel chips = new FlowLayoutPanel();
            chips.FlowDirection = FlowDirection.TopDown; chips.WrapContents = false;
            chips.SetBounds(330, 6, 170, 70); chips.BackColor = Theme.Bg;
            chipBase = new StatusChip("Base"); chipTel = new StatusChip("Telemetria"); chipBridge = new StatusChip("Ponte");
            chipBase.Margin = chipTel.Margin = chipBridge.Margin = new Padding(0);
            chipBase.Height = chipTel.Height = chipBridge.Height = 22; chipBase.Width = chipTel.Width = chipBridge.Width = 170;
            chips.Controls.Add(chipBase); chips.Controls.Add(chipTel); chips.Controls.Add(chipBridge);
            header.Controls.Add(chips);
            chipsBox = chips;

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Right; actions.Width = 470; actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false; actions.Padding = new Padding(0, 18, 12, 0); actions.BackColor = Theme.Bg;

            Button stop = Theme.Btn("PARAR (Esc)", 120, 40, false);
            stop.BackColor = Color.FromArgb(180, 28, 28); stop.FlatAppearance.BorderColor = Color.FromArgb(180, 28, 28);
            stop.FlatAppearance.MouseOverBackColor = Color.FromArgb(210, 40, 40);
            stop.Margin = new Padding(8, 0, 0, 0);
            stop.Click += delegate { Panic("botao PARAR"); };

            enableBox = new CheckBox();
            enableBox.Appearance = Appearance.Button; enableBox.FlatStyle = FlatStyle.Flat;
            enableBox.Width = 140; enableBox.Height = 40; enableBox.TextAlign = ContentAlignment.MiddleCenter;
            enableBox.Font = Theme.Bold(10.5f); enableBox.Cursor = Cursors.Hand;
            enableBox.Margin = new Padding(8, 0, 0, 0);
            StyleToggle();
            enableBox.CheckedChanged += delegate { StyleToggle(); Log(enableBox.Checked ? "FFB ligado." : "FFB desligado."); };

            pinBtn = Theme.Btn("Fixar no topo", 120, 40, false);
            pinBtn.Margin = new Padding(8, 0, 0, 0);
            pinBtn.Click += delegate { TogglePin(); };

            Button play = Theme.Btn("Abrir FS25", 80, 40, false);
            play.Margin = new Padding(8, 0, 0, 0);
            play.Click += delegate { LaunchGame(); };

            actions.Controls.Add(stop); actions.Controls.Add(enableBox); actions.Controls.Add(pinBtn); actions.Controls.Add(play);
            header.Controls.Add(actions);

            // ---------------------------------------------------------- definicoes (coluna esquerda)
            leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Left; leftPanel.Width = W + 44; leftPanel.BackColor = Theme.Panel;
            Panel sep = new Panel(); sep.Dock = DockStyle.Left; sep.Width = 1; sep.BackColor = Theme.Line;

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.FlowDirection = FlowDirection.TopDown; flow.WrapContents = false;
            flow.Dock = DockStyle.Fill; flow.AutoScroll = true;
            flow.Padding = new Padding(16, 4, 8, 16); flow.BackColor = Theme.Panel;

            updateBtn = Theme.Btn("", W, 36, true);
            updateBtn.Visible = false;
            updateBtn.Click += delegate { InstallUpdate(); };
            flow.Controls.Add(updateBtn);

            flow.Controls.Add(Theme.Section("Forca", W));
            flow.Controls.Add(Slider("Forca maxima", 5, (int)(settings.BaseNm * 10), (int)(settings.MaxNm * 10),
                delegate(int v) { settings.MaxNm = v / 10.0; }, delegate(int v) { return (v / 10.0).ToString("0.0") + " Nm"; }));
            flow.Controls.Add(Slider("Centragem / alinhamento", 0, 100, (int)(settings.Center * 100),
                delegate(int v) { settings.Center = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Amortecimento", 0, 100, (int)(settings.Damping * 100),
                delegate(int v) { settings.Damping = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Peso parado (atrito)", 0, 100, (int)(settings.Friction * 100),
                delegate(int v) { settings.Friction = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Velocidade de referencia", 5, 80, (int)settings.RefSpeedKmh,
                delegate(int v) { settings.RefSpeedKmh = v; }, delegate(int v) { return v + " km/h"; }));

            flow.Controls.Add(Theme.Section("Terreno e motor", W));
            flow.Controls.Add(Slider("Pancadas da suspensao", 0, 100, (int)(settings.Bumps * 100),
                delegate(int v) { settings.Bumps = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Textura do terreno", 0, 100, (int)(settings.Texture * 100),
                delegate(int v) { settings.Texture = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Vibracao do motor", 0, 100, (int)(settings.Engine * 100),
                delegate(int v) { settings.Engine = v / 100.0; }, Pct));
            invertBox = Theme.Check("Inverter sentido das pancadas", settings.InvertBumps, W);
            invertBox.CheckedChanged += delegate { settings.InvertBumps = invertBox.Checked; };
            flow.Controls.Add(invertBox);

            flow.Controls.Add(Theme.Section("Direcao e pedais", W));
            CheckBox bridgeBox = Theme.Check("Enviar volante e pedais ao FS25", settings.BridgeOn, W);
            bridgeBox.CheckedChanged += delegate { settings.BridgeOn = bridgeBox.Checked; };
            flow.Controls.Add(bridgeBox);
            flow.Controls.Add(Slider("Sensibilidade da direcao", 50, 400, (int)(settings.SteerSens * 100),
                delegate(int v) { settings.SteerSens = v / 100.0; }, Pct));
            CheckBox steerInv = Theme.Check("Inverter direcao", settings.SteerInvert, W);
            steerInv.CheckedChanged += delegate { settings.SteerInvert = steerInv.Checked; };
            flow.Controls.Add(steerInv);
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                FlowLayoutPanel row = new FlowLayoutPanel();
                row.FlowDirection = FlowDirection.LeftToRight; row.Width = W; row.Height = 38; row.WrapContents = false;
                row.Margin = new Padding(0);
                Button det = Theme.Btn("Detetar " + PedalNames[i], 170, 30, false);
                det.Click += delegate { StartDetect(idx); };
                Label lab = new Label(); lab.AutoSize = true; lab.Padding = new Padding(8, 10, 0, 0); lab.ForeColor = Theme.Muted;
                pedalLabels[i] = lab;
                row.Controls.Add(det); row.Controls.Add(lab);
                flow.Controls.Add(row);
            }
            UpdatePedalLabels();

            flow.Controls.Add(Theme.Section("Comandos -> teclas (opcional)", W));
            Label hint = new Label();
            hint.Text = "So e preciso sem HidHide. Adicionar -> carregue no botao do comando -> carregue na tecla do FS25.";
            hint.Width = W; hint.Height = 36; hint.ForeColor = Theme.Muted;
            flow.Controls.Add(hint);
            CheckBox keysBox = Theme.Check("Ativar traducao de botoes em teclas", settings.KeysOn, W);
            keysBox.CheckedChanged += delegate { settings.KeysOn = keysBox.Checked; if (mapper != null && !keysBox.Checked) mapper.ReleaseAll(); try { settings.Save(); } catch { } };
            flow.Controls.Add(keysBox);
            FlowLayoutPanel brow = new FlowLayoutPanel();
            brow.FlowDirection = FlowDirection.LeftToRight; brow.Width = W; brow.Height = 38; brow.WrapContents = false; brow.Margin = new Padding(0);
            Button addB = Theme.Btn("Adicionar botao", 176, 30, false);
            addB.Margin = new Padding(0, 3, 8, 3);
            addB.Click += delegate { if (mapper != null) mapper.StartLearn(); };
            Button delB = Theme.Btn("Remover selecionado", 176, 30, false);
            delB.Click += delegate
            {
                if (mapper != null && mapList.SelectedItem != null) { mapper.Remove(mapList.SelectedItem as ButtonMapper.Mapping); RefreshMappings(); }
            };
            brow.Controls.Add(addB); brow.Controls.Add(delB);
            flow.Controls.Add(brow);
            Button impB = Theme.Btn("Importar tudo do FS25 (inputBinding.xml)", W, 30, false);
            impB.Click += delegate { if (mapper != null) { mapper.ImportFromGame(); RefreshMappings(); } };
            flow.Controls.Add(impB);
            mapList = new ListBox(); mapList.Width = W; mapList.Height = 120;
            mapList.BackColor = Theme.Card; mapList.ForeColor = Theme.Text; mapList.BorderStyle = BorderStyle.None;
            flow.Controls.Add(mapList);

            flow.Controls.Add(Theme.Section("Sistema", W));
            CheckBox autoFfb = Theme.Check("Ligar o FFB automaticamente ao abrir a app", settings.AutoFfb, W);
            autoFfb.CheckedChanged += delegate { settings.AutoFfb = autoFfb.Checked; try { settings.Save(); } catch { } };
            flow.Controls.Add(autoFfb);
            CheckBox hideBox = Theme.Check("Gerir o HidHide sozinha (Fanatec escondida so com a app aberta)", settings.AutoHide, W);
            hideBox.CheckedChanged += delegate
            {
                settings.AutoHide = hideBox.Checked;
                if (HidHideControl.Available) Log("HidHide: " + (hideBox.Checked ? HidHideControl.On() : HidHideControl.Off()));
                try { settings.Save(); } catch { }
            };
            flow.Controls.Add(hideBox);
            CheckBox altBox = Theme.Check("Usar a 2.a interface da base", settings.AltInterface, W);
            altBox.CheckedChanged += delegate { settings.AltInterface = altBox.Checked; Log("Interface da base: " + (altBox.Checked ? "2" : "1")); };
            flow.Controls.Add(altBox);

            Button save = Theme.Btn("Guardar configuracao", W, 38, true);
            save.Margin = new Padding(0, 12, 0, 4);
            save.Click += delegate
            {
                try { settings.Save(); Log("Configuracao guardada."); }
                catch (Exception ex) { Log("Erro ao guardar: " + ex.Message); }
            };
            flow.Controls.Add(save);
            Button diag = Theme.Btn("Gerar relatorio de diagnostico", W, 30, false);
            diag.Click += delegate { RunDiagnostics(); };
            flow.Controls.Add(diag);
            Button lnk = Theme.Btn("Criar atalho no ambiente de trabalho", W, 30, false);
            lnk.Click += delegate { CreateShortcut(); };
            flow.Controls.Add(lnk);

            leftPanel.Controls.Add(flow);
            leftPanel.Controls.Add(sep);

            // ---------------------------------------------------------- ao vivo (coluna direita)
            live = new LiveView();
            live.Dock = DockStyle.Top; live.Height = 300;

            monitor = new Label();
            monitor.Dock = DockStyle.Top; monitor.Height = 150;
            monitor.Font = new Font("Consolas", 8.5f);
            monitor.Padding = new Padding(14, 8, 8, 8);
            monitor.BackColor = Theme.Bg; monitor.ForeColor = Theme.Muted;

            Label logTitle = Theme.Section("Registo", 300);
            logTitle.Dock = DockStyle.Top; logTitle.Height = 26; logTitle.Padding = new Padding(12, 0, 0, 0);
            logTitle.BackColor = Theme.Bg;

            logBox = new TextBox();
            logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill; logBox.Font = new Font("Consolas", 9f);
            logBox.BackColor = Theme.Bg; logBox.ForeColor = Color.Silver;
            logBox.BorderStyle = BorderStyle.None;

            rightPanel = new Panel(); rightPanel.Dock = DockStyle.Fill; rightPanel.BackColor = Theme.Bg;
            rightPanel.Controls.Add(logBox);
            rightPanel.Controls.Add(logTitle);
            rightPanel.Controls.Add(monitor);
            rightPanel.Controls.Add(live);

            Controls.Add(rightPanel);
            Controls.Add(leftPanel);
            Controls.Add(accent);
            Controls.Add(header);

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

        private void StyleToggle()
        {
            bool on = enableBox.Checked;
            enableBox.Text = on ? "FFB ATIVO" : "FFB DESLIGADO";
            enableBox.BackColor = on ? Theme.Yellow : Theme.Card;
            enableBox.ForeColor = on ? Color.Black : Theme.Text;
            enableBox.FlatAppearance.BorderColor = on ? Theme.Yellow : Theme.Line;
            enableBox.FlatAppearance.CheckedBackColor = Theme.Yellow;
        }

        // Fixar: janela sempre por cima, so com o painel ao vivo (para ver os comandos enquanto joga).
        private void TogglePin()
        {
            pinned = !pinned;
            if (pinned)
            {
                normalBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                WindowState = FormWindowState.Normal;
                leftPanel.Visible = false;
                monitor.Visible = false;
                chipsBox.Visible = false;
                TopMost = true;
                Size = new Size(920, 450);
                pinBtn.Text = "Desafixar";
                pinBtn.BackColor = Theme.Yellow; pinBtn.ForeColor = Color.Black;
            }
            else
            {
                TopMost = false;
                leftPanel.Visible = true;
                monitor.Visible = true;
                chipsBox.Visible = true;
                if (normalBounds.Width > 0) Bounds = normalBounds;
                pinBtn.Text = "Fixar no topo";
                pinBtn.BackColor = Theme.Card; pinBtn.ForeColor = Theme.Text;
            }
            Log(pinned ? "Janela fixada no topo (modo compacto)." : "Janela normal.");
        }

        private delegate void IntAction(int v);
        private delegate string IntFormat(int v);

        private static string Pct(int v) { return v + " %"; }

        private Control Slider(string title, int min, int max, int value, IntAction onChange, IntFormat fmt)
        {
            Panel p = new Panel(); p.Width = W; p.Height = 56; p.Margin = new Padding(0, 2, 0, 2);
            Label l = new Label(); l.AutoSize = false; l.Width = W - 90; l.Height = 20; l.Top = 2; l.ForeColor = Theme.Text;
            Label v = new Label(); v.AutoSize = false; v.Width = 90; v.Height = 20; v.Left = W - 90; v.Top = 2;
            v.TextAlign = ContentAlignment.TopRight; v.ForeColor = Theme.Yellow; v.Font = Theme.Bold(9.5f);
            NoWheelTrackBar t = new NoWheelTrackBar();
            t.Minimum = min; t.Maximum = max; t.Value = Math.Max(min, Math.Min(max, value));
            t.TickStyle = TickStyle.None; t.Width = W; t.Top = 22; t.BackColor = Theme.Panel;
            t.LargeChange = Math.Max(1, (max - min) / 20);
            l.Text = title;
            v.Text = fmt(t.Value);
            t.ValueChanged += delegate { onChange(t.Value); v.Text = fmt(t.Value); };
            p.Controls.Add(l); p.Controls.Add(v); p.Controls.Add(t);
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

            if (now - lastLiveMs >= 33) { lastLiveMs = now; UpdateLive(f); }

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

        private void UpdateLive(TelemetryFrame f)
        {
            if (live == null) return;
            live.Steer = steerOut; live.Throttle = thrOut; live.Brake = brkOut; live.Clutch = cluOut;
            live.BridgeOn = settings.BridgeOn;
            live.Spring = lastOut.SpringNm; live.Damper = lastOut.DamperNm; live.Friction = lastOut.FrictionNm;
            live.Bump = lastOut.BumpNm; live.Texture = lastOut.TextureNm; live.Engine = lastOut.EngineNm;
            live.Gain = lastOut.Gain; live.MaxNm = settings.MaxNm;
            live.Active = f != null && f.Active && telemetry.Connected;
            if (live.Active)
            {
                live.Speed = f.SpeedKmh; live.Mass = f.MassT;
                live.Vehicle = f.Vehicle ?? "-"; live.Surface = "piso " + f.Surface;
            }
            else live.Speed = 0;
            live.Invalidate();
        }

        private void UpdateMonitor(TelemetryFrame f, double age)
        {
            if (chipBase != null)
            {
                chipBase.Set(device.IsOpen, device.IsOpen ? "Base ligada" : "Base nao aberta");
                bool tel = telemetry.Connected && f != null;
                chipTel.Set(tel, tel ? string.Format(CultureInfo.InvariantCulture, "Telemetria {0:0} Hz", telemetryHz) : "Sem telemetria");
                chipBridge.Set(settings.BridgeOn && bridgeRate > 1, settings.BridgeOn ? string.Format(CultureInfo.InvariantCulture, "Ponte {0:0}/s", bridgeRate) : "Ponte desligada");
            }
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
