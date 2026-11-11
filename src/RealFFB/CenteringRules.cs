// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB - centragem realista (0.12).
// Na vida real, o volante so volta ao centro sozinho quando ha ligacao mecanica entre o volante e as
// rodas (caixa de direcao, com ou sem assistencia): carros, pickups, camioes, moto-4.
// Direcao hidrostatica (orbitrol), a de quase todos os tratores modernos, ceifeiras, pulverizadores,
// carregadoras, telescopicas e empilhadores, nao tem essa ligacao: o volante fica onde o deixar
// e a posicao dele nem corresponde sempre ao mesmo angulo das rodas. Articulados e maquinas de
// rastos, idem. Com a opcao ligada, estes veiculos ficam sem mola de centragem; o peso viscoso
// do orbitrol mantem-se (o volante nao fica solto).
// Por veiculo: Automatico (esta regra), Centra sempre ou Nunca centra (%APPDATA%\FS25 RealFFB\centragem.ini).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FS25RealFFB
{
    internal enum SteerKind { Mechanical, Hydrostatic, Articulated, Tracks, Levers, Unknown }

    internal static class CenteringRules
    {
        public static SteerKind Classify(string typeName, string category, bool articulated, bool crawler)
        {
            string c = (category ?? "").Trim().ToUpperInvariant();
            string t = (typeName ?? "").Trim().ToLowerInvariant();

            // 1) Veiculos de estrada: direcao mecanica com assistencia -> centram.
            if (c == "CARS" || c.Contains("TRUCK") || c.Contains("PICKUP") || c.Contains("QUAD") || c.Contains("ATV")) return SteerKind.Mechanical;
            if (t == "car" || t.StartsWith("carfillable") || t.Contains("truck") || t.Contains("pickup") || t.Contains("quad") || t.Contains("atv")) return SteerKind.Mechanical;

            // 2) Comandos por alavancas / joystick: sem volante de verdade.
            if (c.Contains("SKIDSTEER") || t.Contains("skidsteer") || c.Contains("EXCAVATOR") || t.Contains("excavator")) return SteerKind.Levers;

            // 3) Direcao articulada (carregadoras de rodas, tratores articulados, forwarders).
            if (articulated || c.Contains("WHEELLOADER") || t.Contains("wheelloader") || c.Contains("FORWARDER") || t.Contains("forwarder"))
                return SteerKind.Articulated;

            // 4) Rastos sem rodas direcionais (a direcao e por diferencial dos rastos).
            if (crawler && (c.Contains("TRACTOR") || c.Contains("HARVESTER") || t.Contains("tractor") || t.Contains("combine")))
                return SteerKind.Tracks;

            // 5) Tudo o resto que e maquina agricola/industrial: direcao hidrostatica.
            string[] hydroCat = { "TRACTOR", "HARVESTER", "SPRAYER", "MOWER", "LOADER", "TELE", "FORKLIFT", "FORESTRY",
                                  "BEET", "POTATO", "VEGETABLE", "COTTON", "GRAPE", "OLIVE", "SUGARCANE", "RICE", "SPINACH",
                                  "PEA", "BEAN", "SPREADER", "BALER", "MISCVEHICLES", "WINDROWER" };
            foreach (string h in hydroCat) if (c.Contains(h)) return SteerKind.Hydrostatic;
            string[] hydroType = { "tractor", "combine", "selfpropelled", "harvester", "telehandler", "teleloader", "forklift",
                                   "sprayer", "mower", "loader", "woodharvester" };
            foreach (string h in hydroType) if (t.Contains(h)) return SteerKind.Hydrostatic;

            return SteerKind.Unknown;   // desconhecido: centra (comportamento de sempre)
        }

        public static bool CentersInRealLife(SteerKind k) { return k == SteerKind.Mechanical || k == SteerKind.Unknown; }

        // Uma linha simples: porque centra ou nao centra na vida real.
        public static string KindDetail(SteerKind k)
        {
            switch (k)
            {
                case SteerKind.Mechanical: return L.T("Mechanical steering: the steering wheel is linked to the wheels, so it comes back to the centre by itself while driving.");
                case SteerKind.Hydrostatic: return L.T("Hydrostatic steering (orbitrol): only oil links the steering wheel to the wheels. Let go and it stays where it is, with some weight.");
                case SteerKind.Articulated: return L.T("Articulated steering, hydraulic: the machine bends in the middle. Let go and the steering wheel stays where it is.");
                case SteerKind.Tracks: return L.T("Tracks: the steering wheel controls the speed of each track. It does not come back to the centre.");
                case SteerKind.Levers: return L.T("Driven with levers or joystick: there is no steering wheel that comes back to the centre.");
                default: return L.T("Type not recognised: it keeps centering as before. Choose by hand below if you want.");
            }
        }

        public static string KindText(SteerKind k)
        {
            switch (k)
            {
                case SteerKind.Mechanical: return L.T("mechanical steering (car, pickup, truck): centers in real life");
                case SteerKind.Hydrostatic: return L.T("hydrostatic steering (orbitrol): does not center in real life");
                case SteerKind.Articulated: return L.T("articulated steering: does not center in real life");
                case SteerKind.Tracks: return L.T("tracks: does not center in real life");
                case SteerKind.Levers: return L.T("levers/joystick: does not center in real life");
                default: return L.T("unknown type: centers (as before)");
            }
        }
    }

    // Escolha por veiculo: 0 = automatico, 1 = centra sempre, 2 = nunca centra.
    internal static class CenteringOverrides
    {
        private static Dictionary<string, int> map;

        private static string FilePath { get { return AppInfo.DataFile("centragem.ini"); } }

        private static void EnsureLoaded()
        {
            if (map != null) return;
            map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    if (raw.StartsWith(";")) continue;
                    int eq = raw.LastIndexOf('=');
                    if (eq <= 0) continue;
                    string v = raw.Substring(eq + 1).Trim().ToLowerInvariant();
                    int mode = v == "on" ? 1 : (v == "off" ? 2 : 0);
                    if (mode != 0) map[raw.Substring(0, eq).Trim()] = mode;
                }
            }
            catch { }
        }

        public static int Get(string vehicle)
        {
            if (string.IsNullOrEmpty(vehicle)) return 0;
            EnsureLoaded();
            int m;
            return map.TryGetValue(vehicle, out m) ? m : 0;
        }

        public static void Set(string vehicle, int mode)
        {
            if (string.IsNullOrEmpty(vehicle)) return;
            EnsureLoaded();
            if (mode == 0) map.Remove(vehicle); else map[vehicle] = mode;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("; FS25 RealFFB - centering per vehicle (on = always centers, off = never centers)");
            foreach (KeyValuePair<string, int> kv in map) sb.AppendLine(kv.Key + "=" + (kv.Value == 1 ? "on" : "off"));
            File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
        }

        public static int Count() { EnsureLoaded(); return map.Count; }
    }
}
