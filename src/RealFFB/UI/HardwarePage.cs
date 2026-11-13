// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - pagina "Hardware": dispositivos detetados, volante em uso, adaptador, capacidades,
// torque de pico (e de onde vem), nivel de validacao, modo de saida, teste guiado, calibracao e relatorio.
// Regra: nunca mostrar "compativel" sem prova. O nivel vem do catalogo e so "Confirmed" e teste real.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace FS25RealFFB
{
    internal sealed partial class MainForm
    {
        private ListBox hwList;
        private Label hwInfo;
        private ComboBox hwMode;
        private bool fillingHw;
        private WheelTestResult lastWheelTest;

        private void BuildHardwarePage(FlowLayoutPanel flow)
        {
            flow.Controls.Add(Theme.Section(L.T("Wheel in use"), W));
            hwInfo = new Label(); hwInfo.Width = W; hwInfo.Height = 300; hwInfo.ForeColor = Theme.Soft;
            hwInfo.Font = Theme.UI(9f);
            flow.Controls.Add(hwInfo);

            Label mt = new Label(); mt.Width = W; mt.Height = 20; mt.ForeColor = Theme.Soft; mt.Text = L.T("Force output mode");
            flow.Controls.Add(mt);
            hwMode = new ComboBox();
            hwMode.DropDownStyle = ComboBoxStyle.DropDownList; hwMode.Width = W;
            hwMode.FlatStyle = FlatStyle.Flat; hwMode.BackColor = Theme.Card; hwMode.ForeColor = Theme.Text;
            hwMode.Items.Add(L.T("Automatic (recommended)"));
            hwMode.Items.Add(L.T("Condition effects (the wheel's own spring/damper)"));
            hwMode.Items.Add(L.T("Computed torque - experimental"));
            hwMode.SelectedIndex = settings.OutputMode == "conditions" ? 1 : settings.OutputMode == "torque" ? 2 : 0;
            hwMode.SelectedIndexChanged += delegate
            {
                if (fillingHw) return;
                settings.OutputMode = hwMode.SelectedIndex == 1 ? "conditions" : hwMode.SelectedIndex == 2 ? "torque" : "auto";
                try { settings.Save(); } catch { }
                Log("Output mode: " + settings.OutputMode + " (applies next time the app opens).");
                UpdateHardwareInfo();
            };
            flow.Controls.Add(hwMode);
            tips.SetToolTip(hwMode, L.T("Condition effects: the wheel base computes centering, damping and friction itself (validated on the CSL DD). Computed torque: the app computes them and sends only constant force; for wheels whose spring/damper are missing or muted by the manufacturer's software. Experimental."));

            Button test = Theme.Btn(L.T("Test my wheel (guided, low forces)"), W, 32, true);
            test.Click += delegate { RunWheelTest(); };
            flow.Controls.Add(test);
            Button cal = Theme.Btn(L.T("Calibrate force (peak torque and manufacturer FF %)"), W, 30, false);
            cal.Click += delegate { RunCalibration(); };
            flow.Controls.Add(cal);
            Button rep = Theme.Btn(L.T("Export compatibility report"), W, 30, false);
            rep.Click += delegate { ExportCompatibilityReport(); };
            tips.SetToolTip(rep, L.T("Creates an anonymous text report (no user or PC name) with the wheel, its capabilities and the test results, to share in a GitHub issue."));
            flow.Controls.Add(rep);

            flow.Controls.Add(Theme.Section(L.T("Detected devices"), W));
            hwList = new ListBox(); hwList.Width = W; hwList.Height = 150;
            hwList.BackColor = Theme.Card; hwList.ForeColor = Theme.Text; hwList.BorderStyle = BorderStyle.None;
            flow.Controls.Add(hwList);
            Button refresh = Theme.Btn(L.T("Refresh list"), W, 28, false);
            refresh.Click += delegate { RefreshDeviceList(); };
            flow.Controls.Add(refresh);
            Label note = new Label(); note.Width = W; note.Height = 54; note.ForeColor = Theme.Muted;
            note.Text = L.T("Only devices with force feedback can be used as the wheel. Pedals, shifters and handbrakes are listed for information; they never receive forces.");
            flow.Controls.Add(note);
        }

        private static string KindText(DeviceKind k)
        {
            switch (k)
            {
                case DeviceKind.Wheelbase: return L.T("wheel base");
                case DeviceKind.Pedals: return L.T("pedals");
                case DeviceKind.Shifter: return L.T("shifter");
                case DeviceKind.Handbrake: return L.T("handbrake");
                case DeviceKind.Gamepad: return L.T("gamepad");
                case DeviceKind.Joystick: return L.T("joystick");
                default: return L.T("other");
            }
        }

        private void RefreshDeviceList()
        {
            if (hwList == null) return;
            hwList.Items.Clear();
            List<DeviceDescriptor> devs = DirectInputProvider.Enumerate(Log);
            foreach (DeviceDescriptor d in devs)
                hwList.Items.Add("[" + KindText(d.Kind) + (d.HasForceFeedback ? ", FFB" : "") + "]  " + d.Product + "  (" + d.VidPid + ")");
            if (devs.Count == 0) hwList.Items.Add(L.T("No game controllers found (the wheel hidden by HidHide is still visible to this app)."));
            Log("Hardware: " + devs.Count + " game controller(s) listed.");
        }

        private static string StatusMark(EffectStatus s)
        {
            switch (s)
            {
                case EffectStatus.Playing: return L.T("working");
                case EffectStatus.Created: return L.T("created, not confirmed");
                case EffectStatus.Declared: return L.T("declared only");
                case EffectStatus.Failed: return L.T("FAILED");
                default: return L.T("not available");
            }
        }

        private static string Manufacturer(int vid, string product)
        {
            switch (vid)
            {
                case 0x0EB7: return "Fanatec";
                case 0x046D: return "Logitech";
                case 0x044F: return "Thrustmaster";
                case 0x346E: return "MOZA";
            }
            string p = (product ?? "").Trim();
            int sp = p.IndexOf(' ');
            return p.Length == 0 ? L.T("unknown") : (sp > 0 ? p.Substring(0, sp) : p) + " (" + L.T("from the device name") + ")";
        }

        private void UpdateHardwareInfo()
        {
            if (hwInfo == null) return;
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            if (wheel == null || !wheel.IsOpen)
            {
                sb.AppendLine(L.T("No wheel with force feedback is open."));
                hwInfo.Text = sb.ToString();
                return;
            }
            CatalogEntry ce = DeviceCatalog.Find(wheel.OpenVid, wheel.OpenPid, wheel.OpenProduct);
            sb.AppendLine(wheel.OpenProduct + "  (" + wheel.OpenVid.ToString("X4") + ":" + wheel.OpenPid.ToString("X4") + ")");
            sb.AppendLine(L.T("Interface") + ": DirectInput 8  |  " + L.T("Adapter") + ": " + (adapter != null ? adapter.Name : "-"));
            sb.AppendLine(L.T("Validation") + ": " + DeviceCatalog.LevelText(ce.Validation));
            // 0.15: fabricante, modelo, pico do fabricante, pico configurado e maximo permitido, separados
            sb.AppendLine(L.T("Manufacturer") + ": " + Manufacturer(wheel.OpenVid, wheel.OpenProduct) + "  |  " + L.T("Model") + ": "
                + (ce.CeilingNm > 0 ? ce.Model + " (" + (ce.IdentifiedBy == "usb" ? L.T("identified by USB ID") : L.T("identified by device name")) + ")"
                   : settings.HwPeakNm > 0 ? settings.HwModel
                   : L.T("not identified")));
            sb.AppendLine(settings.HwPeakNm > 0
                ? string.Format(c, "{0}: {1:0.0} Nm{2}", L.T("Manufacturer peak torque"), settings.HwPeakNm, ce.CeilingNm > 0 && ce.Sustained ? " (" + L.T("sustained rating") + ")" : "")
                : L.T("Manufacturer peak torque") + ": " + L.T("unknown (choose it in Calibrate force)"));
            sb.AppendLine(string.Format(c, "{0}: {1:0.0} Nm ({2}) | {3}: {4:0} %", L.T("Configured peak torque"), settings.BaseNm,
                settings.TorqueSource == "catalog" ? L.T("from the catalog") : settings.TorqueSource == "user" ? L.T("set by you") : L.T("ASSUMED - please calibrate"),
                L.T("Manufacturer FF"), settings.VendorFfPercent));
            sb.AppendLine(string.Format(c, "{0}: {1:0.0} Nm{2}", L.T("Maximum allowed"), TorqueLimits.Ceiling(settings.HwPeakNm),
                settings.HwPeakNm > 0 ? "" : " (" + L.T("model not identified") + ")"));
            string below = TorqueLimits.BelowSpecWarning(settings.BaseNm, settings.HwPeakNm, settings.HwModel);
            if (below.Length > 0) sb.AppendLine("! " + L.T(below));
            sb.AppendLine();
            sb.AppendLine(L.T("Effects") + ":");
            foreach (string n in Capabilities.Names) sb.AppendLine("   " + n + ": " + StatusMark(wheel.Caps.Get(n)));
            if (wheel.Caps.FfSamplePeriodUs > 0) sb.AppendLine("   " + L.T("FF sample period") + ": " + wheel.Caps.FfSamplePeriodUs + " us");
            if (lastWheelTest != null) { sb.AppendLine(); sb.AppendLine(L.T("Guided test") + ": " + lastWheelTest.Summary()); }
            if (ce.Notes.Length > 0) { sb.AppendLine(); sb.AppendLine(ce.Notes); }
            DiTorqueAdapter ta = adapter as DiTorqueAdapter;
            if (ta != null) sb.AppendLine(L.F("Computed-torque loop: {0} Hz", ta.LoopHz));
            hwInfo.Text = sb.ToString();
            hwInfo.ForeColor = ce.Validation == ValidationLevel.Confirmed ? Theme.Text : Theme.Soft;
        }

        private void RunWheelTest()
        {
            if (wheel == null || !wheel.IsOpen) { Log("Wheel test: no wheel open."); return; }
            bool wasOn = enableBox.Checked;
            Panic("wheel test (normal force feedback paused)");
            using (WheelTestWizard w = new WheelTestWizard(wheel, settings, Log))
            {
                w.ShowDialog(this);
                if (w.Result != null && w.Result.Steps.Count > 0) { lastWheelTest = w.Result; Log("Wheel test: " + w.Result.Summary()); }
            }
            wheel.EmergencyStop();
            UpdateHardwareInfo();
            RestoreFfbAfter("wheel test", wasOn);
        }

        // 0.15: depois do teste/calibracao o FFB voltava a ficar desligado ate o utilizador carregar em "FFB ON".
        // Num relatorio real o testador conduziu com o FFB desligado (ganho 0 %) sem saber porque. Agora volta ao
        // estado anterior, mas so se estava ligado antes e o volante continua aberto e sem falhas.
        private void RestoreFfbAfter(string what, bool wasOn)
        {
            if (wasOn && wheel != null && wheel.IsOpen)
            {
                engine.Halt(); mixer.Reset();
                enableBox.Checked = true;
                Log("FFB restored after the " + what + " (it was on before).");
            }
            else
            {
                ffbOffReason = what + " finished - press FFB ON to start force feedback";
                Log(wasOn ? "FFB stays off after the " + what + ": the wheel is not open." : "FFB stays off after the " + what + " (it was off before).");
            }
        }

        private void RunCalibration()
        {
            if (wheel == null || !wheel.IsOpen) { Log("Calibration: no wheel open."); return; }
            bool wasOn = enableBox.Checked;
            Panic("calibration (normal force feedback paused)");
            CatalogEntry ce = DeviceCatalog.Find(wheel.OpenVid, wheel.OpenPid, wheel.OpenProduct);
            using (CalibrationWizard w = new CalibrationWizard(wheel, settings, ce))
            {
                if (w.ShowDialog(this) == DialogResult.OK)
                {
                    // 0.15: o assistente ja limita o valor ao teto; esta verificacao repete-a (nenhum caminho passa o teto)
                    if (w.ChosenModel != null)
                    {
                        // o utilizador confirmou o modelo: o pico desse modelo passa a ser o teto deste volante
                        settings.UserModel = w.ChosenModel.Model; settings.UserModelPeakNm = w.ChosenModel.PeakNm;
                        settings.UserModelDevice = wheel.OpenVid.ToString("X4") + ":" + wheel.OpenPid.ToString("X4");
                        settings.HwPeakNm = w.ChosenModel.PeakNm; settings.HwModel = w.ChosenModel.Model + " (chosen by you)";
                        Log(string.Format(CultureInfo.InvariantCulture, "Calibration: model confirmed by the user: {0} ({1:0.0} Nm).", w.ChosenModel.Model, w.ChosenModel.PeakNm));
                    }
                    string why;
                    double accepted = TorqueLimits.Validate(w.PeakNm, settings.HwPeakNm, settings.HwModel, out why);
                    if (why.Length > 0)
                    {
                        MessageBox.Show(this, L.F("{0} Nm cannot be used: {1}\n\nThe peak torque was set to {2} Nm.", w.PeakNm.ToString("0.0"), why, accepted.ToString("0.0")),
                            L.T("Calibrate force"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        Log(string.Format(CultureInfo.InvariantCulture, "Calibration: {0:0.0} Nm rejected ({1}); using {2:0.0} Nm.", w.PeakNm, why, accepted));
                    }
                    settings.BaseNm = accepted;
                    settings.VendorFfPercent = w.VendorFf;
                    settings.TorqueSource = "user";
                    SetSlider("Wheel torque (peak)", (int)Math.Round(settings.BaseNm * 10));
                    settings.TorqueSource = "user";
                    UpdateMaxSlider();
                    try { settings.Save(); } catch { }
                    Log(string.Format(CultureInfo.InvariantCulture, "Calibration: peak {0:0.0} Nm, manufacturer FF {1:0} %.", settings.BaseNm, settings.VendorFfPercent));
                    string warn = TorqueLimits.BelowSpecWarning(settings.BaseNm, settings.HwPeakNm, settings.HwModel);
                    if (warn.Length > 0) Log("WARNING: " + warn);
                }
            }
            wheel.EmergencyStop();
            UpdateHardwareInfo();
            RestoreFfbAfter("calibration", wasOn);
        }

        private void ExportCompatibilityReport()
        {
            try
            {
                string path = AppInfo.DataFile("compatibility-report.txt");
                System.IO.File.WriteAllText(path, CompatibilityReport.Build(wheel, adapter, settings, lastWheelTest), Encoding.UTF8);
                Log("Compatibility report saved to " + path);
                System.Diagnostics.Process.Start("notepad.exe", "\"" + path + "\"");
            }
            catch (Exception ex) { Log("Compatibility report failed: " + ex.Message); }
        }
    }
}
