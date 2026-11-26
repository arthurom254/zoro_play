namespace ZoroPlay.Services;

/// <summary>What the UI sees. Never contains the device path.</summary>
public sealed record MouseEvent(string Kind, int Mouse, int Id, long Ts, string Label,
    string? Name = null, string? Button = null, string? Action = null, string? Axis = null,
    int? Dx = null, int? Dy = null, int? X = null, int? Y = null, int? Delta = null,
    int? Local = null, int? Screen = null);   // Local = mouse number on its own screen (1..MaxMice)

public sealed record SlotInfo(int Slot, bool Connected, int? Id, string? Name);

public sealed record DisplayInfo(string Id, int X, int Y, int Width, int Height, double Scale);
