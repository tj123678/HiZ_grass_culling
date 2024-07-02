using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HizTest : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
             Log();
        }
    }

    private void Log()
    {
        var bounds = GetComponent<MeshRenderer>().bounds;
        var distance = Vector3.Distance(bounds.center, Camera.main.transform.position) - bounds.size.z / 2;
        //DX平台
        var dxDepth = ((Camera.main.farClipPlane - distance) * Camera.main.nearClipPlane) / ((Camera.main.farClipPlane - Camera.main.nearClipPlane) * distance);
        //OpenGL 平台
        distance *= -1;
        var openGlDepth = ((Camera.main.nearClipPlane + distance) * Camera.main.farClipPlane) / ((Camera.main.farClipPlane - Camera.main.nearClipPlane) * distance);

        Debug.LogError($"screent index:{gameObject.name} DX_depth:{dxDepth} openGl_depth:{openGlDepth} ");
    }
}
