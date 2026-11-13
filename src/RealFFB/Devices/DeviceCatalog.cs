// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - catalogo de dispositivos lido de dados (devices.ini embutido + %APPDATA%\FS25 RealFFB\devices.ini).
// Ordem de procura: produto [VVVV:PPPP] -> nome do produto [name:...] -> fabricante [vendor:VVVV] -> desconhecido.
// Os valores do produto completam-se com os do fabricante (ex.: as particularidades da Logitech).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.IO;
using System.Reflection;

namespace FS25RealFFB
{
    internal sealed class DeviceQuirks
    {
        public bool StopAllOnOpen, AutocenterOffEachAcquire, SpringFloor1;
        public int SpringBoost;

        public static DeviceQuirks Parse(string s)
        {
            DeviceQuirks q = new DeviceQuirks();
            if (string.IsNullOrEmpty(s)) return q;
            foreach (string raw in s.Split(','))
            {
                string t = raw.Trim();
                if (t.Length == 0) continue;
                string k = t, v = "";
                int eq = t.IndexOf('=');
                if (eq > 0) { k = t.Substring(0, eq).Trim(); v = t.Substring(eq + 1).Trim(); }
                switch (k)
                {
                    case "StopAllOnOpen": q.StopAllOnOpen = true; break;
                    case "AutocenterOffEachAcquire": q.AutocenterOffEachAcquire = true; break;
                    case "SpringFloor1": q.SpringFloor1 = true; break;
                    case "SpringBoost": { int n; if (int.TryParse(v, out n)) q.SpringBoost = Math.Max(0, Math.Min(10000, n)); } break;
                }
            }
            return q;
        }

        public override string ToString()
        {
            List<string> p = new List<string>();
            if (StopAllOnOpen) p.Add("StopAllOnOpen");
            if (AutocenterOffEachAcquire) p.Add("AutocenterOffEachAcquire");
            if (SpringFloor1) p.Add("SpringFloor1");
            if (SpringBoost > 0) p.Add("SpringBoost=" + SpringBoost);
            return p.Count == 0 ? "none" : string.Join(",", p.ToArray());
        }
    }

    internal sealed class CatalogEntry
    {
        public string Key = "";
        public string Model = "";
        public double PeakNm;
        public WheelDrive Drive = WheelDrive.DirectDrive;
        public ValidationLevel Validation = ValidationLevel.Unknown;
        public string QuirksText = "";
        public string Notes = "";
        public string Source = "";
        public bool Known;           // entrada de produto (nao so fabricante/nome)
        // 0.15: como o modelo foi identificado: "usb" (ID do produto), "name" (modelo exato no nome), "" (nao identificado)
        public string IdentifiedBy = "";
        public bool Sustained;       // o fabricante publica binario continuo, nao de pico
        // Teto do binario configuravel: o pico do fabricante para um modelo identificado; 0 = sem teto verificado.
        public double CeilingNm { get { return Known && PeakNm > 0 ? PeakNm : 0; } }
        public DeviceQuirks Quirks { get { return DeviceQuirks.Parse(QuirksText); } }
    }

    internal static class DeviceCatalog
    {
        private static Dictionary<string, Dictionary<string, string>> sections;
        public static string LoadNote = "";

        public static void Reload() { sections = null; }

        private static void Ensure()
        {
            if (sections != null) return;
            sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            string text = null;
            try
            {
                Assembly a = Assembly.GetExecutingAssembly();
                using (Stream st = a.GetManifestResourceStream("RealFFB.devices.ini"))
                    if (st != null) using (StreamReader r = new StreamReader(st)) text = r.ReadToEnd();
            }
            catch { }
            if (text == null) { text = Fallback; LoadNote = "built-in fallback catalog"; }
            else LoadNote = "embedded catalog";
            Parse(text, sections);
            try
            {
                string user = AppInfo.DataFile("devices.ini");
                if (File.Exists(user)) { Parse(File.ReadAllText(user), sections); LoadNote += " + user devices.ini"; }
            }
            catch { }
        }

        // so para os testes
        public static void LoadFromText(string text)
        {
            sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Parse(text, sections);
            LoadNote = "test catalog";
        }

        public static void Parse(string text, Dictionary<string, Dictionary<string, string>> into)
        {
            Dictionary<string, string> cur = null;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    string name = line.Substring(1, line.Length - 2).Trim();
                    if (!into.TryGetValue(name, out cur)) { cur = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); into[name] = cur; }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0 || cur == null) continue;
                cur[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
        }

