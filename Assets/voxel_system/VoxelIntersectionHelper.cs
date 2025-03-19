using UnityEngine;

public static class VoxelIntersectionHelper
{

    //检查体素和模型是否相交
    public static VoxelData.VoxelState CheckIntersection(Bounds voxelBounds, GameObject obj,int nodeID = -1)
    {
        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            // Debug.LogError("No mesh filter found on object");
            return VoxelData.VoxelState.Empty;   
        }

        Mesh mesh = meshFilter.sharedMesh;
        Transform transform = obj.transform;

        // 获取体素的顶点和边心点
        CalculateVoxelPoints(voxelBounds);
        // 1. 检查体素中心点是否在网格内部
        //从体素中心点向任意方向发射射线，检查体素与该模型的碰撞次数，奇数次表示体素在内部，检查射线与三角形是否相交
        if (IsPointInsideMesh(voxelBounds.center, mesh, transform))
            return VoxelData.VoxelState.Solid;

        // 2. 检查网格三角形是否与体素相交
        //检查三角形的三个顶点是否都在包围盒内
        //检查三角形的边是否与包围盒相交，拿三角形的每一个边和体素的起点和终点做投影的检测（占比了多少）
        if (CheckMeshVoxelIntersection(mesh, transform, voxelBounds))
            return VoxelData.VoxelState.Intersecting;

        // 3. 检查是否相切（检查边缘点和顶点）
        //顶点到三角形三个顶点的向量，和三角形顶点的法向量作投影比较，三个顶点的比较结果都在0到1之间则表示在三角形内部
        //直接返回点到三角形的垂直距离，否则返回点到三角形边的最短距离
        if (CheckTouching(mesh, transform, voxelBounds))
            return VoxelData.VoxelState.Touching;

        return VoxelData.VoxelState.Empty;
    }

    //获取体素的8个顶点和12条边的中心点
    private static void CalculateVoxelPoints(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
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
        voxelEdgeCenters[0] = Vector3.Lerp(voxelCorners[0], voxelCorners[1], 0.5f);
        voxelEdgeCenters[1] = Vector3.Lerp(voxelCorners[1], voxelCorners[3], 0.5f);
        voxelEdgeCenters[2] = Vector3.Lerp(voxelCorners[2], voxelCorners[3], 0.5f);
        voxelEdgeCenters[3] = Vector3.Lerp(voxelCorners[0], voxelCorners[2], 0.5f);
        voxelEdgeCenters[4] = Vector3.Lerp(voxelCorners[4], voxelCorners[5], 0.5f);
        voxelEdgeCenters[5] = Vector3.Lerp(voxelCorners[5], voxelCorners[7], 0.5f);
        voxelEdgeCenters[6] = Vector3.Lerp(voxelCorners[6], voxelCorners[7], 0.5f);
        voxelEdgeCenters[7] = Vector3.Lerp(voxelCorners[4], voxelCorners[6], 0.5f);
        voxelEdgeCenters[8] = Vector3.Lerp(voxelCorners[0], voxelCorners[4], 0.5f);
        voxelEdgeCenters[9] = Vector3.Lerp(voxelCorners[1], voxelCorners[5], 0.5f);
        voxelEdgeCenters[10] = Vector3.Lerp(voxelCorners[2], voxelCorners[6], 0.5f);
        voxelEdgeCenters[11] = Vector3.Lerp(voxelCorners[3], voxelCorners[7], 0.5f);
    }

    //检查体素的中心点是否在网格内部
    private static bool IsPointInsideMesh(Vector3 point, Mesh mesh, Transform transform)
    {
        //将点转换到模型空间
        //将体素中心点转换到模型空间
        Vector3 localPoint = transform.InverseTransformPoint(point);

        //交互次数
        var triangles = mesh.triangles;
        var vertices = mesh.vertices;
        var rayIntersectionTimes = 0;
        var insideRayCount = 0;
        
        for(var dIndex = 0; dIndex < _rayEndPoint.Length; dIndex++)
        {
            var offset = new Vector3(
                0.0000001f * ((dIndex * 11) % 7 - 3.5f),
                0.0000001f * ((dIndex * 13) % 7 - 3.5f),
                0.0000001f * ((dIndex * 17) % 7 - 3.5f)
            );

            // 从体素中心点向任意方向发射射线，检查与模型三角面的碰撞次数
            // 发射多次并且使用水密法+多条射线避免射线可能击中共享一条边的两个相邻三角形的情况
            Ray ray = new Ray(localPoint + offset, _rayEndPoint[dIndex]);

            rayIntersectionTimes = 0;
            // 统计射线与三角形的相交次数
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = vertices[triangles[i]];
                Vector3 v1 = vertices[triangles[i + 1]];
                Vector3 v2 = vertices[triangles[i + 2]];

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
    /// 三角形与包围盒相交检测，相交返回true
    /// </summary>
    private static bool TriangleBoxIntersection(Vector3 v0, Vector3 v1, Vector3 v2, Bounds bounds)
    {
        //如果三个顶点都包含在包围盒内，则三角形在包围盒内
        if (!bounds.Contains(v0) && !bounds.Contains(v1) && !bounds.Contains(v2))
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

        //方向向量的倒数，使用smallEpsilon来避免除零的问题
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

    // 辅助方法：点到三角形的距离
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

    // 辅助方法：点到线段的距离
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

        /// <summary>
    /// 体素的八个顶点
    /// </summary>
    private static Vector3[] voxelCorners = new Vector3[8];

    /// <summary>
    /// 体素的十二条边
    /// </summary>
    private static UnityEngine.Vector3[] voxelEdgeCenters = new Vector3[12];

    /// <summary>
    /// 大于0的最小值，用于避免除零错误和边界检查
    /// </summary>
    private static readonly float smallEpsilon = 0.0001f;

    /// <summary>
    /// 体素位于模型内部检查的射线终点
    /// </summary>
    private static UnityEngine.Vector3[] _rayEndPoint = new Vector3[]{
        Vector3.up,
        Vector3.down,
        Vector3.left,
        Vector3.right,
        Vector3.forward
    };
}