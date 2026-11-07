// FS25 Real FFB - ponte de comandos: a app tem a base em exclusivo (para o FFB funcionar),
// por isso e ela que entrega direcao e pedais ao jogo.
// Canal: ficheiro XML pequeno em modSettings, que o mod le a cada frame com a API XML do
// jogo (o FS25 nao deixa os mods lerem de named pipes).
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace FS25RealFFB
{
    internal sealed class InputBridge
    {
        private readonly Action<string> log;
        private readonly string path, tmp;
        private long seq;
        private long written;
        private int lastWriteMs;
        private int errors;

        public InputBridge(Action<string> log)
        {
            this.log = log;
            string dir = Path.Combine(Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "My Games"), "FarmingSimulator2025"), "modSettings");
            try { Directory.CreateDirectory(dir); } catch { }
            path = Path.Combine(dir, "FS25_RealFFB_input.xml");
            tmp = path + ".tmp";
            log("Command bridge: " + path);
        }

        public long Served { get { return written; } }

        public void Start() { }
        public void Stop() { }

        public void Publish(bool active, double steer, double throttle, double brake, double clutch)
        {
            int now = Environment.TickCount;
            if (now - lastWriteMs < 8) return;   // ~120 escritas/s no maximo
            lastWriteMs = now;
            seq++;
            CultureInfo c = CultureInfo.InvariantCulture;
            string xml = string.Format(c, "<i q=\"{0}\" a=\"{1}\" s=\"{2:0.0000}\" t=\"{3:0.0000}\" b=\"{4:0.0000}\" c=\"{5:0.0000}\"/>",
                seq % 1000000, active ? 1 : 0, steer, throttle, brake, clutch);
            try
            {
                File.WriteAllText(tmp, xml, Encoding.ASCII);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                written++;
            }
            catch (Exception ex)
            {
                errors++;
                if (errors == 1 || errors % 5000 == 0) log("Bridge: write failed (" + ex.Message + ") - fine if occasional.");
            }
        }
    }
}
