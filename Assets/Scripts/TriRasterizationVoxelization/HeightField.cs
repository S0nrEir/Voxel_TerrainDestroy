using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TriRasterizationVoxelization
{
    /// <summary>
    /// 高度场
    /// </summary>
    public class HeightField
    {
        public HeightField(Vector3 min,Vector3 max)
        {
            
        }
        
        public float SetHorizontalCellSize(float size) => HorizontalCellSize = size;
        public float SetVerticalCellSize(float size) => VerticalCellSize = size;

        /// <summary>
        /// 水平方向格子尺寸
        /// </summary>
        public float HorizontalCellSize
        {
            get;
            private set;
        }
        
        /// <summary>
        /// 垂直方向格子尺寸
        /// </summary>
        public float VerticalCellSize
        {
            get;
            private set;
        }

        /// <summary>
        /// 高度场最大位置
        /// </summary>
        public Vector3 Max
        {
            get;
            private set;
        }

        /// <summary>
        /// 高度场最小位置
        /// </summary>
        public Vector3 Min
        {
            get;
            private set;
        }
    }
}
