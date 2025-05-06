using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace TriRasterizationVoxelization
{
    /// <summary>
    /// 栅格化三角形job
    /// </summary>
    [BurstCompile]
    public struct RasterizeTrianglesJob : IJobParallelFor
    {
        
        [ReadOnly] public NativeList<TriangleJobData> _trianglesList;
        [WriteOnly] public NativeMultiHashMap<int2, SpanJobData>.ParallelWriter _spanResults;
        public Vector3 _heightFieldMin;
        public Vector3 _heightFieldMax;
        public float _inverseCellSize;
        public float _inverseVerticalCellSize;
        public int _width;
        public int _height;
        public float _cellSize;
        public float _verticalCellSize;
        
        public void Execute(int index)
        {
            TriangleJobData tri = _trianglesList[index];
            Vector3 triBBMin = Vector3.Min(Vector3.Min(tri._v0, tri._v1), tri._v2);
            Vector3 triBBMax = Vector3.Max(Vector3.Max(tri._v0, tri._v1), tri._v2);
            if (!OverlapBounds(triBBMin, triBBMax, _heightFieldMin, _heightFieldMax))
                return;
        
            RasterizeTriangleOptimized(tri._v0, tri._v1, tri._v2);
        }
        
        private void RasterizeTriangleOptimized(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            float3 fv0 = new float3(v0.x, v0.y, v0.z);
            float3 fv1 = new float3(v1.x, v1.y, v1.z);
            float3 fv2 = new float3(v2.x, v2.y, v2.z);
            
            // 计算三角形在XZ平面的边界
            int2 minCell = new int2(
                (int)math.floor((math.min(fv0.x, math.min(fv1.x, fv2.x)) - _heightFieldMin.x) * _inverseCellSize),
                (int)math.floor((math.min(fv0.z, math.min(fv1.z, fv2.z)) - _heightFieldMin.z) * _inverseCellSize)
            );
            int2 maxCell = new int2(
                (int)math.ceil((math.max(fv0.x, math.max(fv1.x, fv2.x)) - _heightFieldMin.x) * _inverseCellSize),
                (int)math.ceil((math.max(fv0.z, math.max(fv1.z, fv2.z)) - _heightFieldMin.z) * _inverseCellSize)
            );
            minCell.x = math.max(0, minCell.x);
            minCell.y = math.max(0, minCell.y);
            maxCell.x = math.min(_width - 1, maxCell.x);
            maxCell.y = math.min(_height - 1, maxCell.y);

            float3 normal = math.normalize(math.cross(fv1 - fv0, fv2 - fv0));
            float d = -math.dot(normal, fv0);

            float2 p0 = new float2(fv0.x, fv0.z);
            float2 p1 = new float2(fv1.x, fv1.z);
            float2 p2 = new float2(fv2.x, fv2.z);

            float doubleArea = EdgeFunction(p0, p1, p2);
            
            if (math.abs(doubleArea) < 1e-6f)
                return;

            for (int z = minCell.y; z <= maxCell.y; z++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    float2 cellCenter = new float2(
                        _heightFieldMin.x + (x + 0.5f) * _cellSize,
                        _heightFieldMin.z + (z + 0.5f) * _cellSize
                    );

                    float w0 = EdgeFunction(p1, p2, cellCenter);
                    float w1 = EdgeFunction(p2, p0, cellCenter);
                    float w2 = EdgeFunction(p0, p1, cellCenter);

                    if (w0 >= 0 && w1 >= 0 && w2 >= 0)
                    {
                        w0 /= doubleArea;
                        w1 /= doubleArea;
                        w2 /= doubleArea;

                        float height = w0 * fv0.y + w1 * fv1.y + w2 * fv2.y;
                        
                        float heightTolerance = _cellSize * 0.5f;
                        float minHeight = height - heightTolerance;
                        float maxHeight = height + heightTolerance;

                        minHeight -= _heightFieldMin.y;
                        maxHeight -= _heightFieldMin.y;

                        ushort spanMin = (ushort)math.clamp(
                            (int)math.floor(minHeight * _inverseVerticalCellSize),
                            0, 65535);
                        ushort spanMax = (ushort)math.clamp(
                            (int)math.ceil(maxHeight * _inverseVerticalCellSize),
                            spanMin + 1, 65535);

                        _spanResults.Add(new int2(x, z), new SpanJobData {
                            _min = spanMin,
                            _max = spanMax
                        });
                    }
                }
            }
        }

        // 计算三角形边缘函数（用于重心坐标计算）
        private float EdgeFunction(float2 a, float2 b, float2 c)
        {
            return (c.x - a.x) * (b.y - a.y) - (c.y - a.y) * (b.x - a.x);
        }
        
        private bool OverlapBounds(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
        {
            return aMin.x <= bMax.x && aMax.x >= bMin.x &&
                   aMin.y <= bMax.y && aMax.y >= bMin.y &&
                   aMin.z <= bMax.z && aMax.z >= bMin.z;
        }
    }

}
