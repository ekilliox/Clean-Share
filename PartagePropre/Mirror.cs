using System;
using System.Runtime.InteropServices;

namespace PartagePropre;

/// <summary>
/// Copie l'image du jeu (avant que Dalamud y dessine ses menus) vers la fenêtre de partage.
/// Appels DirectX "à la main" (vtables COM) pour ne dépendre d'aucune autre bibliothèque.
/// Tout se passe sur le thread de rendu, pendant l'événement Draw de Dalamud.
/// </summary>
internal sealed unsafe class Mirror : IDisposable
{
    private static readonly Guid IID_ID3D11Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
    private static readonly Guid IID_ID3D11Device = new("db6f6ddb-ac77-4e88-8253-819df9bbf140");
    private static readonly Guid IID_IDXGIDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
    private static readonly Guid IID_IDXGIFactory2 = new("50c83a1c-e072-4c48-87b0-3630fa36a6d0");

    private const uint DXGI_USAGE_RENDER_TARGET_OUTPUT = 0x20;
    private const int DXGI_SWAP_EFFECT_DISCARD = 0;
    private const int DXGI_SWAP_EFFECT_FLIP_DISCARD = 4;

    private void* device;      // ID3D11Device (celui du jeu)
    private void* context;     // ID3D11DeviceContext (immédiat)
    private void* swapChain;   // IDXGISwapChain1 (le nôtre)
    private IntPtr swapHwnd;
    private uint width, height;
    private int format;
    private bool broken;

    public string? LastError { get; private set; }

    /// <summary>Résolution du jeu vue à la dernière image.</summary>
    public int GameWidth { get; private set; }

    public int GameHeight { get; private set; }

    public void ClearError()
    {
        this.broken = false;
        this.LastError = null;
    }

    /// <summary>Appelé à chaque image. gameSwapChain = IDXGISwapChain* du jeu.</summary>
    public void Frame(void* gameSwapChain, IntPtr hwnd)
    {
        if (this.broken || gameSwapChain == null || hwnd == IntPtr.Zero) return;

        void* back = null;
        void* mine = null;
        try
        {
            // IDXGISwapChain::GetBuffer (9)
            var iid = IID_ID3D11Texture2D;
            if (Fail(VT<GetBufferFn>(gameSwapChain, 9)(gameSwapChain, 0, &iid, &back), "GetBuffer jeu")) return;

            D3D11_TEXTURE2D_DESC desc;
            VT<GetDescFn>(back, 10)(back, &desc); // ID3D11Texture2D::GetDesc (10)
            this.GameWidth = (int)desc.Width;
            this.GameHeight = (int)desc.Height;

            if (this.device == null && !this.InitDevice(gameSwapChain)) return;

            if (this.swapChain == null || this.swapHwnd != hwnd)
            {
                this.ReleaseSwapChain();
                if (!this.CreateSwapChain(hwnd, desc))
                {
                    this.broken = true; // on réessaiera quand la fenêtre sera rouverte (/partage)
                    return;
                }
            }
            else if (desc.Width != this.width || desc.Height != this.height || MapFormat(desc.Format, out _) != this.format)
            {
                // IDXGISwapChain::ResizeBuffers (13)
                var fmt = MapFormat(desc.Format, out _);
                if (Fail(VT<ResizeBuffersFn>(this.swapChain, 13)(this.swapChain, 0, desc.Width, desc.Height, fmt, 0), "ResizeBuffers"))
                {
                    this.ReleaseSwapChain();
                    return;
                }

                this.width = desc.Width;
                this.height = desc.Height;
                this.format = fmt;
            }

            if (Fail(VT<GetBufferFn>(this.swapChain, 9)(this.swapChain, 0, &iid, &mine), "GetBuffer partage")) return;

            if (desc.SampleCount > 1)
                VT<ResolveFn>(this.context, 57)(this.context, mine, 0, back, 0, this.format); // ResolveSubresource
            else
                VT<CopyResourceFn>(this.context, 47)(this.context, mine, back);                 // CopyResource

            Release(ref mine);

            // IDXGISwapChain::Present (8), sans attendre la synchro écran
            VT<PresentFn>(this.swapChain, 8)(this.swapChain, 0, 0);
        }
        finally
        {
            Release(ref mine);
            Release(ref back);
        }
    }

    private bool InitDevice(void* gameSwapChain)
    {
        void* dev = null;
        var iid = IID_ID3D11Device;
        // IDXGIDeviceSubObject::GetDevice (7)
        if (Fail(VT<GetBufferLikeFn>(gameSwapChain, 7)(gameSwapChain, &iid, &dev), "GetDevice")) { this.broken = true; return false; }
        this.device = dev;

        void* ctx = null;
        VT<GetImmediateContextFn>(this.device, 40)(this.device, &ctx); // ID3D11Device::GetImmediateContext (40)
        this.context = ctx;
        return this.context != null;
    }

