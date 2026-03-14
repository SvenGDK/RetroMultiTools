using Avalonia;

namespace RetroMultiTools;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RetroMultiTools", "crash.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.WriteAllText(logPath,
                    $"[{DateTime.UtcNow:O}] Unhandled exception:\n{ex}\n");
            }
            catch
            {
                Console.Error.WriteLine($"Fatal error: {ex}");
            }
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
