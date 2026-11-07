// Configuracao do HidHide: so a base Fanatec escolhida e escondida, so a RealFFB.exe e autorizada.
// Tudo o que e acrescentado fica registado no estado para a desinstalacao repor exatamente o anterior.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RealFFBSetup
{
    internal static class Config
    {
        // Caminhos HidHide (filhos HID + contentor base) da base escolhida, a partir do --dev-gaming.
        public static List<string> PathsFor(FanatecBase b, List<HidHide.GamingDevice> gaming)
        {
            List<string> r = new List<string>();
            foreach (HidHide.GamingDevice g in gaming)
            {
                int pid;
                string key = Fanatec.GroupKey(g.InstancePath, out pid);
                if (key == null || !key.Equals(b.Key, StringComparison.OrdinalIgnoreCase)) continue;
                if (!Contains(r, g.InstancePath)) r.Add(g.InstancePath);
                if (!string.IsNullOrEmpty(g.ContainerPath) && !Contains(r, g.ContainerPath)) r.Add(g.ContainerPath);
            }
            return r;
        }

        public static bool Contains(List<string> l, string s)
        {
            foreach (string x in l) if (string.Equals(x, s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Guarda uma copia da configuracao atual do HidHide (so na primeira vez).
        public static void BackupOnce(State st, Action<string> log)
        {
            if (st.Get("HH_BackupDone") == "1") return;
            List<string> apps = HidHide.Apps();
            List<string> devs = HidHide.HiddenDevices();
            bool cloak = HidHide.CloakOn();
            string dir = Path.Combine(State.Dir, "backup");
            Directory.CreateDirectory(dir);
            string f = Path.Combine(dir, "hidhide-original-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(cloak ? "--cloak-on" : "--cloak-off");
            foreach (string a in apps) sb.AppendLine("--app-reg \"" + a + "\"");
            foreach (string d in devs) sb.AppendLine("--dev-hide \"" + d + "\"");
            File.WriteAllText(f, sb.ToString());
            st.Set("HH_BackupDone", "1");
            st.Set("HH_OrigCloak", cloak ? "on" : "off");
            st.Set("HH_BackupFile", f);
            st.Save();
            log("Copia da configuracao do HidHide: " + f);
        }

        public static void Apply(State st, FanatecBase b, List<string> paths, string appExe, Action<string> log)
        {
            BackupOnce(st, log);
            List<string> apps = HidHide.Apps();
            List<string> devs = HidHide.HiddenDevices();
            List<string> added = st.GetList("HH_DevAdded");

            if (!Contains(apps, appExe))
            {
                HidHide.Run("--app-reg \"" + appExe + "\"");
                st.Set("HH_AppAdded", appExe);
                log("Autorizada: " + appExe);
            }
            foreach (string p in paths)
            {
                if (Contains(devs, p)) continue;
                HidHide.Run("--dev-hide \"" + p + "\"");
                if (!Contains(added, p)) added.Add(p);
                log("Escondido: " + p);
            }
            st.SetList("HH_DevAdded", added);
            st.Set("HH_Base", b.Key);
            st.Set("HH_BaseLabel", b.Label);
            st.Save();
        }

        // Repoe o que o instalador acrescentou. Nao toca no que ja existia antes.
        public static void Restore(State st, Action<string> log)
        {
            if (HidHide.Cli() == null) { log("HidHide ausente: nada a repor."); return; }
            string app = st.Get("HH_AppAdded");
            if (app != null) { Try("--app-unreg \"" + app + "\"", log); st.Set("HH_AppAdded", null); }
            foreach (string d in st.GetList("HH_DevAdded")) Try("--dev-unhide \"" + d + "\"", log);
            st.Set("HH_DevAdded", null);
            string cloak = st.Get("HH_OrigCloak");
            if (cloak != null) Try(cloak == "on" ? "--cloak-on" : "--cloak-off", log);
            st.Set("HH_Base", null); st.Set("HH_BaseLabel", null);
            st.Set("HH_BackupDone", null); st.Set("HH_OrigCloak", null);
            st.Save();
        }

        private static void Try(string args, Action<string> log)
        {
            try { HidHide.Run(args); log("HidHide " + args); }
            catch (Exception ex) { log("Falhou HidHide " + args + ": " + ex.Message); }
        }
    }
}
