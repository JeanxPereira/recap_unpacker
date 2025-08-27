using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ReCap.Unpacker.Core;
using ReCap.Unpacker.Core.FileStructures;

namespace ReCap.Unpacker.Core.DBPF
{
    public sealed class DBPFUnpacker
    {
        public delegate bool DBPFItemFilter(DBPFItem item);

        private readonly List<FileInfo> _inputFiles = new();
        private readonly IStreamReader? _inputStream;
        private DirectoryInfo _outputFolder;
        private readonly Dictionary<DBPFItem, Exception> _exceptions = new();
        private readonly List<Converter> _converters = new();
        private DBPFItemFilter? _itemFilter;

        public DBPFUnpacker(FileInfo inputFile, DirectoryInfo outputFolder, List<Converter> converters)
        {
            _inputFiles.Add(inputFile);
            _outputFolder = outputFolder;
            _converters = converters;
            _inputStream = null;
        }

        public DBPFUnpacker(IStreamReader inputStream, DirectoryInfo outputFolder)
        {
            _inputStream = inputStream;
            _outputFolder = outputFolder;
        }
        public DBPFUnpacker(ICollection<System.IO.FileInfo> inputFiles, System.IO.DirectoryInfo outputFolder, System.Collections.Generic.List<Converter> converters)
        {
            _inputFiles.AddRange(inputFiles);
            _outputFolder = outputFolder;
            _converters = converters;
            _inputStream = null;
        }
        
        private static void FindNamesFile(List<DBPFItem> items, IStreamReader s, HashManager hasher)
        {
            int group = hasher.GetFileHash("sporemaster");
            int name = hasher.GetFileHash("names");

            foreach (var it in items)
            {
                if (it.Name.GetGroupID() == group && it.Name.GetInstanceID() == name)
                {
                    using var buf = it.ProcessFile(s);
                    using var ms = new MemoryStream(buf.GetRawData());
                    using var sr = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);
                    hasher.GetProjectRegistry().Read(sr);
                    return;
                }
            }
        }

        private static void LoadRegistry(HashManager hasher)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "reg", "reg_file.txt"),
                Path.Combine(AppContext.BaseDirectory, "reg_file.txt"),
                Path.Combine(Environment.CurrentDirectory, "reg", "reg_file.txt"),
                Path.Combine(Environment.CurrentDirectory, "reg_file.txt"),
            };

            foreach (var p in candidates)
            {
                if (File.Exists(p))
                {
                    using var sr = File.OpenText(p);
                    hasher.GetProjectRegistry().Read(sr);
                    Console.WriteLine($"[registry] {p}");
                    return;
                }
            }

            var asm = typeof(DBPFUnpacker).Assembly;
            string? res = null;
            foreach (var name in asm.GetManifestResourceNames())
                if (name.EndsWith("reg_file.txt", StringComparison.OrdinalIgnoreCase)) { res = name; break; }

            if (res != null)
            {
                using var stream = asm.GetManifestResourceStream(res)!;
                using var sr = new StreamReader(stream, Encoding.UTF8, true);
                hasher.GetProjectRegistry().Read(sr);
                Console.WriteLine($"[registry] embedded:{res}");
                return;
            }

            Console.Error.WriteLine("[registry] reg_file.txt not found; names may be hex.");
        }

        private void UnpackStream(IStreamReader packageStream, Dictionary<int, List<ResourceKey>>? writtenFiles)
        {
            var hasher = new HashManager();
            hasher.Initialize();

            LoadRegistry(hasher);

            var header = new DatabasePackedFile();
            header.ReadHeader(packageStream);
            header.ReadIndex(packageStream);

            var index = header.Index;
            index.ReadItems(packageStream, header.IndexCount, header.IsDBBF);
            Console.WriteLine($"[DEBUG] IsDBBF={header.IsDBBF}, IndexCount={header.IndexCount}, IndexOffset=0x{header.IndexOffset:X}, IndexSize={header.IndexSize}");
            Console.WriteLine($"[DEBUG] Parsed items: {index.Items.Count}");
            if (index.Items.Count > 0)
            {
                var it0 = index.Items[0];
                Console.WriteLine($"[DEBUG] First item: G=0x{it0.Name.GetGroupID():X8} T=0x{it0.Name.GetTypeID():X8} I=0x{it0.Name.GetInstanceID():X8}");
            }
            else
            {
                Console.WriteLine("[DEBUG] Nenhum item no índice (verificar parsing/seek).");
            }
            FindNamesFile(index.Items, packageStream, hasher);

            foreach (var item in index.Items)
            {
                if (_itemFilter != null && !_itemFilter(item))
                    continue;

                int groupID    = item.Name.GetGroupID();
                int instanceID = item.Name.GetInstanceID();

                if (writtenFiles != null && writtenFiles.TryGetValue(groupID, out var list))
                {
                    if (list.Exists(k => k.IsEquivalent(item.Name)))
                        continue;
                }

                string fileName = hasher.GetFileName(instanceID);

                // Skip auto_ files under group 0x02FABF01.
                if (groupID == 0x02FABF01 && fileName.StartsWith("auto_", StringComparison.OrdinalIgnoreCase))
                    continue;

                var folder = new DirectoryInfo(Path.Combine(_outputFolder.FullName, hasher.GetFileName(groupID)));
                folder.Create();

                try
                {
                    using var data = item.ProcessFile(packageStream);

                    bool converted = false;

                    if (groupID != 0x40404000 || item.Name.GetTypeID() != 0x00B1B104)
                    {
                        foreach (var conv in _converters)
                        {
                            if (conv.IsDecoder(item.Name))
                            {
                                using var mem = new MemoryReadWriteStream(data.GetRawData());
                                if (conv.Decode(mem, folder, item.Name))
                                {
                                    converted = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (!converted)
                    {
                        string outName = hasher.GetFileName(item.Name.GetInstanceID()) + "." + hasher.GetTypeName(item.Name.GetTypeID());
                        var outFile = new FileInfo(Path.Combine(folder.FullName, outName));
                        data.WriteToFile(outFile);
                    }

                    if (writtenFiles != null)
                    {
                        if (!writtenFiles.TryGetValue(groupID, out var list2))
                        {
                            list2 = new List<ResourceKey>();
                            writtenFiles[groupID] = list2;
                        }
                        list2.Add(item.Name);
                    }
                }
                catch (Exception ex)
                {
                    _exceptions[item] = ex;
                    Console.Error.WriteLine($"[ERROR] {item.Name}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            hasher.GetProjectRegistry().Clear();
        }

        public void AddConverter(Converter c) => _converters.Add(c);
        public void SetItemFilter(DBPFItemFilter filter) => _itemFilter = filter;
        
        public Exception? Call()
        {
            if (_inputStream != null)
            {
                UnpackStream(_inputStream, null);
            }
            else
            {
                var written = new Dictionary<int, List<ResourceKey>>();
                bool checkDup = _inputFiles.Count > 1;

                foreach (var fi in _inputFiles)
                {
                    if (!fi.Exists) return new FileNotFoundException("Input file does not exist", fi.FullName);

                    using var s = new FileReadWriteStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                    UnpackStream(s, checkDup ? written : null);
                }
            }
            return null;
        }
    }
}
