using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace ReCap.Unpacker.Core.FileStructures;

public sealed class FileReadWriteStream : IReadWriteStream
{
    private readonly FileStream _fs;
    private long _baseOffset;

    public FileReadWriteStream(string path, FileMode mode, FileAccess access, FileShare share = FileShare.Read)
    {
        _fs = new FileStream(path, mode, access, share);
    }

    public void Dispose() => _fs.Dispose();

    // --- base offset semantics ---
    public void Seek(long off)     => _fs.Seek(off + _baseOffset, SeekOrigin.Begin);
    public void SeekAbs(long off)  => _fs.Seek(off, SeekOrigin.Begin);
    public void Skip(int n)        => _fs.Seek(n, SeekOrigin.Current);

    public long GetFilePointer()    => _fs.Position - _baseOffset;
    public long GetFilePointerAbs() => _fs.Position;

    public long Length()            => _fs.Length;
    public void SetLength(long n)   => _fs.SetLength(n);

    public void SetBaseOffset(long val) => _baseOffset = val;
    public long GetBaseOffset()         => _baseOffset;

    // --- bulk read/write ---
    private void ReadExactly(byte[] dst)
    {
        int read = 0;
        while (read < dst.Length)
        {
            int r = _fs.Read(dst, read, dst.Length - read);
            if (r <= 0) throw new EndOfStreamException();
            read += r;
        }
    }
    public void Read(byte[] dst) => ReadExactly(dst);

    public void Write(byte[] arr)                 => _fs.Write(arr, 0, arr.Length);
    public void Write(byte[] arr, int off, int n) => _fs.Write(arr, off, n);

    public byte[] ToByteArray()
    {
        long pos = _fs.Position;
        _fs.Seek(0, SeekOrigin.Begin);
        var buf = new byte[_fs.Length];
        ReadExactly(buf);
        _fs.Seek(pos, SeekOrigin.Begin);
        return buf;
    }

    public string ReadLine()
    {
        using var ms = new MemoryStream();
        int b;
        bool seenAny = false;
        while ((b = _fs.ReadByte()) != -1)
        {
            if (b == (byte)'\n' || b == (byte)'\r') break;
            ms.WriteByte((byte)b);
            seenAny = true;
        }
        return seenAny ? Encoding.ASCII.GetString(ms.ToArray()) : string.Empty;
    }

    // --- primitives (BE default) ---
    private byte ReadByteRaw()
    {
        int v = _fs.ReadByte();
        if (v < 0) throw new EndOfStreamException();
        return (byte)v;
    }

    public bool  ReadBoolean() { var v = ReadByteRaw(); if (v == 0) return false; if (v == 1) return true; throw new IOException($"Boolean byte at {GetFilePointerAbs()} is {v}. Must be 0 or 1."); }
    public void  ReadBooleans(bool[] dst) { for (int i=0;i<dst.Length;i++) dst[i] = ReadBoolean(); }

    public byte  ReadByte() => ReadByteRaw();
    public short ReadUByte() => (short)(ReadByteRaw() & 0xFF);

    public void  ReadBytes(byte[] dst) => Read(dst);
    public void  ReadUBytes(int[] dst) { for (int i=0;i<dst.Length;i++) dst[i] = ReadUByte(); }

    public char  ReadChar() { var b = new byte[2]; ReadExactly(b); return (char)((b[0] << 8) | b[1]); }

    public void ReadChars(char[] dst)
    {
        for (int i = 0; i < dst.Length; i++) dst[i] = ReadChar();
    }

    // 16-bit
    public short ReadShort()    { var b = new byte[2]; ReadExactly(b); return (short)((b[0] << 8) | b[1]); }
    public short ReadLEShort()  { var b = new byte[2]; ReadExactly(b); return (short)BinaryPrimitives.ReadInt16LittleEndian(b); }
    public int   ReadUShort()   { var b = new byte[2]; ReadExactly(b); return ((b[0] << 8) | b[1]) & 0xFFFF; }
    public int   ReadLEUShort() { var b = new byte[2]; ReadExactly(b); return BinaryPrimitives.ReadUInt16LittleEndian(b); }

    public void ReadShorts(short[] dst)    { for (int i=0;i<dst.Length;i++) dst[i]=ReadShort(); }
    public void ReadLEShorts(short[] dst)  { for (int i=0;i<dst.Length;i++) dst[i]=ReadLEShort(); }
    public void ReadUShorts(int[] dst)     { for (int i=0;i<dst.Length;i++) dst[i]=ReadUShort(); }
    public void ReadLEUShorts(int[] dst)   { for (int i=0;i<dst.Length;i++) dst[i]=ReadLEUShort(); }