        private static void Fill(CatalogEntry e, Dictionary<string, string> d, bool overwrite)
        {
            string v;
            if (d.TryGetValue("Model", out v) && (overwrite || e.Model.Length == 0)) e.Model = v;
            if (d.TryGetValue("PeakNm", out v) && (overwrite || e.PeakNm <= 0))
            {
                double n;
                if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out n)) e.PeakNm = n;
            }
            if (d.TryGetValue("Drive", out v) && overwrite)
            {
                if (v.Equals("Gear", StringComparison.OrdinalIgnoreCase)) e.Drive = WheelDrive.Gear;
                else if (v.Equals("Belt", StringComparison.OrdinalIgnoreCase)) e.Drive = WheelDrive.Belt;
                else e.Drive = WheelDrive.DirectDrive;
            }
            if (d.TryGetValue("Validation", out v) && (overwrite || e.Validation == ValidationLevel.Unknown))
            {
                ValidationLevel lv;
                try { lv = (ValidationLevel)Enum.Parse(typeof(ValidationLevel), v, true); } catch { lv = ValidationLevel.Unknown; }
                e.Validation = lv;
            }
            if (d.TryGetValue("Quirks", out v) && (overwrite || e.QuirksText.Length == 0)) e.QuirksText = v;
            if (d.TryGetValue("Notes", out v) && (overwrite || e.Notes.Length == 0)) e.Notes = v;
            if (d.TryGetValue("Source", out v) && (overwrite || e.Source.Length == 0)) e.Source = v;
            if (d.TryGetValue("Rating", out v) && (overwrite || !e.Sustained)) e.Sustained = v.Equals("Sustained", StringComparison.OrdinalIgnoreCase);
        }

        // 0.15: modelo pelo nome do dispositivo (so o token exato do modelo; nunca so a marca).
        private static bool ModelMatch(string pattern, string productName)
        {
            try { return Regex.IsMatch(productName, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)); }
            catch { return false; }
        }

        public static CatalogEntry Find(int vid, int pid) { return Find(vid, pid, null); }

        public static CatalogEntry Find(int vid, int pid, string productName)
        {
            Ensure();
            CatalogEntry e = new CatalogEntry();
            Dictionary<string, string> d;
            string key = vid.ToString("X4") + ":" + pid.ToString("X4");
            e.Key = key;
            bool drivSet = false;
            if (sections.TryGetValue(key, out d)) { Fill(e, d, true); e.Known = true; e.IdentifiedBy = "usb"; drivSet = d.ContainsKey("Drive"); }
            if (!e.Known && !string.IsNullOrEmpty(productName))
                foreach (KeyValuePair<string, Dictionary<string, string>> kv in sections)
                    if (kv.Key.StartsWith("model:", StringComparison.OrdinalIgnoreCase) && kv.Value.ContainsKey("PeakNm")
                        && ModelMatch(kv.Key.Substring(6), productName))
                    {
                        Fill(e, kv.Value, true); e.Known = true; e.IdentifiedBy = "name"; drivSet = kv.Value.ContainsKey("Drive");
                        break;
                    }
            if (!string.IsNullOrEmpty(productName))
                foreach (KeyValuePair<string, Dictionary<string, string>> kv in sections)
                    if (kv.Key.StartsWith("name:", StringComparison.OrdinalIgnoreCase)
                        && productName.IndexOf(kv.Key.Substring(5), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!drivSet && kv.Value.ContainsKey("Drive")) { Fill(e, OnlyDrive(kv.Value), true); drivSet = true; }
                        Fill(e, kv.Value, false);
                    }
            if (sections.TryGetValue("vendor:" + vid.ToString("X4"), out d))
            {
                if (!drivSet && d.ContainsKey("Drive")) { Fill(e, OnlyDrive(d), true); drivSet = true; }
                Fill(e, d, false);
            }
            return e;
        }

        private static Dictionary<string, string> OnlyDrive(Dictionary<string, string> d)
        {
            Dictionary<string, string> x = new Dictionary<string, string>();
            x["Drive"] = d["Drive"];
            return x;
        }

        public static int Count { get { Ensure(); return sections.Count; } }

        // 0.15: modelos com pico conhecido (para escolher o modelo quando nao e identificado automaticamente)
        public static List<CatalogEntry> ModelsWithTorque()
        {
            Ensure();
            List<CatalogEntry> r = new List<CatalogEntry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Dictionary<string, string>> kv in sections)
            {
                if (kv.Key.StartsWith("vendor:", StringComparison.OrdinalIgnoreCase) || kv.Key.StartsWith("name:", StringComparison.OrdinalIgnoreCase)) continue;
                if (!kv.Value.ContainsKey("PeakNm") || !kv.Value.ContainsKey("Model")) continue;
                CatalogEntry e = new CatalogEntry(); Fill(e, kv.Value, true); e.Known = true; e.Key = kv.Key;
                if (e.PeakNm <= 0 || !seen.Add(e.Model + "|" + e.PeakNm.ToString(CultureInfo.InvariantCulture))) continue;
                r.Add(e);
            }
            r.Sort(delegate(CatalogEntry x, CatalogEntry y) { return string.Compare(x.Model, y.Model, StringComparison.OrdinalIgnoreCase); });
            return r;
        }

        public static IEnumerable<KeyValuePair<string, Dictionary<string, string>>> All()
        {
            Ensure();
            return sections;
        }

        // usado so se o recurso embutido faltar (compilacao sem /resource)
        private const string Fallback = "[0EB7:0020]\nModel=Fanatec CSL DD / Gran Turismo DD Pro\nPeakNm=8\nDrive=DirectDrive\nValidation=Confirmed\n"
            + "[vendor:046D]\nModel=Logitech\nDrive=Gear\nValidation=Documented\nQuirks=StopAllOnOpen,AutocenterOffEachAcquire,SpringFloor1\n"
            + "[046D:C24F]\nModel=Logitech G29\nPeakNm=2.2\nDrive=Gear\nValidation=Documented\nQuirks=StopAllOnOpen,AutocenterOffEachAcquire,SpringFloor1,SpringBoost=1800\n";

        public static string LevelText(ValidationLevel v)
        {
            switch (v)
            {
                case ValidationLevel.Confirmed: return L.T("Confirmed on real hardware");
                case ValidationLevel.Documented: return L.T("Documented API, not yet tested on this hardware");
                case ValidationLevel.Theoretical: return L.T("Theoretically possible, never tested");
                case ValidationLevel.Experimental: return L.T("Experimental");
                case ValidationLevel.NotSupported: return L.T("Not supported");
                default: return L.T("Unknown device: works through its detected capabilities, not tested");
            }
        }
    }
}
