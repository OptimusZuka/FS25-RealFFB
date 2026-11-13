// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// 0.15: limites do binario de pico configurado ("Wheel torque (peak)").
//
// Quatro grandezas que NAO se podem confundir:
//   - pico do hardware (HwPeakNm): o maximo fisico da base, do catalogo, so para um modelo IDENTIFICADO;
//   - pico configurado (BaseNm): a referencia de calibracao; a app converte Nm em % do motor com ele;
//   - ganho / multiplicador: percentagem da forca calculada;
//   - binario enviado: o que a base recebe (sempre <= "Maximum force" e <= o pico configurado).
//
// Regras:
//   1. Modelo identificado: o pico configurado NUNCA pode passar o pico do fabricante (teto duro).
//      Abaixo dele e permitido (bases limitadas: CSL DD sem Boost Kit = 5 Nm), com aviso.
//   2. Modelo nao identificado: nao ha teto verificado; usa-se o teto generico (40 Nm) e a app ASSUME 20 Nm
//      ate o utilizador confirmar. Assumir um pico ALTO e o lado seguro: as forcas saem mais FRACAS
//      (um pico configurado abaixo do real faria as forcas sairem mais FORTES do que o mostrado).
//   3. Um valor corrigido nunca e aceite em silencio: Validate devolve a explicacao.
using System;
using System.Globalization;

namespace FS25RealFFB
{
    internal static class TorqueLimits
    {
        public const double MinNm = 1.0;
        public const double GenericCeilingNm = 40.0;   // sem modelo identificado (maior base de consumo conhecida ~32 Nm)

        public static double Ceiling(double hwPeakNm)
        {
            return hwPeakNm > 0 ? hwPeakNm : GenericCeilingNm;
        }

        // Valor aceite para um pico pedido. reason = "" se aceite tal como pedido, senao a explicacao (ingles; a UI traduz).
        public static double Validate(double requestedNm, double hwPeakNm, string model, out string reason)
        {
            reason = "";
            if (double.IsNaN(requestedNm) || double.IsInfinity(requestedNm))
            {
                reason = "invalid value";
                return hwPeakNm > 0 ? hwPeakNm : WheelCatalog.UnknownNm;
            }
            double ceil = Ceiling(hwPeakNm);
            if (requestedNm > ceil + 1e-9)
            {
                reason = hwPeakNm > 0
                    ? string.Format(CultureInfo.InvariantCulture, "{0} has {1:0.0} Nm peak torque (manufacturer). A higher value is not physically possible on this base.", model, hwPeakNm)
                    : string.Format(CultureInfo.InvariantCulture, "{0:0} Nm is the maximum accepted for a base whose model is not identified.", ceil);
                return ceil;
            }
            if (requestedNm < MinNm - 1e-9)
            {
                reason = string.Format(CultureInfo.InvariantCulture, "the minimum is {0:0.0} Nm", MinNm);
                return MinNm;
            }
            return requestedNm;
        }

        // Pico configurado abaixo do pico do fabricante: permitido, mas as forcas saem mais fortes do que o mostrado
        // se a base nao estiver mesmo limitada. Devolve o aviso ou "".
        public static string BelowSpecWarning(double configuredNm, double hwPeakNm, string model)
        {
            if (hwPeakNm <= 0 || configuredNm >= hwPeakNm - 0.05) return "";
            return string.Format(CultureInfo.InvariantCulture,
                "Configured {0:0.0} Nm is below the {1:0.0} Nm of the {2}. Only correct if your base is limited (firmware or power mode); otherwise forces are {3:0}% stronger than shown.",
                configuredNm, hwPeakNm, model, (hwPeakNm / Math.Max(0.1, configuredNm) - 1) * 100);
        }
    }
}
