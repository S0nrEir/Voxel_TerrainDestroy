using UnityEngine;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.Burst;

namespace SATIntersectionVoxelize
{
    public static partial class VoxelIntersectionHelper 
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        [BurstCompile(CompileSynchronously=true)]
        public static unsafe int SATIntersect(
            in float3 vert0,
            in float3 vert1,
            in float3 vert2,
            in float3 aabbCenter,
            in float3 aabbExtents)
        {
            float3 edge_0  = vert1 - vert0;
            float3 edge_1  = vert2 - vert1;
            float3 edge_2  = vert0 - vert2;
            float3 normal  = math.normalize( math.cross(edge_0, edge_1) );

            //三角平面测试，检查体素包围盒的中心点到三角形平面的距离是否大于体素包围盒在三角形平面法线上的投影半径
            //包围盒在法线方向上的投影半径
            // \
            //  \----------+---+
            //   \.........|   |
            //    \--------+---+
            //     \
            float boxProjectionRadius = aabbExtents.x * math.abs(normal.x) +
                                      aabbExtents.y * math.abs(normal.y) +
                                      aabbExtents.z * math.abs(normal.z);

            //三维空间平面一般式：n·p + d = 0
            //n为平面法向量，p为平面上的点，d为平面到原点的距离(沿法线方向)
            //因此 n·p = -d
            float d = math.dot(normal, vert0);
            //包围盒中心到三角形平面的距离，如果为0，说明中心点在三角形平面上
            //想象一下，三角形平面法线和原点到center的向量的点乘，然后移动center查看结果
            float boxCenterDistance = math.dot(normal, aabbCenter) - d;
            if (math.abs(boxCenterDistance) > boxProjectionRadius)
                return 0;

            //三角形的包围盒
            float minX = math.min(vert0.x, math.min(vert1.x, vert2.x));
            float maxX = math.max(vert0.x, math.max(vert1.x, vert2.x));

            float minY = math.min(vert0.y, math.min(vert1.y, vert2.y));
            float maxY = math.max(vert0.y, math.max(vert1.y, vert2.y));

            float minZ = math.min(vert0.z, math.min(vert1.z, vert2.z));
            float maxZ = math.max(vert0.z, math.max(vert1.z, vert2.z));

            // 检查三角形在三个轴上的投影是否与包围盒在三个轴上的投影重叠
            //center.x - extents.x = 包围盒在X轴上的最小值（左边界）
            //center.x + extents.x = 包围盒在X轴上的最大值（右边界）
            if (maxX < aabbCenter.x - aabbExtents.x || minX > aabbCenter.x + aabbExtents.x)
                return 0;

            if (maxY < aabbCenter.y - aabbExtents.y || minY > aabbCenter.y + aabbExtents.y)
                return 0;

            if (maxZ < aabbCenter.z - aabbExtents.z || minZ > aabbCenter.z + aabbExtents.z)
                return 0;

            //分离轴检测
            float3* triangleEdges = stackalloc float3[3];
            triangleEdges[0] = edge_0;
            triangleEdges[1] = edge_1;
            triangleEdges[2] = edge_2;
            
            float3* aabbAxes = stackalloc float3[3];
            aabbAxes[0] = new float3(1,0,0);
            aabbAxes[1] = new float3(0,1,0);
            aabbAxes[2] = new float3(0,0,1);

            // var triangleEdges = new float3[]{ edge_0, edge_1,edge_2};

            float3 axis = float3.zero;
            //检查所有可能的分离轴  
            for(var i = 0 ;i < 3 ; i++)
            {
                for(var j = 0 ; j < 3 ; j++)
                {
                    axis = math.cross(triangleEdges[i], aabbAxes[j]);
                    if ( math.length(axis) < 0.00001f )
                        continue;

                    float3 normalAxis = math.normalize( axis );
                    float2 triMinAndTriMax;
                    float2 boxMinAndMax;

                    // 计算包围盒和三角形在该轴上的投影，然后检查重叠
                    ProjectTriangle(in vert0, in vert1, in vert2, in normalAxis ,out triMinAndTriMax);
                    ProjectBox(in aabbCenter,in aabbExtents,in normalAxis ,out boxMinAndMax);

                    if ( triMinAndTriMax.y < boxMinAndMax.x || triMinAndTriMax.x > boxMinAndMax.y)
                        return 0;
                }
            }
            return 1;
        }

        /// <summary>
        /// 将三角形投影到指定轴上,x= triMin,y=triMax
        /// </summary>
        [BurstCompile(CompileSynchronously=true)]
        private static void ProjectTriangle(in float3 vert0, in float3 vert1, in float3 vert2, in float3 axis ,out float2 triMinAndTriMax)
        {
            float dot0 = math.dot(vert0, axis);
            float dot1 = math.dot(vert1, axis);
            float dot2 = math.dot(vert2, axis);
            triMinAndTriMax.x = Mathf.Min( dot0, Mathf.Min( dot1, dot2 ));
            triMinAndTriMax.y = Mathf.Max( dot0, Mathf.Max( dot1, dot2 ));
            // return new float2( Mathf.Min( dot0, Mathf.Min( dot1, dot2 ) ), Mathf.Max( dot0, Mathf.Max( dot1, dot2 ) ) );
        }

        /// <summary>
        /// 将AABB包围盒投影到指定轴上,x= boxMin,y=boxMax
        /// </summary>
        [BurstCompile(CompileSynchronously=true)]
        private static void ProjectBox(in float3 center,in float3 halfSize,in float3 axis,out float2 boxMinAndMax)
        {
            // 计算包围盒在该轴上的投影半径
            float radius = 
                halfSize.x * Mathf.Abs(Vector3.Dot(axis, Vector3.right)) +
                halfSize.y * Mathf.Abs(Vector3.Dot(axis, Vector3.up)) +
                halfSize.z * Mathf.Abs(Vector3.Dot(axis, Vector3.forward));
            
            // 计算中心点在轴上的投影
            float centerProj = Vector3.Dot(center, axis);
            boxMinAndMax.x = centerProj - radius;
            boxMinAndMax.y = centerProj + radius;
            // return new float2( centerProj - radius, centerProj + radius );
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

        //aabb包围盒的三个主轴方向
        //private static readonly Vector3[] _aabbAxes = new Vector3[]{ new float3(1,0,0), new float3(0,1,0), new float3(0,0,1) };
        // private static readonly Vector3[] _aabbAxes = new Vector3[]{ Vector3.right, Vector3.up, Vector3.forward };
    }
}