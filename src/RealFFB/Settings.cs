// FS25 Real FFB - configuracoes (guardadas em RealFFB.ini ao lado do exe).
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace FS25RealFFB
{
    internal sealed class Settings
    {
        public double BaseNm = 8.0;        // torque de pico do volante (definido pelo modelo detetado; ajustavel)
        public string BaseDevice = "";    // VID:PID do volante a que BaseNm diz respeito
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
        public bool PerVehicle = true;  // afinacoes de FFB guardadas por veiculo
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

        // ---- afinacoes de FFB (o que muda de veiculo para veiculo)
        public double[] GlobalTuning;      // se != null, e isto que vai para o RealFFB.ini (o atual e de um perfil)

        public double[] GetTuning()
        {
            return new double[] { MaxNm, Center, Damping, Friction, Bumps, Texture, Engine, RefSpeedKmh, InvertBumps ? 1 : 0 };
        }

        public void SetTuning(double[] t)
        {
            if (t == null || t.Length < 9) return;
            MaxNm = Math.Max(0.5, Math.Min(BaseNm, t[0]));
            Center = Clamp01(t[1]); Damping = Clamp01(t[2]); Friction = Clamp01(t[3]);
            Bumps = Clamp01(t[4]); Texture = Clamp01(t[5]); Engine = Clamp01(t[6]);
            RefSpeedKmh = Math.Max(5, Math.Min(80, t[7]));
            InvertBumps = t[8] > 0.5;
        }

        public static readonly string[] TuningKeys = { "MaxNm", "Center", "Damping", "Friction", "Bumps", "Texture", "Engine", "RefSpeedKmh", "InvertBumps" };

        public static string TuningToText(double[] t)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < TuningKeys.Length; i++) sb.AppendLine(TuningKeys[i] + "=" + t[i].ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        // Le um ficheiro de perfil; chaves em falta ficam com o valor de 'baseline'.
        public static double[] TuningFromLines(string[] lines, double[] baseline)
        {
            double[] t = (double[])baseline.Clone();
            foreach (string raw in lines)
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                int idx = Array.IndexOf(TuningKeys, raw.Substring(0, eq).Trim());
                double d;
                if (idx >= 0 && double.TryParse(raw.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) t[idx] = d;
            }
            return t;
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
            sb.AppendLine("BaseNm=" + BaseNm.ToString(c));
            sb.AppendLine("BaseDevice=" + BaseDevice);
            double[] tu = GlobalTuning ?? GetTuning();
            sb.AppendLine("MaxNm=" + tu[0].ToString(c));
            sb.AppendLine("Center=" + tu[1].ToString(c));
            sb.AppendLine("Damping=" + tu[2].ToString(c));
            sb.AppendLine("Friction=" + tu[3].ToString(c));
            sb.AppendLine("Bumps=" + tu[4].ToString(c));
            sb.AppendLine("Texture=" + tu[5].ToString(c));
            sb.AppendLine("Engine=" + tu[6].ToString(c));
            sb.AppendLine("RefSpeedKmh=" + tu[7].ToString(c));
            sb.AppendLine("InvertBumps=" + (tu[8] > 0.5 ? "1" : "0"));
            sb.AppendLine("AltInterface=" + (AltInterface ? "1" : "0"));
            sb.AppendLine("BridgeOn=" + (BridgeOn ? "1" : "0"));
            sb.AppendLine("KeysOn=" + (KeysOn ? "1" : "0"));
            sb.AppendLine("AutoHide=" + (AutoHide ? "1" : "0"));
            sb.AppendLine("AutoFfb=" + (AutoFfb ? "1" : "0"));
            sb.AppendLine("PerVehicle=" + (PerVehicle ? "1" : "0"));
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
                        case "BaseNm": if (isNum) s.BaseNm = Math.Max(1, Math.Min(25, d)); break;
                        case "BaseDevice": s.BaseDevice = v; break;
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
                        case "PerVehicle": s.PerVehicle = v == "1"; break;
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
