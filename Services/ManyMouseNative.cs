using System.Runtime.InteropServices;

namespace ZoroPlay.Services;

internal static class ManyMouseNative
{
    public const int EV_ABS = 0, EV_REL = 1, EV_BUTTON = 2, EV_SCROLL = 3, EV_DISCONNECT = 4, EV_CONNECT = 5;
    private const string DLL = "manymouse";
    private static string? _located;

    static ManyMouseNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(ManyMouseNative).Assembly, (name, asm, searchPath) =>
        {
            if (name != DLL) return IntPtr.Zero;
            var full = Locate() ?? throw new DllNotFoundException("manymouse.dll not found in any search location (see app.log)");
            try { return NativeLibrary.Load(full); }
            catch (Exception ex) { FileLog.Write("Load failed: " + ex.Message); throw; }
        });
    }

    /// <summary>Finds manymouse.dll: next to the exe, then the dev layout (project/native).</summary>
    public static string? Locate()
    {
        if (_located != null) return _located;
        var tried = new List<string>();
        foreach (var c in Candidates())
        {
            tried.Add(c);
            if (File.Exists(c)) { FileLog.Write("manymouse.dll found: " + c); return _located = c; }
        }
        FileLog.Write("manymouse.dll NOT found. Tried:\n    " + string.Join("\n    ", tried));
        return null;
    }

    private static IEnumerable<string> Candidates()
    {
        yield return System.IO.Path.Combine(AppContext.BaseDirectory, "manymouse.dll");
        if (Environment.ProcessPath is { } p)
            yield return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(p)!, "manymouse.dll");
        yield return System.IO.Path.Combine(Environment.CurrentDirectory, "manymouse.dll");
        yield return System.IO.Path.Combine(Environment.CurrentDirectory, "native", "manymouse.dll");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);       // dev: obj/Host/bin -> <project>/native
        for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
            yield return System.IO.Path.Combine(dir.FullName, "native", "manymouse.dll");
    }

    [DllImport(DLL)] public static extern int ManyMouseDLL_Init();
    [DllImport(DLL)] public static extern void ManyMouseDLL_Quit();
    [DllImport(DLL)] public static extern int ManyMouseDLL_PollEvent(ref MM_Event ev);
    [DllImport(DLL)] public static extern IntPtr ManyMouseDLL_DeviceName(int device);
    [DllImport(DLL)] public static extern IntPtr ManyMouseDLL_DevicePath(int device);

    [StructLayout(LayoutKind.Sequential)]
    public struct MM_Event { public int type; public int device; public int item; public int value; }

    public static string? Str(IntPtr p) => p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
    public static string? Name(int d) => Str(ManyMouseDLL_DeviceName(d));
    public static string? Path(int d) => Str(ManyMouseDLL_DevicePath(d));
}
