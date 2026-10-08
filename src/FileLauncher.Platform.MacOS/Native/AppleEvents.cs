using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace FileLauncher.Platform.MacOS.Native;

/// <summary>Apple Events の権限確認（SPEC §4.3「自動化」）。AE は CoreServices の中にある。</summary>
[SupportedOSPlatform("macos")]
internal static class AppleEvents
{
    private const string Lib = "/System/Library/Frameworks/CoreServices.framework/CoreServices";

    public const int noErr = 0;
    public const int errAEEventNotPermitted = -1743;
    public const int errAEEventWouldRequireUserConsent = -1744;
    public const int procNotFound = -600;

    private const uint typeApplicationBundleID = 0x62756E64; // 'bund'
    private const uint typeWildCard = 0x2A2A2A2A;            // '****'

    [StructLayout(LayoutKind.Sequential)]
    private struct AEDesc
    {
        public uint DescriptorType;
        public nint DataHandle;
    }

    [DllImport(Lib)] private static extern short AECreateDesc(uint typeCode, byte[] dataPtr, nint dataSize, out AEDesc result);
    [DllImport(Lib)] private static extern short AEDisposeDesc(ref AEDesc desc);
    [DllImport(Lib)] private static extern int AEDeterminePermissionToAutomateTarget(ref AEDesc target, uint eventClass, uint eventId, [MarshalAs(UnmanagedType.I1)] bool askUserIfNeeded);

    /// <summary>
    /// bundleId のアプリへ Apple Events を送ってよいか。ask = true なら未確認のとき OS の許可ダイアログを出して答えを待つ（呼び出しスレッドを止める）。
    /// 戻り値は OSStatus（noErr / errAEEventNotPermitted / errAEEventWouldRequireUserConsent / procNotFound など）。
    /// </summary>
    public static int DeterminePermission(string bundleId, bool ask)
    {
        byte[] data = Encoding.UTF8.GetBytes(bundleId);
        int err = AECreateDesc(typeApplicationBundleID, data, data.Length, out var desc);
        if (err != noErr) return err;
        try { return AEDeterminePermissionToAutomateTarget(ref desc, typeWildCard, typeWildCard, ask); }
        finally { AEDisposeDesc(ref desc); }
    }
}
