// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 RealFFB 0.13 - "Testar o meu volante": 6 passos curtos, com forcas baixas (no maximo 30 % do
// limite), para o proprio utilizador confirmar o que o volante faz de facto. O software nao consegue
// medir binario: so a pessoa sabe se sentiu. O resultado entra no relatorio de compatibilidade.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace FS25RealFFB
{
    internal sealed class WheelTestResult
    {
        public readonly List<string[]> Steps = new List<string[]>();   // {passo, resposta}
        public string Summary()
        {
            StringBuilder sb = new StringBuilder();
            foreach (string[] s in Steps) sb.Append(s[0] + "=" + s[1] + "; ");
            return sb.ToString().TrimEnd(' ', ';');
        }
    }

    internal sealed class WheelTestWizard : Form
    {
        private readonly IEffectSink dev;
        private readonly Settings s;
        private readonly Action<string> log;
        public WheelTestResult Result = new WheelTestResult();
        private int step = -1;
        private readonly Label title = new Label(), text = new Label();
        private readonly Button run = new Button(), yes = new Button(), no = new Button(), skip = new Button(), alt = new Button();
        private readonly Timer stopTimer = new Timer();

        private struct Step { public string Id, Title, Text, Question, Yes, No, Alt; public int Ms; }
        private readonly List<Step> steps = new List<Step>();

        public WheelTestWizard(IEffectSink dev, Settings s, Action<string> log)
        {
            this.dev = dev; this.s = s; this.log = log;
            Text = L.T("Test my wheel");
            BackColor = Theme.Bg; ForeColor = Theme.Text; Font = Theme.UI(9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(560, 330);

            Add("left", L.T("1. Push to one side"), L.T("Hold the wheel lightly. The app will push it gently for under a second."),
                L.T("Which way did the wheel push?"), L.T("Left"), L.T("Right"), L.T("Did not move"), 700);
            Add("right", L.T("2. Push to the other side"), L.T("Hold the wheel lightly. The app will push it gently the other way."),
                L.T("Which way did the wheel push?"), L.T("Left"), L.T("Right"), L.T("Did not move"), 700);
            Add("spring", L.T("3. Centering spring"), L.T("For 8 seconds: turn the wheel away from the centre and let go."),
                L.T("Did it pull back towards the centre?"), L.T("Yes"), L.T("No"), null, 8000);
            Add("damper", L.T("4. Damping"), L.T("For 8 seconds: turn the wheel quickly left and right, then slowly."),
                L.T("Was it heavier when turning fast than when turning slowly?"), L.T("Yes"), L.T("No"), null, 8000);
            Add("friction", L.T("5. Friction"), L.T("For 8 seconds: turn the wheel slowly."),
                L.T("Did it feel evenly heavy (sticky), even when turning slowly?"), L.T("Yes"), L.T("No"), null, 8000);
            Add("vibration", L.T("6. Vibration"), L.T("For 2 seconds: keep your hands on the wheel."),
                L.T("Did you feel a vibration?"), L.T("Yes"), L.T("No"), null, 2000);

            title.SetBounds(20, 16, 520, 30); title.Font = Theme.Bold(12f); title.ForeColor = Theme.Yellow;
            text.SetBounds(20, 56, 520, 150); text.ForeColor = Theme.Soft;
            Controls.Add(title); Controls.Add(text);
            Style(run, L.T("Run this step"), 20, true);
            Style(yes, "", 160, false); Style(no, "", 280, false); Style(alt, "", 400, false);
            Style(skip, L.T("Skip"), 20, false); skip.Top = 280;
            run.Click += delegate { RunStep(); };
            yes.Click += delegate { Answer(yes.Text); };
            no.Click += delegate { Answer(no.Text); };
            alt.Click += delegate { Answer(alt.Text); };
            skip.Click += delegate { Answer("skipped"); };
            stopTimer.Tick += delegate { stopTimer.Stop(); StopForces(); ShowAnswers(true); };
            FormClosing += delegate { stopTimer.Stop(); StopForces(); };
            Next();
        }

        private void Add(string id, string t, string x, string q, string y, string n, string a, int ms)
        {
            Step st = new Step(); st.Id = id; st.Title = t; st.Text = x; st.Question = q; st.Yes = y; st.No = n; st.Alt = a; st.Ms = ms;
            steps.Add(st);
        }

        private void Style(Button b, string t, int x, bool primary)
        {
            b.Text = t; b.SetBounds(x, 230, primary ? 130 : 110, 34);
            b.FlatStyle = FlatStyle.Flat; b.BackColor = primary ? Theme.Yellow : Theme.Card; b.ForeColor = primary ? Color.Black : Theme.Text;
            b.FlatAppearance.BorderColor = primary ? Theme.Yellow : Theme.Line;
            Controls.Add(b);
        }

        private void Next()
        {
            step++;
            if (step >= steps.Count)
            {
                title.Text = L.T("Done");
                text.Text = L.T("Thank you. The answers are in the Hardware page and in the compatibility report (Export compatibility report).") + "\n\n" + Result.Summary();
                run.Visible = yes.Visible = no.Visible = alt.Visible = skip.Visible = false;
                return;
            }
            Step st = steps[step];
            title.Text = st.Title;
            text.Text = st.Text + "\n\n" + L.T("Forces are low (at most 30 % of your Maximum force). You can close this window at any time: the forces stop.");
            run.Visible = true; run.Enabled = true;
            ShowAnswers(false);
        }

        private void ShowAnswers(bool show)
        {
            Step st = steps[Math.Min(step, steps.Count - 1)];
            if (show) text.Text = st.Question;
            yes.Text = st.Yes; no.Text = st.No; alt.Text = st.Alt ?? "";
            yes.Visible = no.Visible = show; alt.Visible = show && st.Alt != null;
            run.Visible = !show; skip.Visible = true;
        }

        private void RunStep()
        {
            Step st = steps[step];
            double cap = s.ForceCapNm;
            run.Enabled = false;
            try
            {
                // primeiro tudo a zero (ao reiniciar, os efeitos voltariam com os ultimos valores do jogo)
                dev.SetSpring(0, 0, 0); dev.SetDamper(0, 0); dev.SetFriction(0, 0); dev.SetSine(0, 50000); dev.SetEngine(0, 25000); dev.SetConstant(0);
                switch (st.Id)
                {
                    case "left": dev.SetConstant(-s.ToMag(0.15 * cap)); break;
                    case "right": dev.SetConstant(s.ToMag(0.15 * cap)); break;
                    case "spring": dev.SetSpring(4000, s.ToMag(0.3 * cap), 0); break;
                    case "damper": dev.SetDamper(6000, s.ToMag(0.3 * cap)); break;
                    case "friction": { int m = s.ToMag(0.25 * cap); dev.SetFriction(m, m); } break;
                    case "vibration": dev.SetSine(s.ToMag(0.15 * cap), 50000); break;
                }
                if (log != null) log("Wheel test: step " + st.Id + " running.");
            }
            catch (Exception ex) { if (log != null) log("Wheel test: " + ex.Message); }
            stopTimer.Interval = st.Ms;
            stopTimer.Start();
        }

        private void StopForces()
        {
            try
            {
                dev.SetConstant(0); dev.SetSpring(0, 0, 0); dev.SetDamper(0, 0); dev.SetFriction(0, 0); dev.SetSine(0, 50000);
                dev.EmergencyStop();
            }
            catch { }
        }

        private void Answer(string a)
        {
            stopTimer.Stop();
            StopForces();
            Result.Steps.Add(new string[] { steps[step].Id, a });
            Next();
        }
    }
}
