// FS25 Real FFB - motor de force feedback (v0.1, perfil "estilo Euro Truck").
// Componentes, todas derivadas da telemetria:
//   Centragem  : mola na base; rigidez e forca crescem com a velocidade e a massa;
//                reduzida em piso de menor aderencia e com as rodas no ar.
//   Amortecim. : amortecedor na base; estabilidade e peso, cresce com a velocidade.
//   Atrito     : atrito na base; direcao "pesada" parado/devagar, maior em veiculos pesados.
//   Pancadas   : forca constante a partir da diferenca de suspensao entre rodas do 1.o eixo
//                (ou da aceleracao de rolamento, se a suspensao nao estiver disponivel).
//   Textura    : seno cuja amplitude vem da energia de aceleracao vertical do chassis
//                (o piso irregular sente-se porque o chassis vibra, nao por tabela fixa).
// Todas as forcas somadas nunca passam do limite: se passarem, sao escaladas em conjunto
// (nenhum efeito esconde os outros).
using System;

namespace FS25RealFFB
{
    internal sealed class FfbOutput
    {
        public int SpringOffset;
        public int EngineMag, EnginePeriodUs = 25000; public double EngineNm;
        public double PathSteer, SteerRatio, Wheelbase; public bool PathCalibrated;
        public int SpringCoef, SpringSat, DamperCoef, DamperSat, FrictionCoef, FrictionSat, ConstMag, SineMag, SinePeriodUs;
        public double SpringNm, DamperNm, FrictionNm, BumpNm, TextureNm, Gain, Grip;
        public string State = "";
    }

    internal sealed class FfbEngine
    {
        private double gain;
        private double bump;
        private double vertEnergy;
        private long lastSeq = -1;
        private double prevGameMs, prevHeave = double.NaN, prevHeaveRate, prevVz = double.NaN, prevVx = double.NaN;
        private double impactEnv, collEnv, joltPhase;
        private int collSign;
        public double SentSteer;         // direcao que a app envia ao jogo (mesma convencao do volante)

        // Auto-calibracao da trajetoria (sem dados inventados: tudo medido no proprio jogo)
        private double ratio = 0.6;      // angulo da roda (rad) por unidade de input de direcao
        private double wheelbase = 3.0;  // m
        private double yawSignAcc;       // concordancia de sinal entre yaw e angulo da roda
        private int calibSamples;
        private double pathOffset;       // centro da mola (unidades de input -1..1), filtrado

        private static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }
        private static bool Ok(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }

        private static double Roughness(string s)
        {
            switch (s)
            {
                case "road": return 0.12;
                case "hard": return 0.35;
                case "field": return 0.6;
                case "soft": return 0.45;
                case "snow": return 0.2;
                default: return 0.25;
            }
        }

        private static double SurfaceGrip(string s)
        {
            switch (s)
            {
                case "road": return 1.0;
                case "hard": return 0.9;
                case "field": return 0.75;
                case "soft": return 0.7;
                case "snow": return 0.55;
                case "water": return 0.5;
                case "air": return 0.15;
                default: return 0.9;
            }
        }

