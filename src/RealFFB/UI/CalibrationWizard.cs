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
        public CatalogEntry ChosenModel;   // 0.15: modelo escolhido na lista (modelo nao identificado automaticamente)

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

            // 0.15: modelo nao identificado -> escolher o modelo da lista (o pico vem do catalogo) ou escrever o valor
            int dy = 0;
            bool identified = ce != null && ce.CeilingNm > 0;
            double ceiling = TorqueLimits.Ceiling(s.HwPeakNm);
            if (!identified)
            {
                Label lm = new Label(); lm.SetBounds(20, 100, 330, 24); lm.Text = L.T("Your wheel base model"); Controls.Add(lm);
                ComboBox models = new ComboBox(); models.DropDownStyle = ComboBoxStyle.DropDownList;
                models.SetBounds(360, 98, 180, 24);
                models.Items.Add(L.T("(not in the list: enter the value)"));
                System.Collections.Generic.List<CatalogEntry> list = DeviceCatalog.ModelsWithTorque();
                foreach (CatalogEntry m in list) models.Items.Add(m.Model + string.Format(CultureInfo.InvariantCulture, " - {0:0.0} Nm", m.PeakNm));
                models.SelectedIndex = 0;
                models.SelectedIndexChanged += delegate
                {
                    int i = models.SelectedIndex - 1;
                    ChosenModel = i >= 0 && i < list.Count ? list[i] : null;
                    if (ChosenModel != null) peak.Value = (decimal)Math.Max(1, Math.Min(ceiling, ChosenModel.PeakNm));
                };
                Controls.Add(models);
                dy = 40;
            }
            Label l1 = new Label(); l1.SetBounds(20, 100 + dy, 330, 24); l1.Text = L.T("Peak torque of your base (Nm)"); Controls.Add(l1);
            peak.SetBounds(360, 98 + dy, 120, 24); peak.DecimalPlaces = 1; peak.Increment = 0.5M; peak.Minimum = (decimal)TorqueLimits.MinNm;
            // teto duro: nao se consegue escrever um valor acima do pico do fabricante do modelo identificado
            peak.Maximum = (decimal)ceiling;
            peak.Value = (decimal)Math.Max(TorqueLimits.MinNm, Math.Min(ceiling, s.BaseNm)); Controls.Add(peak);
            Label lmax = new Label(); lmax.SetBounds(484, 100 + dy, 70, 24); lmax.ForeColor = Theme.Muted;
            lmax.Text = string.Format(CultureInfo.InvariantCulture, "max {0:0.0}", ceiling); Controls.Add(lmax);
            Label l2 = new Label(); l2.SetBounds(20, 140 + dy, 330, 24); l2.Text = L.T("Force feedback % in the manufacturer's software"); Controls.Add(l2);
            ff.SetBounds(360, 138 + dy, 120, 24); ff.Minimum = 10; ff.Maximum = 100; ff.Value = (decimal)Math.Max(10, Math.Min(100, s.VendorFfPercent)); Controls.Add(ff);

            Label l3 = new Label(); l3.SetBounds(20, 182 + dy, 520, 60); l3.ForeColor = Theme.Muted;
            l3.Text = (identified
                ? L.F("{0}: the manufacturer's peak torque is {1} Nm. A higher value is not accepted; a lower one only if your base is limited.", ce.Model, ce.PeakNm.ToString("0.0", CultureInfo.InvariantCulture))
                : L.T("Model not identified: choose it above or enter its peak torque. Until then the app assumes a strong base, so forces stay low."))
                + "\n" + L.T("Test pulse: the app pushes the wheel with about 1 Nm for half a second (never above your Maximum force). On most bases this is a light but clear push.");
            Controls.Add(l3);
            ClientSize = new Size(560, 360 + dy + 20);
            Button pulse = Btn(L.T("Send test pulse"), 20, 250 + dy + 10, false);
            pulse.Click += delegate { Pulse(); };
            Button ok = Btn(L.T("Save"), 300, 300 + dy + 20, true);
            ok.DialogResult = DialogResult.OK;
            Button cancel = Btn(L.T("Cancel"), 430, 300 + dy + 20, false);
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
            t.BaseNm = PeakNm; t.HwPeakNm = s.HwPeakNm; t.VendorFfPercent = VendorFf; t.MaxNm = Math.Min(s.MaxNm, PeakNm);
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
