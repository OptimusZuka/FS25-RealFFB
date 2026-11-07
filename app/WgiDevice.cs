// FS25 Real FFB - saida de forca via Windows.Gaming.Input (WGI).
// Porque: o FS25 apanha a base em DirectInput exclusivo quando esta em primeiro plano,
// e o Windows so deixa um programa de cada vez mandar FFB por DirectInput.
// O WGI e outro caminho do Windows ate ao driver, que nao passa por essa disputa.
using System;
using System.Numerics;
using System.Threading;
using Windows.Foundation;
using Windows.Gaming.Input;
using Windows.Gaming.Input.ForceFeedback;

namespace FS25RealFFB
{
    internal sealed class WgiDevice : IWheelOutput
    {
        private static readonly TimeSpan Duration = TimeSpan.FromSeconds(10);
        private static readonly Vector3 AxisX = new Vector3(1f, 0f, 0f);

        private readonly Action<string> log;
        private RacingWheel wheel;
        private ForceFeedbackMotor motor;
        private ConditionForceEffect spring, damper, friction;
        private ConstantForceEffect constant;
        private PeriodicForceEffect sine;
        private readonly long[] cache = new long[5];
        private bool stopped;
        private int lastRefreshMs = Environment.TickCount;
        private int errors;

        public bool IsOpen { get; private set; }
        public string DeviceName { get; private set; }

        public WgiDevice(Action<string> log)
        {
            this.log = log;
            DeviceName = "-";
            ResetCache();
        }

        private void ResetCache() { for (int i = 0; i < cache.Length; i++) cache[i] = long.MinValue; }

        public bool Open()
        {
            // a lista de volantes do WGI enche-se de forma assincrona apos o arranque
            for (int i = 0; i < 40 && RacingWheel.RacingWheels.Count == 0 && RawGameController.RawGameControllers.Count == 0; i++)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(100);
            }
            for (int i = 0; i < 10; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(100); }
            log("WGI: volantes detetados: " + RacingWheel.RacingWheels.Count + " | controladores brutos: " + RawGameController.RawGameControllers.Count);
            ForceFeedbackMotor rawMotor = null;
            foreach (RawGameController rc in RawGameController.RawGameControllers)
            {
                int motors = rc.ForceFeedbackMotors.Count;
                log(string.Format("WGI: controlador VID {0:X4} PID {1:X4} eixos {2} motores FFB {3}", rc.HardwareVendorId, rc.HardwareProductId, rc.AxisCount, motors));
                if (rawMotor == null && rc.HardwareVendorId == 0x0EB7 && motors > 0) rawMotor = rc.ForceFeedbackMotors[0];
            }
            foreach (RacingWheel w in RacingWheel.RacingWheels)
            {
                log("WGI: volante, rotacao max " + w.MaxWheelAngle + " graus, motor FFB: " + (w.WheelMotor != null ? "SIM" : "NAO"));
                if (wheel == null && w.WheelMotor != null) wheel = w;
            }
            if (wheel != null) motor = wheel.WheelMotor;
            else if (rawMotor != null) { motor = rawMotor; log("WGI: a usar o motor FFB do controlador bruto Fanatec."); }
            if (motor == null) { log("WGI: a base nao expoe motor de FFB pelo Windows.Gaming.Input."); return false; }

            log("WGI: eixos FFB suportados: " + motor.SupportedAxes + " | ativo: " + motor.IsEnabled);
            if (!motor.IsEnabled) Await(motor.TryEnableAsync());
            motor.MasterGain = 1.0;

            spring = new ConditionForceEffect(ConditionForceEffectKind.Spring);
            damper = new ConditionForceEffect(ConditionForceEffectKind.Damper);
            friction = new ConditionForceEffect(ConditionForceEffectKind.Friction);
            constant = new ConstantForceEffect();
            sine = new PeriodicForceEffect(PeriodicForceEffectKind.SineWave);

            spring.SetParameters(AxisX, 0f, 0f, 0f, 0f, 0f, 0f);
            damper.SetParameters(AxisX, 0f, 0f, 0f, 0f, 0f, 0f);
            friction.SetParameters(AxisX, 0f, 0f, 0f, 0f, 0f, 0f);
            constant.SetParameters(new Vector3(0f, 0f, 0f), Duration);
            sine.SetParameters(new Vector3(0f, 0f, 0f), 20f, 0f, 0f, Duration);

