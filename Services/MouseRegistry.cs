namespace ZoroPlay.Services;

/// <summary>
/// Maps unstable ManyMouse device ids to stable slots using the device PATH.
/// Slots are filled by plug order; a freed slot stays blank until the next mouse
/// is plugged in (lowest free slot first). A mouse that comes back gets its old
/// slot back if it is still free.
/// </summary>
public sealed class MouseRegistry
{
    public sealed class Entry
    {
        public int Slot;            // 1-based
        public int DeviceId;        // current (volatile) id
        public string Path = "";    // identity - never leaves C#
        public string Name = "";
    }

    private readonly object _l = new();
    private readonly List<Entry?> _slots = new();
    private readonly Dictionary<string, int> _lastSlotByPath = new();
    private readonly Dictionary<int, Entry> _byDevice = new();

    public string? DriverError { get; set; }

    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Completes once ManyMouse has finished its first device scan (or failed).</summary>
    public Task Ready => _ready.Task;
    public void MarkReady() => _ready.TrySetResult();
    public int ConnectedCount { get { lock (_l) return _slots.Count(s => s != null); } }

    public Entry Connect(int deviceId, string path, string name, out bool changed)
    {
        lock (_l)
        {
            var existing = _slots.FirstOrDefault(s => s?.Path == path);
            if (existing != null)   // same physical mouse, id changed (or duplicate event)
            {
                changed = existing.DeviceId != deviceId || existing.Name != name;
                if (_byDevice.TryGetValue(existing.DeviceId, out var o) && o == existing) _byDevice.Remove(existing.DeviceId);
                existing.DeviceId = deviceId; existing.Name = name;
                _byDevice[deviceId] = existing;
                return existing;
            }

            int idx = -1;
            if (_lastSlotByPath.TryGetValue(path, out var prev) && prev < _slots.Count && _slots[prev] == null) idx = prev;
            if (idx < 0) idx = _slots.FindIndex(s => s == null);
            if (idx < 0) { _slots.Add(null); idx = _slots.Count - 1; }

            var e = new Entry { Slot = idx + 1, DeviceId = deviceId, Path = path, Name = name };
            _slots[idx] = e;
            _lastSlotByPath[path] = idx;
            _byDevice[deviceId] = e;
            changed = true;
            return e;
        }
    }

    public Entry? Disconnect(int deviceId)
    {
        lock (_l)
        {
            if (!_byDevice.Remove(deviceId, out var e)) return null;
            _slots[e.Slot - 1] = null;      // leave the gap
            return e;
        }
    }

    public Entry? TryGet(int deviceId)
    {
        lock (_l) return _byDevice.GetValueOrDefault(deviceId);
    }

    public List<SlotInfo> Snapshot()
    {
        lock (_l)
            return _slots.Select((s, i) => s is null
                ? new SlotInfo(i + 1, false, null, null)
                : new SlotInfo(s.Slot, true, s.DeviceId, s.Name)).ToList();
    }
}
