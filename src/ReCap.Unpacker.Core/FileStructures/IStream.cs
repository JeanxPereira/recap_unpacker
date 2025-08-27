using System;
using System.IO;
using System.Threading.Tasks;

namespace ReCap.Unpacker.Core.FileStructures;

public interface IStream : IDisposable
{
    void Seek(long off);
    void SeekAbs(long off);
    void Skip(int n);
    long Length();
    void SetLength(long n);
    long GetFilePointer();
    long GetFilePointerAbs();
    void SetBaseOffset(long val);
    long GetBaseOffset();
}
