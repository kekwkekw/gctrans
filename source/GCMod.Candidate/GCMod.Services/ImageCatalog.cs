using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GCMod.Services;

public sealed class ImageCatalog
{
    [JsonPropertyName("schema")] public string Schema { get; set; }
    [JsonPropertyName("entries")] public List<ImageEntry> Entries { get; set; }

    public static string Hash(string json) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

    public static ImageCatalog Parse(string json)
    {
        var catalog = JsonSerializer.Deserialize<ImageCatalog>(json);
        if (catalog?.Schema != "gc-images-v1" || catalog.Entries == null || catalog.Entries.Count > 128)
            throw new FormatException("Invalid image catalog");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;
        foreach (var entry in catalog.Entries)
        {
            if (entry == null || (entry.Kind != "sprite" && entry.Kind != "texture") ||
                string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Length > 160 ||
                entry.Width < 1 || entry.Height < 1 || entry.Width > 4096 || entry.Height > 4096 ||
                entry.Bytes < 33 || entry.Bytes > 8 * 1024 * 1024 ||
                entry.Sha256 == null || !Regex.IsMatch(entry.Sha256, "^[0-9a-f]{64}$") ||
                entry.File != "images/" + entry.Sha256 + ".png" || !keys.Add(entry.Key))
                throw new FormatException("Invalid or duplicate image target");
            total += entry.Bytes;
        }
        if (total > 64 * 1024 * 1024) throw new FormatException("Image catalog exceeds size limit");
        return catalog;
    }
}

public sealed class ImageEntry
{
    [JsonPropertyName("kind")] public string Kind { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("file")] public string File { get; set; }
    [JsonPropertyName("sha256")] public string Sha256 { get; set; }
    [JsonPropertyName("bytes")] public int Bytes { get; set; }
    [JsonIgnore] public string Key => Kind + ":" + Name + ":" + Width + "x" + Height;

    public bool Matches(byte[] bytes)
    {
        if (bytes == null || bytes.Length != Bytes || bytes.Length < 33 ||
            !bytes.Take(8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ||
            bytes[12] != 73 || bytes[13] != 72 || bytes[14] != 68 || bytes[15] != 82) return false;
        int ReadInt(int offset) => (bytes[offset] << 24) | (bytes[offset+1] << 16) | (bytes[offset+2] << 8) | bytes[offset+3];
        return ReadInt(16) == Width && ReadInt(20) == Height &&
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() == Sha256;
    }
}
