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

        // Entrada de desinstalacao do HidHide no Windows (a visivel em "Aplicacoes", nao os MSI internos).
        public sealed class UninstallEntry { public string Name, Version, UninstallString, QuietUninstallString, KeyName; }

        public static UninstallEntry FindUninstall()
        {
            string[] roots = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };
            UninstallEntry hidden = null;
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
                                if (name == null || name.IndexOf("HidHide", StringComparison.OrdinalIgnoreCase) < 0) continue;
                                UninstallEntry e = new UninstallEntry();
                                e.Name = name; e.KeyName = sub;
                                e.Version = s.GetValue("DisplayVersion") as string;
                                e.UninstallString = s.GetValue("UninstallString") as string;
                                e.QuietUninstallString = s.GetValue("QuietUninstallString") as string;
                                object sc = s.GetValue("SystemComponent");
                                bool sys = sc is int && (int)sc == 1;
                                if (!sys && !string.IsNullOrEmpty(e.UninstallString)) return e;
                                if (hidden == null && !string.IsNullOrEmpty(e.UninstallString)) hidden = e;
                            }
                    }
                }
                catch { }
            }
            return hidden;
        }

        // Comando oficial de desinstalacao (o do proprio HidHide), em modo com progresso e sem reiniciar sozinho.
        // Devolve ficheiro e argumentos; null se nao houver.
        public static string[] UninstallCommand(UninstallEntry e)
        {
            if (e == null || string.IsNullOrEmpty(e.UninstallString)) return null;
            Match msi = Regex.Match(e.UninstallString, "msiexec(\\.exe)?\\s+/[IX]\\s*(\\{[0-9A-Fa-f-]{36}\\})", RegexOptions.IgnoreCase);
            if (msi.Success) return new string[] { "msiexec.exe", "/x " + msi.Groups[2].Value + " /passive /norestart" };
            string cmd = e.UninstallString.Trim();
            string file, args;
            if (cmd.StartsWith("\""))
            {
                int q = cmd.IndexOf('"', 1);
                if (q < 0) return null;
                file = cmd.Substring(1, q - 1); args = cmd.Substring(q + 1).Trim();
            }
            else
            {
                int ex = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (ex < 0) return null;
                file = cmd.Substring(0, ex + 4); args = cmd.Substring(ex + 4).Trim();
            }
            // instalador WiX (Burn): /uninstall com /passive mostra o progresso sem perguntas
            if (args.IndexOf("/uninstall", StringComparison.OrdinalIgnoreCase) >= 0 && args.IndexOf("/passive", StringComparison.OrdinalIgnoreCase) < 0 && args.IndexOf("/quiet", StringComparison.OrdinalIgnoreCase) < 0)
                args += " /passive /norestart";
            return new string[] { file, args };
        }

        // Executa a desinstalacao oficial com elevacao (UAC). Devolve o codigo de saida.
        public static int RunUninstall(string[] cmd)
        {
            ProcessStartInfo psi = new ProcessStartInfo(cmd[0], cmd[1]);
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            try { using (Process p = Process.Start(psi)) { if (!p.WaitForExit(10 * 60 * 1000)) return TimedOut; return p.ExitCode; } }
            catch (Win32Exception ex) { if (ex.NativeErrorCode == 1223) return 1223; throw; }
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
            public string FriendlyName, Product, Vendor, InstancePath, ContainerPath, XusbPath;
            public int Vid, Pid;
            public int ContainerCount;   // baseContainerDeviceCount: funcoes que partilham o contentor (0 = nao indicado)
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
                    g.XusbPath = Field(obj, "xusbDeviceInstancePath");
                    Match cc = Regex.Match(obj, "\"baseContainerDeviceCount\"\\s*:\\s*(\\d+)");
                    if (cc.Success) int.TryParse(cc.Groups[1].Value, out g.ContainerCount);
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

        // Falha ao obter o HidHide: Kind distingue rede/API/limite/sem instalador/integridade.
        public sealed class DownloadException : Exception
        {
            public readonly string Kind;
            public DownloadException(string kind, string msg) : base(msg) { Kind = kind; }
        }

        public sealed class Asset
        {
            public string Version, Name, Url, Sha256;
            public long Size;
            public bool IsMsi { get { return Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase); } }
        }

        public const string OfficialPrefix = "https://github.com/nefarius/HidHide/releases/download/";

        // Le a resposta de /releases/latest com um parser JSON real (os assets tem objetos aninhados,
        // como "uploader", que partiam a leitura por expressoes regulares) e escolhe o instalador x64 oficial.
        public static Asset SelectAsset(string json)
        {
            object root;
            try { root = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }.DeserializeObject(json); }
            catch (Exception ex) { throw new DownloadException("parse", "GitHub API answer could not be read: " + ex.Message); }
            Dictionary<string, object> rel = root as Dictionary<string, object>;
            if (rel == null) throw new DownloadException("parse", "GitHub API answer is not a release object");
            string version = Str(rel, "tag_name") ?? "?";
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase)) version = version.Substring(1);
            object[] assets = Get(rel, "assets") as object[];
            if (assets == null) throw new DownloadException("noasset", "release " + version + " has no files");
            Asset best = null; int bestScore = -1;
            List<string> names = new List<string>();
            foreach (object o in assets)
            {
                Dictionary<string, object> d = o as Dictionary<string, object>;
                if (d == null) continue;
                string name = Str(d, "name") ?? "", url = Str(d, "browser_download_url") ?? "";
                names.Add(name);
                string n = name.ToLowerInvariant();
                if (!url.StartsWith(OfficialPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (!(n.EndsWith(".exe") || n.EndsWith(".msi"))) continue;
                bool x64 = n.Contains("x64") || n.Contains("amd64");
                if (!x64 && (n.Contains("arm64") || n.Contains("x86"))) continue;   // so ARM ou 32 bits
                int score = (x64 ? 4 : 0) + (n.Contains("hidhide") ? 2 : 0) + (n.EndsWith(".exe") ? 1 : 0);
                if (score <= bestScore) continue;
                Asset a = new Asset();
                a.Version = version; a.Name = name; a.Url = url;
                object sz = Get(d, "size"); a.Size = sz == null ? 0 : Convert.ToInt64(sz);
                string dg = Str(d, "digest");
                if (dg != null && dg.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) a.Sha256 = dg.Substring(7).ToLowerInvariant();
                best = a; bestScore = score;
            }
            if (best == null)
                throw new DownloadException("noasset", "no Windows x64 installer in HidHide " + version + " (files: " + string.Join(", ", names.ToArray()) + ")");
            return best;
        }

        private static object Get(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) ? v : null; }
        private static string Str(Dictionary<string, object> d, string k) { return Get(d, k) as string; }

        private static HttpWebRequest Request(string url, int timeoutMs)
        {
            HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "FS25-RealFFB-Installer";
            r.Accept = "application/vnd.github+json";
            r.Timeout = timeoutMs; r.ReadWriteTimeout = timeoutMs;
            r.AllowAutoRedirect = true;
            return r;
        }

        public static string GetLatestJson()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
            try
            {
                using (HttpWebResponse resp = (HttpWebResponse)Request(ReleasesApi, 20000).GetResponse())
                using (StreamReader rd = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return rd.ReadToEnd();
            }
            catch (WebException ex)
            {
                HttpWebResponse hr = ex.Response as HttpWebResponse;
                if (hr == null) throw new DownloadException("network", "no connection to GitHub (" + ex.Status + ": " + ex.Message + ")");
                int code = (int)hr.StatusCode;
                string remaining = hr.Headers["X-RateLimit-Remaining"];
                if ((code == 403 || code == 429) && remaining == "0")
                    throw new DownloadException("ratelimit", "GitHub API limit reached for this connection; try again in about an hour");
                throw new DownloadException("api", "GitHub API answered HTTP " + code + " " + hr.StatusDescription);
            }
        }

        // Descarrega o instalador oficial da ultima versao estavel. Devolve o ficheiro local.
        public static string Download(Action<string> log, out Asset asset)
        {
            asset = SelectAsset(GetLatestJson());
            log("HidHide " + asset.Version + ": " + asset.Name + " (" + asset.Size + " bytes" + (asset.Sha256 != null ? ", SHA-256 published by GitHub" : "") + ")");
            string dir = Path.Combine(Path.GetTempPath(), "FS25RealFFB_hidhide");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, Path.GetFileName(asset.Name));
            log("Downloading " + asset.Url);
            try
            {
                using (HttpWebResponse resp = (HttpWebResponse)Request(asset.Url, 120000).GetResponse())
                using (Stream src = resp.GetResponseStream())
                using (FileStream dst = File.Create(file))
                    src.CopyTo(dst);
            }
            catch (WebException ex) { throw new DownloadException("network", "download failed (" + ex.Status + ": " + ex.Message + ")"); }
            long len = new FileInfo(file).Length;
            if (asset.Size > 0 && len != asset.Size)
            {
                try { File.Delete(file); } catch { }
                throw new DownloadException("integrity", "incomplete download (" + len + " of " + asset.Size + " bytes)");
            }
            string sha = Diagnostics.Sha256(file);
            if (asset.Sha256 != null && asset.Sha256 != sha)
            {
                try { File.Delete(file); } catch { }
                throw new DownloadException("integrity", "SHA-256 does not match the one published by GitHub (" + asset.Sha256 + " vs " + sha + ")");
            }
            asset.Sha256 = sha;
            return file;
        }

        // Executa o instalador oficial (sem o alterar) com elevacao. Devolve o codigo de saida.
        public static int RunInstaller(string file, bool silent)
        {
            bool msi = file.EndsWith(".msi", StringComparison.OrdinalIgnoreCase);
            ProcessStartInfo psi = msi
                ? new ProcessStartInfo("msiexec.exe", "/i \"" + file + "\"" + (silent ? " /qn /norestart" : ""))
                : new ProcessStartInfo(file, silent ? "/exenoui /qn /norestart" : "");
            psi.UseShellExecute = true;
            psi.Verb = "runas";
            try
            {
                // 0.14: nunca esperar para sempre (num teste o instalador oficial ficou parado >20 min em modo
                // silencioso). O processo nao e terminado a forca: a meio da instalacao de um driver isso seria pior.
                using (Process p = Process.Start(psi))
                {
                    if (!p.WaitForExit((silent ? 5 : 20) * 60 * 1000)) return TimedOut;
                    return p.ExitCode;
                }
            }
            catch (Win32Exception ex) { if (ex.NativeErrorCode == 1223) return 1223; throw; }   // 1223 = UAC recusado
        }

        public const int TimedOut = -2;
    }
}
