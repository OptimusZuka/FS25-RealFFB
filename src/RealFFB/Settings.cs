// FS25 Real FFB - configuracoes (guardadas em RealFFB.ini ao lado do exe).
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace FS25RealFFB
{
    internal sealed class Settings
    {
        public double BaseNm = 8.0;        // torque maximo da base (CSL DD + Boost Kit)
        public double MaxNm = 3.0;         // limite de seguranca da app
        public const int Version = 4;      // ao subir, as afinacoes antigas sao repostas
        public double Center = 0.45;
        public double Damping = 0.08;
        public double Friction = 0.10;
        public double Bumps = 0.60;
        public double Texture = 0.60;
        public double Engine = 0.50;
        public double RefSpeedKmh = 30;
        public bool InvertBumps = false;
        public bool AltInterface = false;

        // Ponte de comandos (direcao e pedais para o jogo)
        public bool BridgeOn = true;
        public bool KeysOn = false;
        public bool AutoHide = true;
        public bool AutoFfb = true;    // FFB ativo logo ao abrir a app   // liga o HidHide ao abrir a app e desliga ao fechar   // comandos -> teclas (so sem HidHide)
        public double SteerSens = 1.0;     // 1.0 = rotacao total da base = esbarro total do trator
        public bool SteerInvert = false;
        public int[] PedalAxis = { -1, -1, -1 };       // acelerador, travao, embraiagem (-1 = nenhum)
        public int[] PedalRel = { 0, 0, 0 };           // valor com o pedal solto
        public int[] PedalFull = { 10000, 10000, 10000 }; // valor com o pedal a fundo

        public int ToMag(double nm)
        {
            double cap = Math.Min(MaxNm, BaseNm);
            if (nm < 0) nm = 0;
            if (nm > cap) nm = cap;
            return (int)Math.Round(nm / BaseNm * 10000.0);
        }

        public int ToSignedMag(double nm)
        {
            int m = ToMag(Math.Abs(nm));
            return nm < 0 ? -m : m;
        }

        public static string FilePath
        {
            get { return AppInfo.DataFile("RealFFB.ini"); }
        }

        public void Save()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SettingsVersion=" + Version);
            sb.AppendLine("MaxNm=" + MaxNm.ToString(c));
            sb.AppendLine("Center=" + Center.ToString(c));
            sb.AppendLine("Damping=" + Damping.ToString(c));
            sb.AppendLine("Friction=" + Friction.ToString(c));
            sb.AppendLine("Bumps=" + Bumps.ToString(c));
            sb.AppendLine("Texture=" + Texture.ToString(c));
            sb.AppendLine("Engine=" + Engine.ToString(c));
            sb.AppendLine("RefSpeedKmh=" + RefSpeedKmh.ToString(c));
            sb.AppendLine("InvertBumps=" + (InvertBumps ? "1" : "0"));
            sb.AppendLine("AltInterface=" + (AltInterface ? "1" : "0"));
            sb.AppendLine("BridgeOn=" + (BridgeOn ? "1" : "0"));
            sb.AppendLine("KeysOn=" + (KeysOn ? "1" : "0"));
            sb.AppendLine("AutoHide=" + (AutoHide ? "1" : "0"));
            sb.AppendLine("AutoFfb=" + (AutoFfb ? "1" : "0"));
            sb.AppendLine("SteerSens=" + SteerSens.ToString(c));
            sb.AppendLine("SteerInvert=" + (SteerInvert ? "1" : "0"));
            for (int i = 0; i < 3; i++)
                sb.AppendLine("Pedal" + i + "=" + PedalAxis[i] + ";" + PedalRel[i] + ";" + PedalFull[i]);
            File.WriteAllText(FilePath, sb.ToString());
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            bool sawVersion = false;
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    int eq = raw.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = raw.Substring(0, eq).Trim();
                    string v = raw.Substring(eq + 1).Trim();
                    double d;
                    bool isNum = double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
                    switch (k)
                    {
                        case "SettingsVersion": sawVersion = isNum && (int)d == Version; break;
                        case "MaxNm": if (isNum) s.MaxNm = Math.Max(0.5, Math.Min(s.BaseNm, d)); break;
                        case "Center": if (isNum) s.Center = Clamp01(d); break;
                        case "Damping": if (isNum) s.Damping = Clamp01(d); break;
                        case "Friction": if (isNum) s.Friction = Clamp01(d); break;
                        case "Bumps": if (isNum) s.Bumps = Clamp01(d); break;
                        case "Texture": if (isNum) s.Texture = Clamp01(d); break;
                        case "Engine": if (isNum) s.Engine = Clamp01(d); break;
                        case "RefSpeedKmh": if (isNum) s.RefSpeedKmh = Math.Max(5, Math.Min(80, d)); break;
                        case "InvertBumps": s.InvertBumps = v == "1"; break;
                        case "AltInterface": s.AltInterface = v == "1"; break;
                        case "BridgeOn": s.BridgeOn = v == "1"; break;
                        case "KeysOn": s.KeysOn = v == "1"; break;
                        case "AutoHide": s.AutoHide = v == "1"; break;
                        case "AutoFfb": s.AutoFfb = v == "1"; break;
                        case "SteerSens": if (isNum) s.SteerSens = Math.Max(0.5, Math.Min(4, d)); break;
                        case "SteerInvert": s.SteerInvert = v == "1"; break;
                        case "Pedal0": case "Pedal1": case "Pedal2":
                            {
                                int idx = k[5] - '0';
                                string[] q = v.Split(';');
                                int a, r, f;
                                if (q.Length == 3 && int.TryParse(q[0], out a) && int.TryParse(q[1], out r) && int.TryParse(q[2], out f))
                                { s.PedalAxis[idx] = a; s.PedalRel[idx] = r; s.PedalFull[idx] = f; }
                                break;
                            }
                    }
                }
            }
            catch { }
            if (!sawVersion)
            {   // afinacoes de uma versao antiga: repor os novos valores, mantendo pedais e direcao
                Settings def = new Settings();
                s.Center = def.Center; s.Damping = def.Damping; s.Friction = def.Friction; s.Bumps = def.Bumps; s.Texture = def.Texture;
            }
            return s;
        }

        private static double Clamp01(double v) { return v < 0 ? 0 : (v > 1 ? 1 : v); }
    }
}
