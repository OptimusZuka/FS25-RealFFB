// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB - comandos enviados a um dispositivo de efeitos (unidades DirectInput / HID PID,
// -10000..10000). Antes de 0.13 era a saida do motor de fisica; agora e so a saida dos adaptadores.
// Os campos em Nm sao copiados do ForceRequest para a interface e os registos.
namespace FS25RealFFB
{
    internal sealed class FfbOutput
    {
        public int SpringOffset;
        public int EngineMag, EnginePeriodUs = 25000; public double EngineNm;
        public int SpringCoef, SpringSat, DamperCoef, DamperSat, FrictionCoef, FrictionSat, ConstMag, SineMag, SinePeriodUs = 100000;
        public double SpringNm, DamperNm, FrictionNm, BumpNm, TextureNm, Gain, Grip;
        public int CollCount; public double CollLevel, CollX, CollZ, CollDv;
        public string State = "";
        public bool Follow;
        public double TorqueNm;      // so no modo torque: binario total enviado
        public string Adapter = "";
        public string SafetyNote = "";

        public void CopyInfo(ForceRequest r)
        {
            SpringNm = r.SpringNm; DamperNm = r.DamperNm; FrictionNm = r.FrictionNm; BumpNm = r.BumpNm;
            TextureNm = r.TextureNm; EngineNm = r.EngineNm; Gain = r.Gain; Grip = r.Grip;
            CollCount = r.CollCount; CollLevel = r.CollLevel; CollX = r.CollX; CollZ = r.CollZ; CollDv = r.CollDv;
            State = r.State; Follow = r.Follow; SafetyNote = r.SafetyNote;
        }
    }
}
