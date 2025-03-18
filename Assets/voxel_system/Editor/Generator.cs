// VoxelGenerator.cs
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.SceneManagement;

namespace VoxelGenerator
{
    public partial class VoxelGenerator : EditorWindow
    {
        [MenuItem("Tools/voxel_generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<VoxelGenerator>("Voxel Generator");
            window.titleContent = new GUIContent("Voxel Generator");
            window.minSize = new Vector2(300, 200);
            window.Show();
        }

        /// <summary>
        /// This function is called when the object becomes enabled and active.
        /// </summary>
        private void OnEnable()
        {
            _rootNode = null;
            _voxelFileSaveDir = $"{Application.dataPath}/VoxelData";
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            _sceneBoundsCenter = Vector3.zero;
            _sceneBoundsSize   = Vector3.zero;
            _rootNode          = null;
            
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            GUILayout.Label("Voxel Generation Settings", EditorStyles.boldLabel);
            
            _voxelSize        = EditorGUILayout.FloatField("Voxel Size", _voxelSize);
            _maxDepth         = EditorGUILayout.IntField("Max Depth", _maxDepth);
            _voxelFileSaveDir = EditorGUILayout.TextField("Save Directory", _voxelFileSaveDir);
            _showDebugGizmos  = EditorGUILayout.Toggle("Show Debug Gizmos", _showDebugGizmos);
            EditorGUILayout.Space();

            if (GUILayout.Button("generate voxels"))
                GenerateVoxels();

            if(GUILayout.Button("clear scene bounds"))
            {
                _sceneBoundsCenter = Vector3.zero;
                _sceneBoundsSize = Vector3.zero;
            }

            if(GUILayout.Button("print all nodes"))
                PrintAllNodes();

            if(GUILayout.Button("draw nodes") && _rootNode != null)
                SceneView.RepaintAll();
        }

        /// <summary>
        /// 生成场景体素
        /// </summary>
        private void GenerateVoxels()
        {
            EditorUtility.DisplayProgressBar("Generating Voxels", "Calculating scene bounds...", 0f);
            
            try
            {
                // 计算场景边界
                var sceneBounds = CalculateSceneBounds();
                _sceneBoundsCenter = sceneBounds.center;
                _sceneBoundsSize = sceneBounds.size;

                // 创建根节点
                _rootNode = new OctreeNode(sceneBounds);
                
                // 获取场景中的所有物体
                GameObject[] sceneObjects = GameObject.FindObjectsOfType<GameObject>();
                int totalObjects = sceneObjects.Length;
                int processedObjects = 0;

                foreach (GameObject obj in sceneObjects)
                {
                    if (obj.GetComponent<MeshFilter>() != null)
                    {
                        float progress = (float)processedObjects / totalObjects;
                        EditorUtility.DisplayProgressBar("generating voxel...",$"processing object: {obj.name}", progress);
                        ProcessGameObject(obj, _rootNode, 0);
                    }
                    processedObjects++;
                }
                
                EditorUtility.DisplayProgressBar("Generating Voxels", "Saving voxel data...", 0.9f);
                SaveVoxelData();
                // SceneView.RepaintAll();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private Bounds CalculateSceneBounds()
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool firstBound = true;

            foreach (GameObject obj in GameObject.FindObjectsOfType<GameObject>())
            {
                MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    Debug.Log($"<color=white>get mesh filter,game object name: {obj.name}</color>");
                    Bounds meshBounds = meshFilter.sharedMesh.bounds;
                    meshBounds.center = obj.transform.TransformPoint(meshBounds.center);
                    meshBounds.size = Vector3.Scale(meshBounds.size, obj.transform.lossyScale);

                    if (firstBound)
                    {
                        bounds = meshBounds;
                        firstBound = false;
                    }
                    else
                    {
                        bounds.Encapsulate(meshBounds);
                    }
                }
            }

            // 确保边界是立方体且能被体素大小整除
            float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            maxSize = Mathf.Ceil(maxSize / _voxelSize) * _voxelSize;
            bounds.size = new Vector3(maxSize, maxSize, maxSize);

            return bounds;
        }

        private void ProcessGameObject(GameObject obj, OctreeNode node, int depth)
        {
            if (depth >= _maxDepth)
                return;

            MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
            if (meshFilter == null)
                return;

            // 首先进行快速的包围盒检测
            if (!IsIntersecting(node.bounds, obj))
            {
                Debug.Log($"<color=white>object: {obj.name} not intersecting with node</color>");
                return;    
            }

            // 如果是叶子节点且深度未达到最大值，进行更详细的检查
            if (node.isLeaf)
            {
                //这里有一个问题，当depth=maxDepth - 1，假如当前体素被标记位非Empty状态，且其子节点也为非Empty状态时
                //因为当前节点已经是叶子节点，所以不会再进行分割，导致无法继续检测真正的叶子节点
                //使用详细的相交检测
                VoxelData.VoxelState state = VoxelIntersectionHelper.CheckIntersection(node.bounds, obj,depth == _maxDepth - 1);
                node.data.state = state;
                node.Split();
                if(state != VoxelData.VoxelState.Empty && depth == _maxDepth - 1)
                {
                    foreach(var child in node.children)
                    {
                        var childState = VoxelIntersectionHelper.CheckIntersection(child.bounds,obj,true);
                        child.data.state = childState;
                        return;
                    }
                }
                else
                {
                    for (int i = 0; i < 8; i++)
                        ProcessGameObject(obj, node.children[i], depth + 1);
                }
                // 如果检测到任何占用状态，并且未达到最大深度，则进行分割
                // if (state != VoxelData.VoxelState.Empty && depth < _maxDepth)
                // {
                //     node.Split();
                //     for (int i = 0; i < 8; i++)
                //         ProcessGameObject(obj, node.children[i], depth + 1);
                // }
                // //最大深度则标记八叉树子节点状态
                // else
                // {
                //     Debug.Log("set node state,state: " + state);
                //     // 更新节点状态
                //     node.data.state = state;
                // }
                // return;
            }

            //如果不是叶子节点，递归处理子节点
            //这里是为了处理，一个树节点下可能包含多个mesh的情况
            for (int i = 0; i < 8; i++)
                ProcessGameObject(obj, node.children[i], depth + 1);

            // 优化：合并具有相同状态的子节点
            //OptimizeNode(node);
        }

        private void OptimizeNode(OctreeNode node)
        {
            if (node.isLeaf || node.children == null) 
                return;

            VoxelData.VoxelState firstState = node.children[0].data.state;
            bool allSame = true;

            // 检查所有子节点是否具有相同状态
            for (int i = 1; i < 8; i++)
            {
                if (node.children[i].data.state != firstState)
                {
                    allSame = false;
                    break;
                }
            }

            // 如果所有子节点状态相同，合并它们 
            if (allSame)
            {
                node.data.state = firstState;
                node.children = null;
                node.isLeaf = true;
            }
        }

        private bool IsIntersecting(Bounds bounds, GameObject obj)
        {
            var meshFilter        = obj.GetComponent<MeshFilter>();
            var meshBounds        = meshFilter.sharedMesh.bounds;
                meshBounds.center = obj.transform.TransformPoint(meshBounds.center);
                meshBounds.size   = Vector3.Scale(meshBounds.size, obj.transform.lossyScale);

            return bounds.Intersects(meshBounds);
        }

        /// <summary>
        /// 保存当前场景的体素数据
        /// </summary>
        private void SaveVoxelData()
        {
            if (!Directory.Exists(_voxelFileSaveDir))
                Directory.CreateDirectory(_voxelFileSaveDir);

            var currSceneName = SceneManager.GetActiveScene().name;
            string filePath = Path.Combine(_voxelFileSaveDir, $"scene_{currSceneName}.bytes");
            if(File.Exists(filePath))
                File.Delete(filePath);
                
            using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                // 写入头部信息
                writer.Write(_voxelSize);
                writer.Write(_rootNode.bounds.center.x);
                writer.Write(_rootNode.bounds.center.y);
                writer.Write(_rootNode.bounds.center.z);
                writer.Write(_rootNode.bounds.size.x);

                // 序列化八叉树
                SerializeNode(_rootNode, writer);
            }

            AssetDatabase.Refresh();
            Debug.Log($"Voxel data saved to: {filePath}");
        }

