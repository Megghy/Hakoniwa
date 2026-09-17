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
    private const ushort CurrentVersion = 0x0002;
    private const ushort MinVersion = 0x0001;
    private const int HeaderBytes = 4 + 2 + 4 + 4 + 4 + 4 + 4 + 4;

    public static byte[] Serialize(Schematic schematic, int compressionLevel = 10)
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

            byte[] entities = PackEntities(schematic);
            byte[] packed = entities.Length == 0 ? [] : compressor.Wrap(entities).ToArray();
            writer.Write(packed.Length);
            if (packed.Length > 0)
                writer.Write(packed);
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
        if (version < MinVersion || version > CurrentVersion)
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
        if (version >= 0x0002 && memory.Position < memory.Length)
        {
            int packedLen = ReadPayloadLength(reader, memory, "entity payload");
            if (packedLen > 0)
            {
                byte[] packed = reader.ReadBytes(packedLen);
                using var entityDec = new Decompressor();
                byte[] raw = entityDec.Unwrap(packed).ToArray();
                UnpackEntities(schematic, raw);
            }
        }

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

    private static byte[] PackEntities(Schematic schematic)
    {
        if (schematic.Entities.Count == 0)
            return [];
        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(schematic.Entities.Count);
            for (int i = 0; i < schematic.Entities.Count; i++)
            {
                var entity = schematic.Entities[i];
                writer.Write((byte)entity.Kind);
                writer.Write(entity.X);
                writer.Write(entity.Y);
                writer.Write(entity.Name ?? string.Empty);
                writer.Write(entity.Text ?? string.Empty);
                writer.Write(entity.TeType);
                writer.Write(entity.Items.Length);
                for (int n = 0; n < entity.Items.Length; n++)
                {
                    writer.Write(entity.Items[n].Type);
                    writer.Write(entity.Items[n].Stack);
                    writer.Write(entity.Items[n].Prefix);
                }

                writer.Write(entity.Extra.Length);
                writer.Write(entity.Extra);
            }
        }

        return memory.ToArray();
    }

    private static void UnpackEntities(Schematic schematic, byte[] raw)
    {
        using var memory = new MemoryStream(raw);
        using var reader = new BinaryReader(memory, Encoding.UTF8);
        int count = reader.ReadInt32();
        if (count < 0 || count > schematic.Width * schematic.Height + 16)
            throw new InvalidDataException("Entity count is out of range.");
        schematic.Entities.Clear();
        for (int i = 0; i < count; i++)
        {
            var entity = new SchematicEntity
            {
                Kind = (SchematicEntityKind)reader.ReadByte(),
                X = reader.ReadInt32(),
                Y = reader.ReadInt32(),
                Name = reader.ReadString(),
                Text = reader.ReadString(),
                TeType = reader.ReadByte(),
            };
            int items = reader.ReadInt32();
            if (items < 0 || items > 200)
                throw new InvalidDataException("Entity item count is out of range.");
            entity.Items = new SchematicItem[items];
            for (int n = 0; n < items; n++)
                entity.Items[n] = new SchematicItem(reader.ReadInt32(), reader.ReadInt32(), reader.ReadByte());
            int extra = reader.ReadInt32();
            if (extra < 0 || extra > raw.Length)
                throw new InvalidDataException("Entity extra payload is invalid.");
            entity.Extra = extra == 0 ? [] : reader.ReadBytes(extra);
            schematic.Entities.Add(entity);
        }
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
