// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - testes automaticos da camada universal de FFB (sem hardware).
// Correm no build.bat (build\FfbTests.exe). Usam dispositivos SIMULADOS: passar aqui prova a logica,
// nao a compatibilidade com um volante real (isso so com o teste guiado num volante verdadeiro).
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace FS25RealFFB.Tests
{
    internal static class TestMain
    {
        private static int pass, fail;
        private static readonly List<string> failures = new List<string>();

        private static void Ok(string name, bool cond)
        {
            if (cond) pass++;
            else { fail++; failures.Add(name); Console.WriteLine("FAIL " + name); }
        }

        private static Settings Dd()
        {
            Settings s = new Settings();
            s.BaseNm = 8; s.MaxNm = 3; s.GearDriven = false; s.ForceScale = 1;
            return s;
        }

        private static Settings Gear()
        {
            Settings s = new Settings();
            s.BaseNm = 2.2; s.MaxNm = 2.2; s.GearDriven = true; s.ForceScale = 1;
            return s;
        }

        private static ForceRequest Req(double spring, double k, double damp, double dk, double fric, double bump, double tex, double eng)
        {
            ForceRequest r = new ForceRequest();
            r.Gain = 1; r.SpringNm = spring; r.SpringK = k; r.DamperNm = damp; r.DamperK = dk; r.FrictionNm = fric;
            r.BumpNm = bump; r.TextureNm = tex; r.TextureHz = 30; r.EngineNm = eng; r.EngineHz = 40; r.State = "ACTIVE";
            return r;
        }

        [STAThread]
        public static int Main()
        {
            Console.WriteLine("FS25 RealFFB - FFB tests (simulated devices; not a hardware test)");
            PhysicsIsDeviceIndependent();
            MixerLimits();
            ConditionMapperParity();
            ContractConditionAdapter();
            ContractTorqueAdapter();
            TorqueModelLimits();
            TorqueStabilityVirtualWheel();
            WatchdogTrips();
            OscillationGuardTest();
            CatalogTests();
            ClassificationTests();
            SettingsScaling();
            Console.WriteLine("passed " + pass + ", failed " + fail);
            foreach (string f in failures) Console.WriteLine("  - " + f);
            return fail == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ arquitetura
        private static void PhysicsIsDeviceIndependent()
        {
            foreach (FieldInfo f in typeof(ForceRequest).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Ok("ForceRequest." + f.Name + " has no device type", f.FieldType == typeof(double) || f.FieldType == typeof(bool) || f.FieldType == typeof(string) || f.FieldType == typeof(int));
            foreach (FieldInfo f in typeof(FfbEngine).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Type t = f.FieldType;
                bool dev = t == typeof(FfbOutput) || typeof(IEffectSink).IsAssignableFrom(t) || typeof(IFfbAdapter).IsAssignableFrom(t) || t == typeof(FfbDevice);
                Ok("FfbEngine." + f.Name + " is not a device type", !dev);
            }
            MethodInfo m = typeof(FfbEngine).GetMethod("Update");
            Ok("FfbEngine.Update returns ForceRequest", m != null && m.ReturnType == typeof(ForceRequest));
        }

        // ------------------------------------------------------------------ misturador
        private static void MixerLimits()
        {
            Settings s = Dd();
            FfbMixer mx = new FfbMixer();
            ForceRequest r = mx.Finish(Req(2.5, 0.5, 0.5, 0.1, 1.0, 2.0, 1.0, 0.5), s, 0.01);
            Ok("mixer: centering + friction within Maximum force", r.SpringNm + r.FrictionNm <= s.MaxNm + 1e-9);
            Ok("mixer: bumps+texture use only the room left (>= 25%)", Math.Abs(r.BumpNm) + r.TextureNm <= Math.Max(0.25 * s.MaxNm, s.MaxNm - r.SpringNm - r.FrictionNm) + 1e-9);
            // limite de variacao: 0,3 x pico por segundo
            FfbMixer m2 = new FfbMixer();
            m2.Finish(Req(0, 0.5, 0, 0, 0, 0, 0, 0), s, 0.01);
            ForceRequest r2 = m2.Finish(Req(3.0, 0.5, 0, 0, 0, 0, 0, 0), s, 0.01);
            Ok("mixer: centering slew limited (0.024 Nm in 10 ms at 8 Nm)", r2.SpringNm <= 0.3 * 8 * 0.01 + 1e-9);
            ForceRequest z = Req(1, 0.7, 1, 0.5, 0, 0, 0, 0); z.Gain = 0;
            ForceRequest r3 = new FfbMixer().Finish(z, s, 0.01);
            Ok("mixer: gain 0 -> stiffness and damping coefficients 0", r3.SpringK == 0 && r3.DamperK == 0 && r3.SpringNm == 0);
            ForceRequest big = Req(0, 0, 99, 1, 0, -99, 0, 99);
            ForceRequest r4 = new FfbMixer().Finish(big, s, 0.01);
            Ok("mixer: nothing above the cap", r4.DamperNm <= s.ForceCapNm && Math.Abs(r4.BumpNm) <= s.ForceCapNm && r4.EngineNm <= s.ForceCapNm);
        }

        // ------------------------------------------------------------------ paridade com a 0.12 (modo condicoes)
        private static void ConditionMapperParity()
        {
            Settings s = Dd();
            ForceRequest r = Req(1.0, 0.5, 0.4, 0.2, 0.1, 0.3, 0.2, 0.1);
            r.Follow = true;   // sem centragem moldada: valores diretos
            FfbOutput o = new DiConditionMapper().Map(r, 0.0, 0.01, s);
            Ok("parity: DD spring coef = 10000*K (cf=1 at 8 Nm)", o.SpringCoef == 5000);
            Ok("parity: spring sat = ToMag(1 Nm) = 1250", o.SpringSat == 1250);
            Ok("parity: damper coef 2000", o.DamperCoef == 2000);
            Ok("parity: friction 125", o.FrictionCoef == 125 && o.FrictionSat == 125);
            Ok("parity: constant 375", o.ConstMag == 375);
            Ok("parity: sine period 33333 us", o.SinePeriodUs == 33333);
            Settings g = Gear();
            FfbOutput og = new DiConditionMapper().Map(Req(1.0, 0.5, 0, 0, 0, 0, 0, 0), 0.2, 0.01, g);
            Ok("parity: gear spring coef = min(10000, 5000*sqrt(8/2.2)*1.5)", og.SpringCoef == 10000);
            // centragem moldada (DD): o centro desloca-se para o lado do centro real, nunca troca o sinal
            DiConditionMapper dm = new DiConditionMapper();
            FfbOutput od = dm.Map(Req(1.0, 0.5, 0, 0, 0, 0, 0, 0), 0.3, 0.01, s);
            Ok("shaped centering: offset between centre and wheel (sign kept)", od.SpringOffset > 0 && od.SpringOffset < 3000);
            Ok("shaped centering: saturation never above the cap", od.SpringSat <= s.ToMag(s.ForceCapNm));
        }

        // ------------------------------------------------------------------ contrato dos adaptadores
        private static void ContractChecks(string name, IFfbAdapter a, SimulatedEffectDevice d, Settings s, bool threaded)
        {
            Random rnd = new Random(7);
            bool threw = false;
            int capMag = s.ToMag(s.ForceCapNm);
            for (int i = 0; i < 300; i++)
            {
                ForceRequest r = Req(rnd.NextDouble() * 5, rnd.NextDouble(), rnd.NextDouble() * 5, rnd.NextDouble(), rnd.NextDouble() * 2,
                    (rnd.NextDouble() - 0.5) * 8, rnd.NextDouble() * 3, rnd.NextDouble() * 3);
                r = new FfbMixer().Finish(r, s, 0.01);
                d.Position = (int)((rnd.NextDouble() - 0.5) * 20000);
                try { a.Apply(r, d.Position / 10000.0, 0.01, s); } catch { threw = true; }
                if (threaded) Thread.Sleep(1);
            }
            Ok(name + ": never throws (profile " + d.Profile + ")", !threw);
            Ok(name + ": force magnitudes within the cap (profile " + d.Profile + ")",
                Math.Abs(d.Constant) <= capMag && d.SpringSat <= capMag && d.DamperSat <= capMag && d.FrictionSat <= capMag && d.SineMag <= capMag && d.EngineMag <= capMag);
            a.Stop();
            Ok(name + ": Stop -> device stopped (profile " + d.Profile + ")", d.Stopped);
        }

        private static void ContractConditionAdapter()
        {
            foreach (SimProfile p in Enum.GetValues(typeof(SimProfile)))
            {
                SimulatedEffectDevice d = new SimulatedEffectDevice(p);
                ContractChecks("condition adapter", new DiConditionAdapter(d), d, Dd(), false);
            }
            SimulatedEffectDevice nd = new SimulatedEffectDevice(SimProfile.NoConditions);
            Ok("caps: NoConditions -> conditions not usable", !nd.Caps.ConditionsUsable);
            Ok("caps: AcceptsNotPlays -> created but not playing", new SimulatedEffectDevice(SimProfile.AcceptsNotPlays).Caps.Get("Spring") == EffectStatus.Created);
            Ok("caps: Standard -> conditions usable", new SimulatedEffectDevice(SimProfile.Standard).Caps.ConditionsUsable);
            // dispositivo desligado: nao recebe nada
            SimulatedEffectDevice dr = new SimulatedEffectDevice(SimProfile.DropsAfterN); dr.DropAfter = 3;
            DiConditionAdapter ad = new DiConditionAdapter(dr);
            for (int i = 0; i < 10; i++) ad.Apply(Req(1, 0.5, 0, 0, 0, 0, 0, 0), 0, 0.01, Dd());
            int c = dr.Commands;
            ad.Apply(Req(1, 0.5, 0, 0, 0, 0, 0, 0), 0, 0.01, Dd());
            Ok("disconnected device receives no commands", dr.Commands == c && !dr.IsOpen);
        }

        private static void ContractTorqueAdapter()
        {
            foreach (SimProfile p in new SimProfile[] { SimProfile.Standard, SimProfile.NoConditions, SimProfile.Failing })
            {
                SimulatedEffectDevice d = new SimulatedEffectDevice(p);
                DiTorqueAdapter a = new DiTorqueAdapter(d);
                ContractChecks("torque adapter", a, d, Dd(), true);
                a.Dispose();
            }
            // pedido desatualizado (janela parada) -> binario a zero
            SimulatedEffectDevice ds = new SimulatedEffectDevice(SimProfile.Standard);
            ds.Position = 3000;
            DiTorqueAdapter ta = new DiTorqueAdapter(ds);
            ta.Apply(new FfbMixer().Finish(Req(2, 0.6, 0, 0, 0, 0, 0, 0), Dd(), 1.0), 0.3, 0.01, Dd());
            Thread.Sleep(60);
            bool pushing = ds.Constant != 0;
            Thread.Sleep(300);
            Ok("torque adapter: pushes while the request is fresh", pushing);
            Ok("torque adapter: zero torque when the request is older than 150 ms", ds.Constant == 0);
            ta.Stop();
            Thread.Sleep(30);
            ds.Constant = 0; ds.Stopped = true;
            Thread.Sleep(50);
            Ok("torque adapter: paused after Stop (no new commands)", ds.Constant == 0);
            ta.Dispose();
        }

        private static void TorqueModelLimits()
        {
            TorqueModel m = new TorqueModel();
            ForceRequest r = Req(3, 0.6, 1, 0.3, 0.2, 2, 1, 1);
            double t = m.Torque(r, 0.9, -5, 8, 3, false, true, 0.123);
            Ok("torque model: within cap", Math.Abs(t) <= 3 + 1e-9);
            double c = m.Torque(Req(2, 0.6, 0, 0, 0, 0, 0, 0), 0.3, 0, 8, 3, false, false, 0);
            Ok("torque model: centering pulls towards the centre", c < 0);
            double c2 = m.Torque(Req(2, 0.6, 0, 0, 0, 0, 0, 0), -0.3, 0, 8, 3, false, false, 0);
            Ok("torque model: symmetric", Math.Abs(c + c2) < 1e-9);
            double runaway = m.Torque(Req(0, 0, 0, 0, 0, 2, 0, 0), 0, 8, 8, 3, false, false, 0);
            Ok("torque model: power limit stops pushing a fast-spinning wheel", Math.Abs(runaway) < 1e-9);
        }

        private static void TorqueStabilityVirtualWheel()
        {
            Settings s = Dd();
            TorqueModel m = new TorqueModel();
            VirtualWheel w = new VirtualWheel();
            w.Pos = 0.4;
            ForceRequest r = Req(2, 0.6, 1, 0.1, 0.05, 0, 0, 0);
            double dt = 0.002, prevPos = w.Pos, vel = 0, delayed = 0;
            double maxFirst = 0, maxLast = 0;
            for (int i = 0; i < 1500; i++)   // 3 s a 500 Hz, com 1 amostra de atraso
            {
                double vNow = (w.Pos - prevPos) / dt; prevPos = w.Pos;
                vel += (vNow - vel) * Math.Min(1, dt / 0.004);
                double tq = m.Torque(r, w.Pos, vel, s.BaseNm, s.ForceCapNm, false, false, i * dt);
                w.Step(delayed, dt);
                delayed = tq;
                if (i > 250 && i < 750) maxFirst = Math.Max(maxFirst, Math.Abs(w.Pos));
                if (i >= 1000) maxLast = Math.Max(maxLast, Math.Abs(w.Pos));
            }
            Ok("virtual wheel: returns near the centre within 3 s (simulated, not a hardware test)", Math.Abs(w.Pos) < 0.05);
            Ok("virtual wheel: oscillation decays (simulated)", maxLast <= maxFirst + 1e-6);
        }

        // ------------------------------------------------------------------ seguranca
        private static void WatchdogTrips()
        {
            long now = 0;
            int stops = 0;
            Watchdog w = new Watchdog(delegate { stops++; }, delegate { return now; });
            w.Beat(true);
            now = 100; Ok("watchdog: no trip at 100 ms", !w.Check());
            now = 200; Ok("watchdog: trips after 150 ms", w.Check() && stops == 1);
            now = 400; Ok("watchdog: trips once until the next beat", !w.Check() && stops == 1);
            w.Beat(false);
            now = 2000; Ok("watchdog: not armed with FFB off", !w.Check());
        }

        private static void OscillationGuardTest()
        {
            OscillationGuard g = new OscillationGuard();
            double dt = 0.01;
            bool tripped = false;
            for (int i = 0; i < 150; i++)
            {
                ForceRequest r = Req(2, 0.6, 0.2, 0.1, 0, 0, 0, 0);
                g.Apply(r, 0.1 * Math.Sin(2 * Math.PI * 4 * i * dt), dt);
                if (g.Active) tripped = true;
            }
            Ok("oscillation guard: trips on a 4 Hz oscillation around the centre", tripped);
            OscillationGuard g2 = new OscillationGuard();
            bool t2 = false;
            for (int i = 0; i < 150; i++)
            {
                double pos = i < 40 ? 0.4 - i * 0.012 : (i < 50 ? -0.08 + (i - 40) * 0.008 : 0);   // largar o volante: um so passar do centro
                ForceRequest r = Req(2, 0.6, 0.2, 0.1, 0, 0, 0, 0);
                g2.Apply(r, pos, dt);
                if (g2.Active) t2 = true;
            }
            Ok("oscillation guard: a single overshoot does not trip it", !t2);
        }

        // ------------------------------------------------------------------ catalogo e classificacao
        private static void CatalogTests()
        {
            DeviceCatalog.Reload();
            CatalogEntry csl = DeviceCatalog.Find(0x0EB7, 0x0020);
            Ok("catalog: embedded devices.ini loaded", DeviceCatalog.LoadNote.StartsWith("embedded"));
            Ok("catalog: CSL DD confirmed, 8 Nm", csl.Validation == ValidationLevel.Confirmed && Math.Abs(csl.PeakNm - 8) < 1e-9);
            int confirmed = 0;
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in DeviceCatalog.All())
            {
                string v;
                if (kv.Value.TryGetValue("Validation", out v) && v.Equals("Confirmed", StringComparison.OrdinalIgnoreCase)) confirmed++;
            }
            Ok("catalog: only hardware-tested devices are 'Confirmed' (exactly 1: the author's CSL DD)", confirmed == 1);
            CatalogEntry g29 = DeviceCatalog.Find(0x046D, 0xC24F);
            Ok("catalog: G29 quirks merged with Logitech defaults", g29.Quirks.SpringFloor1 && g29.Quirks.SpringBoost == 1800 && g29.Drive == WheelDrive.Gear);
            CatalogEntry lgUnknown = DeviceCatalog.Find(0x046D, 0x1234);
            Ok("catalog: unknown Logitech gets vendor quirks but is not 'known'", !lgUnknown.Known && lgUnknown.Quirks.StopAllOnOpen);
            CatalogEntry moza = DeviceCatalog.Find(0x1111, 0x2222, "MOZA R9 Base");
            Ok("catalog: name hint (MOZA) -> Documented, not known torque", moza.Validation == ValidationLevel.Documented && moza.PeakNm == 0);
            CatalogEntry none = DeviceCatalog.Find(0x9999, 0x9999, "Some Wheel");
            Ok("catalog: unknown device -> Unknown level, assumed torque", none.Validation == ValidationLevel.Unknown && !none.Known);
            WheelInfo wi = WheelCatalog.Find(0x9999, 0x9999);
            Ok("catalog facade: unknown -> 0 Nm (app assumes 20 Nm)", wi.PeakNm == 0 && WheelCatalog.UnknownNm == 20.0);
            DeviceQuirks q = DeviceQuirks.Parse("SpringFloor1, SpringBoost=99999, Nonsense");
            Ok("quirks: parse and clamp", q.SpringFloor1 && q.SpringBoost == 10000);
        }

        private static void ClassificationTests()
        {
            Ok("classify: FFB driving device -> wheel base", DirectInputProvider.Classify(0x16, 1, true, "FANATEC Wheel") == DeviceKind.Wheelbase);
            Ok("classify: FFB device reported as joystick -> wheel base", DirectInputProvider.Classify(0x14, 0, true, "R9 Base") == DeviceKind.Wheelbase);
            Ok("classify: combined pedals -> pedals", DirectInputProvider.Classify(0x16, 2, false, "Pedals") == DeviceKind.Pedals);
            Ok("classify: shifter by name", DirectInputProvider.Classify(0x14, 0, false, "TH8A Shifter") == DeviceKind.Shifter);
            Ok("classify: handbrake by name", DirectInputProvider.Classify(0x14, 0, false, "USB Handbrake") == DeviceKind.Handbrake);
            Ok("classify: gamepad", DirectInputProvider.Classify(0x15, 0, false, "Controller") == DeviceKind.Gamepad);
            Ok("classify: non-FFB 'wheel' is never a wheel base", DirectInputProvider.Classify(0x16, 1, false, "Wheel rim buttons") != DeviceKind.Wheelbase);
        }

        private static void SettingsScaling()
        {
            Settings s = Dd();
            Ok("scaling: 1 Nm on 8 Nm = 1250", s.ToMag(1.0) == 1250);
            s.VendorFfPercent = 50;
            Ok("scaling: manufacturer FF 50 % -> double scale", s.ToMag(1.0) == 2500);
            Ok("scaling: never above 10000", s.ToMag(100) <= 10000);
            s.VendorFfPercent = 100; s.MaxNm = 3;
            Ok("scaling: cap = Maximum force", s.ToMag(5) == s.ToMag(3));
        }
    }
}
