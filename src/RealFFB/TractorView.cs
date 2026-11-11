// Copyright (c) 2026 OptimusZuka. All rights reserved.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FS25RealFFB
{
    // Veiculo visto de cima (desenho original e generico por categoria: trator, ceifeira, camiao, carro,
    // outro), com cada roda na posicao real enviada pelo mod. As pecas brilham onde estao as forcas:
    //  - rodas que viram (direcao): centragem;
    //  - cada roda em separado: pancadas da suspensao DESSA roda (frente/tras, esquerda/direita);
    //  - rodas no chao: textura do piso;
    //  - motor: vibracao do motor.
    // O brilho sobe logo e desce devagar, para ser legivel a olho. Frente do veiculo para cima.
    internal sealed class TractorView : Control
    {
        public double Steer, Weight, Bump, Texture, Engine;   // Nm, como no painel de forcas
        public bool Active;
        public TelemetryFrame Frame;
        public int TopRightReserve;   // largura do canto superior direito ocupada por um botao
        public int CollCount; public double CollLevel, CollX, CollZ;   // ultimo embate (ver FfbOutput)
        private int lastColl = -1; private double flash, flashX, flashZ;

        private enum Kind { Tractor, Combine, Truck, Car, Other }

        // uma cor por origem da forca (diferem tambem em luminosidade)
        private static readonly Color CSteer = Color.FromArgb(90, 180, 255);   // direcao / centragem: azul
        private static readonly Color CSusp = Color.FromArgb(255, 150, 40);    // suspensao / pancadas: laranja
        private static readonly Color CGround = Color.FromArgb(110, 215, 120); // piso / textura: verde
        private static readonly Color CEngine = Color.FromArgb(235, 70, 70);   // motor: vermelho
        private static readonly Color CImpact = Color.FromArgb(255, 240, 200); // embate: branco quente

        private double gSteer, gTex, gEng, groundOff;
        private double[] gWheel = new double[0];
        private double[] lastSusp = new double[0];
        private double lastT = double.NaN;

        public TractorView()
        {
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        private static double Follow(double g, double target)
        {
            return target > g ? target : g + (target - g) * 0.15;   // sobe ja, desce ~15% por atualizacao
        }

        private static Kind KindOf(TelemetryFrame f)
        {
            string t = ((f.VehicleType ?? "") + " " + (f.Vehicle ?? "")).ToLowerInvariant();
            if (t.Contains("combine") || t.Contains("harvester") || t.Contains("forage") || t.Contains("beet") || t.Contains("potato")) return Kind.Combine;
            if (t.Contains("tractor")) return Kind.Tractor;
            if (t.Contains("truck") || t.Contains("semi") || t.Contains("lorry")) return Kind.Truck;
            if (t.Contains("car") || t.Contains("pickup") || t.Contains("suv")) return Kind.Car;
            if (f.Wheels >= 6) return Kind.Truck;
            return f.Wheels == 4 ? Kind.Tractor : Kind.Other;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Theme.Bg);
            if (Width < 8 || Height < 8) return;
            using (LinearGradientBrush bg = new LinearGradientBrush(ClientRectangle, Theme.Card, Theme.Bg, LinearGradientMode.Vertical))
                g.FillRectangle(bg, ClientRectangle);
            TelemetryFrame f = Frame;
            bool on = Active && f != null && f.Wheels > 0;

            if (!on)
            {
                using (Font fn = Theme.UI(8.5f))
                using (SolidBrush m = new SolidBrush(Theme.Muted))
                    g.DrawString(L.T("Waiting for FS25"), fn, m, 36, 9);
                return;
            }

            int n = f.Wheels;
            if (gWheel.Length != n) { gWheel = new double[n]; lastSusp = new double[0]; }

            // pancadas por roda: velocidade da suspensao de cada roda (m/s) entre amostras
            double tNow = f.GameTimeMs;
            double dt = Ok(lastT) ? (tNow - lastT) / 1000.0 : 0;
            bool fresh = dt > 0.001 && dt < 0.5 && lastSusp.Length == f.Susp.Length;
            double bumpScale = Math.Min(1.0, Math.Abs(Bump) / 0.8);
            for (int i = 0; i < n; i++)
            {
                double v = 0;
                if (fresh && i < f.Susp.Length && Ok(f.Susp[i]) && Ok(lastSusp[i])) v = Math.Abs(f.Susp[i] - lastSusp[i]) / dt;
                double target = Math.Min(1.0, v / 0.6) * Math.Max(0.35, bumpScale);
                gWheel[i] = Follow(gWheel[i], target);
            }
            if (!fresh || tNow != lastT) { lastSusp = (double[])f.Susp.Clone(); lastT = tNow; }
            gSteer = Follow(gSteer, Math.Min(1.0, (Math.Abs(Steer) + Math.Abs(Weight)) / 1.0));
            gTex = Follow(gTex, Math.Min(1.0, Math.Abs(Texture) / 0.35));
            gEng = Follow(gEng, Math.Min(1.0, Math.Abs(Engine) / 0.25));

            // posicoes das rodas (m). Sem posicao: pares da frente para tras, esquerda/direita alternadas.
            double[] wx = new double[n], wz = new double[n];
            bool havePos = f.WheelX.Length == n;
            for (int i = 0; i < n && havePos; i++) if (!Ok(f.WheelX[i]) || !Ok(f.WheelZ[i])) havePos = false;
            for (int i = 0; i < n; i++)
            {
                if (havePos) { wx[i] = f.WheelX[i]; wz[i] = f.WheelZ[i]; }
                else { wx[i] = (i % 2 == 0) ? 0.9 : -0.9; wz[i] = 1.5 - (i / 2) * 1.6; }
            }
            double minX = 0, maxX = 0, minZ = 0, maxZ = 0;
            for (int i = 0; i < n; i++)
            {
                if (i == 0 || wx[i] < minX) minX = wx[i]; if (i == 0 || wx[i] > maxX) maxX = wx[i];
                if (i == 0 || wz[i] < minZ) minZ = wz[i]; if (i == 0 || wz[i] > maxZ) maxZ = wz[i];
            }
            Kind kind = KindOf(f);
            double track = Math.Max(1.2, maxX - minX), wheelbase = Math.Max(1.5, maxZ - minZ);
            // extensao total do desenho em metros (inclui carrocaria a frente/atras das rodas)
            double front = maxZ + (kind == Kind.Combine ? 1.6 : kind == Kind.Tractor ? 1.0 : 0.8);
            double back = minZ - (kind == Kind.Truck ? 1.5 : 0.9);
            double halfW = Math.Max(track / 2 + 0.6, kind == Kind.Combine ? track * 0.85 : 0);
            double midX = (minX + maxX) / 2;

            float pad = 14;
            float sc = (float)Math.Min((Width - 2 * pad) / (2 * halfW + 0.4), (Height - 2 * pad - 40) / (front - back));
            float cx = Width / 2f, top = pad;
            // x do veiculo: positivo = esquerda -> no ecra a esquerda
            Func<double, float> SX = x => cx - (float)((x - midX) * sc);
            Func<double, float> SY = z => top + (float)((front - z) * sc);

            // 0.10.0: cores mais claras (o desenho ficava escuro demais sobre o fundo grafite)
            Color body = Color.FromArgb(98, 106, 118), bodyHi = Color.FromArgb(136, 145, 158), edge = Color.FromArgb(178, 186, 198),
                  glass = Color.FromArgb(62, 98, 132), dark = Color.FromArgb(62, 66, 74);
            float bodyW = (float)(track * sc * 0.72);
            float m1 = (float)sc;   // 1 metro em pixeis

            // chao: linhas de cultivo que passam com a velocidade (andar para a frente -> descem)
            float gap = Math.Max(10f, 1.2f * sc);
            if (fresh && Ok(f.Vz)) groundOff += f.Vz * dt * sc;
            groundOff %= gap; if (groundOff < 0) groundOff += gap;
            using (Pen rowPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1))
                for (float gy = (float)groundOff; gy < Height - 32; gy += gap) g.DrawLine(rowPen, 6, gy, Width - 6, gy);

            // sombra no chao
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                g.FillEllipse(sh, cx - (float)(halfW * sc), SY(front) + 4, (float)(2 * halfW * sc), SY(back) - SY(front));

            // carrocaria por categoria (desenhos originais e genericos)
            RectangleF engineRect;
            switch (kind)
            {
                case Kind.Tractor:
                {
                    float hoodW = bodyW * 0.5f;
                    // pesos frontais
                    RectangleF weights = new RectangleF(cx - hoodW * 0.65f, SY(front) - 2, hoodW * 1.3f, 0.35f * m1);
                    Fill(g, weights, dark, 3);
                    using (Pen p = new Pen(edge, 1)) for (float xx = weights.X + 5; xx < weights.Right - 3; xx += 5) g.DrawLine(p, xx, weights.Y + 2, xx, weights.Bottom - 2);
                    // capo com grelha e escape
                    RectangleF hood = new RectangleF(cx - hoodW / 2, weights.Bottom + 1, hoodW, (float)((front - minZ - 0.6) * sc * 0.55));
                    engineRect = hood;
                    Glow(g, hood, gEng, 8, CEngine);
                    Shade(g, hood, body, bodyHi, 6);
                    using (Pen p = new Pen(dark, 1.2f)) for (int k = 1; k < 4; k++) g.DrawLine(p, hood.X + 4, hood.Y + k * 4, hood.Right - 4, hood.Y + k * 4);
                    using (SolidBrush b = new SolidBrush(dark)) g.FillEllipse(b, hood.Right - 9, hood.Bottom - 12, 6, 6);
                    // guarda-lamas sobre as rodas traseiras
                    float fenderY = SY(minZ + 0.75);
                    Fill(g, new RectangleF(cx - bodyW * 0.95f, fenderY, bodyW * 0.32f, 1.5f * m1), body, 8);
                    Fill(g, new RectangleF(cx + bodyW * 0.63f, fenderY, bodyW * 0.32f, 1.5f * m1), body, 8);
                    // cabine: tejadilho, vidros, porta-vidro traseiro
                    RectangleF cab = new RectangleF(cx - bodyW * 0.5f, hood.Bottom - 2, bodyW, 1.5f * m1);
                    Shade(g, cab, edge, bodyHi, 7);
                    Fill(g, Inset(cab, 4), glass, 5);
                    using (SolidBrush hl = new SolidBrush(Color.FromArgb(80, 255, 255, 255))) g.FillRectangle(hl, cab.X + 6, cab.Y + 6, cab.Width * 0.35f, 3);
                    // engate traseiro
                    Fill(g, new RectangleF(cx - 0.35f * m1, cab.Bottom + 2, 0.7f * m1, 0.3f * m1), dark, 2);
                    break;
                }
                case Kind.Combine:
                {
                    // plataforma de corte: barra, sem-fim e molinete
                    RectangleF header = new RectangleF(cx - (float)(halfW * sc), SY(front) + 2, (float)(2 * halfW * sc), 1.0f * m1);
                    Shade(g, header, edge, bodyHi, 4);
                    using (Pen p = new Pen(dark, 1.4f))
                    {
                        g.DrawLine(p, header.X + 4, header.Y + header.Height * 0.35f, header.Right - 4, header.Y + header.Height * 0.35f);
                        for (float xx = header.X + 6; xx < header.Right - 4; xx += 7) g.DrawLine(p, xx, header.Bottom - 5, xx + 2, header.Bottom - 1);
                    }
                    RectangleF hull = new RectangleF(cx - bodyW / 2, header.Bottom + 4, bodyW, SY(back + 0.2) - header.Bottom - 4);
                    Shade(g, hull, body, bodyHi, 8);
                    // cabine envidracada a frente
                    RectangleF cab = new RectangleF(cx - bodyW * 0.3f, hull.Y + 4, bodyW * 0.6f, 1.1f * m1);
                    Fill(g, cab, glass, 5);
                    using (SolidBrush hl = new SolidBrush(Color.FromArgb(80, 255, 255, 255))) g.FillRectangle(hl, cab.X + 4, cab.Y + 4, cab.Width * 0.4f, 3);
                    // tanque de grao com tampas
                    RectangleF tank = new RectangleF(cx - bodyW * 0.4f, cab.Bottom + 6, bodyW * 0.8f, hull.Height * 0.35f);
                    Fill(g, tank, dark, 6);
                    using (Pen p = new Pen(edge, 1)) g.DrawLine(p, tank.X + tank.Width / 2, tank.Y + 3, tank.X + tank.Width / 2, tank.Bottom - 3);
                    // tubo de descarga (dobrado, lado esquerdo)
                    using (Pen p = new Pen(edge, 0.25f * m1)) g.DrawLine(p, hull.X, tank.Y + 6, hull.X - 0.5f * m1, tank.Y + 6);
                    engineRect = new RectangleF(cx - bodyW * 0.35f, hull.Bottom - 1.1f * m1, bodyW * 0.7f, 0.85f * m1);
                    Glow(g, engineRect, gEng, 8, CEngine);
                    Fill(g, engineRect, edge, 4);
                    using (Pen p = new Pen(dark, 1)) for (int k = 1; k < 4; k++) g.DrawLine(p, engineRect.X + 4, engineRect.Y + k * engineRect.Height / 4, engineRect.Right - 4, engineRect.Y + k * engineRect.Height / 4);
                    break;
                }
                case Kind.Truck:
                {
                    RectangleF cab = new RectangleF(cx - bodyW / 2, SY(front) + 2, bodyW, 2.0f * m1);
                    engineRect = new RectangleF(cab.X + 4, cab.Y + 4, cab.Width - 8, cab.Height * 0.45f);
                    Glow(g, engineRect, gEng, 8, CEngine);
                    Shade(g, cab, body, bodyHi, 7);
                    Fill(g, new RectangleF(cab.X + 5, cab.Y + 4, cab.Width - 10, cab.Height * 0.28f), glass, 3);
                    // espelhos
                    Fill(g, new RectangleF(cab.X - 5, cab.Y + 6, 4, 8), dark, 1);
                    Fill(g, new RectangleF(cab.Right + 1, cab.Y + 6, 4, 8), dark, 1);
                    // caixa com nervuras
                    RectangleF box = new RectangleF(cx - bodyW / 2, cab.Bottom + 5, bodyW, SY(back + 0.1) - cab.Bottom - 5);
                    Shade(g, box, edge, bodyHi, 4);
                    using (Pen p = new Pen(body, 1)) for (float yy = box.Y + 6; yy < box.Bottom - 3; yy += 6) g.DrawLine(p, box.X + 3, yy, box.Right - 3, yy);
                    break;
                }
                default:
                {
                    RectangleF hull = new RectangleF(cx - bodyW / 2, SY(front) + 2, bodyW, SY(back + 0.1) - SY(front) - 2);
                    engineRect = new RectangleF(hull.X + 4, hull.Y + 4, hull.Width - 8, hull.Height * 0.25f);
                    Glow(g, engineRect, gEng, 8, CEngine);
                    Shade(g, hull, body, bodyHi, kind == Kind.Car ? 14 : 8);
                    Fill(g, new RectangleF(hull.X + 6, hull.Y + hull.Height * 0.28f, hull.Width - 12, hull.Height * 0.16f), glass, 4);
                    Fill(g, new RectangleF(hull.X + 8, hull.Y + hull.Height * 0.47f, hull.Width - 16, hull.Height * 0.25f), bodyHi, 5);
                    Fill(g, new RectangleF(hull.X + 6, hull.Y + hull.Height * 0.74f, hull.Width - 12, hull.Height * 0.1f), glass, 3);
                    break;
                }
            }

            // rodas: eixo motriz maior (trator: tras; ceifeira: frente)
            for (int i = 0; i < n; i++)
            {
                bool rearAxle = wz[i] < (minZ + maxZ) / 2;
                double size = 1.0;
                if (kind == Kind.Tractor) size = rearAxle ? 1.6 : 1.05;
                else if (kind == Kind.Combine) size = rearAxle ? 0.95 : 1.6;
                else if (kind == Kind.Car) size = 0.75;
                float wl = (float)(0.85 * size * sc), ww = (float)(0.38 * size * sc);
                float px = SX(wx[i]), py = SY(wz[i]);
                double steerA = (i < f.WheelSteerA.Length && Ok(f.WheelSteerA[i])) ? f.WheelSteerA[i] : 0;
                bool steered = Math.Abs(steerA) > 0.005 || (!havePos ? i < 2 : false);
                bool contact = i < f.WheelContact.Length && f.WheelContact[i];

                GraphicsState st = g.Save();
                g.TranslateTransform(px, py);
                g.RotateTransform((float)(-steerA * 180 / Math.PI));
                RectangleF wr = new RectangleF(-ww / 2, -wl / 2, ww, wl);
                // piso (verde) por baixo, direcao (azul) no contorno das rodas que viram
                if (contact) Glow(g, wr, gTex * 0.9, 9, CGround);
                if (steered) Glow(g, wr, gSteer * 0.6, 5, CSteer);
                Fill(g, wr, Color.FromArgb(40, 43, 48), 3);
                using (Pen tp = new Pen(Color.FromArgb(84, 88, 96), 1))
                    for (float ty = wr.Y + 3; ty < wr.Bottom - 2; ty += 4) g.DrawLine(tp, wr.X + 2, ty, wr.Right - 2, ty + 2);
                Color rimC = steered && gSteer > 0.05 ? Mix(edge, CSteer, gSteer) : edge;
                using (Pen p = new Pen(rimC, steered && gSteer > 0.05 ? 2.6f : 1.6f))
                using (GraphicsPath gp = Rounded(wr, 3)) g.DrawPath(p, gp);
                g.Restore(st);

                // suspensao (laranja): barra ao lado de fora da roda, altura = pancada nessa roda
                float side = wx[i] >= midX ? -1 : 1;   // esquerda do veiculo = esquerda do ecra
                float bx = px + side * (ww / 2 + 5) - (side < 0 ? 4 : 0);
                float bh = wl;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(60, CSusp))) g.FillRectangle(b, bx, py - bh / 2, 4, bh);
                float lvl = (float)Math.Min(1.0, gWheel[i]);
                if (lvl > 0.02)
                {
                    using (SolidBrush b = new SolidBrush(CSusp)) g.FillRectangle(b, bx, py + bh / 2 - bh * lvl, 4, bh * lvl);
                    Glow(g, new RectangleF(px - ww / 2, py - wl / 2, ww, wl), lvl * 0.8, 5, CSusp);
                }
            }

            // embate: clarao no ponto da carrocaria onde aconteceu, ~1,2 s
            if (CollCount != lastColl)
            {
                if (CollCount > lastColl && lastColl >= 0) { flash = Math.Max(0.4, CollLevel); flashX = CollX; flashZ = CollZ; }
                lastColl = CollCount;
            }
            if (flash > 0.02)
            {
                double fx = Clamp1(flashX), fz = Clamp1(flashZ);
                double len = Math.Max(Math.Abs(fx), Math.Abs(fz)); if (len < 1e-3) { fz = 1; len = 1; }
                fx /= len; fz /= len;   // leva o ponto ate a borda do retangulo do veiculo
                float ix = cx - (float)(fx * halfW * sc * 0.95), iy = SY((front + back) / 2 + fz * (front - back) / 2 * 0.95);
                float rr = (float)(10 + 26 * flash);
                for (int k = 3; k >= 1; k--)
                    using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(110 * flash / k), 255, 60, 40)))
                        g.FillEllipse(b, ix - rr * k / 2f, iy - rr * k / 2f, rr * k, rr * k);
                using (Pen p = new Pen(Color.FromArgb((int)(255 * flash), 255, 210, 80), 2))
                    for (int k = 0; k < 8; k++)
                    {
                        double a = k * Math.PI / 4;
                        g.DrawLine(p, ix + (float)(Math.Cos(a) * rr * 0.3), iy + (float)(Math.Sin(a) * rr * 0.3), ix + (float)(Math.Cos(a) * rr), iy + (float)(Math.Sin(a) * rr));
                    }
                using (Font ff = Theme.Bold(8.5f))
                using (SolidBrush tb = new SolidBrush(Color.FromArgb((int)(255 * Math.Min(1, flash * 1.5)), CImpact)))
                {
                    string s = L.T("IMPACT");
                    SizeF sz = g.MeasureString(s, ff);
                    float tx = Math.Max(2, Math.Min(Width - sz.Width - 2, ix - sz.Width / 2));
                    float ty = Math.Max(2, Math.Min(Height - 60, iy - rr - sz.Height));
                    g.DrawString(s, ff, tb, tx, ty);
                }
                flash *= 0.9;
            }

            // legenda: cor + nivel de cada origem
            {
                string[] names = { L.T("Steering"), L.T("Suspension"), L.T("Surface"), L.T("Engine") };
                Color[] cols = { CSteer, CSusp, CGround, CEngine };
                double maxW = 0; for (int i = 0; i < gWheel.Length; i++) maxW = Math.Max(maxW, gWheel[i]);
                double[] lv = { gSteer, maxW, gTex, gEng };
                float lw = (Width - 16) / 4f, ly = Height - 26;
                Fill(g, new RectangleF(4, Height - 30, Width - 8, 27), Color.FromArgb(110, 0, 0, 0), 6);
                using (Font lf = Theme.UI(7.5f))
                using (SolidBrush tb = new SolidBrush(Theme.Soft))
                using (SolidBrush trk = new SolidBrush(Theme.Line))
                    for (int i = 0; i < 4; i++)
                    {
                        float lx = 8 + i * lw;
                        using (SolidBrush cb = new SolidBrush(cols[i])) g.FillRectangle(cb, lx, ly + 3, 8, 8);
                        g.DrawString(names[i], lf, tb, lx + 11, ly);
                        g.FillRectangle(trk, lx, ly + 15, lw - 10, 4);
                        using (SolidBrush cb = new SolidBrush(cols[i])) g.FillRectangle(cb, lx, ly + 15, (float)((lw - 10) * Math.Min(1, lv[i])), 4);
                    }
            }

            // orientacao
            using (Font fn = Theme.UI(7.5f))
            using (SolidBrush m = new SolidBrush(Theme.Muted))
            {
                g.DrawString(L.T("front"), fn, m, Width - 46 - TopRightReserve, 6);
                g.DrawString(L.T("L"), fn, m, 8, Height - 44);
                g.DrawString(L.T("R"), fn, m, Width - 16, Height - 44);
            }
        }

        private static double Clamp1(double v) { return Ok(v) ? Math.Max(-1, Math.Min(1, v)) : 0; }

        private static void Shade(Graphics g, RectangleF r, Color c, Color hi, float rad)
        {
            if (r.Width <= 1 || r.Height <= 1) return;
            using (GraphicsPath p = Rounded(r, rad))
            using (LinearGradientBrush b = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2), hi, c, LinearGradientMode.Horizontal))
            {
                ColorBlend cb = new ColorBlend(3);
                cb.Colors = new Color[] { c, hi, c };
                cb.Positions = new float[] { 0f, 0.45f, 1f };
                b.InterpolationColors = cb;
                g.FillPath(b, p);
            }
        }

        private static bool Ok(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }

        private static RectangleF Inset(RectangleF r, float d) { return new RectangleF(r.X + d, r.Y + d, r.Width - 2 * d, r.Height - 2 * d); }

        private static void Fill(Graphics g, RectangleF r, Color c, float rad)
        {
            if (r.Width <= 1 || r.Height <= 1) return;
            using (GraphicsPath p = Rounded(r, rad))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }

        private static void Glow(Graphics g, RectangleF r, double a, float spread, Color col)
        {
            if (a < 0.03 || r.Width <= 1 || r.Height <= 1) return;
            for (int i = 3; i >= 1; i--)
            {
                float d = spread * i / 3f;
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)Math.Min(255, 125 * a / i), col)))
                using (GraphicsPath p = Rounded(new RectangleF(r.X - d, r.Y - d, r.Width + 2 * d, r.Height + 2 * d), 4 + d))
                    g.FillPath(b, p);
            }
        }

        private static Color Mix(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        private static GraphicsPath Rounded(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Max(1f, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
