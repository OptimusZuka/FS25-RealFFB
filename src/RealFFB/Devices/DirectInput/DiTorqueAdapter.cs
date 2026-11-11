// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - adaptador "binario calculado" (EXPERIMENTAL).
//
// Para volantes onde os efeitos de condicao (spring/damper/friction) faltam, nao funcionam bem, ou sao
// atenuados pelo software da marca (ex.: sliders de efeitos DirectInput no Simucube True Drive, SPR/DPR
// no menu Fanatec, efeitos do MOZA Pit House). Aqui a app calcula ela propria
//     T = centragem(x) + amortecimento(v) + atrito(v) + pancadas
// e envia so CONSTANT FORCE, numa thread propria a ~500 Hz, lendo a posicao do volante a cada passo.
// As vibracoes (textura, motor) continuam nos efeitos sine se o volante os tiver; se nao, sao somadas.
//
// Seguranca propria: pedido com mais de 150 ms (janela bloqueada) -> binario a zero; leitura da posicao
// falhada ha mais de 50 ms -> binario a zero; limite de "potencia": o binario que acelera o volante
// na direcao em que ja gira depressa e reduzido (evita o volante a disparar sozinho).
// Interface: DirectInput 8, efeito ConstantForce (padrao USB HID PID "Constant Force").
using System;
using System.Diagnostics;
using System.Threading;

namespace FS25RealFFB
{
    // Calculo puro (sem threads nem dispositivo): testavel com o VirtualWheel.
    internal sealed class TorqueModel
    {
        public double DampScale = 1.0;      // Nm por (eixo/s) por unidade de DamperK, x pico
        public double FrictionWidth = 0.02; // eixo/s: transicao suave do atrito seco
        public double CenterWidth = 0.08;   // centragem plena a ~8% do curso (~43 graus a 1080), como no modo condicoes
        public double PowerLimitSpeed = 3.0;// eixo/s

        private static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }

