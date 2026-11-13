// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - janela principal: configuracao, monitor e ciclo de atualizacao.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FS25RealFFB
{
    internal sealed partial class MainForm : Form
    {
        [DllImport("winmm.dll")] private static extern int timeBeginPeriod(int ms);
        [DllImport("winmm.dll")] private static extern int timeEndPeriod(int ms);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
        private readonly ToolTip tips = new ToolTip();
        private int pinTicks;

        // Explicacao ao passar o rato (chave = titulo em ingles; o texto e traduzido em Lang.cs)
        private static readonly Dictionary<string, string> Help = new Dictionary<string, string> {
            { "Wheel torque (peak)", "Real peak torque of your wheel base (CSL DD 8 Nm, G29 2.2 Nm, MOZA R5 5.5 Nm). It is NOT the force you want: a higher value makes all forces WEAKER. For an identified model it cannot go above the manufacturer's value." },
            { "Force multiplier", "Raises or lowers ALL forces in proportion (centering, weight, terrain and engine). The quickest way to make everything lighter or stronger." },
            { "Maximum force", "Safety limit: the sum of the forces never goes above this value." },
            { "Engine start", "Starter motor and the moment the engine catches. Scaled by vehicle type: strong in tractors (engine bolted to the chassis), light in trucks and pickups (suspended cab). 0 = off; engine vibration is a separate slider." },
            { "Centering / self-aligning", "How strongly the wheel returns to centre while moving. Grows with speed; zero at a standstill." },
            { "Damping", "Resistance to fast wheel movements while driving. Higher = calmer, slower wheel." },
            { "Standstill weight (friction)", "Weight of the hydraulic steering when stopped or crawling. Fades as speed rises." },
            { "Reference speed", "Speed at which centering and damping reach full strength." },
            { "Suspension bumps", "Strength of bumps, potholes and impacts." },
            { "Terrain texture", "Vibration of the ground: tarmac, dirt, grass and field." },
            { "Engine vibration", "Engine vibration. Follows RPM and load; stronger at a standstill, softer while driving." },
            { "Steering sensitivity", "How much the tractor steers per wheel turn. 100% = full lock at the end of the wheel's rotation." },
        };

        private readonly Settings settings = Settings.Load();
        private readonly FfbEngine engine = new FfbEngine();          // fisica (telemetria -> ForceRequest)
        private readonly FfbMixer mixer = new FfbMixer();             // 0.13: mistura e limites universais
        private readonly OscillationGuard oscGuard = new OscillationGuard();
        private Watchdog watchdog;
        private IFfbAdapter adapter;                                  // 0.13: traducao para o volante
        private ForceRequest lastReq = new ForceRequest();
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
        private static readonly string[] PedalNames = { "Throttle", "Brake", "Clutch" };
        private long bridgeServedLast;
        private double bridgeRate;

        [STAThread]
        private static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { Crash(e.ExceptionObject as Exception); };
            // 0.14: "--list-wheels <ficheiro>" - usado pelo instalador e pelo diagnostico. A RealFFB.exe esta
            // autorizada no HidHide, por isso ve o volante mesmo escondido. So enumera (nao adquire nada).
            string[] cl = Environment.GetCommandLineArgs();
            for (int i = 1; i + 1 < cl.Length; i++)
                if (cl[i] == "--list-wheels")
                {
                    try
                    {
                        System.Text.StringBuilder sb = new System.Text.StringBuilder();
                        foreach (DeviceDescriptor d in DirectInputProvider.Enumerate(null))
                            sb.AppendLine(d.Vid.ToString("X4") + ":" + d.Pid.ToString("X4") + "|" + (d.HasForceFeedback ? "1" : "0") + "|" + (d.Product ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " "));
                        System.IO.File.WriteAllText(cl[i + 1], sb.ToString(), new System.Text.UTF8Encoding(false));
                    }
                    catch { }
                    return;
                }
            bool first;
            using (System.Threading.Mutex single = new System.Threading.Mutex(true, "FS25RealFFB_unica", out first))
            {
                bool unhide = false;
                foreach (string arg in Environment.GetCommandLineArgs()) if (arg == "--unhide") unhide = true;
                if (unhide)
                {
                    // Arranque do Windows (0.10.0): se a app nao esta aberta, o volante nao deve ficar escondido
                    // (fecho a forca, falta de luz ou encerramento do Windows com a app aberta).
                    if (first) { try { if (HidHideControl.Available) HidHideControl.Off(); } catch { } }
                    return;
                }
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
            try { MessageBox.Show(text, "FS25 RealFFB - error"); } catch { }
        }

        private const int W = 360;           // largura dos controlos do painel de definicoes
        private const int NavW = 190;        // largura do menu lateral
        private LiveView live;
        private TractorView tractor;
        private readonly ToolTip helpTip = new ToolTip();
        private int lastCollLogged = -1;
        private StatusChip chipBase, chipTel, chipBridge;
        private Panel leftPanel, rightPanel;
        private Button pinBtn;
        private Control chipsBox;
        private bool pinned, vehPinned;
        private Button pinVehBtn;
        private Panel headerPanel, accentPanel;
        private Label logTitleLbl, unpinBtn;
        private Form helpPop; private int helpClosedAt;
        private Rectangle normalBounds;
        private double lastLiveMs;
        private readonly System.Collections.Generic.Dictionary<string, NoWheelTrackBar> sliders = new System.Collections.Generic.Dictionary<string, NoWheelTrackBar>();
        private bool loadingProfile, profileDirty;
        private string profileVehicle = "";
        // 0.12: centragem realista
        private CheckBox realCenterBox;
        private ComboBox centerModeBox;
        private Label centerInfo;
        private Panel centerCard;
        private Label centerRealLbl, centerWhyLbl, centerNowLbl;
        private string centerNote = "";   // texto curto para o painel ao vivo
        private string centerVeh = null, centerKey = null;
        private SteerKind centerKind = SteerKind.Unknown;
        private bool fillingCenter;
        private double lastProfileSaveMs;
        private Label profileLabel;
        private CheckBox perVehBox;
        private ComboBox wheelBox;
        private Label wheelInfo;
        private bool fillingWheels;
        private bool hidHideMissing;

        public MainForm()
        {
            L.Set(settings.Language.Length > 0 ? settings.Language : (ReadState("Lang") ?? "en"));
            Text = "FS25 RealFFB  v" + AppInfo.Version;
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Width = 1320; Height = 800;
            MinimumSize = new Size(640, 400);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.UI(9.5f);

            // ---------------------------------------------------------- cabecalho
            Panel header = new Panel();
            header.Dock = DockStyle.Top; header.Height = 60; header.BackColor = Theme.Header;
            Panel accent = new Panel();
            accent.Dock = DockStyle.Top; accent.Height = 1; accent.BackColor = Theme.Line;

            PictureBox logo = new PictureBox();
            logo.Image = Theme.Logo(); logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.SetBounds(14, 10, 56, 40);
            header.Controls.Add(logo);

            Label title = new Label();
            title.Text = "FS25 RealFFB"; title.Font = Theme.Bold(12.5f); title.ForeColor = Theme.Text;
            title.AutoSize = true; title.Location = new Point(76, 10);
            header.Controls.Add(title);
            Label sub = new Label();
            sub.Text = "v" + AppInfo.Version + "  ·  OptimusZuka"; sub.Font = Theme.UI(8.5f); sub.ForeColor = Theme.Muted;
            sub.AutoSize = true; sub.Location = new Point(78, 34);
            header.Controls.Add(sub);

            FlowLayoutPanel chips = new FlowLayoutPanel();
            chips.FlowDirection = FlowDirection.LeftToRight; chips.WrapContents = false;
            chips.SetBounds(250, 18, 560, 26); chips.BackColor = Theme.Header;
            chipBase = new StatusChip(L.T("Wheel")); chipTel = new StatusChip(L.T("No telemetry")); chipBridge = new StatusChip(L.T("Bridge off"));
            chipBase.Margin = chipTel.Margin = chipBridge.Margin = new Padding(0, 0, 14, 0);
            chipBase.Height = chipTel.Height = chipBridge.Height = 24; chipBase.Width = 220; chipTel.Width = 160; chipBridge.Width = 150;
            chipBase.BackColor = chipTel.BackColor = chipBridge.BackColor = Theme.Header;
            chips.Controls.Add(chipBase); chips.Controls.Add(chipTel); chips.Controls.Add(chipBridge);
            header.Controls.Add(chips);
            chipsBox = chips;

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Right; actions.AutoSize = true; actions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false; actions.Padding = new Padding(0, 12, 14, 0); actions.BackColor = Theme.Header;

            Button stop = Theme.Btn(L.T("STOP (Esc)"), 110, 36, false);
            stop.BackColor = Theme.Stop; stop.FlatAppearance.BorderColor = Theme.Stop; stop.ForeColor = Color.White;
            stop.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 95, 100);
            stop.Margin = new Padding(8, 0, 0, 0);
            stop.Click += delegate { Panic("STOP button"); };

            enableBox = new CheckBox();
            enableBox.Appearance = Appearance.Button; enableBox.FlatStyle = FlatStyle.Flat;
            enableBox.Width = 120; enableBox.Height = 36; enableBox.TextAlign = ContentAlignment.MiddleCenter;
            enableBox.Font = Theme.Bold(10.5f); enableBox.Cursor = Cursors.Hand;
            enableBox.Margin = new Padding(8, 0, 0, 0);
            StyleToggle();
            enableBox.CheckedChanged += delegate
            {
                // 0.15: o motivo de o FFB estar desligado fica sempre registado e visivel no painel ao vivo
                if (enableBox.Checked) ffbOffReason = "";
                else if (!panicking) ffbOffReason = "turned off with the FFB button";
                StyleToggle();
                Log(enableBox.Checked ? "FFB on." : "FFB off (" + ffbOffReason + ").");
            };

            pinBtn = Theme.Btn(L.T("Pin on top"), 110, 36, false);
            pinBtn.Margin = new Padding(8, 0, 0, 0);
            pinBtn.Click += delegate { TogglePin(); };
            pinVehBtn = Theme.Btn(L.T("Pin vehicle"), 120, 36, false);
            pinVehBtn.Margin = new Padding(8, 0, 0, 0);
            pinVehBtn.Click += delegate { ToggleVehPin(); };

            Button play = Theme.Btn(L.T("Open FS25"), 100, 36, false);
            play.Margin = new Padding(8, 0, 0, 0);
            play.Click += delegate { LaunchGame(); };

            actions.Controls.Add(stop); actions.Controls.Add(enableBox); actions.Controls.Add(pinBtn); actions.Controls.Add(pinVehBtn); actions.Controls.Add(play);
            tips.AutoPopDelay = 15000; tips.InitialDelay = 400; tips.ReshowDelay = 100;
            tips.SetToolTip(stop, L.T("Stops all forces immediately."));
            tips.SetToolTip(enableBox, L.T("Turns force feedback on or off."));
            tips.SetToolTip(pinBtn, L.T("Compact window always on top of the game. FS25 must be in borderless or windowed mode: in exclusive fullscreen Windows draws the game above every window."));
            tips.SetToolTip(pinVehBtn, L.T("Small window always on top of the game with only the vehicle drawing and the force bars. Click the x in its corner to go back."));
            tips.SetToolTip(play, L.T("Starts Farming Simulator 25."));
            header.Controls.Add(actions);
            // 0.10.0: numa janela estreita os indicadores (chips) ficavam por cima dos botoes e tapavam o
            // "Abrir FS25" (relatado num G29). Os chips encolhem/escondem-se e os botoes ficam sempre por cima.
            header.Resize += delegate { FitHeader(actions); };
            actions.SizeChanged += delegate { FitHeader(actions); };

            // ---------------------------------------------------------- definicoes (coluna esquerda)
            // Layout "Cockpit": menu lateral + uma pagina de definicoes por secao + painel ao vivo.
            leftPanel = new Panel();
            leftPanel.Dock = DockStyle.Left; leftPanel.Width = NavW + W + 46; leftPanel.BackColor = Theme.Panel;
            Panel sep = new Panel(); sep.Dock = DockStyle.Left; sep.Width = 1; sep.BackColor = Theme.Line;

            FlowLayoutPanel nav = new FlowLayoutPanel();
            nav.Dock = DockStyle.Left; nav.Width = NavW; nav.FlowDirection = FlowDirection.TopDown; nav.WrapContents = false;
            nav.Padding = new Padding(10, 14, 10, 10); nav.BackColor = Theme.Nav;
            Panel navSep = new Panel(); navSep.Dock = DockStyle.Left; navSep.Width = 1; navSep.BackColor = Theme.Line;

            Panel pageHost = new Panel(); pageHost.Dock = DockStyle.Fill; pageHost.BackColor = Theme.Panel;
            string[] pageNames = { "Force feedback", "Steering and pedals", "Vehicle profile", "Buttons", "System", "Hardware" };
            FlowLayoutPanel[] pages = new FlowLayoutPanel[pageNames.Length];
            Button[] navBtns = new Button[pageNames.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                FlowLayoutPanel pg = new FlowLayoutPanel();
                pg.FlowDirection = FlowDirection.TopDown; pg.WrapContents = false;
                pg.Dock = DockStyle.Fill; pg.AutoScroll = true;
                pg.Padding = new Padding(18, 6, 10, 16); pg.BackColor = Theme.Panel;
                pg.Visible = i == 0;
                pages[i] = pg;
                Button nb = Theme.NavBtn(L.T(pageNames[i]), NavW - 20);
                int idx = i;
                nb.Click += delegate
                {
                    // clicar na secao aberta recolhe a coluna de definicoes (janela estreita sem perder o painel ao vivo)
                    if (pages[idx].Visible && pageHost.Visible)
                    {
                        pageHost.Visible = false; leftPanel.Width = NavW + 2;
                        Theme.NavActive(navBtns[idx], false);
                        return;
                    }
                    pageHost.Visible = true; leftPanel.Width = NavW + W + 46;
                    for (int k = 0; k < pages.Length; k++) { pages[k].Visible = k == idx; Theme.NavActive(navBtns[k], k == idx); }
                };
                navBtns[i] = nb;
                Theme.NavActive(nb, i == 0);
                nav.Controls.Add(nb);
            }

            updateBtn = Theme.Btn("", W, 36, true);
            updateBtn.Visible = false;
            updateBtn.Dock = DockStyle.Top;
            updateBtn.Click += delegate { InstallUpdate(); };

            // rodape fixo: guardar sempre a vista, em qualquer pagina
            FlowLayoutPanel foot = new FlowLayoutPanel();
            foot.Dock = DockStyle.Bottom; foot.Height = 58; foot.FlowDirection = FlowDirection.LeftToRight; foot.WrapContents = false;
            foot.Padding = new Padding(18, 10, 10, 10); foot.BackColor = Theme.Nav;

            FlowLayoutPanel flow = pages[2];
            flow.Controls.Add(Theme.Section(L.T("Vehicle profile"), W));
            perVehBox = Theme.Check(L.T("Separate settings for each vehicle"), settings.PerVehicle, W);
            perVehBox.CheckedChanged += delegate { TogglePerVehicle(); };
            flow.Controls.Add(perVehBox);
            tips.SetToolTip(perVehBox, L.T("Each tractor or machine keeps its own force settings, loaded automatically."));
            profileLabel = new Label(); profileLabel.Width = W; profileLabel.Height = 38; profileLabel.ForeColor = Theme.Muted;
            flow.Controls.Add(profileLabel);
            Button profDel = Theme.Btn(L.T("Delete this vehicle's profile (back to default)"), W, 30, false);
            profDel.Click += delegate { DeleteProfile(); };
            flow.Controls.Add(profDel);
            Button profDef = Theme.Btn(L.T("Set as default"), W, 30, false);
            profDef.Click += delegate { SetAsDefault(); };
            tips.SetToolTip(profDef, L.T("The current settings become the default, used by every vehicle that has no profile of its own."));
            flow.Controls.Add(profDef);
            Button profAll = Theme.Btn(L.T("Save for all vehicles"), W, 30, false);
            profAll.Click += delegate { ApplyToAllVehicles(); };
            tips.SetToolTip(profAll, L.T("The current settings are used by every vehicle. Existing vehicle profiles are moved to a backup folder, not deleted."));
            flow.Controls.Add(profAll);
            UpdateProfileLabel();

            flow = pages[0];
            // 0.12: centragem realista (primeira coisa da pagina inicial)
            flow.Controls.Add(Theme.Section(L.T("Realistic centering"), W));
            realCenterBox = Theme.Check(L.T("Realistic centering for vehicles"), settings.RealisticCentering, W);
            realCenterBox.CheckedChanged += delegate
            {
                settings.RealisticCentering = realCenterBox.Checked;
                try { settings.Save(); } catch { }
                centerKey = null;   // refazer o texto
                Log("Realistic centering: " + (settings.RealisticCentering ? "on" : "off"));
            };
            flow.Controls.Add(realCenterBox);
            tips.SetToolTip(realCenterBox, L.T("In real life only vehicles with mechanical steering (cars, pickups, trucks) bring the wheel back to the center. Tractors, combines, loaders, telehandlers and forklifts use hydrostatic steering: the wheel stays where you leave it. With this on, those vehicles get no centering spring."));
            // Cartao: "na vida real centra / nao centra" + porque + o que o volante faz agora
            centerCard = new Panel(); centerCard.Width = W; centerCard.Height = 124; centerCard.BackColor = Theme.Card;
            centerCard.Margin = new Padding(0, 6, 0, 6);
            Panel stripe = new Panel(); stripe.Dock = DockStyle.Left; stripe.Width = 4; stripe.BackColor = Theme.Line;
            centerCard.Controls.Add(stripe);
            centerRealLbl = new Label(); centerRealLbl.AutoSize = false; centerRealLbl.SetBounds(14, 8, W - 22, 24);
            centerRealLbl.Font = Theme.Bold(11f); centerRealLbl.ForeColor = Theme.Text; centerRealLbl.BackColor = Theme.Card;
            centerWhyLbl = new Label(); centerWhyLbl.AutoSize = false; centerWhyLbl.SetBounds(14, 34, W - 22, 60);
            centerWhyLbl.ForeColor = Theme.Soft; centerWhyLbl.BackColor = Theme.Card;
            centerNowLbl = new Label(); centerNowLbl.AutoSize = false; centerNowLbl.SetBounds(14, 96, W - 22, 22);
            centerNowLbl.Font = Theme.Bold(9.5f); centerNowLbl.ForeColor = Theme.Muted; centerNowLbl.BackColor = Theme.Card;
            centerCard.Controls.Add(centerRealLbl); centerCard.Controls.Add(centerWhyLbl); centerCard.Controls.Add(centerNowLbl);
            centerCard.Tag = stripe;
            centerRealLbl.Text = L.T("Get in a vehicle to see if it centers in real life.");
            flow.Controls.Add(centerCard);
            Label cmT = new Label(); cmT.Width = W; cmT.Height = 20; cmT.ForeColor = Theme.Soft; cmT.Text = L.T("Centering for this vehicle");
            flow.Controls.Add(cmT);
            centerModeBox = new ComboBox();
            centerModeBox.DropDownStyle = ComboBoxStyle.DropDownList; centerModeBox.Width = W;
            centerModeBox.FlatStyle = FlatStyle.Flat; centerModeBox.BackColor = Theme.Card; centerModeBox.ForeColor = Theme.Text;
            centerModeBox.Items.Add(L.T("Automatic (by vehicle type)"));
            centerModeBox.Items.Add(L.T("On: always centers"));
            centerModeBox.Items.Add(L.T("Off: never centers"));
            centerModeBox.SelectedIndex = 0;
            centerModeBox.Enabled = false;
            centerModeBox.SelectedIndexChanged += delegate
            {
                if (fillingCenter || string.IsNullOrEmpty(centerVeh)) return;
                try { CenteringOverrides.Set(centerVeh, centerModeBox.SelectedIndex); }
                catch (Exception ex) { Log("Centering: could not save (" + ex.Message + ")"); }
                centerKey = null;
                Log("Centering for " + centerVeh + ": " + (centerModeBox.SelectedIndex == 1 ? "always" : centerModeBox.SelectedIndex == 2 ? "never" : "automatic"));
            };
            flow.Controls.Add(centerModeBox);
            centerInfo = new Label(); centerInfo.Width = W; centerInfo.Height = 4;   // (texto passou para o cartao)

            flow.Controls.Add(Theme.Section(L.T("Wheel"), W));
            wheelBox = new ComboBox();
            wheelBox.DropDownStyle = ComboBoxStyle.DropDownList; wheelBox.Width = W;
            wheelBox.FlatStyle = FlatStyle.Flat; wheelBox.BackColor = Theme.Card; wheelBox.ForeColor = Theme.Text;
            wheelBox.Items.Add(L.T("Automatic (first wheel found)"));
            wheelBox.SelectedIndex = 0;
            flow.Controls.Add(wheelBox);
            wheelInfo = new Label(); wheelInfo.Width = W; wheelInfo.Height = 36; wheelInfo.ForeColor = Theme.Muted;
            wheelInfo.Text = L.T("Looking for a wheel...");
            flow.Controls.Add(wheelInfo);

            flow.Controls.Add(Theme.Section(L.T("Force"), W));
            flow.Controls.Add(Slider("Wheel torque (peak)", 10, (int)Math.Round(TorqueLimits.GenericCeilingNm * 10), (int)Math.Round(settings.BaseNm * 10),
                delegate(int v)
                {
                    // 0.15: o slider ja tem como maximo o pico do hardware; a validacao repete-se aqui
                    string why;
                    double ok = TorqueLimits.Validate(v / 10.0, settings.HwPeakNm, settings.HwModel, out why);
                    settings.BaseNm = ok; settings.TorqueSource = "user"; UpdateMaxSlider();
                    if (why.Length > 0) Log("Wheel torque (peak): " + (v / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + " Nm not accepted - " + why);
                }, delegate(int v) { return (v / 10.0).ToString("0.0") + " Nm"; }));
            flow.Controls.Add(Slider("Force multiplier", 25, 400, (int)Math.Round(settings.ForceScale * 100),
                delegate(int v) { settings.ForceScale = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Maximum force", 5, (int)(settings.BaseNm * 10), (int)(settings.MaxNm * 10),
                delegate(int v) { settings.MaxNm = v / 10.0; }, delegate(int v) { return (v / 10.0).ToString("0.0") + " Nm"; }));
            flow.Controls.Add(Slider("Centering / self-aligning", 0, 100, (int)(settings.Center * 100),
                delegate(int v) { settings.Center = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Damping", 0, 100, (int)(settings.Damping * 100),
                delegate(int v) { settings.Damping = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Standstill weight (friction)", 0, 100, (int)(settings.Friction * 100),
                delegate(int v) { settings.Friction = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Reference speed", 5, 80, (int)settings.RefSpeedKmh,
                delegate(int v) { settings.RefSpeedKmh = v; }, delegate(int v) { return v + " km/h"; }));

            flow.Controls.Add(Theme.Section(L.T("Terrain and engine"), W));
            flow.Controls.Add(Slider("Suspension bumps", 0, 100, (int)(settings.Bumps * 100),
                delegate(int v) { settings.Bumps = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Terrain texture", 0, 100, (int)(settings.Texture * 100),
                delegate(int v) { settings.Texture = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Engine vibration", 0, 100, (int)(settings.Engine * 100),
                delegate(int v) { settings.Engine = v / 100.0; }, Pct));
            flow.Controls.Add(Slider("Engine start", 0, 100, (int)Math.Round(settings.EngineStart * 100),
                delegate(int v) { settings.EngineStart = v / 100.0; try { settings.Save(); } catch { } }, Pct));
            invertBox = Theme.Check(L.T("Invert bump direction"), settings.InvertBumps, W);
            invertBox.CheckedChanged += delegate { settings.InvertBumps = invertBox.Checked; if (!loadingProfile) TuningChanged(); };
            flow.Controls.Add(invertBox);
            tips.SetToolTip(invertBox, L.T("Use it if bumps push the wheel the wrong way."));

            flow = pages[1];
            flow.Controls.Add(Theme.Section(L.T("Steering and pedals"), W));
            CheckBox bridgeBox = Theme.Check(L.T("Send wheel and pedals to FS25"), settings.BridgeOn, W);
            bridgeBox.CheckedChanged += delegate { settings.BridgeOn = bridgeBox.Checked; };
            flow.Controls.Add(bridgeBox);
            tips.SetToolTip(bridgeBox, L.T("Passes steering and pedals to FS25 through the mod. Needed while HidHide hides the wheel from the game."));
            flow.Controls.Add(Slider("Steering sensitivity", 50, 400, (int)(settings.SteerSens * 100),
                delegate(int v) { settings.SteerSens = v / 100.0; }, Pct));
            CheckBox steerInv = Theme.Check(L.T("Invert steering"), settings.SteerInvert, W);
            steerInv.CheckedChanged += delegate { settings.SteerInvert = steerInv.Checked; };
            flow.Controls.Add(steerInv);
            CheckBox followBox = Theme.Check(L.T("Wheel follows AI workers and GPS steering"), settings.FollowAuto, W);
            followBox.CheckedChanged += delegate { settings.FollowAuto = followBox.Checked; try { settings.Save(); } catch { } };
            flow.Controls.Add(followBox);
            tips.SetToolTip(followBox, L.T("When an AI worker or the game's GPS steering drives, the wheel turns by itself. Hold or turn the wheel firmly to take control: the AI worker is dismissed and the GPS switched off."));
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                FlowLayoutPanel row = new FlowLayoutPanel();
                row.FlowDirection = FlowDirection.LeftToRight; row.Width = W; row.Height = 38; row.WrapContents = false;
                row.Margin = new Padding(0);
                Button det = Theme.Btn(L.F("Detect {0}", L.T(PedalNames[i])), 170, 30, false);
                det.Click += delegate { StartDetect(idx); };
                Label lab = new Label(); lab.AutoSize = true; lab.Padding = new Padding(8, 10, 0, 0); lab.ForeColor = Theme.Muted;
                pedalLabels[i] = lab;
                row.Controls.Add(det); row.Controls.Add(lab);
                flow.Controls.Add(row);
            }
            UpdatePedalLabels();

            flow = pages[3];
            flow.Controls.Add(Theme.Section(L.T("Buttons -> keys (optional)"), W));
            Label hint = new Label();
            hint.Text = L.T("Only needed without HidHide. Add -> press the controller button -> press the FS25 key.");
            hint.Width = W; hint.Height = 36; hint.ForeColor = Theme.Muted;
            flow.Controls.Add(hint);
            CheckBox keysBox = Theme.Check(L.T("Enable button-to-key translation"), settings.KeysOn, W);
            keysBox.CheckedChanged += delegate { settings.KeysOn = keysBox.Checked; if (mapper != null && !keysBox.Checked) mapper.ReleaseAll(); try { settings.Save(); } catch { } };
            flow.Controls.Add(keysBox);
            FlowLayoutPanel brow = new FlowLayoutPanel();
            brow.FlowDirection = FlowDirection.LeftToRight; brow.Width = W; brow.Height = 38; brow.WrapContents = false; brow.Margin = new Padding(0);
            Button addB = Theme.Btn(L.T("Add button"), 176, 30, false);
            addB.Margin = new Padding(0, 3, 8, 3);
            addB.Click += delegate { if (mapper != null) mapper.StartLearn(); };
            Button delB = Theme.Btn(L.T("Remove selected"), 176, 30, false);
            delB.Click += delegate
            {
                if (mapper != null && mapList.SelectedItem != null) { mapper.Remove(mapList.SelectedItem as ButtonMapper.Mapping); RefreshMappings(); }
            };
            brow.Controls.Add(addB); brow.Controls.Add(delB);
            flow.Controls.Add(brow);
            Button impB = Theme.Btn(L.T("Import all from FS25 (inputBinding.xml)"), W, 30, false);
            impB.Click += delegate { if (mapper != null) { mapper.ImportFromGame(); RefreshMappings(); } };
            flow.Controls.Add(impB);
            mapList = new ListBox(); mapList.Width = W; mapList.Height = 120;
            mapList.BackColor = Theme.Card; mapList.ForeColor = Theme.Text; mapList.BorderStyle = BorderStyle.None;
            flow.Controls.Add(mapList);

            BuildHardwarePage(pages[5]);   // 0.13

            flow = pages[4];
            flow.Controls.Add(Theme.Section(L.T("System"), W));
            Label langLab = new Label(); langLab.Text = L.T("Language"); langLab.Width = W; langLab.Height = 20; langLab.ForeColor = Theme.Text;
            flow.Controls.Add(langLab);
            ComboBox langBox = new ComboBox();
            langBox.DropDownStyle = ComboBoxStyle.DropDownList; langBox.Width = W;
            langBox.FlatStyle = FlatStyle.Flat; langBox.BackColor = Theme.Card; langBox.ForeColor = Theme.Text;
            foreach (string n in L.Names) langBox.Items.Add(n);
            langBox.SelectedIndex = Math.Max(0, Array.IndexOf(L.Codes, L.Current));
            Label langInfo = new Label(); langInfo.Width = W; langInfo.Height = 22; langInfo.ForeColor = Theme.Yellow;
            langBox.SelectedIndexChanged += delegate
            {
                string code = L.Codes[Math.Max(0, langBox.SelectedIndex)];
                if (code == settings.Language || (settings.Language.Length == 0 && code == L.Current)) return;
                settings.Language = code;
                try { settings.Save(); } catch { }
                L.Set(code);
                langInfo.Text = L.T("Restart the app to apply the language.");
            };
            flow.Controls.Add(langBox);
            flow.Controls.Add(langInfo);
            CheckBox autoFfb = Theme.Check(L.T("Turn FFB on automatically when the app opens"), settings.AutoFfb, W);
            autoFfb.CheckedChanged += delegate { settings.AutoFfb = autoFfb.Checked; try { settings.Save(); } catch { } };
            flow.Controls.Add(autoFfb);
            CheckBox hideBox = Theme.Check(L.T("Manage HidHide automatically (wheel hidden only while the app is open)"), settings.AutoHide, W);
            hideBox.CheckedChanged += delegate
            {
                settings.AutoHide = hideBox.Checked;
                if (HidHideControl.Available)
                {
                    // 0.12.1: em segundo plano (o HidHideCLI pode levar ate 5 s, 15 s com as tentativas)
                    bool on = hideBox.Checked; string exe = Application.ExecutablePath;
                    System.Threading.Thread t = new System.Threading.Thread(delegate()
                    {
                        Log("HidHide: " + (on ? HidHideControl.On() : HidHideControl.Off()));
                        Log("HidHide: show the wheel at Windows start-up -> " + HidHideControl.SetStartupUnhide(on, exe));
                    });
                    t.IsBackground = true;
                    t.Start();
                }
                try { settings.Save(); } catch { }
            };
            flow.Controls.Add(hideBox);
            tips.SetToolTip(hideBox, L.T("Hides the wheel from FS25 while the app is open, so the game cannot block force feedback."));

            Button reset = Theme.Btn(L.T("Restore default settings"), (W - 10) / 2, 38, false);
            reset.Margin = new Padding(0, 0, 10, 0);
            reset.Click += delegate { RestoreDefaults(); };
            tips.SetToolTip(reset, L.T("Puts every force slider and the force multiplier back to the recommended values for your wheel. Vehicle profiles are moved to a backup folder, not deleted."));
            foot.Controls.Add(reset);

            Button save = Theme.Btn(L.T("Save settings"), (W - 10) / 2, 38, true);
            save.Margin = new Padding(0);
            save.Click += delegate
            {
                try { FlushProfile(); settings.Save(); Log("Settings saved."); }
                catch (Exception ex) { Log("Could not save: " + ex.Message); }
            };
            foot.Controls.Add(save);
            Button diag = Theme.Btn(L.T("Create diagnostic report"), W, 30, false);
            diag.Click += delegate { RunDiagnostics(); };
            flow.Controls.Add(diag);
            Button lnk = Theme.Btn(L.T("Create desktop shortcut"), W, 30, false);
            lnk.Click += delegate { CreateShortcut(); };
            flow.Controls.Add(lnk);

            for (int i = pages.Length - 1; i >= 0; i--) pageHost.Controls.Add(pages[i]);
            pageHost.Controls.Add(updateBtn);
            pageHost.Controls.Add(foot);
            leftPanel.Controls.Add(pageHost);
            leftPanel.Controls.Add(navSep);
            leftPanel.Controls.Add(nav);
            leftPanel.Controls.Add(sep);

            // ---------------------------------------------------------- ao vivo (coluna direita)
            live = new LiveView();
            live.Dock = DockStyle.Top; live.Height = 330;
            tractor = new TractorView();
            tractor.Dock = DockStyle.Top; tractor.Height = 260;
            // botao "?" no canto do desenho: explica cada cor (passar o rato ou clicar)
            Label helpBtn = new Label();
            helpBtn.Text = "?"; helpBtn.Font = Theme.Bold(10f); helpBtn.TextAlign = ContentAlignment.MiddleCenter;
            helpBtn.Size = new Size(22, 22); helpBtn.Location = new Point(8, 6);
            helpBtn.BackColor = Theme.Line; helpBtn.ForeColor = Theme.Yellow; helpBtn.Cursor = Cursors.Hand;
            string helpText = L.T("TRACTOR_HELP");
            if (helpText == "TRACTOR_HELP") helpText = "The vehicle is seen from above, front at the top. Each colour is one source of force in your wheel:\n\nBLUE - steering: the outline of the wheels that steer lights up with the centering force (how hard the wheel pulls back to the centre).\n\nORANGE - suspension: the bar beside each wheel is that wheel's shock absorber. The higher it fills, the harder that wheel was hit (holes, kerbs, ruts). You see exactly which wheel: front or rear, left or right.\n\nGREEN - ground: glow under the wheels touching the ground, grows with the surface texture (gravel, field, tarmac).\n\nRED - engine: engine vibration (RPM and load).\n\nIMPACT - a collision (tree, wall, another vehicle), shown at the point of the vehicle that was hit. Braking does not count.\n\nThe bars at the bottom show the current level of each source. The lines on the ground move with your speed.";
            helpTip.SetToolTip(helpBtn, L.T("How to read the drawing"));
            helpBtn.Click += (hbS, hbE) => ToggleHelp(helpBtn, helpText);
            tractor.Controls.Add(helpBtn);
            // "x" para sair do modo "Fixar veiculo" (so visivel nesse modo)
            unpinBtn = new Label();
            unpinBtn.Text = "\u00D7"; unpinBtn.Font = Theme.Bold(12f); unpinBtn.TextAlign = ContentAlignment.MiddleCenter;
            unpinBtn.Size = new Size(22, 22); unpinBtn.BackColor = Theme.Line; unpinBtn.ForeColor = Theme.Text; unpinBtn.Cursor = Cursors.Hand;
            unpinBtn.Visible = false;
            unpinBtn.Click += delegate { if (vehPinned) ToggleVehPin(); };
            helpTip.SetToolTip(unpinBtn, L.T("Unpin"));
            tractor.Controls.Add(unpinBtn);
            tractor.Resize += delegate { unpinBtn.Location = new Point(tractor.ClientSize.Width - 30, 6); };

            monitor = new Label();
            monitor.Dock = DockStyle.Top; monitor.Height = 150;
            monitor.Font = new Font("Consolas", 8.5f);
            monitor.Padding = new Padding(14, 8, 8, 8);
            monitor.BackColor = Theme.Bg; monitor.ForeColor = Theme.Muted; monitor.Padding = new Padding(18, 10, 10, 8);

            Label logTitle = Theme.Section(L.T("Log"), 300);
            logTitleLbl = logTitle;
            logTitle.Dock = DockStyle.Top; logTitle.Height = 26; logTitle.Padding = new Padding(12, 0, 0, 0);
            logTitle.BackColor = Theme.Bg;

            logBox = new TextBox();
            logBox.Multiline = true; logBox.ReadOnly = true; logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Top; logBox.Height = 180; logBox.Font = new Font("Consolas", 9f);
            logBox.BackColor = Theme.Bg; logBox.ForeColor = Theme.Muted;
            logBox.BorderStyle = BorderStyle.None;

            rightPanel = new Panel(); rightPanel.Dock = DockStyle.Fill; rightPanel.BackColor = Theme.Bg; rightPanel.AutoScroll = true;
            rightPanel.Controls.Add(logBox);
            rightPanel.Controls.Add(logTitle);
            rightPanel.Controls.Add(monitor);
            rightPanel.Controls.Add(live);
            rightPanel.Controls.Add(tractor);

            Controls.Add(rightPanel);
            Controls.Add(leftPanel);
            Controls.Add(accent);
            Controls.Add(header);
            headerPanel = header; accentPanel = accent;

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
                if (e.KeyCode == Keys.Escape) { e.Handled = true; e.SuppressKeyPress = true; Panic("Esc key"); }
            };
            Shown += delegate { Init(); };
            FormClosing += delegate { Shutdown(); };
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Log("ERROR: " + e.Exception.Message);
                Panic("exception");
            };
        }

        private void FitHeader(Control actions)
        {
            if (chipsBox == null) return;
            int room = actions.Left - chipsBox.Left - 12;
            chipsBox.Width = Math.Max(0, Math.Min(560, room));
            chipsBox.Visible = !pinned && room >= 170;
            actions.BringToFront();
        }

        private void StyleToggle()
        {
            bool on = enableBox.Checked;
            enableBox.Text = on ? L.T("FFB ON") : L.T("FFB OFF");
            enableBox.BackColor = on ? Theme.Yellow : Theme.Card;
            enableBox.ForeColor = on ? Color.Black : Theme.Text;
            enableBox.FlatAppearance.BorderColor = on ? Theme.Yellow : Theme.Line;
            enableBox.FlatAppearance.CheckedBackColor = Theme.Yellow;
        }

        // ------------------------------------------------------------------ perfis por veiculo
        private bool ProfileActive { get { return settings.PerVehicle && profileVehicle.Length > 0; } }

        private void TuningChanged()
        {
            if (ProfileActive) { profileDirty = true; UpdateProfileLabel(); }
            // fora de um veiculo os controlos editam o padrao: manter a copia do padrao em dia
            // (antes, o ficheiro gravava a copia antiga e a alteracao perdia-se)
            else if (settings.PerVehicle && settings.GlobalTuning != null) settings.GlobalTuning = settings.GetTuning();
        }

        // Os valores atuais passam a ser o padrao (veiculos sem perfil proprio usam-nos).
        private void SetAsDefault()
        {
            if (settings.PerVehicle) settings.GlobalTuning = settings.GetTuning();
            try { settings.Save(); } catch (Exception ex) { Log("Could not save: " + ex.Message); return; }
            UpdateProfileLabel();
            Log("Current settings saved as the default (vehicles without their own profile use them).");
        }

        // Os valores atuais passam a valer para TODOS os veiculos: padrao novo e perfis proprios
        // movidos para uma pasta de copia (nada e apagado).
        private void ApplyToAllVehicles()
        {
            if (MessageBox.Show(this, L.T("Use the current settings for every vehicle? Vehicle profiles are moved to a backup folder."),
                    "FS25 RealFFB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            double[] cur = settings.GetTuning();
            profileDirty = false;
            string backup;
            int moved = VehicleProfiles.MoveAllToBackup(out backup);
            if (settings.PerVehicle) settings.GlobalTuning = (double[])cur.Clone();
            try { settings.Save(); } catch (Exception ex) { Log("Could not save: " + ex.Message); }
            UpdateProfileLabel();
            Log("Current settings applied to every vehicle." + (moved > 0 ? " " + moved + " vehicle profile(s) moved to " + backup : ""));
        }

        private void FlushProfile()
        {
            if (!profileDirty || !ProfileActive) { profileDirty = false; return; }
            profileDirty = false;
            try { VehicleProfiles.Save(profileVehicle, settings.GetTuning()); }
            catch (Exception ex) { Log("Profile: could not save (" + ex.Message + ")"); }
            UpdateProfileLabel();
        }

        private void SwitchVehicle(string vehicle)
        {
            FlushProfile();
            profileVehicle = vehicle;
            if (!settings.PerVehicle) { UpdateProfileLabel(); return; }
            if (settings.GlobalTuning == null) settings.GlobalTuning = settings.GetTuning();
            bool own = VehicleProfiles.Exists(vehicle);
            double[] t = own ? VehicleProfiles.Load(vehicle, settings.GlobalTuning) : null;
            ApplyTuning(t ?? settings.GlobalTuning);
            Log("Profile: " + vehicle + (own && t != null ? " (own settings)" : " (default)"));
        }

        private void ApplyTuning(double[] t)
        {
            loadingProfile = true;
            try
            {
                settings.SetTuning(t);
                SetSlider("Maximum force", (int)Math.Round(settings.MaxNm * 10));
                SetSlider("Centering / self-aligning", (int)Math.Round(settings.Center * 100));
                SetSlider("Damping", (int)Math.Round(settings.Damping * 100));
                SetSlider("Standstill weight (friction)", (int)Math.Round(settings.Friction * 100));
                SetSlider("Reference speed", (int)Math.Round(settings.RefSpeedKmh));
                SetSlider("Suspension bumps", (int)Math.Round(settings.Bumps * 100));
                SetSlider("Terrain texture", (int)Math.Round(settings.Texture * 100));
                SetSlider("Engine vibration", (int)Math.Round(settings.Engine * 100));
                invertBox.Checked = settings.InvertBumps;
            }
            finally { loadingProfile = false; }
            UpdateProfileLabel();
        }

        // Torque de pico conforme o volante detetado (so quando muda de volante; depois e o utilizador que manda).
        private void ApplyDeviceTorque()
        {
            if (wheel == null || !wheel.IsOpen) return;
            string id = wheel.OpenVid.ToString("X4") + ":" + wheel.OpenPid.ToString("X4");
            WheelInfo info = WheelCatalog.Find(wheel.OpenVid, wheel.OpenPid, wheel.OpenProduct);
            settings.GearDriven = info.GearProfile;
            wheel.GearBoost = info.Drive == WheelDrive.Gear;   // reforco da mola so para engrenagens (G29 igual a antes)
            Log("Profile: " + (info.Model.Length > 0 ? info.Model : "unknown wheel") + " - "
                + (info.Drive == WheelDrive.Gear ? "gear-driven" : info.Drive == WheelDrive.Belt ? "belt-driven" : "direct drive")
                + (info.GearProfile ? " (profile with internal friction: standstill weight, smoother terrain, faster centering)" : " (direct drive profile)")
                + (info.Known ? "" : " - not validated on this wheel"));
            // 0.15: teto do hardware. Modelo identificado pelo catalogo (ID USB ou modelo exato no nome) ou confirmado
            // pelo utilizador para este volante. Sem nenhum dos dois: sem teto verificado (o pico fica "assumido").
            CatalogEntry ce = DeviceCatalog.Find(wheel.OpenVid, wheel.OpenPid, wheel.OpenProduct);
            if (ce.CeilingNm > 0) { settings.HwPeakNm = ce.CeilingNm; settings.HwModel = ce.Model; }
            else if (settings.UserModelDevice == id && settings.UserModelPeakNm > 0) { settings.HwPeakNm = settings.UserModelPeakNm; settings.HwModel = settings.UserModel + " (chosen by you)"; }
            else { settings.HwPeakNm = 0; settings.HwModel = ""; }
            SetSliderMax("Wheel torque (peak)", (int)Math.Round(TorqueLimits.Ceiling(settings.HwPeakNm) * 10));
            Log(settings.HwPeakNm > 0
                ? string.Format(CultureInfo.InvariantCulture, "Hardware limit: {0} = {1:0.0} Nm peak ({2}). The configured peak can never be higher.", settings.HwModel, settings.HwPeakNm,
                    ce.CeilingNm > 0 ? (ce.IdentifiedBy == "usb" ? "identified by USB ID" : "identified by device name") : "model chosen by you")
                : "Hardware limit: model not identified - no verified ceiling. Choose the model in Hardware > Calibrate force.");

            if (settings.BaseDevice == id)
            {
                // instalacoes anteriores a 0.6.1 nao tinham o multiplicador: valor inicial por marca
                if (settings.ForceScaleSaved == false) settings.ForceScale = 1.0;
                // 0.15: migracao / ficheiro de configuracao: um pico acima do hardware nunca e aceite, e a correcao
                // e explicada (antes, um MOZA R5 de 5,5 Nm ficava com 21 Nm e as forcas saiam a ~1/4).
                if (settings.HwPeakNm > 0 && settings.BaseNm > settings.HwPeakNm + 1e-9)
                    CorrectPeak(string.Format(CultureInfo.InvariantCulture, "the configured peak torque was {0:0.0} Nm, above the {1:0.0} Nm of the {2}", settings.BaseNm, settings.HwPeakNm, settings.HwModel));
                else if (settings.HwPeakNm > 0 && settings.TorqueSource == "assumed")
                    CorrectPeak(string.Format(CultureInfo.InvariantCulture, "the {0} is now identified ({1:0.0} Nm); the assumed value was replaced", settings.HwModel, settings.HwPeakNm));
                else
                {
                    string warn = TorqueLimits.BelowSpecWarning(settings.BaseNm, settings.HwPeakNm, settings.HwModel);
                    if (warn.Length > 0) Log("WARNING: " + warn);
                }
                return;
            }
            double nm = settings.HwPeakNm > 0 ? settings.HwPeakNm : (info.Known ? info.PeakNm : 0);
            // Desconhecido: assumir uma base forte. As forcas sao percentagem deste valor, por isso o motor
            // recebe pouco (antes 3 Nm assumidos = 100% do motor numa base de 15-25 Nm).
            settings.BaseNm = nm > 0 ? nm : WheelCatalog.UnknownNm;
            settings.BaseDevice = id;
            // Volantes Logitech (engrenagens/correia) tem atrito interno: forcas pequenas perdem-se.
            // Comecar com o dobro da forca; o utilizador ajusta em "Force multiplier". Fanatec: 100 %.
            settings.ForceScale = 1.0;
            SetSlider("Force multiplier", (int)Math.Round(settings.ForceScale * 100));
            SetSlider("Wheel torque (peak)", (int)Math.Round(settings.BaseNm * 10));
            UpdateMaxSlider();
            settings.TorqueSource = nm > 0 ? "catalog" : "assumed";
            Log(nm > 0 ? string.Format(CultureInfo.InvariantCulture, "Wheel {0}: peak torque {1:0.0} Nm (adjust 'Wheel torque' if yours differs).", id, nm)
                       : string.Format(CultureInfo.InvariantCulture, "WARNING: unknown wheel {0}: assuming {1:0} Nm so the forces start low. Set 'Wheel torque (peak)' to the manufacturer value of YOUR base.", id, WheelCatalog.UnknownNm));
            try { settings.Save(); } catch { }
        }

        // Lista de volantes com FFB ligados. A escolha fica guardada e vale a partir da proxima abertura.
        private void FillWheelList()
        {
            if (wheel == null) return;
            fillingWheels = true;
            wheelBox.Items.Clear();
            wheelBox.Items.Add(L.T("Automatic (first wheel found)"));
            int sel = 0;
            foreach (string[] a in wheel.Available)
            {
                wheelBox.Items.Add(a[1]);
                if (a[0] == settings.PreferredWheel && (settings.PreferredInstance.Length == 0 || a[2] == settings.PreferredInstance || sel == 0)) sel = wheelBox.Items.Count - 1;
            }
            if (settings.PreferredWheel.Length > 0 && sel == 0) wheelBox.Items.Add(L.F("{0} (chosen, not connected)", settings.PreferredWheel));
            wheelBox.SelectedIndex = settings.PreferredWheel.Length > 0 && sel == 0 ? wheelBox.Items.Count - 1 : sel;
            fillingWheels = false;
            wheelInfo.Text = wheel.IsOpen ? L.F("In use: {0}", wheel.DeviceName) : L.T("No wheel open.");
            wheelBox.SelectedIndexChanged -= WheelChosen;
            wheelBox.SelectedIndexChanged += WheelChosen;
        }

        private void WheelChosen(object sender, EventArgs e)
        {
            if (fillingWheels || wheel == null) return;
            int i = wheelBox.SelectedIndex;
            string id = i <= 0 ? "" : (i - 1 < wheel.Available.Count ? wheel.Available[i - 1][0] : settings.PreferredWheel);
            string inst = i <= 0 ? "" : (i - 1 < wheel.Available.Count ? wheel.Available[i - 1][2] : settings.PreferredInstance);
            if (id == settings.PreferredWheel && inst == settings.PreferredInstance) return;
            settings.PreferredWheel = id;
            settings.PreferredInstance = inst;
            try { settings.Save(); } catch { }
            wheelInfo.Text = L.T("Choice saved. Close and reopen RealFFB to switch wheels.");
            wheelInfo.ForeColor = Theme.Yellow;
            Log("Wheel chosen: " + (id.Length == 0 ? "automatic" : id) + " (applies next time the app opens).");
        }

        private void UpdateMaxSlider()
        {
            NoWheelTrackBar t;
            if (!sliders.TryGetValue("Maximum force", out t)) return;
            int max = Math.Max(6, (int)Math.Round(settings.PeakNm * 10));
            if (settings.MaxNm > settings.PeakNm) settings.MaxNm = settings.PeakNm;
            t.Maximum = max;
            if (t.Value > max) t.Value = max;
        }

        // 0.15: pico configurado corrigido para o pico do hardware, com explicacao (nunca em silencio)
        private void CorrectPeak(string why)
        {
            double old = settings.BaseNm;
            settings.BaseNm = settings.HwPeakNm;
            settings.TorqueSource = "catalog";
            SetSlider("Wheel torque (peak)", (int)Math.Round(settings.BaseNm * 10));
            settings.TorqueSource = "catalog";
            UpdateMaxSlider();
            try { settings.Save(); } catch { }
            Log(string.Format(CultureInfo.InvariantCulture, "Peak torque corrected: {0:0.0} -> {1:0.0} Nm ({2}).", old, settings.BaseNm, why));
            BeginInvoke(new Action(delegate
            {
                MessageBox.Show(this, L.F("Wheel torque (peak) was changed from {0} Nm to {1} Nm.\n\nReason: {2}.\n\nThis value must match the wheel base so the forces come out right. To make the forces stronger or lighter use 'Force multiplier' or 'Maximum force'.",
                    old.ToString("0.0"), settings.BaseNm.ToString("0.0"), why), "FS25 RealFFB", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));
        }

        private void SetSliderMax(string title, int max)
        {
            NoWheelTrackBar t;
            if (!sliders.TryGetValue(title, out t)) return;
            if (t.Value > max) t.Value = max;
            t.Maximum = max;
        }

        private void SetSlider(string title, int value)
        {
            NoWheelTrackBar t;
            if (!sliders.TryGetValue(title, out t)) return;
            t.Value = Math.Max(t.Minimum, Math.Min(t.Maximum, value));
        }

        private void TogglePerVehicle()
        {
            FlushProfile();
            settings.PerVehicle = perVehBox.Checked;
            if (!settings.PerVehicle)
            {
                if (settings.GlobalTuning != null) { double[] g = settings.GlobalTuning; settings.GlobalTuning = null; ApplyTuning(g); }
            }
            else if (profileVehicle.Length > 0)
            {
                string v = profileVehicle; profileVehicle = ""; SwitchVehicle(v);
            }
            try { settings.Save(); } catch { }
            UpdateProfileLabel();
        }

        // Repor as definicoes de forca recomendadas (volante, pedais, botoes e idioma ficam como estao).
        private void RestoreDefaults()
        {
            if (MessageBox.Show(this, L.T("Restore all force settings to the recommended values? Vehicle profiles are moved to a backup folder."),
                    "FS25 RealFFB", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            profileDirty = false;
            string backup;
            int moved = VehicleProfiles.MoveAllToBackup(out backup);
            Settings def = new Settings();
            def.BaseNm = settings.BaseNm;
            double[] t = def.GetTuning();
            settings.GlobalTuning = (double[])t.Clone();
            ApplyTuning(t);
            settings.ForceScale = 1.0;
            loadingProfile = true;
            try { SetSlider("Force multiplier", 100); } finally { loadingProfile = false; }
            UpdateMaxSlider();
            try { settings.Save(); } catch { }
            UpdateProfileLabel();
            Log("Default settings restored." + (moved > 0 ? " " + moved + " vehicle profile(s) moved to " + backup : ""));
        }

        private void DeleteProfile()
        {
            if (!ProfileActive) { Log("Profile: no active vehicle."); return; }
            profileDirty = false;
            VehicleProfiles.Delete(profileVehicle);
            ApplyTuning(settings.GlobalTuning ?? settings.GetTuning());
            Log("Profile of " + profileVehicle + " deleted: using the default settings.");
        }

        // ------------------------------------------------------------------ 0.12: centragem realista
        // 1 = o volante centra; 0 = nao centra. Por veiculo: automatico (tipo do veiculo + opcao geral),
        // sempre ou nunca.
        private double CenteringNow(TelemetryFrame f, double age)
        {
            bool inVeh = f != null && f.Active && age < 300 && !string.IsNullOrEmpty(f.Vehicle);
            string veh = inVeh ? f.Vehicle : "";
            if (inVeh && veh != centerVeh)
            {
                centerKind = CenteringRules.Classify(f.VehicleType, f.Category, f.Articulated, f.Crawler);
                Log("Centering: " + veh + " [" + f.VehicleType + "/" + f.Category + (f.Articulated ? "/articulated" : "") + (f.Crawler ? "/tracks" : "") + "] -> " + centerKind);
            }
            if (inVeh || centerVeh == null) centerVeh = veh;   // ao sair do veiculo mantem-se o ultimo (pausa, menus)
            int mode = CenteringOverrides.Get(centerVeh);
            bool centers = mode == 1 || (mode == 0 && (!settings.RealisticCentering || CenteringRules.CentersInRealLife(centerKind)));
            string key = centerVeh + "|" + mode + "|" + centerKind + "|" + settings.RealisticCentering + "|" + L.Current;
            if (key != centerKey && centerCard != null)
            {
                centerKey = key;
                fillingCenter = true;
                try { centerModeBox.SelectedIndex = mode; } finally { fillingCenter = false; }
                centerModeBox.Enabled = centerVeh.Length > 0;
                Panel stripe = centerCard.Tag as Panel;
                if (centerVeh.Length == 0)
                {
                    centerRealLbl.Text = L.T("Get in a vehicle to see if it centers in real life.");
                    centerRealLbl.ForeColor = Theme.Muted;
                    centerWhyLbl.Text = L.T("Cars, pickups and trucks center. Tractors, combines, loaders and other machines with hydrostatic steering do not.");
                    centerNowLbl.Text = "";
                    if (stripe != null) stripe.BackColor = Theme.Line;
                    centerNote = "";
                }
                else
                {
                    bool real = CenteringRules.CentersInRealLife(centerKind);
                    bool unknown = centerKind == SteerKind.Unknown;
                    centerRealLbl.Text = unknown ? L.F("{0}: type not recognised", centerVeh)
                        : (real ? "\u2714  " + L.F("{0}: CENTERS in real life", centerVeh) : "\u2716  " + L.F("{0}: does NOT center in real life", centerVeh));
                    centerRealLbl.ForeColor = unknown ? Theme.Muted : (real ? Theme.Good : Theme.Yellow);
                    centerWhyLbl.Text = CenteringRules.KindDetail(centerKind);
                    string now;
                    if (mode == 1) now = L.T("Your wheel now: WITH centering (you chose: always)");
                    else if (mode == 2) now = L.T("Your wheel now: NO centering (you chose: never)");
                    else if (!settings.RealisticCentering) now = L.T("Your wheel now: WITH centering (realistic centering is off)");
                    else now = centers ? L.T("Your wheel now: WITH centering, like the real one") : L.T("Your wheel now: NO centering, like the real one");
                    centerNowLbl.Text = now;
                    centerNowLbl.ForeColor = centers ? Theme.Text : Theme.Yellow;
                    if (stripe != null) stripe.BackColor = unknown ? Theme.Line : (real ? Theme.Good : Theme.Yellow);
                    centerNote = centers ? "" : L.T("OFF: no centering");
                }
            }
            return centers ? 1.0 : 0.0;
        }

        private void UpdateProfileLabel()
        {
            if (profileLabel == null) return;
            if (!settings.PerVehicle) profileLabel.Text = L.T("Off: the same settings for every vehicle.");
            else if (profileVehicle.Length == 0) profileLabel.Text = L.F("No vehicle in game: changes edit the default. {0} profile(s) saved.", VehicleProfiles.Count());
            else if (VehicleProfiles.Exists(profileVehicle) || profileDirty) profileLabel.Text = L.F("{0}: own settings (saved automatically).", profileVehicle);
            else profileLabel.Text = L.F("{0}: uses the default. Move a control to create this vehicle's profile.", profileVehicle);
            profileLabel.ForeColor = ProfileActive ? Theme.Yellow : Theme.Muted;
        }

        private delegate void IntAction(int v);
        private delegate string IntFormat(int v);

        private static string Pct(int v) { return v + " %"; }

        private Control Slider(string title, int min, int max, int value, IntAction onChange, IntFormat fmt)
        {
            Panel p = new Panel(); p.Width = W; p.Height = 56; p.Margin = new Padding(0, 2, 0, 2);
            Label l = new Label(); l.AutoSize = false; l.Width = W - 90; l.Height = 20; l.Top = 2; l.ForeColor = Theme.Soft;
            Label v = new Label(); v.AutoSize = false; v.Width = 90; v.Height = 20; v.Left = W - 90; v.Top = 2;
            v.TextAlign = ContentAlignment.TopRight; v.ForeColor = Theme.Text; v.Font = Theme.Mono(9.5f);
            NoWheelTrackBar t = new NoWheelTrackBar();
            t.Minimum = min; t.Maximum = max; t.Value = Math.Max(min, Math.Min(max, value));
            t.TickStyle = TickStyle.None; t.Width = W; t.Top = 22; t.BackColor = Theme.Panel;
            t.LargeChange = Math.Max(1, (max - min) / 20);
            l.Text = L.T(title);
            v.Text = fmt(t.Value);
            t.ValueChanged += delegate { onChange(t.Value); v.Text = fmt(t.Value); if (!loadingProfile) TuningChanged(); };
            sliders[title] = t;
            string help;
            if (Help.TryGetValue(title, out help)) { help = L.T(help); tips.SetToolTip(p, help); tips.SetToolTip(l, help); tips.SetToolTip(v, help); tips.SetToolTip(t, help); }
            p.Controls.Add(l); p.Controls.Add(v); p.Controls.Add(t);
            return p;
        }

        // ------------------------------------------------------------------ log
        private int logLinesSinceCheck = 200;
        private void Log(string s)
        {
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(Log), s); } catch { }
                return;
            }
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + s;
            // 0.12.1: a caixa do registo crescia sem limite (horas de jogo = MBs de texto e AppendText cada vez
            // mais lento no ciclo do FFB). Fica com as ultimas ~60 000 letras.
            if (logBox.TextLength > 120000)
            {
                string keep = logBox.Text.Substring(logBox.TextLength - 60000);
                int nl = keep.IndexOf('\n');
                logBox.Text = nl >= 0 ? keep.Substring(nl + 1) : keep;
            }
            logBox.AppendText(line + Environment.NewLine);
            lock (logLock)
            {
                try
                {
                    // e o ficheiro tambem: passa a RealFFB_log.old.txt aos 5 MB (antes crescia para sempre)
                    if (++logLinesSinceCheck >= 200)
                    {
                        logLinesSinceCheck = 0;
                        FileInfo fi = new FileInfo(logPath);
                        if (fi.Exists && fi.Length > 5L * 1024 * 1024)
                        {
                            string old = AppInfo.DataFile("RealFFB_log.old.txt");
                            if (File.Exists(old)) File.Delete(old);
                            File.Move(logPath, old);
                        }
                    }
                    File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
                }
                catch { }
            }
        }

        // ------------------------------------------------------------------ ciclo
        private void Init()
        {
            Log("==================== FS25 RealFFB v" + AppInfo.Version + " - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " ====================");
            timeBeginPeriod(1);
            Log("Windows " + Environment.OSVersion.Version + (Environment.Is64BitOperatingSystem ? " x64" : " x86") + " | language " + L.Current);
            hidHideMissing = !HidHideControl.Available;
            if (hidHideMissing)
                Log("WARNING: HidHide is not installed. FS25 will take control of the wheel, so only the game's (or G HUB's) own centering is felt. Run the FS25 RealFFB installer again to install HidHide.");
            else if (settings.AutoHide)
            {
                Log("HidHide: wheel hidden from FS25 -> " + HidHideControl.On());
                // rede de seguranca: no proximo arranque do Windows o volante volta a ficar visivel, mesmo que a
                // app nao tenha fechado bem (o log de 2026-11-08 mostrou o "desligar" a falhar no encerramento)
                Log("HidHide: show the wheel at Windows start-up -> " + HidHideControl.SetStartupUnhide(true, Application.ExecutablePath));
                Microsoft.Win32.SystemEvents.SessionEnding += delegate { ReleaseHidHide("Windows is shutting down"); };
            }
            else Log("HidHide: installed, automatic management off (the wheel is hidden only if set up by hand).");
            device = OpenOutput();
            ApplyDeviceTorque();
            // 0.13: watchdog - se este ciclo parar mais de 150 ms com o FFB ligado, as forcas param
            watchdog = new Watchdog(delegate
            {
                if (device != null) device.EmergencyStop();
                int n = watchdog != null ? watchdog.Trips : 0;
                if (n <= 5 || n % 50 == 0)
                    Log("SAFETY: the FFB loop stopped for more than 150 ms (window busy?) - forces stopped by the watchdog. They come back on the next cycle." + (n > 1 ? " (" + n + "x)" : ""));
            });
            watchdog.Start();
            UpdateHardwareInfo();
            RefreshDeviceList();
            FillWheelList();
            if (hidHideMissing && wheelInfo != null)
            {
                wheelInfo.Text = L.T("HidHide is not installed: FS25 will take the wheel. Run the installer again.");
                wheelInfo.ForeColor = Theme.Bad;
            }
            if (!device.IsOpen) Log("The wheel did not open. Close games/programs using it and restart the app.");

            for (int q = 1; q < 3; q++)
                if (settings.PedalAxis[q] >= 0 && settings.PedalAxis[q] == settings.PedalAxis[0])
                {
                    Log("WARNING: " + PedalNames[q] + " was on the same axis as the throttle. Detect it again.");
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
            Log("Waiting for FS25 (mod 'Real FFB Telemetry' active in the savegame).");

            lastTickMs = clock.Elapsed.TotalMilliseconds;
            hzStartMs = lastTickMs;
            loopTimer = new Timer();
            loopTimer.Interval = 5;
            loopTimer.Tick += delegate { Tick(); };
            loopTimer.Start();

            if (settings.AutoFfb && device.IsOpen) enableBox.Checked = true;
            // (o atalho com o icone do FS25 e criado pelo instalador; o botao continua disponivel)
            updater = new Updater(Log);
            updater.CheckAsync(delegate(Updater.Release r)
            {
                // 0.12.1: se a janela ja fechou, BeginInvoke lancava uma excecao nesta thread -> janela de erro
                try { if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action<Updater.Release>(ShowUpdate), r); } catch { }
            });
            foreach (string a in Environment.GetCommandLineArgs())
                if (a == "--jogar") LaunchGame();
        }

        // Atalho "FS25 com FFB" com o icone do proprio jogo (lido do executavel instalado).
        private void CreateShortcut()
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string link = Path.Combine(desktop, "FS25 RealFFB.lnk");
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
                st.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { "FS25 with force feedback (RealFFB)" });
                st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(sc);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
                Log("Shortcut created: " + link);
            }
            catch (Exception ex) { Log("Could not create the shortcut: " + ex.Message); }
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
            updateBtn.Text = L.F("New version available: v{0}  (install)", r.Version);
            updateBtn.Visible = true;
            Log("Updates: new version v" + r.Version + ". Notes: " + r.PageUrl);
        }

        private void InstallUpdate()
        {
            if (pendingUpdate == null) return;
            DialogResult d = MessageBox.Show(this,
                L.F("Install version v{0}?\n\nYour settings, pedals and buttons are kept.\nRelease notes: {1}", pendingUpdate.Version, pendingUpdate.PageUrl),
                L.T("Update FS25 RealFFB"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (d != DialogResult.OK) return;
            // 0.12.1: o download corre neste ciclo e a app fica parada uns segundos: antes as forcas ficavam
            // presas no ultimo valor durante esse tempo. Agora param primeiro.
            Panic("installing update");
            try { if (updater.DownloadAndRun(pendingUpdate)) Close(); }
            catch (Exception ex) { Log("Update failed: " + ex.Message); }
        }

        private void RunDiagnostics()
        {
            try
            {
                if (!File.Exists(AppInfo.SetupHelper)) { Log("Diagnostics: RealFFB.Setup.exe not found (install with the installer)."); return; }
                string report = AppInfo.DataFile("diagnostico.txt");
                ProcessStartInfo psi = new ProcessStartInfo(AppInfo.SetupHelper, "diagnose --quiet --report \"" + report + "\"");
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                Log("Diagnostics: creating the report...");
                // 0.12.1: em segundo plano. Antes a app esperava ate 30 s aqui, com o FFB parado no ultimo valor.
                System.Threading.Thread t = new System.Threading.Thread(delegate()
                {
                    try
                    {
                        using (Process p = Process.Start(psi)) p.WaitForExit(30000);
                        Log("Diagnostic report saved to " + report + " (send this file when asking for help).");
                        Process.Start("notepad.exe", "\"" + report + "\"");
                    }
                    catch (Exception ex) { Log("Diagnostics failed: " + ex.Message); }
                });
                t.IsBackground = true;
                t.Start();
            }
            catch (Exception ex) { Log("Diagnostics failed: " + ex.Message); }
        }

        private void LaunchGame()
        {
            try { Process.Start("steam://rungameid/2300320"); Log("Opening FS25 through Steam..."); }
            catch (Exception ex) { Log("Could not open FS25: " + ex.Message); }
        }

        // Preferencia: Windows.Gaming.Input (nao disputa a base com o FS25).
        // Recurso: DirectInput (funciona so se nenhum jogo prender a base).
        // v0.17: a app controla a base diretamente (DirectInput); o FS25 nao toca na base.
        private IWheelOutput OpenOutput()
        {
            FfbDevice d = new FfbDevice(Log);
            d.Preferred = settings.PreferredWheel;
            d.PreferredInstance = settings.PreferredInstance;
            d.Open(Handle);
            wheel = d;
            if (d.IsOpen) Log("Force output: DirectInput (FS25 must not be using the wheel).");
            adapter = ChooseAdapter(d);
            return d;
        }

        // 0.13: escolha do adaptador. "auto": efeitos de condicao se a base os tiver a funcionar (o modo validado);
        // se faltarem, binario calculado (experimental). O utilizador pode forcar um dos dois na pagina Hardware.
        private IFfbAdapter ChooseAdapter(FfbDevice d)
        {
            if (d == null || !d.IsOpen) return null;
            string why;
            bool torque;
            if (settings.OutputMode == "torque") { torque = true; why = "chosen in the Hardware page"; }
            else if (settings.OutputMode == "conditions") { torque = false; why = "chosen in the Hardware page"; }
            else if (d.Caps.ConditionsUsable) { torque = false; why = "the wheel has working spring and damper effects"; }
            else { torque = true; why = "spring/damper effects are missing or not playing on this wheel"; }
            IFfbAdapter a = torque ? (IFfbAdapter)new DiTorqueAdapter(d) : new DiConditionAdapter(d);
            Log("FFB adapter: " + a.Name + " v" + a.Version + " (" + why + ")");
            return a;
        }

        private int lastAuto; private bool lastFollowing;   // 0.11: registo de seguir IA/GPS

        private void Tick()
        {
            double now = clock.Elapsed.TotalMilliseconds;
            double dt = (now - lastTickMs) / 1000.0;
            lastTickMs = now;
            if (dt <= 0 || dt > 0.2) dt = 0.016;
            if ((pinned || vehPinned) && ++pinTicks >= 100) { pinTicks = 0; KeepOnTop(); }   // ~0,5 s

            UpdateInputs(now);
            if (mapper != null)
            {
                if (settings.KeysOn) mapper.Poll(wheel != null && wheel.IsOpen ? wheel.Buttons : null);
                if (mapper.Mappings.Count != mapCount) RefreshMappings();
            }

            double age;
            TelemetryFrame f = telemetry.GetLatest(out age);
            engine.SentSteer = steerOut;
            engine.BrakeInput = brkOut;
            engine.WheelPos = (wheel != null && wheel.IsOpen) ? wheel.Axes[0] / 10000.0 : double.NaN;
            engine.CenterFactor = CenteringNow(f, age);
            ForceRequest req = engine.Update(f, age, enableBox.Checked && device.IsOpen, dt, settings);
            if (!enableBox.Checked) req.State = "FFB off - " + ffbOffReason;
            else if (!device.IsOpen) req.State = "FFB on, but no wheel is open";
            LogTransitions(f, age, req);
            req = mixer.Finish(req, settings, dt);
            oscGuard.Apply(req, engine.WheelPos, dt);
            if (oscGuard.Trips != lastOscTrips) { lastOscTrips = oscGuard.Trips; Log("SAFETY: wheel oscillation detected - centering halved and damping raised for 2 s."); }
            lastReq = req;
            int autoNow = (f != null && f.Active && age < 300) ? f.Auto : 0;
            if (autoNow != lastAuto || engine.Following != lastFollowing)
            {
                Log(string.Format("Auto steering: {0} | wheel follows: {1}{2}",
                    autoNow == 1 ? "AI worker" : (autoNow == 2 ? "GPS" : "off"), engine.Following ? "yes" : "no",
                    autoNow != 0 && !engine.Following ? (settings.FollowAuto ? " (driver took over: asking the game to stop the AI/GPS)" : " (option off)") : ""));
                lastAuto = autoNow; lastFollowing = engine.Following;
            }

            // 0.12.1: com o FFB desligado deixa de se enviar (depois de um ultimo frame a zero). Antes o ciclo
            // reiniciava os efeitos logo a seguir ao STOP (SetSpring -> StartAll).
            bool sendFfb = enableBox.Checked || req.Gain > 0 || lastSentGain > 0;
            lastSentGain = req.Gain;
            FfbOutput o;
            if (device.IsOpen && sendFfb && adapter != null) o = adapter.Apply(req, engine.WheelPos, dt, settings);
            else { o = new FfbOutput(); o.CopyInfo(req); }
            lastOut = o;
            if (watchdog != null) watchdog.Beat(enableBox.Checked && device.IsOpen);
            Trace(f, o);

            if (now - lastLiveMs >= 33) { lastLiveMs = now; UpdateLive(f); }
            if (f != null && f.Active && telemetry.Connected && !string.IsNullOrEmpty(f.Vehicle) && f.Vehicle != profileVehicle) SwitchVehicle(f.Vehicle);
            if (profileDirty && now - lastProfileSaveMs > 1000) { lastProfileSaveMs = now; FlushProfile(); }

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
                if (hwInfo != null && hwInfo.Visible) UpdateHardwareInfo();
                string st = device.DeviceName;
                if (st != lastStatus) { lastStatus = st; Log("Status: " + st); }
            }

            if (now >= nextStatsLogMs && telemetry.Connected && f != null && f.Active)
            {
                nextStatsLogMs = now + 5000;
                Log(string.Format(CultureInfo.InvariantCulture,
                    "[stats] ffb {25} | ses {26} | engine {27} mst {28} {29:0.00} Nm {30:0} Hz | {20} | bridge {16:0}/s steer {17:+0.00;-0.00} thr {18:0.00} brk {19:0.00} | {0:0} Hz | {1:0.0} km/h | {2:0.0} t | surface {3} | wheels {4}/{5} | steer {6:0.00} | vAcc {7:0.0} | rollAcc {8:0.0} | susp {9:0.00} || spring {10:0.00} Nm | damper {11:0.00} | friction {12:0.00} | bump {13:+0.00;-0.00} | texture {14:0.00} | gain {15:0%} | springK {21} dampK {22} off {23} pos {24:+0.000;-0.000}",
                    telemetryHz, f.SpeedKmh, f.MassT, f.Surface, f.Contacts, f.Wheels, f.Steer, f.VertAcc, f.RollAcc, f.SuspSpreadRate,
                    o.SpringNm, o.DamperNm, o.FrictionNm, o.BumpNm, o.TextureNm, o.Gain, bridgeRate, steerOut, thrOut, brkOut,
                    wheel != null ? wheel.Health : "no wheel", o.SpringCoef, o.DamperCoef, o.SpringOffset,
                    (wheel != null && wheel.IsOpen) ? wheel.Axes[0] / 10000.0 : 0.0,
                    enableBox.Checked ? "on" : "OFF (" + ffbOffReason + ")", f.Session.Length > 0 ? f.Session : "?",
                    engine.EngineType.Name, f.MotorState, o.EngineNm, lastReq != null ? lastReq.EngineHz : 0));
            }
        }

        // 0.15: regista as transicoes que explicam o comportamento do FFB: telemetria perdida/recuperada, entrar e
        // sair de um veiculo, tipo de sessao e estado do motor (com as forcas do motor nesse instante).
        private void LogTransitions(TelemetryFrame f, double age, ForceRequest req)
        {
            bool telOk = f != null && age < 300;
            if (telOk != logTelOk)
            {
                logTelOk = telOk;
                Log(telOk ? "Telemetry: receiving." : string.Format(CultureInfo.InvariantCulture, "Telemetry: lost (last frame {0:0} ms ago) - forces fade out.", age));
            }
            if (!telOk) return;
            string veh = f.Active ? (f.Vehicle ?? "") : "";
            if (veh != logVeh)
            {
                Log(veh.Length > 0 ? string.Format("Vehicle: entered {0} (category {1}, engine profile {2})", veh, f.Category, EngineProfile.For(f.Category).Name)
                                   : "Vehicle: left the vehicle or game paused - forces fade out.");
                logVeh = veh; logMst = -99;
            }
            if (f.Session != logSes)
            {
                logSes = f.Session;
                if (f.MpClient) Log("Session: multiplayer client (dedicated server or someone else's game). Wheel contact and surface are not available here: centering uses neutral grip, terrain texture and speed-based impacts are off.");
                else if (f.Session.Length > 0) Log("Session: " + (f.Session == "host" ? "multiplayer host" : "single player") + ".");
            }
            if (f.Active && f.MotorState != logMst)
            {
                Log(string.Format(CultureInfo.InvariantCulture, "Engine: state {0} -> {1} ({2}) | rpm {3:0} | engine force {4:0.00} Nm at {5:0} Hz",
                    logMst == -99 ? "?" : logMst.ToString(), f.MotorState,
                    f.MotorState == 2 ? "starting" : f.MotorState == 3 ? "running" : f.MotorState == 1 ? "ignition" : f.MotorState == 0 ? "off" : "unknown",
                    f.Rpm, req.EngineNm, req.EngineHz));
                logMst = f.MotorState;
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
            // Todos os eixos dos pedais exatamente a 0 = pedais desligados da base: nunca enviar meio pedal ao jogo
            bool pedalsDead = true;
            for (int k = 1; k < FfbDevice.AxisCount; k++) if (wheel.Axes[k] != 0) pedalsDead = false;
            if (pedalsDead) { thrOut = brkOut = cluOut = 0; }
            else { thrOut = Pedal(0); brkOut = Pedal(1); cluOut = Pedal(2); }
            bridge.Publish(settings.BridgeOn, steerOut, thrOut, brkOut, cluOut, engine.Following, engine.TakeOver);

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
            if (wheel == null || !wheel.IsOpen) { Log("The wheel is not open."); return; }
            wheel.ReadState();
            detectBase = (int[])wheel.Axes.Clone();
            detectPeakVal = new int[FfbDevice.AxisCount];
            detectPeakDelta = new int[FfbDevice.AxisCount];
            detectStartMs = clock.Elapsed.TotalMilliseconds;
            detectPedal = i;
            Log("Press the " + PedalNames[i] + " all the way down now (3 s)...");
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
            if (best < 0 || bestD < 2000) { Log(PedalNames[i] + ": no movement detected. Try again."); return; }
            settings.PedalAxis[i] = best;
            settings.PedalRel[i] = detectBase[best];
            settings.PedalFull[i] = detectPeakVal[best];
            Log(string.Format("{0}: axis {1} (released {2}, full {3}). Click 'Save settings'.",
                PedalNames[i], FfbDevice.AxisNames[best], detectBase[best], detectPeakVal[best]));
            UpdatePedalLabels();
        }

        private void UpdatePedalLabels()
        {
            for (int i = 0; i < 3; i++)
                if (pedalLabels[i] != null)
                    pedalLabels[i].Text = settings.PedalAxis[i] < 0 ? L.T("(not set)") : L.F("axis {0}", FfbDevice.AxisNames[settings.PedalAxis[i]]);
        }

        private void UpdateLive(TelemetryFrame f)
        {
            if (live == null) return;
            live.Steer = steerOut; live.Throttle = thrOut; live.Brake = brkOut; live.Clutch = cluOut;
            live.BridgeOn = settings.BridgeOn;
            live.Spring = lastOut.SpringNm; live.Damper = lastOut.DamperNm; live.Friction = lastOut.FrictionNm;
            live.Bump = lastOut.BumpNm; live.Texture = lastOut.TextureNm; live.Engine = lastOut.EngineNm;
            live.Gain = lastOut.Gain; live.MaxNm = settings.MaxNm;
            live.CenterNote = centerNote;
            live.Active = f != null && f.Active && telemetry.Connected;
            if (live.Active)
            {
                live.Speed = f.SpeedKmh; live.Mass = f.MassT;
                live.Vehicle = f.Vehicle ?? "-"; live.Surface = L.F("surface {0}", f.Surface);
            }
            else live.Speed = 0;
            live.Invalidate();
            if (tractor != null)
            {
                // centragem real aproximada: forca maxima x quanto o volante esta fora do centro
                double wp = (wheel != null && wheel.IsOpen) ? Math.Abs(wheel.Axes[0] / 10000.0) : 0;
                tractor.Steer = lastOut.SpringNm * Math.Min(1.0, wp / 0.08);
                tractor.Weight = 0;
                tractor.Bump = lastOut.BumpNm; tractor.Texture = lastOut.TextureNm; tractor.Engine = lastOut.EngineNm;
                tractor.Active = live.Active && lastOut.Gain > 0;
                tractor.Frame = f;
                if (lastOut.CollCount != lastCollLogged)
                {
                    if (lastCollLogged >= 0 && lastOut.CollCount > lastCollLogged)
                        Log(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Impacto: -{0:0.0} km/h em ~0,1 s, travao {1:0}%", lastOut.CollDv * 3.6, brkOut * 100));
                    lastCollLogged = lastOut.CollCount;
                }
                tractor.CollCount = lastOut.CollCount; tractor.CollLevel = lastOut.CollLevel; tractor.CollX = lastOut.CollX; tractor.CollZ = lastOut.CollZ;
                tractor.Invalidate();
            }
        }

        private void UpdateMonitor(TelemetryFrame f, double age)
        {
            if (chipBase != null)
            {
                // 0.10.0: "conectado" so se o volante responde de facto aos efeitos
                bool responding = device.IsOpen && (wheel == null || wheel.Responding);
                chipBase.Set(responding, !device.IsOpen ? L.T("Wheel not open") : responding ? L.T("Wheel connected") : L.T("Wheel not responding"));
                bool tel = telemetry.Connected && f != null;
                chipTel.Set(tel, tel ? L.F("Telemetry {0:0} Hz", telemetryHz) : L.T("No telemetry"));
                chipBridge.Set(settings.BridgeOn && bridgeRate > 1, settings.BridgeOn ? L.F("Bridge {0:0}/s", bridgeRate) : L.T("Bridge off"));
            }
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Wheel:       " + (device.IsOpen ? device.DeviceName : "NOT OPEN"));
            sb.AppendLine("Status:      " + L.T(lastOut.State));
            if (telemetry.Connected && f != null)
                sb.AppendLine(string.Format(c, "Telemetry:   connected | {0:0} Hz | age {1:0} ms", telemetryHz, age));
            else
                sb.AppendLine("Telemetry:   not connected (FS25 closed or mod not active)");
            if (f != null && f.Active)
            {
                sb.AppendLine(string.Format(c, "Vehicle:     {0} | {1:0.0} t | {2:0.0} km/h | surface {3} | wheels on ground {4}/{5}",
                    f.Vehicle, f.MassT, f.SpeedKmh, f.Surface, f.Contacts, f.Wheels));
                sb.AppendLine(string.Format(c, "Steering:    input {0:0.00} | wheel {1:0} units", f.Steer, device.IsOpen ? device.ReadPosition() : 0));
            }
            else
            {
                sb.AppendLine("Vehicle:     -");
                sb.AppendLine("Steering:    -");
            }
            sb.AppendLine(string.Format(c, "Commands:    steer {0:+0.00;-0.00} | thr {1:0%} | brk {2:0%} | clu {3:0%} | bridge {4:0}/s {5}",
                steerOut, thrOut, brkOut, cluOut, bridgeRate, settings.BridgeOn ? "" : "(OFF)"));
            if (wheel != null)
            {
                StringBuilder ax = new StringBuilder("Axes:       ");
                for (int i = 0; i < FfbDevice.AxisCount; i++) ax.Append(" " + FfbDevice.AxisNames[i] + " " + wheel.Axes[i]);
                sb.AppendLine(ax.ToString());
            }
            sb.AppendLine(string.Format(c, "Output:      gain {0:0%} | spring {1:0.00} Nm | damper {2:0.00} | friction {3:0.00} | bump {4:+0.00;-0.00} | texture {5:0.00} | engine {6:0.00}",
                lastOut.Gain, lastOut.SpringNm, lastOut.DamperNm, lastOut.FrictionNm, lastOut.BumpNm, lastOut.TextureNm, lastOut.EngineNm));
            sb.Append(string.Format(c, "Limit:       {0:0.0} Nm (wheel {1:0.0} Nm)", settings.MaxNm, settings.BaseNm));
            monitor.Text = sb.ToString();
        }

        private double lastSentGain;
        private int lastOscTrips;
        // 0.15: porque e que o FFB esta desligado (mostrado no painel ao vivo e no registo)
        private string ffbOffReason = "not turned on yet (press FFB ON)";
        private bool panicking;
        // 0.15: transicoes registadas (para diagnostico: entrada no veiculo, motor, telemetria)
        private string logVeh = ""; private int logMst = -99; private bool logTelOk; private string logSes = "";

        private void Panic(string why)
        {
            ffbOffReason = why;
            panicking = true;
            try { enableBox.Checked = false; } finally { panicking = false; }
            engine.Halt();
            mixer.Reset();
            oscGuard.Reset();
            lastSentGain = 0;
            if (adapter != null) adapter.Stop();
            if (device != null) device.EmergencyStop();
            Log("EMERGENCY STOP (" + why + ")");
        }

        // Volta a mostrar o volante aos outros programas (uma so vez: fecho normal ou encerramento do Windows).
        private bool hidHideReleased;
        private void ReleaseHidHide(string why)
        {
            if (hidHideReleased || !settings.AutoHide || !HidHideControl.Available) return;
            hidHideReleased = true;
            string r = HidHideControl.Off();
            lock (logLock) { try { File.AppendAllText(logPath, "HidHide: wheel visible to other games again (" + why + ") -> " + r + Environment.NewLine, Encoding.UTF8); } catch { } }
        }

        private bool shutDown;
        private void Shutdown()
        {
            if (shutDown) return;   // o log mostrava o encerramento a correr duas vezes
            shutDown = true;
            if (traceW != null) { try { traceW.Dispose(); } catch { } traceW = null; }
            try { FlushProfile(); } catch { }
            if (loopTimer != null) loopTimer.Stop();
            if (telemetry != null) telemetry.Stop();
            if (bridge != null) { bridge.Publish(false, 0, 0, 0, 0); bridge.Stop(); }
            if (mapper != null) mapper.Dispose();
            if (watchdog != null) watchdog.Dispose();
            if (adapter != null) { try { adapter.Stop(); adapter.Dispose(); } catch { } }
            if (device != null) device.Dispose();
            timeEndPeriod(1);
            ReleaseHidHide("app closed");
            lock (logLock)
            {
                try { File.AppendAllText(logPath, "---- app closed, forces stopped ----" + Environment.NewLine, Encoding.UTF8); } catch { }
            }
        }
    }
}
