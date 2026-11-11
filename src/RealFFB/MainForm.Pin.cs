// FS25 RealFFB - Copyright (c) 2026 OptimusZuka. All rights reserved. See LICENSE.
// FS25 Real FFB - janela principal: modos fixos por cima do jogo ("Fixar no topo", "Fixar veiculo") e ajuda do desenho.
// Separado de MainForm.cs na 0.10.0 (mesmo codigo, so mudou de ficheiro).
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
        // Alguns jogos, ao ganhar o foco, colocam-se acima das janelas "sempre por cima".
        // Reafirmar a posicao (sem roubar o foco ao jogo).
        private void KeepOnTop()
        {
            try { SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE); } catch { }
        }

        // Fixar: janela sempre por cima, so com o painel ao vivo (para ver os comandos enquanto joga).
        private void TogglePin()
        {
            if (vehPinned && !pinned) ToggleVehPin();   // sai do modo so-veiculo antes
            pinned = !pinned;
            if (pinned)
            {
                normalBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                WindowState = FormWindowState.Normal;
                leftPanel.Visible = false;
                monitor.Visible = false;
                chipsBox.Visible = false;
                TopMost = true;
                Size = new Size(920, 450);
                KeepOnTop();
                pinBtn.Text = L.T("Unpin");
                pinBtn.BackColor = Theme.Yellow; pinBtn.ForeColor = Color.Black;
            }
            else
            {
                TopMost = false;
                leftPanel.Visible = true;
                monitor.Visible = true;
                chipsBox.Visible = true;
                if (pinBtn.Parent != null) FitHeader(pinBtn.Parent);   // chips voltam so se couberem
                if (normalBounds.Width > 0) Bounds = normalBounds;
                pinBtn.Text = L.T("Pin on top");
                pinBtn.BackColor = Theme.Card; pinBtn.ForeColor = Theme.Text;
            }
            Log(pinned ? "Window pinned on top (compact mode). If it disappears behind FS25, set the game's display mode to borderless or windowed: exclusive fullscreen covers every window." : "Normal window.");
        }

        // Fixar veiculo: janela pequena por cima do jogo so com o desenho do veiculo e as barras de forca.
        private void ToggleVehPin()
        {
            if (pinned && !vehPinned) TogglePin();      // sai do modo compacto normal antes
            vehPinned = !vehPinned;
            bool v = vehPinned;
            if (v)
            {
                normalBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                WindowState = FormWindowState.Normal;
            }
            SuspendLayout();
            headerPanel.Visible = !v; accentPanel.Visible = !v; leftPanel.Visible = !v;
            live.Visible = !v; monitor.Visible = !v; logBox.Visible = !v; logTitleLbl.Visible = !v;
            rightPanel.AutoScroll = !v;
            tractor.Dock = v ? DockStyle.Fill : DockStyle.Top;
            if (!v) tractor.Height = 260;
            tractor.TopRightReserve = v ? 34 : 0;
            unpinBtn.Visible = v;
            unpinBtn.Location = new Point(tractor.ClientSize.Width - 30, 6);
            ResumeLayout(true);
            if (v)
            {
                MinimumSize = new Size(260, 240);
                TopMost = true;
                ClientSize = new Size(380, 330);
                KeepOnTop();
            }
            else
            {
                TopMost = false;
                MinimumSize = new Size(640, 400);
                if (normalBounds.Width > 0) Bounds = normalBounds;
            }
            pinVehBtn.Text = L.T("Pin vehicle");
            Log(v ? "Vehicle pinned on top. Click the x in the drawing's corner to go back." : "Normal window.");
        }

        // Ajuda do desenho: janela pequena com "x" para fechar; clicar outra vez no "?" (ou Esc, ou clicar
        // fora) tambem fecha.
        private void ToggleHelp(Control anchor, string text)
        {
            if (helpPop != null && !helpPop.IsDisposed) { helpPop.Close(); return; }
            if (unchecked(Environment.TickCount - helpClosedAt) < 250) return;   // o clique no "?" acabou de a fechar
            Form pop = new Form();
            pop.FormBorderStyle = FormBorderStyle.None; pop.ShowInTaskbar = false; pop.StartPosition = FormStartPosition.Manual;
            pop.BackColor = Theme.Line; pop.Padding = new Padding(1); pop.KeyPreview = true; pop.TopMost = TopMost;
            Panel inner = new Panel(); inner.Dock = DockStyle.Fill; inner.BackColor = Theme.Card; inner.Padding = new Padding(14, 10, 10, 12);
            Label title = new Label(); title.Text = L.T("How to read the drawing"); title.Font = Theme.Bold(10.5f);
            title.ForeColor = Theme.Yellow; title.AutoSize = true; title.Location = new Point(14, 12);
            Label close = new Label(); close.Text = "\u00D7"; close.Font = Theme.Bold(13f); close.ForeColor = Theme.Text;
            close.Size = new Size(26, 26); close.TextAlign = ContentAlignment.MiddleCenter; close.Cursor = Cursors.Hand; close.BackColor = Theme.Line;
            Label body = new Label(); body.Text = text; body.Font = Theme.UI(9f); body.ForeColor = Theme.Text;
            body.AutoSize = true; body.MaximumSize = new Size(380, 0); body.Location = new Point(14, 42);
            inner.Controls.Add(title); inner.Controls.Add(close); inner.Controls.Add(body);
            pop.Controls.Add(inner);
            Size need = body.GetPreferredSize(new Size(380, 0));
            pop.ClientSize = new Size(Math.Max(need.Width, 300) + 40, need.Height + 62);
            close.Location = new Point(pop.ClientSize.Width - 34, 8);
            Point at = anchor.PointToScreen(new Point(0, anchor.Height + 4));
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            at.X = Math.Max(wa.Left, Math.Min(at.X, wa.Right - pop.Width));
            at.Y = Math.Max(wa.Top, Math.Min(at.Y, wa.Bottom - pop.Height));
            pop.Location = at;
            close.Click += delegate { pop.Close(); };
            pop.KeyDown += delegate(object ks, KeyEventArgs ke) { if (ke.KeyCode == Keys.Escape) pop.Close(); };
            pop.Deactivate += delegate { pop.Close(); };
            pop.FormClosed += delegate { helpClosedAt = Environment.TickCount; helpPop = null; };
            helpPop = pop;
            pop.Show(this);
        }
    }
}
