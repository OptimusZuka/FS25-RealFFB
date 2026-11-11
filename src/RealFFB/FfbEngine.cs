// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - motor de force feedback.
// Componentes, todas derivadas da telemetria:
//   Centragem  : mola na base; rigidez e forca crescem com a velocidade e a massa;
//                reduzida em piso de menor aderencia e com as rodas no ar.
//   Amortecim. : amortecedor na base; estabilidade e peso, cresce com a velocidade.
//   Atrito     : atrito na base; direcao "pesada" parado/devagar, maior em veiculos pesados.
//   Pancadas   : forca constante a partir da diferenca de suspensao entre rodas do 1.o eixo
//                (ou da aceleracao de rolamento, se a suspensao nao estiver disponivel).
//   Textura    : seno cuja amplitude vem da energia de aceleracao vertical do chassis
//                (o piso irregular sente-se porque o chassis vibra, nao por tabela fixa).
//
// 0.13 (arquitetura universal): esta e a camada de FISICA. Devolve um ForceRequest em Nm e
// coeficientes normalizados (0..1), sem unidades nem tipos de DirectInput. A mistura final
// (multiplicador, ganho, limites, prioridades, limite de variacao) esta em Ffb/FfbMixer.cs e a
// traducao para o volante esta nos adaptadores (Devices/DirectInput/*). Ver docs/dev.
using System;

namespace FS25RealFFB
{
    internal sealed class FfbEngine
    {
        private double gain;
        private double bump;
        private double vertEnergy;
        private long lastSeq = -1;
        private double prevGameMs, prevHeave = double.NaN, prevHeaveRate, prevVz = double.NaN, prevVx = double.NaN;
        private double impactEnv, collEnv, joltPhase;
        private double bumpOut;   // pancada suavizada (todos os volantes; ataque mais lento nas engrenagens)
        // textura: ruido filtrado para a amplitude e a frequencia nao serem uma sinusoide perfeita
        private readonly Random rng = new Random();
        private double texNoise, texNoiseTarget, texNoiseT, texJitter;
        // motor: rpm suavizado (a telemetria chega a ~20 Hz) e media lenta para sentir aceleracoes
        private double rpmSm = double.NaN, rpmSlow = double.NaN;
        // 0.12: arranque do motor (motor de arranque + "pegar")
        private double crankT, catchEnv, crankPhaseN; private int lastMotorState = -1;
        private int collSign, collCount;
        private double collLevel, collX, collZ, collDv;
        // historico curto da velocidade (m/s) para medir quanto ela mudou nos ultimos ~0,12 s
        private readonly double[] hVx = new double[16], hVz = new double[16], hT = new double[16];
        private int hN, hI; private string histVeh = "";
        public double WheelPos = double.NaN; // posicao bruta do volante (-1..1 do eixo), para moldar a centragem
        public double SentSteer;    // direcao que a app envia ao jogo (mesma convencao do volante)
        public double BrakeInput;   // pedal de travao 0..1 (para nao confundir travagem com embate)
        // 0.12: centragem realista. 1 = centra (normal); 0 = este veiculo nao centra na vida real (direcao
        // hidrostatica, articulada, rastos): sem mola, e o peso viscoso do orbitrol fica a qualquer velocidade.
        public double CenterFactor = 1.0;
        private double centerSm = 1.0;   // transicao suave (~0,5 s) ao trocar de veiculo ou de opcao

        // 0.12.1: STOP/panico. Antes, depois do STOP o ganho descia em 0,25 s e a mola so podia baixar
        // ~2,4 Nm/s (limite de seguranca), e o ciclo voltava a ligar os efeitos logo a seguir: a centragem
        // demorava ate ~1 s a desaparecer. Agora tudo vai a zero no mesmo instante.
        public void Halt()
        {
            gain = 0; bump = 0; bumpOut = 0; centerSm = CenterFactor;
            Following = false; TakeOver = false; followOverride = false; followTgt = double.NaN;
        }

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

