// FS25 Real FFB - saida de forca pelo proprio jogo.
// A app calcula as forcas e escreve-as em memoria partilhada ("FS25RealFFB_Out");
// o dinput8.dll do projeto, carregado dentro do FS25, aplica-as na base.
using System;
using System.IO.MemoryMappedFiles;

namespace FS25RealFFB
{
    internal sealed class SharedMemOutput : IWheelOutput
    {
        public const string MapName = "FS25RealFFB_Out";
        private readonly Action<string> log;
        private readonly Settings settings;
        private readonly MemoryMappedFile mmf;
        private readonly MemoryMappedViewAccessor acc;
        private readonly int[] v = new int[16];
        private int seq;
        private bool enabled = true;
        private int lastBeat = int.MinValue, lastBeatMs;

        public bool IsOpen { get; private set; }

        public SharedMemOutput(Action<string> log, Settings settings)
        {
            this.log = log;
            this.settings = settings;
            mmf = MemoryMappedFile.CreateOrOpen(MapName, 256);
            acc = mmf.CreateViewAccessor(0, 256);
            acc.Write(0 * 4, 0x42464652);
            acc.Write(1 * 4, 1);
            IsOpen = true;
            log("Saida de forca: pelo FS25 (dinput8.dll do projeto).");
        }

        public void SetSpring(int coef, int sat) { v[4] = coef; v[5] = sat; enabled = true; }
        public void SetDamper(int coef, int sat) { v[6] = coef; v[7] = sat; }
        public void SetFriction(int coef, int sat) { v[8] = coef; v[9] = sat; }
        public void SetConstant(int mag) { v[10] = mag; }
        public void SetSine(int mag, int periodUs) { v[11] = mag; v[12] = periodUs; }

        public void Commit()
        {
            acc.Write(3 * 4, enabled ? 1 : 0);
            for (int i = 4; i <= 12; i++) acc.Write(i * 4, v[i]);
            acc.Write(13 * 4, settings.AltInterface ? 1 : 0);
            seq++;
            acc.Write(2 * 4, seq);
        }

        public int ReadPosition() { return 0; }

        public void EmergencyStop()
        {
            enabled = false;
            for (int i = 4; i <= 12; i++) v[i] = 0;
            Commit();
        }

        public bool DllAlive
        {
            get
            {
                int beat = acc.ReadInt32(16 * 4);
                int now = Environment.TickCount;
                if (beat != lastBeat) { lastBeat = beat; lastBeatMs = now; }
                return now - lastBeatMs < 1000 && beat != 0;
            }
        }

        public string DeviceName
        {
            get
            {
                if (!DllAlive) return "FS25: dinput8.dll nao detetada (jogo fechado ou dll nao instalada)";
                int st = acc.ReadInt32(17 * 4);
                int hr = acc.ReadInt32(18 * 4);
                int devs = acc.ReadInt32(19 * 4);
                int fx = acc.ReadInt32(21 * 4);
                return string.Format("FS25 dll ATIVA | bases {0} | efeitos {1}/5 | {2} | dados {3} | hr 0x{4:X8}",
                    devs, fx, (st & 8) != 0 ? "A TOCAR" : "parado", (st & 16) != 0 ? "ok" : "sem", hr);
            }
        }

        public void Dispose()
        {
            try { EmergencyStop(); } catch { }
            acc.Dispose();
            mmf.Dispose();
            IsOpen = false;
        }
    }
}
