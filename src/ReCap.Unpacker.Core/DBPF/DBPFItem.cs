using System;
using System.IO;
using ReCap.Unpacker.Core.FileStructures;

namespace ReCap.Unpacker.Core.DBPF
{
    public sealed class DBPFItem
    {
        public bool IsCompressed;
        public long ChunkOffset;
        public int MemSize;
        public int CompressedSize;

        public readonly ResourceKey Name = new();
        public bool IsSaved = true; // In .package files this is always true.

        public void Read(IStreamReader s, bool isDBBF, bool readType, bool readGroup)
        {
            if (readType)  Name.SetTypeID(s.ReadLEInt());
            if (readGroup) Name.SetGroupID(s.ReadLEInt());
            Name.SetInstanceID(s.ReadLEInt());

            ChunkOffset = isDBBF ? s.ReadLELong() : s.ReadLEUInt();

            CompressedSize = s.ReadLEInt() & 0x7FFFFFFF;
            MemSize        = s.ReadLEInt();

            short comp = s.ReadLEShort();
            switch (comp)
            {
                case 0:   IsCompressed = false; break;
                case -1:  IsCompressed = true;  break;
                default:  throw new IOException("Unknown compression label at position " + s.GetFilePointer());
            }

            IsSaved = s.ReadBoolean();
            s.Skip(1);

#if DEBUG
            if (MemSize <= 0 || CompressedSize < 0 || ChunkOffset <= 0)
            {
                System.Console.WriteLine(
                    $"[DEBUG] Suspicious item meta @0x{s.GetFilePointer():X}: " +
                    $"G=0x{Name.GetGroupID():X8} T=0x{Name.GetTypeID():X8} I=0x{Name.GetInstanceID():X8} " +
                    $"Off=0x{ChunkOffset:X} Comp={CompressedSize} Mem={MemSize} Compressed={IsCompressed}"
                );
            }
#endif
        }

        public void Write(IStreamWriter s, bool isDBBF, bool writeType, bool writeGroup)
        {
            if (writeType)  s.WriteLEInt(Name.GetTypeID());
            if (writeGroup) s.WriteLEInt(Name.GetGroupID());
            s.WriteLEInt(Name.GetInstanceID());

            if (isDBBF) s.WriteLELong(ChunkOffset);
            else        s.WriteLEUInt(ChunkOffset);

            s.WriteLEInt(CompressedSize | unchecked((int)0x80000000));
            s.WriteLEInt(MemSize);
            s.WriteLEShort(IsCompressed ? unchecked((short)0xFFFF) : (short)0);
            s.WriteBoolean(IsSaved);
            s.WritePadding(1);
        }

        public MemoryBuffer ProcessFile(IStreamReader s)
        {
            s.Seek(ChunkOffset);

            if (IsCompressed)
            {
                var comp = new byte[CompressedSize];
                ReadFully(s, comp);

                var raw = new byte[MemSize];
                RefPackCompression.DecompressFast(comp, raw);
                return new MemoryBuffer(raw);
            }
            else
            {
                var raw = new byte[MemSize];
                ReadFully(s, raw);
                return new MemoryBuffer(raw);
            }
        }

        private static void ReadFully(IStreamReader s, byte[] buffer)
        {
            s.Read(buffer);
        }
    }

    public sealed class MemoryBuffer : IDisposable
    {
        private byte[] _data;
        public MemoryBuffer(byte[] data) { _data = data; }
        public byte[] GetRawData() => _data;

        public void WriteToFile(FileInfo fi)
        {
            Directory.CreateDirectory(fi.DirectoryName!);
            File.WriteAllBytes(fi.FullName, _data);
        }

        public void Dispose() { _data = Array.Empty<byte>(); }
    }
}
