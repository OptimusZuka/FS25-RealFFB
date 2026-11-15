// Copyright (c) 2026 OptimusZuka. All rights reserved.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms;

namespace FS25RealFFB
{
    // 0.15.1: "--readme-shots <pasta>" - usado so pelo workflow de capturas do README. Desenha a vista do
    // veiculo da propria app (TractorView) para quatro veiculos, com telemetria SIMULADA passada pelo motor
    // de FFB real (FfbEngine). Nao abre o volante nem o jogo e nao toca no HidHide. A imagem diz que os
    // dados sao simulados.
    internal static class ReadmeShots
    {
        private sealed class Scene
        {
            public string Title, Type, Cat, Surface;
            public double Speed, Rpm, Load, Mass, Steer;
            public double[] X, Z, SteerA;
            public int BumpWheel = -1;
        }

        public static void Run(string dir)
        {
            L.Set("en");
            System.IO.Directory.CreateDirectory(dir);
            Scene[] sc =
            {
                new Scene { Title = "Tractor - field, 9 km/h", Type = "tractor", Cat = "TRACTORSM", Surface = "field",
                    Speed = 9, Rpm = 1500, Load = 0.6, Mass = 7, Steer = 0.25,
                    X = new double[] { 0.85, -0.85, 0.95, -0.95 }, Z = new double[] { 1.3, 1.3, -1.2, -1.2 },
                    SteerA = new double[] { 0.25, 0.25, 0, 0 }, BumpWheel = 0 },
                new Scene { Title = "Combine harvester - field, 6 km/h", Type = "combineDrivable", Cat = "HARVESTERS", Surface = "field",
                    Speed = 6, Rpm = 1900, Load = 0.8, Mass = 18, Steer = -0.15,
                    X = new double[] { 1.4, -1.4, 1.2, -1.2 }, Z = new double[] { 1.2, 1.2, -2.6, -2.6 },
                    SteerA = new double[] { 0, 0, -0.2, -0.2 }, BumpWheel = 3 },
                new Scene { Title = "Truck - road, 60 km/h", Type = "truck", Cat = "TRUCKS", Surface = "road",
                    Speed = 60, Rpm = 1300, Load = 0.4, Mass = 12, Steer = 0.05,
                    X = new double[] { 1.0, -1.0, 1.0, -1.0, 1.0, -1.0 }, Z = new double[] { 2.0, 2.0, -1.4, -1.4, -2.7, -2.7 },
                    SteerA = new double[] { 0.05, 0.05, 0, 0, 0, 0 }, BumpWheel = -1 },
                new Scene { Title = "Pickup - gravel, 35 km/h", Type = "car", Cat = "CARS", Surface = "gravel",
                    Speed = 35, Rpm = 2200, Load = 0.3, Mass = 2.5, Steer = -0.2,
                    X = new double[] { 0.8, -0.8, 0.8, -0.8 }, Z = new double[] { 1.6, 1.6, -1.6, -1.6 },
                    SteerA = new double[] { -0.2, -0.2, 0, 0 }, BumpWheel = 1 },
            };

            const int tw = 520, th = 360, cap = 46, pad = 14;
            using (Bitmap all = new Bitmap(pad + 2 * (tw + pad), 40 + 2 * (th + cap + pad) + 30))
            using (Graphics g = Graphics.FromImage(all))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(Theme.Bg);
                using (Font tf = new Font("Segoe UI", 12f, FontStyle.Bold))
                using (SolidBrush tb = new SolidBrush(Theme.Text))
                    g.DrawString("Live vehicle view: where each force comes from", tf, tb, pad, 10);
                for (int i = 0; i < sc.Length; i++)
                {
                    int x = pad + (i % 2) * (tw + pad), y = 40 + (i / 2) * (th + cap + pad);
                    string info;
                    using (Bitmap b = RenderScene(sc[i], tw, th, out info))
                    {
                        b.Save(System.IO.Path.Combine(dir, "vehicle-view-" + (i + 1) + ".png"), ImageFormat.Png);
                        g.DrawImage(b, x, y);
                    }
                    using (Font f1 = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                    using (Font f2 = Theme.UI(8.5f))
                    using (SolidBrush b1 = new SolidBrush(Theme.Text))
                    using (SolidBrush b2 = new SolidBrush(Theme.Muted))
                    {
                        g.DrawString(sc[i].Title, f1, b1, x, y + th + 4);
                        g.DrawString(info, f2, b2, x, y + th + 24);
                    }
                }
                using (Font nf = Theme.UI(8.5f))
                using (SolidBrush nb = new SolidBrush(Theme.Muted))
                    g.DrawString("Drawn by the RealFFB app from SIMULATED telemetry, forces computed by the app's FFB engine (8 Nm base). Not an in-game screenshot.",
                        nf, nb, pad, all.Height - 26);
                all.Save(System.IO.Path.Combine(dir, "vehicle-views.png"), ImageFormat.Png);
            }
        }