        public FfbOutput Update(TelemetryFrame f, double ageMs, bool enabled, double dt, Settings s)
        {
            FfbOutput o = new FfbOutput();
            bool fresh = f != null && ageMs < 300;
            bool active = fresh && f.Active;

            double target = (enabled && active) ? 1.0 : 0.0;
            if (gain < target) gain = Math.Min(target, gain + dt / 1.0);   // entrada suave: 1 s
            else gain = Math.Max(target, gain - dt / 0.25);                // saida rapida: 0,25 s
            o.Gain = gain;

            if (!enabled) o.State = "FFB desligado";
            else if (!fresh) o.State = "a aguardar telemetria do FS25";
            else if (!f.Active) o.State = "pausa / fora do veiculo";
            else o.State = "ATIVO";

            if (f == null || !active)
            {
                bump = 0;
                o.SinePeriodUs = 100000;
                return Finish(o, s);
            }

            double v = Ok(f.Vz) ? Math.Abs(f.Vz) : (Ok(f.SpeedKmh) ? Math.Abs(f.SpeedKmh) / 3.6 : 0.0);
            double vRef = Math.Max(1.0, s.RefSpeedKmh / 3.6);
            double vn = Clamp(v / vRef, 0, 1);
            double massT = Ok(f.MassT) && f.MassT > 0.1 ? f.MassT : 5.0;
            double massF = Clamp(Math.Sqrt(massT / 8.0), 0.6, 1.5);
            double grip = SurfaceGrip(f.Surface);
            o.Grip = grip;

            // Centragem (alinhamento): so existe com o trator em andamento;
            // parado, o volante fica onde o deixar. Direcao hidraulica de trator = leve.
            double speedF = Math.Pow(Clamp((v - 0.4) / vRef, 0, 1), 0.7);
            o.SpringNm = s.MaxNm * s.Center * 0.6 * speedF * Math.Sqrt(massF) * grip;
            // Rigidez tambem a zero quando parado: alguns drivers tratam saturacao 0 como "sem limite",
            // o que puxava o volante para o centro com o trator parado.
            o.SpringCoef = (int)(10000 * (0.15 + 0.45 * vn) * speedF);

            // O centro da mola segue a TRAJETORIA real: o angulo de roda que corresponde a curva
            // que o trator esta de facto a fazer (yaw rate). Em reta -> centro; em curva -> o
            // volante assenta no angulo da curva e vai endireitando com o trator.
            double vz = Ok(f.Vz) ? f.Vz : 0, yaw = Ok(f.Wy) ? f.Wy : 0, sIn = SentSteer, d = f.WheelSteer;
            if (f.Seq != lastSeq && Math.Abs(vz) > 2.0 && Math.Abs(sIn) > 0.08 && Math.Abs(d) > 0.03)
            {
                ratio += (d / sIn - ratio) * 0.02;
                if (Math.Abs(yaw) > 0.03)
                {
                    double L = Math.Tan(Math.Abs(d)) * Math.Abs(vz) / Math.Abs(yaw);
                    if (L > 1.5 && L < 8) wheelbase += (L - wheelbase) * 0.02;
                    yawSignAcc += Math.Sign(yaw * d * Math.Sign(vz));
                    calibSamples++;
                }
            }
            double pathTarget = 0;
            bool calibrated = calibSamples > 40 && Math.Abs(yawSignAcc) > 25 && Math.Abs(ratio) > 0.05;
            if (calibrated && Math.Abs(vz) > 0.5)
            {
                double dPath = Math.Sign(yawSignAcc) * Math.Atan(wheelbase * yaw / vz);
                pathTarget = Clamp(dPath / ratio, -1, 1) * Clamp(Math.Abs(vz) / 3.0, 0, 1);
            }
            pathOffset += (pathTarget - pathOffset) * Clamp(dt / 0.15, 0, 1);
            o.PathSteer = pathOffset; o.SteerRatio = ratio; o.Wheelbase = wheelbase; o.PathCalibrated = calibrated;
            double offUnits = pathOffset / Math.Max(0.1, s.SteerSens) * (s.SteerInvert ? -1 : 1);
            // Comportamento pedido (e o de um carro real): o volante volta ao centro a medida que
            // o veiculo rola, com forca proporcional a velocidade; parado, nao volta.
            // O centro e sempre o volante direito (o seguimento de trajetoria prendia o volante).
            o.SpringOffset = 0;

            // Amortecimento
            o.DamperNm = s.MaxNm * s.Damping * (0.3 + 0.7 * vn);
            o.DamperCoef = (int)(10000 * Clamp(s.Damping * (0.3 + 0.5 * vn), 0, 1));

            // Atrito (direcao pesada a baixa velocidade)
            o.FrictionNm = s.MaxNm * s.Friction * 0.4 * (1.0 - 0.8 * vn) * massF;

            // Pancadas, buracos e choques (tudo a partir de dados medidos a cada amostra nova):
            //  - torcao: diferenca de velocidade de suspensao esquerda/direita do 1.o eixo -> puxao lateral
            //  - buraco/lomba: variacao brusca da compressao media do 1.o eixo -> "tum" curto no volante
            //  - choque: desaceleracao longitudinal/lateral acima do que o trator consegue fazer sozinho
            if (f.Seq != lastSeq)
            {
                lastSeq = f.Seq;
                double fdt = prevGameMs > 0 ? (f.GameTimeMs - prevGameMs) / 1000.0 : 0;
                prevGameMs = f.GameTimeMs;

                double kick;
                if (f.Susp.Length >= 2 && Math.Abs(f.SuspSpreadRate) > 1e-6) kick = Clamp(f.SuspSpreadRate / 0.25, -1, 1);
                else kick = Clamp(f.RollAcc / 3.0, -1, 1);
                if (Math.Abs(kick) < 0.04) kick = 0;
                if (Math.Abs(kick) > Math.Abs(bump)) bump = kick;

                if (fdt > 0.002 && fdt < 0.2)
                {
                    if (f.Susp.Length >= 2 && Ok(f.Susp[0]) && Ok(f.Susp[1]))
                    {
                        double heave = (f.Susp[0] + f.Susp[1]) * 0.5;
                        double heaveRate = Ok(prevHeave) ? (heave - prevHeave) / fdt : 0;
                        double jerk = Math.Abs(heaveRate - prevHeaveRate);
                        prevHeave = heave; prevHeaveRate = heaveRate;
                        if (jerk > 0.15) impactEnv = Math.Max(impactEnv, Clamp(jerk / 0.8, 0.2, 1));
                    }
                    if (Ok(f.Vz) && Ok(prevVz) && Ok(f.Vx) && Ok(prevVx))
                    {
                        double ax = (f.Vz - prevVz) / fdt, ay = (f.Vx - prevVx) / fdt;
                        double am = Math.Sqrt(ax * ax + ay * ay);
                        if (am > 7.0)
                        {
                            collEnv = Math.Max(collEnv, Clamp((am - 7.0) / 10.0 + 0.3, 0.3, 1));
                            collSign = Math.Abs(ay) > 1 ? Math.Sign(ay) : (collSign == 0 ? 1 : -collSign);
                        }
                    }
                }
                prevVz = f.Vz; prevVx = f.Vx;

                double a = Ok(f.VertAcc) ? f.VertAcc : 0;
                vertEnergy += (a * a - vertEnergy) * 0.15;
            }
            bump *= Math.Exp(-dt / 0.08);
            impactEnv *= Math.Exp(-dt / 0.09);
            collEnv *= Math.Exp(-dt / 0.16);
            joltPhase += dt * 2 * Math.PI * 16.0;
            if (joltPhase > 2 * Math.PI) joltPhase -= 2 * Math.PI;
            double jolt = s.Bumps * s.MaxNm * (0.45 * impactEnv * Math.Sin(joltPhase)
                        + 0.8 * collEnv * (0.6 * collSign + 0.4 * Math.Sin(joltPhase * 0.7)));
            o.BumpNm = (s.Bumps * s.MaxNm * 0.6 * bump + jolt) * (s.InvertBumps ? -1 : 1);

            // Textura do piso = rugosidade da classe de piso (aproximacao) + vibracao vertical medida.
            // Asfalto: zumbido dos tacos do pneu agricola (frequencia = rotacao da roda x ~38 tacos).
            // Terra/campo: ondulacao mais grossa e de frequencia mais baixa.
            double rms = Math.Sqrt(Math.Max(0, vertEnergy));
            double rough = Roughness(f.Surface);
            double sf = Clamp(v / 4.0, 0, 1);
            o.TextureNm = s.Texture * s.MaxNm * 0.3 * (rough * sf + 0.5 * Clamp(rms / 2.0, 0, 1) * Clamp(v / 1.0, 0, 1));
            double hz;
            if (f.Surface == "road") hz = Clamp(v / (2 * Math.PI * 0.85) * 38.0, 5, 60);
            else hz = Clamp(6 + v * 1.5, 6, 25);
            o.SinePeriodUs = (int)(1000000 / hz);

            // Motor: vibracao a frequencia de combustao (6 cilindros, 4 tempos: rpm/20 Hz),
            // um pouco mais forte com carga; parado e ao ralenti sente-se melhor.
            if (f.EngineOn && Ok(f.Rpm) && f.Rpm > 200)
            {
                double load = Ok(f.EngineLoad) ? Clamp(f.EngineLoad, 0, 1) : 0.3;
                o.EngineNm = s.Engine * 0.12 * (0.7 + 0.5 * load) * (1.0 - 0.4 * vn);
                double ehz = Clamp(f.Rpm / 20.0, 15, 150);
                o.EnginePeriodUs = (int)(1000000 / ehz);
            }

            return Finish(o, s);
        }

