using UnityEngine;
using System.IO;

public class VoxelSystem : MonoBehaviour
{
    void Awake()
    {
        LoadVoxelData();
        meshGenerator = GetComponent<MeshGenerator>();
        if (meshGenerator == null)
        {
            meshGenerator = gameObject.AddComponent<MeshGenerator>();
            meshGenerator.voxelSystem = this;
        }
    }
    
    #region Terrain Modification
    
    /// <summary>
    /// 破坏地形
    /// </summary>
    public void DestroyTerrain(Vector3 position, float radius)
    {
        if (rootNode == null)
            return;
        
        //修改体素数据，然后重新生成相关的网格
        ModifyTerrain(rootNode, position, radius, VoxelData.VoxelState.Empty);
        if (meshGenerator != null)
            meshGenerator.RegenerateMeshInRadius(position, radius);
    }
    
    /// <summary>
    /// 修改地形
    /// </summary>
    private void ModifyTerrain(OctreeNode node, Vector3 position, float radius, VoxelData.VoxelState newState)
    {
        // 修改体素状态
        // 判断节点与球体是否相交
        if (!BoundsIntersectsSphere(node.bounds, position, radius))
            return;
        
        if (node.isLeaf)
        {
            float distanceToCenter = Vector3.Distance(node.bounds.center, position);
            if (distanceToCenter <= radius + node.bounds.extents.magnitude)
            {
                node.data.state = newState;
                return;
            }
        }
        
        // 非叶子节点则递归直到找到相应的叶子节点e
        if (!node.isLeaf)
        {
            //#todo 优化：直接使用位标记确定要检查的子节点的象限，不要递归
            for (int i = 0; i < 8; i++)
                ModifyTerrain(node.children[i], position, radius, newState);
            
            // 检查所有子节点状态，如果一致则合并
            TryMergeChildren(node);
        }
        else if (node.bounds.size.x > voxelSize * 2)
        {
            // 细分当前节点以获得更精确的破坏效果
            node.Split();
            for (int i = 0; i < 8; i++)
            {
                node.children[i].data.state = node.data.state;
                ModifyTerrain(node.children[i], position, radius, newState);
            }
        }
    }
    
    /// <summary>
    /// 检查包围盒与球体是否相交
    /// </summary>
    private bool BoundsIntersectsSphere(Bounds bounds, Vector3 sphereCenter, float radius)
    {
        // 计算包围盒到球心的最近点
        Vector3 closest = bounds.ClosestPoint(sphereCenter);
        float distanceSquared = (closest - sphereCenter).sqrMagnitude;
        return distanceSquared <= radius * radius;
    }
    
    /// <summary>
    /// 尝试合并子节点
    /// </summary>
    private void TryMergeChildren(OctreeNode node)
    {
        if (node.isLeaf || node.children == null)
            return;
        
        VoxelData.VoxelState firstState = node.children[0].data.state;
        bool allSame = true;
        bool allLeaves = true;
        
        for (int i = 0; i < 8; i++)
        {
            if (!node.children[i].isLeaf || node.children[i].data.state != firstState)
            {
                allSame = false;
                break;
            }
        }
        
        if (allSame && allLeaves)
        {
            node.data.state = firstState;
            node.isLeaf = true;
            node.children = null;
        }
    }

    public VoxelData.VoxelState IsPositionOccupied(Vector3 worldPosition)
    {
        return QueryNodeState(rootNode, worldPosition);
    }

    private VoxelData.VoxelState QueryNodeState(OctreeNode node, Vector3 position)
    {
        if (!node.bounds.Contains(position))
            return VoxelData.VoxelState.Empty;

        var currNode = node;
        while(!node.isLeaf)
        {
            var center = currNode.bounds.center;
            var childIndex = 0;
            //二进制第一位不包含1，表示x在中心点左边，反过来，如果在右边，则给索引的二进制第一位添加1
            if(position.x >= center.x)
                childIndex |= 1;
                
            if(position.y >= center.y)
                childIndex |= 2;

            if(position.z >= center.z)
                childIndex |= 4;

            currNode = currNode.children[childIndex];
        }
        
        if(currNode is null)
            return VoxelData.VoxelState.Empty;
            
        return currNode.data.state;

        // //优化，直接确定八叉树子节点，不再遍历
        // var center = node.bounds.center;
        // var childIndex = 0;
        // //确定包围盒节点在父包围盒中的3D空间象限位置
        // //在生成体素的时候，
        // if(position.x >= center.x)
        //     childIndex |= 1;
            
        // if(position.y >= center.y)
        //     childIndex |= 2;

        // if(position.z >= center.z)
        //     childIndex |= 4;

        // return QueryPosition(node.children[childIndex], position);
        // foreach (OctreeNode child in node.children)
        // {
        //     if (child.bounds.Contains(position))
        //         return QueryPosition(child, position);
        // }
    }
    
    #endregion

    #region Load/Desrialize Voxel Data
    
    private void LoadVoxelData()
    {
        TextAsset dataFile = Resources.Load<TextAsset>(dataPath);
        if (dataFile == null)
        {
            Debug.LogError("Failed to load voxel data!");
            return;
        }

        using (MemoryStream ms = new MemoryStream(dataFile.bytes))
        {
            using (BinaryReader reader = new BinaryReader(ms))
            {
                // 读取头部信息
                voxelSize = reader.ReadSingle();
                Vector3 center = new Vector3(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );
                float size = reader.ReadSingle();

                // 创建根节点
                rootNode = new OctreeNode(new Bounds(center, new Vector3(size, size, size)));
                
                // 反序列化八叉树
                DeserializeNode(rootNode, reader);
            }
        }
    }

    private void DeserializeNode(OctreeNode node, BinaryReader reader)
    {
        node.isLeaf = reader.ReadBoolean();
        node.data.state = (VoxelData.VoxelState)reader.ReadByte();

        if (!node.isLeaf)
        {
            node.Split();
            for (int i = 0; i < 8; i++)
                DeserializeNode(node.children[i], reader);
        }
    }
    
    #endregion

    #region Debug/Visualization
    
    // 用于可视化调试的方法
    private void OnDrawGizmos()
    {
        if (rootNode != null)
        {
            DrawOctreeNode(rootNode);
        }
    }

    private void DrawOctreeNode(OctreeNode node)
    {
        if (node.isLeaf && node.data.state == VoxelData.VoxelState.Solid)
        {
            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawCube(node.bounds.center, node.bounds.size);
        }

        if (!node.isLeaf)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
            foreach (OctreeNode child in node.children)
            {
                DrawOctreeNode(child);
            }
        }
    }
    
    #endregion
    
    private OctreeNode rootNode;
    private float voxelSize;
    private string dataPath = "VoxelData/voxel_data";
    private MeshGenerator meshGenerator;
    public OctreeNode RootNode => rootNode;
    public float VoxelSize => voxelSize;
    [SerializeField] private float destroyRadius = 2.0f;
}