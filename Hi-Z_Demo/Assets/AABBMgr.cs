using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AABBMgr : MonoBehaviour
{
    private MeshRenderer[] _renderers;
    private Texture2D _centerTexture;//保存包围盒center信息
    private Texture2D _sizeTexture;//保存包围盒size信息
    private Color[] _centerInfos;
    private Color[] _sizeInfos;
    private Bounds[] _bounds;
    public int Size { get; private set; }

    // Start is called before the first frame update
    void Start()
    {
        _renderers = FindObjectsOfType<MeshRenderer>();

        //例如 5 -> NextPowerOfTwo -> 8 -> Sqrt -> 2.xxx -> CeilToInt -> 3
        Size = Mathf.CeilToInt(Mathf.Sqrt(Mathf.NextPowerOfTwo(_renderers.Length)));
        Debug.Log($"create aabb texture size:{Size}");
        _centerTexture = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false);
        _centerTexture.filterMode = FilterMode.Point;
        _sizeTexture = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false);
        _sizeTexture.filterMode = FilterMode.Point;
        _centerInfos = new Color[Size * Size];
        _sizeInfos = new Color[Size * Size];
        _bounds = new Bounds[_renderers.Length];
        
        UpdateAABB();
    }

    void UpdateAABB()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                //写入AABB信息
                Bounds aabb = _renderers[i].bounds;
                var center = aabb.center;
                var size = aabb.size;
                _centerInfos[i] = new Color(center.x, center.y, center.z, 1);
                _sizeInfos[i] = new Color(size.x, size.y, size.z, 1);
                _bounds[i] = aabb;
                Debug.Log($"{i} name:{_renderers[i].gameObject.name}");
                LogScreenPos(i, aabb);
            }
            else
            {
                _centerInfos[i]=Color.black;
                _sizeInfos[i]=Color.black;
            }
        }

        for (int i = _renderers.Length; i < _centerInfos.Length; i++)
        {
            _centerInfos[i]=Color.clear;
            _sizeInfos[i]=Color.clear;
        }

        _centerTexture.SetPixels(_centerInfos, 0);
        _centerTexture.Apply();
        _sizeTexture.SetPixels(_sizeInfos, 0);
        _sizeTexture.Apply();
    }


    public (Texture2D,Texture2D,Bounds[]) GetAABBInfo()
    {
        return (_centerTexture, _sizeTexture, _bounds);
    }

    public void UpdateRender(uint[] isCulls)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].renderingLayerMask = isCulls[i] > 0 ? 1u : 0u;
        }
    }
    
    public void UpdateRender(bool[] isCulls)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].gameObject.SetActive(!isCulls[i]);
            // _renderers[i].renderingLayerMask = renderStates[i] ? 1u : 0u;
        }
    }

    public void ShowAll()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].gameObject.SetActive(true);
        }
    }
    
    private void LogScreenPos(int index,Bounds bounds)
    {
        var center = bounds.center;
        var size = bounds.size;
        var posMax = Camera.main.WorldToViewportPoint(bounds.max);//center + size / 2f);
        var posMin = Camera.main.WorldToViewportPoint(bounds.min);//center - size / 2f);
        
        
        // 使用String.Format保留三位小数
        string posMaxString = String.Format("({0:F3},{1:F3},{2:F3})",posMax.x,1-posMax.y,posMax.z);
        string posMinString = String.Format("({0:F3},{1:F3},{2:F3})", posMin.x, 1 - posMin.y, posMin.z);

        var distance = Vector3.Distance(bounds.center, Camera.main.transform.position) - bounds.size.z / 2;
        //DX平台
        var dxDepth = ((Camera.main.farClipPlane - distance) * Camera.main.nearClipPlane) / ((Camera.main.farClipPlane - Camera.main.nearClipPlane) * distance);
        //OpenGL 平台
        distance *= -1;
        var openGlDepth = ((Camera.main.nearClipPlane + distance) * Camera.main.farClipPlane) / ((Camera.main.farClipPlane - Camera.main.nearClipPlane) * distance);

        Debug.Log($"screent index:{index} posMax:{posMaxString}  posMin:{posMinString} DX_depth:{dxDepth} openGl_depth:{openGlDepth} ");
    }

    public void UpdateInfo() {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                //写入AABB信息
                Bounds aabb = _renderers[i].bounds;
                var center = aabb.center;
                var size = aabb.size;
                _centerInfos[i] = new Color(center.x, center.y, center.z, 1);
                _sizeInfos[i] = new Color(size.x, size.y, size.z, 1);
                _bounds[i] = aabb;
            }
        }
    }
}
