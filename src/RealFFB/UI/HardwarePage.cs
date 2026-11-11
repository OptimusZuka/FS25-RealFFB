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
            sb.AppendLine(string.Format(c, "{0}: {1:0.0} Nm ({2}) | {3}: {4:0} %", L.T("Peak torque"), settings.BaseNm,
                settings.TorqueSource == "catalog" ? L.T("from the catalog") : settings.TorqueSource == "user" ? L.T("set by you") : L.T("ASSUMED - please calibrate"),
                L.T("Manufacturer FF"), settings.VendorFfPercent));
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
            Panic("wheel test (normal force feedback paused)");
            using (WheelTestWizard w = new WheelTestWizard(wheel, settings, Log))
            {
                w.ShowDialog(this);
                if (w.Result != null && w.Result.Steps.Count > 0) { lastWheelTest = w.Result; Log("Wheel test: " + w.Result.Summary()); }
            }
            wheel.EmergencyStop();
            UpdateHardwareInfo();
            Log("Wheel test finished. Turn FFB on again when ready.");
        }

        private void RunCalibration()
        {
            if (wheel == null || !wheel.IsOpen) { Log("Calibration: no wheel open."); return; }
            Panic("calibration (normal force feedback paused)");
            CatalogEntry ce = DeviceCatalog.Find(wheel.OpenVid, wheel.OpenPid, wheel.OpenProduct);
            using (CalibrationWizard w = new CalibrationWizard(wheel, settings, ce))
            {
                if (w.ShowDialog(this) == DialogResult.OK)
                {
                    settings.BaseNm = w.PeakNm;
                    settings.VendorFfPercent = w.VendorFf;
                    settings.TorqueSource = "user";
                    SetSlider("Wheel torque (peak)", (int)Math.Round(settings.BaseNm * 10));
                    settings.TorqueSource = "user";
                    UpdateMaxSlider();
                    try { settings.Save(); } catch { }
                    Log(string.Format(CultureInfo.InvariantCulture, "Calibration: peak {0:0.0} Nm, manufacturer FF {1:0} %.", settings.BaseNm, settings.VendorFfPercent));
                }
            }
            wheel.EmergencyStop();
            UpdateHardwareInfo();
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
