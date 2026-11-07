using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace PartagePropre;

/// <summary>
/// Une vraie fenêtre Windows, créée DANS le processus du jeu (donc Discord y associe le son du jeu).
/// Elle tourne sur son propre thread avec sa propre boucle de messages.
/// Fermer la fenêtre la cache seulement : /partage la rouvre.
/// </summary>
internal sealed unsafe class ShareWindow : IDisposable
{
    public const string Title = "FFXIV Partage (sans menus)";

    private const uint WM_CLOSE = 0x0010;
    private const uint WM_APP_SHOW = 0x8001;
    private const uint WM_APP_HIDE = 0x8002;
    private const uint WM_APP_QUIT = 0x8003;
    private const uint WM_APP_LAYOUT = 0x8004;
    private const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    private const uint WS_EX_APPWINDOW = 0x00040000;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const uint SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_BOTTOM = new(1);
    private const int SW_HIDE = 0;
    private const int SW_SHOWNOACTIVATE = 4;
    private const int CW_USEDEFAULT = unchecked((int)0x80000000);

    private readonly string className = "PartagePropre_" + Guid.NewGuid().ToString("N");
    private readonly WndProcDelegate wndProc;
    private readonly Thread thread;
    private readonly ManualResetEventSlim ready = new(false);
    private IntPtr hInstance;
    private volatile IntPtr hwnd;
    private volatile bool offScreen = true;
    private volatile bool hideTaskbar = true;
    private void* taskbar; // ITaskbarList (thread de la fenêtre)
    private int wantW = 1280, wantH = 720;

    public ShareWindow()
    {
        this.wndProc = this.WndProc;
        this.thread = new Thread(this.ThreadMain) { IsBackground = true, Name = "PartagePropre fenetre" };
        this.thread.Start();
        this.ready.Wait(5000);
    }

    public IntPtr Handle => this.hwnd;

    public bool IsVisible => this.hwnd != IntPtr.Zero && IsWindowVisible(this.hwnd) && !IsIconic(this.hwnd);

    public void Show() { if (this.hwnd != IntPtr.Zero) PostMessageW(this.hwnd, WM_APP_SHOW, IntPtr.Zero, IntPtr.Zero); }

    public void Hide() { if (this.hwnd != IntPtr.Zero) PostMessageW(this.hwnd, WM_APP_HIDE, IntPtr.Zero, IntPtr.Zero); }

    /// <summary>true = fenêtre placée hors de l'écran (invisible pour toi, Discord la voit quand même).</summary>
    public bool OffScreen
    {
        get => this.offScreen;
        set
        {
            this.offScreen = value;
            this.Relayout();
        }
    }

    /// <summary>Donne à la fenêtre exactement la résolution du jeu.</summary>
    public void EnsureSize(int w, int h)
    {
        if (w <= 0 || h <= 0 || (w == this.wantW && h == this.wantH)) return;
        this.wantW = w;
        this.wantH = h;
        this.Relayout();
    }

    /// <summary>true = pas de bouton dans la barre des tâches (Discord voit toujours la fenêtre).</summary>
    public bool HideTaskbarButton
    {
        get => this.hideTaskbar;
        set
        {
            this.hideTaskbar = value;
            this.Relayout();
        }
    }

    private void Relayout() { if (this.hwnd != IntPtr.Zero) PostMessageW(this.hwnd, WM_APP_LAYOUT, IntPtr.Zero, IntPtr.Zero); }

    private void Layout(IntPtr h)
    {
        int w = this.wantW, ht = this.wantH, x, y;
        if (this.offScreen)
        {
            // Complètement à gauche de tous les écrans : rien ne dépasse chez toi.
            x = GetSystemMetrics(SM_XVIRTUALSCREEN) - w - 500;
            y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        }
        else
        {
            x = 0; // coin de l'écran principal, derrière le jeu
            y = 0;
        }

        // La zone d'image (sans la barre de titre) = exactement la résolution du jeu.
        var r = new RECT { Left = 0, Top = 0, Right = w, Bottom = ht };
        AdjustWindowRectEx(ref r, WS_OVERLAPPEDWINDOW, false, WS_EX_APPWINDOW);
        int ow = r.Right - r.Left, oh = r.Bottom - r.Top;
        if (this.offScreen) x = GetSystemMetrics(SM_XVIRTUALSCREEN) - ow - 500;

        SetWindowPos(h, HWND_BOTTOM, x, y, ow, oh, SWP_NOACTIVATE);
        this.UpdateTaskbar(h);
    }

