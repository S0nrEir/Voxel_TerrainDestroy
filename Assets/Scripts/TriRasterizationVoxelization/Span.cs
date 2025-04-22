using Editor;
using UnityEditor;

namespace TriRasterizationVoxelization
{
    public class HeightFieldSpan : IClear
    {
        public HeightFieldSpan()
        {
        }

        public uint _smin = 0;
        public uint _smax = 0;
        public HeightFieldSpan _pNext = null;

        public void Clear()
        {
            _smin = 0;
            _smax = 0;
            _pNext = null;
        }
    }
}
