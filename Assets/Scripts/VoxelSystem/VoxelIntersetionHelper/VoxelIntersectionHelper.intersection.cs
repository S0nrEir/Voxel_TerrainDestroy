using System.Collections;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;

namespace Voxel
{
    public static partial class VoxelIntersectionHelper 
    {
        public static bool SATIntersect(Vector3 vert0,Vector3 vert1,Vector3 vert2,Bounds aabb)
        {
            Vector3 center  = aabb.center;
            var     extents = aabb.extents;
            
            var edge_0 = vert1 - vert0;
            var edge_1 = vert2 - vert1;
            var edge_2 = vert0 - vert2;
            var normal = Vector3.Cross(edge_0, edge_1).normalized;

            var boxProjectionRadius = extents.x * Mathf.Abs(normal.x) +
                                      extents.y * Mathf.Abs(normal.y) +
                                      extents.z * Mathf.Abs(normal.z);

            var planeDistance = Vector3.Dot(normal, vert0);
            var boxCenterDistance = Vector3.Dot(normal, center) - planeDistance;
            if (Mathf.Abs(boxCenterDistance) > boxProjectionRadius)
                return false;

            float minX = Mathf.Min(vert0.x, Mathf.Min(vert1.x, vert2.x));
            float maxX = Mathf.Max(vert0.x, Mathf.Max(vert1.x, vert2.x));
            float minY = Mathf.Min(vert0.y, Mathf.Min(vert1.y, vert2.y));
            float maxY = Mathf.Max(vert0.y, Mathf.Max(vert1.y, vert2.y));
            float minZ = Mathf.Min(vert0.z, Mathf.Min(vert1.z, vert2.z));
            float maxZ = Mathf.Max(vert0.z, Mathf.Max(vert1.z, vert2.z));

            // 检查包围盒和三角形在三个主轴上的投影是否重叠
            if (maxX < center.x - extents.x || minX > center.x + extents.x)
                return false;
            if (maxY < center.y - extents.y || minY > center.y + extents.y)
                return false;
            if (maxZ < center.z - extents.z || minZ > center.z + extents.z)
                return false;
                
            var aabbAxes = new Vector3[]{
                Vector3.right,
                Vector3.up,
                Vector3.forward
            };

            var triangleEdges = new Vector3[]{
                edge_0,
                edge_1,
                edge_2
            };

            for(var i = 0 ;i<3;i++)
            {
                for(var j = 0 ; j < 3 ; j++)
                {
                    Vector3 axis = Vector3.Cross(triangleEdges[i], aabbAxes[j]);
                    
                    // 如果叉积为零向量，则跳过
                    if (axis.sqrMagnitude < smallEpsilon)
                        continue;
                    
                    // 标准化轴向量
                    axis.Normalize();
                    
                    // 计算三角形在该轴上的投影范围
                    // var triMin = 0f;
                    // var triMax = 0f;
                    (float triMin,float triMax) = ProjectTriangle(vert0, vert1, vert2, axis);
                    
                    // 计算包围盒在该轴上的投影范围
                    (float boxMin,float boxMax) = ProjectBox(center, extents, axis);
                    
                    // 检查投影是否重叠
                    if (triMax < boxMin || triMin > boxMax)
                        return false; // 找到分离轴，不相交
                }
            }
            return true;
        }

        /// <summary>
        /// 将三角形投影到指定轴上
        /// </summary>
        private static (float triain,float triMax) ProjectTriangle(Vector3 vert0, Vector3 vert1, Vector3 vert2, Vector3 axis)
        {
            float dot0 = Vector3.Dot(vert0, axis);
            float dot1 = Vector3.Dot(vert1, axis);
            float dot2 = Vector3.Dot(vert2, axis);
            
            // min = Mathf.Min(dot0, Mathf.Min(dot1, dot2));
            // max = Mathf.Max(dot0, Mathf.Max(dot1, dot2));
            return (Mathf.Min(dot0, Mathf.Min(dot1, dot2)) , Mathf.Max(dot0, Mathf.Max(dot1, dot2)));
        }