        private static Bitmap RenderScene(Scene s, int w, int h, out string info)
        {
            Settings st = new Settings();
            st.BaseNm = 8; st.MaxNm = 6; st.GearDriven = false; st.ForceScale = 1;
            FfbEngine eng = new FfbEngine();
            TractorView v = new TractorView();
            v.Size = new Size(w, h);
            Bitmap bmp = new Bitmap(w, h);
            ForceRequest r = null;
            double maxBump = 0, maxTex = 0, maxEng = 0, maxSpring = 0;
            int n = s.X.Length;
            for (int k = 0; k < 160; k++)
            {
                TelemetryFrame f = new TelemetryFrame();
                f.Seq = k + 1; f.GameTimeMs = k * 16.7; f.Active = true;
                f.Vehicle = s.Title; f.VehicleType = s.Type; f.Category = s.Cat; f.Session = "sp";
                f.SpeedKmh = s.Speed; f.MassT = s.Mass; f.Steer = s.Steer; f.MovingDir = 1;
                f.Vx = 0; f.Vy = 0; f.Vz = s.Speed / 3.6; f.Wx = 0; f.Wy = 0; f.Wz = 0;
                f.Wheels = n; f.Contacts = n; f.Surface = s.Surface;
                f.Rpm = s.Rpm; f.EngineLoad = s.Load; f.EngineOn = true; f.HasEngine = true; f.MotorState = 3;
                f.WheelX = s.X; f.WheelZ = s.Z; f.WheelSteerA = s.SteerA; f.WheelSteer = Math.Abs(s.SteerA[0]);
                f.WheelContact = new bool[n]; f.Susp = new double[n];
                for (int i = 0; i < n; i++)
                {
                    f.WheelContact[i] = true;
                    // pequena ondulacao do piso + um buraco numa roda no fim da sequencia
                    f.Susp[i] = 0.01 * Math.Sin(k * 0.9 + i);
                    if (i == s.BumpWheel && k >= 150) f.Susp[i] += (k % 2 == 0) ? 0.06 : -0.06;
                }
                if (s.BumpWheel >= 0 && k >= 150) { f.SuspSpreadRate = 3.0; f.VertAcc = 6.0; }
                try { r = eng.Update(f, 10, true, 1.0 / 60, st); } catch { r = null; }
                if (r != null && k >= 100)
                {
                    maxBump = Math.Max(maxBump, Math.Abs(r.BumpNm)); maxTex = Math.Max(maxTex, r.TextureNm);
                    maxEng = Math.Max(maxEng, r.EngineNm); maxSpring = Math.Max(maxSpring, r.SpringNm);
                }
                if (k >= 140)
                {
                    v.Frame = f; v.Active = true;
                    if (r != null)
                    {
                        v.Steer = r.SpringNm; v.Weight = 0; v.Bump = r.BumpNm; v.Texture = r.TextureNm; v.Engine = r.EngineNm;
                        v.CollCount = r.CollCount; v.CollLevel = r.CollLevel; v.CollX = r.CollX; v.CollZ = r.CollZ;
                    }
                    using (Graphics g = Graphics.FromImage(bmp)) v.PaintTo(g);
                }
            }
            info = string.Format(CultureInfo.InvariantCulture, "centering {0:0.00} Nm | surface {1:0.00} Nm | bumps {2:0.00} Nm | engine {3:0.00} Nm",
                maxSpring, maxTex, maxBump, maxEng);
            v.Dispose();
            return bmp;
        }
    }
}
