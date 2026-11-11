// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB - identidade visual (OptimusZuka: preto, amarelo, branco) e controlos proprios.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace FS25RealFFB
{
    internal static class Theme
    {
        // Paleta "Cockpit": grafite neutro, amarelo ZukaForce so como destaque, vermelho so no STOP.
        public static readonly Color Bg = Color.FromArgb(14, 15, 17);
        public static readonly Color Header = Color.FromArgb(18, 20, 23);
        public static readonly Color Nav = Color.FromArgb(16, 18, 21);
        public static readonly Color Panel = Color.FromArgb(21, 23, 26);
        public static readonly Color Card = Color.FromArgb(28, 31, 35);
        public static readonly Color Line = Color.FromArgb(38, 41, 46);
        public static readonly Color Yellow = Color.FromArgb(255, 204, 0);
        public static readonly Color Text = Color.FromArgb(232, 233, 235);
        public static readonly Color Soft = Color.FromArgb(183, 187, 193);
        public static readonly Color Muted = Color.FromArgb(139, 144, 151);
        public static readonly Color Good = Color.FromArgb(74, 222, 128);
        public static readonly Color Bad = Color.FromArgb(229, 72, 77);
        public static readonly Color Stop = Color.FromArgb(229, 72, 77);

        public static Font UI(float size) { return new Font("Segoe UI", size); }
        public static Font Bold(float size) { return new Font("Segoe UI Semibold", size); }
        // numeros em fonte monoespacada: os valores nao "saltam" enquanto mudam
        public static Font Mono(float size) { return new Font("Consolas", size); }

        // Botao do menu lateral
        public static Button NavBtn(string text, int w)
        {
            Button b = new Button();
            b.Text = text; b.Width = w; b.Height = 40;
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0;
            b.TextAlign = ContentAlignment.MiddleLeft; b.Padding = new Padding(10, 0, 0, 0);
            b.Cursor = Cursors.Hand; b.Font = UI(10f);
            b.Margin = new Padding(0, 0, 0, 4);
            b.FlatAppearance.MouseOverBackColor = Card;
            b.FlatAppearance.MouseDownBackColor = Line;
            return b;
        }

        public static void NavActive(Button b, bool active)
        {
            b.BackColor = active ? Card : Nav;
            b.ForeColor = active ? Yellow : Soft;
            b.Font = active ? Bold(10f) : UI(10f);
        }

        public static Image Logo()
        {
            try
            {
                Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("RealFFB.logo.png");
                return s == null ? null : Image.FromStream(s);
            }
            catch { return null; }
        }

        public static Button Btn(string text, int w, int h, bool primary)
        {
            Button b = new Button();
            b.Text = text; b.Width = w; b.Height = h;
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            b.Font = primary ? Bold(9.5f) : UI(9.5f);
            b.BackColor = primary ? Yellow : Card;
            b.ForeColor = primary ? Color.Black : Text;
            b.FlatAppearance.BorderColor = primary ? Yellow : Line;
            b.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(255, 222, 80) : Color.FromArgb(36, 40, 45);
            b.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(220, 175, 0) : Color.FromArgb(46, 50, 56);
            b.Margin = new Padding(0, 3, 0, 3);
            return b;
        }

        public static Label Section(string text, int w)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = Bold(11f); l.ForeColor = Text;
            l.Width = w; l.Height = 34;
            l.TextAlign = ContentAlignment.BottomLeft;
            l.Margin = new Padding(0, 10, 0, 4);
            l.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Line)) e.Graphics.DrawLine(p, 0, l.Height - 1, l.Width, l.Height - 1);
            };
            return l;
        }

        public static CheckBox Check(string text, bool value, int w)
        {
            CheckBox c = new CheckBox();
            c.Text = text; c.Checked = value;
            c.AutoSize = true; c.MaximumSize = new Size(w, 0);
            c.ForeColor = Soft; c.Cursor = Cursors.Hand;
            c.Margin = new Padding(0, 3, 0, 3);
            return c;
        }
    }

    // TrackBar que NAO muda com a roda do rato: a roda passa para o painel e a pagina desce.
    internal sealed class NoWheelTrackBar : TrackBar
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEWHEEL)
            {
                int delta = (short)((long)m.WParam >> 16 & 0xFFFF);
                Control p = Parent;
                while (p != null && !(p is ScrollableControl && ((ScrollableControl)p).AutoScroll)) p = p.Parent;
                if (p != null)
                {
                    ScrollableControl sc = (ScrollableControl)p;
                    int y = -sc.AutoScrollPosition.Y - delta / 120 * 60;
                    sc.AutoScrollPosition = new Point(0, Math.Max(0, y));
                }
                return;
            }
            base.WndProc(ref m);
        }
    }

    // Painel de dados ao vivo: comandos enviados ao jogo e forcas na base.
    internal sealed class LiveView : Control
    {
        public double Steer, Throttle, Brake, Clutch;
        public double Spring, Damper, Friction, Bump, Texture, Engine, MaxNm = 3, Gain;
        public double Speed, Mass;
        public string Vehicle = "-", Surface = "-";
        public bool BridgeOn = true, Active;
        public string CenterNote = "";   // 0.12.1: "sem centragem" quando o veiculo nao centra na vida real

        public LiveView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int pad = 16, w = Width - pad * 2;
            int colW = (w - 24) / 2;
            using (Font h = Theme.Bold(10.5f))
            using (Font f = Theme.UI(9f))
            using (Font big = Theme.Mono(20f))
            using (SolidBrush yb = new SolidBrush(Theme.Yellow))
            using (SolidBrush tb = new SolidBrush(Theme.Text))
            using (SolidBrush mb = new SolidBrush(Theme.Muted))
            {
                // ---- coluna 1: comandos
                int x = pad, y = 12;
                g.DrawString(L.T("COMMANDS") + "  " + (BridgeOn ? "" : L.T("(BRIDGE OFF)")), h, tb, x, y);
                y += 28;
                y = Centered(g, f, tb, mb, L.T("Steering"), Steer, x, y, colW);
                y = Bar(g, f, tb, mb, L.T("Throttle"), Throttle, 1, (Throttle * 100).ToString("0") + " %", x, y, colW, Theme.Good);
                y = Bar(g, f, tb, mb, L.T("Brake"), Brake, 1, (Brake * 100).ToString("0") + " %", x, y, colW, Theme.Bad);
                y = Bar(g, f, tb, mb, L.T("Clutch"), Clutch, 1, (Clutch * 100).ToString("0") + " %", x, y, colW, Theme.Muted);
                y += 6;
                g.DrawString(Speed.ToString("0") , big, tb, x, y);
                SizeF sz = g.MeasureString(Speed.ToString("0"), big);
                g.DrawString("km/h", f, mb, x + sz.Width, y + 12);
                g.DrawString(Active ? Vehicle + "  |  " + Mass.ToString("0.0") + " t  |  " + Surface : L.T("waiting for FS25..."), f, mb, x, y + 34);

                // ---- coluna 2: forcas
                x = pad + colW + 24; y = 12;
                // titulo encolhe se a coluna for estreita (janela pequena): o ganho nunca fica cortado
                string forceHdr = L.F("FORCE AT THE WHEEL  (gain {0} %)", (Gain * 100).ToString("0"));
                if (g.MeasureString(forceHdr, h).Width <= colW) g.DrawString(forceHdr, h, tb, x, y);
                else
                {
                    float hs = 10f;
                    Font hSmall = Theme.Bold(hs);
                    while (hs > 7.5f && g.MeasureString(forceHdr, hSmall).Width > colW)
                    {
                        hSmall.Dispose(); hs -= 0.5f; hSmall = Theme.Bold(hs);
                    }
                    g.DrawString(forceHdr, hSmall, tb, x, y + (10.5f - hs));
                    hSmall.Dispose();
                }
                y += 28;
                double m = Math.Max(0.5, MaxNm);
                if (CenterNote.Length > 0)
                {
                    // veiculo sem centragem na vida real: diz-se em vez de mostrar uma barra vazia sem explicacao
                    g.DrawString(L.T("Centering"), f, tb, x, y);
                    SizeF cs = g.MeasureString(CenterNote, f);
                    g.DrawString(CenterNote, f, yb, x + colW - cs.Width, y);
                    Track(g, x, y + 20, colW);
                    y += 40;
                }
                else y = Bar(g, f, tb, mb, L.T("Centering"), Spring, m, Spring.ToString("0.00") + " Nm", x, y, colW, Theme.Yellow);
                y = Bar(g, f, tb, mb, L.T("Damping"), Damper, m, Damper.ToString("0.00") + " Nm", x, y, colW, Theme.Yellow);
                y = Bar(g, f, tb, mb, L.T("Friction"), Friction, m, Friction.ToString("0.00") + " Nm", x, y, colW, Theme.Yellow);
                y = CenteredNm(g, f, tb, mb, L.T("Bumps"), Bump, m, x, y, colW);
                y = Bar(g, f, tb, mb, L.T("Texture"), Texture, m, Texture.ToString("0.00") + " Nm", x, y, colW, Theme.Yellow);
                y = Bar(g, f, tb, mb, L.T("Engine"), Engine, m, Engine.ToString("0.00") + " Nm", x, y, colW, Theme.Yellow);
            }
        }

        private static void Track(Graphics g, int x, int y, int w)
        {
            using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillRectangle(b, x, y + 2, w, 6);
        }

        private int Bar(Graphics g, Font f, Brush tb, Brush mb, string name, double v, double max, string txt, int x, int y, int w, Color c)
        {
            g.DrawString(name, f, tb, x, y);
            SizeF s = g.MeasureString(txt, f);
            g.DrawString(txt, f, mb, x + w - s.Width, y);
            Track(g, x, y + 20, w);
            double r = max <= 0 ? 0 : Math.Max(0, Math.Min(1, v / max));
            using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, x, y + 22, (float)(w * r), 6);
            return y + 40;
        }

        private int Centered(Graphics g, Font f, Brush tb, Brush mb, string name, double v, int x, int y, int w)
        {
            string txt = (v >= 0 ? "+" : "") + (v * 100).ToString("0") + " %";
            g.DrawString(name, f, tb, x, y);
            SizeF s = g.MeasureString(txt, f);
            g.DrawString(txt, f, mb, x + w - s.Width, y);
            Track(g, x, y + 20, w);
            float mid = x + w / 2f;
            float len = (float)(w / 2.0 * Math.Max(-1, Math.Min(1, v)));
            using (SolidBrush b = new SolidBrush(Theme.Yellow))
                g.FillRectangle(b, len >= 0 ? mid : mid + len, y + 22, Math.Abs(len), 6);
            using (Pen p = new Pen(Theme.Text, 2)) g.DrawLine(p, mid, y + 17, mid, y + 33);
            return y + 40;
        }

        private int CenteredNm(Graphics g, Font f, Brush tb, Brush mb, string name, double v, double max, int x, int y, int w)
        {
            string txt = (v >= 0 ? "+" : "") + v.ToString("0.00") + " Nm";
            g.DrawString(name, f, tb, x, y);
            SizeF s = g.MeasureString(txt, f);
            g.DrawString(txt, f, mb, x + w - s.Width, y);
            Track(g, x, y + 20, w);
            float mid = x + w / 2f;
            float len = (float)(w / 2.0 * Math.Max(-1, Math.Min(1, v / max)));
            using (SolidBrush b = new SolidBrush(Theme.Yellow))
                g.FillRectangle(b, len >= 0 ? mid : mid + len, y + 22, Math.Abs(len), 6);
            using (Pen p = new Pen(Theme.Muted, 1)) g.DrawLine(p, mid, y + 18, mid, y + 32);
            return y + 40;
        }
    }

    // Indicador de estado do cabecalho: ponto colorido + texto.
    internal sealed class StatusChip : Control
    {
        private bool ok;
        public StatusChip(string text)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Text = text; Width = 150; Height = 24;
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.UI(9f);
        }

        public void Set(bool good, string text)
        {
            if (good == ok && text == Text) return;
            ok = good; Text = text; Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush b = new SolidBrush(ok ? Theme.Good : Theme.Bad)) e.Graphics.FillEllipse(b, 2, 7, 10, 10);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Point(18, 3), ForeColor);
        }
    }
}
