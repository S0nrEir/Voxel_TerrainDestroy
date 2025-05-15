using System;
using System.Linq;
using Editor.SATIntersectionVoxelize.IDPool;
using UnityEngine;
using SATIntersectionVoxelize;

namespace TriRasterizationVoxelization.Editor
{
    public partial class RasterizationGeneratorEditor 
    {
        // /// <summary>
        // /// 全体素化 - 将整个高度场转换为体素表示
        // /// </summary>
        // /// <param name="heightField">高度场数据</param>
        // /// <returns>包含体素数据的三维数组</returns>
        // private VoxelData[,,] FullVoxelizeGrid(HeightField heightField)
        // {
        //     if (heightField == null || heightField.Span == null)
        //     {
        //         Debug.LogError("高度场数据无效");
        //         return null;
        //     }
        //     
        //     // 获取高度场的尺寸信息
        //     int width = heightField.Width;
        //     int height = heightField.Height;
        //     
        //     // 计算Y方向上的范围和体素数量
        //     float minY = heightField.Min.y;
        //     float maxY = heightField.Max.y;
        //     int yVoxelCount = Mathf.CeilToInt((maxY - minY) / heightField.VerticalCellSize);
        //     
        //     // 创建体素三维数组
        //     VoxelData[,,] voxels = new VoxelData[width, yVoxelCount, height];
        //     
        //     // 初始化所有体素为空状态
        //     for (int x = 0; x < width; x++)
        //         for (int y = 0; y < yVoxelCount; y++)
        //             for (int z = 0; z < height; z++)
        //                 voxels[x, y, z] = new VoxelData { state = VoxelData.VoxelState.Empty };
        //     
        //     // 遍历整个高度场填充体素
        //     for (int x = 0; x < width; x++)
        //     {
        //         for (int z = 0; z < height; z++)
        //         {
        //             // 获取当前位置的span链
        //             HeightFieldSpan span = heightField.Span[x, z];
        //             
        //             if (span != null)
        //             {
        //                 // 遍历所有span
        //                 while (span != null)
        //                 {
        //                     // 计算span覆盖的体素范围
        //                     uint yMin = span._smin;
        //                     uint yMax = span._smax;
        //                     
        //                     // 标记span覆盖的所有体素为实体
        //                     for (uint y = yMin; y <= yMax && y < yVoxelCount; y++)
        //                         voxels[x, y, z].state = VoxelData.VoxelState.Solid;
        //                     
        //                     span = span._pNext;
        //                 }
        //             }
        //             // 即使没有span，仍然保持其他体素为Empty状态
        //         }
        //     }
        //     
        //     Debug.Log($"完成全体素化，生成体素网格大小: {width}x{yVoxelCount}x{height}");
        //     return voxels;
        // }
        //
        // /// <summary>
        // /// 获取体素位置对应的世界坐标
        // /// </summary>
        // private Vector3 GetVoxelWorldPosition(int x, int y, int z, HeightField heightField)
        // {
        //     return new Vector3(
        //         heightField.Min.x + (x + 0.5f) * heightField.CellSize,
        //         heightField.Min.y + (y + 0.5f) * heightField.VerticalCellSize,
        //         heightField.Min.z + (z + 0.5f) * heightField.CellSize
        //     );
        // }
        //
        // /// <summary>
        // /// 获取世界坐标对应的体素索引
        // /// </summary>
        // private bool GetVoxelIndex(Vector3 worldPos, HeightField heightField, 
        //     out int x, out int y, out int z, out int yVoxelCount)
        // {
        //     x = Mathf.FloorToInt((worldPos.x - heightField.Min.x) / heightField.CellSize);
        //     y = Mathf.FloorToInt((worldPos.y - heightField.Min.y) / heightField.VerticalCellSize);
        //     z = Mathf.FloorToInt((worldPos.z - heightField.Min.z) / heightField.CellSize);
        //     
        //     yVoxelCount = Mathf.CeilToInt((heightField.Max.y - heightField.Min.y) / heightField.VerticalCellSize);
        //     
        //     return x >= 0 && x < heightField.Width && 
        //            y >= 0 && y < yVoxelCount && 
        //            z >= 0 && z < heightField.Height;
        // }
        //
        // // /// <summary>
        // // /// 将三维体素网格转换为八叉树
        // // /// </summary>
        // private OctreeNode ConvertVoxelGridToOctree(VoxelData[,,] voxels, HeightField heightField)
        // {
        //     if (voxels == null || heightField == null)
        //     {
        //         Debug.LogError("体素数据或高度场无效");
        //         return null;
        //     }
        //
        //     // 获取体素网格尺寸
        //     int width = voxels.GetLength(0);
        //     int yVoxelCount = voxels.GetLength(1);
        //     int height = voxels.GetLength(2);
        //
        //     // 创建包含整个体素网格的边界
        //     Bounds totalBounds = new Bounds
        //     {
        //         min = heightField.Min,
        //         max = new Vector3(
        //             heightField.Min.x + width * heightField.CellSize,
        //             heightField.Min.y + yVoxelCount * heightField.VerticalCellSize,
        //             heightField.Min.z + height * heightField.CellSize
        //         )
        //     };
        //
        //     // 创建根节点
        //     OctreeNode rootNode = new OctreeNode
        //     {
        //         bounds = totalBounds,
        //         isLeaf = false,
        //         ID = IDPool.Gen(),
        //         data = new VoxelData { state = VoxelData.VoxelState.Empty }
        //     };
        //
        //     // 递归构建八叉树
        //     BuildOctree(rootNode, voxels, heightField);
        //
        //     Debug.Log($"完成体素网格转换为八叉树");
        //     return rootNode;
        // }
        //
        // /// <summary>
        // /// 递归构建八叉树
        // /// </summary>
        // private void BuildOctree(OctreeNode node, VoxelData[,,] voxels, HeightField heightField)
        // {
        //     // 检查当前节点中的体素是否都是同一状态
        //     if (CheckNodeHomogeneity(node, voxels, heightField, out VoxelData.VoxelState state))
        //     {
        //         // 如果所有体素状态相同，则设为叶节点
        //         node.isLeaf = true;
        //         node.data = new VoxelData { state = state };
        //         return;
        //     }
        //
        //     // 如果节点内体素状态不同且节点尺寸大于单个体素，继续划分
        //     if (node.bounds.size.x > heightField.CellSize * 1.1f || 
        //         node.bounds.size.z > heightField.CellSize * 1.1f ||
        //         node.bounds.size.y > heightField.VerticalCellSize * 1.1f)
        //     {
        //         // 划分为8个子节点
        //         node.isLeaf = false;
        //         node.children = new OctreeNode[8];
        //         Vector3 center = node.bounds.center;
        //         Vector3 extents = node.bounds.extents * 0.5f;
        //
        //         for (int i = 0; i < 8; i++)
        //         {
        //             Vector3 childCenter = center + new Vector3(
        //                 ((i & 1) == 0) ? -extents.x : extents.x,
        //                 ((i & 2) == 0) ? -extents.y : extents.y,
        //                 ((i & 4) == 0) ? -extents.z : extents.z
        //             );
        //             
        //             Bounds childBounds = new Bounds(childCenter, extents * 2);
        //             node.children[i] = new OctreeNode
        //             {
        //                 bounds = childBounds,
        //                 isLeaf = true,
        //                 ID = IDPool.Gen(),
        //                 data = new VoxelData { state = VoxelData.VoxelState.Empty }
        //             };
        //             
        //             // 递归处理子节点
        //             BuildOctree(node.children[i], voxels, heightField);
        //         }
        //
        //         // 尝试优化树结构
        //         //OptimizeNode(node);
        //     }
        //     else
        //     {
        //         // 如果节点已经足够小，则设为叶节点
        //         node.isLeaf = true;
        //         node.data = new VoxelData { state = GetDominantState(node, voxels, heightField) };
        //     }
        // }
        //
        // /// <summary>
        // /// 检查节点中的体素是否都是同一状态
        // /// </summary>
        // private bool CheckNodeHomogeneity(OctreeNode node, VoxelData[,,] voxels, HeightField heightField, out VoxelData.VoxelState state)
        // {
        //     state = VoxelData.VoxelState.Empty;
        //     
        //     // 计算体素索引范围
        //     int x0, y0, z0, x1, y1, z1;
        //     GetVoxelIndexRange(node.bounds, heightField, out x0, out y0, out z0, out x1, out y1, out z1);
        //     
        //     if (x0 > x1 || y0 > y1 || z0 > z1)
        //         return true;
        //     
        //     // 检查第一个体素的状态
        //     state = voxels[x0, y0, z0].state;
        //     
        //     // 检查所有体素是否都是同一状态
        //     for (int x = x0; x <= x1; x++)
        //     {
        //         for (int y = y0; y <= y1; y++)
        //         {
        //             for (int z = z0; z <= z1; z++)
        //             {
        //                 if (voxels[x, y, z].state != state)
        //                     return false;
        //             }
        //         }
        //     }
        //     
        //     return true;
        // }
        //
        // /// <summary>
        // /// 获取节点内占主导的体素状态
        // /// </summary>
        // private VoxelData.VoxelState GetDominantState(OctreeNode node, VoxelData[,,] voxels, HeightField heightField)
        // {
        //     int x0, y0, z0, x1, y1, z1;
        //     GetVoxelIndexRange(node.bounds, heightField, out x0, out y0, out z0, out x1, out y1, out z1);
        //     
        //     int solidCount = 0;
        //     int totalCount = 0;
        //     
        //     for (int x = x0; x <= x1; x++)
        //     {
        //         for (int y = y0; y <= y1; y++)
        //         {
        //             for (int z = z0; z <= z1; z++)
        //             {
        //                 if (voxels[x, y, z].state == VoxelData.VoxelState.Solid)
        //                     solidCount++;
        //                 totalCount++;
        //             }
        //         }
        //     }
        //     
        //     return (solidCount > totalCount / 2) ? VoxelData.VoxelState.Solid : VoxelData.VoxelState.Empty;
        // }
        //
        // /// <summary>
        // /// 计算边界在体素网格中的索引范围
        // /// </summary>
        // private void GetVoxelIndexRange(Bounds bounds, HeightField heightField, 
        //     out int x0, out int y0, out int z0, out int x1, out int y1, out int z1)
        // {
        //     Vector3 min = bounds.min;
        //     Vector3 max = bounds.max;
        //     
        //     x0 = Mathf.FloorToInt((min.x - heightField.Min.x) / heightField.CellSize);
        //     y0 = Mathf.FloorToInt((min.y - heightField.Min.y) / heightField.VerticalCellSize);
        //     z0 = Mathf.FloorToInt((min.z - heightField.Min.z) / heightField.CellSize);
        //     
        //     x1 = Mathf.CeilToInt((max.x - heightField.Min.x) / heightField.CellSize) - 1;
        //     y1 = Mathf.CeilToInt((max.y - heightField.Min.y) / heightField.VerticalCellSize) - 1;
        //     z1 = Mathf.CeilToInt((max.z - heightField.Min.z) / heightField.CellSize) - 1;
        //     
        //     // 边界检查
        //     int width = heightField.Width;
        //     int height = heightField.Height;
        //     int yVoxelCount = Mathf.CeilToInt((heightField.Max.y - heightField.Min.y) / heightField.VerticalCellSize);
        //     
        //     x0 = Mathf.Clamp(x0, 0, width - 1);
        //     y0 = Mathf.Clamp(y0, 0, yVoxelCount - 1);
        //     z0 = Mathf.Clamp(z0, 0, height - 1);
        //     
        //     x1 = Mathf.Clamp(x1, 0, width - 1);
        //     y1 = Mathf.Clamp(y1, 0, yVoxelCount - 1);
        //     z1 = Mathf.Clamp(z1, 0, height - 1);
        // }
        //
        // /// <summary>
        // /// 优化八叉树节点，合并相同状态的子节点
        // /// </summary>
        // private void OptimizeNode(OctreeNode node)
        // {
        //     if (node.isLeaf || node.children == null)
        //         return;
        //
        //     // 检查所有子节点是否为叶节点且状态相同
        //     bool allLeaves = true;
        //     VoxelData.VoxelState firstState = node.children[0].data.state;
        //
        //     foreach (var child in node.children)
        //     {
        //         if (!child.isLeaf || child.data.state != firstState)
        //         {
        //             allLeaves = false;
        //             break;
        //         }
        //     }
        //
        //     // 如果所有子节点状态相同，合并为一个节点
        //     if (allLeaves)
        //     {
        //         node.isLeaf = true;
        //         node.data = new VoxelData { state = firstState };
        //         node.children = null;
        //     }
        // }
        