            bool ok = Load(spring, "Spring") & Load(damper, "Damper") & Load(friction, "Friction")
                    & Load(constant, "Constant") & Load(sine, "Sine");
            if (!ok)
            {
                log("WGI: efeitos nao carregaram - WGI desativado, a usar DirectInput.");
                return false;
            }
            StartAll();
            DeviceName = "Fanatec via Windows.Gaming.Input";
            IsOpen = true;
            log("WGI: base pronta.");
            return true;
        }

        // Espera simples por uma operacao assincrona do WinRT.
        private static T Await<T>(IAsyncOperation<T> op)
        {
            int start = Environment.TickCount;
            while (op.Status == AsyncStatus.Started)
            {
                if (Environment.TickCount - start > 5000) throw new TimeoutException("WGI nao respondeu em 5 s");
                Thread.Sleep(2);
            }
            if (op.Status != AsyncStatus.Completed) throw new InvalidOperationException("WGI: operacao " + op.Status);
            return op.GetResults();
        }

        private bool Load(IForceFeedbackEffect e, string name)
        {
            try
            {
                ForceFeedbackLoadEffectResult r = Await(motor.LoadEffectAsync(e));
                log("WGI: efeito " + name + ": " + r);
                return r == ForceFeedbackLoadEffectResult.Succeeded;
            }
            catch (Exception ex)
            {
                log("WGI: efeito " + name + " falhou: " + ex.Message);
                return false;
            }
        }

        private void StartAll()
        {
            Try(delegate { spring.Start(); });
            Try(delegate { damper.Start(); });
            Try(delegate { friction.Start(); });
            Try(delegate { constant.Start(); });
            Try(delegate { sine.Start(); });
            stopped = false;
            lastRefreshMs = Environment.TickCount;
        }

        private void Try(Action a)
        {
            try { a(); }
            catch (Exception ex)
            {
                errors++;
                if (errors < 5 || errors % 1000 == 0) log("WGI: erro " + ex.Message);
            }
        }

        // Constante e seno tem duracao finita no WGI: renovam-se a cada 5 s.
        private void Refresh()
        {
            if (stopped) { StartAll(); return; }
            int now = Environment.TickCount;
            if (now - lastRefreshMs < 5000) return;
            lastRefreshMs = now;
            Try(delegate { constant.Start(); });
            Try(delegate { sine.Start(); });
        }

        private static float F(int v) { return v / 10000f; }

        public void SetSpring(int coef, int sat) { Condition(spring, 0, coef, sat); }
        public void SetDamper(int coef, int sat) { Condition(damper, 1, coef, sat); }
        public void SetFriction(int coef, int sat) { Condition(friction, 2, coef, sat); }

        private void Condition(ConditionForceEffect e, int slot, int coef, int sat)
        {
            if (!IsOpen || e == null) return;
            Refresh();
            long key = ((long)coef << 32) | (uint)sat;
            if (cache[slot] == key) return;
            cache[slot] = key;
            float c = F(coef), s = F(sat);
            Try(delegate { e.SetParameters(AxisX, c, c, s, s, 0f, 0f); });
        }

        public void SetConstant(int mag)
        {
            if (!IsOpen || constant == null) return;
            Refresh();
            if (cache[3] == mag) return;
            cache[3] = mag;
            float m = F(mag);
            Try(delegate { constant.SetParameters(new Vector3(m, 0f, 0f), Duration); });
        }

        public void SetSine(int mag, int periodUs)
        {
            if (!IsOpen || sine == null) return;
            Refresh();
            long key = ((long)mag << 32) | (uint)periodUs;
            if (cache[4] == key) return;
            cache[4] = key;
            float m = F(mag);
            float hz = periodUs > 0 ? 1000000f / periodUs : 20f;
            Try(delegate { sine.SetParameters(new Vector3(m, 0f, 0f), hz, 0f, 0f, Duration); });
        }

        public void Commit() { }

        public int ReadPosition()
        {
            if (wheel == null) return 0;
            try { return (int)(wheel.GetCurrentReading().Wheel * 10000); } catch { return 0; }
        }

        public void EmergencyStop()
        {
            if (motor == null) return;
            Try(delegate { motor.StopAllEffects(); });
            stopped = true;
            ResetCache();
        }

        public void Dispose()
        {
            EmergencyStop();
            if (motor != null)
            {
                IForceFeedbackEffect[] all = { spring, damper, friction, constant, sine };
                foreach (IForceFeedbackEffect e in all)
                {
                    IForceFeedbackEffect x = e;
                    if (x != null) Try(delegate { motor.TryUnloadEffectAsync(x); });
                }
            }
            IsOpen = false;
        }
    }
}
