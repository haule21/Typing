using System.Windows;
using TypingApp.Components;

namespace TypingApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private TrayContextManager? _trayManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); // Ensure base startup logic runs
        _trayManager = new TrayContextManager();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayManager?.Dispose();
        base.OnExit(e);
    }
}

