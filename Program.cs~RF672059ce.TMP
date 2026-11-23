using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace Updater
{
    internal static class Program
    {
        private const string Base = "https://expressts.co.in/api/files/";

        private static string _ver = "";

        [STAThread]
        private static void Main(string[] args)
        {
            if (args != null && args.Length > 0) _ver = args[0];
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UpdateForm());
        }

        internal static void DoUpdate(UpdateForm f)
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            string exe = Path.Combine(dir, "Express.exe");
            string tmp = Path.Combine(dir, "Express.new");

            try
            {
                for (int i = 0; i < 60; i++)
                {
                    if (Process.GetProcessesByName("Express").Length == 0) break;
                    Thread.Sleep(500);
                }
                foreach (Process p in Process.GetProcessesByName("Express"))
                {
                    try { p.Kill(); } catch { }
                }
                Thread.Sleep(800);

                f.Status("Connecting to update server…");

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "ExpressUpdater");
                    wc.DownloadProgressChanged += (s, e) => f.Progress(e.ProgressPercentage, e.TotalBytesToReceive);
                    wc.DownloadFileAsync(new Uri(Base + "Express.exe"), tmp);

                    while (wc.IsBusy)
                    {
                        Application.DoEvents();
                        Thread.Sleep(20);
                    }
                }

                if (!File.Exists(tmp) || new FileInfo(tmp).Length < 100000)
                {
                    f.Status("Download fail — Express dobara chalao.");
                    Thread.Sleep(2500);
                    Close(f);
                    return;
                }

                f.Status("Installing update…");
                Application.DoEvents();
                Thread.Sleep(300);

                if (File.Exists(exe)) File.Copy(exe, exe + ".old", true);
                File.Copy(tmp, exe, true);
                File.Delete(tmp);
                if (!string.IsNullOrEmpty(_ver))
                {
                    try { File.WriteAllText(Path.Combine(dir, "ver.txt"), _ver); } catch { }
                }

                f.Progress(100, 1);
                f.Status("Starting Express…");
                Application.DoEvents();
                Thread.Sleep(700);

                Process.Start(exe);

                Thread.Sleep(600);
                Close(f);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                f.Status("Update fail: " + ex.Message);
                Thread.Sleep(2500);
                Close(f);
            }
        }

        private static void Close(UpdateForm f)
        {
            try { f.BeginInvoke(new Action(() => Application.Exit())); } catch { }
        }
    }

    internal class UpdateForm : Form
    {
        private readonly Label _status;
        private readonly Label _pct;
        private int _percent;
        private long _total;
        private readonly Timer _anim = new Timer();

        public UpdateForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(440, 176);
            BackColor = Color.FromArgb(8, 26, 52);
            ShowInTaskbar = true;
            TopMost = true;
            DoubleBuffered = true;

            _status = new Label
            {
                Text = "Preparing update…",
                ForeColor = Color.FromArgb(214, 236, 255),
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Bounds = new Rectangle(22, 92, 396, 22)
            };
            _pct = new Label
            {
                Text = "0%",
                ForeColor = Color.FromArgb(255, 226, 148),
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Bounds = new Rectangle(330, 92, 88, 22)
            };

            Controls.Add(_status);
            Controls.Add(_pct);

            _anim.Interval = 33;
            _anim.Tick += (s, e) => Invalidate();
            _anim.Start();

            Shown += (s, e) =>
            {
                ThreadPool.QueueUserWorkItem(_ => Program.DoUpdate(this));
            };
        }

        public void Status(string t)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Status), t); return; }
            _status.Text = t;
        }

        public void Progress(int pct, long total)
        {
            if (InvokeRequired) { BeginInvoke(new Action<int, long>(Progress), pct, total); return; }
            _percent = Math.Max(0, Math.Min(100, pct));
            _total = total;
            _pct.Text = _percent + "%";
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (LinearGradientBrush bg = new LinearGradientBrush(
                ClientRectangle, Color.FromArgb(8, 26, 52), Color.FromArgb(16, 52, 96), 90f))
                g.FillRectangle(bg, ClientRectangle);

            using (Pen edge = new Pen(Color.FromArgb(198, 244, 50), 1.6f))
                g.DrawRectangle(edge, 1, 1, ClientSize.Width - 3, ClientSize.Height - 3);

            using (Font f = new Font("Segoe UI", 17f, FontStyle.Bold))
            using (SolidBrush w = new SolidBrush(Color.White))
            {
                g.DrawString("EXPRESS", f, w, 22, 18);
                SizeF sz = g.MeasureString("EXPRESS", f);
                using (SolidBrush lime = new SolidBrush(Color.FromArgb(198, 244, 50)))
                    g.DrawString("  UPDATE", f, lime, 22 + sz.Width - 6, 18);
            }

            using (Font f = new Font("Segoe UI Semibold", 8.6f, FontStyle.Bold))
            using (SolidBrush m = new SolidBrush(Color.FromArgb(178, 206, 236)))
                g.DrawString("PREMIUM TATKAL SOFTWARE  •  AUTO UPDATE", f, m, 24, 52);

            Rectangle track = new Rectangle(22, 74, 396, 9);
            using (GraphicsPath tp = Round(track, 5))
            {
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(46, 74, 112))) g.FillPath(tb, tp);
                if (_percent > 0)
                {
                    Rectangle fill = new Rectangle(track.X, track.Y, Math.Max(6, track.Width * _percent / 100), track.Height);
                    using (GraphicsPath fp = Round(fill, 5))
                    using (LinearGradientBrush fb = new LinearGradientBrush(fill,
                        Color.FromArgb(255, 226, 148), Color.FromArgb(245, 146, 28), 0f))
                        g.FillPath(fb, fp);
                }
            }

            if (_percent == 0)
            {
                int x = 22 + (int)((DateTime.Now.Millisecond / 1000f) * 380);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(198, 244, 50)))
                    g.FillEllipse(b, x, 76, 6, 6);
            }

            if (_percent > 0 && _percent < 100 && _total > 0)
                using (Font f = new Font("Segoe UI", 7.6f))
                using (SolidBrush m = new SolidBrush(Color.FromArgb(150, 180, 212)))
                    g.DrawString((_total / 1024 / 1024) + " MB", f, m, 24, 122);

            using (Font f = new Font("Segoe UI", 7.8f))
            using (SolidBrush m = new SolidBrush(Color.FromArgb(150, 180, 212)))
                g.DrawString("Update ke dauran Express band rahega, khud restart ho jayega.", f, m, 24, 146);
        }

        private static GraphicsPath Round(Rectangle r, int rad)
        {
            GraphicsPath p = new GraphicsPath();
            int d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}