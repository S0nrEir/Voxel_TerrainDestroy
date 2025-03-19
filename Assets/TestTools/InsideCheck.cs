#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public class InsideCheck
    {
        [MenuItem("TestTools/点与三角形相交检查")]
        public static void InsideCheck_()
        {
            var ray_1 = new Ray(new Vector3(33.03f, 11.29f, -81.74f), Vector3.right);
            var ray_2 = new Ray(new Vector3(33.09f, 11.29f, -81.74f), Vector3.right);

            Debug.Log($"ray_1: {_InsideCheck_(ray_1, new Vector3(33.03f, 11.29f, -81.74f), new Vector3(33.03f, 11.29f, -81.74f), new Vector3(33.03f, 11.29f, -81.74f))}");
            Debug.Log($"ray_2: {_InsideCheck_(ray_2, new Vector3(33.03f, 11.29f, -81.74f), new Vector3(33.03f, 11.29f, -81.74f), new Vector3(33.03f, 11.29f, -81.74f))}");
            
        }

        private static bool _InsideCheck_(Ray ray,Vector3 v0,Vector3 v1,Vector3 v2)
        {
            return VoxelIntersectionHelper.RayTriangleIntersection(ray, v0, v1, v2);
        }
    }

}


#endif