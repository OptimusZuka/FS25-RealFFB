// Estado da instalacao (%APPDATA%\FS25 RealFFB\install-state.ini): o que o instalador
// alterou, para a desinstalacao repor exatamente isso e nada mais.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RealFFBSetup
{
    internal sealed class State
    {
        private readonly Dictionary<string, string> v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string Dir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FS25 RealFFB");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string FilePath { get { return Path.Combine(Dir, "install-state.ini"); } }

        public static State Load()
        {
            State s = new State();
            try
            {
                if (File.Exists(FilePath))
                    foreach (string line in File.ReadAllLines(FilePath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq > 0) s.v[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                    }
            }
            catch { }
            return s;
        }

        public string Get(string k) { string r; return v.TryGetValue(k, out r) && r.Length > 0 ? r : null; }
        public void Set(string k, string value) { if (value == null) v.Remove(k); else v[k] = value; }

        public List<string> GetList(string k)
        {
            List<string> l = new List<string>();
            string s = Get(k);
            if (s != null) foreach (string p in s.Split('|')) if (p.Length > 0) l.Add(p);
            return l;
        }

        // Idioma escolhido no instalador (en, pt, es, fr); aceita tambem nomes do Inno (english, brazilianportuguese...)
        public void SetLang(string code)
        {
            string c = (code ?? "").ToLowerInvariant();
            if (c.StartsWith("pt") || c.Contains("portug")) c = "pt";
            else if (c.StartsWith("es") || c.Contains("spanish")) c = "es";
            else if (c.StartsWith("fr") || c.Contains("french")) c = "fr";
            else c = "en";
            Set("Lang", c); Save();
        }

        public void SetList(string k, List<string> l) { Set(k, l.Count == 0 ? null : string.Join("|", l.ToArray())); }

        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("; FS25 RealFFB - estado da instalacao (nao editar)");
            foreach (KeyValuePair<string, string> kv in v) sb.AppendLine(kv.Key + "=" + kv.Value);
            File.WriteAllText(FilePath, sb.ToString());
        }
    }

    // Resultado para o instalador: ficheiro chave=valor (--out).
    internal sealed class Result
    {
        private readonly StringBuilder sb = new StringBuilder();
        public void Add(string k, string value) { sb.AppendLine(k + "=" + (value ?? "").Replace("\r", " ").Replace("\n", " ")); }
        // Formato .ini (seccao [r]) em UTF-16 com BOM: o Inno Setup le com GetIniString, com acentos.
        public void Write(string path)
        {
            if (path == null) Console.Write(sb.ToString());
            else File.WriteAllText(path, "[r]\r\n" + sb.ToString(), Encoding.Unicode);
        }
    }
}
