using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEngine.SceneManagement;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Voxel;

namespace Editor.Voxel
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
            _meshCloneMap = new Dictionary<int, MeshCloneData>();
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
            _optimizeAfterGenerate = false;
            if(_meshCloneMap != null)
                _meshCloneMap.Clear();

            _meshCloneMap          = null;
            _intersectionDuration  = 0;
            _maxIntersectionCount = 0;
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
            _optimizeAfterGenerate = EditorGUILayout.Toggle("Optimize After Generate", _optimizeAfterGenerate);

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
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();
            CacheMeshData();
            VoxelIntersectionHelper._longgestDuration = 0;
            EditorUtility.DisplayProgressBar("Generating Voxels", "Calculating scene bounds...", 0f);
            if(_generateVoxelInstance)
                _voxelItem = AssetDatabase.LoadAssetAtPath<GameObject>( "Assets/Prefab/voxel_instance.prefab" );

            try
            {
                _generateCompelete = false;
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

                var logHelper = new StringBuilder();
                MeshFilter mesh = null;
                foreach (GameObject obj in sceneObjects)
                {
                    mesh = obj.GetComponent<MeshFilter>();
                    if (mesh != null)
                    {
                        float progress = (float)processedObjects / totalObjects;
                        EditorUtility.DisplayProgressBar("generating voxel...",$"processing object: {obj.name}", progress);
#if GEN_OPTIMIZE
                        ProcessGameObject_BF(mesh, obj ,_rootNode, 0,logHelper);
#else
                        ProcessGameObject_DF(mesh, obj, _rootNode, 0, logHelper);
#endif
                    }
                    processedObjects++;
                }
                
                File.WriteAllText(Path.Combine(Application.dataPath, "voxel_log.txt" ),logHelper.ToString(),Encoding.UTF8);
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
                watch.Stop();
                _meshCloneMap.Clear();
                Debug.Log($"Voxel generation completed in {watch.ElapsedMilliseconds / 1000} sec");

                if(_maxIntersectionCount != 0)
                    Debug.Log( $"avg voxel intersection duration:{( _intersectionDuration / _maxIntersectionCount ) / 1000} sec" );

                Debug.Log( $"max voxel intersection duration:{VoxelIntersectionHelper._longgestDuration} sec" );
                VoxelIntersectionHelper._longgestDuration = 0;
            }
        }

        /// <summary>
        /// 缓存场景中所有的网格数据
        /// </summary>
        private void CacheMeshData()
        {
            if ( _meshCloneMap is null )
                _meshCloneMap = new Dictionary<int, MeshCloneData>();

            var meshObjects = GameObject.FindObjectsOfType<MeshFilter>();
            foreach ( var meshObj in meshObjects )
            {
                var mesh = meshObj.sharedMesh;
                if ( mesh is null || _meshCloneMap.ContainsKey( mesh.GetInstanceID() ) )
                    continue;

                var cloneData = new MeshCloneData
                {
                    Bounds = mesh.bounds,
                    Vertices = ( Vector3[] ) mesh.vertices.Clone(),
                    Triangles = ( int[] ) mesh.triangles.Clone(),
                    Position = meshObj.transform.position,
                    Rotation = meshObj.transform.rotation,
                    Scale = meshObj.transform.lossyScale
                };
                _meshCloneMap.Add( meshObj.GetInstanceID(), cloneData );
            }
        }

        /// <summary>
        /// 计算场景包围盒
        /// </summary>
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

        /// <summary>
        /// 优化合并树
        /// </summary>
        private void OptimizeNode(OctreeNode node)
        {
            if (node.isLeaf || node.children == null || node.children.Length == 0) 
                return;

            bool allChildrenAreSame = true;
            var len = node.children.Length;
            for (int i = 1; i < len; i++)
            {
                if ( node.children[i].data.state == node.children[0].data.state )
                    continue;

                allChildrenAreSame = false;
                break;
            }

            if ( allChildrenAreSame )
            {
                node.data.state = node.children[0].data.state;
                node.children = null;
                node.isLeaf = true;
            }
        }

        /// <summary>
        /// 快速的包围盒检测
        /// </summary>
        //#attention:快速包围盒检测存在的问题：
        //如果是一个旋转物体（如绕三个轴各旋转了45°），其世界空间包围盒将不再是轴对齐的简单缩放版本。正确的包围盒应该是能包含所有旋转后顶点的最小盒子
        //在性能上这种方法更快，但只适用于轴对齐的版本
        private bool QuickCheckIntersecting(Bounds bounds,MeshFilter mesh,GameObject obj)
        {
            var meshBounds = mesh.sharedMesh.bounds;
            meshBounds.center = obj.transform.TransformPoint(meshBounds.center);
            meshBounds.size = Vector3.Scale(meshBounds.size, obj.transform.lossyScale);

            return bounds.Intersects(meshBounds);
        }

        /// <summary>
        /// 快速包围盒检测
        /// </summary>
        //计算一个新的能包含所有旋转后顶点的轴对齐包围盒，性能比快速检查要差，但最精确
        private bool IsIntersecting(Bounds bounds, MeshFilter mesh, GameObject obj)
        {
            var     localBounds = mesh.sharedMesh.bounds;
            var     worldBounds = new Bounds();
            Vector3[] points    = new Vector3[8];
            points [0]          = localBounds.center + new Vector3(-localBounds.extents.x, -localBounds.extents.y, -localBounds.extents.z);
            points [1]          = localBounds.center + new Vector3(-localBounds.extents.x, -localBounds.extents.y, localBounds.extents.z);
            points [2]          = localBounds.center + new Vector3(-localBounds.extents.x, localBounds.extents.y, -localBounds.extents.z);
            points [3]          = localBounds.center + new Vector3(-localBounds.extents.x, localBounds.extents.y, localBounds.extents.z);
            points [4]          = localBounds.center + new Vector3(localBounds.extents.x, -localBounds.extents.y, -localBounds.extents.z);
            points [5]          = localBounds.center + new Vector3(localBounds.extents.x, -localBounds.extents.y, localBounds.extents.z);
            points [6]          = localBounds.center + new Vector3(localBounds.extents.x, localBounds.extents.y, -localBounds.extents.z);
            points [7]          = localBounds.center + new Vector3(localBounds.extents.x, localBounds.extents.y, localBounds.extents.z);
            
            worldBounds.center = obj.transform.TransformPoint(points[0]);
            
            for (int i = 1; i < 8; i++)
                worldBounds.Encapsulate(obj.transform.TransformPoint(points[i]));
            
            return bounds.Intersects(worldBounds);
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
                writer.Write(_voxelSize);
                writer.Write(_rootNode.bounds.center.x);
                writer.Write(_rootNode.bounds.center.y);
                writer.Write(_rootNode.bounds.center.z);
                writer.Write(_rootNode.bounds.size.x);

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
        
            if (node.data.state == VoxelData.VoxelState.Empty)
                return;
        
            var indent = "";
            for (int i = 0; i < depth; i++)
                indent += "  ";
        
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
        
            Debug.Log($"{indent}<color={colorTag}>Node{(string.IsNullOrEmpty(path) ? "" : " " + path)} " +
                      $"[{node.bounds.center.x:F2}, {node.bounds.center.y:F2}, {node.bounds.center.z:F2}] " +
                      $"Size: {node.bounds.size.x:F2} " +
                      $"Status: {node.data.state} " +
                      $"{(node.isLeaf ? "(Leaf)" : "(Branch)")}</color>");
        
            if (!node.isLeaf && node.children != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    string childPosition;
                    switch (i)
                    {
                        case 0: childPosition = "BLF"; 
                            break;

                        case 1: childPosition = "BRF"; 
                            break;

                        case 2: childPosition = "TRF"; 
                            break;

                        case 3: childPosition = "TLF"; 
                            break;

                        case 4: childPosition = "BLB"; 
                            break;

                        case 5: childPosition = "BRB"; 
                            break;

                        case 6: childPosition = "TRB"; 
                            break;

                        case 7: childPosition = "TLB"; 
                            break;

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
        /// 为游戏对象生成体素，广度优先版本
        /// </summary>
        private void ProcessGameObject_BF(MeshFilter mesh, GameObject obj, OctreeNode rootNode, int startDepth, StringBuilder logHelper = null)
        {
            Queue<(OctreeNode node, int depth)> nodeQueue = new Queue<(OctreeNode, int)>();
            nodeQueue.Enqueue((rootNode, startDepth));
            
            while (nodeQueue.Count > 0)
            {
                var (node, depth) = nodeQueue.Dequeue();
                if (depth >= _maxDepth)
                    continue;

                // 为节点分配ID
#if GEN_VOXEL_ID
                node.ID = IDPool.Gen();
#endif
                if (!IsIntersecting(node.bounds, mesh, obj))
                {
                    // Debug.Log($"<color=white>node is not intersecting,id:{node.ID} , center: {node.bounds.center},size: {node.bounds.size}</color>");
                    continue;
                }

                if (node.isLeaf)
                {
                    ( VoxelData.VoxelState state,long duration) intersectionInfo = VoxelIntersectionHelper.IsIntersection(node.bounds, obj, mesh);
                    _maxIntersectionCount++;
                    _intersectionDuration += intersectionInfo.duration;
                    node.data.state = intersectionInfo.state;
                    
                    if (intersectionInfo.state != VoxelData.VoxelState.Empty)
                    {
                        node.Split();
                        if (depth == _maxDepth - 1)
                        {
                            for (var i = 0; i < node.children.Length; i++)
                            {
                                (VoxelData.VoxelState state, long duration) childIntersectionInfo = VoxelIntersectionHelper.IsIntersection(node.children[i].bounds, obj, mesh, node.children[i].ID);
                                _maxIntersectionCount++;
                                _intersectionDuration += childIntersectionInfo.duration;
                                node.children[i].data.state = childIntersectionInfo.state;
                                _leafCount++;
                                if (intersectionInfo.state != VoxelData.VoxelState.Empty)
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
                            continue;
                        }
                        for (var i = 0; i < node.children.Length; i++)
                            nodeQueue.Enqueue((node.children[i], depth + 1));
                    }
                }
                else if (node.children != null)
                {
                    for (int i = 0; i < node.children.Length; i++)
                        nodeQueue.Enqueue((node.children[i], depth + 1));
                }
            }
        }

        /// <summary>
        /// 为游戏对象生成体素，深度优先版本
        /// </summary>
        private void ProcessGameObject_DF(MeshFilter mesh,GameObject obj, OctreeNode node, int depth, StringBuilder logHelper = null)
        {
            if (depth >= _maxDepth)
                return;

#if GEN_VOXEL_ID
            node.ID = IDPool.Gen();
#endif

            if (!IsIntersecting(node.bounds, mesh,obj))
            {
                // Debug.Log($"<color=white>node is not intersecting,id:{node.ID} , center: {node.bounds.center},size: {node.bounds.size}</color>");
                return;
            }

            if (node.isLeaf)
            {
                (VoxelData.VoxelState state, long duration) intersectionInfo = VoxelIntersectionHelper.IsIntersection( node.bounds, obj, mesh );
                node.data.state = intersectionInfo.state;
                
                if (intersectionInfo.state != VoxelData.VoxelState.Empty)
                {
                    node.Split();
                    
                    // 特殊处理最大深度前一层
                    if (depth == _maxDepth - 1)
                    {
                        for (var i = 0; i < node.children.Length; i++)
                        {
                            (VoxelData.VoxelState state, long duration) childIntersectionInfo = VoxelIntersectionHelper.IsIntersection(node.children[i].bounds, obj,mesh, node.children[i].ID);
                            node.children[i].data.state = childIntersectionInfo.state;
                            _leafCount++;
                            if(intersectionInfo.state != VoxelData.VoxelState.Empty)
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
                        return;
                    }
                    for (var i = 0; i < node.children.Length; i++)
                        ProcessGameObject_DF(mesh,obj, node.children[i], depth + 1, logHelper);
                }
            }
            else if (node.children != null)
            {
                for (int i = 0; i < node.children.Length; i++)
                    ProcessGameObject_DF(mesh,obj, node.children[i], depth + 1, logHelper);
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
        private bool _optimizeAfterGenerate = false;
        private Dictionary<int, MeshCloneData> _meshCloneMap = null;
        private long _intersectionDuration = 0;
        private int _maxIntersectionCount = 0;
    }
    
    public struct MeshCloneData
    {
        public Bounds Bounds;
        public Vector3[] Vertices;
        public int[] Triangles;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }
}