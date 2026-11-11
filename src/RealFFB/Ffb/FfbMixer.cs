// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - misturador universal (antes estava dentro de FfbEngine.Finish).
// Recebe o pedido da fisica e devolve-o pronto para qualquer adaptador, em Nm:
//   1. multiplicador geral ("Force multiplier");
//   2. ganho de entrada/saida (rampa da fisica);
//   3. limite global com prioridade: a direcao (centragem + atrito) primeiro, pancadas e textura
//      usam a folga que sobra;
//   4. tetos: nada acima de min("Maximum force", torque de pico do volante);
//   5. limite de variacao da centragem: no maximo 0,3 x pico por segundo (2,4 Nm/s numa base de 8 Nm),
//      igual ao comportamento anterior;
//   6. ganho 0 = rigidez e amortecimento a zero.
// Nao sabe nada de DirectInput nem de marcas.
using System;

namespace FS25RealFFB
{
    internal sealed class FfbMixer
    {
        private double lastSpringNm = double.NaN;

        public void Reset() { lastSpringNm = 0; }

        private static double Cap(double nm, double cap) { if (nm < 0) return 0; return nm > cap ? cap : nm; }

        public ForceRequest Finish(ForceRequest o, Settings s, double dt)
        {
            double gain = o.Gain;
            double scale = Math.Max(0.25, Math.Min(4.0, s.ForceScale));
            o.SpringNm *= scale; o.DamperNm *= scale; o.FrictionNm *= scale; o.BumpNm *= scale; o.TextureNm *= scale; o.EngineNm *= scale;

            o.SpringNm *= gain; o.DamperNm *= gain; o.FrictionNm *= gain; o.BumpNm *= gain; o.TextureNm *= gain;

            double steerTot = o.SpringNm + o.FrictionNm;
            if (steerTot > s.MaxNm && steerTot > 0)
            {
                double k = s.MaxNm / steerTot;
                o.SpringNm *= k; o.FrictionNm *= k; steerTot = s.MaxNm;
            }
            double room = Math.Max(0.25 * s.MaxNm, s.MaxNm - steerTot);
            double fx = Math.Abs(o.BumpNm) + o.TextureNm;
            if (fx > room && fx > 0)
            {
                double k = room / fx;
                o.BumpNm *= k; o.TextureNm *= k;
            }
            if (o.DamperNm > s.MaxNm) o.DamperNm = s.MaxNm;

            double cap = s.ForceCapNm;
            o.SpringNm = Cap(o.SpringNm, cap);
            // limite de variacao da centragem (seguranca: troca de veiculo, perda de telemetria, arranque)
            double maxStep = 0.3 * s.BaseNm * Math.Max(0.002, dt);
            if (!double.IsNaN(lastSpringNm) && Math.Abs(o.SpringNm - lastSpringNm) > maxStep)
                o.SpringNm = lastSpringNm + Math.Sign(o.SpringNm - lastSpringNm) * maxStep;
            lastSpringNm = o.SpringNm;

            o.DamperNm = Cap(o.DamperNm, cap);
            o.FrictionNm = Cap(o.FrictionNm, cap);
            o.TextureNm = Cap(o.TextureNm, cap);
            o.EngineNm *= gain;
            o.EngineNm = Cap(o.EngineNm, cap);
            if (o.BumpNm > cap) o.BumpNm = cap; else if (o.BumpNm < -cap) o.BumpNm = -cap;
            if (gain <= 0) { o.SpringK = 0; o.DamperK = 0; }
            o.SpringK = Math.Max(0, Math.Min(1, o.SpringK));
            o.DamperK = Math.Max(0, Math.Min(1, o.DamperK));
            o.SpringOffset = Math.Max(-1, Math.Min(1, o.SpringOffset));
            return o;
        }
    }
}
