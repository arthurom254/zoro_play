using ElectronNET.API;
using ElectronNET.API.Entities;

namespace ZoroPlay.Services;

/// <summary>
/// One frameless BrowserWindow per monitor (/game1, /game2, ...), per-screen config persisted to disk,
/// mouse-slot routing, and hot-plug handling (close on disconnect, recreate on reconnect).
/// </summary>
public sealed class ScreenManager
{
    private sealed class GameWindow
    {
        public GameWindow(int game, BrowserWindow window, DisplayInfo info) { Game = game; Window = window; Info = info; }
        public int Game { get; }
        public BrowserWindow Window { get; }
        public DisplayInfo Info { get; set; }
    }

    private readonly UiBridge _ui;
    private readonly MouseRegistry _reg;
    private readonly int _cap;
    private readonly bool _fullscreen;
    private readonly object _l = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<int, GameWindow> _windows = new();   // by game number
    private readonly HashSet<int> _closingIds = new();               // windows WE are closing (monitor unplugged)
    private readonly HashSet<int> _userClosed = new();               // games the user closed manually
    private readonly HashSet<int> _warned = new();
    private AppConfig _cfg = new();
    private bool _dirty;

    public ScreenManager(UiBridge ui, MouseRegistry reg, IConfiguration config)
    {
        _ui = ui; _reg = reg;
        _cap = config.GetValue("Screens:MaxMicePerScreen", 3);
        _fullscreen = config.GetValue("Screens:Fullscreen", true);
    }

    public bool HasUserClosed { get { lock (_l) return _userClosed.Count > 0; } }

    // ------------------------------------------------------------ launch

    /// <summary>Runs once at launch. The ONLY place a configuration is (re)generated.</summary>
    public async Task StartAsync(int mouseCount)
    {
        await _gate.WaitAsync();
        try
        {
            var displays = await GetDisplaysAsync();
            FileLog.Write($"Launch: {displays.Count} display(s), {mouseCount} mouse(s)");
            foreach (var d in displays)
                FileLog.Write($"  display {d.Id}: {d.Width}x{d.Height} at {d.X},{d.Y} scale {d.Scale}");

            var cfg = AppConfig.Load();
            bool regenerate = cfg is null || cfg.Screens.Count != displays.Count
                              || cfg.ScreenCount != displays.Count || cfg.MouseCount != mouseCount;
            if (regenerate)
            {
                var previous = cfg;
                cfg = AppConfig.Generate(displays, mouseCount, _cap);
                if (previous != null)                        // keep hand-set game URLs across regeneration
                    foreach (var s in cfg.Screens)
                        s.GameUrl = previous.Screens.FirstOrDefault(o => o.Game == s.Game)?.GameUrl;
                cfg.Save();
                FileLog.Write("Configuration generated: " +
                    string.Join(", ", cfg.Screens.Select(s => $"game{s.Game}={s.MaxMice} mice")));
            }
            else FileLog.Write("Configuration restored from " + AppConfig.PathOnDisk);

            lock (_l) _cfg = cfg!;
            await ApplyAsync(displays);
        }
        finally { _gate.Release(); }

        Electron.Screen.OnDisplayAdded += display => { _ = ReconcileAsync(); };
        Electron.Screen.OnDisplayRemoved += display => { _ = ReconcileAsync(); };
        Electron.Screen.OnDisplayMetricsChanged += (display, changed) => { _ = ReconcileAsync(); };
    }

    // ------------------------------------------------------------ hot-plug

    private async Task ReconcileAsync()
    {
        try
        {
            await Task.Delay(500);                       // a plug/unplug fires several events; let them settle
            await _gate.WaitAsync();
            try { await ApplyAsync(await GetDisplaysAsync()); }
            finally { _gate.Release(); }
        }
        catch (Exception ex) { FileLog.Write("reconcile failed: " + ex); }
    }

