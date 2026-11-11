// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - versao e pastas da aplicacao.
using System;
using System.IO;
using System.Windows.Forms;

namespace FS25RealFFB
{
    internal static class AppInfo
    {
        // Versao unica: ficheiro VERSION na raiz -> build.bat gera Version.g.cs (AssemblyVersion).
        public static readonly string Version = ReadVersion();

        private static string ReadVersion()
        {
            Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v.Major + "." + v.Minor + "." + v.Build;
        }
        public const string Repo = "OptimusZuka/FS25-RealFFB";

        private static string dataDir;

        // Definicoes, perfis e registos ficam em %APPDATA%\FS25 RealFFB:
        // sobrevivem a atualizacoes e reinstalacoes.
        public static string DataDir
        {
            get
            {
                if (dataDir != null) return dataDir;
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FS25 RealFFB");
                try { Directory.CreateDirectory(d); } catch { }
                MigrateOld(d);
                dataDir = d;
                return d;
            }
        }

        public static string DataFile(string name) { return Path.Combine(DataDir, name); }

        // Versoes antigas guardavam tudo ao lado do .exe: copia-se uma vez para a nova pasta.
        private static void MigrateOld(string d)
        {
            string[] names = { "RealFFB.ini", "RealFFB_comandos.txt" };
            // pasta do exe e a pasta da versao de desenvolvimento (Documentos\GitHub\FS25-RealFFB\app)
            string[] dirs = { Application.StartupPath,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"GitHub\FS25-RealFFB\app") };
            foreach (string n in names)
                foreach (string dir in dirs)
                {
                    try
                    {
                        string oldPath = Path.Combine(dir, n);
                        string newPath = Path.Combine(d, n);
                        if (File.Exists(oldPath) && !File.Exists(newPath)) File.Copy(oldPath, newPath);
                    }
                    catch { }
                }
        }

        public static string SetupHelper { get { return Path.Combine(Application.StartupPath, "RealFFB.Setup.exe"); } }
    }
}
