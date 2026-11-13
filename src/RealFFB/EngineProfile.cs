// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// 0.15: quanto da vibracao do motor chega ao VOLANTE, por tipo de veiculo.
//
// O jogo da o estado do motor (desligado / a arrancar / ligado), as rotacoes e a carga, mas nao diz como o motor
// esta montado. A diferenca de sensacao entre um trator e um camiao vem sobretudo da montagem, que depende do tipo
// de veiculo (categoria da loja do jogo):
//   - trator: o bloco do motor faz parte do chassis e a coluna da direcao esta aparafusada a ele -> sente-se tudo;
//   - trator grande: mais massa e cabine muitas vezes suspensa -> um pouco menos;
//   - ceifeiras/colhedoras: motor atras, cabine isolada noutra estrutura -> bastante menos;
//   - carregadoras/telescopicas: chassis rigido mas cabine com apoios de borracha -> intermedio;
//   - camioes: cabine suspensa (molas/ar) sobre o chassis -> pouco chega ao volante;
//   - carros/pickups: motor em apoios hidraulicos, coluna isolada -> quase nada.
// Os fatores sao uma APROXIMACAO (documentada), nao uma medicao: o jogo nao expoe a montagem nem o numero de
// cilindros. A frequencia continua a vir das rotacoes reais (ver FfbEngine).
using System;

namespace FS25RealFFB
{
    internal sealed class EngineProfile
    {
        public readonly string Name;
        public readonly double Mount;   // 0..1: fracao da vibracao do motor que chega ao volante

        private EngineProfile(string name, double mount) { Name = name; Mount = mount; }

        public static readonly EngineProfile Tractor = new EngineProfile("tractor", 1.0);
        public static readonly EngineProfile HeavyTractor = new EngineProfile("heavy tractor", 0.85);
        public static readonly EngineProfile Harvester = new EngineProfile("harvester", 0.55);
        public static readonly EngineProfile Loader = new EngineProfile("loader", 0.75);
        public static readonly EngineProfile Truck = new EngineProfile("truck", 0.45);
        public static readonly EngineProfile Car = new EngineProfile("car / pickup", 0.35);
        public static readonly EngineProfile Default = new EngineProfile("other vehicle", 0.8);

        public static EngineProfile For(string category)
        {
            string c = (category ?? "").ToUpperInvariant();
            if (c.Length == 0 || c == "?") return Default;
            if (c == "TRACTORSL") return HeavyTractor;
            if (c.StartsWith("TRACTORS")) return Tractor;
            if (c.Contains("HARVEST")) return Harvester;
            if (c.Contains("LOADER") || c.Contains("SKIDSTEER") || c.Contains("FORKLIFT")) return Loader;
            if (c == "TRUCKS") return Truck;
            if (c == "CARS") return Car;
            return Default;
        }
    }
}
