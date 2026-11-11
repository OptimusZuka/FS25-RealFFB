// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - catalogo de volantes.
// 0.13: os dados passaram para Devices/devices.ini (DeviceCatalog). Esta classe fica como fachada, com a
// mesma API de antes, para nao mudar o resto da app. Valores iguais aos da 0.12.
namespace FS25RealFFB
{
    internal enum WheelDrive { Gear, Belt, DirectDrive }

    internal sealed class WheelInfo
    {
        public readonly string Model;
        public readonly double PeakNm;      // 0 = desconhecido
        public readonly WheelDrive Drive;
        public readonly bool Known;
        public readonly ValidationLevel Validation;
        public readonly string Notes;
        public WheelInfo(string model, double nm, WheelDrive drive, bool known, ValidationLevel v, string notes)
        { Model = model; PeakNm = nm; Drive = drive; Known = known; Validation = v; Notes = notes ?? ""; }

        // engrenagens e correia tem atrito interno e nao tem o Natural Damper das bases Fanatec DD
        public bool GearProfile { get { return Drive != WheelDrive.DirectDrive; } }
    }

    internal static class WheelCatalog
    {
        public const int VidFanatec = 0x0EB7, VidLogitech = 0x046D, VidThrustmaster = 0x044F;

        // Volante desconhecido: assumir uma base forte. As forcas sao calculadas em Nm e convertidas em
        // percentagem deste valor, por isso assumir muito = pedir pouco ao motor (seguro).
        public const double UnknownNm = 20.0;

        public static WheelInfo Find(int vid, int pid) { return Find(vid, pid, null); }

        public static WheelInfo Find(int vid, int pid, string productName)
        {
            CatalogEntry e = DeviceCatalog.Find(vid, pid, productName);
            bool known = e.Known && e.PeakNm > 0;
            return new WheelInfo(e.Model, known ? e.PeakNm : 0, e.Drive, known, e.Validation, e.Notes);
        }
    }
}
