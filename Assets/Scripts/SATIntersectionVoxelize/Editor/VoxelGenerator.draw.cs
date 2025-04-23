using UnityEditor;
using UnityEngine;
using SATIntersectionVoxelize;

namespace Editor.SATIntersectionVoxelize
{
    
    public partial class VoxelGenerator
    {
        private void OnSceneGUI(SceneView sceneView)
        {
            if(_generateCompelete)
                DrawSceneBounds(_sceneBoundsCenter,_sceneBoundsSize);

            // if (!_showDebugGizmos || _rootNode == null)
            //     return;

            // DrawOctreeNode(_rootNode);
        }
        
        private void FillCubeVertices(Bounds bounds, Vector3[] vertices)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            vertices[0] = new Vector3(min.x, min.y, min.z);
            vertices[1] = new Vector3(max.x, min.y, min.z);
            vertices[2] = new Vector3(max.x, max.y, min.z);
            vertices[3] = new Vector3(min.x, max.y, min.z);
            vertices[4] = new Vector3(min.x, min.y, max.z);
            vertices[5] = new Vector3(max.x, min.y, max.z);
            vertices[6] = new Vector3(max.x, max.y, max.z);
            vertices[7] = new Vector3(min.x, max.y, max.z);
        }

        private void DrawTransparentCube(Vector3[] vertices, Color color)
        {
            Color transparentColor = new Color(color.r, color.g, color.b, 0.2f);
            Handles.color = transparentColor;

            // 绘制六个面
            int[][] faces = new int[][]
            {
                new int[] { 0, 1, 2, 3 }, // 前
                new int[] { 5, 4, 7, 6 }, // 后
                new int[] { 4, 0, 3, 7 }, // 左
                new int[] { 1, 5, 6, 2 }, // 右
                new int[] { 3, 2, 6, 7 }, // 上
                new int[] { 4, 5, 1, 0 }  // 下
            };

            foreach (int[] face in faces)
            {
                Vector3[] faceVertices = new Vector3[4];
                for (int i = 0; i < 4; i++)
                    faceVertices[i] = vertices[face[i]];
                
                Handles.DrawSolidRectangleWithOutline(faceVertices, transparentColor, Color.clear);
            }
        }

        private void DrawOctreeNode(OctreeNode node)
        {
            if (!node.isLeaf)
            {
                if (node.children != null)
                {
                    //Debug.Log($"draw octree node,children count: {node.children.Length}");
                    foreach (OctreeNode child in node.children)
                        DrawOctreeNode(child);
                }
            }
            //绘制叶子节点，只处理部位empty的体素
            else if (node.data.state != VoxelData.VoxelState.Empty)
            {
                // 根据不同状态使用不同颜色
                switch (node.data.state)
                {
                    case VoxelData.VoxelState.Solid:
                        Handles.color = SOLIDE_VOX_COLOR;
                        break;
                    
                    case VoxelData.VoxelState.Intersecting:
                        Handles.color = INTERSECTING_VOX_COLOR;
                        break;
                    
                    case VoxelData.VoxelState.Touching:
                        Handles.color = TOUCHING_VOX_COLOR;
                        break;

                    case VoxelData.VoxelState.Empty:
                        Handles.color = Color.clear;
                        break;

                    default:
                        Handles.color = Color.clear;
                        break;
                }
                Handles.DrawWireCube(node.bounds.center, node.bounds.size);
                
                // 绘制半透明立方体
                Vector3[] vertices = new Vector3[8];
                FillCubeVertices(node.bounds, vertices);
                DrawTransparentCube(vertices, Handles.color);
            }
            else if(node.data.state == VoxelData.VoxelState.Empty)
            {
                Handles.color = Color.white;
                Handles.DrawWireCube(node.bounds.center, node.bounds.size);
            }
        }
        
        private void DrawSceneBounds(Vector3 center,Vector3 size)
        {
            Handles.color = new Color(0.1f, 0.6f, 1.0f, 1.0f);
            Handles.DrawWireCube(center, size);
            Handles.Label(center + new Vector3(0, size.y/2 + 1, 0), "Scene Bounds", EditorStyles.boldLabel);
        }

