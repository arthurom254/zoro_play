using System.Text.Json;

namespace ZoroPlay.Services;

/// <summary>One entry per screen. Edit maxMice by hand if you want a different split.</summary>
public sealed class ScreenConfig
{
    public string DisplayId { get; set; } = "";
    public int Game { get; set; }          // window path: /game{Game}
    public int MaxMice { get; set; }       // how many mouse slots this screen owns
    public string? GameUrl { get; set; }   // optional: show this web page instead of the built-in game
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Scale { get; set; } = 1;
}

public sealed class AppConfig
{
    public int Version { get; set; } = 1;
    public int ScreenCount { get; set; }
    public int MouseCount { get; set; }
    public List<ScreenConfig> Screens { get; set; } = new();

    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static readonly string PathOnDisk = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoroPlay", "config.json");

    public static AppConfig? Load()
    {
        try
        {
            return File.Exists(PathOnDisk)
                ? JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(PathOnDisk), Opts) : null;
        }
        catch (Exception ex) { FileLog.Write("config load failed: " + ex.Message); return null; }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathOnDisk)!);
            File.WriteAllText(PathOnDisk, JsonSerializer.Serialize(this, Opts));
        }
        catch (Exception ex) { FileLog.Write("config save failed: " + ex.Message); }
    }

    /// <summary>
    /// Screens are numbered left-to-right (game1, game2, ...). Mice are dealt out in slot order:
    /// with a cap, each screen is filled up to the cap before the next one gets any;
    /// without a cap (or if the cap is too small for everyone) the mice are split evenly.
    /// </summary>
    public static AppConfig Generate(IReadOnlyList<DisplayInfo> displays, int mice, int cap)
    {
        var ordered = displays.OrderBy(d => d.X).ThenBy(d => d.Y).ToList();
        int n = ordered.Count;
        var max = new int[n];
        if (n > 0)
        {
            if (cap > 0 && (long)cap * n >= mice)
            {
                int left = mice;
                for (int i = 0; i < n; i++) { max[i] = Math.Min(cap, left); left -= max[i]; }
            }
            else
                for (int i = 0; i < n; i++) max[i] = mice / n + (i < mice % n ? 1 : 0);
        }

        var cfg = new AppConfig { ScreenCount = n, MouseCount = mice };
        for (int i = 0; i < n; i++)
            cfg.Screens.Add(new ScreenConfig
            {
                DisplayId = ordered[i].Id, Game = i + 1, MaxMice = max[i],
                X = ordered[i].X, Y = ordered[i].Y, Width = ordered[i].Width, Height = ordered[i].Height,
                Scale = ordered[i].Scale
            });
        return cfg;
    }
}
