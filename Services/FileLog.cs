namespace ZoroPlay.Services;

public static class FileLog
{
    public static readonly string Dir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZoroPlay", "logs");
    private static readonly object L = new();

    public static void Write(string msg)
    {
        try
        {
            lock (L)
            {
                Directory.CreateDirectory(Dir);
                File.AppendAllText(System.IO.Path.Combine(Dir, "app.log"), $"{DateTime.Now:O}  {msg}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
