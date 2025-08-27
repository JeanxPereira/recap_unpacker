using System;
using System.Text;

namespace ReCap.Unpacker.Core.FileStructures
{
    public sealed class MemoryReadWriteStream : IReadWriteStream
    {
        private byte[] _data;
        private int _pos;
        private int _base;
        private int _length;
        private const int INITIAL_SIZE = 8192;
        private const float RESIZE_FACTOR = 1.5f;

        public MemoryReadWriteStream() { _data = new byte[INITIAL_SIZE]; _pos = 0; _base = 0; _length = 0; }
        public MemoryReadWriteStream(int capacity) { _data = new byte[capacity]; _pos = 0; _base = 0; _length = 0; }
        public MemoryReadWriteStream(byte[] data) { _data = data ?? Array.Empty<byte>(); _pos = 0; _base = 0; _length = _data.Length; }

        public void Dispose() { _data = Array.Empty<byte>(); _pos = 0; _length = 0; _base = 0; }

        private void Ensure(int count)
        {
            if (_pos + count <= _data.Length) return;
            int newSize = _data.Length == 0 ? Math.Max(INITIAL_SIZE, count) : _data.Length;
            while (_pos + count > newSize) newSize = (int)(newSize * RESIZE_FACTOR);
            var arr = new byte[newSize];
            Buffer.BlockCopy(_data, 0, arr, 0, _length);
            _data = arr;
        }

        public void Seek(long off)    => _pos = checked((int)(off + _base));
        public void SeekAbs(long off) => _pos = checked((int)off);
        public void Skip(int n)       => _pos += n;

        public long Length()          => _length;
        public void SetLength(long n)
        {
            if (n > _data.Length) Ensure((int)(n - _pos));
            _length = checked((int)n);
            if (_pos > _length) _pos = _length;
        }

        public long GetFilePointer()    => _pos - _base;
        public long GetFilePointerAbs() => _pos;

        public void SetBaseOffset(long v) => _base = checked((int)v);
        public long GetBaseOffset()       => _base;

        public byte[] ToByteArray()
        {
            var arr = new byte[_length];
            Buffer.BlockCopy(_data, 0, arr, 0, _length);
            return arr;
        }

        // --- Reads ---
        public void Read(byte[] dst) { Buffer.BlockCopy(_data, _pos, dst, 0, dst.Length); _pos += dst.Length; }

        public string ReadCString(StringEncoding e)
        {
            int charSize = e == StringEncoding.ASCII ? 1 : 2;
            int start = _pos;
            if (charSize == 1)
            {
                while (_pos < _length && _data[_pos] != 0) _pos++;
                int len = _pos - start;
                if (_pos < _length) _pos++; // consume 0
                return e.GetEncoding().GetString(_data, start, len);
            }
            else
            {
                // UTF-16: look for 0x00 0x00
                while (_pos + 1 < _length && (_data[_pos] != 0 || _data[_pos + 1] != 0)) _pos += 2;
                int len = _pos - start;
                if (_pos + 1 < _length) _pos += 2; // consume 00 00
                return e.GetEncoding().GetString(_data, start, len);
            }
        }

        private static int GetCStringLength(byte[] array, int start, int characterSize)
        {
            int end = array.Length;
            for (int i = start; i <= array.Length - characterSize; i += characterSize)
            {
                if (characterSize == 1)
                {
                    if (array[i] == 0) { end = i; break; }
                }
                else
                {
                    if (array[i] == 0 && array[i + 1] == 0) { end = i; break; }
                }
            }
            return end - start;
        }

        public string ReadString(StringEncoding e, int length)
        {
            int charSize = e == StringEncoding.ASCII ? 1 : 2;
            int bytes = length * charSize;

            var slice = new byte[bytes];
            Buffer.BlockCopy(_data, _pos, slice, 0, bytes);
            _pos += bytes;

            // Trim trailing zero bytes
            int realLen = GetCStringLength(slice, 0, charSize);
            if (realLen != slice.Length)
            {
                var tmp = new byte[realLen];
                Buffer.BlockCopy(slice, 0, tmp, 0, realLen);
                slice = tmp;
            }

            return e.GetEncoding().GetString(slice);
        }

