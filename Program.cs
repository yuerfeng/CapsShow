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

    void OnCapsLockPressed(object? sender, EventArgs e)
    {
        var current = Control.IsKeyLocked(Keys.CapsLock);
        var newState = !current;
        var toast = new ToastForm(newState);
        toast.Show();
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

    public ToastForm(bool capsOn)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.White;
        Opacity = 0.8;
        Size = new Size(300, 200);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };

        table.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        var picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        var logoPath = Path.Combine(AppContext.BaseDirectory, "logo.png");

        if (File.Exists(logoPath))
        {
            using var img = Image.FromFile(logoPath);
            picture.Image = new Bitmap(img);
        }

        var label = new Label
        {
            ForeColor = Color.Black,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Text = capsOn ? "当前状态：大写" : "当前状态：小写",
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        table.Controls.Add(picture, 0, 0);
        table.Controls.Add(label, 0, 1);

        Controls.Add(table);

        Region = new Region(CreateRoundRectangle(new Rectangle(0, 0, Width, Height), 24));

        timer = new System.Windows.Forms.Timer
        {
            Interval = 3000
        };

        timer.Tick += OnTimerTick;
        Shown += OnShown;
    }

    void OnShown(object? sender, EventArgs e)
    {
        PositionWindow();
        timer.Start();
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
