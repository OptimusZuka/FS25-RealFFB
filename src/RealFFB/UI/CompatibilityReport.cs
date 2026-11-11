// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - relatorio de compatibilidade (formato em docs/dev/DEVICE-REPORT-FORMAT.md).
// Anonimo: sem nome de utilizador, nome do PC nem caminhos. O GUID de instancia do volante e reduzido
// a um resumo SHA-256 de 8 caracteres (so serve para distinguir dois relatorios do mesmo volante).
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FS25RealFFB
{
    internal static class CompatibilityReport
    {
        public const int FormatVersion = 1;

        public static string ShortHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return "-";
            using (SHA256 h = SHA256.Create())
            {
                byte[] b = h.ComputeHash(Encoding.UTF8.GetBytes(s));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 4; i++) sb.Append(b[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static string Build(FfbDevice wheel, IFfbAdapter adapter, Settings s, WheelTestResult test)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("FS25 RealFFB - compatibility report (format " + FormatVersion + ")");
            sb.AppendLine("app_version: " + AppInfo.Version);
            sb.AppendLine("date: " + DateTime.UtcNow.ToString("yyyy-MM-dd", c));
            sb.AppendLine("windows: " + Environment.OSVersion.Version + (Environment.Is64BitOperatingSystem ? " x64" : " x86"));
            if (wheel == null || !wheel.IsOpen)
            {
                sb.AppendLine("wheel: none open");
                return sb.ToString();
            }
            CatalogEntry ce = DeviceCatalog.Find(wheel.OpenVid, wheel.OpenPid, wheel.OpenProduct);
            sb.AppendLine("product: " + wheel.OpenProduct);
            sb.AppendLine("vid_pid: " + wheel.OpenVid.ToString("X4") + ":" + wheel.OpenPid.ToString("X4"));
            sb.AppendLine("instance_hash: " + ShortHash(wheel.OpenInstance));
            sb.AppendLine("catalog_model: " + (ce.Model.Length > 0 ? ce.Model : "-"));
            sb.AppendLine("catalog_validation: " + ce.Validation);
            sb.AppendLine("catalog_source: " + DeviceCatalog.LoadNote);
            sb.AppendLine("quirks: " + wheel.Quirks);
            sb.AppendLine("interface: DirectInput 8");
            sb.AppendLine("adapter: " + (adapter != null ? adapter.Name + " v" + adapter.Version : "-"));
            sb.AppendLine("output_mode_setting: " + s.OutputMode);
            sb.AppendLine("effects: " + wheel.Caps.Summary());
            sb.AppendLine("declared_effects: " + string.Join(", ", wheel.Caps.DeclaredEffectNames.ToArray()));
            sb.AppendLine(string.Format(c, "peak_nm: {0:0.0} ({1})", s.BaseNm, s.TorqueSource.Length > 0 ? s.TorqueSource : "-"));
            sb.AppendLine(string.Format(c, "vendor_ff_percent: {0:0}", s.VendorFfPercent));
            sb.AppendLine(string.Format(c, "max_force_nm: {0:0.0}", s.MaxNm));
            sb.AppendLine("drive_profile: " + (s.GearDriven ? "gear/belt" : "direct drive"));
            sb.AppendLine("health: " + wheel.Health);
            DiTorqueAdapter ta = adapter as DiTorqueAdapter;
            if (ta != null) sb.AppendLine("torque_loop_hz: " + ta.LoopHz + " | stale_stops: " + ta.StaleStops);
            sb.AppendLine("guided_test: " + (test != null && test.Steps.Count > 0 ? test.Summary() : "not run"));
            sb.AppendLine();
            sb.AppendLine("Physical result reported by the user (edit before sending):");
            sb.AppendLine("  works_in_FS25: yes / partly / no");
            sb.AppendLine("  notes: ");
            return sb.ToString();
        }
    }
}
