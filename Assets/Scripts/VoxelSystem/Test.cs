using UnityEngine;
using Voxel;

public class VoxelTest : MonoBehaviour
{  
    public VoxelSystem voxelSystem;  

    void Update()  
    {  
        // 示例：检测鼠标点击位置是否有体素  
        if (Input.GetMouseButtonDown(0))  
        {  
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);  
            RaycastHit hit;  
            
            if (Physics.Raycast(ray, out hit))  
            {  
                var voxelState = voxelSystem.IsPositionOccupied(hit.point);  
                Debug.Log($"Position {hit.point} is {voxelState.ToString() }");  
            }  
        }  
    }  
}