        /// <summary>
        /// 将AABB包围盒投影到指定轴上
        /// </summary>
        private static (float boxMin,float boxMax) ProjectBox(Vector3 center, Vector3 halfSize, Vector3 axis)
        {
            // 计算包围盒在该轴上的投影半径
            float radius = 
                halfSize.x * Mathf.Abs(Vector3.Dot(axis, Vector3.right)) +
                halfSize.y * Mathf.Abs(Vector3.Dot(axis, Vector3.up)) +
                halfSize.z * Mathf.Abs(Vector3.Dot(axis, Vector3.forward));
            
            // 计算中心点在轴上的投影
            float centerProj = Vector3.Dot(center, axis);
            
            // min = centerProj - radius;
            // max = centerProj + radius;
            return (centerProj - radius , centerProj + radius );
        }        

        private static bool CheckMeshVoxelIntersection(Mesh mesh, Transform transform, Bounds voxelBounds)
        {
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;

            // 检查模型的每个三角形是否与体素相交
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = transform.TransformPoint(vertices[triangles[i]]);
                Vector3 v1 = transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 2]]);

                if (TriangleBoxIntersection(v0, v1, v2, voxelBounds))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 三角形与包围盒相交检测，相交返回true
        /// </summary>
        private static bool TriangleBoxIntersection(Vector3 v0, Vector3 v1, Vector3 v2, Bounds bounds)
        {
            //#attention:三角形三个顶点都不在包围盒内，但是三角形和包围盒相交（三角形穿过包围盒）
            //#attention:三角形的所有顶点都在体素外部，三角形的边不与体素相交，但是三角形平面穿过了体素
            //如果三个顶点都包含在包围盒内，则三角形在包围盒内
            if (!bounds.Contains(v0) && !bounds.Contains(v1) && !bounds.Contains(v2))//三个顶点都不在包围盒内，就检查有任意一条边和包围盒是否相交
            {
                Vector3 center = bounds.center;
                Vector3 extents = bounds.extents;

                // 检查三角形边是否与包围盒相交
                if (LineBoxIntersection(v0, v1, center, extents) ||
                    LineBoxIntersection(v1, v2, center, extents) ||
                    LineBoxIntersection(v2, v0, center, extents))
                    return true;

                return false;
            }

            return true;
        }

        /// <summary>
        /// 线段与包围盒相交检测
        /// </summary>
        /// <param name="start">顶点位置1</param>
        /// <param name="end">顶点位置2</param>
        /// <param name="boxCenter">包围盒（体素）中心</param>
        /// <param name="boxExtents">包围盒范围</param>
        /// <returns></returns>
        private static bool LineBoxIntersection(Vector3 start, Vector3 end, Vector3 boxCenter, Vector3 boxExtents)
        {
            Vector3 direction = end - start;
            Vector3 dirFrac = new Vector3(
                1.0f / (direction.x == 0 ? smallEpsilon : direction.x),
                1.0f / (direction.y == 0 ? smallEpsilon : direction.y),
                1.0f / (direction.z == 0 ? smallEpsilon : direction.z)
            );

            //包围盒的最小和最大位置
            Vector3 lb = boxCenter - boxExtents;
            Vector3 rt = boxCenter + boxExtents;

            //计算线段与包围盒在每个轴上的交点，t表示线段从起点到交点的比例

            ////体素最小位置的点到顶点某分量的距离，除以两顶点之间的长度，算出的比例可以表示体素是否处于两个顶点之间
            // 如果在0到1之间则表示体素在两顶点之间
            // 如果小于0则表示体素在顶点的后面（不相交）
            // 如果大于1则表示体素在顶点的前面（不相交）
            float t1 = (lb.x - start.x) * dirFrac.x;
            float t2 = (rt.x - start.x) * dirFrac.x;

            float t3 = (lb.y - start.y) * dirFrac.y;
            float t4 = (rt.y - start.y) * dirFrac.y;

            float t5 = (lb.z - start.z) * dirFrac.z;
            float t6 = (rt.z - start.z) * dirFrac.z;

            //如果 tmax< 0，表示线段在包围盒的后面。
            //如果 tmin > tmax，表示线段没有穿过包围盒。
            //如果 tmin > 1，表示线段的起点在包围盒外部。
            float tmin = Mathf.Max(Mathf.Max(Mathf.Min(t1, t2), Mathf.Min(t3, t4)), Mathf.Min(t5, t6));
            float tmax = Mathf.Min(Mathf.Min(Mathf.Max(t1, t2), Mathf.Max(t3, t4)), Mathf.Max(t5, t6));

            return !(tmax < 0 || tmin > tmax || tmin > 1);
        }
    }
}
