// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - adaptador "efeitos de condicao" (o modo de sempre, migrado 1:1 de FfbEngine.Finish).
//
// A base faz a centragem (spring), o amortecimento (damper) e o atrito (friction) ela propria, a ~1 kHz;
// a app atualiza os parametros a ~100 Hz. Pancadas = constant force; textura e motor = dois sine.
// Interface: DirectInput 8 (efeitos padrao; nos dispositivos HID PID o Windows traduz para o padrao USB PID).
// Referencias: Microsoft Docs, "DirectInput - Using Force Feedback" e "DICONDITION".
//
// Particularidades de dispositivo que vivem aqui (e nao na fisica):
//  - normalizacao da rigidez pelo torque de pico (as afinacoes foram feitas numa base de 8 Nm):
//    cf = sqrt(8 / pico); transmissao por engrenagens/correia: rigidez x1,5;
//  - centragem moldada (direct drive): o centro da mola desloca-se a cada ciclo para que, na posicao
//    atual, a mola faca sat * tanh(|x| / 0,08) (a mola DI linear dava pouca forca perto do centro);
//    + ajuda ao regresso (compensa o Natural Damper da base) e travao a chegada ao centro.
using System;

namespace FS25RealFFB
{
    internal sealed class DiConditionMapper
    {
        public const double TunedBaseNm = 8.0;
        private const double CenterShapeU = 800.0;
        private double prevPos = double.NaN, turnFac = 1.0, lastVel, lastDt = 0.016;

        private static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }
        private static bool Ok(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }

        public void Reset() { prevPos = double.NaN; turnFac = 1.0; lastVel = 0; }

        public FfbOutput Map(ForceRequest r, double wheelPos, double dt, Settings s)
        {
            lastDt = dt > 0 ? dt : 0.016;
            FfbOutput o = new FfbOutput();
            o.CopyInfo(r);
            double cf = Math.Sqrt(TunedBaseNm / Math.Max(1.0, s.PeakNm));
            o.SpringCoef = (int)Math.Min(10000, (int)(10000 * r.SpringK) * cf * (s.GearDriven ? 1.5 : 1.0));
            o.SpringSat = s.ToMag(r.SpringNm);
            o.SpringOffset = (int)Math.Round(Clamp(r.SpringOffset, -1, 1) * 10000);
            o.DamperCoef = (int)(10000 * r.DamperK);
            o.DamperSat = s.ToMag(r.DamperNm);
            o.FrictionCoef = o.FrictionSat = s.ToMag(r.FrictionNm);
            o.ConstMag = s.ToSignedMag(r.BumpNm);
            o.SineMag = s.ToMag(r.TextureNm);
            o.SinePeriodUs = (int)(1000000 / Math.Max(1.0, r.TextureHz));
            o.EngineMag = s.ToMag(r.EngineNm);
            o.EnginePeriodUs = (int)(1000000 / Math.Max(1.0, r.EngineHz));
            if (!s.GearDriven && !r.Follow)
            {
                int sat = o.SpringSat;
                o.SpringOffset = ShapeCentering(o.SpringCoef, ref sat, wheelPos, s.ToMag(s.ForceCapNm));
                o.SpringSat = sat;
                // travao so a chegada ao centro (ultimos ~25 graus, volante a chegar depressa)
                if (o.SpringCoef > 0 && Ok(wheelPos))
                {
                    double nearC = 1 - Clamp(Math.Abs(wheelPos) / 0.046, 0, 1);
                    double fast = Clamp((Math.Abs(lastVel) - 0.15) / 0.45, 0, 1);
                    double add = nearC * fast;
                    if (add > 0)
                    {
                        o.DamperCoef = (int)Math.Max(o.DamperCoef, 6000 * add);
                        o.DamperSat = Math.Max(o.DamperSat, s.ToMag(0.6 * add));
                    }
                }
            }
            return o;
        }

        private int ShapeCentering(int coef, ref int satU, double wheelPos, int capU)
        {
            if (coef < 200 || satU <= 0 || !Ok(wheelPos)) { prevPos = double.NaN; turnFac = 1.0; return 0; }
            double x = Clamp(wheelPos, -1, 1) * 10000.0;
            double ax = Math.Abs(x);
            double vel = Ok(prevPos) ? (wheelPos - prevPos) / Math.Max(0.002, lastDt) : 0;
            prevPos = wheelPos;
            lastVel = vel;
            double toward = -vel * Math.Sign(x);
            double fade = Clamp((ax / 10000.0 - 0.02) / 0.10, 0, 1);
            double tgt = 1.0 + 0.8 * Clamp(toward / 0.15, 0, 1) * fade;
            turnFac += (tgt - turnFac) * Clamp(lastDt / 0.08, 0, 1);
            // 0.13: a ajuda ao regresso nunca passa o teto ("Maximum force" / pico): antes podia chegar a 1,8x
            satU = (int)Math.Min(capU, satU * turnFac);
            double want = satU * Math.Tanh(ax / CenterShapeU);
            if (ax < 1) return 0;
            double off = x - Math.Sign(x) * want * 10000.0 / coef;
            return (int)Math.Round(Clamp(off, -10000, 10000));
        }
    }

    internal sealed class DiConditionAdapter : IFfbAdapter
    {
        private readonly IEffectSink sink;
        private readonly DiConditionMapper mapper = new DiConditionMapper();

        public DiConditionAdapter(IEffectSink sink) { this.sink = sink; }

        public string Name { get { return "DirectInput - condition effects"; } }
        public string Version { get { return "1.0"; } }
        public ValidationLevel Level { get { return ValidationLevel.Confirmed; } }   // validado na CSL DD (0.6-0.12)

        public FfbOutput Apply(ForceRequest r, double wheelPos, double dt, Settings s)
        {
            FfbOutput o = mapper.Map(r, wheelPos, dt, s);
            o.Adapter = "conditions";
            if (sink == null || !sink.IsOpen) return o;
            sink.SetSpring(o.SpringCoef, o.SpringSat, o.SpringOffset);
            sink.SetDamper(o.DamperCoef, o.DamperSat);
            sink.SetFriction(o.FrictionCoef, o.FrictionSat);
            sink.SetConstant(o.ConstMag);
            sink.SetSine(o.SineMag, o.SinePeriodUs);
            sink.SetEngine(o.EngineMag, o.EnginePeriodUs);
            return o;
        }

        public void Stop()
        {
            mapper.Reset();
            if (sink != null) sink.EmergencyStop();
        }

        public void Dispose() { }
    }
}
