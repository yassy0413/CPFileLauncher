using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

namespace IconSpike.Native;

/// <summary>32bpp BGRA（上から下）のピクセル。Premultiplied は alpha が乗算済みかどうか。</summary>
public sealed record IconPixels(int Width, int Height, byte[] Bgra, bool Premultiplied, string Note);

[Flags]
public enum SIIGBF
{
    ResizeToFit = 0x00,
    BiggerSizeOk = 0x01,
    MemoryOnly = 0x02,
    IconOnly = 0x04,
    ThumbnailOnly = 0x08,
    InCacheOnly = 0x10,
    ScaleUp = 0x100,
}

/// <summary>
/// Windows のアイコン取得。製品では IIconProvider の Windows 実装に入る部分。
/// 方式 A: IShellItemImageFactory.GetImage — 任意サイズ、サムネイル可、alpha 付き HBITMAP。実在するパス（と ::{GUID} 等のシェル名）のみ。
/// 方式 B: SHGetFileInfo + システムイメージリスト — 16/32/48/256 の固定サイズ。USEFILEATTRIBUTES で存在しないパスも拡張子から取れる。
/// どちらも COM を使うので STA スレッドから呼ぶこと。
/// </summary>
[SupportedOSPlatform("windows")]
public static class ShellIcons
{
    // ---------------- 方式 A: IShellItemImageFactory ----------------

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, [In] ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    public static IconPixels FromImageFactory(string parsingName, int px, SIIGBF flags)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out var factory);
        try
        {
            int hr = factory.GetImage(new SIZE { cx = px, cy = px }, flags, out var hbm);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            try
            {
                var p = ReadHBitmap(hbm) ?? throw new InvalidOperationException("GetDIBits failed");
                // サムネイル（JPEG 等）は alpha が全部 0 で返ることがある → 不透明扱いにする
                bool allZero = FixAllZeroAlpha(p.Bgra);
                // GetImage の HBITMAP は乗算済み alpha（AlphaBlend 用 DIB）とされる。画面で縁の白/黒にじみを目視確認する
                return p with { Premultiplied = true, Note = allZero ? "alpha 全 0 → 不透明化" : "" };
            }
            finally { DeleteObject(hbm); }
        }
        finally { Marshal.ReleaseComObject(factory); }
    }

    // ---------------- 方式 B: SHGetFileInfo + システムイメージリスト ----------------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint attrs, ref SHFILEINFO psfi, uint cb, uint flags);

    [DllImport("shell32.dll", PreserveSig = false)]
    private static extern void SHGetImageList(int iImageList, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IImageList ppv);

    // IImageList: GetIcon は vtable の 8 番目。手前のメソッドは呼ばないので並び合わせのダミー
    [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add();
        [PreserveSig] int ReplaceIcon();
        [PreserveSig] int SetOverlayImage();
        [PreserveSig] int Replace();
        [PreserveSig] int AddMasked();
        [PreserveSig] int Draw();
        [PreserveSig] int Remove();
        [PreserveSig] int GetIcon(int i, int flags, out IntPtr picon);
    }

    private const uint SHGFI_SYSICONINDEX = 0x4000;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const int SHIL_LARGE = 0;      // 32px
    private const int SHIL_SMALL = 1;      // 16px
    private const int SHIL_EXTRALARGE = 2; // 48px
    private const int SHIL_JUMBO = 4;      // 256px
    private const int ILD_TRANSPARENT = 1;

    /// <summary>要求サイズ以上で最小のイメージリストを選ぶ。exists=false なら拡張子だけで引く。</summary>
    public static IconPixels FromSystemImageList(string path, int px, bool exists, bool isDirectory)
    {
        var info = new SHFILEINFO();
        uint flags = SHGFI_SYSICONINDEX;
        uint attrs = 0;
        if (!exists)
        {
            flags |= SHGFI_USEFILEATTRIBUTES;
            attrs = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
        }
        if (SHGetFileInfo(path, attrs, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags) == IntPtr.Zero)
            throw new InvalidOperationException("SHGetFileInfo failed");

        int list = px <= 16 ? SHIL_SMALL : px <= 32 ? SHIL_LARGE : px <= 48 ? SHIL_EXTRALARGE : SHIL_JUMBO;
        var iid = typeof(IImageList).GUID;
        SHGetImageList(list, ref iid, out var il);
        try
        {
            int hr = il.GetIcon(info.iIcon, ILD_TRANSPARENT, out var hIcon);
            if (hr < 0) Marshal.ThrowExceptionForHR(hr);
            try
            {
                var p = ReadHIcon(hIcon);
                string listName = list switch { SHIL_SMALL => "16", SHIL_LARGE => "32", SHIL_EXTRALARGE => "48", _ => "256(jumbo)" };
                return p with { Note = $"list={listName} {p.Note}".Trim() };
            }
            finally { DestroyIcon(hIcon); }
        }
        finally { Marshal.ReleaseComObject(il); }
    }

    // ---------------- .lnk の解決 ----------------

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
    }

    /// <summary>Target が空でも ParsingName（ID リストから得たシェル名。例 "::{GUID}"）があればアイコンを引ける。</summary>
    public sealed record LinkInfo(string Target, string ParsingName, string Arguments, string WorkingDirectory, string IconLocation);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetNameFromIDList(IntPtr pidl, uint sigdnName, out IntPtr ppszName);

    private const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;

    /// <summary>
    /// .lnk を読む（Resolve はしない＝ディスク探索・UI を出さない）。
    /// ファイルではなくシェル項目（エクスプローラー、PC 等）を指すリンクは GetPath が空になるので、ID リストからシェル名を取る。
    /// </summary>
    public static LinkInfo ReadLink(string lnkPath)
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            ((IPersistFile)link).Load(lnkPath, 0);
            var sb = new StringBuilder(1024);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
            string target = sb.ToString();

            string parsingName = "";
            link.GetIDList(out var pidl);
            if (pidl != IntPtr.Zero)
            {
                try
                {
                    if (SHGetNameFromIDList(pidl, SIGDN_DESKTOPABSOLUTEPARSING, out var psz) == 0)
                    {
                        parsingName = Marshal.PtrToStringUni(psz) ?? "";
                        Marshal.FreeCoTaskMem(psz);
                    }
                }
                finally { Marshal.FreeCoTaskMem(pidl); }
            }
            sb.Clear(); link.GetArguments(sb, sb.Capacity);
            string args = sb.ToString();
            sb.Clear(); link.GetWorkingDirectory(sb, sb.Capacity);
            string wd = sb.ToString();
            sb.Clear(); link.GetIconLocation(sb, sb.Capacity, out int iconIndex);
            string icon = sb.Length > 0 ? $"{sb},{iconIndex}" : "";
            return new LinkInfo(Environment.ExpandEnvironmentVariables(target), parsingName, args, wd, icon);
        }
        finally { Marshal.ReleaseComObject(link); }
    }

    // ---------------- URL → 既定ブラウザ ----------------

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(int flags, int str, string pszAssoc, string? pszExtra, [Out] StringBuilder? pszOut, ref uint pcchOut);

    private const int ASSOCSTR_EXECUTABLE = 2;

    /// <summary>スキーム（http 等）に関連付けられた実行ファイル。URL アイテムのアイコンに使う。</summary>
    public static string? DefaultHandlerExe(string scheme)
    {
        uint len = 1024;
        var sb = new StringBuilder((int)len);
        int hr = AssocQueryString(0, ASSOCSTR_EXECUTABLE, scheme, "open", sb, ref len);
        return hr == 0 ? sb.ToString() : null;
    }

    // ---------------- GDI: HBITMAP / HICON → BGRA ----------------

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType, bmWidth, bmHeight, bmWidthBytes;
        public ushort bmPlanes, bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
        // カラーテーブル分の余白（32bpp BI_RGB では使われないが GetDIBits が書き込んでも溢れないように）
        public uint c0, c1, c2, c3;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public int xHotspot, yHotspot;
        public IntPtr hbmMask, hbmColor;
    }

    [DllImport("gdi32.dll")] private static extern int GetObject(IntPtr h, int c, out BITMAP bm);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, [Out] byte[] bits, ref BITMAPINFO bmi, uint usage);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);

    private static IconPixels? ReadHBitmap(IntPtr hbm)
    {
        if (GetObject(hbm, Marshal.SizeOf<BITMAP>(), out var bm) == 0) return null;
        int w = bm.bmWidth, h = Math.Abs(bm.bmHeight);
        var bmi = new BITMAPINFO
        {
            biSize = 40, biWidth = w, biHeight = -h, // 負 = トップダウン
            biPlanes = 1, biBitCount = 32, biCompression = 0,
        };
        var buf = new byte[w * h * 4];
        IntPtr hdc = GetDC(IntPtr.Zero);
        try
        {
            if (GetDIBits(hdc, hbm, 0, (uint)h, buf, ref bmi, 0) == 0) return null;
        }
        finally { ReleaseDC(IntPtr.Zero, hdc); }
        return new IconPixels(w, h, buf, false, $"src {bm.bmBitsPixel}bpp");
    }

    /// <summary>HICON → BGRA（straight alpha）。alpha を持たない古いアイコンはマスクから alpha を作る。</summary>
    private static IconPixels ReadHIcon(IntPtr hIcon)
    {
        if (!GetIconInfo(hIcon, out var ii)) throw new InvalidOperationException("GetIconInfo failed");
        try
        {
            var color = ReadHBitmap(ii.hbmColor) ?? throw new InvalidOperationException("color bitmap read failed");
            bool hasAlpha = false;
            for (int i = 3; i < color.Bgra.Length; i += 4) if (color.Bgra[i] != 0) { hasAlpha = true; break; }
            if (hasAlpha) return color with { Note = "alpha" };

            var mask = ReadHBitmap(ii.hbmMask);
            if (mask is null || mask.Width != color.Width || mask.Height != color.Height)
            {
                FixAllZeroAlpha(color.Bgra);
                return color with { Note = "mask 読めず → 不透明" };
            }
            // マスク: 黒(0)=不透明 / 白=透明
            for (int i = 0; i < color.Bgra.Length; i += 4)
                color.Bgra[i + 3] = mask.Bgra[i] == 0 ? (byte)255 : (byte)0;
            return color with { Note = "mask→alpha" };
        }
        finally
        {
            if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
            if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
        }
    }

    private static bool FixAllZeroAlpha(byte[] bgra)
    {
        for (int i = 3; i < bgra.Length; i += 4) if (bgra[i] != 0) return false;
        for (int i = 3; i < bgra.Length; i += 4) bgra[i] = 255;
        return true;
    }
}
