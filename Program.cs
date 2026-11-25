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

        private static string _ver ="";

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
        // ===== Express theme (visual only). Program.DoUpdate drives Status()/Progress() exactly as before. =====
        private static readonly Color C_BG = Color.FromArgb(236, 242, 251);
        private static readonly Color C_HEAD_L = Color.FromArgb(214, 96, 4);
        private static readonly Color C_HEAD_R = Color.FromArgb(248, 158, 44);
        private static readonly Color C_NAVY = Color.FromArgb(15, 42, 92);
        private static readonly Color C_ORANGE = Color.FromArgb(245, 146, 28);
        private static readonly Color C_BLUE = Color.FromArgb(37, 99, 235);
        private static readonly Color C_TEXT = Color.FromArgb(30, 41, 59);
        private static readonly Color C_MUTED = Color.FromArgb(100, 116, 139);
        private static readonly Color C_LINE = Color.FromArgb(220, 228, 241);
        private static readonly Color C_GREEN = Color.FromArgb(22, 163, 74);
        private static readonly Color C_RED = Color.FromArgb(220, 38, 38);

        private const int W = 500, H = 280, HEAD = 66;
        private static readonly Rectangle CARD = new Rectangle(16, HEAD + 14, W - 32, 158);
        private static readonly Rectangle BAR = new Rectangle(32, HEAD + 82, W - 64, 10);

        private readonly Font _fTitle = new Font("Segoe UI Semibold", 14f);
        private readonly Font _fSub = new Font("Segoe UI", 8.5f);
        private readonly Font _fStatus = new Font("Segoe UI Semibold", 11f);
        private readonly Font _fCaption = new Font("Segoe UI", 8.5f);
        private readonly Font _fPct = new Font("Segoe UI Semibold", 22f);
        private readonly Font _fStep = new Font("Segoe UI Semibold", 8f);
        private readonly Font _fNote = new Font("Segoe UI", 8.5f);
        private readonly Font _fBadge = new Font("Segoe UI Black", 15f);

        // The two labels still hold the live text (code sets them); they are drawn by OnPaint.
        private readonly Label _status;
        private readonly Label _pct;
        private int _percent;
        private long _total;
        private float _shown;                       // eased percentage for smooth bar movement
        private readonly Timer _anim = new Timer();
        private readonly int _t0 = Environment.TickCount;

        public UpdateForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(W, H);
            BackColor = C_BG;
            ShowInTaskbar = true;
            TopMost = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Text = "Express Update";

            _status = new Label { Text = "Preparing update…", Visible = false };
            _pct = new Label { Text = "0%", Visible = false };
            Controls.Add(_status);
            Controls.Add(_pct);

            using (GraphicsPath rp = Round(new Rectangle(0, 0, W, H), 14))
                Region = new Region(rp);

            _anim.Interval = 16;
            _anim.Tick += (s, e) =>
            {
                _shown += (_percent - _shown) * 0.12f;
                if (Math.Abs(_percent - _shown) < 0.05f) _shown = _percent;
                Invalidate();
            };
            _anim.Start();
            FormClosed += (s, e) => { _anim.Stop(); _anim.Dispose(); };

            Shown += (s, e) =>
            {
                ThreadPool.QueueUserWorkItem(_ => Program.DoUpdate(this));
            };
        }

        public void Status(string t)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Status), t); return; }
            _status.Text = t;
            Invalidate();
        }

        public void Progress(int pct, long total)
        {
            if (InvokeRequired) { BeginInvoke(new Action<int, long>(Progress), pct, total); return; }
            _percent = Math.Max(0, Math.Min(100, pct));
            _total = total;
            _pct.Text = _percent + "%";
            Invalidate();
        }

        // ---- stage is read from the status text the update code already sets ----
        private bool Failed { get { return (_status.Text ?? "").IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0; } }

        private int Stage
        {
            get
            {
                string s = _status.Text ?? "";
                if (s.StartsWith("Starting", StringComparison.OrdinalIgnoreCase)) return 2;
                if (s.StartsWith("Installing", StringComparison.OrdinalIgnoreCase)) return 1;
                return 0;
            }
        }

        private float Secs { get { return (Environment.TickCount - _t0) / 1000f; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(C_BG);

            PaintHeader(g);
            PaintCard(g);

            // footer note
            Rectangle ni = new Rectangle(22, H - 27, 14, 14);
            DrawIco(g, "info", ni, C_BLUE, 1.6f);
            TextRenderer.DrawText(g, "Update ke dauran Express band rahega, khud restart ho jayega.", _fNote,
                new Rectangle(ni.Right + 8, H - 30, W - ni.Right - 30, 20), C_MUTED,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            // window border (inside the rounded region)
            using (GraphicsPath bp = Round(new Rectangle(0, 0, W - 1, H - 1), 14))
            using (Pen p = new Pen(Color.FromArgb(178, 196, 226)))
                g.DrawPath(p, bp);
        }

        private void PaintHeader(Graphics g)
        {
            Rectangle r = new Rectangle(0, 0, W, HEAD);
            using (LinearGradientBrush b = new LinearGradientBrush(r, C_HEAD_L, C_HEAD_R, LinearGradientMode.Horizontal))
                g.FillRectangle(b, r);
            using (SolidBrush shine = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
                g.FillRectangle(shine, 0, 0, W, (HEAD - 3) / 2);
            // soft moving sheen across the ribbon
            float sx = (Secs * 120f) % (W + 240) - 120;
            using (LinearGradientBrush sheen = new LinearGradientBrush(new RectangleF(sx, 0, 120, HEAD),
                Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
            {
                ColorBlend cb = new ColorBlend();
                cb.Positions = new[] { 0f, 0.5f, 1f };
                cb.Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(38, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
                sheen.InterpolationColors = cb;
                g.FillRectangle(sheen, sx, 0, 120, HEAD - 3);
            }
            using (SolidBrush navy = new SolidBrush(C_NAVY))
                g.FillRectangle(navy, 0, HEAD - 3, W, 3);

            // "E" logo badge
            Rectangle badge = new Rectangle(18, (HEAD - 3 - 42) / 2, 42, 42);
            Fill(g, new RectangleF(badge.X, badge.Y + 2, badge.Width, badge.Height), 11, Color.FromArgb(60, 120, 40, 0), Color.Empty);
            Fill(g, badge, 11, Color.White, Color.Empty);
            Rectangle inner = Rectangle.Inflate(badge, -5, -5);
            using (GraphicsPath ip = Round(inner, 8))
            using (LinearGradientBrush ib = new LinearGradientBrush(inner, Color.FromArgb(132, 204, 22), Color.FromArgb(77, 160, 30), 90f))
                g.FillPath(ib, ip);
            TextRenderer.DrawText(g, "E", _fBadge, inner, C_NAVY,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            int tx = badge.Right + 14;
            TextRenderer.DrawText(g, "Express Update", _fTitle, new Point(tx, badge.Y - 1), Color.White, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "Premium Tatkal Software  •  Auto update", _fSub, new Point(tx + 1, badge.Y + 26),
                Color.FromArgb(255, 237, 213), TextFormatFlags.NoPadding);

            // secure pill on the right
            string pill = "SECURE";
            Size ps = TextRenderer.MeasureText(g, pill, _fStep, Size.Empty, TextFormatFlags.NoPadding);
            Rectangle pr = new Rectangle(W - 22 - ps.Width - 30, (HEAD - 3 - 24) / 2, ps.Width + 30, 24);
            using (GraphicsPath pp = Round(pr, 12))
            {
                using (SolidBrush pb = new SolidBrush(Color.FromArgb(46, 255, 255, 255))) g.FillPath(pb, pp);
                using (Pen pen = new Pen(Color.FromArgb(90, 255, 255, 255))) g.DrawPath(pen, pp);
            }
            DrawIco(g, "shield", new Rectangle(pr.X + 9, pr.Y + 5, 13, 14), Color.White, 1.6f);
            TextRenderer.DrawText(g, pill, _fStep, new Rectangle(pr.X + 25, pr.Y, pr.Width - 27, pr.Height), Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private void PaintCard(Graphics g)
        {
            bool failed = Failed;
            int stage = Stage;
            Color accent = failed ? C_RED : C_ORANGE;

            Fill(g, new RectangleF(CARD.X, CARD.Y + 2, CARD.Width - 1, CARD.Height - 1), 12, Color.FromArgb(214, 225, 243), Color.Empty);
            Fill(g, new RectangleF(CARD.X, CARD.Y, CARD.Width - 1, CARD.Height - 1), 12, Color.White, C_LINE);
            Fill(g, new RectangleF(CARD.X + 1, CARD.Y + 14, 3, 36), 1.5f, accent, Color.Empty);

            // stage icon in a tinted circle (animated)
            Rectangle ic = new Rectangle(32, CARD.Y + 14, 46, 46);
            Color tint = failed ? Color.FromArgb(254, 226, 226) : Color.FromArgb(255, 237, 213);
            float pulse = (float)(Math.Sin(Secs * 3.2) * 0.5 + 0.5);
            if (!failed)
                using (SolidBrush halo = new SolidBrush(Color.FromArgb((int)(40 + 50 * pulse), accent)))
                    g.FillEllipse(halo, RectangleF.Inflate(ic, 3 + 2 * pulse, 3 + 2 * pulse));
            using (SolidBrush tb = new SolidBrush(tint)) g.FillEllipse(tb, ic);
            Rectangle gi = Rectangle.Inflate(ic, -12, -12);
            string kind = failed ? "alert" : stage == 2 ? "rocket" : stage == 1 ? "package" : "download";
            DrawIco(g, kind, gi, accent, 2f);

            // status text + caption
            int sx = ic.Right + 14;
            Rectangle pctR = new Rectangle(CARD.Right - 16 - 100, CARD.Y + 12, 100, 40);
            if (failed)
            {
                // error: show the whole message (up to 3 lines) instead of a caption
                TextRenderer.DrawText(g, _status.Text, _fCaption, new Rectangle(sx, CARD.Y + 12, pctR.X - sx - 6, 50), C_RED,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, _pct.Text, _fPct, pctR, C_RED,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                PaintBar(g, true);
                PaintSteps(g, stage, true);
                return;
            }
            TextRenderer.DrawText(g, _status.Text, _fStatus, new Rectangle(sx, CARD.Y + 14, pctR.X - sx - 6, 24),
                C_TEXT,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            string cap;
            if (failed) cap = "Kuch galat ho gaya — Express apne aap band hoga";
            else if (stage == 2) cap = "Update complete — Express khul raha hai";
            else if (stage == 1) cap = "Nayi files install ho rahi hain";
            else if (_percent > 0 && _total > 0)
                cap = string.Format("Downloading  •  {0:0.0} MB of {1:0.0} MB", _total * _percent / 100.0 / 1048576.0, _total / 1048576.0);
            else cap = "Server se connect ho raha hai";
            TextRenderer.DrawText(g, cap, _fCaption, new Rectangle(sx, CARD.Y + 38, pctR.X - sx - 6, 20), C_MUTED,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            // big percentage
            TextRenderer.DrawText(g, _pct.Text, _fPct, pctR, failed ? C_RED : C_HEAD_L,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            PaintBar(g, failed);
            PaintSteps(g, stage, failed);
        }

        private void PaintBar(Graphics g, bool failed)
        {
            Rectangle t = BAR;
            using (GraphicsPath tp = Round(t, t.Height / 2))
            {
                using (SolidBrush tb = new SolidBrush(Color.FromArgb(226, 232, 240))) g.FillPath(tb, tp);

                Region old = g.Clip;
                g.SetClip(tp, CombineMode.Intersect);
                if (_shown < 0.5f && !failed)
                {
                    // indeterminate: a soft bhagwa comet sliding back and forth
                    float span = t.Width * 0.32f;
                    float ph = (float)((Math.Sin(Secs * 2.4) + 1) / 2);
                    float x = t.X - span + (t.Width + span) * ph;
                    RectangleF cr = new RectangleF(x, t.Y, span, t.Height);
                    using (LinearGradientBrush cb = new LinearGradientBrush(cr, C_HEAD_R, C_HEAD_L, 0f))
                    {
                        ColorBlend bl = new ColorBlend();
                        bl.Positions = new[] { 0f, 0.5f, 1f };
                        bl.Colors = new[] { Color.FromArgb(0, C_HEAD_R), C_ORANGE, Color.FromArgb(0, C_HEAD_L) };
                        cb.InterpolationColors = bl;
                        g.FillRectangle(cb, cr);
                    }
                }
                else
                {
                    float pct = failed ? Math.Max(_shown, 100f) : _shown;
                    float fw = Math.Max(t.Height, t.Width * pct / 100f);
                    RectangleF fr = new RectangleF(t.X, t.Y, fw, t.Height);
                    using (GraphicsPath fp = Round(Rectangle.Round(fr), t.Height / 2))
                    {
                        Color a = failed ? Color.FromArgb(248, 113, 113) : C_HEAD_R;
                        Color b = failed ? C_RED : C_HEAD_L;
                        using (LinearGradientBrush fb = new LinearGradientBrush(new RectangleF(t.X, t.Y, t.Width, t.Height), a, b, 0f))
                            g.FillPath(fb, fp);
                        if (!failed && pct < 100f)
                        {
                            // moving shimmer stripe inside the filled part
                            Region o2 = g.Clip;
                            g.SetClip(fp, CombineMode.Intersect);
                            float sw = 60f, sx = t.X - sw + ((Secs * 160f) % (fw + sw));
                            using (LinearGradientBrush sh = new LinearGradientBrush(new RectangleF(sx, t.Y, sw, t.Height),
                                Color.Transparent, Color.Transparent, 0f))
                            {
                                ColorBlend bl = new ColorBlend();
                                bl.Positions = new[] { 0f, 0.5f, 1f };
                                bl.Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) };
                                sh.InterpolationColors = bl;
                                g.FillRectangle(sh, sx, t.Y, sw, t.Height);
                            }
                            g.Clip = o2;
                        }
                    }
                }
                g.Clip = old;
            }
        }

        private void PaintSteps(Graphics g, int stage, bool failed)
        {
            string[] names = { "Download", "Install", "Restart" };
            int y = BAR.Bottom + 18, d = 20;
            int x0 = BAR.X + 40, x1 = BAR.Right - 40;
            int[] cx = { x0, (x0 + x1) / 2, x1 };

            for (int i = 0; i < 2; i++)
            {
                bool done = !failed && stage > i;
                using (Pen p = new Pen(done ? C_GREEN : C_LINE, 2f))
                {
                    if (!done) p.DashStyle = DashStyle.Dash;
                    g.DrawLine(p, cx[i] + d / 2 + 6, y + d / 2, cx[i + 1] - d / 2 - 6, y + d / 2);
                }
            }

            for (int i = 0; i < 3; i++)
            {
                Rectangle c = new Rectangle(cx[i] - d / 2, y, d, d);
                bool done = !failed && stage > i;
                bool active = stage == i;
                Color label;
                if (active && failed)
                {
                    using (SolidBrush b = new SolidBrush(C_RED)) g.FillEllipse(b, c);
                    DrawIco(g, "x", Rectangle.Inflate(c, -6, -6), Color.White, 2f);
                    label = C_RED;
                }
                else if (done)
                {
                    using (SolidBrush b = new SolidBrush(C_GREEN)) g.FillEllipse(b, c);
                    DrawIco(g, "check", Rectangle.Inflate(c, -5, -5), Color.White, 2f);
                    label = C_GREEN;
                }
                else if (active)
                {
                    float pulse = (float)(Math.Sin(Secs * 4) * 0.5 + 0.5);
                    using (SolidBrush halo = new SolidBrush(Color.FromArgb((int)(50 + 70 * pulse), C_ORANGE)))
                        g.FillEllipse(halo, RectangleF.Inflate(c, 3 + 2 * pulse, 3 + 2 * pulse));
                    using (SolidBrush b = new SolidBrush(C_ORANGE)) g.FillEllipse(b, c);
                    using (SolidBrush w = new SolidBrush(Color.White)) g.FillEllipse(w, Rectangle.Inflate(c, -6, -6));
                    label = C_HEAD_L;
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, c);
                    using (Pen p = new Pen(C_LINE, 2f)) g.DrawEllipse(p, c);
                    label = C_MUTED;
                }
                TextRenderer.DrawText(g, names[i], _fStep, new Rectangle(c.X - 40, c.Bottom + 3, d + 80, 16), label,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }
        }

        // ---- Lucide-style line icons (24-grid scaled into r) ----
        private void DrawIco(Graphics g, string kind, Rectangle r, Color c, float stroke)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(r.Width, r.Height) / 24f;
            Func<float, float, PointF> P = (x, y) => new PointF(r.X + x * s, r.Y + y * s);
            using (Pen p = new Pen(c, stroke))
            using (SolidBrush b = new SolidBrush(c))
            {
                p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
                switch (kind)
                {
                    case "download":
                    {
                        float bob = (float)(Math.Sin(Secs * 5) * 1.5);
                        g.DrawLines(p, new[] { P(3, 15), P(3, 19), P(5, 21), P(19, 21), P(21, 19), P(21, 15) });
                        g.DrawLine(p, P(12, 3 + bob), P(12, 15 + bob));
                        g.DrawLines(p, new[] { P(7, 10 + bob), P(12, 15 + bob), P(17, 10 + bob) });
                        break;
                    }
                    case "package":
                        g.DrawPolygon(p, new[] { P(12, 2), P(21, 7), P(21, 17), P(12, 22), P(3, 17), P(3, 7) });
                        g.DrawLines(p, new[] { P(3, 7), P(12, 12), P(21, 7) });
                        g.DrawLine(p, P(12, 12), P(12, 22));
                        g.DrawLine(p, P(7.5f, 4.5f), P(16.5f, 9.5f));
                        break;
                    case "rocket":
                    {
                        float fl = (float)(Math.Sin(Secs * 9) * 0.8);
                        g.DrawBezier(p, P(12, 15), P(9, 10), P(12, 4), P(20, 4));
                        g.DrawBezier(p, P(20, 4), P(20, 12), P(14, 15), P(9, 12));
                        g.DrawLines(p, new[] { P(9, 12), P(5, 12), P(7, 8), P(11, 8) });
                        g.DrawLines(p, new[] { P(12, 15), P(12, 19), P(16, 17), P(16, 13) });
                        g.DrawLine(p, P(7, 17 + fl), P(4, 20 + fl));
                        break;
                    }
                    case "alert":
                        g.DrawPolygon(p, new[] { P(12, 3), P(22, 20), P(2, 20) });
                        g.DrawLine(p, P(12, 9), P(12, 13));
                        g.FillEllipse(b, r.X + 11 * s, r.Y + 16 * s, 2 * s, 2 * s);
                        break;
                    case "info":
                        g.DrawEllipse(p, r.X + 1 * s, r.Y + 1 * s, 22 * s, 22 * s);
                        g.DrawLine(p, P(12, 11), P(12, 17));
                        g.FillEllipse(b, r.X + 10.8f * s, r.Y + 6.3f * s, 2.4f * s, 2.4f * s);
                        break;
                    case "shield":
                        g.DrawBezier(p, P(12, 22), P(4, 18), P(4, 13), P(4, 5));
                        g.DrawLine(p, P(4, 5), P(12, 2));
                        g.DrawLine(p, P(12, 2), P(20, 5));
                        g.DrawBezier(p, P(20, 5), P(20, 13), P(20, 18), P(12, 22));
                        g.DrawLines(p, new[] { P(9, 12), P(11, 14), P(15, 10) });
                        break;
                    case "check":
                        g.DrawLines(p, new[] { P(4, 12), P(10, 18), P(20, 6) });
                        break;
                    case "x":
                        g.DrawLine(p, P(5, 5), P(19, 19));
                        g.DrawLine(p, P(19, 5), P(5, 19));
                        break;
                }
            }
            g.SmoothingMode = old;
        }

        private static void Fill(Graphics g, RectangleF r, float rad, Color fill, Color border)
        {
            using (GraphicsPath p = Round(Rectangle.Round(r), (int)Math.Round(rad)))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (border.A > 0) using (Pen pen = new Pen(border)) g.DrawPath(pen, p);
            }
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
