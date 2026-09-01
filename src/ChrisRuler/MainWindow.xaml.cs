using System.Windows;

namespace ChrisRuler;

public partial class MainWindow : Window
{
    private readonly NativeOverlayWindowBehavior nativeBehavior;

    public MainWindow()
    {
        InitializeComponent();
        nativeBehavior = new NativeOverlayWindowBehavior(this, CloseButton, NextRowButton);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void NextRowButton_Click(object sender, RoutedEventArgs e) => nativeBehavior.MoveDownOneHeight();

    protected override void OnClosed(EventArgs e)
    {
        nativeBehavior.Dispose();
        base.OnClosed(e);
    }
}