        private OctreeNode FullVoxelize(HeightField heightField)
        {
            if (heightField == null || heightField.Span == null)
                return null;
        
            // 创建体素数据类
            if (!TryGetVoxelDataType(out Type voxelDataType))
            {
                Debug.LogError("无法获取VoxelData类型");
                return null;
            }
            
            // Bounds sceneBounds = new Bounds(
            //     (heightField.Min + heightField.Max) * 0.5f,
            //     heightField.Max - heightField.Min
            // );
            
            Bounds sceneBounds = new Bounds
            {
                min = heightField.Min,
                max = heightField.Max
            };
        
            // 创建根节点
            OctreeNode rootNode = CreateOctreeNode(sceneBounds);
            rootNode.ID = IDPool.Gen();
            
            // 设置初始深度和最大深度
            int maxDepth = 6; // 可调整最大细分深度
            
            // 开始递归构建八叉树
            BuildOctree(rootNode, heightField, 0, maxDepth);
            
            Debug.Log($"完成体素化，生成八叉树深度: {maxDepth}");
            return rootNode;
        }
        
        private bool TryGetVoxelDataType(out Type voxelDataType)
        {
            // 尝试获取VoxelData类型
            voxelDataType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == "VoxelData");
            
            return voxelDataType != null;
        }
        