    // 32-bit
    public int  ReadInt()    { var b = new byte[4]; ReadExactly(b); return (b[0]<<24)|(b[1]<<16)|(b[2]<<8)|b[3]; }
    public int  ReadLEInt()  { var b = new byte[4]; ReadExactly(b); return BinaryPrimitives.ReadInt32LittleEndian(b); }
    public long ReadUInt()   { var b = new byte[4]; ReadExactly(b); return (uint)((b[0]<<24)|(b[1]<<16)|(b[2]<<8)|b[3]); }
    public long ReadLEUInt() { var b = new byte[4]; ReadExactly(b); return BinaryPrimitives.ReadUInt32LittleEndian(b); }

    public void ReadInts(int[] dst)     { for (int i=0;i<dst.Length;i++) dst[i]=ReadInt(); }
    public void ReadLEInts(int[] dst)   { for (int i=0;i<dst.Length;i++) dst[i]=ReadLEInt(); }
    public void ReadUInts(long[] dst)   { for (int i=0;i<dst.Length;i++) dst[i]=ReadUInt(); }
    public void ReadLEUInts(long[] dst) { for (int i=0;i<dst.Length;i++) dst[i]=ReadLEUInt(); }

    // 64-bit
    public long ReadLong()
    {
        var b = new byte[8]; ReadExactly(b);
        return ((long)b[0]<<56)|((long)b[1]<<48)|((long)b[2]<<40)|((long)b[3]<<32)|((long)b[4]<<24)|((long)b[5]<<16)|((long)b[6]<<8)|b[7];
    }
    public long ReadLELong()  { var b = new byte[8]; ReadExactly(b); return BinaryPrimitives.ReadInt64LittleEndian(b); }

    public void ReadLongs(long[] dst)    { for (int i=0;i<dst.Length;i++) dst[i]=ReadLong(); }
    public void ReadLELongs(long[] dst)  { for (int i=0;i<dst.Length;i++) dst[i]=ReadLELong(); }

    // floats
    public float  ReadFloat()    => BitConverter.Int32BitsToSingle(ReadInt());
    public float  ReadLEFloat()  => BitConverter.Int32BitsToSingle(ReadLEInt());

    public void   ReadFloats(float[] dst)   { for (int i=0;i<dst.Length;i++) dst[i]=ReadFloat(); }
    public void   ReadLEFloats(float[] dst) { for (int i=0;i<dst.Length;i++) dst[i]=ReadLEFloat(); }

    // doubles
    public double ReadDouble()    => BitConverter.Int64BitsToDouble(ReadLong());
    public double ReadLEDouble()  => BitConverter.Int64BitsToDouble(ReadLELong());

    public void   ReadDoubles(double[] dst)   { for (int i=0;i<dst.Length;i++) dst[i]=ReadDouble(); }
    public void   ReadLEDoubles(double[] dst) { for (int i=0;i<dst.Length;i++) dst[i]=ReadLEDouble(); }

    // --- writers ---
    public void WritePadding(int pad)
    {
        var zeros = new byte[pad];
        _fs.Write(zeros, 0, zeros.Length);
    }

    public void WriteBoolean(bool v) => WriteByte(v ? 1 : 0);
    public void WriteBooleans(params bool[] vals) { foreach (var v in vals) WriteBoolean(v); }

    public void WriteByte(int v) => _fs.WriteByte((byte)v);
    public void WriteBytes(params int[] vals) { foreach (var v in vals) WriteByte(v); }
    public void WriteUByte(int v) => _fs.WriteByte((byte)(v & 0xFF));
    public void WriteUBytes(params int[] vals) { foreach (var v in vals) WriteUByte(v); }

    public void WriteShort(int v)    { Span<byte> b= stackalloc byte[2]; b[0]=(byte)((v>>8)&0xFF); b[1]=(byte)(v&0xFF); _fs.Write(b); }
    public void WriteLEShort(int v)  { Span<byte> b= stackalloc byte[2]; BinaryPrimitives.WriteInt16LittleEndian(b,(short)v); _fs.Write(b); }
    public void WriteShorts(params int[] vals)    { foreach (var v in vals) WriteShort(v); }
    public void WriteLEShorts(params int[] vals)  { foreach (var v in vals) WriteLEShort(v); }
    public void WriteUShort(int v)   => WriteShort(v & 0xFFFF);
    public void WriteUShorts(params int[] vals) { foreach (var v in vals) WriteUShort(v); }
    public void WriteLEUShort(int v) => WriteLEShort(v & 0xFFFF);
    public void WriteLEUShorts(params int[] vals) { foreach (var v in vals) WriteLEUShort(v); }

