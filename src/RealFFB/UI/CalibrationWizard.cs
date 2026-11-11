// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - calibracao da forca. O software nao consegue medir binario; o que se calibra e:
//   1. o torque de pico real da base (do fabricante; ex.: CSL DD 5 Nm sem Boost Kit, 8 Nm com);
//   2. o "FF %" configurado no software/menu do fabricante (Fanatec FF, MOZA "game FFB intensity",
//      ganho Thrustmaster, ...). Se estiver a 50 %, a app usa o dobro da escala para o mesmo Nm.
// Um impulso de teste de 1 Nm (limitado pela "Maximum force") ajuda a confirmar que soa razoavel.
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FS25RealFFB
{
    internal sealed class CalibrationWizard : Form
    {
        private readonly IEffectSink dev;
        private readonly Settings s;
        private readonly NumericUpDown peak = new NumericUpDown(), ff = new NumericUpDown();
        private readonly Timer pulseOff = new Timer();
        public double PeakNm { get { return (double)peak.Value; } }
        public double VendorFf { get { return (double)ff.Value; } }

        public CalibrationWizard(IEffectSink dev, Settings s, CatalogEntry ce)
        {
            this.dev = dev; this.s = s;
            Text = L.T("Calibrate force");
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.UI(9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 360);

            Label info = new Label(); info.SetBounds(20, 14, 520, 70); info.ForeColor = Theme.Soft;
            info.Text = L.T("The app cannot measure torque. Enter your base's real peak torque (from the manufacturer) and the force feedback percentage set in the manufacturer's software or wheel menu.")
                + (ce != null && ce.Notes.Length > 0 ? "\n" + ce.Notes : "");
            Controls.Add(info);

            Label l1 = new Label(); l1.SetBounds(20, 100, 330, 24); l1.Text = L.T("Peak torque of your base (Nm)"); Controls.Add(l1);
            peak.SetBounds(360, 98, 120, 24); peak.DecimalPlaces = 1; peak.Increment = 0.5M; peak.Minimum = 1; peak.Maximum = 40;
            peak.Value = (decimal)Math.Max(1, Math.Min(40, s.BaseNm)); Controls.Add(peak);
            Label l2 = new Label(); l2.SetBounds(20, 140, 330, 24); l2.Text = L.T("Force feedback % in the manufacturer's software"); Controls.Add(l2);
            ff.SetBounds(360, 138, 120, 24); ff.Minimum = 10; ff.Maximum = 100; ff.Value = (decimal)Math.Max(10, Math.Min(100, s.VendorFfPercent)); Controls.Add(ff);

            Label l3 = new Label(); l3.SetBounds(20, 182, 520, 60); l3.ForeColor = Theme.Muted;
            l3.Text = L.T("Test pulse: the app pushes the wheel with about 1 Nm for half a second (never above your Maximum force). On most bases this is a light but clear push.");
            Controls.Add(l3);
            Button pulse = Btn(L.T("Send test pulse"), 20, 250, false);
            pulse.Click += delegate { Pulse(); };
            Button ok = Btn(L.T("Save"), 300, 300, true);
            ok.DialogResult = DialogResult.OK;
            Button cancel = Btn(L.T("Cancel"), 430, 300, false);
            cancel.DialogResult = DialogResult.Cancel;
            AcceptButton = ok; CancelButton = cancel;
            pulseOff.Interval = 500;
            pulseOff.Tick += delegate { pulseOff.Stop(); try { dev.SetConstant(0); dev.EmergencyStop(); } catch { } };
            FormClosing += delegate { pulseOff.Stop(); try { dev.SetConstant(0); dev.EmergencyStop(); } catch { } };
        }

        private Button Btn(string t, int x, int y, bool primary)
        {
            Button b = new Button(); b.Text = t; b.SetBounds(x, y, primary ? 120 : 180, 34);
            b.FlatStyle = FlatStyle.Flat; b.BackColor = primary ? Theme.Yellow : Theme.Card; b.ForeColor = primary ? Color.Black : Theme.Text;
            b.FlatAppearance.BorderColor = primary ? Theme.Yellow : Theme.Line;
            Controls.Add(b);
            return b;
        }

        private void Pulse()
        {
            // calcular com os valores ainda nao gravados
            Settings t = new Settings();
            t.BaseNm = PeakNm; t.VendorFfPercent = VendorFf; t.MaxNm = Math.Min(s.MaxNm, PeakNm);
            double nm = Math.Min(1.0, t.ForceCapNm);
            try
            {
                dev.SetSpring(0, 0, 0); dev.SetDamper(0, 0); dev.SetFriction(0, 0); dev.SetSine(0, 50000); dev.SetEngine(0, 25000);
                dev.SetConstant(t.ToMag(nm));
            }
            catch { }
            pulseOff.Start();
        }
    }
}
