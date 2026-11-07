// FS25 Real FFB - liga/desliga o HidHide automaticamente.
// App aberta -> Fanatec escondida (o FS25 nao a agarra; a app esta na lista de excecoes).
// App fechada -> Fanatec visivel para os outros jogos (iRacing, ACC, ETS2...).
// Usa o HidHideCLI oficial (nao precisa de administrador).
using System;
using System.Diagnostics;
using System.IO;

namespace FS25RealFFB
{
    internal static class HidHideControl
    {
        private static string Cli()
        {
            string pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            string a = Path.Combine(pf, @"Nefarius Software Solutions\HidHide\HidHideCLI.exe");
            string b = Path.Combine(pf, @"Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe");
            if (File.Exists(a)) return a;
            if (File.Exists(b)) return b;
            return null;
        }

        public static bool Available { get { return Cli() != null; } }

        public static string Run(string args)
        {
            string cli = Cli();
            if (cli == null) return "HidHideCLI not found";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(cli, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return "no response (is the HidHide Configuration Client open? close it)"; }
                    return p.ExitCode == 0 ? "ok" + (output.Trim().Length > 0 ? " (" + output.Trim() + ")" : "") : "error " + p.ExitCode + ": " + output.Trim();
                }
            }
            catch (Exception ex) { return "failed: " + ex.Message; }
        }

        public static string On() { return Run("--cloak-on"); }
        public static string Off() { return Run("--cloak-off"); }
    }
}
