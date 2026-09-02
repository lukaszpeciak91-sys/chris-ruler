using System.ComponentModel;
using System.Windows;

namespace ChrisRuler;

public partial class MainWindow : Window
{
    private readonly NativeOverlayWindowBehavior nativeBehavior;

    public MainWindow()
    {
        InitializeComponent();
        nativeBehavior = new NativeOverlayWindowBehavior(
            this, CloseButton, MinimizeButton, LockButton, UpRowButton, DownRowButton);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void LockButton_Click(object sender, RoutedEventArgs e) =>
        nativeBehavior.IsLocked = LockButton.IsChecked == true;

    private void UpRowButton_Click(object sender, RoutedEventArgs e) => nativeBehavior.MoveUpOneRow();

    private void DownRowButton_Click(object sender, RoutedEventArgs e) => nativeBehavior.MoveDownOneRow();

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel)
        {
            nativeBehavior.SaveWindowGeometry();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        nativeBehavior.Dispose();
        base.OnClosed(e);
    }
}
