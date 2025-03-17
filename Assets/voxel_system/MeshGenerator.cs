using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

[RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
public class MeshGenerator : MonoBehaviour
{
    public VoxelSystem voxelSystem;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    
    private void Start()
    {
        voxelSystem  = GetComponent<VoxelSystem>();
        meshFilter   = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
        
        // 初始化时生成整个网格
        GenerateEntireMesh();
    }
    
    /// <summary>
    /// 生成整个地形网格
    /// </summary>
    public void GenerateEntireMesh()
    {
        if (voxelSystem == null || voxelSystem.RootNode == null)
        {
            Debug.LogError("体素系统未初始化");
            return;
        }
        
        Bounds bounds = voxelSystem.RootNode.bounds;
        RegenerateMesh(bounds);
    }
    
    /// <summary>
    /// 重新生成网格
    /// </summary>
    /// <param name="position">破坏位置</param>
    /// <param name="radius">范围半径</param>
    public void RegenerateMeshInRadius(Vector3 position, float radius)
    {
        Bounds affectedBounds = new Bounds(position, Vector3.one * radius * 2);
        RegenerateMesh(affectedBounds);
    }
    
    /// <summary>
    /// 根据指定的包围盒范围重新生成网格
    /// </summary>
    private void RegenerateMesh(Bounds bounds)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        
        // 使用Marching Cubes生成网格
        // 将破坏范围转化为包围盒立方体，然后查找周围的顶点
        MarchingCubes.GenerateMesh(voxelSystem, bounds, voxelSystem.VoxelSize, out vertices, out triangles);
        
        // 更新网格
        UpdateMesh(vertices, triangles);
    }
    
    // 异步版本的网格生成 (用于大型区域以避免卡顿)
    public async void RegenerateMeshAsync(Bounds bounds)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        
        await Task.Run(() => {
            MarchingCubes.GenerateMesh(voxelSystem, bounds, voxelSystem.VoxelSize, out vertices, out triangles);
        });
        
        UpdateMesh(vertices, triangles);
    }
    
    private void UpdateMesh(List<Vector3> vertices, List<int> triangles)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // 支持大型网格
        
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        meshFilter.sharedMesh = mesh;
        meshCollider.sharedMesh = mesh;
    }
}