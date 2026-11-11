// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - pedido de forca universal (independente do fabricante e da API).
//
// E o que a fisica (FfbEngine) e o misturador (FfbMixer) entregam aos adaptadores. Tudo em Nm no eixo
// do volante, ou em coeficientes normalizados 0..1. Nenhum campo esta em unidades DirectInput.
//
//  Centragem: binario maximo SpringNm, rigidez relativa SpringK (0..1) e centro SpringOffset (-1..1
//             do curso do volante; 0 = volante direito). A rigidez segue a convencao das "condition
//             effects" do padrao USB HID PID: 1 = rigidez maxima do dispositivo.
//  Amortecimento: binario maximo DamperNm, coeficiente relativo DamperK (0..1).
//  Atrito seco: FrictionNm.
//  Binario direto: BumpNm (pancadas e embates, com sinal).
//  Vibracoes: textura do piso (TextureNm a TextureHz) e motor (EngineNm a EngineHz).
using System;

namespace FS25RealFFB
{
    internal sealed class ForceRequest
    {
        public double SpringNm, SpringK, SpringOffset;
        public double DamperNm, DamperK;
        public double FrictionNm;
        public double BumpNm;
        public double TextureNm, TextureHz = 10;
        public double EngineNm, EngineHz = 40;

        // estado (para a interface e os registos)
        public double Gain, Grip;
        public string State = "";
        public bool Follow;                 // o volante segue a IA / o GPS (centro deslocado, rigidez maxima)
        public int CollCount; public double CollLevel, CollX, CollZ, CollDv;
        public string SafetyNote = "";      // preenchido pela camada de seguranca (ex.: "oscillation guard")

        public ForceRequest Clone() { return (ForceRequest)MemberwiseClone(); }

        public static ForceRequest Zero()
        {
            ForceRequest r = new ForceRequest();
            r.State = "stopped";
            return r;
        }

        // Soma dos binarios que podem estar ativos ao mesmo tempo (para testes de limite).
        public double PeakDemandNm()
        {
            return Math.Abs(SpringNm) + Math.Abs(FrictionNm) + Math.Abs(BumpNm) + Math.Abs(TextureNm) + Math.Abs(EngineNm);
        }
    }
}
