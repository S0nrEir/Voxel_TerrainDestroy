using UnityEngine;
using UnityEditor;

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
            
            _horizontalCellSize = EditorGUILayout.IntField("horizontal cell Size:", _horizontalCellSize);
            _verticalCellSize   = EditorGUILayout.IntField("vertical cell Size:"  , _verticalCellSize);
            
            // 确保格子尺寸始终为正值
            if (_horizontalCellSize <= 0) 
                _horizontalCellSize = 1;
            
            if (_verticalCellSize <= 0) 
                _verticalCellSize = 1;
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("generate height field"))
                GenerateHeightField();
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// 生成高度场的方法根据设置的水平和垂直格子尺寸生成高度场
        /// </summary>
        private void GenerateHeightField()
        {
            // _heightField = new HeightField();
            Debug.Log($"generating height field with grid size: {_horizontalCellSize} x {_verticalCellSize}");
        }
        
        /// <summary>
        /// 水平方向上的格子尺寸
        /// </summary>
        private int _horizontalCellSize = 10;
        
        /// <summary>
        /// 垂直方向上的格子尺寸
        /// </summary>
        private int _verticalCellSize   = 10;

        /// <summary>
        /// 高度场
        /// </summary>
        private HeightField _heightField = null;

    }
}
