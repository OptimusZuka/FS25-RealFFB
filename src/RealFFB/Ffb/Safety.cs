// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - camada de seguranca universal.
//
// Watchdog: o ciclo do FFB corre na thread da janela. Os efeitos de condicao nao tem duracao (ficam a
//   aplicar a ultima forca), por isso, se a janela bloquear (diálogo, disco lento, antivirus), a forca
//   ficava no volante. Uma thread propria verifica o "batimento" do ciclo e, se faltar mais de
//   TimeoutMs com o FFB ligado, para todos os efeitos do volante. O ciclo seguinte volta a liga-los.
//
// OscillationGuard: deteta o volante a oscilar sozinho (inversoes rapidas de velocidade, com amplitude
//   a crescer, perto do centro). Reage durante 2 s: centragem a metade e mais amortecimento. Um unico
//   "passar do centro" ao largar o volante nao conta.
using System;
using System.Diagnostics;
using System.Threading;

namespace FS25RealFFB
{
    internal sealed class Watchdog : IDisposable
    {
        public int TimeoutMs = 150;
        private readonly Action onTimeout;
        private readonly Func<long> clockMs;
        private long lastBeat;
        private volatile bool armed, running;
        private bool tripped;
        private Thread thread;
        public int Trips { get; private set; }

        public Watchdog(Action onTimeout) : this(onTimeout, null) { }

        // clock injetavel para os testes
        public Watchdog(Action onTimeout, Func<long> clockMs)
        {
            this.onTimeout = onTimeout;
            if (clockMs == null) { Stopwatch sw = Stopwatch.StartNew(); this.clockMs = delegate { return sw.ElapsedMilliseconds; }; }
            else this.clockMs = clockMs;
            lastBeat = this.clockMs();
        }

        public void Start()
        {
            if (thread != null) return;
            running = true;
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Name = "RealFFB watchdog";
            thread.Priority = ThreadPriority.AboveNormal;
            thread.Start();
        }

        // chamado em cada ciclo do FFB; armed = o FFB esta a enviar forcas
        public void Beat(bool armed)
        {
            Interlocked.Exchange(ref lastBeat, clockMs());
            this.armed = armed;
            tripped = false;
        }

        // um passo da verificacao (a thread chama a cada 25 ms; os testes chamam diretamente)
        public bool Check()
        {
            if (!armed || tripped) return false;
            long age = clockMs() - Interlocked.Read(ref lastBeat);
            if (age <= TimeoutMs) return false;
            tripped = true;
            Trips++;
            try { onTimeout(); } catch { }
            return true;
        }

        private void Loop()
        {
            while (running)
            {
                Thread.Sleep(25);
                Check();
            }
        }

        public void Dispose() { running = false; }
    }

    internal sealed class OscillationGuard
    {
        // parametros (eixo do volante normalizado -1..1)
        public double MinSpeed = 1.2;        // eixo/s: so inversoes rapidas contam (~1300 graus/s num volante de 1080)
        public double NearCenter = 0.15;     // so perto do centro (~80 graus)
        public double Window = 1.2;          // s
        public int FlipsToTrip = 4;
        public double HoldSeconds = 2.0;

        private double prevPos = double.NaN, vel, lastSign, t;
        private readonly double[] flips = new double[8];
        private int nFlips;
        private double activeUntil = -1;
        public int Trips { get; private set; }
        public bool Active { get { return t < activeUntil; } }

        public void Reset() { prevPos = double.NaN; vel = 0; lastSign = 0; nFlips = 0; activeUntil = -1; }

        // Atualiza com a posicao do volante e, se estiver a oscilar, suaviza o pedido.
        public void Apply(ForceRequest r, double pos, double dt)
        {
            if (dt <= 0) dt = 0.016;
            t += dt;
            if (double.IsNaN(pos)) { prevPos = double.NaN; return; }
            double vNow = double.IsNaN(prevPos) ? 0 : (pos - prevPos) / dt;
            prevPos = pos;
            vel += (vNow - vel) * Math.Min(1, dt / 0.02);
            double sg = Math.Sign(vel);
            if (Math.Abs(vel) > MinSpeed && Math.Abs(pos) < NearCenter && sg != 0 && sg != lastSign)
            {
                if (lastSign != 0)
                {
                    // guardar o instante da inversao
                    if (nFlips < flips.Length) flips[nFlips++] = t;
                    else { Array.Copy(flips, 1, flips, 0, flips.Length - 1); flips[flips.Length - 1] = t; }
                }
                lastSign = sg;
            }
            // contar inversoes dentro da janela
            int recent = 0;
            for (int i = 0; i < nFlips; i++) if (t - flips[i] <= Window) recent++;
            if (recent >= FlipsToTrip && !Active)
            {
                activeUntil = t + HoldSeconds;
                Trips++;
                nFlips = 0;
            }
            if (Active && !r.Follow)
            {
                r.SpringNm *= 0.5;
                r.SpringK *= 0.5;
                r.DamperK = Math.Min(1, r.DamperK + 0.25);
                r.DamperNm = Math.Max(r.DamperNm, 0.15 * Math.Max(0.5, r.SpringNm * 2 + 0.5));
                r.SafetyNote = "oscillation guard";
            }
        }
    }
}