    private bool CreateSwapChain(IntPtr hwnd, D3D11_TEXTURE2D_DESC src)
    {
        void* dxgiDevice = null, adapter = null, factory = null;
        try
        {
            var iid = IID_IDXGIDevice;
            if (Fail(QI(this.device, iid, &dxgiDevice), "QI IDXGIDevice")) return false;
            // IDXGIDevice::GetAdapter (7)
            if (Fail(VT<GetAdapterFn>(dxgiDevice, 7)(dxgiDevice, &adapter), "GetAdapter")) return false;
            var iidF = IID_IDXGIFactory2;
            // IDXGIObject::GetParent (6)
            if (Fail(VT<GetBufferLikeFn>(adapter, 6)(adapter, &iidF, &factory), "GetParent Factory2")) return false;

            var fmt = MapFormat(src.Format, out var flipOk);
            var d = new DXGI_SWAP_CHAIN_DESC1
            {
                Width = src.Width,
                Height = src.Height,
                Format = fmt,
                SampleCount = 1,
                BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
                BufferCount = flipOk ? 2u : 1u,
                Scaling = 0, // STRETCH : l'image s'adapte à la taille de la fenêtre
                SwapEffect = flipOk ? DXGI_SWAP_EFFECT_FLIP_DISCARD : DXGI_SWAP_EFFECT_DISCARD,
            };

            void* sc = null;
            // IDXGIFactory2::CreateSwapChainForHwnd (15)
            if (Fail(VT<CreateSwapChainForHwndFn>(factory, 15)(factory, this.device, hwnd, &d, null, null, &sc), "CreateSwapChainForHwnd")) return false;

            // IDXGIFactory::MakeWindowAssociation (8) : pas d'Alt+Entrée sur notre fenêtre
            VT<MakeWindowAssociationFn>(factory, 8)(factory, hwnd, 3);

            this.swapChain = sc;
            this.swapHwnd = hwnd;
            this.width = src.Width;
            this.height = src.Height;
            this.format = fmt;
            return true;
        }
        finally
        {
            Release(ref factory);
            Release(ref adapter);
            Release(ref dxgiDevice);
        }
    }

    /// <summary>Format du jeu -> format accepté par une swapchain "flip" (sinon on garde le format et on passe en mode classique).</summary>
    private static int MapFormat(uint f, out bool flipOk)
    {
        flipOk = true;
        switch (f)
        {
            case 27: case 28: case 29: return 28;   // R8G8B8A8 (typeless/unorm/srgb)
            case 87: case 90: case 91: return 87;   // B8G8R8A8
            case 23: case 24: return 24;            // R10G10B10A2
            case 9: case 10: return 10;             // R16G16B16A16_FLOAT
            default: flipOk = false; return (int)f;
        }
    }

    private bool Fail(int hr, string what)
    {
        if (hr >= 0) return false;
        this.LastError = Loc.Failed(what, hr);
        return true;
    }

    private void ReleaseSwapChain()
    {
        Release(ref this.swapChain);
        this.swapHwnd = IntPtr.Zero;
    }

    public void Dispose()
    {
        this.ReleaseSwapChain();
        Release(ref this.context);
        Release(ref this.device);
    }

    // ---------- COM brut ----------
    private static T VT<T>(void* obj, int index) where T : Delegate
        => Marshal.GetDelegateForFunctionPointer<T>((*(IntPtr**)obj)[index]);

    private static int QI(void* obj, Guid iid, void** outp) => VT<GetBufferLikeFn>(obj, 0)(obj, &iid, outp);

    private static void Release(ref void* obj)
    {
        if (obj == null) return;
        VT<ReleaseFn>(obj, 2)(obj);
        obj = null;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate uint ReleaseFn(void* self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBufferFn(void* self, uint buffer, Guid* riid, void** surface);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBufferLikeFn(void* self, Guid* riid, void** outp);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void GetDescFn(void* self, D3D11_TEXTURE2D_DESC* desc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ResizeBuffersFn(void* self, uint count, uint w, uint h, int fmt, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void ResolveFn(void* self, void* dst, uint dstSub, void* src, uint srcSub, int fmt);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void CopyResourceFn(void* self, void* dst, void* src);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int PresentFn(void* self, uint sync, uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate void GetImmediateContextFn(void* self, void** ctx);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetAdapterFn(void* self, void** adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CreateSwapChainForHwndFn(void* self, void* device, IntPtr hwnd, DXGI_SWAP_CHAIN_DESC1* desc, void* fullscreenDesc, void* restrictOutput, void** swapChain);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int MakeWindowAssociationFn(void* self, IntPtr hwnd, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct D3D11_TEXTURE2D_DESC
    {
        public uint Width, Height, MipLevels, ArraySize, Format, SampleCount, SampleQuality, Usage, BindFlags, CPUAccessFlags, MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_SWAP_CHAIN_DESC1
    {
        public uint Width, Height;
        public int Format;
        public int Stereo;
        public uint SampleCount, SampleQuality;
        public uint BufferUsage, BufferCount;
        public int Scaling, SwapEffect, AlphaMode;
        public uint Flags;
    }
}
