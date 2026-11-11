// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - recetor de telemetria (named pipe \\.\pipe\FS25RealFFB).
// Linha do mod: RFFB|1|seq=..|t=..|act=1|veh=..|spd=..|mass=..|steer=..|dir=..|vx=..|..|w=c,surf,susp,steer;...
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace FS25RealFFB
{
    internal sealed class TelemetryFrame
    {
        public long Seq;
        public double GameTimeMs;
        public bool Active;
        public string Vehicle = "";
        public double SpeedKmh = double.NaN, MassT = double.NaN, Steer = double.NaN, MovingDir = double.NaN;
        public double Vx = double.NaN, Vy = double.NaN, Vz = double.NaN;
        public double Wx = double.NaN, Wy = double.NaN, Wz = double.NaN;
        public int Wheels, Contacts;
        public string Surface = "-";
        public double Rpm = double.NaN, EngineLoad = double.NaN;
        public bool EngineOn;
        public bool HasEngine;          // 0.12.1: o mod enviou o estado do motor
        public int MotorState = -1;     // 0.12: 0 desligado, 1 ignicao, 2 a arrancar, 3 a trabalhar, -1 desconhecido
        public double WheelSteer;       // angulo de direcao da roda mais virada (rad)
        public double SuspSpreadRate;   // velocidade de diferenca de suspensao entre rodas (m/s)
        public double VertAcc, RollAcc; // derivados entre amostras (m/s2, rad/s2)
        public double[] Susp = new double[0];
        public double[] WheelX = new double[0], WheelZ = new double[0];   // posicao no veiculo (m): x esquerda, z frente
        public double[] WheelSteerA = new double[0];
        public bool[] WheelContact = new bool[0];
        public string VehicleType = "";
        public string Category = "";   // 0.12: categoria da loja do jogo
        public bool Articulated, Crawler;   // 0.12: direcao articulada / rastos
        public int Auto;                // 0 jogador, 1 trabalhador de IA, 2 GPS do jogo (0.11)
    }

    internal sealed class TelemetryServer
    {
        public const string PipeName = "FS25RealFFB";

        private readonly Action<string> log;
        private readonly object gate = new object();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private Thread thread;
        private volatile bool running;
        private TelemetryFrame latest;
        private TelemetryFrame previous;
        private long latestAtMs = -1;
        private long frameCount;
        private string lastVehicle = "";
        private bool firstLineLogged;

        public volatile bool Connected;

        public TelemetryServer(Action<string> log) { this.log = log; }

        public long FrameCount { get { return Interlocked.Read(ref frameCount); } }

        public void Start()
        {
            running = true;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "Telemetria";
            thread.Start();
        }

        public void Stop() { running = false; }

        public TelemetryFrame GetLatest(out double ageMs)
        {
            lock (gate)
            {
                ageMs = latestAtMs < 0 ? double.MaxValue : clock.ElapsedMilliseconds - latestAtMs;
                return latest;
            }
        }

        private void Run()
        {
            while (running)
            {
                try
                {
                    using (NamedPipeServerStream pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
                    {
                        IAsyncResult ar = pipe.BeginWaitForConnection(null, null);
                        while (running && !ar.AsyncWaitHandle.WaitOne(250)) { }
                        if (!running) break;
                        pipe.EndWaitForConnection(ar);
                        Connected = true;
                        Post("FS25 connected to telemetry.");
                        using (StreamReader reader = new StreamReader(pipe, Encoding.UTF8))
                        {
                            string line;
                            while (running && (line = reader.ReadLine()) != null) Handle(line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Post("Telemetry: " + ex.GetType().Name + " - " + ex.Message);
                    Thread.Sleep(500);
                }
                finally
                {
                    if (Connected) Post("FS25 disconnected from telemetry.");
                    Connected = false;
                }
            }
        }

        private void Post(string s) { try { log(s); } catch { } }

        private static double Num(string s)
        {
            double v;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return double.NaN;
        }

        private void Handle(string line)
        {
            string[] parts = line.Split('|');
            if (parts.Length < 2 || parts[0] != "RFFB") return;
            if (!firstLineLogged) { firstLineLogged = true; Post("First line: " + line); }

            TelemetryFrame f = new TelemetryFrame();
            string wheels = null;
            for (int i = 2; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq <= 0) continue;
                string k = parts[i].Substring(0, eq);
                string v = parts[i].Substring(eq + 1);
                switch (k)
                {
                    case "seq": f.Seq = (long)Num(v); break;
                    case "t": f.GameTimeMs = Num(v); break;
                    case "act": f.Active = v == "1"; break;
                    case "veh": f.Vehicle = v.Trim(); break;
                    case "vtype": f.VehicleType = v.Trim(); break;
                    case "cat": f.Category = v.Trim(); break;
                    case "art": f.Articulated = v == "1"; break;
                    case "crawl": f.Crawler = v == "1"; break;
                    case "spd": f.SpeedKmh = Num(v); break;
                    case "mass": f.MassT = Num(v); break;
                    case "steer": f.Steer = Num(v); break;
                    case "dir": f.MovingDir = Num(v); break;
                    case "vx": f.Vx = Num(v); break;
                    case "vy": f.Vy = Num(v); break;
                    case "vz": f.Vz = Num(v); break;
                    case "wx": f.Wx = Num(v); break;
                    case "wy": f.Wy = Num(v); break;
                    case "wz": f.Wz = Num(v); break;
                    case "w": wheels = v; break;
                    case "rpm": f.Rpm = Num(v); break;
                    case "load": f.EngineLoad = Num(v); break;
                    case "eng": f.EngineOn = v == "1"; f.HasEngine = true; break;
                    case "mst": { double m = Num(v); f.MotorState = double.IsNaN(m) ? -1 : (int)m; } break;
                    case "auto": { double a = Num(v); f.Auto = double.IsNaN(a) ? 0 : (int)a; } break;
                }
            }
            if (wheels != null) ParseWheels(f, wheels);

            TelemetryFrame p = previous;
            if (p != null && f.Active && p.Active)
            {
                double dt = (f.GameTimeMs - p.GameTimeMs) / 1000.0;
                if (dt > 0.002 && dt < 0.25)
                {
                    if (!double.IsNaN(f.Vy) && !double.IsNaN(p.Vy)) f.VertAcc = (f.Vy - p.Vy) / dt;
                    if (!double.IsNaN(f.Wz) && !double.IsNaN(p.Wz)) f.RollAcc = (f.Wz - p.Wz) / dt;
                    if (f.Susp.Length >= 2 && p.Susp.Length == f.Susp.Length)
                    {
                        // diferenca esquerda/direita do primeiro eixo (rodas 1 e 2), em m/s
                        double a = (f.Susp[0] - p.Susp[0]) / dt, b = (f.Susp[1] - p.Susp[1]) / dt;
                        if (!double.IsNaN(a) && !double.IsNaN(b)) f.SuspSpreadRate = a - b;
                    }
                }
            }
            previous = f;

            if (f.Active && f.Vehicle != lastVehicle)
            {
                lastVehicle = f.Vehicle;
                Post(string.Format(CultureInfo.InvariantCulture, "Vehicle: {0} | mass {1:0.0} t | wheels {2}", f.Vehicle, f.MassT, f.Wheels));
            }

            lock (gate)
            {
                latest = f;
                latestAtMs = clock.ElapsedMilliseconds;
            }
            Interlocked.Increment(ref frameCount);
        }

        private static void ParseWheels(TelemetryFrame f, string s)
        {
            if (s.Length == 0) return;
            string[] ws = s.Split(';');
            Dictionary<string, int> count = new Dictionary<string, int>();
            List<double> susp = new List<double>(), wx = new List<double>(), wz = new List<double>(), wst = new List<double>();
            List<bool> wc = new List<bool>();
            foreach (string w in ws)
            {
                string[] c = w.Split(',');
                if (c.Length < 3) continue;
                f.Wheels++;
                if (c[0] == "1")
                {
                    f.Contacts++;
                    int n;
                    count.TryGetValue(c[1], out n);
                    count[c[1]] = n + 1;
                }
                susp.Add(Num(c[2]));
                wc.Add(c[0] == "1");
                wst.Add(c.Length >= 4 ? Num(c[3]) : double.NaN);
                wx.Add(c.Length >= 6 ? Num(c[4]) : double.NaN);
                wz.Add(c.Length >= 6 ? Num(c[5]) : double.NaN);
                if (c.Length >= 4)
                {
                    double wsa = Num(c[3]);
                    if (!double.IsNaN(wsa) && Math.Abs(wsa) > Math.Abs(f.WheelSteer)) f.WheelSteer = wsa;
                }
            }
            f.Susp = susp.ToArray();
            f.WheelX = wx.ToArray(); f.WheelZ = wz.ToArray(); f.WheelSteerA = wst.ToArray(); f.WheelContact = wc.ToArray();
            int best = 0;
            foreach (KeyValuePair<string, int> kv in count)
                if (kv.Value > best) { best = kv.Value; f.Surface = kv.Key; }
            if (f.Contacts == 0) f.Surface = "air";
        }
    }
}