    private async Task ApplyAsync(List<DisplayInfo> displays)
    {
        // 1) monitor gone -> close its window
        List<GameWindow> gone;
        lock (_l) gone = _windows.Values.Where(w => displays.All(d => d.Id != w.Info.Id)).ToList();
        foreach (var gw in gone) CloseWindow(gw, "display disconnected");

        // 2) monitor present -> make sure it has a correctly placed window
        foreach (var d in displays)
        {
            ScreenConfig? sc; GameWindow? existing = null; bool blocked = false;
            lock (_l)
            {
                sc = FindEntry(d, displays);
                if (sc != null)
                {
                    _windows.TryGetValue(sc.Game, out existing);
                    blocked = _userClosed.Contains(sc.Game);
                }
            }
            if (sc is null) { FileLog.Write($"display {d.Id} has no configuration (restart to generate one)"); continue; }
            if (existing != null) { MoveIfNeeded(existing, sc, d); continue; }
            if (!blocked) await CreateWindowAsync(sc, d);
        }

        lock (_l) { if (_dirty) { _dirty = false; _cfg.Save(); } }
    }

    // Caller holds _l. Matches by display id, then by identical bounds, then any entry whose screen is absent.
    private ScreenConfig? FindEntry(DisplayInfo d, List<DisplayInfo> displays)
    {
        var s = _cfg.Screens.FirstOrDefault(x => x.DisplayId == d.Id)
             ?? _cfg.Screens.FirstOrDefault(x => !_windows.ContainsKey(x.Game)
                    && x.X == d.X && x.Y == d.Y && x.Width == d.Width && x.Height == d.Height)
             ?? _cfg.Screens.Where(x => !_windows.ContainsKey(x.Game) && displays.All(o => o.Id != x.DisplayId))
                    .OrderBy(x => x.Game).FirstOrDefault();
        if (s != null && s.DisplayId != d.Id) { s.DisplayId = d.Id; _dirty = true; }
        return s;
    }

    private void UpdateBounds(ScreenConfig sc, DisplayInfo d)   // caller holds _l
    {
        if (sc.X != d.X || sc.Y != d.Y || sc.Width != d.Width || sc.Height != d.Height || sc.Scale != d.Scale)
        { sc.X = d.X; sc.Y = d.Y; sc.Width = d.Width; sc.Height = d.Height; sc.Scale = d.Scale; _dirty = true; }
    }

    private void MoveIfNeeded(GameWindow gw, ScreenConfig sc, DisplayInfo d)
    {
        if (gw.Info == d) return;
        gw.Info = d;
        lock (_l) UpdateBounds(sc, d);
        FileLog.Write($"game{gw.Game}: display changed to {d.Width}x{d.Height} at {d.X},{d.Y}");
        if (!_fullscreen) Fit(gw);
        PushScreenInfo(gw);
    }

    // ------------------------------------------------------------ windows

