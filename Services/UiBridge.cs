using System.Text.Json;
using System.Text.Json.Serialization;
using ElectronNET.API;

namespace ZoroPlay.Services;

/// <summary>C# -> renderer via Electron.IpcMain.Send.</summary>
public sealed class UiBridge
{
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web)
    { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>To every open window.</summary>
    public void Send(string channel, object payload)
    {
        if (!HybridSupport.IsElectronActive) return;
        var json = JsonSerializer.Serialize(payload, Opts);
        foreach (var w in Electron.WindowManager.BrowserWindows.ToArray()) Push(w, channel, json);
    }

    /// <summary>To one window.</summary>
    public void SendTo(BrowserWindow window, string channel, object payload)
    {
        if (!HybridSupport.IsElectronActive) return;
        Push(window, channel, JsonSerializer.Serialize(payload, Opts));
    }

    private static void Push(BrowserWindow w, string channel, string json)
    {
        try { Electron.IpcMain.Send(w, channel, json); }
        catch (Exception ex) { FileLog.Write($"send '{channel}' failed: {ex.Message}"); }
    }
}
