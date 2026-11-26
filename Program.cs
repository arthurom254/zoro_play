using System.Diagnostics;
using System.Text.RegularExpressions;
using ElectronNET.API;
using ElectronNET.API.Entities;
using ZoroPlay.Services;

AppDomain.CurrentDomain.UnhandledException += (_, e) => FileLog.Write("UNHANDLED: " + e.ExceptionObject);
TaskScheduler.UnobservedTaskException += (_, e) => FileLog.Write("UNOBSERVED: " + e.Exception);

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseElectron(args);
builder.Services.AddElectron();
builder.Services.AddSingleton<UiBridge>();
builder.Services.AddSingleton<MouseRegistry>();
builder.Services.AddSingleton<ScreenManager>();
builder.Services.AddSingleton<UpdateService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<UpdateService>());
builder.Services.AddHostedService<MouseWorker>();

var app = builder.Build();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");     // the React router serves /game1, /game2, ...
await app.StartAsync();

if (HybridSupport.IsElectronActive)
{
    var ui = app.Services.GetRequiredService<UiBridge>();
    var registry = app.Services.GetRequiredService<MouseRegistry>();
    var updates = app.Services.GetRequiredService<UpdateService>();
    var screens = app.Services.GetRequiredService<ScreenManager>();

    // Wait for ManyMouse's first device scan so the launch configuration knows the mouse count.
    await Task.WhenAny(registry.Ready, Task.Delay(TimeSpan.FromSeconds(8)));
    await screens.StartAsync(registry.ConnectedCount);

    // Windows closed because a monitor was unplugged must not quit the app; windows the user closed do.
    Electron.App.WindowAllClosed += () => { if (screens.HasUserClosed) Electron.App.Quit(); };

    void OpenDevTools() { foreach (var w in Electron.WindowManager.BrowserWindows.ToArray()) w.WebContents.OpenDevTools(); }
    Electron.Menu.SetApplicationMenu(new[] {
        new MenuItem { Label = "Debug", Submenu = new[] {
            new MenuItem { Label = "Developer tools", Accelerator = "F12", Click = OpenDevTools },
            new MenuItem { Label = "Open log folder", Click = () => Process.Start("explorer.exe", FileLog.Dir) },
            new MenuItem { Label = "Show config file", Click = () => Process.Start("explorer.exe", $"/select,\"{AppConfig.PathOnDisk}\"") }
        }}
    });

    // Renderer -> C#
    await Electron.IpcMain.On("ui-ready", arg =>
    {
        var m = Regex.Match(arg?.ToString() ?? "", @"\d+");        // page sends its game number
        int game = m.Success ? int.Parse(m.Value) : 1;
        ui.Send("app-info", new { version = UpdateService.CurrentVersion.ToString(3) });
        if (registry.DriverError is { } err) ui.Send("driver-error", new { message = err });
        screens.SendInitial(game);
        updates.PushState();
    });
    await Electron.IpcMain.On("open-devtools", a => OpenDevTools());
    await Electron.IpcMain.On("install-update", a => { _ = updates.InstallAsync(); });
}

await app.WaitForShutdownAsync();
