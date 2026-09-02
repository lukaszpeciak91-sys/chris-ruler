using System.Windows;

namespace ChrisRuler;

public partial class MainWindow : Window
{
    private readonly NativeOverlayWindowBehavior nativeBehavior;

    public MainWindow()
    {
        InitializeComponent();
        nativeBehavior = new NativeOverlayWindowBehavior(
            this,
            SetCalibrationLineEmphasis,
            CloseButton,
            LockButton,
            UpRowButton,
            DownRowButton);
    }

    private void SetCalibrationLineEmphasis(bool isResizing)
    {
        double opacity = isResizing ? 0.95 : 0.55;
        TopCalibrationLine.Opacity = opacity;
        BottomCalibrationLine.Opacity = opacity;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void LockButton_Click(object sender, RoutedEventArgs e) =>
        nativeBehavior.IsLocked = LockButton.IsChecked == true;

    private void UpRowButton_Click(object sender, RoutedEventArgs e) => nativeBehavior.MoveUpOneRow();

    private void DownRowButton_Click(object sender, RoutedEventArgs e) => nativeBehavior.MoveDownOneRow();

    protected override void OnClosed(EventArgs e)
    {
        nativeBehavior.SaveWindowGeometry();
        nativeBehavior.Dispose();
        base.OnClosed(e);
    }
}
