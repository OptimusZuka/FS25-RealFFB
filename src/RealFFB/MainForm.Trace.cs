// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - registo de diagnostico (trace). Separado de MainForm.cs na 0.10.0.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FS25RealFFB
{
    internal sealed partial class MainForm
    {
        // Registo de diagnostico a cada ciclo (~60 Hz), so quando existe o ficheiro "trace.on" na pasta de
        // dados da app. Desligado = custo zero (verifica-se a existencia do ficheiro de 2 em 2 s).
        // 0.10.0: desliga-se sozinho ao fim de 30 min ou 50 MB (antes crescia sem limite), e o tempo vem do
        // relogio de alta resolucao (Environment.TickCount so avanca em degraus de ~15,6 ms).
        private const double TraceMaxMinutes = 30;
        private const long TraceMaxBytes = 50L * 1024 * 1024;
        private StreamWriter traceW;
        private int traceCheckMs;
        private bool traceChecked, traceOn, traceStopped;
        private double traceStartMs;

        private void Trace(TelemetryFrame f, FfbOutput o)
        {
            int now = Environment.TickCount;
            // (0.9.0 usava int.MinValue como inicio: now - MinValue transborda e o registo nunca ligava)
            if (!traceChecked || unchecked(now - traceCheckMs) > 2000)
            {
                traceCheckMs = now; traceChecked = true;
                bool want = !traceStopped && File.Exists(AppInfo.DataFile("trace.on"));
                if (want && traceW == null)
                {
                    try
                    {
                        traceW = new StreamWriter(AppInfo.DataFile("RealFFB_trace.csv"), true);
                        traceW.WriteLine("ms;kmh;wheelPos;sentSteer;gameSteer;springSat;springK;springOff;damperSat;damperK;frictionSat;const;sine;active;engineOn;rpm;gain;engineMag");
                        traceStartMs = clock.Elapsed.TotalMilliseconds;
                        Log("Diagnostic trace ON -> RealFFB_trace.csv (stops by itself after 30 min or 50 MB)");
                    }
                    catch { traceW = null; }
                }
                else if (!want && traceW != null) { try { traceW.Dispose(); } catch { } traceW = null; Log("Diagnostic trace OFF"); }
                traceOn = traceW != null;
                if (traceOn)
                {
                    try { traceW.Flush(); } catch { }
                    long size = 0;
                    try { size = traceW.BaseStream.Length; } catch { }
                    double mins = (clock.Elapsed.TotalMilliseconds - traceStartMs) / 60000.0;
                    if (mins > TraceMaxMinutes || size > TraceMaxBytes)
                    {
                        try { traceW.Dispose(); } catch { }
                        traceW = null; traceOn = false; traceStopped = true;
                        Log("Diagnostic trace OFF automatically (30 min / 50 MB limit). Delete the file trace.on when you no longer need it.");
                    }
                }
            }
            if (!traceOn || o == null) return;
            try
            {
                System.Globalization.CultureInfo c = System.Globalization.CultureInfo.InvariantCulture;
                double kmh = (f != null && !double.IsNaN(f.SpeedKmh)) ? f.SpeedKmh : 0;
                double gs = (f != null && !double.IsNaN(f.Steer)) ? f.Steer : 0;
                double wp = (wheel != null && wheel.IsOpen) ? wheel.Axes[0] / 10000.0 : 0;
                traceW.WriteLine(string.Format(c, "{0:0.0};{1:0.0};{2:0.0000};{3:0.0000};{4:0.0000};{5};{6};{7};{8};{9};{10};{11};{12};{13};{14};{15:0};{16:0.00};{17}",
                    clock.Elapsed.TotalMilliseconds, kmh, wp, steerOut, gs, o.SpringSat, o.SpringCoef, o.SpringOffset, o.DamperSat, o.DamperCoef, o.FrictionSat, o.ConstMag, o.SineMag,
                    (f != null && f.Active) ? 1 : 0, (f != null && f.EngineOn) ? 1 : 0, (f != null && !double.IsNaN(f.Rpm)) ? f.Rpm : 0, o.Gain, o.EngineMag));
            }
            catch { }
        }
    }
}
