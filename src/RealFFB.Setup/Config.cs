// Configuracao do HidHide pelo HidHideCLI oficial (sem administrador, segundo a documentacao da Nefarius).
// Regras (0.14):
//   - so se ACRESCENTA as entradas da FS25 RealFFB; as de outros programas nunca sao tocadas;
//   - acrescentar o que ja existe nao faz nada (idempotente), por isso reinstalar/reparar e seguro;
//   - cada entrada acrescentada fica registada no estado, para a desinstalacao retirar so essas;
//   - o esconderijo (cloak) nao e ligado aqui: a app liga-o quando abre e desliga-o quando fecha
//     (o volante continua a funcionar noutros jogos com a app fechada).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RealFFBSetup
{
    internal static class Config
    {
        public static bool Contains(List<string> l, string s)
        {
            foreach (string x in l) if (string.Equals(x, s, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Guarda uma copia da configuracao atual do HidHide (so na primeira vez), em formato que a CLI aceita.
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
            log("HidHide configuration backup: " + f);
        }

        // Autoriza a RealFFB.exe (o caminho real da instalacao, inclusive pastas personalizadas).
        // Se uma instalacao anterior NOSSA estava noutra pasta, essa entrada antiga e retirada.
        public static void EnsureApp(State st, string appExe, Action<string> log)
        {
            List<string> apps = HidHide.Apps();
            string prev = st.Get("HH_AppAdded");
            if (prev != null && !prev.Equals(appExe, StringComparison.OrdinalIgnoreCase) && Contains(apps, prev))
            {
                HidHide.Run("--app-unreg \"" + prev + "\"");
                log("Removed old entry (previous install folder): " + prev);
                st.Set("HH_AppAdded", null);
            }
            if (!Contains(apps, appExe))
            {
                HidHide.Run("--app-reg \"" + appExe + "\"");
                st.Set("HH_AppAdded", appExe);
                log("Allowed in HidHide: " + appExe);
            }
            else log("Already allowed in HidHide: " + appExe);
            st.Save();
        }

        public static void HideDevices(State st, WheelDevice w, List<string> paths, Action<string> log)
        {
            List<string> devs = HidHide.HiddenDevices();
            List<string> added = st.GetList("HH_DevAdded");
            foreach (string p in paths)
            {
                if (Contains(devs, p)) { log("Already hidden: " + p); continue; }
                HidHide.Run("--dev-hide \"" + p + "\"");
                if (!Contains(added, p)) added.Add(p);
                log("Hidden from games: " + p);
            }
            st.SetList("HH_DevAdded", added);
            st.Set("HH_Base", w.Key);
            st.Set("HH_BaseLabel", w.Label);
            st.Save();
        }

        // Le de novo as listas do HidHide e confirma que tudo ficou aplicado. Devolve o que falta (vazio = ok).
        public static List<string> Missing(string appExe, List<string> paths)
        {
            List<string> miss = new List<string>();
            if (appExe != null && !Contains(HidHide.Apps(), appExe)) miss.Add("application: " + appExe);
            if (paths != null && paths.Count > 0)
            {
                List<string> devs = HidHide.HiddenDevices();
                foreach (string p in paths) if (!Contains(devs, p)) miss.Add("device: " + p);
            }
            return miss;
        }

        // Repoe o que o instalador acrescentou. Nao toca no que ja existia antes nem no de outros programas.
        public static void Restore(State st, Action<string> log)
        {
            if (HidHide.Cli() == null) { log("HidHide not present: nothing to restore."); return; }
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
            catch (Exception ex) { log("HidHide " + args + " failed: " + ex.Message); }
        }
    }
}