        /// <summary>
        /// 生成实际的体素游戏对象
        /// </summary>
        private void GenerateVoxelGameObjects()
        {
            if (_rootNode == null)
                return;
            
            GameObject existingContainer = GameObject.Find("VoxelContainer");
            if (existingContainer != null)
                DestroyImmediate(existingContainer);

            var voxelContainer = GameObject.Find("VoxelContainer");
            //clean exsiting childs
            if(voxelContainer != null)
            {
                while(voxelContainer.transform.childCount > 0)
                    DestroyImmediate(voxelContainer.transform.GetChild(0).gameObject);
            }
            voxelContainer = new GameObject("VoxelContainer");
            
            EditorUtility.DisplayProgressBar("generate voxel object", "generating voxel instance...", 0f);
            try
            {
                int processedNodes = 0;
                GenerateVoxelGameObjectsRecursive(_rootNode, voxelContainer.transform, ref processedNodes, _notEmptyLeafCount);

                if(_optimizeAfterGenerate)
                    OptimizeNode( _rootNode );
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Selection.activeGameObject = voxelContainer;
        }

        /// <summary>
        /// 创建体素游戏对象
        /// </summary>
        private void GenerateVoxelGameObjectsRecursive(OctreeNode node, Transform parent, ref int processedNodes, int totalNodes)
        {
            // if ( node == null )
            //     return;

            if (node == null || node.data.state == VoxelData.VoxelState.Empty)
               return;

            if (node.isLeaf)
            {
                float progress = (float)processedNodes / totalNodes;
                EditorUtility.DisplayProgressBar("生成体素对象", $"创建体素 {processedNodes}/{totalNodes}", progress);
                processedNodes++;
                
                GameObject voxelObj = PrefabUtility.InstantiatePrefab(_voxelItem) as GameObject;
                if (voxelObj != null)
                {
                    voxelObj.transform.SetParent(parent);
                    voxelObj.transform.position = node.bounds.center;
                    voxelObj.transform.localScale = node.bounds.size;
                    
                    MeshRenderer renderer = voxelObj.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        Material material = new Material(renderer.sharedMaterial);
                        switch (node.data.state)
                        {
                            case VoxelData.VoxelState.Solid:
                                material.color = SOLIDE_VOX_COLOR;
                                break;
                            
                            case VoxelData.VoxelState.Intersecting:
                                material.color = INTERSECTING_VOX_COLOR;
                                break;
                            
                            case VoxelData.VoxelState.Touching:
                                material.color = TOUCHING_VOX_COLOR;
                                break;

                            default:
                                material.color = Color.white;
                                break;
                        }
                        
                        // Color color = material.color;
                        // material.color = new Color(color.r, color.g, color.b, 0.5f);

                        //透明模式
                        // material.SetFloat("_Mode", 3);
                        // material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        // material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        // material.SetInt("_ZWrite", 0);
                        // material.DisableKeyword("_ALPHATEST_ON");
                        // material.EnableKeyword("_ALPHABLEND_ON");
                        // material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = 3000;
                        renderer.material = material;
                    }
                    
#if GEN_VOXEL_ID
                    voxelObj.name = $"Voxel_{node.data.state}_{node.ID}";
#endif
                }
            }
            // 处理非叶子节点 - 递归处理子节点
            else if (node.children != null)
            {
                foreach (OctreeNode child in node.children)
                    GenerateVoxelGameObjectsRecursive(child, parent, ref processedNodes, totalNodes);
            }
        }

        /// <summary>
        /// 相交体素的可视化颜色
        /// </summary>
        private Color INTERSECTING_VOX_COLOR = Color.yellow;  // 黄色

        /// <summary>
        /// 相切体素的可视化颜色
        /// </summary>
        private Color TOUCHING_VOX_COLOR     = Color.green;  // 绿色

        /// <summary>
        /// 实体体素的可视化颜色
        /// </summary>
        private Color SOLIDE_VOX_COLOR        = Color.red;  // 红色
    }
   
}
