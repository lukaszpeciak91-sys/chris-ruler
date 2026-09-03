using System.Windows;

namespace ChrisRuler;

public partial class App : Application
{
    private RulerCoordinator? coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        coordinator = new RulerCoordinator();
        CreateRulerWindow(ownsGeometryPersistence: true).Show();
    }

    /// <summary>Creates another ruler in this process; creation UI is intentionally deferred.</summary>
    internal MainWindow CreateRulerWindow(bool ownsGeometryPersistence = false)
    {
        if (coordinator is null)
        {
            throw new InvalidOperationException("The application has not started.");
        }

        return new MainWindow(coordinator, ownsGeometryPersistence);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        coordinator?.Dispose();
        coordinator = null;
        base.OnExit(e);
    }
}
