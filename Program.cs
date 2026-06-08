using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;

namespace CapsShow;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new CapsApplicationContext());
    }
}

class CapsApplicationContext : ApplicationContext
{
    readonly KeyboardHook hook;
    readonly NotifyIcon notifyIcon;
    readonly ContextMenuStrip menu;

    public CapsApplicationContext()
    {
        hook = new KeyboardHook();
        hook.CapsLockPressed += OnCapsLockPressed;

        menu = new ContextMenuStrip();
        var exitItem = new ToolStripMenuItem("退出(&E)");
        exitItem.Click += OnExitClick;
        menu.Items.Add(exitItem);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "main.ico");
        Icon icon;

        if (File.Exists(iconPath))
        {
            using var bmp = new Bitmap(iconPath);
            var handle = bmp.GetHicon();
            icon = Icon.FromHandle(handle);
        }
        else
        {
            icon = SystemIcons.Information;
        }

        notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Visible = true,
            Text = "CapsShow",
            ContextMenuStrip = menu
        };
    }

    void OnExitClick(object? sender, EventArgs e)
    {
        ExitThread();
    }

    ToastForm? currentToast;

    void OnCapsLockPressed(object? sender, EventArgs e)
    {
        var current = Control.IsKeyLocked(Keys.CapsLock);
        var newState = !current;

        if (currentToast is { IsDisposed: false })
        {
            currentToast.UpdateState(newState);
        }
        else
        {
            currentToast = new ToastForm(newState);
            currentToast.Show();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            hook.CapsLockPressed -= OnCapsLockPressed;
            hook.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            menu.Dispose();
        }

        base.Dispose(disposing);
    }
}

class KeyboardHook : IDisposable
{
    const int WhKeyboardLl = 13;
    const int WmKeyDown = 0x0100;
    const int WmSysKeyDown = 0x0104;
    const int VkCapital = 0x14;

    IntPtr hookId;
    LowLevelKeyboardProc hookProc;

    public event EventHandler? CapsLockPressed;

    delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    public KeyboardHook()
    {
        hookProc = HookCallback;
        hookId = SetHook(hookProc);
    }

    IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        var moduleHandle = curModule is not null
            ? GetModuleHandle(curModule.ModuleName)
            : IntPtr.Zero;
        return SetWindowsHookEx(WhKeyboardLl, proc, moduleHandle, 0);
    }

    IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();

            if (msg == WmKeyDown || msg == WmSysKeyDown)
            {
                var vkCode = Marshal.ReadInt32(lParam);

                if (vkCode == VkCapital)
                {
                    CapsLockPressed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        return CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hookId);
            hookId = IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr GetModuleHandle(string lpModuleName);
}

class ToastForm : Form
{
    readonly System.Windows.Forms.Timer timer;
    readonly Image? logo;
    readonly int drawLogoW, drawLogoH, formW, formH;
    Bitmap? formBitmap;
    bool currentCapsOn;

    const int WsExLayered = 0x00080000;
    const int AcSrcOver = 0x00;
    const int AcSrcAlpha = 0x01;
    const int UlwAlpha = 0x02;

    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    struct BLENDFUNCTION
    {
        public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
    }

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
        ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
        int crKey, ref BLENDFUNCTION pblend, int dwFlags);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObj);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DeleteObject(IntPtr hObj);

    public ToastForm(bool capsOn)
    {
        currentCapsOn = capsOn;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;

        // 加载 logo
        var logoPath = Path.Combine(AppContext.BaseDirectory, "logo.png");
        if (File.Exists(logoPath))
        {
            using var img = Image.FromFile(logoPath);
            logo = new Bitmap(img);
        }

        // 根据 logo 大小决定窗口尺寸
        int logoW = logo?.Width ?? 128;
        int logoH = logo?.Height ?? 128;
        float scale = Math.Min(280f / logoW, 180f / logoH);
        drawLogoW = (int)(logoW * scale);
        drawLogoH = (int)(logoH * scale);
        formW = drawLogoW + 80;
        formH = drawLogoH + 100;
        Size = new Size(formW, formH);

        timer = new System.Windows.Forms.Timer { Interval = 3000 };
        timer.Tick += OnTimerTick;
        Shown += OnShown;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExLayered;
            return cp;
        }
    }

    void OnShown(object? sender, EventArgs e)
    {
        PositionWindow();
        RedrawAndUpdate();
        timer.Start();
    }

    public void UpdateState(bool capsOn)
    {
        currentCapsOn = capsOn;
        RedrawAndUpdate();
        timer.Stop();
        timer.Start(); // 重置计时器
    }

    void RedrawAndUpdate()
    {
        formBitmap?.Dispose();
        formBitmap = new Bitmap(formW, formH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(formBitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // 半透明圆角浅色背景
        using (var bgBrush = new SolidBrush(Color.FromArgb(220, 245, 245, 245)))
        {
            using var rrPath = CreateRoundRectangle(new Rectangle(0, 0, formW, formH), 24);
            g.FillPath(bgBrush, rrPath);
        }

        // 绘制 logo（居中偏上）
        if (logo != null)
        {
            int logoX = (formW - drawLogoW) / 2;
            int logoY = 20;
            g.DrawImage(logo, logoX, logoY, drawLogoW, drawLogoH);
        }

        // 绘制文字
        var text = currentCapsOn ? "当前状态：大写" : "当前状态：小写";
        using var font = new Font("Segoe UI", 12, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(50, 50, 50));
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var textRect = new RectangleF(0, drawLogoH + 20, formW, formH - drawLogoH - 30);
        g.DrawString(text, font, textBrush, textRect, sf);

        UpdateLayeredWindowBitmap();
    }

    void UpdateLayeredWindowBitmap()
    {
        if (formBitmap == null) return;

        var screen = Graphics.FromHwnd(IntPtr.Zero);
        var hdcScreen = screen.GetHdc();
        var memDc = CreateCompatibleDC(hdcScreen);
        var hBitmap = formBitmap.GetHbitmap(Color.FromArgb(0));
        var oldBitmap = SelectObject(memDc, hBitmap);

        var ptDst = new POINT { X = Left, Y = Top };
        var sz = new SIZE { cx = formBitmap.Width, cy = formBitmap.Height };
        var ptSrc = new POINT { X = 0, Y = 0 };
        var blend = new BLENDFUNCTION
        {
            BlendOp = (byte)AcSrcOver,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = (byte)AcSrcAlpha
        };

        UpdateLayeredWindow(Handle, hdcScreen, ref ptDst, ref sz, memDc, ref ptSrc, 0, ref blend, UlwAlpha);

        SelectObject(memDc, oldBitmap);
        DeleteObject(hBitmap);
        DeleteDC(memDc);
        screen.ReleaseHdc(hdcScreen);
        screen.Dispose();
    }

    void PositionWindow()
    {
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? SystemInformation.WorkingArea;
        Location = new Point(
            workingArea.Left + (workingArea.Width - Width) / 2,
            workingArea.Top + (workingArea.Height - Height) / 2
        );
    }

    void OnTimerTick(object? sender, EventArgs e)
    {
        timer.Stop();
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            formBitmap?.Dispose();
            logo?.Dispose();
        }
        base.Dispose(disposing);
    }

    static GraphicsPath CreateRoundRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }
}
