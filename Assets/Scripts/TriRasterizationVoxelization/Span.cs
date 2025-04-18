using UnityEngine;

namespace TriRasterizationVoxelization
{
    public unsafe struct HeightFieldSpan
    {
        public uint _smin;
        public uint _smax;
        public HeightFieldSpan* _pNext;
    }
}
