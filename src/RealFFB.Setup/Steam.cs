// Localizar o Farming Simulator 25: Steam (qualquer disco e biblioteca), GIANTS, Epic, ou pasta manual.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace RealFFBSetup
{
    internal static class GameLocator
    {
        public const string SteamAppId = "2300320";

        // Le os caminhos de bibliotecas do libraryfolders.vdf (formato KeyValues da Valve).
        public static List<string> ParseLibraryFolders(string vdf)
        {
            List<string> r = new List<string>();
            foreach (Match m in Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase))
                r.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
            return r;
        }

        public static string ParseInstallDir(string acf)
        {
            Match m = Regex.Match(acf, "\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        public static string SteamRoot()
        {
            string[,] keys = { { "HKCU", @"Software\Valve\Steam", "SteamPath" }, { "HKLM", @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath" }, { "HKLM", @"SOFTWARE\Valve\Steam", "InstallPath" } };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                RegistryKey root = keys[i, 0] == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                try
                {
                    using (RegistryKey k = root.OpenSubKey(keys[i, 1]))
                    {
                        string p = k == null ? null : k.GetValue(keys[i, 2]) as string;
                        if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return Path.GetFullPath(p.Replace('/', '\\'));
                    }
                }
                catch { }
            }
            return null;
        }

        public static bool IsValidGameDir(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            return File.Exists(Path.Combine(dir, "FarmingSimulator2025.exe")) || File.Exists(Path.Combine(dir, @"x64\FarmingSimulator2025Game.exe"));
        }

        public static string GameIcon(string dir)
        {
            string a = Path.Combine(dir, "FarmingSimulator2025.exe");
            if (File.Exists(a)) return a;
            string b = Path.Combine(dir, @"x64\FarmingSimulator2025Game.exe");
            return File.Exists(b) ? b : "";
        }

        // Devolve (pasta, origem) ou (null, motivo).
        public static string Find(out string source)
        {
            string steam = SteamRoot();
            if (steam != null)
            {
                List<string> libs = new List<string>();
                libs.Add(steam);
                string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                try { if (File.Exists(vdf)) libs.AddRange(ParseLibraryFolders(File.ReadAllText(vdf))); } catch { }
                foreach (string lib in libs)
                {
                    string acf = Path.Combine(lib, @"steamapps\appmanifest_" + SteamAppId + ".acf");
                    if (!File.Exists(acf)) continue;
                    string inst = null;
                    try { inst = ParseInstallDir(File.ReadAllText(acf)); } catch { }
                    string dir = Path.Combine(lib, @"steamapps\common\" + (inst ?? "Farming Simulator 25"));
                    if (IsValidGameDir(dir)) { source = "Steam (" + lib + ")"; return dir; }
                }
            }
            // Outras lojas / instalacao direta da GIANTS: pastas habituais
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfx = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? pf;
            string[] guesses = {
                Path.Combine(pf, "Farming Simulator 2025"), Path.Combine(pfx, "Farming Simulator 2025"),
                Path.Combine(pf, @"Epic Games\FarmingSimulator25"), Path.Combine(pf, @"Epic Games\FarmingSimulator2025") };
            foreach (string g in guesses) if (IsValidGameDir(g)) { source = "instalacao fora da Steam"; return g; }
            source = steam == null ? "Steam nao encontrada e jogo fora das pastas habituais" : "FS25 nao encontrado nas bibliotecas da Steam";
            return null;
        }

        // Pasta de perfil (game.xml, mods). Respeita Documentos redirecionados (OneDrive).
        public static string ProfileDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"My Games\FarmingSimulator2025");
        }
    }
}
