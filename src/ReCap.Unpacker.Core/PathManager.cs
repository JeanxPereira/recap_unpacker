using System;
using System.IO;

namespace ReCap.Unpacker.Core
{
    public sealed class PathManager
    {
        private static readonly Lazy<PathManager> _inst = new(() => new PathManager());
        public static PathManager Get() => _inst.Value;

        public string ProgramDir { get; private set; } = "";
        public string RegDir     { get; private set; } = "";

        public void Initialize()
        {
            ProgramDir = Path.GetFullPath(AppContext.BaseDirectory);
            RegDir     = Path.Combine(ProgramDir, "reg");

            if (!Directory.Exists(RegDir))
                throw new DirectoryNotFoundException(
                    $"The 'reg' folder was not found next to the executable. Expected: {RegDir}");

            var log = LoggerManager.GetLogger<PathManager>();
            log.Fine($"ProgramDir = {ProgramDir}");
            log.Fine($"RegDir     = {RegDir}");
        }

        public FileInfo GetProgramFile(string name)
        {
            if (Path.IsPathRooted(name)) return new FileInfo(name);
            return new FileInfo(Path.Combine(RegDir, name));
        }
    }
}
