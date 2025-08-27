using System.IO;
using ReCap.Unpacker.Core;
using ReCap.Unpacker.Core.FileStructures;

namespace ReCap.Unpacker.Core.DBPF
{
    public sealed class DBPFConverter : Converter
    {
        public const int TYPE_ID = 0x06EFC6AA;

        private static DBPFUnpacker CreateUnpackTask(IStreamReader stream, DirectoryInfo outDir)
        {
            if (outDir.Exists)
            {
                foreach (var f in outDir.GetFiles())
                    f.Delete();
            }
            else
            {
                outDir.Create();
            }

            // Keep base so the decoder can restore it after unpacking.
            stream.SetBaseOffset(stream.GetFilePointer());
            return new DBPFUnpacker(stream, outDir);
        }

        public override bool Decode(IStreamReader stream, DirectoryInfo outputFolder, ResourceKey key)
        {
            long oldBase = stream.GetBaseOffset();

            var outDir = Converter.GetOutputFile(key, outputFolder, "unpacked");
            var task = CreateUnpackTask(stream, outDir);
            task.Call();

            stream.SetBaseOffset(oldBase);
            return true;
        }

        public override bool IsDecoder(ResourceKey key) => key.GetTypeID() == TYPE_ID;

        public override string GetName() => "Localization Package (." + HashManager.Get().GetTypeName(TYPE_ID) + ")";
    }
}
