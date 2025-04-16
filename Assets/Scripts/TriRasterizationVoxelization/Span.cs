using UnityEngine;

namespace TriRasterizationVoxelization
{
    public unsafe struct Span
    {
        public uint _smin;
        public uint _smax;
        public Span* _pNext;
    }
}
