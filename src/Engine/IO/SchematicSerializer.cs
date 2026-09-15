using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Hakoniwa.Engine.Data;
using ZstdSharp;

namespace Hakoniwa.Engine.IO;

public static class SchematicSerializer
{
    private static readonly byte[] Magic = [0x48, 0x4B, 0x4E, 0x57];
    private const ushort CurrentVersion = 0x0001;
    private const int HeaderBytes = 4 + 2 + 4 + 4 + 4 + 4 + 4 + 4;

    public static byte[] Serialize(Schematic schematic, int compressionLevel = 3)
    {
        if (schematic is null)
            throw new ArgumentNullException(nameof(schematic));
        if (compressionLevel is < 1 or > 22)
            throw new ArgumentOutOfRangeException(nameof(compressionLevel), compressionLevel, "Zstd level must be 1..22.");

        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(CurrentVersion);
            writer.Write(schematic.Width);
            writer.Write(schematic.Height);
            writer.Write(schematic.AnchorX);
            writer.Write(schematic.AnchorY);

            var meta = new SchematicMeta
            {
                Name = schematic.Name,
                Author = schematic.Author,
                Description = schematic.Description,
                Tags = schematic.Tags,
                CreatedAt = schematic.CreatedAt
            };
            byte[] metaJson = JsonSerializer.SerializeToUtf8Bytes(meta);
            writer.Write(metaJson.Length);
            writer.Write(metaJson);

            byte[] rawBuffer = new byte[schematic.Tiles.Length * TileDataBlock.ByteSize];
            CopyTilesToBytes(schematic.Tiles, rawBuffer);

            using var compressor = new Compressor(compressionLevel);
            byte[] compressedData = compressor.Wrap(rawBuffer).ToArray();
            writer.Write(compressedData.Length);
            writer.Write(compressedData);
        }

        return memory.ToArray();
    }

    public static Schematic Deserialize(byte[] data)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length < HeaderBytes)
            throw new InvalidDataException("Schematic is truncated.");

        using var memory = new MemoryStream(data);
        using var reader = new BinaryReader(memory, Encoding.UTF8);

        byte[] magic = reader.ReadBytes(4);
        if (magic.Length != 4 || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
            throw new InvalidDataException("Invalid Hakoniwa schematic magic header.");

        ushort version = reader.ReadUInt16();
        if (version != CurrentVersion)
            throw new NotSupportedException($"Unsupported schematic version: {version}");

        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        int anchorX = reader.ReadInt32();
        int anchorY = reader.ReadInt32();

        int metaLen = ReadPayloadLength(reader, memory, "metadata");
        byte[] metaBytes = reader.ReadBytes(metaLen);
        var meta = metaLen == 0 ? null : JsonSerializer.Deserialize<SchematicMeta>(metaBytes);

        int compressedLen = ReadPayloadLength(reader, memory, "tile payload");
        byte[] compressedBytes = reader.ReadBytes(compressedLen);

        var schematic = new Schematic(width, height)
        {
            AnchorX = anchorX,
            AnchorY = anchorY,
            Name = meta?.Name ?? "Untitled",
            Author = meta?.Author ?? "Anonymous",
            Description = meta?.Description ?? string.Empty,
            Tags = meta?.Tags ?? [],
            CreatedAt = meta?.CreatedAt ?? DateTime.UtcNow
        };

        int rawByteSize = schematic.Tiles.Length * TileDataBlock.ByteSize;
        using var decompressor = new Decompressor();
        byte[] rawBuffer = decompressor.Unwrap(compressedBytes, rawByteSize).ToArray();
        if (rawBuffer.Length != rawByteSize)
            throw new InvalidDataException($"Tile payload size mismatch: expected {rawByteSize}, got {rawBuffer.Length}.");

        CopyBytesToTiles(rawBuffer, schematic.Tiles);
        return schematic;
    }

    public static void Save(Schematic schematic, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));
        File.WriteAllBytes(path, Serialize(schematic));
    }

    public static Schematic Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));
        return Deserialize(File.ReadAllBytes(path));
    }

    private static int ReadPayloadLength(BinaryReader reader, MemoryStream memory, string name)
    {
        int length = reader.ReadInt32();
        if (length < 0 || length > memory.Length - memory.Position)
            throw new InvalidDataException($"Invalid {name} length.");
        return length;
    }

    private static unsafe void CopyTilesToBytes(TileDataBlock[] tiles, byte[] dest)
    {
        int expected = tiles.Length * TileDataBlock.ByteSize;
        if (dest.Length < expected)
            throw new ArgumentException("Destination buffer is too small.", nameof(dest));
        if (Marshal.SizeOf<TileDataBlock>() != TileDataBlock.ByteSize)
            throw new InvalidOperationException("TileDataBlock layout drifted from ByteSize.");

        fixed (TileDataBlock* src = tiles)
        fixed (byte* dst = dest)
            Buffer.MemoryCopy(src, dst, dest.Length, expected);
    }

    private static unsafe void CopyBytesToTiles(byte[] src, TileDataBlock[] tiles)
    {
        int expected = tiles.Length * TileDataBlock.ByteSize;
        if (src.Length < expected)
            throw new InvalidDataException("Tile payload is truncated.");

        fixed (byte* s = src)
        fixed (TileDataBlock* dst = tiles)
            Buffer.MemoryCopy(s, dst, expected, expected);
    }

    private sealed class SchematicMeta
    {
        public string Name { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] Tags { get; set; } = [];
        public DateTime CreatedAt { get; set; }
    }
}
