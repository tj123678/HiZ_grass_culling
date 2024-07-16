using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MaskTest : MonoBehaviour
{
    public LayerMask layer;

    [ContextMenu("SetMask")]
    void SetMask()
    {
        GetComponent<MeshRenderer>().renderingLayerMask = (uint)layer.value;
        Debug.LogError(GetComponent<MeshRenderer>().renderingLayerMask);
    }
    
    [ContextMenu("ShowCamera")]
    void ShowCamera()
    {
        var mask = Camera.main.cullingMask;
       Debug.LogError(mask);
    }
}
