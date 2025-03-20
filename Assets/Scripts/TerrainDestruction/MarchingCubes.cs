using System.Collections.Generic;
using UnityEngine;
using TerrainDesctruction;
using Voxel;

public static class MarchingCubes
{
    // 立方体的8个顶点相对于立方体原点(最小点)的偏移
    private static readonly Vector3Int[] _vertexOffsets = new Vector3Int[8]
    {
        new Vector3Int(0, 0, 0), // 0: 左下后
        new Vector3Int(1, 0, 0), // 1: 右下后
        new Vector3Int(1, 0, 1), // 2: 右下前
        new Vector3Int(0, 0, 1), // 3: 左下前
        new Vector3Int(0, 1, 0), // 4: 左上后
        new Vector3Int(1, 1, 0), // 5: 右上后
        new Vector3Int(1, 1, 1), // 6: 右上前
        new Vector3Int(0, 1, 1)  // 7: 左上前
    };

    // 立方体的12条边的顶点索引对
    private static readonly int[,] _edgeVertexIndices = new int[12, 2]
    {
        {0, 1}, {1, 2}, {2, 3}, {3, 0}, // 底部四边
        {4, 5}, {5, 6}, {6, 7}, {7, 4}, // 顶部四边
        {0, 4}, {1, 5}, {2, 6}, {3, 7}  // 连接顶部和底部的边
    };

    // 根据立方体配置获取三角形列表
    public static int GetTrianglesForConfiguration(int configuration, int[] triangles)
    {
        int tableIndex = configuration * 16;
        int triangleCount = 0;
        
        for (int i = 0; i < 16; i += 3)
        {
            if (Constant.TriangleSearchingTable.Table[tableIndex + i] == -1)
                break;
                
            triangles[triangleCount * 3]     = Constant.TriangleSearchingTable.Table[tableIndex + i];
            triangles[triangleCount * 3 + 1] = Constant.TriangleSearchingTable.Table[tableIndex + i + 1];
            triangles[triangleCount * 3 + 2] = Constant.TriangleSearchingTable.Table[tableIndex + i + 2];
            triangleCount++;
        }
        
        return triangleCount;
    }
    
    // 将包围盒立方体作为输入（破坏范围），输出平滑后的，破坏后的网格（？）
    /// <summary>
    /// Marching Cube网格生成算法
    /// <param>将体素数据转化为平滑的网格</param>
    /// </summary>
    public static void GenerateMesh(
        VoxelSystem voxelSystem, 
        Bounds bounds, 
        float voxelSize, 
        out List<Vector3> vertices, 
        out List<int> triangles)
    {
        vertices = new List<Vector3>();
        triangles = new List<int>();
        
        // 确定采样范围
        //将包围盒转化为体素坐标，将目标包围盒按照voxelSize大小分割成为立方体集合
        Vector3Int min = new Vector3Int(
            Mathf.FloorToInt(bounds.min.x / voxelSize),
            Mathf.FloorToInt(bounds.min.y / voxelSize),
            Mathf.FloorToInt(bounds.min.z / voxelSize)
        );
        
        Vector3Int max = new Vector3Int(
            Mathf.CeilToInt(bounds.max.x / voxelSize),
            Mathf.CeilToInt(bounds.max.y / voxelSize),
            Mathf.CeilToInt(bounds.max.z / voxelSize)
        );
        
        //立方体的8个顶点值
        float[] cubeValues = new float[8];
        ///立方体12个边上生成的顶点索引
        int[] edgeVertexIndices = new int[12];
        //边索引
        int[] triangleIndices = new int[15];
        
        // 创建顶点查找字典，避免重复顶点
        Dictionary<Vector3, int> vertexLookup = new Dictionary<Vector3, int>();

        // 遍历所有体素
        // 将要破坏的范围立方体切割为若干数量的小立方体集合，并检查每个小立方体八个顶点的体素碰撞状态
        for (int x = min.x; x < max.x; x++)
        {
            for (int y = min.y; y < max.y; y++)
            {
                for (int z = min.z; z < max.z; z++)
                {
                    // 获取立方体8个顶点的值 (0=空，1=实体)
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 samplePos = new Vector3(
                            (x + _vertexOffsets[i].x) * voxelSize,
                            (y + _vertexOffsets[i].y) * voxelSize,
                            (z + _vertexOffsets[i].z) * voxelSize
                        );
                        
                        //检查立方体8个顶点对应体素的碰撞状态
                        cubeValues[i] = voxelSystem.IsPositionOccupied(samplePos) == VoxelData.VoxelState.Solid ? 1.0f : 0.0f;
                    }
                    
                    // 计算立方体配置 (0-255)
                    int cubeIndex = 0;
                    //将八个顶点的体素碰撞状态按位标记存入cubeIndex
                    for (int i = 0; i < 8; i++)
                    {                        
                        if (cubeValues[i] >= 0.5f)
                            cubeIndex |= 1 << i;
                    }
                    
                    //如果小立方体内部或外部没有交点，则跳过
                    if (cubeIndex == 0 || cubeIndex == 255)
                        continue;
                    
                    // 计算与等值面相交的边
                    for (int i = 0; i < 12; i++)
                    {
                        int edgeIndex = (1 << i) & (1 << 12);
                        if (edgeIndex == 0)
                            continue;
                            
                        int v0 = _edgeVertexIndices[i, 0];
                        int v1 = _edgeVertexIndices[i, 1];
                        
                        // 计算边上的插值点 (使用线性插值)
                        float t = (0.5f - cubeValues[v0]) / (cubeValues[v1] - cubeValues[v0]);
                        Vector3 vertexPosition = new Vector3(
                            (x + _vertexOffsets[v0].x + t * (_vertexOffsets[v1].x - _vertexOffsets[v0].x)) * voxelSize,
                            (y + _vertexOffsets[v0].y + t * (_vertexOffsets[v1].y - _vertexOffsets[v0].y)) * voxelSize,
                            (z + _vertexOffsets[v0].z + t * (_vertexOffsets[v1].z - _vertexOffsets[v0].z)) * voxelSize
                        );
                        
                        // 查找或添加顶点
                        int vertexIndex;
                        if (!vertexLookup.TryGetValue(vertexPosition, out vertexIndex))
                        {
                            vertexIndex = vertices.Count;
                            vertexLookup.Add(vertexPosition, vertexIndex);
                            vertices.Add(vertexPosition);
                        }
                        
                        edgeVertexIndices[i] = vertexIndex;
                    }
                    
                    // 获取三角形
                    int numTriangles = GetTrianglesForConfiguration(cubeIndex, triangleIndices);
                    
                    // 添加三角形
                    for (int i = 0; i < numTriangles * 3; i += 3)
                    {
                        triangles.Add(edgeVertexIndices[triangleIndices[i]]);
                        triangles.Add(edgeVertexIndices[triangleIndices[i + 1]]);
                        triangles.Add(edgeVertexIndices[triangleIndices[i + 2]]);
                    }
                }//end for

            }//end for

        }//end for

    }
}