using UnityEngine;

namespace TriRasterizationVoxelization
{
    public class HeightFieldSpan
    {
        public HeightFieldSpan()
        {
        }

        public uint _smin;
        public uint _smax;
        public HeightFieldSpan _pNext = null;
    }
}
