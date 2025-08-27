using System;
using System.Collections.Generic;
using System.IO;
using ReCap.Unpacker.Core.FileStructures;

namespace ReCap.Unpacker.Core.DBPF
{
    public sealed class DBPFUnpackingTask
    {
        public delegate bool DBPFItemFilter(DBPFItem item);

        const double INDEX_PROGRESS = 0.05;
        const double CLEAR_FOLDER_PROGRESS = 0.10;

        private List<Converter> converters = new();
        private readonly List<FileInfo> inputFiles = new();
        private readonly IStreamReader? inputStream;
        private readonly List<FileInfo> failedDBPFs = new();
        private DirectoryInfo outputFolder;
        private readonly Dictionary<DBPFItem, Exception> exceptions = new();
        private long ellapsedTime;
        private DBPFItemFilter? itemFilter;
        //private bool isParallel = true;
        //private bool noJavaFX = false;
        //private Action<double>? noJavaFXProgressListener;

        public DBPFUnpackingTask(FileInfo inputFile, DirectoryInfo outputFolder, List<Converter> converters)
        {
            this.inputFiles.Add(inputFile);
            this.outputFolder = outputFolder;
            this.converters = converters;
            this.inputStream = null;
        }

        public DBPFUnpackingTask(ICollection<FileInfo> inputFiles, DirectoryInfo outputFolder, List<Converter> converters)
        {
            this.inputFiles.AddRange(inputFiles);
            this.outputFolder = outputFolder;
            this.converters = converters;
            this.inputStream = null;
        }

        public DBPFUnpackingTask(IStreamReader inputStream, DirectoryInfo outputFolder)
        {
            this.inputStream = inputStream;
            this.outputFolder = outputFolder;
        }

        //public void setNoJavaFX() { noJavaFX = true; }
        //public void setNoJavaFXProgressListener(Action<double> listener) { noJavaFXProgressListener = listener; }

        public List<Converter> getConverters() => converters;
        public List<FileInfo> getInputFiles() => inputFiles;
        public DirectoryInfo getOutputFolder() => outputFolder;

        public object? getProject() => null;

        public void setItemFilter(DBPFItemFilter filter) => itemFilter = filter;

        public Exception? call()
        {
            var start = DateTimeOffset.UtcNow;

            if (inputStream != null)
            {
                var unpacker = new DBPFUnpacker(inputStream, outputFolder);
                if (itemFilter != null) unpacker.SetItemFilter(item => itemFilter(item));
                foreach (var conv in converters) unpacker.AddConverter(conv);
                var err = unpacker.Call();
                ellapsedTime = (long)(DateTimeOffset.UtcNow - start).TotalMilliseconds;
                return err;
            }
            else
            {
                var unpacker = new DBPFUnpacker(inputFiles, outputFolder, converters);
                if (itemFilter != null) unpacker.SetItemFilter(item => itemFilter(item));
                var err = unpacker.Call();
                ellapsedTime = (long)(DateTimeOffset.UtcNow - start).TotalMilliseconds;
                return err;
            }
        }
    }
}
