using System.IO;
using ReCap.Unpacker.Core.FileStructures;

namespace ReCap.Unpacker.Core
{
    public abstract class Converter
    {
        public abstract bool Decode(IStreamReader stream, DirectoryInfo outputFolder, ResourceKey key);
        public abstract bool IsDecoder(ResourceKey key);
        public abstract string GetName();
        public virtual void Reset() { }
        public static DirectoryInfo GetOutputFile(ResourceKey key, DirectoryInfo folder, string extraExtension)
        {
            var hasher = HashManager.Get();
            var name = hasher.GetFileName(key.GetInstanceID()) + "." +
                       hasher.GetTypeName(key.GetTypeID()) + "." +
                       extraExtension;
            return new DirectoryInfo(Path.Combine(folder.FullName, name));
        }
    }
}
