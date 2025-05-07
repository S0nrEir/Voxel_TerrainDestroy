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
    // [BurstCompile]
    public struct RasterizeTrianglesJob : IJobParallelFor
    {
        public void Execute(int index)
        {
            TriangleJobData tri = _trianglesList[index];
            Vector3 triBBMin = Vector3.Min(Vector3.Min(tri._v0, tri._v1), tri._v2);
            Vector3 triBBMax = Vector3.Max(Vector3.Max(tri._v0, tri._v1), tri._v2);
            
            if (!OverlapBounds(triBBMin, triBBMax, _heightFieldMin, _heightFieldMax))
                return;

            var len = _trianglesList.Length;
            for (var i = 0; i < len; i++)
            {
                int w = _heightFieldWidth;
                int h = _heightFieldHeight;
                float by = _heightFieldMax.y - _heightFieldMin.y;

                int z0 = (int)((triBBMin.z - _heightFieldMin.z) * _inverseCellSize);
                int z1 = (int)((triBBMax.z - _heightFieldMin.z) * _inverseCellSize);

                z0 = math.clamp(z0, -1, h - 1);
                z1 = math.clamp(z1, 0, h - 1);

                NativeList<Vector3> inVerts    = new NativeList<Vector3>(Allocator.Temp);
                NativeList<Vector3> inRowVerts = new NativeList<Vector3>(Allocator.Temp);
                NativeList<Vector3> p1Verts    = new NativeList<Vector3>(Allocator.Temp);
                NativeList<Vector3> p2Verts    = new NativeList<Vector3>(Allocator.Temp);
                
                inVerts.Add(_trianglesList[i]._v0);
                inVerts.Add(_trianglesList[i]._v1);
                inVerts.Add(_trianglesList[i]._v2);
                int nvRow;
                int nvIn = 3;

                for (int z = z0; z <= z1; z++)
                {
                    float cellZ = _heightFieldMin.z + (float)z * _cellSize;

                    DividePoly(inVerts, inRowVerts, out nvRow, p1Verts, out nvIn, cellZ + _cellSize,
                        AxisTypeEnum.Z);

                    (inVerts, p1Verts) = (p1Verts, inVerts);

                    if (nvRow < 3 || z < 0)
                        continue;

                    float minX = inRowVerts[0].x;
                    float maxX = inRowVerts[0].x;
                    for (int vert = 1; vert < nvRow; vert++)
                    {
                        minX = Mathf.Min(minX, inRowVerts[vert].x);
                        maxX = Mathf.Max(maxX, inRowVerts[vert].x);
                    }

                    int x0 = (int)((minX - _heightFieldMin.x) * _inverseCellSize);
                    int x1 = (int)((maxX - _heightFieldMin.x) * _inverseCellSize);

                    if (x1 < 0 || x0 >= w)
                        continue;

                    x0 = Mathf.Clamp(x0, -1, w - 1);
                    x1 = Mathf.Clamp(x1, 0, w - 1);

                    int nv;
                    int nv2 = nvRow;

                    for (int x = x0; x <= x1; x++)
                    {
                        float cx = _heightFieldMin.x + (float)x * _cellSize;
                        DividePoly(inRowVerts, p1Verts, out nv, p2Verts, out nv2, cx + _cellSize,AxisTypeEnum.X);

                        (inRowVerts, p2Verts) = (p2Verts, inRowVerts);

                        if (nv < 3 || x < 0)
                            continue;

                        float spanMin = p1Verts[0].y;
                        float spanMax = p1Verts[0].y;

                        for (int vert = 1; vert < nv; vert++)
                        {
                            spanMin = math.min(spanMin, p1Verts[vert].y);
                            spanMax = math.max(spanMax, p1Verts[vert].y);
                        }

                        spanMin -= _heightFieldMin.y;
                        spanMax -= _heightFieldMin.y;

                        if (spanMax < 0f || spanMin > by || spanMin < 0f || spanMax > by)
                            continue;

                        spanMin = math.max(0.0f, spanMin);
                        spanMax = math.min(by, spanMax);

                        ushort spanMinCellIndex = (ushort)math.clamp((int)math.floor(spanMin * _inverseVerticalCellHeight), 0,(1 << 0xd ) - 1);
                        ushort spanMaxCellIndex = (ushort)math.clamp((int)math.ceil(spanMax * _inverseVerticalCellHeight),(int)spanMinCellIndex + 1, (1 << 0xd ) - 1);
                        // 添加体素到高度场中
                        AddSpan(x, z, spanMinCellIndex, spanMaxCellIndex, -1);
                    }
                }

                inVerts.Dispose();
                inRowVerts.Dispose();
                p1Verts.Dispose();
                p2Verts.Dispose();
            }
        }

        private void AddSpan(int x,int z,ushort min,ushort max,int flagMergeThreshold)
        {
            _spanResults.Add(new int2(x, z), new SpanJobData(){ _max = max,_min = min});
        }
        
        // [BurstCompile]
        private void DividePoly(
            NativeList<Vector3> inVerts,
            NativeList<Vector3> outVerts1,
            out int outVerts1Count,
            NativeList<Vector3> outVerts2,
            out int outVerts2Count,
            float axisOffset,
            AxisTypeEnum axis)
        {
            Debug.Assert(inVerts.Length <= 12);
            
            outVerts1.Clear();
            outVerts2.Clear();
            
            float[] inVertAxisDelta = new float[inVerts.Length];
            for (int i = 0; i < inVerts.Length; i++)
            {
                var axisValue = axis == AxisTypeEnum.X ? inVerts[i].x : axis == AxisTypeEnum.Y ? inVerts[i].y : inVerts[i].z;
                inVertAxisDelta[i] = axisOffset - axisValue;
            }
            
            for (int inVertA = 0, inVertB = inVerts.Length - 1; inVertA < inVerts.Length; inVertB = inVertA, inVertA++)
            {
                bool sameSide = (inVertAxisDelta[inVertA] >= 0) == (inVertAxisDelta[inVertB] >= 0);
                if (!sameSide)
                {
                    float s = inVertAxisDelta[inVertB] / (inVertAxisDelta[inVertB] - inVertAxisDelta[inVertA]);
                    Vector3 intersection = Vector3.Lerp(inVerts[inVertB], inVerts[inVertA], s);
                    
                    outVerts1.Add(intersection);
                    outVerts2.Add(intersection);
                    
                    if (inVertAxisDelta[inVertA] > 0)
                        outVerts1.Add(inVerts[inVertA]);
                    else if (inVertAxisDelta[inVertA] < 0)
                        outVerts2.Add(inVerts[inVertA]);
                }
                else
                {
                    if (inVertAxisDelta[inVertA] >= 0)
                    {
                        outVerts1.Add(inVerts[inVertA]);
                        if (inVertAxisDelta[inVertA] != 0)
                            continue;
                    }
                    outVerts2.Add(inVerts[inVertA]);
                }
            }

            outVerts1Count = outVerts1.Length;
            outVerts2Count = outVerts2.Length;
        }

        [BurstCompile(CompileSynchronously = true)]
        private bool OverlapBounds(float3 aMin, float3 aMax, float3 bMin, float3 bMax)
        {
            return aMin.x <= bMax.x && aMax.x >= bMin.x &&
                   aMin.y <= bMax.y && aMax.y >= bMin.y &&
                   aMin.z <= bMax.z && aMax.z >= bMin.z;
        }
        
        [ReadOnly] public NativeList<TriangleJobData> _trianglesList;
        [WriteOnly] public NativeMultiHashMap<int2, SpanJobData>.ParallelWriter _spanResults;
        public Vector3 _heightFieldMin;
        public Vector3 _heightFieldMax;
        public float _inverseCellSize;
        public float _inverseVerticalCellHeight;
        public int _heightFieldWidth;
        public int _heightFieldHeight;
        public float _cellSize;
        public float _verticalCellSize;
    }

}
