using System;

namespace ReCap.Unpacker.Core
{

    public sealed class ResourceKey
    {
        private int _typeID;
        private int _groupID;
        private int _instanceID;

        public int  GetTypeID()      => _typeID;
        public void SetTypeID(int v) => _typeID = v;

        public int  GetGroupID()      => _groupID;
        public void SetGroupID(int v) => _groupID = v;

        public int  GetInstanceID()      => _instanceID;
        public void SetInstanceID(int v) => _instanceID = v;

        public bool IsEquivalent(ResourceKey other) =>
            other is not null && _typeID == other._typeID && _instanceID == other._instanceID;

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            if (obj is not ResourceKey k) return false;
            return _typeID == k._typeID && _groupID == k._groupID && _instanceID == k._instanceID;
        }

        public override int GetHashCode() => HashCode.Combine(_typeID, _groupID, _instanceID);

        public override string ToString() => $"[{_typeID:X8}:{_groupID:X8}:{_instanceID:X8}]";
    }
}
