using System.Diagnostics;
using static ZoroPlay.Services.ManyMouseNative;

namespace ZoroPlay.Services;

public sealed class MouseWorker : BackgroundService
{
    private sealed class Move { public int Dx, Dy; public int? X, Y; }

    private readonly MouseRegistry _reg;
    private readonly UiBridge _ui;
    private readonly ScreenManager _screens;
    private readonly ILogger<MouseWorker> _log;
    private readonly Dictionary<int, Move> _moves = new();

    public MouseWorker(MouseRegistry reg, UiBridge ui, ScreenManager screens, ILogger<MouseWorker> log)
    { _reg = reg; _ui = ui; _screens = screens; _log = log; }

    protected override Task ExecuteAsync(CancellationToken ct) =>
        Task.Factory.StartNew(() => Run(ct), ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private void Emit(MouseEvent e) => _screens.RouteEvent(e);
    private void PushSnapshot() => _screens.PushSnapshots();

    private void Run(CancellationToken ct)
    {
        try
        {
            FileLog.Write("manymouse.dll: " + (ManyMouseNative.Locate() ?? "NOT FOUND"));
            int count = ManyMouseDLL_Init();
            FileLog.Write($"ManyMouseDLL_Init returned {count}");
            if (count < 0) { Fail($"ManyMouse failed to initialise (Init returned {count})."); return; }
            for (int i = 0; i < count; i++) Connect(i);
            _reg.MarkReady();
        }
        catch (Exception ex)
        { Fail($"manymouse.dll error: {ex.GetType().Name}: {ex.Message}"); return; }

        var flush = Stopwatch.StartNew();
        var ev = new MM_Event();
        while (!ct.IsCancellationRequested)
        {
            bool any = false;
            while (ManyMouseDLL_PollEvent(ref ev) != 0)
            {
                any = true;
                try { Handle(ev); } catch (Exception ex) { _log.LogError(ex, "event failed"); }
                if (flush.ElapsedMilliseconds >= 30) { FlushMoves(); flush.Restart(); }
            }
            if (flush.ElapsedMilliseconds >= 30) { FlushMoves(); flush.Restart(); }
            if (!any) Thread.Sleep(2);
        }
        ManyMouseDLL_Quit();
    }

    private void Fail(string msg)
    {
        FileLog.Write("ERROR: " + msg);
        _reg.MarkReady();
        _reg.DriverError = msg;
        _log.LogError(msg);
        _ui.Send("driver-error", new { message = msg });
    }

    private MouseRegistry.Entry Connect(int device)
    {
        // ids are volatile -> identity is the path (fallback only if the driver gives none)
        var name = Name(device) ?? $"Device {device}";
        var path = Path(device) ?? $"noid:{name}:{device}";
        var e = _reg.Connect(device, path, name, out bool changed);
        if (changed)
        {
            FileLog.Write($"connect: device {device} -> mouse{e.Slot} ({name})");
            Emit(new MouseEvent("connect", e.Slot, device, Now(), $"mouse{e.Slot}-connected", Name: name));
            PushSnapshot();
        }
        return e;
    }

    private void Handle(in MM_Event ev)
    {
        if (ev.type == EV_CONNECT) { Connect(ev.device); return; }
        if (ev.type == EV_DISCONNECT)
        {
            _moves.Remove(ev.device);
            var gone = _reg.Disconnect(ev.device);
            if (gone != null)
            {
                Emit(new MouseEvent("disconnect", gone.Slot, ev.device, Now(), $"mouse{gone.Slot}-disconnected", Name: gone.Name));
                PushSnapshot();
            }
            return;
        }

        var m = _reg.TryGet(ev.device) ?? Connect(ev.device);   // tolerate missed connect events
        switch (ev.type)
        {
            case EV_REL:
            case EV_ABS:
                if (!_moves.TryGetValue(ev.device, out var mv)) _moves[ev.device] = mv = new Move();
                if (ev.type == EV_REL) { if (ev.item == 0) mv.Dx += ev.value; else mv.Dy += ev.value; }
                else { if (ev.item == 0) mv.X = ev.value; else mv.Y = ev.value; }
                break;

            case EV_BUTTON:
                var btn = ev.item switch { 0 => "left", 1 => "right", 2 => "middle", _ => $"button{ev.item + 1}" };
                bool down = ev.value != 0;
                Emit(new MouseEvent("button", m.Slot, ev.device, Now(), $"{btn}-{(down ? "click" : "release")}",
                    Button: btn, Action: down ? "down" : "up"));
                break;

            case EV_SCROLL:
                var axis = ev.item == 0 ? "vertical" : "horizontal";
                Emit(new MouseEvent("scroll", m.Slot, ev.device, Now(), $"scroll-{axis}", Axis: axis, Delta: ev.value));
                break;
        }
    }

    private void FlushMoves()
    {
        if (_moves.Count == 0) return;
        foreach (var (dev, mv) in _moves)
        {
            var m = _reg.TryGet(dev);
            if (m is null) continue;
            Emit(new MouseEvent("move", m.Slot, dev, Now(), "move", Dx: mv.Dx, Dy: mv.Dy, X: mv.X, Y: mv.Y));
        }
        _moves.Clear();
    }
}
