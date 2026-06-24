using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PixelThis.Models;

namespace PixelThis.Services;

/// <summary>
/// Reads and writes the native PixelThis project format (<c>.pxt</c>): a JSON
/// document capturing every layer (raw pixels, name, visibility, opacity), the
/// canvas size, the palette and the current color, so a session can be reopened
/// and edited exactly where it was left off.
/// </summary>
public static class ProjectSerializer
{
    public const string Extension = "pxt";
    private const string FormatTag = "PixelThis";
    private const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Everything needed to restore a working session.</summary>
    public sealed record ProjectData(PixelDocument Document, IReadOnlyList<uint> Palette, uint CurrentColor);

    public static void Save(PixelDocument doc, IEnumerable<uint> palette, uint currentColor, Stream stream)
    {
        var dto = new ProjectDto
        {
            Format = FormatTag,
            Version = CurrentVersion,
            Width = doc.Width,
            Height = doc.Height,
            ActiveLayerIndex = doc.ActiveLayerIndex,
            CurrentColor = PixelColor.ToHex(currentColor, includeAlpha: true),
        };

        foreach (var layer in doc.Layers)
        {
            dto.Layers.Add(new LayerDto
            {
                Name = layer.Name,
                Visible = layer.IsVisible,
                Opacity = layer.Opacity,
                Pixels = Convert.ToBase64String(PackPixels(layer.Pixels)),
            });
        }

        foreach (var c in palette)
            dto.Palette.Add(PixelColor.ToHex(c, includeAlpha: true));

        JsonSerializer.Serialize(stream, dto, JsonOptions);
    }

    public static ProjectData Load(Stream stream)
    {
        var dto = JsonSerializer.Deserialize<ProjectDto>(stream, JsonOptions)
                  ?? throw new InvalidDataException("Empty or unreadable project file.");

        if (!string.Equals(dto.Format, FormatTag, StringComparison.Ordinal))
            throw new InvalidDataException("Not a PixelThis project file.");
        if (dto.Width <= 0 || dto.Height <= 0)
            throw new InvalidDataException("Project has an invalid canvas size.");

        int count = dto.Width * dto.Height;
        var layers = new List<Layer>(dto.Layers.Count);
        foreach (var l in dto.Layers)
        {
            var pixels = UnpackPixels(l.Pixels, count);
            layers.Add(Layer.FromSaved(dto.Width, dto.Height, l.Name ?? "Layer", pixels, l.Visible, l.Opacity));
        }

        var document = new PixelDocument(dto.Width, dto.Height, layers, dto.ActiveLayerIndex);

        var palette = new List<uint>(dto.Palette.Count);
        foreach (var hex in dto.Palette)
            if (PixelColor.TryParse(hex, out uint c)) palette.Add(c);

        uint current = PixelColor.TryParse(dto.CurrentColor, out uint cc) ? cc : 0xFFFFFFFFu;

        return new ProjectData(document, palette, current);
    }

    // Pixels are persisted as little-endian BGRA bytes, matching the documented
    // in-memory layout in PixelColor, so the blob is portable across machines.
    private static byte[] PackPixels(uint[] pixels)
    {
        var bytes = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            uint c = pixels[i];
            int o = i * 4;
            bytes[o] = (byte)c;             // B
            bytes[o + 1] = (byte)(c >> 8);  // G
            bytes[o + 2] = (byte)(c >> 16); // R
            bytes[o + 3] = (byte)(c >> 24); // A
        }
        return bytes;
    }

    private static uint[] UnpackPixels(string? base64, int expectedCount)
    {
        var pixels = new uint[expectedCount];
        if (string.IsNullOrEmpty(base64)) return pixels;

        var bytes = Convert.FromBase64String(base64);
        int n = Math.Min(expectedCount, bytes.Length / 4);
        for (int i = 0; i < n; i++)
        {
            int o = i * 4;
            pixels[i] = PixelColor.Pack(bytes[o + 3], bytes[o + 2], bytes[o + 1], bytes[o]);
        }
        return pixels;
    }

    private sealed class ProjectDto
    {
        public string Format { get; set; } = FormatTag;
        public int Version { get; set; } = CurrentVersion;
        public int Width { get; set; }
        public int Height { get; set; }
        public int ActiveLayerIndex { get; set; }
        public List<LayerDto> Layers { get; set; } = new();
        public List<string> Palette { get; set; } = new();
        public string CurrentColor { get; set; } = "#FFFFFFFF";
    }

    private sealed class LayerDto
    {
        public string? Name { get; set; }
        public bool Visible { get; set; } = true;
        public double Opacity { get; set; } = 1.0;
        public string? Pixels { get; set; }
    }
}
