using Avalonia;

namespace SignalClean.Client;

internal static class Program
{
    // Точка входу: Avalonia запускає один і той самий код на Windows, Linux і macOS.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
