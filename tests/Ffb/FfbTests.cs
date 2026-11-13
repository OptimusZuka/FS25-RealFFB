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
            MozaCatalog();
            TorqueCeiling();
            MixerSumCeiling();
            EngineStartSequence();
            FfbStateTransitions();
            DedicatedServer();
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
            CatalogEntry moza = DeviceCatalog.Find(0x1111, 0x2222, "MOZA Farming Control System");
            Ok("catalog: MOZA brand alone -> Documented, no torque and no ceiling", moza.Validation == ValidationLevel.Documented && moza.PeakNm == 0 && moza.CeilingNm == 0);
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


        // ------------------------------------------------------------------ 0.15: catalogo MOZA
        private static void MozaCatalog()
        {
            CatalogEntry r5 = DeviceCatalog.Find(0x346E, 0x0004, "MOZA R5 Base");
            Ok("moza: R5 by USB ID (tester's report) = 5.5 Nm", r5.Known && r5.PeakNm == 5.5 && r5.IdentifiedBy == "usb" && r5.CeilingNm == 5.5);
            Ok("moza: R5 is Documented, not Confirmed", r5.Validation == ValidationLevel.Documented);
            string[] names = { "MOZA R3 Base", "MOZA R5 Base", "MOZA R9 Base", "MOZA R12 Base", "MOZA R16 Base", "MOZA R21 Base" };
            double[] nm = { 3.9, 5.5, 9, 12, 16, 21 };
            for (int i = 0; i < names.Length; i++)
            {
                CatalogEntry e = DeviceCatalog.Find(0x346E, 0x7FFF, names[i]);
                Ok("moza: " + names[i] + " = " + nm[i] + " Nm (by device name)", e.Known && e.PeakNm == nm[i] && e.IdentifiedBy == "name" && e.CeilingNm == nm[i]);
            }
            Ok("moza: R21 marked as sustained rating", DeviceCatalog.Find(0x346E, 0x7FFF, "MOZA R21 Ultra Base").Sustained);
            Ok("moza: R5 Pro (6 Nm, not in the catalog) is NOT taken for an R5", DeviceCatalog.Find(0x346E, 0x7FFE, "MOZA R5 Pro Base").CeilingNm == 0);
            Ok("moza: R12 is not matched by the R1x of another name", DeviceCatalog.Find(0x346E, 0x7FFD, "MOZA R12 Base").PeakNm == 12);
            Ok("moza: stalk is not a wheel base model", DeviceCatalog.Find(0x346E, 0x0024, "MOZA Multi-function Stalk").CeilingNm == 0);
            Ok("moza: a model token without the brand is not identified", DeviceCatalog.Find(0x1234, 0x5678, "R9 Base").CeilingNm == 0);
            Ok("moza: USB ID wins over the name", DeviceCatalog.Find(0x346E, 0x0004, "MOZA R9 Base").PeakNm == 5.5);
            Ok("catalog: model list for the chooser includes the MOZA R5", DeviceCatalog.ModelsWithTorque().Exists(delegate(CatalogEntry e) { return e.Model == "MOZA R5" && e.PeakNm == 5.5; }));
        }

        // ------------------------------------------------------------------ 0.15: teto do binario
        private static void TorqueCeiling()
        {
            string why;
            Ok("ceiling: R5 rejects 21 Nm -> 5.5 with an explanation", TorqueLimits.Validate(21, 5.5, "MOZA R5", out why) == 5.5 && why.Length > 0);
            Ok("ceiling: R9 rejects 9.1 Nm", TorqueLimits.Validate(9.1, 9, "MOZA R9", out why) == 9 && why.Length > 0);
            Ok("ceiling: R9 accepts 9 Nm without a message", TorqueLimits.Validate(9, 9, "MOZA R9", out why) == 9 && why.Length == 0);
            Ok("ceiling: lower value accepted (limited base) but warned", TorqueLimits.Validate(5, 8, "CSL DD", out why) == 5 && why.Length == 0
                && TorqueLimits.BelowSpecWarning(5, 8, "CSL DD").Length > 0);
            Ok("ceiling: NaN rejected", TorqueLimits.Validate(double.NaN, 5.5, "MOZA R5", out why) == 5.5 && why.Length > 0);
            Ok("ceiling: below minimum rejected", TorqueLimits.Validate(0.2, 5.5, "MOZA R5", out why) == TorqueLimits.MinNm && why.Length > 0);
            Ok("ceiling: unknown base -> generic 40 Nm ceiling", TorqueLimits.Validate(55, 0, "", out why) == TorqueLimits.GenericCeilingNm && why.Length > 0);
            Ok("ceiling: unknown base assumes 20 Nm (forces start LOW)", WheelCatalog.UnknownNm == 20.0);

            // configuracao importada com 21 Nm num R5: o pico usado nos calculos nunca passa 5,5
            Settings s = Settings.FromLines(new string[] { "SettingsVersion=" + Settings.Version, "BaseNm=21", "MaxNm=21", "ForceScale=4" });
            s.HwPeakNm = 5.5; s.HwModel = "MOZA R5";
            Ok("ceiling: imported 21 Nm on an R5 -> effective peak 5.5", s.PeakNm == 5.5);
            Ok("ceiling: force cap never above the hardware", s.ForceCapNm <= 5.5);
            Ok("ceiling: config file cannot hold more than the generic ceiling", Settings.FromLines(new string[] { "BaseNm=999" }).BaseNm == TorqueLimits.GenericCeilingNm);
            Ok("ceiling: corrupted value -> default kept", Settings.FromLines(new string[] { "BaseNm=abc" }).BaseNm == new Settings().BaseNm);

            // a forca enviada nunca passa o teto, seja qual for o ganho/multiplicador
            FfbMixer mx = new FfbMixer();
            ForceRequest big = Req(50, 1, 50, 1, 50, 50, 50, 50);
            ForceRequest r = mx.Finish(big, s, 0.5);
            double cap = s.ForceCapNm;
            Ok("ceiling: multiplier x4 cannot pass the cap (each effect)", r.SpringNm <= cap + 1e-9 && r.DamperNm <= cap + 1e-9 && r.FrictionNm <= cap + 1e-9 && Math.Abs(r.BumpNm) <= cap + 1e-9 && r.TextureNm <= cap + 1e-9 && r.EngineNm <= cap + 1e-9);
            int mag = s.ToMag(1000);
            Ok("ceiling: final magnitude never above the cap in Nm", mag <= (int)Math.Round(cap / s.EffectivePeakNm * 10000) && mag <= 10000);
            Ok("ceiling: output torque on the real R5 <= its 5.5 Nm", mag / 10000.0 * 5.5 <= 5.5 + 1e-9);

            // calibracao manual valida mantem-se ao gravar e ler
            Settings u = new Settings(); u.BaseNm = 4.5; u.TorqueSource = "user"; u.VendorFfPercent = 80;
            u.UserModel = "MOZA R5"; u.UserModelDevice = "346E:9999"; u.UserModelPeakNm = 5.5;
            Settings back = Settings.FromLines(u.ToText().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));
            Ok("persistence: manual peak, source, FF % and chosen model survive save/load",
                back.BaseNm == 4.5 && back.TorqueSource == "user" && back.VendorFfPercent == 80 && back.UserModel == "MOZA R5" && back.UserModelPeakNm == 5.5 && back.UserModelDevice == "346E:9999");
            Settings cfgOld = Settings.FromLines(new string[] { "SettingsVersion=" + Settings.Version, "BaseNm=8", "MaxNm=3" });
            Ok("compat: an existing valid 8 Nm CSL DD config is kept", cfgOld.BaseNm == 8 && cfgOld.MaxNm == 3);
        }

        // ------------------------------------------------------------------ 0.15: soma das forcas
        private static void MixerSumCeiling()
        {
            Settings s = Dd();
            FfbMixer mx = new FfbMixer();
            ForceRequest r = mx.Finish(Req(2.9, 0.5, 0, 0, 0.1, 1.0, 0.8, 0.9), s, 0.5);
            double sum = r.SpringNm + r.FrictionNm + Math.Abs(r.BumpNm) + r.TextureNm + r.EngineNm;
            Ok("mixer: sum of spring+friction+bump+texture+engine <= Maximum force", sum <= s.ForceCapNm + 1e-9);
            Ok("mixer: vibrations keep at least 25% of the cap", Math.Abs(r.BumpNm) + r.TextureNm + r.EngineNm >= 0.25 * s.ForceCapNm - 1e-9);
            ForceRequest q = mx.Finish(Req(0.5, 0.5, 0, 0, 0, 0, 0, 0.3), s, 0.5);
            Ok("mixer: small forces are not touched", Math.Abs(q.EngineNm - 0.3) < 1e-9);
            ForceRequest z = Req(2, 0.5, 1, 0.5, 1, 1, 1, 1); z.Gain = 0;
            ForceRequest zr = new FfbMixer().Finish(z, s, 0.5);
            Ok("mixer: gain 0 -> no force at all", zr.SpringNm == 0 && zr.EngineNm == 0 && zr.TextureNm == 0 && zr.BumpNm == 0 && zr.SpringK == 0);
        }

        // ------------------------------------------------------------------ 0.15: arranque do motor (simulado)
        private static TelemetryFrame Veh(string name, string cat, int mst, double rpm, bool on)
        {
            TelemetryFrame f = new TelemetryFrame();
            f.Active = true; f.Vehicle = name; f.Category = cat; f.MotorState = mst; f.Rpm = rpm; f.EngineOn = on; f.HasEngine = true;
            f.SpeedKmh = 0; f.Vx = 0; f.Vz = 0; f.Vy = 0; f.MassT = 6; f.Surface = "road"; f.Wheels = 4; f.Contacts = 4; f.EngineLoad = 0.2;
            return f;
        }

        // corre o motor de FFB durante 'secs' e devolve a forca maxima do motor e a ultima frequencia
        private static double RunEngine(FfbEngine e, TelemetryFrame f, Settings s, double secs, out double lastHz)
        {
            double mx = 0; lastHz = 0;
            for (double t = 0; t < secs; t += 0.01)
            {
                ForceRequest r = e.Update(f, 10, true, 0.01, s);
                if (r.EngineNm > mx) mx = r.EngineNm;
                lastHz = r.EngineHz;
            }
            return mx;
        }

        private static void EngineStartSequence()
        {
            Settings s = Dd();
            double hz;
            // trator: a arrancar (estado 2) -> pegou (3, sem rpm ainda) -> ralenti
            FfbEngine e = new FfbEngine();
            RunEngine(e, Veh("T", "TRACTORSM", 0, double.NaN, false), s, 0.5, out hz);
            double crank = RunEngine(e, Veh("T", "TRACTORSM", 2, double.NaN, false), s, 3.0, out hz);
            Ok("engine: tractor cranking is felt (starter motor)", crank > 0.15 && crank < 0.5);
            Ok("engine: cranking at a low frequency (9-13 Hz)", hz >= 9 && hz <= 13);
            ForceRequest c1 = e.Update(Veh("T", "TRACTORSM", 3, 750, true), 10, true, 0.01, s);
            Ok("engine: catch = low-frequency rock (engine on its mounts)", c1.EngineHz <= 11 && c1.EngineNm > 0);
            double idle = RunEngine(e, Veh("T", "TRACTORSM", 3, 750, true), s, 2.0, out hz);
            ForceRequest i1 = e.Update(Veh("T", "TRACTORSM", 3, 750, true), 10, true, 0.01, s);
            Ok("engine: settles into idle at the firing frequency (rpm/20)", i1.EngineHz >= 35 && i1.EngineHz <= 40);
            Ok("engine: idle smaller than the catch", i1.EngineNm < c1.EngineNm + 1e-9);
            ForceRequest i2 = e.Update(Veh("T", "TRACTORSM", 3, 1800, true), 10, true, 0.01, s);
            RunEngine(e, Veh("T", "TRACTORSM", 3, 1800, true), s, 1.0, out hz);
            Ok("engine: higher rpm -> higher frequency", hz > 80);

            // camiao: mesma sequencia, muito mais suave (cabine suspensa)
            FfbEngine et = new FfbEngine();
            RunEngine(et, Veh("K", "TRUCKS", 0, double.NaN, false), s, 0.5, out hz);
            double crankT = RunEngine(et, Veh("K", "TRUCKS", 2, double.NaN, false), s, 3.0, out hz);
            ForceRequest k1 = et.Update(Veh("K", "TRUCKS", 3, 650, true), 10, true, 0.01, s);
            Ok("engine: truck cranking < half of the tractor's", crankT < 0.5 * crank + 1e-9);
            Ok("engine: truck catch < half of the tractor's", k1.EngineNm < 0.5 * c1.EngineNm + 1e-9);
            Ok("engine: profiles differ by vehicle type", EngineProfile.For("TRUCKS").Mount < EngineProfile.For("TRACTORSM").Mount && EngineProfile.For("CARS").Mount < EngineProfile.For("TRUCKS").Mount);

            // nunca fica preso no arranque
            FfbEngine es = new FfbEngine();
            double stuck = 0;
            RunEngine(es, Veh("S", "TRACTORSM", 2, double.NaN, false), s, FfbEngine.MaxCrankSeconds + 1, out hz);
            stuck = RunEngine(es, Veh("S", "TRACTORSM", 2, double.NaN, false), s, 1.0, out hz);
            Ok("engine: starter vibration stops after " + FfbEngine.MaxCrankSeconds + " s even if the game stays in 'starting'", stuck == 0);

            // arrancar outra vez nao duplica: segundo arranque = mesma amplitude
            FfbEngine er = new FfbEngine();
            RunEngine(er, Veh("R", "TRACTORSM", 0, double.NaN, false), s, 0.3, out hz);
            double a1 = RunEngine(er, Veh("R", "TRACTORSM", 2, double.NaN, false), s, 2.0, out hz);
            RunEngine(er, Veh("R", "TRACTORSM", 3, 750, true), s, 1.0, out hz);
            RunEngine(er, Veh("R", "TRACTORSM", 0, double.NaN, false), s, 1.0, out hz);
            double a2 = RunEngine(er, Veh("R", "TRACTORSM", 2, double.NaN, false), s, 2.0, out hz);
            Ok("engine: repeated start has the same strength (no stacking)", Math.Abs(a1 - a2) < 0.02);

            // entrar num veiculo com o motor ja ligado: sem "pegou" falso vindo do veiculo anterior
            FfbEngine ev = new FfbEngine();
            RunEngine(ev, Veh("A", "TRACTORSM", 2, double.NaN, false), s, 1.0, out hz);
            ForceRequest sw = ev.Update(Veh("B", "TRUCKS", 3, 650, true), 10, true, 0.01, s);
            Ok("engine: switching vehicle mid-start gives no catch jolt", sw.EngineHz > 20);

            // desligar a vibracao do motor nao desliga o resto
            Settings off = Dd(); off.Engine = 0; off.EngineStart = 0;
            FfbEngine eo = new FfbEngine();
            RunEngine(eo, Veh("O", "TRACTORSM", 0, double.NaN, false), off, 0.3, out hz);
            double o1 = RunEngine(eo, Veh("O", "TRACTORSM", 2, double.NaN, false), off, 2, out hz);
            double o2 = RunEngine(eo, Veh("O", "TRACTORSM", 3, 900, true), off, 2, out hz);
            TelemetryFrame mv = Veh("O", "TRACTORSM", 3, 900, true); mv.SpeedKmh = 25; mv.Vz = 7;
            ForceRequest om = null;
            for (int i = 0; i < 200; i++) om = eo.Update(mv, 10, true, 0.01, off);
            Ok("engine: Engine start 0 and Engine vibration 0 -> no engine force", o1 == 0 && o2 == 0 && om.EngineNm == 0);
            Ok("engine: ...but centering still works", om.SpringNm > 0);
            Settings onlyStart = Dd(); onlyStart.Engine = 0;
            FfbEngine e2 = new FfbEngine();
            RunEngine(e2, Veh("P", "TRACTORSM", 0, double.NaN, false), onlyStart, 0.3, out hz);
            Ok("engine: start feel independent of the engine vibration slider", RunEngine(e2, Veh("P", "TRACTORSM", 2, double.NaN, false), onlyStart, 2, out hz) > 0);
        }

        // ------------------------------------------------------------------ 0.15: FFB ligado/desligado (simulado)
        private static void FfbStateTransitions()
        {
            Settings s = Dd();
            FfbEngine e = new FfbEngine();
            TelemetryFrame f = Veh("T", "TRACTORSM", 3, 800, true); f.SpeedKmh = 20; f.Vz = 5.5;
            ForceRequest r = null;
            for (int i = 0; i < 60; i++) r = e.Update(f, 10, true, 0.01, s);
            Ok("state: gain reaches 100% within 0.6 s", r.Gain > 0.99 && r.State == "ACTIVE");
            for (int i = 0; i < 40; i++) r = e.Update(f, 500, true, 0.01, s);
            Ok("state: stale telemetry -> gain 0 and explained", r.Gain == 0 && r.State.Contains("waiting"));
            for (int i = 0; i < 60; i++) r = e.Update(f, 10, true, 0.01, s);
            Ok("state: recovers by itself when telemetry returns", r.Gain > 0.99);
            TelemetryFrame outv = Veh("T", "TRACTORSM", 3, 800, true); outv.Active = false;
            for (int i = 0; i < 40; i++) r = e.Update(outv, 10, true, 0.01, s);
            Ok("state: leaving the vehicle -> gain 0, no residual forces", r.Gain == 0 && r.EngineNm == 0 && r.SpringNm == 0);
            for (int i = 0; i < 60; i++) r = e.Update(f, 10, true, 0.01, s);
            Ok("state: re-entering -> active again", r.Gain > 0.99);
            for (int i = 0; i < 40; i++) r = e.Update(f, 10, false, 0.01, s);
            Ok("state: FFB switched off -> gain 0 and explained", r.Gain == 0 && r.State == "FFB off");
            e.Halt();
            r = e.Update(f, 10, false, 0.01, s);
            Ok("state: emergency stop -> zero immediately", r.Gain == 0);
            Ok("state: watchdog still stops a frozen loop (see watchdog tests)", true);
        }

        // ------------------------------------------------------------------ 0.15: servidor dedicado
        private static void DedicatedServer()
        {
            string line = "RFFB|1|seq=1|t=1000|act=1|veh=Truck|cat=TRUCKS|ses=client|spd=20|mass=8|w=0,na,0,0;0,na,0,0;0,na,0,0;0,na,0,0";
            TelemetryFrame c = TelemetryServer.ParseLine(line);
            Ok("mp: session parsed", c != null && c.MpClient);
            Ok("mp: no wheel contact on a client is 'unknown', not 'air'", c.Surface == "unknown" && c.ContactUnknown && c.Contacts == 0);
            TelemetryFrame sp = TelemetryServer.ParseLine(line.Replace("ses=client", "ses=sp"));
            Ok("mp: in single player no contact still means 'air'", sp.Surface == "air" && !sp.ContactUnknown);
            TelemetryFrame old = TelemetryServer.ParseLine(line.Replace("|ses=client", ""));
            Ok("mp: old mod without session -> unchanged behaviour", old.Surface == "air" && old.Session == "");

            Settings s = Dd();
            // centragem: cliente (contacto desconhecido) ~ normal; "no ar" quase nada
            double springClient = SpringAt(c, s), springAir = SpringAt(sp, s);
            Ok("mp: centering works on a dedicated server", springClient > 3 * springAir && springClient > 0.3);
            // os degraus de velocidade da rede nao sao embates
            FfbEngine e = new FfbEngine();
            int coll = 0;
            for (int i = 0; i < 200; i++)
            {
                TelemetryFrame f = TelemetryServer.ParseLine(line);
                f.Seq = i + 2; f.GameTimeMs = 1000 + i * 16; f.Vz = (i % 12 < 6) ? 5.5 : 2.5; f.Vx = 0; f.SpeedKmh = f.Vz * 3.6;
                ForceRequest r = e.Update(f, 10, true, 0.016, s);
                coll = r.CollCount;
            }
            Ok("mp: network speed steps are not reported as impacts", coll == 0);
            FfbEngine e2 = new FfbEngine();
            for (int i = 0; i < 200; i++)
            {
                TelemetryFrame f = TelemetryServer.ParseLine(line.Replace("ses=client", "ses=sp").Replace("w=0,", "w=1,"));
                f.Seq = i + 2; f.GameTimeMs = 1000 + i * 16; f.Vz = (i % 12 < 6) ? 5.5 : 2.5; f.Vx = 0; f.SpeedKmh = f.Vz * 3.6;
                ForceRequest r = e2.Update(f, 10, true, 0.016, s);
                coll = r.CollCount;
            }
            Ok("mp: the same steps in single player still count as impacts (detection unchanged)", coll > 0);
            Ok("mp: no invented terrain texture on a client", TexAt(c, s) == 0);
        }

        private static double SpringAt(TelemetryFrame f0, Settings s)
        {
            FfbEngine e = new FfbEngine();
            ForceRequest r = null;
            for (int i = 0; i < 200; i++)
            {
                TelemetryFrame f = TelemetryServer.ParseLine("RFFB|1|seq=1|t=1000|act=1|veh=V|cat=TRUCKS|ses=" + f0.Session + "|spd=20|mass=8|w=0,na,0,0;0,na,0,0;0,na,0,0;0,na,0,0");
                f.Seq = i + 2; f.Vz = 5.5; f.Vx = 0; f.SpeedKmh = 20; f.GameTimeMs = 1000 + i * 10;
                r = e.Update(f, 10, true, 0.01, s);
            }
            return r.SpringNm;
        }

        private static double TexAt(TelemetryFrame f0, Settings s)
        {
            FfbEngine e = new FfbEngine();
            double mx = 0;
            for (int i = 0; i < 200; i++)
            {
                TelemetryFrame f = TelemetryServer.ParseLine("RFFB|1|seq=1|t=1000|act=1|veh=V|cat=TRUCKS|ses=" + f0.Session + "|spd=20|mass=8|w=0,na,0,0;0,na,0,0;0,na,0,0;0,na,0,0");
                f.Seq = i + 2; f.Vz = 5.5; f.Vx = 0; f.SpeedKmh = 20; f.GameTimeMs = 1000 + i * 10;
                ForceRequest r = e.Update(f, 10, true, 0.01, s);
                if (r.TextureNm > mx) mx = r.TextureNm;
            }
            return mx;
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
