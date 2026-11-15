// Copyright (c) 2026 OptimusZuka. All rights reserved.
using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace FS25RealFFB
{
    // 0.15.2: onde esta o perfil do FS25 (game.xml, mods, modSettings, inputBinding.xml) e como abrir o jogo,
    // para as duas edicoes de PC:
    //  - Steam / Epic / GIANTS eShop: Documentos\My Games\FarmingSimulator2025
    //  - Microsoft Store / app Xbox / PC Game Pass: %LOCALAPPDATA%\Packages\GIANTSSoftware.FarmingSimulator25PC_<sufixo>\LocalCache\Local
    // Se existirem as duas, usa a que o jogo usou por ultimo (log.txt / game.xml mais recente).
    // Usado pela app e pelo RealFFB.Setup (o mesmo ficheiro e compilado nos dois).
    internal static class GameEdition
    {
        public const string StorePackagePrefix = "GIANTSSoftware.FarmingSimulator25PC_";

        public static string DocsProfile()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"My Games\FarmingSimulator2025");
        }

        // pasta do pacote da Store em %LOCALAPPDATA%\Packages (null se nao instalado/nunca aberto)
        public static string StorePackageDir()
        {
            try
            {
                string pk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
                if (!Directory.Exists(pk)) return null;
                foreach (string d in Directory.GetDirectories(pk, StorePackagePrefix + "*")) return d;
            }
            catch { }
            return null;
        }

        public static string StoreProfile()
        {
            string p = StorePackageDir();
            if (p == null) return null;
            return Path.Combine(p, @"LocalCache\Local");
        }

        public static bool StoreInstalled() { return StorePackageDir() != null || StoreAumid() != null; }

        private static DateTime LastUse(string profile)
        {
            DateTime t = DateTime.MinValue;
            try
            {
                foreach (string f in new string[] { "log.txt", "game.xml" })
                {
                    string p = Path.Combine(profile, f);
                    if (File.Exists(p)) { DateTime w = File.GetLastWriteTimeUtc(p); if (w > t) t = w; }
                }
            }
            catch { }
            return t;
        }

        // perfil em uso: o mais recente; sem nenhum, Documentos (como antes)
        public static string ProfileDir()
        {
            string docs = DocsProfile();
            string store = StoreProfile();
            if (store == null || !Directory.Exists(store)) return docs;
            if (!Directory.Exists(docs)) return store;
            return LastUse(store) > LastUse(docs) ? store : docs;
        }

        public static bool IsStoreProfile(string profile)
        {
            return profile != null && profile.IndexOf(StorePackagePrefix, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // pasta do jogo da Store (so para mostrar/lembrar; o conteudo pode nao ser legivel)
        public static string StoreGameDir()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
                {
                    if (k != null)
                        foreach (string n in k.GetSubKeyNames())
                            if (n.StartsWith("GIANTSSoftware.FarmingSimulator25PC_", StringComparison.OrdinalIgnoreCase))
                                using (RegistryKey p = k.OpenSubKey(n))
                                {
                                    string root = p == null ? null : p.GetValue("PackageRootFolder") as string;
                                    if (!string.IsNullOrEmpty(root)) return root;
                                }
                }
            }
            catch { }
            return StorePackageDir() != null ? "Microsoft Store / Xbox app" : null;
        }

        private static string aumid; private static bool aumidDone;

        // AppUserModelID do jogo da Store ("<familia>!<id>"), para o abrir por shell:AppsFolder.
        // 1) registo do AppModel (subchaves da aplicacao); 2) Get-StartApps (PowerShell), como recurso.
        public static string StoreAumid()
        {
            if (aumidDone) return aumid;
            aumidDone = true;
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
                {
                    if (k != null)
                        foreach (string n in k.GetSubKeyNames())
                        {
                            if (!n.StartsWith(StorePackagePrefix.TrimEnd('_'), StringComparison.OrdinalIgnoreCase)) continue;
                            using (RegistryKey p = k.OpenSubKey(n))
                            {
                                if (p == null) continue;
                                foreach (string app in p.GetSubKeyNames())
                                    using (RegistryKey a = p.OpenSubKey(app))
                                    {
                                        string id = a == null ? null : a.GetValue("AppUserModelID") as string;
                                        if (!string.IsNullOrEmpty(id) && id.Contains("!")) { aumid = id; return aumid; }
                                    }
                            }
                        }
                }
                // recurso: lista do menu Iniciar
                ProcessStartInfo si = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -NonInteractive -Command \"Get-StartApps | Where-Object { $_.AppID -like 'GIANTSSoftware.FarmingSimulator25PC*' } | Select-Object -First 1 -ExpandProperty AppID\"");
                si.UseShellExecute = false; si.CreateNoWindow = true; si.RedirectStandardOutput = true;
                using (Process pr = Process.Start(si))
                {
                    string o = pr.StandardOutput.ReadToEnd().Trim();
                    pr.WaitForExit(8000);
                    if (o.Contains("!")) aumid = o;
                }
            }
            catch { }
            return aumid;
        }

        // Abre o FS25 na edicao em uso. Devolve uma frase para o registo.
        public static string Launch()
        {
            bool store = IsStoreProfile(ProfileDir()) || (StorePackageDir() != null && !Directory.Exists(DocsProfile()));
            if (store)
            {
                string id = StoreAumid();
                if (id != null)
                {
                    Process.Start("explorer.exe", "shell:AppsFolder\\" + id);
                    return "Opening FS25 (Microsoft Store / Xbox app)...";
                }
                return "Could not find the FS25 Microsoft Store / Xbox app entry. Open the game from the Xbox app.";
            }
            Process.Start("steam://rungameid/2300320");
            return "Opening FS25 through Steam...";
        }
    }
}
