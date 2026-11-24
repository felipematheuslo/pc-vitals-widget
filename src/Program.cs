using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;

[assembly: AssemblyTitle("PC Vitals Widget")]
[assembly: AssemblyProduct("pc-vitals-widget")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace PcVitalsWidget
{
    internal static class Program
    {
        static Program()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnResolveAssembly;
        }

        private static Assembly OnResolveAssembly(object sender, ResolveEventArgs args)
        {
            string shortName = new AssemblyName(args.Name).Name + ".dll";
            Assembly executing = Assembly.GetExecutingAssembly();
            foreach (string resName in executing.GetManifestResourceNames())
            {
                if (resName.EndsWith(shortName, StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream stream = executing.GetManifestResourceStream(resName))
                    {
                        if (stream == null) return null;
                        byte[] raw = new byte[stream.Length];
                        stream.Read(raw, 0, raw.Length);
                        return Assembly.Load(raw);
                    }
                }
            }
            return null;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            return Run(args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Run(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (VitalsWidgetForm form = new VitalsWidgetForm(args))
            {
                Application.Run(form);
            }
            return 0;
        }
    }

    internal class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware)
            {
                subHardware.Accept(this);
            }
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }

    internal class VitalsSnapshot
    {
        public float? CpuTemp;
        public float? CpuLoad;
        public float? GpuTemp;
        public float? GpuLoad;
        public float? VramUsedGb;
        public float? VramTotalGb;
        public float? VramPct;
        public float? RamUsedGb;
        public float? RamTotalGb;
        public float? RamPct;
    }

    internal class WidgetConfig
    {
        public string Corner = "top-right";
        public int? X = null;
        public int? Y = null;
        public double Alpha = 0.92;
        public bool Compact = false;
        public bool Topmost = true;

        private static string ConfigPath
        {
            get
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(dir, "config.json");
            }
        }

        public static WidgetConfig Load()
        {
            WidgetConfig cfg = new WidgetConfig();
            try
            {
                if (!File.Exists(ConfigPath)) return cfg;
                string txt = File.ReadAllText(ConfigPath);

                Match mCorner = Regex.Match(txt, "\"corner\"\\s*:\\s*(null|\"([^\"]*)\")");
                if (mCorner.Success)
                {
                    cfg.Corner = mCorner.Groups[1].Value == "null" ? null : mCorner.Groups[2].Value;
                }

                Match mX = Regex.Match(txt, "\"x\"\\s*:\\s*(-?\\d+)");
                if (mX.Success) cfg.X = int.Parse(mX.Groups[1].Value, CultureInfo.InvariantCulture);

                Match mY = Regex.Match(txt, "\"y\"\\s*:\\s*(-?\\d+)");
                if (mY.Success) cfg.Y = int.Parse(mY.Groups[1].Value, CultureInfo.InvariantCulture);

                Match mAlpha = Regex.Match(txt, "\"alpha\"\\s*:\\s*([0-9.]+)");
                if (mAlpha.Success)
                {
                    double a;
                    if (double.TryParse(mAlpha.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out a))
                        cfg.Alpha = Math.Max(0.3, Math.Min(1.0, a));
                }

                Match mCompact = Regex.Match(txt, "\"compact\"\\s*:\\s*(true|false)");
                if (mCompact.Success) cfg.Compact = mCompact.Groups[1].Value == "true";

                Match mTop = Regex.Match(txt, "\"topmost\"\\s*:\\s*(true|false)");
                if (mTop.Success) cfg.Topmost = mTop.Groups[1].Value == "true";
            }
            catch { }
            return cfg;
        }

        public void Save()
        {
            try
            {
                string cornerJson = Corner == null ? "null" : "\"" + Corner + "\"";
                string xJson = X.HasValue ? X.Value.ToString(CultureInfo.InvariantCulture) : "null";
                string yJson = Y.HasValue ? Y.Value.ToString(CultureInfo.InvariantCulture) : "null";
                string alphaJson = Alpha.ToString("0.00", CultureInfo.InvariantCulture);
                string compactJson = Compact ? "true" : "false";
                string topJson = Topmost ? "true" : "false";

                string json = string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\r\n  \"corner\": {0},\r\n  \"x\": {1},\r\n  \"y\": {2},\r\n  \"alpha\": {3},\r\n  \"compact\": {4},\r\n  \"topmost\": {5}\r\n}}\r\n",
                    cornerJson, xJson, yJson, alphaJson, compactJson, topJson
                );
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }

    internal class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(24, 27, 34); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(24, 27, 34); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(24, 27, 34); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(24, 27, 34); } }
        public override Color MenuBorder { get { return Color.FromArgb(43, 50, 68); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(65, 76, 102); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(45, 53, 72); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(45, 53, 72); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(45, 53, 72); } }
        public override Color SeparatorDark { get { return Color.FromArgb(43, 50, 68); } }
        public override Color SeparatorLight { get { return Color.FromArgb(43, 50, 68); } }
        public override Color CheckBackground { get { return Color.FromArgb(45, 53, 72); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(60, 70, 96); } }
    }

    internal sealed class VitalsWidgetForm : Form
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        private static readonly Color TransparentKey = Color.FromArgb(1, 1, 1);
        private static readonly Color BgCard = Color.FromArgb(13, 15, 20);
        private static readonly Color BorderCard = Color.FromArgb(38, 44, 58);
        private static readonly Color BgSection = Color.FromArgb(23, 27, 36);
        private static readonly Color BorderSection = Color.FromArgb(43, 50, 68);
        private static readonly Color TrackBg = Color.FromArgb(35, 40, 54);
        private static readonly Color TextMuted = Color.FromArgb(123, 132, 152);
        private static readonly Color TextSub = Color.FromArgb(203, 213, 225);
        private static readonly Color AccentCpu = Color.FromArgb(96, 165, 250);
        private static readonly Color AccentGpu = Color.FromArgb(192, 132, 252);
        private static readonly Color AccentRam = Color.FromArgb(45, 212, 191);

        private readonly WidgetConfig _cfg;
        private readonly Font _fontHeader = new Font("Segoe UI", 8f, FontStyle.Bold);
        private readonly Font _fontLabel = new Font("Segoe UI", 9f, FontStyle.Bold);
        private readonly Font _fontSub = new Font("Segoe UI", 8f, FontStyle.Regular);
        private readonly Font _fontVal = new Font("Consolas", 10.5f, FontStyle.Bold);
        private readonly Font _fontTempCompact = new Font("Consolas", 12f, FontStyle.Bold);
        private readonly Font _fontIcon = new Font("Segoe UI", 9f, FontStyle.Bold);

        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _compactItem;
        private readonly ToolStripMenuItem _topmostItem;

        private readonly object _lock = new object();
        private VitalsSnapshot _data = new VitalsSnapshot();
        private volatile bool _running = true;
        private Thread _sensorThread;

        private bool _dragging;
        private Point _dragStart;
        private bool _closeHover;
        private bool _menuHover;
        private bool _menuOpen;
        private readonly System.Windows.Forms.Timer _uiTimer;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // WS_EX_TOOLWINDOW (0x80) hides the overlay from Alt+Tab and taskbar
                cp.ExStyle |= 0x80;
                return cp;
            }
        }

        public VitalsWidgetForm(string[] args)
        {
            _cfg = WidgetConfig.Load();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = TransparentKey;
            TransparencyKey = TransparentKey;
            TopMost = _cfg.Topmost;
            Opacity = _cfg.Alpha;
            Text = "PC Vitals Widget";

            _menu = new ContextMenuStrip
            {
                Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable()),
                ForeColor = Color.FromArgb(226, 232, 240),
                ShowImageMargin = true
            };
            _menu.Opening += (s, e) => { _menuOpen = true; };
            _menu.Closed += (s, e) => { _menuOpen = false; };

            ToolStripMenuItem cornerMenu = CreateSubMenu("Screen Corner");
            cornerMenu.DropDownItems.Add(CreateMenuItem("Top Right", () => SetCorner("top-right")));
            cornerMenu.DropDownItems.Add(CreateMenuItem("Top Left", () => SetCorner("top-left")));
            cornerMenu.DropDownItems.Add(CreateMenuItem("Bottom Right", () => SetCorner("bottom-right")));
            cornerMenu.DropDownItems.Add(CreateMenuItem("Bottom Left", () => SetCorner("bottom-left")));
            _menu.Items.Add(cornerMenu);

            ToolStripMenuItem opacityMenu = CreateSubMenu("Opacity");
            foreach (int pct in new[] { 100, 92, 80, 65 })
            {
                int p = pct;
                opacityMenu.DropDownItems.Add(CreateMenuItem(p + "%", () => SetAlpha(p / 100.0)));
            }
            _menu.Items.Add(opacityMenu);

            _menu.Items.Add(new ToolStripSeparator());

            _compactItem = new ToolStripMenuItem("Compact Mode")
            {
                CheckOnClick = true,
                Checked = _cfg.Compact,
                ForeColor = Color.FromArgb(226, 232, 240)
            };
            _compactItem.Click += (s, e) => ToggleCompact();
            _menu.Items.Add(_compactItem);

            _topmostItem = new ToolStripMenuItem("Always on Top")
            {
                CheckOnClick = true,
                Checked = _cfg.Topmost,
                ForeColor = Color.FromArgb(226, 232, 240)
            };
            _topmostItem.Click += (s, e) => ToggleTopmost();
            _menu.Items.Add(_topmostItem);

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(CreateMenuItem("Close", Close));

            ApplySizeAndPosition(true);

            _sensorThread = new Thread(SensorLoop) { IsBackground = true };
            _sensorThread.Start();

            _uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _uiTimer.Tick += (s, e) =>
            {
                Invalidate();
                if (_cfg.Topmost && !_menuOpen && !TopMost)
                    TopMost = true;
            };
            _uiTimer.Start();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--smoke-test")
                {
                    System.Windows.Forms.Timer exitTimer = new System.Windows.Forms.Timer { Interval = 3500 };
                    exitTimer.Tick += (s, e) =>
                    {
                        exitTimer.Stop();
                        Close();
                    };
                    exitTimer.Start();
                }
            }
        }

        private ToolStripMenuItem CreateSubMenu(string title)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(title)
            {
                ForeColor = Color.FromArgb(226, 232, 240)
            };
            item.DropDown.Renderer = _menu.Renderer;
            return item;
        }

        private static ToolStripMenuItem CreateMenuItem(string title, Action onClick)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(title)
            {
                ForeColor = Color.FromArgb(226, 232, 240)
            };
            item.Click += (s, e) => onClick();
            return item;
        }

        private Size GetTargetSize()
        {
            return _cfg.Compact ? new Size(174, 118) : new Size(196, 264);
        }

        private void ApplySizeAndPosition(bool initial)
        {
            Size sz = GetTargetSize();
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            int margin = 14;
            int x, y;

            if (initial && _cfg.X.HasValue && _cfg.Y.HasValue)
            {
                x = Math.Max(0, Math.Min(_cfg.X.Value, screen.Width - sz.Width));
                y = Math.Max(0, Math.Min(_cfg.Y.Value, screen.Height - sz.Height));
            }
            else if (!initial && _cfg.Corner == null && _cfg.X.HasValue)
            {
                x = Math.Max(0, Math.Min(Left, screen.Width - sz.Width));
                y = Math.Max(0, Math.Min(Top, screen.Height - sz.Height));
            }
            else
            {
                string corner = _cfg.Corner ?? "top-right";
                if (corner == "top-left")
                {
                    x = work.Left + margin;
                    y = work.Top + margin;
                }
                else if (corner == "bottom-left")
                {
                    x = work.Left + margin;
                    y = work.Bottom - sz.Height - margin;
                }
                else if (corner == "bottom-right")
                {
                    x = work.Right - sz.Width - margin;
                    y = work.Bottom - sz.Height - margin;
                }
                else
                {
                    x = work.Right - sz.Width - margin;
                    y = work.Top + margin;
                }
            }

            SetBounds(x, y, sz.Width, sz.Height);
            Invalidate();
        }

        private void SetCorner(string corner)
        {
            _cfg.Corner = corner;
            _cfg.X = null;
            _cfg.Y = null;
            _cfg.Save();
            ApplySizeAndPosition(false);
        }

        private void SetAlpha(double alpha)
        {
            _cfg.Alpha = alpha;
            Opacity = alpha;
            _cfg.Save();
        }

        private void ToggleCompact()
        {
            _cfg.Compact = _compactItem.Checked;
            _cfg.Save();
            ApplySizeAndPosition(false);
        }

        private void ToggleTopmost()
        {
            _cfg.Topmost = _topmostItem.Checked;
            TopMost = _cfg.Topmost;
            _cfg.Save();
        }

        private bool HitClose(int x, int y)
        {
            return x >= Width - 24 && x <= Width - 4 && y >= 4 && y <= 24;
        }

        private bool HitMenu(int x, int y)
        {
            return !_cfg.Compact && x >= Width - 42 && x <= Width - 24 && y >= 4 && y <= 24;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                Point p = PointToScreen(e.Location);
                Location = new Point(p.X - _dragStart.X, p.Y - _dragStart.Y);
                return;
            }

            bool ch = HitClose(e.X, e.Y);
            bool mh = HitMenu(e.X, e.Y);
            Cursor = (ch || mh) ? Cursors.Hand : Cursors.SizeAll;
            if (ch != _closeHover || mh != _menuHover)
            {
                _closeHover = ch;
                _menuHover = mh;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_closeHover || _menuHover)
            {
                _closeHover = false;
                _menuHover = false;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                _menu.Show(this, e.Location);
                return;
            }
            if (e.Button == MouseButtons.Left)
            {
                if (HitClose(e.X, e.Y))
                {
                    Close();
                    return;
                }
                if (HitMenu(e.X, e.Y))
                {
                    _menu.Show(this, e.Location);
                    return;
                }
                _dragging = true;
                _dragStart = e.Location;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging)
            {
                _dragging = false;
                _cfg.Corner = null;
                _cfg.X = Left;
                _cfg.Y = Top;
                _cfg.Save();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left && !HitClose(e.X, e.Y) && !HitMenu(e.X, e.Y))
            {
                _compactItem.Checked = !_compactItem.Checked;
                ToggleCompact();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
                Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _running = false;
            _uiTimer.Stop();
            base.OnFormClosing(e);
        }

        private static Color TempColor(float? temp)
        {
            if (!temp.HasValue) return Color.FromArgb(100, 116, 139);
            if (temp.Value < 60f) return Color.FromArgb(74, 222, 128);
            if (temp.Value < 75f) return Color.FromArgb(250, 204, 21);
            if (temp.Value < 85f) return Color.FromArgb(251, 146, 60);
            return Color.FromArgb(248, 113, 113);
        }

        private static Color UsageColor(float? pct)
        {
            if (!pct.HasValue) return Color.FromArgb(100, 116, 139);
            if (pct.Value < 70f) return Color.FromArgb(74, 222, 128);
            if (pct.Value < 85f) return Color.FromArgb(250, 204, 21);
            if (pct.Value < 95f) return Color.FromArgb(251, 146, 60);
            return Color.FromArgb(248, 113, 113);
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2f;
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0.5f || d > r.Width || d > r.Height)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void FillRounded(Graphics g, Color fill, Color? border, RectangleF rect, float radius)
        {
            using (GraphicsPath path = RoundedRect(rect, radius))
            {
                using (SolidBrush b = new SolidBrush(fill))
                {
                    g.FillPath(b, path);
                }
                if (border.HasValue)
                {
                    using (Pen p = new Pen(border.Value, 1f))
                    {
                        g.DrawPath(p, path);
                    }
                }
            }
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, float x, float y, bool rightAlign = false)
        {
            using (SolidBrush b = new SolidBrush(color))
            using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
            {
                sf.Alignment = rightAlign ? StringAlignment.Far : StringAlignment.Near;
                sf.LineAlignment = StringAlignment.Center;
                g.DrawString(text, font, b, x, y, sf);
            }
        }

        private static void DrawMiniBar(Graphics g, float x1, float y1, float x2, float y2, float? val, Color color)
        {
            float w = x2 - x1;
            float h = y2 - y1;
            FillRounded(g, TrackBg, null, new RectangleF(x1, y1, w, h), 2f);
            if (val.HasValue && val.Value > 0f)
            {
                float ratio = Math.Max(0.05f, Math.Min(1f, val.Value / 100f));
                float fw = w * ratio;
                if (fw > 4f)
                    FillRounded(g, color, null, new RectangleF(x1, y1, fw, h), 2f);
            }
        }

        private static void DrawSectionBox(Graphics g, float x1, float y1, float x2, float y2, Color accent)
        {
            FillRounded(g, BgSection, BorderSection, new RectangleF(x1, y1, x2 - x1, y2 - y1), 8f);
            FillRounded(g, accent, null, new RectangleF(x1 + 2f, y1 + 7f, 3f, (y2 - y1) - 14f), 1.5f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            VitalsSnapshot d;
            lock (_lock)
            {
                d = _data;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float w = Width;
            float h = Height;

            FillRounded(g, BgCard, BorderCard, new RectangleF(1f, 1f, w - 2f, h - 2f), 11f);

            float? maxTemp = null;
            if (d.CpuTemp.HasValue && d.GpuTemp.HasValue)
                maxTemp = Math.Max(d.CpuTemp.Value, d.GpuTemp.Value);
            else if (d.CpuTemp.HasValue)
                maxTemp = d.CpuTemp;
            else if (d.GpuTemp.HasValue)
                maxTemp = d.GpuTemp;

            Color statusCol = TempColor(maxTemp);

            if (_cfg.Compact)
            {
                using (SolidBrush sb = new SolidBrush(statusCol))
                {
                    g.FillEllipse(sb, 10f, 10f, 6f, 6f);
                }
                DrawText(g, "×", _fontIcon, _closeHover ? Color.FromArgb(248, 113, 113) : Color.FromArgb(71, 85, 105), w - 9f, 13f, true);

                string cpuT = d.CpuTemp.HasValue ? d.CpuTemp.Value.ToString("0", CultureInfo.InvariantCulture) + "°C" : "--°C";
                string cpuU = d.CpuLoad.HasValue ? d.CpuLoad.Value.ToString("0", CultureInfo.InvariantCulture) + "%" : "--%";
                DrawText(g, "CPU", _fontLabel, AccentCpu, 13f, 34f);
                DrawText(g, cpuU, _fontSub, UsageColor(d.CpuLoad), 58f, 34f);
                DrawText(g, cpuT, _fontTempCompact, TempColor(d.CpuTemp), w - 13f, 34f, true);

                string gpuT = d.GpuTemp.HasValue ? d.GpuTemp.Value.ToString("0", CultureInfo.InvariantCulture) + "°C" : "--°C";
                string gpuU = d.GpuLoad.HasValue ? d.GpuLoad.Value.ToString("0", CultureInfo.InvariantCulture) + "%" : "--%";
                DrawText(g, "GPU", _fontLabel, AccentGpu, 13f, 56f);
                DrawText(g, gpuU, _fontSub, UsageColor(d.GpuLoad), 58f, 56f);
                DrawText(g, gpuT, _fontTempCompact, TempColor(d.GpuTemp), w - 13f, 56f, true);

                string vramU = d.VramPct.HasValue ? d.VramPct.Value.ToString("0", CultureInfo.InvariantCulture) + "%" : "--%";
                string vramGb = d.VramUsedGb.HasValue ? d.VramUsedGb.Value.ToString("0.1", CultureInfo.InvariantCulture) + "G" : "";
                DrawText(g, "VRAM", _fontSub, AccentGpu, 13f, 78f);
                DrawText(g, vramGb, _fontSub, TextSub, 58f, 78f);
                DrawText(g, vramU, _fontTempCompact, UsageColor(d.VramPct), w - 13f, 78f, true);

                string ramU = d.RamPct.HasValue ? d.RamPct.Value.ToString("0", CultureInfo.InvariantCulture) + "%" : "--%";
                string ramGb = d.RamUsedGb.HasValue ? d.RamUsedGb.Value.ToString("0.1", CultureInfo.InvariantCulture) + "G" : "";
                DrawText(g, "RAM", _fontLabel, AccentRam, 13f, 100f);
                DrawText(g, ramGb, _fontSub, TextSub, 58f, 100f);
                DrawText(g, ramU, _fontTempCompact, UsageColor(d.RamPct), w - 13f, 100f, true);
                return;
            }

            // Header
            using (SolidBrush sb = new SolidBrush(statusCol))
            {
                g.FillEllipse(sb, 12f, 11f, 7f, 7f);
            }
            DrawText(g, "MONITOR", _fontHeader, TextMuted, 25f, 14f);
            DrawText(g, "⋮", _fontIcon, _menuHover ? Color.FromArgb(226, 232, 240) : Color.FromArgb(82, 92, 112), w - 28f, 13f, true);
            DrawText(g, "×", _fontIcon, _closeHover ? Color.FromArgb(248, 113, 113) : Color.FromArgb(82, 92, 112), w - 11f, 13f, true);

            // 1. CPU Sub-card
            DrawComponentGroup(g, 30f, w, "CPU", AccentCpu, d.CpuTemp, d.CpuLoad, null, null);

            // 2. GPU Sub-card
            string vramSub = (d.VramUsedGb.HasValue && d.VramTotalGb.HasValue)
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.1}/{1:0}G", d.VramUsedGb.Value, d.VramTotalGb.Value)
                : "";
            DrawComponentGroup(g, 111f, w, "GPU", AccentGpu, d.GpuTemp, d.GpuLoad, d.VramPct, vramSub);

            // 3. RAM Sub-card
            string ramSub = (d.RamUsedGb.HasValue && d.RamTotalGb.HasValue)
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.1}/{1:0}G", d.RamUsedGb.Value, d.RamTotalGb.Value)
                : "";
            DrawRamGroup(g, 218f, w, ramSub, d.RamPct);
        }

        private void DrawComponentGroup(Graphics g, float yTop, float width, string title, Color accent, float? temp, float? load, float? vramPct, string vramSub)
        {
            bool hasVram = vramSub != null;
            float boxH = hasVram ? 100f : 74f;
            DrawSectionBox(g, 8f, yTop, width - 8f, yTop + boxH, accent);

            float padL = 18f;
            float padR = width - 16f;

            DrawText(g, title, _fontLabel, accent, padL, yTop + 11f);

            string tStr = temp.HasValue ? temp.Value.ToString("0", CultureInfo.InvariantCulture) + "°C" : "--°C";
            Color tCol = TempColor(temp);
            DrawText(g, "Temp", _fontSub, TextMuted, padL, yTop + 27f);
            DrawText(g, tStr, _fontVal, tCol, padR, yTop + 27f, true);
            DrawMiniBar(g, padL, yTop + 36f, padR, yTop + 40f, temp, tCol);

            string uStr = load.HasValue ? load.Value.ToString("0", CultureInfo.InvariantCulture) + "%" : "--%";
            Color uCol = UsageColor(load);
            DrawText(g, "Load", _fontSub, TextMuted, padL, yTop + 53f);
            DrawText(g, uStr, _fontVal, uCol, padR, yTop + 53f, true);
            DrawMiniBar(g, padL, yTop + 62f, padR, yTop + 66f, load, uCol);

            if (hasVram)
            {
                string vStr = vramPct.HasValue ? vramPct.Value.ToString("0", CultureInfo.InvariantCulture) + "%" : "--%";
                Color vCol = UsageColor(vramPct);
                DrawText(g, "VRAM", _fontSub, TextMuted, padL, yTop + 79f);
                if (!string.IsNullOrEmpty(vramSub))
                    DrawText(g, vramSub, _fontSub, TextSub, padL + 36f, yTop + 79f);
                DrawText(g, vStr, _fontVal, vCol, padR, yTop + 79f, true);
                DrawMiniBar(g, padL, yTop + 88f, padR, yTop + 92f, vramPct, vCol);
            }
        }

        private void DrawRamGroup(Graphics g, float yTop, float width, string sub, float? pct)
        {
            DrawSectionBox(g, 8f, yTop, width - 8f, yTop + 38f, AccentRam);

            float padL = 18f;
            float padR = width - 16f;

            DrawText(g, "RAM", _fontLabel, AccentRam, padL, yTop + 13f);
            if (!string.IsNullOrEmpty(sub))
                DrawText(g, sub, _fontSub, TextSub, padL + 36f, yTop + 13f);

            string pStr = pct.HasValue ? pct.Value.ToString("0", CultureInfo.InvariantCulture) + "%" : "--%";
            Color pCol = UsageColor(pct);
            DrawText(g, pStr, _fontVal, pCol, padR, yTop + 13f, true);
            DrawMiniBar(g, padL, yTop + 25f, padR, yTop + 29f, pct, pCol);
        }

        private void SensorLoop()
        {
            Computer computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = false,
                IsMemoryEnabled = false,
                IsControllerEnabled = false,
                IsNetworkEnabled = false,
                IsStorageEnabled = false
            };

            UpdateVisitor visitor = new UpdateVisitor();

            try
            {
                computer.Open();

                while (_running)
                {
                    computer.Accept(visitor);

                    float? cpuTemp = null;
                    float? cpuLoad = null;
                    float? gpuTemp = null;
                    float? gpuLoad = null;
                    float? gpuVramUsed = null;
                    float? gpuVramTotal = null;

                    foreach (IHardware hw in computer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.Cpu)
                        {
                            List<ISensor> sensors = new List<ISensor>();
                            CollectSensors(hw, sensors);
                            cpuTemp = PickCpuTemp(sensors);
                            cpuLoad = PickCpuLoad(sensors);
                        }
                        else if (hw.HardwareType == HardwareType.GpuAmd ||
                                 hw.HardwareType == HardwareType.GpuNvidia ||
                                 hw.HardwareType == HardwareType.GpuIntel)
                        {
                            List<ISensor> sensors = new List<ISensor>();
                            CollectSensors(hw, sensors);
                            float? t = PickGpuTemp(sensors);
                            float? l = PickGpuLoad(sensors);
                            float? vUsed;
                            float? vTotal;
                            PickGpuVram(sensors, out vUsed, out vTotal);
                            if (gpuTemp == null || t != null)
                            {
                                if (t != null) gpuTemp = t;
                                if (l != null) gpuLoad = l;
                                if (vUsed != null) gpuVramUsed = vUsed;
                                if (vTotal != null) gpuVramTotal = vTotal;
                            }
                        }
                    }

                    VitalsSnapshot snap = new VitalsSnapshot
                    {
                        CpuTemp = cpuTemp,
                        CpuLoad = cpuLoad,
                        GpuTemp = gpuTemp,
                        GpuLoad = gpuLoad
                    };

                    if (gpuVramUsed.HasValue && gpuVramTotal.HasValue && gpuVramTotal.Value > 0f)
                    {
                        snap.VramUsedGb = gpuVramUsed.Value / 1024f;
                        snap.VramTotalGb = gpuVramTotal.Value / 1024f;
                        snap.VramPct = Math.Max(0f, Math.Min(100f, (gpuVramUsed.Value / gpuVramTotal.Value) * 100f));
                    }

                    MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
                    if (GlobalMemoryStatusEx(mem))
                    {
                        snap.RamTotalGb = (float)(mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0));
                        snap.RamUsedGb = (float)((mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0));
                        snap.RamPct = (float)mem.dwMemoryLoad;
                    }

                    lock (_lock)
                    {
                        _data = snap;
                    }

                    Thread.Sleep(1000);
                }
            }
            catch { }
            finally
            {
                try { computer.Close(); } catch { }
            }
        }

        private static void CollectSensors(IHardware hw, List<ISensor> list)
        {
            foreach (ISensor s in hw.Sensors) list.Add(s);
            foreach (IHardware sub in hw.SubHardware) CollectSensors(sub, list);
        }

        private static float? PickCpuTemp(List<ISensor> sensors)
        {
            float? pkg = null, tctl = null, coreAvg = null, coreMax = null, fallback = null;
            float coreSum = 0f;
            int coreCount = 0;

            foreach (ISensor s in sensors)
            {
                if (s.SensorType != SensorType.Temperature || !s.Value.HasValue) continue;
                float v = s.Value.Value;
                if (v <= 0 || v > 150) continue;
                string name = (s.Name ?? "").ToLowerInvariant();
                if (name.Contains("package")) pkg = v;
                else if (name.Contains("tctl") || name.Contains("tdie")) tctl = v;
                else if (name.Contains("core average")) coreAvg = v;
                else if (name.Contains("core max")) coreMax = v;
                else if (name.Contains("core #") || name.Contains("cpu core")) { coreSum += v; coreCount++; }
                else if (!fallback.HasValue) fallback = v;
            }

            if (pkg.HasValue) return pkg;
            if (tctl.HasValue) return tctl;
            if (coreAvg.HasValue) return coreAvg;
            if (coreCount > 0) return coreSum / coreCount;
            if (coreMax.HasValue) return coreMax;
            return fallback;
        }

        private static float? PickCpuLoad(List<ISensor> sensors)
        {
            float? total = null;
            float coreSum = 0f;
            int coreCount = 0;

            foreach (ISensor s in sensors)
            {
                if (s.SensorType != SensorType.Load || !s.Value.HasValue) continue;
                string name = (s.Name ?? "").ToLowerInvariant();
                if (name.Contains("cpu total")) total = s.Value.Value;
                else if (name.Contains("core")) { coreSum += s.Value.Value; coreCount++; }
            }

            if (total.HasValue) return total;
            if (coreCount > 0) return coreSum / coreCount;
            return null;
        }

        private static float? PickGpuTemp(List<ISensor> sensors)
        {
            float? core = null, edge = null, hotspot = null, fallback = null;
            foreach (ISensor s in sensors)
            {
                if (s.SensorType != SensorType.Temperature || !s.Value.HasValue) continue;
                float v = s.Value.Value;
                if (v <= 0 || v > 150) continue;
                string name = (s.Name ?? "").ToLowerInvariant();
                if (name.Contains("gpu core") || name == "core") core = v;
                else if (name.Contains("edge")) edge = v;
                else if (name.Contains("hot spot") || name.Contains("hotspot")) hotspot = v;
                else if (!fallback.HasValue) fallback = v;
            }
            return core ?? edge ?? fallback ?? hotspot;
        }

        private static float? PickGpuLoad(List<ISensor> sensors)
        {
            float? best = null, fallback = null;
            foreach (ISensor s in sensors)
            {
                if (s.SensorType != SensorType.Load || !s.Value.HasValue) continue;
                string name = (s.Name ?? "").ToLowerInvariant();
                if (name.Contains("memory") || name.Contains("vram")) continue;
                float v = Math.Max(0f, Math.Min(100f, s.Value.Value));
                if (name.Contains("gpu core") || name == "core" || name.Contains("d3d"))
                {
                    if (!best.HasValue || v > best.Value) best = v;
                }
                else if (!fallback.HasValue || v > fallback.Value)
                {
                    fallback = v;
                }
            }
            return best ?? fallback;
        }

        private static void PickGpuVram(List<ISensor> sensors, out float? usedMb, out float? totalMb)
        {
            float? gpuUsed = null, d3dUsed = null, gpuTotal = null, d3dTotal = null;
            foreach (ISensor s in sensors)
            {
                if ((s.SensorType != SensorType.SmallData && s.SensorType != SensorType.Data) || !s.Value.HasValue)
                    continue;
                string name = (s.Name ?? "").ToLowerInvariant();
                float v = s.Value.Value;
                if (s.SensorType == SensorType.Data) v *= 1024f;
                if (name == "gpu memory used") gpuUsed = v;
                else if (name.Contains("dedicated memory used")) d3dUsed = v;
                else if (name == "gpu memory total") gpuTotal = v;
                else if (name.Contains("dedicated memory total")) d3dTotal = v;
            }
            usedMb = d3dUsed ?? gpuUsed;
            totalMb = gpuTotal ?? d3dTotal;
        }
    }
}
