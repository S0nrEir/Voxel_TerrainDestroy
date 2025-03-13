using UnityEngine;
using System.IO;

public class VoxelSystem : MonoBehaviour
{
    private OctreeNode rootNode;
    private float voxelSize;
    private string dataPath = "VoxelData/voxel_data";

    void Awake()
    {
        LoadVoxelData();
    }

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
}