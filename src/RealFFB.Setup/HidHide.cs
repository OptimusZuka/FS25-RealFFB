// HidHide: deteccao, instalacao a partir da fonte oficial (GitHub nefarius/HidHide),
// verificacao de assinatura digital e configuracao pelo HidHideCLI oficial.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace RealFFBSetup
{
    internal static class HidHide
    {
        public const string MinVersion = "1.2.0";
        public const string ReleasesApi = "https://api.github.com/repos/nefarius/HidHide/releases/latest";

        public static string Cli()
        {
            string pf = Environment.GetEnvironmentVariable("ProgramW6432") ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string[] c = {
                Path.Combine(pf, @"Nefarius Software Solutions\HidHide\HidHideCLI.exe"),
                Path.Combine(pf, @"Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe") };
            foreach (string p in c) if (File.Exists(p)) return p;
            return null;
        }

        // Versao instalada (entrada de desinstalacao do Windows), ou null.
        public static string InstalledVersion()
        {
            string[] roots = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };
            foreach (string r in roots)
            {
                try
                {
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(r))
                    {
                        if (k == null) continue;
                        foreach (string sub in k.GetSubKeyNames())
                            using (RegistryKey s = k.OpenSubKey(sub))
                            {
                                string name = s == null ? null : s.GetValue("DisplayName") as string;
                                if (name != null && name.IndexOf("HidHide", StringComparison.OrdinalIgnoreCase) >= 0)
                                    return (s.GetValue("DisplayVersion") as string) ?? "0";
                            }
                    }
                }
                catch { }
            }
            return Cli() != null ? "desconhecida" : null;
        }

        public static int CompareVersions(string a, string b)
        {
            string[] x = a.Split('.'), y = b.Split('.');
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                int p = 0, q = 0;
                if (i < x.Length) int.TryParse(Regex.Match(x[i], "^\\d+").Value, out p);
                if (i < y.Length) int.TryParse(Regex.Match(y[i], "^\\d+").Value, out q);
                if (p != q) return p.CompareTo(q);
            }
            return 0;
        }

        // ------------------------------------------------------------------ CLI
        // Devolve o texto escrito pela CLI; lanca excecao se falhar.
        public static string Run(string args)
        {
            string cli = Cli();
            if (cli == null) throw new InvalidOperationException("HidHideCLI nao encontrado");
            ProcessStartInfo psi = new ProcessStartInfo(cli, args);
            psi.UseShellExecute = false; psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            using (Process p = Process.Start(psi))
            {
                MemoryStream ms = new MemoryStream();
                p.StandardOutput.BaseStream.CopyTo(ms);
                string o = Decode(ms.ToArray());
                string e = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(15000)) { try { p.Kill(); } catch { } throw new InvalidOperationException("HidHideCLI sem resposta (feche o HidHide Configuration Client)"); }
                if (p.ExitCode != 0) throw new InvalidOperationException("HidHideCLI " + args + " -> codigo " + p.ExitCode + " " + e.Trim());
                return o;
            }
        }

        // A CLI pode escrever UTF-16 ou ANSI/UTF-8 consoante a versao: detetar pelos bytes nulos.
        public static string Decode(byte[] b)
        {
            int zeros = 0;
            for (int i = 1; i < b.Length && i < 400; i += 2) if (b[i] == 0) zeros++;
            if (b.Length >= 2 && zeros * 4 >= Math.Min(b.Length, 400) / 2) return Encoding.Unicode.GetString(b).TrimStart('\uFEFF');
            return Encoding.UTF8.GetString(b).TrimStart('\uFEFF');
        }

        // --app-list / --dev-list escrevem linhas do tipo: --app-reg "caminho"
        public static List<string> ParseList(string output, string option)
        {
            List<string> r = new List<string>();
            foreach (Match m in Regex.Matches(output, "--" + option + "\\s+\"([^\"]+)\""))
                r.Add(m.Groups[1].Value);
            return r;
        }

        public static List<string> Apps() { return ParseList(Run("--app-list"), "app-reg"); }
        public static List<string> HiddenDevices() { return ParseList(Run("--dev-list"), "dev-hide"); }
        public static bool CloakOn() { return Run("--cloak-state").Contains("--cloak-on"); }

        public sealed class GamingDevice
        {
            public string FriendlyName, Product, Vendor, InstancePath, ContainerPath;
            public int Vid, Pid;
        }

        // --dev-gaming: JSON com os dispositivos de jogo. Le-se cada objeto que tenha deviceInstancePath,
        // sem depender da ordem dos campos nem do aninhamento.
        public static List<GamingDevice> GamingDevices() { return ParseGaming(Run("--dev-gaming")); }

        public static List<GamingDevice> ParseGaming(string json)
        {
            List<GamingDevice> list = new List<GamingDevice>();
            // Objetos-folha (sem objetos dentro), ignorando chavetas dentro de strings:
            // o HidHide escreve GUIDs como "{745A17A0-...}".
            List<int> stack = new List<int>();
            List<bool> hasChild = new List<bool>();
            bool inStr = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inStr)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') inStr = true;
                else if (c == '{')
                {
                    if (hasChild.Count > 0) hasChild[hasChild.Count - 1] = true;
                    stack.Add(i); hasChild.Add(false);
                }
                else if (c == '}' && stack.Count > 0)
                {
                    int start = stack[stack.Count - 1];
                    bool child = hasChild[hasChild.Count - 1];
                    stack.RemoveAt(stack.Count - 1); hasChild.RemoveAt(hasChild.Count - 1);
                    if (child) continue;
                    string obj = json.Substring(start, i - start + 1);
                    GamingDevice g = new GamingDevice();
                    g.InstancePath = Field(obj, "deviceInstancePath");
                    if (string.IsNullOrEmpty(g.InstancePath)) continue;
                    g.ContainerPath = Field(obj, "baseContainerDeviceInstancePath");
                    g.Product = Field(obj, "product");
                    g.Vendor = Field(obj, "vendor");
                    g.FriendlyName = Field(obj, "friendlyName");
                    if (g.FriendlyName == null)
                    {   // nome do grupo (objeto pai), antes deste objeto
                        MatchCollection fn = Regex.Matches(json.Substring(0, start), "\"friendlyName\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                        if (fn.Count > 0) g.FriendlyName = Unescape(fn[fn.Count - 1].Groups[1].Value);
                    }
                    Match vp = Regex.Match(g.InstancePath, "VID_([0-9A-Fa-f]{4})&PID_([0-9A-Fa-f]{4})");
                    if (vp.Success) { g.Vid = Convert.ToInt32(vp.Groups[1].Value, 16); g.Pid = Convert.ToInt32(vp.Groups[2].Value, 16); }
                    list.Add(g);
                }
            }
            return list;
        }

        private static string Field(string obj, string name)
        {
            Match m = Regex.Match(obj, "\"" + name + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Unescape(m.Groups[1].Value) : null;
        }

        // Escapes JSON: \\ \" \/ \uXXXX
        public static string Unescape(string s)
        {
            return Regex.Replace(s, "\\\\(u[0-9a-fA-F]{4}|.)", delegate(Match m)
            {
                string e = m.Groups[1].Value;
                if (e.Length == 5) return ((char)Convert.ToInt32(e.Substring(1), 16)).ToString();
                switch (e) { case "n": return "\n"; case "t": return "\t"; case "r": return "\r"; default: return e; }
            });
        }

        // ------------------------------------------------------------------ instalacao
        [StructLayout(LayoutKind.Sequential)]
        private struct WINTRUST_FILE_INFO { public int cbStruct; [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINTRUST_DATA
        {
            public int cbStruct; public IntPtr pPolicyCallbackData; public IntPtr pSIPClientData; public int dwUIChoice;
            public int fdwRevocationChecks; public int dwUnionChoice; public IntPtr pFile; public int dwStateAction;
            public IntPtr hWVTStateData; public IntPtr pwszURLReference; public int dwProvFlags; public int dwUIContext; public IntPtr pSignatureSettings;
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, ref WINTRUST_DATA data);

        // Assinatura Authenticode valida (cadeia de confianca do Windows) e emitida para a Nefarius.
        public static bool VerifySignature(string file, out string signer)
        {
            signer = "";
            try { signer = new X509Certificate(X509Certificate.CreateFromSignedFile(file)).Subject; } catch { return false; }
            WINTRUST_FILE_INFO fi = new WINTRUST_FILE_INFO();
            fi.cbStruct = Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)); fi.pcwszFilePath = file;
            IntPtr pfi = Marshal.AllocHGlobal(fi.cbStruct);
            try
            {
                Marshal.StructureToPtr(fi, pfi, false);
                WINTRUST_DATA wd = new WINTRUST_DATA();
                wd.cbStruct = Marshal.SizeOf(typeof(WINTRUST_DATA));
                wd.dwUIChoice = 2;          // WTD_UI_NONE
                wd.fdwRevocationChecks = 0; // WTD_REVOKE_NONE
                wd.dwUnionChoice = 1;       // WTD_CHOICE_FILE
                wd.pFile = pfi;
                Guid action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE"); // WINTRUST_ACTION_GENERIC_VERIFY_V2
                int r = WinVerifyTrust(new IntPtr(-1), action, ref wd);
                return r == 0 && signer.IndexOf("Nefarius", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            finally { Marshal.FreeHGlobal(pfi); }
        }

        // Descarrega o instalador oficial da ultima versao. Devolve o caminho local.
        public static string Download(Action<string> log, out string version, out string sha256)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            version = null; sha256 = null;
            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "FS25-RealFFB-Installer";
                string json = wc.DownloadString(ReleasesApi);
                Match tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"");
                version = tag.Success ? tag.Groups[1].Value : "?";
                string url = null, digest = null;
                foreach (Match a in Regex.Matches(json, "\\{[^{}]*\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"[^{}]*\\}", RegexOptions.Singleline))
                {
                    string u = a.Groups[1].Value;
                    string name = u.Substring(u.LastIndexOf('/') + 1);
                    if (!Regex.IsMatch(name, "^HidHide.*x64.*\\.exe$", RegexOptions.IgnoreCase)) continue;
                    if (!u.StartsWith("https://github.com/nefarius/HidHide/releases/download/", StringComparison.OrdinalIgnoreCase)) continue;
                    url = u;
                    Match dg = Regex.Match(a.Value, "\"digest\"\\s*:\\s*\"sha256:([0-9a-fA-F]{64})\"");
                    if (dg.Success) digest = dg.Groups[1].Value;
                    break;
                }
                if (url == null) throw new InvalidOperationException("instalador x64 do HidHide nao encontrado na ultima versao oficial");
                string dir = Path.Combine(Path.GetTempPath(), "FS25RealFFB_hidhide");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, url.Substring(url.LastIndexOf('/') + 1));
                log("A descarregar " + url);
                wc.DownloadFile(url, file);
                sha256 = Diagnostics.Sha256(file);
                if (digest != null && !digest.Equals(sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("SHA-256 do HidHide nao confere com o publicado pelo GitHub (" + digest + " vs " + sha256 + ")");
                return file;
            }
        }

        // Executa o instalador oficial (sem o alterar) com elevacao. Devolve o codigo de saida.
        public static int RunInstaller(string file, bool silent)
        {
            ProcessStartInfo psi = new ProcessStartInfo(file, silent ? "/exenoui /qn /norestart" : "");
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            try
            {
                using (Process p = Process.Start(psi)) { p.WaitForExit(); return p.ExitCode; }
            }
            catch (Win32Exception ex) { if (ex.NativeErrorCode == 1223) return 1223; throw; }   // 1223 = UAC recusado
        }
    }
}
