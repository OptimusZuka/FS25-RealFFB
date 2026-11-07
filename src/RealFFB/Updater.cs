// FS25 Real FFB - verificacao de atualizacoes no GitHub Releases.
// So informa; descarregar e instalar e sempre uma decisao do utilizador.
// O instalador descarregado e verificado contra o SHA256SUMS.txt publicado na mesma versao.
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace FS25RealFFB
{
    internal sealed class Updater
    {
        public sealed class Release
        {
            public string Tag, Version, PageUrl, InstallerUrl, InstallerName, SumsUrl;
        }

        public delegate void Found(Release r);

        private readonly Action<string> log;

        public Updater(Action<string> log) { this.log = log; }

        private static WebClient Client()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2
            WebClient wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "FS25-RealFFB/" + AppInfo.Version;
            wc.Encoding = Encoding.UTF8;
            return wc;
        }

        // Compara "1.2.3" com "1.10.0" numericamente. Devolve >0 se a for mais recente.
        public static int Compare(string a, string b)
        {
            string[] x = a.TrimStart('v', 'V').Split('.'), y = b.TrimStart('v', 'V').Split('.');
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                int p = 0, q = 0;
                if (i < x.Length) int.TryParse(Regex.Match(x[i], "^\\d+").Value, out p);
                if (i < y.Length) int.TryParse(Regex.Match(y[i], "^\\d+").Value, out q);
                if (p != q) return p.CompareTo(q);
            }
            return 0;
        }

        public void CheckAsync(Found onNewer)
        {
            Thread t = new Thread(delegate()
            {
                try
                {
                    string json;
                    using (WebClient wc = Client()) json = wc.DownloadString("https://api.github.com/repos/" + AppInfo.Repo + "/releases?per_page=1");   // inclui versoes beta (pre-lancamento)
                    Release r = Parse(json);
                    if (r == null) return;
                    if (Compare(r.Version, AppInfo.Version) > 0) onNewer(r);
                    else log("Atualizacoes: esta e a versao mais recente (" + AppInfo.Version + ").");
                }
                catch (WebException wex)
                {
                    HttpWebResponse hr = wex.Response as HttpWebResponse;
                    if (hr != null && hr.StatusCode == HttpStatusCode.NotFound) log("Atualizacoes: nenhuma versao publica encontrada no GitHub (repositorio privado ou sem releases).");
                    else log("Atualizacoes: nao foi possivel verificar (" + wex.Message + ").");
                }
                catch (Exception ex) { log("Atualizacoes: nao foi possivel verificar (" + ex.Message + ")."); }
            });
            t.IsBackground = true;
            t.Start();
        }

        private static Release Parse(string json)
        {
            Match tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            if (!tag.Success) return null;
            Release r = new Release();
            r.Tag = tag.Groups[1].Value;
            r.Version = r.Tag.TrimStart('v', 'V');
            Match page = Regex.Match(json, "\"html_url\"\\s*:\\s*\"(https://github.com/[^\"]+/releases/tag/[^\"]+)\"");
            r.PageUrl = page.Success ? page.Groups[1].Value : "https://github.com/" + AppInfo.Repo + "/releases";
            foreach (Match m in Regex.Matches(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\""))
            {
                string url = m.Groups[1].Value;
                string name = url.Substring(url.LastIndexOf('/') + 1);
                if (Regex.IsMatch(name, "^FS25_RealFFB_Setup_.*\\.exe$", RegexOptions.IgnoreCase)) { r.InstallerUrl = url; r.InstallerName = name; }
                else if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) r.SumsUrl = url;
            }
            return r;
        }

        // Descarrega, verifica o SHA-256 publicado e abre o instalador. Devolve true se arrancou.
        public bool DownloadAndRun(Release r)
        {
            if (r.InstallerUrl == null || r.SumsUrl == null) { log("Atualizacao: a versao " + r.Tag + " nao tem instalador ou SHA256SUMS.txt."); return false; }
            string dir = Path.Combine(Path.GetTempPath(), "FS25RealFFB_update");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, r.InstallerName);
            using (WebClient wc = Client())
            {
                log("Atualizacao: a descarregar " + r.InstallerName + "...");
                wc.DownloadFile(r.InstallerUrl, file);
                string sums = wc.DownloadString(r.SumsUrl);
                Match m = Regex.Match(sums, "([0-9a-fA-F]{64})\\s+\\*?" + Regex.Escape(r.InstallerName));
                if (!m.Success) { log("Atualizacao: o instalador nao aparece no SHA256SUMS.txt - cancelado."); return false; }
                string actual = Sha256(file);
                if (!actual.Equals(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                {
                    log("Atualizacao: SHA-256 NAO confere (esperado " + m.Groups[1].Value + ", obtido " + actual + ") - cancelado.");
                    try { File.Delete(file); } catch { }
                    return false;
                }
                log("Atualizacao: SHA-256 confere (" + actual + "). A abrir o instalador.");
            }
            Process.Start(file);
            return true;
        }

        public static string Sha256(string file)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(file))
            {
                byte[] h = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