        public ForceRequest Update(TelemetryFrame f, double ageMs, bool enabled, double dt, Settings s)
        {
            ForceRequest o = new ForceRequest();
            bool fresh = f != null && ageMs < 300;
            bool active = fresh && f.Active;

            double target = (enabled && active) ? 1.0 : 0.0;
            // entrada suave: 0,3 s (0.10.0; era 1 s e somava-se ao arranque do motor no jogo -> "3 s ate vibrar",
            // relatado num G29). A mola continua limitada a ~2,4 Nm/s em Finish, por isso nao ha tranco.
            if (gain < target) gain = Math.Min(target, gain + dt / 0.3);
            else gain = Math.Max(target, gain - dt / 0.25);                // saida rapida: 0,25 s
            o.Gain = gain;

            if (!enabled) o.State = "FFB off";
            else if (!fresh) o.State = "waiting for FS25 telemetry";
            else if (!f.Active) o.State = "paused / out of vehicle";
            else o.State = "ACTIVE";

            if (f == null || !active)
            {
                bump = 0; bumpOut = 0;
                o.TextureHz = 10;
                Following = false; TakeOver = false; followOverride = false; followTgt = double.NaN; followFar = 0; followAge = 0;
                return o;
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
            // Direct drive: a centragem cresce mais cedo (plena a ~60% da velocidade de referencia) e
            // com mais rigidez, para o volante voltar ao centro a andar em ~1 s, como num trator real.
            // Logitech: curva da 0.6.2, ja validada pelo utilizador.
            double speedF = s.GearDriven ? Math.Pow(Clamp((v - 0.4) / vRef, 0, 1), 0.7)
                                         : Math.Pow(Clamp((v - 0.4) / (0.4 * vRef), 0, 1), 0.5);
            // Rigidez da mola (DD) com rampa propria e mais rapida: a rigidez define a forca PERTO do
            // centro; a saturacao (SpringNm) define o peso maximo numa curva longa.
            double stiffF = Math.Sqrt(Clamp((v - 0.4) / (0.3 * vRef), 0, 1));
            o.SpringNm = s.MaxNm * s.Center * (s.GearDriven ? 0.6 : 0.65) * speedF * Math.Sqrt(massF) * grip;
            // Rigidez tambem a zero quando parado: alguns drivers tratam saturacao 0 como "sem limite",
            // o que puxava o volante para o centro com o trator parado.
            // DD: antes a rigidez (~0,28 a 10 km/h) so atingia a forca maxima com ~80 graus de volante
            // fora do centro; nos ultimos 30-40 graus quase nao havia forca e o volante "morria" a meio
            // caminho -> trajetoria imprevisivel. Agora a forca maxima chega a ~35-45 graus e a forca
            // perto do centro e ~2,5x maior; o peso maximo (saturacao) fica igual.
            o.SpringK = s.GearDriven ? (0.15 + 0.45 * vn) * speedF
                                     : Clamp((0.5 + 0.4 * vn) * stiffF, 0, 1);

            // Comportamento pedido (e o de um carro real): o volante volta ao centro a medida que
            // o veiculo rola, com forca proporcional a velocidade; parado, nao volta.
            // O centro e sempre o volante direito (o seguimento de trajetoria prendia o volante).
            o.SpringOffset = 0;

            // 0.12: centragem realista (ver CenteringRules). A forca maxima da mola ja esta limitada a
            // ~2,4 Nm/s em Finish; o fator tambem muda devagar para nao haver salto de rigidez.
            centerSm += Clamp(Clamp(CenterFactor, 0, 1) - centerSm, -dt / 0.5, dt / 0.5);
            o.SpringNm *= centerSm;
            o.SpringK *= centerSm;
            double noCenter = 1 - centerSm;

            // Direcao hidraulica (orbitrol) parado ou quase parado: o volante nao centra, mas tem peso
            // viscoso (o oleo a passar na valvula) e o pneu arrasta no chao. Plena ate ~1 km/h e some
            // de forma suave ate ~11 km/h: parado sente-se o mesmo peso que a 1 km/h, sem "desligar".
            // DD: o peso de parado some ate ~4 km/h (antes ~11 km/h). O log mostrou que a 4-10 km/h, onde
            // se manobra quase sempre, amortecimento+atrito (~0,45 Nm) igualavam a centragem: o volante
            // pesava a virar, parava a meio a voltar, e o atrito comia a textura do piso.
            double hx = s.GearDriven ? Clamp((v - 0.3) / 2.7, 0, 1) : Clamp((v - 0.3) / 0.9, 0, 1);
            double hyd = 1 - hx * hx * (3 - 2 * hx);
            // sem centragem (orbitrol): o peso viscoso da valvula nao depende da velocidade
            hyd = Math.Max(hyd, noCenter);

            // Amortecimento (viscoso): base + direcao hidraulica a baixa velocidade
            o.DamperNm = s.MaxNm * s.Damping * (0.3 + 0.7 * vn)
                       + s.MaxNm * s.Friction * (s.GearDriven ? 1.15 : 0.6) * hyd * massF;
            o.DamperK = (Clamp(s.Damping * (0.3 + 0.5 * vn)
                       + s.Friction * (s.GearDriven ? 2.8 : 0.8) * hyd, 0, 1));

            // Atrito: pneu a arrastar no chao. Nos volantes com engrenagens o peso parado vem sobretudo
            // do amortecimento, porque um atrito forte "come" a vibracao do motor nas engrenagens.
            // Atrito seco e o que torna a direcao "pegajosa" e pesada e come as forcas pequenas (a centragem
            // perto do centro, a textura). Como no ETS2: pouco atrito, e nenhum a velocidade de cruzeiro.
            // Logitech (0.9.7): metade do atrito seco a andar (0.25 -> 0.12). Licao dos testes na Fanatec: o atrito a
            // andar prende o volante e come forcas pequenas; num volante com engrenagens fica uma parte, porque
            // as engrenagens ja tem atrito proprio mas o G29 sem nenhum fica "solto". Parado: igual.
            o.FrictionNm = s.MaxNm * s.Friction * ((s.GearDriven ? 0.12 : 0.15) * (1.0 - vn) + (s.GearDriven ? 0.2 : 0.3) * hyd) * massF;
            if (!s.GearDriven)
            {
                // Perfil direct drive (0.9): a andar NAO ha atrito seco. Testes de 2026-11-08: atrito e
                // amortecimento somados ao Natural Damper da base eram a origem do peso e do retorno lento;
                // o atrito, alem disso, prende o volante (retorno aos solavancos). Parado (< ~1 km/h) fica o
                // atrito do "Peso parado", para o volante nao rodar sozinho.
                double sx = Clamp((v - 0.1) / 0.3, 0, 1);
                o.FrictionNm = s.MaxNm * s.Friction * 0.45 * Math.Max(1 - sx * sx * (3 - 2 * sx), noCenter) * massF;
                // A andar, o amortecimento principal e o da propria base (NDP, 50% de fabrica). A app so
                // acrescenta 30% do seu, para nao somar resistencia por cima (testado: NDP 50 + app 8% =
                // pesado e lento). Parado, o amortecimento hidraulico fica igual.
                double hydPart = s.MaxNm * s.Friction * 0.6 * hyd * massF;
                o.DamperNm = (o.DamperNm - hydPart) * 0.3 + hydPart;
                o.DamperK *= 0.3 + 0.7 * hyd;
            }

            if (s.GearDriven)
            {
                // A andar: menos amortecimento para o volante voltar ao centro mais depressa.
                // (0.12.1: nao num veiculo sem centragem - ai o volante nao volta, e o peso do orbitrol fica.)
                o.DamperK *= 1 - 0.4 * vn * (1 - noCenter);
            }

            // 0.12.1 (centragem realista): motor desligado = sem bomba de direcao. Na direcao hidrostatica o
            // orbitrol passa a funcionar como bomba manual (o volante fica muito pesado, ~3x); na direcao
            // mecanica perde-se a assistencia (~2x). So com a opcao ligada, para nao mudar quem nao a usa.
            if (s.RealisticCentering && f.HasEngine && !f.EngineOn)
            {
                double k = 2.0 + noCenter;
                o.FrictionNm *= k; o.DamperNm *= k;
                o.DamperK = Math.Min(1.0, o.DamperK * k);
            }

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
                        // 0.9.9: embate medido pela VARIACAO DE VELOCIDADE numa janela curta (~0,12 s), nao pela
                        // aceleracao de uma so amostra. Um embate tira >= 7 km/h em ~0,1 s (> 16 m/s2 de media);
                        // uma travagem, mesmo a fundo, fica abaixo de ~8 m/s2 e o "encaixe" final na paragem
                        // (o jogo poe a velocidade a zero de uma vez) e sempre < 2 m/s. Na 0.9.8 bastava um salto
                        // numa amostra, e esse encaixe ou o ruido da telemetria disparavam o embate ao travar.
                        double tS = prevGameMs;   // instante desta amostra (ms de jogo)
                        // troca de veiculo: as velocidades anteriores sao de outro veiculo -> recomeca o historico
                        string veh = f.Vehicle ?? "";
                        if (veh != histVeh) { hN = 0; histVeh = veh; }
                        hVx[hI] = f.Vx; hVz[hI] = f.Vz; hT[hI] = tS; hI = (hI + 1) % hVx.Length; if (hN < hVx.Length) hN++;
                        int best = -1;
                        for (int k = 0; k < hN; k++)
                        {
                            double age = (tS - hT[k]) / 1000.0;
                            if (age >= 0.06 && age <= 0.14 && (best < 0 || hT[k] < hT[best])) best = k;
                        }
                        if (best >= 0)
                        {
                            double dz = f.Vz - hVz[best], dx = f.Vx - hVx[best];
                            double dv = Math.Sqrt(dz * dz + dx * dx);
                            double need = BrakeInput > 0.15 ? 2.6 : 2.0;   // m/s (7 km/h; 9,4 km/h a travar)
                            if (dv > need)
                            {
                                double lvl = Clamp(dv / 6.0, 0.3, 1);
                                // novo embate (nao a cauda do anterior): regista onde foi, para o desenho.
                                // A velocidade muda no sentido contrario ao ponto de embate: (-dx, -dz).
                                if (collEnv < 0.25) { collCount++; collLevel = lvl; collDv = dv; collX = -dx / dv; collZ = -dz / dv; }
                                else if (lvl > collLevel) collLevel = lvl;
                                collEnv = Math.Max(collEnv, lvl);
                                collSign = Math.Abs(dx) > 1 ? Math.Sign(dx) : (collSign == 0 ? 1 : -collSign);
                            }
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
            joltPhase += dt * 2 * Math.PI * (s.GearDriven ? 9.0 : 16.0);
            if (joltPhase > 2 * Math.PI) joltPhase -= 2 * Math.PI;
            double jolt = s.Bumps * s.MaxNm * (0.35 * impactEnv * Math.Sin(joltPhase)
                        + (s.GearDriven ? 0.8 : 1.2) * collEnv * (0.6 * collSign + 0.4 * Math.Sin(joltPhase * 0.7)));
            // 0.12.1: sem ligacao mecanica (hidrostatica/articulada) os solavancos das rodas nao chegam ao
            // volante (nao ha "coice" da direcao); so os embates fortes, que abanam a cabine toda.
            o.BumpNm = (s.Bumps * s.MaxNm * 0.6 * bump * (1 - 0.8 * noCenter) + jolt * (1 - 0.4 * noCenter)) * (s.InvertBumps ? -1 : 1);
            o.CollCount = collCount; o.CollDv = collDv; o.CollLevel = collLevel; o.CollX = collX; o.CollZ = collZ;
            // Logitech: os efeitos sao relativos a forca maxima, por isso num G29 (2,2 Nm) as pancadas
            // chegavam perto do maximo do volante e soavam brutas. Testado pelo utilizador: ~30-40% fica bem.
            if (s.GearDriven) o.BumpNm *= 0.4;
            {
                // A telemetria chega a ~20 Hz: sem filtro cada pancada e um degrau de forca, que numa
                // base direct drive soa a "clique" digital e nas engrenagens a "pancada seca".
                // Subida rapida (DD ~12 ms, engrenagens ~35 ms) e descida curta; a amplitude mantem-se.
                bool rising = Math.Abs(o.BumpNm) > Math.Abs(bumpOut);
                double tau = s.GearDriven ? (rising ? 0.035 : 0.06) : (rising ? 0.012 : 0.03);
                bumpOut += (o.BumpNm - bumpOut) * Clamp(dt / tau, 0, 1);
                o.BumpNm = bumpOut;
            }

            // Textura do piso = rugosidade da classe de piso (aproximacao) + vibracao vertical medida.
            // Asfalto: zumbido dos tacos do pneu agricola (frequencia = rotacao da roda x ~38 tacos).
            // Terra/campo: ondulacao mais grossa e de frequencia mais baixa.
            double rms = Math.Sqrt(Math.Max(0, vertEnergy));
            double rough = Roughness(f.Surface);
            // raiz: a baixa velocidade (2-5 km/h no campo) ja se sente o piso, sem esperar pelos 15 km/h
            double sf = Math.Sqrt(Clamp(v / 4.0, 0, 1));
            // Piso real nao e uniforme: ruido aleatorio filtrado (~0,15 s) modula a amplitude.
            // Asfalto quase constante; terra/campo bem irregulares (torroes, sulcos, pedras).
            texNoiseT -= dt;
            if (texNoiseT <= 0) { texNoiseTarget = rng.NextDouble() * 2 - 1; texNoiseT = 0.08 + rng.NextDouble() * 0.12; }
            texNoise += (texNoiseTarget - texNoise) * Clamp(dt / 0.06, 0, 1);
            double depth = f.Surface == "road" ? 0.15 : (f.Surface == "snow" ? 0.25 : 0.45);
            double mod = 1.0 + depth * texNoise;
            o.TextureNm = s.Texture * s.MaxNm * 0.3 * mod * (rough * sf + 0.5 * Clamp(rms / 2.0, 0, 1) * Clamp(v / 1.0, 0, 1));
            if (s.GearDriven) o.TextureNm *= 0.4;
            else o.TextureNm *= 1.3;   // DD: mais informacao do piso (sem o atrito a come-la)
            double hz;
            if (f.Surface == "road") hz = Clamp(v / (2 * Math.PI * 0.85) * 38.0, 5, 60);
            else
            {
                hz = s.GearDriven ? Clamp(5 + v * 1.0, 5, 14) : Clamp(6 + v * 1.5, 6, 25);
                // fora de estrada a frequencia varia +-12% com o mesmo ruido (passo de 1 Hz para nao
                // reenviar parametros ao volante a cada ciclo)
                texJitter += (texNoise - texJitter) * Clamp(dt / 0.25, 0, 1);
                hz = Math.Round(hz * (1.0 + 0.12 * texJitter));
                if (hz < 4) hz = 4;
            }
            o.TextureHz = hz;

            // Motor: vibracao a frequencia de combustao (6 cilindros, 4 tempos: rpm/20 Hz),
            // um pouco mais forte com carga; parado e ao ralenti sente-se melhor.
            // 0.12: o jogo so poe o motor "ligado" e da rotacoes no fim da sequencia de arranque (~2-3 s).
            // Antes o volante ficava mudo esse tempo. Agora, mal a chave roda (estado "a arrancar"), sente-se o
            // motor de arranque: sacudidelas irregulares a ~9-12 Hz (as compressoes dos cilindros), e quando o
            // motor pega, um esticao curto que assenta no ralenti.
            bool cranking = f.MotorState == 2;   // (mesmo que o jogo ja diga "ligado": ainda nao ha rotacoes)
            if (f.MotorState == 3 && lastMotorState == 2) catchEnv = 1.0;   // pegou
            if (f.MotorState >= 0) lastMotorState = f.MotorState;
            catchEnv *= Math.Exp(-dt / 0.25);
            crankT = cranking ? crankT + dt : 0;
            // ate as rotacoes chegarem: ralenti assumido para nao haver um buraco entre "pegou" e as primeiras rotacoes
            double rpmIn = Ok(f.Rpm) && f.Rpm > 200 ? f.Rpm : (f.EngineOn ? 750.0 : double.NaN);
            if (cranking)
            {
                rpmSm = double.NaN; rpmSlow = double.NaN;
                // irregular: a amplitude pulsa com cada compressao (~2,5 por segundo por cilindro "dificil")
                crankPhaseN += dt * 2 * Math.PI * 2.6;
                double pulse = 0.65 + 0.35 * Math.Abs(Math.Sin(crankPhaseN));
                double rampIn = Clamp(crankT / 0.15, 0, 1);
                o.EngineNm = s.Engine * (s.GearDriven ? 1.0 : 0.55) * pulse * rampIn;
                double chz = (s.GearDriven ? 9.0 : 11.0) + 1.5 * Math.Sin(crankPhaseN * 0.37);
                o.EngineHz = Math.Round(chz);
            }
            else if (f.EngineOn && Ok(rpmIn))
            {
                double rpmF = rpmIn;
                double load = Ok(f.EngineLoad) ? Clamp(f.EngineLoad, 0, 1) : 0.3;
                // rpm suavizado: a telemetria vem aos degraus (~20 Hz); assim a frequencia sobe e desce
                // de forma continua ao acelerar, como num motor real
                if (!Ok(rpmSm)) { rpmSm = rpmF; rpmSlow = rpmF; }
                rpmSm += (rpmF - rpmSm) * Clamp(dt / 0.08, 0, 1);
                rpmSlow += (rpmSm - rpmSlow) * Clamp(dt / 0.5, 0, 1);
                double rpmN = Clamp((rpmSm - 850.0) / (2200.0 - 850.0), 0, 1);
                // acelerar em vazio ("dar gas" parado) sacode o trator: reacao de binario
                double rev = Clamp((rpmSm - rpmSlow) / 400.0, 0, 1);
                // Amplitude em Nm: ralenti ja se sente numa base de 8 Nm (~0,15 Nm com o slider a 50%),
                // cresce com as rotacoes e a carga; em andamento o piso e o volante abafam um pouco.
                // Parado o motor domina (cabine e coluna vibram com o trator imovel); em andamento o piso
                // e o rolamento mascaram-no: 1,3x parado, a cair ate 0,4x a partir de ~14 km/h.
                double engMove = 1.3 - 0.9 * Clamp(v / 4.0, 0, 1);
                o.EngineNm = s.Engine * (0.30 + 0.25 * rpmN + 0.20 * load + 0.25 * rev) * engMove;
                // frequencia de combustao (6 cilindros, 4 tempos: rpm/20 Hz): ralenti ~42 Hz, 2200 rpm ~110 Hz
                double ehz = Clamp(rpmSm / 20.0, 15, 150);
                if (s.GearDriven)
                {
                    // Engrenagens: forcas abaixo de ~0,4 Nm perdem-se na folga e no atrito interno, por isso
                    // o motor tem uma base maior (ralenti ~0,45 Nm com o slider a 50%) e frequencia mais baixa
                    // (ralenti ~17 Hz, regime alto ~44 Hz).
                    o.EngineNm = s.Engine * (0.9 + 0.25 * rpmN + 0.15 * load + 0.25 * rev) * engMove;
                    ehz = Clamp(rpmSm / 50.0, 14, 45);
                }
                ehz = Math.Round(ehz);
                o.EngineHz = ehz;
                // motor acabou de pegar: esticao curto por cima do ralenti
                if (catchEnv > 0.02) o.EngineNm += s.Engine * (s.GearDriven ? 0.8 : 0.6) * catchEnv;
            }
            else { rpmSm = double.NaN; rpmSlow = double.NaN; }

            // Mistura: os efeitos nao se devem empilhar (era o "tudo ao mesmo tempo" que embrulhava a
            // sensacao). Como o ouvido num carro, o volante destaca o evento mais forte:
            //  - durante uma pancada a textura e o motor baixam ate metade (a pancada le-se limpa);
            //  - quando o piso vibra, o motor recua para segundo plano (fica a ~40% em piso forte).
            double duck = Clamp(Math.Abs(o.BumpNm) / (0.3 * Math.Max(0.5, s.MaxNm)), 0, 1);
            o.TextureNm *= 1 - 0.5 * duck;
            o.EngineNm *= 1 - 0.5 * duck;
            if (o.TextureNm + o.EngineNm > 1e-6) o.EngineNm *= 1 - 0.6 * o.TextureNm / (o.TextureNm + o.EngineNm);

            ApplyFollow(f, o, s, dt);
            return o;
        }

        // ------------------------------------------------------------------ 0.11: seguir a IA / o GPS
        // Quando um trabalhador de IA (ou Courseplay/AutoDrive) ou o GPS do jogo conduz, o volante roda
        // sozinho para o angulo de direcao do jogo: mola com o centro no alvo, rigidez maxima, forca
        // limitada a 2 Nm (e ao "Maximum force"), sem atrito. Pancadas, piso e motor continuam.
        // 0.11.2: o trace de 2026-11-09 mostrou o volante a ir atras do alvo a ~0,45 do eixo/s (o alvo anda ate
        // 1,0/s numa curva de cabeceira) com 0,15-0,65 de atraso -> a 0.11.1 achava que era o condutor e
        // dispensava a IA sozinha. Agora so conta como "assumir" se o volante NAO estiver a avancar para o
        // alvo: parado (seguro) ou a fugir dele (virado ao contrario). Atrasado mas a caminho = seguir.
        // Tambem: menos amortecimento proprio (era 2500 e travava o volante) e forca ate 2,5 Nm.
        // Tomar o controlo: segurar ou virar o volante longe do alvo (> ~80 graus durante 0,5 s). O volante
        // deixa logo de seguir e a app pede ao mod para devolver o trator ao condutor (0.11.1): a IA e
        // dispensada, o GPS e desligado. (0.11.0: com a IA o volante so parava 3 s e voltava a seguir.)
        // O alvo move-se no maximo 1,5 do eixo por segundo (sem trancos).
        public bool Following;          // lido pela MainForm (ponte: o mod nao envia a direcao)
        public bool TakeOver;           // o condutor assumiu: o mod para a IA / desliga o GPS
        private bool followOverride;
        private double followTgt = double.NaN, followFar, followClearT, followAge, followPrevPos = double.NaN, followVel;
        private const double FollowNm = 2.5, FollowSlew = 1.5, FollowGrabErr = 0.15, FollowGrabSec = 0.5, FollowProgress = 0.12;

        private void ApplyFollow(TelemetryFrame f, ForceRequest o, Settings s, double dt)
        {
            bool auto = s.FollowAuto && f.Auto != 0 && Ok(f.Steer) && Ok(WheelPos) && gain > 0.01;
            if (!auto)
            {
                Following = false; TakeOver = false; followOverride = false; followTgt = double.NaN; followFar = 0; followClearT = 0; followAge = 0;
                followPrevPos = double.NaN; followVel = 0;
                return;
            }
            // Medido no trace de 2026-11-09: direcao no jogo = -(direcao enviada); enviada = posicao x sensibilidade (x -1 se invertida)
            double k = s.SteerSens * (s.SteerInvert ? -1 : 1);
            double tgt = Clamp(-f.Steer / k, -1, 1);
            if (!Ok(followTgt)) followTgt = WheelPos;     // comeca onde o volante esta
            double step = FollowSlew * dt;
            followTgt += Clamp(tgt - followTgt, -step, step);

            if (!followOverride)
            {
                // nos primeiros 1,5 s a forca ainda esta a subir (limite de 2,4 Nm/s): o volante pode ir
                // atrasado sem ser o condutor a segurar
                followAge += dt;
                // velocidade do volante (eixo/s), filtrada ~60 ms
                double vNow = Ok(followPrevPos) && dt > 0 ? (WheelPos - followPrevPos) / dt : 0;
                followPrevPos = WheelPos;
                followVel += (vNow - followVel) * Clamp(dt / 0.06, 0, 1);
                double e = followTgt - WheelPos;                           // quanto falta ate ao alvo
                double progress = followVel * Math.Sign(e);                // > 0: a caminho do alvo
                bool held = Math.Abs(e) > FollowGrabErr && progress < FollowProgress;
                followFar = followAge > 1.5 && held ? followFar + dt : 0;
                if (followFar > FollowGrabSec) { followOverride = true; followClearT = 0; }
            }
            Following = !followOverride;
            TakeOver = followOverride;      // fica pedido ate a IA/GPS parar (auto = 0)
            if (!Following) return;

            o.Follow = true;
            o.State = f.Auto == 2 ? "following GPS steering" : "following AI steering";
            o.SpringOffset = Clamp(followTgt, -1, 1);
            o.SpringK = 1.0;
            o.SpringNm = Math.Min(s.MaxNm, FollowNm);
            o.FrictionNm = 0;
            o.DamperNm = 0.25;
            o.DamperK = 0.12;
        }

        // (0.13: a normalizacao por volante, a centragem moldada e a conversao para unidades do
        // dispositivo passaram para Devices/DirectInput/DiConditionMapper.cs; a mistura final para
        // Ffb/FfbMixer.cs.)
    }
}
