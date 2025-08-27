using System.Collections.Generic;

namespace ReCap.Unpacker.Core
{
    public abstract class AbstractManager
    {
        public virtual void Initialize(IDictionary<string, string> properties) { }
        public virtual void Dispose() { }
        public virtual void SaveSettings(IDictionary<string, string> properties) { }
    }
}