    public void WriteInt(int v)    { Span<byte> b= stackalloc byte[4]; b[0]=(byte)((v>>24)&0xFF); b[1]=(byte)((v>>16)&0xFF); b[2]=(byte)((v>>8)&0xFF); b[3]=(byte)(v&0xFF); _fs.Write(b); }
    public void WriteLEInt(int v)  { Span<byte> b= stackalloc byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, v); _fs.Write(b); }
    public void WriteInts(params int[] vals)    { foreach (var v in vals) WriteInt(v); }
    public void WriteLEInts(params int[] vals)  { foreach (var v in vals) WriteLEInt(v); }
    public void WriteUInt(long v)  => WriteInt((int)(v & 0xFFFFFFFF));
    public void WriteUInts(params long[] vals) { foreach (var v in vals) WriteUInt(v); }
    public void WriteLEUInt(long v){ Span<byte> b= stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(b, (uint)(v & 0xFFFFFFFF)); _fs.Write(b); }
    public void WriteLEUInts(params long[] vals) { foreach (var v in vals) WriteLEUInt(v); }

    public void WriteLong(long v)
    {
        Span<byte> b= stackalloc byte[8];
        b[0]=(byte)((v>>56)&0xFF); b[1]=(byte)((v>>48)&0xFF); b[2]=(byte)((v>>40)&0xFF); b[3]=(byte)((v>>32)&0xFF);
        b[4]=(byte)((v>>24)&0xFF); b[5]=(byte)((v>>16)&0xFF); b[6]=(byte)((v>>8)&0xFF); b[7]=(byte)(v&0xFF);
        _fs.Write(b);
    }
    public void WriteLELong(long v) { Span<byte> b= stackalloc byte[8]; BinaryPrimitives.WriteInt64LittleEndian(b, v); _fs.Write(b); }
    public void WriteLongs(params long[] vals)   { foreach (var v in vals) WriteLong(v); }
    public void WriteLELongs(params long[] vals) { foreach (var v in vals) WriteLELong(v); }

    public void WriteFloat(float v)    => WriteInt(BitConverter.SingleToInt32Bits(v));
    public void WriteFloats(params float[] vals)  { foreach (var v in vals) WriteFloat(v); }
    public void WriteLEFloat(float v)  => WriteLEInt(BitConverter.SingleToInt32Bits(v));
    public void WriteLEFloats(params float[] vals){ foreach (var v in vals) WriteLEFloat(v); }

    public void WriteDouble(double v)    => WriteLong(BitConverter.DoubleToInt64Bits(v));
    public void WriteDoubles(params double[] vals)  { foreach (var v in vals) WriteDouble(v); }
    public void WriteLEDouble(double v)  => WriteLELong(BitConverter.DoubleToInt64Bits(v));
    public void WriteLEDoubles(params double[] vals){ foreach (var v in vals) WriteLEDouble(v); }

    public string ReadCString(StringEncoding enc)
    {
        int charSize = enc == StringEncoding.ASCII ? 1 : 2;
        using var ms = new MemoryStream();
        if (charSize == 1)
        {
            int b;
            while ((b = _fs.ReadByte()) != -1)
            {
                if (b == 0) break;
                ms.WriteByte((byte)b);
            }
        }
        else
        {
            while (true)
            {
                int b1 = _fs.ReadByte();
                int b2 = _fs.ReadByte();
                if (b1 == -1 || b2 == -1) break;
                if (b1 == 0 && b2 == 0) break;
                ms.WriteByte((byte)b1);
                ms.WriteByte((byte)b2);
            }
        }
        return enc.GetEncoding().GetString(ms.ToArray());
    }

    public string ReadString(StringEncoding enc, int length)
    {
        int charSize = enc == StringEncoding.ASCII ? 1 : 2;
        var buf = new byte[length * charSize];
        ReadExactly(buf);
        return enc.GetEncoding().GetString(buf);
    }

    public void WriteCString(string? text, StringEncoding enc)
    {
        if (!string.IsNullOrEmpty(text))
        {
            var bytes = enc.GetEncoding().GetBytes(text);
            _fs.Write(bytes, 0, bytes.Length);
        }
        _fs.WriteByte(0);
    }

    public void WriteString(string? text, StringEncoding enc)
    {
        if (string.IsNullOrEmpty(text)) return;
        var bytes = enc.GetEncoding().GetBytes(text);
        _fs.Write(bytes, 0, bytes.Length);
    }

    public void WriteString(string? text, StringEncoding enc, int length)
    {
        int charSize = enc == StringEncoding.ASCII ? 1 : 2;
        int total = length * charSize;
        var buf = new byte[total];
        if (!string.IsNullOrEmpty(text))
        {
            var src = enc.GetEncoding().GetBytes(text);
            Array.Copy(src, 0, buf, 0, Math.Min(src.Length, total));
        }
        _fs.Write(buf, 0, buf.Length);
    }
}
