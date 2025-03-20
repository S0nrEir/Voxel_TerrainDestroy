using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.SceneManagement;
using System.Text;

namespace Voxel
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

        private void OnEnable()
        {
            ClearVoxelData();
            _voxelFileSaveDir = $"{Application.dataPath}/VoxelData";
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            ClearVoxelData();
        }

        private void ClearVoxelData()
        {
            var voxelContainer = GameObject.Find("VoxelContainer");
            if(voxelContainer != null)
                DestroyImmediate(voxelContainer);
            _generateCompelete     = false;
            _sceneBoundsCenter     = Vector3.zero;
            _sceneBoundsSize       = Vector3.zero;
            _rootNode              = null;
            _voxelItem             = null;
            _leafCount             = 0;
            _notEmptyLeafCount     = 0;
            _generateVoxelInstance = false;
            SceneView.RepaintAll();

#if GEN_VOXEL_ID
            IDPool.Reset();
#endif      
        }

        private void OnGUI()
        {
            GUILayout.Label("Voxel Generation Settings", EditorStyles.boldLabel);
            
            _voxelSize             = EditorGUILayout.FloatField("Voxel Size", _voxelSize);
            _maxDepth              = EditorGUILayout.IntField("Max Depth", _maxDepth);
            _voxelFileSaveDir      = EditorGUILayout.TextField("Save Directory", _voxelFileSaveDir);
            _showDebugGizmos       = EditorGUILayout.Toggle("Show Debug Gizmos", _showDebugGizmos);
            _generateVoxelInstance = EditorGUILayout.Toggle("Generate Voxel Instance", _generateVoxelInstance);

            EditorGUILayout.Space();

            if (GUILayout.Button("generate voxels"))
                GenerateVoxels();

            if(GUILayout.Button("clear voxel data"))
            {
                ClearVoxelData();
                SceneView.RepaintAll();
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
            if(_voxelItem == null)
                _voxelItem = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/voxel_instance.prefab");

            try
            {
                _generateCompelete = false;
                // 计算场景边界
                var sceneBounds = CalculateSceneBounds();
                _sceneBoundsCenter = sceneBounds.center;
                _sceneBoundsSize   = sceneBounds.size;

                // 创建根节点
                _rootNode = new OctreeNode(sceneBounds);
                // 获取场景中的所有物体
                GameObject[] sceneObjects = GameObject.FindObjectsOfType<GameObject>();
                int totalObjects = sceneObjects.Length;
                int processedObjects = 0;

                var logHelper = new StringBuilder();
                MeshFilter mesh = null;
                foreach (GameObject obj in sceneObjects)
                {
                    mesh = obj.GetComponent<MeshFilter>();
                    if (mesh != null)
                    {
                        float progress = (float)processedObjects / totalObjects;
                        EditorUtility.DisplayProgressBar("generating voxel...",$"processing object: {obj.name}", progress);
                        ProcessGameObject(mesh, obj ,_rootNode, 0,logHelper);
                    }
                    processedObjects++;
                }
                
                File.WriteAllText(@"F:\voxel_log.txt",logHelper.ToString(),Encoding.UTF8);
                logHelper = null;

                EditorUtility.DisplayProgressBar("Generating Voxels", "saving voxel data...", 0.9f);
                SaveVoxelData();
                _generateCompelete = true;
                if(_generateVoxelInstance)
                    GenerateVoxelGameObjects();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
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
            if (node == null)
                return;
            
            // 处理叶子节点 - 只为叶子节点创建实例
            if (node.isLeaf)
            {
                // 更新进度条
                float progress = (float)processedNodes / totalNodes;
                EditorUtility.DisplayProgressBar("生成体素对象", $"创建体素 {processedNodes}/{totalNodes}", progress);
                processedNodes++;
                
                // 实例化预制体
                GameObject voxelObj = PrefabUtility.InstantiatePrefab(_voxelItem) as GameObject;
                if (voxelObj != null)
                {
                    voxelObj.transform.SetParent(parent);
                    voxelObj.transform.position   = node.bounds.center;
                    voxelObj.transform.localScale = node.bounds.size;
                    
                    // 根据体素状态设置材质和颜色
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
                        
                        // 调整透明度
                        Color color = material.color;
                        material.color = new Color(color.r, color.g, color.b, 0.5f);
                        
                        // 设置材质为透明模式
                        material.SetFloat("_Mode", 3); // 透明模式
                        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        material.SetInt("_ZWrite", 0);
                        material.DisableKeyword("_ALPHATEST_ON");
                        material.EnableKeyword("_ALPHABLEND_ON");
                        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        material.renderQueue = 3000;
                        
                        renderer.material = material;
                    }
                    
#if GEN_VOXEL_ID
                    // 设置对象名称
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

        private void ProcessGameObject(MeshFilter mesh,GameObject obj, OctreeNode node, int depth, StringBuilder logHelper = null)
        {
            if (depth >= _maxDepth)
                return;

#if GEN_VOXEL_ID
            node.ID = IDPool.Gen();                
#endif

            if (!IsIntersecting(node.bounds, mesh,obj))
                return;    

            // 处理叶子节点
            if (node.isLeaf)
            {
                VoxelData.VoxelState state = VoxelIntersectionHelper.IsIntersection(node.bounds, obj,mesh);
                node.data.state = state;
                
                if (state != VoxelData.VoxelState.Empty)
                {
                    node.Split();
                    
                    // 特殊处理最大深度前一层
                    if (depth == _maxDepth - 1)
                    {
                        for (var i = 0; i < node.children.Length; i++)
                        {
                            var childState = VoxelIntersectionHelper.IsIntersection(node.children[i].bounds, obj,mesh, node.children[i].ID);
                            node.children[i].data.state = childState;
                            _leafCount++;
                            if(state != VoxelData.VoxelState.Empty)
                                _notEmptyLeafCount++;

#if GEN_VOXEL_ID
                        node.children[i].ID = IDPool.Gen();
#endif

                        }
                        
#if GEN_VOXEL_ID
                        if (logHelper != null)
                        {
                            foreach (var child in node.children)
                                logHelper.AppendLine($"set leaf node,state : {child.data.state}, center : {child.bounds.center},id : {child.ID}");
                        }
#endif
                        OptimizeNode(node);
                        return;
                    }
                    
                    // 递归处理新创建的子节点
                    for (var i = 0; i < node.children.Length; i++)
                        ProcessGameObject(mesh,obj, node.children[i], depth + 1, logHelper);
                        
                    // 优化当前节点
                    OptimizeNode(node);
                }
            }
            // 处理非叶子节点
            else if (node.children != null)
            {
                // 递归处理子节点
                for (int i = 0; i < node.children.Length; i++)
                    ProcessGameObject(mesh,obj, node.children[i], depth + 1, logHelper);
                
                // 优化当前节点
                OptimizeNode(node);
            }
        }

        private void OptimizeNode(OctreeNode node)
        {
            return;
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

        /// <summary>
        /// 快速的包围盒检测
        /// </summary>
        private bool IsIntersecting(Bounds bounds, MeshFilter mesh,GameObject obj)
        {
            var meshBounds        = mesh.sharedMesh.bounds;
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

        /// <summary>
        /// 非空子节点体素数量
        /// </summary>
        private int _notEmptyLeafCount = 0;
        /// <summary>
        /// 子节点体素总数量
        /// </summary>
        private int _leafCount = 0;
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

        private Vector3 _sceneBoundsCenter = Vector3.zero;
        private Vector3 _sceneBoundsSize = Vector3.zero;
        private GameObject _voxelItem = null;
        private bool _generateVoxelInstance = false;
        private bool _generateCompelete = false;
    }   
}