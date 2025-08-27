using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ReCap.Unpacker.Core;
using ReCap.Unpacker.Core.DBPF;

namespace ReCap.Unpacker
{
    internal static class Program
    {
        public static string Version = "0.0.10";

        private static int Main(string[] args)
        {
            bool debug = false;
            var rest = new List<string>(args);
            for (int i = rest.Count - 1; i >= 0; i--)
            {
                var a = rest[i];
                if (a == "-d" || a == "--debug") { debug = true; rest.RemoveAt(i); }
            }

            if (rest.Count != 2)
            {
                Console.Error.WriteLine($"dbpf_unpacker v{Version}");
                Console.Error.WriteLine("usage: dbpf_unpacker [-d|--debug] <file> <destination>");
                return 1;
            }

            var inputFile = new FileInfo(rest[0]);
            var outputDir = new DirectoryInfo(rest[1]);

            if (debug) Console.WriteLine("Debug mode enabled");

            if (!inputFile.Exists)
            {
                Console.Error.WriteLine($"error: input file does not exist: {inputFile.FullName}");
                return 1;
            }

            try { if (!outputDir.Exists) outputDir.Create(); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"error: could not create output folder: {ex.Message}");
                return 1;
            }

            if (debug)
            {
                Console.WriteLine($"Input : {inputFile.FullName}");
                Console.WriteLine($"Output: {outputDir.FullName}");
            }

            try
            {
                HashManager.Get().Initialize();

                var converters = new List<Converter> { new DBPFConverter() };
                var unpacker = new DBPFUnpacker(inputFile, outputDir, converters);

                var sw = Stopwatch.StartNew();
                var err = unpacker.Call();
                sw.Stop();

                if (err != null)
                {
                    Console.Error.WriteLine($"error: {err.GetType().Name}: {err.Message}");
                    if (debug) Console.Error.WriteLine(err);
                    return 1;
                }

                int itemErrors = 0;
                var prop = typeof(DBPFUnpacker).GetProperty("ErrorCount");
                if (prop != null)
                {
                    itemErrors = (int)(prop.GetValue(unpacker) ?? 0);
                }

                if (itemErrors > 0)
                {
                    Console.Error.WriteLine($"finished with {itemErrors} item errors ({sw.ElapsedMilliseconds} ms)");
                }
                else
                {
                    Console.WriteLine($"done ({sw.ElapsedMilliseconds} ms)");
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"fatal: {ex.GetType().Name}: {ex.Message}");
                if (debug) Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }
}