        // pos: -1..1; vel: eixo/s; devolve Nm (positivo = mesmo sentido do eixo)
        public double Torque(ForceRequest r, double pos, double vel, double peakNm, double capNm, bool gearLike, bool addVibration, double t)
        {
            double dx = pos - r.SpringOffset;
            double spring;
            if (r.Follow || gearLike)
            {
                // mola linear (como uma "spring" DI): rigidez relativa x pico, limitada por SpringNm
                double cf = Math.Sqrt(DiConditionMapper.TunedBaseNm / Math.Max(1.0, peakNm)) * (gearLike && !r.Follow ? 1.5 : 1.0);
                spring = Clamp(-r.SpringK * cf * dx * peakNm, -r.SpringNm, r.SpringNm);
            }
            else
                spring = -Math.Sign(dx) * r.SpringNm * Math.Tanh(Math.Abs(dx) / CenterWidth) * (r.SpringK > 0.02 ? 1 : 0);
            double damp = Clamp(-r.DamperK * DampScale * vel * peakNm, -r.DamperNm, r.DamperNm);
            double fric = -r.FrictionNm * Math.Tanh(vel / FrictionWidth);
            double tq = spring + damp + fric + r.BumpNm;
            if (addVibration)
                tq += r.TextureNm * Math.Sin(2 * Math.PI * r.TextureHz * t) + r.EngineNm * Math.Sin(2 * Math.PI * r.EngineHz * t);
            // limite de "potencia": nao empurrar mais o volante no sentido em que ja gira muito depressa
            if (Math.Abs(vel) > PowerLimitSpeed && Math.Sign(tq) == Math.Sign(vel))
                tq *= Clamp(1 - (Math.Abs(vel) - PowerLimitSpeed) / PowerLimitSpeed, 0, 1);
            return Clamp(tq, -capNm, capNm);
        }
    }

    internal sealed class DiTorqueAdapter : IFfbAdapter
    {
        private readonly IEffectSink sink;
        private readonly TorqueModel model = new TorqueModel();
        private readonly object gate = new object();
        private ForceRequest req = ForceRequest.Zero();
        private long reqAtMs;
        private Settings settings;
        private Thread thread;
        private volatile bool running, paused;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private double lastTorque;
        public int LoopHz { get; private set; }
        public int StaleStops { get; private set; }
        public bool UseSineEffects = true;

        public DiTorqueAdapter(IEffectSink sink) { this.sink = sink; }

        public string Name { get { return "DirectInput - computed torque (experimental)"; } }
        public string Version { get { return "0.1"; } }
        public ValidationLevel Level { get { return ValidationLevel.Experimental; } }

        public void Start()
        {
            if (thread != null) return;
            running = true;
            thread = new Thread(Loop);
            thread.IsBackground = true;
            thread.Name = "RealFFB torque";
            thread.Priority = ThreadPriority.Highest;
            thread.Start();
        }

        public FfbOutput Apply(ForceRequest r, double wheelPos, double dt, Settings s)
        {
            lock (gate) { req = r.Clone(); reqAtMs = clock.ElapsedMilliseconds; settings = s; paused = false; }
            if (thread == null) Start();
            FfbOutput o = new FfbOutput();
            o.CopyInfo(r);
            o.Adapter = "torque";
            o.TorqueNm = lastTorque;
            // vibracoes pelos efeitos sine, se existirem
            bool sineOk = UseSineEffects && sink != null && sink.Caps != null && sink.Caps.Usable("Sine");
            if (sink != null && sink.IsOpen && sineOk)
            {
                o.SineMag = s.ToMag(r.TextureNm); o.SinePeriodUs = (int)(1000000 / Math.Max(1.0, r.TextureHz));
                o.EngineMag = s.ToMag(r.EngineNm); o.EnginePeriodUs = (int)(1000000 / Math.Max(1.0, r.EngineHz));
                sink.SetSine(o.SineMag, o.SinePeriodUs);
                sink.SetEngine(o.EngineMag, o.EnginePeriodUs);
            }
            return o;
        }

        private void Loop()
        {
            double prevPos = double.NaN, vel = 0, prevT = clock.Elapsed.TotalSeconds;
            long lastGoodRead = clock.ElapsedMilliseconds;
            int count = 0; long hzStart = clock.ElapsedMilliseconds;
            // condicoes desligadas: neste modo a base so recebe constant force (e sine)
            try { if (sink != null && sink.IsOpen) { sink.SetSpring(0, 0, 0); sink.SetDamper(0, 0); sink.SetFriction(0, 0); } } catch { }
            while (running)
            {
                Thread.Sleep(1);   // com timeBeginPeriod(1) (a app ja o pede) da ~500-1000 Hz
                if (sink == null || !sink.IsOpen || paused) continue;
                ForceRequest r; Settings s; long at;
                lock (gate) { r = req; s = settings; at = reqAtMs; }
                if (s == null) continue;
                double t = clock.Elapsed.TotalSeconds;
                double dt = Math.Max(0.0005, t - prevT); prevT = t;
                int raw;
                bool ok = false;
                try { ok = sink.Poll(out raw); } catch { raw = 0; }
                long now = clock.ElapsedMilliseconds;
                if (ok) lastGoodRead = now;
                bool stale = now - at > 150 || now - lastGoodRead > 50;
                double tq = 0;
                if (!stale && ok)
                {
                    double pos = raw / 10000.0;
                    double vNow = double.IsNaN(prevPos) ? 0 : (pos - prevPos) / dt;
                    prevPos = pos;
                    vel += (vNow - vel) * Math.Min(1, dt / 0.004);   // filtro ~4 ms
                    bool sineOk = UseSineEffects && sink.Caps != null && sink.Caps.Usable("Sine");
                    tq = model.Torque(r, pos, vel, s.BaseNm, s.ForceCapNm, s.GearDriven, !sineOk, t);
                }
                else if (stale && Math.Abs(lastTorque) > 0.01) StaleStops++;
                lastTorque = tq;
                // envio sob o mesmo lock que Stop(): depois de Stop() nao sai mais nenhum comando
                lock (gate)
                {
                    if (paused) continue;
                    try { sink.SetConstant(s.ToSignedMag(tq)); } catch { }
                }
                count++;
                if (now - hzStart >= 1000) { LoopHz = count; count = 0; hzStart = now; }
            }
        }

        public void Stop()
        {
            lock (gate)
            {
                req = ForceRequest.Zero(); paused = true;
                lastTorque = 0;
                if (sink != null) sink.EmergencyStop();
            }
        }

        public void Dispose()
        {
            running = false;
            if (thread != null) { try { thread.Join(200); } catch { } thread = null; }
        }
    }
}
