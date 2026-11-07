// Dialogos do assistente (preto/branco/amarelo), usados na configuracao dos dispositivos.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RealFFBSetup
{
    internal static class Ui
    {
        public static readonly Color Bg = Color.FromArgb(18, 18, 18);
        public static readonly Color Fg = Color.White;
        public static readonly Color Accent = Color.FromArgb(255, 204, 0);

        private static Form NewForm(string title, int w, int h)
        {
            Form f = new Form();
            f.Text = "FS25 RealFFB Installer - " + title;
            f.BackColor = Bg; f.ForeColor = Fg;
            f.Font = new Font("Segoe UI", 9.5f);
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MaximizeBox = false; f.MinimizeBox = false;
            f.StartPosition = FormStartPosition.CenterScreen;
            f.ClientSize = new Size(w, h);
            f.TopMost = true;
            try { f.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Label head = new Label();
            head.Text = title; head.ForeColor = Accent;
            head.Font = new Font("Segoe UI Semibold", 13f);
            head.SetBounds(16, 12, w - 32, 30);
            f.Controls.Add(head);
            return f;
        }

        private static Button NewButton(string text, DialogResult r, int x, int y, bool primary)
        {
            Button b = new Button();
            b.Text = text; b.DialogResult = r;
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = primary ? Accent : Color.FromArgb(45, 45, 45);
            b.ForeColor = primary ? Color.Black : Fg;
            b.FlatAppearance.BorderColor = Accent;
            b.SetBounds(x, y, 120, 32);
            return b;
        }

        // Devolve o indice escolhido ou -1 se cancelado.
        public static int Choose(string title, string text, List<string> options)
        {
            int h = 140 + options.Count * 30;
            Form f = NewForm(title, 520, h);
            Label t = new Label(); t.Text = text; t.SetBounds(16, 48, 488, 44); f.Controls.Add(t);
            List<RadioButton> rb = new List<RadioButton>();
            for (int i = 0; i < options.Count; i++)
            {
                RadioButton r = new RadioButton();
                r.Text = options[i]; r.SetBounds(24, 96 + i * 30, 470, 26);
                r.Checked = false;   // nunca pre-selecionar: o utilizador escolhe
                f.Controls.Add(r); rb.Add(r);
            }
            Button ok = NewButton("Continuar", DialogResult.OK, 252, h - 46, true);
            Button no = NewButton("Cancelar", DialogResult.Cancel, 384, h - 46, false);
            ok.Enabled = false;
            foreach (RadioButton r in rb) r.CheckedChanged += delegate { ok.Enabled = true; };
            f.Controls.Add(ok); f.Controls.Add(no);
            f.AcceptButton = ok; f.CancelButton = no;
            if (f.ShowDialog() != DialogResult.OK) return -1;
            for (int i = 0; i < rb.Count; i++) if (rb[i].Checked) return i;
            return -1;
        }

        public static bool Confirm(string title, string text)
        {
            Form f = NewForm(title, 560, 360);
            TextBox t = new TextBox();
            t.Multiline = true; t.ReadOnly = true; t.ScrollBars = ScrollBars.Vertical;
            t.BackColor = Color.FromArgb(30, 30, 30); t.ForeColor = Fg; t.BorderStyle = BorderStyle.None;
            t.Text = text.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n");
            t.SetBounds(16, 50, 528, 250);
            f.Controls.Add(t);
            Button ok = NewButton("Aplicar", DialogResult.Yes, 292, 314, true);
            Button no = NewButton("Nao alterar", DialogResult.No, 424, 314, false);
            f.Controls.Add(ok); f.Controls.Add(no);
            f.CancelButton = no;
            f.Shown += delegate { no.Focus(); };
            return f.ShowDialog() == DialogResult.Yes;
        }

        public static void Info(string title, string text) { MessageBox.Show(text, "FS25 RealFFB Installer - " + title, MessageBoxButtons.OK, MessageBoxIcon.Information); }
        public static void Error(string title, string text) { MessageBox.Show(text, "FS25 RealFFB Installer - " + title, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