        private FfbOutput Finish(FfbOutput o, Settings s)
        {
            // ganho de entrada/saida
            o.SpringNm *= gain; o.DamperNm *= gain; o.FrictionNm *= gain; o.BumpNm *= gain; o.TextureNm *= gain;

            // limite global: soma das forcas nunca excede MaxNm
            double total = o.SpringNm + o.FrictionNm + Math.Abs(o.BumpNm) + o.TextureNm;
            if (total > s.MaxNm && total > 0)
            {
                double k = s.MaxNm / total;
                o.SpringNm *= k; o.FrictionNm *= k; o.BumpNm *= k; o.TextureNm *= k;
            }
            if (o.DamperNm > s.MaxNm) o.DamperNm = s.MaxNm;

            o.SpringSat = s.ToMag(o.SpringNm);
            o.DamperSat = s.ToMag(o.DamperNm);
            o.FrictionCoef = o.FrictionSat = s.ToMag(o.FrictionNm);
            o.ConstMag = s.ToSignedMag(o.BumpNm);
            o.SineMag = s.ToMag(o.TextureNm);
            o.EngineNm *= gain;
            o.EngineMag = s.ToMag(o.EngineNm);
            if (gain <= 0) { o.SpringCoef = 0; o.DamperCoef = 0; }
            return o;
        }
    }
}
