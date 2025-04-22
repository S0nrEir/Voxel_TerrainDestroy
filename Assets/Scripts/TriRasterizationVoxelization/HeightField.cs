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
        /// <param name="min">高度场的最小位置</param>
        /// <param name="max">高度场的最大位置</param>
        /// <param name="horizontalCellSize">水平方向单个体素大小</param>
        /// <param name="verticalCellSize">垂直方向单个体素大小</param>
        public HeightField(Vector3 min,Vector3 max,float horizontalCellSize,float verticalCellSize)
        {
            HorizontalCellSize = horizontalCellSize;
            VerticalCellSize   = verticalCellSize;
            Min                = min;
    
            var widthCells     = Mathf.CeilToInt(max.x - min.x / horizontalCellSize);
            var heightCells    = Mathf.CeilToInt(max.z - min.z / verticalCellSize);
    
            var adjustedWidth  = widthCells * horizontalCellSize;
            var adjustedHeight = heightCells * verticalCellSize;
    
            Max    = new Vector3( min.x + adjustedWidth, max.y , min.z + adjustedHeight );
            Width  = widthCells;
            Height = heightCells;
            Span   = new HeightFieldSpan[Width,Height];
        }

        public float SetHorizontalCellSize(float size) => HorizontalCellSize = size;
        public float SetVerticalCellSize(float size) => VerticalCellSize = size;

        /// <summary>
        /// 高度场高度，垂直方向格子数
        /// </summary>
        public int Height
        {
            get;
            private set;
        }

        /// <summary>
        /// 高度场宽度，水平方向格子数
        /// </summary>
        public int Width
        {
            get;
            private set;
        }

        /// <summary>
        /// span集合
        /// </summary>
        public HeightFieldSpan[,] Span
        {
            get;
            set;
        }

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
