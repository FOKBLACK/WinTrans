// WinTrans - 非注入式窗口透明度工具
// Copyright (C) 2026 FOKBLACK
//
// 本程序是自由软件：你可以依据自由软件基金会发布的 GNU 通用公共许可证
// 第 3 版或（由你选择）任何更高版本，重新分发和/或修改它。
// 本程序基于"有用"的目的分发，但不提供任何担保，也不承诺适销性或
// 适用于特定用途——详见 GNU 通用公共许可证。
// 许可证全文见项目根目录的 LICENSE 文件。
// 功能：整窗透明(WS_EX_LAYERED) / DWM 缩略图镜像浮窗 / 区域裁剪 / 预设档+平滑渐变
//       / 确认弹窗 / 样式自愈 / 每窗口独立滑块 / 热键可配 / 排除名单 / 列表选窗
// 编译：csc /target:winexe /platform:x64 /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WinTrans
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public RECT(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; }
        public int Width { get { return Right - Left; } }
        public int Height { get { return Bottom - Top; } }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_THUMBNAIL_PROPERTIES
    {
        public uint dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        public int fVisible;
        public int fSourceClientAreaOnly;
    }

    static class Nat
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const uint LWA_ALPHA = 0x2;
        public const uint GA_ROOT = 2;

        public const uint DWM_TNP_RECTDESTINATION = 0x1;
        public const uint DWM_TNP_RECTSOURCE = 0x2;
        public const uint DWM_TNP_OPACITY = 0x4;
        public const uint DWM_TNP_VISIBLE = 0x8;
        public const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x10;

        public const uint MOD_ALT = 0x1;
        public const uint MOD_CONTROL = 0x2;
        public const uint MOD_SHIFT = 0x4;
        public const uint MOD_WIN = 0x8;

        public const int WM_HOTKEY = 0x0312;
        public const int WM_NCHITTEST = 0x0084;
        public const int WM_MOUSEWHEEL = 0x020A;

        [DllImport("user32.dll")] public static extern int GetWindowLongW(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] public static extern int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", SetLastError = true)] public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")] public static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint crKey, out byte bAlpha, out uint dwFlags);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT pt);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hAfter, int x, int y, int cx, int cy, uint flags);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
        [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(IntPtr thumb);
        [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref DWM_THUMBNAIL_PROPERTIES props);
        [DllImport("dwmapi.dll")] public static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out SIZE pSize);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetPrivateProfileStringW(string section, string key, string def, StringBuilder ret, int size, string file);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool WritePrivateProfileStringW(string section, string key, string val, string file);

        public static string Title(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(512);
            GetWindowTextW(h, sb, 512);
            string s = sb.ToString();
            if (s.Length == 0) s = "(无标题窗口)";
            return s;
        }

        public static string ClassName(IntPtr h)
        {
            StringBuilder sb = new StringBuilder(256);
            GetClassNameW(h, sb, 256);
            return sb.ToString();
        }

        public static string ProcName(IntPtr h)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            try { return Process.GetProcessById((int)pid).ProcessName + ".exe"; }
            catch { return "(未知)"; }
        }

        public static bool IsOwnProcess(IntPtr h)
        {
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            return pid == (uint)Process.GetCurrentProcess().Id;
        }
    }

    // 配置文件（放在 exe 同目录，可随绿色版一起搬走）
    static class Cfg
    {
        public static string Path = System.IO.Path.Combine(Application.StartupPath, "WinTrans.ini");

        public static string Get(string sec, string key, string def)
        {
            StringBuilder sb = new StringBuilder(2048);
            try { Nat.GetPrivateProfileStringW(sec, key, def, sb, sb.Capacity, Path); } catch { }
            string s = sb.ToString();
            return s.Length == 0 ? def : s;
        }

        public static void Set(string sec, string key, string val)
        {
            try { Nat.WritePrivateProfileStringW(sec, key, val, Path); } catch { }
        }
    }

    class Hotkey
    {
        public uint Mods;
        public uint Vk;

        public Hotkey(uint mods, uint vk) { Mods = mods; Vk = vk; }

        public bool Same(Hotkey k) { return k != null && k.Mods == Mods && k.Vk == Vk; }

        public override string ToString()
        {
            string s = "";
            if ((Mods & Nat.MOD_CONTROL) != 0) s += "Ctrl+";
            if ((Mods & Nat.MOD_ALT) != 0) s += "Alt+";
            if ((Mods & Nat.MOD_SHIFT) != 0) s += "Shift+";
            if ((Mods & Nat.MOD_WIN) != 0) s += "Win+";
            s += KeyName();
            return s;
        }

        string KeyName()
        {
            Keys k = (Keys)Vk;
            if (k >= Keys.D0 && k <= Keys.D9) return ((char)('0' + (int)(k - Keys.D0))).ToString();
            if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num" + (int)(k - Keys.NumPad0);
            if (Vk == 0x26) return "↑";
            if (Vk == 0x28) return "↓";
            if (Vk == 0x25) return "←";
            if (Vk == 0x27) return "→";
            return k.ToString();
        }

        public static Hotkey Parse(string s, Hotkey def)
        {
            if (string.IsNullOrEmpty(s)) return def;
            string[] parts = s.Split(',');
            if (parts.Length != 2) return def;
            try
            {
                return new Hotkey(uint.Parse(parts[0]), uint.Parse(parts[1]));
            }
            catch { return def; }
        }

        public string Serialize() { return Mods + "," + Vk; }
    }

    // 一个被托管的窗口
    class Managed
    {
        public IntPtr Hwnd;
        public string Title;
        public string Proc;
        public bool CloneMode;
        public int Alpha = 70;
        public int FadeFrom;
        public int FadeTo;
        public int FadeStart;
        public bool Fading;

        public bool Touched;
        public int OrigExStyle;
        public bool OrigLayered;
        public uint OrigColor;
        public byte OrigAlpha;
        public uint OrigFlags;

        public OverlayForm Overlay;
        public Rectangle Region = Rectangle.Empty; // 相对目标客户区
        public WindowCard Card;
    }

    // 镜像浮窗
    class OverlayForm : Form
    {
        public IntPtr Target;
        public IntPtr Thumb = IntPtr.Zero;
        public int AlphaPct = 70;
        public double ScaleFactor = 0.42;
        public bool ClickThrough = false;
        public Rectangle SourceRect = Rectangle.Empty;
        public Action<int> OnAlphaChanged;
        public Action<OverlayForm> ClosedCallback;

        bool dragging;
        Point dragStart;
        Point dragOrigin;
        int resizeDir;              // 0=不在缩放；1左 2右 4上 8下（可组合）
        Rectangle resizeStartBounds;
        Point resizeStartCursor;
        int lastApplyTick;
        public bool FitLocked = true;   // true=按源窗口比例自动贴合；手动拉伸后置 false
        public Action<string> OnNote;

        const int RZ_LEFT = 1;
        const int RZ_RIGHT = 2;
        const int RZ_TOP = 4;
        const int RZ_BOTTOM = 8;
        const int GRIP = 7;   // 外轮廓可拉伸的边宽（像素）

        public OverlayForm(IntPtr target, int alpha, double scale)
        {
            Target = target;
            AlphaPct = alpha;
            ScaleFactor = scale;
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Text = "WinTrans 克隆";
            MinimumSize = new Size(80, 60);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BuildMenu();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Nat.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        void BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem ct = new ToolStripMenuItem("点击透传（鼠标穿到下层）");
            ct.CheckOnClick = true;
            ct.Click += delegate { SetClickThrough(ct.Checked); };
            ToolStripMenuItem up = new ToolStripMenuItem("更不透明 (+5%)");
            up.Click += delegate { SetAlpha(AlphaPct + 5); };
            ToolStripMenuItem down = new ToolStripMenuItem("更透明 (-5%)");
            down.Click += delegate { SetAlpha(AlphaPct - 5); };
            ToolStripMenuItem full = new ToolStripMenuItem("恢复 100%");
            full.Click += delegate { SetAlpha(100); };
            ToolStripMenuItem bigger = new ToolStripMenuItem("放大 (+10%)");
            bigger.Click += delegate { Zoom(1.1); };
            ToolStripMenuItem smaller = new ToolStripMenuItem("缩小 (-10%)");
            smaller.Click += delegate { Zoom(0.9); };
            ToolStripMenuItem fit = new ToolStripMenuItem("贴合源窗口比例");
            fit.Click += delegate
            {
                FitLocked = true;
                SnapSize();
                UpdateThumb();
            };
            ToolStripMenuItem close = new ToolStripMenuItem("关闭这个浮窗");
            close.Click += delegate { Close(); };
            menu.Items.Add(ct);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(up);
            menu.Items.Add(down);
            menu.Items.Add(full);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(bigger);
            menu.Items.Add(smaller);
            menu.Items.Add(fit);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(close);
            this.ContextMenuStrip = menu;
        }

        public void Zoom(double f)
        {
            FitLocked = true;
            ScaleFactor = Math.Max(0.05, Math.Min(2.0, ScaleFactor * f));
            SnapSize();
            UpdateThumb();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            IntPtr t = IntPtr.Zero;
            int hr = Nat.DwmRegisterThumbnail(this.Handle, Target, out t);
            if (hr != 0)
            {
                MessageBox.Show("DWM 缩略图注册失败（HRESULT=0x" + hr.ToString("X8") + "）。\n"
                    + "常见原因：窗口已关闭、未开启桌面合成，或该窗口是独占全屏游戏。",
                    "WinTrans", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }
            Thumb = t;
            SnapSize();
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int nx = Left, ny = Top;
            if (Right > wa.Right) nx = wa.Right - Width - 8;
            if (Bottom > wa.Bottom) ny = wa.Bottom - Height - 8;
            if (nx < wa.Left) nx = wa.Left + 8;
            if (ny < wa.Top) ny = wa.Top + 8;
            Location = new Point(nx, ny);
            UpdateThumb();
        }

        public SIZE SourceSize()
        {
            if (!SourceRect.IsEmpty)
            {
                SIZE s2 = new SIZE();
                s2.cx = SourceRect.Width;
                s2.cy = SourceRect.Height;
                return s2;
            }
            SIZE s = new SIZE();
            if (Thumb != IntPtr.Zero) Nat.DwmQueryThumbnailSourceSize(Thumb, out s);
            if (s.cx <= 0 || s.cy <= 0)
            {
                RECT r = new RECT();
                Nat.GetClientRect(Target, out r);
                s.cx = r.Width; s.cy = r.Height;
            }
            return s;
        }

        public void SnapSize()
        {
            if (!FitLocked) return;   // 手动拉伸过就保持用户尺寸，交给心跳跳过
            SIZE s = SourceSize();
            int w = Math.Max(80, (int)(s.cx * ScaleFactor));
            int h = Math.Max(60, (int)(s.cy * ScaleFactor));
            ClientSize = new Size(w, h);
        }

        public void UpdateThumb()
        {
            if (Thumb == IntPtr.Zero) return;
            DWM_THUMBNAIL_PROPERTIES p = new DWM_THUMBNAIL_PROPERTIES();
            p.dwFlags = Nat.DWM_TNP_RECTDESTINATION | Nat.DWM_TNP_OPACITY | Nat.DWM_TNP_VISIBLE | Nat.DWM_TNP_SOURCECLIENTAREAONLY;
            p.rcDestination = new RECT(0, 0, ClientSize.Width, ClientSize.Height);
            if (!SourceRect.IsEmpty)
            {
                p.dwFlags |= Nat.DWM_TNP_RECTSOURCE;
                p.rcSource = new RECT(SourceRect.X, SourceRect.Y, SourceRect.X + SourceRect.Width, SourceRect.Y + SourceRect.Height);
            }
            p.opacity = (byte)(255 * AlphaPct / 100);
            p.fVisible = 1;
            p.fSourceClientAreaOnly = 1;
            Nat.DwmUpdateThumbnailProperties(Thumb, ref p);
        }

        public void SetAlpha(int pct)
        {
            if (pct < 5) pct = 5;
            if (pct > 100) pct = 100;
            AlphaPct = pct;
            UpdateThumb();
            if (OnAlphaChanged != null) OnAlphaChanged(pct);
        }

        public void SetClickThrough(bool on)
        {
            ClickThrough = on;
            Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Nat.WM_NCHITTEST && ClickThrough)
            {
                m.Result = (IntPtr)(-1);
                return;
            }
            if (m.Msg == Nat.WM_MOUSEWHEEL)
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                SetAlpha(AlphaPct + (delta > 0 ? 5 : -5));
                return;
            }
            base.WndProc(ref m);
        }

        int HitDir(Point p)
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            if (p.X < 0 || p.Y < 0 || p.X >= w || p.Y >= h) return 0;
            bool l = p.X < GRIP, r = p.X >= w - GRIP;
            bool tp = p.Y < GRIP, bt = p.Y >= h - GRIP;
            if (tp && l) return RZ_TOP | RZ_LEFT;
            if (tp && r) return RZ_TOP | RZ_RIGHT;
            if (bt && l) return RZ_BOTTOM | RZ_LEFT;
            if (bt && r) return RZ_BOTTOM | RZ_RIGHT;
            if (tp) return RZ_TOP;
            if (bt) return RZ_BOTTOM;
            if (l) return RZ_LEFT;
            if (r) return RZ_RIGHT;
            return 0;
        }

        static Cursor CursorFor(int dir)
        {
            if (dir == (RZ_TOP | RZ_LEFT) || dir == (RZ_BOTTOM | RZ_RIGHT)) return Cursors.SizeNWSE;
            if (dir == (RZ_TOP | RZ_RIGHT) || dir == (RZ_BOTTOM | RZ_LEFT)) return Cursors.SizeNESW;
            if (dir == RZ_LEFT || dir == RZ_RIGHT) return Cursors.SizeWE;
            if (dir == RZ_TOP || dir == RZ_BOTTOM) return Cursors.SizeNS;
            return Cursors.Default;
        }

        // 拖动：直接 SetWindowPos + 8ms 合并，避免原 Location 赋值每次都走 WinForms 布局
        void ApplyMove(bool force)
        {
            int now = Environment.TickCount;
            if (!force && now - lastApplyTick < 8) return;
            lastApplyTick = now;
            Point p = Cursor.Position;
            int nx = dragOrigin.X + (p.X - dragStart.X);
            int ny = dragOrigin.Y + (p.Y - dragStart.Y);
            if (nx == Left && ny == Top) return;
            Nat.SetWindowPos(Handle, IntPtr.Zero, nx, ny, 0, 0,
                Nat.SWP_NOSIZE | Nat.SWP_NOZORDER | Nat.SWP_NOACTIVATE);
        }

        // 缩放：按抓到的边计算新矩形，最小尺寸夹取，缩略图目标区随之刷新
        void ApplyResize(bool force)
        {
            int now = Environment.TickCount;
            if (!force && now - lastApplyTick < 8) return;
            Point p = Cursor.Position;
            int dx = p.X - resizeStartCursor.X;
            int dy = p.Y - resizeStartCursor.Y;
            Rectangle r = resizeStartBounds;
            int minW = MinimumSize.Width, minH = MinimumSize.Height;
            int x = r.X, y = r.Y, w = r.Width, h = r.Height;
            if ((resizeDir & RZ_LEFT) != 0)
            {
                w = r.Width - dx;
                if (w < minW) w = minW;
                x = r.Right - w;
            }
            else if ((resizeDir & RZ_RIGHT) != 0)
            {
                w = r.Width + dx;
                if (w < minW) w = minW;
            }
            if ((resizeDir & RZ_TOP) != 0)
            {
                h = r.Height - dy;
                if (h < minH) h = minH;
                y = r.Bottom - h;
            }
            else if ((resizeDir & RZ_BOTTOM) != 0)
            {
                h = r.Height + dy;
                if (h < minH) h = minH;
            }
            if (x == Left && y == Top && w == Width && h == Height) return;
            lastApplyTick = now;
            Nat.SetWindowPos(Handle, IntPtr.Zero, x, y, w, h,
                Nat.SWP_NOZORDER | Nat.SWP_NOACTIVATE);
            UpdateThumb();
        }

        void EndGesture()
        {
            Program.Log("overlay end cur=" + Cursor.Position + " bounds=" + Bounds + " resizeDir=" + resizeDir);
            if (resizeDir != 0) ApplyResize(true);
            else if (dragging) ApplyMove(true);
            bool wasResize = resizeDir != 0;
            dragging = false;
            resizeDir = 0;
            if (wasResize)
            {
                FitLocked = false;
                SIZE s = SourceSize();
                if (s.cx > 0) ScaleFactor = (double)ClientSize.Width / s.cx;
                UpdateThumb();
                if (OnNote != null)
                    OnNote("浮窗尺寸 " + ClientSize.Width + "×" + ClientSize.Height
                        + "（已按此尺寸锁定，右键可恢复贴合源窗口比例）");
            }
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || ClickThrough) return;
            int dir = HitDir(e.Location);
            Program.Log("overlay down loc=" + e.Location + " cur=" + Cursor.Position + " bounds=" + Bounds + " dir=" + dir);
            if (dir != 0)
            {
                resizeDir = dir;
                resizeStartBounds = Bounds;
                resizeStartCursor = Cursor.Position;
            }
            else
            {
                dragging = true;
                dragStart = Cursor.Position;
                dragOrigin = Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (ClickThrough) return;
            if (resizeDir != 0) { ApplyResize(false); return; }
            if (dragging) { ApplyMove(false); return; }
            Cursor = CursorFor(HitDir(e.Location));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging || resizeDir != 0) EndGesture();
        }

        static readonly Font hintFont = new Font("Microsoft YaHei", 8f);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics gi = e.Graphics;
            int w = ClientSize.Width, h = ClientSize.Height;
            using (Pen pen = new Pen(Color.FromArgb(90, 200, 255), 1))
            {
                gi.DrawRectangle(pen, 0, 0, w - 1, h - 1);
                if (!ClickThrough)
                {
                    // 左下/右下角两段短线，提示外轮廓可拉伸
                    pen.Color = Color.FromArgb(150, 200, 255);
                    int g = Math.Min(14, Math.Min(w, h) / 5);
                    gi.DrawLine(pen, 0, h - 1 - g, 0, h - 1);
                    gi.DrawLine(pen, 0, h - 1, g, h - 1);
                    gi.DrawLine(pen, w - 1 - g, h - 1, w - 1, h - 1);
                    gi.DrawLine(pen, w - 1, h - 1 - g, w - 1, h - 1);
                }
            }
            if (ClickThrough)
                gi.DrawString("透传中 · 右键菜单", hintFont, Brushes.Orange, 6, 4);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (Thumb != IntPtr.Zero)
            {
                Nat.DwmUnregisterThumbnail(Thumb);
                Thumb = IntPtr.Zero;
            }
            if (ClosedCallback != null) ClosedCallback(this);
            base.OnFormClosed(e);
        }
    }

    // 区域框选
    class RegionForm : Form
    {
        IntPtr target;
        Rectangle winRect;
        POINT clientOrigin;
        Point start;
        Point cur;
        bool selecting;
        public Action<Rectangle> OnRegion;

        public RegionForm(IntPtr t)
        {
            target = t;
            RECT r = new RECT();
            Nat.GetWindowRect(t, out r);
            winRect = new Rectangle(r.Left, r.Top, r.Width, r.Height);
            POINT p = new POINT();
            Nat.ClientToScreen(t, ref p);
            clientOrigin = p;

            FormBorderStyle = FormBorderStyle.None;
            Bounds = SystemInformation.VirtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Opacity = 0.35;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            start = e.Location;
            cur = start;
            selecting = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (selecting) { cur = e.Location; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            selecting = false;
            cur = e.Location;
            Rectangle selScreen = new Rectangle(
                Math.Min(start.X, cur.X), Math.Min(start.Y, cur.Y),
                Math.Abs(cur.X - start.X), Math.Abs(cur.Y - start.Y));
            if (selScreen.Width < 24 || selScreen.Height < 24)
            {
                Close();
                return;
            }
            // 屏幕坐标 -> 目标客户区坐标（窗体现在覆盖整个虚拟屏，客户区坐标即屏幕相对坐标）
            Rectangle sel = new Rectangle(Bounds.Left + selScreen.Left, Bounds.Top + selScreen.Top,
                selScreen.Width, selScreen.Height);
            int cx = sel.Left - clientOrigin.X;
            int cy = sel.Top - clientOrigin.Y;
            RECT cr = new RECT();
            Nat.GetClientRect(target, out cr);
            Rectangle clientR = new Rectangle(0, 0, cr.Width, cr.Height);
            Rectangle reg = Rectangle.Intersect(new Rectangle(cx, cy, sel.Width, sel.Height), clientR);
            if (reg.Width < 24 || reg.Height < 24)
            {
                Close();
                return;
            }
            Program.Log("region picked " + reg.X + "," + reg.Y + " " + reg.Width + "x" + reg.Height);
            Close();
            if (OnRegion != null) OnRegion(reg);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.Orange, 2, ButtonBorderStyle.Solid, Color.Orange, 2, ButtonBorderStyle.Solid, Color.Orange, 2, ButtonBorderStyle.Solid, Color.Orange, 2, ButtonBorderStyle.Solid);
            if (selecting)
            {
                int x = Math.Min(start.X, cur.X), y = Math.Min(start.Y, cur.Y);
                int w = Math.Abs(cur.X - start.X), h = Math.Abs(cur.Y - start.Y);
                using (Pen pen = new Pen(Color.DodgerBlue, 2))
                    e.Graphics.DrawRectangle(pen, x, y, w, h);
                e.Graphics.DrawString(w + " × " + h, new Font("Microsoft YaHei", 10f, FontStyle.Bold), Brushes.White, x + 6, y + 4);
            }
            else
            {
                e.Graphics.DrawString("拖动框选要镜像的区域 · Esc 取消", new Font("Microsoft YaHei", 12f, FontStyle.Bold), Brushes.White, 20, 20);
            }
        }
    }

    // 准星取窗口
    class PickerForm : Form
    {
        public Action<IntPtr> OnPick;
        Rectangle full;

        public PickerForm()
        {
            full = SystemInformation.VirtualScreen;
            FormBorderStyle = FormBorderStyle.None;
            Bounds = full;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Opacity = 0.28;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        IntPtr FindUnder(POINT pt)
        {
            List<IntPtr> found = new List<IntPtr>();
            Nat.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!Nat.IsWindowVisible(h)) return true;
                if (Nat.IsOwnProcess(h)) return true;
                RECT r = new RECT();
                if (!Nat.GetWindowRect(h, out r)) return true;
                if (r.Width <= 0 || r.Height <= 0) return true;
                if (pt.X < r.Left || pt.X >= r.Right || pt.Y < r.Top || pt.Y >= r.Bottom) return true;
                string cls = Nat.ClassName(h);
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd"
                    || cls == "Shell_SecondaryTrayWnd" || cls == "Windows.UI.Core.CoreWindow") return true;

                // 跳过"贴在全屏最上层的透明覆盖层"（NVIDIA Overlay、Discord/Steam overlay 之类）
                // 它们永远盖在你真正想选的窗口上面，取到毫无意义
                int ex = Nat.GetWindowLongW(h, Nat.GWL_EXSTYLE);
                if ((ex & Nat.WS_EX_TRANSPARENT) != 0) return true;
                if (cls == "CEF-OSC-WIDGET") return true;
                string proc = Nat.ProcName(h);
                if (proc == "NVIDIA Overlay.exe" || proc == "NVIDIA Share.exe") return true;

                found.Add(h);
                return true;
            }, IntPtr.Zero);
            return found.Count == 0 ? IntPtr.Zero : found[0];
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Point p = Cursor.Position;
            POINT pt = new POINT();
            pt.X = p.X; pt.Y = p.Y;
            IntPtr root = FindUnder(pt);
            if (root == IntPtr.Zero || root == this.Handle || Nat.IsOwnProcess(root)) return;
            Close();
            if (OnPick != null) OnPick(root);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.DeepSkyBlue, 2, ButtonBorderStyle.Solid, Color.DeepSkyBlue, 2, ButtonBorderStyle.Solid, Color.DeepSkyBlue, 2, ButtonBorderStyle.Solid, Color.DeepSkyBlue, 2, ButtonBorderStyle.Solid);
            string text = "点击要控制的窗口 · Esc 取消";
            Font f = new Font("Microsoft YaHei", 14f, FontStyle.Bold);
            SizeF sz = e.Graphics.MeasureString(text, f);
            e.Graphics.DrawString(text, f, Brushes.White, (Width - sz.Width) / 2, Height - 90);
        }
    }

    // 从列表里挑窗口（准星点不到时的备用入口：窗口被覆盖层挡住、窗口太窄、鼠标不听话）
    class WindowListForm : Form
    {
        public IntPtr Selected = IntPtr.Zero;
        ListView lv;
        Label lblTip;
        Button btnOk;

        public WindowListForm()
        {
            Text = "选择窗口";
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(680, 430);
            MinimumSize = new Size(520, 300);

            lblTip = new Label();
            lblTip.SetBounds(12, 10, 656, 20);
            lblTip.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblTip.ForeColor = Color.DimGray;
            Controls.Add(lblTip);

            lv = new ListView();
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.MultiSelect = false;
            lv.HideSelection = false;
            lv.GridLines = false;
            lv.Columns.Add("标题", 250);
            lv.Columns.Add("进程", 160);
            lv.Columns.Add("窗口类", 170);
            lv.Columns.Add("句柄", 78);
            lv.SetBounds(12, 34, 656, 340);
            lv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lv.DoubleClick += delegate { Accept(); };
            lv.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { Accept(); e.Handled = true; }
            };
            lv.SelectedIndexChanged += delegate
            {
                if (btnOk != null) btnOk.Enabled = lv.SelectedItems.Count > 0;
            };
            Controls.Add(lv);

            btnOk = new Button();
            btnOk.Text = "确定";
            btnOk.SetBounds(492, 388, 84, 30);
            btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOk.Enabled = false;
            btnOk.Click += delegate { Accept(); };
            Controls.Add(btnOk);

            Button btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.SetBounds(584, 388, 84, 30);
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Fill();
        }

        void Fill()
        {
            List<object[]> rows = new List<object[]>();
            Nat.EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!Nat.IsWindowVisible(h)) return true;
                if (Nat.IsOwnProcess(h)) return true;
                RECT r = new RECT();
                if (!Nat.GetWindowRect(h, out r)) return true;
                if (r.Width <= 0 || r.Height <= 0) return true;
                string cls = Nat.ClassName(h);
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd"
                    || cls == "Shell_SecondaryTrayWnd" || cls == "Windows.UI.Core.CoreWindow") return true;
                if (cls == "IME" || cls == "MSCTFIME UI") return true;
                if (cls == "CEF-OSC-WIDGET") return true;
                int ex = Nat.GetWindowLongW(h, Nat.GWL_EXSTYLE);
                if ((ex & Nat.WS_EX_TRANSPARENT) != 0) return true;
                string proc = Nat.ProcName(h);
                if (proc == "NVIDIA Overlay.exe" || proc == "NVIDIA Share.exe") return true;
                string title = Nat.Title(h);
                if (title == "(无标题窗口)" && (ex & Nat.WS_EX_TOOLWINDOW) != 0) return true;
                rows.Add(new object[] { h, title, proc, cls });
                return true;
            }, IntPtr.Zero);

            rows.Sort(delegate(object[] a, object[] b)
            {
                int c = string.Compare((string)a[2], (string)b[2], StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return string.Compare((string)a[1], (string)b[1], StringComparison.OrdinalIgnoreCase);
            });

            lv.BeginUpdate();
            for (int i = 0; i < rows.Count; i++)
            {
                object[] row = rows[i];
                IntPtr h = (IntPtr)row[0];
                ListViewItem it = new ListViewItem((string)row[1]);
                it.SubItems.Add((string)row[2]);
                it.SubItems.Add((string)row[3]);
                it.SubItems.Add("0x" + h.ToString("X"));
                it.Tag = h;
                lv.Items.Add(it);
            }
            lv.EndUpdate();

            lblTip.Text = "当前桌面可见窗口 " + rows.Count + " 个（按进程名排序，已滤掉托盘、覆盖层、输入法面板）：";
        }

        void Accept()
        {
            if (lv.SelectedItems.Count == 0) return;
            Selected = (IntPtr)lv.SelectedItems[0].Tag;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // 接管前确认
    class ConfirmForm : Form
    {
        public bool Accepted = false;
        public bool CloneMode = false;
        public int Alpha = 70;
        public bool AddExclude = false;

        RadioButton rbAlpha;
        RadioButton rbClone;
        TrackBar tb;
        Label lblPct;
        CheckBox chkExclude;

        public ConfirmForm(IntPtr h)
        {
            Text = "确认接管窗口";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 344);
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            Font = new Font("Microsoft YaHei", 9f);

            Label l1 = new Label();
            l1.Text = Nat.Title(h);
            l1.Font = new Font("Microsoft YaHei", 10f, FontStyle.Bold);
            l1.SetBounds(16, 14, 488, 24);
            l1.AutoEllipsis = true;
            Controls.Add(l1);

            Label l2 = new Label();
            l2.Text = "进程：" + Nat.ProcName(h) + "    窗口类：" + Nat.ClassName(h) + "\r\n句柄：0x" + h.ToString("X");
            l2.ForeColor = Color.DimGray;
            l2.SetBounds(16, 42, 488, 40);
            Controls.Add(l2);

            GroupBox gb = new GroupBox();
            gb.Text = "接管方式";
            gb.SetBounds(16, 88, 488, 76);
            rbAlpha = new RadioButton();
            rbAlpha.Text = "整窗透明（浏览器 / 普通窗口）";
            rbAlpha.Checked = true;
            rbAlpha.SetBounds(16, 22, 300, 22);
            gb.Controls.Add(rbAlpha);
            rbClone = new RadioButton();
            rbClone.Text = "镜像浮窗（游戏 / D3D 窗口）";
            rbClone.SetBounds(16, 46, 300, 22);
            gb.Controls.Add(rbClone);
            Controls.Add(gb);

            Label l3 = new Label();
            l3.Text = "初始不透明度";
            l3.SetBounds(16, 174, 100, 22);
            Controls.Add(l3);

            tb = new TrackBar();
            tb.Minimum = 5; tb.Maximum = 100; tb.Value = 70; tb.TickFrequency = 5;
            tb.SetBounds(110, 170, 330, 40);
            tb.Scroll += delegate { lblPct.Text = tb.Value + "%"; };
            Controls.Add(tb);

            lblPct = new Label();
            lblPct.Text = "70%";
            lblPct.Font = new Font("Microsoft YaHei", 10f, FontStyle.Bold);
            lblPct.SetBounds(448, 176, 50, 24);
            Controls.Add(lblPct);

            chkExclude = new CheckBox();
            chkExclude.Text = "排除此软件（以后不再接管它的任何窗口）";
            chkExclude.SetBounds(16, 216, 480, 22);
            Controls.Add(chkExclude);

            Label warn = new Label();
            warn.Text = "提示：以管理员权限运行的程序无法被普通权限接管；独占全屏游戏无法镜像。";
            warn.ForeColor = Color.Gray;
            warn.SetBounds(16, 244, 488, 36);
            Controls.Add(warn);

            Button ok = new Button();
            ok.Text = "接管";
            ok.SetBounds(400, 296, 90, 32);
            ok.Click += delegate
            {
                CloneMode = rbClone.Checked;
                Alpha = tb.Value;
                AddExclude = chkExclude.Checked;
                Accepted = true;
                Close();
            };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(300, 296, 90, 32);
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }
    }

    // 设置：热键 + 排除名单
    class SettingsForm : Form
    {
        public Dictionary<int, Hotkey> Hotkeys;
        public List<string> Exclude;
        readonly string[] names = { "选取窗口", "预设 25%", "预设 40%", "预设 60%", "预设 75%", "预设 100%",
                                    "透明度 +5%", "透明度 -5%", "全部恢复", "切换点击透传" };
        readonly int[] ids = { 1, 10, 11, 12, 13, 14, 2, 3, 4, 15 };
        TextBox[] boxes = new TextBox[10];
        TextBox tbExclude;

        public SettingsForm(Dictionary<int, Hotkey> hk, List<string> ex)
        {
            Text = "设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 560);
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            Font = new Font("Microsoft YaHei", 9f);
            Hotkeys = new Dictionary<int, Hotkey>(hk);
            Exclude = new List<string>(ex);

            Label title = new Label();
            title.Text = "全局热键（点进输入框后直接按下组合键）";
            title.Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold);
            title.SetBounds(16, 12, 520, 20);
            Controls.Add(title);

            for (int i = 0; i < names.Length; i++)
            {
                Label lbl = new Label();
                lbl.Text = names[i];
                lbl.SetBounds(16, 40 + i * 30, 120, 22);
                Controls.Add(lbl);

                TextBox tb = new TextBox();
                tb.ReadOnly = true;
                tb.Tag = ids[i];
                Hotkey cur = Hotkeys.ContainsKey(ids[i]) ? Hotkeys[ids[i]] : new Hotkey(0, 0);
                tb.Text = cur.ToString();
                tb.SetBounds(140, 37 + i * 30, 200, 24);
                tb.KeyDown += OnHotkeyKey;
                Controls.Add(tb);
                boxes[i] = tb;
            }

            Label le = new Label();
            le.Text = "排除名单（每行一个进程名，如 chrome.exe）";
            le.Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold);
            le.SetBounds(16, 356, 520, 20);
            Controls.Add(le);

            tbExclude = new TextBox();
            tbExclude.Multiline = true;
            tbExclude.ScrollBars = ScrollBars.Vertical;
            tbExclude.SetBounds(16, 380, 520, 96);
            tbExclude.Text = string.Join("\r\n", Exclude.ToArray());
            Controls.Add(tbExclude);

            Button save = new Button();
            save.Text = "保存";
            save.SetBounds(440, 504, 90, 32);
            save.Click += delegate
            {
                Exclude = new List<string>();
                string[] lines = tbExclude.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string ln in lines)
                {
                    string t = ln.Trim();
                    if (t.Length > 0 && !Exclude.Contains(t)) Exclude.Add(t);
                }
                Close();
            };
            Controls.Add(save);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(340, 504, 90, 32);
            cancel.Click += delegate { Hotkeys = null; Close(); };
            Controls.Add(cancel);
        }

        void OnHotkeyKey(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            Keys k = e.KeyCode;
            if (k == Keys.ControlKey || k == Keys.Menu || k == Keys.ShiftKey || k == Keys.LWin || k == Keys.RWin) return;
            uint mods = 0;
            if (e.Control) mods |= Nat.MOD_CONTROL;
            if (e.Alt) mods |= Nat.MOD_ALT;
            if (e.Shift) mods |= Nat.MOD_SHIFT;
            KeysConverter kc = new KeysConverter();
            TextBox tb = (TextBox)sender;
            int id = (int)tb.Tag;
            Hotkey hk = new Hotkey(mods, (uint)k);
            if (mods == 0)
            {
                MessageBox.Show("必须带至少一个修饰键（Ctrl / Alt / Shift / Win）。", "WinTrans");
                return;
            }
            Hotkeys[id] = hk;
            tb.Text = hk.ToString();
        }
    }

    // 窗口卡片
    class WindowCard : Panel
    {
        public Managed Model;
        Label lblTitle, lblSub, lblPct;
        public TrackBar Slider;
        Button btnClose, btnRegion, btnThrough;
        public bool syncing;
        int lastLabelTick;

        public Action<WindowCard, int, bool> OnAlpha;
        public Action<WindowCard> OnClose;
        public Action<WindowCard> OnRegion;
        public Action<WindowCard, bool> OnThrough;
        public Action<WindowCard> OnActivate;

        public WindowCard(Managed m)
        {
            Model = m;
            Height = 112;
            BackColor = Color.White;
            Padding = new Padding(0);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = true;

            lblTitle = new Label();
            lblTitle.Text = m.Title;
            lblTitle.Font = new Font("Microsoft YaHei", 9.5f, FontStyle.Bold);
            lblTitle.AutoEllipsis = true;
            lblTitle.SetBounds(12, 8, 300, 22);
            lblTitle.Click += delegate { if (OnActivate != null) OnActivate(this); };
            Controls.Add(lblTitle);

            lblSub = new Label();
            lblSub.Text = SubText();
            lblSub.ForeColor = Color.Gray;
            lblSub.AutoEllipsis = true;
            lblSub.SetBounds(12, 30, 400, 20);
            Controls.Add(lblSub);

            Slider = new TrackBar();
            Slider.Minimum = 5; Slider.Maximum = 100;
            Slider.Value = Clamp(m.Alpha);
            Slider.TickFrequency = 10;
            Slider.Height = 45;
            Slider.SetBounds(10, 50, 300, 45);
            Slider.Scroll += delegate
            {
                if (!syncing && OnAlpha != null) OnAlpha(this, Slider.Value, true);
            };
            Slider.MouseUp += delegate { ForcePctLabel(); };
            Slider.KeyUp += delegate { ForcePctLabel(); };
            Controls.Add(Slider);

            lblPct = new Label();
            lblPct.Text = m.Alpha + "%";
            lblPct.Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold);
            lblPct.SetBounds(316, 58, 46, 22);
            Controls.Add(lblPct);

            btnThrough = new Button();
            btnThrough.Text = "透传";
            btnThrough.SetBounds(366, 54, 54, 28);
            btnThrough.Click += delegate
            {
                bool on = btnThrough.BackColor != Color.LightGreen;
                btnThrough.BackColor = on ? Color.LightGreen : SystemColors.Control;
                if (OnThrough != null) OnThrough(this, on);
            };
            Controls.Add(btnThrough);

            btnRegion = new Button();
            btnRegion.Text = "区域";
            btnRegion.SetBounds(424, 54, 54, 28);
            btnRegion.Click += delegate { if (OnRegion != null) OnRegion(this); };
            Controls.Add(btnRegion);

            btnClose = new Button();
            btnClose.Text = "×";
            btnClose.Font = new Font("Microsoft YaHei", 10f, FontStyle.Bold);
            btnClose.SetBounds(484, 6, 28, 24);
            btnClose.Click += delegate { if (OnClose != null) OnClose(this); };
            Controls.Add(btnClose);

            UpdateModeUi();
        }

        string SubText()
        {
            return Model.Proc + " · " + (Model.CloneMode ? "镜像浮窗" : "整窗透明")
                + (Model.Region.IsEmpty ? "" : " · 区域 " + Model.Region.Width + "×" + Model.Region.Height);
        }

        public void UpdateModeUi()
        {
            btnRegion.Visible = Model.CloneMode;
            btnThrough.Visible = Model.CloneMode;
            bool through = Model.Overlay != null && Model.Overlay.ClickThrough;
            btnThrough.BackColor = through ? Color.LightGreen : SystemColors.Control;
            lblSub.Text = SubText();
            LayoutChildren();
        }

        public void SyncAlpha(int pct) { SyncAlpha(pct, true); }

        // updateSlider=false 时只刷数字，不写回 TrackBar，避免拖动中滑块被程序回写打断
        public void SyncAlpha(int pct, bool updateSlider)
        {
            int v = Clamp(pct);
            syncing = true;
            if (updateSlider && Slider.Value != v) Slider.Value = v;
            string t = v + "%";
            if (lblPct.Text != t)
            {
                int now = Environment.TickCount;
                // 拖动中把数字限制在 ~33fps，避免每个像素都重绘数字造成闪
                if (updateSlider || now - lastLabelTick >= 30)
                {
                    lblPct.Text = t;
                    lastLabelTick = now;
                }
            }
            syncing = false;
        }

        public void ForcePctLabel()
        {
            syncing = true;
            lblPct.Text = Clamp(Slider.Value) + "%";
            lastLabelTick = Environment.TickCount;
            syncing = false;
        }

        int Clamp(int v) { return v < 5 ? 5 : (v > 100 ? 100 : v); }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            LayoutChildren();
        }

        void LayoutChildren()
        {
            if (btnClose == null || btnThrough == null || btnRegion == null
                || Slider == null || lblPct == null || lblTitle == null || lblSub == null) return;
            int w = Width;
            btnClose.Left = w - 34;
            Slider.Width = Math.Max(120, w - 250);
            lblPct.Left = Slider.Left + Slider.Width + 8;
            btnThrough.Left = lblPct.Left + 54;
            btnRegion.Left = btnThrough.Left + 58;
            lblTitle.Width = w - 50;
            lblSub.Width = w - 24;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Color.FromArgb(220, 224, 230)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    class MainForm : Form
    {
        readonly List<Managed> items = new List<Managed>();
        Dictionary<int, Hotkey> hotkeys = new Dictionary<int, Hotkey>();
        List<string> exclude = new List<string>();
        FlowLayoutPanel flow;
        Label lblStatus;
        NotifyIcon tray;
        Timer tick;      // 心跳：自愈/尺寸跟随
        Timer fade;      // 16ms 渐变
        Managed lastTarget;

        readonly int[] presetLevels = { 25, 40, 60, 75, 100 };

        public MainForm()
        {
            Text = "WinTrans";
            ClientSize = new Size(820, 600);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 520);
            Font = new Font("Microsoft YaHei", 9f);
            LoadCfg();
            BuildUi();
            BuildTray();
            Icon = AppIconLarge();
            ApplyHotkeys();
            tick = new Timer();
            tick.Interval = 900;
            tick.Tick += delegate { HeartBeat(); };
            tick.Start();
            fade = new Timer();
            fade.Interval = 16;
            fade.Tick += delegate { FadeTick(); };
        }

        void LoadCfg()
        {
            string ex = Cfg.Get("General", "Exclude", "taskmgr.exe");
            foreach (string s in ex.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                exclude.Add(s.Trim());

            hotkeys[1] = Hotkey.Parse(Cfg.Get("Hotkeys", "Pick", ""), new Hotkey(Nat.MOD_CONTROL | Nat.MOD_ALT, 0x54));
            hotkeys[10] = Hotkey.Parse(Cfg.Get("Hotkeys", "Preset1", ""), new Hotkey(Nat.MOD_CONTROL | Nat.MOD_ALT, 0x31));
            hotkeys[11] = Hotkey.Parse(Cfg.Get("Hotkeys", "Preset2", ""), new Hotkey(Nat.MOD_CONTROL | Nat.MOD_ALT, 0x32));
            hotkeys[12] = Hotkey.Parse(Cfg.Get("Hotkeys", "Preset3", ""), new Hotkey(Nat.MOD_CONTROL | Nat.MOD_ALT, 0x33));
            hotkeys[13] = Hotkey.Parse(Cfg.Get("Hotkeys", "Preset4", ""), new Hotkey(Nat.MOD_CONTROL | Nat.MOD_ALT, 0x34));
            hotkeys[14] = Hotkey.Parse(Cfg.Get("Hotkeys", "Preset5", ""), new Hotkey(Nat.MOD_CONTROL | Nat.MOD_ALT, 0x35));
            hotkeys[2] = Hotkey.Parse(Cfg.Get("Hotkeys", "Up", ""), new Hotkey(Nat.MOD_CONTROL | Nat.MOD_ALT, 0x26));
            hotkeys[3] = Hotkey.Parse(Cfg.Get("Hotkeys", "Down", ""), new Hotkey(Nat.MOD_CONTROL | Nat.MOD_ALT, 0x28));
            hotkeys[4] = Hotkey.Parse(Cfg.Get("Hotkeys", "Restore", ""), new Hotkey(Nat.MOD_CONTROL | Nat.MOD_ALT, 0x52));
            hotkeys[15] = Hotkey.Parse(Cfg.Get("Hotkeys", "Through", ""), new Hotkey(Nat.MOD_CONTROL | Nat.MOD_ALT, 0x43));
        }

        void SaveCfg()
        {
            Cfg.Set("General", "Exclude", string.Join(";", exclude.ToArray()));
            Cfg.Set("Hotkeys", "Pick", hotkeys[1].Serialize());
            Cfg.Set("Hotkeys", "Preset1", hotkeys[10].Serialize());
            Cfg.Set("Hotkeys", "Preset2", hotkeys[11].Serialize());
            Cfg.Set("Hotkeys", "Preset3", hotkeys[12].Serialize());
            Cfg.Set("Hotkeys", "Preset4", hotkeys[13].Serialize());
            Cfg.Set("Hotkeys", "Preset5", hotkeys[14].Serialize());
            Cfg.Set("Hotkeys", "Up", hotkeys[2].Serialize());
            Cfg.Set("Hotkeys", "Down", hotkeys[3].Serialize());
            Cfg.Set("Hotkeys", "Restore", hotkeys[4].Serialize());
            Cfg.Set("Hotkeys", "Through", hotkeys[15].Serialize());
        }

        void BuildUi()
        {
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 54;
            Controls.Add(top);

            Button btnAdd = new Button();
            btnAdd.Text = "添加窗口";
            btnAdd.SetBounds(12, 12, 118, 30);
            btnAdd.Click += delegate { StartPick(); };
            top.Controls.Add(btnAdd);

            Button btnList = new Button();
            btnList.Text = "选择窗口";
            btnList.SetBounds(136, 12, 118, 30);
            btnList.Click += delegate { StartList(); };
            top.Controls.Add(btnList);

            Button btnSet = new Button();
            btnSet.Text = "设置";
            btnSet.SetBounds(262, 12, 80, 30);
            btnSet.Click += delegate { OpenSettings(); };
            top.Controls.Add(btnSet);

            Button btnRestore = new Button();
            btnRestore.Text = "全部恢复";
            btnRestore.SetBounds(348, 12, 96, 30);
            btnRestore.Click += delegate { RestoreAll(); };
            top.Controls.Add(btnRestore);

            Label hint = new Label();
            hint.Text = hotkeys[1].ToString() + " 选取窗口 · 卡片里滑块单独调透明度";
            hint.ForeColor = Color.DimGray;
            hint.SetBounds(454, 18, 356, 22);
            hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            top.Controls.Add(hint);

            flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.None;
            flow.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;
            flow.Padding = new Padding(10);
            flow.BackColor = Color.FromArgb(246, 247, 249);
            flow.Resize += delegate { ResizeCards(); };
            Controls.Add(flow);

            lblStatus = new Label();
            lblStatus.Dock = DockStyle.Bottom;
            lblStatus.Height = 26;
            lblStatus.Text = "就绪";
            lblStatus.ForeColor = Color.DimGray;
            lblStatus.Padding = new Padding(10, 4, 0, 0);
            Controls.Add(lblStatus);

            flow.SetBounds(0, 54, ClientSize.Width, ClientSize.Height - 54 - 26);
        }

        void ResizeCards()
        {
            int w = flow.ClientSize.Width - 22;
            if (w < 400) w = 400;
            for (int i = 0; i < flow.Controls.Count; i++)
            {
                WindowCard c = flow.Controls[i] as WindowCard;
                if (c != null) c.Width = w;
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int ExtractIconEx(string szFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, int nIcons);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        // 从 exe 内嵌图标里取 16x16 小帧给托盘用，避免缩放发虚
        static Icon AppIconSmall()
        {
            try
            {
                IntPtr[] large = new IntPtr[1];
                IntPtr[] small = new IntPtr[1];
                if (ExtractIconEx(Application.ExecutablePath, 0, large, small, 1) > 0)
                {
                    IntPtr h = small[0];
                    if (h != IntPtr.Zero)
                    {
                        Icon ic = (Icon)Icon.FromHandle(h).Clone();
                        DestroyIcon(h);
                        return ic;
                    }
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        // 从 exe 内嵌图标里取 32x32 大帧给主窗/任务栏用
        static Icon AppIconLarge()
        {
            try
            {
                IntPtr[] large = new IntPtr[1];
                IntPtr[] small = new IntPtr[1];
                if (ExtractIconEx(Application.ExecutablePath, 0, large, small, 1) > 0)
                {
                    IntPtr h = large[0];
                    if (h != IntPtr.Zero)
                    {
                        Icon ic = (Icon)Icon.FromHandle(h).Clone();
                        DestroyIcon(h);
                        return ic;
                    }
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        void BuildTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("显示主界面", null, delegate { ShowMain(); });
            menu.Items.Add("添加窗口", null, delegate { StartPick(); });
            menu.Items.Add("全部恢复", null, delegate { RestoreAll(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { ExitApp(); });
            tray = new NotifyIcon();
            tray.Icon = AppIconSmall();
            tray.Text = "WinTrans";
            tray.Visible = true;
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowMain(); };
        }

        // ---------- 热键 ----------
        void UnregisterAll()
        {
            foreach (int id in hotkeys.Keys) Nat.UnregisterHotKey(Handle, id);
        }

        void ApplyHotkeys()
        {
            UnregisterAll();
            List<string> conflicts = new List<string>();
            foreach (KeyValuePair<int, Hotkey> kv in hotkeys)
            {
                if (!Nat.RegisterHotKey(Handle, kv.Key, kv.Value.Mods, kv.Value.Vk))
                    conflicts.Add(kv.Value.ToString());
            }
            if (conflicts.Count > 0)
                Status("热键被占用：" + string.Join("、", conflicts.ToArray()) + "（可在设置里改）");
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Nat.WM_HOTKEY)
            {
                int id = (int)m.WParam;
                if (id == 1) StartPick();
                else if (id == 2) { Managed t = PickTarget(); if (t != null) SetAlpha(t, t.Alpha + 5); }
                else if (id == 3) { Managed t = PickTarget(); if (t != null) SetAlpha(t, t.Alpha - 5); }
                else if (id == 4) RestoreAll();
                else if (id == 15) ToggleThrough();
                else if (id >= 10 && id <= 14)
                {
                    Managed t = PickTarget();
                    if (t == null) { Status("先添加一个窗口，再用预设档"); return; }
                    SetAlpha(t, presetLevels[id - 10]);
                }
                return;
            }
            base.WndProc(ref m);
        }

        Managed PickTarget()
        {
            if (lastTarget != null && Nat.IsWindow(lastTarget.Hwnd)) return lastTarget;
            IntPtr fg = Nat.GetForegroundWindow();
            Managed m = Find(fg);
            return m;
        }

        // ---------- 添加窗口 ----------
        void StartPick()
        {
            ShowMain();
            PickerForm pf = new PickerForm();
            pf.OnPick = delegate(IntPtr h) { OnPicked(h); };
            pf.Show();
            pf.Activate();
        }

        // 列表选窗：不依赖鼠标点中目标，解决被覆盖层挡住 / 窗口太小 的情况
        void StartList()
        {
            ShowMain();
            WindowListForm wl = new WindowListForm();
            if (wl.ShowDialog(this) == DialogResult.OK && wl.Selected != IntPtr.Zero)
                OnPicked(wl.Selected);
            else
                Status("已取消选择窗口");
        }

        void OnPicked(IntPtr h)
        {
            Program.Log("OnPicked hwnd=0x" + h.ToString("X") + " proc=" + Nat.ProcName(h) + " title=" + Nat.Title(h));
            Managed exist = Find(h);
            if (exist != null)
            {
                lastTarget = exist;
                Status("这个窗口已经在列表里了");
                return;
            }
            string proc = Nat.ProcName(h);
            if (IsExcluded(proc))
            {
                MessageBox.Show(proc + " 在排除名单里，不会被接管。\n如需接管，请在「设置」里删掉这条。", "WinTrans",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ConfirmForm cf = new ConfirmForm(h);
            Program.Log("confirm shown hwnd=0x" + h.ToString("X"));
            cf.ShowDialog(this);
            Program.Log("dialog closed accepted=" + cf.Accepted + " alpha=" + cf.Alpha + " clone=" + cf.CloneMode);
            if (!cf.Accepted) { Status("已取消接管"); return; }

            if (cf.AddExclude && !IsExcluded(proc))
            {
                exclude.Add(proc);
                SaveCfg();
            }

            Managed m = new Managed();
            m.Hwnd = h;
            m.Title = Nat.Title(h);
            m.Proc = proc;
            m.CloneMode = cf.CloneMode;
            m.Alpha = cf.Alpha;
            items.Add(m);
            lastTarget = m;
            try
            {
                AddCard(m);
                ApplyTarget(m);
            }
            catch (Exception ex) { Program.Log("OnPicked apply EX: " + ex.GetType().Name + " " + ex.Message + " @ " + ex.StackTrace); }
            Status("已接管：" + m.Title);
        }

        bool IsExcluded(string proc)
        {
            for (int i = 0; i < exclude.Count; i++)
                if (string.Equals(exclude[i], proc, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void AddCard(Managed m)
        {
            WindowCard c = new WindowCard(m);
            m.Card = c;
            c.Width = Math.Max(400, flow.ClientSize.Width - 22);
            c.Margin = new Padding(0, 0, 0, 8);
            c.OnAlpha = delegate(WindowCard card, int pct, bool instant) { SetAlpha(card.Model, pct, instant); };
            c.OnClose = delegate(WindowCard card) { Remove(card.Model); };
            c.OnRegion = delegate(WindowCard card) { PickRegion(card.Model); };
            c.OnThrough = delegate(WindowCard card, bool on) { SetThrough(card.Model, on); };
            c.OnActivate = delegate(WindowCard card) { lastTarget = card.Model; };
            flow.Controls.Add(c);
            flow.Controls.SetChildIndex(c, 0);
            ResizeCards();
        }

        // ---------- 应用 ----------
        void ApplyTarget(Managed m)
        {
            if (!Nat.IsWindow(m.Hwnd)) { Remove(m); return; }
            if (m.CloneMode)
            {
                RestoreAlpha(m);
                EnsureOverlay(m);
                if (m.Overlay != null) m.Overlay.SetAlpha(m.Alpha);
            }
            else
            {
                if (m.Overlay != null) CloseOverlay(m);
                CaptureOriginal(m);
                ApplyAlphaValue(m, m.Alpha);
            }
            if (m.Card != null) m.Card.UpdateModeUi();
        }

        void SetAlpha(Managed m, int pct) { SetAlpha(m, pct, false); }

        // instant=true 用于滑块拖动：立刻生效，不走 220ms 渐变，避免拖动中反复重启动画造成跳变
        void SetAlpha(Managed m, int pct, bool instant)
        {
            if (pct < 5) pct = 5;
            if (pct > 100) pct = 100;
            Program.Log("SetAlpha hwnd=0x" + m.Hwnd.ToString("X") + " pct=" + pct + (instant ? " instant" : " fade"));
            if (instant)
            {
                m.Fading = false;
                m.Alpha = pct;
                ApplyCurrent(m);
                if (m.Card != null) m.Card.SyncAlpha(pct, false);
            }
            else
            {
                if (m.Card != null) m.Card.SyncAlpha(pct);
                m.FadeFrom = m.Alpha;
                m.FadeTo = pct;
                m.FadeStart = Environment.TickCount;
                m.Fading = true;
                if (!fade.Enabled) fade.Enabled = true;
            }
            lastTarget = m;
        }

        void FadeTick()
        {
            bool any = false;
            for (int i = 0; i < items.Count; i++)
            {
                Managed m = items[i];
                if (!m.Fading) continue;
                any = true;
                int elapsed = Environment.TickCount - m.FadeStart;
                double t = elapsed / 220.0;
                if (t >= 1.0)
                {
                    m.Alpha = m.FadeTo;
                    m.Fading = false;
                    ApplyCurrent(m);
                    if (m.Card != null) m.Card.SyncAlpha(m.Alpha);
                    continue;
                }
                if (t < 0) t = 0;
                int v = (int)(m.FadeFrom + (m.FadeTo - m.FadeFrom) * t);
                ApplyCurrent2(m, v);
            }
            if (!any) fade.Enabled = false;
        }

        void ApplyCurrent(Managed m)
        {
            if (!Nat.IsWindow(m.Hwnd)) { Remove(m); return; }
            if (m.CloneMode)
            {
                if (m.Overlay != null && !m.Overlay.IsDisposed) m.Overlay.SetAlpha(m.Alpha);
            }
            else
            {
                CaptureOriginal(m);
                ApplyAlphaValue(m, m.Alpha);
            }
        }

        void ApplyCurrent2(Managed m, int v)
        {
            if (!Nat.IsWindow(m.Hwnd)) { Remove(m); return; }
            if (m.CloneMode)
            {
                if (m.Overlay != null && !m.Overlay.IsDisposed) m.Overlay.SetAlpha(v);
            }
            else
            {
                CaptureOriginal(m);
                ApplyAlphaValue(m, v);
            }
        }

        void ApplyAlphaValue(Managed m, int pct)
        {
            int ex = Nat.GetWindowLongW(m.Hwnd, Nat.GWL_EXSTYLE);
            if ((ex & Nat.WS_EX_LAYERED) == 0)
                Nat.SetWindowLongW(m.Hwnd, Nat.GWL_EXSTYLE, ex | Nat.WS_EX_LAYERED);
            byte a = (byte)(255 * pct / 100);
            if (!Nat.SetLayeredWindowAttributes(m.Hwnd, 0, a, Nat.LWA_ALPHA))
            {
                Program.Log("SetLayeredWindowAttributes failed hwnd=" + m.Hwnd.ToString("X") + " err=" + Marshal.GetLastWin32Error());
                Status("设置失败：该窗口可能以管理员权限运行（请以管理员身份启动 WinTrans）");
            }
            else Program.Log("alpha set hwnd=" + m.Hwnd.ToString("X") + " pct=" + pct + " a=" + a);
        }

        void CaptureOriginal(Managed m)
        {
            if (m.Touched) return;
            m.OrigExStyle = Nat.GetWindowLongW(m.Hwnd, Nat.GWL_EXSTYLE);
            m.OrigLayered = (m.OrigExStyle & Nat.WS_EX_LAYERED) != 0;
            uint c = 0; byte a = 255; uint f = 0;
            if (m.OrigLayered) Nat.GetLayeredWindowAttributes(m.Hwnd, out c, out a, out f);
            m.OrigColor = c; m.OrigAlpha = a; m.OrigFlags = f;
            m.Touched = true;
        }

        void RestoreAlpha(Managed m)
        {
            if (!m.Touched || !Nat.IsWindow(m.Hwnd)) return;
            if (m.OrigLayered)
                Nat.SetLayeredWindowAttributes(m.Hwnd, m.OrigColor, m.OrigAlpha, m.OrigFlags);
            else
                Nat.SetWindowLongW(m.Hwnd, Nat.GWL_EXSTYLE, m.OrigExStyle);
            m.Touched = false;
        }

        void EnsureOverlay(Managed m)
        {
            if (m.Overlay != null && !m.Overlay.IsDisposed)
            {
                m.Overlay.SourceRect = m.Region;
                m.Overlay.SnapSize();
                m.Overlay.UpdateThumb();
                return;
            }
            OverlayForm o = new OverlayForm(m.Hwnd, m.Alpha, 0.42);
            o.SourceRect = m.Region;
            Managed owner = m;
            o.OnAlphaChanged = delegate(int pct)
            {
                owner.Alpha = pct;
                if (owner.Card != null) owner.Card.SyncAlpha(pct);
            };
            o.ClosedCallback = delegate(OverlayForm f)
            {
                if (owner.Overlay == f) owner.Overlay = null;
                if (owner.Card != null) owner.Card.UpdateModeUi();
            };
            o.OnNote = delegate(string s) { Status(s); };
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            o.Location = new Point(wa.Right - 640, wa.Bottom - 360);
            m.Overlay = o;
            o.Show();
        }

        void CloseOverlay(Managed m)
        {
            if (m.Overlay != null)
            {
                OverlayForm o = m.Overlay;
                m.Overlay = null;
                o.Close();
            }
        }

        void SetThrough(Managed m, bool on)
        {
            if (m.Overlay == null) return;
            m.Overlay.SetClickThrough(on);
            if (m.Card != null) m.Card.UpdateModeUi();
        }

        void ToggleThrough()
        {
            Managed t = PickTarget();
            if (t == null || t.Overlay == null) { Status("先给一个窗口开镜像浮窗，再切换透传"); return; }
            SetThrough(t, !t.Overlay.ClickThrough);
            Status("点击透传：" + (t.Overlay.ClickThrough ? "开" : "关"));
        }

        void PickRegion(Managed m)
        {
            if (!Nat.IsWindow(m.Hwnd)) { Remove(m); return; }
            if (!m.CloneMode) { Status("区域裁剪只对镜像浮窗有效"); return; }
            RegionForm rf = new RegionForm(m.Hwnd);
            rf.OnRegion = delegate(Rectangle r)
            {
                m.Region = r;
                if (m.Overlay != null && !m.Overlay.IsDisposed)
                {
                    m.Overlay.SourceRect = r;
                    m.Overlay.FitLocked = true;
                    m.Overlay.SnapSize();
                    m.Overlay.UpdateThumb();
                }
                if (m.Card != null) m.Card.UpdateModeUi();
                Status("已设置镜像区域 " + r.Width + "×" + r.Height);
            };
            rf.Show();
            rf.Activate();
        }

        // ---------- 自愈 + 跟随 ----------
        void HeartBeat()
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                Managed m = items[i];
                if (!Nat.IsWindow(m.Hwnd)) { Remove(m); continue; }

                // 样式自愈：只修复"分层样式被程序重置"，不动用户主动调过的 alpha
                if (!m.CloneMode && !m.Fading && m.Touched)
                {
                    int ex = Nat.GetWindowLongW(m.Hwnd, Nat.GWL_EXSTYLE);
                    if ((ex & Nat.WS_EX_LAYERED) == 0)
                    {
                        Nat.SetWindowLongW(m.Hwnd, Nat.GWL_EXSTYLE, ex | Nat.WS_EX_LAYERED);
                        Nat.SetLayeredWindowAttributes(m.Hwnd, 0, (byte)(255 * m.Alpha / 100), Nat.LWA_ALPHA);
                        Program.Log("SelfHeal hwnd=0x" + m.Hwnd.ToString("X"));
                    }
                }

                if (m.Overlay != null && !m.Overlay.IsDisposed && m.Overlay.FitLocked)
                {
                    SIZE s = m.Overlay.SourceSize();
                    int w = (int)(s.cx * m.Overlay.ScaleFactor);
                    int h = (int)(s.cy * m.Overlay.ScaleFactor);
                    if (w > 0 && h > 0 && (Math.Abs(w - m.Overlay.ClientSize.Width) > 2 || Math.Abs(h - m.Overlay.ClientSize.Height) > 2))
                    {
                        m.Overlay.ClientSize = new Size(Math.Max(80, w), Math.Max(60, h));
                        m.Overlay.UpdateThumb();
                    }
                }
            }
        }

        // ---------- 其他 ----------
        Managed Find(IntPtr h)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].Hwnd == h) return items[i];
            return null;
        }

        void Remove(Managed m)
        {
            CloseOverlay(m);
            RestoreAlpha(m);
            if (m.Card != null)
            {
                flow.Controls.Remove(m.Card);
                m.Card.Dispose();
                m.Card = null;
            }
            items.Remove(m);
            if (lastTarget == m) lastTarget = null;
        }

        void RestoreAll()
        {
            Program.Log("RestoreAll called");
            for (int i = items.Count - 1; i >= 0; i--) Remove(items[i]);
            Status("已全部恢复原样");
        }

        void OpenSettings()
        {
            SettingsForm sf = new SettingsForm(hotkeys, exclude);
            sf.ShowDialog(this);
            if (sf.Hotkeys == null) return;
            hotkeys = sf.Hotkeys;
            exclude = sf.Exclude;
            SaveCfg();
            ApplyHotkeys();
            Status("设置已保存");
        }

        void Status(string s) { lblStatus.Text = s; }

        void ShowMain()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        bool exiting = false;

        void ExitApp()
        {
            if (exiting) return;
            exiting = true;
            if (tray != null) tray.Visible = false;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                tray.ShowBalloonTip(1200, "WinTrans", "已最小化到托盘，热键仍可用。", ToolTipIcon.Info);
                return;
            }
            try { RestoreAll(); } catch { }
            try { UnregisterAll(); } catch { }
            if (tray != null) tray.Visible = false;
            base.OnFormClosing(e);
        }
    }

    static class Program
    {
        public static void Log(string s)
        {
            if (Environment.GetEnvironmentVariable("WINTRANS_LOG") == null) return;
            try
            {
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "wintrans.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\r\n");
            }
            catch { }
        }

        static bool ParseHwnd(string[] args, out IntPtr h)
        {
            h = IntPtr.Zero;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--hwnd" && i + 1 < args.Length)
                {
                    string v = args[i + 1];
                    h = (v.StartsWith("0x") || v.StartsWith("0X"))
                        ? (IntPtr)Convert.ToInt64(v.Substring(2), 16)
                        : (IntPtr)Convert.ToInt64(v);
                }
            }
            return h != IntPtr.Zero;
        }

        static int IntArg(string[] args, string name, int def)
        {
            for (int i = 0; i < args.Length; i++)
                if (args[i] == name && i + 1 < args.Length) return int.Parse(args[i + 1]);
            return def;
        }

        static Rectangle RectArg(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--region" && i + 1 < args.Length)
                {
                    string[] p = args[i + 1].Split(',');
                    if (p.Length == 4)
                        return new Rectangle(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]));
                }
            }
            return Rectangle.Empty;
        }

        static bool RunClone(string[] args)
        {
            IntPtr h;
            if (!ParseHwnd(args, out h) || !Nat.IsWindow(h)) return false;
            int pct = IntArg(args, "--alpha", 70);
            double scale = 0.42;
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--scale" && i + 1 < args.Length)
                    scale = double.Parse(args[i + 1]);
            OverlayForm o = new OverlayForm(h, pct, scale);
            o.SourceRect = RectArg(args);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            o.Location = new Point(wa.Right - 640, wa.Bottom - 360);
            Application.Run(o);
            return true;
        }

        static bool RunCli(string[] args)
        {
            IntPtr h;
            if (!ParseHwnd(args, out h) || !Nat.IsWindow(h)) return false;
            bool restore = false;
            for (int i = 0; i < args.Length; i++) if (args[i] == "--restore") restore = true;
            int ex = Nat.GetWindowLongW(h, Nat.GWL_EXSTYLE);
            if (restore)
            {
                Nat.SetLayeredWindowAttributes(h, 0, 255, Nat.LWA_ALPHA);
                Nat.SetWindowLongW(h, Nat.GWL_EXSTYLE, ex & ~Nat.WS_EX_LAYERED);
                return true;
            }
            int pct = IntArg(args, "--alpha", 70);
            if ((ex & Nat.WS_EX_LAYERED) == 0)
                Nat.SetWindowLongW(h, Nat.GWL_EXSTYLE, ex | Nat.WS_EX_LAYERED);
            Nat.SetLayeredWindowAttributes(h, 0, (byte)(255 * pct / 100), Nat.LWA_ALPHA);
            return true;
        }

        [STAThread]
        static void Main(string[] args)
        {
            try { Nat.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Log("UI EXCEPTION: " + e.Exception.GetType().Name + " " + e.Exception.Message
                    + " @ " + (e.Exception.StackTrace == null ? "" : e.Exception.StackTrace));
            };

            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--clone") { RunClone(args); return; }

            if (args.Length > 0 && (args[0] == "--alpha" || args[0] == "--restore"))
            {
                RunCli(args);
                return;
            }

            Application.Run(new MainForm());
        }
    }
}


