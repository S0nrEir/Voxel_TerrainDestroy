using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voxel
{
    public static partial class VoxelIntersectionHelper 
    {
        /// <summary>
        /// 检查体素是否在模型内部
        /// </summary>
        private static bool IsPointInsideMesh(Vector3 point, Mesh mesh, Transform transform)
        {
            //将点转换到模型空间
            //将体素中心点转换到模型空间
            Vector3 localPoint = transform.InverseTransformPoint(point);

            //优化：提前排除不在模型包围盒内的体素
            if (!mesh.bounds.Contains(localPoint))
                return false;

            //交互次数
            // var triangles = mesh.triangles;
            // var vertices = mesh.vertices;
            var rayIntersectionTimes = 0;
            var insideRayCount = 0;
            
            for(var dIndex = 0; dIndex < _rayEndPoint.Length; dIndex++)
            {
                // 从体素中心点向任意方向发射射线，检查与模型三角面的碰撞次数
                //#attention:发射多次并且使用水密法+多条射线避免射线可能击中共享一条边的两个相邻三角形的情况
                Ray ray = new Ray(localPoint + _rayOffset, _rayEndPoint[dIndex] + _rayOffset);

                rayIntersectionTimes = 0;
                // 统计射线与三角形的相交次数
                for (int i = 0; i < _processingTriangles.Length; i += 3)
                {
                    Vector3 v0 = _processingVertices[_processingTriangles[i]];
                    Vector3 v1 = _processingVertices[_processingTriangles[i + 1]];
                    Vector3 v2 = _processingVertices[_processingTriangles[i + 2]];

                    if (RayTriangleIntersection(ray, v0, v1, v2))
                        rayIntersectionTimes++;
                }

                if(rayIntersectionTimes % 2 == 1)
                    insideRayCount++;
            }

            //奇数次相交表示点在内部
            //这是因为射线从点出发时，如果点在网格内部，射线必然会穿过网格的边界奇数次才能到达外部
            // return ( intersections % 2) == 1;
            return insideRayCount > _rayEndPoint.Length / 2;
        }


        /// <summary>
        /// Möller–Trumbore射线与三角形相交检测法，检查射线是否与三角形相交
        /// <para>思路是计算射线与三角形所构成平面的交点，检查该交点是否在三角形平面内，来判断是否发生了交叉</para>
        /// </summary>
        public static bool RayTriangleIntersection(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2)
        {
#region 原始方案

            //三角形的两条边
            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 h = Vector3.Cross(ray.direction, edge2);
            float a = Vector3.Dot(edge1, h);

            //**平行意味着射线方向与三角形所在的平面没有交点**
            //a用于检查射线是否与三角形平平行或几乎平行
            if (a > -smallEpsilon && a < smallEpsilon)
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
            Vector3 s = ray.origin - v0;
            float u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u >= 1.0f)
                return false;

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(ray.direction, q);

            if (v < 0.0f || u + v >= 1.0f)
                return false;

            float t = f * Vector3.Dot(edge2, q);
            return t > smallEpsilon;

#endregion
        }

        /// <summary>
        /// 体素位于模型内部检查的射线终点
        /// 优化：修改为三次，不再使用五次射线检测
        /// </summary>
        private static UnityEngine.Vector3[] _rayEndPoint = new Vector3[]{
            Vector3.up,
            Vector3.down,
            Vector3.left,
        };

        private static Vector3 _rayOffset = new Vector3(
                    0.0000001f * ((0 * 11) % 7 - 3.5f),
                    0.0000001f * ((1 * 13) % 7 - 3.5f),
                    0.0000001f * ((2 * 17) % 7 - 3.5f)
                );
    }
}

