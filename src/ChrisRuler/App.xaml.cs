using System.Windows;

namespace ChrisRuler;

public partial class App : Application
{
    private RulerCoordinator? coordinator;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        coordinator = new RulerCoordinator();
        coordinator.CreateInitialRuler();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        coordinator?.Dispose();
        coordinator = null;
        base.OnExit(e);
    }
}