    // Enlève/remet le bouton de la barre des tâches via ITaskbarList, sans changer le style de la fenêtre
    // (si on changeait le style, Discord ne la listerait plus).
    private void UpdateTaskbar(IntPtr h)
    {
        if (this.taskbar == null)
        {
            CoInitializeEx(IntPtr.Zero, 2); // COINIT_APARTMENTTHREADED
            var clsid = new Guid("56FDF344-FD6D-11d0-958A-006097C9A090");
            var iid = new Guid("56FDF342-FD6D-11d0-958A-006097C9A090");
            void* tb = null;
            if (CoCreateInstance(&clsid, IntPtr.Zero, 1 /* CLSCTX_INPROC_SERVER */, &iid, &tb) < 0 || tb == null) return;
            ((delegate* unmanaged[Stdcall]<void*, int>)(*(void***)tb)[3])(tb); // HrInit
            this.taskbar = tb;
        }

        // AddTab = 4, DeleteTab = 5
        ((delegate* unmanaged[Stdcall]<void*, IntPtr, int>)(*(void***)this.taskbar)[this.hideTaskbar ? 5 : 4])(this.taskbar, h);
    }

    public void Toggle()
    {
        if (this.hwnd != IntPtr.Zero && IsWindowVisible(this.hwnd)) this.Hide();
        else this.Show();
    }

    private void ThreadMain()
    {
        try
        {
            this.hInstance = GetModuleHandleW(null);
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(this.wndProc),
                hInstance = this.hInstance,
                hCursor = LoadCursorW(IntPtr.Zero, (IntPtr)32512),
                hbrBackground = GetStockObject(4), // BLACK_BRUSH
                lpszClassName = this.className,
            };
            RegisterClassExW(ref wc);

            // Fenêtre normale (barre de titre) : Discord ne liste que ce genre de fenêtres.
            this.hwnd = CreateWindowExW(
                WS_EX_APPWINDOW, this.className, Title, WS_OVERLAPPEDWINDOW,
                0, 0, this.wantW, this.wantH,
                IntPtr.Zero, IntPtr.Zero, this.hInstance, IntPtr.Zero);
            if (this.hwnd != IntPtr.Zero) this.Layout(this.hwnd);
        }
        finally
        {
            this.ready.Set();
        }

        if (this.hwnd == IntPtr.Zero) return;

        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }

        if (this.taskbar != null)
        {
            ((delegate* unmanaged[Stdcall]<void*, uint>)(*(void***)this.taskbar)[2])(this.taskbar); // Release
            this.taskbar = null;
        }

        UnregisterClassW(this.className, this.hInstance);
    }

    private IntPtr WndProc(IntPtr h, uint msg, IntPtr w, IntPtr l)
    {
        switch (msg)
        {
            case WM_CLOSE:
                ShowWindow(h, SW_HIDE); // on cache au lieu de détruire
                return IntPtr.Zero;
            case WM_APP_SHOW:
                this.Layout(h);
                ShowWindow(h, SW_SHOWNOACTIVATE);
                this.Layout(h);
                return IntPtr.Zero;
            case WM_APP_HIDE:
                ShowWindow(h, SW_HIDE);
                return IntPtr.Zero;
            case WM_APP_LAYOUT:
                this.Layout(h);
                return IntPtr.Zero;
            case WM_APP_QUIT:
                DestroyWindow(h);
                PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return DefWindowProcW(h, msg, w, l);
    }

    public void Dispose()
    {
        var h = this.hwnd;
        if (h != IntPtr.Zero)
        {
            PostMessageW(h, WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero);
            this.thread.Join(3000);
        }

        this.hwnd = IntPtr.Zero;
        this.ready.Dispose();
    }

    // ---------- Win32 ----------
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
        public uint lPrivate;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool UnregisterClassW(string cls, IntPtr hInstance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string cls, string title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);

    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] private static extern bool AdjustWindowRectEx(ref RECT r, uint style, bool menu, uint exStyle);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] private static extern int GetMessageW(out MSG msg, IntPtr h, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessageW(ref MSG msg);
    [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursorW(IntPtr inst, IntPtr name);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint coinit);
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(Guid* clsid, IntPtr outer, uint ctx, Guid* iid, void** obj);
    [DllImport("gdi32.dll")] private static extern IntPtr GetStockObject(int i);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? name);
}
