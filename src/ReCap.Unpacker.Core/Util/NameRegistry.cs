using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ReCap.Unpacker.Core.Util
{
    public sealed class NameRegistry
    {
        private readonly Dictionary<string, int> _hashes = new(StringComparer.Ordinal);
        private readonly Dictionary<int, string> _names = new();

        public string FileName { get; }
        public string Description { get; }

        public NameRegistry(string description, string fileName)
        {
            Description = description;
            FileName = fileName;
        }

        public void Clear()
        {
            _hashes.Clear();
            _names.Clear();
        }

        public string? GetName(int hash) => _names.TryGetValue(hash, out var s) ? s : null;

        public int? GetHash(string name)
        {
            if (name is null) return null;
            return _hashes.TryGetValue(name, out var h)
                ? h
                : _hashes.TryGetValue(name.ToLowerInvariant(), out var hl) ? hl : (int?)null;
        }

        public void Add(string name, int hash)
        {
            if (name is null) return;
            var key = name.EndsWith("~", StringComparison.Ordinal) ? name.ToLowerInvariant() : name;
            _hashes[key] = hash;
            _names[hash] = name;
        }

        public void Read(FileInfo file)
        {
            using var sr = new StreamReader(file.FullName, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            Read(sr);
        }

        public void Read(TextReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var noComment = StripComment(line).Trim();
                if (noComment.Length == 0) continue;
                if (noComment.StartsWith("#", StringComparison.Ordinal)) continue;

                ParseEntry(noComment);
            }
        }

        public void WriteTo(TextWriter writer)
        {
            var eol = Environment.NewLine;
            foreach (var kv in _names)
            {
                var hash = kv.Key;
                var name = kv.Value;
                var needExplicitHash =
                    name.EndsWith("~", StringComparison.Ordinal) ||
                    ReCap.Unpacker.Core.HashManager.Get().FnvHash(name) != hash;

                if (needExplicitHash)
                    writer.Write($"{name}\t0x{hash:x}{eol}");
                else
                    writer.Write($"{name}{eol}");
            }
        }

        public bool IsEmpty => _names.Count == 0 && _hashes.Count == 0;

        public IEnumerable<string> GetNames() => _names.Values;

        // -------- internals --------

        private static string StripComment(string s)
        {
            var idx = s.IndexOf("//", StringComparison.Ordinal);
            return idx >= 0 ? s[..idx] : s;
        }

        private void ParseEntry(string str)
        {
            var parts = str.Split('\t');
            var name = parts[0].Trim();

            if (parts.Length < 2)
            {
                var hash = ReCap.Unpacker.Core.HashManager.Get().FnvHash(name);
                _names[hash] = name;
                return;
            }

            var hashStr = parts[1].Trim();
            var hashVal = ReCap.Unpacker.Core.HashManager.Get().Int32(hashStr);

            if (name.EndsWith("~", StringComparison.Ordinal))
                _hashes[name.ToLowerInvariant()] = hashVal;
            else
                _hashes[name] = hashVal;

            _names[hashVal] = name;
        }
    }
}
