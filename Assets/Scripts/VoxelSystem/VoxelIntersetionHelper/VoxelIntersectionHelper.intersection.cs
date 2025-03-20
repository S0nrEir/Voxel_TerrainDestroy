using System.Collections;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;

namespace Voxel
{
    public static partial class VoxelIntersectionHelper 
    {
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
