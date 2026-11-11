// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - camada de abstracao dos volantes (contratos comuns).
//
//   IEffectSink   : o que um dispositivo com efeitos de force feedback sabe fazer (spring, damper,
//                   friction, constant, sine x2, paragem). Implementado pelo dispositivo DirectInput real
//                   (FfbDevice) e pelo dispositivo simulado dos testes (SimulatedEffectDevice).
//   IFfbAdapter   : traduz um ForceRequest (Nm) para um IEffectSink. Ha dois:
//                   DiConditionAdapter (efeitos de condicao da base) e DiTorqueAdapter (binario
//                   calculado pela app, so constant force; experimental).
//   DeviceDescriptor / Capabilities / ValidationLevel : o que se sabe de cada dispositivo.
// A fisica e o misturador nao conhecem nenhum destes tipos de dispositivo concretos.
using System;
using System.Collections.Generic;

namespace FS25RealFFB
{
    internal enum DeviceKind { Wheelbase, Pedals, Shifter, Handbrake, Gamepad, Joystick, Other }

    // C: confirmado em hardware real; D: API documentada, implementado, nao testado fisicamente;
    // T: teoricamente possivel; E: experimental; N: nao suportado; U: desconhecido (sem entrada no catalogo).
    internal enum ValidationLevel { Confirmed, Documented, Theoretical, Experimental, NotSupported, Unknown }

    internal enum EffectStatus { NotDeclared, Declared, Created, Playing, Failed }

    internal sealed class DeviceDescriptor
    {
        public string Interface = "DirectInput";
        public string Product = "";
        public int Vid, Pid;
        public string InstanceId = "";        // GUID de instancia do DirectInput (distingue duas bases iguais)
        public string Connection = "USB";
        public DeviceKind Kind = DeviceKind.Other;
        public bool HasForceFeedback;
        public int DevType, DevSubType;       // tipo e subtipo DirectInput (para a classificacao)

        public string VidPid { get { return Vid.ToString("X4") + ":" + Pid.ToString("X4"); } }
        public override string ToString() { return Product + " (" + VidPid + ")"; }
    }

    internal sealed class Capabilities
    {
        public bool ForceFeedback;
        public int FfSamplePeriodUs, FfMinTimeResolutionUs;
        public bool GainProperty, AutocenterProperty;
        public readonly Dictionary<string, EffectStatus> Effects = new Dictionary<string, EffectStatus>();
        public readonly List<string> DeclaredEffectNames = new List<string>();

        public static readonly string[] Names = { "Constant", "Spring", "Damper", "Friction", "Sine" };

        public EffectStatus Get(string name)
        {
            EffectStatus s;
            return Effects.TryGetValue(name, out s) ? s : EffectStatus.NotDeclared;
        }

        public void Mark(string name, EffectStatus st)
        {
            EffectStatus old = Get(name);
            if ((int)st > (int)old || st == EffectStatus.Failed) Effects[name] = st;
        }

        public bool Usable(string name) { EffectStatus s = Get(name); return s == EffectStatus.Playing || s == EffectStatus.Created; }

        // "condicoes" completas: a base faz centragem, amortecimento e atrito sozinha
        public bool ConditionsUsable { get { return Usable("Spring") && Usable("Damper"); } }

        public string Summary()
        {
            List<string> p = new List<string>();
            foreach (string n in Names) p.Add(n + "=" + Get(n));
            return string.Join(" ", p.ToArray()) + (FfSamplePeriodUs > 0 ? " | FF sample period " + FfSamplePeriodUs + " us" : "")
                + " | gain property " + (GainProperty ? "yes" : "no") + " | autocenter property " + (AutocenterProperty ? "yes" : "no");
        }
    }

    internal interface IEffectSink
    {
        bool IsOpen { get; }
        bool Responding { get; }
        string DeviceName { get; }
        Capabilities Caps { get; }
        void SetSpring(int coef, int sat, int offset);
        void SetDamper(int coef, int sat);
        void SetFriction(int coef, int sat);
        void SetConstant(int mag);
        void SetSine(int mag, int periodUs);
        void SetEngine(int mag, int periodUs);
        bool Poll(out int position);        // le a posicao do volante agora (-10000..10000)
        void EmergencyStop();
        string Health { get; }
    }

    internal interface IFfbAdapter : IDisposable
    {
        string Name { get; }
        string Version { get; }
        ValidationLevel Level { get; }      // nivel de validacao do proprio adaptador
        // envia o pedido; devolve o que foi enviado (para o registo e o trace)
        FfbOutput Apply(ForceRequest r, double wheelPos, double dt, Settings s);
        void Stop();
    }
}