        private OctreeNode CreateOctreeNode(Bounds bounds)
        {
            // 创建新的八叉树节点
            OctreeNode node = new OctreeNode
            {
                bounds = bounds,
                isLeaf = true,
                children = null,
                data = new VoxelData { state = VoxelData.VoxelState.Empty }
            };
            
            return node;
        }
        
        private void BuildOctree(OctreeNode node, HeightField heightField, int depth, int maxDepth)
        {
            // 检查当前节点是否与高度场中的任何span相交
            if (IsNodeSolid(node, heightField))
            {
                node.data.state = VoxelData.VoxelState.Solid;
                if (depth >= maxDepth)
                    return;
                
                node.Split();
                for (int i = 0; i < 8; i++)
                {
                    node.children[i].ID = IDPool.Gen();
                    BuildOctree(node.children[i], heightField, depth + 1, maxDepth);
                }
                
                //OptimizeNode(node);
            }
        }
        
        private bool IsNodeSolid(OctreeNode node, HeightField heightField)
        {
            Vector3 min = node.bounds.min;
            Vector3 max = node.bounds.max;
            
            int x0 = Mathf.FloorToInt((min.x - heightField.Min.x) / heightField.CellSize);
            int z0 = Mathf.FloorToInt((min.z - heightField.Min.z) / heightField.CellSize);
            int x1 = Mathf.CeilToInt((max.x - heightField.Min.x) / heightField.CellSize);
            int z1 = Mathf.CeilToInt((max.z - heightField.Min.z) / heightField.CellSize);
            
            x0 = Mathf.Clamp(x0, 0, heightField.Width - 1);
            z0 = Mathf.Clamp(z0, 0, heightField.Height - 1);
            x1 = Mathf.Clamp(x1, 0, heightField.Width - 1);
            z1 = Mathf.Clamp(z1, 0, heightField.Height - 1);
            
            for (int x = x0; x < x1; x++)
            {
                for (int z = z0; z < z1; z++)
                {
                    HeightFieldSpan span = heightField.Span[x, z];
                    while (span != null)
                    {
                        float spanMinY = heightField.Min.y + span._smin * heightField.VerticalCellSize;
                        float spanMaxY = heightField.Min.y + span._smax * heightField.VerticalCellSize;
                        
                        if (min.y <= spanMaxY && max.y >= spanMinY)
                            return true;
                        
                        span = span._pNext;
                    }
                }
            }
            
            return false;
        }
        
