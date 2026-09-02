using System.Windows;

namespace ChrisRuler;

public partial class MainWindow : Window
{
    private readonly NativeOverlayWindowBehavior nativeBehavior;

    public MainWindow()
    {
        InitializeComponent();
        nativeBehavior = new NativeOverlayWindowBehavior(this, CloseButton, UpRowButton, DownRowButton);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void UpRowButton_Click(object sender, RoutedEventArgs e) => nativeBehavior.MoveUpOneRow();

    private void DownRowButton_Click(object sender, RoutedEventArgs e) => nativeBehavior.MoveDownOneRow();

    protected override void OnClosed(EventArgs e)
    {
        nativeBehavior.Dispose();
        base.OnClosed(e);
    }
}