    private async Task CreateWindowAsync(ScreenConfig sc, DisplayInfo d)
    {
        lock (_l) UpdateBounds(sc, d);
        int game = sc.Game;

        var win = await Electron.WindowManager.CreateWindowAsync(new BrowserWindowOptions
        {
            X = d.X, Y = d.Y, Width = d.Width, Height = d.Height,
            Frame = false, Resizable = false, Movable = false, Show = false,
            AutoHideMenuBar = true, BackgroundColor = "#121212", Title = $"Monitor {game}",
            WebPreferences = new WebPreferences { NodeIntegration = true, ContextIsolation = false }
        }, $"{(Environment.GetEnvironmentVariable("ZORO_VITE_URL")?.TrimEnd('/') ?? $"http://localhost:{BridgeSettings.WebPort}")}/game{game}");

        var gw = new GameWindow(game, win, d);
        lock (_l) _windows[game] = gw;

        win.OnReadyToShow += () =>
        {
            Fit(gw);
            win.Show();
            // mixed-DPI setups need a second pass after the window exists on its monitor
            _ = Task.Delay(250).ContinueWith(_ => { Fit(gw); if (_fullscreen) win.SetFullScreen(true); });
        };
        win.OnClosed += () => HandleClosed(game, win);
        FileLog.Write($"game{game}: window created on display {d.Id} ({d.Width}x{d.Height} at {d.X},{d.Y})");
    }

    private static void Fit(GameWindow gw) =>
        gw.Window.SetBounds(new Rectangle { X = gw.Info.X, Y = gw.Info.Y, Width = gw.Info.Width, Height = gw.Info.Height });

    private void CloseWindow(GameWindow gw, string reason)
    {
        lock (_l)
        {
            if (_windows.TryGetValue(gw.Game, out var cur) && cur == gw) _windows.Remove(gw.Game);
            _closingIds.Add(gw.Window.Id);
        }
        FileLog.Write($"game{gw.Game}: closing window ({reason})");
        try { gw.Window.Close(); } catch (Exception ex) { FileLog.Write("close failed: " + ex.Message); }
    }

    private void HandleClosed(int game, BrowserWindow win)
    {
        lock (_l)
        {
            if (_closingIds.Remove(win.Id)) return;      // we closed it because the monitor went away
            if (_windows.TryGetValue(game, out var cur) && cur.Window.Id == win.Id) _windows.Remove(game);
            _userClosed.Add(game);                       // user closed it: do not recreate
        }
        FileLog.Write($"game{game}: window closed by user");
    }

    private static DisplayInfo Map(Display d) => new(
        Convert.ToString(d.Id) ?? "", d.Bounds.X, d.Bounds.Y, d.Bounds.Width, d.Bounds.Height,
        Convert.ToDouble(d.ScaleFactor));

    private static async Task<List<DisplayInfo>> GetDisplaysAsync() =>
        ((await Electron.Screen.GetAllDisplaysAsync()) ?? Array.Empty<Display>()).Select(Map).ToList();

    // ------------------------------------------------------------ mouse routing

    // Caller holds _l. Slots are dealt to screens in game order: game1 gets 1..m1, game2 the next m2, ...
    private (int game, int local)? Resolve(int slot)
    {
        int start = 1;
        foreach (var s in _cfg.Screens.OrderBy(x => x.Game))
        {
            if (slot < start + s.MaxMice) return (s.Game, slot - start + 1);
            start += s.MaxMice;
        }
        return null;
    }

    private (int first, int count) RangeOf(int game)         // caller holds _l
    {
        int start = 1;
        foreach (var s in _cfg.Screens.OrderBy(x => x.Game))
        {
            if (s.Game == game) return (start, s.MaxMice);
            start += s.MaxMice;
        }
        return (start, 0);
    }

    public void RouteEvent(MouseEvent e)
    {
        GameWindow? gw = null; int local = 0;
        lock (_l)
        {
            var r = Resolve(e.Mouse);
            if (r is null)
            {
                if (_warned.Add(e.Mouse)) FileLog.Write($"mouse{e.Mouse} is not assigned to any screen (config made for {_cfg.MouseCount} mice)");
                return;
            }
            if (!_windows.TryGetValue(r.Value.game, out gw)) return;
            local = r.Value.local;
        }
        _ui.SendTo(gw.Window, "mouse-event", e with { Local = local, Screen = gw.Game });
    }

    public void PushSnapshots()
    {
        List<GameWindow> ws;
        lock (_l) ws = _windows.Values.ToList();
        foreach (var w in ws) PushSnapshot(w);
    }

    private void PushSnapshot(GameWindow gw)
    {
        var snap = _reg.Snapshot();
        (int first, int count) r;
        lock (_l) r = RangeOf(gw.Game);
        var slots = Enumerable.Range(0, r.count).Select(i =>
        {
            int slot = r.first + i;
            var s = snap.FirstOrDefault(x => x.Slot == slot);
            return new { slot, local = i + 1, connected = s?.Connected ?? false, id = s?.Id, name = s?.Name };
        }).ToList();
        _ui.SendTo(gw.Window, "mouse-snapshot", slots);
    }

    private void PushScreenInfo(GameWindow gw)
    {
        ScreenConfig? sc; (int first, int count) r;
        lock (_l) { sc = _cfg.Screens.FirstOrDefault(s => s.Game == gw.Game); r = RangeOf(gw.Game); }
        _ui.SendTo(gw.Window, "screen-info", new
        {
            game = gw.Game, displayId = gw.Info.Id,
            x = gw.Info.X, y = gw.Info.Y, width = gw.Info.Width, height = gw.Info.Height, scale = gw.Info.Scale,
            maxMice = sc?.MaxMice ?? 0, firstSlot = r.first, gameUrl = sc?.GameUrl
        });
    }

    /// <summary>Called when a page announces itself with ui-ready.</summary>
    public void SendInitial(int game)
    {
        GameWindow? gw;
        lock (_l) _windows.TryGetValue(game, out gw);
        if (gw is null) return;
        PushScreenInfo(gw);
        PushSnapshot(gw);
    }
}
