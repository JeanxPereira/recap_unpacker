using System;
using System.Collections.Generic;
using System.Globalization;
using ReCap.Unpacker.Core.Util;
using ReCap.Unpacker.Core.FileStructures;

namespace ReCap.Unpacker.Core
{
    public sealed class HashManager
    {
        private static readonly Lazy<HashManager> _inst = new(() => new HashManager());
        public static HashManager Get() => _inst.Value;

        public HashManager() { }

        private readonly NumberFormatInfo _nfi = new() { NumberDecimalSeparator = "." };
        //_decimalFormat = "#.#######";

        private readonly NameRegistry _originalFileRegistry  = new("File Names", "reg_file.txt");
        private readonly NameRegistry _originalTypeRegistry  = new("Types", "reg_type.txt");
        private readonly NameRegistry _originalPropRegistry  = new("Properties", "reg_property.txt");
        private readonly NameRegistry _simulatorRegistry     = new("Simulator Attributes", "reg_simulator.txt");

        private NameRegistry _fileRegistry  = null!;
        private NameRegistry _typeRegistry  = null!;
        private NameRegistry _propRegistry  = null!;

        private readonly NameRegistry _projectRegistry = new("Names used by the project", "names.txt");
        private bool _updateProjectRegistry = false;

        private NameRegistry _extraRegistry = null!;

        private readonly Dictionary<string, NameRegistry> _registries = new();

        public void Initialize()
        {
            PathManager.Get().Initialize();

            _fileRegistry = _originalFileRegistry;
            _typeRegistry = _originalTypeRegistry;
            _propRegistry = _originalPropRegistry;

            try { _fileRegistry.Read(PathManager.Get().GetProgramFile(_fileRegistry.FileName)); }
            catch (Exception ex) { throw new Exception($"The file name registry ({_fileRegistry.FileName}) is corrupt or missing. Expected in: {PathManager.Get().RegDir}. Details: {ex.Message}"); }

            try { _typeRegistry.Read(PathManager.Get().GetProgramFile(_typeRegistry.FileName)); }
            catch (Exception ex) { throw new Exception($"The types registry ({_typeRegistry.FileName}) is corrupt or missing. Expected in: {PathManager.Get().RegDir}. Details: {ex.Message}"); }

            try { _propRegistry.Read(PathManager.Get().GetProgramFile(_propRegistry.FileName)); }
            catch (Exception ex) { throw new Exception($"The property registry ({_propRegistry.FileName}) is corrupt or missing. Expected in: {PathManager.Get().RegDir}. Details: {ex.Message}"); }

            try
            {
                _simulatorRegistry.Read(PathManager.Get().GetProgramFile(_simulatorRegistry.FileName));
                _simulatorRegistry.Read(PathManager.Get().GetProgramFile("reg_simulator_stub.txt"));
            }
            catch (Exception ex)
            {
                throw new Exception($"The simulator registry files are corrupt or missing (reg_simulator.txt/reg_simulator_stub.txt). Expected in: {PathManager.Get().RegDir}. Details: {ex.Message}");
            }


            _registries[_fileRegistry.FileName] = _fileRegistry;
            _registries[_typeRegistry.FileName] = _typeRegistry;
            _registries[_propRegistry.FileName] = _propRegistry;
            _registries[_simulatorRegistry.FileName] = _simulatorRegistry;
            _registries[_projectRegistry.FileName] = _projectRegistry;
        }

        public NameRegistry GetProjectRegistry() => _projectRegistry;

        public int FnvHash(string s)
        {
            if (s == null) return 0;
            uint rez = 0x811C9DC5u;
            var lower = s.ToLowerInvariant();
            unchecked
            {
                for (int i = 0; i < lower.Length; i++)
                {
                    rez *= 0x1000193u;
                    rez ^= lower[i];
                }
            }
            return unchecked((int)rez);
        }

        public string HexToString(int num)   => "0x" + num.ToString("x8", CultureInfo.InvariantCulture);
        public string HexToStringUC(int num) => "0x" + num.ToString("X8", CultureInfo.InvariantCulture);

        public int Int32(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return 0;

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return (int)uint.Parse(str.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            if (str.StartsWith("#", StringComparison.Ordinal))
                return (int)uint.Parse(str.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            if (str.StartsWith("$", StringComparison.Ordinal))
                return GetFileHash(str.Substring(1));

            if (str.EndsWith("b", StringComparison.Ordinal))
                return Convert.ToInt32(str[..^1], 2);

            return int.Parse(str, CultureInfo.InvariantCulture);
        }

        public string GetFileName(int hash)
        {
            var s = GetFileNameOptional(hash);
            return s ?? HexToStringUC(hash);
        }

        private string? GetFileNameOptional(int hash)
        {
            var s = _fileRegistry.GetName(hash);
            if (s != null) return s;
            return _projectRegistry.GetName(hash);
        }

        public string GetTypeName(int hash)
        {
            var s = _typeRegistry.GetName(hash);
            if (s == null && _extraRegistry != null)
                s = _extraRegistry.GetName(hash);
            return s ?? HexToStringUC(hash);
        }

        public int GetFileHash(string name)
        {
            if (name == null) return -1;

            if (name.StartsWith("#", StringComparison.Ordinal))
                return (int)uint.Parse(name.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return (int)uint.Parse(name.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            if (!name.EndsWith("~", StringComparison.Ordinal))
            {
                var hash = FnvHash(name);
                if (_updateProjectRegistry) _projectRegistry.Add(name, hash);
                return hash;
            }
            else
            {
                var lc = name.ToLowerInvariant();
                int? i = _fileRegistry.GetHash(lc);
                i ??= _projectRegistry.GetHash(lc);
                if (i == null)
                    throw new ArgumentException($"Unable to find {name} hash. It does not exist in the reg_file registry.");
                if (_updateProjectRegistry) _projectRegistry.Add(name, i.Value);
                return i.Value;
            }
        }

        public int GetTypeHash(string name)
        {
            if (name == null) return -1;

            if (name.StartsWith("#", StringComparison.Ordinal))
                return (int)uint.Parse(name.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            if (name.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return (int)uint.Parse(name.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            var i = _typeRegistry.GetHash(name);
            if (i == null && _extraRegistry != null)
            {
                _extraRegistry.Add(name, FnvHash(name));
            }
            return i ?? FnvHash(name);
        }
    }
}
