using System.Diagnostics;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Voxel
{
    /// <summary>
    /// 体素相交检查
    /// </summary>
    [BurstCompile]
    public static partial class VoxelIntersectionHelper 
    {
        /// <summary>
        /// 相交检测
        /// </summary>
        public static (VoxelData.VoxelState state, long duration) IsIntersection(Bounds voxelBounds, GameObject obj,MeshFilter mf,int nodeID = -1)
        {
            Watch.Start();
            Mesh mesh = mf.sharedMesh;
            Transform transform = obj.transform;
            CalculateVoxelPoints(voxelBounds);

            if(_instanceID != obj.GetInstanceID())
            {
                _instanceID          = nodeID;
                _processingVertices  = mesh.vertices;
                _processingTriangles = mesh.triangles;
            }

            // 1. 检查体素中心点是否在网格内部
            //从体素中心点向任意方向发射射线，检查体素与该模型的碰撞次数，奇数次表示体素在内部，检查射线与三角形是否相交
            if ( IsPointInsideMesh( voxelBounds.center, mesh, transform ) )
            {
                Watch.Stop();
                RecordDuration( ( float ) Watch.ElapsedMilliseconds / 1000 );
                Watch.Reset();
                return ( VoxelData.VoxelState.Solid , Watch.ElapsedMilliseconds);
            }

            // 2. 检查网格三角形是否与体素相交
            //检查三角形的三个顶点是否都在包围盒内
            //检查三角形的边是否与包围盒相交，拿三角形的每一个边和体素的起点和终点做投影的检测（占比了多少）
            // if (CheckMeshVoxelIntersection(mesh, transform, voxelBounds))
            //     return VoxelData.VoxelState.Intersecting;
            
            //SAT相交检测
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                float3 vert0   = transform.TransformPoint(vertices[triangles[i]]);
                float3 vert1   = transform.TransformPoint(vertices[triangles[i + 1]]);
                float3 vert2   = transform.TransformPoint(vertices[triangles[i + 2]]);
                float3 center  = voxelBounds.center;
                float3 extents = voxelBounds.extents;

                if ( SATIntersect( in vert0, in vert1, in vert2, in center, in extents ) == 1)
                {
                    Watch.Stop();
                    RecordDuration( ( float ) Watch.ElapsedMilliseconds / 1000 );
                    Watch.Reset();
                    return (VoxelData.VoxelState.Intersecting, Watch.ElapsedMilliseconds);
                }
            }

            //优化：取消相切检测
            // 3. 检查是否相切（检查边缘点和顶点）
            //顶点到三角形三个顶点的向量，和三角形顶点的法向量作投影比较，三个顶点的比较结果都在0到1之间则表示在三角形内部
            //直接返回点到三角形的垂直距离，否则返回点到三角形边的最短距离
            // if ( CheckTouching( mesh, transform, voxelBounds ) )
            // {
            //     Watch.Stop();
            //     RecordDuration( ( float ) Watch.ElapsedMilliseconds / 1000 );
            //     Watch.Reset();
            //     return ( VoxelData.VoxelState.Touching ,Watch.ElapsedMilliseconds);
            // }

            Watch.Stop();
            RecordDuration( (float)Watch.ElapsedMilliseconds / 1000 );
            Watch.Reset();
            return ( VoxelData.VoxelState.Empty ,Watch.ElapsedMilliseconds);
        }

        private static void CalculateVoxelPoints(Bounds bounds)
        {
            Vector3 min  = bounds.min;
            Vector3 max  = bounds.max;
            Vector3 size = bounds.size;

            // 计算8个顶点
            voxelCorners[0] = new Vector3(min.x, min.y, min.z);
            voxelCorners[1] = new Vector3(max.x, min.y, min.z);
            voxelCorners[2] = new Vector3(min.x, max.y, min.z);
            voxelCorners[3] = new Vector3(max.x, max.y, min.z);
            voxelCorners[4] = new Vector3(min.x, min.y, max.z);
            voxelCorners[5] = new Vector3(max.x, min.y, max.z);
            voxelCorners[6] = new Vector3(min.x, max.y, max.z);
            voxelCorners[7] = new Vector3(max.x, max.y, max.z);

            // 计算12个边的中心点
            voxelEdgeCenters[0]  = Vector3.Lerp(voxelCorners[0], voxelCorners[1], 0.5f);
            voxelEdgeCenters[1]  = Vector3.Lerp(voxelCorners[1], voxelCorners[3], 0.5f);
            voxelEdgeCenters[2]  = Vector3.Lerp(voxelCorners[2], voxelCorners[3], 0.5f);
            voxelEdgeCenters[3]  = Vector3.Lerp(voxelCorners[0], voxelCorners[2], 0.5f);
            voxelEdgeCenters[4]  = Vector3.Lerp(voxelCorners[4], voxelCorners[5], 0.5f);
            voxelEdgeCenters[5]  = Vector3.Lerp(voxelCorners[5], voxelCorners[7], 0.5f);
            voxelEdgeCenters[6]  = Vector3.Lerp(voxelCorners[6], voxelCorners[7], 0.5f);
            voxelEdgeCenters[7]  = Vector3.Lerp(voxelCorners[4], voxelCorners[6], 0.5f);
            voxelEdgeCenters[8]  = Vector3.Lerp(voxelCorners[0], voxelCorners[4], 0.5f);
            voxelEdgeCenters[9]  = Vector3.Lerp(voxelCorners[1], voxelCorners[5], 0.5f);
            voxelEdgeCenters[10] = Vector3.Lerp(voxelCorners[2], voxelCorners[6], 0.5f);
            voxelEdgeCenters[11] = Vector3.Lerp(voxelCorners[3], voxelCorners[7], 0.5f);
        }

        private static void RecordDuration( float duration )
        {
            if ( duration > _longgestDuration )
                _longgestDuration = duration;

            UnityEngine.Debug.Log( $"duration:{duration}" );
        }

        /// <summary>
        /// 体素顶点
        /// </summary>
        private static Vector3[] voxelCorners = new Vector3[8];

        /// <summary>
        /// 体素条边
        /// </summary>
        private static UnityEngine.Vector3[] voxelEdgeCenters = new Vector3[12];
        private static readonly float smallEpsilon = 0.01f;
        private static Stopwatch Watch = new Stopwatch();
        public static float _longgestDuration = 0;
        private static int _instanceID = -1;
        private static Vector3[] _processingVertices = null;
        private static int[] _processingTriangles = null;
    }
}
