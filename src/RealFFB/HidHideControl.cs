// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - liga/desliga o HidHide automaticamente.
// App aberta -> volante escondido (o FS25 nao o agarra; a app esta na lista de excecoes).
// App fechada -> volante visivel para os outros jogos (iRacing, ACC, ETS2...).
// Usa o HidHideCLI oficial (nao precisa de administrador).
//
// 0.10.0:
//  - o limite de 5 s e real: as saidas do CLI sao lidas de forma assincrona (antes a leitura bloqueava
//    antes de esperar, e um CLI pendurado congelava a app para sempre);
//  - Off() tenta de novo se falhar (o log de 2026-11-08 mostrou o "desligar" a falhar com 0xC0000142
//    quando o Windows estava a encerrar, e o volante ficou escondido);
//  - "--unhide" no arranque do Windows (registo em HKCU\...\Run, sem administrador): se a app nao estiver
//    aberta, desliga o esconderijo deixado por um fecho a forca, falta de luz ou encerramento do Windows.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace FS25RealFFB
{
    internal static class HidHideControl
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "FS25 RealFFB - show wheel";

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
                StringBuilder output = new StringBuilder();
                object gate = new object();
                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.OutputDataReceived += delegate(object so, DataReceivedEventArgs eo) { if (eo.Data != null) lock (gate) output.AppendLine(eo.Data); };
                    p.ErrorDataReceived += delegate(object se, DataReceivedEventArgs ee) { if (ee.Data != null) lock (gate) output.AppendLine(ee.Data); };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(5000))
                    {
                        try { p.Kill(); } catch { }
                        return "no response (is the HidHide Configuration Client open? close it)";
                    }
                    p.WaitForExit();   // espera tambem pelo fim das leituras assincronas
                    string text;
                    lock (gate) text = output.ToString().Trim();
                    return p.ExitCode == 0 ? "ok" + (text.Length > 0 ? " (" + text + ")" : "") : "error " + p.ExitCode + ": " + text;
                }
            }
            catch (Exception ex) { return "failed: " + ex.Message; }
        }

        public static string On() { return Run("--cloak-on"); }

        // Desligar e critico (senao o volante fica escondido de todos os programas): ate 3 tentativas.
        public static string Off()
        {
            string r = Run("--cloak-off");
            for (int i = 0; i < 2 && !r.StartsWith("ok"); i++)
            {
                Thread.Sleep(400);
                r = Run("--cloak-off") + " (retry " + (i + 1) + ")";
            }
            return r;
        }

        // Regista (ou remove) o "--unhide" no arranque do Windows para este utilizador.
        public static string SetStartupUnhide(bool on, string exePath)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return "registry not available";
                    if (on) k.SetValue(RunValue, "\"" + exePath + "\" --unhide");
                    else if (k.GetValue(RunValue) != null) k.DeleteValue(RunValue, false);
                }
                return "ok";
            }
            catch (Exception ex) { return "failed: " + ex.Message; }
        }
    }
}
