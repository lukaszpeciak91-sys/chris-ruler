using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ChrisRuler;

public partial class MainWindow : Window
{
    // Each threshold includes the fixed-width controls, margins, and a small gap
    // between the left- and right-aligned groups at that responsive state.
    private const double ScratchpadVisibleWidth = 370;
    private const double CopyVisibleWidth = 210;
    private const double ColorSelectorVisibleWidth = 180;
    private const double MinimizeVisibleWidth = 150;
    private const double LockVisibleWidth = 120;
    private const double RowButtonsVisibleWidth = 90;

    private readonly NativeOverlayWindowBehavior nativeBehavior;
    private readonly RulerCoordinator coordinator;
    private readonly bool ownsGeometryPersistence;
    private readonly ContextMenu colorMenu = new();
    private ColorTheme selectedTheme;

    internal MainWindow(
        RulerCoordinator coordinator,
        bool ownsGeometryPersistence,
        ColorTheme selectedTheme,
        WindowGeometry? initialGeometry)
    {
        this.coordinator = coordinator;
        this.ownsGeometryPersistence = ownsGeometryPersistence;
        this.selectedTheme = selectedTheme;
        InitializeComponent();
#if CHRIS_RULER_ICON
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/ChrisRuler.ico"));
#endif
        ApplyTheme(selectedTheme);
        BuildColorMenu();
        SizeChanged += OnResponsiveSizeChanged;
        UpdateResponsiveControlVisibility(Width);
        nativeBehavior = new NativeOverlayWindowBehavior(
            this, ownsGeometryPersistence, initialGeometry, MarkActive,
            ColorSelectorButton, ScratchpadTextBox, CopyButton, CloseButton, MinimizeButton, LockButton,
            UpRowButton, DownRowButton, NewRulerButton);
        PreviewMouseDown += OnPreviewMouseDown;
        coordinator.ActiveRulerChanged += OnActiveRulerChanged;
        coordinator.Register(this);
        UpdateActiveAppearance();
    }

    internal ColorTheme SelectedTheme => selectedTheme;

    internal WindowGeometry? GetOffsetCloneGeometry() => nativeBehavior.GetOffsetCloneGeometry();

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

    private void NewRulerButton_Click(object sender, RoutedEventArgs e) => coordinator.CreateNewRuler();

    private void OnActiveRulerChanged(object? sender, EventArgs e) => UpdateActiveAppearance();

    private void OnResponsiveSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateResponsiveControlVisibility(e.NewSize.Width);

    private void UpdateResponsiveControlVisibility(double width)
    {
        ScratchpadTextBox.Visibility = VisibilityForWidth(width, ScratchpadVisibleWidth);
        CopyButton.Visibility = VisibilityForWidth(width, CopyVisibleWidth);
        ColorSelectorButton.Visibility = VisibilityForWidth(width, ColorSelectorVisibleWidth);
        MinimizeButton.Visibility = VisibilityForWidth(width, MinimizeVisibleWidth);
        LockButton.Visibility = VisibilityForWidth(width, LockVisibleWidth);

        Visibility rowButtonsVisibility = VisibilityForWidth(width, RowButtonsVisibleWidth);
        UpRowButton.Visibility = rowButtonsVisibility;
        DownRowButton.Visibility = rowButtonsVisibility;
    }

    private static Visibility VisibilityForWidth(double width, double threshold) =>
        width >= threshold ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateActiveAppearance()
    {
        double detailOpacity = coordinator.IsActive(this) ? 1.0 : 0.52;
        TopCalibrationLine.Opacity = detailOpacity;
        BottomCalibrationLine.Opacity = detailOpacity;
        OuterAccentBorder.Opacity = detailOpacity;
        InnerAccentBorder.Opacity = detailOpacity;
        LeftControls.Opacity = detailOpacity;
        RightControls.Opacity = detailOpacity;
        NewRulerButton.Opacity = detailOpacity;
    }

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
        colorMenu.Style = (Style)FindResource("ColorMenuStyle");
        Style itemStyle = (Style)FindResource("ColorMenuItemStyle");

        foreach (ColorTheme theme in ColorTheme.Available)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new Ellipse
            {
                Width = 10,
                Height = 10,
                Margin = new Thickness(0, 0, 7, 0),
                Fill = theme.BrushFor(theme.Highlight)
            });
            header.Children.Add(new TextBlock { Text = theme.Name });

            var item = new MenuItem
            {
                Header = header,
                IsCheckable = true,
                IsChecked = theme == selectedTheme,
                Style = itemStyle,
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
        Resources["FrameBrush"] = theme.BrushFor(theme.Frame, 0xE0);
        Resources["AccentBrush"] = theme.BrushFor(theme.Highlight);
        Resources["CalibrationBrush"] = theme.BrushFor(theme.Calibration);
        Resources["InnerBorderBrush"] = theme.BrushFor(theme.InnerGuide, 0xD9);
        Resources["OuterBorderBrush"] = theme.BrushFor(theme.ControlBorder, 0xA6);
        Resources["ControlBackgroundBrush"] = theme.BrushFor(theme.ControlBackground, 0xB3);
        Resources["TextInputBackgroundBrush"] = theme.BrushFor(theme.ControlBackground, 0xE6);
        Resources["ControlBorderBrush"] = theme.BrushFor(theme.ControlBorder, 0xA6);
        Resources["ControlHoverBrush"] = theme.BrushFor(theme.ControlHover, 0xCC);
        Resources["ControlHoverBorderBrush"] = theme.BrushFor(theme.FocusBorder, 0xE6);
        Resources["ControlPressedBrush"] = theme.BrushFor(theme.ControlPressed, 0xE6);
        Resources["TextSelectionBrush"] = theme.BrushFor(theme.SelectionAccent, 0xCC);
        Resources["TextFocusBorderBrush"] = theme.BrushFor(theme.FocusBorder);
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
        SizeChanged -= OnResponsiveSizeChanged;
        PreviewMouseDown -= OnPreviewMouseDown;
        coordinator.ActiveRulerChanged -= OnActiveRulerChanged;
        nativeBehavior.Dispose();
        coordinator.Unregister(this);
        base.OnClosed(e);
    }
}
