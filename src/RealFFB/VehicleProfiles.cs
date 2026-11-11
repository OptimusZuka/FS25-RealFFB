// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB - um perfil de FFB por veiculo (%APPDATA%\FS25 RealFFB\perfis\<veiculo>.ini).
// Veiculo sem perfil usa as afinacoes gerais; mexer nos controlos dentro de um veiculo cria o perfil dele.
using System;
using System.IO;
using System.Text;

namespace FS25RealFFB
{
    internal static class VehicleProfiles
    {
        public static string Dir
        {
            get { string d = AppInfo.DataFile("perfis"); try { Directory.CreateDirectory(d); } catch { } return d; }
        }

        public static string FileFor(string vehicle)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in vehicle.Trim())
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), ch) >= 0 ? '_' : ch);
            string n = sb.ToString();
            if (n.Length == 0) n = "_";
            if (n.Length > 80) n = n.Substring(0, 80);
            return Path.Combine(Dir, n + ".ini");
        }

        public static bool Exists(string vehicle) { return File.Exists(FileFor(vehicle)); }

        public static double[] Load(string vehicle, double[] baseline)
        {
            try { return Settings.TuningFromLines(File.ReadAllLines(FileFor(vehicle)), baseline); }
            catch { return null; }
        }

        public static void Save(string vehicle, double[] tuning)
        {
            File.WriteAllText(FileFor(vehicle), "; FS25 RealFFB - perfil de " + vehicle + "\r\nVehicle=" + vehicle + "\r\n" + Settings.TuningToText(tuning));
        }

        public static void Delete(string vehicle) { try { File.Delete(FileFor(vehicle)); } catch { } }

        // Repor tudo: os perfis nao sao apagados, vao para uma pasta de copia (reversivel).
        public static int MoveAllToBackup(out string backupDir)
        {
            backupDir = Path.Combine(Dir, "backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            int n = 0;
            try
            {
                string[] files = Directory.GetFiles(Dir, "*.ini");
                if (files.Length == 0) return 0;
                Directory.CreateDirectory(backupDir);
                foreach (string f in files)
                {
                    try { File.Move(f, Path.Combine(backupDir, Path.GetFileName(f))); n++; } catch { }
                }
            }
            catch { }
            return n;
        }

        public static int Count()
        {
            try { return Directory.GetFiles(Dir, "*.ini").Length; } catch { return 0; }
        }
    }
}