        public string ReadLine()
        {
            int start = _pos;
            while (_pos < _length && _data[_pos] != (byte)'\n' && _data[_pos] != (byte)'\r') _pos++;
            int len = _pos - start;
            return Encoding.ASCII.GetString(_data, start, len);
        }

        public bool  ReadBoolean()
        {
            byte v = _data[_pos++];
            if (v == 0) return false;
            if (v == 1) return true;
            throw new Exception($"Boolean byte at {_pos - 1} is {v}. Must be 0 or 1.");
        }
        public void  ReadBooleans(bool[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadBoolean(); }

        public byte  ReadByte() => _data[_pos++];
        public short ReadUByte() => (short)(_data[_pos++] & 0xFF);
        public void  ReadBytes(byte[] dst){ Read(dst); }
        public void  ReadUBytes(int[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadUByte(); }

        public char  ReadChar(){ int v = (_data[_pos++]<<8)|_data[_pos++]; return (char)(v & 0xFFFF); }
        public void  ReadChars(char[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadChar(); }

        public short ReadShort(){ int v = (_data[_pos]<<8)|_data[_pos+1]; _pos+=2; return (short)v; }
        public short ReadLEShort(){ int v = (_data[_pos+1]<<8)|_data[_pos]; _pos+=2; return (short)v; }
        public int ReadUShort(){
            int v = ((_data[_pos]   & 0xFF) << 8) | (_data[_pos+1] & 0xFF);
            _pos += 2; return v;
        }
        public int ReadLEUShort(){
            int v = ((_data[_pos+1] & 0xFF) << 8) | (_data[_pos]   & 0xFF);
            _pos += 2; return v;
        }
        public void  ReadShorts(short[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadShort(); }
        public void  ReadLEShorts(short[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadLEShort(); }
        public void  ReadUShorts(int[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadUShort(); }
        public void  ReadLEUShorts(int[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadLEUShort(); }

        public int ReadInt()
        {
            int v = ((_data[_pos]   & 0xFF) << 24)
                | ((_data[_pos+1] & 0xFF) << 16)
                | ((_data[_pos+2] & 0xFF) << 8)
                |  (_data[_pos+3] & 0xFF);
            _pos += 4;
            return v;
        }

        public int ReadLEInt()
        {
            int v =  (_data[_pos]   & 0xFF)
                | ((_data[_pos+1] & 0xFF) << 8)
                | ((_data[_pos+2] & 0xFF) << 16)
                | ((_data[_pos+3] & 0xFF) << 24);
            _pos += 4;
            return v;
        }
        public long  ReadUInt(){ return (uint)ReadInt(); }
        public long  ReadLEUInt(){ return (uint)ReadLEInt(); }
        public void  ReadInts(int[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadInt(); }
        public void  ReadLEInts(int[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadLEInt(); }
        public void  ReadUInts(long[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadUInt(); }
        public void  ReadLEUInts(long[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadLEUInt(); }
        public long ReadLong()
        {
            ulong r = 0;
            for (int i = 0; i < 8; i++)
            {
                r = (r << 8) | _data[_pos++];
            }
            return unchecked((long)r);
        }

        public long ReadLELong()
        {
            ulong r = 0;
            for (int i = 7; i >= 0; i--)
            {
                r = (r << 8) | _data[_pos + i];
            }
            _pos += 8;
            return unchecked((long)r);
        }
        public void  ReadLongs(long[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadLong(); }
        public void  ReadLELongs(long[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadLELong(); }

        public float  ReadFloat(){ return BitConverter.Int32BitsToSingle(ReadInt()); }
        public float  ReadLEFloat(){ return BitConverter.Int32BitsToSingle(ReadLEInt()); }
        public void   ReadFloats(float[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadFloat(); }
        public void   ReadLEFloats(float[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadLEFloat(); }

        public double ReadDouble(){ return BitConverter.Int64BitsToDouble(ReadLong()); }
        public double ReadLEDouble(){ return BitConverter.Int64BitsToDouble(ReadLELong()); }
        public void   ReadDoubles(double[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadDouble(); }
        public void   ReadLEDoubles(double[] dst){ for(int i=0;i<dst.Length;i++) dst[i]=ReadLEDouble(); }

        // --- Writes (dynamic growth) ---
        public void WritePadding(int pad)
        {
            Ensure(pad);
            Array.Clear(_data, _pos, pad);
            _pos += pad;
            if (_pos > _length) _length = _pos;
        }

        public void Write(byte[] arr) { Write(arr, 0, arr.Length); }
        public void Write(byte[] arr, int off, int len)
        {
            Ensure(len);
            Buffer.BlockCopy(arr, off, _data, _pos, len);
            _pos += len;
            if (_pos > _length) _length = _pos;
        }

        public void WriteBoolean(bool v) => WriteByte(v ? 1 : 0);
        public void WriteBooleans(params bool[] vals){ foreach (var v in vals) WriteBoolean(v); }

        public void WriteByte(int v)
        {
            Ensure(1);
            _data[_pos++] = (byte)v;
            if (_pos > _length) _length = _pos;
        }
        public void WriteBytes(params int[] vals){ foreach (var v in vals) WriteByte(v); }

        public void WriteUByte(int v) => WriteByte(v & 0xFF);
        public void WriteUBytes(params int[] vals){ foreach (var v in vals) WriteUByte(v); }

        public void WriteShort(int v)
        {
            Ensure(2);
            _data[_pos++] = (byte)((v >> 8) & 0xFF);
            _data[_pos++] = (byte)(v & 0xFF);
            if (_pos > _length) _length = _pos;
        }
        public void WriteLEShort(int v)
        {
            Ensure(2);
            _data[_pos++] = (byte)(v & 0xFF);
            _data[_pos++] = (byte)((v >> 8) & 0xFF);
            if (_pos > _length) _length = _pos;
        }
        public void WriteShorts(params int[] vals){ foreach (var v in vals) WriteShort(v); }
        public void WriteLEShorts(params int[] vals){ foreach (var v in vals) WriteLEShort(v); }

        public void WriteUShort(int v)   => WriteShort(v & 0xFFFF);
        public void WriteUShorts(params int[] vals){ foreach (var v in vals) WriteUShort(v); }
        public void WriteLEUShort(int v) => WriteLEShort(v & 0xFFFF);
        public void WriteLEUShorts(params int[] vals){ foreach (var v in vals) WriteLEUShort(v); }

        public void WriteInt(int v)
        {
            Ensure(4);
            _data[_pos++] = (byte)((v >> 24) & 0xFF);
            _data[_pos++] = (byte)((v >> 16) & 0xFF);
            _data[_pos++] = (byte)((v >> 8) & 0xFF);
            _data[_pos++] = (byte)(v & 0xFF);
            if (_pos > _length) _length = _pos;
        }
        public void WriteLEInt(int v)
        {
            Ensure(4);
            _data[_pos++] = (byte)(v & 0xFF);
            _data[_pos++] = (byte)((v >> 8) & 0xFF);
            _data[_pos++] = (byte)((v >> 16) & 0xFF);
            _data[_pos++] = (byte)((v >> 24) & 0xFF);
            if (_pos > _length) _length = _pos;
        }
        public void WriteInts(params int[] vals){ foreach (var v in vals) WriteInt(v); }
        public void WriteLEInts(params int[] vals){ foreach (var v in vals) WriteLEInt(v); }

        public void WriteUInt(long v)  => WriteInt((int)(v & 0xFFFFFFFF));
        public void WriteUInts(params long[] vals){ foreach (var v in vals) WriteUInt(v); }
        public void WriteLEUInt(long v)=> WriteLEInt((int)(v & 0xFFFFFFFF));
        public void WriteLEUInts(params long[] vals){ foreach (var v in vals) WriteLEUInt(v); }

        public void WriteLong(long v)
        {
            Ensure(8);
            _data[_pos++] = (byte)((v >> 56) & 0xFF);
            _data[_pos++] = (byte)((v >> 48) & 0xFF);
            _data[_pos++] = (byte)((v >> 40) & 0xFF);
            _data[_pos++] = (byte)((v >> 32) & 0xFF);
            _data[_pos++] = (byte)((v >> 24) & 0xFF);
            _data[_pos++] = (byte)((v >> 16) & 0xFF);
            _data[_pos++] = (byte)((v >> 8) & 0xFF);
            _data[_pos++] = (byte)(v & 0xFF);
            if (_pos > _length) _length = _pos;
        }
        public void WriteLELong(long v)
        {
            Ensure(8);
            _data[_pos++] = (byte)(v & 0xFF);
            _data[_pos++] = (byte)((v >> 8) & 0xFF);
            _data[_pos++] = (byte)((v >> 16) & 0xFF);
            _data[_pos++] = (byte)((v >> 24) & 0xFF);
            _data[_pos++] = (byte)((v >> 32) & 0xFF);
            _data[_pos++] = (byte)((v >> 40) & 0xFF);
            _data[_pos++] = (byte)((v >> 48) & 0xFF);
            _data[_pos++] = (byte)((v >> 56) & 0xFF);
            if (_pos > _length) _length = _pos;
        }
        public void WriteLongs(params long[] vals){ foreach (var v in vals) WriteLong(v); }
        public void WriteLELongs(params long[] vals){ foreach (var v in vals) WriteLELong(v); }

        public void WriteFloat(float v)    => WriteInt(BitConverter.SingleToInt32Bits(v));
        public void WriteFloats(params float[] vals){ foreach (var v in vals) WriteFloat(v); }
        public void WriteLEFloat(float v)  => WriteLEInt(BitConverter.SingleToInt32Bits(v));
        public void WriteLEFloats(params float[] vals){ foreach (var v in vals) WriteLEFloat(v); }

        public void WriteDouble(double v)    => WriteLong(BitConverter.DoubleToInt64Bits(v));
        public void WriteDoubles(params double[] vals){ foreach (var v in vals) WriteDouble(v); }
        public void WriteLEDouble(double v)  => WriteLELong(BitConverter.DoubleToInt64Bits(v));
        public void WriteLEDoubles(params double[] vals){ foreach (var v in vals) WriteLEDouble(v); }

        public void WriteCString(string? text, StringEncoding e)
        {
            if (!string.IsNullOrEmpty(text)) WriteString(text, e);
            if (e != StringEncoding.ASCII) WriteShort(0);
            else WriteByte(0);
        }

        public void WriteString(string? text, StringEncoding e)
        {
            if (string.IsNullOrEmpty(text)) return;
            var bytes = e.GetEncoding().GetBytes(text);
            Write(bytes, 0, bytes.Length);
        }

        public void WriteString(string? text, StringEncoding e, int length)
        {
            int charSize = e == StringEncoding.ASCII ? 1 : 2;
            int total = length * charSize;
            byte[] array;
            if (text == null)
            {
                array = new byte[total];
            }
            else
            {
                var src = e.GetEncoding().GetBytes(text);
                if (src.Length != total)
                {
                    array = new byte[total];
                    Buffer.BlockCopy(src, 0, array, 0, Math.Min(src.Length, total));
                }
                else array = src;
            }
            Write(array, 0, array.Length);
        }
    }
}
