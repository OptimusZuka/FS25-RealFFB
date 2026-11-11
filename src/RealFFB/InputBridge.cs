// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - ponte de comandos: a app tem a base em exclusivo (para o FFB funcionar),
// por isso e ela que entrega direcao e pedais ao jogo.
// Canal: ficheiro XML pequeno em modSettings, que o mod le a cada frame com a API XML do
// jogo (o FS25 nao deixa os mods lerem de named pipes).
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

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

        public long Served { get { return Interlocked.Read(ref written); } }

        // A escrita do ficheiro (gravar + substituir) custa varios ms, sobretudo com o antivirus a
        // analisar cada ficheiro novo. Antes era feita ate 120x/s dentro do ciclo do FFB, atrasando-o,
        // e o jogo tentava ler o ficheiro enquanto ele era substituido. Agora:
        //  - escreve numa thread propria (o ciclo do FFB nunca espera pelo disco);
        //  - so quando os valores mudam (no maximo ~60x/s, o ritmo de frames do jogo);
        //  - parado, um "sinal de vida" a cada 200 ms (o mod desliga a ponte apos 500 ms sem novidades).
        private readonly object gate = new object();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private Thread worker;
        private volatile bool running;
        private bool curActive; private double curS, curT, curB, curC; private bool curF, curO;
        private bool lastActive; private double lastS = double.NaN, lastT, lastB, lastC; private bool lastF, lastO;
        private const int MinGapMs = 16, HeartbeatMs = 200;

        public void Start()
        {
            if (worker != null) return;
            running = true;
            worker = new Thread(Loop);
            worker.IsBackground = true;
            worker.Name = "RealFFB bridge";
            worker.Start();
        }

        public void Stop()
        {
            running = false;
            wake.Set();
            if (worker != null) { try { worker.Join(500); } catch { } worker = null; }
            bool a; double s, th, b, c; bool fo, ov;
            lock (gate) { a = curActive; s = curS; th = curT; b = curB; c = curC; fo = curF; ov = curO; }
            Write(a, s, th, b, c, fo, ov);   // estado final (normalmente "inativo")
        }

        public void Publish(bool active, double steer, double throttle, double brake, double clutch)
        {
            Publish(active, steer, throttle, brake, clutch, false, false);
        }

        // follow = o volante esta a seguir a IA/GPS: o mod nao envia a direcao ao jogo (0.11)
        // takeOver = o condutor segurou o volante: o mod dispensa a IA / desliga o GPS (0.11.1)
        public void Publish(bool active, double steer, double throttle, double brake, double clutch, bool follow, bool takeOver)
        {
            lock (gate) { curActive = active; curS = steer; curT = throttle; curB = brake; curC = clutch; curF = follow; curO = takeOver; }
            if (worker == null) { Write(active, steer, throttle, brake, clutch, follow, takeOver); return; }
            wake.Set();
        }

        private void Loop()
        {
            while (running)
            {
                wake.WaitOne(HeartbeatMs);
                if (!running) break;
                bool a; double s, th, b, c; bool fo, ov;
                lock (gate) { a = curActive; s = curS; th = curT; b = curB; c = curC; fo = curF; ov = curO; }
                int now = Environment.TickCount;
                int since = now - lastWriteMs;
                bool changed = a != lastActive || fo != lastF || ov != lastO || double.IsNaN(lastS) || Math.Abs(s - lastS) > 0.0002
                    || Math.Abs(th - lastT) > 0.002 || Math.Abs(b - lastB) > 0.002 || Math.Abs(c - lastC) > 0.002;
                if (!changed && since < HeartbeatMs) continue;
                if (since < MinGapMs) { Thread.Sleep(MinGapMs - since); }
                lock (gate) { a = curActive; s = curS; th = curT; b = curB; c = curC; fo = curF; ov = curO; }
                Write(a, s, th, b, c, fo, ov);
            }
        }

        private void Write(bool active, double steer, double throttle, double brake, double clutch, bool follow, bool takeOver)
        {
            lastWriteMs = Environment.TickCount;
            seq++;
            CultureInfo c = CultureInfo.InvariantCulture;
            // Tamanho fixo (espacos no fim): o ficheiro e reescrito no mesmo sitio (sem apagar nem substituir), por isso o
            // jogo le sempre um XML completo. Antes, File.Replace trocava o ficheiro enquanto o jogo o
            // abria: ~25% das leituras falhavam ("Sharing violation", ~10 erros/s no log.txt do jogo).
            string xml = string.Format(c, "<i q=\"{0:000000}\" a=\"{1}\" s=\"{2:0.0000}\" t=\"{3:0.0000}\" b=\"{4:0.0000}\" c=\"{5:0.0000}\" f=\"{6}\" o=\"{7}\"/>",
                seq % 1000000, active ? 1 : 0, steer, throttle, brake, clutch, follow ? 1 : 0, takeOver ? 1 : 0);
            xml = xml.PadRight(112);   // espacos depois do elemento sao XML valido; tamanho sempre igual
            byte[] data = Encoding.ASCII.GetBytes(xml);
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    fs.Write(data, 0, data.Length);
                    if (fs.Length != data.Length) fs.SetLength(data.Length);
                }
                // so conta como escrito se correu bem; se o jogo tinha o ficheiro aberto, tenta-se de novo ja a seguir
                lastActive = active; lastS = steer; lastT = throttle; lastB = brake; lastC = clutch; lastF = follow; lastO = takeOver;
                Interlocked.Increment(ref written);
            }
            catch (Exception ex)
            {
                errors++;
                if (errors == 1 || errors % 5000 == 0) log("Bridge: write failed (" + ex.Message + ") - fine if occasional.");
            }
        }
    }
}
