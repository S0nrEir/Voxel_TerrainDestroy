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

        public void Init(Vector3 min,Vector3 max,float horizontalCellSize,float verticalCellSize)
        {
            CellSize = horizontalCellSize;
            VerticalCellSize = verticalCellSize;
            
            float minX = Mathf.Floor(min.x / horizontalCellSize) * horizontalCellSize;
            float minZ = Mathf.Floor(min.z / horizontalCellSize) * horizontalCellSize;
            Min = new Vector3(minX, min.y, minZ);
            
            float maxX = Mathf.Ceil(max.x / horizontalCellSize) * horizontalCellSize;
            float maxZ = Mathf.Ceil(max.z / horizontalCellSize) * horizontalCellSize;
            Max = new Vector3(maxX, max.y, maxZ);

            Width = Mathf.Abs(Mathf.RoundToInt((Max.x - Min.x) / horizontalCellSize));
            Height = Mathf.Abs(Mathf.RoundToInt((Max.z - Min.z) / horizontalCellSize));

            Width = Mathf.Max(1, Width);
            Height = Mathf.Max(1, Height);

            Span = new HeightFieldSpan[Width, Height];
        }

        /// <summary>
        /// 清除数据
        /// </summary>
        public void Clear()
        {
            Span             = null;
            CellSize         = -1;
            VerticalCellSize = -1;
            Min              = Vector3.zero;
            Max              = Vector3.zero;
            Width            = -1;
            Height           = -1;
        }

        /// <param name="min">高度场的最小位置</param>
        /// <param name="max">高度场的最大位置</param>
        /// <param name="horizontalCellSize">水平方向单个体素大小</param>
        /// <param name="verticalCellSize">垂直方向单个体素大小</param>
        public HeightField(Vector3 min,Vector3 max,float horizontalCellSize,float verticalCellSize) => Init(min,max,horizontalCellSize,verticalCellSize);

        /// <summary>
        /// 高度场长度，z轴方向格子数
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
        public float CellSize
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
