using System;

namespace ReCap.Unpacker.Core.FileStructures;

public interface IStreamReader : IStream
{
    byte[] ToByteArray();

    void Read(byte[] dst);

    string ReadCString(StringEncoding encoding);
    string ReadString(StringEncoding encoding, int length);

    string ReadLine();

    bool  ReadBoolean();
    void  ReadBooleans(bool[] dst);

    byte  ReadByte();
    short ReadUByte();

    void  ReadBytes(byte[] dst);
    void  ReadUBytes(int[] dst);

    char  ReadChar();

    void  ReadChars(char[] dst);

    short ReadShort();
    short ReadLEShort();
    int   ReadUShort();
    int   ReadLEUShort();

    void  ReadShorts(short[] dst);
    void  ReadLEShorts(short[] dst);
    void  ReadUShorts(int[] dst);
    void  ReadLEUShorts(int[] dst);

    int   ReadInt();
    int   ReadLEInt();
    long  ReadUInt();
    long  ReadLEUInt();

    void  ReadInts(int[] dst);
    void  ReadLEInts(int[] dst);
    void  ReadUInts(long[] dst);
    void  ReadLEUInts(long[] dst);

    long  ReadLong();
    long  ReadLELong();

    void  ReadLongs(long[] dst);
    void  ReadLELongs(long[] dst);

    float ReadFloat();
    float ReadLEFloat();

    void  ReadFloats(float[] dst);
    void  ReadLEFloats(float[] dst);

    double ReadDouble();
    double ReadLEDouble();

    void   ReadDoubles(double[] dst);
    void   ReadLEDoubles(double[] dst);
}
