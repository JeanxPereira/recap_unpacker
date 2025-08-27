using System;
using System.IO;
using ReCap.Unpacker.Core.FileStructures;

namespace ReCap.Unpacker.Core.DBPF
{
    public sealed class DatabasePackedFile
    {
        private const int TYPE_DBPF = 0x46504244; // "DBPF" LE
        private const int TYPE_DBBF = 0x46424244; // "DBBF" LE

        public int MajorVersion = 3;
        public int MinVersion = 0;
        public int IndexMajorVersion = 0;
        public int IndexMinorVersion = 3;
        public bool IsDBBF;

        public readonly DBPFIndex Index = new();

        public int IndexCount;
        public long IndexOffset;
        public long IndexSize;

        private void ReadDBPF(IStreamReader s)
        {
            MajorVersion = s.ReadLEInt();
            MinVersion   = s.ReadLEInt();

            s.Skip(20);
            IndexMajorVersion = s.ReadLEInt();
            IndexCount        = s.ReadLEInt();
            s.Skip(4);
            IndexSize         = s.ReadLEUInt();
            s.Skip(12);
            IndexMinorVersion = s.ReadLEInt();
            IndexOffset       = s.ReadLEUInt();
        }

        private void ReadDBBF(IStreamReader s)
        {
            MajorVersion = s.ReadLEInt();
            MinVersion   = s.ReadLEInt();

            s.Skip(20);
            IndexMajorVersion = s.ReadLEInt();
            IndexCount        = s.ReadLEInt();

            // Java DBBF reads these as 32-bit ints in this variant.
            IndexSize   = s.ReadLEInt();
            s.Skip(8);
            IndexMinorVersion = s.ReadLEInt();
            IndexOffset = s.ReadLEInt();
        }

        public void ReadHeader(IStreamReader s)
        {
            int magic = s.ReadLEInt();
            if (magic == TYPE_DBPF) { IsDBBF = false; ReadDBPF(s); }
            else if (magic == TYPE_DBBF) { IsDBBF = true; ReadDBBF(s); }
            else throw new IOException("Unrecognised DBPF type magic: 0x" + magic.ToString("X8"));
        }

        public void ReadIndex(IStreamReader s)
        {
            s.Seek(IndexOffset);
            Index.Read(s);
        }

        public void WriteIndex(IStreamWriter s)
        {
            IndexOffset = s.GetFilePointer();

            long baseOff = s.GetFilePointer();
            Index.Write(s);
            Index.WriteItems(s, IsDBBF);
            IndexSize = s.GetFilePointer() - baseOff;
        }

        public void ReadAll(IStreamReader s)
        {
            ReadHeader(s);
            ReadIndex(s);
            Index.ReadItems(s, IndexCount, IsDBBF);
        }

        public DBPFItem? GetItem(ResourceKey key)
        {
            foreach (var it in Index.Items)
                if (it.Name.Equals(key)) return it;
            return null;
        }
    }
}
