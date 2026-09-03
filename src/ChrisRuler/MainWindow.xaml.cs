using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ChrisRuler;

public partial class MainWindow : Window
{
    private readonly NativeOverlayWindowBehavior nativeBehavior;
    private readonly RulerCoordinator coordinator;
    private readonly bool ownsGeometryPersistence;
    private readonly ContextMenu colorMenu = new();
    private ColorTheme selectedTheme = ColorTheme.Available[0];

    internal MainWindow(RulerCoordinator coordinator, bool ownsGeometryPersistence)
    {
        this.coordinator = coordinator;
        this.ownsGeometryPersistence = ownsGeometryPersistence;
        InitializeComponent();
        ApplyTheme(selectedTheme);
        BuildColorMenu();
        nativeBehavior = new NativeOverlayWindowBehavior(
            this, ownsGeometryPersistence, MarkActive,
            ColorSelectorButton, ScratchpadTextBox, CopyButton, CloseButton, MinimizeButton, LockButton,
            UpRowButton, DownRowButton);
        PreviewMouseDown += OnPreviewMouseDown;
        coordinator.Register(this);
    }

    internal void MoveUpOneRow() => nativeBehavior.MoveUpOneRow();

    internal void MoveDownOneRow() => nativeBehavior.MoveDownOneRow();

    private void MarkActive() => coordinator.MarkActive(this);

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e) => MarkActive();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void LockButton_Click(object sender, RoutedEventArgs e) =>
        nativeBehavior.IsLocked = LockButton.IsChecked == true;

    private void UpRowButton_Click(object sender, RoutedEventArgs e) => nativeBehavior.MoveUpOneRow();

    private void DownRowButton_Click(object sender, RoutedEventArgs e) => nativeBehavior.MoveDownOneRow();

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScratchpadTextBox.Text.Length > 0)
        {
            Clipboard.SetText(ScratchpadTextBox.Text);
        }
    }

    private void ColorSelectorButton_Click(object sender, RoutedEventArgs e)
    {
        colorMenu.PlacementTarget = ColorSelectorButton;
        colorMenu.Placement = PlacementMode.Bottom;
        colorMenu.IsOpen = true;
    }

    private void BuildColorMenu()
    {
        colorMenu.Background = new SolidColorBrush(Color.FromRgb(0x24, 0x29, 0x2D));
        colorMenu.Foreground = new SolidColorBrush(Color.FromRgb(0xF2, 0xF6, 0xF8));

        foreach (ColorTheme theme in ColorTheme.Available)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Margin = new Thickness(0, 0, 7, 0),
                Fill = theme.Brush()
            });
            header.Children.Add(new TextBlock { Text = theme.Name });

            var item = new MenuItem
            {
                Header = header,
                IsCheckable = true,
                IsChecked = theme == selectedTheme,
                Tag = theme
            };
            item.Click += ColorTheme_Click;
            colorMenu.Items.Add(item);
        }
    }

    private void ColorTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: ColorTheme theme })
        {
            return;
        }

        selectedTheme = theme;
        ApplyTheme(theme);
        foreach (object menuItem in colorMenu.Items)
        {
            var item = (MenuItem)menuItem;
            item.IsChecked = ReferenceEquals(item.Tag, theme);
        }
    }

    private void ApplyTheme(ColorTheme theme)
    {
        Resources["AccentBrush"] = theme.Brush();
        Resources["CalibrationBrush"] = theme.Brush(0xCC);
        Resources["InnerBorderBrush"] = theme.Brush(0xB3);
        Resources["ControlBackgroundBrush"] = theme.ControlBrush(0x33);
        Resources["ControlBorderBrush"] = theme.BrushFor(theme.ControlBorder, 0x66);
        Resources["ControlHoverBrush"] = theme.BrushFor(theme.HoverAccent, 0x80);
        Resources["ControlHoverBorderBrush"] = theme.BrushFor(theme.HoverBorder, 0xBF);
        Resources["ControlPressedBrush"] = theme.BrushFor(theme.PressedAccent, 0xCC);
        Resources["TextSelectionBrush"] = theme.Brush(0xCC);
        Resources["TextFocusBorderBrush"] = theme.FocusBrush();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (!e.Cancel)
        {
            if (ownsGeometryPersistence)
            {
                nativeBehavior.SaveWindowGeometry();
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        PreviewMouseDown -= OnPreviewMouseDown;
        nativeBehavior.Dispose();
        coordinator.Unregister(this);
        base.OnClosed(e);
    }
}
