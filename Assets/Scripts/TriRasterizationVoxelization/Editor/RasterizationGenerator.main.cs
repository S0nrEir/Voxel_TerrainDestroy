using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace TriRasterizationVoxelization.Editor
{
    /// <summary>
    /// 栅格化生成器编辑器窗口类
    /// 用于设置高度场的水平和垂直方向上的格子尺寸
    /// </summary>
    public class RasterizationGeneratorEditor : EditorWindow
    {
        [MenuItem("Tools/Rasterization/Height Field Generator")]
        public static void ShowWindow()
        {
            GetWindow<RasterizationGeneratorEditor>("height field generator");
        }

        private void OnSceneGUI()
        {

        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_heightField != null)
                DrawHeightFieldBounds(_heightField);
        }

        private void DrawHeightFieldBounds(HeightField heightField)
        {
            Color originalColor = Handles.color;
            Matrix4x4 originalMatrix = Handles.matrix;

            Handles.color = new Color(0.0f, 1.0f, 0.5f, 1.0f);

            Vector3 min = heightField.Min;
            Vector3 max = new Vector3(
                heightField.Min.x + heightField.Width * heightField.CellSize,
                heightField.Max.y,
                heightField.Min.z + heightField.Height * heightField.CellSize
            );

            Vector3 p1 = new Vector3(min.x, min.y, min.z);
            Vector3 p2 = new Vector3(max.x, min.y, min.z);
            Vector3 p3 = new Vector3(max.x, min.y, max.z);
            Vector3 p4 = new Vector3(min.x, min.y, max.z);

            Handles.DrawLine(p1, p2);
            Handles.DrawLine(p2, p3);
            Handles.DrawLine(p3, p4);
            Handles.DrawLine(p4, p1);

            Vector3 p5 = new Vector3(min.x, max.y, min.z);
            Vector3 p6 = new Vector3(max.x, max.y, min.z);
            Vector3 p7 = new Vector3(max.x, max.y, max.z);
            Vector3 p8 = new Vector3(min.x, max.y, max.z);

            Handles.DrawLine(p5, p6);
            Handles.DrawLine(p6, p7);
            Handles.DrawLine(p7, p8);
            Handles.DrawLine(p8, p5);
            Handles.DrawLine(p1, p5);
            Handles.DrawLine(p2, p6);
            Handles.DrawLine(p3, p7);
            Handles.DrawLine(p4, p8);

            Handles.color = originalColor;
            Handles.matrix = originalMatrix;

            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("height field grid settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            _xCellSize        = EditorGUILayout.FloatField("xz cell size:"      , _xCellSize);
            _spanSize         = EditorGUILayout.FloatField("vertical span size:", _spanSize);
            _showVoxel        = EditorGUILayout.Toggle("show voxel"             , _showVoxel);
            _parallelVoxelize = EditorGUILayout.Toggle("parallel voxelize"      , _parallelVoxelize);

            if (_xCellSize <= 0)
                _xCellSize = 1;

            if (_spanSize <= 0)
                _spanSize = 1;

            EditorGUILayout.Space();

            if (GUILayout.Button("generate height field"))
            {
                if (_pool is null)
                    _pool = new SpanPool();

                if (_xCellSize <= 0 || _spanSize <= 0)
                {
                    Debug.LogError(("_xCellSize <= 0 || _zCellSize <= 0"));
                    return;
                }

                Stopwatch watch = new Stopwatch();
                watch.Start();
                
                if(_parallelVoxelize)
                    GenerateHeightFieldParallel();
                else
                    GenerateHeightField();
                
                watch.Stop();
                Debug.Log($"<color=white>elapsed seconds : {(float)watch.ElapsedMilliseconds / (float)1000}</color>");
                
                if (_showVoxel)
                    Visualize(_heightField);
                
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("clear voxel data"))
            {
                _heightField?.Clear();
                ClearHeightFieldVisualizetion();
            }

            EditorGUILayout.EndVertical();
        }



        private static void VisualizeSpan(HeightFieldSpan span, HeightField heightField, int x, int z)
        {
            var voxelObj = AssetDatabase.LoadAssetAtPath<GameObject>(_voxelInstancePath);
            if (!voxelObj)
            {
                Debug.LogError($"can not load voxel prefab : {_voxelInstancePath}");
                return;
            }

            var container = GameObject.Find("HeightFieldVisualization");
            if (container != null)
                Object.DestroyImmediate(container);

            container = new GameObject("HeightFieldVisualization");

            var worldX = heightField.Min.x + x * heightField.CellSize;
            var worldZ = heightField.Min.z + z * heightField.CellSize;

            var spanMinY = heightField.Min.y + span._smin * heightField.VerticalCellSize;
            var spanMaxY = heightField.Min.y + span._smax * heightField.VerticalCellSize;
            var spanHeight = Mathf.Max(0.01f, spanMaxY - spanMinY);
            var spanObj = PrefabUtility.InstantiatePrefab(voxelObj) as GameObject;

            if (spanObj)
            {
                spanObj.name = $"X{x}_Z{z}_Y{span._smin}-{span._smax}";
                spanObj.transform.SetParent(container.transform);
                spanObj.transform.position = new Vector3(
                    worldX + heightField.CellSize * 0.5f,
                    spanMinY + spanHeight * 0.5f,
                    worldZ + heightField.CellSize * 0.5f
                );

                spanObj.transform.localScale = new Vector3(
                    heightField.CellSize,
                    spanHeight,
                    heightField.CellSize
                );
            }
        }

        private static void ClearHeightFieldVisualizetion()
        {
            var container = GameObject.Find("HeightFieldVisualization");
            if (container == null)
                return;

            DestroyImmediate(container);
        }

        private static void Visualize(HeightField heightField)
        {
            if (heightField is null || heightField.Span is null)
            {
                Debug.LogWarning("invalid height field data");
                return;
            }

            var voxelObj = AssetDatabase.LoadAssetAtPath<GameObject>(_voxelInstancePath);
            if (!voxelObj)
            {
                Debug.LogError($"can not load voxel prefab : {_voxelInstancePath}");
                return;
            }

            var container = GameObject.Find("HeightFieldVisualization");
            if (container != null)
                Object.DestroyImmediate(container);

            container = new GameObject("HeightFieldVisualization");


            for (var x = 0; x < heightField.Width; x++)
            {
                for (var z = 0; z < heightField.Height; z++)
                {
                    var currSpan = heightField.Span[x, z];
                    while (currSpan != null)
                    {
                        var worldX = heightField.Min.x + x * heightField.CellSize;
                        var worldZ = heightField.Min.z + z * heightField.CellSize;

                        var spanMinY = heightField.Min.y + currSpan._smin * heightField.VerticalCellSize;
                        var spanMaxY = heightField.Min.y + currSpan._smax * heightField.VerticalCellSize;
                        var spanHeight = Mathf.Max(0.01f, spanMaxY - spanMinY);

                        var spanObj = PrefabUtility.InstantiatePrefab(voxelObj) as GameObject;
                        if (spanObj)
                        {
                            spanObj.name = $"X{x}_Z{z}_Y{currSpan._smin}-{currSpan._smax}";
                            spanObj.transform.SetParent(container.transform);
                            spanObj.transform.position = new Vector3(
                                worldX + heightField.CellSize * 0.5f,
                                spanMinY + spanHeight * 0.5f,
                                worldZ + heightField.CellSize * 0.5f
                            );

                            spanObj.transform.localScale = new Vector3(
                                heightField.CellSize,
                                spanHeight,
                                heightField.CellSize
                            );
                        }

                        currSpan = currSpan._pNext;
                    }
                }
            }
        }

        /// <summary>
        /// 多线程并行生成高度场
        /// </summary>
        public void GenerateHeightFieldParallel()
        {
            var allMeshes = FindObjectsOfType<MeshFilter>();
            var sceneBounds = CalculateSceneBounds();
            _heightField = new HeightField(sceneBounds.min, sceneBounds.max, _xCellSize, _spanSize);

            var inverseCellSize = 1f / _heightField.CellSize;
            var inverseVSize = 1f / _heightField.VerticalCellSize;
    
            List<(Vector3, Vector3, Vector3)> trianglesList = new List<(Vector3, Vector3, Vector3)>();
            foreach (var mesh in allMeshes)
            {
                Mesh tempMesh = mesh.sharedMesh;
                Vector3[] verts = tempMesh.vertices;
                int[] tris = tempMesh.triangles;
                Matrix4x4 local2WorldMatrix = mesh.transform.localToWorldMatrix;

                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 v0 = local2WorldMatrix.MultiplyPoint3x4(verts[tris[i]]);
                    Vector3 v1 = local2WorldMatrix.MultiplyPoint3x4(verts[tris[i + 1]]);
                    Vector3 v2 = local2WorldMatrix.MultiplyPoint3x4(verts[tris[i + 2]]);
                    trianglesList.Add((v0, v1, v2));
                }
            }

            // 使用线程安全的字典来存储生成的Span数据
            var spanContainer = new ConcurrentDictionary<(int, int), ConcurrentBag<(ushort, ushort)>>();
    
            // 分批次并行处理三角形
            int batchSize = 8;
            int batchCount = (int)Math.Ceiling((double)trianglesList.Count / batchSize);
    
            System.Threading.Tasks.Parallel.For(0, batchCount, (batchIndex) =>
            {
                int startIndex = batchIndex * batchSize;
                int endIndex = Math.Min(startIndex + batchSize, trianglesList.Count);
        
                for (int i = startIndex; i < endIndex; i++)
                {
                    var triangle = trianglesList[i];
                    ProcessTriangleParallel(triangle.Item1, triangle.Item2, triangle.Item3, 
                        _heightField, inverseCellSize, inverseVSize, spanContainer);
                }
            });

            MergeSpansIntoHeightField(spanContainer, _heightField);
            Debug.Log($" finished , triangle count : {trianglesList.Count} ");
        }
        
        // 并行处理单个三角形
        private void ProcessTriangleParallel(
            Vector3 v0, 
            Vector3 v1, 
            Vector3 v2,
            HeightField heightfield, 
            float inverseCellSize, 
            float inverseCellHeight,
            ConcurrentDictionary<(int, int), ConcurrentBag<(ushort, ushort)>> spanContainer)
        {
            Vector3 triBBMin = Vector3.Min(Vector3.Min(v0, v1), v2);
            Vector3 triBBMax = Vector3.Max(Vector3.Max(v0, v1), v2);

            if (!OverlapBounds(triBBMin, triBBMax, heightfield.Min, heightfield.Max))
                return;

            int w = heightfield.Width;
            int h = heightfield.Height;
            float by = heightfield.Max.y - heightfield.Min.y;

            int z0 = (int)((triBBMin.z - heightfield.Min.z) * inverseCellSize);
            int z1 = (int)((triBBMax.z - heightfield.Min.z) * inverseCellSize);

            z0 = Mathf.Clamp(z0, -1, h - 1);
            z1 = Mathf.Clamp(z1, 0, h - 1);

            List<Vector3> inVerts = new List<Vector3>(7);
            List<Vector3> inRowVerts = new List<Vector3>(7);
            List<Vector3> p1Verts = new List<Vector3>(7);
            List<Vector3> p2Verts = new List<Vector3>(7);

            inVerts.Add(v0);
            inVerts.Add(v1);
            inVerts.Add(v2);
            int nvRow;
            int nvIn = 3;

            for (int z = z0; z <= z1; z++)
            {
                float cellZ = heightfield.Min.z + (float)z * heightfield.CellSize;

                DividePoly(inVerts, inRowVerts, out nvRow, p1Verts, out nvIn, cellZ + heightfield.CellSize, AxisTypeEnum.Z);

                (inVerts, p1Verts) = (p1Verts, inVerts);

                if (nvRow < 3 || z < 0)
                    continue;

                float minX = inRowVerts[0].x;
                float maxX = inRowVerts[0].x;
                for (int vert = 1; vert < nvRow; vert++)
                {
                    minX = Mathf.Min(minX, inRowVerts[vert].x);
                    maxX = Mathf.Max(maxX, inRowVerts[vert].x);
                }

                int x0 = (int)((minX - heightfield.Min.x) * inverseCellSize);
                int x1 = (int)((maxX - heightfield.Min.x) * inverseCellSize);

                if (x1 < 0 || x0 >= w)
                    continue;

                x0 = Mathf.Clamp(x0, -1, w - 1);
                x1 = Mathf.Clamp(x1, 0, w - 1);

                int nv;
                int nv2 = nvRow;

                for (int x = x0; x <= x1; x++)
                {
                    float cx = heightfield.Min.x + (float)x * heightfield.CellSize;

                    DividePoly(inRowVerts, p1Verts, out nv, p2Verts, out nv2, cx + heightfield.CellSize, AxisTypeEnum.X);

                    (inRowVerts, p2Verts) = (p2Verts, inRowVerts);

                    if (nv < 3 || x < 0)
                        continue;

                    float spanMin = p1Verts[0].y;
                    float spanMax = p1Verts[0].y;

                    for (int vert = 1; vert < nv; vert++)
                    {
                        spanMin = Mathf.Min(spanMin, p1Verts[vert].y);
                        spanMax = Mathf.Max(spanMax, p1Verts[vert].y);
                    }

                    spanMin -= heightfield.Min.y;
                    spanMax -= heightfield.Min.y;

                    if (spanMax < 0f || spanMin > by || spanMin < 0f || spanMax > by)
                        continue;

                    spanMin = Mathf.Max(0.0f, spanMin);
                    spanMax = Mathf.Min(by, spanMax);

                    ushort spanMinCellIndex = (ushort)Mathf.Clamp((int)Mathf.Floor(spanMin * inverseCellHeight), 0, RC_SPAN_MAX_HEIGHT);
                    ushort spanMaxCellIndex = (ushort)Mathf.Clamp((int)Mathf.Ceil(spanMax * inverseCellHeight), (int)spanMinCellIndex + 1, RC_SPAN_MAX_HEIGHT);
                    
                    // 添加到并发字典中
                    var key = (x, z);
                    if (!spanContainer.TryGetValue(key, out var spans))
                    {
                        spans = new ConcurrentBag<(ushort, ushort)>();
                        spanContainer.TryAdd(key, spans);
                    }
                    
                    spans.Add((spanMinCellIndex, spanMaxCellIndex));
                }
            }
        }
        
        private void MergeSpansIntoHeightField(
            ConcurrentDictionary<(int, int), ConcurrentBag<(ushort, ushort)>> spanContainer, 
            HeightField heightField)
        {
            foreach (var kvp in spanContainer)
            {
                int x = kvp.Key.Item1;
                int z = kvp.Key.Item2;
        
                if (x < 0 || x >= heightField.Width || z < 0 || z >= heightField.Height)
                    continue;
            
                foreach (var span in kvp.Value)
                {
                    AddSpan(heightField, x, z, span.Item1, span.Item2, -1);
                }
            }
        }
        
        /// <summary>
        /// 生成高度场的方法根据设置的水平和垂直格子尺寸生成高度场
        /// </summary>
        private void GenerateHeightField()
        {
            // _heightField = new HeightField();
            Debug.Log($"generating height field with grid size: {_xCellSize} x {_spanSize}");
            var allMeshs = FindObjectsOfType<MeshFilter>();
            var sceneBounds = CalculateSceneBounds();
            _heightField = new HeightField
            (
                sceneBounds.min,
                sceneBounds.max,
                _xCellSize,
                _spanSize
            );

            var inverseCellSize = 1f / _heightField.CellSize;
            var inverseVSize = 1f / _heightField.VerticalCellSize;

            (Vector3 vert0, Vector3 vert1, Vector3 vert2) worldPosition;
            Mesh tempMesh = null;
            Vector3[] verts = null;
            int[] triangles = null;
            Matrix4x4 local2WorldMatrix;
            foreach (var mesh in allMeshs)
            {
                tempMesh = mesh.sharedMesh;
                verts = tempMesh.vertices;
                triangles = tempMesh.triangles;
                local2WorldMatrix = mesh.transform.localToWorldMatrix;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    worldPosition = Local2World(verts[triangles[i]], verts[triangles[i + 1]], verts[triangles[i + 2]],
                        local2WorldMatrix);
                    RasterizeTriangle(worldPosition.vert0, worldPosition.vert1, worldPosition.vert2, _heightField,
                        inverseCellSize, inverseVSize);
                }
            }
        }
        
        /// <summary>
        /// 光栅化一个三角形到高度场中
        /// </summary>
        /// <returns>操作是否成功</returns>
        private static bool RasterizeTriangle
        (
            Vector3 v0, 
            Vector3 v1, 
            Vector3 v2,
            HeightField heightfield,
            float inverseCellSize, float inverseCellHeight
        )
        {
            // 计算三角形的包围盒
            Vector3 triBBMin = Vector3.Min(Vector3.Min(v0, v1), v2);
            Vector3 triBBMax = Vector3.Max(Vector3.Max(v0, v1), v2);

            if (!OverlapBounds(triBBMin, triBBMax, heightfield.Min, heightfield.Max))
                return true;

            int w = heightfield.Width;
            int h = heightfield.Height;
            float by = heightfield.Max.y - heightfield.Min.y;

            int z0 = (int)((triBBMin.z - heightfield.Min.z) * inverseCellSize);
            int z1 = (int)((triBBMax.z - heightfield.Min.z) * inverseCellSize);

            z0 = Mathf.Clamp(z0, -1, h - 1);
            z1 = Mathf.Clamp(z1, 0, h - 1);

            List<Vector3> inVerts = new List<Vector3>(7);
            List<Vector3> inRowVerts = new List<Vector3>(7);
            List<Vector3> p1Verts = new List<Vector3>(7);
            List<Vector3> p2Verts = new List<Vector3>(7);

            inVerts.Add(v0);
            inVerts.Add(v1);
            inVerts.Add(v2);
            int nvRow;
            int nvIn = 3;

            for (int z = z0; z <= z1; z++)
            {
                float cellZ = heightfield.Min.z + (float)z * heightfield.CellSize;
                
                DividePoly(inVerts, inRowVerts, out nvRow, p1Verts, out nvIn, cellZ + heightfield.CellSize, AxisTypeEnum.Z);
                
                (inVerts, p1Verts) = (p1Verts, inVerts);

                if (nvRow < 3 || z < 0)
                    continue;

                float minX = inRowVerts[0].x;
                float maxX = inRowVerts[0].x;
                for (int vert = 1; vert < nvRow; vert++)
                {
                    minX = Mathf.Min(minX, inRowVerts[vert].x);
                    maxX = Mathf.Max(maxX, inRowVerts[vert].x);
                }

                int x0 = (int)((minX - heightfield.Min.x) * inverseCellSize);
                int x1 = (int)((maxX - heightfield.Min.x) * inverseCellSize);
                
                if (x1 < 0 || x0 >= w)
                    continue;
                
                x0 = Mathf.Clamp(x0, -1, w - 1);
                x1 = Mathf.Clamp(x1, 0, w - 1);

                int nv;
                int nv2 = nvRow;
                
                for (int x = x0; x <= x1; x++)
                {
                    float cx = heightfield.Min.x + (float)x * heightfield.CellSize;
                    
                    DividePoly(inRowVerts, p1Verts, out nv, p2Verts, out nv2, cx + heightfield.CellSize, AxisTypeEnum.X);
                    
                    (inRowVerts, p2Verts) = (p2Verts, inRowVerts);
                    
                    if (nv < 3 || x < 0)
                        continue;
                    
                    float spanMin = p1Verts[0].y;
                    float spanMax = p1Verts[0].y;
                    
                    for (int vert = 1; vert < nv; vert++)
                    {
                        spanMin = Mathf.Min(spanMin, p1Verts[vert].y);
                        spanMax = Mathf.Max(spanMax, p1Verts[vert].y);
                    }
                    
                    spanMin -= heightfield.Min.y;
                    spanMax -= heightfield.Min.y;
                    
                    if (spanMax < 0f || spanMin > by || spanMin < 0f || spanMax > by)
                        continue;
                    
                    spanMin = Mathf.Max(0.0f, spanMin);
                    spanMax = Mathf.Min(by, spanMax);
                    
                    ushort spanMinCellIndex = (ushort)Mathf.Clamp((int)Mathf.Floor(spanMin * inverseCellHeight), 0, RC_SPAN_MAX_HEIGHT);
                    ushort spanMaxCellIndex = (ushort)Mathf.Clamp((int)Mathf.Ceil(spanMax * inverseCellHeight), (int)spanMinCellIndex + 1, RC_SPAN_MAX_HEIGHT);
                    // 添加体素到高度场中
                    if (!AddSpan(heightfield, x, z, spanMinCellIndex, spanMaxCellIndex, -1))
                        return false;
                }
            }

            return true;
        }

        private static bool AddSpan(HeightField heightField,int x,int z,ushort min,ushort max,int flagMergeThreshold)
        {
            HeightFieldSpan newSpan = _pool.Gen();
            newSpan._smin = min;
            newSpan._smax = max;
            newSpan._pNext = null;
            
            HeightFieldSpan prevSpan = null;
            HeightFieldSpan currentSpan = heightField.Span[x, z];
            while (currentSpan != null)
            {
                if (currentSpan._smin > newSpan._smax)
                    break;

                if (currentSpan._smax < newSpan._smin)
                {
                    prevSpan = currentSpan;
                    currentSpan = currentSpan._pNext;
                } 
                else
                {
                    if(currentSpan._smin < newSpan._smin)
                        newSpan._smin = currentSpan._smin;
                    
                    if(currentSpan._smax > newSpan._smax)
                        newSpan._smax = currentSpan._smax;

                    if (Mathf.Abs((int)newSpan._smax - (int)currentSpan._smax) <= flagMergeThreshold)
                        ; //merge

                    HeightFieldSpan next = currentSpan._pNext;
                    _pool.Release(currentSpan);
                    if( prevSpan != null)
                        prevSpan._pNext = next;
                    else
                        heightField.Span[x,z] = next;

                    currentSpan = next;
                }
            }

            if (prevSpan != null)
            {
                newSpan._pNext = prevSpan._pNext;
                prevSpan._pNext = newSpan;
            }
            else
            {
                newSpan._pNext = heightField.Span[x, z];
                heightField.Span[x, z] = newSpan;
                
            }
            return true;
        }

        /// <summary>
        /// 分割多边形，返回分割后和分割前的顶点集合
        /// </summary>
        private static void DividePoly(
            List<Vector3> inVerts,
            List<Vector3> outVerts1,
            out int outVerts1Count,
            List<Vector3> outVerts2,
            out int outVerts2Count,
            float axisOffset,
            AxisTypeEnum axis)
        {
            Debug.Assert(inVerts.Count <= 12);
            
            outVerts1.Clear();
            outVerts2.Clear();

            float[] inVertAxisDelta = new float[inVerts.Count];
            for (int i = 0; i < inVerts.Count; i++)
            {
                var axisValue = axis == AxisTypeEnum.X ? inVerts[i].x : axis == AxisTypeEnum.Y ? inVerts[i].y : inVerts[i].z;
                inVertAxisDelta[i] = axisOffset - axisValue;
            }

            for (int inVertA = 0, inVertB = inVerts.Count - 1; inVertA < inVerts.Count; inVertB = inVertA, inVertA++)
            {
                bool sameSide = (inVertAxisDelta[inVertA] >= 0) == (inVertAxisDelta[inVertB] >= 0);
                if (!sameSide)
                {
                    float s = inVertAxisDelta[inVertB] / (inVertAxisDelta[inVertB] - inVertAxisDelta[inVertA]);
                    Vector3 intersection = Vector3.Lerp(inVerts[inVertB], inVerts[inVertA], s);
                    
                    outVerts1.Add(intersection);
                    outVerts2.Add(intersection);
                    
                    if (inVertAxisDelta[inVertA] > 0)
                        outVerts1.Add(inVerts[inVertA]);
                    else if (inVertAxisDelta[inVertA] < 0)
                        outVerts2.Add(inVerts[inVertA]);
                }
                else
                {
                    if (inVertAxisDelta[inVertA] >= 0)
                    {
                        outVerts1.Add(inVerts[inVertA]);
                        if (inVertAxisDelta[inVertA] != 0)
                            continue;
                    }
                    outVerts2.Add(inVerts[inVertA]);
                }
            }

            outVerts1Count = outVerts1.Count;
            outVerts2Count = outVerts2.Count;
        }

        /// <summary>
        /// 检查包围盒相交
        /// </summary>
        private static bool OverlapBounds(Vector3 aMin,Vector3 aMax,Vector3 bMin,Vector3 bMax)
        {
            return aMin.x <= bMax.x && aMax.x >= bMin.x &&
                   aMin.y <= bMax.y && aMax.y >= bMin.y &&
                   aMin.z <= bMax.z && aMax.z >= bMin.z;
        }

        private (Vector3 vert0, Vector3 vert1, Vector3 vert2) Local2World
        (
            Vector3 vert0,
            Vector3 vert1,
            Vector3 vert2,
            Transform transform 
        )
        {
            return 
                (
                    transform.TransformPoint(vert0), 
                    transform.TransformPoint(vert1), 
                    transform.TransformPoint(vert2)
                );
        }

        /// <summary>
        /// 将顶点由模型空间变换到世界空间
        /// </summary>
        private (Vector3 vert0, Vector3 vert1, Vector3 vert2) Local2World
        (
            in Vector3 vert0, 
            in Vector3 vert1, 
            in Vector3 vert2,
            in Matrix4x4 matrix
        )
        {
            // var world0 = matrix.MultiplyPoint3x4(vert0);
            // var world1 = matrix.MultiplyPoint3x4(vert1);
            // var world2 = matrix.MultiplyPoint3x4(vert2);
            return (matrix.MultiplyPoint3x4(vert0), matrix.MultiplyPoint3x4(vert1), matrix.MultiplyPoint3x4(vert2));
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
                    var meshBounds = TransformBoundsToWorldSpace(meshFilter.sharedMesh.bounds, meshFilter.transform);

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

            return bounds;
        }
        
        private Bounds TransformBoundsToWorldSpace(Bounds localBounds, Transform transform)
        {
            Vector3[] corners = new Vector3[8];
            corners[0] = new Vector3(localBounds.min.x, localBounds.min.y, localBounds.min.z);
            corners[1] = new Vector3(localBounds.min.x, localBounds.min.y, localBounds.max.z);
            corners[2] = new Vector3(localBounds.min.x, localBounds.max.y, localBounds.min.z);
            corners[3] = new Vector3(localBounds.min.x, localBounds.max.y, localBounds.max.z);
            corners[4] = new Vector3(localBounds.max.x, localBounds.min.y, localBounds.min.z);
            corners[5] = new Vector3(localBounds.max.x, localBounds.min.y, localBounds.max.z);
            corners[6] = new Vector3(localBounds.max.x, localBounds.max.y, localBounds.min.z);
            corners[7] = new Vector3(localBounds.max.x, localBounds.max.y, localBounds.max.z);

            for (int i = 0; i < 8; i++)
                corners[i] = transform.TransformPoint(corners[i]);

            // 重新计算包围盒
            Bounds worldBounds = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 8; i++)
                worldBounds.Encapsulate(corners[i]);

            return worldBounds;
        }
        
        private static SpanPool _pool = null;
        
        /// <summary>
        /// 水平方向上的格子尺寸
        /// </summary>
        private float _xCellSize = 10;
        
        /// <summary>
        /// 垂直方向上的格子尺寸
        /// </summary>
        private float _spanSize   = 10;

        /// <summary>
        /// 高度场
        /// </summary>
        private HeightField _heightField = null;
        
        private const int RC_SPAN_HEIGHT_BITS = 13;
        
        /// <summary>
        /// span最大高度
        /// </summary>
        private const int RC_SPAN_MAX_HEIGHT = (1 << RC_SPAN_HEIGHT_BITS) - 1;
        
        /// <summary>
        /// 是否显示体素
        /// </summary>
        private bool _showVoxel = false;
        
        /// <summary>
        /// 体素实例资源路径
        /// </summary>
        private const string _voxelInstancePath = @"Assets/Prefab/voxel_instance.prefab";
        
        /// <summary>
        /// 并行体素化
        /// </summary>
        private bool _parallelVoxelize = false;
    }   
}
