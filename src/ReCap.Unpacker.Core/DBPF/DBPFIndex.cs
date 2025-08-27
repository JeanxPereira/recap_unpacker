using System.Collections.Generic;
using ReCap.Unpacker.Core.FileStructures;

namespace ReCap.Unpacker.Core.DBPF
{
    public sealed class DBPFIndex
    {
        public int GroupID = -1;
        public int TypeID = -1;
        public readonly List<DBPFItem> Items = new();
        private long _itemsOffset;

        public void Read(IStreamReader s)
        {
            int typeFlags = s.ReadLEInt();
            if ((typeFlags & (1 << 0)) != 0) TypeID = s.ReadLEInt();
            if ((typeFlags & (1 << 1)) != 0) GroupID = s.ReadLEInt();
            if ((typeFlags & (1 << 2)) != 0) s.ReadLEInt(); // unknown

            _itemsOffset = s.GetFilePointer();
            System.Console.WriteLine($"[DEBUG] DBPFIndex.Read: flags=0x{typeFlags:X}, TypeID={TypeID}, GroupID={GroupID}, itemsOffset=0x{_itemsOffset:X}");
        }

        public void Write(IStreamWriter s)
        {
            int typeFlags = 0;
            typeFlags |= 1 << 2;
            if (TypeID != -1) typeFlags |= 1 << 0;
            if (GroupID != -1) typeFlags |= 1 << 1;

            s.WriteLEInt(typeFlags);

            if (TypeID != -1) s.WriteLEInt(TypeID);
            if (GroupID != -1) s.WriteLEInt(GroupID);
            s.WriteLEInt(0);
        }

        public void ReadItems(IStreamReader s, int numItems, bool isDBBF)
        {
            s.Seek(_itemsOffset);
            System.Console.WriteLine($"[DEBUG] DBPFIndex.ReadItems: after Seek pos=0x{s.GetFilePointer():X}, expect=0x{_itemsOffset:X}");
            bool readGroup = GroupID == -1;
            bool readType  = TypeID == -1;

            for (int i = 0; i < numItems; i++)
            {
                var item = new DBPFItem();

                if (!readGroup) item.Name.SetGroupID(GroupID);
                if (!readType)  item.Name.SetTypeID(TypeID);

                item.Read(s, isDBBF, readType, readGroup);
                Items.Add(item);
            }
        }

        public void WriteItems(IStreamWriter s, bool isDBBF)
        {
            bool writeGroup = GroupID == -1;
            bool writeType  = TypeID == -1;

            foreach (var it in Items)
                it.Write(s, isDBBF, writeType, writeGroup);
        }
    }
}
