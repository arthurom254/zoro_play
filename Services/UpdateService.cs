using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ElectronNET.API;

namespace ZoroPlay.Services;

/// <summary>Polls update.php?action=latest, tells the UI, and installs on request.</summary>
public sealed class UpdateService : BackgroundService
{
    private sealed record Latest(string Version, string DownloadUrl, string? Sha256, long? Size);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(15) };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string? _url;
    private readonly UiBridge _ui;
    private readonly ILogger<UpdateService> _log;
    private Latest? _available;
    private int _installing;

    public UpdateService(IConfiguration cfg, UiBridge ui, ILogger<UpdateService> log)
    { _url = cfg["Update:Url"]; _ui = ui; _log = log; }

    private static Version Norm(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
    public static Version CurrentVersion =>
        Norm(Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0));

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), ct);
            while (!ct.IsCancellationRequested)
            {
                await CheckAsync(ct);
                await Task.Delay(TimeSpan.FromHours(6), ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_url)) return;
        try
        {
            var url = _url + (_url.Contains('?') ? "&" : "?") + "action=latest";
            var json = await Http.GetStringAsync(url, ct);
            var latest = JsonSerializer.Deserialize<Latest>(json, Json);
            if (latest is null || !Version.TryParse(latest.Version, out var remote)) return;
            if (Norm(remote) > CurrentVersion) { _available = latest; PushState(); }
        }
        catch (Exception ex) { _log.LogWarning("Update check failed: {m}", ex.Message); }
    }

    public void PushState()
    {
        if (_available is { } a)
            _ui.Send("update-available", new { version = a.Version, current = CurrentVersion.ToString(3) });
    }

    public async Task InstallAsync()
    {
        if (_available is not { } a || Interlocked.Exchange(ref _installing, 1) == 1) return;
        try
        {
            var file = Path.GetFileName(new Uri(a.DownloadUrl).LocalPath);
            if (!file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Bad update file.");
            var target = Path.Combine(Path.GetTempPath(), file);

            using var resp = await Http.GetAsync(a.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? a.Size ?? 0;

            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var src = await resp.Content.ReadAsStreamAsync())
            await using (var dst = File.Create(target))
            {
                var buf = new byte[81920]; long done = 0; int lastPct = -1, n;
                while ((n = await src.ReadAsync(buf)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n));
                    sha.AppendData(buf, 0, n);
                    done += n;
                    int pct = total > 0 ? (int)(done * 100 / total) : 0;
                    if (pct != lastPct) { lastPct = pct; _ui.Send("update-progress", new { percent = pct }); }
                }
            }

            if (!string.IsNullOrEmpty(a.Sha256) &&
                !Convert.ToHexString(sha.GetHashAndReset()).Equals(a.Sha256, StringComparison.OrdinalIgnoreCase))
            { File.Delete(target); throw new InvalidOperationException("Checksum mismatch."); }

            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });  // NSIS one-click installer relaunches the app
            Electron.App.Quit();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "update failed");
            _ui.Send("update-error", new { message = ex.Message });
            Interlocked.Exchange(ref _installing, 0);
        }
    }
}
