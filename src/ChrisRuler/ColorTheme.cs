using System.Windows.Media;

namespace ChrisRuler;

internal sealed record ColorTheme(
    string Name,
    Color Accent,
    Color ControlAccent,
    Color FocusAccent,
    Color ControlBorder,
    Color HoverAccent,
    Color HoverBorder,
    Color PressedAccent)
{
    public static IReadOnlyList<ColorTheme> Available { get; } =
    [
        new(
            "Graphite / Blue",
            Color.FromRgb(0x42, 0x8B, 0xB5), Color.FromRgb(0x2B, 0x8A, 0xC6),
            Color.FromRgb(0x67, 0xA9, 0xCF), Color.FromRgb(0x7F, 0xA7, 0xBE),
            Color.FromRgb(0x50, 0x9B, 0xC7), Color.FromRgb(0xD0, 0xE9, 0xF5),
            Color.FromRgb(0x24, 0x6C, 0x96)),
        new(
            "Graphite / Green",
            Color.FromRgb(0x4E, 0x9B, 0x72), Color.FromRgb(0x3A, 0x91, 0x67),
            Color.FromRgb(0x74, 0xB8, 0x91), Color.FromRgb(0x7D, 0xA9, 0x90),
            Color.FromRgb(0x54, 0xA4, 0x78), Color.FromRgb(0xD0, 0xEB, 0xDA),
            Color.FromRgb(0x2D, 0x73, 0x50)),
        new(
            "Graphite / Amber",
            Color.FromRgb(0xC0, 0x8A, 0x3E), Color.FromRgb(0xB5, 0x78, 0x28),
            Color.FromRgb(0xD4, 0xAA, 0x66), Color.FromRgb(0xB5, 0x9A, 0x72),
            Color.FromRgb(0xC0, 0x8B, 0x42), Color.FromRgb(0xF1, 0xDF, 0xC1),
            Color.FromRgb(0x8D, 0x5B, 0x1E)),
        new(
            "Graphite / Red",
            Color.FromRgb(0xB8, 0x5A, 0x55), Color.FromRgb(0xAD, 0x48, 0x43),
            Color.FromRgb(0xCF, 0x7B, 0x76), Color.FromRgb(0xB0, 0x83, 0x80),
            Color.FromRgb(0xBC, 0x62, 0x5D), Color.FromRgb(0xF0, 0xD0, 0xCE),
            Color.FromRgb(0x87, 0x35, 0x31)),
        new(
            "Graphite / Purple",
            Color.FromRgb(0x8D, 0x6B, 0xB5), Color.FromRgb(0x7C, 0x58, 0xA8),
            Color.FromRgb(0xAA, 0x8A, 0xCC), Color.FromRgb(0x9D, 0x8A, 0xB3),
            Color.FromRgb(0x91, 0x70, 0xB8), Color.FromRgb(0xE2, 0xD5, 0xEF),
            Color.FromRgb(0x61, 0x42, 0x82))
    ];

    public SolidColorBrush Brush(byte alpha = 0xFF) =>
        new(Color.FromArgb(alpha, Accent.R, Accent.G, Accent.B));

    public SolidColorBrush ControlBrush(byte alpha) =>
        new(Color.FromArgb(alpha, ControlAccent.R, ControlAccent.G, ControlAccent.B));

    public SolidColorBrush FocusBrush(byte alpha = 0xFF) =>
        new(Color.FromArgb(alpha, FocusAccent.R, FocusAccent.G, FocusAccent.B));

    public SolidColorBrush BrushFor(Color color, byte alpha) =>
        new(Color.FromArgb(alpha, color.R, color.G, color.B));
}
