using UnityEngine;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine.SocialPlatforms;

namespace Voxel
{
    public static partial class VoxelIntersectionHelper 
    {
        /// <summary>
        /// 检查体素是否在模型内部
        /// </summary>
        // [MethodImpl(MethodImplOptions.NoInlining)]
        // [BurstCompile(CompileSynchronously=true)]
        // private static bool IsPointInsideMesh(
        //     in float3 localPoint,
        //     in float3 rayOffset,
        //     in float3 vert0,
        //     in float3 vert1, 
        //     in float3 vert2,
        //     in float3 rayOrigin,
        //     in float3 rayDirection)
        // {
        //     int rayIntersectionTimes = 0;
        //     int insideRayCount = 0;
        //     float3 normalizedDirection = math.normalize(rayDirection);
        //     for(var dIndex = 0; dIndex < _rayEndPoint.Length; dIndex++)
        //     {
        //         // 从体素中心点向任意方向发射射线，检查与模型三角面的碰撞次数
        //         //#attention:发射多次并且使用水密法+多条射线避免射线可能击中共享一条边的两个相邻三角形的情况
        //         Ray ray = new Ray(localPoint + rayOffset, _rayEndPoint[dIndex] + rayOffset);

        //         rayIntersectionTimes = 0;
        //         // 统计射线与三角形的相交次数
        //         for (int i = 0; i < _processingTriangles.Length; i += 3)
        //         {
        //             // Vector3 v0 = _processingVertices[_processingTriangles[i]];
        //             // Vector3 v1 = _processingVertices[_processingTriangles[i + 1]];
        //             // Vector3 v2 = _processingVertices[_processingTriangles[i + 2]];

        //             if (RayTriangleIntersection(vert0, vert1, vert2,in rayOrigin,in normalizedDirection))
        //                 rayIntersectionTimes++;
        //         }

        //         if(rayIntersectionTimes % 2 == 1)
        //             insideRayCount++;
        //     }

        //     //奇数次相交表示点在内部
        //     //这是因为射线从点出发时，如果点在网格内部，射线必然会穿过网格的边界奇数次才能到达外部
        //     return insideRayCount > _rayEndPoint.Length / 2;
        // }


        /// <summary>
        /// Möller–Trumbore射线与三角形相交检测法，检查射线是否与三角形相交
        /// <para>思路是计算射线与三角形所构成平面的交点，检查该交点是否在三角形平面内，来判断是否发生了交叉</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        [BurstCompile(CompileSynchronously=true)]
        public static bool RayTriangleIntersection(in float3 v0, in float3 v1, in float3 v2,in float3 rayOrigin,in float3 rayDirection)
        {
            //三角形的两条边
            float3 edge1 = v1 - v0;
            float3 edge2 = v2 - v0;
            float3 h     = math.cross(rayDirection, edge2);
            float  a     = math.dot(edge1, h);

            //**平行意味着射线方向与三角形所在的平面没有交点**
            //a用于检查射线是否与三角形平平行或几乎平行
            const float epsilon = 1e-6f;
            if (a > -epsilon && a < epsilon)
                return false;
                
            //U和V代表三角形三个顶点的权重中的两个，也即重心坐标
            //重心坐标的三个值u v w的范围都在0到1内。并且满足u+v+w=1
            //在下面的步骤中。u和v将通过射线和三角形的交点计算出来
            //所以下面的步骤其实就是看计算出的重心坐标是不是在三角形内部

            //** u用于判断交点是否在三角形的边界内。如果 u 不在 [0, 1] 范围内，则交点不在三角形内，射线与三角形不相交。 **
            //u 表示交点在三角形边 edge1 上的相对位置
            float f = 1.0f / a;

            //这里实际上就是：让从三角形顶点到射线源的向量，与三角形顶点之间的线段作投影，如果超出去则视为重心坐标不在三角形平面内
            //如果想用同一个向量（从三角形顶点到射线源）进行所有比较，
            //则与他比较的对象可以取垂直于这条射线和另一个定点向量叉乘得到的垂直向量
            float3 s = rayOrigin - v0;
            float u = f * math.dot(s, h);

            if (u < 0.0f || u >= 1.0f)
                return false;

            float3 q = math.cross(s, edge1);
            float v = f * math.dot(rayDirection, q);

            if (v < 0.0f || u + v >= 1.0f)
                return false;

            float t = f * math.dot(edge2, q);
            return t > epsilon;
        }
    }
}

