using UnityEngine;

namespace SATIntersectionVoxelize
{
    /// <summary>
    /// 体素相交检查
    /// </summary>
    public static partial class VoxelIntersectionHelper 
    {
        private static bool CheckTouching(Mesh mesh, Transform transform, Bounds voxelBounds)
        {
            float touchDistance = smallEpsilon;

            // 检查体素的八个顶点是否和网格贴近
            foreach (Vector3 corner in voxelCorners)
            {
                if (IsPointNearMesh(corner, mesh, transform, touchDistance))
                    return true;
            }

            // 检查边的中心点
            foreach (Vector3 edgeCenter in voxelEdgeCenters)
            {
                if (IsPointNearMesh(edgeCenter, mesh, transform, touchDistance))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 检查一个点是否和网格贴近
        /// </summary>
        /// <param name="point">要检查的体素顶点</param>
        /// <param name="mesh">网格</param>
        /// <param name="transform"></param>
        /// <param name="threshold">贴近阈值</param>
        /// <returns>贴近返回true</returns>
        private static bool IsPointNearMesh(Vector3 point, Mesh mesh, Transform transform, float threshold)
        {
            //模型空间
            Vector3 localPoint = transform.InverseTransformPoint(point);
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];
                //检查网格每个三角形顶点和体素顶点之间的距离
                if (PointTriangleDistance(localPoint, v0, v1, v2) <= threshold)
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// 点到三角形的距离
        /// </summary>
        private static float PointTriangleDistance(Vector3 point, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            //三角形的三条边
            Vector3 ab = v1 - v0;
            Vector3 bc = v2 - v1;
            Vector3 ca = v0 - v2;

            //体素顶点到三角形顶点的向量
            Vector3 ap = point - v0;
            Vector3 bp = point - v1;
            Vector3 cp = point - v2;

            Vector3 normal = Vector3.Cross(ab, -ca).normalized;

            //空间中点到三角形的最短距离有两种情况：
            //点的投影落在三角形内部 → 最短距离是点到三角形平面的垂直距离
            //点的投影落在三角形外部 → 最短距离是点到三角形边缘的最短距离

            //计算点到三角形平面的垂直距离
            //点到平面的距离 = 点到平面上任意一点的向量在法线方向上的投影长度
            float planeDistance = Mathf.Abs(Vector3.Dot(ap, normal));

            // 检查点是否在三角形内部投影
            float d1 = Vector3.Dot(Vector3.Cross(ab, normal), ap);
            float d2 = Vector3.Dot(Vector3.Cross(bc, normal), bp);
            float d3 = Vector3.Dot(Vector3.Cross(ca, normal), cp);

            //在的话返回点到平面的垂直距离
            if (d1 >= 0.0f && d2 >= 0.0f && d3 >= 0.0f)
                return planeDistance;

            //否则计算点到边的最短距离
            float distance = Mathf.Min(
                PointLineDistance(point, v0, v1),
                PointLineDistance(point, v1, v2),
                PointLineDistance(point, v2, v0)
            );

            return distance;
        }
        
        /// <summary>
        /// 点到线段的距离
        /// </summary>
        private static float PointLineDistance(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 line = end - start;
            float length = line.magnitude;
            line.Normalize();

            Vector3 v = point - start;
            float d = Vector3.Dot(v, line);
            d = Mathf.Clamp(d, 0f, length);

            Vector3 projection = start + line * d;
            return Vector3.Distance(point, projection);
        }
    }
}
