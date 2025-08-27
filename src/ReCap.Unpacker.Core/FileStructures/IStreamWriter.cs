namespace ReCap.Unpacker.Core.FileStructures;

public interface IStreamWriter : IStream
{
    void WritePadding(int pad);

    void Write(byte[] arr);
    void Write(byte[] arr, int off, int len);

    void WriteCString(string? text, StringEncoding encoding);
    void WriteString(string? text, StringEncoding encoding);
    void WriteString(string? text, StringEncoding encoding, int length);

    void WriteBoolean(bool val);
    void WriteBooleans(params bool[] vals);

    void WriteByte(int val);
    void WriteBytes(params int[] vals);
    void WriteUByte(int val);
    void WriteUBytes(params int[] vals);

    void WriteShort(int val);
    void WriteShorts(params int[] vals);
    void WriteLEShort(int val);
    void WriteLEShorts(params int[] vals);
    void WriteUShort(int val);
    void WriteUShorts(params int[] vals);
    void WriteLEUShort(int val);
    void WriteLEUShorts(params int[] vals);

    void WriteInt(int val);
    void WriteInts(params int[] vals);
    void WriteLEInt(int val);
    void WriteLEInts(params int[] vals);
    void WriteUInt(long val);
    void WriteUInts(params long[] vals);
    void WriteLEUInt(long val);
    void WriteLEUInts(params long[] vals);

    void WriteLong(long val);
    void WriteLongs(params long[] vals);
    void WriteLELong(long val);
    void WriteLELongs(params long[] vals);

    void WriteFloat(float val);
    void WriteFloats(params float[] vals);
    void WriteLEFloat(float val);
    void WriteLEFloats(params float[] vals);

    void WriteDouble(double val);
    void WriteDoubles(params double[] vals);
    void WriteLEDouble(double val);
    void WriteLEDoubles(params double[] vals);
}
