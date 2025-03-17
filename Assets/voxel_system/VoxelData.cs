// VoxelData.cs

using UnityEngine;

[System.Serializable]
public struct VoxelData
{
    public enum VoxelState : byte
    {
        Empty = 0,
        Solid = 1,
        Intersecting = 2,  // 与模型相交
        Touching = 3       // 与模型相切
    }

    public VoxelState state;
    public byte material;

    public static bool IsOccupied(VoxelState state)
    {
        return state == VoxelState.Solid || 
               state == VoxelState.Intersecting || 
               state == VoxelState.Touching;
    }
}

// OctreeNode.cs
public class OctreeNode
{
    public OctreeNode(Bounds bounds)
    {
        this.bounds = bounds;
        this.isLeaf = true;
        this.data.state = VoxelData.VoxelState.Empty;
        this.children = null;
    }

    //八叉树分割体素
    public void Split()
    {
        if (!isLeaf)
            return;
        
        children = new OctreeNode[8];
        Vector3 size = bounds.size * 0.5f;
        Vector3 center = bounds.center;
        
        for (int i = 0; i < 8; i++)
        {
            //确定包围盒节点在父包围盒中的3D空间象限位置
            Vector3 newCenter = center + new Vector3(
                ((i & 1) == 0 ? -size.x : size.x) * 0.5f,
                ((i & 2) == 0 ? -size.y : size.y) * 0.5f,
                ((i & 4) == 0 ? -size.z : size.z) * 0.5f
            );
            
            Bounds childBounds = new Bounds(newCenter, size);
            children[i] = new OctreeNode(childBounds);
        }
        
        isLeaf = false;
    }
    
    public VoxelData data;
    public OctreeNode[] children; // 8个子节点
    public Bounds bounds;
    public bool isLeaf;
}