using System.IO;
using System.Text.Json;

namespace ChrisRuler;

internal sealed class WindowGeometryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string filePath;

    public WindowGeometryStore()
    {
        filePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ChrisRuler",
            "window.json");
    }

    public WindowGeometry? Load()
    {
        try
        {
            return File.Exists(filePath)
                ? JsonSerializer.Deserialize<WindowGeometry>(File.ReadAllText(filePath))
                : null;
        }
        catch (Exception)
        {
            // A damaged or inaccessible preference must never prevent the overlay starting.
            return null;
        }
    }

    public void Save(WindowGeometry geometry)
    {
        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (directory is null)
            {
                return;
            }

            Directory.CreateDirectory(directory);
            string temporaryPath = filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(geometry, JsonOptions));
            File.Move(temporaryPath, filePath, true);
        }
        catch (Exception)
        {
            // Persistence is optional; shutdown remains clean on read-only or full disks.
        }
    }
}

internal sealed record WindowGeometry(int Left, int Top, int Width, int Height);