        private void SubdivideNode(OctreeNode node)
        {
            // 创建8个子节点
            node.isLeaf = false;
            node.children = new OctreeNode[8];
            Vector3 center = node.bounds.center;
            Vector3 extents = node.bounds.extents * 0.5f;
            
            // 创建8个子节点，按八叉树标准顺序
            for (int i = 0; i < 8; i++)
            {
                Vector3 childCenter = center;
                childCenter.x += ((i & 1) == 0) ? -extents.x : extents.x;
                childCenter.y += ((i & 2) == 0) ? -extents.y : extents.y;
                childCenter.z += ((i & 4) == 0) ? -extents.z : extents.z;
                
                Bounds childBounds = new Bounds(childCenter, extents * 2);
                node.children[i] = CreateOctreeNode(childBounds);
            }
        }
        
        private void OptimizeNode(OctreeNode node)
        {
            if (node.isLeaf || node.children == null)
                return;
            
            // 检查所有子节点是否为叶节点且状态相同
            bool allLeaves = true;
            VoxelData.VoxelState firstState = node.children[0].data.state;
            
            foreach (var child in node.children)
            {
                if (!child.isLeaf || child.data.state != firstState)
                {
                    allLeaves = false;
                    break;
                }
            }
            
            // 如果所有子节点状态相同，合并为一个节点
            if (allLeaves)
            {
                node.isLeaf = true;
                node.data.state = firstState;
                node.children = null;
            }
        }
 
    }
}