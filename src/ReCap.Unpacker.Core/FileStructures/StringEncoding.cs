namespace ReCap.Unpacker.Core.FileStructures;

public enum StringEncoding
{
    ASCII,
    UTF16LE,
    UTF16BE
}

internal static class StringEncodingExtensions
{
    public static System.Text.Encoding GetEncoding(this StringEncoding e) =>
        e switch
        {
            StringEncoding.ASCII   => System.Text.Encoding.ASCII,
            StringEncoding.UTF16LE => System.Text.Encoding.Unicode,          // LE
            StringEncoding.UTF16BE => System.Text.Encoding.BigEndianUnicode, // BE
            _ => System.Text.Encoding.ASCII
        };
}
