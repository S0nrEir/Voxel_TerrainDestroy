using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TriRasterizationVoxelization
{
    public struct SpanJobData : IComparable<SpanJobData>
    {
        public ushort _max;
        public ushort _min;

        public int CompareTo(SpanJobData other)
        {
            int result = _min.CompareTo(other._min);
            if (result == 0)
                result = _max.CompareTo(other._max);
            
            return result;
        }
    }
}