        private void SerializeNode(OctreeNode node, BinaryWriter writer)
        {
            // 写入节点类型和数据
            writer.Write(node.isLeaf);
            writer.Write((byte)node.data.state);

            if (!node.isLeaf && node.children != null)
            {
                for (int i = 0; i < 8; i++)
                    SerializeNode(node.children[i], writer);
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if( _sceneBoundsCenter != Vector3.zero && _sceneBoundsSize != Vector3.zero)
                DrawSceneBounds(_sceneBoundsCenter,_sceneBoundsSize);

            if (!_showDebugGizmos || _rootNode == null)
                return;

            DrawOctreeNode(_rootNode);
        }

        private void DrawSceneBounds(Vector3 center,Vector3 size)
        {
            Handles.color = new Color(0.1f, 0.6f, 1.0f, 1.0f);
            Handles.DrawWireCube(center, size);
            Handles.Label(center + new Vector3(0, size.y/2 + 1, 0), "Scene Bounds", EditorStyles.boldLabel);
        }

        private void DrawOctreeNode(OctreeNode node)
        {
            if (!node.isLeaf)
            {
                //非叶子节点不绘制
                // Handles.color = Color.white;
                // Handles.DrawWireCube(node.bounds.center, node.bounds.size);

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
                    
                    default:
                        Handles.color = Color.white;
                        break;
                }
                Handles.DrawWireCube(node.bounds.center, node.bounds.size);
                
                // 绘制半透明立方体
                Vector3[] vertices = new Vector3[8];
                FillCubeVertices(node.bounds, vertices);
                DrawTransparentCube(vertices, Handles.color);
            }
            // else if(node.data.state == VoxelData.VoxelState.Empty)
            // {
            //     Handles.color = Color.white;
            //     Handles.DrawWireCube(node.bounds.center, node.bounds.size);
            // }
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
        
        private void PrintAllNodes()
        {
            if (_rootNode is null)
                return;
        
            int nodeCount         = 0;
            int solidNodes        = 0;
            int intersectingNodes = 0;
            int touchingNodes     = 0;
        
            Debug.Log($"<color=white>===== OCTREE STRUCTURE (Max Depth: {_maxDepth}) =====</color>");
            PrintNodeRecursive(_rootNode, 0, "", ref nodeCount, ref solidNodes, ref intersectingNodes, ref touchingNodes);
            
            Debug.Log($"<color=white>===== OCTREE STATISTICS =====</color>");
            Debug.Log($"<color=white>Total non-empty nodes: {nodeCount}</color>");
            Debug.Log($"<color=red>Solid nodes: {solidNodes}</color>");
            Debug.Log($"<color=yellow>Intersecting nodes: {intersectingNodes}</color>");
            Debug.Log($"<color=green>Touching nodes: {touchingNodes}</color>");
        }
        
        private void PrintNodeRecursive(OctreeNode node, int depth, string path, 
                                       ref int nodeCount, ref int solidNodes, 
                                       ref int intersectingNodes, ref int touchingNodes)
        {
            if (node is null)
                return;
        
            // 跳过空节点
            if (node.data.state == VoxelData.VoxelState.Empty)
                return;
        
            // 生成缩进
            var indent = "";
            for (int i = 0; i < depth; i++)
                indent += "  ";
        
            // 颜色代码
            string colorTag;
            switch (node.data.state)
            {
                case VoxelData.VoxelState.Solid:
                    colorTag = "red";
                    solidNodes++;
                    break;

                case VoxelData.VoxelState.Intersecting:
                    colorTag = "yellow";
                    intersectingNodes++;
                    break;

                case VoxelData.VoxelState.Touching:
                    colorTag = "green";
                    touchingNodes++;
                    break;

                default:
                    colorTag = "white";
                    break;
            }
        
            nodeCount++;
        
            // 打印节点信息
            Debug.Log($"{indent}<color={colorTag}>Node{(string.IsNullOrEmpty(path) ? "" : " " + path)} " +
                      $"[{node.bounds.center.x:F2}, {node.bounds.center.y:F2}, {node.bounds.center.z:F2}] " +
                      $"Size: {node.bounds.size.x:F2} " +
                      $"Status: {node.data.state} " +
                      $"{(node.isLeaf ? "(Leaf)" : "(Branch)")}</color>");
        
            // 递归处理子节点
            if (!node.isLeaf && node.children != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    string childPosition;
                    switch (i)
                    {
                        case 0: childPosition = "BLF"; 
                            break; // Bottom Left Front

                        case 1: childPosition = "BRF"; 
                            break; // Bottom Right Front

                        case 2: childPosition = "TRF"; 
                            break; // Top Right Front

                        case 3: childPosition = "TLF"; 
                            break; // Top Left Front

                        case 4: childPosition = "BLB"; 
                            break; // Bottom Left Back

                        case 5: childPosition = "BRB"; 
                            break; // Bottom Right Back

                        case 6: childPosition = "TRB"; 
                            break; // Top Right Back

                        case 7: childPosition = "TLB"; 
                            break; // Top Left Back

                        default: childPosition = i.ToString(); 
                            break;
                    }
                    
                    string newPath = string.IsNullOrEmpty(path) ? childPosition : path + ">" + childPosition;
                    PrintNodeRecursive(node.children[i], depth + 1, newPath, 
                                      ref nodeCount, ref solidNodes, ref intersectingNodes, ref touchingNodes);
                }
            }
        }

        private float _voxelSize = 1.0f;
        private int _maxDepth = 4;

        /// <summary>
        /// 要保存的体素文件目录
        /// </summary>
        private string _voxelFileSaveDir = string.Empty;

        /// <summary>
        /// 要保存的体素文件名
        /// </summary>
        // private string _voxelFileSaveFileName = "voxel_data.bytes";
        private OctreeNode _rootNode = null;
        private bool _showDebugGizmos = true;

        /// <summary>
        /// 相交体素的可视化颜色
        /// </summary>
        private Color INTERSECTING_VOX_COLOR = new Color(1, 1, 0, 0.5f);  // 黄色

        /// <summary>
        /// 相切体素的可视化颜色
        /// </summary>
        private Color TOUCHING_VOX_COLOR     = new Color(0, 1, 0, 0.5f);  // 绿色

        /// <summary>
        /// 实体体素的可视化颜色
        /// </summary>
        private Color SOLIDE_VOX_COLOR        = new Color(1, 0, 0, 0.5f);  // 红色

        private Vector3 _sceneBoundsCenter = Vector3.zero;
        private Vector3 _sceneBoundsSize = Vector3.zero;
    }   
}