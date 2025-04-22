using System.Collections.Generic;
using Editor;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

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
        
        private void OnGUI()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("height field grid settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUI.BeginChangeCheck();
            
            _xCellSize = EditorGUILayout.FloatField("x cell Size:", _xCellSize);
            _zCellSize = EditorGUILayout.FloatField("z cell Size:"  , _zCellSize);
            _showVoxel = EditorGUILayout.Toggle("Show voxel", _showVoxel);
            
            if (_xCellSize <= 0) 
                _xCellSize = 1;
            
            if (_zCellSize <= 0) 
                _zCellSize = 1;
            
            EditorGUILayout.Space();

            if (GUILayout.Button("generate height field"))
            {
                if (_pool is null)
                    _pool = new SpanPool();
                
                GenerateHeightField();
                if (_showVoxel)
                    Visualized(_heightField);
            }
            
            EditorGUILayout.EndVertical();
        }

        private static void Visualized(HeightField heightField)
        {
            if (heightField is null)
                return;

            var voxelObj = AssetDatabase.LoadAssetAtPath<GameObject>(_voxelInstancePath);
            if (!voxelObj)
                return;

            var container = GameObject.Find("HeightFieldVisualizetion");
            if (container != null)
                Object.DestroyImmediate(container);

            container = new GameObject("HeightFieldVisualizetion");
            var spans = heightField.Span;

            HeightFieldSpan currSpan = null;
            for (var x = 0; x < heightField.Width; x++)
            {
                for (var z = 0; x < heightField.Height; z++)
                {
                    currSpan = heightField.Span[x, z];
                    while (currSpan != null)
                    {
                        var worldX = heightField.Min.x + x * heightField.CellSize;
                        var worldZ = heightField.Min.z + z * heightField.CellSize;

                        var spanMinY = heightField.Min.y + currSpan._smin * heightField.VerticalCellSize;
                        var spanMaxY = heightField.Min.y + currSpan._smax * heightField.VerticalCellSize;
                        var spanHeight = spanMaxY - spanMinY;
                        
                        var spanObj = PrefabUtility.InstantiatePrefab(voxelObj) as GameObject;
                        if (spanObj != null)
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
        /// 生成高度场的方法根据设置的水平和垂直格子尺寸生成高度场
        /// </summary>
        private void GenerateHeightField()
        {
            // _heightField = new HeightField();
            Debug.Log($"generating height field with grid size: {_xCellSize} x {_zCellSize}");
            var allMeshs = FindObjectsOfType<MeshFilter>();
            var sceneBounds = CalculateSceneBounds();
            _heightField = new HeightField
                (
                    sceneBounds.min,
                    sceneBounds.max,
                    _xCellSize,
                    _zCellSize
                );
            
            var inverseCellSize = 1f / _heightField.CellSize;
            var inverseVSize = 1f / _heightField.VerticalCellSize;
            
            (Vector3 vert0, Vector3 vert1, Vector3 vert2) worldPosition;
            Mesh tempMesh   = null;
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
                    worldPosition = Local2World(verts[triangles[i]], verts[triangles[i + 1]], verts[triangles[i + 2]], local2WorldMatrix);
                    RasterizeTriangle(worldPosition.vert0, worldPosition.vert1, worldPosition.vert2, _heightField, inverseCellSize, inverseVSize);
                }
            }
        }
        
        /// <summary>
        /// 栅格化三角形
        /// </summary>
        private static void RasterizeTriangle
            (
                Vector3 vert0,
                Vector3 vert1,
                Vector3 vert2,
                HeightField heightField,
                float inverseCellSize,
                float inverseVCellSize
            )
        {
            var triBounds = new Bounds(vert0,Vector3.zero);
            triBounds.Encapsulate(vert1);
            triBounds.Encapsulate(vert2);
            if (!OverlapBounds(triBounds.min, triBounds.max, heightField.Min, heightField.Max))
            {
                Debug.Log($"<color=orange>not overlap,</color>");
                return;
            }
            float by = heightField.Max.y - heightField.Min.y;
            var z0 = (int)((triBounds.min.z - heightField.Min.z) * inverseCellSize);
            var z1 = (int)((triBounds.max.z - heightField.Min.z) * inverseCellSize);

            z0 = Mathf.Clamp(z0, -1, heightField.Height - 1);
            z1 = Mathf.Clamp(z1, 0, heightField.Height - 1);
            
            //attention:这里顶点坐标的数据组织方式可以再考虑下，最好可以按照原来的版本只需要基于buf来操作就可以
            List<Vector3> buf   = new List<Vector3>(7 * 4);
            List<Vector3> in_   = buf;
            List<Vector3> inRow = new List<Vector3>(buf.Count / 2 + 1);
            List<Vector3> p1    = new List<Vector3>(buf.Count / 2 + 1);
            List<Vector3> p2    = new List<Vector3>(buf.Count / 2 + 1);

            // buf[0] = vert0;
            // buf[1] = vert1;
            // buf[2] = vert2;
            buf.Add(vert0);
            buf.Add(vert1);
            buf.Add(vert2);
            
            int nvRow;
            int nvIn = 3;

            for (var z = z0; z <= z1; z++)
            {
                var cellZ = heightField.Min.z + z * heightField.CellSize;
                DividePoly(buf:buf, outVerts1:inRow, outVerts2:p1, axisOffset:cellZ, axis:AxisTypeEnum.Z);
                (in_,p1) = (p1,in_);

                // if (inRow.Count < 3)
                //     continue;
                if (inRow.Count == 0)
                    continue;
                
                if (z < 0)
                    continue;
                
                float minX = inRow[0].x;
                float maxX = inRow[0].x;
                for (nvRow = 1; nvRow < inRow.Count; nvRow++)
                {
                    minX = Mathf.Min(minX, inRow[nvRow].x);
                    maxX = Mathf.Max(maxX, inRow[nvRow].x);
                }
                
                int x0 = (int)((minX - heightField.Min.x) * inverseCellSize);
                int x1 = (int)((maxX - heightField.Min.x) * inverseCellSize);
                if(x1 < 0 || x0 >= heightField.Width)
                    continue;
                
                x0 = Mathf.Clamp(x0, -1, heightField.Width - 1);
                x1 = Mathf.Clamp(x1, 0, heightField.Width - 1);

                int nv;
                int nv2 = inRow.Count;

                for (var x = x0; x <= x1; x++)
                {
                    float cx = heightField.Min.x + (float)x * heightField.CellSize;
                    DividePoly(buf:inRow,outVerts1:p1,outVerts2:p2,cx + heightField.CellSize,AxisTypeEnum.X);
                    (inRow,p2) = (p2,inRow);
                    
                    // if (inRow.Count < 3)
                    //     continue;
                    
                    if (p1.Count == 0)
                        continue;
                    
                    if (x < 0)
                        continue;
                    
                    float spanMin = p1[0].y;
                    float spanMax = p1[0].y;
                    for (var vert = 1; vert < p1.Count; vert++)
                    {
                        spanMin = Mathf.Min(spanMin, p1[vert].y);
                        spanMax = Mathf.Max(spanMax, p1[vert].y);
                    }
                    
                    spanMin -= heightField.Min.y;
                    spanMax -= heightField.Min.y;
                    
                    if(spanMax < 0f)
                        continue;

                    if (spanMin > by)
                        continue;

                    if (spanMin < 0f)
                        spanMin = 0;
                    
                    if(spanMax > by)
                        spanMax = by;

                    ushort spanMinCellIndex = (ushort)Mathf.Clamp(Mathf.FloorToInt(spanMin * inverseVCellSize), 0, RC_SPAN_MAX_HEIGHT);
                    ushort spanMaxCellIndex = (ushort)Mathf.Clamp(Mathf.CeilToInt(spanMax * inverseVCellSize), (int)spanMinCellIndex + 1, RC_SPAN_MAX_HEIGHT);

                    //#todo:添加flagMergeThrehold相关逻辑和参数
                    if (!AddSpan(heightField,x,z,spanMinCellIndex,spanMaxCellIndex,-1))
                    {
                        Debug.Log($"<color=orange>add span failed</color>");
                        return;
                    }
                }
            }
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
                    currentSpan = newSpan;
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
                    //#attention:对象池分配span
                    // currentSpan = null;
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
        private static void DividePoly
            (
                List<Vector3> buf,
                List<Vector3> outVerts1,
                List<Vector3> outVerts2,
                float axisOffset,
                AxisTypeEnum axis)
        {
            Debug.Assert(buf.Count <= 4);
            var inVertAxisDelta = new float[4];
            for (var inVert = 0; inVert < buf.Count; inVert++)
            {
                var axisValue = axis == AxisTypeEnum.X ? buf[inVert].x : axis == AxisTypeEnum.Y ? buf[inVert].y : buf[inVert].z;
                inVertAxisDelta[inVert] = axisOffset - axisValue;
            }

            var poly1Vert = 0;
            var poly2Vert = 0;
            
            for(int inVertA = 0, inVertB = buf.Count - 1; inVertA < buf.Count; inVertB = inVertA,inVertA++)
            {
                var sameSide = (inVertAxisDelta[inVertA] >= 0) == (inVertAxisDelta[inVertB] >= 0);
                if (!sameSide)
                {
                    float s = inVertAxisDelta[inVertB] / (inVertAxisDelta[inVertB] - inVertAxisDelta[inVertA]);
                    // var intersection = buf[inVertA] + (buf[inVertA] - buf[inVertB]) * s;
                    var intersection = buf[inVertB] + (buf[inVertA] - buf[inVertB]) * s;
                    outVerts1.Add(intersection);
                    outVerts2.Add(intersection);
                    poly1Vert++;
                    poly2Vert++;
                    if (inVertAxisDelta[inVertA] > 0)
                    {
                        outVerts1.Add(buf[inVertA]);
                        poly1Vert++;
                    }
                    else if (inVertAxisDelta[inVertA] < 0)
                    {
                        outVerts2.Add(buf[inVertA]);
                        poly2Vert++;
                    }
                }
                else
                {
                    if (inVertAxisDelta[inVertA] >= 0)
                    {
                        outVerts1.Add(buf[inVertA]);
                        poly1Vert++;
                        if (inVertAxisDelta[inVertA] != 0)
                            continue;
                    }
                    
                    outVerts2.Add(buf[inVertA]);
                    poly2Vert++;
                }
            }
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

            return bounds;
        }

        private static SpanPool _pool = null;
        
        /// <summary>
        /// 水平方向上的格子尺寸
        /// </summary>
        private float _xCellSize = 10;
        
        /// <summary>
        /// 垂直方向上的格子尺寸
        /// </summary>
        private float _zCellSize   = 10;

        /// <summary>
        /// 高度场
        /// </summary>
        private HeightField _heightField = null;
        
        private const int RC_SPAN_HEIGHT_BITS = 13;
        private const int RC_SPAN_MAX_HEIGHT = (1 << RC_SPAN_HEIGHT_BITS) - 1;
        private bool _showVoxel = false;
        private const string _voxelInstancePath = @"Assets/Prefab/voxel_instance.prefab";
    }

    public enum AxisTypeEnum : byte
    {
        X = 0,
        Y = 1,
        Z = 2
    }
}
