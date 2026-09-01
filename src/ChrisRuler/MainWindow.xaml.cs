using System.Windows;

namespace ChrisRuler;

public partial class MainWindow : Window
{
    private readonly NativeOverlayWindowBehavior nativeBehavior;

    public MainWindow()
    {
        InitializeComponent();
        nativeBehavior = new NativeOverlayWindowBehavior(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        nativeBehavior.Dispose();
        base.OnClosed(e);
    }
}
