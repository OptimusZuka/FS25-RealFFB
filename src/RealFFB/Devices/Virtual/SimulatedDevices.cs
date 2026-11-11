// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - dispositivos SIMULADOS, so para testes automaticos.
// Um teste com estes dispositivos verifica a logica da app; NUNCA conta como teste em hardware real.
//
// SimulatedEffectDevice: implementa IEffectSink e regista tudo o que recebe. Perfis:
//   Standard        todos os efeitos funcionam
//   NoConditions    so constant e sine (spring/damper/friction nao existem)
//   AcceptsNotPlays cria os efeitos mas o estado fica "parado" (ha bases assim)
//   DropsAfterN     desliga-se ao fim de N comandos (desconexao USB)
//   Failing         todos os comandos falham
// VirtualWheel: volante fisico simulado (inercia, atrito, binario maximo) para testar estabilidade.
using System;
using System.Collections.Generic;

namespace FS25RealFFB
{
    internal enum SimProfile { Standard, NoConditions, AcceptsNotPlays, DropsAfterN, Failing }

    internal sealed class SimulatedEffectDevice : IEffectSink
    {
        public readonly SimProfile Profile;
        public int DropAfter = 50;
        public int Commands, Rejected;
        public bool Stopped;
        public int SpringCoef, SpringSat, SpringOffset, DamperCoef, DamperSat, FrictionCoef, FrictionSat, Constant, SineMag, EngineMag;
        public int MaxAbsCommand;           // maior valor absoluto recebido em qualquer efeito
        public int Position;                // -10000..10000 (os testes mexem aqui ou ligam um VirtualWheel)
        public VirtualWheel Wheel;
        public readonly List<string> Log = new List<string>();
        private bool open = true;
        private readonly Capabilities caps = new Capabilities();

        public SimulatedEffectDevice(SimProfile p)
        {
            Profile = p;
            foreach (string n in Capabilities.Names)
            {
                bool cond = n == "Spring" || n == "Damper" || n == "Friction";
                if (p == SimProfile.NoConditions && cond) continue;
                caps.Mark(n, EffectStatus.Declared);
                caps.Mark(n, EffectStatus.Created);
                if (p != SimProfile.AcceptsNotPlays) caps.Mark(n, EffectStatus.Playing);
            }
            caps.ForceFeedback = true;
        }

        public bool IsOpen { get { return open; } }
        public bool Responding { get { return open && Profile != SimProfile.Failing; } }
        public string DeviceName { get { return "Simulated " + Profile; } }
        public Capabilities Caps { get { return caps; } }
        public string Health { get { return "commands " + Commands + " rejected " + Rejected; } }

        private bool Accept(string what, int a, int b)
        {
            if (!open) return false;
            Commands++;
            if (Profile == SimProfile.DropsAfterN && Commands > DropAfter) { open = false; Log.Add("disconnected"); return false; }
            if (Profile == SimProfile.Failing) { Rejected++; return false; }
            MaxAbsCommand = Math.Max(MaxAbsCommand, Math.Max(Math.Abs(a), Math.Abs(b)));
            Stopped = false;
            return true;
        }

        public void SetSpring(int coef, int sat, int offset)
        {
            if (Profile == SimProfile.NoConditions) { Rejected++; return; }
            if (Accept("spring", coef, sat)) { SpringCoef = coef; SpringSat = sat; SpringOffset = offset; }
        }
        public void SetDamper(int coef, int sat)
        {
            if (Profile == SimProfile.NoConditions) { Rejected++; return; }
            if (Accept("damper", coef, sat)) { DamperCoef = coef; DamperSat = sat; }
        }
        public void SetFriction(int coef, int sat)
        {
            if (Profile == SimProfile.NoConditions) { Rejected++; return; }
            if (Accept("friction", coef, sat)) { FrictionCoef = coef; FrictionSat = sat; }
        }
        public void SetConstant(int mag) { if (Accept("constant", mag, 0)) Constant = mag; }
        public void SetSine(int mag, int periodUs) { if (Accept("sine", mag, 0)) SineMag = mag; }
        public void SetEngine(int mag, int periodUs) { if (Accept("engine", mag, 0)) EngineMag = mag; }

        public bool Poll(out int position)
        {
            if (Wheel != null) Position = (int)Math.Round(Math.Max(-1, Math.Min(1, Wheel.Pos)) * 10000);
            position = Position;
            return open && Profile != SimProfile.Failing;
        }

        public void EmergencyStop()
        {
            Stopped = true;
            SpringCoef = SpringSat = DamperCoef = DamperSat = FrictionCoef = FrictionSat = Constant = SineMag = EngineMag = 0;
            Log.Add("stop");
        }
    }

    // Volante simulado: J * a = T - b * v - atrito(v). Posicao -1..1 (limites duros). Unidades: Nm, eixo/s.
    internal sealed class VirtualWheel
    {
        public double Inertia = 0.35;       // Nm*s^2 por unidade de eixo (~0,04 kg m2 de aro+rotor; 1 unidade = 540 graus)
        public double Damping = 0.3;        // amortecimento mecanico proprio (Nm por eixo/s)
        public double Coulomb = 0.02;       // atrito seco proprio (Nm)
        public double MaxTorque = 8.0;
        public double Pos, Vel;

        public void Step(double torqueNm, double dt)
        {
            double t = Math.Max(-MaxTorque, Math.Min(MaxTorque, torqueNm));
            double fr = Math.Abs(Vel) > 1e-4 ? Math.Sign(Vel) * Coulomb : 0;
            double acc = (t - Damping * Vel - fr) / Inertia;
            Vel += acc * dt;
            Pos += Vel * dt;
            if (Pos > 1) { Pos = 1; Vel = 0; }
            if (Pos < -1) { Pos = -1; Vel = 0; }
        }
    }
}
